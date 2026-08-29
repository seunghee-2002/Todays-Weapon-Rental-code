// Scripts/Data/Config/AdventureConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "AdventureConfig", menuName = "TodaysWeaponRental/Config/AdventureConfig")]
    public class AdventureConfig : ScriptableObject
    {
        [Header("성공률 범위")]
        [Tooltip("최소 성공률")]
        public float successRateMin = 0f;
        [Tooltip("최대 성공률")]
        public float successRateMax = 1f;
        [Tooltip("기본 무기 사용 시 성공률 배율")]
        public float defaultWeaponSuccessMultiplier = 0.8f;

        #region 사망률 계산
        
        [Header("사망률 계산")]
        [Tooltip("기본 사망률 (%)")]
        public float baseDeathRate = 5f;
        [Tooltip("스탯이 던전 요구치에 미달할 때 최대 가산 사망률 (%)")]
        public float deathRateStatWeight = 25f;
        [Tooltip("무기 등급 차이당 보호 효과 (%)")]
        public float weaponProtectionGradeDiff = 3f;
        [Tooltip("무기 강화 레벨당 보호 효과 (%)")]
        public float weaponProtectionEnforcement = 0.5f;
        [Tooltip("최대 사망률")]
        public float maxDeathRate = 0.5f;
        [Tooltip("STR 사망 재굴림 최대 확률 (STR 100 기준)")]
        public float strDeathRerollMax = 0.5f;
        [Tooltip("STR 사망 재굴림 곡선 지수. 클수록 고스탯에서만 효과가 커진다")]
        public float strDeathRerollExponent = 2.32f;

        #endregion

        #region 함정

        [Header("함정")]
        [Tooltip("DEX 함정 회피 최대 확률 (DEX 100 기준)")]
        public float dexTrapEvadeMax = 0.5f;
        [Tooltip("DEX 함정 회피 곡선 지수. 클수록 고스탯에서만 효과가 커진다")]
        public float dexTrapEvadeExponent = 2.32f;

        #endregion

        #region 대성공

        [Header("대성공 시스템")]
        [Tooltip("기본 대성공 확률")]
        public float baseGreatSuccessChance = 0.05f;

        #endregion

        #region 탐험도

        [Header("탐험도")]
        [Tooltip("모험 성공 시 탐험도 증가량")]
        public int explorationGainOnSuccess = 10;
        [Tooltip("모험 실패 시 탐험도 증가량")]
        public int explorationGainOnFail = 5;

        #endregion

        #region 소요 시간 계산
        
        [Tooltip("AdventureTimeReduction 효과 최대 감소율 (0~1)")]
        public float adventureTimeReductionMax = 0.5f;
        
        #endregion

        #region 보너스
        
        [Header("호감도 보너스")]
        [Tooltip("호감도 Max 보너스")]
        public float affectionMaxBonus = 0.05f;
        [Tooltip("호감도 High 보너스")]
        public float affectionHighBonus = 0.03f;
        [Tooltip("호감도 Medium 보너스")]
        public float affectionMediumBonus = 0.01f;

        [Header("수집 보너스")]
        [Tooltip("던전 클리어 카운트 마일스톤 간격")]
        public int dungeonClearMilestone = 10;
        [Tooltip("던전 클리어 마일스톤당 보너스")]
        public float dungeonClearMilestoneBonus = 0.02f;
        [Tooltip("무기 사용 횟수 마일스톤 간격")]
        public int weaponUsageMilestone = 20;
        [Tooltip("무기 사용 마일스톤당 보너스")]
        public float weaponUsageMilestoneBonus = 0.01f;
        [Tooltip("무기 사용 마일스톤 최대 횟수")]
        public int weaponUsageMilestoneMax = 5;
        
        #endregion

        #region 골드 보상

        [Header("던전 등급 난이도 배율")]
        [Tooltip("Common 던전 난이도 배율")]
        public float dungeonCommonMultiplier = 1.0f;
        [Tooltip("Uncommon 던전 난이도 배율")]
        public float dungeonUncommonMultiplier = 1.3f;
        [Tooltip("Rare 던전 난이도 배율")]
        public float dungeonRareMultiplier = 1.6f;
        [Tooltip("Epic 던전 난이도 배율")]
        public float dungeonEpicMultiplier = 2.0f;
        [Tooltip("Legendary 던전 난이도 배율")]
        public float dungeonLegendaryMultiplier = 5.0f;
        
        #endregion

        #region 재료 보상
        
        [Header("재료 드롭")]
        [Tooltip("기본 재료 드롭 최소 개수")]
        public int materialDropMin = 1;
        [Tooltip("기본 재료 드롭 최대 개수")]
        public int materialDropMax = 4;
        [Tooltip("대성공 시 재료 배율")]
        public int greatSuccessMaterialMultiplier = 2;
        [Tooltip("대성공 시 보스 골드 배율")]
        public float greatSuccessGoldMultiplier = 2f;
        // 이전에는 MaterialData.baseValue를 가중치로 썼는데, 고등급 재료가 baseValue가 높아
        // 같은 던전 안에서 오히려 더 자주 나왔다(희귀 던전 17/33/50%). 수요는 반대로
        // 일반 재료가 무기 1자루당 6~10개, 희귀 재료는 아이템 1개당 1개다.
        [Tooltip("제작 재료 드롭 추첨 가중치 (일반/고급/희귀/영웅/전설). 클수록 자주 나온다")]
        public int[] materialDropWeightByGrade = { 6, 3, 1, 1, 1 };

        [Header("특수 재료 드롭")]
        [Tooltip("특수 재료 드롭 최소 개수")]
        public int specialMaterialDrop = 1;
        // 등급 난이도는 이미 등장 비율이 담당한다(1~4주 일반 57.5%/전설 0.4% -> 31~40주 일반 2.0%/전설 39.9%).
        // 드롭 확률까지 전 등급 균일이면 저등급 던전이 이중으로 죽어, 그 재료를 쓰는 레시피
        // (선물 12종 + 희귀 무기 8종)가 후반에 통째로 막힌다. 등장 비율과 반대 방향으로 걸어
        // "그 던전에 가면 그 재료가 나온다"가 성립하게 한다 - 그래도 총 공급은 여전히 고등급 우위다.
        [Tooltip("Common 특수 재료 드롭 확률")]
        public float specialDropCommon = 0.35f;
        [Tooltip("Uncommon 특수 재료 드롭 확률")]
        public float specialDropUncommon = 0.3f;
        [Tooltip("Rare 특수 재료 드롭 확률")]
        public float specialDropRare = 0.25f;
        [Tooltip("Epic 특수 재료 드롭 확률")]
        public float specialDropEpic = 0.2f;
        [Tooltip("Legendary 특수 재료 드롭 확률")]
        public float specialDropLegendary = 0.15f;
        
        #endregion

        #region 평판&호감도
        
        [Header("평판 변화")]
        [Tooltip("모험 성공 시 등급별 평판 가산 (일반/고급/희귀/영웅/전설). 진행 칸 수의 절반과 합산된다")]
        public int[] gradeRepBonus = { 2, 3, 5, 7, 11 };
        [Tooltip("모험 실패 시 등급별 감점 기준 (일반/고급/희귀/영웅/전설). '진행 칸 수 절반 - 이 값'이 평판 변화이며 상한 -1")]
        public int[] gradeRepFailBase = { 3, 3, 4, 4, 5 };
        [Tooltip("타입 매칭 시 평판 증가")]
        public int typeMatchReputationGain = 1;
        [Tooltip("대성공 시 평판 증가")]
        public int greatSuccessReputationGain = 2;

        [Header("호감도 변화")]
        [Tooltip("모험 성공 시 호감도 증가")]
        public int successAffectionGain = 3;
        [Tooltip("타입 매칭 시 호감도 증가")]
        public int typeMatchAffectionGain = 2;
        [Tooltip("모험 실패 시 호감도 감소")]
        public int failAffectionLoss = -3;
        
        #endregion

        #region 사망
        
        [Header("사망 시스템")]
        [Tooltip("사망 시 평판 감소 (CalculateRewards에서 사용)")]
        public int deathReputationChange = -50;
        [Tooltip("사망 보호 사용 시 호감도 비율")]
        public float deathProtectionAffectionRatio = 0.5f;
        [Tooltip("최대 사망 보호 충전 수")]
        public int maxProtectionCharges = 5;
        [Tooltip("보관할 모험 결과 로그 최대 건수 (건당 약 0.9KB, Cloud Save 한도 5MB)")]
        public int maxCompletedResultLog = 1000;
        
        #endregion

        #region 수수료

        [Header("수수료")]
        [Tooltip("기본 수수료율 (항상 적용)")]
        public float baseCommissionRate = 0.20f;
        [Tooltip("무기 대여 시 추가 수수료율")]
        public float rentalCommissionRate = 0.15f;

        [Header("상성 팁 임계값")]
        [Tooltip("이 값 이상이면 팁 5%")]
        public float tipThreshold1 = 0.01f;
        [Tooltip("이 값 이상이면 팁 10%")]
        public float tipThreshold2 = 0.20f;
        [Tooltip("이 값 이상이면 팁 15%")]
        public float tipThreshold3 = 0.35f;
        [Tooltip("이 값 이상이면 팁 20%")]
        public float tipThreshold4 = 0.50f;
        public float tipRate1 = 0.05f;
        public float tipRate2 = 0.10f;
        public float tipRate3 = 0.15f;
        public float tipRate4 = 0.20f;

        #endregion

        #region 이벤트

        [Header("이벤트 시스템")]
        [Tooltip("등급별 최대 이벤트 수 (Common/Uncommon/Rare/Epic/Legendary)")]
        public int[] maxEventCountByGrade = { 3, 4, 5, 7, 10 };
        [Tooltip("이벤트 1개당 소요 인게임 시간 (시간 단위, 1 = 1시간)")]
        public int eventIntervalHours = 1;

        [Header("보상 비율")]
        [Tooltip("후퇴 시 누적 보상 지급 비율 (1.0 = 100%)")]
        public float retreatGoldRatio = 0.5f;

        [Header("부가효과")]
        [Tooltip("EventCountBonus 최대 추가 이벤트 수")]
        public int eventCountBonusMax = 3;

        [Header("이벤트 보상 배율")]
        [Tooltip("Battle 이벤트 보상 배율")]
        public float battleRewardMultiplier = 1.0f;
        [Tooltip("MiniBoss 이벤트 보상 배율")]
        public float miniBossRewardMultiplier = 1.5f;
        [Tooltip("Boss 이벤트 보상 배율")]
        public float bossRewardMultiplier = 2.0f;
        [Tooltip("TreasureChest 이벤트 보상 배율")]
        public float treasureChestRewardMultiplier = 2.5f;
        [Tooltip("RareDrop 추가 재료 수량")]
        public int rareDropMaterialCount = 2;

        [Header("누적 보정치")]
        [Tooltip("Trap 이벤트 성공률 패널티")]
        public float trapSuccessPenalty = -0.1f;

        #endregion

        #region 기분 시스템 (Mood)

        [Header("Mood System - Score Threshold")]
        [Tooltip("matchScore 정규화 분모 (던전 무관, 모험가+무기 매칭 품질 기준)")]
        public float moodScoreThreshold = 200f;

        [Header("Mood System - Multiplier Ranges")]
        public Vector2 moodNormalRange        = new Vector2(0.70f, 1.30f);
        public Vector2 moodMoodyLowRange      = new Vector2(0.70f, 0.85f);
        public Vector2 moodMoodyHighRange     = new Vector2(1.15f, 1.30f);
        public Vector2 moodDepressedRange     = new Vector2(0.90f, 1.10f);
        public Vector2 moodOverconfidentRange = new Vector2(0.75f, 1.10f);
        public Vector2 moodConfidentRange     = new Vector2(0.90f, 1.25f);

        [Header("Mood System - Probability Curve (Overconfident at matchScore 0/0.5/1)")]
        [Tooltip("matchScore 0.0일 때 Overconfident 확률")]
        public float moodOverconfidentProbAtZero = 0.35f;
        [Tooltip("matchScore 0.5일 때 Overconfident 확률")]
        public float moodOverconfidentProbAtMid  = 0.10f;
        [Tooltip("matchScore 1.0일 때 Overconfident 확률 (Confident는 미러링)")]
        public float moodOverconfidentProbAtMax  = 0.05f;

        [Header("Mood System - Half Strength Events")]
        [Tooltip("Boss/Trap 이벤트에 적용되는 강도 계수 (1을 중심으로 한 압축률)")]
        public float moodHalfStrength = 0.5f;

        #endregion

        // 기분 확률쌍 검증 - pOver+pConf > 1이면 기본 기분 구간이 음수가 되어 분포가 붕괴한다.
        // Confident는 Overconfident의 미러이므로 앵커별 쌍은 (Zero,Max) / (Mid,Mid) / (Max,Zero)
        private void OnValidate()
        {
            ValidateMoodPair("matchScore=0", moodOverconfidentProbAtZero, moodOverconfidentProbAtMax);
            ValidateMoodPair("matchScore=0.5", moodOverconfidentProbAtMid, moodOverconfidentProbAtMid);
            ValidateMoodPair("matchScore=1", moodOverconfidentProbAtMax, moodOverconfidentProbAtZero);
        }

        private static void ValidateMoodPair(string label, float over, float confident)
        {
            if (over < 0f || confident < 0f || over + confident > 1f)
                Log.Error($"[AdventureConfig] 기분 확률쌍 오류 ({label}): over={over}, confident={confident} - 합이 0~1 범위를 벗어남");
        }
    }
}
