// Scripts/Systems/QuestManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class QuestManager : BaseManager<QuestManager>
    {
        [Header("Runtime Data")]
        [SerializeField] private WeeklyQuestInstance currentQuest;
        private GameData gameData;
        
        // Events
        public event Action<WeeklyQuestInstance> OnQuestStarted;
        public event Action<WeeklyQuestInstance, int> OnQuestProgressUpdated;
        public event Action<WeeklyQuestInstance> OnQuestCompleted;
        public event Action<WeeklyQuestInstance, int> OnQuestFailed;
        
        // Properties
        public WeeklyQuestInstance CurrentQuest => currentQuest;

        // 결과창에 표시할 직전 결과 퀘스트. 성공 시 다음 주로 진행하기 전 스냅샷.
        public WeeklyQuestInstance LastResultQuest { get; private set; }

        // 결과가 속한 주차. 성공 시 GrantRewardsAndAdvance가 currentWeek를 올린 뒤 결과창이 열리므로
        // 지금 값을 그대로 쓰면 한 주 앞선 숫자가 표시된다.
        public int LastResultWeek { get; private set; }

        // 실패 판정 시점에 확정된 벌금. 표시값과 실제 청구액이 어긋나지 않도록 캐싱한다.
        public int LastResultFine { get; private set; }
        
        #region 초기화

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged += CheckQuestDeadline;
                TimeManager.Instance.OnDayChanged += CheckDeadlineWarning;
            }
        }

        /// <summary>
        /// GameData 연동 및 퀘스트 초기화
        /// </summary>
        public void Initialize(GameData data)
        {
            gameData = data;
            LoadFromGameData(data);
        }

        /// <summary>
        /// 강제종료 복구: 실패한 퀘스트가 미처리 상태로 남아있으면 결과창 재오픈.
        /// 성공 케이스는 이미 보상 지급 후 다음 주로 진행된 상태로 저장되므로 트리거되지 않는다.
        /// 결과창이 복구 전 게임 상태 위에 뜨지 않도록, GameManager가 전 매니저 초기화와
        /// 참조 무결성 복구를 마친 뒤 호출한다 (Initialize에서 직접 호출하지 않는다).
        /// </summary>
        public void CheckDeadlineAfterLoad()
        {
            if (currentQuest != null)
                CheckQuestDeadline(gameData.currentDay);
        }

        /// <summary>
        /// 저장된 퀘스트 진행도 로드 또는 새 퀘스트 발급
        /// </summary>
        private void LoadFromGameData(GameData gameData)
        {
            if (gameData.currentQuestProgress != null && gameData.currentQuestProgress.Length > 0)
            {
                // 기존 퀘스트 진행도 복원.
                // 엔드리스 구간은 주차로 되찾을 수 없다(추첨 결과라서) - 저장된 템플릿 ID로 복원한다.
                var questData = !string.IsNullOrEmpty(gameData.currentEndlessQuestID)
                    ? DataManager.Instance.GetQuestByID(gameData.currentEndlessQuestID)
                    : GetQuestDataByWeek(gameData.currentWeek);

                // 해당 주차 데이터가 없으면 마지막 가용 퀘스트로 폴백 — questStartDay/currentProgress는 보존해 복구 흐름 유지
                if (questData == null)
                {
                    var allQuests = DataManager.Instance.GetAllWeeklyQuests();
                    questData = allQuests?.OrderByDescending(q => q.weekNumber).FirstOrDefault();
                    if (questData != null)
                        Log.Warn($"[QuestManager] Week {gameData.currentWeek} 데이터 없음 — '{questData.questTitle}'(주차 {questData.weekNumber})로 폴백, 진행도 보존");
                }

                if (questData != null)
                {
                    currentQuest = new WeeklyQuestInstance(questData, gameData.questStartDay)
                    {
                        // 폴백 등으로 요구 조건 수가 달라져도 배열 길이를 맞춘다 - 범위 초과 방지
                        currentProgress = NormalizeProgress(gameData.currentQuestProgress, questData),
                        // Completed만 복원한다. Failed는 Active로 두어 기존 재판정 흐름
                        // (CheckQuestDeadline -> Failed 전환 -> 결과창 재오픈)이 벌금 화면을 복구하게 한다.
                        // Failed를 그대로 복원하면 재오픈 방지 가드에 막혀 벌금 미납 소프트락이 된다.
                        status = gameData.currentQuestStatus == QuestStatus.Completed
                            ? QuestStatus.Completed
                            : QuestStatus.Active
                    };

                    // 정규화된 배열을 저장본과 공유해 UpdateProgress의 이중 기록 인덱스를 일치시킨다
                    gameData.currentQuestProgress = currentQuest.currentProgress;
                    Log.Info($"[QuestManager] Loaded quest: {questData.questTitle}, Week {gameData.currentWeek}, Status {currentQuest.status}");
                }
                else
                {
                    // 어떤 퀘스트 데이터도 사용 불가 — 새 퀘스트 발급
                    Log.Error("[QuestManager] 사용 가능한 퀘스트 데이터 없음 — 새 퀘스트 발급");
                    IssueNewQuest();
                }
            }
            else
            {
                // 새 게임 - 첫 퀘스트 발급
                IssueNewQuest();
            }
        }

        /// <summary>
        /// PlayerData에 현재 퀘스트 진행 상황 저장.
        /// 진행도는 UpdateProgress가 이미 gameData에 직접 기록하므로 상태만 동기화한다
        /// </summary>
        public void SaveToGameData(GameData gameData)
        {
            if (gameData == null) return;
            gameData.currentQuestStatus = currentQuest?.status ?? QuestStatus.Active;
        }

        /// <summary>
        /// 저장 진행도 배열을 현재 퀘스트 요구 조건 수에 맞게 정규화한다.
        /// 길이가 부족하면 0으로 채우고, 초과분은 버린다.
        /// </summary>
        private static int[] NormalizeProgress(int[] savedProgress, WeeklyQuestData questData)
        {
            int count = questData?.requirements?.Count ?? 0;
            var normalized = new int[count];

            for (int i = 0; i < count; i++)
                normalized[i] = savedProgress != null && i < savedProgress.Length ? savedProgress[i] : 0;

            return normalized;
        }

        #endregion

        #region 퀘스트 관리
        
        /// <summary>
        /// 새 주간 퀘스트 발급
        /// </summary>
        public void IssueNewQuest()
        {
            WeeklyQuestData questData;

            if (IsEndlessWeek(gameData.currentWeek))
            {
                questData = DrawEndlessQuest();
                gameData.currentEndlessQuestID = questData?.StaticID;
                RecordEndlessPick(questData?.StaticID);
            }
            else
            {
                gameData.currentEndlessQuestID = null;
                questData = GetQuestDataByWeek(gameData.currentWeek);
            }

            if (questData == null)
            {
                Log.Warn($"[QuestManager] No quest data for week {gameData.currentWeek}, using last available");
                var allQuests = DataManager.Instance.GetAllWeeklyQuests();
                questData = allQuests.OrderByDescending(q => q.weekNumber).FirstOrDefault();
            }

            if (questData == null)
            {
                Log.Error("[QuestManager] No quest data available!");
                return;
            }

            currentQuest = new WeeklyQuestInstance(questData, gameData.currentDay);
            gameData.questStartDay = gameData.currentDay;
            gameData.currentQuestProgress = new int[questData.requirements.Count];

            Log.Info($"[QuestManager] New quest issued: {questData.questTitle} ({gameData.currentWeek}주차" +
                     (IsEndlessWeek(gameData.currentWeek) ? $", 엔드리스 {questData.difficulty})" : ")"));
            OnQuestStarted?.Invoke(currentQuest);
        }
        
        #endregion

        #region 엔드리스 구간 (캠페인 이후 무한 반복)

        /// <summary>캠페인이 끝난 뒤인가. 이후 주차는 고정 커리큘럼 대신 난이도 풀에서 뽑는다.</summary>
        private bool IsEndlessWeek(int week)
        {
            var cfg = ConfigManager.Instance?.EndlessQuest;
            return cfg != null && week > cfg.campaignLastWeek;
        }

        /// <summary>
        /// 엔드리스 템플릿 추첨 — 난이도 등급을 가중치로 먼저 뽑고, 그 등급 풀에서 하나를 고른다.
        /// 추첨은 회차마다 달라야 하므로 시드를 고정하지 않는다. 저장/로드는 결과 ID로 복원한다.
        /// </summary>
        private WeeklyQuestData DrawEndlessQuest()
        {
            var cfg = ConfigManager.Instance?.EndlessQuest;
            if (cfg == null)
            {
                Log.Error("[QuestManager] EndlessQuestConfig 없음 - 마지막 주차 폴백");
                return null;
            }

            var pool = DataManager.Instance.GetAllWeeklyQuests()
                .Where(q => q.weekNumber > cfg.campaignLastWeek).ToList();
            if (pool.Count == 0)
            {
                Log.Error("[QuestManager] 엔드리스 템플릿이 없습니다");
                return null;
            }

            // 직전 N개 제외. 전부 걸러지면(풀이 작으면) 제외를 포기한다 - 못 뽑는 것보다 낫다.
            var recent = gameData.recentEndlessQuestIDs ?? new List<string>();
            var candidates = pool.Where(q => !recent.Contains(q.StaticID)).ToList();
            if (candidates.Count == 0) candidates = pool;

            // Extreme 2연속 차단 - 연속으로 걸리면 운이 아니라 사형 선고가 된다
            if (cfg.blockConsecutiveExtreme && LastPickWasExtreme(cfg))
            {
                var nonExtreme = candidates.Where(q => q.difficulty != QuestDifficulty.Extreme).ToList();
                if (nonExtreme.Count > 0) candidates = nonExtreme;
            }

            var difficulty = DrawDifficulty(cfg, candidates);
            var tier = candidates.Where(q => q.difficulty == difficulty).ToList();
            if (tier.Count == 0) tier = candidates;

            return tier[UnityEngine.Random.Range(0, tier.Count)];
        }

        /// <summary>가중치 추첨. 후보에 없는 등급은 제외해 빈 등급이 뽑히지 않게 한다.</summary>
        private QuestDifficulty DrawDifficulty(EndlessQuestConfig cfg, List<WeeklyQuestData> candidates)
        {
            var present = candidates.Select(q => q.difficulty).Distinct().ToList();
            float total = present.Sum(cfg.WeightOf);
            if (total <= 0f) return present.Count > 0 ? present[0] : QuestDifficulty.Normal;

            float roll = UnityEngine.Random.value * total;
            foreach (var d in present)
            {
                roll -= cfg.WeightOf(d);
                if (roll <= 0f) return d;
            }
            return present[present.Count - 1];
        }

        private bool LastPickWasExtreme(EndlessQuestConfig cfg)
        {
            var recent = gameData.recentEndlessQuestIDs;
            if (recent == null || recent.Count == 0) return false;

            var last = DataManager.Instance.GetQuestByID(recent[recent.Count - 1]);
            return last != null && last.difficulty == QuestDifficulty.Extreme;
        }

        private void RecordEndlessPick(string staticID)
        {
            if (string.IsNullOrEmpty(staticID)) return;

            gameData.recentEndlessQuestIDs ??= new List<string>();
            gameData.recentEndlessQuestIDs.Add(staticID);

            int window = Mathf.Max(0, ConfigManager.Instance?.EndlessQuest?.noRepeatWindow ?? 0);
            while (gameData.recentEndlessQuestIDs.Count > window)
                gameData.recentEndlessQuestIDs.RemoveAt(0);
        }

        #endregion

        #region 퀘스트 관리 (이어서)

        /// <summary>
        /// 퀘스트 진행도 업데이트 (다른 매니저에서 호출)
        /// </summary>
        /// <param name="amount">한 번에 더할 진행량. 금액 누적형(GoldEarned)만 1이 아닌 값을 넘긴다.</param>
        public void UpdateProgress(QuestType type, Grade? grade = null, WeaponType? weaponType = null, string dungeonID = null, int amount = 1)
        {
            if (currentQuest == null || currentQuest.status != QuestStatus.Active) return;
            if (amount <= 0) return;

            for (int i = 0; i < currentQuest.questData.requirements.Count; i++)
            {
                var req = currentQuest.questData.requirements[i];
                
                if (req.questType != type) continue;
                
                bool matches = CheckRequirementMatch(req, grade, weaponType, dungeonID);
                
                if (matches)
                {
                    int before = currentQuest.currentProgress[i];
                    currentQuest.currentProgress[i] = before + amount;
                    gameData.currentQuestProgress[i] = currentQuest.currentProgress[i];

                    Log.Info($"[QuestManager] Progress updated: {req.requirementText} ({currentQuest.currentProgress[i]}/{req.targetCount})");
                    OnQuestProgressUpdated?.Invoke(currentQuest, i);

                    // 이번 증가로 해당 Requirement가 처음 완료됐는지 판정 (초과 진행 시 재발생 방지).
                    // 골드처럼 amount가 큰 유형은 목표를 정확히 밟지 않고 건너뛰므로 임계값 통과로 판정한다.
                    bool requirementJustCleared = before < req.targetCount && currentQuest.currentProgress[i] >= req.targetCount;
                    // 엔드리스 템플릿의 weekNumber는 풀 인덱스라 표시에 쓸 수 없다
                    int weekNumber = gameData.currentWeek;

                    // 완료 체크
                    if (currentQuest.IsComplete)
                    {
                        currentQuest.status = QuestStatus.Completed;
                        Log.Info("[QuestManager] Quest completed!");
                        OnQuestCompleted?.Invoke(currentQuest);
                        SendQuestCompletedAnalytics(currentQuest);

                        UIPopupController.Instance?.ShowToast(WeekMessage("Quest_WeekSuccessToast", weekNumber));
                    }
                    else if (requirementJustCleared)
                    {
                        int percent = Mathf.RoundToInt(currentQuest.GetOverallProgress() * 100f);
                        UIPopupController.Instance?.ShowToast(
                            WeekMessage("Quest_WeekProgressToast", weekNumber, percent));
                    }
                }
            }
        }
        
        /// <summary>
        /// 요구사항 조건 매칭 확인
        /// </summary>
        private bool CheckRequirementMatch(QuestRequirement req, Grade? grade, WeaponType? weaponType, string dungeonID)
        {
            switch (req.questType)
            {
                case QuestType.SuccessfulAdventures:
                    return true;
                    
                case QuestType.RentSpecificGrade:
                    return grade.HasValue && grade.Value >= req.minGrade;
                    
                case QuestType.RentSpecificWeapon:
                    return weaponType.HasValue && weaponType.Value == req.specificWeaponType;
                    
                case QuestType.CompleteSpecificDungeon:
                    return !string.IsNullOrEmpty(dungeonID) && dungeonID == req.specificDungeonID;
                    
                // 아래는 부가 조건 없는 단순 카운트형 - 해당 행동이 발생하면 무조건 진행
                case QuestType.GreatSuccessCount:
                case QuestType.CraftComplete:
                case QuestType.EnforceSuccess:
                case QuestType.EvolveSuccess:
                case QuestType.RerollComplete:
                case QuestType.SeerComplete:
                case QuestType.WeaponPurchase:
                case QuestType.GoldEarned:
                case QuestType.GiftComplete:
                    return true;

                default:
                    return false;
            }
        }
        
        /// <summary>
        /// 보상 즉시 지급 후 다음 주로 진행. 강제종료 복구 불필요화를 위해
        /// 결과창 표시 전에 호출된다 — currentQuest는 새 퀘스트로 교체된다.
        /// </summary>
        private void GrantRewardsAndAdvance()
        {
            if (currentQuest == null) return;

            EconomyManager.Instance?.AddGold(currentQuest.questData.goldReward, "퀘스트 보상");
            ReputationManager.Instance?.AddReputation(currentQuest.questData.reputationReward, "퀘스트 보상");
            if (currentQuest.questData.insightReward > 0)
                InsightManager.Instance?.AddInsight(currentQuest.questData.insightReward);

            Log.Info($"[QuestManager] 자동 보상 지급: {currentQuest.questData.goldReward}G, {currentQuest.questData.reputationReward} Rep");

            gameData.currentWeek++;
            gameData.currentQuestProgress = null;

            IssueNewQuest();
        }

        /// <summary>quest_completed 이벤트 발행 (Documents/Analytics_이벤트_설계.md Level 4)</summary>
        private void SendQuestCompletedAnalytics(WeeklyQuestInstance quest)
        {
            AnalyticsManager.Instance?.Send("quest_completed", new Dictionary<string, object>
            {
                { "quest_id", quest.questData.StaticID },
                { "issue_day", quest.startDay },
                { "complete_day", gameData.currentDay },
                { "days_to_complete", gameData.currentDay - quest.startDay },
                { "reward_type", GetRewardTypeAnalyticsName(quest.questData) }
            });
        }

        /// <summary>보상 구성 표기: 0이 아닌 보상들을 "+"로 연결 (예: "gold+reputation")</summary>
        private static string GetRewardTypeAnalyticsName(WeeklyQuestData questData)
        {
            var parts = new List<string>();
            if (questData.goldReward > 0) parts.Add("gold");
            if (questData.reputationReward > 0) parts.Add("reputation");
            if (questData.insightReward > 0) parts.Add("insight");
            return parts.Count > 0 ? string.Join("+", parts) : "none";
        }

        #endregion

        #region 벌금 관리
        
        /// <summary>
        /// 벌금 납부 — 검증·골드/평판 차감·멱등을 모두 책임진다.
        /// LastResultQuest가 Failed일 때만 1회 처리하고, 즉시 Claimed로 전이해 더블클릭/콜백 재진입을 차단한다.
        /// 호출부(UI)는 반환값으로 결과만 표현한다.
        /// </summary>
        public bool PayFine()
        {
            var quest = LastResultQuest;
            if (quest == null || quest.status != QuestStatus.Failed)
            {
                Log.Warn("[QuestManager] PayFine: 처리할 실패 퀘스트 없음 (이미 처리됨)");
                return false;
            }

            int fineAmount = LastResultFine;   // 실패 판정 시 확정된 금액 — 결과창 표시값과 일치

            // 골드 차감 성공 후에만 상태를 전이한다. 먼저 전이하면 차감 실패 시 재시도가 막힌다.
            // 동기 실행이므로 더블클릭 재진입은 두 번째 호출의 status 검사에서 차단된다.
            if (!EconomyManager.Instance.SpendGold(fineAmount, "주간 벌금 납부"))
            {
                Log.Warn($"[QuestManager] PayFine: 골드 부족. 필요={fineAmount}");
                return false;
            }

            quest.status = QuestStatus.Claimed;   // 결과 처리 완료로 전이 → 재진입 차단
            ReputationManager.Instance?.AddReputation(-quest.questData.reputationPenalty, "퀘스트 실패");

            gameData.totalFinePaid += fineAmount;
            gameData.currentWeek++;
            gameData.currentQuestProgress = null;

            Log.Info($"[QuestManager] Fine paid: {fineAmount}G");
            IssueNewQuest();
            GameManager.Instance?.SaveAfterCommittedAction("Quest.PayFine");
            return true;
        }
        
        /// <summary>
        /// 현재 벌금 계산.
        /// 캠페인은 퀘스트 SO의 고정값을 쓰고, 엔드리스는 주차 곡선으로 계산한다 -
        /// 성장은 정점에서 멈추는데 실패 비용만 계속 오르므로 회차가 언젠가 반드시 끝난다 (레벨디자인 §8-4).
        /// </summary>
        public int CalculateWeeklyFine()
        {
            var cfg = ConfigManager.Instance?.EndlessQuest;
            if (cfg != null && IsEndlessWeek(gameData.currentWeek))
                return cfg.FineForWeek(gameData.currentWeek);

            if (currentQuest?.questData != null)
            {
                return currentQuest.questData.weeklyFine;
            }

            // 기본 벌금 + 주별 증가
            int baseFine = 500;
            float increaseRate = 0.1f;
            return Mathf.RoundToInt(baseFine * Mathf.Pow(1 + increaseRate, gameData.currentWeek - 1));
        }

        /// <summary>
        /// 날짜 변경 시 마감일 체크 (TimeManager.OnDayChanged에서 호출)
        /// </summary>
        public void CheckQuestDeadline(int currentDay)
        {
            if (currentQuest == null) return;
            if (!currentQuest.IsExpired(currentDay)) return;

            if (currentQuest.status == QuestStatus.Active)
            {
                if (currentQuest.IsComplete)
                {
                    currentQuest.status = QuestStatus.Completed;
                    Log.Info("[QuestManager] Quest completed on deadline");
                    OnQuestCompleted?.Invoke(currentQuest);
                    SendQuestCompletedAnalytics(currentQuest);
                }
                else
                {
                    currentQuest.status = QuestStatus.Failed;
                    int fineAmount = CalculateWeeklyFine();
                    LastResultFine = fineAmount;
                    Log.Info($"[QuestManager] Quest failed on deadline. Fine: {fineAmount}G");
                    OnQuestFailed?.Invoke(currentQuest, fineAmount);
                    AnalyticsManager.Instance?.Send("quest_failed", new Dictionary<string, object>
                    {
                        { "quest_id", currentQuest.questData.StaticID },
                        { "issue_day", currentQuest.startDay },
                        { "fail_day", gameData.currentDay },
                        { "fine", fineAmount }
                    });
                }
            }
            else if (currentQuest.status != QuestStatus.Completed)
            {
                // Failed(이미 결과창 표시됨) / Claimed(이미 처리됨): 재오픈 방지
                return;
            }
            // 위 분기 후 도달 status: Completed (방금/mid-week) 또는 Failed (방금 전환)

            // 결과창 스냅샷 — 성공 시 GrantRewardsAndAdvance가 currentQuest를 새 퀘스트로 교체하기 전 캐싱
            LastResultQuest = currentQuest;
            LastResultWeek = gameData.currentWeek;

            if (currentQuest.status == QuestStatus.Completed)
                GrantRewardsAndAdvance();

            UIManager.Instance?.OpenPanel<QuestResultView>();
        }

        /// <summary>
        /// 마감 당일 아침에 미달성 상태면 토스트로 경고 (TimeManager.OnDayChanged에서 호출).
        /// 실제 성공/실패 판정은 다음날 아침 CheckQuestDeadline에서 이뤄지므로, 이 날 하루가 마지막 만회 기회다.
        /// </summary>
        private void CheckDeadlineWarning(int currentDay)
        {
            if (currentQuest == null || currentQuest.status != QuestStatus.Active) return;
            if (currentDay != currentQuest.deadlineDay) return;
            if (currentQuest.IsComplete) return;

            int percent = Mathf.RoundToInt(currentQuest.GetOverallProgress() * 100f);
            UIPopupController.Instance?.ShowPopup(
                WeekMessage("Quest_DeadlineWarn", gameData.currentWeek, percent),
                type: PopupSfxType.Warning);
        }

        /// <summary>주차(+진행률) 인자를 쓰는 퀘스트 토스트/팝업 문구. 세 문구가 같은 인자 이름을 공유한다.</summary>
        private static string WeekMessage(string key, int week, int percent = 0)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Messages", key,
                   arguments: new object[] { new Dictionary<string, object>
                   {
                       { "week", week }, { "percent", percent }
                   } });

        #endregion

        #region 조회 기능
        
        /// <summary>
        /// 특정 주의 퀘스트 데이터 조회
        /// </summary>
        private WeeklyQuestData GetQuestDataByWeek(int week)
        {
            return DataManager.Instance.GetQuestByWeek(week);
        }

        #endregion

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged -= CheckQuestDeadline;
                TimeManager.Instance.OnDayChanged -= CheckDeadlineWarning;
            }
        }
    }
}
