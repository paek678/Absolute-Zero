using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using UnityEditor;

namespace AbsoluteZero.Tests
{
    // PLAN046: the approved solo bot score sheet, reproduced with the real item assets.
    public sealed class Plan046BotJudgementTests
    {
        static readonly (string Name, string Path, float Value, BotMiniGameTier Tier)[] Sheet =
        {
            ("Fan", "Assets/Data/Items/Basic/Fan.asset", 3, BotMiniGameTier.None),
            ("Windbreaker", "Assets/Data/Items/Basic/Windbreaker.asset", 3, BotMiniGameTier.None),
            ("WarmTea", "Assets/Data/Items/Basic/WarmTea.asset", 7, BotMiniGameTier.None),
            ("Cat", "Assets/Data/Items/Basic/Cat.asset", 4, BotMiniGameTier.None),
            ("Screwdriver", "Assets/Data/Items/Random/Special/Screwdriver.asset", 6, BotMiniGameTier.Hard),
            ("Samgyetang", "Assets/Data/Items/Random/Debuff/Samgyetang.asset", 4, BotMiniGameTier.None),
            ("ClawMachine", "Assets/Data/Items/Random/Sabotage/ClawMachine.asset", 6, BotMiniGameTier.Hard),
            ("IceCream", "Assets/Data/Items/Random/Attack/IceCream.asset", 5, BotMiniGameTier.None),
            ("WaterGun", "Assets/Data/Items/Random/Attack/WaterGun.asset", 8, BotMiniGameTier.Hard),
            ("HotPack", "Assets/Data/Items/Random/Recovery/HotPack.asset", 12, BotMiniGameTier.Easy),
            ("BlueTape", "Assets/Data/Items/Random/Sabotage/BlueTape.asset", 3, BotMiniGameTier.Normal),
            ("Mask", "Assets/Data/Items/Random/Defense/Mask.asset", 4, BotMiniGameTier.None),
            ("IcedAmericano", "Assets/Data/Items/Random/Attack/IcedAmericano.asset", 5, BotMiniGameTier.None),
            ("HotAmericano", "Assets/Data/Items/Random/Recovery/HotAmericano.asset", 5, BotMiniGameTier.None),
            ("HandFan", "Assets/Data/Items/Random/Attack/HandFan.asset", 4, BotMiniGameTier.None),
            ("Smartphone", "Assets/Data/Items/Random/Recovery/Smartphone.asset", 6, BotMiniGameTier.Normal),
            ("BuldakNoodles", "Assets/Data/Items/Random/Buff/BuldakNoodles.asset", 16, BotMiniGameTier.Easy),
            ("Soda", "Assets/Data/Items/Random/Buff/Soda.asset", 10, BotMiniGameTier.None),
            ("RedCard", "Assets/Data/Items/Random/Sabotage/RedCard.asset", 6, BotMiniGameTier.Normal),
            ("HugTshirt", "Assets/Data/Items/Random/Attack/HugTshirt.asset", 8, BotMiniGameTier.Hard),
        };
        static readonly PrepInputKey Key = new(1, 1, 3, 1);
        ItemDataSO[] _items;
        BotItemProfile[] _advanced, _beginner;

        [OneTimeSetUp]
        public void LoadSheet()
        {
            _items = Sheet.Select(row => AssetDatabase.LoadAssetAtPath<ItemDataSO>(row.Path)).ToArray();
            Assert.That(_items.All(item => item != null), Is.True);
            _advanced = Profiles(BotJudgementSettings.Advanced);
            _beginner = Profiles(BotJudgementSettings.Beginner);
        }

        BotItemProfile[] Profiles(BotJudgementSettings judgement)
            => Enumerable.Range(0, Sheet.Length).Select(i => BotDecisionModel.Profile(_items[i], (short)i,
                new BotItemKnowledgeEntry { Item = _items[i], OpponentValue = Sheet[i].Value, MiniGameTier = Sheet[i].Tier }, i, judgement)).ToArray();

        static short Id(string name) => (short)Array.FindIndex(Sheet, row => row.Name == name);

        // Spec delays: advanced 2 s instant / 4 s mini-game; beginner adds 2 s.
        BotCandidate Hold(string name, bool beginner = false, float heal = 0)
        {
            short id = Id(name);
            double delay = (_items[id].RequiresMiniGame ? 4 : 2) + (beginner ? 2 : 0);
            return new BotCandidate((uint)(id + 1), 0, id, delay, heal);
        }

