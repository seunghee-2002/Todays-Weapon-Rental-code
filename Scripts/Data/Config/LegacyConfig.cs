using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    public enum UpgradeKey
    {
        None = 0,

        StartingGold          = 1,
        WeaponRare            = 2,
        WeaponEpic            = 3,
        StartingInsight       = 4,
        InventorySlots        = 5,

        EnforceRate           = 6,
        EvolveRate            = 7,
        EnforceCost           = 8,
        EvolveCost            = 9,
        MaterialReduction     = 10,
        RerollCount           = 11,
        RerollCost            = 12,
        DisassembleBonus      = 13,

        CommissionRate        = 14,
        TipRate               = 15,
        GreatSuccessRate      = 16,
        AdventureSpeed        = 17,

        RegularAdventurer     = 18,
        MorningEventGuarantee = 19,
        NamedSpawnWeight      = 20,
        ShopRefresh           = 21,
    }

    [Serializable]
    public class LegacyUpgradeDefinition
    {
        public UpgradeKey key;
        [Tooltip("식별자용 표시 이름")]
        public string displayName;
        [Tooltip("UI에 표시할 아이콘 스프라이트")]
        public Sprite icon;
        [Tooltip("선행 업그레이드. 없으면 None")]
        public UpgradeKey prerequisite;
        public int maxLevel;
        public int baseCost;
        [Tooltip("레벨당 비용 배율")]
        public float costScalePerLevel = 1.5f;
        [Tooltip("레벨당 효과 값 (각 getter 메서드의 공식에 따라 해석됨)")]
        public float effectValue;
        [Tooltip("현재 누적 효과 표시 템플릿. {current}=effectValue×level, {value}=effectValue (예: 시작 골드 +{current})")]
        public string description;
        [Tooltip("다음 단계 효과 텍스트 (초록색 표시). {value}=effectValue (예: +{value} 골드)")]
        public string upgradeStepDescription;
    }

    [CreateAssetMenu(fileName = "LegacyConfig", menuName = "TodaysWeaponRental/Config/LegacyConfig")]
    public class LegacyConfig : ScriptableObject
    {
        [Header("업그레이드 목록")]
        public List<LegacyUpgradeDefinition> upgrades = new();

        [Header("AdventurerTalk 프리미엄 비용")]
        [Tooltip("프리미엄 대화 유산 비용 공식: CeilToInt(시간비용 / divisor)")]
        public float adventurerTalkLegacyCostDivisor = 8f;
        [Tooltip("네임드 모험가 프리미엄 대화 비용 배율")]
        public float namedTalkLegacyCostMultiplier = 3f;

        [Header("재부여 추가 1회 비용")]
        [Tooltip("재부여 소진 시 유산으로 1회 추가 재부여하는 등급별 비용 (Common=0 ~ Legendary=4), 예: [1, 3, 5, 7, 10]")]
        public int[] extraRerollCostByGrade = { 1, 3, 5, 7, 10 };

        [Header("유산 골드 환전")]
        [Tooltip("유산 1당 환전되는 골드량. StartingGold 업그레이드(80유산 -> +1000G/회차)의 손익분기를 4회차로 맞춘 값")]
        public int goldPerLegacy = 50;

        [Header("튜토리얼 완주 보상")]
        [Tooltip("계정 최초 1회 튜토리얼 완주 시 지급하는 유산")]
        public int tutorialCompleteReward = 100;

        [Header("강화 즉시 재시도 비용")]
        [Tooltip("등급별 기본 유산 비용 (Common=0 ~ Legendary=4), 예: [1, 2, 3, 5, 8]")]
        public int[] enforceRetryCostByGrade = { 1, 2, 3, 5, 8 };
        [Tooltip("강화 레벨당 추가 유산 비용, 예: 1")]
        public int enforceRetryCostPerLevel = 1;
    }
}
