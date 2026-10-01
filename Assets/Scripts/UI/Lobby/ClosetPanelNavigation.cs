using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AbsoluteZero.UI.LobbyUI
{
    [RequireComponent(typeof(ClosetViewBindings))]
    public sealed class ClosetPanelNavigation : MonoBehaviour
    {
        ClosetViewBindings _view;
        GameObject _previousSelection;
        readonly List<UnityEngine.UI.Button> _buttons = new();
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            _view = GetComponent<ClosetViewBindings>();
            _previousSelection = EventSystem.current?.currentSelectedGameObject;
            StartCoroutine(InitialFocus());
        }
        IEnumerator InitialFocus()
        {
            yield return null;
            if (_view.Tabs?.Length > 0 && _view.Tabs[0] != null)
                EventSystem.current?.SetSelectedGameObject(_view.Tabs[0].gameObject);
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || EventSystem.current == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_view.Close != null && _view.Close.interactable) _view.Close.onClick.Invoke();
                return;
            }
            if (!keyboard.tabKey.wasPressedThisFrame) return;
            _buttons.Clear();
            foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button != _view.Dim && button.isActiveAndEnabled && button.IsInteractable()) _buttons.Add(button);
            if (_buttons.Count == 0) return;
            int current = _buttons.FindIndex(b => b.gameObject == EventSystem.current.currentSelectedGameObject);
            int next = keyboard.shiftKey.isPressed
                ? (current <= 0 ? _buttons.Count - 1 : current - 1) : (current + 1) % _buttons.Count;
            var selected = _buttons[next];
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
            EnsureVisible((RectTransform)selected.transform);
        }
        void EnsureVisible(RectTransform target)
        {
            if (_view.Scroll == null || !target.IsChildOf(_view.Content)) return;
            Canvas.ForceUpdateCanvases();
            var viewport = _view.Scroll.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            float offset = bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y
                : bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y : 0;
            _view.Content.anchoredPosition += new Vector2(0, offset);
            _view.Scroll.StopMovement();
        }
        void OnDisable()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null
                && events.currentSelectedGameObject.transform.IsChildOf(transform))
                events.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            _previousSelection = null;
        }
    }
}