        // 예시 상황: own 20, opponent 5, opponent may Windbreaker, no Mask, tool max 8, food max 5,
        // highest opponent value 8, random value sum 20, opponent loses 3 per Prep.
        static readonly string[] OpponentHand = { "Fan", "Windbreaker", "WarmTea", "Cat", "WaterGun", "IceCream", "HandFan", "BlueTape" };
        static BotObservation Example(IEnumerable<BotCandidate> held, float own = 20, float opponent = 5,
            IEnumerable<string> opponentHand = null, int ownSlots = 8, float opponentFan = 1, bool opponentReady = false)
            => new(Key, 1, 0, own, opponent, 1, opponentFan, opponentReady, 20, 3, ownSlots, PlayerInventory.SlotCapacity,
                (opponentHand ?? OpponentHand).Select(name => new BotOpponentItem(Id(name), true)), held);
        BotCandidate[] ExampleHand(bool beginner = false) => new[]
        {
            Hold("Fan", beginner), Hold("Windbreaker", beginner), Hold("WarmTea", beginner, 7), Hold("Cat", beginner),
            Hold("ClawMachine", beginner), Hold("IceCream", beginner), Hold("WaterGun", beginner), Hold("HotPack", beginner, 12)
        };
        BotScoreSheet Advanced(BotObservation o) => BotDecisionModel.Score(o, _advanced, BotJudgementSettings.Advanced, BotScoringCoefficients.Approved, .25);
        BotScoreSheet Beginner(BotObservation o) => BotDecisionModel.Score(o, _beginner, BotJudgementSettings.Beginner, BotScoringCoefficients.Approved, .25);
        static float Final(BotScoreSheet sheet, string name) => sheet.Ranked.Single(x => x.Candidate.Item == Id(name)).Final;
        static float Base(BotScoreSheet sheet, string name) => sheet.Ranked.Single(x => x.Candidate.Item == Id(name)).Score;

        [Test]
        public void ProfilesFollowItemDataAndSheetTiers()
        {
            var p = _advanced;
            Assert.That((p[Id("WaterGun")].Formula, p[Id("WaterGun")].Kind, p[Id("WaterGun")].Amount), Is.EqualTo((BotItemFormula.DirectAttack, BotAttackKind.Tool, 8f)));
            Assert.That((p[Id("IceCream")].Kind, p[Id("IcedAmericano")].Kind), Is.EqualTo((BotAttackKind.Food, BotAttackKind.Food)));
            Assert.That((p[Id("Samgyetang")].Formula, p[Id("Samgyetang")].Kind, p[Id("Samgyetang")].Amount, p[Id("Samgyetang")].Offset),
                Is.EqualTo((BotItemFormula.DelayedDamage, BotAttackKind.Food, 7f, 3f)));
            Assert.That((p[Id("Soda")].Formula, p[Id("Soda")].Amount, p[Id("Soda")].Offset), Is.EqualTo((BotItemFormula.DelayedHeal, 15f, 5f)));
            Assert.That((p[Id("BuldakNoodles")].Amount, p[Id("BuldakNoodles")].Offset), Is.EqualTo((20f, 0f)));
            Assert.That((p[Id("Windbreaker")].Formula, p[Id("Windbreaker")].Kind), Is.EqualTo((BotItemFormula.Guard, BotAttackKind.Tool)));
            Assert.That((p[Id("Mask")].Formula, p[Id("Mask")].Kind), Is.EqualTo((BotItemFormula.Guard, BotAttackKind.Food)));
            Assert.That(new[] { "Cat", "ClawMachine", "BlueTape", "RedCard", "Screwdriver", "HugTshirt" }.Select(n => p[Id(n)].Formula),
                Is.EqualTo(new[] { BotItemFormula.Reroll, BotItemFormula.Steal, BotItemFormula.BlockBasics, BotItemFormula.Neutralize,
                    BotItemFormula.FanBoost, BotItemFormula.Equalize }));
            Assert.That(new[] { "Fan", "IceCream", "WaterGun", "HotPack", "Smartphone", "Screwdriver" }.Select(n => p[Id(n)].Success),
                Is.EqualTo(new[] { 1f, 1f, .7f, .9f, .8f, .7f }));
            Assert.That(new[] { "WaterGun", "HotPack", "BlueTape" }.Select(n => _beginner[Id(n)].Success), Is.EqualTo(new[] { .45f, .75f, .6f }));
            Assert.That((p[Id("Fan")].Finite, p[Id("WarmTea")].Finite, p[Id("WaterGun")].IsRandomItem, p[Id("Cat")].IsRandomItem),
                Is.EqualTo((false, true, true, false)));
        }

