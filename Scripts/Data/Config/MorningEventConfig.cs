// Scripts/Data/Config/MorningEventConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "MorningEventConfig", menuName = "TodaysWeaponRental/Config/MorningEventConfig")]
    public class MorningEventConfig : ScriptableObject
    {
        [Header("이벤트 등장 가중치 (순서: WeaponEnhance ~ BlackMarket)")]
        public float[] eventWeights = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        [Header("상자/보상에서 지급될 재료 ID 풀")]
        public string[] rewardMaterialPool;

        // ─────────────────────────────────────────────
        // 1. 랜덤 무기 강화
        // ─────────────────────────────────────────────
        [Header("1. 랜덤 무기 강화")]
        public float enhanceMinus1Chance = 0.15f;
        public float enhanceZeroChance   = 0.15f;
        public float enhancePlus1Chance  = 0.55f;
        public float enhancePlus2Chance  = 0.14f;
        // Plus3 = 1 - (0.15+0.15+0.55+0.14) = 0.01

        // ─────────────────────────────────────────────
        // 2. 교환 상인 (등급합 0~8 → 결과 등급 확률, 각 5개 float[])
        // ─────────────────────────────────────────────
        [Header("2. 교환 상인 — 등급합별 결과 확률 (index = 결과 등급)")]
        public float[] exchange0 = { 0.70f, 0.30f, 0f,    0f,    0f    };
        public float[] exchange1 = { 0.40f, 0.50f, 0.10f, 0f,    0f    };
        public float[] exchange2 = { 0.15f, 0.55f, 0.25f, 0.05f, 0f    };
        public float[] exchange3 = { 0.05f, 0.35f, 0.45f, 0.15f, 0f    };
        public float[] exchange4 = { 0f,    0.20f, 0.54f, 0.25f, 0.01f };
        public float[] exchange5 = { 0f,    0.10f, 0.40f, 0.45f, 0.05f };
        public float[] exchange6 = { 0f,    0f,    0.10f, 0.60f, 0.30f };
        public float[] exchange7 = { 0f,    0f,    0.05f, 0.35f, 0.60f };
        public float[] exchange8 = { 0f,    0f,    0f,    0.15f, 0.85f };

        // ─────────────────────────────────────────────
        // 3. 수상한 투자자
        // ─────────────────────────────────────────────
        [Header("3. 수상한 투자자")]
        public int   investMinGold        = 1000;
        public int   investMaxGoldPerDay  = 500;   // 최대 = 진행일수 × this
        public float investLoseChance     = 0.40f; // × 0
        public float investSuccessChance  = 0.50f; // × 1.5
        public float investBigChance      = 0.09f; // × 2
        // 대박(× 5) = 1 - 합계 = 0.01
        public float investSuccessMulti   = 1.5f;
        public float investBigMulti       = 2.0f;
        public float investJackpotMulti   = 5.0f;

        [Header("투자액 비율별 대화 임계값 (min~max 정규화 0~1, 4개 → 5구간: Min/Low/Mid/High/Max)")]
        public float[] investAmountDialogueThresholds = { 0.05f, 0.30f, 0.60f, 0.95f };

        // ─────────────────────────────────────────────
        // 4. 떠돌이 대장장이
        // ─────────────────────────────────────────────
        [Header("4. 떠돌이 대장장이")]
        public float blacksmithEnhancePlus1 = 0.80f;
        public float blacksmithEnhancePlus2 = 0.19f;
        // Plus3 = 0.01

        // ─────────────────────────────────────────────
        // 5. 길드 사절단
        // ─────────────────────────────────────────────
        [Header("5. 길드 사절단 — 평판 등급별 선물 확률 ([0]=Bronze … [4]=Diamond)")]
        public float[] guildGiftChance = { 0.60f, 0.70f, 0.80f, 0.90f, 0.95f };

        [Header("선물 골드 확률 (고정가)")]
        public float guildGoldLowChance  = 0.35f; // guildGoldLow 지급
        public float guildGoldMidChance  = 0.45f; // guildGoldMid 지급
        // 고액 = 0.20
        public int   guildGoldLow        = 250;
        public int   guildGoldMid        = 500;
        public int   guildGoldHigh       = 750;
        public int   guildMaterialCount  = 2;

        [Header("강제 납부 금액 확률 (고정가)")]
        public float guildForceLowChance = 0.70f; // guildForceLow 납부
        // 고액 = 0.30
        public int   guildForceLow       = 500;
        public int   guildForceHigh      = 1000;

        // ─────────────────────────────────────────────
        // 6. 수수께끼 상자
        // ─────────────────────────────────────────────
        [Header("6. 수수께끼 상자 — 등장 확률 가중치 (일반/희귀/신비)")]
        public float[] boxTierWeights = { 0.60f, 0.30f, 0.10f };

        [Header("6. 수수께끼 상자 — 가격 (고정)")]
        public int boxNormalCost = 1500;
        public int boxRareCost   = 4000;
        public int boxMythicCost = 10000;

        [Header("일반 상자 확률")]
        public float boxNormalMaterial = 0.40f;
        public float boxNormalGold     = 0.30f;
        public float boxNormalWeapon   = 0.20f;
        // 꽝 = 0.10
        public int   boxNormalGoldMin = 2000;
        public int   boxNormalGoldMax = 4000;

        [Header("희귀 상자 확률")]
        public float boxRareMaterial   = 0.30f;
        public float boxRareGold       = 0.25f;
        public float boxRareWeapon     = 0.30f;
        // 꽝 = 0.15
        public int   boxRareGoldMin  = 5500;
        public int   boxRareGoldMax  = 10500;

        [Header("신비 상자 확률")]
        public float boxMythicMaterial = 0.25f;
        public float boxMythicGold     = 0.20f;
        public float boxMythicWeapon   = 0.40f;
        // 꽝 = 0.15
        public int   boxMythicGoldMin = 14500;
        public int   boxMythicGoldMax = 22000;

        // ─────────────────────────────────────────────
        // 7. 난민 돕기
        // ─────────────────────────────────────────────
        [Header("7. 난민 돕기")]
        public int   refugeeCost                    = 500;
        public float refugeeDonateRepLowChance      = 0.70f; // +refugeeDonateRepLow
        // +refugeeDonateRepHigh = 0.30
        public int   refugeeDonateRepLow            = 5;
        public int   refugeeDonateRepHigh           = 10;
        public float refugeeRejectRepPenaltyChance  = 0.60f; // -refugeeRejectRepPenalty, 나머지 변화 없음
        public int   refugeeRejectRepPenalty        = 5;

        // ─────────────────────────────────────────────
        // 8. 수집가
        // ─────────────────────────────────────────────
        [Header("8. 수집가")]
        public Grade collectorMinGrade    = Grade.Rare;
        public float collectorMult3Chance = 0.40f; // × 3
        public float collectorMult4Chance = 0.40f; // × 4
        public float collectorMult5Chance = 0.20f; // × 5 (나머지 = 1 - mult3 - mult4)

        // ─────────────────────────────────────────────
        // 9. 암시장 상인
        // ─────────────────────────────────────────────
        [Header("9. 암시장 상인")]
        public Grade blackMarketMinGrade  = Grade.Rare;
        public float blackMarketDiscount  = 0.50f; // 50% 할인
        public int   blackMarketRepPenalty = 10;

        // ─────────────────────────────────────────────
        // 헬퍼 — 교환 테이블 접근
        // ─────────────────────────────────────────────
        public float[] GetExchangeTable(int gradeSum)
        {
            return gradeSum switch
            {
                0 => exchange0,
                1 => exchange1,
                2 => exchange2,
                3 => exchange3,
                4 => exchange4,
                5 => exchange5,
                6 => exchange6,
                7 => exchange7,
                8 => exchange8,
                _ => exchange8
            };
        }
    }
}
