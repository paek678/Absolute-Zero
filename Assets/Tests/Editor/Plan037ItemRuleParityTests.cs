using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037ItemRuleParityTests
    {
        const string GoldenPath = "Assets/Tests/Editor/Plan037ItemRuleGolden.json";
        [Serializable] public sealed class Entry
        {
            public string asset;
            public int temperature, defense, use;
            public string duel, multi3, multi4;
        }
        [Serializable] public sealed class Corpus { public Entry[] entries; }

        // Explicit one-time capture against the pre-extraction implementation.
        // Tests never regenerate their own expected values.
        public static int CaptureBaseline(string tracePath)
        {
            if (File.Exists(GoldenPath)) throw new InvalidOperationException("Baseline already exists.");
            var entries = new List<Entry>();
            var traces = new StringBuilder();
            foreach (string asset in AssetDatabase.FindAssets("t:ItemDataSO", new[] { "Assets/Data/Items" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!ItemAvailability.IsEnabled(AssetDatabase.LoadAssetAtPath<ItemDataSO>(asset))) continue;
                for (int temp = 0; temp < 3; temp++)
                for (int defense = 0; defense < 4; defense++)
                for (int use = 0; use < 2; use++)
                {
                    var entry = new Entry { asset = asset, temperature = temp, defense = defense, use = use };
                    var values = Observe(entry);
                    entry.duel = Digest(values[0]); entry.multi3 = Digest(values[1]); entry.multi4 = Digest(values[2]);
                    entries.Add(entry);
                    traces.AppendLine(JsonUtility.ToJson(entry));
                    foreach (string value in values) traces.AppendLine(value);
                }
            }
            if (entries.Count == 0) throw new InvalidOperationException("No item rules captured.");
            File.WriteAllText(tracePath, traces.ToString());
            File.WriteAllText(GoldenPath, JsonUtility.ToJson(new Corpus { entries = entries.ToArray() }, true));
            AssetDatabase.ImportAsset(GoldenPath);
            return entries.Count;
        }

        static IEnumerable<TestCaseData> Cases()
        {
            if (!File.Exists(GoldenPath)) throw new FileNotFoundException("Frozen item corpus is required.", GoldenPath);
            foreach (var entry in JsonUtility.FromJson<Corpus>(File.ReadAllText(GoldenPath)).entries)
                yield return new TestCaseData(entry).SetName("ItemParity_" + Path.GetFileNameWithoutExtension(entry.asset)
                    + "_T" + entry.temperature + "_D" + entry.defense + "_U" + entry.use);
        }

        [TestCaseSource(nameof(Cases))]
        public void FrozenDuelAndMultiOutputsRemainEqual(Entry entry)
        {
            var observed = Observe(entry);
            var expected = new[] { entry.duel, entry.multi3, entry.multi4 };
            for (int i = 0; i < expected.Length; i++)
                Assert.That(Digest(observed[i]), Is.EqualTo(expected[i]), "Policy " + i + ": " + observed[i]);
        }

        static string[] Observe(Entry entry)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(entry.asset);
            Assert.That(item, Is.Not.Null);
            string beforeAsset = EditorJsonUtility.ToJson(item);
            var userObject = new GameObject("Rule sample actor") { hideFlags = HideFlags.HideAndDontSave };
            var targetObject = new GameObject("Rule sample target") { hideFlags = HideFlags.HideAndDontSave };
            userObject.SetActive(false); targetObject.SetActive(false);
            try
            {
                var user = userObject.AddComponent<PlayerState>();
                var target = targetObject.AddComponent<PlayerState>();
                float[] pair = entry.temperature switch { 0 => new[] { 37f, 12f }, 1 => new[] { 12f, 37f }, _ => new[] { 20f, 20f } };
                user.Temperature.Value = pair[0]; target.Temperature.Value = pair[1];
                byte remaining = (byte)Math.Max(1, (item.MaxUses > 0 ? item.MaxUses : 255) - entry.use);
                var modifiers = new PlayerModifiers[4];
                if (entry.defense != 0)
                    modifiers[1].ActiveDefense = new DefenseInfo { ItemId = 99,
                        Filter = entry.defense == 1 ? DamageFilter.Temperature : entry.defense == 2 ? DamageFilter.Food : DamageFilter.All,
                        BlockAmount = entry.defense == 1 ? 3f : float.MaxValue };
                var context = new ItemContext { User = user, Target = target, UserIndex = 0, TargetIndex = 1,
                    UserSlot = new ItemSlotNetData { ItemId = 10, CopyId = 7, RemainingUses = remaining }, AllModifiers = modifiers };
                var randomBefore = UnityEngine.Random.state;
                string duel = Canonical(item.ComputeEffect(context));
                var results = new[] { duel, Multi(3), Multi(4) };
                Assert.That(UnityEngine.Random.state, Is.EqualTo(randomBefore), "Rule calculation must not draw from the production RNG.");
                Assert.That(EditorJsonUtility.ToJson(item), Is.EqualTo(beforeAsset), "Runtime rule calculation mutated its SO.");
                return results;

                string Multi(int count)
                {
                    var temps = Enumerable.Range(0, count).Select(i => i < 2 ? pair[i] : 37f).ToArray();
                    var inventories = Enumerable.Range(0, count).Select(i => new InventorySnapshot((byte)i,
                        new[] { new SlotSnapshot(10, remaining == 255, remaining) })).ToArray();
                    var snapshot = new MatchCombatSnapshot(temps, temps, modifiers.Take(count).ToArray(),
                        Enumerable.Repeat(LifeState.Alive, count).ToArray(), inventories, Array.Empty<ScheduledEffectSnapshot>(),
                        new int[count], Enumerable.Repeat(true, count).ToArray(), EnvironmentType.None, default,
                        new[] { ItemEffectRuleSnapshot.From(item, 10) });
                    var intent = new ActionIntent(0, 0, 10, item.GetTargetMode() == TargetMode.Self ? ActionIntent.NoTarget : (byte)1, 1);
                    var resolver = new CombatResolver();
                    return Canonical(item is DefenseItemDataSO
                        ? resolver.ResolveMultiDefenses(snapshot, new[] { intent }.Concat(Enumerable.Repeat(ActionIntent.Empty, count - 1)).ToArray())
                        : resolver.ResolveMultiAction(snapshot, intent));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(userObject); UnityEngine.Object.DestroyImmediate(targetObject); }
        }

        static string Digest(string value)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        // Include nullable structs, readonly fields and ordered arrays, which
        // Unity's JSON serializer would otherwise omit from these result types.
        static string Canonical(object value)
        {
            if (value == null) return "null";
            var type = value.GetType();
            if (type.IsEnum) return type.Name + ":" + value;
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            if (type.IsPrimitive || value is string) return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is IEnumerable items) return "[" + string.Join(",", items.Cast<object>().Select(Canonical)) + "]";
            return "{" + string.Join(",", type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Name + ":" + Canonical(x.GetValue(value)))) + "}";
        }
    }
}
