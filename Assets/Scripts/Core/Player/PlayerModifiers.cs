namespace AbsoluteZero.Core.Player
{
    // SYSTEM_ARCHITECTURE.md Section 2.3 — 서버 전용, 턴마다 리셋되는 수정자.
    // ※ 공유 계약 타입: 코어 시스템(동업자) 병합 시 이 파일이 기준 정의.

    /// <summary>턴별 플레이어 수정자. TurnManager가 PlayerModifiers[2] 배열로 보유 ([0]=P1, [1]=P2)</summary>
    public struct PlayerModifiers
    {
        public bool BasicItemsBlocked;      // 청테이프 효과: 기본 아이템 사용 불가
        public bool ActionNeutralized;      // 레드카드 효과: Main 행동 무효화
        public DefenseInfo? ActiveDefense;  // 바람막이/마스크 방어 활성
        public bool HasExtraAction;         // 타로카드 효과: 추가 행동 가능
        public bool OpponentRevealed;       // 타로카드 효과: 상대 선택 보임

        public void Reset()
        {
            BasicItemsBlocked = false;
            ActionNeutralized = false;
            ActiveDefense = null;
            HasExtraAction = false;
            OpponentRevealed = false;
        }
    }

    /// <summary>활성 방어 정보 (바람막이 = {Temperature, 4}, 마스크 = {Food, float.MaxValue})</summary>
    public struct DefenseInfo
    {
        public DamageFilter Filter;    // Temperature, Food
        public float BlockAmount;      // 4 (Windbreaker), float.MaxValue (Mask)
    }

    /// <summary>공격/방어 매칭 필터</summary>
    public enum DamageFilter : byte { Temperature, Food, All }
}
