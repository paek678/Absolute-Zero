using System.Collections.Generic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Core.Combat
{
    // Screen-aligned artwork: camera orientation, never LookAt(camera.position).
    // Borrow transforms for this camera's render, then return them before gameplay runs.
    [DisallowMultipleComponent]
    public sealed class DuelSpriteFacing : MonoBehaviour
    {
        public static event System.Action<Camera> ArtworkAligned;
        readonly HashSet<Transform> _roots = new();
        readonly List<(Transform root, Quaternion localRotation, Vector3 position)> _borrowed = new();
        readonly List<SpriteRenderer> _renderers = new();
        readonly HashSet<Transform> _worldUi = new();
        Camera _camera;

        public static bool UsesTurningCamera(GameMode mode)
            => EnvironmentPanDegrees(mode) > 0f;

        public static float EnvironmentPanDegrees(GameMode mode, EnvironmentType environment)
            => environment is EnvironmentType.Kids or EnvironmentType.Ambulance
                ? EnvironmentPanDegrees(mode) : 0f;

        public static float EnvironmentPanDegrees(GameMode mode) => mode switch
        {
            // Four-player framing already occupies both edges of the screen.
            GameMode.Multi => 3f,
            GameMode.OneVsOne or GameMode.Solo => 25f,
            _ => 0f
        };

        public static bool UsesScreenAlignedArt(GameMode mode)
            => mode == GameMode.OneVsOne || mode == GameMode.Solo || mode == GameMode.Multi;

        public static void Install(GameObject owner)
        {
            var camera = MatchViewBindings.ForScene(owner.scene)?.GameplayCamera ?? Camera.main;
            if (camera == null || camera.gameObject.scene != owner.scene) return;
            var facing = camera.GetComponent<DuelSpriteFacing>();
            if (facing == null) facing = camera.gameObject.AddComponent<DuelSpriteFacing>();
            facing.DiscoverScene();
        }

        public static void Register(GameObject root)
        {
            if (root == null) return;
            var camera = MatchViewBindings.ForScene(root.scene)?.GameplayCamera ?? Camera.main;
            if (camera == null || camera.gameObject.scene != root.scene) return;
            var facing = camera.GetComponent<DuelSpriteFacing>();
            if (facing == null) facing = camera.gameObject.AddComponent<DuelSpriteFacing>();
            facing.AddRoot(root.transform);
        }

        public static void RegisterWorldUi(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;
            var camera = MatchViewBindings.ForScene(canvas.gameObject.scene)?.GameplayCamera ?? Camera.main;
            if (camera == null || camera.gameObject.scene != canvas.gameObject.scene) return;
            var facing = camera.GetComponent<DuelSpriteFacing>();
            if (facing == null) facing = camera.gameObject.AddComponent<DuelSpriteFacing>();
            facing._worldUi.Add(canvas.transform);
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginRender;
            RenderPipelineManager.endCameraRendering += EndRender;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginRender;
            RenderPipelineManager.endCameraRendering -= EndRender;
            Restore();
        }

        public void DiscoverScene()
        {
            var maps = new List<Transform>();
            foreach (var sceneRoot in gameObject.scene.GetRootGameObjects())
                foreach (var map in sceneRoot.GetComponentsInChildren<MapSceneBinding>(true))
                    if (map.EnvironmentInstance != null) maps.Add(map.EnvironmentInstance.transform);
            foreach (var sceneRoot in gameObject.scene.GetRootGameObjects())
                foreach (var sprite in sceneRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    bool backdrop = false;
                    foreach (var map in maps)
                        if (sprite.transform.IsChildOf(map)) { backdrop = true; break; }
                    if (!backdrop) AddRoot(FindArtworkRoot(sprite));
                }
            foreach (var sceneRoot in gameObject.scene.GetRootGameObjects())
                foreach (var canvas in sceneRoot.GetComponentsInChildren<Canvas>(true))
                    if (canvas.renderMode == RenderMode.WorldSpace) _worldUi.Add(canvas.transform);
        }

        public static Transform FindArtworkRoot(SpriteRenderer sprite)
        {
            if (sprite == null || sprite.GetComponentInParent<Canvas>(true) != null
                || sprite.GetComponentInParent<Camera>(true) != null
                || sprite.GetComponentInParent<FPSVisualController>(true) != null) return null;
            // Floor decals are deliberately horizontal, not standing illustrations.
            if (Mathf.Abs(Vector3.Dot(sprite.transform.forward, Vector3.up)) > .8f) return null;
            var root = sprite.transform;
            for (var parent = sprite.transform; parent != null; parent = parent.parent)
                if (parent.GetComponent<Animator>() != null || parent.GetComponent<SortingGroup>() != null
                    || parent.GetComponent<ItemWorldView>() != null) root = parent;
            return root;
        }

        public void AddRoot(Transform root)
        {
            if (root == null || root.gameObject.scene != gameObject.scene || root.IsChildOf(transform)
                || root.GetComponentInParent<FPSVisualController>(true) != null
                || root.GetComponentInParent<Canvas>(true) != null) return;
            _roots.RemoveWhere(value => value == null);
            foreach (var label in root.GetComponentsInChildren<TextMesh>(true)) _worldUi.Add(label.transform);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.renderMode == RenderMode.WorldSpace) _worldUi.Add(canvas.transform);
            foreach (var existing in _roots)
                if (root.IsChildOf(existing)) return;
            _roots.RemoveWhere(value => value.IsChildOf(root));
            _roots.Add(root);
        }

        void BeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera) return;
            var mode = MatchCompositionRoot.Instance?.ActiveConfig?.Mode;
            if (!mode.HasValue || !UsesScreenAlignedArt(mode.Value)) return;
            ApplyFacing(camera.transform.rotation);
            ArtworkAligned?.Invoke(camera);
        }

        void LateUpdate()
        {
            var mode = MatchCompositionRoot.Instance?.ActiveConfig?.Mode;
            if (_camera == null || !mode.HasValue || !UsesScreenAlignedArt(mode.Value)) return;
            // Keep interactive world UI's raycast plane consistent outside rendering too.
            _worldUi.RemoveWhere(value => value == null);
            foreach (var ui in _worldUi)
                if (ui.gameObject.activeInHierarchy) ui.rotation = _camera.transform.rotation;
        }

        public static Quaternion ScreenAlignedRotation(Quaternion cameraRotation, Quaternion artRotation)
        {
            // Retain the artwork's in-plane animation lean, discard its out-of-plane tilt.
            float length = Mathf.Sqrt(artRotation.z * artRotation.z + artRotation.w * artRotation.w);
            var roll = length > .0001f
                ? new Quaternion(0, 0, artRotation.z / length, artRotation.w / length)
                : Quaternion.identity;
            return cameraRotation * roll;
        }

        // Public for focused pose/cleanup validation. No authoritative state is touched.
        public void ApplyFacing(Quaternion cameraRotation)
        {
            Restore();
            _roots.RemoveWhere(value => value == null);
            foreach (var root in _roots)
            {
                if (!root.gameObject.activeInHierarchy) continue;
                _borrowed.Add((root, root.localRotation, root.position));
                root.GetComponentsInChildren(false, _renderers);
                float bottom = BottomHeight(_renderers);
                root.rotation = ScreenAlignedRotation(cameraRotation, root.rotation);
                float rotatedBottom = BottomHeight(_renderers);
                if (!float.IsPositiveInfinity(bottom) && !float.IsPositiveInfinity(rotatedBottom))
                    root.position += Vector3.up * (bottom - rotatedBottom);
                if (root.TryGetComponent<AbsoluteZero.Core.Inventory.InventoryFloorContact>(out var contact))
                    contact.Apply();
            }
            // Labels and attached canvases stay upright, including under a leaning actor.
            _worldUi.RemoveWhere(value => value == null);
            foreach (var ui in _worldUi)
            {
                if (!ui.gameObject.activeInHierarchy) continue;
                _borrowed.Add((ui, ui.localRotation, ui.position));
                ui.rotation = cameraRotation;
            }
        }

        static float BottomHeight(List<SpriteRenderer> renderers)
        {
            float bottom = float.PositiveInfinity;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                var bounds = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, 0));
                        bottom = Mathf.Min(bottom, renderer.transform.TransformPoint(corner).y);
                    }
            }
            return bottom;
        }

        void EndRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _camera) Restore();
        }

        public void Restore()
        {
            for (int i = _borrowed.Count - 1; i >= 0; --i)
                if (_borrowed[i].root != null)
                {
                    _borrowed[i].root.localRotation = _borrowed[i].localRotation;
                    _borrowed[i].root.position = _borrowed[i].position;
                }
            _borrowed.Clear();
        }
    }
}
