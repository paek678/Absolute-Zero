using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan048EnvironmentTests
    {
        [TestCase(GameMode.OneVsOne)]
        [TestCase(GameMode.Solo)]
        [TestCase(GameMode.Multi)]
        public void SelectionUsesOneDrawAndOnlyTheModePoolAcrossRoundReseeds(GameMode mode)
        {
            var saved = Random.state;
            var expected = mode == GameMode.Multi
                ? new[] { EnvironmentType.CoolBreeze, EnvironmentType.Kids, EnvironmentType.Ambulance, EnvironmentType.SummerVacation }
                : new[] { EnvironmentType.SunnyDay, EnvironmentType.CoolBreeze,
                    EnvironmentType.Kids, EnvironmentType.Ambulance, EnvironmentType.SummerVacation, EnvironmentType.HeatWaveWarning };
            var seen = new HashSet<EnvironmentType>();
            try
            {
                var service = new EnvironmentRuleService();
                for (int round = 0; round < 20; round++)
                {
                    Random.InitState(4800 + round);
                    for (int i = 0; i < 100; i++)
                    {
                        var before = Random.state;
                        var value = expected[Random.Range(0, expected.Length)];
                        var after = Random.state;
                        Random.state = before;
                        var actual = service.SelectRandom(mode);
                        Assert.That(actual, Is.EqualTo(value));
                        Assert.That(Random.state, Is.EqualTo(after));
                        seen.Add(actual);
                    }
                }
                Assert.That(seen, Is.EquivalentTo(expected));
            }
            finally { Random.state = saved; }
        }

        [TestCase("GameScene", "pavilion-day")]
        [TestCase("GameScene_Solo", "pavilion-day")]
        [TestCase("GameScene_Multi", "pavilion-night")]
        public void ShippedSceneUsesRequiredMapAndValidAnchors(string name, string id)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
            try
            {
                var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MapSceneBinding>(true)).Single();
                Assert.That(map.AppliedMap.Id, Is.EqualTo(id));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(map.EnvironmentInstance), Is.EqualTo(map.AppliedMap.EnvironmentPrefab));
                var layout = map.EnvironmentInstance.GetComponent<MapCharacterLayout>();
                Assert.That(layout.TryValidateMode(name == "GameScene_Multi" ? MapLayoutMode.Multi : MapLayoutMode.Duel, out var error), Is.True, error);
                Assert.That(map.EnvironmentInstance.GetComponentsInChildren<Light>().Count(l => l.enabled && l.type == LightType.Directional), Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
