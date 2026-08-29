// Assets/_Projects/Scripts/Systems/ReputationManager.cs
using System;
using UnityEngine;

namespace TodaysWeaponRental
{
    public class ReputationManager : BaseManager<ReputationManager>
    {
        // 이벤트
        public event Action<int> OnReputationChanged;
        public event Action<ReputationLevel> OnReputationLevelChanged;

        // 프로퍼티
        public int CurrentReputation { get; private set; }
        public ReputationLevel CurrentLevel { get; private set; }

        private ReputationConfig Config => ConfigManager.Instance.Reputation;
        private GameData gameData;

        public void Initialize(GameData data)
        {
            gameData = data;
            CurrentReputation = data.reputation;
            CurrentLevel = CalculateReputationLevel(CurrentReputation);
        }

        /// <summary>
        /// 평판 추가/감소. 평판은 0 아래로 내려가지 않는다 —
        /// 마이너스 구간은 Bronze와 체감이 같으면서 복구만 불가능하게 만든다.
        /// </summary>
        public void AddReputation(int amount, string reason)
        {
            int oldReputation = CurrentReputation;
            ReputationLevel oldLevel = CurrentLevel;

            CurrentReputation = Mathf.Max(0, CurrentReputation + amount);
            gameData.reputation = CurrentReputation;

            if (amount > 0)
                gameData.totalCumulativeReputation += amount;

            // 평판 변경 이벤트 발행
            OnReputationChanged?.Invoke(CurrentReputation);

            // 레벨 변경 체크
            ReputationLevel newLevel = CalculateReputationLevel(CurrentReputation);
            if (newLevel != oldLevel)
            {
                CurrentLevel = newLevel;
                OnReputationLevelChanged?.Invoke(newLevel);
                Log.Info($"평판 레벨 변경: {oldLevel} → {newLevel}");
            }

            Log.Info($"평판 변경: {oldReputation} → {CurrentReputation} ({amount:+#;-#;0}) - {reason}");
        }

        #region 헬퍼 메서드

        /// <summary>
        /// 평판 레벨 계산
        /// </summary>
        private ReputationLevel CalculateReputationLevel(int reputation)
        {
            if (reputation >= Config.diamondThreshold) return ReputationLevel.Diamond;
            if (reputation >= Config.platinumThreshold) return ReputationLevel.Platinum;
            if (reputation >= Config.goldThreshold) return ReputationLevel.Gold;
            if (reputation >= Config.silverThreshold) return ReputationLevel.Silver;
            return ReputationLevel.Bronze;
        }

        /// <summary>
        /// 모험가 스폰 간격 반환 (VisitorManager에서 사용)
        /// </summary>
        public (float min, float max) GetAdventurerSpawnInterval()
        {
            return CurrentLevel switch
            {
                ReputationLevel.Bronze => (Config.bronzeSpawnInterval.x, Config.bronzeSpawnInterval.y),
                ReputationLevel.Silver => (Config.silverSpawnInterval.x, Config.silverSpawnInterval.y),
                ReputationLevel.Gold => (Config.goldSpawnInterval.x, Config.goldSpawnInterval.y),
                ReputationLevel.Platinum => (Config.platinumSpawnInterval.x, Config.platinumSpawnInterval.y),
                ReputationLevel.Diamond => (Config.diamondSpawnInterval.x, Config.diamondSpawnInterval.y),
                _ => (Config.silverSpawnInterval.x, Config.silverSpawnInterval.y)
            };
        }

        /// <summary>
        /// 이벤트 발생 확률 반환 (VisitorManager에서 사용)
        /// </summary>
        public float GetEventChance()
        {
            return CurrentLevel switch
            {
                ReputationLevel.Bronze => Config.bronzeEventChance,
                ReputationLevel.Silver => Config.silverEventChance,
                ReputationLevel.Gold => Config.goldEventChance,
                ReputationLevel.Platinum => Config.platinumEventChance,
                ReputationLevel.Diamond => Config.diamondEventChance,
                _ => Config.silverEventChance
            };
        }

        /// <summary>
        /// 다음 레벨 임계값 (Diamond일 경우 자기 자신 반환 — 만렙)
        /// </summary>
        public int GetNextLevelThreshold()
        {
            return CurrentLevel switch
            {
                ReputationLevel.Bronze => Config.silverThreshold,
                ReputationLevel.Silver => Config.goldThreshold,
                ReputationLevel.Gold => Config.platinumThreshold,
                ReputationLevel.Platinum => Config.diamondThreshold,
                ReputationLevel.Diamond => Config.diamondThreshold,
                _ => Config.silverThreshold
            };
        }

        #endregion
    }
}