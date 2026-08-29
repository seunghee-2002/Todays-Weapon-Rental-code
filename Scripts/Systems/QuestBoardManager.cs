// Scripts/Systems/QuestBoardManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 매일 낮(9시) 의뢰판 생성 및 관리
    /// </summary>
    public class QuestBoardManager : BaseManager<QuestBoardManager>
    {
        private QuestBoardConfig Config => ConfigManager.Instance.QuestBoard;

        // 이벤트
        public event Action<List<DungeonData>> OnBoardGenerated;               // 의뢰판 생성/새로고침 완료 (풀 던전 목록)
        public event Action<List<DungeonData>, List<DungeonData>> OnBoardConfirmed;

        // 프로퍼티
        public bool IsConfirmed            => GetSaveData().isConfirmed;
        public bool IsTodayGenerated       => GetSaveData().generatedDay == TimeManager.Instance.CurrentDay;
        public int RefreshCount            => GetSaveData().refreshCount;
        public int MaxRefreshCount         => Config.maxRefreshCount;
        public bool CanRefresh             => GetSaveData().refreshCount < Config.maxRefreshCount && !IsConfirmed && !IsTutorialActive;

        // 튜토리얼 4단계(의뢰판) 고정 매물 장치 — 매니저 내부 가드(다른 매니저들과 동일한 컨벤션).
        private bool IsTutorialActive => TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;

        #region 초기화

        public void Initialize(GameData gameData)
        {
            LoadFromGameData(gameData);
        }

        private void Start()
        {
            SubscribeEvents();

            // Start 시점 페이즈 체크 (로드 재진입 대응)
            if (TimeManager.Instance != null &&
                TimeManager.Instance.CurrentPhase == TimePhase.Day)
            {
                TryGenerateDailyBoard(TimeManager.Instance.CurrentDay);
            }
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
                TimeManager.Instance.OnPhaseChanged += OnPhaseChanged;
                TimeManager.Instance.OnDayChanged   += OnNewDayStarted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPhaseChanged -= OnPhaseChanged;
                TimeManager.Instance.OnDayChanged   -= OnNewDayStarted;
            }
        }

        #endregion

        #region 페이즈 / 날짜 감지

        private void OnPhaseChanged(TimePhase newPhase)
        {
            if (newPhase == TimePhase.Day)
                TryGenerateDailyBoard(TimeManager.Instance.CurrentDay);
        }

        private void OnNewDayStarted(int day)
        {
            GameManager.Instance.GameData.dailyQuestBoardData = new QuestBoardSaveData();
            Log.Info($"[QuestBoardManager] 의뢰판 초기화 — Day {day}");
        }

        #endregion

        #region 의뢰판 생성

        private void TryGenerateDailyBoard(int day)
        {
            if (!IsTodayGenerated)
            {
                GenerateDailyBoard(day);
                return;
            }

            // 오늘 이미 생성됐지만 미확정이면 UI 재오픈 — Phase 1으로 복원
            if (!IsConfirmed)
            {
                Log.Info("[QuestBoardManager] 오늘 의뢰판 미확정 — UI 재오픈");
                UIManager.Instance?.OpenPanel<QuestBoardView>();
                OnBoardGenerated?.Invoke(GetPoolDungeons());
                return;
            }

            // 확정 완료 상태 — Phase 2(수색)로 복원
            // 사용자가 한 번이라도 닫기를 눌렀다면 당일 재오픈하지 않는다.
            // 파견 가능한 던전이 하나도 없으면 열지 않는다.
            // QuestBoardController는 OnEnable의 SyncCurrentState로 Phase 2 UI를 복원함.
            var saveData = GetSaveData();
            if (!saveData.scoutPhaseClosedByUser &&
                GetAvailableDungeons().Any(d => ScoutManager.Instance.CanSendScout(d)))
            {
                UIManager.Instance?.OpenPanel<QuestBoardView>();
            }
        }

        private void GenerateDailyBoard(int day)
        {
            List<DungeonData> selectedPool;
            if (IsTutorialActive)
            {
                selectedPool = GetTutorialFixedPool();
            }
            else
            {
                var (poolSize, _) = GetBoardConfig(ReputationManager.Instance.CurrentLevel);
                var allDungeons   = DataManager.Instance.GetAllDungeons();
                selectedPool      = SelectDungeonsWeighted(allDungeons, poolSize);
            }

            GameManager.Instance.GameData.dailyQuestBoardData = new QuestBoardSaveData
            {
                generatedDay          = day,
                poolDungeonIDs        = selectedPool.Select(d => d.StaticID).ToList(),
                selectedDungeonIDs    = new List<string>(),
                highlightedDungeonIDs = new List<string>(),
                isConfirmed           = false,
                refreshCount          = 0
            };

            RollDailyArmorTypes(selectedPool);

            Log.Info($"[QuestBoardManager] 의뢰판 생성 완료 — Day {day}, 주차 {GameManager.Instance.GameData.currentWeek}, 풀 {selectedPool.Count}개");
            UIManager.Instance?.OpenPanel<QuestBoardView>();
            OnBoardGenerated?.Invoke(selectedPool);
        }

        /// <summary>
        /// 새로고침 — 풀만 교체, 선택 초기화, refreshCount 증가
        /// </summary>
        public bool RefreshBoard()
        {
            if (!CanRefresh)
            {
                Log.Warn($"[QuestBoardManager] 새로고침 불가 — 횟수: {RefreshCount}/{Config.maxRefreshCount}, 확정: {IsConfirmed}");
                return false;
            }

            int cost = GetRefreshCost();
            if (!EconomyManager.Instance.SpendGold(cost, "의뢰판 새로고침"))
            {
                Log.Warn($"[QuestBoardManager] 골드 부족 — 필요: {cost}G");
                return false;
            }

            var (poolSize, _) = GetBoardConfig(ReputationManager.Instance.CurrentLevel);
            var allDungeons   = DataManager.Instance.GetAllDungeons();
            var newPool       = SelectDungeonsWeighted(allDungeons, poolSize);

            var saveData = GetSaveData();
            saveData.poolDungeonIDs       = newPool.Select(d => d.StaticID).ToList();
            saveData.selectedDungeonIDs   = new List<string>();
            saveData.highlightedDungeonIDs = new List<string>();
            saveData.refreshCount++;

            Log.Info($"[QuestBoardManager] 새로고침 완료 — {cost}G 소모, 횟수: {saveData.refreshCount}/{Config.maxRefreshCount}");
            OnBoardGenerated?.Invoke(newPool);
            return true;
        }

        /// <summary>튜토리얼 4단계 고정 매물 — TutorialConfig에 등록된 A/B/C 던전으로 풀을 대체한다.</summary>
        private List<DungeonData> GetTutorialFixedPool()
        {
            var tutorialConfig = ConfigManager.Instance.Tutorial;
            var ids = new[]
            {
                tutorialConfig.TutorialDungeonAID,
                tutorialConfig.TutorialDungeonBID,
                tutorialConfig.TutorialDungeonCID
            };

            var pool = ids
                .Select(id => DataManager.Instance.GetDungeon(id))
                .Where(d => d != null)
                .ToList();

            if (pool.Count != ids.Length)
                Log.Warn("[QuestBoardManager] 튜토리얼 고정 던전 ID 중 일부를 찾지 못했습니다. TutorialConfig.asset을 확인하세요.");

            return pool;
        }

        /// <summary>오늘자 의뢰판이 아직 없으면 강제 생성한다(튜토리얼 디버그 단계 점프 지원용).</summary>
        public void EnsureTodayBoardGenerated()
        {
            if (!IsTodayGenerated)
                GenerateDailyBoard(TimeManager.Instance.CurrentDay);
        }

        /// <summary>
        /// 유효 가중치 = questWeight x 주차별 등급 배율 (계수 문서 §4-2).
        /// 후반으로 갈수록 저등급 던전 비율이 줄어 희귀+ 던전 목표가 성립한다.
        /// </summary>
        private float GetEffectiveWeight(DungeonData dungeon, int week)
        {
            return dungeon.questWeight * Config.GetQuestWeightMultiplier(dungeon.grade, week);
        }

        private List<DungeonData> SelectDungeonsWeighted(List<DungeonData> all, int count)
        {
            int week      = GameManager.Instance.GameData.currentWeek;
            var result    = new List<DungeonData>();
            var remaining = all.Where(d => d.questWeight > 0).ToList();

            // questWeight > 0 던전이 하나도 없으면(데이터 입력 오류) 전체 던전 균등 가중으로 폴백한다.
            // 풀이 0개면 Phase 1이 닫기 불가 흐름이라 진행 불가가 된다
            if (remaining.Count == 0 && all.Count > 0)
            {
                Log.Error("[QuestBoardManager] questWeight > 0 던전 없음 - 전체 던전 균등 폴백 (DungeonData 확인 필요)");
                remaining = new List<DungeonData>(all);

                // 균등 추첨 (questWeight를 쓸 수 없으므로 인덱스 추첨)
                int pickCount = Mathf.Min(count, remaining.Count);
                for (int i = 0; i < pickCount; i++)
                {
                    var picked = remaining[Random.Range(0, remaining.Count)];
                    result.Add(picked);
                    remaining.Remove(picked);
                }
                return result;
            }

            count = Mathf.Min(count, remaining.Count);

            for (int i = 0; i < count; i++)
            {
                float total      = remaining.Sum(d => GetEffectiveWeight(d, week));
                float rand       = Random.Range(0f, total);
                float cumulative = 0f;

                foreach (var dungeon in remaining)
                {
                    cumulative += GetEffectiveWeight(dungeon, week);
                    // 경계 미포함(<) - rand가 정확히 0일 때 weight 0 후보 당첨 방지
                    if (rand < cumulative)
                    {
                        result.Add(dungeon);
                        remaining.Remove(dungeon);
                        break;
                    }
                }
            }

            return result;
        }

        #endregion

        #region ArmorType 롤링

        private void RollDailyArmorTypes(List<DungeonData> poolDungeons)
        {
            var saveData = GetSaveData();
            saveData.dailyDungeonArmorTypes.Clear();

            var tutorialConfig = IsTutorialActive ? ConfigManager.Instance.Tutorial : null;

            foreach (var dungeon in poolDungeons)
            {
                ArmorType rolled;
                if (tutorialConfig != null && dungeon.StaticID == tutorialConfig.TutorialDungeonAID)
                    rolled = ArmorType.MagicalArmor;
                else if (tutorialConfig != null && dungeon.StaticID == tutorialConfig.TutorialDungeonBID)
                    rolled = ArmorType.HeavyArmor;
                else
                    rolled = RollArmorType(dungeon);

                saveData.dailyDungeonArmorTypes[dungeon.StaticID] = (int)rolled;
            }
        }

        private ArmorType RollArmorType(DungeonData dungeon)
        {
            if (dungeon.armorTypeVariants == null || dungeon.armorTypeVariants.Count == 0)
                return dungeon.armorType;

            float roll       = Random.value;
            float cumulative = 0f;
            foreach (var variant in dungeon.armorTypeVariants)
            {
                cumulative += variant.weight;
                // 경계 미포함(<) - weight 0 variant 당첨 방지
                if (roll < cumulative) return variant.armorType;
            }
            return dungeon.armorType; // fallback
        }

        /// <summary>
        /// 오늘 해당 던전에 롤링된 방어구 타입을 반환. 수색꾼 조회 등 외부에서 사용.
        /// </summary>
        public ArmorType GetTodayArmorType(string dungeonStaticID)
        {
            var saveData = GetSaveData();
            if (saveData.dailyDungeonArmorTypes.TryGetValue(dungeonStaticID, out int armorTypeInt))
                return (ArmorType)armorTypeInt;
            return DataManager.Instance.GetDungeon(dungeonStaticID)?.armorType ?? ArmorType.Unarmored;
        }

        #endregion

        #region 확정

        public bool ConfirmBoard(List<string> selectedIDs)
        {
            if (IsConfirmed)
            {
                Log.Warn("[QuestBoardManager] 이미 확정된 의뢰판입니다.");
                return false;
            }

            if (!ValidateSelection(selectedIDs)) return false;

            ApplyConfirmation(selectedIDs);

            // 선택 던전별로 1건씩 발행 (G16 등급 선택 성향)
            foreach (var id in selectedIDs)
            {
                var dungeon = DataManager.Instance.GetDungeon(id);
                if (dungeon == null) continue;
                AnalyticsManager.Instance?.Send("quest_board_confirmed", new Dictionary<string, object>
                {
                    { "dungeon_id", dungeon.StaticID },
                    { "dungeon_grade", (int)dungeon.grade }
                });
            }

            return true;
        }

        /// <summary>
        /// 실제 요구 선택 수. 풀이 설정값(selectCount)보다 작으면 풀 크기로 낮춘다.
        /// Phase 1은 닫기가 막힌 흐름이라 고정 요구 수를 쓰면 소프트락이 된다
        /// </summary>
        public int GetRequiredSelectionCount()
        {
            var (_, selectCount) = GetBoardConfig(ReputationManager.Instance.CurrentLevel);
            int poolCount = GetSaveData().poolDungeonIDs?.Count ?? 0;
            return Mathf.Min(selectCount, poolCount);
        }

        private bool ValidateSelection(List<string> selectedIDs)
        {
            if (selectedIDs == null || selectedIDs.Count == 0)
            {
                Log.Warn("[QuestBoardManager] 선택된 던전이 없습니다.");
                return false;
            }

            int requiredCount = GetRequiredSelectionCount();
            if (selectedIDs.Count != requiredCount)
            {
                Log.Warn($"[QuestBoardManager] 선택 수 불일치 — 필요: {requiredCount}, 실제: {selectedIDs.Count}");
                return false;
            }

            var poolIDs = GetSaveData().poolDungeonIDs;
            foreach (var id in selectedIDs)
            {
                if (!poolIDs.Contains(id))
                {
                    Log.Warn($"[QuestBoardManager] 풀에 없는 던전 ID: {id}");
                    return false;
                }
            }

            return true;
        }

        private void ApplyConfirmation(List<string> selectedIDs)
        {
            List<string> highlighted;
            if (IsTutorialActive)
            {
                string dungeonCID = ConfigManager.Instance.Tutorial.TutorialDungeonCID;
                highlighted = selectedIDs.Where(id => id == dungeonCID).ToList();
            }
            else
            {
                highlighted = selectedIDs.Where(_ => Random.value < 0.2f).ToList();
            }

            var saveData = GetSaveData();
            saveData.selectedDungeonIDs    = new List<string>(selectedIDs);
            saveData.highlightedDungeonIDs = highlighted;
            saveData.isConfirmed           = true;

            // 수색 비용/시간 사전 확정 — UI가 OnBoardConfirmed를 받기 전에 완료해야 함
            ScoutManager.Instance.PrerollScoutValues(GetAvailableDungeons());

            Log.Info($"[QuestBoardManager] 확정 완료 — {selectedIDs.Count}개, 2배 던전: {highlighted.Count}개");
            OnBoardConfirmed?.Invoke(GetAvailableDungeons(), GetHighlightedDungeons());
            // 패널 전환 없음 — QuestBoardView가 그대로 Phase 2(수색)로 전환된다.
        }

        #endregion

        #region 조회

        public List<DungeonData> GetPoolDungeons()
        {
            var ids = GetSaveData().poolDungeonIDs;
            if (ids == null) return new List<DungeonData>();

            return ids
                .Select(id => DataManager.Instance.GetDungeon(id))
                .Where(d => d != null)
                .ToList();
        }

        public List<DungeonData> GetAvailableDungeons()
        {
            var saveData = GetSaveData();
            if (!saveData.isConfirmed || saveData.selectedDungeonIDs == null)
                return new List<DungeonData>();

            return saveData.selectedDungeonIDs
                .Select(id => DataManager.Instance.GetDungeon(id))
                .Where(d => d != null)
                .ToList();
        }

        public bool IsDungeonAvailableToday(string dungeonStaticID)
        {
            var saveData = GetSaveData();
            if (!saveData.isConfirmed || saveData.selectedDungeonIDs == null) return false;
            return saveData.selectedDungeonIDs.Contains(dungeonStaticID);
        }

        public bool IsHighlightedDungeon(string dungeonStaticID)
        {
            var ids = GetSaveData().highlightedDungeonIDs;
            return ids != null && ids.Contains(dungeonStaticID);
        }

        public List<DungeonData> GetHighlightedDungeons()
        {
            var ids = GetSaveData().highlightedDungeonIDs;
            if (ids == null) return new List<DungeonData>();
            return ids
                .Select(id => DataManager.Instance.GetDungeon(id))
                .Where(d => d != null)
                .ToList();
        }

        /// <summary>
        /// 새로고침 비용 = 기본 비용 + 일차 가산 (수색 비용과 동일 구조)
        /// </summary>
        public int GetRefreshCost()
        {
            // 기본가 x 주차 계단 배율 — 다시 뽑는 풀의 질이 후반일수록 좋아지므로 값도 따라간다
            float mult = ConfigManager.Instance.PriceMult(p => p.boardRefreshCost);
            return Mathf.RoundToInt(Config.refreshBaseCost * mult);
        }

        public (int poolSize, int selectCount) GetBoardConfig(ReputationLevel level)
        {
            return level switch
            {
                ReputationLevel.Bronze   => (Config.bronzePoolSize,   Config.bronzeSelectCount),
                ReputationLevel.Silver   => (Config.silverPoolSize,   Config.silverSelectCount),
                ReputationLevel.Gold     => (Config.goldPoolSize,     Config.goldSelectCount),
                ReputationLevel.Platinum => (Config.platinumPoolSize, Config.platinumSelectCount),
                ReputationLevel.Diamond  => (Config.diamondPoolSize,  Config.diamondSelectCount),
                _                        => (Config.bronzePoolSize,   Config.bronzeSelectCount)
            };
        }

        #endregion

        #region 저장/불러오기

        // 저장은 별도 처리하지 않는다 — GetSaveData()가 항상 GameData 참조를 직접 사용한다.

        private void LoadFromGameData(GameData gameData)
        {
            if (gameData.dailyQuestBoardData == null)
                gameData.dailyQuestBoardData = new QuestBoardSaveData();

            Log.Info($"[QuestBoardManager] 로드 — Day:{gameData.dailyQuestBoardData.generatedDay}, confirmed:{gameData.dailyQuestBoardData.isConfirmed}, refresh:{gameData.dailyQuestBoardData.refreshCount}");
        }

        private QuestBoardSaveData GetSaveData()
        {
            var data = GameManager.Instance.GameData.dailyQuestBoardData;
            if (data == null)
            {
                data = new QuestBoardSaveData();
                GameManager.Instance.GameData.dailyQuestBoardData = data;
            }
            return data;
        }

        #endregion

