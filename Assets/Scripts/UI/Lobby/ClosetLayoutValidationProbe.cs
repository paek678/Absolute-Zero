#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AbsoluteZero.Core.Cosmetic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AbsoluteZero.UI.LobbyUI
{
    // Isolated C01 layout fixture: no profile commands, saving or networking.
    public sealed class ClosetLayoutValidationProbe : MonoBehaviour
    {
        public ClosetViewBindings View;
        public CosmeticRegistrySO Registry;
        [Serializable] sealed class Report
        {
            public bool passed; public string failure;
            public List<string> checks = new(), captures = new();
        }
        readonly Report _report = new();
        readonly List<ClosetItemCellBindings> _cells = new();
        readonly List<InputDevice> _physical = new();
        InputSettings _oldSettings, _settings;
        Mouse _mouse; Keyboard _keyboard;
        string _directory;
        int _clicks, _closes, _tabs;
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--closet-layout-output");
            if (at < 0 || at + 1 >= args.Length) yield break;
            _directory = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(_directory);
            Application.runInBackground = true; Application.targetFrameRate = 60;
            _oldSettings = InputSystem.settings; _settings = Instantiate(_oldSettings);
            _settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = _settings;
            foreach (var device in InputSystem.devices)
                if (device.enabled && (device is Mouse || device is Keyboard)) _physical.Add(device);
            foreach (var device in _physical) InputSystem.DisableDevice(device);
            _mouse = InputSystem.AddDevice<Mouse>(); _keyboard = InputSystem.AddDevice<Keyboard>();
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object yielded = null; bool failed = false;
                try
                {
                    var flow = stack.Peek();
                    if (!flow.MoveNext()) { stack.Pop(); continue; }
                    yielded = flow.Current;
                    if (yielded is IEnumerator child) { stack.Push(child); continue; }
                }
                catch (Exception error) { _report.failure = error.ToString(); failed = true; }
                if (failed) break;
                yield return yielded;
            }
            _report.passed = _report.failure == null;
            CleanupInput();
            File.WriteAllText(Path.Combine(_directory, "report.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("[C01_LAYOUT] " + (_report.passed ? "PASS " : "FAIL ") + _report.checks.Count + " " + _report.failure);
            Application.Quit(_report.passed ? 0 : 2);
        }
        IEnumerator Run()
        {
            Check(View.Validate(out var error), "Serialized bindings valid: " + error);
            var tabReference = View.Tabs[1]; View.Tabs[1] = View.Tabs[0];
            Check(!View.Validate(out _), "Duplicate tab reference rejected"); View.Tabs[1] = tabReference;
            var closeReference = View.Close; View.Close = null;
            Check(!View.Validate(out _), "Missing close reference rejected"); View.Close = closeReference;
            Check(!View.Preview.raycastTarget, "Private image does not intercept input");
            View.Close.onClick.AddListener(Close); View.Dim.onClick.AddListener(Close);
            for (int t = 0; t < View.Tabs.Length; t++)
            {
                int tab = t;
                View.Tabs[t].onClick.AddListener(() =>
                {
                    _tabs++; View.Content.gameObject.SetActive(tab == 0);
                    View.EmptyMessage.gameObject.SetActive(tab != 0);
                    View.SelectedName.text = "선택한 항목이 없습니다";
                    View.EquippedName.text = tab == 0 ? "착용 중: 기본 모자" : "착용 중: 기본 모습";
                    View.Equip.interactable = false; View.Unequip.interactable = tab == 0;
                });
            }
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(800, 600) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return Frames(12);
                float deadline = Time.realtimeSinceStartup + 8;
                while ((Screen.width != size.x || Screen.height != size.y) && Time.realtimeSinceStartup < deadline) yield return null;
                Check(Screen.width == size.x && Screen.height == size.y, "Exact framebuffer " + size);
                foreach (var old in _cells) if (old != null) Destroy(old.gameObject);
                _cells.Clear(); yield return null;
                View.Content.gameObject.SetActive(true); View.EmptyMessage.gameObject.SetActive(false);
                var items = Registry.GetByPart(CosmeticPart.Head);
                for (int i = 0; i < 16; i++)
                {
                    int index = i;
                    var item = items[i % items.Count];
                    var cell = Instantiate(View.ItemTemplate, View.Content);
                    cell.name = "FixtureCell" + i; cell.gameObject.SetActive(true);
                    cell.Name.text = i == 1 ? "아주 긴 이름을 가진 겨울 산책용 모자 테스트 항목" : item.DisplayName;
                    cell.Icon.sprite = i == 1 ? null : item.Sprite;
                    cell.Icon.enabled = cell.Icon.sprite != null;
                    cell.Fallback.gameObject.SetActive(!cell.Icon.enabled);
                    cell.EquippedBadge.gameObject.SetActive(i == 0);
                    cell.SelectionBorder.gameObject.SetActive(i == 1);
                    cell.Button.onClick.AddListener(() =>
                    {
                        _clicks++;
                        View.SelectedName.text = "미리보기: " + cell.Name.text;
                        View.Equip.interactable = true;
                        foreach (var candidate in _cells) candidate.SelectionBorder.gameObject.SetActive(candidate == cell);
                    });
                    _cells.Add(cell);
                }
                View.SelectedName.text = "미리보기: 아주 긴 이름을 가진 겨울 산책용 모자 테스트 항목";
                View.EquippedName.text = "착용 중: 기본 모자";
                View.Equip.interactable = View.Unequip.interactable = true;
                View.Scroll.verticalNormalizedPosition = 1;
                yield return Frames(6); Canvas.ForceUpdateCanvases();
                View.Content.GetComponent<ClosetResponsiveGrid>().Refresh();
                yield return Frames(3);
                Check(View.Content.rect.height > View.Scroll.viewport.rect.height, "Scrollable overflow " + size);
                Check(View.Content.GetComponent<UnityEngine.UI.GridLayoutGroup>().constraintCount == (size.x == 800 ? 2 : 3), "Responsive columns " + size);
                foreach (var button in View.Tabs) CheckBounds((RectTransform)button.transform, size);
                foreach (var button in new[] { View.Equip, View.Unequip, View.Close, View.ResetPreview }) CheckBounds((RectTransform)button.transform, size);
                Check(_cells[0].Name.fontSize * View.GetComponentInParent<Canvas>().scaleFactor >= 10, "Minimum readable cell text " + size);
                yield return Capture(size + "-catalog");
                int before = _clicks;
                yield return Click((RectTransform)_cells[1].Button.transform);
                Check(_clicks == before + 1, "Actual item pointer click " + size);
                Check(_cells[1].SelectionBorder.gameObject.activeSelf && _cells[0].EquippedBadge.gameObject.activeSelf,
                    "Selected and equipped remain distinct " + size);
                var point = ScreenPoint(View.Scroll.viewport);
                for (int i = 0; i < 5; i++)
                {
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = point, scroll = new Vector2(0, -120) });
                    yield return Frames(2);
                }
                Check(View.Scroll.verticalNormalizedPosition < .5f, "Mouse wheel scrolls catalog " + size);
                yield return Capture(size + "-scrolled");
                int tabClicks = _tabs;
                EventSystem.current.SetSelectedGameObject(View.Tabs[0].gameObject);
                yield return PressKey(Key.Tab);
                Check(EventSystem.current.currentSelectedGameObject == View.Tabs[1].gameObject, "Tab focus remains in panel " + size);
                yield return PressKey(Key.Enter);
                Check(_tabs == tabClicks + 1 && View.EmptyMessage.gameObject.activeSelf, "Enter selects empty category " + size);
                Check(!View.Equip.interactable && !View.Unequip.interactable, "Empty category offers no invalid equipment action " + size);
                yield return Capture(size + "-empty");
                yield return Click((RectTransform)View.Tabs[0].transform);
                Check(View.Content.gameObject.activeSelf && !View.EmptyMessage.gameObject.activeSelf, "Return to populated category " + size);
            }
            int closeCount = _closes;
            yield return Click((RectTransform)View.Close.transform);
            Check(_closes == closeCount + 1 && !View.gameObject.activeSelf, "Close button closes once");
            Check(EventSystem.current.currentSelectedGameObject == null, "Closed panel releases its keyboard focus");
            View.gameObject.SetActive(true); yield return Frames(3);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(4, 4) }.WithButton(MouseButton.Left));
            yield return Frames(2); InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(4, 4) }); yield return Frames(2);
            Check(_closes == closeCount + 2 && !View.gameObject.activeSelf, "Dim pointer closes once");
            View.gameObject.SetActive(true); yield return Frames(3); yield return PressKey(Key.Escape);
            Check(_closes == closeCount + 3 && !View.gameObject.activeSelf, "Escape closes current panel once");
        }
        void Close() { _closes++; View.gameObject.SetActive(false); }
        void CheckBounds(RectTransform rect, Vector2Int size)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Check(corners[0].x >= 0 && corners[0].y >= 0 && corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1
                && (corners[2] - corners[0]).x >= 28 && (corners[2] - corners[0]).y >= 22, "Visible control " + rect.name + " " + size);
        }
        static Vector2 ScreenPoint(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        IEnumerator Click(RectTransform rect)
        {
            var point = ScreenPoint(rect);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point }); yield return Frames(2);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return Frames(2);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point }); yield return Frames(2);
        }
        IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key)); yield return Frames(2);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return Frames(2);
        }
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string name = label.Replace("(", "").Replace(")", "").Replace(", ", "x") + ".png";
                File.WriteAllBytes(Path.Combine(_directory, name), texture.EncodeToPNG()); _report.captures.Add(name);
            }
            finally { Destroy(texture); }
        }
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            _report.checks.Add(label);
        }
        void CleanupInput()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            foreach (var device in _physical) if (device.added) InputSystem.EnableDevice(device);
            _physical.Clear();
            if (_settings != null)
            {
                if (InputSystem.settings == _settings) InputSystem.settings = _oldSettings;
                Destroy(_settings); _settings = null;
            }
        }
        void OnDestroy() => CleanupInput();
    }
}
#endif
