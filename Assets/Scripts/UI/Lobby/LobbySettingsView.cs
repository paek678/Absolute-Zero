using System;
using AbsoluteZero.Core.Common;
using UnityEngine;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class LobbySettingsView : IDisposable
    {
        readonly GameObject _root;
        readonly SettingsPanelBindings _bindings;
        readonly LocalSettingsService _settings;
        readonly LocalSettingsRuntime _display;
        bool _disposed;
        public event Action OnCloseClicked;
        public GameObject Root => _root;
        public LobbySettingsView(GameObject root, LocalSettingsService settings = null)
        {
            _root = root;
            _bindings = root.GetComponent<SettingsPanelBindings>();
            if (_bindings == null || !_bindings.IsComplete) throw new InvalidOperationException("Settings panel bindings are incomplete.");
            _display = LocalSettingsRuntime.Instance;
            _settings = settings ?? _display.Service;
            _settings.Changed += Render;
            if (_display != null) _display.ScreenChanged += Render;
            _bindings.Master.onValueChanged.AddListener(Master);
            _bindings.Bgm.onValueChanged.AddListener(Bgm);
            _bindings.Sfx.onValueChanged.AddListener(Sfx);
            _bindings.Shake.onValueChanged.AddListener(Shake);
            _bindings.Fullscreen.onValueChanged.AddListener(Fullscreen);
            _bindings.Close.onClick.AddListener(Close);
            _bindings.Dim.onClick.AddListener(Close);
            _bindings.Retry.onClick.AddListener(Retry);
            _bindings.Cancelled += Close;
        }
        bool AcceptsInput => !_disposed && _root.activeInHierarchy;
        void Master(float value) { if (AcceptsInput) _settings.SetVolumes(value, _settings.Current.Bgm, _settings.Current.Sfx); }
        void Bgm(float value) { if (AcceptsInput) _settings.SetVolumes(_settings.Current.Master, value, _settings.Current.Sfx); }
        void Sfx(float value) { if (AcceptsInput) _settings.SetVolumes(_settings.Current.Master, _settings.Current.Bgm, value); }
        void Shake(bool value) { if (AcceptsInput) _settings.SetShake(value); }
        void Fullscreen(bool value) { if (AcceptsInput && _display != null) { _display.RequestFullscreen(value); Render(); } }
        void Close() { if (AcceptsInput) OnCloseClicked?.Invoke(); }
        void Retry() { if (AcceptsInput) _settings.Save(); }
        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            _root.SetActive(visible);
            if (visible) Render();
        }
        void Render()
        {
            if (!AcceptsInput) return;
            var value = _settings.Current;
            _bindings.Master.SetValueWithoutNotify(value.Master);
            _bindings.Bgm.SetValueWithoutNotify(value.Bgm);
            _bindings.Sfx.SetValueWithoutNotify(value.Sfx);
            _bindings.Shake.SetIsOnWithoutNotify(value.Shake);
            _bindings.Fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            _bindings.Fullscreen.interactable = _display != null && !_display.ScreenPending;
            _bindings.MasterValue.text = Mathf.RoundToInt(value.Master * 100) + "%";
            _bindings.BgmValue.text = Mathf.RoundToInt(value.Bgm * 100) + "%";
            _bindings.SfxValue.text = Mathf.RoundToInt(value.Sfx * 100) + "%";
            _bindings.Status.text = _settings.HasUnsaved ? "설정은 적용됐지만 저장하지 못했습니다. 다시 저장해 주세요."
                : _display != null && _display.ScreenPending ? "화면 모드를 적용하는 중입니다…"
                : _display?.ScreenError ?? "변경한 설정은 이 PC에 자동으로 저장됩니다";
            _bindings.Retry.gameObject.SetActive(_settings.HasUnsaved);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _settings.Changed -= Render;
            if (_display != null) _display.ScreenChanged -= Render;
            _bindings.Master.onValueChanged.RemoveListener(Master);
            _bindings.Bgm.onValueChanged.RemoveListener(Bgm);
            _bindings.Sfx.onValueChanged.RemoveListener(Sfx);
            _bindings.Shake.onValueChanged.RemoveListener(Shake);
            _bindings.Fullscreen.onValueChanged.RemoveListener(Fullscreen);
            _bindings.Close.onClick.RemoveListener(Close);
            _bindings.Dim.onClick.RemoveListener(Close);
            _bindings.Retry.onClick.RemoveListener(Retry);
            _bindings.Cancelled -= Close;
        }
    }
}
