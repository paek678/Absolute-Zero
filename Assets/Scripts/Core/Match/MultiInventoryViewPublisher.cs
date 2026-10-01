using System;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using Unity.Netcode;

namespace AbsoluteZero.Core.Match
{
    /// <summary>Coalesces authoritative inventory mutations into one retained view revision.</summary>
    public sealed class MultiInventoryViewPublisher : IDisposable
    {
        sealed class Binding
        {
            public PlayerInventory Inventory;
            public NetworkList<ItemSlotNetData>.OnListChangedDelegate Changed;
        }

        readonly MatchNetworkState _state;
        readonly Binding[] _bindings = new Binding[MultiInventorySnapshotNetData.MaxSeats];
        bool _dirty = true;
        uint _revision;
        uint _match;
        uint _round;
        byte _lastFaulted;

        public MultiInventoryViewPublisher(MatchNetworkState state) => _state = state;
        public void MarkDirty() => _dirty = true;

        public void Flush()
        {
            if (!_state.IsSpawned || !_state.IsServer
                || (GameMode)_state.Config.Value.Mode != GameMode.Multi
                || _state.GhostMatchEpoch.Value == 0 || _state.GhostRoundEpoch.Value == 0)
                return;

            Reconcile();
            byte faulted = (byte)(_state.InitializationError.Value.Length > 0
                || _state.DeathmatchGrant.Value.Stage == DeathmatchGrantStage.Failed ? 1 : 0);
            if (faulted != _lastFaulted) _dirty = true;
            if (!_dirty) return;
            if (_state.DeathmatchGrant.Value.Stage == DeathmatchGrantStage.Pending) return;
            var snapshot = Capture(faulted);
            _state.ServerPublishInventorySnapshot(snapshot);
            _lastFaulted = faulted;
            _dirty = false;
        }

        void Reconcile()
        {
            var registry = MatchCompositionRoot.Instance?.Registry;
            if (registry == null) return;
            for (int seat = 0; seat < _bindings.Length; seat++)
            {
                PlayerInventory inventory = null;
                if (registry.TryGetByPlayerIndex((byte)seat, out var player)
                    && player.State != null && player.State.IsSpawned)
                    inventory = player.State.GetInventory();
                var previous = _bindings[seat]?.Inventory;
                if (ReferenceEquals(previous, inventory)
                    && (inventory == null || inventory.IsSpawned)) continue;
                Unbind(seat);
                if (inventory != null && inventory.SlotStates != null)
                {
                    var binding = new Binding { Inventory = inventory };
                    binding.Changed = _ => MarkDirty();
                    inventory.SlotStates.OnListChanged += binding.Changed;
                    _bindings[seat] = binding;
                }
                _dirty = true;
            }
        }

        MultiInventorySnapshotNetData Capture(byte faulted)
        {
            uint match = _state.GhostMatchEpoch.Value;
            uint round = _state.GhostRoundEpoch.Value;
            if (_match != match || _round != round)
            {
                _match = match;
                _round = round;
                _revision = 0;
            }
            if (_revision == uint.MaxValue)
                throw new InvalidOperationException("Inventory view revision exhausted");
            var view = new MultiInventorySnapshotNetData
            {
                MatchEpoch = match,
                RoundEpoch = round,
                Revision = ++_revision,
                Faulted = faulted
            };
            var grant = _state.DeathmatchGrant.Value;
            if (grant.RoundEpoch == round && grant.Stage == DeathmatchGrantStage.Completed)
                view.CommittedGrantTransaction = grant.TransactionId;

            for (int seat = 0; seat < MultiInventorySnapshotNetData.MaxSeats; seat++)
            {
                var inventory = _bindings[seat]?.Inventory;
                if (inventory == null || !inventory.IsSpawned) inventory = null;
                var slots = inventory != null ? inventory.SlotStates : null;
                int count = slots != null ? slots.Count : 0;
                if (count > MultiInventorySnapshotNetData.MaxSlots)
                    throw new InvalidOperationException($"Seat {seat} inventory exceeds snapshot capacity");
                if (inventory != null)
                {
                    view.SeatPresenceMask |= (byte)(1 << seat);
                    view.SetCount(seat, (byte)count);
                }
                for (int index = 0; index < MultiInventorySnapshotNetData.MaxSlots; index++)
                    view.Slots.Add(index < count ? slots[index] : ItemSlotNetData.Empty);
            }
            return view;
        }

        void Unbind(int seat)
        {
            var binding = _bindings[seat];
            if (binding != null && binding.Inventory != null
                && binding.Inventory.SlotStates != null)
                binding.Inventory.SlotStates.OnListChanged -= binding.Changed;
            _bindings[seat] = null;
        }

        public void Dispose()
        {
            for (int seat = 0; seat < _bindings.Length; seat++) Unbind(seat);
        }
    }
}
