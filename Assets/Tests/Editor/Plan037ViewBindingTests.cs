using System.Collections.Generic;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037ViewBindingTests
    {
        readonly List<Scene> _scenes = new();
        Scene Scene() { var value = EditorSceneManager.NewPreviewScene(); _scenes.Add(value); return value; }
        static GameObject Create(Scene scene, string name)
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
        }
        static void Set(Object owner, string field, Object value)
        {
            var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        [TearDown] public void TearDown()
        {
            foreach (var scene in _scenes) EditorSceneManager.ClosePreviewScene(scene);
            _scenes.Clear();
        }

        [Test] public void MissingAndCrossSceneReferencesAreReportedWithoutRequiringLateFps()
        {
            var scene = Scene(); var other = Scene();
            var bindings = Create(scene, "Bindings").AddComponent<MatchViewBindings>();
            Set(bindings, "gameplayCamera", Create(other, "OtherCamera").AddComponent<Camera>());
            var errors = new List<string>(); Assert.IsFalse(bindings.Validate(null, errors));
            Assert.Contains("Cross-scene gameplay camera reference.", errors);
            Assert.Contains("Missing FPS spawn reference.", errors);
            Assert.IsFalse(errors.Exists(e => e.Contains("late") || e.Contains("PlayerSeatMarker")));
            Assert.IsNull(bindings.GetBoundFps()); Assert.IsNull(bindings.GetBoundRemoteVisual(null));
            Assert.IsFalse(bindings.RegisterFps(null, null));
        }

        [Test] public void DuplicateAnchorsCannotSilentlyStackItemsAndBoundsDoNotWrap()
        {
            var scene = Scene(); var bindings = Create(scene, "Bindings").AddComponent<MatchViewBindings>();
            var shared = Create(scene, "Anchor").transform;
            var so = new SerializedObject(bindings); var array = so.FindProperty("localItems"); array.arraySize = 12;
            for (int i = 0; i < 12; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = shared;
            so.ApplyModifiedPropertiesWithoutUndo();
            var errors = new List<string>(); bindings.Validate(null, errors);
            Assert.IsTrue(errors.Exists(e => e.StartsWith("Duplicate local item")));
            Assert.IsNull(bindings.GetItemAnchor(true, -1)); Assert.IsNull(bindings.GetItemAnchor(true, 12));
            Assert.AreSame(shared, bindings.GetItemAnchor(true, 0));
            Assert.IsNull(bindings.GetRemoteVisual(3));
        }

        [Test] public void MapReplacementResolvesCurrentEnvironmentWithoutCopyingTransforms()
        {
            var scene = Scene(); var bindings = Create(scene, "Bindings").AddComponent<MatchViewBindings>();
            var map = Create(scene, "Map").AddComponent<MapSceneBinding>(); Set(bindings, "map", map);
            var first = Create(scene, "First").AddComponent<MapCharacterLayout>();
            var second = Create(scene, "Second").AddComponent<MapCharacterLayout>();
            second.transform.position = new Vector3(7, 8, 9);
            Set(map, "_environmentInstance", first.gameObject); Assert.AreSame(first, bindings.MapLayout);
            Set(map, "_environmentInstance", second.gameObject); Assert.AreSame(second, bindings.MapLayout);
            Assert.AreEqual(new Vector3(7, 8, 9), second.transform.position);
            Object.DestroyImmediate(second.gameObject); Assert.IsNull(bindings.MapLayout);
        }
    }
}
