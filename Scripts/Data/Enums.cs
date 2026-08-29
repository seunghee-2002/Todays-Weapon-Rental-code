// Scripts/Data/Enums.cs

namespace TodaysWeaponRental
{
    public enum Grade
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum WeaponType
    {
        Sword,
        Axe,
        Bow,
        Crossbow,
        Staff,
        Tome,
        Dagger,
        Shuriken
    }

    public enum AdventurerStat
    {
        STR,
        DEX,
        INT,
        LUK
    }

    /// <summary>2차 대화 테스트 종류. 0~3은 AdventurerStat과 인덱스가 일치한다.</summary>
    public enum TalkTestType
    {
        STR,
        DEX,
        INT,
        LUK,
        Trait
    }

    public enum ArmorType
    {
        Unarmored,
        LightArmor,
        HeavyArmor,
        MagicalArmor
    }

    public enum TraitType
    {
        Lucky,          // 행운아
        Veteran,        // 베테랑
        Looter,         // 약탈자
        Swift,          // 신속
        Rich,           // 부자
        Porter,         // 짐꾼
        Berserker,      // 광전사
        Enduring,       // 생존가
        Rising,         // 성장하는 자
        Famous,         // 유명인
        Haggler,        // 흥정꾼
        Coward,         // 겁쟁이
        Focused,        // 집중
        Butcher,        // 도축업자
        EasyExpert,     // 양학
        BattleManiac,   // 전투광
    }

    public enum VisitorType
    {
        Adventurer,
        WeaponShop,
        Blacksmith,
        EventNPC,
        InvestorResult,  // 수상한 투자자 결과 전달 NPC
        DeadEvent,       // 사망 모험가 이벤트 NPC (청소/부활제안/기적생환)
        Herald           // 모험 결과 보고 전령 (저녁 6시 등장)
    }

    public enum DeadEventKind
    {
        NoVisitor,      // 청소 이벤트 (일 한번 할라우 → 시간 경과 + 골드)
        ReviveOffer,    // 부활 제안 (골드 지불, 수락/거절)
        MiracleRevive   // 기적의 생환 (죽은 모험가 본인이 무료로 귀환)
    }

    public enum MaterialType
    {
        Enforce,
        Craft,
        Special
    }

    public enum DungeonEventType
    {
        Entrance,        // 입장 (모험 시작)
        Battle,         // 일반 전투
        MiniBoss,       // 중간보스
        Boss,           // 보스
        TreasureChest,  // 보물상자 (자동 성공)
        Rest,           // 휴식 (보호 횟수 +1)
        Trap,           // 함정 (성공률 패널티)
        TrapEvade,     // 함정 회피 (패널티 없이 넘어감)
        RareDrop,        // 희귀 드롭 (재료 추가)
        Retreat,         // 후퇴 (모험 종료)
        Retry,            // 재도전 (다시 시도)
        Return,      // 귀환 (모험 종료)
        Protection,     // 보호 발동
    }

    // 몬스터 카테고리 — EnemyMonster Animator 컨트롤러명(= DungeonBattleEventData.summary 접두사)과 1:1.
    // 렌더 스테이지의 카메라 ortho 크기·스폰 오프셋을 타입별로 결정하는 키.
    public enum MonsterCategory
    {
        Animal,
        Bear,
        Beetle,
        Bird,
        Blowfish,
        Buffalo,
        Crab,
        Goat,
        Goby,
        Jellyfish,
        Lizard,
        Octopus,
        Slime,
        Snail,
        Snake,
        Turtle,
        Wolf,
    }

    public enum AffectionLevel
    {
        Low,
        Medium,
        High,
        Max
    }

    public enum ReputationLevel
    {
        Bronze,
        Silver,
        Gold,
        Platinum,
        Diamond
    }

    public enum MorningEventType
    {
        WeaponEnhance,       // 1. 랜덤 무기 강화
        WeaponExchange,      // 2. 교환 상인
        SuspiciousInvestor,  // 3. 수상한 투자자
        WanderingBlacksmith, // 4. 떠돌이 대장장이
        GuildEnvoy,          // 5. 길드 사절단
        MysteryBox,          // 6. 수수께끼 상자 장수
        RefugeeHelp,         // 7. 난민 돕기
        Collector,           // 8. 수집가
        BlackMarket,         // 9. 암시장 상인
    }

    public enum MysteryBoxRewardType
    {
        Nothing,
        Gold,
        Material,
        Weapon
    }

    public enum BlacksmithType
    {
        None = -1,                       // 요청 없음
        CostReduction,           // 비용 30% 감소
        MaterialReduction,       // 재료 20% 감소
        SuccessRateBoost,        // 성공률 x1.1배
        DisassembleBoost        // 분해 결과물 x1.3배
    }

    public enum WeaponEffectType
    {
        DungeonGradeBonus,
        StatBonus,
        ArmorTypeBonus,
        WeaponTypeMatchBonus,
        AllStatBonus,
        RetreatPrevention,
        GreatSuccessBonus,
        DoubleReward,
        BattleGoldBonus,          // 일반 전투 골드 +N%
        MiniBossGoldBonus,        // 미니보스 골드 +N%
        BossGoldBonus,            // 보스 골드 +N%
        TreasureGoldBonus,        // 보물상자 골드 +N%
        AllGoldBonus,             // 모든 골드 +N%
        MaterialAmountBonus,      // 재료 획득량 +N개
        RestChanceBonus,          // Rest 이벤트 등장 확률 +N%
        TreasureChestChanceBonus, // TreasureChest 등장 확률 +N%
        RareDropChanceBonus,      // RareDrop 등장 확률 +N%
        FailGoldBonus,            // 실패 시 골드 보존 +N%
        TrapNegation,             // 함정 패널티 무효화 확률 N%
        SpecialMaterialChance,    // 특수 재료 드롭 확률 +N%
        EnforceMaterialBonus,     // 진화 재료 +N개
        EventCountBonus,          // 이벤트 수 +N개
        AdventureTimeReduction,   // 이벤트 간격 시간 -N%
    }

    public enum Gender
    {
        None,
        Male,
        Female
    }

    public enum AdventurerMood
    {
        Normal,         // 평범     — 기본 3종
        Moody,          // 기분파   — 기본 3종
        Depressed,      // 우울     — 기본 3종
        Overconfident,  // 자기과신 — 특수 (점수 낮을 때 출현)
        Confident,      // 자신감   — 특수 (점수 높을 때 출현)
    }
}