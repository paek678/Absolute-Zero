using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Player.Identity
{
    public sealed class PlayerRegistry : IReadOnlyPlayerRegistry
    {
        readonly Dictionary<ulong, PlayerBinding> _byClientId = new();
        readonly Dictionary<byte, PlayerBinding> _byIndex = new();
        readonly Dictionary<string, PlayerBinding> _byParticipant = new(StringComparer.Ordinal);
        readonly Dictionary<ulong, PlayerBinding> _byObject = new();
        readonly Dictionary<ulong, PlayerBinding> _pending = new();
        readonly List<PlayerBinding> _readyList = new();
        long? _generation;

        public IReadOnlyCollection<PlayerBinding> Players => _readyList;
        public int ReadyCount => _readyList.Count;
        public int PendingCount => _pending.Count;
        public int TotalCount => _pending.Count + _readyList.Count;

        public event Action<PlayerBinding> Registered;
        public event Action<PlayerIdentity> Unregistered;

        public bool TryGetByPlayerIndex(byte index, out PlayerBinding player)
        {
            if (_byIndex.TryGetValue(index, out player) && player.IsValid)
                return true;
            player = null;
            return false;
        }

        public bool TryGetByClientId(ulong clientId, out PlayerBinding player)
        {
            if (_byClientId.TryGetValue(clientId, out player) && player.IsValid)
                return true;
            player = null;
            return false;
        }

        public IEnumerable<PlayerBinding> EnumeratePending() => _pending.Values;

        public bool TryGetByParticipantId(string participantId, out PlayerBinding player)
        {
            if (participantId != null && _byParticipant.TryGetValue(participantId, out player) && player.IsValid)
                return true;
            player = null;
            return false;
        }

        public bool RegisterPending(PlayerBinding binding)
        {
            if (binding == null || !binding.IsValid) return false;
            if (_byObject.TryGetValue(binding.NetworkObjectId, out var existing))
                return ReferenceEquals(existing, binding);
            if (binding.HasIdentity && _generation.HasValue && binding.Identity.Generation != _generation.Value)
                return false;
            _byObject.Add(binding.NetworkObjectId, binding);
            _pending.Add(binding.NetworkObjectId, binding);
            return true;
        }

        public bool TryPromote(PlayerBinding binding, MatchParticipantDescriptor participant, out string error)
        {
            error = null;
            if (binding == null || participant == null || !binding.IsValid
                || !_byObject.TryGetValue(binding.NetworkObjectId, out var tracked) || !ReferenceEquals(tracked, binding))
            { error = "Binding is not the live registered network object."; return false; }
            if (binding.HasIdentity)
            {
                if (!binding.Participant.Equals(participant))
                { error = "A ready binding cannot change participant identity."; return false; }
                if (_byIndex.TryGetValue(participant.Seat, out var ready) && ReferenceEquals(ready, binding))
                    return true;
            }
            if (_generation.HasValue && _generation.Value != participant.Generation)
            { error = "Binding belongs to a stale session generation."; return false; }
            if (participant.ControllerKind == PlayerControllerKind.Human)
            {
                if (!participant.ClientId.HasValue || participant.ClientId.Value != binding.NetworkObject.OwnerClientId
                    || !binding.NetworkObject.IsPlayerObject)
                { error = "A live human must own its real player object and connection."; return false; }
            }
            else if (!binding.NetworkObject.IsOwnedByServer || binding.NetworkObject.IsPlayerObject)
            { error = "A bot must be a server-owned ordinary network object."; return false; }
            if (_byIndex.ContainsKey(participant.Seat) || _byParticipant.ContainsKey(participant.ParticipantId)
                || (participant.ClientId.HasValue && _byClientId.ContainsKey(participant.ClientId.Value)))
            { error = "Seat, participant or human connection is already registered."; return false; }

            _pending.Remove(binding.NetworkObjectId);
            binding.AssignIdentity(participant);
            _generation = participant.Generation;
            if (participant.ClientId.HasValue) _byClientId.Add(participant.ClientId.Value, binding);
            _byIndex.Add(participant.Seat, binding);
            _byParticipant.Add(participant.ParticipantId, binding);
            _readyList.Add(binding);
            Registered?.Invoke(binding);
            return true;
        }

        public bool TryCaptureReadyBindings(IReadOnlyList<MatchParticipantDescriptor> expected,
            out PlayerBinding[] bindings, out string error)
        {
            bindings = null;
            if (!MatchParticipantDescriptor.TryValidateSet(expected, expected?.Count ?? 0, out var bySeat, out error))
                return false;
            if (_readyList.Count != bySeat.Length || _pending.Count != 0)
            { error = "The exact participant set is not ready."; return false; }
            var snapshot = new PlayerBinding[bySeat.Length];
            var objects = new HashSet<ulong>();
            foreach (var participant in bySeat)
            {
                if (!TryGetByPlayerIndex(participant.Seat, out var binding)
                    || !binding.HasIdentity || !binding.Participant.Equals(participant)
                    || !objects.Add(binding.NetworkObjectId)
                    || !_byObject.TryGetValue(binding.NetworkObjectId, out var tracked) || !ReferenceEquals(binding, tracked))
                { error = "A ready seat is missing, stale or mapped to the wrong participant/object."; return false; }
                snapshot[participant.Seat] = binding;
            }
            bindings = snapshot;
            return true;
        }

        public void Unregister(PlayerBinding binding)
        {
            if (binding == null || !_byObject.TryGetValue(binding.NetworkObjectId, out var tracked)
                || !ReferenceEquals(tracked, binding)) return;
            _byObject.Remove(binding.NetworkObjectId);
            _pending.Remove(binding.NetworkObjectId);
            if (!binding.HasIdentity) return;
            var identity = binding.Identity;
            if (!_byIndex.TryGetValue(identity.PlayerIndex, out var existing) || !ReferenceEquals(existing, binding)) return;
            _byIndex.Remove(identity.PlayerIndex);
            _byParticipant.Remove(identity.ParticipantId);
            if (identity.ClientId.HasValue && _byClientId.TryGetValue(identity.ClientId.Value, out var human)
                && ReferenceEquals(human, binding)) _byClientId.Remove(identity.ClientId.Value);
            _readyList.Remove(binding);
            Unregistered?.Invoke(identity);
        }

        public void Clear()
        {
            _pending.Clear();
            _byClientId.Clear();
            _byIndex.Clear();
            _byObject.Clear();
            _byParticipant.Clear();
            _readyList.Clear();
            _generation = null;
            Debug.Log("[PlayerRegistry] Cleared");
        }
    }
}
