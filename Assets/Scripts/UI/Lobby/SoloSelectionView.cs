using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class SoloSelectionView : ISoloSelectionView, IDisposable
    {
        readonly SoloSelectionBindings _bindings;
        readonly List<(Button button, UnityAction listener)> _options = new();
        bool _disposed;
        public event Action<int> Selected;
        public event Action StartClicked, BackClicked;
        public SoloSelectionView(SoloSelectionBindings bindings)
        {
            _bindings = bindings;
            if (bindings == null || !bindings.IsComplete) throw new InvalidOperationException("Solo selection bindings are incomplete.");
            bindings.StartButton.onClick.AddListener(Start);
            bindings.BackButton.onClick.AddListener(Back);
            bindings.OptionTemplate.gameObject.SetActive(false);
        }
        void Start() => StartClicked?.Invoke();
        void Back() => BackClicked?.Invoke();
        public void SetVisible(bool visible) { if (!_disposed && _bindings != null) _bindings.gameObject.SetActive(visible); }
        public void Render(IReadOnlyList<string> titles, int selected, string details, string status, bool canStart, bool canSelect, bool canBack)
        {
            if (_disposed || _bindings == null) return;
            while (_options.Count < titles.Count)
            {
                int index = _options.Count;
                var button = UnityEngine.Object.Instantiate(_bindings.OptionTemplate, _bindings.Content);
                button.name = "Profile" + index; button.gameObject.SetActive(true);
                UnityAction listener = () => Selected?.Invoke(index);
                button.onClick.AddListener(listener); _options.Add((button, listener));
            }
            for (int i = 0; i < _options.Count; i++)
            {
                var button = _options[i].button;
                button.gameObject.SetActive(i < titles.Count);
                if (i >= titles.Count) continue;
                button.GetComponentInChildren<TMP_Text>().text = titles[i];
                button.interactable = canSelect;
                button.GetComponent<Image>().color = i == selected ? new Color(.25f, .48f, .45f) : new Color(.22f, .26f, .30f);
            }
            _bindings.Details.text = details;
            _bindings.Status.text = status;
            _bindings.StartButton.interactable = canStart;
            _bindings.BackButton.interactable = canBack;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_bindings != null) { _bindings.StartButton.onClick.RemoveListener(Start); _bindings.BackButton.onClick.RemoveListener(Back); }
            foreach (var entry in _options) if (entry.button != null) { entry.button.onClick.RemoveListener(entry.listener); UnityEngine.Object.Destroy(entry.button.gameObject); }
            _options.Clear();
        }
    }
}
