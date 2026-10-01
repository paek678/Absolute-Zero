using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using UnityEngine;

namespace AbsoluteZero.Core.Match
{
    public enum SeatConnectionState : byte
    {
        Connected,
        Disconnected,
        TimedOut,
        Local
    }

    public sealed class MatchRoster : IDisconnectHandler, ISeatStateAccessor, IDisposable
    {
        public struct SeatEntry
        {
            public byte SeatIndex;
            public string ParticipantId;
            public ulong? ClientId;
            public SeatConnectionState State;
            public PlayerControllerKind ControllerKind;
            public long Generation;
        }

        readonly Dictionary<byte, SeatEntry> _seats = new();
        readonly Dictionary<ulong, byte> _clientToSeat = new();
        readonly Dictionary<byte, SeatRuntimeState> _offlineStates = new();
        readonly int _requiredPlayerCount;
        bool _disposed;

        IReadOnlyPlayerRegistry _registry;
        Func<byte, PlayerModifiers> _getModifiers;
        Action<byte, PlayerModifiers> _setModifiers;

        public bool RosterReady { get; private set; }
        public int RequiredPlayerCount => _requiredPlayerCount;
        public IReadOnlyDictionary<byte, SeatEntry> Seats => _seats;

        public event Action<ulong, byte> OnPlayerDisconnectedFromSeat;

        public MatchRoster(int requiredPlayerCount)
        {
            _requiredPlayerCount = requiredPlayerCount;
        }

        public bool Hydrate(IEnumerable<SessionParticipantEntry> participants)
            => Hydrate(participants, 0);

        public bool Hydrate(IEnumerable<SessionParticipantEntry> participants, long generation)
        {
            if (_disposed || participants == null) return false;
            var descriptors = new List<MatchParticipantDescriptor>();
            foreach (var p in participants)
            {
                if (p == null || p.SeatIndex >= 4 || generation < 0) return false;
                try
                {
                    descriptors.Add(new MatchParticipantDescriptor(p.ParticipantId, p.SeatIndex,
                        PlayerControllerKind.Human, p.IsConnected ? p.CurrentClientId : null, generation));
                }
                catch (ArgumentException) { return false; }
            }
            return Hydrate(descriptors);
        }

        public bool Hydrate(IEnumerable<MatchParticipantDescriptor> participants)
        {
            if (_disposed || participants == null || RosterReady) return false;
            var descriptors = new List<MatchParticipantDescriptor>(participants);
            if (!MatchParticipantDescriptor.TryValidateSet(descriptors, _requiredPlayerCount, out var ordered, out _))
                return false;
            _seats.Clear();
            _clientToSeat.Clear();
            foreach (var p in ordered)
            {
                bool isConnected = p.ControllerKind == PlayerControllerKind.Human && p.ClientId.HasValue;
                var entry = new SeatEntry
                {
                    SeatIndex = p.Seat,
                    ParticipantId = p.ParticipantId,
                    ClientId = p.ClientId,
                    ControllerKind = p.ControllerKind,
                    Generation = p.Generation,
                    State = p.ControllerKind == PlayerControllerKind.Bot ? SeatConnectionState.Local
                        : isConnected ? SeatConnectionState.Connected : SeatConnectionState.Disconnected
                };
                _seats.Add(p.Seat, entry);
                if (isConnected)
                    _clientToSeat.Add(p.ClientId.Value, p.Seat);
            }

            RosterReady = true;
            Debug.Log($"[MatchRoster] Hydrated: {_seats.Count} seats");
            return true;
        }

