using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Match
{
    public enum MatchSpriteRole
    {
        Freeze1, Freeze2, Freeze3, FanBody, FanBlades, FanGrille,
        CatSleep, CatWakeup, CatJump, CatJump2, CatRummage, BannedTape
    }

    // Scene references, not placement data or a second participant registry.
    [DisallowMultipleComponent]
    public sealed class MatchViewBindings : MonoBehaviour
    {
        [Serializable]
        sealed class SpriteReference
        {
            public MatchSpriteRole role;
            public Sprite sprite;
        }
        [Serializable]
        sealed class FpsSpriteReference
        {
            public string trigger;
            public Sprite sprite;
        }

        [SerializeField] ItemPresentationCatalogSO catalog;
        [SerializeField] Camera gameplayCamera;
        [SerializeField] Transform fpsSpawn;
        [SerializeField] Transform iceboxSpawn;
        [SerializeField] Transform localStayItem;
        [SerializeField] Transform opponentStayItem;
        [SerializeField] Transform[] localItems = Array.Empty<Transform>();
        [SerializeField] Transform[] opponentItems = Array.Empty<Transform>();
        [SerializeField] Transform[] remoteVisuals = Array.Empty<Transform>();
        [SerializeField] MapSceneBinding map;
        [SerializeField] RuntimeAnimatorController fpsController;
        [SerializeField] FPSVisualController authoredFps;
        [SerializeField] SpriteReference[] sprites = Array.Empty<SpriteReference>();
        [SerializeField] FpsSpriteReference[] fpsSprites = Array.Empty<FpsSpriteReference>();

        FPSVisualController _fps;
        readonly Dictionary<byte, (PlayerBinding binding, PlayerSeatMarker marker)> _targets = new();

        internal void RegisterTarget(PlayerBinding binding, PlayerSeatMarker marker)
        {
            if (binding?.IsValid != true || marker == null || marker.Player != binding.State
                || marker.gameObject.scene != gameObject.scene) return;
            _targets[binding.Identity.PlayerIndex] = (binding, marker);
        }

        internal void ReleaseTarget(PlayerSeatMarker marker, PlayerState owner)
        {
            if (marker != null && _targets.TryGetValue(marker.SeatIndex, out var entry)
                && entry.marker == marker && entry.binding.State == owner)
                _targets.Remove(marker.SeatIndex);
        }

        public Transform GetTarget(byte seat)
        {
            if (!_targets.TryGetValue(seat, out var entry) || entry.marker == null
                || !entry.marker.isActiveAndEnabled || entry.binding?.IsValid != true
                || entry.marker.Player != entry.binding.State
                || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetBinding(seat, out var current)
                || !ReferenceEquals(current, entry.binding)) return null;
            return entry.marker.transform;
        }
        public ItemPresentationCatalogSO Catalog => catalog;
        public Camera GameplayCamera => gameplayCamera;
        public Transform FpsSpawn => fpsSpawn;
        public Transform IceboxSpawn => iceboxSpawn;
        public Transform LocalStayItem => localStayItem;
        public Transform OpponentStayItem => opponentStayItem;
        public RuntimeAnimatorController FpsController => fpsController;
        public FPSVisualController AuthoredFps => authoredFps;
        // An Editor map swap replaces the environment, not this stable scene binding.
        public MapCharacterLayout MapLayout => map != null && map.EnvironmentInstance != null
            ? map.EnvironmentInstance.GetComponent<MapCharacterLayout>() : null;
        public int RemoteVisualCount => remoteVisuals?.Length ?? 0;

        public Transform GetItemAnchor(bool local, int index) => At(local ? localItems : opponentItems, index);
        public Transform GetRemoteVisual(int localRelativeSlot) => At(remoteVisuals, localRelativeSlot);
        static Transform At(Transform[] values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : null;

        public Sprite GetSprite(MatchSpriteRole role)
        {
            if (sprites != null) foreach (var value in sprites)
                if (value != null && value.role == role) return value.sprite;
            return null;
        }
        public Sprite GetFpsSprite(string trigger)
        {
            if (fpsSprites != null) foreach (var value in fpsSprites)
                if (value != null && value.trigger == trigger) return value.sprite;
            return null;
        }

        public static MatchViewBindings ForScene(Scene scene)
        {
            var root = MatchCompositionRoot.Instance;
            return root != null && root.IsSessionCurrent && root.gameObject.scene == scene ? root.ViewBindings : null;
        }

        internal bool RegisterFps(FPSVisualController view, PlayerBinding binding)
        {
            if (view == null || view.gameObject.scene != gameObject.scene || binding == null || !binding.IsValid
                || binding.State.gameObject.scene != gameObject.scene
                || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !ReferenceEquals(perspective.HumanBinding, binding)
                || !ReferenceEquals(view.BoundHuman, binding)) return false;
            if (_fps != null && _fps != view) return false;
            _fps = view;
            return true;
        }
        internal void ReleaseFps(FPSVisualController view)
        {
            if (_fps == view) _fps = null;
        }
        public FPSVisualController GetBoundFps()
        {
            if (_fps == null || _fps.BoundHuman?.IsValid != true
                || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !ReferenceEquals(perspective.HumanBinding, _fps.BoundHuman)) return null;
            return _fps;
        }

        // AZPlayerVisual still owns the existing PlayerSeatMarker registration/cleanup.
        public Transform GetBoundRemoteVisual(PlayerBinding binding)
        {
            if (binding?.IsValid != true || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetBinding(binding.Identity.PlayerIndex, out var current)
                || !ReferenceEquals(binding, current)) return null;
            var visual = GetRemoteVisual(AZPlayerVisual.GetRemoteVisualSlot(binding.Identity.PlayerIndex, perspective.HumanSeat));
            return visual != null && visual.GetComponent<PlayerSeatMarker>()?.Player == binding.State ? visual : null;
        }

        public bool Validate(IReadOnlyList<ItemDataSO> registry, List<string> errors)
        {
            int start = errors.Count;
            if (catalog == null) errors.Add("Item presentation catalog is missing.");
            else catalog.Validate(registry, errors);
            var seen = new HashSet<Transform>();
            Check(gameplayCamera != null ? gameplayCamera.transform : null, "gameplay camera", seen, errors);
            Check(fpsSpawn, "FPS spawn", seen, errors);
            Check(iceboxSpawn, "icebox spawn", seen, errors);
            Check(localStayItem, "local stay item", seen, errors);
            Check(opponentStayItem, "opponent stay item", seen, errors);
            CheckArray(localItems, 12, "local item", seen, errors);
            CheckArray(opponentItems, 12, "opponent item", seen, errors);
            if (RemoteVisualCount != 1 && RemoteVisualCount != 3) errors.Add("Expected one or three remote visual slots.");
            CheckArray(remoteVisuals, RemoteVisualCount, "remote visual", seen, errors);
            Check(map != null ? map.transform : null, "map binding", seen, errors);
            if (MapLayout == null) errors.Add("Map environment has no character layout.");
            if (fpsController == null) errors.Add("FPS animator controller is missing.");
            if (authoredFps != null && authoredFps.gameObject.scene != gameObject.scene)
                errors.Add("Cross-scene authored FPS reference.");
            var roles = new HashSet<MatchSpriteRole>();
            if (sprites != null) foreach (var value in sprites)
            {
                if (value == null || !roles.Add(value.role)) errors.Add("Duplicate or null shared sprite role.");
            }
            foreach (MatchSpriteRole role in Enum.GetValues(typeof(MatchSpriteRole)))
                if (GetSprite(role) == null) errors.Add($"Shared sprite is missing: {role}");
            var triggers = new HashSet<string>(StringComparer.Ordinal);
            if (fpsSprites != null) foreach (var value in fpsSprites)
                if (value == null || string.IsNullOrEmpty(value.trigger) || value.sprite == null || !triggers.Add(value.trigger))
                    errors.Add("Invalid or duplicate FPS sprite binding.");
            foreach (var trigger in new[] { "gun", "tape", "fan", "mask", "card", "eat", "hug", "swing", "defence", "use", "feed" })
                if (GetFpsSprite(trigger) == null) errors.Add($"FPS fallback sprite is missing: {trigger}");
            // FPS and PlayerSeatMarker are created only after valid participant metadata.
            return errors.Count == start;
        }

        void CheckArray(Transform[] values, int count, string role, HashSet<Transform> seen, List<string> errors)
        {
            if (values == null || values.Length != count) { errors.Add($"Incorrect {role} anchor count; expected {count}."); return; }
            for (int i = 0; i < values.Length; i++) Check(values[i], $"{role} {i}", seen, errors);
        }
        void Check(Transform value, string role, HashSet<Transform> seen, List<string> errors)
        {
            if (value == null) errors.Add($"Missing {role} reference.");
            else if (value.gameObject.scene != gameObject.scene) errors.Add($"Cross-scene {role} reference.");
            else if (!seen.Add(value)) errors.Add($"Duplicate {role} reference.");
        }
    }
}
