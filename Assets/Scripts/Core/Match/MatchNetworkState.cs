using System;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace AbsoluteZero.Core.Match
{
    public enum DeathmatchGrantStage : byte { None, Pending, Completed, Failed }

    public struct DeathmatchGrantNetData : INetworkSerializable, IEquatable<DeathmatchGrantNetData>
    {
        public uint TransactionId;
        public uint RoundEpoch;
        public DeathmatchGrantStage Stage;
        public byte FirstSeat;
        public byte SecondSeat;
        public byte FirstCount;
        public byte SecondCount;
        public uint FirstFingerprint;
        public uint SecondFingerprint;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TransactionId);
            serializer.SerializeValue(ref RoundEpoch);
            serializer.SerializeValue(ref Stage);
            serializer.SerializeValue(ref FirstSeat);
            serializer.SerializeValue(ref SecondSeat);
            serializer.SerializeValue(ref FirstCount);
            serializer.SerializeValue(ref SecondCount);
            serializer.SerializeValue(ref FirstFingerprint);
            serializer.SerializeValue(ref SecondFingerprint);
        }

        public bool Equals(DeathmatchGrantNetData other)
            => TransactionId == other.TransactionId && RoundEpoch == other.RoundEpoch
                && Stage == other.Stage && FirstSeat == other.FirstSeat
                && SecondSeat == other.SecondSeat && FirstCount == other.FirstCount
                && SecondCount == other.SecondCount
                && FirstFingerprint == other.FirstFingerprint
                && SecondFingerprint == other.SecondFingerprint;

        public override bool Equals(object obj) => obj is DeathmatchGrantNetData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(
            HashCode.Combine(TransactionId, RoundEpoch, Stage, FirstSeat, SecondSeat),
            FirstCount, SecondCount, FirstFingerprint, SecondFingerprint);
    }

    public class MatchNetworkState : NetworkBehaviour
    {
        public readonly NetworkVariable<FixedString128Bytes> InitializationError = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<MatchConfigNetData> Config = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<MultiTerminalResultNetData> TerminalResult = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<uint> GhostMatchEpoch = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<uint> GhostRoundEpoch = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> GhostPossessionSpentMask = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> GhostPossessedMask = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> GhostDamageMask = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<DeathmatchGrantNetData> DeathmatchGrant = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<MultiInventorySnapshotNetData> MultiInventoryView = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        float _grantSyncWaitStart = -1f;
        bool _inventoryResyncRequested;
        readonly System.Collections.Generic.Dictionary<ulong, float> _lastResyncByClient = new();
        MultiInventoryViewPublisher _inventoryPublisher;
        bool _inventoryPublisherFaultReported;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int _debugGrantViewDelayMs;
        bool _debugGrantViewDelayUsed;
        MultiInventorySnapshotNetData _debugDelayedGrantView;
        float _debugDelayedGrantViewUntil;
        int _debugGrantDescriptorDelayMs;
        bool _debugGrantDescriptorDelayUsed;
        bool _debugGrantDescriptorHeld;
        DeathmatchGrantNetData _debugPriorGrantDescriptor;
        float _debugGrantDescriptorReleaseAt;
#endif
        public MultiInventoryReadModel InventoryReadModel { get; private set; }
        const float GrantSyncTimeoutSeconds = 10f;

        public NetworkList<int> KillScores { get; private set; }
        public NetworkList<GhostCooldownNetData> GhostCooldowns { get; private set; }

        public event Action<MatchConfigNetData> OnConfigSynced;

        void Awake()
        {
            KillScores = new NetworkList<int>();
            GhostCooldowns = new NetworkList<GhostCooldownNetData>();
        }

        void Update()
        {
            if (!IsSpawned) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugGrantDescriptorHeld
                && Time.unscaledTime >= _debugGrantDescriptorReleaseAt)
            {
                _debugGrantDescriptorHeld = false;
                Debug.Log("[MATRIX] GRANT_DESCRIPTOR_DELAY_RELEASED tx="
                    + DeathmatchGrant.Value.TransactionId);
                InventoryReadModel?.NotifyGrantChanged();
            }
            if (_debugDelayedGrantView.Revision != 0
                && Time.unscaledTime >= _debugDelayedGrantViewUntil)
            {
                var pending = _debugDelayedGrantView;
                _debugDelayedGrantView = default;
                InventoryReadModel?.Adopt(pending, Config.Value.RequiredPlayerCount);
                Debug.Log("[MATRIX] GRANT_VIEW_DELAY_RELEASED tx="
                    + pending.CommittedGrantTransaction);
            }
#endif
            if (!IsDeathmatchGrantViewBlocked)
            {
                _grantSyncWaitStart = -1f;
                _inventoryResyncRequested = false;
                return;
            }
            if (InventoryReadModel?.IsFaulted == true
                || InitializationError.Value.Length > 0) return;
            if (_grantSyncWaitStart < 0f)
                _grantSyncWaitStart = Time.unscaledTime;
            if (!IsServer && !_inventoryResyncRequested
                && Time.unscaledTime - _grantSyncWaitStart > 2f)
            {
                _inventoryResyncRequested = true;
                RequestInventoryViewServerRpc();
            }
            if (Time.unscaledTime - _grantSyncWaitStart > GrantSyncTimeoutSeconds
                && MatchCompositionRoot.Instance?.InitializationFailure == null)
                MatchCompositionRoot.Instance?.FailInitialization(
                    "Multi inventory view synchronization timed out");
        }

        void LateUpdate()
        {
            if (!IsServer || _inventoryPublisher == null || _inventoryPublisherFaultReported) return;
            try { _inventoryPublisher.Flush(); }
            catch (Exception error)
            {
                _inventoryPublisherFaultReported = true;
                MatchCompositionRoot.Instance?.FailInitialization(
                    $"Multi inventory snapshot failed: {error.Message}");
                Debug.LogException(error);
            }
        }

        public bool IsDeathmatchGrantViewBlocked
        {
            get
            {
                var root = MatchCompositionRoot.Instance;
                if (!IsSpawned || root?.ActiveConfig?.Mode != GameMode.Multi
                    || TerminalResult.Value.WinnerMask != 0)
                    return false;
                if (GhostMatchEpoch.Value == 0 || GhostRoundEpoch.Value == 0) return false;
                var view = InventoryReadModel;
                var grant = DeathmatchGrant.Value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!IsServer && _debugGrantDescriptorHeld)
                    grant = _debugPriorGrantDescriptor;
#endif
                return InitializationError.Value.Length > 0 || view == null
                    || view.IsBlockedForGrant(GhostMatchEpoch.Value,
                        GhostRoundEpoch.Value, grant);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugIsGrantDescriptorHeld => _debugGrantDescriptorHeld;
#endif

        public override void OnNetworkSpawn()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugGrantViewDelayMs = 0;
            _debugGrantViewDelayUsed = false;
            _debugDelayedGrantView = default;
            _debugGrantDescriptorDelayMs = 0;
            _debugGrantDescriptorDelayUsed = false;
            _debugGrantDescriptorHeld = false;
            if (!IsServer)
            {
                var args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "--az-delay-inventory-view-ms");
                if (index >= 0 && index + 1 < args.Length)
                    int.TryParse(args[index + 1], out _debugGrantViewDelayMs);
                index = Array.IndexOf(args, "--az-delay-grant-descriptor-ms");
                if (index >= 0 && index + 1 < args.Length)
                    int.TryParse(args[index + 1], out _debugGrantDescriptorDelayMs);
            }
