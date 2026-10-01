using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AbsoluteZero.Core.Inventory
{
    public class InventoryPresenter : MonoBehaviour
    {
        public static InventoryPresenter Instance { get; private set; }

        // --- Player references ---
        PlayerState _localPlayer;
        PlayerState _opponentPlayer;
        PlayerInventory _localInventory;
        PlayerInventory _opponentInventory;
        PlayerBinding _localBinding;
        PlayerBinding _opponentBinding;
        MultiInventoryReadModel _multiInventoryView;
        bool _boundMulti;
        BoundInventoryReader _localReader;
        InventoryViewSnapshot _localSnapshot;
        bool _localBound;
        bool _opponentBound;

        // --- Local item views ---
        ItemWorldView[] _localViews;
        readonly InventoryViewReconciler<ItemWorldView> _localReconciler = new();
        readonly InventoryViewReconciler<GameObject> _opponentReconciler = new();
        BoundInventoryReader _opponentReader;
        int _confirmedSlotIndex = -1;
        readonly ItemSelectionInteraction _interaction = new();
        public ItemSelectionStage SelectionStage => _interaction.Stage;
        bool _needsLocalRebuild;
        bool _fullRedistribute;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _debugFullRebuild;
        public int DebugRebuildCreates { get; private set; }
        public long DebugMeasureRebuildAllocations(bool full, int iterations)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            DebugRebuildCreates = 0;
            _debugFullRebuild = full;
            try { for (int i = 0; i < iterations; i++) RebuildLocalViews(); }
            finally { _debugFullRebuild = false; }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
#endif

        // --- Rebuild lock (E1: deadlock prevention) ---
        bool _rebuildLocked;
        float _rebuildLockTime;
        const float REBUILD_LOCK_TIMEOUT = 15f;

        // --- Opponent item views ---
        GameObject[] _opponentItemObjects;
        bool _needsOpponentRebuild;

        // --- Selection arrow ---
        GameObject _selectionArrow;
        SpriteRenderer _arrowRenderer;
        Texture2D _arrowTexture;

        // --- Layout constants ---
        const float FALLBACK_SPACING = 0.9f;
        const float FALLBACK_Y = 0.5f;

        // --- Events ---
        public event Action OnLocalInventoryChanged;
        public event Action OnOpponentInventoryChanged;
        public event Action<int> OnLocalSlotValueChanged;
        public event Action OnSelectionChanged;
        public event Action OnBannedStateChanged;
        public event Action OnViewsRebuilt;
        public event Action<int> OnWorldItemClicked;
        public event Action<int, int> OnItemConfirmRequested;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        void OnDestroy()
        {
            DestroyLocalViews();
            DestroyOpponentViews();
            Unbind();
            if (_arrowRenderer != null && _arrowRenderer.sprite != null) Destroy(_arrowRenderer.sprite);
            if (_arrowTexture != null) Destroy(_arrowTexture);
            if (_selectionArrow != null) Destroy(_selectionArrow);
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            bool multi = IsMultiMode();
            bool hasPerspective = LocalMatchPerspective.TryResolveCurrent(out var perspective);
            if (_localBound && (!hasPerspective || !ReferenceEquals(_localBinding, perspective.HumanBinding)))
            {
                DestroyLocalViews();
                DestroyOpponentViews();
                Unbind();
                _rebuildLocked = false;
                _confirmedSlotIndex = -1;
                _interaction.Reset();
                if (_selectionArrow != null) _selectionArrow.SetActive(false);
            }
            else if (!multi && _opponentBound && (!hasPerspective
                || !perspective.TryGetOpponentBinding(out var opponent)
                || !ReferenceEquals(opponent, _opponentBinding)))
            {
                DestroyOpponentViews();
                UnbindOpponent();
            }

            if (!_localBound || (!multi && !_opponentBound))
                TryBindPlayers();

            if (_localBound)
            {
                SynchronizeInteraction();
                HandleClick();
            }

            if (_selectionArrow != null && _selectionArrow.activeSelf)
            {
                if (TurnManager.Instance == null
                    || TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase)
                {
                    _selectionArrow.SetActive(false);
                }
                else if (_confirmedSlotIndex >= 0
                    && _localViews != null
                    && _confirmedSlotIndex < _localViews.Length
                    && _localViews[_confirmedSlotIndex] != null)
                {
                    var basePos = _localViews[_confirmedSlotIndex].transform.position
                                  + new Vector3(0f, 0.6f, 0f);
                    float bounce = Mathf.Sin(Time.time * 4f) * 0.05f;
                    _selectionArrow.transform.position = basePos + new Vector3(0f, bounce, 0f);
                }
            }
        }

        void LateUpdate()
        {
            // E1: timeout fallback for rebuild lock
            if (_rebuildLocked && Time.time - _rebuildLockTime > REBUILD_LOCK_TIMEOUT)
            {
                Debug.LogWarning("[InventoryPresenter] Rebuild lock timeout — force unlock");
                UnlockRebuild();
                return;
            }

            if (_needsLocalRebuild && CanRebuild())
            {
                _needsLocalRebuild = false;
                RebuildLocalViews();
            }

            if (!IsMultiMode() && _needsOpponentRebuild && CanRebuild())
            {
                _needsOpponentRebuild = false;
                RebuildOpponentItems();
            }
        }

        // ─── Binding ────────────────────────────────────────────

        void TryBindPlayers()
        {
            if (NetworkManager.Singleton == null) return;

            if (!_localBound)
                TryBindLocal();

            if (!IsMultiMode() && !_opponentBound && _localBound)
                TryBindOpponent();
        }

        static bool IsMultiMode()
        {
            var root = MatchCompositionRoot.Instance;
            return root != null && root.ActiveConfig != null
                && root.ActiveConfig.Mode == GameMode.Multi;
        }

        int LocalCount => _localSnapshot?.Count ?? 0;

        ItemSlotNetData LocalSlot(int index) => _localSnapshot != null ? _localSnapshot[index] : ItemSlotNetData.Empty;
        ItemDataSO LocalItem(int index) => ItemManager.Instance?.GetItemData(LocalSlot(index).ItemId);

        void TryBindLocal()
        {
            if (!LocalMatchPerspective.TryResolveCurrent(out var perspective)) return;
            var ps = perspective.HumanBinding.State;

            var inv = ps.GetInventory();
            if (inv == null || inv.SlotStates == null) return;
            if (ItemManager.Instance == null) return;
            var multiView = IsMultiMode()
                ? MatchCompositionRoot.Instance?.NetworkState?.InventoryReadModel : null;
            if (IsMultiMode() && (multiView == null || !multiView.HasSeat(ps.PlayerIndex)
                || multiView.GetCount(ps.PlayerIndex) == 0)) return;
            if (!IsMultiMode() && inv.SlotStates.Count == 0) return;

            if (!inv.IsRegistryReady)
                ItemManager.Instance.InitializeClientRegistry(inv);
            if (!inv.IsRegistryReady) return;

            _localPlayer = ps;
            _localBinding = perspective.HumanBinding;
            _localInventory = inv;
            _multiInventoryView = multiView;
            _boundMulti = multiView != null;
            _localReader = new BoundInventoryReader(_localBinding, multiView);
            _localReader.TryRead(out _localSnapshot);

            if (HoverRaycaster.Instance == null)
                gameObject.AddComponent<HoverRaycaster>();

            if (IsMultiMode()) _multiInventoryView.Changed += OnMultiInventoryChanged;
            else _localInventory.SlotStates.OnListChanged += OnLocalSlotStatesChanged;
            _localPlayer.HasSelectedItem.OnValueChanged += OnHasSelectedItemChanged;
            _localPlayer.IsBasicBlocked.OnValueChanged += OnBasicBlockedChanged;
            _localPlayer.OnItemSelectionRejected += OnItemSelectionRejected;

            _localBound = true;

            // E5: snapshot — items may already exist before subscription
            if (LocalCount > 0)
                _needsLocalRebuild = true;

            Debug.Log($"[InventoryPresenter] Local bound — {inv.SlotStates.Count} slots");
        }

        void TryBindOpponent()
        {
            if (!LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetOpponentBinding(out var opponent)) return;
            var opp = opponent.State;

            var inv = opp.GetInventory();
            if (inv == null || inv.SlotStates == null || inv.SlotStates.Count == 0) return;
            if (ItemManager.Instance == null) return;

            if (!inv.IsRegistryReady)
                ItemManager.Instance.InitializeClientRegistry(inv);

            _opponentPlayer = opp;
            _opponentBinding = opponent;
            _opponentInventory = inv;
            _opponentReader = new BoundInventoryReader(opponent);

            _opponentInventory.SlotStates.OnListChanged += OnOpponentSlotStatesChanged;
            _opponentBound = true;

            // E5: snapshot
            if (_opponentInventory.SlotStates.Count > 0)
                _needsOpponentRebuild = true;

            Debug.Log($"[InventoryPresenter] Opponent bound — {inv.SlotStates.Count} slots");
        }

        // ─── Safe Unbind (E2) ───────────────────────────────────

        void Unbind()
        {
            UnbindLocal();
            UnbindOpponent();
        }

        void UnbindLocal()
        {
            if (_multiInventoryView != null)
                _multiInventoryView.Changed -= OnMultiInventoryChanged;
            else if (_localInventory != null && _localInventory.SlotStates != null)
                _localInventory.SlotStates.OnListChanged -= OnLocalSlotStatesChanged;

            if (_localPlayer != null)
            {
                _localPlayer.HasSelectedItem.OnValueChanged -= OnHasSelectedItemChanged;
                _localPlayer.IsBasicBlocked.OnValueChanged -= OnBasicBlockedChanged;
                _localPlayer.OnItemSelectionRejected -= OnItemSelectionRejected;
            }

            _localBound = false;
            _localBinding = null;
            _localPlayer = null;
            _localInventory = null;
            _needsLocalRebuild = false;
            _boundMulti = false;
            _multiInventoryView = null;
            _localReader = null;
            _localSnapshot = null;
            _interaction.Reset();
            _confirmedSlotIndex = -1;
        }

        void OnMultiInventoryChanged() => _needsLocalRebuild = true;

        void OnItemSelectionRejected(uint copyId)
        {
            SynchronizeInteraction();
            _interaction.Reject(copyId);
            ResolveConfirmedSlotByCopyId();
            UpdateSelectionVisuals();
            OnSelectionChanged?.Invoke();
        }

        void UnbindOpponent()
        {
            if (_opponentInventory != null && _opponentInventory.SlotStates != null)
                _opponentInventory.SlotStates.OnListChanged -= OnOpponentSlotStatesChanged;

            _opponentBound = false;
            _opponentBinding = null;
            _opponentPlayer = null;
            _opponentInventory = null;
            _opponentReader = null;
            _needsOpponentRebuild = false;
        }

        // ─── Rebuild Lock (E1) ──────────────────────────────────

        public void LockRebuild()
        {
            _rebuildLocked = true;
            _rebuildLockTime = Time.time;
        }

        public void UnlockRebuild()
        {
            _rebuildLocked = false;
            // Combat presentation may be destroyed after the player or match root.
            // Releasing the lock must not rebuild from disposed network objects.
            if (!_localBound || _localPlayer == null || !_localPlayer.IsSpawned
                || _localInventory == null || !_localInventory.IsSpawned)
                return;
            if (_needsLocalRebuild && CanRebuild())
            {
                _needsLocalRebuild = false;
                RebuildLocalViews();
            }
            if (!IsMultiMode() && _needsOpponentRebuild)
            {
                _needsOpponentRebuild = false;
                RebuildOpponentItems();
            }
        }

        // E3: combined guard
        bool CanRebuild()
        {
            if (_rebuildLocked) return false;
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true) return false;
            if (_localInventory != null && _localInventory.TopUpCommitInProgress) return false;
            if (IceboxController.Instance != null && IceboxController.Instance.IsAnimating) return false;
            return true;
        }

        // ─── OnListChanged Handlers ─────────────────────────────

        void OnLocalSlotStatesChanged(NetworkListEvent<ItemSlotNetData> evt)
        {
            if (_localInventory == null) { Unbind(); return; }

            Debug.Log($"[InventoryPresenter] LocalSlotChanged: type={evt.Type}, index={evt.Index}");

            switch (evt.Type)
            {
                case NetworkListEvent<ItemSlotNetData>.EventType.Add:
                case NetworkListEvent<ItemSlotNetData>.EventType.Remove:
                case NetworkListEvent<ItemSlotNetData>.EventType.RemoveAt:
                case NetworkListEvent<ItemSlotNetData>.EventType.Insert:
                    _needsLocalRebuild = true;
                    break;

                case NetworkListEvent<ItemSlotNetData>.EventType.Clear:
                case NetworkListEvent<ItemSlotNetData>.EventType.Full:
                    _needsLocalRebuild = true;
                    _fullRedistribute = true;
                    break;

                case NetworkListEvent<ItemSlotNetData>.EventType.Value:
                    HandleLocalSlotValueChange(evt.Index);
                    break;
            }
        }

        void HandleLocalSlotValueChange(int index)
        {
            if (!CanRebuild()) { _needsLocalRebuild = true; return; }
            if (_localViews == null || index < 0 || index >= _localViews.Length)
                return;

            if (_localReader == null || !_localReader.TryRead(out var snapshot)) return;
            if (snapshot[index].CopyId != LocalSlot(index).CopyId)
            { _needsLocalRebuild = true; return; }
            _localSnapshot = snapshot;
            var slot = LocalSlot(index);

            if (slot.IsEmpty)
            {
                _needsLocalRebuild = true;
                return;
            }

            if (_localViews[index] == null)
            {
                _needsLocalRebuild = true;
                return;
            }

            var itemData = LocalItem(index);
            string currentName = _localViews[index].gameObject.name;
            string expectedName = itemData != null ? $"Item_{index}_{itemData.ItemName}" : "";
            if (currentName != expectedName)
            {
                _needsLocalRebuild = true;
                return;
            }

            string name = itemData != null ? itemData.ItemName : "Empty";
            string uses = slot.IsUnlimited ? "∞" : $"{slot.RemainingUses}";
            _localViews[index].UpdateDisplay(name, uses, slot.IsUsable);

            UpdateSelectionVisuals();
            OnLocalSlotValueChanged?.Invoke(index);
        }

        void OnOpponentSlotStatesChanged(NetworkListEvent<ItemSlotNetData> evt)
        {
            if (_opponentInventory == null) { UnbindOpponent(); return; }
            _needsOpponentRebuild = true;
        }

        void OnHasSelectedItemChanged(bool oldVal, bool newVal)
        {
            SynchronizeInteraction();
            UpdateSelectionVisuals();
            OnSelectionChanged?.Invoke();
        }

        void OnBasicBlockedChanged(bool oldVal, bool newVal)
        {
            Debug.Log($"[InventoryPresenter] BasicBlocked: {oldVal} → {newVal}");
            UpdateBannedOverlays();
            OnBannedStateChanged?.Invoke();
        }

        // ─── Local View Management ──────────────────────────────

        void RebuildLocalViews()
        {
            if (_localReader == null || !_localReader.TryRead(out var snapshot)) return;
            _localSnapshot = snapshot;
            bool animateAll = _fullRedistribute;
            _fullRedistribute = false;

            var previousSlots = new HashSet<int>();
            if (!animateAll && _localViews != null)
            {
                for (int i = 0; i < _localViews.Length; i++)
                    if (_localViews[i] != null) previousSlots.Add(i);
            }

            // Same copy keeps its object; full rebuild remains a legacy fallback.
            // E4: re-locate confirmed item by ItemId after rebuild
            _confirmedSlotIndex = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugFullRebuild) DestroyLocalViews();
#endif
            SpawnLocalViews();

            // E4: restore confirmed slot by ItemId
            if (_interaction.CopyId != 0 && _localPlayer != null && _localPlayer.HasSelectedItem.Value)
                ResolveConfirmedSlotByCopyId();

            if (animateAll)
                TriggerIceboxAnimation();
            else
                TriggerIceboxAnimationPartial(previousSlots);

            UpdateSelectionVisuals();
            UpdateBannedOverlays();
            OnViewsRebuilt?.Invoke();
            OnLocalInventoryChanged?.Invoke();
        }

        void SpawnLocalViews()
        {
            var result = _localReconciler.Reconcile(_localSnapshot,
                (index, slot) => CreateLocalView(index, ItemManager.Instance?.GetItemData(slot.ItemId)),
                (view, index, slot) => view.RefreshItem(index, ItemManager.Instance.GetItemData(slot.ItemId),
                    slot.IsUnlimited ? "∞" : $"{slot.RemainingUses}", slot.IsUsable),
                DestroyLocalView, view => view != null);
            if (result == null)
            {
                DestroyLocalViews();
                result = new ItemWorldView[LocalCount];
                for (int i = 0; i < result.Length; i++)
                    if (!LocalSlot(i).IsEmpty) result[i] = CreateLocalView(i, LocalItem(i));
            }
            _localViews = result;
            int random = 0, basic = 0;
            for (int i = 0; i < _localViews.Length; i++)
            {
                var view = _localViews[i];
                if (view == null) continue;
                int marker = view.Item.Persistence == ItemPersistence.RandomConsumable ? random++ : 8 + basic++;
                PositionLocalView(view.transform, marker, i, LocalCount);
            }
        }

        ItemWorldView CreateLocalView(int index, ItemDataSO item)
        {
            if (item == null) return null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugRebuildCreates++;
#endif
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<ItemWorldView>();
            view.InitializeItem(index, item, Color.white);
            var slot = LocalSlot(index);
            view.UpdateDisplay(item.ItemName, slot.IsUnlimited ? "∞" : $"{slot.RemainingUses}", slot.IsUsable);
            return view;
        }

        void PositionLocalView(Transform target, int markerIndex, int index, int count)
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            var marker = views != null ? views.GetItemAnchor(true, markerIndex)
                : GameObject.Find($"PlayerItem{markerIndex + 1}")?.transform;
            if (marker != null) target.position = marker.position;
            else target.localPosition = new Vector3(-(count - 1) * FALLBACK_SPACING / 2f + index * FALLBACK_SPACING, FALLBACK_Y, 0f);
        }

        static void DestroyLocalView(ItemWorldView view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        void DestroyLocalViews()
        {
            _localReconciler.Clear(_ => { });
            if (_localViews != null) foreach (var view in _localViews) DestroyLocalView(view);
            _localViews = null;
        }

        // ─── Opponent View Management ───────────────────────────

        void RebuildOpponentItems()
        {
            if (_opponentReader == null || !_opponentReader.TryRead(out var snapshot)) return;
            int random = 0, basic = 0;
            _opponentItemObjects = _opponentReconciler.Reconcile(snapshot,
                (i, slot) => {
                    if (ItemManager.Instance?.GetItemData(slot.ItemId) == null) return null;
                    var go = new GameObject();
                    go.transform.SetParent(transform, false);
                    go.AddComponent<SpriteRenderer>().sortingOrder = 5;
                    return go;
                },
                (go, i, slot) => {
                    var item = ItemManager.Instance.GetItemData(slot.ItemId);
                    go.name = $"OppItem_{i}_{item.ItemName}";
                    go.GetComponent<SpriteRenderer>().sprite = GameSprites.GetItemSpriteFor(item);
                    int markerIndex = item.Persistence == ItemPersistence.RandomConsumable ? random++ : 8 + basic++;
                    var views = MatchViewBindings.ForScene(gameObject.scene);
                    var marker = views != null ? views.GetItemAnchor(false, markerIndex)
                        : GameObject.Find($"EnemyItem{markerIndex + 1}")?.transform;
                    if (marker != null) go.transform.position = marker.position;
                }, DestroyRemoteView, go => go != null) ?? _opponentItemObjects;
            OnOpponentInventoryChanged?.Invoke();
        }

        static void DestroyRemoteView(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            Destroy(go);
        }
        void DestroyOpponentViews()
        {
            _opponentReconciler.Clear(DestroyRemoteView);
            _opponentItemObjects = null;
        }

        // ─── Selection & Banned ─────────────────────────────────

        void UpdateSelectionVisuals()
        {
            if (_localViews == null || _localPlayer == null || _localInventory == null) return;

            bool hasSelected = _localPlayer.HasSelectedItem.Value;

            for (int i = 0; i < _localViews.Length; i++)
            {
                if (_localViews[i] == null) continue;

                bool isThisSelected = hasSelected && _confirmedSlotIndex == i;

                if (_localViews[i].Hover != null)
                    _localViews[i].Hover.SetSelected(isThisSelected);

                if (!isThisSelected)
                {
                    var itemData = LocalItem(i);
                    bool isBanned = _localPlayer.IsBasicBlocked.Value
                                    && itemData != null
                                    && itemData.SlotType == ItemSlotType.Main;
                    bool usable = LocalSlot(i).IsUsable;
                    _localViews[i].SetInteractable(!isBanned && !hasSelected && usable);
                }
            }

            UpdateArrowVisibility();
        }

        void EnsureArrow()
        {
            if (_selectionArrow != null) return;
            _selectionArrow = new GameObject("SelectionArrow");

            const int size = 32;
            _arrowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int halfW = size / 2;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float ny = (float)y / size;
                float hw = halfW * (1f - ny);
                bool inside = x >= halfW - hw && x <= halfW + hw;
                _arrowTexture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
            _arrowTexture.Apply();
            _arrowTexture.filterMode = FilterMode.Bilinear;

            _arrowRenderer = _selectionArrow.AddComponent<SpriteRenderer>();
            _arrowRenderer.sprite = Sprite.Create(
                _arrowTexture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0f),
                100f);
            _arrowRenderer.color = new Color(1f, 0.85f, 0.2f);
            _arrowRenderer.sortingOrder = 95;
            _selectionArrow.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            _selectionArrow.SetActive(false);
        }

        void UpdateArrowVisibility()
        {
            EnsureArrow();
            bool visible = _localPlayer != null
                && _localPlayer.HasSelectedItem.Value
                && _confirmedSlotIndex >= 0
                && _localViews != null
                && _confirmedSlotIndex < _localViews.Length
                && _localViews[_confirmedSlotIndex] != null
                && TurnManager.Instance != null
                && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase;

            _selectionArrow.SetActive(visible);
        }

        void UpdateBannedOverlays()
        {
            if (_localViews == null || _localInventory == null || _localPlayer == null) return;

            bool blocked = _localPlayer.IsBasicBlocked.Value;

            for (int i = 0; i < _localViews.Length; i++)
            {
                if (_localViews[i] == null) continue;

                var itemData = LocalItem(i);
                if (itemData == null) continue;

                bool isBanned = blocked && itemData.SlotType == ItemSlotType.Main;
                _localViews[i].SetBanned(isBanned);
                if (isBanned)
                    _localViews[i].SetInteractable(false);
            }
        }

        // ─── Click Handling ─────────────────────────────────────

        void HandleClick()
        {
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true) return;
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
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true) return false;
            if (_localPlayer == null) return false;
            if (_localPlayer.IsReady.Value || _interaction.Stage == ItemSelectionStage.AwaitingServer) return false;

            if (IceboxController.Instance != null && IceboxController.Instance.IsAnimating) return false;

            var tm = TurnManager.Instance;
            if (tm == null || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return false;

            if (slotIndex < 0 || slotIndex >= LocalCount) return false;
            if (!LocalSlot(slotIndex).IsUsable) return false;

            var itemData = LocalItem(slotIndex);
            if (itemData == null) return false;

            if (_localPlayer.IsBasicBlocked.Value && itemData.Persistence == ItemPersistence.Permanent)
                return false;

            if (!_localPlayer.HasSelectedItem.Value) return true;
            return slotIndex == _confirmedSlotIndex;
        }

        // Local interaction state uses physical copy identity, never ItemId or slot identity.
        void SynchronizeInteraction()
        {
            var tm = TurnManager.Instance;
            var before = _interaction.Stage;
            _interaction.Observe(_localBinding,
                MatchCompositionRoot.Instance?.MatchManager?.RoundNumber.Value ?? 0,
                tm != null ? tm.TurnNumber.Value : 0,
                tm != null && tm.CurrentPhase.Value == TurnPhase.PrepPhase,
                _localPlayer != null && _localPlayer.HasSelectedItem.Value,
                _localPlayer != null && _localPlayer.IsReady.Value);
            ResolveConfirmedSlotByCopyId();
            if (before != _interaction.Stage) UpdateSelectionVisuals();
        }

        public void RequestItemConfirm(int slotIndex)
        {
            SynchronizeInteraction();
            if (!CanSelectItem(slotIndex) || !_interaction.Aim(LocalSlot(slotIndex).CopyId)) return;
            OnItemConfirmRequested?.Invoke(slotIndex, LocalSlot(slotIndex).ItemId);
        }

        public int ResolvePendingSlotIndex() => _interaction.Stage == ItemSelectionStage.Aiming
            ? _localSnapshot?.FindCopy(_interaction.CopyId) ?? -1 : -1;

        public ulong BeginItemSubmission(int slotIndex)
        {
            SynchronizeInteraction();
            return CanSelectItem(slotIndex) ? _interaction.Submit(LocalSlot(slotIndex).CopyId) : 0;
        }

        public void CompleteItemSubmission(ulong generation, bool sent)
        {
            if (!sent) _interaction.DispatchFailed(generation);
            SynchronizeInteraction();
            UpdateSelectionVisuals();
        }

        public void NotifyMiniGameFinished(bool success)
        {
            SynchronizeInteraction();
            // The server response releases failure. Releasing here could admit a
            // retry before the old same-CopyId rejection arrives.
            ResolveConfirmedSlotByCopyId();
            UpdateSelectionVisuals();
        }

        public void CancelPendingItem() => _interaction.CancelAim();

        public bool IsConfirmedSlot(int slotIndex) => _localPlayer != null && _localPlayer.HasSelectedItem.Value
            && !_localPlayer.IsReady.Value && slotIndex == _confirmedSlotIndex;

        void ResolveConfirmedSlotByCopyId() => _confirmedSlotIndex =
            _interaction.CopyId != 0 ? _localSnapshot?.FindCopy(_interaction.CopyId) ?? -1 : -1;

        // ─── Icebox Animation ───────────────────────────────────

        void TriggerIceboxAnimation()
        {
            var icebox = IceboxController.Instance;
            if (icebox == null || _localViews == null) return;

            int count = 0;
            for (int i = 0; i < _localViews.Length; i++)
                if (_localViews[i] != null) count++;

            if (count == 0) return;

            var transforms = new Transform[count];
            int idx = 0;
            for (int i = 0; i < _localViews.Length; i++)
                if (_localViews[i] != null)
                    transforms[idx++] = _localViews[i].transform;

            icebox.PlayDistribution(transforms);
        }

        void TriggerIceboxAnimationPartial(HashSet<int> previousSlots)
        {
            var icebox = IceboxController.Instance;
            if (icebox == null || _localViews == null) return;

            int count = 0;
            for (int i = 0; i < _localViews.Length; i++)
                if (_localViews[i] != null && !previousSlots.Contains(i)) count++;

            if (count == 0) return;

            var transforms = new Transform[count];
            int idx = 0;
            for (int i = 0; i < _localViews.Length; i++)
                if (_localViews[i] != null && !previousSlots.Contains(i))
                    transforms[idx++] = _localViews[i].transform;

            icebox.PlayDistribution(transforms);
        }

        // ─── Accessors ──────────────────────────────────────────

        public int LocalSlotCount => LocalCount;

        public ItemSlotNetData GetLocalSlot(int index)
        {
            return LocalSlot(index);
        }

        public ItemDataSO GetLocalItemData(int index)
        {
            return LocalItem(index);
        }

        public ItemWorldView GetLocalView(int index)
        {
            if (_localViews == null || index < 0 || index >= _localViews.Length) return null;
            return _localViews[index];
        }

        public Transform FindLocalViewByName(string nameFragment)
        {
            if (_localViews == null) return null;
            foreach (var v in _localViews)
                if (v != null && v.gameObject.name.Contains(nameFragment))
                    return v.transform;
            return null;
        }

        public Transform FindLocalView(ItemDataSO item)
        {
            if (item == null || _localViews == null) return null;
            foreach (var view in _localViews)
                if (view != null && view.Item == item) return view.transform;
            return null;
        }

        public PlayerInventory GetLocalInventory() => _localInventory;
        public PlayerInventory GetOpponentInventory() => _opponentInventory;
        public PlayerState GetLocalPlayer() => _localPlayer;
        public bool IsLocalBound => _localBound;
        public bool IsOpponentBound => _opponentBound;
    }
}
