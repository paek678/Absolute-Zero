using System;
using AbsoluteZero.Core.Common;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan040SettingsTests
    {
        sealed class Store : ILocalSettingsStore
        {
            public LocalSettingsSnapshot Value = new(1);
            public bool Fail;
            public int Saves;
            public Action DuringSave;
            public LocalSettingsSnapshot Load() => Value;
            public bool Save(LocalSettingsSnapshot value, out string error)
            {
                Saves++; DuringSave?.Invoke();
                error = Fail ? "injected" : null;
                if (!Fail) Value = value;
                return !Fail;
            }
        }
        [Test] public void DefaultsAndCorruptRangesPreserveSafePresentationValues()
        {
            var defaults = new LocalSettingsSnapshot(1);
            Assert.That(defaults.Shake); Assert.That(defaults.Fullscreen, Is.EqualTo(-1));
            var bad = new LocalSettingsSnapshot(float.NaN, -2, float.PositiveInfinity, false, 4);
            Assert.That(bad.Master, Is.EqualTo(1)); Assert.That(bad.Bgm, Is.Zero);
            Assert.That(bad.Sfx, Is.EqualTo(1)); Assert.That(bad.Fullscreen, Is.EqualTo(-1));
        }
        [TestCase(0f)] [TestCase(.5f)] [TestCase(1f)]
        public void VolumesPersistExactlyAndDuplicateCommandsDoNotSave(float volume)
        {
            var store = new Store(); var service = new LocalSettingsService(store);
            service.SetVolumes(volume, .25f, .75f);
            Assert.That(store.Value.Master, Is.EqualTo(volume)); Assert.That(store.Value.Bgm, Is.EqualTo(.25f));
            Assert.That(store.Value.Sfx, Is.EqualTo(.75f));
            service.SetVolumes(volume, .25f, .75f); Assert.That(store.Saves, Is.EqualTo(1)); Assert.That(service.HasUnsaved, Is.False);
        }
        [Test] public void FailedSaveStillAppliesButLastSavedStaysUntilExplicitRetry()
        {
            var store = new Store { Fail = true }; var service = new LocalSettingsService(store);
            service.SetShake(false);
            Assert.That(service.Current.Shake, Is.False); Assert.That(service.LastSaved.Shake); Assert.That(service.HasUnsaved);
            store.Fail = false; Assert.That(service.Save()); Assert.That(service.LastSaved.Shake, Is.False);
        }
        [Test] public void ReentrantChangesCannotBeMarkedAsSavedByAnOlderWrite()
        {
            var store = new Store(); var service = new LocalSettingsService(store);
            store.DuringSave = () => { store.DuringSave = null; service.SetShake(false); };
            service.SetVolumes(.5f, 1, 1);
            Assert.That(service.HasUnsaved); Assert.That(service.LastSaved.Shake);
            Assert.That(service.Save()); Assert.That(service.LastSaved.Shake, Is.False);
        }
        [Test] public void LegacyKeysLoadWithoutWritingNewPreferences()
        {
            string prefix = "p40test_" + Guid.NewGuid().ToString("N") + "_";
            try
            {
                PlayerPrefs.SetFloat(prefix + "bgm_volume", .2f); PlayerPrefs.SetFloat(prefix + "sfx_volume", .8f);
                var service = new LocalSettingsService(new PlayerPrefsSettingsStore(prefix));
                Assert.That(service.Current.Master, Is.EqualTo(1)); Assert.That(service.Current.Bgm, Is.EqualTo(.2f));
                Assert.That(service.Current.Sfx, Is.EqualTo(.8f)); Assert.That(service.Current.Fullscreen, Is.EqualTo(-1));
                Assert.That(PlayerPrefs.HasKey(prefix + "master_volume"), Is.False);
            }
            finally { Cleanup(prefix); }
        }
        [TestCase(false)] [TestCase(true)]
        public void FailedFlushRestoresOnlyOwnedCachedValuesAndReportsRollbackFailure(bool failRollback)
        {
            string prefix = "p40test_" + Guid.NewGuid().ToString("N") + "_";
            int calls = 0;
            try
            {
                PlayerPrefs.SetFloat(prefix + "bgm_volume", .2f); PlayerPrefs.SetString(prefix + "unrelated", "keep");
                var store = new PlayerPrefsSettingsStore(prefix, () => { if (++calls == 1 || failRollback) throw new InvalidOperationException("injected flush"); });
                Assert.That(store.Save(new LocalSettingsSnapshot(.3f, .4f, .5f, false, 1), out var error), Is.False);
                Assert.That(PlayerPrefs.GetFloat(prefix + "bgm_volume"), Is.EqualTo(.2f));
                Assert.That(PlayerPrefs.HasKey(prefix + "master_volume"), Is.False);
                Assert.That(PlayerPrefs.GetString(prefix + "unrelated"), Is.EqualTo("keep"));
                Assert.That(error.Contains("rollback unconfirmed"), Is.EqualTo(failRollback));
            }
            finally { PlayerPrefs.DeleteKey(prefix + "unrelated"); Cleanup(prefix); }
        }
        [Test] public void OffsetDoesNotAccumulateOrRestoreOverAnotherOwner()
        {
            Vector3 position = new(4, 5, 6);
            var effect = new OwnedPositionOffset(() => position, next => position = next);
            effect.Apply(Vector3.one); effect.Apply(Vector3.one * 2);
            Assert.That(position, Is.EqualTo(new Vector3(6, 7, 8)));
            effect.Clear(); effect.Clear(); Assert.That(position, Is.EqualTo(new Vector3(4, 5, 6)));
            effect.Apply(Vector3.one); position = new Vector3(10, 20, 30); effect.Clear();
            Assert.That(position, Is.EqualTo(new Vector3(10, 20, 30)));
            effect.Apply(Vector3.one); effect.Apply(Vector3.zero); effect.Clear();
            Assert.That(position, Is.EqualTo(new Vector3(10, 20, 30)));
        }
        [Test] public void OffsetCanBeClearedAfterItsRectTransformWasDestroyed()
        {
            var go = new GameObject("Offset lifetime", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            var effect = OwnedPositionOffset.ForRectTransform(rect);
            effect.Apply(Vector3.one);
            UnityEngine.Object.DestroyImmediate(go);
            Assert.DoesNotThrow(() => { effect.Clear(); effect.Clear(); effect.Apply(Vector3.one); effect.Clear(); });
        }

        [Test] public void RectOffsetPreservesALayoutChangeAndCanBeReused()
        {
            var go = new GameObject("Offset reuse", typeof(RectTransform));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(10, 20);
                var effect = OwnedPositionOffset.ForRectTransform(rect);
                effect.Apply(Vector3.one);
                rect.anchoredPosition = new Vector2(30, 40);
                effect.Clear();
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(30, 40)));
                effect.Apply(Vector3.one); effect.Clear(); effect.Clear();
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(30, 40)));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void Cleanup(string prefix)
        {
            foreach (string key in new[] { "master_volume", "bgm_volume", "sfx_volume", "screen_shake", "fullscreen" }) PlayerPrefs.DeleteKey(prefix + key);
            PlayerPrefs.Save();
        }
    }
}
