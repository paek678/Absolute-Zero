using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    public class MiniGameHub : MonoBehaviour
    {
        public static MiniGameHub Instance { get; private set; }

        public static bool IsRunning => Instance != null && Instance._active != null;

        public event System.Action<byte, bool> OnFinishedLocal;

        MatchCompositionRoot _match;
        PlayerState _localPlayer;
        TurnManager _tm;
        Canvas _canvas;
        MiniGameUIBase _active;
        MiniGameUIBase _outgoing;
        MiniGameTicket _activeTicket;
        System.Action<byte, bool> _activeHandler;
        bool _cancelCurrent;

        void Awake()
        {
            Instance = this;
            _match = MatchCompositionRoot.Instance;
            BuildCanvas();
        }

        void OnDisable()
        {
            if (_active != null && _activeHandler != null)
                _active.OnFinished -= _activeHandler;
            if (_localPlayer != null)
            {
                _localPlayer.OnMiniGameStart -= HandleStart;
                _localPlayer.OnItemSelectionRejected -= HandleRejected;
            }
            if (_tm != null) _tm.CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            _localPlayer = null;
            _tm = null;
            if (_active != null) Destroy(_active.gameObject);
            if (_outgoing != null) Destroy(_outgoing.gameObject);
            _active = _outgoing = null;
            _activeHandler = null;
            _cancelCurrent = false;
        }

        void OnDestroy()
        {
            OnDisable();
            if (Instance == this) Instance = null;
            OnFinishedLocal = null;
        }

        void Update()
        {
            if (_match == null || _match != MatchCompositionRoot.Instance || !_match.IsSessionCurrent)
            { OnDisable(); return; }
            if (_tm == null && TurnManager.Instance != null
                && TurnManager.Instance.gameObject.scene == gameObject.scene)
            {
                _tm = TurnManager.Instance;
                _tm.CurrentPhase.OnValueChanged += HandlePhaseChanged;
            }

            if (_localPlayer == null)
                TryBindLocalPlayer();

            if (_active != null && _localPlayer != null
                && (_localPlayer.IsReady.Value || !HasTicketItem()))
            {
                _cancelCurrent = true;
                _active.ForceCancel();
            }
        }

        bool HasTicketItem()
        {
            var root = MatchCompositionRoot.Instance;
            if (root?.ActiveConfig?.Mode == GameMode.Multi)
                // Inventory view delivery may trail the ticket. The server checks
                // its CopyId and deadline when the result arrives.
                return true;
            var inventory = _localPlayer.GetInventory();
            if (inventory == null || _activeTicket.CopyId == 0) return false;
            for (int i = 0; i < inventory.SlotStates.Count; i++)
                if (!inventory.SlotStates[i].IsEmpty
                    && inventory.SlotStates[i].CopyId == _activeTicket.CopyId)
                    return true;
            return false;
        }

        void TryBindLocalPlayer()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var localObj = nm.SpawnManager?.GetLocalPlayerObject();
            if (localObj == null) return;

            var ps = localObj.GetComponent<PlayerState>();
            if (ps == null) return;

            _localPlayer = ps;
            _localPlayer.OnMiniGameStart += HandleStart;
            _localPlayer.OnItemSelectionRejected += HandleRejected;
        }

        void HandleRejected(uint copyId)
        {
            if (_active == null || copyId == 0 || copyId != _activeTicket.CopyId) return;
            // Server invalidation is not a failed mini-game: do not submit a
            // second result or debit an item that has already been invalidated.
            _cancelCurrent = true;
            _active.ForceCancel();
        }

        void HandleStart(MiniGameTicket ticket)
        {
            if (_activeTicket.AttemptId != 0 && !ticket.IsNewerThan(_activeTicket))
                return;
            if (_outgoing != null)
            {
                Destroy(_outgoing.gameObject);
                _outgoing = null;
            }
            if (_active != null)
            {
                // A newer server ticket replaces the previous attempt. The old
                // view must not submit a result or block the new attempt.
                if (_activeHandler != null) _active.OnFinished -= _activeHandler;
                _active.gameObject.SetActive(false);
                Destroy(_active.gameObject);
                _active = null;
                _activeHandler = null;
                _cancelCurrent = false;
            }
            _activeTicket = ticket;
            byte slotIndex = ticket.Slot;
            MiniGameType type = ticket.Type;
            float timeLimit = ticket.TimeLimit;
            int goal = ticket.Goal;

            float budget = timeLimit;
            var nm = NetworkManager.Singleton;
            if (_tm != null && nm != null)
            {
                float prepRemain = (float)(_tm.PrepStartServerTime.Value + _tm.PrepDuration.Value
                                           - nm.ServerTime.Time);
                budget = Mathf.Min(timeLimit, Mathf.Max(0.5f, prepRemain));
            }

            var go = new GameObject($"MiniGame_{type}");
            _active = type switch
            {
                MiniGameType.TapRepeat => go.AddComponent<HotPackMiniGameUI>(),
                MiniGameType.BoilWater => go.AddComponent<BuldakMiniGameUI>(),
                MiniGameType.TightenScrews => go.AddComponent<ScrewdriverMiniGameUI>(),
                MiniGameType.HitTargets => go.AddComponent<WaterGunMiniGameUI>(),
                MiniGameType.ClawGrab => go.AddComponent<ClawGrabMiniGameUI>(),
                MiniGameType.TimingCut => go.AddComponent<TapeCutMiniGameUI>(),
                MiniGameType.PatternUnlock => go.AddComponent<PatternUnlockMiniGameUI>(),
                MiniGameType.TapCard => go.AddComponent<RedCardMiniGameUI>(),
                MiniGameType.HugCharacter => go.AddComponent<HugCharacterMiniGameUI>(),
                _ => null
            };

            if (_active == null)
            {
                Debug.LogWarning($"[MiniGameHub] Unimplemented mini-game type: {type} — auto-fail");
                Destroy(go);
                SubmitResult(ticket, false);
                return;
            }

            Sprite itemIcon = null;
            var inv = _localPlayer?.GetInventory();
            if (inv != null)
            {
                var data = inv.GetItemData(slotIndex);
                if (data != null) itemIcon = data.Icon;
            }

            _cancelCurrent = false;
            var startedUi = _active;
            _activeHandler = (finishedSlot, success) =>
                HandleFinished(startedUi, ticket, finishedSlot, success);
            _active.OnFinished += _activeHandler;
            _active.Begin(slotIndex, budget, goal, _canvas.transform, itemIcon);
        }

        void HandleFinished(MiniGameUIBase source, MiniGameTicket ticket,
            byte slotIndex, bool success)
        {
            if (_active != source || ticket.Slot != slotIndex) return;
            if (_activeHandler != null) source.OnFinished -= _activeHandler;
            _activeHandler = null;
            _active = null;
            _outgoing = source;

            bool cancelled = _cancelCurrent;
            if (!cancelled)
                SubmitResult(ticket, success);
            _cancelCurrent = false;
            if (!cancelled)
                OnFinishedLocal?.Invoke(slotIndex, success);
        }

        void SubmitResult(MiniGameTicket ticket, bool success)
        {
            _localPlayer?.SubmitMiniGameResultServerRpc(ticket.Slot, success,
                ticket.MatchEpoch, ticket.RoundEpoch, ticket.Turn,
                ticket.AttemptId, ticket.CopyId);
        }

        void HandlePhaseChanged(TurnPhase oldPhase, TurnPhase newPhase)
        {
            if (newPhase != TurnPhase.PrepPhase && _active != null)
            {
                _cancelCurrent = true;
                _active.ForceCancel();
            }
        }

        void BuildCanvas()
        {
            var canvasGO = new GameObject("MiniGameCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }
    }
}
