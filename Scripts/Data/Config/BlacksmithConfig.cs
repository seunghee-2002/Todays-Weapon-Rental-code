// Scripts/Data/Config/BlacksmithConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "BlacksmithConfig", menuName = "TodaysWeaponRental/Config/BlacksmithConfig")]
    public class BlacksmithConfig : ScriptableObject
    {
        [Header("Blacksmith Data")]
        public BlacksmithData[] blacksmithDataArray;

        [Header("Spawn System")]
        [Tooltip("요청한 타입 확률")]
        public float requestedTypeBonus = 0.5f;
        [Tooltip("프리미엄 대장장이 등장 확률 (0~1)")]
        public float premiumSpawnChance = 0.2f;

        [Header("Blacksmith Type Bonuses")]
        [Tooltip("성공률 부스트 - 일반 (가산, 0.1 = +10%)")]
        public float successRateBoostNormal = 0.1f;
        [Tooltip("성공률 부스트 - 프리미엄 (가산, 0.2 = +20%)")]
        public float successRateBoostPremium = 0.2f;
        [Tooltip("비용 감소 배율 - 일반 (곱산, 0.7 = 30% 감소)")]
        public float costReductionNormal = 0.7f;
        [Tooltip("비용 감소 배율 - 프리미엄 (곱산, 0.5 = 50% 감소)")]
        public float costReductionPremium = 0.5f;
        [Tooltip("재료 감소 배율 - 일반 (곱산, 0.8 = 20% 감소)")]
        public float materialReductionNormal = 0.8f;
        [Tooltip("재료 감소 배율 - 프리미엄 (곱산, 0.65 = 35% 감소)")]
        public float materialReductionPremium = 0.65f;
        [Tooltip("분해 보상 배율 - 일반 (곱산, 1.3 = 30% 증가)")]
        public float disassembleBoostNormal = 1.3f;
        [Tooltip("분해 보상 배율 - 프리미엄 (곱산, 1.5 = 50% 증가)")]
        public float disassembleBoostPremium = 1.5f;

        [Header("Success Rates")]
        [Tooltip("강화 성공률 (레벨별: +0 → +5)")]
        public float[] enforceSuccessRates = { 65f, 50f, 35f, 15f, 5f };
        [Tooltip("진화 성공률 (등급별: Common → Legendary)")]
        public float[] evolveSuccessRates = { 60f, 40f, 25f, 10f };

        [Header("Disassemble Rewards")]
        [Tooltip("등급별 분해 골드 (Common ~ Legendary)")]
        public int[] disassembleGoldByGrade = { 50, 150, 400, 1000, 3000 };
        [Tooltip("등급별 분해 재료 (Common ~ Legendary)")]
        public int[] disassembleMaterialByGrade = { 2, 5, 10, 20, 40 };
        [Tooltip("강화 레벨당 보너스 (20%)")]
        public float disassembleEnforceBonus = 0.2f;

        [Header("Enforce Cost")]
        [Tooltip("등급별 강화 기본 골드 (Common ~ Legendary). 실제 비용 = baseGold × (enforceLevel + 1)")]
        public int[] enforceBaseGoldByGrade = { 100, 200, 500, 1000, 2000 };

        [Header("Evolve Cost")]
        [Tooltip("등급별 진화 골드 (Common ~ Legendary). Legendary는 진화 불가이므로 0.")]
        public int[] evolveCostByGrade = { 500, 1000, 5000, 10000, 0 };
        [Tooltip("진화 주력 재료 개수 - 현재 등급의 진화 재료")]
        public int evolveMainMaterialCount = 5;
        [Tooltip("진화 상위 재료 개수 - 다음 등급의 진화 재료")]
        public int evolveNextMaterialCount = 3;

        [Header("Reroll System")]
        [Tooltip("마법 재부여 기본 골드 비용. 잠금 개수 배율이 곱해진다. " +
                 "일수 비례를 쓰지 않는 이유는 모험 수입(R)이 자연히 성장하기 때문 — 이중 인플레를 피한다")]
        public int rerollBaseGold = 2500;
        [Tooltip("잠금 개수(1~5)별 비용 배율. 잠금 없을 때는 x1 고정. 5개(전체)=0으로 설정 시 재부여 불가 표시.")]
        public float[] rerollLockCostMultipliers = { 1.5f, 2.0f, 3.0f, 4.5f, 0f };
    }
}