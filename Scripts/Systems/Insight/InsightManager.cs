using UnityEngine;

namespace TodaysWeaponRental
{
    public partial class InsightManager : BaseManager<InsightManager>
    {
        public int CurrentInsight => GameManager.Instance.GameData.playerInsight;

        #region 초기화

        public void Initialize(GameData gameData)
        {
            Log.Info($"[InsightManager] Initialized. Current insight: {gameData.playerInsight}");
        }

        private void Start()
        {
            if (ReputationManager.Instance != null)
                ReputationManager.Instance.OnReputationLevelChanged += OnReputationLevelUp;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (ReputationManager.Instance != null)
                ReputationManager.Instance.OnReputationLevelChanged -= OnReputationLevelUp;
        }

        private void OnReputationLevelUp(ReputationLevel newLevel)
        {
            var rewards = ConfigManager.Instance.Insight.reputationLevelUpInsightReward;
            int index = (int)newLevel;
            if (rewards == null || index < 0 || index >= rewards.Length) return;

            int reward = rewards[index];
            if (reward <= 0) return;

            AddInsight(reward);
            Log.Info($"[InsightManager] 평판 단계 돌파 통찰 +{reward} ({newLevel})");
        }

        #endregion

        #region 통찰 변경

        public void AddInsight(int amount)
        {
            if (amount == 0) return;

            var gameData = GameManager.Instance.GameData;
            int newValue = Mathf.Clamp(gameData.playerInsight + amount, 0, 100);

            if (newValue == gameData.playerInsight) return;

            gameData.playerInsight = newValue;
            Log.Info($"[InsightManager] Insight changed: {newValue} (+{amount})");
        }

        #endregion

        #region 공개 범위 판단 — 일반 모험가

        /// <summary>통찰에 따라 일반 모험가에게 공개할 스탯 수 (0~2)</summary>
        public int GetNormalAdventurerRevealCount()
        {
            var cfg = ConfigManager.Instance.Insight;
            int insight = CurrentInsight;
            if (insight >= cfg.normalReveal2StatThreshold) return 2;
            if (insight >= cfg.normalReveal1StatThreshold) return 1;
            return 0;
        }

        /// <summary>통찰 임계값 이상이면 일반 모험가 최고 스탯 종류+수치 공개</summary>
        public bool CanRevealNormalAdventurerHighestStat()
        {
            return CurrentInsight >= ConfigManager.Instance.Insight.normalRevealHighestStatThreshold;
        }

        #endregion

        #region 2차 대화 공개

        /// <summary>
        /// 대화 행동을 시작할 수 있는지 검사.
        /// 소요 시간이 21:00까지 남은 시간 미만이고 InsightConfig.maxTalkDurationMinutes 이내인 경우만 허용.
        /// 정확히 21:00에 닿는 선택은 막는다 - 상호작용 중에는 시간이 정지 상태라
        /// 하루 종료 팝업 조건(!isTimePaused)이 만족되지 않아 조용히 마감돼 버린다
        /// </summary>
        public bool CanStartTalkAction(int timeCostMinutes)
        {
            int remaining = TimeManager.Instance.GetRemainingMinutesUntilDayEnd();
            int maxDuration = ConfigManager.Instance.Insight.maxTalkDurationMinutes;
            return timeCostMinutes < remaining && timeCostMinutes <= maxDuration;
        }

        /// <summary>개별 스탯 대화 시도 소요 시간 (게임 내 분)</summary>
        public int GetStatTalkTimeCost()
        {
            var cfg = ConfigManager.Instance.Insight;
            int insight = CurrentInsight;
            if (insight >= cfg.normalRevealHighestStatThreshold) return cfg.statTalkCostAt70;
            if (insight >= cfg.normalReveal2StatThreshold) return cfg.statTalkCostAt50;
            if (insight >= cfg.normalRevealAverageThreshold) return cfg.statTalkCostAt30;
            return cfg.statTalkCostDefault;
        }

        /// <summary>전체 스탯 대화 시도 소요 시간 (게임 내 분)</summary>
        public int GetAllStatTalkTimeCost()
        {
            return GetStatTalkTimeCost() * 2;
        }

        /// <summary>개별 스탯 밝히기 성공 확률 (0~1)</summary>
        public float GetStatRevealSuccessRate(AdventurerInstance adventurer, AdventurerStat targetStat)
        {
            if (adventurer == null) return 0f;

            var cfg = ConfigManager.Instance.Insight;
            int insight = CurrentInsight;
            float baseProbability = cfg.statRevealBaseChance + insight * cfg.statRevealInsightBonus;
            float penalty = (cfg.statRevealPenaltyBase - insight * cfg.statRevealPenaltyInsightFactor)
                            * adventurer.revealedStatIndices.Count;
            float bonus = GetIntTalkBonus(adventurer, targetStat);
            return Mathf.Clamp01((baseProbability - penalty + bonus) / 100f) * GetNamedChanceMultiplier(adventurer.isNamed);
        }

