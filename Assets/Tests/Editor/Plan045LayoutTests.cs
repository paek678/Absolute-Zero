using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Tests
{
    public sealed class Plan045LayoutTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("PavilionDay")]
        [TestCase("PavilionNight")]
        [TestCase("ForestNight")]
        [TestCase("PavilionNightWide")]
        public void SavedMapMovesOnlyMultiPresentationAnchorsAndPreservesDuelSpawns(string folder)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Maps/{folder}/{folder}.prefab");
            Assert.That(prefab, Is.Not.Null);
            var layout = prefab.GetComponent<MapCharacterLayout>();
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.TryGetSpawnAnchors(MapLayoutMode.Duel, out var duel, out var error), Is.True, error);
            Assert.That(duel.Select(t => t.position), Is.EqualTo(new[] { new Vector3(0, 0, -1), new Vector3(0, 0, 8) }));
            Assert.That(layout.TryGetSpawnAnchors(MapLayoutMode.Multi, out var multi, out error), Is.True, error);
            Assert.That(multi.Select(t => t.position), Is.EqualTo(new[]
            {
                new Vector3(-3, 0, 0), new Vector3(3, 0, 0), new Vector3(-3, 0, 6), new Vector3(3, 0, 6)
            }), "Presentation placement must not move authoritative spawn seats.");
            var expected = new[] { new Vector3(-7.2f, 0, 4.2f), new Vector3(0, 0, 9.2f), new Vector3(7.2f, 0, 4.2f) };
            for (int slot = 0; slot < expected.Length; slot++)
            {
                var anchor = layout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, slot);
                Assert.That(anchor, Is.Not.Null, $"{folder} slot {slot}");
                Assert.That(Vector3.Distance(anchor.transform.position, expected[slot]), Is.LessThan(0.0001f), $"{folder} slot {slot}");
            }
        }

        [Test]
        public void SavedMultiSceneHidesAll24GridRenderersAndRetainsAnchorsAndCharacterSprites()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene_Multi.unity");
            try
            {
                var bindings = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MatchViewBindings>(true)).Single();
                var anchors = new HashSet<Transform>();
                var markers = new HashSet<SpriteRenderer>();
                foreach (bool local in new[] { true, false })
                for (int index = 0; index < 12; index++)
                {
                    var anchor = bindings.GetItemAnchor(local, index);
                    Assert.That(anchor, Is.Not.Null, $"local={local}, index={index}");
                    Assert.That(anchor.gameObject.activeSelf, Is.True, "Hide the graphic without deactivating its anchor.");
                    Assert.That(anchor.gameObject.scene, Is.EqualTo(scene));
                    Assert.That(anchors.Add(anchor), Is.True, "Each item slot retains a distinct serialized anchor.");
                    var marker = anchor.GetComponentsInChildren<SpriteRenderer>(true).Single(renderer => renderer.name == "ItemPlace");
                    Assert.That(marker.enabled, Is.False, anchor.name);
                    Assert.That(marker.gameObject.activeSelf, Is.True, "Keep the marker object and transform for authoring.");
                    Assert.That(marker.sprite, Is.Not.Null, "The sprite reference survives renderer hiding.");
                    markers.Add(marker);
                }
                Assert.That(anchors.Count, Is.EqualTo(24));
                Assert.That(markers.Count, Is.EqualTo(24));
                Assert.That(bindings.RemoteVisualCount, Is.EqualTo(3));
                var visuals = new HashSet<Transform>();
                for (int slot = 0; slot < 3; slot++)
                {
                    var visual = bindings.GetRemoteVisual(slot);
                    Assert.That(visual, Is.Not.Null);
                    Assert.That(visual.gameObject.scene, Is.EqualTo(scene));
                    Assert.That(visuals.Add(visual), Is.True, "Each relative slot retains its own character visual.");
                    Assert.That(visual.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer => renderer.enabled && renderer.sprite != null),
                        Is.True, "Grid hiding must preserve the assigned character artwork.");
                    // AZPlayerVisual.SetupSeatMarker adds/enables the target BoxCollider
                    // at runtime. The actual input fixture checks that collider path.
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void DistributionArcRestoresItsCapturedNonuniformScaleAndDestination()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var owner = new GameObject("Inactive icebox test owner");
                owner.SetActive(false);
                SceneManager.MoveGameObjectToScene(owner, scene);
                var controller = owner.AddComponent<IceboxController>();
                var item = new GameObject("Scaled inventory item");
                SceneManager.MoveGameObjectToScene(item, scene);
                var expectedScale = new Vector3(0.46f, 0.83f, 0.65f);
                var destination = new Vector3(2.4f, 0.2f, 3.8f);
                var arc = typeof(IceboxController).GetMethod("ArcMoveTo", Private);
                Assert.That(arc, Is.Not.Null);
                var routine = (IEnumerator)arc.Invoke(controller, new object[] { item.transform, Vector3.zero, destination, expectedScale });
                Assert.That(routine.MoveNext(), Is.True, "Enter the actual distribution coroutine.");

                // EditMode has no reliable Time.deltaTime. Advance only the iterator's
                // elapsed clock, then execute its real completion branch. Neither the
                // output position nor the output scale is injected by this test.
                var elapsed = routine.GetType().GetFields(Private).Single(field => field.FieldType == typeof(float) && field.Name.Contains("elapsed"));
                elapsed.SetValue(routine, 1f);
                item.transform.position = Vector3.one * -10;
                item.transform.localScale = Vector3.one * 0.1f;
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(item.transform.position, Is.EqualTo(destination));
                Assert.That(item.transform.localScale, Is.EqualTo(expectedScale), "Completion must preserve each item's size instead of restoring Vector3.one.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void IceboxPresentationScaleSurvivesBothAuthoredAnimationClips()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            IceboxController controller = null;
            var presentationRootField = typeof(IceboxController).GetField("_iceboxPresentationRoot", Private);
            try
            {
                var owner = new GameObject("Inactive scaled icebox owner");
                owner.SetActive(false);
                SceneManager.MoveGameObjectToScene(owner, scene);
                controller = owner.AddComponent<IceboxController>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Icebox.prefab");
                Assert.That(prefab, Is.Not.Null);
                var scaled = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var original = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                typeof(IceboxController).GetField("_iceboxInstance", Private).SetValue(controller, scaled);

                Assert.That(controller.TrySetPresentationScale(0.55f), Is.True);
                var wrapper = scaled.transform.parent;
                Assert.That(wrapper, Is.Not.Null);
                Assert.That(wrapper, Is.Not.SameAs(owner.transform), "The size owner must sit outside the animated prefab root.");
                Assert.That(controller.TrySetPresentationScale(0.55f), Is.True);
                Assert.That(scaled.transform.parent, Is.SameAs(wrapper), "Repeated sizing reuses one wrapper.");
                bool observedAnimatedScale = false;
                foreach (string state in new[] { "open", "close" })
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Art/gameGem/MainFolder/Animations/iceboxA_{state}.anim");
                    Assert.That(clip, Is.Not.Null);
                    foreach (float time in new[] { 0f, 0.25f, 0.3f, clip.length })
                    {
                        clip.SampleAnimation(original, time);
                        clip.SampleAnimation(scaled, time);
                        observedAnimatedScale |= Vector3.Distance(original.transform.localScale, Vector3.one) > 0.001f;
                        Assert.That(Vector3.Distance(wrapper.localScale, Vector3.one * 0.55f), Is.LessThan(0.0001f), $"{state} at {time}");
                        Assert.That(Vector3.Distance(scaled.transform.lossyScale, original.transform.lossyScale * 0.55f), Is.LessThan(0.0001f),
                            $"{state} at {time}: the animated shape keeps the 0.55 presentation multiplier.");
                    }
                }
                Assert.That(observedAnimatedScale, Is.True, "These samples must exercise actual root scale animation.");
            }
            finally
            {
                // The preview scene owns and destroys the wrapper; avoid requesting
                // delayed PlayMode destruction from the controller's OnDestroy hook.
                if (controller != null && presentationRootField != null) presentationRootField.SetValue(controller, null);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
