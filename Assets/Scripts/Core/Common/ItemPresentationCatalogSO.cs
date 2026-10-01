using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item.Data;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // Presentation routing only. Damage, targeting and timing remain on ItemDataSO.
    public enum ItemChoreography { Ordinary, Feed, Hug, Cat, Buldak, Screwdriver, RedCard }

    [CreateAssetMenu(menuName = "Absolute Zero/Item Presentation Catalog")]
    public sealed class ItemPresentationCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] ItemDataSO item;
            [SerializeField] Sprite sprite;
            [SerializeField] AudioClip audioOverride;
            [SerializeField] ItemChoreography choreography;

            public ItemDataSO Item => item;
            public Sprite Sprite => sprite;
            public AudioClip AudioOverride => audioOverride;
            public ItemChoreography Choreography => choreography;
        }

        [Serializable]
        sealed class TriggerAudio
        {
            [SerializeField] string trigger;
            [SerializeField] AudioClip clip;
            public string Trigger => trigger;
            public AudioClip Clip => clip;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        [SerializeField] TriggerAudio[] triggerAudio = Array.Empty<TriggerAudio>();

        public int Count => entries?.Length ?? 0;

        public bool TryGet(ItemDataSO item, out Entry entry)
        {
            if (item != null && entries != null)
                foreach (var candidate in entries)
                    if (candidate != null && candidate.Item == item)
                    {
                        entry = candidate;
                        return true;
                    }
            entry = null;
            return false;
        }

        // True with a null clip is an intentional silent trigger, not a failed lookup.
        public bool TryResolveAudio(ItemDataSO item, string trigger, out AudioClip clip)
        {
            clip = null;
            if (!TryGet(item, out var entry)) return false;
            // Unity may deserialize an empty Object field as a managed wrapper
            // that compares equal to null. Return actual null for silent audio.
            clip = entry.AudioOverride != null ? entry.AudioOverride : null;
            if (clip != null || string.IsNullOrEmpty(trigger)) return true;
            if (triggerAudio != null)
                foreach (var sound in triggerAudio)
                    if (sound != null && sound.Trigger == trigger)
                    {
                        clip = sound.Clip;
                        break;
                    }
            return true;
        }

        public bool Validate(IReadOnlyList<ItemDataSO> registry, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            int start = errors.Count;
            var identities = new HashSet<ItemDataSO>();
            if (entries == null || entries.Length == 0) errors.Add("Presentation entries are empty.");
            else foreach (var entry in entries)
            {
                if (entry?.Item == null) { errors.Add("Presentation entry has no item identity."); continue; }
                if (!identities.Add(entry.Item)) errors.Add($"Duplicate presentation identity: {entry.Item.name}");
                if (entry.Sprite == null) errors.Add($"Missing item sprite: {entry.Item.name}");
                if (!Enum.IsDefined(typeof(ItemChoreography), entry.Choreography))
                    errors.Add($"Unknown choreography: {entry.Item.name}");
            }
            if (registry == null) errors.Add("Item registry is missing.");
            else foreach (var item in registry)
                if (item == null || !identities.Contains(item))
                    errors.Add($"Unmapped registry identity: {(item != null ? item.name : "null")}");

            var triggers = new HashSet<string>(StringComparer.Ordinal);
            if (triggerAudio != null) foreach (var sound in triggerAudio)
            {
                if (sound == null || string.IsNullOrWhiteSpace(sound.Trigger))
                    errors.Add("Audio trigger is empty.");
                else if (!triggers.Add(sound.Trigger)) errors.Add($"Duplicate audio trigger: {sound.Trigger}");
                if (sound?.Clip == null) errors.Add("Audio trigger has no clip; omit silent triggers.");
            }
            return errors.Count == start;
        }
    }
}
