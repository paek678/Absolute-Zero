using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.Loading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037SavedPresentationTests
    {
        [TestCase("Assets/Scenes/GameScene.unity", 1)]
        [TestCase("Assets/Scenes/GameScene_Multi.unity", 3)]
        [TestCase("Assets/Scenes/GameScene_Solo.unity", 1)]
        public void SavedBindingsRetainLegacyArtAndSurviveAnchorRename(string path, int remoteCount)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var views = roots.SelectMany(g => g.GetComponentsInChildren<MatchViewBindings>(true)).Single();
                var composition = roots.SelectMany(g => g.GetComponentsInChildren<MatchCompositionRoot>(true)).Single();
                Assert.AreSame(views, composition.ViewBindings);
                Assert.IsTrue(new SerializedObject(composition).FindProperty("requireViewBindings").boolValue);
                var items = roots.SelectMany(g => g.GetComponentsInChildren<ItemManager>(true)).Single().GetAllItems();
                var errors = new List<string>(); Assert.IsTrue(views.Validate(items, errors), string.Join("; ", errors));
                Assert.AreEqual(remoteCount, views.RemoteVisualCount); Assert.AreEqual(21, views.Catalog.Count);
                Assert.AreSame(roots.SelectMany(g => g.GetComponentsInChildren<FPSVisualController>(true)).SingleOrDefault(), views.AuthoredFps);
                Assert.IsNull(views.GetBoundFps());
                var overrides = (Dictionary<string, string>)typeof(GameAudioManager).GetField("ItemNameToSfx", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                var sounds = (Dictionary<string, string>)typeof(GameAudioManager).GetField("TriggerToSfx", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                foreach (var item in items)
                {
                    Assert.IsTrue(views.Catalog.TryGet(item, out var entry));
                    Assert.AreSame(GameSprites.GetItemSprite(item.ItemName), GameSprites.GetItemSpriteFor(item, views.Catalog), item.name);
                    Assert.AreEqual(ItemPresentation.LegacyChoreography(item.ItemName), entry.Choreography, item.name);
                    foreach (var trigger in new[] { item.AnimTrigger, item.OpponentAnimTrigger, "feed", "eat", "" })
                    {
                        AudioClip expected = null;
                        if (overrides.TryGetValue(item.ItemName, out var sound)
                            || (!string.IsNullOrEmpty(trigger) && sounds.TryGetValue(trigger, out sound)))
                            expected = Resources.Load<AudioClip>("Audio/" + sound);
                        Assert.IsTrue(views.Catalog.TryResolveAudio(item, trigger, out var clip)); Assert.AreSame(expected, clip, item.name + " / " + trigger);
                    }
                }
                foreach (var trigger in new[] { "gun", "tape", "fan", "mask", "card", "eat", "hug" })
                    Assert.AreSame(Resources.LoadAll<Sprite>("FPS/FPS_" + trigger)[0], views.GetFpsSprite(trigger));
                Assert.AreSame(views.GetFpsSprite("fan"), views.GetFpsSprite("swing"));
                Assert.AreSame(views.GetFpsSprite("mask"), views.GetFpsSprite("defence"));
                Assert.AreSame(views.GetFpsSprite("gun"), views.GetFpsSprite("use"));
                Assert.AreSame(views.GetFpsSprite("eat"), views.GetFpsSprite("feed"));
                var anchor = views.GetItemAnchor(true, 0); var position = anchor.position;
                anchor.name = "Renamed by level author";
                views.GetRemoteVisual(0).name = "Renamed character view";
                errors.Clear(); Assert.IsTrue(views.Validate(items, errors), string.Join("; ", errors));
                Assert.AreSame(anchor, views.GetItemAnchor(true, 0)); Assert.AreEqual(position, anchor.position);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void LobbyLoadingTipsHaveExplicitIdentityBeforeMatchRootExists()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
            try
            {
                var loading = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LoadingScreenManager>(true)).Single();
                var so = new SerializedObject(loading);
                var catalog = (ItemPresentationCatalogSO)so.FindProperty("presentationCatalog").objectReferenceValue;
                Assert.NotNull(catalog);
                var tips = so.FindProperty("tipItems"); Assert.AreEqual(21, tips.arraySize);
                for (int i = 0; i < tips.arraySize; i++)
                {
                    var item = (ItemDataSO)tips.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.NotNull(item); Assert.IsTrue(catalog.TryGet(item, out var entry)); Assert.NotNull(entry.Sprite);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