        /// <summary>전체 스탯 한번에 밝히기 성공 확률 (0~1)</summary>
        public float GetAllStatRevealSuccessRate(AdventurerInstance adventurer)
        {
            // 튜토리얼 중 종합 테스트는 성공을 보장하므로(RevealAllStats) 확률도 100%로 일치시킨다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                return 1f;
            if (adventurer == null) return 0f;

            var cfg = ConfigManager.Instance.Insight;
            int insight = CurrentInsight;
            float baseProbability = cfg.allStatRevealBaseChance + insight * cfg.allStatRevealInsightBonus;
            float penalty = (cfg.statRevealPenaltyBase - insight * cfg.statRevealPenaltyInsightFactor)
                            * adventurer.revealedStatIndices.Count;
            float bonus = GetIntTalkBonus(adventurer);
            return Mathf.Clamp01((baseProbability - penalty * cfg.allStatPenaltyMultiplier + bonus) / 100f);
        }

        /// <summary>특성 밝히기 성공 확률 (0~1)</summary>
        public float GetTraitRevealSuccessRate(AdventurerInstance adventurer)
        {
            if (adventurer == null) return 0f;

            var cfg = ConfigManager.Instance.Insight;
            int insight = CurrentInsight;
            float bonus = GetIntTalkBonus(adventurer);
            return Mathf.Clamp01((cfg.traitRevealBaseChance + insight * cfg.traitRevealInsightBonus + bonus) / 100f)
                   * GetNamedChanceMultiplier(adventurer.isNamed);
        }

        /// <summary>
        /// INT가 이미 공개된 모험가는 말이 통해 테스트가 수월해진다 (성공률 %p 가산).
        /// INT 테스트 자신에는 걸지 않는다 — 모르는 값이 그 값을 알아낼 확률을 정하면
        /// 확인 패널에 표시되는 확률이 거짓이 되거나 INT가 역추론된다.
        /// </summary>
        private float GetIntTalkBonus(AdventurerInstance adventurer, AdventurerStat? targetStat = null)
        {
            if (targetStat == AdventurerStat.INT) return 0f;
            if (!GetStatVisibility(adventurer).IsVisible(AdventurerStat.INT)) return 0f;

            var cfg = ConfigManager.Instance.Insight;
            return StatCurve.Evaluate(adventurer.INT, cfg.intRevealBonusMax, cfg.intRevealBonusExponent);
        }

        /// <summary>네임드는 정보가 영구 보존되므로 최종 확률에 배율을 건다. 튜토리얼 중에는 우회.</summary>
        private float GetNamedChanceMultiplier(bool isNamed)
        {
            if (!isNamed || IsNamedRestrictionBypassed) return 1f;
            return ConfigManager.Instance.Insight.namedRevealChanceMultiplier;
        }

        /// <summary>기초 무기 타입 힌트 해금 여부</summary>
        public bool CanRevealWeaponTypeHint()
        {
            return CurrentInsight >= ConfigManager.Instance.Insight.weaponHintRequiredInsight;
        }

        #endregion

        #region 네임드 제약

        /// <summary>튜토리얼 중에는 네임드 제약(확률 배율/하루 1회/종합 테스트 차단)을 전부 우회한다.</summary>
        private bool IsNamedRestrictionBypassed
            => TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;

        /// <summary>네임드는 테스트 종류마다 하루 1회만 일반 판정을 시도할 수 있다. 일반 모험가는 제한 없음.</summary>
        public bool CanTalkTestToday(AdventurerInstance adventurer, TalkTestType test)
        {
            if (adventurer == null) return false;
            if (!adventurer.isNamed || IsNamedRestrictionBypassed) return true;
            return adventurer.lastTalkDayByTest[(int)test] != TimeManager.Instance.CurrentDay;
        }

        /// <summary>일반 판정 실행 시 호출 — 네임드만 오늘 날짜를 기록한다.</summary>
        public void MarkTalkTestAttempted(AdventurerInstance adventurer, TalkTestType test)
        {
            if (adventurer == null || !adventurer.isNamed) return;
            adventurer.lastTalkDayByTest[(int)test] = TimeManager.Instance.CurrentDay;
        }

        /// <summary>네임드는 종합 테스트를 할 수 없다. 프리미엄으로도 우회 불가.</summary>
        public bool CanRevealAllStats(AdventurerInstance adventurer)
        {
            if (adventurer == null) return false;
            return !adventurer.isNamed || IsNamedRestrictionBypassed;
        }

        #endregion

        #region 던전 정보 공개

        /// <summary>예상 소요 시간 공개 여부. 임계값은 InsightConfig.estimatedDurationRevealThreshold.</summary>
        public bool CanRevealEstimatedDuration()
            => CurrentInsight >= ConfigManager.Instance.Insight.estimatedDurationRevealThreshold;

        #endregion

        #region 암시장 효과 공개

        /// <summary>
        /// 암시장 무기의 부가효과 공개 개수. 통찰 step당 1개씩 공개. totalEffects 상한으로 클램프.
        /// </summary>
        public int GetBlackMarketRevealCount(int totalEffects)
        {
            if (totalEffects <= 0) return 0;
            int step = ConfigManager.Instance.Insight.blackMarketEffectRevealStep;
            if (step <= 0) return 0;
            return Mathf.Min(CurrentInsight / step, totalEffects);
        }

        #endregion
    }
}
