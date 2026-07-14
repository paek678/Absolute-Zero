using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 아이템 데이터 ScriptableObject 기반 클래스 (SYSTEM_ARCHITECTURE.md Section 4.1).
    /// ※ RULE-001: SO는 런타임 read-only — ExecuteEffect는 SO 필드를 절대 수정하지 않는다.
    /// 효과 실행/검증은 서버 전용 (ItemContext는 서버에서만 생성됨).
    /// </summary>
    public abstract class ItemDataSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string ItemName;            // "부채", "바람막이" 등
        public string Description;         // 한글 설명
        public Sprite Icon;                // 2D 스프라이트

        [Header("Classification")]
        public ItemCategory Category;      // ATK, DEF, REC, BUF, DBF, SAB, SPC
        public ItemSlotType SlotType;      // Main, Sub
        public ItemPersistence Persistence; // Permanent, BasicConsumable, RandomConsumable

        [Header("Usage")]
        public int MaxUses = 1;            // -1 = 무한 (영구), 1 = 1회, 3 = 스마트폰

        [Header("Drop")]
        public float DropWeight;           // 랜덤 풀 가중치 (기본 아이템은 0)

        [Header("Mini-Game")]
        public bool RequiresMiniGame;
        public MiniGameType MiniGameType;
        public float MiniGameTimeLimit;
        public string MiniGameDescription;

        /// <summary>서버 전용: 효과 실행</summary>
        public abstract void ExecuteEffect(ItemContext ctx);

        /// <summary>서버 전용: 사용 가능 여부 검증</summary>
        public virtual bool CanUse(ItemContext ctx)
        {
            if (ctx.UserModifiers.BasicItemsBlocked && Persistence != ItemPersistence.RandomConsumable)
                return false;  // 청테이프: 기본 아이템 봉쇄 중
            return true;
        }
    }
}
