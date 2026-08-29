using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "InsightConfig", menuName = "TodaysWeaponRental/Config/InsightConfig")]
    public class InsightConfig : ScriptableObject
    {
        [Header("일반 모험가 — 자동 공개 임계값")]
        public int normalReveal1StatThreshold = 15;
        public int normalRevealAverageThreshold = 30;
        public int normalReveal2StatThreshold = 50;
        public int normalRevealHighestStatThreshold = 70;

        [Header("2차 대화 — 시간 제한")]
        [Tooltip("대화 행동 최대 소요 시간 (게임 내 분). 남은 시간과 이 값 중 더 작은 값을 기준으로 가능 여부를 판단.")]
        public int maxTalkDurationMinutes = 180;

        [Header("2차 대화 — 소요 시간 단계 (게임 내 분)")]
        public int statTalkCostDefault = 60;
        public int statTalkCostAt30 = 42;
        public int statTalkCostAt50 = 24;
        public int statTalkCostAt70 = 12;

        [Header("2차 대화 — 성공 확률 공식")]
        [Tooltip("개별 스탯 기본 성공 확률 (%)")]
        public float statRevealBaseChance = 70f;
        [Tooltip("개별 스탯 - 통찰 1당 성공률 보정 (%)")]
        public float statRevealInsightBonus = 0.4f;
        [Tooltip("전체 스탯 기본 성공 확률 (%)")]
        public float allStatRevealBaseChance = 35f;
        [Tooltip("전체 스탯 - 통찰 1당 성공률 보정 (%)")]
        public float allStatRevealInsightBonus = 0.65f;
        [Tooltip("이미 공개된 스탯 1개당 패널티 기준 (%)")]
        public float statRevealPenaltyBase = 30f;
        [Tooltip("통찰 1당 패널티 감소 계수")]
        public float statRevealPenaltyInsightFactor = 0.2f;
        [Tooltip("전체 스탯 밝히기 패널티 배율")]
        public float allStatPenaltyMultiplier = 1f;
        [Tooltip("특성 밝히기 기본 성공 확률 (%)")]
        public float traitRevealBaseChance = 50f;
        [Tooltip("특성 밝히기 통찰 1당 보정 (%)")]
        public float traitRevealInsightBonus = 0.8f;
        [Tooltip("네임드 모험가 최종 성공률 배율. 네임드는 정보가 영구 보존되므로 확률을 낮춘다.")]
        public float namedRevealChanceMultiplier = 0.4f;
        [Tooltip("INT가 공개된 모험가의 테스트 성공률 최대 가산 (%p, INT 100 기준)")]
        public float intRevealBonusMax = 30f;
        [Tooltip("INT 테스트 보정 곡선 지수. 클수록 고스탯에서만 효과가 커진다")]
        public float intRevealBonusExponent = 2.32f;

        [Header("2차 대화 — 무기 힌트")]
        [Tooltip("무기 타입 힌트 해금에 필요한 최소 통찰값")]
        public int weaponHintRequiredInsight = 0;

        [Header("대화 애니메이션")]
        [Tooltip("대화 애니메이션 게이지 채우는 실제 소요 시간 (초)")]
        public float talkAnimDuration = 1.5f;

        [Header("수색꾼 파견 비용")]
        [Tooltip("파견 비용 = scoutBaseCost x 등급배율 (수색은 이미 등급 연동이라 주차 계단 미적용)")]
        public int scoutBaseCost = 250;

        [Header("수색꾼 — 소요 시간 임계값")]
        [Tooltip("이 통찰값 이상이면 scoutBaseDurationAt25 적용")]
        public int scoutDurationTier25Threshold = 25;
        [Tooltip("이 통찰값 이상이면 scoutBaseDurationAt50 적용")]
        public int scoutDurationTier50Threshold = 50;
        [Tooltip("이 통찰값 이상이면 scoutBaseDurationAt75 적용")]
        public int scoutDurationTier75Threshold = 75;

        [Header("수색꾼 기본 소요 시간 (게임 내 분)")]
        [Tooltip("통찰 0~24 구간 기본 소요 시간")]
        public int scoutBaseDurationDefault = 120;
        [Tooltip("통찰 25~49 구간 기본 소요 시간")]
        public int scoutBaseDurationAt25 = 80;
        [Tooltip("통찰 50~74 구간 기본 소요 시간")]
        public int scoutBaseDurationAt50 = 40;
        [Tooltip("통찰 75~100 구간 기본 소요 시간")]
        public int scoutBaseDurationAt75 = 20;

        [Header("수색꾼 소요 시간 — 보정")]
        [Tooltip("등급별 배수 (Common / Uncommon / Rare / Epic / Legendary). 최종 시간 = 기본 소요시간 × 등급 배수 × 랜덤")]
        public float[] scoutGradeMultipliers = { 0.8f, 1f, 1.2f, 1.5f, 2f };
        [Tooltip("랜덤 배수 최솟값")]
        public float scoutRandomMin = 0.5f;
        [Tooltip("랜덤 배수 최댓값")]
        public float scoutRandomMax = 1.5f;

        [Header("모험 준비 화면 — 성공률 표시 임계값")]
        [Tooltip("이 통찰값 이상이면 기본/최종 성공률 행 표시")]
        public int successRateRevealThreshold = 80;

        [Header("던전 정보 공개 임계값")]
        [Tooltip("이 통찰값 이상이면 예상 소요 시간 표시")]
        public int estimatedDurationRevealThreshold = 55;

        [Header("암시장 — 효과 공개")]
        [Tooltip("통찰 N당 효과 1개씩 공개. revealedCount = floor(통찰 / step)")]
        public int blackMarketEffectRevealStep = 20;

        [Header("통찰 획득 — 보상")]
        [Tooltip("모험 대성공 시 획득하는 통찰량")]
        public int greatSuccessInsightReward = 1;
        [Tooltip("평판 단계 돌파 시 통찰 보너스. 인덱스는 새 등급(ReputationLevel). [0]Bronze는 미사용.")]
        public int[] reputationLevelUpInsightReward = new int[5];
    }
}
