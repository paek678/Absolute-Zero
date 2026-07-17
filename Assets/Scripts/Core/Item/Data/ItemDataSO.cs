using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    public abstract class ItemDataSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string ItemName;
        public string Description;
        public Sprite Icon;

        [Header("Classification")]
        public ItemCategory Category;
        public ItemPersistence Persistence;
        public ItemSlotType SlotType;

        public bool IsInstantUse => SlotType == ItemSlotType.Sub;

        [Header("Usage")]
        public int MaxUses = 1;

        [Header("Drop")]
        public float DropWeight;

        [Header("Mini-Game")]
        public bool RequiresMiniGame;
        public MiniGameType MiniGameType;
        public float MiniGameTimeLimit;
        public int MiniGameGoal = 1;   // 게임별 목표치 (연타 횟수 / 게이지 채움 탭 수 / 나사당 회전 수)

        public abstract void ExecuteEffect(ItemContext ctx);

        public virtual bool CanUse(ItemContext ctx)
        {
            if (ctx.UserModifiers.BasicItemsBlocked && Persistence != ItemPersistence.RandomConsumable)
                return false;
            return true;
        }
    }
}