        public void OnPlayerDisconnected(ulong clientId)
        {
            if (_disposed) return;
            if (!_clientToSeat.TryGetValue(clientId, out byte seat)) return;

            var ps = FindPlayerState(seat);
            if (ps != null)
            {
                var captured = new SeatRuntimeState
                {
                    SeatIndex = seat,
                    Temperature = ps.Temperature.Value,
                    CurrentLifeState = ps.CurrentLifeState.Value,
                    FanSpeed = ps.FanSpeed.Value,
                    IsFanActive = ps.IsFanActive.Value,
                    IsFanUpgraded = ps.IsFanUpgraded.Value,
                    IsBasicBlocked = ps.IsBasicBlocked.Value,
                    Modifiers = _getModifiers != null ? _getModifiers(seat) : default,
                    LastDamageSource = DamageSource.None,
                };

                var inv = ps.GetInventory();
                if (inv?.SlotStates != null)
                {
                    var slots = new ItemSlotNetData[inv.SlotStates.Count];
                    for (int i = 0; i < inv.SlotStates.Count; i++)
                        slots[i] = inv.SlotStates[i];
                    captured.InventorySlots = slots;
                }

                CaptureOfflineState(seat, captured);

                var queue = ps.GetActionQueue();
                if (queue != null)
                {
                    queue.Clear();
                    ps.HasSelectedItem.Value = false;
                }
            }

            // Despawn+Destroy via PSM (triggers PlayerState.OnNetworkDespawn → registry.Unregister)
            var psm = Network.PlayerSpawnManager.Instance;
            psm?.OnPlayerDisconnected(clientId);

            var nsc = Session.NetworkSessionCoordinator.Instance;
            nsc?.ParticipantTable?.Unbind(clientId);

            var entry = _seats[seat];
            entry.State = SeatConnectionState.Disconnected;
            entry.ClientId = null;
            _seats[seat] = entry;
            _clientToSeat.Remove(clientId);

            Debug.Log($"[MatchRoster] Seat {seat} disconnected (ClientId={clientId}), state captured={ps != null}");
            OnPlayerDisconnectedFromSeat?.Invoke(clientId, seat);
        }

        public bool BindReconnect(byte seat, ulong newClientId)
        {
            if (_disposed) return false;
            if (!_seats.TryGetValue(seat, out var entry)) return false;
            if (entry.ControllerKind != PlayerControllerKind.Human) return false;

            if (entry.State == SeatConnectionState.TimedOut)
            {
                Debug.LogWarning($"[MatchRoster] Cannot reconnect timed-out seat {seat}");
                return false;
            }

            if (entry.State == SeatConnectionState.Connected)
            {
                Debug.LogWarning($"[MatchRoster] Seat {seat} already connected");
                return false;
            }

            if (_clientToSeat.ContainsKey(newClientId))
            {
                Debug.LogWarning($"[MatchRoster] ClientId {newClientId} already bound to another seat");
                return false;
            }

            entry.ClientId = newClientId;
            entry.State = SeatConnectionState.Connected;
            _seats[seat] = entry;
            _clientToSeat[newClientId] = seat;

            Debug.Log($"[MatchRoster] Seat {seat} reconnected (ClientId={newClientId})");
            return true;
        }

        public void MarkTimedOut(byte seat)
        {
            if (_disposed) return;
            if (!_seats.TryGetValue(seat, out var entry)) return;
            if (entry.ControllerKind != PlayerControllerKind.Human) return;

            if (entry.ClientId.HasValue)
            {
                _clientToSeat.Remove(entry.ClientId.Value);
                entry.ClientId = null;
            }

            entry.State = SeatConnectionState.TimedOut;
            _seats[seat] = entry;
            Debug.Log($"[MatchRoster] Seat {seat} timed out");
        }

        public bool TryGetSeatByClientId(ulong clientId, out byte seat)
        {
            return _clientToSeat.TryGetValue(clientId, out seat);
        }

        public bool TryGetEntry(byte seat, out SeatEntry entry)
        {
            return _seats.TryGetValue(seat, out entry);
        }