#endif
            InventoryReadModel = new MultiInventoryReadModel();
            MultiInventoryView.OnValueChanged += OnInventoryViewChanged;
            DeathmatchGrant.OnValueChanged += OnDeathmatchGrantChanged;
            if (IsServer) _inventoryPublisher = new MultiInventoryViewPublisher(this);
            Config.OnValueChanged += OnConfigChanged;
            KillScores.OnListChanged += OnKillScoresChanged;
            GhostCooldowns.OnListChanged += OnGhostCooldownsChanged;

            if ((GameMode)Config.Value.Mode != GameMode.None)
                OnConfigReceived(Config.Value);

            if (KillScores.Count > 0)
                RefreshKillScoresFromCurrentState();
            OnInventoryViewChanged(default, MultiInventoryView.Value);
        }

        public override void OnNetworkDespawn()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugDelayedGrantView = default;
            _debugGrantDescriptorHeld = false;
#endif
            DeathmatchGrant.OnValueChanged -= OnDeathmatchGrantChanged;
            MultiInventoryView.OnValueChanged -= OnInventoryViewChanged;
            _inventoryPublisher?.Dispose();
            _inventoryPublisher = null;
            _inventoryPublisherFaultReported = false;
            InventoryReadModel?.Clear();
            InventoryReadModel = null;
            _grantSyncWaitStart = -1f;
            _inventoryResyncRequested = false;
            _lastResyncByClient.Clear();
            Config.OnValueChanged -= OnConfigChanged;
            KillScores.OnListChanged -= OnKillScoresChanged;
            GhostCooldowns.OnListChanged -= OnGhostCooldownsChanged;
        }

        public override void OnDestroy()
        {
            KillScores?.Dispose();
            GhostCooldowns?.Dispose();
            base.OnDestroy();
        }

        // ─── Server API ──────────────────────────────────────

        public bool ServerInitialize(MatchConfigNetData configData)
        {
            if (!IsSpawned || !IsServer) return false;

            if (Config.Value.RequiredPlayerCount > 0)
                return Config.Value.Mode == configData.Mode
                    && Config.Value.RequiredPlayerCount == configData.RequiredPlayerCount;

            int playerCount = configData.RequiredPlayerCount;
            if (playerCount < 1 || playerCount > 4)
            {
                Debug.LogError($"[MatchNetworkState] Invalid RequiredPlayerCount: {playerCount}");
                return false;
            }

            KillScores.Clear();
            for (int i = 0; i < playerCount; i++)
                KillScores.Add(0);

            TerminalResult.Value = default;
            DeathmatchGrant.Value = default;
            MultiInventoryView.Value = default;
            Config.Value = configData;

            Debug.Log($"[MatchNetworkState] Server initialized — Mode={(GameMode)configData.Mode}, Players={playerCount}");
            return true;
        }

        public void ServerAddKill(int seatIndex)
        {
            if (!IsServer) return;
            if (seatIndex < 0 || seatIndex >= KillScores.Count) return;

            KillScores[seatIndex]++;
        }

        public void ServerResetKillScores()
        {
            if (!IsServer) return;
            for (int i = 0; i < KillScores.Count; i++)
                KillScores[i] = 0;
        }

        public void ServerSetTerminalResult(uint decidingSequence, MultiMatchOutcome outcome,
            byte winnerMask, bool released)
        {
            if (!IsServer || decidingSequence == 0 || winnerMask == 0) return;
            TerminalResult.Value = new MultiTerminalResultNetData
            {
                DecidingSequence = decidingSequence,
                Outcome = outcome,
                WinnerMask = winnerMask,
                Released = released
            };
        }

        public void ServerClearTerminalResult()
        {
            if (!IsServer) return;
            TerminalResult.Value = default;
        }

        public uint ServerBeginGhostMatch()
        {
            if (!IsServer) return 0;
            uint next = GhostMatchEpoch.Value + 1;
            if (next == 0) throw new InvalidOperationException("Ghost match epoch exhausted");
            GhostMatchEpoch.Value = next;
            GhostRoundEpoch.Value = 1;
            GhostPossessionSpentMask.Value = 0;
            GhostPossessedMask.Value = 0;
            GhostDamageMask.Value = 0;
            DeathmatchGrant.Value = default;
            _inventoryPublisher?.MarkDirty();
            ServerClearAllCooldowns();
            return next;
        }

        public void ServerBeginGhostRound()
        {
            if (!IsServer || GhostMatchEpoch.Value == 0) return;
            uint next = GhostRoundEpoch.Value + 1;
            if (next == 0) throw new InvalidOperationException("Ghost round epoch exhausted");
            GhostRoundEpoch.Value = next;
            GhostPossessedMask.Value = 0;
            GhostDamageMask.Value = 0;
            DeathmatchGrant.Value = default;
            _inventoryPublisher?.MarkDirty();
            ServerClearAllCooldowns();
        }

        public void ServerPublishGhostLedger(GhostSkillLedger ledger)
        {
            if (!IsServer || ledger == null) return;
            if (ledger.MatchEpoch != GhostMatchEpoch.Value || ledger.RoundEpoch != GhostRoundEpoch.Value)
                throw new InvalidOperationException("Ghost ledger context does not match the network state");
            GhostPossessionSpentMask.Value = ledger.PossessionSpentMask;
            GhostPossessedMask.Value = ledger.PossessedMask;
            GhostDamageMask.Value = ledger.GhostDamageMask;
        }

        public void ServerPublishDeathmatchGrant(DeathmatchGrantNetData grant)
        {
            if (!IsServer || grant.TransactionId == 0
                || grant.RoundEpoch != GhostRoundEpoch.Value)
                throw new InvalidOperationException("Invalid deathmatch grant context");
            DeathmatchGrant.Value = grant;
            _inventoryPublisher?.MarkDirty();
        }

        public void ServerPublishInventorySnapshot(MultiInventorySnapshotNetData snapshot)
        {
            if (!IsServer || !snapshot.IsValid(Config.Value.RequiredPlayerCount)
                || snapshot.MatchEpoch != GhostMatchEpoch.Value
                || snapshot.RoundEpoch != GhostRoundEpoch.Value)
                throw new InvalidOperationException("Invalid Multi inventory snapshot context");
            MultiInventoryView.Value = snapshot;
        }

        void OnInventoryViewChanged(MultiInventorySnapshotNetData _,
            MultiInventorySnapshotNetData current)
        {
            int seats = Config.Value.RequiredPlayerCount;
            if (seats < 1 || seats > MultiInventorySnapshotNetData.MaxSeats) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer && _debugGrantViewDelayMs > 0
                && !_debugGrantViewDelayUsed
                && current.CommittedGrantTransaction != 0)
            {
                _debugGrantViewDelayUsed = true;
                _debugDelayedGrantView = current;
                _debugDelayedGrantViewUntil = Time.unscaledTime
                    + _debugGrantViewDelayMs / 1000f;
                Debug.Log("[MATRIX] GRANT_VIEW_DELAY_STARTED tx="
                    + current.CommittedGrantTransaction);
                return;
            }
