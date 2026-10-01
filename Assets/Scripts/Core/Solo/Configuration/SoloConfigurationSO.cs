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

    [Serializable]
    public struct BotTacticalWeights
    {
        public float Survival;
        public float Finish;
        public float Counter;
        public float Resource;
        public float General;
        public static BotTacticalWeights Neutral => new()
        { Survival = 1, Finish = 1, Counter = 1, Resource = 1, General = 1 };
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
