using System;
using AbsoluteZero.Core.Cosmetic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class LobbyCharacterPresenter : IDisposable
    {
        readonly CosmeticEquipmentService _equipment;
        readonly CosmeticEquipState _state;
        readonly SpriteRenderer _fallback;
        readonly Transform _source;
        readonly CosmeticRegistrySO _registry;
        readonly Rect _visibleRect;
        readonly Sprite _lobbyFace;
        PrivateCosmeticPreview _visual;
        public GameObject VisualRoot => _visual?.Root;

        public LobbyCharacterPresenter(Transform source, CosmeticRegistrySO registry, SpriteRenderer fallback,
            CosmeticEquipmentService equipment, CosmeticEquipState state, Rect visibleRect, Sprite lobbyFace = null)
        {
            _source = source; _registry = registry; _fallback = fallback; _equipment = equipment; _state = state;
            _visibleRect = visibleRect;
            _lobbyFace = lobbyFace;
            _fallback.enabled = true;
            _state.OnEquipChanged += Refresh;
            Refresh();
        }

        void Refresh()
        {
            PrivateCosmeticPreview candidate = null;
            try
            {
                // Build off-screen/inactive and swap only on success. Failed overlays cannot damage the last good avatar.
                candidate = new PrivateCosmeticPreview(_source, _registry, _fallback.gameObject.layer);
                var root = candidate.Root.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.localScale = Vector3.one;
                if (!candidate.Apply(new CosmeticSnapshot(new CosmeticDto(), 0))) throw new InvalidOperationException("Base visual is invalid.");
                bool found = false; Bounds bounds = default;
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.enabled && sr.sprite != null && sr.gameObject.activeSelf)
                    { if (!found) { bounds = sr.bounds; found = true; } else bounds.Encapsulate(sr.bounds); }
                if (!found || bounds.size.y <= 0) throw new InvalidOperationException("Base visual has no bounds.");
                var target = _fallback.bounds;
                target = new Bounds(new Vector3(target.min.x + target.size.x * _visibleRect.center.x,
                    target.min.y + target.size.y * _visibleRect.center.y, target.center.z),
                    new Vector3(target.size.x * _visibleRect.width, target.size.y * _visibleRect.height, target.size.z));
                float scale = target.size.y / bounds.size.y;
                root.localScale = Vector3.one * scale;
                root.position = new Vector3(target.center.x - bounds.center.x * scale,
                    target.min.y - bounds.min.y * scale, _fallback.transform.position.z);
                var group = candidate.Root.GetComponent<SortingGroup>();
                if (group == null) group = candidate.Root.AddComponent<SortingGroup>();
                group.sortingLayerID = _fallback.sortingLayerID; group.sortingOrder = _fallback.sortingOrder;
                if (!candidate.Apply(_equipment.Snapshot)) throw new InvalidOperationException("Equipped visual is invalid.");
                // Lobby-only expression on the private copy. Shared closet/game art stays in its idle pose.
                if (_lobbyFace != null)
                {
                    var head = root.Find("body/head")?.GetComponent<SpriteRenderer>();
                    if (head == null) throw new InvalidOperationException("Lobby expression head is missing.");
                    head.sprite = _lobbyFace;
                    root.GetComponent<CosmeticAtlasRenderer>().ApplyFrame();
                }
                candidate.Root.name = "LobbyEquippedCharacter";
                candidate.Root.SetActive(true);
                _visual?.Dispose(); _visual = candidate; candidate = null;
                _fallback.enabled = false;
            }
            catch (Exception error) { Debug.LogWarning("[LobbyCharacter] Retained previous appearance: " + error.Message); }
            finally { candidate?.Dispose(); }
        }

        public void Dispose()
        {
            _state.OnEquipChanged -= Refresh;
            _visual?.Dispose(); _visual = null;
            if (_fallback != null) _fallback.enabled = true;
        }
    }
}
