using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.UI.Game.Build;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using AbsoluteZero.UI.Common;
using AbsoluteZero.UI.LobbyUI;
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
        GameObject _dialogRoot;
        LobbySettingsView _settingsView;
        TextMeshProUGUI _message;
        Button _leave, _continue, _settings;
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
            _dialogRoot = dim.gameObject;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var panel = GameHudBuilder.CreatePanel(dim.transform, "Dialog", Vector2.zero,
                new Vector2(580, 320), new Color(0.10f, 0.18f, 0.20f, 1));
            Text(panel.transform, "Title", new Vector2(0, 106), new Vector2(520, 50), "로비로 돌아갈까요?", 30);
            _message = Text(panel.transform, "Message", new Vector2(0, 23), new Vector2(510, 104), "", 21);
            var notice = Text(panel.transform, "LiveNotice", new Vector2(0, -52), new Vector2(520, 34),
                "창을 열어도 게임과 제한 시간은 계속 진행됩니다.", 17);
            notice.color = CasualUiSkin.Muted;
            _continue = GameHudBuilder.CreateButton(panel.transform, "Continue", new Vector2(-176, -112),
                new Vector2(164, 55), "계속하기", CasualUiSkin.Mint);
            _settings = GameHudBuilder.CreateButton(panel.transform, "Settings", new Vector2(0, -112),
                new Vector2(164, 55), "설정", CasualUiSkin.Butter);
            _leave = GameHudBuilder.CreateButton(panel.transform, "Leave", new Vector2(176, -112),
                new Vector2(164, 55), "로비로 나가기", CasualUiSkin.Peach);
            CasualUiSkin.Surface(panel, CasualUiSkin.Cream);
            CasualUiSkin.Labels(panel.transform);
            CasualUiSkin.Label(notice, CasualUiSkin.Muted);
            CasualUiSkin.Button(_continue, CasualUiSkin.Mint);
            CasualUiSkin.Button(_settings, CasualUiSkin.Butter);
            CasualUiSkin.Button(_leave, CasualUiSkin.Peach);
            _continue.onClick.AddListener(Close);
            _settings.onClick.AddListener(OpenSettings);
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
                if (_settingsView?.Root.activeSelf == true) CloseSettings();
                else if (IsOpen) Close(); else Open();
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
            CloseSettings();
            _canvas.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            LocalGameplayInput.Release(this);
        }

        void OpenSettings()
        {
            if (_busy || !IsOpen) return;
            if (_settingsView == null)
            {
                var prefab = Resources.Load<GameObject>("UI/SettingsPanel");
                if (prefab == null)
                {
                    Debug.LogError("[MatchMenu] Missing UI/SettingsPanel prefab.");
                    return;
                }
                var root = Instantiate(prefab, _canvas.transform, false);
                root.name = "SettingsPanel";
                // The lobby panel uses a 1920x1080 canvas; this menu uses 1280x720.
                root.transform.Find("PanelBG").localScale = Vector3.one * .8f;
                root.GetComponent<SettingsPanelBindings>().HandleEscape = false;
                _settingsView = new LobbySettingsView(root);
                _settingsView.OnCloseClicked += CloseSettings;
            }
            EventSystem.current?.SetSelectedGameObject(null);
            _dialogRoot.SetActive(false);
            _settingsView.SetVisible(true);
        }

        void CloseSettings()
        {
            _settingsView?.SetVisible(false);
            _dialogRoot.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public async void ConfirmLeave()
        {
            if (!IsOpen || _busy) return;
            var departure = MatchCompositionRoot.Instance?.GetComponent<MatchDepartureController>();
            if (departure == null) { _message.text = "경기 정보를 기다리고 있습니다. 잠시 후 다시 시도해주세요."; return; }
            _busy = true;
            _exitRequested = true;
            _leave.interactable = _continue.interactable = _settings.interactable = false;
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
            _settingsView?.SetVisible(false);
            if (_dialogRoot != null) _dialogRoot.SetActive(true);
            if (_canvas != null) _canvas.SetActive(false);
            LocalGameplayInput.Release(this);
        }
        void OnDestroy()
        {
            if (_settingsView != null)
            {
                _settingsView.OnCloseClicked -= CloseSettings;
                _settingsView.Dispose();
            }
            LocalGameplayInput.Release(this);
        }
    }
}
