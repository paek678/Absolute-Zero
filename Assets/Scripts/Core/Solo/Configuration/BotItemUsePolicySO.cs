using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item.Data;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    [Serializable]
    public struct BotItemDelayOverride
    {
        public ItemDataSO Item;
        public float DelaySeconds;
    }

    [CreateAssetMenu(menuName = "AbsoluteZero/Solo/Item use", fileName = "BotItemUse")]
    public sealed class BotItemUsePolicySO : SoloConfigurationSO
    {
        [SerializeField] bool provisionalTuning = true;
        [Tooltip("Must be explicitly set and positive. Zero-delay policy is not approved.")]
        [SerializeField] float defaultDelaySeconds = -1;
        [SerializeField] BotItemDelayOverride[] overrides = Array.Empty<BotItemDelayOverride>();
        public bool ProvisionalTuning => provisionalTuning;
        public float DefaultDelaySeconds => defaultDelaySeconds;
        public IReadOnlyList<BotItemDelayOverride> Overrides => Array.AsReadOnly(overrides ?? Array.Empty<BotItemDelayOverride>());
    }
}
