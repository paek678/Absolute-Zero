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

        [Header("Special Behavior")]
        public bool IsFreeAction;

        [Header("Mini-Game")]
        public bool RequiresMiniGame;
        public MiniGameType MiniGameType;
        public float MiniGameTimeLimit;
        public int MiniGameGoal = 1;

        public abstract void ExecuteEffect(ItemContext ctx);

        public virtual bool CanUse(ItemContext ctx)
        {
            if (ctx.User.IsBasicBlocked.Value && Persistence != ItemPersistence.RandomConsumable)
                return false;
            return true;
        }
    }
}
