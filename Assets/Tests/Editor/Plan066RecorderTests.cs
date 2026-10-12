using System;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Solo;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public class Plan066RecorderTests
    {
        string _folder;
        [SetUp] public void Setup() => _folder = Path.Combine(Path.GetTempPath(), "az-plan066-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
        [Test] public void TerminalFlushPreservesOrderAndKoreanText()
        {
            string path = Path.Combine(_folder, "events.jsonl");
            var sink = new PlaytestJsonlSink(path);
            for (int i = 0; i < 100; i++) Assert.True(sink.Append("{\"index\":" + i + ",\"label\":\"빙의\"}"));
            sink.Dispose(); sink.Dispose();
            Assert.True(sink.Complete); Assert.Zero(sink.Dropped); Assert.IsNull(sink.Error);
            Assert.That(sink.Written, Is.EqualTo(100));
            Assert.That(File.ReadAllLines(path).Last(), Does.Contain("99").And.Contain("빙의"));
            Assert.False(File.Exists(path + ".partial"));
        }
        [Test] public void ExistingEvidenceIsNeverOverwritten()
        {
            string path = Path.Combine(_folder, "events.jsonl");
            using (var sink = new PlaytestJsonlSink(path)) sink.Append("{}");
            Assert.Throws<IOException>(() => new PlaytestJsonlSink(path));
        }
        [Test] public void FileFailureCannotBecomeCompleteEvidence()
        {
            string path = Path.Combine(_folder, "events.jsonl");
            Directory.CreateDirectory(path + ".partial");
            var sink = new PlaytestJsonlSink(path); sink.Append("{}"); sink.Dispose();
            Assert.False(sink.Complete); Assert.IsNotNull(sink.Error); Assert.False(File.Exists(path));
        }
        [Test] public void RejectsMultilineRecords()
        {
            using var sink = new PlaytestJsonlSink(Path.Combine(_folder, "events.jsonl"));
            Assert.Throws<ArgumentException>(() => sink.Append("{}\n{}"));
        }
        [Test] public void OverflowRetainsPartialEvidenceAndCannotPass()
        {
            using var gate = new System.Threading.ManualResetEventSlim(false);
            string path = Path.Combine(_folder, "overflow.jsonl");
            var sink = new PlaytestJsonlSink(path, 1, () => gate.Wait());
            try
            {
                Assert.True(sink.Append("{\"index\":1}"));
                Assert.False(sink.Append("{\"index\":2}"));
            }
            finally { gate.Set(); sink.Dispose(); }
            Assert.That(sink.Dropped, Is.EqualTo(1));
            Assert.False(sink.Complete);
            Assert.False(File.Exists(path));
            Assert.That(File.ReadAllText(path + ".partial"), Does.Contain("\"index\":1"));
        }
        [Test] public void SnapshotIncludesPhysicalItemCopiesInSerializedEvidence()
        {
            var record = new PlaytestRecord { seats = new[] { new PlaytestSeatRecord {
                seat = 2, inventory = new[] { PlaytestItemRecord.From(new AbsoluteZero.Core.Item.ItemSlotNetData
                    { ItemId = 3, CopyId = 42, RemainingUses = 2, Flags = 1 }) }
            } } };
            string json = UnityEngine.JsonUtility.ToJson(record);
            Assert.That(json, Does.Contain("\"inventory\"").And.Contain("\"CopyId\":42"));
            var restored = UnityEngine.JsonUtility.FromJson<PlaytestRecord>(json);
            Assert.That(restored.seats[0].inventory[0].RemainingUses, Is.EqualTo(2));
        }
        [Test] public void InactiveCampaignPreservesUnityRandomDrawsExactly()
        {
            var previous = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(6601);
                int integer = UnityEngine.Random.Range(0, 17);
                float real = UnityEngine.Random.Range(0f, 123f);
                var expected = UnityEngine.Random.state;
                UnityEngine.Random.InitState(6601);
                Assert.That(AbsoluteZero.Core.Common.GameplayRandom.Range("item-drop", 0, 17), Is.EqualTo(integer));
                Assert.That(AbsoluteZero.Core.Common.GameplayRandom.Range("item-drop", 0f, 123f), Is.EqualTo(real));
                Assert.That(UnityEngine.Random.state, Is.EqualTo(expected));
            }
            finally { UnityEngine.Random.state = previous; }
        }
        [Test] public void SeededDomainsIgnorePresentationDrawsAndOtherDomains()
        {
            var type = typeof(AbsoluteZero.Core.Common.GameplayRandom);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var reset = type.GetMethod("Reset", flags);
            var previous = UnityEngine.Random.state;
            void Seed()
            {
                reset.Invoke(null, null);
                type.GetField("_resolved", flags).SetValue(null, true);
                type.GetField("_seeded", flags).SetValue(null, true);
                type.GetField("_seed", flags).SetValue(null, 6601);
            }
            try
            {
                Seed();
                var expected = Enumerable.Range(0, 50).Select(_ => AbsoluteZero.Core.Common.GameplayRandom.Range("item-drop", 0, 100000)).ToArray();
                Seed();
                for (int i = 0; i < expected.Length; i++)
                {
                    UnityEngine.Random.Range(0f, 1f);
                    AbsoluteZero.Core.Common.GameplayRandom.Range("environment", 0, 7);
                    Assert.That(AbsoluteZero.Core.Common.GameplayRandom.Range("item-drop", 0, 100000), Is.EqualTo(expected[i]));
                }
            }
            finally { reset.Invoke(null, null); UnityEngine.Random.state = previous; }
        }
    }
}
