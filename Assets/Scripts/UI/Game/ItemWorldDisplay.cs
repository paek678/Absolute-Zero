using System;
using System.Collections.Generic;
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
        bool _needsRebuild;
        bool _fullRedistribute;

        const float FALLBACK_SPACING = 0.9f;
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

        void LateUpdate()
        {
            if (_needsRebuild && _initialized)
            {
                _needsRebuild = false;
                RebuildViews();
            }
        }

        void TryInitialize()
        {
            if (NetworkManager.Singleton == null) return;

            var localObj = NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject();
            if (localObj == null) return;

            var ps = localObj.GetComponent<PlayerState>();
            if (ps == null) return;

            var inv = ps.GetInventory();
            if (inv == null || inv.SlotStates == null || inv.SlotStates.Count == 0) return;
            if (ItemManager.Instance == null) return;

            if (!inv.IsRegistryReady)
                ItemManager.Instance.InitializeClientRegistry(inv);
            if (!inv.IsRegistryReady) return;

            _localPlayer = ps;
            _inventory = inv;

            if (HoverRaycaster.Instance == null)
                gameObject.AddComponent<HoverRaycaster>();

            Debug.Log($"[ItemDisplay] TryInitialize SUCCESS — {inv.SlotStates.Count} slots");
            SpawnItemViews();
            TriggerIceboxAnimation();
            _inventory.SlotStates.OnListChanged += OnSlotStatesChanged;
            _localPlayer.HasSelectedItem.OnValueChanged += OnHasSelectedItemChanged;
            _initialized = true;
        }

        void SpawnItemViews()
        {
            int count = _inventory.SlotStates.Count;
            _views = new ItemWorldView[count];

            int randomMarker = 0;
            int basicMarker = 0;
            int spawnedCount = 0;

            for (int i = 0; i < count; i++)
            {
                var slot = _inventory.SlotStates[i];
                if (slot.IsEmpty) continue;

                var itemData = _inventory.GetItemData(i);
                if (itemData == null) continue;

                string markerName;
                if (itemData.Persistence == ItemPersistence.RandomConsumable)
                {
                    markerName = $"PlayerItem{randomMarker + 1}";
                    randomMarker++;
                }
                else
                {
                    markerName = $"PlayerItem{9 + basicMarker}";
                    basicMarker++;
                }

                var go = new GameObject();
                go.transform.SetParent(transform, false);

                var marker = GameObject.Find(markerName);
                if (marker != null)
                {
                    go.transform.position = marker.transform.position;
                    Debug.Log($"[ItemDisplay] SpawnView: slot{i} '{itemData.ItemName}' → {markerName} pos={marker.transform.position}");
                }
                else
                {
                    float totalWidth = (count - 1) * FALLBACK_SPACING;
                    go.transform.localPosition = new Vector3(
                        -totalWidth / 2f + i * FALLBACK_SPACING, FALLBACK_Y, 0f);
                    Debug.LogWarning($"[ItemDisplay] SpawnView: slot{i} '{itemData.ItemName}' — marker '{markerName}' NOT FOUND, using fallback");
                }

                var view = go.AddComponent<ItemWorldView>();
                view.Initialize(i, itemData.ItemName, Color.white);

                string uses = slot.IsUnlimited ? "∞" : $"{slot.RemainingUses}";
                view.UpdateDisplay(itemData.ItemName, uses, slot.IsUsable);

                _views[i] = view;
                spawnedCount++;
            }
            Debug.Log($"[ItemDisplay] SpawnItemViews: {spawnedCount}/{count} views created (random={randomMarker}, basic={basicMarker})");
        }

        void RebuildViews()
        {
            bool animateAll = _fullRedistribute;
            _fullRedistribute = false;

            var previousSlots = new HashSet<int>();
            if (!animateAll && _views != null)
            {
                for (int i = 0; i < _views.Length; i++)
                {
                    if (_views[i] != null)
                        previousSlots.Add(i);
                }
            }

            Debug.Log($"[ItemDisplay] RebuildViews: animateAll={animateAll}, previousSlots={previousSlots.Count}");

            if (_views != null)
            {
                foreach (var v in _views)
                {
                    if (v != null) Destroy(v.gameObject);
                }
            }

            _confirmedSlotIndex = -1;
            SpawnItemViews();

            if (animateAll)
            {
                Debug.Log("[ItemDisplay] RebuildViews → TriggerIceboxAnimation (ALL items)");
                TriggerIceboxAnimation();
            }
            else
            {
                Debug.Log($"[ItemDisplay] RebuildViews → TriggerIceboxAnimationPartial (new items only, prev={previousSlots.Count})");
                TriggerIceboxAnimationPartial(previousSlots);
            }

            UpdateSelectionVisuals();
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

            if (IceboxController.Instance != null && IceboxController.Instance.IsAnimating) return false;

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

            Debug.Log($"[ItemDisplay] OnSlotStatesChanged: type={changeEvent.Type}, index={changeEvent.Index}");

            if (changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.Add
                || changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.Remove
                || changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.RemoveAt
                || changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.Insert
                || changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.Clear
                || changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.Full)
            {
                _needsRebuild = true;
                if (changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.Clear
                    || changeEvent.Type == NetworkListEvent<ItemSlotNetData>.EventType.Full)
                {
                    _fullRedistribute = true;
                    Debug.Log("[ItemDisplay] → fullRedistribute=true (Clear/Full)");
                }
                return;
            }

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

        void TriggerIceboxAnimation()
        {
            var icebox = IceboxController.Instance;
            if (icebox == null || _views == null)
            {
                Debug.LogWarning($"[ItemDisplay] TriggerIceboxAnimation SKIP — icebox={icebox != null}, views={_views != null}");
                return;
            }

            int count = 0;
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] != null) count++;

            Debug.Log($"[ItemDisplay] TriggerIceboxAnimation: {count} items to animate");
            if (count == 0) return;

            var transforms = new Transform[count];
            int idx = 0;
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] != null)
                    transforms[idx++] = _views[i].transform;
            }

            icebox.PlayDistribution(transforms);
        }

        void TriggerIceboxAnimationPartial(HashSet<int> previousSlots)
        {
            var icebox = IceboxController.Instance;
            if (icebox == null || _views == null) return;

            int count = 0;
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] != null && !previousSlots.Contains(i)) count++;

            Debug.Log($"[ItemDisplay] TriggerIceboxAnimationPartial: {count} NEW items (total views={_views.Length}, prev={previousSlots.Count})");
            if (count == 0)
            {
                Debug.Log("[ItemDisplay] No new items to animate — skipping icebox");
                return;
            }

            var transforms = new Transform[count];
            int idx = 0;
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] != null && !previousSlots.Contains(i))
                    transforms[idx++] = _views[i].transform;
            }

            icebox.PlayDistribution(transforms);
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
