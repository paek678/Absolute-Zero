using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 미니게임 클라이언트 허브 (AZGameUI가 런타임 생성).
    /// 로컬 플레이어의 OnMiniGameStart(서버 승인)를 받아 타입별 그레이박스 게임을 열고,
    /// 클라 판정 결과를 SubmitMiniGameResultServerRpc로 서버에 보고한다 (Q11).
    /// 준비 페이즈가 끝나면(서버 타이머 마스터) 진행 중인 게임을 강제 실패 종료한다.
    /// </summary>
    public class MiniGameHub : MonoBehaviour
    {
        public static MiniGameHub Instance { get; private set; }

        /// <summary>미니게임 진행 중 여부 — 준비끝/아이템 클릭 잠금용</summary>
        public static bool IsRunning => Instance != null && Instance._active != null;

        /// <summary>(slotIndex, success) — 로컬 판정 종료 시 발화 (AZGameUI 표시용)</summary>
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
            // 지연 바인딩 (AZGameUI와 동일 패턴 — spawn 타이밍이 UI보다 늦음)
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
            if (_active != null) return;   // 서버가 막지만 방어

            // 클라 표시 시간 = min(제한시간, 남은 준비시간) — 서버 마감과 일치시킴
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
                // 아직 미구현 타입 — 즉시 실패 반환해 서버 대기를 해소
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
            // 준비 페이즈 종료 = 서버 판정 — 진행 중 게임 강제 실패 (기획: 프렙 만료 시 강제 취소)
            if (newPhase != TurnPhase.PrepPhase && _active != null)
                _active.ForceCancel();
        }

        void BuildCanvas()
        {
            var canvasGO = new GameObject("MiniGameCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;   // 게임 UI(10)보다 위

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }
    }
}