        [Test]
        public void WorkedExampleMatchesTheSpecSheetAndTakesTheLethalIceCream()
        {
            var sheet = Advanced(Example(ExampleHand()));
            Assert.That(sheet.Crisis, Is.False);
            Assert.That(sheet.Care, Is.EqualTo(.5f));
            Assert.That(sheet.OwnLoss, Is.EqualTo(2f));
            Assert.That(sheet.Threat, Is.EqualTo(8f));
            var expected = new (string Name, float Final)[]
            {
                ("IceCream", 105f), ("ClawMachine", 8.96f), ("Cat", 6.4f), ("HotPack", 5.4f),
                ("WaterGun", 3.92f), ("WarmTea", 3.5f), ("Windbreaker", 2.8f), ("Fan", 2.1f)
            };
            Assert.That(sheet.Ranked.Select(x => Sheet[x.Candidate.Item].Name), Is.EqualTo(expected.Select(e => e.Name)));
            foreach (var row in expected) Assert.That(Final(sheet, row.Name), Is.EqualTo(row.Final).Within(.01f), row.Name);
            Assert.That(sheet.Ranked.Where(x => x.Lethal).Select(x => Sheet[x.Candidate.Item].Name), Is.EqualTo(new[] { "IceCream" }));
            Assert.That(BotDecisionModel.TryPickLethal(sheet, BotJudgementSettings.Advanced, out var chosen), Is.True);
            Assert.That(chosen.Item, Is.EqualTo(Id("IceCream")));
        }

        [Test]
        public void EverySheetFormulaMatchesTheExampleScores()
        {
            var hand = Sheet.Select(row => Hold(row.Name, heal: row.Name == "WarmTea" ? 7 : row.Name == "HotPack" ? 12
                : row.Name == "HotAmericano" ? 5 : row.Name == "Smartphone" ? 4 : 0));
            var sheet = Advanced(Example(hand));
            var expected = new Dictionary<string, float>
            {
                ["Fan"] = 2.1f, ["Windbreaker"] = 2.8f, ["WarmTea"] = 3.5f, ["Cat"] = 6.4f, ["Screwdriver"] = 3.36f,
                ["Samgyetang"] = 2.6f, ["ClawMachine"] = 8.96f, ["IceCream"] = 5f, ["WaterGun"] = 3.92f, ["HotPack"] = 5.4f,
                ["BlueTape"] = 1.92f, ["Mask"] = 1.75f, ["IcedAmericano"] = 5f, ["HotAmericano"] = 2.5f, ["Smartphone"] = 1.6f,
                ["Soda"] = 3.5f, ["RedCard"] = 4.48f,
                // Item data: HandFan deals 2 (the sheet text assumed 4 -> 2.8).
                ["HandFan"] = 1.4f,
                // Heal above 37 is lost: 17 of +20 counts at 20 degrees (the sheet shows 7.2 without the cap).
                ["BuldakNoodles"] = 6.12f,
            };
            foreach (var row in expected) Assert.That(Base(sheet, row.Key), Is.EqualTo(row.Value).Within(.01f), row.Key);
            Assert.That(sheet.Ranked.Any(x => x.Candidate.Item == Id("HugTshirt")), Is.False, "Hug T-shirt at or below zero is excluded.");
        }

        [Test]
        public void BeginnerIgnoresOpponentDefenseAndOnlyAddsTheLethalBonus()
        {
            var sheet = Beginner(Example(ExampleHand(beginner: true)));
            Assert.That(Base(sheet, "Fan"), Is.EqualTo(3f).Within(.001f));
            Assert.That(Final(sheet, "IceCream"), Is.EqualTo(105f).Within(.001f));
            Assert.That(BotDecisionModel.MustTakeLethal(sheet, BotJudgementSettings.Beginner), Is.False);
            Assert.That(BotDecisionModel.TryPickLethal(sheet, BotJudgementSettings.Beginner, out _), Is.False);
        }

        [Test]
        public void PickOddsFollowTheDifficultyTable()
        {
            var sheet = Beginner(Example(ExampleHand(beginner: true)));
            int n = sheet.Ranked.Count, rolls = 20000, best = 0, second = 0;
            var random = new System.Random(4601);
            for (int i = 0; i < rolls; i++)
            {
                BotDecisionModel.TryPickWeighted(sheet, BotJudgementSettings.Beginner, random, out var pick);
                if (pick.Item == sheet.Ranked[0].Candidate.Item) best++;
                else if (pick.Item == sheet.Ranked[1].Candidate.Item) second++;
            }
            Assert.That(best / (double)rolls, Is.EqualTo(.6 + .15 / n).Within(.015));
            Assert.That(second / (double)rolls, Is.EqualTo(.25 + .15 / n).Within(.015));
        }

