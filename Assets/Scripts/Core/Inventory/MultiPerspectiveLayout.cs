using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Inventory
{
    /// <summary>
    /// Client-local four-player presentation layout. The owning player is always
    /// represented by the south/FPS view; remote visual slots are west, north and east.
    /// This component never changes authoritative seat identity or gameplay state.
    /// </summary>
    public sealed class MultiPerspectiveLayout : MonoBehaviour
    {
        sealed class RemoteInventoryBinding
        {
            public PlayerState Player;
            public PlayerInventory Inventory;
            public PlayerBinding Participant;
            public readonly InventoryViewReconciler<GameObject> Views = new();
            public readonly InventoryPresentationSlots Slots = new();
        }

        static readonly Vector3[] RemotePlayerPositions =
        {
            new(-7.2f, 1.60f, 4.2f),
            new(0f, 1.60f, 9.2f),
            new(7.2f, 1.60f, 4.2f)
        };

        readonly Dictionary<byte, RemoteInventoryBinding> _bindings = new();
        readonly HashSet<byte> _dirtySeats = new();
        MultiInventoryReadModel _inventoryView;
        PlayerState _localPlayer;
        int _localSeat = -1;
        float _nextReconcile;
        bool _visualSlotsPositioned;
        bool _localIceboxPositioned;
        MapCharacterLayout _mapLayout;
        Quaternion _inventoryFacing = Quaternion.identity;
        readonly Transform[][] _itemAnchors = new Transform[3][];
        bool _localSlotPlacesReady;

        // Centered behind the local item row so the Ready control and arrow tail stay visible.
        static readonly Vector3 LocalIceboxPosition = new(0f, 0.05f, 2.4f);
        public static Vector3 IceboxCenter => LocalIceboxPosition;
        // These are offsets in the local viewer's map frame, not the character's facing.
        // Keep side inventories inward even when an artist rotates a character anchor.
        static readonly Vector3[] InventoryFloorOffsets =
        {
            new(2.9f, 0.85f, -0.6f), new(0f, 0.85f, -3.4f), new(-2.9f, 0.85f, -0.6f)
        };
        const float RemoteIconSize = 0.54f;
        const float SideIconSize = 0.44f;
        const float RemoteColumnSpacing = 0.62f;
        const float SideColumnSpacing = 1.00f;
        const float RemoteRowSpacing = 0.60f;

        void Update()
        {
            if (!IsMultiScene()) return;
            var view = MatchCompositionRoot.Instance?.NetworkState?.InventoryReadModel;
            if (_inventoryView != view)
            {
                if (_inventoryView != null) _inventoryView.Changed -= OnInventoryViewChanged;
                _inventoryView = view;
                if (_inventoryView != null) _inventoryView.Changed += OnInventoryViewChanged;
                OnInventoryViewChanged();
            }

            if (!_visualSlotsPositioned)
                PositionRemoteVisualSlots();
            if (!_localIceboxPositioned)
                PositionLocalIcebox();
            if (!_localSlotPlacesReady)
                PositionLocalSlotPlaces();

            if (Time.unscaledTime < _nextReconcile) return;
            _nextReconcile = Time.unscaledTime + 0.5f;
            ReconcileBindings();
        }

        void OnDestroy()
        {
            if (_inventoryView != null) _inventoryView.Changed -= OnInventoryViewChanged;
            foreach (var binding in _bindings.Values)
                Unbind(binding);
            _bindings.Clear();
            _dirtySeats.Clear();
        }

        void OnInventoryViewChanged()
        {
            foreach (byte seat in _bindings.Keys) _dirtySeats.Add(seat);
        }

        void LateUpdate()
        {
            if (_dirtySeats.Count == 0) return;
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true)
                return;
            var rebuilt = new List<byte>();
            foreach (byte seat in _dirtySeats)
            {
                if (!_bindings.TryGetValue(seat, out var binding))
                {
                    rebuilt.Add(seat);
                    continue;
                }
                if (_inventoryView != null)
                {
                    Rebuild(binding);
                    rebuilt.Add(seat);
                }
            }
            foreach (byte seat in rebuilt)
                _dirtySeats.Remove(seat);
        }

        static bool IsMultiScene()
        {
            var root = MatchCompositionRoot.Instance;
            return root != null && root.ActiveConfig != null
                && root.ActiveConfig.Mode == GameMode.Multi;
        }

        void PositionRemoteVisualSlots()
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            if (views != null) _mapLayout = views.MapLayout;
            else MapCharacterLayout.TryFind(gameObject.scene, out _mapLayout);
            // Capture the seating camera before combat close-ups. A delayed inventory
            // snapshot must not permanently rotate its grid to an animation camera.
            var camera = views != null ? views.GameplayCamera : Camera.main;
            if (camera != null) _inventoryFacing = camera.transform.rotation;
            int found = 0;
            for (int slot = 0; slot < RemotePlayerPositions.Length; slot++)
            {
                var visual = views != null ? views.GetRemoteVisual(slot)?.gameObject : FindSceneObject($"EnemyPlayer_{slot}");
                if (visual == null) continue;
                if (_mapLayout != null)
                {
                    if (!_mapLayout.TryApplyRemoteVisual(MapLayoutMode.Multi, slot, visual.transform)) continue;
                }
                else visual.transform.position = RemotePlayerPositions[slot];
                if (visual.GetComponent<PlayerSeatMarker>()?.Player == null) visual.SetActive(false);
                found++;
            }

            _visualSlotsPositioned = found == RemotePlayerPositions.Length;
            if (_visualSlotsPositioned)
                Debug.Log("[MultiLayout] Remote visual slots positioned west/north/east");
        }

        void PositionLocalIcebox()
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            var spawnPoint = views != null ? views.IceboxSpawn : FindSceneObject("BoxSpawnPoint")?.transform;
            if (spawnPoint == null) return;
            spawnPoint.position = LocalIceboxPosition;
            var icebox = IceboxController.Instance;
            if (icebox == null || !icebox.TrySetPresentationPosition(LocalIceboxPosition)) return;
            if (!icebox.TrySetPresentationScale(0.55f)) return;
            _localIceboxPositioned = true;
            Debug.Log($"[MultiLayout] Local icebox positioned south-center at {LocalIceboxPosition}");
        }

        void ReconcileBindings()
        {
            if (!LocalMatchPerspective.TryResolveCurrent(out var perspective))
            {
                foreach (var staleBinding in _bindings.Values) Unbind(staleBinding);
                _bindings.Clear(); _dirtySeats.Clear(); _localPlayer = null; _localSeat = -1;
                foreach (var anchors in _itemAnchors)
                    if (anchors != null) anchors[0].parent.gameObject.SetActive(false);
                return;
            }
            _localPlayer = perspective.HumanBinding.State;
            _localSeat = perspective.HumanSeat;
            var players = MatchCompositionRoot.Instance.Registry.Players;

            // Reserve visible places even before the first inventory is dealt.
            // A departed seat loses its places together with its character.
            for (int slot = 0; slot < _itemAnchors.Length; slot++)
                if (_itemAnchors[slot] != null)
                    _itemAnchors[slot][0].parent.gameObject.SetActive(false);

            var present = new HashSet<byte>();
            foreach (var participant in players)
            {
                var player = participant.State;
                if (!participant.IsValid || !participant.HasIdentity || player == _localPlayer
                    || !perspective.TryGetBinding(participant.Identity.PlayerIndex, out var current)
                    || !ReferenceEquals(current, participant))
                    continue;

                byte seat = (byte)player.PlayerIndex;
                present.Add(seat);
                int visualSlot = AZPlayerVisual.GetRemoteVisualSlot(player.PlayerIndex, _localSeat);
                if (visualSlot >= 0 && visualSlot < _itemAnchors.Length)
                {
                    EnsureItemAnchors(visualSlot);
                    _itemAnchors[visualSlot][0].parent.gameObject.SetActive(true);
                }
                var inventory = player.GetInventory();
                if (inventory == null || _inventoryView == null || !_inventoryView.HasSeat(seat)
                    || _inventoryView.GetCount(seat) == 0)
                    continue;
                if (!inventory.IsRegistryReady)
                    ItemManager.Instance?.InitializeClientRegistry(inventory);
                if (!inventory.IsRegistryReady) continue;

                if (_bindings.TryGetValue(seat, out var existing))
                {
                    if (ReferenceEquals(existing.Participant, participant) && existing.Inventory == inventory) continue;
                    Unbind(existing);
                    _bindings.Remove(seat);
                }

                var binding = new RemoteInventoryBinding
                {
                    Player = player,
                    Inventory = inventory,
                    Participant = participant
                };
                _bindings.Add(seat, binding);
                _dirtySeats.Add(seat);
            }

            var stale = new List<byte>();
            foreach (var pair in _bindings)
                if (!present.Contains(pair.Key) || pair.Value.Player == null)
                    stale.Add(pair.Key);
            foreach (byte seat in stale)
            {
                Unbind(_bindings[seat]);
                _bindings.Remove(seat);
            }
        }

        void Rebuild(RemoteInventoryBinding binding)
        {
            if (binding.Player == null || _localSeat < 0 || _inventoryView == null
                || !new BoundInventoryReader(binding.Participant, _inventoryView).TryRead(out var snapshot)) return;
            int slot = AZPlayerVisual.GetRemoteVisualSlot(binding.Player.PlayerIndex, _localSeat);
            if (slot < 0 || slot >= RemotePlayerPositions.Length) return;
            EnsureItemAnchors(slot);
            binding.Slots.Refresh(snapshot, id => ItemManager.Instance.GetItemData(id)?.Persistence == ItemPersistence.RandomConsumable);
            binding.Views.Reconcile(snapshot, (i, state) => CreateRemoteItem(state.ItemId),
                (go, i, state) => {
                    var item = ItemManager.Instance.GetItemData(state.ItemId);
                    go.name = $"RemoteSeat{binding.Player.PlayerIndex}_Item{i}_{item.ItemName}";
                    // Every rack reserves all 12 slots from the start. Adding or
                    // removing a copy never recenters/rescales the other copies.
                    int anchor = binding.Slots.Get(state.CopyId);
                    if (anchor < 0) return;
                    int basicCount = InventoryPresentationSlots.Capacity - InventoryPresentationSlots.RandomCapacity;
                    int cell = anchor >= InventoryPresentationSlots.RandomCapacity
                        ? anchor - InventoryPresentationSlots.RandomCapacity : anchor + basicCount;
                    const float fit = 8f / InventoryPresentationSlots.Capacity;
                    go.transform.position = _itemAnchors[slot][cell].position;
                    go.transform.rotation = _inventoryFacing;
                    go.transform.localScale = Vector3.one;
                    var renderer = go.GetComponentInChildren<SpriteRenderer>();
                    renderer.sprite = GameSprites.GetItemSpriteFor(item);
                    var art = VisibleSpriteBounds(renderer.sprite);
                    Vector2 size = art.size;
                    float largestSide = Mathf.Max(size.x, size.y);
                    float iconSize = slot == 1 ? RemoteIconSize : SideIconSize;
                    float scale = largestSide > 0f ? iconSize * fit / largestSide : 1f;
                    renderer.transform.localScale = Vector3.one * scale;
                    renderer.transform.rotation = MultiItemFacing.RackFacing(RackOrigin(slot), LocalIceboxPosition, _inventoryFacing);
                    // Imported pivots differ. Center the icon inside its cell while
                    // keeping text size and the cell's transform independent of art.
                    renderer.transform.localPosition = -(renderer.transform.localRotation * (art.center * scale));
                    InventoryFloorContact.Bind(go, renderer, FloorPosition(slot).y);

                }, DestroyView, go => go != null);
        }

        GameObject CreateRemoteItem(short itemId)
        {
            if (ItemManager.Instance?.GetItemData(itemId) == null) return null;
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            var icon = new GameObject("Icon");
            icon.transform.SetParent(go.transform, false);
            WorldInteractionLayer.Apply(icon.AddComponent<SpriteRenderer>());
            AbsoluteZero.Core.Combat.DuelSpriteFacing.Register(go);
            return go;
        }

        void EnsureItemAnchors(int slot)
        {
            if (_itemAnchors[slot] != null) return;
            var root = new GameObject($"RemoteInventoryAnchors_{slot}").transform;
            root.SetParent(transform, false);
            _itemAnchors[slot] = new Transform[InventoryPresentationSlots.Capacity];
            for (int i = 0; i < _itemAnchors[slot].Length; i++)
            {
                var anchor = new GameObject($"Slot_{i:00}").transform;
                anchor.SetParent(root, false);
                anchor.position = ItemPosition(slot, i);
                _itemAnchors[slot][i] = anchor;
            }
            int columns = slot == 1 ? InventoryPresentationSlots.Capacity : InventoryPresentationSlots.Capacity / 2;
            int rows = InventoryPresentationSlots.Capacity / columns;
            var first = _itemAnchors[slot][0].position;
            var last = _itemAnchors[slot][InventoryPresentationSlots.Capacity - 1].position;
            var step = _itemAnchors[slot][1].position - first;
            InventorySlotGrid.Create(root, (first + last) * .5f,
                step, columns, rows, step.magnitude, slot == 1 ? step.magnitude : RemoteRowSpacing);
        }

        void PositionLocalSlotPlaces()
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            if (views == null) return;
            var anchors = new Transform[InventoryPresentationSlots.Capacity];
            for (int i = 0; i < anchors.Length; i++)
            {
                anchors[i] = views.GetItemAnchor(true, i);
                if (anchors[i] == null) return;
            }
            // Keep the Ready button gap. No anchor or item is moved.
            foreach (var anchor in anchors)
            {
                var oldPlace = anchor.Find("ItemPlace")?.GetComponent<SpriteRenderer>();
                if (oldPlace != null) oldPlace.enabled = false;
            }
            CreateLocalStrip(anchors, 0, 8, 1f);
            CreateLocalStrip(anchors, 8, 2, 1.2f);
            CreateLocalStrip(anchors, 10, 2, 1.2f);
            _localSlotPlacesReady = true;
        }

        void CreateLocalStrip(Transform[] anchors, int start, int count, float depth)
        {
            var first = anchors[start].position;
            var last = anchors[start + count - 1].position;
            var step = (last - first) / (count - 1);
            InventorySlotGrid.Create(transform, (first + last) * .5f,
                step, count, 1, step.magnitude, depth);
        }

        Vector3 ItemPosition(int slot, int index)
        {
            int columns = slot == 1 ? InventoryPresentationSlots.Capacity : InventoryPresentationSlots.Capacity / 2;
            const float fit = 8f / InventoryPresentationSlots.Capacity;
            int column = index % columns;
            int row = index / columns;
            float spacing = slot == 1 ? RemoteColumnSpacing : SideColumnSpacing;
            float across = (column - (columns - 1) * 0.5f) * spacing * fit;
            float inward = slot == 1 ? 0f : (0.5f - row) * RemoteRowSpacing;
            return MultiItemFacing.RackPosition(RackOrigin(slot), LocalIceboxPosition, across, inward);
        }

        Vector3 FloorPosition(int slot)
        {
            var anchor = _mapLayout != null ? _mapLayout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, slot) : null;
            return anchor != null ? anchor.transform.position : RemotePlayerPositions[slot] - Vector3.up * 1.6f;
        }

        Vector3 RackOrigin(int slot) => FloorPosition(slot) + InventoryFloorOffsets[slot];

        // Expose the same stable row used by rendered items, in this viewer's seat frame.
        public bool TryGetItemSweep(int seat, out Vector3 first, out Vector3 last)
        {
            first = last = default;
            if (_localSeat < 0 || seat == _localSeat || !_bindings.ContainsKey((byte)seat)) return false;
            int slot = AZPlayerVisual.GetRemoteVisualSlot(seat, _localSeat);
            if (slot < 0 || slot >= _itemAnchors.Length) return false;
            EnsureItemAnchors(slot);
            int columns = slot == 1 ? InventoryPresentationSlots.Capacity : InventoryPresentationSlots.Capacity / 2;
            first = _itemAnchors[slot][0].position;
            last = _itemAnchors[slot][columns - 1].position;
            return true;
        }

        static Bounds VisibleSpriteBounds(Sprite sprite)
        {
            if (sprite == null) return new Bounds(Vector3.zero, Vector3.one);
            // Tight sprite vertices exclude the atlas's transparent padding. The
            // full 256px rect makes small artwork (e.g. tea) nearly invisible.
            var vertices = sprite.vertices;
            if (vertices.Length == 0) return sprite.bounds;
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            return bounds;
        }

        static GameObject FindSceneObject(string objectName)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == objectName) return candidate.gameObject;
            return null;
        }

        static void DestroyView(GameObject view)
        {
            if (view == null) return;
            view.SetActive(false);
            Destroy(view);
        }

        static void Unbind(RemoteInventoryBinding binding)
        {
            binding.Views.Clear(DestroyView);
            binding.Slots.Clear();
        }
    }
}
