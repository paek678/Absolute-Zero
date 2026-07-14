namespace AbsoluteZero.Core.Item
{
    // SYSTEM_ARCHITECTURE.md Section 4.6 — 아이템 시스템 공용 열거형

    /// <summary>아이템 카테고리 (ATK/DEF/REC/BUF/DBF/SAB/SPC)</summary>
    public enum ItemCategory : byte { Attack, Defense, Recovery, Buff, Debuff, Sabotage, Special }

    /// <summary>Main = 선택 즉시 턴 종료(자동 Ready), Sub = 즉시 실행 후 턴 유지</summary>
    public enum ItemSlotType : byte { Main, Sub }

    /// <summary>Permanent = 무한, BasicConsumable = 라운드마다 리필, RandomConsumable = 1회성</summary>
    public enum ItemPersistence : byte { Permanent, BasicConsumable, RandomConsumable }

    /// <summary>사보타주 효과 종류 (고양이=Reroll, 집게손=Steal, 청테이프=BlockBasic, 레드카드=Neutralize)</summary>
    public enum SabotageType : byte { Reroll, Steal, BlockBasic, Neutralize }

    /// <summary>미니게임 종류 (Section 4.6)</summary>
    public enum MiniGameType : byte
    {
        None, HitTargets, HugCharacter, PatternUnlock, TapRepeat,
        GaugeMatch, BoilWater, ScrewTighten, CardPick, ClawGrab, TapeCut, RedTap
    }

    /// <summary>BuffDebuffSystem.Schedule()에서 사용하는 지연 효과 종류</summary>
    public enum EffectType : byte { TempChange, FanSpeedChange, BasicBlock, RecoveryRateChange }

    /// <summary>
    /// [DRAFT — 병합 전 검토 필요] SpecialItemDataSO 전용 효과 종류.
    /// Section 4에 미정의 — PlayerModifiers(2.3)/FanSpeed NV(2.1) 근거로 초안 작성.
    /// FanSpeedChange = 십자드라이버, ExtraAction/RevealOpponent = 속마음 타로카드
    /// </summary>
    public enum SpecialEffectType : byte { FanSpeedChange, ExtraAction, RevealOpponent }

    /// <summary>
    /// [임시 위치] 환경 변수 종류 — ItemContext.ActiveEnvironment에서만 사용.
    /// EnvironmentSystem(코어 담당) 구현 시 해당 파일로 이동할 것.
    /// SummerVacation/HeatWaveWarning 이름은 기획서 코드에서 직접 참조되므로 유지 필수.
    /// </summary>
    public enum EnvironmentType : byte
    {
        None = 0,
        SunnyDay,          // 햇살쨍쨍: 회복 2°/sec
        CoolBreeze,        // 바람선선: 회복 0°/sec
        CicadaSong,        // 매미울음: 연출 방해
        Kids,              // 잼민이들: 랜덤 아이템 1개 도난
        Ambulance,         // 앰뷸런스: 4턴째 열세 +10°
        SummerVacation,    // 여름방학: 준비 시간 10초
        HeatWaveWarning    // 폭염경보: 열세 플레이어 선공
    }
}
