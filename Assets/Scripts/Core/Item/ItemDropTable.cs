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

        public ItemDropTable(ItemDataSO[] allItems)
        {
            int count = 0;
            for (int i = 0; i < allItems.Length; i++)
            {
                if (allItems[i].DropWeight > 0f) count++;
            }

            _entries = new WeightedItem[count];
            _totalWeight = 0f;
            int idx = 0;

            for (int i = 0; i < allItems.Length; i++)
            {
                if (allItems[i].DropWeight > 0f)
                {
                    _entries[idx++] = new WeightedItem { Item = allItems[i], Weight = allItems[i].DropWeight };
                    _totalWeight += allItems[i].DropWeight;
                }
            }
        }

        public bool IsEmpty => _entries.Length == 0;

        public ItemDataSO Roll()
        {
            if (_entries.Length == 0) return null;

            float roll = Random.Range(0f, _totalWeight);
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
