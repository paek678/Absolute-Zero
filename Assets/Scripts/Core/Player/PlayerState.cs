using System;
using System.Text;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Emote;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Turn;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    public readonly struct MiniGameTicket
    {
        public readonly byte Slot;
        public readonly MiniGameType Type;
        public readonly float TimeLimit;
        public readonly int Goal;
        public readonly uint MatchEpoch;
        public readonly uint RoundEpoch;
        public readonly int Turn;
        public readonly uint AttemptId;
        public readonly uint CopyId;

        public MiniGameTicket(byte slot, MiniGameType type, float timeLimit, int goal,
            uint matchEpoch, uint roundEpoch, int turn, uint attemptId, uint copyId)
        {
            Slot = slot;
            Type = type;
            TimeLimit = timeLimit;
            Goal = goal;
            MatchEpoch = matchEpoch;
            RoundEpoch = roundEpoch;
            Turn = turn;
            AttemptId = attemptId;
            CopyId = copyId;
        }

        public bool IsNewerThan(MiniGameTicket current)
        {
            if (MatchEpoch != current.MatchEpoch) return MatchEpoch > current.MatchEpoch;
            if (RoundEpoch != current.RoundEpoch) return RoundEpoch > current.RoundEpoch;
            if (Turn != current.Turn) return Turn > current.Turn;
            return AttemptId > current.AttemptId;
        }
    }

    public class PlayerState : NetworkBehaviour
    {
        public readonly NetworkVariable<float> Temperature = new(
            37f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> FanSpeed = new(
            1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsReady = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsFanActive = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> SyncedPlayerIndex = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<ParticipantMetadataNetData> ParticipantMetadata = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> HasSelectedItem = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsFanUpgraded = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsBasicBlocked = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<LifeState> CurrentLifeState = new(
            LifeState.Alive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString128Bytes> CosmeticDataNV = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        bool _hasAcceptedCosmetic;

        public static event Action<Transform, Vector3, byte> OnEmoteRequested;

        readonly ActionQueue _actionQueue = new();
        PlayerInventory _inventory;
        PlayerIdentity? _cachedIdentity;
        PlayerRegistry _registeredRegistry;
        PlayerBinding _binding;
        MatchParticipantDescriptor _configuredParticipant;
        MatchParticipantDescriptor _participant;
        CosmeticSubmissionTracker _cosmeticSubmission;
        PlayerBinding _cosmeticBinding;
        public CosmeticSubmissionStatus? CosmeticSubmission => _cosmeticSubmission?.Status;
        ITurnContext _turnContext;
        TrustedBotCommandAdapter _botCommands;
        PlayerActionAdmission _admission;
        PlayerActionAdmission Admission => _admission ??= new PlayerActionAdmission(this);
        internal PlayerRegistry ActionRegistry => _registeredRegistry;
        internal ITurnContext ActionTurnContext => _turnContext;
        internal bool HasPendingMiniGame => _miniGameAttempts.HasPending;

        void Update()
        {
            _botCommands?.Tick();
            TickCosmeticSubmission();
            if (IsServer && _miniGameAttempts.Pending is MiniGameAttempt attempt
                && (!IsMiniGameScopeCurrent(attempt) || _turnContext?.Phase != TurnPhase.PrepPhase
                    || IsReady.Value || !Admission.IsLiveActionParticipant(_binding)))
                CancelPendingMiniGame();
        }

        bool IsMiniGameScopeCurrent(MiniGameAttempt attempt)
        {
            var root = MatchCompositionRoot.Instance;
            var network = root?.NetworkState;
            return IsSpawned && Admission.IsCurrentActionBinding(_binding) && root.MatchManager != null
                && attempt.IsCurrent(_binding, root.MatchManager.RoundNumber.Value,
                    network?.GhostMatchEpoch.Value ?? 0, network?.GhostRoundEpoch.Value ?? 0,
                    TurnManager.Instance?.TurnNumber.Value ?? 0);
        }

        internal TrustedBotCommandAdapter GetBotCommands()
        {
            if (!IsServer || !IsSpawned || !IsBotControlled || !IsParticipantReady) return null;
            var settings = AppBootstrapper.Instance?.SessionRouter.LaunchContext?.Solo;
            if (settings == null || !Admission.IsCurrentActionBinding(_binding)) return null;
            return _botCommands ??= new TrustedBotCommandAdapter(this, settings, new NgoBotActionClock(NetworkManager));
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal TrustedBotCommandAdapter DebugCreateBotCommands(IBotActionClock clock)
        {
            if (GetBotCommands() == null) return null;
            _botCommands.Dispose();
            return _botCommands = new TrustedBotCommandAdapter(this, AppBootstrapper.Instance.SessionRouter.LaunchContext.Solo, clock);
        }
#endif

        ActionIntent? _pendingIntent;

        readonly MiniGameAttemptTracker _miniGameAttempts = new();
        int _readyServerTick;

        public event System.Action<MiniGameTicket> OnMiniGameStart;
        public event System.Action<uint> OnItemSelectionRejected;

        public int PlayerIndex => SyncedPlayerIndex.Value;
        public ActionQueue GetActionQueue() => _actionQueue;
        public ActionIntent? PendingIntent => _pendingIntent;
        public MatchParticipantDescriptor Participant => _participant;
        public PlayerBinding Binding => _binding;
        public bool IsBotControlled => (_participant ?? _configuredParticipant)?.ControllerKind == PlayerControllerKind.Bot;
        public bool IsParticipantReady => _cachedIdentity.HasValue && _binding != null && _binding.IsValid
            && _registeredRegistry != null
            && _registeredRegistry.TryGetByPlayerIndex(_cachedIdentity.Value.PlayerIndex, out var current)
            && ReferenceEquals(current, _binding);
        public event Action<PlayerBinding> BindingReady;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public uint DebugPendingMiniGameAttemptId => _miniGameAttempts.Pending?.Ticket.AttemptId ?? 0;
        public bool DebugSuppressPresentationAck;
#endif

        public ActionIntent BuildActionIntent()
        {
            int idx = SyncedPlayerIndex.Value;
            if (idx < 0 || idx > 254)
                return ActionIntent.Empty;

            var q = _actionQueue;
            byte seat = (byte)idx;
            short itemId = -1;
            byte slotIndex = 0;
            byte targetSeat = ActionIntent.NoTarget;

            if (q.selectedAction.HasValue)
            {
                var sel = q.selectedAction.Value;
                slotIndex = sel.SlotIndex;
                targetSeat = sel.TargetSeat;
                if (sel.ItemData != null)
                {
                    var inv = GetInventory();
                    if (inv != null && sel.SlotIndex < inv.SlotStates.Count)
                        itemId = inv.SlotStates[sel.SlotIndex].ItemId;
                }
            }

            var intent = new ActionIntent(seat, slotIndex, itemId, targetSeat, _readyServerTick);
            _pendingIntent = intent;
            return intent;
        }

        public void ClearPendingIntent() => _pendingIntent = null;

        public void CancelPendingMiniGame() => CancelPendingMiniGame(true);

        void CancelPendingMiniGame(bool notifyOwner)
        {
            if (!IsServer || !_miniGameAttempts.Cancel(out var attempt)) return;
            // Clear before invoking a synchronous Host observer. Despawn needs no
            // RPC; the view is being torn down with its participant binding.
            if (notifyOwner && IsSpawned && NetworkManager != null && NetworkManager.IsListening
                && !NetworkManager.ShutdownInProgress && !IsBotControlled
                && NetworkManager.ConnectedClients.ContainsKey(OwnerClientId))
                ItemSelectionRejectedClientRpc(attempt.Ticket.CopyId);
        }

        void OnInventorySlotChanged(NetworkListEvent<ItemSlotNetData> change)
        {
            if (IsServer && _miniGameAttempts.Pending is MiniGameAttempt attempt
                && Admission.FindItemCopy(attempt.Ticket.CopyId) < 0)
                CancelPendingMiniGame();
        }

        public PlayerInventory GetInventory()
        {
            if (_inventory == null)
                _inventory = GetComponent<PlayerInventory>();
            return _inventory;
        }

        public void Initialize(int playerIndex, PlayerInventory inventory)
        {
            if (!IsServer) return;
            _inventory = inventory;
            if (_participant != null && _participant.Seat == playerIndex)
            {
                AssignParticipant(_participant);
                return;
            }
            var participants = MatchCompositionRoot.Instance?.Participants;
            if (participants != null)
                foreach (var descriptor in participants)
                    if (descriptor.Seat == playerIndex)
                    {
                        AssignParticipant(descriptor);
                        return;
                    }
            throw new InvalidOperationException("Assign a roster participant before initializing its player state.");
        }

        public void BindTurnContext(ITurnContext ctx) => _turnContext = ctx;

        public void ConfigureParticipant(MatchParticipantDescriptor participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (IsSpawned) throw new InvalidOperationException("Configure the participant before spawning.");
            _configuredParticipant = participant;
        }

        public bool AssignParticipant(MatchParticipantDescriptor participant)
        {
            if (!IsServer || !IsSpawned || participant == null || _binding == null || _registeredRegistry == null)
                return false;
            if (_participant != null && !_participant.Equals(participant)) return false;
            if (participant.ControllerKind == PlayerControllerKind.Human
                && (!participant.ClientId.HasValue || participant.ClientId.Value != OwnerClientId || !NetworkObject.IsPlayerObject))
                return false;
            if (participant.ControllerKind == PlayerControllerKind.Bot
                && (!NetworkObject.IsOwnedByServer || NetworkObject.IsPlayerObject)) return false;
            SyncedPlayerIndex.Value = participant.Seat;
            ParticipantMetadata.Value = new ParticipantMetadataNetData(participant);
            // Also reconcile an already-published descriptor after a pending/ready
            // handoff. An identical value does not raise a NetworkVariable callback.
            return TryCompleteBinding();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
                GetInventory().SlotStates.OnListChanged += OnInventorySlotChanged;

            CosmeticDataNV.OnValueChanged += OnCosmeticNVChanged;

            var mcr = MatchCompositionRoot.Instance;
            if (mcr == null)
            {
                Debug.LogWarning("[PlayerState] MatchCompositionRoot not found — skipping registry");
                return;
            }

            _registeredRegistry = mcr.WritableRegistry;
            _binding = new PlayerBinding(this, GetInventory(), NetworkObject);
            if (!_registeredRegistry.RegisterPending(_binding))
            {
                mcr.FailInitialization("Player network object could not enter the registry.");
                return;
            }
            SyncedPlayerIndex.OnValueChanged += OnIndexAssigned;
            ParticipantMetadata.OnValueChanged += OnParticipantAssigned;
            if (IsServer && _configuredParticipant != null)
                AssignParticipant(_configuredParticipant);
            else
                TryCompleteBinding();
        }

        void OnIndexAssigned(int prev, int cur) => TryCompleteBinding();
        void OnParticipantAssigned(ParticipantMetadataNetData prev, ParticipantMetadataNetData cur)
            => TryCompleteBinding();

        bool TryCompleteBinding()
        {
            if (!IsSpawned || _binding == null || _registeredRegistry == null || !ParticipantMetadata.Value.Assigned
                || SyncedPlayerIndex.Value != ParticipantMetadata.Value.Seat) return false;
            MatchParticipantDescriptor descriptor;
            try { descriptor = ParticipantMetadata.Value.ToDescriptor(); }
            catch (ArgumentException error)
            {
                Debug.LogError("[PlayerState] Invalid participant metadata: " + error.Message);
                return false;
            }
            if (_participant != null && !_participant.Equals(descriptor)) return false;
            bool wasReady = IsParticipantReady;
            var previousIdentity = _cachedIdentity;
            var previousParticipant = _participant;
            // Set the descriptor before publishing Registered, so its synchronous
            // observers can distinguish a local bot from the human owner.
            _participant = descriptor;
            _cachedIdentity = new PlayerIdentity(descriptor, _binding.NetworkObjectId);
            if (!_registeredRegistry.TryPromote(_binding, descriptor, out var reason))
            {
                _cachedIdentity = previousIdentity;
                _participant = previousParticipant;
                Debug.LogError("[PlayerState] Participant binding failed: " + reason);
                return false;
            }
            _cachedIdentity = _binding.Identity;
            if (!wasReady)
            {
                ApplyInitialBotCosmetics();
                BindingReady?.Invoke(_binding);
                if (CosmeticDataNV.Value.Length > 0)
                    OnCosmeticNVChanged(default, CosmeticDataNV.Value);
            }
            TickCosmeticSubmission();
            return true;
        }

        void TickCosmeticSubmission()
        {
            var root = MatchCompositionRoot.Instance;
            bool current = IsSpawned && IsParticipantReady && _binding != null && _binding.IsValid
                && root != null && root.IsSessionCurrent && ReferenceEquals(root.Registry, _registeredRegistry)
                // Root.Generation is assigned by ServerBootstrapMatch, not on remote clients.
                // Exact current registry membership checks the server-authored binding instead.
                && LocalMatchPerspective.IsLocalHuman(this);
            if (!current)
            {
                _cosmeticSubmission?.Dispose();
                return;
            }
            if (!ReferenceEquals(_cosmeticBinding, _binding))
            {
                _cosmeticSubmission?.Dispose();
                _cosmeticBinding = _binding;
                _cosmeticSubmission = new CosmeticSubmissionTracker(Time.unscaledTimeAsDouble);
            }
            var before = _cosmeticSubmission.Status;
            if (before != CosmeticSubmissionStatus.WaitingForDependencies && before != CosmeticSubmissionStatus.AwaitingAcceptance) return;
            var profile = CosmeticProfileService.Instance;
            var candidate = before == CosmeticSubmissionStatus.WaitingForDependencies ? profile?.EquipState?.ToDto() : null;
            _cosmeticSubmission.Tick(Time.unscaledTimeAsDouble, profile?.Registry, candidate,
                CosmeticDataNV.Value.ToString(), dto => SubmitCosmeticRpc(dto));
            ReportCosmeticStatus(before);
        }

        void ReportCosmeticStatus(CosmeticSubmissionStatus? before)
        {
            if (_cosmeticSubmission != null && before != _cosmeticSubmission.Status && _cosmeticSubmission.Status is
                CosmeticSubmissionStatus.DependenciesUnavailable or CosmeticSubmissionStatus.InvalidLocalData
                or CosmeticSubmissionStatus.AcceptanceUnconfirmed or CosmeticSubmissionStatus.AcceptedDifferent)
                Debug.LogWarning($"[PlayerState P{PlayerIndex}] Appearance {_cosmeticSubmission.Status} object={NetworkObjectId}");
        }

        void ApplyInitialBotCosmetics()
        {
            if (!IsServer || !IsBotControlled || !IsParticipantReady || _hasAcceptedCosmetic) return;
            var root = MatchCompositionRoot.Instance;
            var launch = AppBootstrapper.Instance?.SessionRouter.LaunchContext;
            if (root == null || !ReferenceEquals(root.Registry, _registeredRegistry) || !root.IsSessionCurrent
                || root.ActiveConfig?.Mode != GameMode.Solo || root.Generation != _participant.Generation
                || launch == null || launch.Mode != GameMode.Solo || launch.Generation != _participant.Generation
                || launch.Solo == null) return;
            // The immutable resolved settings have already checked every part ID
            // against the encounter's registry. Never read the host's equip state.
            if (!TrySerializeBotCosmetics(launch.Solo.Cosmetics, out var canonical))
            {
                root.FailInitialization("Resolved bot appearance exceeds the supported cosmetic message size.");
                return;
            }
            _hasAcceptedCosmetic = true;
            CosmeticDataNV.Value = canonical;
        }

        public static bool TrySerializeBotCosmetics(BotCosmeticIds ids, out FixedString128Bytes canonical)
        {
            canonical = default;
            var dto = new CosmeticDto
            {
                head = ids.Head ?? "", top = ids.Top ?? "", back = ids.Back ?? "",
                bottom = ids.Bottom ?? "", tail = ids.Tail ?? ""
            };
            string json = JsonUtility.ToJson(dto);
            if (Encoding.UTF8.GetByteCount(json) > 125) return false;
            canonical = new FixedString128Bytes(json);
            return true;
        }

        public override void OnNetworkDespawn()
        {
            _botCommands?.Dispose();
            _botCommands = null;
            if (IsServer && _inventory != null)
                _inventory.SlotStates.OnListChanged -= OnInventorySlotChanged;
            _miniGameAttempts.Cancel(out _);
            CosmeticDataNV.OnValueChanged -= OnCosmeticNVChanged;
            SyncedPlayerIndex.OnValueChanged -= OnIndexAssigned;
            ParticipantMetadata.OnValueChanged -= OnParticipantAssigned;
            _registeredRegistry?.Unregister(_binding);
            _registeredRegistry = null;
            _binding = null;
            _participant = null;
            _configuredParticipant = null;
            _cosmeticSubmission?.Dispose();
            _cosmeticSubmission = null;
            _cosmeticBinding = null;
            _hasAcceptedCosmetic = false;
            _cachedIdentity = null;
            base.OnNetworkDespawn();
        }

        public void ResetForNewTurn()
        {
            HasSelectedItem.Value = false;
            _actionQueue.Clear();
            CancelPendingMiniGame();
            _readyServerTick = 0;
            _pendingIntent = null;
        }

        internal ItemContext BuildContext(byte targetSeat = ActionIntent.NoTarget)
        {
            int myIndex = SyncedPlayerIndex.Value;

            PlayerState opponent = null;
            var mcr = MatchCompositionRoot.Instance;
            if (mcr != null)
            {
                if (targetSeat != ActionIntent.NoTarget
                    && mcr.Registry.TryGetByPlayerIndex(targetSeat, out var targetEntry))
                {
                    opponent = targetEntry.State;
                }
                else
                {
                    foreach (var p in mcr.Registry.Players)
                    {
                        if (p.Identity.PlayerIndex != (byte)myIndex)
                        {
                            opponent = p.State;
                            break;
                        }
                    }
                }
            }
            // Identity/target validation resolves a live registry binding before
            // context construction. Never manufacture an opponent from seat math.

            if (opponent == null)
            {
                Debug.LogError($"[PlayerState P{myIndex}] BuildContext: no opponent found");
                return new ItemContext
                {
                    User = this,
                    UserIndex = myIndex,
                    UserInventory = _inventory,
                    AllModifiers = _turnContext?.GetModifiers(),
                    TempSystem = _turnContext?.GetTempSystem(),
                    BuffSystem = _turnContext?.GetBuffSystem(),
                    DropTable = _turnContext?.GetDropTable(),
                };
            }

            return new ItemContext
            {
                User = this,
                Target = opponent,
                UserIndex = myIndex,
                TargetIndex = opponent.PlayerIndex,
                UserInventory = _inventory,
                TargetInventory = opponent.GetInventory(),
                AllModifiers = _turnContext?.GetModifiers(),
                TempSystem = _turnContext?.GetTempSystem(),
                BuffSystem = _turnContext?.GetBuffSystem(),
                DropTable = _turnContext?.GetDropTable(),
            };
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectItemServerRpc(byte slotIndex, byte targetSeat = ActionIntent.NoTarget,
            uint expectedCopyId = 0, uint matchEpoch = 0, uint roundEpoch = 0,
            uint observedGrantTransaction = 0,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!IsParticipantReady || IsBotControlled || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (TurnManager.Instance?.IsDeathmatchGrantInProgress == true
                || _turnContext == null || _turnContext.Phase != TurnPhase.PrepPhase || IsReady.Value)
            {
                ItemSelectionRejectedClientRpc(expectedCopyId);
                return;
            }
            if (_miniGameAttempts.HasPending)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: mini-game in progress");
                // A duplicate request for the active copy must not reject that
                // attempt's UI. Different-copy submissions can be rejected safely.
                if (expectedCopyId != 0 && expectedCopyId != _miniGameAttempts.Pending.Value.Ticket.CopyId)
                    ItemSelectionRejectedClientRpc(expectedCopyId);
                return;
            }
            var root = MatchCompositionRoot.Instance;
            if (root?.ActiveConfig?.Mode == GameMode.Multi)
            {
                var network = root.NetworkState;
                if (network == null || expectedCopyId == 0
                    || matchEpoch != network.GhostMatchEpoch.Value
                    || roundEpoch != network.GhostRoundEpoch.Value)
                {
                    ItemSelectionRejectedClientRpc(expectedCopyId);
                    return;
                }
                var grant = network.DeathmatchGrant.Value;
                if (grant.RoundEpoch == roundEpoch
                    && grant.Stage == DeathmatchGrantStage.Completed
                    && observedGrantTransaction != grant.TransactionId)
                {
                    ItemSelectionRejectedClientRpc(expectedCopyId);
                    return;
                }
                // A displayed slot may have compacted. Keep the clicked physical copy.
                int actualSlot = -1;
                for (int i = 0; i < _inventory.SlotStates.Count; i++)
                    if (!_inventory.SlotStates[i].IsEmpty
                        && _inventory.SlotStates[i].CopyId == expectedCopyId)
                    {
                        actualSlot = i;
                        break;
                    }
                if (actualSlot < 0)
                {
                    ItemSelectionRejectedClientRpc(expectedCopyId);
                    return;
                }
                slotIndex = (byte)actualSlot;
            }
            // Online Multi's epoch/display reconciliation remains above this
            // shared legality boundary. Ordinary 1v1 retains its slot RPC contract.
            var validation = Admission.ValidateItemCandidate(slotIndex, expectedCopyId, targetSeat, null, out var candidate);
            if (!validation.Succeeded)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: {validation.Reason}");
                ItemSelectionRejectedClientRpc(expectedCopyId);
                return;
            }
            slotIndex = candidate.SlotIndex;
            targetSeat = candidate.TargetSeat;
            var itemData = candidate.ItemData;
            var slot = _inventory.SlotStates[slotIndex];

            if (itemData.RequiresMiniGame)
            {
                double now = NetworkManager.ServerTime.Time;
                double prepEnd = _turnContext.PrepStartTime + _turnContext.PrepDurationSeconds;
                var netState = root.NetworkState;
                var description = new MiniGameTicket(slotIndex, itemData.MiniGameType,
                    itemData.MiniGameTimeLimit, itemData.MiniGameGoal,
                    netState?.GhostMatchEpoch.Value ?? 0, netState?.GhostRoundEpoch.Value ?? 0,
                    TurnManager.Instance?.TurnNumber.Value ?? 0, 0, slot.CopyId);
                var ticket = _miniGameAttempts.Begin(_binding, root.MatchManager.RoundNumber.Value,
                    description, targetSeat, now, prepEnd).Ticket;

                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game START: {itemData.ItemName} " +
                          $"({itemData.MiniGameType}, {itemData.MiniGameTimeLimit}s, goal={itemData.MiniGameGoal})");

                StartMiniGameClientRpc(slotIndex, (byte)itemData.MiniGameType,
                    itemData.MiniGameTimeLimit, itemData.MiniGameGoal,
                    ticket.MatchEpoch, ticket.RoundEpoch, ticket.Turn, ticket.AttemptId, ticket.CopyId);
                return;
            }

            if (!QueueValidatedItem(candidate).Succeeded) ItemSelectionRejectedClientRpc(expectedCopyId);
        }

        [Rpc(SendTo.Owner)]
        void ItemSelectionRejectedClientRpc(uint copyId)
            => OnItemSelectionRejected?.Invoke(copyId);

        // These trusted internal entry points are bound to this live Solo bot.
        // A human cannot use them to bypass the RPC or its mini-game ticket.
        internal PlayerActionResult ServerValidateBotItem(PrepInputKey key, uint copyId, byte targetSeat,
            out PlayerActionCandidate candidate)
        {
            candidate = default;
            var check = Admission.CheckBotCommand(key);
            if (!check.Succeeded) return check;
            if (copyId == 0) return PlayerActionResult.Reject(PlayerActionReason.CopyMissing);
            return Admission.ValidateItemCandidate(0, copyId, targetSeat, key, out candidate);
        }

        internal PlayerActionResult ServerQueueBotItem(PrepInputKey key, PlayerActionCandidate candidate, TrustedBotCommandAdapter authorization = null)
        {
            var check = Admission.CheckBotCommand(key);
            if (!check.Succeeded) return check;
            if (!candidate.PrepKey.HasValue || candidate.PrepKey.Value != key || candidate.CopyId == 0
                || !ReferenceEquals(candidate.Binding, _binding))
                return PlayerActionResult.Stale(PlayerActionReason.StaleCandidate);
            if (_botCommands?.HasPending == true && !ReferenceEquals(_botCommands, authorization))
                return PlayerActionResult.Reject(PlayerActionReason.BotUsePending);
            return QueueValidatedItem(candidate);
        }

        internal PlayerActionResult ServerCancelBotSelection(PrepInputKey key)
        {
            var check = Admission.CheckBotCommand(key);
            if (_botCommands?.HasPending == true) return PlayerActionResult.Reject(PlayerActionReason.BotUsePending);
            return check.Succeeded ? CancelSelectedItem() : check;
        }

        internal PlayerActionResult ServerPressBotReady(PrepInputKey key)
        {
            var check = Admission.CheckBotCommand(key);
            if (_botCommands?.HasPending == true) return PlayerActionResult.Reject(PlayerActionReason.BotUsePending);
            return check.Succeeded ? MarkReady() : check;
        }

        PlayerActionResult QueueValidatedItem(PlayerActionCandidate candidate)
        {
            if (!ReferenceEquals(candidate.Binding, _binding))
                return PlayerActionResult.Stale(PlayerActionReason.StaleCandidate);
            var result = Admission.ValidateItemCandidate(candidate.SlotIndex, candidate.CopyId,
                candidate.TargetSeat, candidate.PrepKey, out var current);
            if (!result.Succeeded) return result;
            if (current.ItemData != candidate.ItemData)
                return PlayerActionResult.Stale(PlayerActionReason.StaleCandidate);

            // Copy identity is required for Solo's delayed selection and existing
            // Multi. Preserve the established online duel's queue payload.
            var mode = MatchCompositionRoot.Instance.ActiveConfig.Mode;
            uint copyId = mode == GameMode.Multi || mode == GameMode.Solo ? current.CopyId : 0;
            _actionQueue.SetSelected(current.SlotIndex, current.ItemData, current.TargetSeat, copyId);
            HasSelectedItem.Value = true;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Item selected: {current.ItemData.ItemName} (queued for Attack)");

            _turnContext.PublishItemUsed(
                (byte)SyncedPlayerIndex.Value, current.SlotIndex, (byte)current.ItemData.Category, false);
            return new PlayerActionResult(PlayerActionStatus.Queued);
        }

        [Rpc(SendTo.Owner)]
        void StartMiniGameClientRpc(byte slotIndex, byte miniGameType, float timeLimit, int goal,
            uint matchEpoch, uint roundEpoch, int turn, uint attemptId, uint copyId)
        {
            OnMiniGameStart?.Invoke(new MiniGameTicket(slotIndex, (MiniGameType)miniGameType,
                timeLimit, goal, matchEpoch, roundEpoch, turn, attemptId, copyId));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SubmitMiniGameResultServerRpc(byte slotIndex, bool success,
            uint matchEpoch, uint roundEpoch, int turn, uint attemptId, uint copyId,
            RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!IsParticipantReady || IsBotControlled || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (TurnManager.Instance?.IsDeathmatchGrantInProgress == true) return;
            if (!_miniGameAttempts.TryTake(slotIndex, matchEpoch, roundEpoch, turn,
                    attemptId, copyId, out var attempt))
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: stale ticket {attemptId}");
                return;
            }
            try
            {
                if (!IsMiniGameScopeCurrent(attempt)) return;
                byte savedTarget = attempt.Target;
                int actualSlot = Admission.FindItemCopy(copyId);

                if (_turnContext == null || _turnContext.Phase != TurnPhase.PrepPhase)
                {
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: prep phase already over");
                    return;
                }
                if (IsReady.Value) return;
                if (!attempt.IsWithinDeadline(NetworkManager.ServerTime.Time))
                {
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: past deadline");
                    return;
                }

                if (actualSlot < 0) return;
                slotIndex = (byte)actualSlot;
                var slot = _inventory.SlotStates[slotIndex];
                if (!slot.IsUsable) return;
                var itemData = _inventory.GetItemData(slotIndex);
                if (!ItemAvailability.IsEnabled(itemData)) return;
                if (!Admission.TryResolveTargetSeat(itemData, savedTarget, out byte resolvedTarget))
                {
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: actor or target no longer eligible");
                    return;
                }

                if (!success)
                {
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game FAILED: slot {slotIndex} — consuming 1 use");
                    _inventory.ConsumeItem(slotIndex);
                    _inventory.CompactSlots();
                    return;
                }

                var validation = Admission.ValidateItemCandidate(slotIndex, copyId, resolvedTarget, null, out var candidate);
                if (!validation.Succeeded) return;

                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game SUCCESS: {itemData.ItemName} → queueing");
                QueueValidatedItem(candidate);
            }
            finally
            {
                // Existing response and payload; stale/duplicate tickets returned above.
                if (!HasSelectedItem.Value) ItemSelectionRejectedClientRpc(copyId);
            }
        }

        void ExecuteFreeAction(byte slotIndex, ItemDataSO itemData, ItemContext ctx)
        {
            itemData.ExecuteEffect(ctx);
            _inventory.ConsumeItem(slotIndex);

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Free action executed: {itemData.ItemName}");

            if (ctx.UserModifiers.OpponentRevealed)
            {
                var opponent = ctx.Target;
                var oppQueue = opponent.GetActionQueue();
                short oppItemId = -1;
                if (oppQueue.selectedAction.HasValue)
                {
                    var oppInv = opponent.GetInventory();
                    byte oppSlot = oppQueue.selectedAction.Value.SlotIndex;
                    if (oppSlot < oppInv.SlotStates.Count)
                        oppItemId = oppInv.SlotStates[oppSlot].ItemId;
                }
                _turnContext.PublishOpponentRevealed(
                    (byte)SyncedPlayerIndex.Value, oppItemId);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void CancelSelectionServerRpc(uint matchEpoch = 0, uint roundEpoch = 0,
            uint observedGrantTransaction = 0, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!IsParticipantReady || IsBotControlled || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (TurnManager.Instance?.IsDeathmatchGrantInProgress == true) return;
            if (_turnContext == null || _turnContext.Phase != TurnPhase.PrepPhase) return;
            if (IsReady.Value) return;
            if (!HasSelectedItem.Value) return;
            var root = MatchCompositionRoot.Instance;
            if (root?.ActiveConfig?.Mode == GameMode.Multi)
            {
                var state = root.NetworkState;
                if (state == null || state.GhostMatchEpoch.Value != matchEpoch
                    || state.GhostRoundEpoch.Value != roundEpoch) return;
                var grant = state.DeathmatchGrant.Value;
                if (grant.RoundEpoch == roundEpoch
                    && grant.Stage == DeathmatchGrantStage.Completed
                    && observedGrantTransaction != grant.TransactionId) return;
            }

            CancelSelectedItem();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void PressReadyServerRpc(uint matchEpoch = 0, uint roundEpoch = 0,
            uint observedGrantTransaction = 0, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!IsParticipantReady || IsBotControlled || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (TurnManager.Instance?.IsDeathmatchGrantInProgress == true) return;
            if (_turnContext == null || _turnContext.Phase != TurnPhase.PrepPhase) return;
            if (IsReady.Value) return;
            if (CurrentLifeState.Value != LifeState.Alive) return;
            var root = MatchCompositionRoot.Instance;
            if (root?.ActiveConfig?.Mode == GameMode.Multi)
            {
                var state = root.NetworkState;
                if (state == null || state.GhostMatchEpoch.Value != matchEpoch
                    || state.GhostRoundEpoch.Value != roundEpoch) return;
                var grant = state.DeathmatchGrant.Value;
                if (grant.RoundEpoch == roundEpoch && grant.Stage == DeathmatchGrantStage.Completed
                    && observedGrantTransaction != grant.TransactionId) return;
            }

            MarkReady();
        }

        PlayerActionResult CancelSelectedItem()
        {
            var check = Admission.CheckSharedActionWindow(requireAlive: false);
            if (!check.Succeeded) return check;
            if (!HasSelectedItem.Value) return PlayerActionResult.Reject(PlayerActionReason.NoSelection);
            _actionQueue.selectedAction = null;
            HasSelectedItem.Value = false;
            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Selection cancelled");
            return new PlayerActionResult(PlayerActionStatus.Cancelled);
        }

        PlayerActionResult MarkReady()
        {
            var check = Admission.CheckSharedActionWindow();
            if (!check.Succeeded) return check;
            CancelPendingMiniGame();
            _readyServerTick = NetworkManager.ServerTime.Tick;
            _actionQueue.SetReady(Time.time);
            IsReady.Value = true;
            IsFanActive.Value = false;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Ready pressed (hasItem={HasSelectedItem.Value}, tick={_readyServerTick})");
            return new PlayerActionResult(PlayerActionStatus.ReadyAccepted);
        }

        // ─── Presentation ACK ────────────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void PresentationAckServerRpc(uint sequence, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!IsParticipantReady || IsBotControlled || rpcParams.Receive.SenderClientId != OwnerClientId) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugSuppressPresentationAck) return;
#endif
            _turnContext?.ReceivePresentationAck(
                sequence, rpcParams.Receive.SenderClientId);
        }

        // ─── Cosmetic Sync ──────────────────────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SubmitCosmeticRpc(string dto, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!IsParticipantReady || IsBotControlled || rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (_hasAcceptedCosmetic)
            {
                Debug.LogWarning($"[PlayerState P{SyncedPlayerIndex.Value}] SubmitCosmetic rejected: already accepted");
                return;
            }

            var service = CosmeticProfileService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[PlayerState] CosmeticProfileService not found — rejecting cosmetic");
                return;
            }

            if (service.TryValidateAndCanonicalizeDto(dto, out var canonical))
            {
                CosmeticDataNV.Value = canonical;
                _hasAcceptedCosmetic = true;
            }
            else
            {
                Debug.LogWarning($"[PlayerState P{SyncedPlayerIndex.Value}] SubmitCosmetic rejected: validation failed");
            }
        }

        void OnCosmeticNVChanged(FixedString128Bytes prev, FixedString128Bytes cur)
        {
            if (!IsParticipantReady || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetBinding(PlayerIndex, out var binding) || !ReferenceEquals(binding, _binding)) return;
            var previousStatus = _cosmeticSubmission?.Status;
            _cosmeticSubmission?.Observe(cur.ToString());
            ReportCosmeticStatus(previousStatus);
            if (perspective.IsHumanSeat(PlayerIndex))
            {
                FPSVisualController.Instance?.ApplyCosmeticDto(cur.ToString());
                return;
            }
            var visual = GetComponent<AZPlayerVisual>();
            if (visual != null)
                visual.ApplyRemoteCosmetic(cur.ToString());
        }

        // ─── 도발 이모티콘 ──────────────────────────────────────

        double _lastEmoteServerTime = -100.0;
        public double LastEmoteServerTime => _lastEmoteServerTime;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SendEmoteServerRpc(byte emoteId, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (!IsParticipantReady || IsBotControlled || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (_turnContext == null || !_turnContext.CanAcceptEmotes) return;
            if (emoteId >= EmoteCatalog.Count) return;

            _lastEmoteServerTime = NetworkManager.ServerTime.Time;
            ShowEmoteClientRpc(emoteId);
        }

        [Rpc(SendTo.Everyone)]
        void ShowEmoteClientRpc(byte emoteId)
        {
            if (!IsParticipantReady || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetBinding(PlayerIndex, out var binding) || !ReferenceEquals(binding, _binding)
                || perspective.IsHumanSeat(PlayerIndex)) return;

            var visual = GetComponent<AZPlayerVisual>();
            Transform root = visual != null ? visual.GetVisualRoot() : null;
            if (root == null) return;

            OnEmoteRequested?.Invoke(root, root.position, emoteId);
        }
    }
}
