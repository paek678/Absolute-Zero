using System;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AbsoluteZero.UI.Game
{
    public class ItemWorldDisplay : MonoBehaviour
    {
        public event Action<int> OnWorldItemClicked;

        PlayerState _localPlayer;
        PlayerInventory _inventory;
        ItemWorldView[] _views;
        bool _initialized;
        int _confirmedSlotIndex = -1;

        const float FALLBACK_SPACING = 1.5f;
        const float FALLBACK_Y = 0.5f;

        void Update()
        {
            if (!_initialized)
            {
                TryInitialize();
                if (!_initialized) return;
            }

            HandleClick();
        }

        void TryInitialize()
        {
            if (NetworkManager.Singleton == null) return;

            var localObj = NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject();
            if (localObj == null) return;

            var ps = localObj.GetComponent<PlayerState>();
            if (ps == null) return;

            var inv = ps.GetInventory();
            // 12칸 전체 복제 완료까지 대기 — 일부만 복제된 시점에 뷰를 만들면 나머지 슬롯이 영영 안 생김 (클라 레이스)
            if (inv == null || inv.SlotStates == null || inv.SlotStates.Count < ItemSlotLayout.TOTAL_SLOTS) return;
            if (ItemManager.Instance == null) return;

            if (!inv.IsRegistryReady)
                ItemManager.Instance.InitializeClientRegistry(inv);
            if (!inv.IsRegistryReady) return;

            _localPlayer = ps;
            _inventory = inv;

            if (HoverRaycaster.Instance == null)
                gameObject.AddComponent<HoverRaycaster>();

            SpawnItemViews();
            _inventory.SlotStates.OnListChanged += OnSlotStatesChanged;
            _localPlayer.HasSelectedItem.OnValueChanged += OnHasSelectedItemChanged;
            _initialized = true;
        }

        void SpawnItemViews()
        {
            int count = _inventory.SlotStates.Count;
            _views = new ItemWorldView[count];
            // 기본 4칸 = 마커, 랜덤 8칸 = 아이스박스 옆 4×2 그리드 (PLAN_009 — 마커 기반 런타임 파생)
            var layout = ItemSlotLayout.Build("PlayerItem");

            for (int i = 0; i < count; i++)
            {
                var itemData = _inventory.GetItemData(i);
                string itemName = itemData != null ? itemData.ItemName : "Empty";

                var go = new GameObject();
                go.transform.SetParent(transform, false);

                if (layout.Valid && i < layout.Slots.Length)
                {
                    go.transform.position = layout.Slots[i];
                }
                else
                {
                    var marker = GameObject.Find($"PlayerItem{i + 1}");
                    if (marker != null)
                        go.transform.position = marker.transform.position;
                    else
                    {
                        float totalWidth = (count - 1) * FALLBACK_SPACING;
                        go.transform.localPosition = new Vector3(
                            -totalWidth / 2f + i * FALLBACK_SPACING, FALLBACK_Y, 0f);
                    }
                }

                var view = go.AddComponent<ItemWorldView>();
                view.Initialize(i, itemName, Color.white);
                _views[i] = view;
            }
        }

        void HandleClick()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            var hovered = HoverRaycaster.Instance?.CurrentHovered;
            if (hovered == null) return;

            var view = hovered.GetComponent<ItemWorldView>();
            if (view == null) return;

            if (!CanSelectItem(view.SlotIndex)) return;
            OnWorldItemClicked?.Invoke(view.SlotIndex);
        }

        bool CanSelectItem(int slotIndex)
        {
            if (_localPlayer == null) return false;
            if (_localPlayer.IsReady.Value) return false;

            var tm = TurnManager.Instance;
            if (tm == null || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return false;

            if (slotIndex < 0 || slotIndex >= _inventory.SlotStates.Count) return false;
            if (!_inventory.SlotStates[slotIndex].IsUsable) return false;

            var itemData = _inventory.GetItemData(slotIndex);
            if (itemData == null) return false;

            if (itemData.SlotType == ItemSlotType.Sub)
                return true;

            return !_localPlayer.HasSelectedItem.Value;
        }

        public void NotifyItemConfirmed(int slotIndex)
        {
            _confirmedSlotIndex = slotIndex;
            UpdateSelectionVisuals();
        }

        void OnSlotStatesChanged(NetworkListEvent<ItemSlotNetData> changeEvent)
        {
            if (_views == null || _inventory == null) return;

            int idx = changeEvent.Index;
            if (idx < 0 || idx >= _views.Length || _views[idx] == null) return;

            var slot = _inventory.SlotStates[idx];
            var itemData = _inventory.GetItemData(idx);
            string name = itemData != null ? itemData.ItemName : "Empty";
            string uses = slot.IsUnlimited ? "∞" : $"{slot.RemainingUses}";
            _views[idx].UpdateDisplay(name, uses, slot.IsUsable);

            UpdateSelectionVisuals();
        }

        void OnHasSelectedItemChanged(bool oldVal, bool newVal)
        {
            UpdateSelectionVisuals();
        }

        void UpdateSelectionVisuals()
        {
            if (_views == null || _localPlayer == null || _inventory == null) return;

            bool hasSelected = _localPlayer.HasSelectedItem.Value;

            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null) continue;

                bool isThisSelected = hasSelected && _confirmedSlotIndex == i;

                if (_views[i].Hover != null)
                    _views[i].Hover.SetSelected(isThisSelected);

                if (!isThisSelected)
                {
                    bool usable = i < _inventory.SlotStates.Count && _inventory.SlotStates[i].IsUsable;
                    _views[i].SetInteractable(!hasSelected && usable);
                }
            }
        }

        void OnDestroy()
        {
            if (_inventory != null)
                _inventory.SlotStates.OnListChanged -= OnSlotStatesChanged;

            if (_localPlayer != null)
                _localPlayer.HasSelectedItem.OnValueChanged -= OnHasSelectedItemChanged;

            if (_views != null)
            {
                foreach (var v in _views)
                {
                    if (v != null) Destroy(v.gameObject);
                }
            }
        }
    }
}
