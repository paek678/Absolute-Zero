using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item.Data;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    [CreateAssetMenu(menuName = "AbsoluteZero/Solo/Stats (inherit only)", fileName = "BotStats")]
    public sealed class BotStatProfileSO : SoloConfigurationSO
    {
        // Schema only: custom reset/merge policies have not been approved.
        // The resolver rejects every enabled override, even a numerically neutral one.
        [Tooltip("Not enabled yet: custom starting stats/loadouts are rejected until reset and grant policies are approved.")]
        [SerializeField] bool overrideStartingTemperature;
        [SerializeField] float startingTemperature = TemperatureSystem.MAX_TEMP;
        [SerializeField] bool overrideFanBaseline;
        [SerializeField] float fanBaseline = TemperatureSystem.DEFAULT_FAN_SPEED;
        [SerializeField] bool overrideInitialItems;
        [Tooltip("Must remain empty for current inherit-only launches. These are item references, not catalog indices.")]
        [SerializeField] ItemDataSO[] initialItems = Array.Empty<ItemDataSO>();
        public bool OverrideStartingTemperature => overrideStartingTemperature;
        public float StartingTemperature => startingTemperature;
        public bool OverrideFanBaseline => overrideFanBaseline;
        public float FanBaseline => fanBaseline;
        public bool OverrideInitialItems => overrideInitialItems;
        public IReadOnlyList<ItemDataSO> InitialItems => Array.AsReadOnly(initialItems ?? Array.Empty<ItemDataSO>());
    }
}
