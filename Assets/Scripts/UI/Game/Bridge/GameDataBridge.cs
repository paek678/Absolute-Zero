using System;
using System.Collections;
using System.Collections.Generic;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.UI.Game.Bridge
{
    public sealed class GameDataBridge : MonoBehaviour, IGameDataBridge
    {
        readonly Dictionary<byte, SeatSnapshot> _seats = new();
        readonly HashSet<byte> _dirtySeatIndices = new();
        bool _matchDirty;
        readonly MatchResultBuffer _results = new();

        MatchSnapshot _currentMatch;
        byte _localSeatIndex = byte.MaxValue;
        bool _localSeatResolved;

        bool _roundResultPending;
        float _roundResultTimer;
        bool _matchEndPending;
        float _matchEndTimer;
        const float SETTLE_TIME = 0.15f;

        IReadOnlyPlayerRegistry _registry;
        TurnManager _tm;
        MatchManager _mm;
        MatchNetworkState _mns;
        MatchCompositionRoot _root;
        CombatVFXManager _vfx;
        Coroutine _managerWait;
        bool _binding;
        bool _initialized;
        bool IsMatchCurrent => _root != null && _root == MatchCompositionRoot.Instance
            && _root.IsSessionCurrent && _root.InitializationFailure == null
            && ReferenceEquals(_root.Registry, _registry)
            && _root.gameObject.scene == gameObject.scene;

        readonly Dictionary<byte, PlayerState> _boundStates = new();
        readonly Dictionary<byte, PlayerIdentity> _boundIdentities = new();
        readonly Dictionary<byte, SeatCallbacks> _seatCallbacks = new();
        readonly Dictionary<byte, LifeState> _cachedLifeStates = new();

        struct SeatCallbacks
        {
            public NetworkVariable<float>.OnValueChangedDelegate OnTemp;
            public NetworkVariable<float>.OnValueChangedDelegate OnFanSpeed;
            public NetworkVariable<bool>.OnValueChangedDelegate OnReady;
            public NetworkVariable<bool>.OnValueChangedDelegate OnFanActive;
            public NetworkVariable<bool>.OnValueChangedDelegate OnFanUpgraded;
            public NetworkVariable<bool>.OnValueChangedDelegate OnBasicBlocked;
            public NetworkVariable<bool>.OnValueChangedDelegate OnHasSelected;
            public NetworkVariable<LifeState>.OnValueChangedDelegate OnLifeState;
        }

        public int SeatCount => _seats.Count;
        public byte LocalSeatIndex => _localSeatIndex;
        public MatchSnapshot CurrentMatch => _currentMatch;
        public bool TryGetLatestResult(out MatchResultNotice result)
        {
            result = default;
            return (!_initialized || (_binding && IsMatchCurrent && _localSeatResolved))
                && _results.TryRead(out result);
        }

        public bool TryGetDisplayTemperature(byte seat, out float value)
        {
            value = default;
            return _vfx != null && IsMatchCurrent && _vfx.TryGetDisplayTemperature(seat, out value);
        }

        public event Action<byte, SeatSnapshot> OnSeatSnapshotChanged;
        public event Action<MatchSnapshot> OnMatchSnapshotChanged;
        public event Action<byte, SeatSnapshot> OnSeatRegistered;
        public event Action<byte> OnSeatUnregistered;
        public event Action<TurnPhase, TurnPhase> OnPhaseChanged;
        public event Action<EnvironmentType> OnEnvironmentAnnounced;
        public event Action<byte, float> OnTempOverride;
        public event Action OnTempOverridesClear;
        public event Action<bool> OnLocalHasSelectedItemChanged;
        public event Action<byte, short> OnOpponentRevealed;
        public event Action<int> OnCurrentAttackerChanged;
        public event Action<MatchSnapshot> OnRoundResult;
        public event Action<MatchSnapshot> OnMatchEnd;
        public event Action<byte> OnRematchDecisionChanged;

        public bool TryGetSeat(byte seat, out SeatSnapshot snapshot)
        {
            return _seats.TryGetValue(seat, out snapshot);
        }

        public void Initialize(IReadOnlyPlayerRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (ReferenceEquals(_registry, registry)) return;
            if (_registry != null) throw new InvalidOperationException("A scene bridge cannot be rebound to another match registry.");
            _root = MatchCompositionRoot.Instance;
            if (_root == null || !ReferenceEquals(_root.Registry, registry)
                || _root.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException("Bridge requires its own scene's match registry.");
            _registry = registry;
            _initialized = true;
            BeginBinding();
        }

        void OnEnable() => BeginBinding();

        void BeginBinding()
        {
            if (!_initialized || !isActiveAndEnabled || _binding || !IsMatchCurrent) return;
            _binding = true;
            _registry.Registered += OnPlayerRegistered;
            _registry.Unregistered += OnPlayerUnregistered;

            foreach (var p in _registry.Players)
                BindSeat(p);

            _managerWait = StartCoroutine(WaitForManagers());
        }

        IEnumerator WaitForManagers()
        {
            float deadline = Time.realtimeSinceStartup + MatchCompositionRoot.InitializationTimeout;
            while (IsMatchCurrent && isActiveAndEnabled)
            {
                var turn = TurnManager.Instance;
                var match = _root.MatchManager;
                if (turn != null && turn.IsSpawned && turn.gameObject.scene == _root.gameObject.scene
                    && match != null && match.IsSpawned)
                {
                    _tm = turn;
                    _mm = match;
                    _mns = _root.NetworkState;
                    var visual = CombatVFXManager.Instance;
                    _vfx = visual != null && visual.gameObject.scene == _root.gameObject.scene ? visual : null;
                    SubscribeTurnManager();
                    SubscribeMatchManager();
                    if (_mns != null) SubscribeMatchNetworkState();
                    var previousPhase = _currentMatch.CurrentPhase;
                    ReadCurrentMatchValues();
                    if (_currentMatch.MatchState != MatchState.MatchComplete
                        && _currentMatch.CurrentPhase == TurnPhase.PrepPhase)
                    {
                        _results.Clear();
                        _roundResultPending = false;
                        _matchEndPending = false;
                        // A disabled view can miss an entire round and return to Prep.
                        OnPhaseChanged?.Invoke(previousPhase, _currentMatch.CurrentPhase);
                    }
                    _managerWait = null;
                    yield break;
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning("[GameDataBridge] Current match managers did not become ready before the binding deadline.");
                    break;
                }
                yield return null;
            }
            _managerWait = null;
            EndBinding();
        }

        void SubscribeTurnManager()
        {
            _tm.CurrentPhase.OnValueChanged += OnPhaseNVChanged;
            _tm.TurnNumber.OnValueChanged += OnMatchNVChanged_Int;
            _tm.RemainingTime.OnValueChanged += OnMatchNVChanged_Int;
            _tm.PrepDuration.OnValueChanged += OnMatchNVChanged_Float;
            _tm.LastRoundWinner.OnValueChanged += OnMatchNVChanged_Int;
            _tm.ActiveEnvironment.OnValueChanged += OnMatchNVChanged_Env;
            _tm.FirstReadySeat.OnValueChanged += OnMatchNVChanged_Byte;

            TurnManager.OnEnvironmentAnnounced += HandleEnvironmentAnnounced;
            _tm.OnOpponentRevealed += HandleOpponentRevealed;
            _tm.OnMultiMatchOutcome += HandleMultiMatchOutcome;
            if (_vfx != null)
            {
                _vfx.OnTempOverridesClear += HandleTempOverridesClear;
                _vfx.OnTempTargetsOverride += HandleTempTargetsOverride;
                _vfx.OnPlayerTempOverride += HandlePlayerTempOverride;
                HandleTempOverridesClear();
                foreach (var seat in _seats.Keys)
                    if (_vfx.TryGetDisplayTemperature(seat, out var value))
                        HandlePlayerTempOverride(seat, value);
            }
            CombatVFXManager.OnAttackerChanged += HandleAttackerChanged;
            CombatVFXManager.OnPresentationSettled += HandlePresentationSettled;
        }

        void SubscribeMatchManager()
        {
            _mm.RoundNumber.OnValueChanged += OnMatchNVChanged_Int;
            _mm.P1RoundWins.OnValueChanged += OnMatchNVChanged_Int;
            _mm.P2RoundWins.OnValueChanged += OnMatchNVChanged_Int;
            _mm.CurrentMatchState.OnValueChanged += OnMatchStateNVChanged;
            _mm.RematchDecisionMask.OnValueChanged += OnRematchDecisionMaskChanged;
            _mm.RematchDeadlineServerTime.OnValueChanged += OnMatchNVChanged_Double;
            _mm.RematchVoteEpoch.OnValueChanged += OnMatchNVChanged_UInt;
        }

        void SubscribeMatchNetworkState()
        {
            _mns.Config.OnValueChanged += OnConfigNVChanged;
            _mns.TerminalResult.OnValueChanged += OnTerminalResultChanged;
            _mns.KillScores.OnListChanged += OnKillScoresChanged;
        }

        void OnConfigNVChanged(MatchConfigNetData _, MatchConfigNetData __) => _matchDirty = true;
        void OnKillScoresChanged(NetworkListEvent<int> _) => _matchDirty = true;
        void OnTerminalResultChanged(MultiTerminalResultNetData _, MultiTerminalResultNetData newValue)
        {
            _matchDirty = true;
            _matchEndPending = newValue.IsValid;
            if (newValue.IsValid)
                _roundResultPending = false;
        }

        void HandlePresentationSettled(uint sequence)
        {
            if (_currentMatch.Mode != GameMode.Multi) return;
            ReadMatchNetworkStateValues();
            if (HasValidMultiTerminal()
                && _currentMatch.MultiDecidingSequence == sequence)
                _matchEndPending = true;
        }

        void ReadCurrentMatchValues()
        {
            _currentMatch = new MatchSnapshot
            {
                CurrentPhase = _tm.CurrentPhase.Value,
                TurnNumber = _tm.TurnNumber.Value,
                RemainingTime = _tm.RemainingTime.Value,
                PrepDuration = _tm.PrepDuration.Value,
                ActiveEnvironment = _tm.ActiveEnvironment.Value,
                LastRoundWinner = _tm.LastRoundWinner.Value,
                FirstReadySeat = _tm.FirstReadySeat.Value,
                RoundNumber = _mm.RoundNumber.Value,
                P1RoundWins = _mm.P1RoundWins.Value,
                P2RoundWins = _mm.P2RoundWins.Value,
                MatchState = _mm.CurrentMatchState.Value,
                RematchDecisionMask = _mm.RematchDecisionMask.Value,
                RematchDeadlineServerTime = _mm.RematchDeadlineServerTime.Value,
                RematchVoteEpoch = _mm.RematchVoteEpoch.Value,
            };
            ReadMatchNetworkStateValues();
            _matchDirty = true;

            if (_currentMatch.MatchState == MatchState.MatchComplete)
                _matchEndPending = true;
            else if (_currentMatch.CurrentPhase == TurnPhase.RoundOver
                && !HasValidMultiTerminal())
                _roundResultPending = true;
        }

        bool HasValidMultiTerminal()
        {
            return _currentMatch.MultiDecidingSequence != 0
                && _currentMatch.MultiOutcome != MultiMatchOutcome.InProgress
                && _currentMatch.MultiWinnerMask != 0;
        }

        void ReadMatchNetworkStateValues()
        {
            if (_mns == null) return;
            var cfg = _mns.Config.Value;
            _currentMatch.Mode = (GameMode)cfg.Mode;
            _currentMatch.RequiredPlayerCount = cfg.RequiredPlayerCount;
            int count = _mns.KillScores.Count;
            if (count > 0)
            {
                _currentMatch.KillScores = new int[count];
                for (int i = 0; i < count; i++)
                    _currentMatch.KillScores[i] = _mns.KillScores[i];
            }

            int pc = cfg.RequiredPlayerCount > 0 ? cfg.RequiredPlayerCount : 2;
            var ls = new LifeState[pc];
            for (int i = 0; i < pc; i++)
            {
                byte seat = (byte)i;
                if (_boundStates.TryGetValue(seat, out var boundPs))
                    ls[i] = boundPs.CurrentLifeState.Value;
                else if (_cachedLifeStates.TryGetValue(seat, out var cached))
                    ls[i] = cached;
                else
                    ls[i] = LifeState.Alive;
            }
            _currentMatch.LifeStates = ls;
            var terminal = _mns.TerminalResult.Value;
            _currentMatch.MultiOutcome = terminal.Outcome;
            _currentMatch.MultiWinnerMask = terminal.WinnerMask;
            _currentMatch.MultiDecidingSequence = terminal.DecidingSequence;
            _currentMatch.MultiResultReleased = terminal.Released;
        }

        // ─── Seat binding ────────────────────────────────────────

        void OnPlayerRegistered(PlayerBinding binding)
        {
            BindSeat(binding);
        }

        void OnPlayerUnregistered(PlayerIdentity identity)
        {
            if (!_boundIdentities.TryGetValue(identity.PlayerIndex, out var bound) || bound != identity) return;
            UnbindSeat(identity.PlayerIndex);
            ResolveLocalSeat();
            OnSeatUnregistered?.Invoke(identity.PlayerIndex);
        }

        void BindSeat(PlayerBinding binding)
        {
            if (binding == null || !binding.IsValid || !binding.HasIdentity) return;
            byte idx = binding.Identity.PlayerIndex;
            if (_boundStates.TryGetValue(idx, out var existing))
            {
                if (existing == binding.State && _boundIdentities.TryGetValue(idx, out var identity) && identity == binding.Identity)
                { ResolveLocalSeat(); return; }
                UnbindSeat(idx);
            }

            var ps = binding.State;
            _boundStates[idx] = ps;
            _boundIdentities[idx] = binding.Identity;

            var cb = new SeatCallbacks
            {
                OnTemp = (_, _) => MarkSeatDirty(idx),
                OnFanSpeed = (_, _) => MarkSeatDirty(idx),
                OnReady = (_, _) => MarkSeatDirty(idx),
                OnFanActive = (_, _) => MarkSeatDirty(idx),
                OnFanUpgraded = (_, _) => MarkSeatDirty(idx),
                OnBasicBlocked = (_, _) => MarkSeatDirty(idx),
                OnHasSelected = (_, cur) =>
                {
                    MarkSeatDirty(idx);
                    if (IsLocalSeat(idx))
                        OnLocalHasSelectedItemChanged?.Invoke(cur);
                },
                OnLifeState = (_, _) => _matchDirty = true
            };
            _seatCallbacks[idx] = cb;

            ps.Temperature.OnValueChanged += cb.OnTemp;
            ps.FanSpeed.OnValueChanged += cb.OnFanSpeed;
            ps.IsReady.OnValueChanged += cb.OnReady;
            ps.IsFanActive.OnValueChanged += cb.OnFanActive;
            ps.IsFanUpgraded.OnValueChanged += cb.OnFanUpgraded;
            ps.IsBasicBlocked.OnValueChanged += cb.OnBasicBlocked;
            ps.HasSelectedItem.OnValueChanged += cb.OnHasSelected;
            ps.CurrentLifeState.OnValueChanged += cb.OnLifeState;

            ResolveLocalSeat();
            BuildSeatSnapshot(idx, ps);
            OnSeatRegistered?.Invoke(idx, _seats[idx]);
        }

        void UnbindSeat(byte idx)
        {
            if (_boundStates.TryGetValue(idx, out var ps) && ps != null && _seatCallbacks.TryGetValue(idx, out var cb))
            {
                _cachedLifeStates[idx] = ps.CurrentLifeState.Value;

                ps.Temperature.OnValueChanged -= cb.OnTemp;
                ps.FanSpeed.OnValueChanged -= cb.OnFanSpeed;
                ps.IsReady.OnValueChanged -= cb.OnReady;
                ps.IsFanActive.OnValueChanged -= cb.OnFanActive;
                ps.IsFanUpgraded.OnValueChanged -= cb.OnFanUpgraded;
                ps.IsBasicBlocked.OnValueChanged -= cb.OnBasicBlocked;
                ps.HasSelectedItem.OnValueChanged -= cb.OnHasSelected;
                ps.CurrentLifeState.OnValueChanged -= cb.OnLifeState;
            }
            _seatCallbacks.Remove(idx);
            _seats.Remove(idx);
            _boundStates.Remove(idx);
            _boundIdentities.Remove(idx);
            _dirtySeatIndices.Remove(idx);
            _matchDirty = true;
        }

        void ResolveLocalSeat()
        {
            byte previous = _localSeatIndex;
            bool wasResolved = _localSeatResolved;
            LocalMatchPerspective perspective = default;
            _localSeatResolved = _registry != null
                && ReferenceEquals(MatchCompositionRoot.Instance?.Registry, _registry)
                && LocalMatchPerspective.TryResolveCurrent(out perspective);
            _localSeatIndex = _localSeatResolved ? perspective.HumanSeat : byte.MaxValue;
            if (previous == _localSeatIndex && wasResolved == _localSeatResolved) return;
            _matchDirty = true;
            foreach (byte seat in _boundStates.Keys) MarkSeatDirty(seat);
        }

        bool IsLocalSeat(byte idx) => _localSeatResolved && idx == _localSeatIndex;

        void BuildSeatSnapshot(byte idx, PlayerState ps)
        {
            if (ps == null || !_boundIdentities.TryGetValue(idx, out var identity)) return;
            _seats[idx] = new SeatSnapshot
            {
                SeatIndex = idx,
                ClientId = identity.ClientId,
                ControllerKind = identity.ControllerKind,
                Temperature = ps.Temperature.Value,
                FanSpeed = ps.FanSpeed.Value,
                IsReady = ps.IsReady.Value,
                IsFanActive = ps.IsFanActive.Value,
                IsFanUpgraded = ps.IsFanUpgraded.Value,
                IsBasicBlocked = ps.IsBasicBlocked.Value,
                HasSelectedItem = ps.HasSelectedItem.Value,
                IsLocal = IsLocalSeat(idx),
            };
        }

        void MarkSeatDirty(byte idx) => _dirtySeatIndices.Add(idx);

        // ─── Match NV handlers ─────────────────────────────────────

        void OnPhaseNVChanged(TurnPhase oldVal, TurnPhase newVal)
        {
            _currentMatch.CurrentPhase = newVal;
            if (newVal == TurnPhase.PrepPhase)
            {
                _results.Clear();
                _roundResultPending = false;
                _matchEndPending = false;
            }
            _matchDirty = true;
            ResolveLocalSeat();
            FlushSeats();
            OnPhaseChanged?.Invoke(oldVal, newVal);

            if (newVal == TurnPhase.RoundOver)
            {
                _roundResultPending = true;
                _roundResultTimer = 0f;
            }
        }

        void OnMatchNVChanged_Int(int _, int __) => _matchDirty = true;
        void OnMatchNVChanged_Float(float _, float __) => _matchDirty = true;
        void OnMatchNVChanged_Env(EnvironmentType _, EnvironmentType __) => _matchDirty = true;
        void OnMatchNVChanged_Byte(byte _, byte __) => _matchDirty = true;
        void OnMatchNVChanged_Double(double _, double __) => _matchDirty = true;
        void OnMatchNVChanged_UInt(uint _, uint __) => _matchDirty = true;

        void OnRematchDecisionMaskChanged(byte _, byte newVal)
        {
            _matchDirty = true;
            OnRematchDecisionChanged?.Invoke(newVal);
        }

        void OnMatchStateNVChanged(MatchState _, MatchState newVal)
        {
            _matchDirty = true;
            if (newVal == MatchState.MatchComplete)
            {
                _matchEndPending = true;
                _matchEndTimer = 0f;
            }
        }

        // ─── Edge event handlers (immediate) ───────────────────────

        void HandleEnvironmentAnnounced(EnvironmentType env) => OnEnvironmentAnnounced?.Invoke(env);
        void HandleOpponentRevealed(byte seat, short itemId) => OnOpponentRevealed?.Invoke(seat, itemId);

        void HandleMultiMatchOutcome(Core.Match.MultiMatchOutcome outcome, byte winnerMask)
        {
            _currentMatch.MultiOutcome = outcome;
            _currentMatch.MultiWinnerMask = winnerMask;
        }
        void HandleAttackerChanged(int seatIdx) => OnCurrentAttackerChanged?.Invoke(seatIdx);
        void HandleTempOverridesClear() => OnTempOverridesClear?.Invoke();

        void HandleTempTargetsOverride(float p1Temp, float p2Temp)
        {
            OnTempOverride?.Invoke(0, p1Temp);
            OnTempOverride?.Invoke(1, p2Temp);
        }

        void HandlePlayerTempOverride(int playerIdx, float temp)
        {
            OnTempOverride?.Invoke((byte)playerIdx, temp);
        }

        // ─── LateUpdate flush ──────────────────────────────────────

        void LateUpdate()
        {
            if (_initialized && !IsMatchCurrent)
            {
                EndBinding();
                return;
            }
            if (_initialized && (!_binding || _tm == null || _mm == null)) return;
            ResolveLocalSeat();
            FlushSeats();
            FlushMatch();
            ProcessRoundResult();
            ProcessMatchEnd();
        }

        void FlushSeats()
        {
            if (_dirtySeatIndices.Count == 0) return;

            foreach (byte idx in _dirtySeatIndices)
            {
                if (!_boundStates.TryGetValue(idx, out var ps)) continue;
                BuildSeatSnapshot(idx, ps);
                OnSeatSnapshotChanged?.Invoke(idx, _seats[idx]);
            }
            _dirtySeatIndices.Clear();
        }

        void FlushMatch()
        {
            if (!_matchDirty) return;
            _matchDirty = false;

            if (_tm != null)
            {
                _currentMatch.CurrentPhase = _tm.CurrentPhase.Value;
                _currentMatch.TurnNumber = _tm.TurnNumber.Value;
                _currentMatch.RemainingTime = _tm.RemainingTime.Value;
                _currentMatch.PrepDuration = _tm.PrepDuration.Value;
                _currentMatch.ActiveEnvironment = _tm.ActiveEnvironment.Value;
                _currentMatch.LastRoundWinner = _tm.LastRoundWinner.Value;
                _currentMatch.FirstReadySeat = _tm.FirstReadySeat.Value;
            }
            if (_mm != null)
            {
                _currentMatch.RoundNumber = _mm.RoundNumber.Value;
                _currentMatch.P1RoundWins = _mm.P1RoundWins.Value;
                _currentMatch.P2RoundWins = _mm.P2RoundWins.Value;
                _currentMatch.MatchState = _mm.CurrentMatchState.Value;
                _currentMatch.RematchDecisionMask = _mm.RematchDecisionMask.Value;
                _currentMatch.RematchDeadlineServerTime = _mm.RematchDeadlineServerTime.Value;
                _currentMatch.RematchVoteEpoch = _mm.RematchVoteEpoch.Value;
            }
            ReadMatchNetworkStateValues();

            OnMatchSnapshotChanged?.Invoke(_currentMatch);
        }

        void ProcessRoundResult()
        {
            if (!_roundResultPending || !_localSeatResolved) return;

            if (_currentMatch.Mode == GameMode.Multi)
            {
                ReadMatchNetworkStateValues();
                if (HasValidMultiTerminal()
                    || _currentMatch.MatchState == MatchState.MatchComplete)
                {
                    _roundResultPending = false;
                    return;
                }

                // RoundEnd is written before RoundOver on the server. If the phase
                // arrives first, wait for the matching state instead of briefly
                // showing a round result for a terminal action.
                if (_currentMatch.MatchState == MatchState.RoundInProgress)
                    return;
            }

            _roundResultTimer += Time.unscaledDeltaTime;
            if (_roundResultTimer < SETTLE_TIME) return;

            _roundResultPending = false;
            PublishResult(false);
        }

        void ProcessMatchEnd()
        {
            if (!_matchEndPending || !_localSeatResolved) return;

            if (_currentMatch.Mode == GameMode.Multi)
            {
                ReadMatchNetworkStateValues();
                if (_currentMatch.MatchState != MatchState.MatchComplete
                    || !_currentMatch.MultiResultReleased
                    || _currentMatch.MultiOutcome == MultiMatchOutcome.InProgress
                    || _currentMatch.MultiWinnerMask == 0)
                    return;
                var vfx = _vfx;
                if (vfx != null && vfx.HasPendingPresentation(_currentMatch.MultiDecidingSequence))
                {
                    vfx.ForceSettleMultiPresentation(_currentMatch.MultiDecidingSequence);
                    return;
                }
                _matchEndPending = false;
                PublishResult(true);
                return;
            }

            _matchEndTimer += Time.unscaledDeltaTime;
            if (_matchEndTimer < SETTLE_TIME) return;

            _matchEndPending = false;
            PublishResult(true);
        }

        void PublishResult(bool matchEnd)
        {
            if (!_results.TryPublish(_currentMatch, matchEnd,
                _tm != null ? _tm.PrepStartServerTime.Value : 0, out var notice)) return;
            if (matchEnd) OnMatchEnd?.Invoke(notice.Snapshot);
            else OnRoundResult?.Invoke(notice.Snapshot);
        }

        // ─── Cleanup ──────────────────────────────────────────────

        void OnDisable() => EndBinding();

        void OnDestroy()
        {
            EndBinding();
            _results.Clear();
        }

        void EndBinding()
        {
            bool wasBound = _binding || _tm != null || _boundStates.Count > 0;
            if (_managerWait != null) StopCoroutine(_managerWait);
            _managerWait = null;
            _binding = false;
            foreach (var idx in new List<byte>(_boundStates.Keys))
                UnbindSeat(idx);

            if (_registry != null)
            {
                _registry.Registered -= OnPlayerRegistered;
                _registry.Unregistered -= OnPlayerUnregistered;
            }

            if (_tm != null)
            {
                _tm.CurrentPhase.OnValueChanged -= OnPhaseNVChanged;
                _tm.TurnNumber.OnValueChanged -= OnMatchNVChanged_Int;
                _tm.RemainingTime.OnValueChanged -= OnMatchNVChanged_Int;
                _tm.PrepDuration.OnValueChanged -= OnMatchNVChanged_Float;
                _tm.LastRoundWinner.OnValueChanged -= OnMatchNVChanged_Int;
                _tm.ActiveEnvironment.OnValueChanged -= OnMatchNVChanged_Env;
                _tm.FirstReadySeat.OnValueChanged -= OnMatchNVChanged_Byte;
                _tm.OnOpponentRevealed -= HandleOpponentRevealed;
                _tm.OnMultiMatchOutcome -= HandleMultiMatchOutcome;
            }

            if (_mm != null)
            {
                _mm.RoundNumber.OnValueChanged -= OnMatchNVChanged_Int;
                _mm.P1RoundWins.OnValueChanged -= OnMatchNVChanged_Int;
                _mm.P2RoundWins.OnValueChanged -= OnMatchNVChanged_Int;
                _mm.CurrentMatchState.OnValueChanged -= OnMatchStateNVChanged;
                _mm.RematchDecisionMask.OnValueChanged -= OnRematchDecisionMaskChanged;
                _mm.RematchDeadlineServerTime.OnValueChanged -= OnMatchNVChanged_Double;
                _mm.RematchVoteEpoch.OnValueChanged -= OnMatchNVChanged_UInt;
            }

            if (_mns != null)
            {
                _mns.Config.OnValueChanged -= OnConfigNVChanged;
                _mns.TerminalResult.OnValueChanged -= OnTerminalResultChanged;
                _mns.KillScores.OnListChanged -= OnKillScoresChanged;
            }

            TurnManager.OnEnvironmentAnnounced -= HandleEnvironmentAnnounced;
            if (_vfx != null)
            {
                _vfx.OnTempOverridesClear -= HandleTempOverridesClear;
                _vfx.OnTempTargetsOverride -= HandleTempTargetsOverride;
                _vfx.OnPlayerTempOverride -= HandlePlayerTempOverride;
            }
            CombatVFXManager.OnAttackerChanged -= HandleAttackerChanged;
            CombatVFXManager.OnPresentationSettled -= HandlePresentationSettled;
            _tm = null; _mm = null; _mns = null; _vfx = null;
            _localSeatResolved = false;
            _localSeatIndex = byte.MaxValue;
            _dirtySeatIndices.Clear();
            _roundResultPending = false;
            _matchEndPending = false;
            _roundResultTimer = _matchEndTimer = 0;
            if (wasBound)
            {
                OnTempOverridesClear?.Invoke();
                OnCurrentAttackerChanged?.Invoke(-1);
            }
        }
    }
}
