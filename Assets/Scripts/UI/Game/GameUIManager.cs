using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using AbsoluteZero.UI.Game.Presenters;
using AbsoluteZero.UI.MiniGame;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game
{
    public class GameUIManager : MonoBehaviour
    {
        IGameDataBridge _bridge;
        ILocalPlayerCommands _commands;
        GameHudRefs _refs;

        TemperaturePresenter _tempPresenter;
        MatchHudPresenter _matchHudPresenter;
        RoundResultPresenter _roundResultPresenter;
        OpponentBarPresenter _oppBarPresenter;
        InventoryPresenter _inventoryPresenter;
        GhostSkillPresenter _ghostSkillPresenter;

        bool _initialized;
        MiniGameHub _miniGameHub;

        // Multi target selection state
        bool _waitingForTarget;
        byte _snappedTargetSeat = byte.MaxValue;
        Transform _snappedTargetTransform;
        TargetingArrowPresenter _targetingArrowPresenter;
        const float TargetSnapEnterPixels = 105f;
        const float TargetSnapReleasePixels = 155f;
        readonly List<RaycastResult> _pointerHits = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Transform _debugHoverTarget;
#endif

        public MatchHudPresenter MatchHud => _matchHudPresenter;

        public void Initialize(IGameDataBridge bridge, ILocalPlayerCommands commands, GameHudRefs refs)
        {
            _bridge = bridge;
            _commands = commands;
            _refs = refs;

            _tempPresenter = new TemperaturePresenter(bridge, refs);

            _matchHudPresenter = gameObject.AddComponent<MatchHudPresenter>();
            _matchHudPresenter.Initialize(bridge, commands, refs);
            gameObject.AddComponent<ParticipantBannerPresenter>().Initialize(bridge, refs);
            gameObject.AddComponent<CombatRoleIconPresenter>().Initialize(bridge, refs);

            _roundResultPresenter = new RoundResultPresenter(bridge, commands, refs, this);

            _oppBarPresenter = gameObject.AddComponent<OpponentBarPresenter>();
            _oppBarPresenter.Initialize(bridge, refs);

            var fanSpawner = gameObject.GetComponent<FanSpawner>();
            if (fanSpawner == null)
                fanSpawner = gameObject.AddComponent<FanSpawner>();
            fanSpawner.SpawnStayItemFans();

            _ghostSkillPresenter = gameObject.AddComponent<GhostSkillPresenter>();
            _ghostSkillPresenter.Initialize(bridge, commands, refs);
            gameObject.AddComponent<GhostSkillVFXPresenter>();

            SpawnInventoryPresenter();
            _targetingArrowPresenter = gameObject.AddComponent<TargetingArrowPresenter>();
            _targetingArrowPresenter.Initialize(Resources.Load<Sprite>("UI/TargetingArrowRounded"), 64f);
            SpawnMiniGameHub();

            _bridge.OnPhaseChanged += HandlePhaseChanged;

            _initialized = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = System.Environment.GetCommandLineArgs();
            int scenario = System.Array.IndexOf(args, "--az-matrix");
            if (scenario >= 0 && scenario + 1 < args.Length && args[scenario + 1] == "role-cat")
                gameObject.AddComponent<CombatRoleCaptureProbe>();
            if (scenario >= 0 && scenario + 1 < args.Length && args[scenario + 1] == "input")
                gameObject.AddComponent<InventoryInteractionProbe>().Initialize(this, _inventoryPresenter, _commands);
#endif
            Debug.Log("[GameUIManager] Initialized — all presenters created");
        }

        void SpawnInventoryPresenter()
        {
            var presenterGO = new GameObject("InventoryPresenter");
            var spawnRoot = GameObject.Find("MyItemSpawnRoot");
            if (spawnRoot != null)
                presenterGO.transform.position = spawnRoot.transform.position;
            _inventoryPresenter = presenterGO.AddComponent<InventoryPresenter>();
            presenterGO.AddComponent<MultiPerspectiveLayout>();
            _inventoryPresenter.OnWorldItemClicked += OnItemClicked;
        }

        void SpawnMiniGameHub()
        {
            var hubGO = new GameObject("MiniGameHub");
            _miniGameHub = hubGO.AddComponent<MiniGameHub>();
            _miniGameHub.OnFinishedLocal += OnMiniGameFinished;
        }

        void OnEnable()
        {
            if (!_initialized) return;
            if (_miniGameHub != null)
            {
                _miniGameHub.OnFinishedLocal -= OnMiniGameFinished;
                _miniGameHub.OnFinishedLocal += OnMiniGameFinished;
                _miniGameHub.enabled = true;
            }
            _roundResultPresenter?.Resume();
        }

        void OnDisable()
        {
            if (_waitingForTarget) CancelTargetSelection();
            _targetingArrowPresenter?.Hide();
            if (_miniGameHub != null)
            {
                _miniGameHub.OnFinishedLocal -= OnMiniGameFinished;
                _miniGameHub.enabled = false;
            }
            _roundResultPresenter?.Suspend();
        }

        void Update()
        {
            if (!_initialized) return;
            _tempPresenter.Tick(Time.deltaTime);
            if (LocalGameplayInput.IsBlocked)
            {
                _targetingArrowPresenter?.Hide();
                _snappedTargetSeat = byte.MaxValue;
                _snappedTargetTransform = null;
                return;
            }

            if (_waitingForTarget)
            {
                HandleTargetSelection();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                _matchHudPresenter?.TryReadyByKey();
        }

        void HandlePhaseChanged(TurnPhase oldPhase, TurnPhase newPhase)
        {
            if (newPhase == TurnPhase.PrepPhase)
                _tempPresenter.ReconcilePrepTemperature();

            if (_waitingForTarget)
                CancelTargetSelection(clearStatus: false);
        }

        void OnItemClicked(int slotIndex)
        {
            if (LocalGameplayInput.IsBlocked) return;
            if (MiniGameHub.IsRunning) return;
            if (_inventoryPresenter == null) return;

            if (_inventoryPresenter.IsConfirmedSlot(slotIndex))
            {
                if (_commands.TryCancelSelection())
                {
                    GameAudioManager.Instance?.PlayButtonClick();
                    _matchHudPresenter.SetStatusText("선택 취소");
                }
                return;
            }

            if (_waitingForTarget)
            {
                bool sameItem = _inventoryPresenter.ResolvePendingSlotIndex() == slotIndex;
                CancelTargetSelection();
                if (sameItem) return;
            }

            var itemData = _inventoryPresenter.GetLocalItemData(slotIndex);
            if (itemData == null) return;

            bool isMulti = _bridge.CurrentMatch.Mode == GameMode.Multi;
            bool needsTarget = itemData.GetTargetMode() == TargetMode.SingleTarget;

            if (isMulti && needsTarget)
            {
                _inventoryPresenter.RequestItemConfirm(slotIndex);
                if (_inventoryPresenter.SelectionStage != ItemSelectionStage.Aiming) return;
                _waitingForTarget = true;
                _snappedTargetSeat = byte.MaxValue;
                _snappedTargetTransform = null;
                _matchHudPresenter.SetStatusText("대상을 조준한 뒤 클릭하세요");
                return;
            }

            ulong generation = _inventoryPresenter.BeginItemSubmission(slotIndex);
            if (generation == 0) return;
            bool sent = _commands.TrySelectItem((byte)slotIndex, _inventoryPresenter.GetLocalSlot(slotIndex).CopyId);
            _inventoryPresenter.CompleteItemSubmission(generation, sent);
            if (sent)
            {
                GameAudioManager.Instance?.PlayButtonClick();
                _matchHudPresenter.SetStatusText("아이템 선택 완료");
            }
        }

        void HandleTargetSelection()
        {
            UpdateTargetingArrow();
            if (!_waitingForTarget) return;

            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            {
                CancelTargetSelection();
                return;
            }

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (PointerOverCanvasControl()) return;

            byte target = _snappedTargetSeat;
            if (target == byte.MaxValue) return;

            int resolvedSlot = _inventoryPresenter.ResolvePendingSlotIndex();
            if (resolvedSlot < 0)
            {
                CancelTargetSelection();
                return;
            }
            byte slot = (byte)resolvedSlot;
            _waitingForTarget = false;
            _targetingArrowPresenter?.Hide();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugHoverTarget = null;
#endif

            ulong generation = _inventoryPresenter.BeginItemSubmission(slot);
            if (generation == 0) { CancelTargetSelection(); return; }
            bool sent = _commands.TrySelectItemWithTarget(slot, target, _inventoryPresenter.GetLocalSlot(slot).CopyId);
            _inventoryPresenter.CompleteItemSubmission(generation, sent);
            if (sent)
            {
                GameAudioManager.Instance?.PlayButtonClick();
                _matchHudPresenter.SetStatusText($"플레이어 {target + 1} 선택 완료");
            }
            else
            {
                _matchHudPresenter.SetStatusText("선택 실패");
            }
        }

        public void CancelAimForMenu()
        {
            if (_waitingForTarget) CancelTargetSelection();
            _ghostSkillPresenter?.CancelAimForMenu();
        }

        // OpponentBarPresenter lays out in Update. LateUpdate follows all those updates.
        void LateUpdate()
        {
            if (!_initialized || !_waitingForTarget || LocalGameplayInput.IsBlocked)
            { _targetingArrowPresenter?.Hide(); return; }
            UpdateTargetingArrow();
        }

        void CancelTargetSelection(bool clearStatus = true)
        {
            _waitingForTarget = false;
            _snappedTargetSeat = byte.MaxValue;
            _snappedTargetTransform = null;
            _inventoryPresenter?.CancelPendingItem();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugHoverTarget = null;
#endif
            _targetingArrowPresenter?.Hide();
            if (clearStatus) _matchHudPresenter?.SetStatusText("");
        }

        bool PointerOverCanvasControl()
        {
            var events = EventSystem.current;
            if (events == null || Mouse.current == null) return false;
            _pointerHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = Mouse.current.position.ReadValue() }, _pointerHits);
            // World colliders (including the target itself) can also be EventSystem
            // hits. Only a Canvas UI hit should prevent committing that target.
            foreach (var hit in _pointerHits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        void UpdateTargetingArrow()
        {
            var cam = Camera.main;
            if (cam == null || Mouse.current == null || _inventoryPresenter == null)
            {
                CancelTargetSelection();
                return;
            }

            int resolvedSlot = _inventoryPresenter.ResolvePendingSlotIndex();
            var itemView = _inventoryPresenter.GetLocalView(resolvedSlot);
            if (resolvedSlot < 0 || itemView == null)
            {
                CancelTargetSelection();
                return;
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            Vector2 targetScreen = pointer;
            bool snapped = TryResolveSnap(pointer, out byte targetSeat, out Transform targetTransform, out targetScreen);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugHoverTarget != null)
            {
                targetTransform = _debugHoverTarget;
                var marker = targetTransform.GetComponentInParent<PlayerSeatMarker>();
                targetSeat = marker != null ? marker.SeatIndex : byte.MaxValue;
                snapped = targetSeat != byte.MaxValue && TryGetEligibleTarget(targetSeat, out targetTransform)
                    && TryTargetSnapScreenPosition(cam, targetTransform, out targetScreen);
            }
#endif

            if (snapped)
            {
                int slot = AZPlayerVisual.GetRemoteVisualSlot(targetSeat, _bridge.LocalSeatIndex);
                RectTransform bar = slot >= 0 && slot < _refs.OppBars.Count && _refs.OppBars[slot].HpSlider != null
                    ? _refs.OppBars[slot].HpSlider.transform as RectTransform : null;
                snapped = _targetingArrowPresenter != null && _targetingArrowPresenter.Show(cam, targetTransform, bar);
            }
            if (!snapped) _targetingArrowPresenter?.Hide();
            _snappedTargetSeat = snapped ? targetSeat : byte.MaxValue;
            _snappedTargetTransform = snapped ? targetTransform : null;
        }

        bool TryResolveSnap(Vector2 pointer, out byte targetSeat, out Transform targetTransform,
            out Vector2 targetScreen)
        {
            targetSeat = byte.MaxValue;
            targetTransform = null;
            targetScreen = pointer;
            var cam = Camera.main;
            if (cam == null) return false;

            if (_snappedTargetSeat != byte.MaxValue
                && TryGetEligibleTarget(_snappedTargetSeat, out var current))
            {
                if (TryTargetSnapScreenPosition(cam, current, out var currentScreen)
                    && Vector2.Distance(pointer, currentScreen) <= TargetSnapReleasePixels)
                {
                    targetSeat = _snappedTargetSeat;
                    targetTransform = current;
                    targetScreen = currentScreen;
                    return true;
                }
            }

            float closest = TargetSnapEnterPixels;
            var views = MatchViewBindings.ForScene(gameObject.scene);
            int count = _bridge.CurrentMatch.LifeStates?.Length ?? 0;
            for (byte seat = 0; seat < count; seat++)
            {
                if (!IsEligibleTarget(seat)) continue;
                var target = views?.GetTarget(seat);
                if (target == null) continue;
                if (!TryTargetSnapScreenPosition(cam, target, out var screen)) continue;
                float distance = Vector2.Distance(pointer, screen);
                if (distance > closest) continue;
                closest = distance;
                targetSeat = seat;
                targetTransform = target;
                targetScreen = screen;
            }
            return targetSeat != byte.MaxValue;
        }

        bool TryGetEligibleTarget(byte seat, out Transform target)
        {
            target = null;
            if (!IsEligibleTarget(seat)) return false;
            target = MatchViewBindings.ForScene(gameObject.scene)?.GetTarget(seat);
            return target != null;
        }

        bool IsEligibleTarget(byte seat)
        {
            var match = _bridge.CurrentMatch;
            return seat != _bridge.LocalSeatIndex
                && match.LifeStates != null
                && seat < match.LifeStates.Length
                && match.LifeStates[seat] == LifeState.Alive;
        }

        static bool TryTargetSnapScreenPosition(Camera cam, Transform target, out Vector2 screen)
        {
            var collider = target.GetComponentInChildren<Collider>();
            Vector3 world = collider != null
                ? collider.bounds.center + Vector3.up * collider.bounds.extents.y * 0.35f
                : target.position + Vector3.up;
            return TargetingArrowPresenter.TryProjectVisible(cam, world, out screen);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugBeginTargetHover(byte slotIndex, byte targetSeat)
        {
            if (!_initialized || _inventoryPresenter == null || Mouse.current == null) return false;
            var item = _inventoryPresenter.GetLocalItemData(slotIndex);
            if (item == null || item.GetTargetMode() != TargetMode.SingleTarget) return false;

            if (!TryGetEligibleTarget(targetSeat, out var target) || Camera.main == null) return false;
            var targetMarker = target.GetComponent<PlayerSeatMarker>();

            _inventoryPresenter.RequestItemConfirm(slotIndex);
            if (_inventoryPresenter.SelectionStage != ItemSelectionStage.Aiming) return false;
            _waitingForTarget = true;
            _snappedTargetSeat = targetSeat;
            _snappedTargetTransform = targetMarker.transform;
            _debugHoverTarget = targetMarker.transform;
            _matchHudPresenter?.SetStatusText($"{item.ItemName} — hover target P{targetSeat + 1}");
            Vector3 screen = Camera.main.WorldToScreenPoint(targetMarker.transform.position + Vector3.up);
            Mouse.current.WarpCursorPosition(new Vector2(screen.x, screen.y));
            return true;
        }

        public bool DebugTargetHoverVisible => _targetingArrowPresenter != null && _targetingArrowPresenter.IsVisible;
        public void DebugCancelTargetHover()
        {
            _debugHoverTarget = null;
            CancelTargetSelection();
        }
#endif

        void OnMiniGameFinished(byte slotIndex, bool success)
        {
            _inventoryPresenter?.NotifyMiniGameFinished(success);
            _matchHudPresenter?.SetStatusText(success ? "미니게임 성공!" : "미니게임 실패");
        }

        void OnDestroy()
        {
            if (_bridge != null)
                _bridge.OnPhaseChanged -= HandlePhaseChanged;
            _tempPresenter?.Dispose();
            _roundResultPresenter?.Dispose();
            if (_miniGameHub != null) _miniGameHub.OnFinishedLocal -= OnMiniGameFinished;
            if (_inventoryPresenter != null)
                _inventoryPresenter.OnWorldItemClicked -= OnItemClicked;
            if (GameAudioManager.Instance != null)
                GameAudioManager.Instance.StopBGM();
        }
    }
}
