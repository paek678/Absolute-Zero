using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AbsoluteZero.EditorTools.Maps
{
    // Temporary renderer-only views. Never instantiate gameplay/NGO behaviours for authoring.
    [InitializeOnLoad]
    public static class MapCharacterPreview
    {
        sealed class Entry { public MapCharacterAnchor Anchor; public Transform View; }
        static readonly List<Entry> Entries = new();
        static readonly Dictionary<Renderer, bool> HiddenRenderers = new();
        static GameObject _root;
        static MapCharacterLayout _layout;
        public static MapLayoutMode Mode { get; set; } = MapLayoutMode.Multi;
        public static int LocalSeat { get; set; }
        public static int PlayerCount { get; set; } = 4;
        public static bool ShowServerAnchors { get; set; }
        public static bool IsActive => _root != null;

        static MapCharacterPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Stop(); };
            EditorSceneManager.sceneClosing += (scene, removing) => { if (_root != null && _root.scene == scene) Stop(); };
            EditorApplication.update += Update;
        }

        public static int SeatForRemoteSlot(int slot, int localSeat, int count)
        {
            if (count < 2 || count > 4 || localSeat < 0 || localSeat >= count || slot < 0 || slot >= count - 1) return -1;
            return slot < localSeat ? slot : slot + 1;
        }

        public static void Start(MapCharacterLayout layout, GameObject template)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || layout == null || template == null)
                throw new InvalidOperationException("Select a loaded map and preview character in Edit Mode.");
            if (!layout.TryValidateMode(Mode, out var error)) throw new InvalidOperationException(error);
            if (template.GetComponentsInChildren<SpriteRenderer>(true).Length == 0)
                throw new InvalidOperationException("The preview template needs SpriteRenderer parts.");
            Stop();
            _layout = layout;
            foreach (var sceneRoot in layout.gameObject.scene.GetRootGameObjects())
                foreach (var candidate in sceneRoot.GetComponentsInChildren<Transform>(true))
                    if (candidate.name.StartsWith("EnemyPlayer", StringComparison.Ordinal) || candidate.name == "PreviewOnly_CharacterScaleReferences")
                        foreach (var renderer in candidate.GetComponentsInChildren<Renderer>(true))
                            if (!HiddenRenderers.ContainsKey(renderer)) { HiddenRenderers.Add(renderer, renderer.forceRenderingOff); renderer.forceRenderingOff = true; }
            _root = new GameObject("[Temporary Map Character Preview]") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(_root, layout.gameObject.scene);
            try
            {
                for (int slot = 0; slot < MapCharacterLayout.Capacity(Mode) - 1; slot++)
                {
                    int seat = SeatForRemoteSlot(slot, LocalSeat, PlayerCount);
                    if (seat < 0) continue;
                    var anchor = layout.GetAnchor(Mode, MapAnchorRole.RemoteVisual, slot);
                    var view = CopyRenderers(template.transform, _root.transform, true);
                    view.name = $"P{seat + 1} — display slot {slot}";
                    anchor.ApplyVisualPose(view);
                    Entries.Add(new Entry { Anchor = anchor, View = view });
                }
            }
            catch { Stop(); throw; }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        static Transform CopyRenderers(Transform source, Transform parent, bool isRoot)
        {
            var obj = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave, layer = source.gameObject.layer };
            obj.transform.SetParent(parent, false);
            obj.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            obj.transform.localScale = source.localScale;
            foreach (var renderer in source.GetComponents<SpriteRenderer>())
            {
                var copy = obj.AddComponent<SpriteRenderer>();
                copy.sprite = renderer.sprite; copy.sharedMaterials = renderer.sharedMaterials; copy.color = renderer.color;
                copy.flipX = renderer.flipX; copy.flipY = renderer.flipY; copy.drawMode = renderer.drawMode;
                copy.size = renderer.size; copy.sortingLayerID = renderer.sortingLayerID; copy.sortingOrder = renderer.sortingOrder;
                copy.maskInteraction = renderer.maskInteraction; copy.enabled = renderer.enabled;
            }
            var group = source.GetComponent<SortingGroup>();
            if (group != null) { var copy = obj.AddComponent<SortingGroup>(); copy.sortingLayerID = group.sortingLayerID; copy.sortingOrder = group.sortingOrder; }
            foreach (Transform child in source) CopyRenderers(child, obj.transform, false);
            obj.SetActive(isRoot || source.gameObject.activeSelf);
            return obj.transform;
        }

        static void Update()
        {
            if (_root == null) return;
            if (_layout == null || EditorApplication.isPlayingOrWillChangePlaymode) { Stop(); return; }
            bool changed = false;
            foreach (var entry in Entries)
            {
                if (entry.Anchor == null || entry.View == null) { Stop(); return; }
                if (entry.View.position == entry.Anchor.VisualPosition && entry.View.rotation == entry.Anchor.transform.rotation
                    && entry.View.localScale == entry.Anchor.VisualScale) continue;
                entry.Anchor.ApplyVisualPose(entry.View); changed = true;
            }
            if (changed) { SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate(); }
        }

        public static void Stop()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null; _layout = null; Entries.Clear();
            foreach (var pair in HiddenRenderers) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            HiddenRenderers.Clear();
            SceneView.RepaintAll();
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void DrawAnchor(MapCharacterAnchor anchor, GizmoType flags)
        {
            if (anchor.Mode != Mode || (anchor.Role == MapAnchorRole.ServerSpawn && !ShowServerAnchors)) return;
            int seat = anchor.Role == MapAnchorRole.ServerSpawn ? anchor.Index : SeatForRemoteSlot(anchor.Index, LocalSeat, PlayerCount);
            var color = anchor.Role == MapAnchorRole.ServerSpawn ? Color.cyan : new Color(1f, .76f, .15f);
            var old = Handles.color; Handles.color = color;
            Handles.DrawWireDisc(anchor.transform.position, Vector3.up, .35f);
            Handles.DrawLine(anchor.transform.position, anchor.transform.position + anchor.transform.forward * .8f);
            string who = seat >= 0 && seat < PlayerCount ? $"P{seat + 1} (seat {seat})" : "Empty";
            string role = anchor.Role == MapAnchorRole.ServerSpawn ? "Server spawn" : $"View {anchor.Index} / local P{LocalSeat + 1}";
            Handles.Label(anchor.transform.position + Vector3.up * .25f, who + "\n" + role);
            Handles.color = old;
        }
    }
}
