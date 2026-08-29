// Scripts/Data/Config/TraitConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "TraitConfig", menuName = "TodaysWeaponRental/Config/TraitConfig")]
    public class TraitConfig : ScriptableObject
    {
        [Header("행운아 - Lucky")]
        [Tooltip("대성공 확률 보너스")]
        public float traitLuckyGreatSuccessBonus = 0.05f;

        [Header("베테랑 - Veteran")]
        [Tooltip("탐험도 상승량 배율")]
        public float traitVeteranExplorationMultiplier = 2.0f;

        [Header("수집가 - Looter")]
        [Tooltip("재료 드롭 보너스 개수")]
        public int traitLooterMaterialBonus = 1;

        [Header("신속 - Swift")]
        [Tooltip("모험 소요 시간 배율 (탐험도 0은 코드 분기 처리)")]
        public float traitSwiftDurationMultiplier = 0.5f;

        [Header("부자 - Rich")]
        [Tooltip("수수료 대비 팁 비율")]
        public float traitRichTipRate = 0.1f;

        [Header("짐꾼 - Porter")]
        [Tooltip("재료 구매 가격 배율")]
        public float traitPorterMaterialPriceMultiplier = 0.5f;

        [Header("광전사 - Berserker")]
        [Tooltip("성공률 배율")]
        public float traitBerserkerSuccessMultiplier = 1.1f;
        [Tooltip("사망률 배율")]
        public float traitBerserkerDeathMultiplier = 1.1f;

        [Header("생존가 - Enduring")]
        [Tooltip("모험 시작 시 추가 사망 보호 충전 수")]
        public int traitEnduringProtectionBonus = 1;

        [Header("성장하는 자 - Rising")]
        [Tooltip("던전 등급당 성공률 보너스")]
        public float traitRisingBonusPerTier = 0.02f;

        [Header("유명인 - Famous")]
        [Tooltip("성공/대성공 시 추가 평판 획득량")]
        public int traitFamousReputationBonus = 2;

        [Header("흥정꾼 - Haggler")]
        [Tooltip("수수료 변동 - 낮음 (-15%)")]
        public float traitHagglerRateLow = -0.15f;
        [Tooltip("수수료 변동 - 중간 (0%)")]
        public float traitHagglerRateMid = 0f;
        [Tooltip("수수료 변동 - 높음 (+20%)")]
        public float traitHagglerRateHigh = 0.2f;

        [Header("겁쟁이 - Coward")]
        [Tooltip("사망률 배율")]
        public float traitCowardDeathMultiplier = 0.3f;
        [Tooltip("성공률 배율")]
        public float traitCowardSuccessMultiplier = 0.85f;

        [Header("집중 - Focused")]
        [Tooltip("소요 시간 배율")]
        public float traitFocusedDurationMultiplier = 1.5f;
        [Tooltip("성공률 보너스")]
        public float traitFocusedSuccessBonus = 0.15f;

        [Header("도축업자 - Butcher")]
        [Tooltip("재료 드롭 보너스 개수")]
        public int traitButcherMaterialBonus = 2;
        [Tooltip("수수료 배율")]
        public float traitButcherFeeMultiplier = 0.8f;

        [Header("양학 - EasyExpert")]
        [Tooltip("1~2등급 던전 성공률 보너스")]
        public float traitEasyExpertLowTierBonus = 0.2f;
        [Tooltip("3등급 이상 던전 성공률 패널티")]
        public float traitEasyExpertHighTierPenalty = -0.15f;

        [Header("전투광 - BattleManiac")]
        [Tooltip("성공률 보너스 (탐험도 0은 코드 분기 처리)")]
        public float traitBattleManiacSuccessBonus = 0.1f;
    }
}
