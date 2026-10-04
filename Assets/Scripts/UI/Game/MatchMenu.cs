using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.UI.Game.Build;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game
{
    // Earlier than gameplay input: Escape and the closing click cannot also select/ready.
    [DefaultExecutionOrder(-200)]
    public sealed class MatchMenu : MonoBehaviour
    {
        GameObject _canvas;
        TextMeshProUGUI _message;
        Button _leave, _continue;
        bool _busy, _exitRequested;
        public bool IsOpen => _canvas != null && _canvas.activeSelf;

        void Awake()
        {
            _canvas = new GameObject("MatchMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var dim = GameHudBuilder.CreatePanel(_canvas.transform, "InputBlocker", Vector2.zero,
                Vector2.zero, new Color(0.02f, 0.04f, 0.05f, 0.7f));
            var rect = dim.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var panel = GameHudBuilder.CreatePanel(dim.transform, "Dialog", Vector2.zero,
                new Vector2(580, 320), new Color(0.10f, 0.18f, 0.20f, 1));
            Text(panel.transform, "Title", new Vector2(0, 106), new Vector2(520, 50), "로비로 돌아갈까요?", 30);
            _message = Text(panel.transform, "Message", new Vector2(0, 23), new Vector2(510, 104), "", 21);
            var notice = Text(panel.transform, "LiveNotice", new Vector2(0, -52), new Vector2(520, 34),
                "창을 열어도 게임과 제한 시간은 계속 진행됩니다.", 17);
            notice.color = new Color(0.78f, 0.86f, 0.85f);
            _continue = GameHudBuilder.CreateButton(panel.transform, "Continue", new Vector2(-132, -112),
                new Vector2(236, 55), "계속하기", new Color(0.24f, 0.39f, 0.41f));
            _leave = GameHudBuilder.CreateButton(panel.transform, "Leave", new Vector2(132, -112),
                new Vector2(236, 55), "로비로 나가기", new Color(0.59f, 0.23f, 0.19f));
            _continue.onClick.AddListener(Close);
            _leave.onClick.AddListener(ConfirmLeave);
            _canvas.SetActive(false);
        }

        static TextMeshProUGUI Text(Transform parent, string name, Vector2 position, Vector2 size, string value, int sizePt)
        {
            var text = GameHudBuilder.CreateText(parent, name, position, size, value, sizePt);
            text.raycastTarget = false;
            return text;
        }

        void Update()
        {
            if (_exitRequested) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                if (IsOpen) Close(); else Open();
            }
            if (IsOpen) _message.text = DepartureMessage();
        }

        string DepartureMessage()
        {
            var match = MatchCompositionRoot.Instance;
            if (match?.ActiveConfig?.Mode == GameMode.Solo) return "현재 봇 대전을 종료하고\n메인 로비로 돌아갑니다.";
            if (NetworkManager.Singleton?.IsServer == true) return "방장이 나가면 경기가 종료되고\n모두 메인 로비로 돌아갑니다.";
            int count = 0;
            if (match?.Registry != null)
                foreach (var player in match.Registry.Players)
                    if (player.IsValid && player.HasIdentity && player.Identity.ControllerKind == PlayerControllerKind.Human) count++;
            return match?.ActiveConfig?.Mode == GameMode.Multi && count >= 4
                ? "나는 메인 로비로 돌아갑니다.\n남은 3명은 경기를 계속합니다."
                : "참가자가 부족해 경기가 종료되고\n모두 메인 로비로 돌아갑니다.";
        }

        public void Open()
        {
            if (_busy || IsOpen || MatchCompositionRoot.Instance == null) return;
            LocalGameplayInput.Block(this);
            GetComponent<GameUIManager>()?.CancelAimForMenu();
            EventSystem.current?.SetSelectedGameObject(null);
            _message.text = DepartureMessage();
            _canvas.SetActive(true);
        }

        public void Close()
        {
            if (_exitRequested || !IsOpen) return;
            _canvas.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            LocalGameplayInput.Release(this);
        }

        public async void ConfirmLeave()
        {
            if (!IsOpen || _busy) return;
            var departure = MatchCompositionRoot.Instance?.GetComponent<MatchDepartureController>();
            if (departure == null) { _message.text = "경기 정보를 기다리고 있습니다. 잠시 후 다시 시도해주세요."; return; }
            _busy = true;
            _exitRequested = true;
            _leave.interactable = _continue.interactable = false;
            _message.text = "로비로 돌아가는 중…";
            try { await departure.LeaveAsync(); }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                if (this == null) return;
                _busy = false;
                _leave.interactable = true;
                _message.text = "복귀를 완료하지 못했습니다.\n로비로 나가기를 다시 눌러주세요.";
            }
        }

        void OnDisable()
        {
            if (_canvas != null) _canvas.SetActive(false);
            LocalGameplayInput.Release(this);
        }
        void OnDestroy() => LocalGameplayInput.Release(this);
    }
}
