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
        }

        static readonly Vector3[] RemotePlayerPositions =
        {
            new(-4.8f, 1.60f, 5.1f),
            new(0f, 1.60f, 8.8f),
            new(4.8f, 1.60f, 5.1f)
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

        // Centered behind the local item row so the Ready control and arrow tail stay visible.
        static readonly Vector3 LocalIceboxPosition = new(0f, 0.05f, 2.4f);
        static readonly Vector3 InventoryFloorOffset = new(0f, 0.85f, -3.4f);
        const float RemoteIconSize = 0.54f;
        const float RemoteColumnSpacing = 0.72f;
        const float RemoteRowSpacing = 0.76f;

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
                return;
            }
            _localPlayer = perspective.HumanBinding.State;
            _localSeat = perspective.HumanSeat;
            var players = MatchCompositionRoot.Instance.Registry.Players;

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
            int visibleIndex = 0;
            binding.Views.Reconcile(snapshot, (i, state) => CreateRemoteItem(state.ItemId),
                (go, i, state) => {
                    var item = ItemManager.Instance.GetItemData(state.ItemId);
                    go.name = $"RemoteSeat{binding.Player.PlayerIndex}_Item{i}_{item.ItemName}";
                    // Two rows on the sides, one row in front of the north seat so
                    // the south-center icebox has room to open. Stolen items may
                    // exceed the normal eight: fit extra columns inside the same area.
                    int columns = slot == 1 ? Mathf.Max(8, snapshot.Count) : Mathf.Max(4, Mathf.CeilToInt(snapshot.Count / 2f));
                    float fit = (slot == 1 ? 8f : 4f) / columns;
                    go.transform.position = ItemPosition(slot, visibleIndex++, columns, fit);
                    go.transform.rotation = _inventoryFacing;
                    go.transform.localScale = Vector3.one;
                    var renderer = go.GetComponentInChildren<SpriteRenderer>();
                    renderer.sprite = GameSprites.GetItemSpriteFor(item);
                    var art = VisibleSpriteBounds(renderer.sprite);
                    Vector2 size = art.size;
                    float largestSide = Mathf.Max(size.x, size.y);
                    float scale = largestSide > 0f ? RemoteIconSize * fit / largestSide : 1f;
                    renderer.transform.localScale = Vector3.one * scale;
                    // Imported pivots differ. Center the icon inside its cell while
                    // keeping text size and the cell's transform independent of art.
                    renderer.transform.localPosition = -art.center * scale;
                    var label = go.GetComponentInChildren<TextMesh>();
                    label.transform.localPosition = new Vector3(0f, -0.35f * fit, -0.01f);
                    label.characterSize = 0.07f * fit;
                    label.text = state.IsUnlimited ? "--" : state.RemainingUses.ToString();
                }, DestroyView, go => go != null);
        }

        GameObject CreateRemoteItem(short itemId)
        {
            if (ItemManager.Instance?.GetItemData(itemId) == null) return null;
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            var icon = new GameObject("Icon");
            icon.transform.SetParent(go.transform, false);
            icon.AddComponent<SpriteRenderer>().sortingOrder = 8;
            var label = new GameObject("Uses");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, -0.35f, -0.01f);
            var text = label.AddComponent<TextMesh>();
            text.fontSize = 24;
            text.characterSize = 0.07f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            label.GetComponent<MeshRenderer>().sortingOrder = 9;
            return go;
        }

        Vector3 ItemPosition(int slot, int index, int columns, float fit)
        {
            int column = index % columns;
            int row = index / columns;
            float across = (column - (columns - 1) * 0.5f) * RemoteColumnSpacing * fit;
            float down = slot == 1 ? 0f : (0.5f - row) * RemoteRowSpacing;
            var anchor = _mapLayout != null ? _mapLayout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, slot) : null;
            var floor = anchor != null ? anchor.transform.position : RemotePlayerPositions[slot] - Vector3.up * 1.6f;
            var orientation = anchor != null ? anchor.transform.rotation : Quaternion.identity;
            return floor + orientation * InventoryFloorOffset + _inventoryFacing * new Vector3(across, down, 0f);
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

        static void Unbind(RemoteInventoryBinding binding) => binding.Views.Clear(DestroyView);
    }
}
