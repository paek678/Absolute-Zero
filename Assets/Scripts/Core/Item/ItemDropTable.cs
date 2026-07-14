namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 가중치 기반 랜덤 아이템 드롭 테이블 (SYSTEM_ARCHITECTURE.md Section 4.5 — 서버 전용).
    /// 가중치 합은 확률 분포가 아님 (기획서 합계 102) — 상대 가중치로만 사용.
    /// </summary>
    public class ItemDropTable
    {
        struct WeightedItem { public ItemDataSO Item; public float Weight; }

        readonly WeightedItem[] _pool;
        readonly float _totalWeight;  // = 102 (기획서 합계)

        public ItemDropTable(ItemDataSO[] randomItems)
        {
            _pool = new WeightedItem[randomItems.Length];
            float total = 0f;
            for (int i = 0; i < randomItems.Length; i++)
            {
                _pool[i] = new WeightedItem { Item = randomItems[i], Weight = randomItems[i].DropWeight };
                total += randomItems[i].DropWeight;
            }
            _totalWeight = total;
        }

        public ItemDataSO Roll()
        {
            float roll = UnityEngine.Random.Range(0f, _totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < _pool.Length; i++)
            {
                cumulative += _pool[i].Weight;
                if (roll <= cumulative) return _pool[i].Item;
            }
            return _pool[^1].Item;  // fallback
        }
    }
}
