using System.Collections.Generic;
using System.Reflection;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Tests
{
    public class Plan039MapLayoutTests
    {
        readonly List<Scene> _scenes = new();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void TearDown()
        {
            foreach (var scene in _scenes) EditorSceneManager.ClosePreviewScene(scene);
            _scenes.Clear();
        }

        MapCharacterLayout Create(out Scene scene)
        {
            scene = EditorSceneManager.NewPreviewScene(); _scenes.Add(scene);
            var owner = new GameObject("Map"); SceneManager.MoveGameObjectToScene(owner, scene);
            var binding = owner.AddComponent<MapSceneBinding>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maps/PavilionNight/PavilionNight.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(owner.transform, false);
            typeof(MapSceneBinding).GetField("_environmentInstance", Private).SetValue(binding, instance);
            return instance.GetComponent<MapCharacterLayout>();
        }

        [TestCase("PavilionDay")]
        [TestCase("PavilionNight")]
        [TestCase("ForestNight")]
        [TestCase("PavilionNightWide")]
        public void EveryMapHasValidDuelAndMultiAnchorsWithOriginalDefaults(string folder)
        {
            var map = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Maps/{folder}/{folder}.prefab");
            var layout = map.GetComponent<MapCharacterLayout>();
            Assert.That(layout.TryGetSpawnAnchors(MapLayoutMode.Duel, out var duel, out var error), Is.True, error);
            Assert.That(duel[0].position, Is.EqualTo(new Vector3(0, 0, -1)));
            Assert.That(duel[1].position, Is.EqualTo(new Vector3(0, 0, 8)));
            Assert.That(layout.TryGetSpawnAnchors(MapLayoutMode.Multi, out var multi, out error), Is.True, error);
            Assert.That(multi.Length, Is.EqualTo(4));
            var expected = new[] { new Vector3(-3,0,0), new Vector3(3,0,0), new Vector3(-3,0,6), new Vector3(3,0,6) };
            for (int seat = 0; seat < expected.Length; seat++) Assert.That(multi[seat].position, Is.EqualTo(expected[seat]));
            Assert.That(layout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, 0).VisualPosition, Is.EqualTo(new Vector3(-4.35f, 1.6f, 4.45f)));
            Assert.That(layout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, 1).VisualPosition, Is.EqualTo(new Vector3(0, 1.6f, 7.85f)));
            Assert.That(layout.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual, 2).VisualPosition, Is.EqualTo(new Vector3(4.35f, 1.6f, 4.45f)));
        }

        [Test]
        public void ServerResolverUsesMovedMapSeatBeforeLegacyMarkersAndExplicitPoints()
        {
            var layout = Create(out var scene);
            var seat = layout.GetAnchor(MapLayoutMode.Duel, MapAnchorRole.ServerSpawn, 0);
            seat.transform.position = new Vector3(11, 2, 13);
            var legacy = new GameObject("Legacy marker"); SceneManager.MoveGameObjectToScene(legacy, scene);
            legacy.transform.position = Vector3.one * 999; legacy.AddComponent<PlayerSpawnPoint3D>();
            var owner = new GameObject("Disabled spawner fixture"); owner.SetActive(false); SceneManager.MoveGameObjectToScene(owner, scene);
            var spawner = owner.AddComponent<PlayerSpawnManager>();
            typeof(PlayerSpawnManager).GetField("spawnPoints", Private).SetValue(spawner, new[] { legacy.transform });
            typeof(PlayerSpawnManager).GetMethod("ResolveSpawnPoints", Private).Invoke(spawner, new object[] { scene });
            Assert.That(spawner.GetSpawnPositionBySeat(0), Is.EqualTo(seat.transform.position));
            Assert.That(spawner.GetSpawnPositionBySeat(1), Is.EqualTo(new Vector3(0,0,8)));
            Assert.That(typeof(PlayerSpawnManager).GetField("_mapLayoutError", Private).GetValue(spawner), Is.Null);
        }

        [TestCase(MapLayoutMode.Duel)]
        [TestCase(MapLayoutMode.Multi)]
        public void MissingDuplicateOrInactiveSeatRejectsWithoutCompressingSeatIndices(MapLayoutMode mode)
        {
            var layout = Create(out _);
            var last = layout.GetAnchor(mode, MapAnchorRole.ServerSpawn, MapCharacterLayout.Capacity(mode) - 1);
            last.gameObject.SetActive(false);
            Assert.That(layout.TryGetSpawnAnchors(mode, out var points, out _), Is.False);
            Assert.That(points, Is.Null);
            last.gameObject.SetActive(true);
            typeof(MapCharacterAnchor).GetField("_index", Private).SetValue(last, 0);
            Assert.That(layout.TryGetSpawnAnchors(mode, out points, out _), Is.False);
            Assert.That(points, Is.Null);
        }

        [TestCase(MapLayoutMode.Duel, 0)]
        [TestCase(MapLayoutMode.Multi, 0)]
        [TestCase(MapLayoutMode.Multi, 1)]
        [TestCase(MapLayoutMode.Multi, 2)]
        public void VisualPoseUsesAnchorFeetOffsetRotationAndIndependentScale(MapLayoutMode mode, int slot)
        {
            var layout = Create(out var scene);
            var anchor = layout.GetAnchor(mode, MapAnchorRole.RemoteVisual, slot);
            anchor.transform.SetPositionAndRotation(new Vector3(-8, .4f, 12), Quaternion.Euler(0, 20, 0));
            var parent = new GameObject("Scaled presentation owner"); SceneManager.MoveGameObjectToScene(parent, scene);
            parent.transform.localScale = Vector3.one * 2;
            var view = new GameObject("Visual"); view.transform.SetParent(parent.transform);
            Assert.That(layout.TryApplyRemoteVisual(mode, slot, view.transform), Is.True);
            Assert.That(view.transform.position, Is.EqualTo(anchor.VisualPosition));
            Assert.That(Quaternion.Angle(view.transform.rotation, anchor.transform.rotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(view.transform.lossyScale, anchor.VisualScale), Is.LessThan(.0001f));
        }

        [Test]
        public void LayoutLookupIsSceneScopedAndChangingInstanceDoesNotChangeMapAsset()
        {
            var first = Create(out var a); var second = Create(out var b);
            first.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.ServerSpawn, 0).transform.position = Vector3.one * 123;
            Assert.That(MapCharacterLayout.TryFind(a, out var resolvedA), Is.True); Assert.That(resolvedA, Is.SameAs(first));
            Assert.That(MapCharacterLayout.TryFind(b, out var resolvedB), Is.True); Assert.That(resolvedB, Is.SameAs(second));
            Assert.That(second.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.ServerSpawn, 0).transform.position, Is.EqualTo(new Vector3(-3,0,0)));
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maps/PavilionNight/PavilionNight.prefab").GetComponent<MapCharacterLayout>();
            Assert.That(asset.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.ServerSpawn, 0).transform.position, Is.EqualTo(new Vector3(-3,0,0)));
        }

        [TestCase(GameMode.OneVsOne)]
        [TestCase(GameMode.Solo)]
        public void DuelAndSoloSharePlacementProfile(GameMode mode)
            => Assert.That(MapCharacterLayout.ModeFor(mode), Is.EqualTo(MapLayoutMode.Duel));

        [Test]
        public void AllLocalSeatsRetainExistingRemoteSlotIdentity()
        {
            for (int count = 2; count <= 4; count++)
            for (int local = 0; local < count; local++)
            for (int remote = 0; remote < count; remote++)
            {
                if (remote == local) { Assert.That(AZPlayerVisual.GetRemoteVisualSlot(remote, local), Is.EqualTo(-1)); continue; }
                int slot = AZPlayerVisual.GetRemoteVisualSlot(remote, local);
                Assert.That(slot < local ? slot : slot + 1, Is.EqualTo(remote));
            }
        }
    }
}
