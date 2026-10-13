using System;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    public abstract class SoloConfigurationSO : ScriptableObject
    {
        [Tooltip("Stable lowercase ID (1..64). Use a new ID when copying a profile; never use a translated display name.")]
        [SerializeField] string configId;
        [Tooltip("Increase when changing approved data. The launch snapshot records this version.")]
        [SerializeField, Min(1)] int version = 1;
        public string ConfigId => configId;
        public int Version => version;
    }

    public enum SoloConfigurationReadiness { Draft, ValidationFixture, Ready }
    public enum BotReadyPolicy { AfterSelection, NearDeadline }

    // Mini-game difficulty group of an item whose human path plays a mini-game.
    public enum BotMiniGameTier : byte { None, Easy, Normal, Hard }

    // One difficulty column of the approved bot settings sheet (PLAN046).
    // Chances are 0..1; temperature values are degrees.
    [Serializable]
    public struct BotJudgementSettings
    {
        [Tooltip("Success chance for tapping-type mini-games (Hot Pack, Buldak).")]
        public float EasyMiniGameSuccess;
        [Tooltip("Success chance for timing/pattern mini-games (Blue Tape, Smartphone, Red Card).")]
        public float NormalMiniGameSuccess;
        [Tooltip("Success chance for aiming mini-games (Water Gun, Screwdriver, Claw, Hug T-shirt).")]
        public float HardMiniGameSuccess;
        [Tooltip("Chance to pick the highest-scoring item.")]
        public float BestPickChance;
        [Tooltip("Chance to pick the second-highest item. The remainder picks any positive-score item uniformly.")]
        public float SecondPickChance;
        [Tooltip("Multiplier for effects that land on a later turn.")]
        public float LookaheadFactor;
        [Tooltip("Chance that an attack is blocked when the opponent can use the matching Windbreaker/Mask. Zero ignores opponent defense.")]
        public float OpponentBlockChance;
        [Tooltip("Chance that the opponent actually attacks; scales Windbreaker/Mask scores.")]
        public float OpponentAttackChance;
        [Tooltip("Added to a lethal attack's score, multiplied by its success chance.")]
        public float KillBonus;
        [Tooltip("Recovery/defense multiplier while the bot could die this turn.")]
        public float CrisisCareMultiplier;
        [Tooltip("Recovery/defense multiplier while the bot leads by at least the comfort margin.")]
        public float ComfortCareMultiplier;
        [Tooltip("Own temperature minus opponent temperature that counts as comfortable.")]
        public float ComfortMargin;
        [Tooltip("When on, a lethal attack only receives its bonus and the normal pick odds apply. When off, the lethal attack is always taken.")]
        public bool MayMissLethal;
        [Tooltip("When on, crisis recovery/defense also uses the pick odds. When off, the best one is always taken.")]
        public bool MayMissCrisis;

        // Spec column 상급. Assets created before PLAN046 deserialize with these values.
        public static BotJudgementSettings Advanced => new()
        {
            EasyMiniGameSuccess = .9f, NormalMiniGameSuccess = .8f, HardMiniGameSuccess = .7f,
            BestPickChance = .9f, SecondPickChance = .08f, LookaheadFactor = .8f,
            OpponentBlockChance = .3f, OpponentAttackChance = .7f, KillBonus = 100,
            CrisisCareMultiplier = 2, ComfortCareMultiplier = .5f, ComfortMargin = 10,
            MayMissLethal = false, MayMissCrisis = false
        };

        // Spec column 초급.
        public static BotJudgementSettings Beginner => new()
        {
            EasyMiniGameSuccess = .75f, NormalMiniGameSuccess = .6f, HardMiniGameSuccess = .45f,
            BestPickChance = .6f, SecondPickChance = .25f, LookaheadFactor = .5f,
            OpponentBlockChance = 0, OpponentAttackChance = .7f, KillBonus = 100,
            CrisisCareMultiplier = 2, ComfortCareMultiplier = .5f, ComfortMargin = 10,
            MayMissLethal = true, MayMissCrisis = false
        };

        public float SuccessFor(BotMiniGameTier tier) => tier switch
        {
            BotMiniGameTier.Easy => EasyMiniGameSuccess,
            BotMiniGameTier.Normal => NormalMiniGameSuccess,
            BotMiniGameTier.Hard => HardMiniGameSuccess,
            _ => 1
        };
    }

    [Serializable]
    public struct BotCosmeticIds
    {
        public string Head;
        public string Top;
        public string Back;
        public string Bottom;
        public string Tail;
    }
}
