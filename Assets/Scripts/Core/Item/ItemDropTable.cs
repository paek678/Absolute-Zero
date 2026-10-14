using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item.Data;
using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    public class ItemDropTable
    {
        struct WeightedItem
        {
            public ItemDataSO Item;
            public float Weight;
        }

        WeightedItem[] _entries;
        float _totalWeight;

        public ItemDropTable(ItemDataSO[] allItems) : this(allItems, null) { }

        public ItemDropTable(ItemDataSO[] allItems, Predicate<ItemDataSO> filter)
        {
            if (allItems == null || allItems.Length == 0)
            {
                _entries = Array.Empty<WeightedItem>();
                _totalWeight = 0f;
                return;
            }

            var list = new List<WeightedItem>(allItems.Length);
            _totalWeight = 0f;

            for (int i = 0; i < allItems.Length; i++)
            {
                var item = allItems[i];
                if (ItemAvailability.IsEnabled(item) && item.DropWeight > 0f
                    && (filter == null || filter(item)))
                {
                    list.Add(new WeightedItem { Item = item, Weight = item.DropWeight });
                    _totalWeight += item.DropWeight;
                }
            }

            _entries = list.Count > 0 ? list.ToArray() : Array.Empty<WeightedItem>();
        }

        public bool IsEmpty => _entries.Length == 0;

        public ItemDataSO Roll(Predicate<ItemDataSO> eligibility, string context = "unspecified", int seat = -1)
        {
            if (_entries.Length == 0 || eligibility == null) return null;

            float eligibleWeight = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var recorder = Solo.PlaytestTelemetry.Current;
            var candidates = recorder != null ? new List<Solo.BalanceDropCandidate>(_entries.Length) : null;
#endif
            for (int i = 0; i < _entries.Length; i++)
            {
                bool allowed = eligibility(_entries[i].Item);
                if (allowed)
                    eligibleWeight += _entries[i].Weight;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                candidates?.Add(new Solo.BalanceDropCandidate { item = _entries[i].Item.ItemName,
                    weight = _entries[i].Weight, eligible = allowed });
#endif
            }

            ItemDataSO selected = null;
            if (eligibleWeight > 0f)
            {
                float roll = Common.GameplayRandom.Range("item-drop", 0f, eligibleWeight);
                selected = SelectEligible(eligibility, roll);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            recorder?.Balance("drop_draw", new Solo.BalanceObservation {
                action = new Solo.BalanceActionIdentity { source = seat, item = -1 },
                reason = context, dropCandidates = candidates?.ToArray(),
                selectedDrop = selected != null ? selected.ItemName : null, eligibleWeight = eligibleWeight });
#endif
            return selected;
        }

        // Draws remain in Roll. Explicit values make selection independently
        // characterizable without changing the global RNG stream or predicate order.
        internal ItemDataSO SelectEligible(Predicate<ItemDataSO> eligibility, float roll)
        {
            ItemDataSO lastEligible = null;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (!eligibility(_entries[i].Item)) continue;
                lastEligible = _entries[i].Item;
                roll -= _entries[i].Weight;
                if (roll <= 0f) return lastEligible;
            }

            return lastEligible;
        }

        public ItemDataSO Roll()
        {
            if (_entries.Length == 0) return null;

            float roll = Common.GameplayRandom.Range("item-drop", 0f, _totalWeight);
            return Select(roll);
        }

        internal ItemDataSO Select(float roll)
        {
            if (_entries.Length == 0) return null;
            float cumulative = 0f;

            for (int i = 0; i < _entries.Length; i++)
            {
                cumulative += _entries[i].Weight;
                if (roll <= cumulative) return _entries[i].Item;
            }

            return _entries[_entries.Length - 1].Item;
        }
    }
}
