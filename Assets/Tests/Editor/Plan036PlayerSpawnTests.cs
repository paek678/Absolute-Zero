using System.Collections.Generic;
using System.Reflection;
using AbsoluteZero.Core.Network;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Tests
{
    public class Plan036PlayerSpawnTests
    {
        readonly List<Scene> _scenes = new();
        GameObject _owner;
        PlayerSpawnManager _spawner;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Plan036 spawn unit fixture");
            _owner.SetActive(false);
            _spawner = _owner.AddComponent<PlayerSpawnManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
            foreach (var scene in _scenes) EditorSceneManager.ClosePreviewScene(scene);
            _scenes.Clear();
        }

        [Test]
        public void MarkerResolutionUsesOnlyRequestedSceneAndSeatOrder()
        {
            var oldScene = NewScene();
            var currentScene = NewScene();
            var old = Marker(oldScene, 0, new Vector3(-50, 1, 0));
            var second = Marker(currentScene, 1, new Vector3(20, 1, 0));
            var first = Marker(currentScene, 0, new Vector3(10, 1, 0));
            Set("spawnPoints", new[] { old.transform });
            Resolve(oldScene);
            Assert.That(_spawner.GetSpawnPositionBySeat(0), Is.EqualTo(old.transform.position));
            Resolve(currentScene);
            Assert.That(_spawner.GetSpawnPositionBySeat(0), Is.EqualTo(first.transform.position));
            Assert.That(_spawner.GetSpawnPositionBySeat(1), Is.EqualTo(second.transform.position));
            Assert.That(Points.Count, Is.EqualTo(2), "Previous-scene serialized transforms must not survive.");
        }

        [Test]
        public void ExplicitCurrentScenePointsKeepOrderWithoutDuplicatingMarkers()
        {
            var current = NewScene();
            var first = Marker(current, 10, new Vector3(10, 2, 3));
            var second = Marker(current, 0, new Vector3(20, 2, 3));
            Set("spawnPoints", new[] { first.transform, first.transform, null });
            Resolve(current);
            Assert.That(Points.Count, Is.EqualTo(2));
            Assert.That(_spawner.GetSpawnPositionBySeat(0), Is.EqualTo(first.transform.position));
            Assert.That(_spawner.GetSpawnPositionBySeat(1), Is.EqualTo(second.transform.position));
        }

        [Test]
        public void InactiveRootMarkersRequireTheExplicitIncludeFlag()
        {
            var current = NewScene();
            var hidden = Marker(current, 0, new Vector3(91, 1, 0));
            hidden.gameObject.SetActive(false);
            var visible = Marker(current, 1, new Vector3(92, 1, 0));
            Resolve(current);
            Assert.That(Points.Count, Is.EqualTo(1));
            Assert.That(_spawner.GetSpawnPositionBySeat(0), Is.EqualTo(visible.transform.position));
            Set("includeInactiveSceneSpawnMarkers", true);
            Resolve(current);
            Assert.That(Points.Count, Is.EqualTo(2));
            Assert.That(_spawner.GetSpawnPositionBySeat(0), Is.EqualTo(hidden.transform.position));
        }

        [Test]
        public void EmptyNextSceneCannotReusePreviousSceneMarkers()
        {
            var oldScene = NewScene();
            Marker(oldScene, 0, new Vector3(300, 100, -20));
            Resolve(oldScene);
            var emptyScene = NewScene();
            Resolve(emptyScene);
            Assert.That(Points, Is.Empty);
            var position = _spawner.GetSpawnPositionBySeat(0);
            Assert.That(position.y, Is.EqualTo(1f));
            Assert.That(new Vector2(position.x, position.z).magnitude, Is.EqualTo(5f).Within(0.0001f),
                "Online's existing circular fallback remains available after fresh resolution.");
        }

        [Test]
        public void DispatcherDefaultReplacementPreservesRosterUntilItsExplicitRestore()
        {
            var dispatcher = _owner.AddComponent<DisconnectDispatcher>();
            var first = new Handler();
            var second = new Handler();
            var roster = new Handler();
            dispatcher.SetDefaultHandler(first);
            dispatcher.SetDefaultHandler(second);
            Assert.That(CurrentHandler(dispatcher), Is.SameAs(second));
            dispatcher.SetHandler(roster);
            dispatcher.ClearDefaultIfCurrent(first);
            Assert.That(CurrentHandler(dispatcher), Is.SameAs(roster));
            dispatcher.SetDefaultHandler(first);
            Assert.That(CurrentHandler(dispatcher), Is.SameAs(roster), "Replacing PSM must not bypass Multi's snapshot handler.");
            dispatcher.RestoreDefaultIfCurrent(second);
            Assert.That(CurrentHandler(dispatcher), Is.SameAs(roster));
            dispatcher.RestoreDefaultIfCurrent(roster);
            Assert.That(CurrentHandler(dispatcher), Is.SameAs(first));
            dispatcher.ClearDefaultIfCurrent(first);
            Assert.That(CurrentHandler(dispatcher), Is.Null);
        }

        Scene NewScene()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            _scenes.Add(scene);
            return scene;
        }

        static PlayerSpawnPoint3D Marker(Scene scene, int order, Vector3 position)
        {
            var obj = new GameObject("Seat " + order);
            SceneManager.MoveGameObjectToScene(obj, scene);
            obj.transform.position = position;
            var marker = obj.AddComponent<PlayerSpawnPoint3D>();
            typeof(PlayerSpawnPoint3D).GetField("order", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(marker, order);
            return marker;
        }

        List<Transform> Points => (List<Transform>)typeof(PlayerSpawnManager)
            .GetField("resolvedSpawnPoints", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_spawner);
        void Set(string name, object value) => typeof(PlayerSpawnManager)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_spawner, value);
        void Resolve(Scene scene) => typeof(PlayerSpawnManager)
            .GetMethod("ResolveSpawnPoints", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_spawner, new object[] { scene });
        static object CurrentHandler(DisconnectDispatcher dispatcher) => typeof(DisconnectDispatcher)
            .GetField("_currentHandler", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dispatcher);
        sealed class Handler : IDisconnectHandler { public void OnPlayerDisconnected(ulong clientId) { } }
    }
}
