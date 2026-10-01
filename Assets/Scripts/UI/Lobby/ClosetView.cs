using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Cosmetic;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class ClosetView : IClosetView, IDisposable
    {
        sealed class Cell
        {
            public CosmeticItemSO Item;
            public ClosetItemCellBindings View;
            public UnityEngine.Events.UnityAction Click;
        }
        readonly GameObject _root;
        readonly ClosetViewBindings _bindings;
        readonly ClosetPreviewSurface _preview;
        readonly Dictionary<string, Cell> _cells = new(StringComparer.Ordinal);
        readonly List<(Button button, UnityEngine.Events.UnityAction callback)> _listeners = new();
        CosmeticItemSO _selected;
        bool _disposed;
        public GameObject Root => _root;
        public CosmeticPart CurrentTab { get; private set; } = CosmeticPart.Head;
        public event Action<CosmeticPart> OnTabChanged;
        public event Action<CosmeticItemSO> OnEquipClicked, OnItemSelected;
        public event Action<CosmeticPart> OnUnequipClicked;
        public event Action OnCloseClicked, OnResetPreview;
        public event Action<bool> OnVisibilityChanged;

        public ClosetView(GameObject root)
        {
            _root = root;
            _bindings = root != null ? root.GetComponent<ClosetViewBindings>() : null;
            if (_bindings == null || !_bindings.Validate(out _))
                throw new ArgumentException("Closet panel is missing valid serialized view bindings.", nameof(root));
            _preview = root.GetComponent<ClosetPreviewSurface>();
            for (int i = 0; i < _bindings.Tabs.Length; i++)
            {
                var part = (CosmeticPart)i;
                Listen(_bindings.Tabs[i], () => {
                    if (_disposed) return;
                    CurrentTab = part; _selected = null;
                    UpdateTabs(); _bindings.Scroll.verticalNormalizedPosition = 1;
                    OnTabChanged?.Invoke(part);
                });
            }
            Listen(_bindings.Close, () => OnCloseClicked?.Invoke());
            Listen(_bindings.Dim, () => OnCloseClicked?.Invoke());
            Listen(_bindings.ResetPreview, () => OnResetPreview?.Invoke());
            Listen(_bindings.Equip, () => { if (_selected != null) OnEquipClicked?.Invoke(_selected); });
            Listen(_bindings.Unequip, () => OnUnequipClicked?.Invoke(CurrentTab));
            _bindings.VisibilityChanged += HandleVisibility;
        }
        void HandleVisibility(bool visible)
        {
            if (_disposed) return;
            if (!visible) _selected = null;
            OnVisibilityChanged?.Invoke(visible);
        }
        void Listen(Button button, UnityEngine.Events.UnityAction callback)
        { button.onClick.AddListener(callback); _listeners.Add((button, callback)); }

        public void RenderItems(IReadOnlyList<CosmeticItemSO> items, CosmeticSnapshot snapshot)
        {
            if (_disposed) return;
            foreach (var cell in _cells.Values) cell.View.gameObject.SetActive(false);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (items != null) foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || !seen.Add(item.Id)) continue;
                if (!_cells.TryGetValue(item.Id, out var cell))
                {
                    cell = new Cell { View = UnityEngine.Object.Instantiate(_bindings.ItemTemplate, _bindings.Content) };
                    var captured = cell;
                    cell.Click = () => { if (!_disposed && captured.Item != null) OnItemSelected?.Invoke(captured.Item); };
                    cell.View.Button.onClick.AddListener(cell.Click);
                    _cells.Add(item.Id, cell);
                }
                cell.Item = item;
                cell.View.name = "Item_" + item.Id;
                cell.View.Name.text = item.DisplayName;
                cell.View.Icon.sprite = item.Sprite;
                cell.View.Icon.enabled = item.Sprite != null;
                cell.View.Fallback.gameObject.SetActive(item.Sprite == null);
                cell.View.gameObject.SetActive(true);
                cell.View.transform.SetAsLastSibling();
            }
            var stale = new List<string>();
            foreach (var pair in _cells)
                if (pair.Value.Item == null || (pair.Value.Item.Part == CurrentTab && !seen.Contains(pair.Key))) stale.Add(pair.Key);
            foreach (var id in stale) { DestroyCell(_cells[id]); _cells.Remove(id); }
            _bindings.EmptyMessage.gameObject.SetActive(seen.Count == 0);
        }
        public void SetSelection(CosmeticSnapshot equipped, CosmeticItemSO selected, CosmeticSnapshot preview)
        {
            if (_disposed) return;
            _selected = selected;
            var id = equipped.Get(CurrentTab);
            string equippedName = "기본 모습";
            foreach (var cell in _cells.Values)
            {
                bool isEquipped = cell.Item != null && equipped.Get(cell.Item.Part) == cell.Item.Id;
                cell.View.EquippedBadge.gameObject.SetActive(isEquipped);
                cell.View.SelectionBorder.gameObject.SetActive(selected != null && cell.Item == selected);
                if (cell.Item != null && cell.Item.Id == id) equippedName = cell.Item.DisplayName;
            }
            _bindings.SelectedName.text = selected != null ? "미리보기: " + selected.DisplayName : "선택한 항목이 없습니다";
            _bindings.EquippedName.text = "착용 중: " + equippedName;
            _bindings.Equip.interactable = selected != null && selected.Id != id;
            _bindings.Unequip.interactable = !string.IsNullOrEmpty(id);
            _bindings.ResetPreview.interactable = selected != null;
            _preview?.Show(preview);
        }
        void UpdateTabs()
        {
            for (int i = 0; i < _bindings.Tabs.Length; i++)
            {
                var colors = _bindings.Tabs[i].colors;
                colors.normalColor = i == (int)CurrentTab ? new Color(.35f, .53f, .53f) : new Color(.19f, .39f, .40f);
                _bindings.Tabs[i].colors = colors;
            }
        }
        public void SetVisible(bool visible)
        {
            if (_disposed || _root == null) return;
            if (visible && !_root.activeSelf) { CurrentTab = CosmeticPart.Head; UpdateTabs(); }
            _root.SetActive(visible);
        }
        public void SetStatus(string message) { if (!_disposed) _bindings.SaveStatus.text = message; }
        public void SetPublicationStatus(string message) { if (!_disposed) _bindings.PublicationStatus.text = message; }
        void DestroyCell(Cell cell)
        {
            if (cell.View == null) return;
            cell.View.Button.onClick.RemoveListener(cell.Click);
            cell.View.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(cell.View.gameObject);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_bindings != null) _bindings.VisibilityChanged -= HandleVisibility;
            foreach (var pair in _listeners) if (pair.button != null) pair.button.onClick.RemoveListener(pair.callback);
            _listeners.Clear();
            foreach (var cell in _cells.Values) DestroyCell(cell);
            _cells.Clear(); _selected = null;
            _preview?.Release();
        }
    }
}