#if UNITY_EDITOR
        #region 디버그

        /// <summary>
        /// 현재 풀에서 랜덤 3개를 골라 의뢰판을 강제 확정. 풀이 없으면 먼저 생성.
        /// </summary>
        public void DebugForceConfirm3RandomDungeons()
        {
            var saveData = GetSaveData();

            if (saveData.poolDungeonIDs == null || saveData.poolDungeonIDs.Count == 0)
            {
                var allDungeons = DataManager.Instance.GetAllDungeons();
                var pool = SelectDungeonsWeighted(allDungeons, Mathf.Max(3, Config.bronzePoolSize));
                saveData.poolDungeonIDs = pool.Select(d => d.StaticID).ToList();
                saveData.generatedDay = TimeManager.Instance.CurrentDay;
                RollDailyArmorTypes(pool);
            }

            var poolDungeons = GetPoolDungeons();
            var selected = poolDungeons.OrderBy(_ => Random.value).Take(3).ToList();

            saveData.selectedDungeonIDs = selected.Select(d => d.StaticID).ToList();
            saveData.highlightedDungeonIDs = new List<string>();
            saveData.isConfirmed = true;

            ScoutManager.Instance.PrerollScoutValues(selected);
            ScoutManager.Instance.DebugInstantScoutAll(selected);
            // OnBoardConfirmed를 발행하지 않아 QuestBoardView가 Phase 2 전환 애니메이션을 트리거하지 않게 한다.
            // 디버그 강제 확정은 즉시 결과 처리만 수행한다.
            saveData.scoutPhaseClosedByUser = true;

            Log.Info($"[QuestBoardManager] 디버그: {string.Join(", ", selected.Select(d => d.dungeonName))} 강제 확정");
        }

        #endregion
#endif
    }
}
