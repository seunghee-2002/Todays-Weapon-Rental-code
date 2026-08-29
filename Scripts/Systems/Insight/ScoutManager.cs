// Scripts/Systems/ScoutManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    [Serializable]
    public class ScoutMission
    {
        public string dungeonStaticID;
        public int startTimeMinutes; // TimeManager.CurrentTime 기준
        public int durationMinutes;
        public int cost;             // 파견 시 지불한 비용 (환불용)
        public ArmorType armorType;  // JsonUtility는 enum을 정수로 직렬화 - 기존 int 저장과 wire 호환
        public bool isComplete;      // 완료 즉시 방어타입 공개 (별도 확인 단계 없음)
    }

    public class ScoutManager : BaseManager<ScoutManager>
    {
        /// <summary>수색 완료 시 발생 - 의뢰판 슬롯 갱신용</summary>
        public event Action<string, ArmorType> OnScoutComplete;

        // GameData.scoutMissions를 그대로 참조 - 직접 mutation/조회. SerializeField는 expression-bodied property에 무효라 제거.
        private List<ScoutMission> missions => GameManager.Instance.GameData.scoutMissions;

        // 던전 ID → mission 인덱스 캐시 (런타임 전용, 직렬화 안 함).
        // invariant: 던전 1개당 missions에 최대 1개의 ScoutMission. CanSendScout가 보장.
        private readonly Dictionary<string, ScoutMission> byDungeon = new Dictionary<string, ScoutMission>();

        #region 초기화

        public void Initialize(GameData gameData)
        {
            gameData.scoutMissions ??= new List<ScoutMission>();
            RebuildByDungeonIndex();
            Log.Info($"[ScoutManager] Initialized. 진행 중: {gameData.scoutMissions.Count(m => !m.isComplete)}개");
        }

        private void RebuildByDungeonIndex()
        {
            byDungeon.Clear();
            foreach (var m in missions)
                byDungeon[m.dungeonStaticID] = m;
        }

        private void Start()
        {
            SubscribeEvents();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged += OnTimeChanged;
                TimeManager.Instance.OnDayChanged   += OnNewDay;
            }
        }

        private void UnsubscribeEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged -= OnTimeChanged;
                TimeManager.Instance.OnDayChanged   -= OnNewDay;
            }
        }

        public void SaveToGameData(GameData gameData)
        {
            // missions는 GameData.scoutMissions를 직접 참조하므로 별도 처리 불필요
        }

        #endregion

        #region View로부터 호출되는 메서드

        public int GetScoutCost(DungeonData dungeon)
        {
            var scoutCosts = GameManager.Instance.GameData.dailyQuestBoardData?.scoutCosts;
            if (scoutCosts != null && scoutCosts.TryGetValue(dungeon.StaticID, out int stored))
                return stored;

            return CalcScoutCost(dungeon);
        }

        private int CalcScoutCost(DungeonData dungeon)
        {
            // 1~7단계 수색 비용은 전령(본부)이 보전 — 튜토리얼 중엔 0원.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                return 0;

            // 등급 비례. 수색은 이미 등급 연동이라 주차 계단을 걸지 않는다
            // (골드_경제_구조.md 4장 "고정으로 둘 것" — 총액은 가격이 아니라 의뢰판 크기 상한이 정한다)
            var cfg = ConfigManager.Instance.Insight;
            int gradeIndex = Mathf.Clamp((int)dungeon.grade, 0, cfg.scoutGradeMultipliers.Length - 1);
            float multiplier = cfg.scoutGradeMultipliers[gradeIndex];
            return Mathf.RoundToInt(cfg.scoutBaseCost * multiplier);
        }

        public bool CanSendScout(DungeonData dungeon)
            => !IsScoutOngoing(dungeon.StaticID)
            && !IsArmorTypeKnown(dungeon.StaticID);

        public void SendScout(DungeonData dungeon)
        {
            if (!CanSendScout(dungeon))
            {
                Log.Warn("[ScoutManager] 수색꾼 파견 조건 불충족");
                return;
            }

            int cost     = GetScoutCost(dungeon);
            int duration = GetScoutDuration(dungeon);

            // 차감 실패 시 임무 생성 없이 중단
            if (!EconomyManager.Instance.SpendGold(cost, "수색꾼 파견"))
            {
                Log.Warn("[ScoutManager] SendScout: 골드 부족");
                return;
            }

            ArmorType result = QuestBoardManager.Instance.GetTodayArmorType(dungeon.StaticID);

            var mission = new ScoutMission
            {
                dungeonStaticID  = dungeon.StaticID,
                startTimeMinutes = TimeManager.Instance.CurrentTime,
                durationMinutes  = duration,
                cost             = cost,
                armorType        = result,
                isComplete       = false
            };
            missions.Add(mission);
            byDungeon[dungeon.StaticID] = mission;

            AnalyticsManager.Instance?.Send("scout_sent", new Dictionary<string, object>
            {
                { "dungeon_id", dungeon.StaticID },
                { "armor_type", result.ToString() }
            });

            Log.Info($"[ScoutManager] 수색꾼 파견 - {dungeon.StaticID}, 소요: {duration}분, 비용: {cost}G");
        }

        /// <summary>
        /// 튜토리얼용 — 지정 던전의 수색을 즉시 완료 처리한다(진행 중이면 완료로 전환, 없으면 완료 상태로 생성).
        /// 결과 armorType은 GetTodayArmorType(튜토리얼 고정 롤). 시간이 정지된 튜토리얼에서 6단계 전 수색 도착을 보장한다.
        /// </summary>
        public void CompleteScoutImmediate(DungeonData dungeon)
        {
            if (dungeon == null || IsArmorTypeKnown(dungeon.StaticID)) return;

            ArmorType armorType = QuestBoardManager.Instance.GetTodayArmorType(dungeon.StaticID);

            if (byDungeon.TryGetValue(dungeon.StaticID, out var mission))
            {
                mission.armorType  = armorType;
                mission.isComplete = true;
            }
            else
            {
                mission = new ScoutMission
                {
                    dungeonStaticID  = dungeon.StaticID,
                    startTimeMinutes = TimeManager.Instance.CurrentTime,
                    durationMinutes  = 0,
                    cost             = 0,
                    armorType        = armorType,
                    isComplete       = true
                };
                missions.Add(mission);
                byDungeon[dungeon.StaticID] = mission;
            }

            OnScoutComplete?.Invoke(dungeon.StaticID, armorType);
            Log.Info($"[ScoutManager] 수색 즉시 완료 - {dungeon.StaticID}: {armorType}");
        }

        // ── 상태 조회 ──────────────────────────────────────────────

        public bool IsScoutOngoing(string dungeonStaticID)
            => byDungeon.TryGetValue(dungeonStaticID, out var m) && !m.isComplete;

        /// <summary>완료된 수색 결과가 있으면 true (ArmorType 표시 가능)</summary>
        public bool IsArmorTypeKnown(string dungeonStaticID)
            => byDungeon.TryGetValue(dungeonStaticID, out var m) && m.isComplete;

        public ArmorType GetKnownArmorType(string dungeonStaticID)
        {
            if (byDungeon.TryGetValue(dungeonStaticID, out var m) && m.isComplete)
                return m.armorType;
            return ArmorType.Unarmored;
        }

        #endregion

        #region 이벤트 핸들러

        private void OnTimeChanged(int hour, int minute)
        {
            CheckMissionCompletion();
        }

        public void OnNewDay(int day)
        {
            int totalRefund = missions.Where(m => !m.isComplete).Sum(m => m.cost);
            if (totalRefund > 0)
            {
                EconomyManager.Instance.AddGold(totalRefund, "수색꾼 환불");
                Log.Info($"[ScoutManager] 날짜 변경 - 미완료 임무 환불 ({totalRefund}G)");
            }

            missions.Clear();
            byDungeon.Clear();
            Log.Info($"[ScoutManager] 날짜 변경 - 수색 임무 초기화 (Day {day})");
        }

        #endregion

        #region 내부 메서드

        private void CheckMissionCompletion()
        {
            int currentTime = TimeManager.Instance.CurrentTime;

            foreach (var mission in missions)
            {
                if (!mission.isComplete && currentTime >= mission.startTimeMinutes + mission.durationMinutes)
                {
                    mission.isComplete = true;
                    OnScoutComplete?.Invoke(mission.dungeonStaticID, mission.armorType);
                    ShowCompletionToast(mission);
                    Log.Info($"[ScoutManager] 수색 완료 - {mission.dungeonStaticID}: {mission.armorType}");
                }
            }
        }

        private void ShowCompletionToast(ScoutMission mission)
        {
            var dungeon = DataManager.Instance.GetDungeon(mission.dungeonStaticID);
            string dungeonName = dungeon != null ? dungeon.DisplayName : mission.dungeonStaticID;
            UIPopupController.Instance?.ShowToast(LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "Scout_Completed",
                    arguments: new object[] { new System.Collections.Generic.Dictionary<string, object> {
                        { "dungeon", dungeonName },
                        { "armor", UITranslator.GetString(mission.armorType) } } }));
        }

        /// <summary>
        /// 수색꾼 파견 소요 시간 (게임 내 분).
        /// 의뢰판 확정 시 사전 계산된 값을 반환하며, 없으면 즉석 계산한다.
        /// </summary>
        public int GetScoutDuration(DungeonData dungeon)
        {
            var scoutDurations = GameManager.Instance.GameData.dailyQuestBoardData?.scoutDurations;
            if (scoutDurations != null && scoutDurations.TryGetValue(dungeon.StaticID, out int stored))
                return stored;

            return CalcScoutDuration(dungeon);
        }

        private int CalcScoutDuration(DungeonData dungeon)
        {
            var cfg     = ConfigManager.Instance.Insight;
            int insight = InsightManager.Instance.CurrentInsight;

            int baseDuration;
            if (insight >= cfg.scoutDurationTier75Threshold)      baseDuration = cfg.scoutBaseDurationAt75;
            else if (insight >= cfg.scoutDurationTier50Threshold) baseDuration = cfg.scoutBaseDurationAt50;
            else if (insight >= cfg.scoutDurationTier25Threshold) baseDuration = cfg.scoutBaseDurationAt25;
            else                                                  baseDuration = cfg.scoutBaseDurationDefault;

            int gradeIndex = Mathf.Clamp((int)dungeon.grade, 0, cfg.scoutGradeMultipliers.Length - 1);
            float final = baseDuration * cfg.scoutGradeMultipliers[gradeIndex]
                          * UnityEngine.Random.Range(cfg.scoutRandomMin, cfg.scoutRandomMax);
            return Mathf.Max(1, Mathf.RoundToInt(final));
        }

        /// <summary>
        /// 의뢰판 확정 시 호출 - 선택된 던전들의 수색 비용/시간을 미리 계산해 저장한다.
        /// </summary>
        public void PrerollScoutValues(List<DungeonData> dungeons)
        {
            var saveData = GameManager.Instance.GameData.dailyQuestBoardData;
            if (saveData == null) return;

            saveData.scoutCosts.Clear();
            saveData.scoutDurations.Clear();

            foreach (var dungeon in dungeons)
            {
                saveData.scoutCosts[dungeon.StaticID]     = CalcScoutCost(dungeon);
                saveData.scoutDurations[dungeon.StaticID] = CalcScoutDuration(dungeon);
            }

            Log.Info($"[ScoutManager] 수색 비용/시간 사전 확정 - {dungeons.Count}개 던전");
        }

        #endregion

#if UNITY_EDITOR
        #region 디버그

        /// <summary>
        /// 비용 없이 모든 던전을 즉시 파견 완료 처리 (View 없이 armorType 노출)
        /// </summary>
        public void DebugInstantScoutAll(List<DungeonData> dungeons)
        {
            foreach (var dungeon in dungeons)
            {
                if (IsArmorTypeKnown(dungeon.StaticID) || IsScoutOngoing(dungeon.StaticID)) continue;

                ArmorType armorType = QuestBoardManager.Instance.GetTodayArmorType(dungeon.StaticID);
                var mission = new ScoutMission
                {
                    dungeonStaticID  = dungeon.StaticID,
                    startTimeMinutes = TimeManager.Instance.CurrentTime,
                    durationMinutes  = 0,
                    cost             = 0,
                    armorType        = armorType,
                    isComplete       = true
                };
                missions.Add(mission);
                byDungeon[dungeon.StaticID] = mission;

                Log.Info($"[ScoutManager] 디버그: {dungeon.dungeonName} 즉시 파견 완료 - {armorType}");
            }
        }

        #endregion
#endif
    }
}
