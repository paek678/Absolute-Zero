using System.Collections.Generic;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037PresentationCatalogTests
    {
        readonly List<Object> _objects = new();
        T Own<T>(T value) where T : Object { _objects.Add(value); return value; }
        T Create<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
        [TearDown] public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        ItemPresentationCatalogSO Catalog(ItemDataSO item, Sprite sprite, AudioClip itemAudio = null,
            AudioClip triggerAudio = null, int copies = 1)
        {
            var catalog = Create<ItemPresentationCatalogSO>();
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries"); entries.arraySize = copies;
            for (int i = 0; i < copies; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                entry.FindPropertyRelative("audioOverride").objectReferenceValue = itemAudio;
                entry.FindPropertyRelative("choreography").enumValueIndex = (int)ItemChoreography.Feed;
            }
            if (triggerAudio != null)
            {
                var sounds = so.FindProperty("triggerAudio"); sounds.arraySize = 1;
                var sound = sounds.GetArrayElementAtIndex(0);
                sound.FindPropertyRelative("trigger").stringValue = "feed";
                sound.FindPropertyRelative("clip").objectReferenceValue = triggerAudio;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        Sprite Sprite() => Own(UnityEngine.Sprite.Create(Own(new Texture2D(2, 2)), new Rect(0, 0, 2, 2), Vector2.zero));
        AudioClip Clip(string name) => Own(AudioClip.Create(name, 10, 1, 44100, false));

        [Test] public void IdentitySurvivesDisplayRenameAndDoesNotFallBackToIcon()
        {
            var item = Create<AttackItemDataSO>(); item.ItemName = "Before"; item.Icon = Sprite();
            item.AnimTrigger = "feed"; item.AnimDuration = 1.5f;
            var sprite = Sprite(); var clip = Clip("use");
            var catalog = Catalog(item, sprite, clip);
            string authored = EditorJsonUtility.ToJson(catalog);
            item.ItemName = "Localized label"; item.AnimDuration = 3f;
            Assert.IsTrue(catalog.TryGet(item, out var entry));
            Assert.AreSame(sprite, entry.Sprite); Assert.AreNotSame(item.Icon, entry.Sprite);
            Assert.AreEqual(ItemChoreography.Feed, entry.Choreography);
            Assert.AreEqual(3f, entry.Item.AnimDuration, "Timing has only one authored owner.");
            Assert.IsTrue(catalog.TryResolveAudio(item, "feed", out var sound)); Assert.AreSame(clip, sound);
            Assert.AreEqual(authored, EditorJsonUtility.ToJson(catalog), "Runtime reads must not mutate the asset.");
            var lookalike = Create<AttackItemDataSO>(); lookalike.ItemName = item.ItemName;
            Assert.IsFalse(catalog.TryGet(lookalike, out _));
            Assert.IsFalse(catalog.TryGet(null, out _));
        }

        [Test] public void ItemAudioOverridePrecedesTriggerAndSilentTriggerIsSuccessful()
        {
            var item = Create<AttackItemDataSO>(); var sprite = Sprite();
            var special = Clip("special"); var fallback = Clip("feed");
            var catalog = Catalog(item, sprite, special, fallback);
            Assert.IsTrue(catalog.TryResolveAudio(item, "feed", out var clip)); Assert.AreSame(special, clip);
            catalog = Catalog(item, sprite, null, fallback);
            Assert.IsTrue(catalog.TryResolveAudio(item, "feed", out clip)); Assert.AreSame(fallback, clip);
            Assert.IsTrue(catalog.TryResolveAudio(item, "use", out clip)); Assert.IsNull(clip);
            Assert.IsTrue(catalog.TryResolveAudio(item, null, out clip)); Assert.IsNull(clip);
            Assert.IsFalse(catalog.TryResolveAudio(Create<AttackItemDataSO>(), "feed", out clip));
        }

        [Test] public void ValidationRejectsMissingSpritesDuplicateIdentityAndUnmappedRegistry()
        {
            var item = Create<AttackItemDataSO>(); var missing = Create<AttackItemDataSO>();
            var catalog = Catalog(item, null, copies: 2);
            var errors = new List<string>();
            Assert.IsFalse(catalog.Validate(new[] { item, missing }, errors));
            Assert.IsTrue(errors.Exists(e => e.StartsWith("Duplicate presentation")));
            Assert.IsTrue(errors.Exists(e => e.StartsWith("Unmapped registry")));
            Assert.IsTrue(errors.Exists(e => e.StartsWith("Missing item sprite")));
            catalog = Catalog(item, Sprite()); errors.Clear();
            Assert.IsTrue(catalog.Validate(new[] { item }, errors), "An item need not have an audio override.");
            Assert.IsEmpty(errors);
        }

        [Test] public void DuplicateOrNullTriggerAudioIsRejectedBeforeUse()
        {
            var item = Create<AttackItemDataSO>(); var catalog = Catalog(item, Sprite(), triggerAudio: Clip("feed"));
            var so = new SerializedObject(catalog); var sounds = so.FindProperty("triggerAudio"); sounds.arraySize = 2;
            sounds.GetArrayElementAtIndex(1).FindPropertyRelative("clip").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            var errors = new List<string>(); Assert.IsFalse(catalog.Validate(new[] { item }, errors));
            Assert.IsTrue(errors.Exists(e => e.StartsWith("Duplicate audio trigger")));
            Assert.IsTrue(errors.Exists(e => e.StartsWith("Audio trigger has no clip")));
        }
    }
}