#endif
            InventoryReadModel?.Adopt(current, seats);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer && _debugGrantDescriptorHeld
                && current.CommittedGrantTransaction != 0)
                Debug.Log("[MATRIX] GRANT_VIEW_BEFORE_DESCRIPTOR tx="
                    + current.CommittedGrantTransaction);
#endif
        }

        void OnDeathmatchGrantChanged(DeathmatchGrantNetData previous,
            DeathmatchGrantNetData current)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer && _debugGrantDescriptorDelayMs > 0
                && !_debugGrantDescriptorDelayUsed
                && current.Stage == DeathmatchGrantStage.Completed)
            {
                _debugGrantDescriptorDelayUsed = true;
                _debugGrantDescriptorHeld = true;
                // Preserve the transaction identity but expose its pre-completion
                // state to the local read-model gate until the injected release.
                _debugPriorGrantDescriptor = current;
                _debugPriorGrantDescriptor.Stage = DeathmatchGrantStage.Pending;
                _debugGrantDescriptorReleaseAt = Time.unscaledTime
                    + _debugGrantDescriptorDelayMs / 1000f;
                Debug.Log("[MATRIX] GRANT_DESCRIPTOR_DELAY_STARTED tx="
                    + current.TransactionId);
            }
#endif
            InventoryReadModel?.NotifyGrantChanged();
        }

        [Rpc(SendTo.Server)]
        void RequestInventoryViewServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || (GameMode)Config.Value.Mode != GameMode.Multi) return;
            ulong client = rpcParams.Receive.SenderClientId;
            if (NetworkManager == null || !NetworkManager.ConnectedClients.ContainsKey(client)) return;
            if (_lastResyncByClient.TryGetValue(client, out float last)
                && Time.unscaledTime - last < 5f) return;
            _lastResyncByClient[client] = Time.unscaledTime;
            _inventoryPublisher?.MarkDirty();
        }

        // ─── Change Handlers ─────────────────────────────────

        void OnConfigChanged(MatchConfigNetData prev, MatchConfigNetData current)
        {
            OnConfigReceived(current);
        }

        void OnConfigReceived(MatchConfigNetData data)
        {
            Debug.Log($"[MatchNetworkState] Config received — Mode={(GameMode)data.Mode}, RequiredPlayers={data.RequiredPlayerCount}");
            OnConfigSynced?.Invoke(data);
        }

        void OnKillScoresChanged(NetworkListEvent<int> changeEvent)
        {
        }

        void OnGhostCooldownsChanged(NetworkListEvent<GhostCooldownNetData> changeEvent)
        {
        }

        public void ServerSetCooldown(byte seat, byte skill, byte remainingTurns)
        {
            if (!IsServer) return;
            for (int i = 0; i < GhostCooldowns.Count; i++)
            {
                var cd = GhostCooldowns[i];
                if (cd.Seat == seat && cd.Skill == skill)
                {
                    if (remainingTurns == 0)
                        GhostCooldowns.RemoveAt(i);
                    else
                        GhostCooldowns[i] = new GhostCooldownNetData
                            { Seat = seat, Skill = skill, RemainingTurns = remainingTurns };
                    return;
                }
            }
            if (remainingTurns > 0)
                GhostCooldowns.Add(new GhostCooldownNetData
                    { Seat = seat, Skill = skill, RemainingTurns = remainingTurns });
        }

        public byte ServerGetCooldown(byte seat, byte skill)
        {
            for (int i = 0; i < GhostCooldowns.Count; i++)
            {
                var cd = GhostCooldowns[i];
                if (cd.Seat == seat && cd.Skill == skill)
                    return cd.RemainingTurns;
            }
            return 0;
        }

        public void ServerTickAllCooldowns()
        {
            if (!IsServer) return;
            for (int i = GhostCooldowns.Count - 1; i >= 0; i--)
            {
                var cd = GhostCooldowns[i];
                if (cd.RemainingTurns <= 1)
                    GhostCooldowns.RemoveAt(i);
                else
                    GhostCooldowns[i] = new GhostCooldownNetData
                        { Seat = cd.Seat, Skill = cd.Skill, RemainingTurns = (byte)(cd.RemainingTurns - 1) };
            }
        }

        public void ServerClearAllCooldowns()
        {
            if (!IsServer) return;
            GhostCooldowns.Clear();
        }

        void RefreshKillScoresFromCurrentState()
        {
            for (int i = 0; i < KillScores.Count; i++)
                Debug.Log($"[MatchNetworkState] Initial KillScore[{i}]={KillScores[i]}");
        }
    }
}
