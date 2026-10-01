using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Cosmetic;
using UnityEngine;

namespace AbsoluteZero.UI.LobbyUI
{
    // One screen owns its visual clone, camera, render target and temporary camera masks.
    [DefaultExecutionOrder(11000)]
    [RequireComponent(typeof(ClosetViewBindings))]
    public sealed class ClosetPreviewSurface : MonoBehaviour
    {
        public GameObject VisualPrefab;
        public CosmeticRegistrySO Registry;
        public string PreviewLayer = "ClosetPreview";
        [Range(1.05f, 2f)] public float FramePadding = 1.16f;
        readonly Dictionary<Camera, (int original, int applied)> _masks = new();
        ClosetViewBindings _view;
        PrivateCosmeticPreview _visual;
        Camera _camera;
        RenderTexture _texture;
        CosmeticSnapshot _applied, _requested;
        Bounds _frame;
        int _layer;
        bool _failed;
        internal Func<int, int, RenderTexture> AllocateTarget = CreateTarget;
        public Camera PreviewCamera => _camera;
        public RenderTexture Texture => _texture;
        public GameObject VisualRoot => _visual?.Root;

        public bool Show(CosmeticSnapshot snapshot)
        {
            _requested = snapshot;
            if (!isActiveAndEnabled || snapshot == null) return false;
            _view ??= GetComponent<ClosetViewBindings>();
            try
            {
                if (_visual == null) Create();
                if (_applied == null || !CosmeticCodec.SameIds(_applied.ToDto(), snapshot.ToDto()))
                {
                    if (!_visual.Apply(snapshot)) throw new InvalidOperationException("Invalid preview outfit.");
                    _applied = snapshot;
                }
                _failed = false;
                _view.PreviewHint.gameObject.SetActive(false);
                return true;
            }
            catch (Exception error)
            {
                Fail(error);
                return false;
            }
        }
        void Create()
        {
            _layer = LayerMask.NameToLayer(PreviewLayer);
            if (VisualPrefab == null || Registry == null || _layer < 8)
                throw new InvalidOperationException("Preview art, catalog or reserved layer is missing.");
            _visual = new PrivateCosmeticPreview(VisualPrefab.transform, Registry, _layer);
            _visual.Root.name = "ClosetPreview_Visual";
            _visual.Root.transform.position = new Vector3(10000, 10000, 0);
            _visual.Root.SetActive(true);
            // Measure the catalog once, before the first visible frame. Changing a hat
            // never changes zoom; each part uses the same supplied idle mapping.
            bool found = false;
            _visual.Apply(new CosmeticSnapshot(new CosmeticDto(), 0));
            IncludeBounds(ref found);
            foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart)))
                foreach (var item in Registry.GetByPart(part))
                {
                    if (item == null) continue;
                    var dto = new CosmeticDto(); CosmeticCodec.SetPart(dto, part, item.Id);
                    if (_visual.Apply(new CosmeticSnapshot(dto, 0))) IncludeBounds(ref found);
                }
            if (!found) throw new InvalidOperationException("Preview has no visible sprites.");
            var cameraObject = new GameObject("ClosetPreview_Camera");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true; _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.81f, .87f, .85f, 1);
            _camera.cullingMask = 1 << _layer;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 50;
            _camera.allowHDR = false; _camera.allowMSAA = false;
            _camera.transform.position = new Vector3(_frame.center.x, _frame.center.y, _frame.min.z - 10);
            IsolateSceneCameras();
        }
        void IncludeBounds(ref bool found)
        {
            foreach (var renderer in _visual.Root.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.enabled && renderer.sprite != null)
                {
                    if (!found) { _frame = renderer.bounds; found = true; }
                    else _frame.Encapsulate(renderer.bounds);
                }
        }
        void LateUpdate()
        {
            if (_failed || _visual == null || _camera == null) return;
            try
            {
                _visual.Isolate(); // Includes overlays added after the original clone.
                IsolateSceneCameras();
                var rect = _view.Preview.rectTransform.rect;
                float scale = _view.Preview.canvas != null ? _view.Preview.canvas.scaleFactor : 1;
                int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 64, 1536);
                int height = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 64, 1536);
                if (_texture == null || !_texture.IsCreated() || _texture.width != width || _texture.height != height)
                    Resize(width, height);
                _camera.orthographicSize = Mathf.Max(_frame.extents.y, _frame.extents.x * height / width) * FramePadding;
                _camera.enabled = true; // URP renders after atlas LateUpdate; no manual Camera.Render.
                _view.Preview.enabled = true;
            }
            catch (Exception error) { Fail(error); }
        }
        void IsolateSceneCameras()
        {
            int bit = 1 << _layer;
            foreach (var camera in Camera.allCameras)
            {
                if (camera == _camera || (camera.cullingMask & bit) == 0) continue;
                int original = camera.cullingMask;
                // If another owner has changed the mask, this new value becomes the
                // restoration baseline. Restore only the value still owned by us.
                _masks[camera] = (original, original & ~bit);
                camera.cullingMask = original & ~bit;
            }
        }
        void Resize(int width, int height)
        {
            var next = AllocateTarget(width, height);
            if (next == null) throw new InvalidOperationException("Preview texture allocation failed.");
            var previous = _texture;
            _camera.enabled = false;
            _camera.targetTexture = next; _view.Preview.texture = next; _texture = next;
            if (previous != null) { previous.Release(); Destroy(previous); }
        }
        static RenderTexture CreateTarget(int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = "ClosetPreview_Texture", antiAliasing = 1, useMipMap = false };
            if (target.Create()) return target;
            Destroy(target); return null;
        }
        void Fail(Exception error)
        {
            Release(); _failed = true;
            if (_view != null)
            {
                _view.PreviewHint.text = "미리보기를 표시하지 못했습니다\n다시 열어 주세요";
                _view.PreviewHint.gameObject.SetActive(true);
            }
            Debug.LogWarning("[ClosetPreview] " + error.Message);
        }
        public void Release()
        {
            if (_camera != null) { _camera.enabled = false; _camera.targetTexture = null; }
            if (_view != null) { _view.Preview.texture = null; _view.Preview.enabled = false; }
            if (_texture != null) { _texture.Release(); Destroy(_texture); _texture = null; }
            if (_camera != null) { Destroy(_camera.gameObject); _camera = null; }
            _visual?.Dispose(); _visual = null; _applied = null;
            foreach (var pair in _masks)
                if (pair.Key != null && pair.Key.cullingMask == pair.Value.applied) pair.Key.cullingMask = pair.Value.original;
            _masks.Clear(); _failed = false;
        }
        void OnEnable() { if (_requested != null) Show(_requested); }
        void OnDisable() { Release(); _requested = null; }
        void OnDestroy() => Release();
    }
}
