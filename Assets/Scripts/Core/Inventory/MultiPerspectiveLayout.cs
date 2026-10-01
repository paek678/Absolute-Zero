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
            new(-4.35f, 1.60f, 4.45f),
            new(0f, 1.60f, 7.85f),
            new(4.35f, 1.60f, 4.45f)
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

        // Centered behind the local item row so the Ready control and arrow tail stay visible.
        static readonly Vector3 LocalIceboxPosition = new(0f, 0.05f, 3.75f);

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
                    go.transform.position = ItemPosition(slot, visibleIndex++);
                    var anchor = _mapLayout != null ? _mapLayout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, slot) : null;
                    go.transform.rotation = (anchor != null ? anchor.transform.rotation : Quaternion.identity)
                        * Quaternion.Euler(0f, 0f, ItemRotation(slot));
                    var renderer = go.GetComponent<SpriteRenderer>();
                    renderer.sprite = GameSprites.GetItemSpriteFor(item);
                    Vector2 size = renderer.sprite != null ? renderer.sprite.bounds.size : Vector2.zero;
                    float largestSide = Mathf.Max(size.x, size.y);
                    go.transform.localScale = Vector3.one * (largestSide > 0f ? 0.72f / largestSide : 0.38f);
                    go.GetComponentInChildren<TextMesh>().text = state.IsUnlimited ? "--" : state.RemainingUses.ToString();
                }, DestroyView, go => go != null);
        }

        GameObject CreateRemoteItem(short itemId)
        {
            if (ItemManager.Instance?.GetItemData(itemId) == null) return null;
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>().sortingOrder = 8;
            var label = new GameObject("Uses");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, -0.72f, -0.01f);
            var text = label.AddComponent<TextMesh>();
            text.fontSize = 24;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            label.GetComponent<MeshRenderer>().sortingOrder = 9;
            return go;
        }

        Vector3 ItemPosition(int slot, int index)
        {
            int column = index % 4;
            int row = index / 4;
            float across = (column - 1.5f) * 0.62f;
            float inward = row * 0.72f;

            var position = slot switch
            {
                0 => new Vector3(-3.20f + inward, 0.72f, 4.45f + across),
                1 => new Vector3(across, 0.72f, 6.55f - inward),
                2 => new Vector3(3.20f - inward, 0.72f, 4.45f - across),
                _ => Vector3.zero
            };
            var anchor = _mapLayout != null ? _mapLayout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, slot) : null;
            return anchor != null
                ? anchor.VisualPosition + anchor.transform.rotation * (position - RemotePlayerPositions[slot])
                : position;
        }

        static float ItemRotation(int slot) => slot switch
        {
            0 => -90f,
            1 => 180f,
            2 => 90f,
            _ => 0f
        };

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
