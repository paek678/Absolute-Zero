using UnityEngine;
using AbsoluteZero.Core.Item;

namespace AbsoluteZero.UI.TestUI
{
    /// <summary>
    /// 테스트용 아이템 레지스트리 팩토리 (테스트 전용).
    /// 서버/클라이언트가 각자 호출해도 "같은 순서"의 레지스트리가 나와야
    /// ItemSlotNetData.ItemId(레지스트리 인덱스)가 양쪽에서 동일하게 해석된다 — 순서 변경 금지.
    /// 랜덤 아이템은 런타임 인스턴스(에셋 아님, RULE-001 무관). 수치는 GAME_DESIGN 표 기준.
    /// </summary>
    public static class ItemTestRegistry
    {
        public const int BasicCount = 4;

        /// <summary>기본 4종 + 테스트 랜덤 4종으로 전체 레지스트리 구성 (+드롭 테이블)</summary>
        public static ItemDataSO[] Build(ItemDataSO fan, ItemDataSO windbreaker, ItemDataSO warmTea, ItemDataSO cat,
                                         out ItemDropTable dropTable)
        {
            var randoms = CreateTestRandomItems();
            dropTable = new ItemDropTable(randoms);

            var registry = new ItemDataSO[BasicCount + randoms.Length];
            registry[0] = fan;
            registry[1] = windbreaker;
            registry[2] = warmTea;
            registry[3] = cat;
            randoms.CopyTo(registry, BasicCount);
            return registry;
        }

        /// <summary>Sub/Main 분류 미확정 랜덤 아이템은 전부 Main 처리 (기획 확정 시 수정)</summary>
        static ItemDataSO[] CreateTestRandomItems()
        {
            var handFan = ScriptableObject.CreateInstance<AttackItemDataSO>();
            SetupBase(handFan, "손풍기", "상대 온도 -4°", ItemCategory.Attack, 12f);
            handFan.Damage = 4f;

            var iceCream = ScriptableObject.CreateInstance<AttackItemDataSO>();
            SetupBase(iceCream, "아이스크림", "상대 온도 -5°", ItemCategory.Attack, 10f);
            iceCream.Damage = 5f;

            var hotAmericano = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            SetupBase(hotAmericano, "뜨.아", "자신 온도 +5°", ItemCategory.Recovery, 10f);
            hotAmericano.HealPerUse = new[] { 5f };

            var hotPack = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            SetupBase(hotPack, "핫팩", "자신 온도 +10°", ItemCategory.Recovery, 4f);
            hotPack.HealPerUse = new[] { 10f };

            return new ItemDataSO[] { handFan, iceCream, hotAmericano, hotPack };
        }

        static void SetupBase(ItemDataSO item, string name, string desc, ItemCategory cat, float weight)
        {
            item.ItemName = name;
            item.Description = desc;
            item.Category = cat;
            item.SlotType = ItemSlotType.Main;
            item.Persistence = ItemPersistence.RandomConsumable;
            item.MaxUses = 1;
            item.DropWeight = weight;
        }
    }
}
