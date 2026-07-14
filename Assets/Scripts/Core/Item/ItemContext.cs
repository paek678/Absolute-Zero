using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 아이템 효과 실행 시 전달되는 컨텍스트 (SYSTEM_ARCHITECTURE.md Section 4.2 — 서버 전용).
    /// FIX-01: struct에 ref 필드 사용 불가 → class + 배열 인덱스 패턴.
    /// PlayerModifiers는 TurnManager가 보유한 배열([0]=P1, [1]=P2)을 통해 접근.
    /// </summary>
    public class ItemContext
    {
        // 플레이어 참조
        public PlayerState User;
        public PlayerState Target;
        public PlayerInventory UserInventory;
        public PlayerInventory TargetInventory;

        // Modifiers 배열 접근용 인덱스
        public int UserIndex;
        public int TargetIndex;

        // 슬롯 정보
        public ItemSlotNetData UserSlot;
        public byte SlotIndex;

        // 시스템 참조
        public TemperatureSystem TempSystem;
        public BuffDebuffSystem BuffSystem;
        public ItemDropTable DropTable;
        public EnvironmentType ActiveEnvironment;
        public PlayerModifiers[] AllModifiers;  // [0]=P1, [1]=P2

        // 편의 접근자 (ref 반환 → 배열 원소 직접 수정 가능)
        public ref PlayerModifiers UserModifiers => ref AllModifiers[UserIndex];
        public ref PlayerModifiers TargetModifiers => ref AllModifiers[TargetIndex];
    }
}