        [Test]
        public void MortalDangerTakesTheBestRecoveryOrDefense()
        {
            var sheet = Advanced(Example(new[] { Hold("Fan"), Hold("Windbreaker"), Hold("WarmTea", heal: 7), Hold("HotPack", heal: 12) }, own: 6, opponent: 30));
            Assert.That(sheet.Crisis, Is.True);
            Assert.That(BotDecisionModel.TryPickCrisis(sheet, BotJudgementSettings.Advanced, new System.Random(1), out var chosen), Is.True);
            Assert.That(chosen.Item, Is.EqualTo(Id("HotPack")));
            var noCare = Advanced(Example(new[] { Hold("Fan"), Hold("IceCream") }, own: 6, opponent: 30));
            Assert.That(noCare.Crisis, Is.True);
            Assert.That(BotDecisionModel.TryPickCrisis(noCare, BotJudgementSettings.Advanced, new System.Random(1), out _), Is.False);
        }

        [Test]
        public void FullTemperatureNeverDrinksWarmTeaAndTiesFollowTheSheetOrder()
        {
            var basics = new[] { "Fan", "Windbreaker", "WarmTea", "Cat" };
            var sheet = Advanced(Example(new[] { Hold("Fan"), Hold("Windbreaker"), Hold("WarmTea", heal: 7), Hold("Cat") },
                own: 37, opponent: 37, opponentHand: basics));
            Assert.That(sheet.Ranked.Select(x => Sheet[x.Candidate.Item].Name), Is.EqualTo(new[] { "Fan", "Windbreaker" }));
            Assert.That(Final(sheet, "Fan"), Is.EqualTo(Final(sheet, "Windbreaker")).Within(1e-4f));
        }

        [Test]
        public void SodaIsSkippedWhenItsSelfDamageCreatesDanger()
        {
            var basics = new[] { "Fan", "Windbreaker", "WarmTea", "Cat" };
            var safe = Advanced(Example(new[] { Hold("Soda"), Hold("Fan") }, own: 20, opponent: 25, opponentHand: basics));
            Assert.That(safe.Ranked.Any(x => x.Candidate.Item == Id("Soda")), Is.True);
            var risky = Advanced(Example(new[] { Hold("Soda"), Hold("Fan") }, own: 10, opponent: 25, opponentHand: basics));
            Assert.That(risky.Ranked.Any(x => x.Candidate.Item == Id("Soda")), Is.False);
        }

        [Test]
        public void ClawNeedsAFreeSlotAndScrewdriverNeedsAnUnboostedFan()
        {
            var hand = new[] { Hold("ClawMachine"), Hold("Screwdriver"), Hold("Fan") };
            var full = Advanced(Example(hand, ownSlots: PlayerInventory.SlotCapacity));
            Assert.That(full.Ranked.Any(x => x.Candidate.Item == Id("ClawMachine")), Is.False);
            var boosted = Advanced(Example(hand, opponentFan: 2));
            Assert.That(boosted.Ranked.Any(x => x.Candidate.Item == Id("Screwdriver")), Is.False);
            var nothingToSteal = Advanced(Example(hand, opponentHand: new[] { "Fan", "Windbreaker" }));
            Assert.That(nothingToSteal.Ranked.Any(x => x.Candidate.Item == Id("ClawMachine")), Is.False);
        }

        [Test]
        public void HugScoresTheTemperatureGapOnlyWhenColder()
        {
            var sheet = Advanced(Example(new[] { Hold("HugTshirt"), Hold("Fan") }, own: 10, opponent: 25));
            Assert.That(Base(sheet, "HugTshirt"), Is.EqualTo(15 * .7f).Within(.01f));
        }

        [Test]
        public void MaskMakesFoodAttacksBlockableAndRemovesTheirLethalFlag()
        {
            var hand = new[] { Hold("IceCream"), Hold("Fan") };
            var masked = Advanced(Example(hand, opponentHand: OpponentHand.Append("Mask")));
            Assert.That(Base(masked, "IceCream"), Is.EqualTo(3.5f).Within(.01f));
            Assert.That(masked.HasLethal, Is.False);
        }

        [Test]
        public void OpponentReadyRemovesOwnCoolingFromTheDangerCheck()
        {
            var hand = new[] { Hold("Fan"), Hold("WarmTea", heal: 7) };
            var waiting = Advanced(Example(hand, own: 10, opponent: 30));
            var ready = Advanced(Example(hand, own: 10, opponent: 30, opponentReady: true));
            Assert.That(waiting.Crisis, Is.True);
            Assert.That(ready.Crisis, Is.False);
        }
    }
}
