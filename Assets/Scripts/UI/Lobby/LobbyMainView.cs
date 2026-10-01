using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public class LobbyMainView : IDisposable
    {
        readonly GameObject _root;
        TextMeshProUGUI _statusText;
        TMP_InputField _nicknameInput;
        Button _soloButton;
        readonly System.Collections.Generic.List<(Button button, UnityEngine.Events.UnityAction action)> _listeners = new();

        public event Action OnArenaClicked;
        public event Action OnSoloClicked;
        public event Action OnClosetClicked;
        public event Action OnSettingsClicked;
        public event Action<string> OnNicknameEndEdit;

        public GameObject Root => _root;

        public LobbyMainView(GameObject root)
        {
            _root = root;
            Bind();
        }

        void Bind()
        {
            var t = _root.transform;

            _nicknameInput = t.Find("NicknameBar")?.GetComponent<TMP_InputField>();
            if (_nicknameInput != null)
                _nicknameInput.onEndEdit.AddListener(NicknameEdited);

            var arenaBtn = t.Find("ArenaBtn")?.GetComponent<Button>();
            if (arenaBtn != null)
                Listen(arenaBtn, () => OnArenaClicked?.Invoke());

            _soloButton = t.Find("SoloBtn")?.GetComponent<Button>();
            if (_soloButton != null) Listen(_soloButton, () => OnSoloClicked?.Invoke());

            var closetBtn = t.Find("ClosetBtn")?.GetComponent<Button>();
            if (closetBtn != null)
                Listen(closetBtn, () => OnClosetClicked?.Invoke());

            var settingsBtn = t.Find("SettingsBtn")?.GetComponent<Button>();
            if (settingsBtn != null)
                Listen(settingsBtn, () => OnSettingsClicked?.Invoke());

            _statusText = t.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        }

        public void SetVisible(bool visible) => _root.SetActive(visible);

        void Listen(Button button, UnityEngine.Events.UnityAction action)
        { button.onClick.AddListener(action); _listeners.Add((button, action)); }
        void NicknameEdited(string value) => OnNicknameEndEdit?.Invoke(value);
        public void SetBusy(bool busy)
        {
            foreach (var entry in _listeners) if (entry.button != null) entry.button.interactable = !busy;
        }
        public void Dispose()
        {
            foreach (var entry in _listeners) if (entry.button != null) entry.button.onClick.RemoveListener(entry.action);
            _listeners.Clear();
            if (_nicknameInput != null) _nicknameInput.onEndEdit.RemoveListener(NicknameEdited);
        }

        public void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        public void SetNicknameText(string text)
        {
            if (_nicknameInput != null) _nicknameInput.text = text;
        }
    }
}
