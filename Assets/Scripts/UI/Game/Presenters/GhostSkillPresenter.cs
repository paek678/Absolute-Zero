using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game.Presenters
{
    public sealed class GhostSkillPresenter : MonoBehaviour
    {
        IGameDataBridge _bridge;
        ILocalPlayerCommands _commands;

        GameObject _panel;
        Button _frostBtn;
        TextMeshProUGUI _frostCdText;
        Button _chillBtn;
        TextMeshProUGUI _chillCdText;
        TextMeshProUGUI _statusText;

        byte _pendingSkill = byte.MaxValue;
        bool _selectingTarget;

        byte _lastSentSkill = byte.MaxValue;
        float _skillPendingTimer;
        uint _nextRequestId;
        uint _lastRequestId;
        const float SKILL_PENDING_TIMEOUT = 2f;

        NetworkList<GhostCooldownNetData> _subscribedList;
        MatchNetworkState _subscribedState;
        MatchCompositionRoot _root;
        bool _attached;
        bool IsCurrent => _root != null && _root == MatchCompositionRoot.Instance
            && _root.IsSessionCurrent && _root.gameObject.scene == gameObject.scene;

        public void Initialize(IGameDataBridge bridge, ILocalPlayerCommands commands, GameHudRefs refs)
        {
            _bridge = bridge;
            _commands = commands;
            _root = MatchCompositionRoot.Instance;

            _panel = refs.GhostSkillPanel;
            _frostBtn = refs.FrostStrikeButton;
            _frostCdText = refs.FrostStrikeCooldownText;
            _chillBtn = refs.ChillAuraButton;
            _chillCdText = refs.ChillAuraCooldownText;
            _statusText = refs.GhostStatusText;

            if (_frostBtn != null) _frostBtn.onClick.AddListener(OnFrostStrikeClicked);
            if (_chillBtn != null) _chillBtn.onClick.AddListener(OnChillAuraClicked);

            Attach();
        }

        void OnEnable() => Attach();
        void Attach()
        {
            if (_bridge == null || _attached || !isActiveAndEnabled || !IsCurrent) return;
            _attached = true;
            _bridge.OnPhaseChanged += OnPhaseChanged;
            _bridge.OnMatchSnapshotChanged += OnMatchSnapshotChanged;
            TurnManager.OnGhostSkillRequestResult += OnSkillRequestResult;

            TrySubscribeCooldownList();
            UpdateVisibility(_bridge.CurrentMatch.CurrentPhase);
            UpdateCooldowns();
        }

        void OnDisable()
        {
            _attached = false;
            if (_bridge != null)
            {
                _bridge.OnPhaseChanged -= OnPhaseChanged;
                _bridge.OnMatchSnapshotChanged -= OnMatchSnapshotChanged;
            }
            UnsubscribeCooldownList();
            UnsubscribeState();
            TurnManager.OnGhostSkillRequestResult -= OnSkillRequestResult;
            CancelTargetSelection();
            ClearPending();
            if (_panel != null) _panel.SetActive(false);
        }

        void OnDestroy()
        {
            OnDisable();
            if (_frostBtn != null) _frostBtn.onClick.RemoveListener(OnFrostStrikeClicked);
            if (_chillBtn != null) _chillBtn.onClick.RemoveListener(OnChillAuraClicked);
        }

        void TrySubscribeCooldownList()
        {
            var mcr = _root;
            if (!IsCurrent || mcr.NetworkState == null)
            {
                UnsubscribeCooldownList();
                UnsubscribeState();
                return;
            }
            if (_subscribedState != mcr.NetworkState)
            {
                UnsubscribeState();
                _subscribedState = mcr.NetworkState;
                _subscribedState.GhostPossessionSpentMask.OnValueChanged += OnPossessionSpentChanged;
            }
            var current = mcr.NetworkState.GhostCooldowns;
            if (_subscribedList == current) return;
            UnsubscribeCooldownList();
            _subscribedList = current;
            _subscribedList.OnListChanged += OnCooldownListChanged;
        }

        void UnsubscribeCooldownList()
        {
            if (_subscribedList != null)
            {
                _subscribedList.OnListChanged -= OnCooldownListChanged;
                _subscribedList = null;
            }
        }

        void OnCooldownListChanged(NetworkListEvent<GhostCooldownNetData> changeEvent)
        {
            CheckPendingResolved();
            UpdateCooldowns();
        }

        void OnPossessionSpentChanged(byte previous, byte current)
        {
            CheckPendingResolved();
            UpdateCooldowns();
        }

        void OnSkillRequestResult(uint requestId, GhostSkillRequestResult result)
        {
            if (!_attached || !IsCurrent || requestId != _lastRequestId) return;
            ClearPending();
            if (result != GhostSkillRequestResult.Accepted && _statusText != null)
                _statusText.text = $"스킬 사용 불가: {result}";
            UpdateCooldowns();
        }

        void UnsubscribeState()
        {
            if (_subscribedState == null) return;
            _subscribedState.GhostPossessionSpentMask.OnValueChanged -= OnPossessionSpentChanged;
            _subscribedState = null;
        }

        void OnPhaseChanged(TurnPhase oldPhase, TurnPhase newPhase)
        {
            UpdateVisibility(newPhase);
            CancelTargetSelection();
            ClearPending();
            UpdateCooldowns();
        }

        void OnMatchSnapshotChanged(MatchSnapshot match)
        {
            TrySubscribeCooldownList();
            UpdateVisibility(_bridge.CurrentMatch.CurrentPhase);
            UpdateCooldowns();
        }

        void UpdateVisibility(TurnPhase phase)
        {
            if (_panel == null) return;

            bool show = phase == TurnPhase.PrepPhase && IsLocalGhost();
            bool wasActive = _panel.activeInHierarchy;
            _panel.SetActive(show);

            if (wasActive && !show)
                CancelTargetSelection();
        }

        bool IsLocalGhost()
        {
            var match = _bridge.CurrentMatch;
            byte seat = _bridge.LocalSeatIndex;
            if (match.LifeStates == null || seat >= match.LifeStates.Length) return false;
            return match.LifeStates[seat] == LifeState.Ghost;
        }

        bool IsSkillPending => _lastSentSkill != byte.MaxValue;

        void CheckPendingResolved()
        {
            if (!IsSkillPending) return;
            var mcr = MatchCompositionRoot.Instance;
            if (mcr == null || mcr.NetworkState == null) return;
            byte seat = _bridge.LocalSeatIndex;
            if (GetCooldown(mcr.NetworkState, seat, _lastSentSkill) > 0
                || (_lastSentSkill == GhostSkillService.SKILL_POSSESSION
                    && (mcr.NetworkState.GhostPossessionSpentMask.Value & (1 << seat)) != 0))
                ClearPending();
        }

        void ClearPending()
        {
            _lastSentSkill = byte.MaxValue;
            _skillPendingTimer = 0f;
        }

        void UpdateCooldowns()
        {
            byte seat = _bridge.LocalSeatIndex;
            var mcr = MatchCompositionRoot.Instance;
            if (mcr == null || mcr.NetworkState == null)
            {
                SetButtonsDisabled();
                return;
            }

            var nState = mcr.NetworkState;
            byte frostCd = GetCooldown(nState, seat, GhostSkillService.SKILL_GRUDGE);
            bool possessionSpent = (nState.GhostPossessionSpentMask.Value & (1 << seat)) != 0;

            bool pending = IsSkillPending;
            if (_frostBtn != null)
                _frostBtn.interactable = frostCd == 0 && !pending;
            if (_chillBtn != null)
                _chillBtn.interactable = !possessionSpent && !pending;

            if (_frostCdText != null)
                _frostCdText.text = frostCd > 0
                    ? $"사용 가능: T+{frostCd}" : "";
            if (_chillCdText != null)
                _chillCdText.text = possessionSpent ? "사용 완료" : "경기당 1회";
        }

        void SetButtonsDisabled()
        {
            if (_frostBtn != null) _frostBtn.interactable = false;
            if (_chillBtn != null) _chillBtn.interactable = false;
        }

        static byte GetCooldown(MatchNetworkState nState, byte seat, byte skill)
        {
            for (int i = 0; i < nState.GhostCooldowns.Count; i++)
            {
                var cd = nState.GhostCooldowns[i];
                if (cd.Seat == seat && cd.Skill == skill)
                    return cd.RemainingTurns;
            }
            return 0;
        }

        void OnFrostStrikeClicked()
        {
            BeginTargetSelection(GhostSkillService.SKILL_GRUDGE);
        }

        void OnChillAuraClicked()
        {
            BeginTargetSelection(GhostSkillService.SKILL_POSSESSION);
        }

        void BeginTargetSelection(byte skillIndex)
        {
            if (_bridge.CurrentMatch.CurrentPhase != TurnPhase.PrepPhase) return;
            if (!IsLocalGhost()) return;
            if (IsSkillPending) return;

            var mcr = MatchCompositionRoot.Instance;
            if (mcr != null && mcr.NetworkState != null)
            {
                byte seat = _bridge.LocalSeatIndex;
                if (GetCooldown(mcr.NetworkState, seat, skillIndex) > 0) return;
                if (skillIndex == GhostSkillService.SKILL_POSSESSION
                    && (mcr.NetworkState.GhostPossessionSpentMask.Value & (1 << seat)) != 0) return;
            }

            _pendingSkill = skillIndex;
            _selectingTarget = true;
            if (_statusText != null)
            {
                string skillName = skillIndex == GhostSkillService.SKILL_GRUDGE
                    ? "귀신의 한" : "빙의";
                _statusText.text = $"{skillName} — click a target";
            }
        }

        void CancelTargetSelection()
        {
            _selectingTarget = false;
            _pendingSkill = byte.MaxValue;
            if (_statusText != null)
                _statusText.text = "Ghost Mode — select a skill";
        }

        void Update()
        {
            if (!IsCurrent) { if (_attached) OnDisable(); return; }
            if (IsSkillPending)
            {
                _skillPendingTimer += Time.unscaledDeltaTime;
                if (_skillPendingTimer >= SKILL_PENDING_TIMEOUT)
                {
                    ClearPending();
                    UpdateCooldowns();
                }
            }

            if (!_selectingTarget) return;
            if (_panel == null || !_panel.activeInHierarchy) return;

            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelTargetSelection();
                return;
            }

            if (UnityEngine.InputSystem.Mouse.current == null
                || !UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            byte target = FindClickedAliveSeat();
            if (target == byte.MaxValue) return;

            _lastSentSkill = _pendingSkill;
            _lastRequestId = ++_nextRequestId;
            if (_lastRequestId == 0) _lastRequestId = ++_nextRequestId;
            _skillPendingTimer = 0f;
            _commands.UseGhostSkill(_pendingSkill, target, _lastRequestId);
            CancelTargetSelection();
            UpdateCooldowns();
        }

        byte FindClickedAliveSeat()
        {
            var cam = Camera.main;
            if (cam == null) return byte.MaxValue;

            var mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            var ray = cam.ScreenPointToRay(mousePos);
            var hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);

            var match = _bridge.CurrentMatch;
            byte localSeat = _bridge.LocalSeatIndex;

            float closestDist = float.MaxValue;
            byte closestSeat = byte.MaxValue;

            foreach (var hit in hits)
            {
                var marker = hit.collider.GetComponentInParent<PlayerSeatMarker>();
                if (marker == null || marker.Player == null) continue;

                byte seat = marker.SeatIndex;
                if (seat == localSeat) continue;
                if (match.LifeStates == null || seat >= match.LifeStates.Length) continue;
                if (match.LifeStates[seat] != LifeState.Alive) continue;

                if (hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                    closestSeat = seat;
                }
            }
            return closestSeat;
        }
    }
}
