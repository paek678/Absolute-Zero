using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
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

        public static event System.Action<byte, bool> OnFinishedLocal;

        PlayerState _localPlayer;
        TurnManager _tm;
        Canvas _canvas;
        MiniGameUIBase _active;

        void Awake()
        {
            Instance = this;
            BuildCanvas();
        }

        void OnDestroy()
        {
            if (_localPlayer != null) _localPlayer.OnMiniGameStart -= HandleStart;
            if (_tm != null) _tm.CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            if (Instance == this) Instance = null;
            OnFinishedLocal = null;
        }

        void Update()
        {
            if (_tm == null && TurnManager.Instance != null)
            {
                _tm = TurnManager.Instance;
                _tm.CurrentPhase.OnValueChanged += HandlePhaseChanged;
            }

            if (_localPlayer == null)
                TryBindLocalPlayer();
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
        }

        void HandleStart(byte slotIndex, MiniGameType type, float timeLimit, int goal)
        {
            if (_active != null) return;

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
                _ => null
            };

            if (_active == null)
            {
                Debug.LogWarning($"[MiniGameHub] Unimplemented mini-game type: {type} — auto-fail");
                Destroy(go);
                _localPlayer.SubmitMiniGameResultServerRpc(slotIndex, false);
                return;
            }

            _active.OnFinished += HandleFinished;
            _active.Begin(slotIndex, budget, goal, _canvas.transform);
        }

        void HandleFinished(byte slotIndex, bool success)
        {
            if (_active != null) _active.OnFinished -= HandleFinished;
            _active = null;

            _localPlayer?.SubmitMiniGameResultServerRpc(slotIndex, success);
            OnFinishedLocal?.Invoke(slotIndex, success);
        }

        void HandlePhaseChanged(TurnPhase oldPhase, TurnPhase newPhase)
        {
            if (newPhase != TurnPhase.PrepPhase && _active != null)
                _active.ForceCancel();
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
