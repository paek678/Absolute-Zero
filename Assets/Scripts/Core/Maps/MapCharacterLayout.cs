using AbsoluteZero.Core.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Maps
{
    // Immutable placement configuration. Participants and local visual slots remain separate identities.
    [DisallowMultipleComponent]
    public sealed class MapCharacterLayout : MonoBehaviour
    {
        [SerializeField] MapCharacterAnchor[] _anchors;
        public static MapLayoutMode ModeFor(GameMode mode) => mode == GameMode.Multi ? MapLayoutMode.Multi : MapLayoutMode.Duel;
        public static int Capacity(MapLayoutMode mode) => mode == MapLayoutMode.Multi ? 4 : 2;

        public MapCharacterAnchor GetAnchor(MapLayoutMode mode, MapAnchorRole role, int index)
        {
            MapCharacterAnchor result = null;
            if (_anchors == null) return null;
            foreach (var anchor in _anchors)
            {
                if (anchor == null || anchor.Mode != mode || anchor.Role != role || anchor.Index != index) continue;
                if (result != null || !anchor.IsUsable(transform)) return null;
                result = anchor;
            }
            return result;
        }

        public bool TryGetSpawnAnchors(MapLayoutMode mode, out Transform[] points, out string error)
        {
            int count = Capacity(mode);
            points = new Transform[count];
            if (!TryValidateMode(mode, out error)) { points = null; return false; }
            for (int seat = 0; seat < count; seat++)
            {
                var anchor = GetAnchor(mode, MapAnchorRole.ServerSpawn, seat);
                if (anchor == null)
                {
                    points = null;
                    error = $"Map {name} requires one active {mode} spawn anchor for seat {seat}.";
                    return false;
                }
                points[seat] = anchor.transform;
            }
            error = null;
            return true;
        }

        public bool TryValidateMode(MapLayoutMode mode, out string error)
        {
            error = $"Map {name}: invalid or duplicate {mode} character anchors.";
            if (_anchors == null) return false;
            foreach (var anchor in _anchors)
                if (anchor == null || (anchor.Mode != MapLayoutMode.Duel && anchor.Mode != MapLayoutMode.Multi)
                    || (anchor.Role != MapAnchorRole.ServerSpawn && anchor.Role != MapAnchorRole.RemoteVisual)
                    || (anchor.Mode == mode && (anchor.Index < 0 ||
                    anchor.Index >= Capacity(mode) - (anchor.Role == MapAnchorRole.RemoteVisual ? 1 : 0)))) return false;
            foreach (var role in new[] { MapAnchorRole.ServerSpawn, MapAnchorRole.RemoteVisual })
                for (int index = 0; index < Capacity(mode) - (role == MapAnchorRole.RemoteVisual ? 1 : 0); index++)
                    if (GetAnchor(mode, role, index) == null) return false;
            error = null;
            return true;
        }

        public bool TryApplyRemoteVisual(MapLayoutMode mode, int slot, Transform target)
        {
            var anchor = GetAnchor(mode, MapAnchorRole.RemoteVisual, slot);
            if (anchor == null || target == null) return false;
            anchor.ApplyVisualPose(target);
            return true;
        }

        public static bool TryFind(Scene scene, out MapCharacterLayout layout)
        {
            layout = null;
            if (!scene.IsValid() || !scene.isLoaded) return false;
            foreach (var root in scene.GetRootGameObjects())
            {
                var binding = root.GetComponent<MapSceneBinding>();
                if (binding == null || binding.EnvironmentInstance == null) continue;
                layout = binding.EnvironmentInstance.GetComponent<MapCharacterLayout>();
                return layout != null;
            }
            return false;
        }
    }
}