        public int ConnectedCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _seats)
                    if (kvp.Value.State == SeatConnectionState.Connected) count++;
                return count;
            }
        }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _seats)
                    if (IsActive(kvp.Key)) count++;
                return count;
            }
        }

        public void SetRegistry(IReadOnlyPlayerRegistry registry) => _registry = registry;
        public void SetModifiersSource(Func<byte, PlayerModifiers> getter, Action<byte, PlayerModifiers> setter = null)
        {
            _getModifiers = getter;
            _setModifiers = setter;
        }

        public void CaptureOfflineState(byte seat, SeatRuntimeState state)
        {
            if (_seats.TryGetValue(seat, out var entry) && entry.ControllerKind == PlayerControllerKind.Bot)
                throw new InvalidOperationException("A local bot cannot use disconnected-human snapshot storage.");
            _offlineStates[seat] = state;
        }

        public void RemoveOfflineState(byte seat) => _offlineStates.Remove(seat);

        public bool TryGetOfflineState(byte seat, out SeatRuntimeState state)
        {
            return _offlineStates.TryGetValue(seat, out state);
        }

        // ─── ISeatStateAccessor ─────────────────────────────────

        static bool IsServerContext => Unity.Netcode.NetworkManager.Singleton != null
                                       && Unity.Netcode.NetworkManager.Singleton.IsServer;

        PlayerState FindPlayerState(byte seat)
        {
            if (_registry == null) return null;
            if (!_registry.TryGetByPlayerIndex(seat, out var binding)) return null;
            if (!_seats.TryGetValue(seat, out var entry) || !binding.HasIdentity
                || binding.Identity.ParticipantId != entry.ParticipantId
                || binding.Identity.Generation != entry.Generation
                || binding.Identity.ControllerKind != entry.ControllerKind
                || binding.Identity.ClientId != entry.ClientId) return null;
            return binding.State;
        }

        public float GetTemperature(byte seat)
        {
            var ps = FindPlayerState(seat);
            if (ps != null) return ps.Temperature.Value;
            return _offlineStates.TryGetValue(seat, out var s) ? s.Temperature : 37f;
        }

        SeatRuntimeState GetOrCreateOffline(byte seat)
        {
            if (_seats.TryGetValue(seat, out var entry) && entry.ControllerKind == PlayerControllerKind.Bot)
                throw new InvalidOperationException("A missing local bot binding cannot become an offline snapshot.");
            if (!_offlineStates.TryGetValue(seat, out var s))
            {
                s = SeatRuntimeState.CreateDefault(seat);
                _offlineStates[seat] = s;
            }
            return s;
        }

        void SetOffline(byte seat, SeatRuntimeState s) => _offlineStates[seat] = s;

        public void SetTemperature(byte seat, float value)
        {
            if (!IsServerContext) return;
            var ps = FindPlayerState(seat);
            if (ps != null) { ps.Temperature.Value = value; return; }
            var s = GetOrCreateOffline(seat); s.Temperature = value; SetOffline(seat, s);
        }

        public LifeState GetLifeState(byte seat)
        {
            var ps = FindPlayerState(seat);
            if (ps != null) return ps.CurrentLifeState.Value;
            return _offlineStates.TryGetValue(seat, out var s) ? s.CurrentLifeState : LifeState.Alive;
        }

        public void SetLifeState(byte seat, LifeState value)
        {
            if (!IsServerContext) return;
            var ps = FindPlayerState(seat);
            if (ps != null) { ps.CurrentLifeState.Value = value; return; }
            var s = GetOrCreateOffline(seat); s.CurrentLifeState = value; SetOffline(seat, s);
        }

        public float GetFanSpeed(byte seat)
        {
            var ps = FindPlayerState(seat);
            if (ps != null) return ps.FanSpeed.Value;
            return _offlineStates.TryGetValue(seat, out var s) ? s.FanSpeed : 1f;
        }

        public void SetFanSpeed(byte seat, float value)
        {
            if (!IsServerContext) return;
            var ps = FindPlayerState(seat);
            if (ps != null) { ps.FanSpeed.Value = value; return; }
            var s = GetOrCreateOffline(seat); s.FanSpeed = value; SetOffline(seat, s);
        }

        public bool GetIsFanActive(byte seat)
        {
            var ps = FindPlayerState(seat);
            if (ps != null) return ps.IsFanActive.Value;
            return _offlineStates.TryGetValue(seat, out var s) && s.IsFanActive;
        }

        public void SetIsFanActive(byte seat, bool value)
        {
            if (!IsServerContext) return;
            var ps = FindPlayerState(seat);
            if (ps != null) { ps.IsFanActive.Value = value; return; }
            var s = GetOrCreateOffline(seat); s.IsFanActive = value; SetOffline(seat, s);
        }

        public bool GetIsFanUpgraded(byte seat)
        {
            var ps = FindPlayerState(seat);
            if (ps != null) return ps.IsFanUpgraded.Value;
            return _offlineStates.TryGetValue(seat, out var s) && s.IsFanUpgraded;
        }

        public void SetIsFanUpgraded(byte seat, bool value)
        {
            if (!IsServerContext) return;
            var ps = FindPlayerState(seat);
            if (ps != null) { ps.IsFanUpgraded.Value = value; return; }
            var s = GetOrCreateOffline(seat); s.IsFanUpgraded = value; SetOffline(seat, s);
        }

        public bool GetIsBasicBlocked(byte seat)
        {
            var ps = FindPlayerState(seat);
            if (ps != null) return ps.IsBasicBlocked.Value;
            return _offlineStates.TryGetValue(seat, out var s) && s.IsBasicBlocked;
        }

        public void SetIsBasicBlocked(byte seat, bool value)
        {
            if (!IsServerContext) return;
            var ps = FindPlayerState(seat);
            if (ps != null) { ps.IsBasicBlocked.Value = value; return; }
            var s = GetOrCreateOffline(seat); s.IsBasicBlocked = value; SetOffline(seat, s);
        }

        public PlayerModifiers GetModifiers(byte seat)
        {
            if (IsParticipantAvailable(seat) && _getModifiers != null) return _getModifiers(seat);
            return _offlineStates.TryGetValue(seat, out var s) ? s.Modifiers : default;
        }

        public void SetModifiers(byte seat, PlayerModifiers value)
        {
            if (!IsServerContext) return;
            if (IsParticipantAvailable(seat) && _setModifiers != null) { _setModifiers(seat, value); return; }
            var s = GetOrCreateOffline(seat); s.Modifiers = value; SetOffline(seat, s);
        }

        public ItemSlotNetData[] GetInventorySlots(byte seat)
        {
            var ps = FindPlayerState(seat);
            if (ps != null)
            {
                var inv = ps.GetInventory();
                if (inv != null && inv.SlotStates != null)
                {
                    var slots = new ItemSlotNetData[inv.SlotStates.Count];
                    for (int i = 0; i < inv.SlotStates.Count; i++)
                        slots[i] = inv.SlotStates[i];
                    return slots;
                }
            }
            if (!_offlineStates.TryGetValue(seat, out var s) || s.InventorySlots == null) return null;
            var copy = new ItemSlotNetData[s.InventorySlots.Length];
            Array.Copy(s.InventorySlots, copy, copy.Length);
            return copy;
        }

        public void SetInventorySlots(byte seat, ItemSlotNetData[] slots)
        {
            if (!IsServerContext) return;
            var ps = FindPlayerState(seat);
            if (ps != null)
            {
                var inv = ps.GetInventory();
                if (inv != null && inv.SlotStates != null)
                {
                    inv.SlotStates.Clear();
                    if (slots != null)
                        foreach (var slot in slots)
                            inv.SlotStates.Add(slot);
                }
                return;
            }
            var s = GetOrCreateOffline(seat);
            s.InventorySlots = slots != null ? (ItemSlotNetData[])slots.Clone() : null;
            SetOffline(seat, s);
        }

        public void ClearInventory(byte seat)
        {
            SetInventorySlots(seat, System.Array.Empty<ItemSlotNetData>());
        }

        public bool IsConnected(byte seat)
        {
            return _seats.TryGetValue(seat, out var entry) && entry.State == SeatConnectionState.Connected;
        }

        public bool IsParticipantAvailable(byte seat)
        {
            if (_disposed || !_seats.TryGetValue(seat, out var entry)) return false;
            if (entry.ControllerKind == PlayerControllerKind.Human) return entry.State == SeatConnectionState.Connected;
            return entry.State == SeatConnectionState.Local && _registry != null
                && _registry.TryGetByPlayerIndex(seat, out var binding) && binding.IsValid
                && binding.Identity.ControllerKind == PlayerControllerKind.Bot
                && binding.Identity.ParticipantId == entry.ParticipantId
                && binding.Identity.Generation == entry.Generation;
        }

        public bool IsActive(byte seat)
        {
            return _seats.TryGetValue(seat, out var entry) && entry.State != SeatConnectionState.TimedOut
                && (entry.ControllerKind == PlayerControllerKind.Human || IsParticipantAvailable(seat));
        }

        // ─── B0-1c State Combination Queries ────────────────────
        // A real connected human or live local bot, and Alive.
        public bool IsTurnEligible(byte seat)
            => IsParticipantAvailable(seat) && GetLifeState(seat) == LifeState.Alive;

        // Connected && Ghost
        public bool IsGhostEligible(byte seat)
            => IsConnected(seat) && GetLifeState(seat) == LifeState.Ghost;

        // (Connected || Disconnected) && Alive — TimedOut excluded
        public bool CountsAsAliveForRoundEnd(byte seat)
            => IsActive(seat) && GetLifeState(seat) == LifeState.Alive;

        // Disconnected && Alive — auto-ready with no action
        public bool IsAutoReady(byte seat)
            => _seats.TryGetValue(seat, out var e)
               && e.ControllerKind == PlayerControllerKind.Human
               && e.State == SeatConnectionState.Disconnected
               && GetLifeState(seat) == LifeState.Alive;

        public int CountAliveForRoundEnd()
        {
            int count = 0;
            foreach (var kvp in _seats)
                if (CountsAsAliveForRoundEnd(kvp.Key)) count++;
            return count;
        }

        public int CountAllGhosts()
        {
            int count = 0;
            foreach (var kvp in _seats)
                if (IsActive(kvp.Key) && GetLifeState(kvp.Key) == LifeState.Ghost) count++;
            return count;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RosterReady = false;
            _seats.Clear();
            _clientToSeat.Clear();
            _offlineStates.Clear();
            _registry = null;
            OnPlayerDisconnectedFromSeat = null;
            Debug.Log("[MatchRoster] Disposed");
        }
    }
}
