// Scripts/Managers/VisitorManager.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace TodaysWeaponRental
{
    public partial class VisitorManager : BaseManager<VisitorManager>
    {
        [Header("모험가 관리")]
        [Tooltip("normal Adventurer의 남성 이름 풀 (Names 테이블 키)")]
        [SerializeField] private List<string> maleAdventurerNamePool = new List<string>();
        [Tooltip("normal Adventurer의 여성 이름 풀 (Names 테이블 키)")]
        [SerializeField] private List<string> femaleAdventurerNamePool = new List<string>();
        [Tooltip("normal Adventurer의 중립 이름 풀 (남녀 공용, Names 테이블 키)")]
        [SerializeField] private List<string> neutralAdventurerNamePool = new List<string>();
        [Tooltip("VisitorNPC 저장 데이터")]
        [SerializeField] private List<VisitorNPC> activeVisitors = new();
        [Tooltip("네임드 모험가 인스턴스 캐시 (instanceID -> AdventurerInstance)")]
        [SerializeField] private SerializableDictionary<string, AdventurerInstance> namedAdventurerCache = new();
        [Tooltip("일반 모험가 인스턴스 캐시 (instanceID -> AdventurerInstance)")]
        [SerializeField] private SerializableDictionary<string, AdventurerInstance> normalAdventurerCache = new();
        [Tooltip("튜토리얼 전용 모험가 캐시 - 스폰 풀과 분리해 영속성만 담당 (instanceID -> AdventurerInstance)")]
        [SerializeField] private SerializableDictionary<string, AdventurerInstance> tutorialAdventurerCache = new();
        [Tooltip("아침 페이즈에 미리 생성해둘 일반 모험가 풀")]
        [SerializeField] private List<AdventurerInstance> dailyNormalVisitorPool = new();
        [SerializeField] private int dailyNormalVisitorIndex = 0;

        [Header("참조")]
        [SerializeField] private GameObject VisitorPrefab;
        [SerializeField] private AppearanceGeneratorData appearanceGeneratorData;

        [Header("스폰 포인트")]
        [Tooltip("문 안쪽 경유점 = [point0(문), point1] 순서")]
        [SerializeField] private List<Transform> entryPath;
        [Tooltip("대기열 슬롯 = [point2(줄 머리), q1, q2, ...]. 개수 = 대기열 상한")]
        [SerializeField] private List<Transform> queuePoints;
        [SerializeField] private List<Transform> interactionPoints;
        [SerializeField] private Transform visitorParent;
        [Tooltip("아래길 좌 끝 (Top·Bottom 사이 랜덤 y로 출현). 방문자+행인 공유")]
        [SerializeField] private Transform bottomRoadLeftTop;
        [SerializeField] private Transform bottomRoadLeftBottom;
        [Tooltip("아래길 우 끝")]
        [SerializeField] private Transform bottomRoadRightTop;
        [SerializeField] private Transform bottomRoadRightBottom;
        [Tooltip("윗길 좌 끝 (행인 전용)")]
        [SerializeField] private Transform topRoadLeftTop;
        [SerializeField] private Transform topRoadLeftBottom;
        [Tooltip("윗길 우 끝 (행인 전용)")]
        [SerializeField] private Transform topRoadRightTop;
        [SerializeField] private Transform topRoadRightBottom;
        [Tooltip("전령 전용 위치 — 상호작용 포인트 점유 시스템을 쓰지 않고 항상 여기 선다")]
        [SerializeField] private Transform heraldPoint;

        [Header("행인(Walker)")]
        [SerializeField] private GameObject walkerPrefab;
        [SerializeField] private Transform walkerParent;

        // 방문자 입장/퇴장 경로 접근용 (entryPath[0]=문, [1]=중간 경유점)
        public Vector3 EntryPoint0 => entryPath[0].position;
        public Vector3 EntryPoint1 => entryPath[1].position;
        public bool PathReady =>
            entryPath != null && entryPath.Count >= 2 && entryPath[0] != null && entryPath[1] != null
            && queuePoints != null && queuePoints.Count >= 1
            && (bottomRoadLeftTop != null || bottomRoadLeftBottom != null)
            && (bottomRoadRightTop != null || bottomRoadRightBottom != null);

        [SerializeField] private VisitorNPC[] interactionPointOccupants;

        // 상호작용 포인트가 다 찼을 때 줄서는 방문자들 (명시적 FIFO)
        private readonly List<VisitorNPC> waitingQueue = new();

        [Header("외형 데이터")]
        [SerializeField] private FixedAppearanceData weaponShopAppearance;
        [SerializeField] private FixedAppearanceData noVisitorAppearance;    // 청소 이벤트 NPC 외형
        [SerializeField] private FixedAppearanceData reviveOfferAppearance;  // 부활 제안 NPC 외형
        [SerializeField] private FixedAppearanceData heraldAppearance;       // 모험 결과 보고 전령 외형

        // 죽은 모험가 이벤트 대화 — DataManager에서 StaticID로 조회
        private const string NoVisitorDialogueID    = "DEAD_ADV_NO_VISITOR";
        private const string ReviveOfferDialogueID   = "DEAD_ADV_REVIVE_OFFER";
        private const string MiracleReviveDialogueID = "DEAD_ADV_MIRACLE_REVIVE";

        public DialogueData NoVisitorDialogue    => DataManager.Instance.GetDialogue(NoVisitorDialogueID);
        public DialogueData ReviveOfferDialogue   => DataManager.Instance.GetDialogue(ReviveOfferDialogueID);
        public DialogueData MiracleReviveDialogue => DataManager.Instance.GetDialogue(MiracleReviveDialogueID);

        // 사망 이벤트 보상/비용
        public int CleaningReward => 50 * TimeManager.Instance.CurrentDay;
        // 부활 대상은 항상 네임드. 기본가 x 주차 계단 배율 — "귀한 사람일수록 값을 더 받는다"
        public int ReviveCost
        {
            get
            {
                var tier = ConfigManager.Instance.PriceTier;
                if (tier == null) return 1500;
                return Mathf.RoundToInt(tier.reviveBaseCost * tier.At(tier.reviveCost, ConfigManager.CurrentWeek));
            }
        }

        private VisitorConfig Config => ConfigManager.Instance.Visitor;

        // 모험가 스폰 관리 (게임 시간 기준, 분 단위)
        private float lastAdventurerSpawnTime = 0f;
        private float nextAdventurerSpawnInterval = 0f;

        // 스폰 상태
        private bool HasSpawnedMorningNPCs
        {
            get => GameManager.Instance.GameData.hasSpawnedMorningNPCs;
            set => GameManager.Instance.GameData.hasSpawnedMorningNPCs = value;
        }
        private bool isSpawningAdventurers = false;

        public List<VisitorNPC> ActiveVisitors => activeVisitors;

        /// <summary>현재 상점에 있는 전령. 없으면 null.</summary>
        public VisitorNPC CurrentHerald =>
            activeVisitors.FirstOrDefault(v => v != null && v.visitorType == VisitorType.Herald && !v.isLeaving);

        // 아침 스킵 안내 팝업 중복 방지 (하루 1회, OnNewDayStarted에서 리셋)
        private bool hasPromptedSkipMorning = false;

        // 저녁 스킵 안내 팝업 중복 방지 (하루 1회, OnNewDayStarted에서 리셋)
        private bool hasPromptedSkipEvening = false;

        public event Action<VisitorNPC, float, float> OnVisitorTimerUpdated;
        public event Action<VisitorNPC, bool> OnVisitorWarningStateChanged;
        public event Action<VisitorNPC> OnVisitorAdded;
        public event Action<VisitorNPC> OnVisitorRemoved;

        /// <summary>
        /// 아침 스킵 가능 여부 — 그날 등장한 아침 NPC(무기상점/대장장이/이벤트)가 activeVisitors에 하나도 남지 않았을 때.
        /// 상호작용으로 처리됐든 체류시간 만료로 떠났든(둘 다 RemoveVisitor로 제거) "더 볼 일이 없으면" 스킵을 연다.
        /// 등장하지 않은 NPC는 애초에 목록에 없으므로 자동으로 조건에서 빠진다.
        /// HasSpawnedMorningNPCs로 "아침 NPC 생성 완료" 이후에만 판정되도록 가드한다.
        /// 단, 튜토리얼(1일차) 진행 중에는 아침 스킵을 발동하지 않는다.
        /// </summary>
        public bool CanSkipMorning =>
            HasSpawnedMorningNPCs
            && !IsTutorialBlockingMorningSkip
            && !activeVisitors.Any(v => v != null && IsMorningSkipNPC(v.visitorType) && !v.isLeaving);

        /// <summary>
        /// 저녁 스킵 가능 여부 - 저녁이고 상점에 남은 방문자가 하나도 없을 때.
        /// 전령은 보고를 모두 끝내야 퇴장(isLeaving)하므로, 보고가 남아 있으면 자동으로 조건에서 걸린다.
        /// 단, 튜토리얼(1일차) 진행 중에는 저녁 스킵을 발동하지 않는다.
        /// </summary>
        public bool CanSkipEvening =>
            TimeManager.Instance != null
            && TimeManager.Instance.IsEvening()
            && !(TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            && !activeVisitors.Any(v => v != null && !v.isLeaving);

        /// 튜토리얼 중에는 아침 스킵을 막되, 3단계(아침 스킵 버튼 유도)에서만 연다.
        private static bool IsTutorialBlockingMorningSkip =>
            TutorialManager.Instance != null
            && TutorialManager.Instance.IsTutorialActive
            && !TutorialManager.Instance.IsMorningSkipAllowed;

        /// 튜토리얼(1일차) 진행 중에는 낮 진입 첫 모험가 스폰을 억제한다(튜토리얼 전용 모험가와 중복 방지).
        private static bool IsTutorialSuppressingFirstAdventurer =>
            TutorialManager.Instance != null
            && TutorialManager.Instance.IsFirstDay()
            && TutorialManager.Instance.IsTutorialActive;

        /// 아침 스킵 판정에 포함되는 NPC 유형 (그날 등장 시 상호작용/퇴장 전까지 스킵을 막는다)
        private static bool IsMorningSkipNPC(VisitorType type) =>
            type == VisitorType.WeaponShop || type == VisitorType.Blacksmith || type == VisitorType.EventNPC;

        protected override void Awake()
        {
            base.Awake();

            int slotCount = (interactionPoints != null && interactionPoints.Count > 0) ? interactionPoints.Count : 3;
            interactionPointOccupants = new VisitorNPC[slotCount];
        }

        public void Initialize(GameData gameData)
        {
            if (activeVisitors == null)
            {
                activeVisitors = new List<VisitorNPC>();
                Log.Warn("[VisitorManager] activeVisitors가 null이었으므로 새 리스트 초기화함");
            }

            LoadFromGameData(gameData);
        }

        private void Start()
        {
            SubscribeEvents();

            if (TimeManager.Instance != null)
            {
                TimePhase currentPhase = TimeManager.Instance.CurrentPhase;

                switch (currentPhase)
                {
                    case TimePhase.Morning:
                        OnEnterMorningPhase();
                        break;
                    case TimePhase.Day:
                        OnEnterDaytimePhase();
                        break;
                    case TimePhase.Evening:
                        OnEnterEveningPhase();
                        break;
                    case TimePhase.Night:
                        OnEnterNightPhase();
                        break;
                }

                // 씬 시작(불러오기 포함) 시 빈 거리로 시작하지 않도록 페이즈별 행인 시드 스폰
                SpawnRestoreWalkers();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeEvents();
        }

        public void SubscribeEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged += OnTimeChanged;
                TimeManager.Instance.OnTimeSkipped += OnTimeSkipped;
                TimeManager.Instance.OnDayChanged += OnNewDayStarted;
                TimeManager.Instance.OnPhaseChanged += OnPhaseChanged;
                TimeManager.Instance.OnTimeSkipStarted += HandleTimeSkipStarted;
            }
        }

        public void UnsubscribeEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged -= OnTimeChanged;
                TimeManager.Instance.OnTimeSkipped -= OnTimeSkipped;
                TimeManager.Instance.OnDayChanged -= OnNewDayStarted;
                TimeManager.Instance.OnPhaseChanged -= OnPhaseChanged;
                TimeManager.Instance.OnTimeSkipStarted -= HandleTimeSkipStarted;
            }
        }

        #region 시간 이벤트

        private void UpdateVisitorTimersByGameTime(float gameMinutes)
        {
            for (int i = activeVisitors.Count - 1; i >= 0; i--)
            {
                VisitorNPC visitor = activeVisitors[i];

                if (visitor.isInteracting || visitor.isLeaving || visitor.StayDuration <= 0)
                    continue;

                visitor.UpdateTimer(gameMinutes);

                if (visitor.RemainingTime <= 0)
                {
                    RemoveVisitor(visitor);
                }
            }
        }

        private void CheckAdventurerSpawnByGameTime(float gameMinutes)
        {
            // 호출당 고정 +3이 아니라 실제 경과 분을 누적한다 - 시간 스킵 시 스폰 타이머 누락 방지
            lastAdventurerSpawnTime += gameMinutes;

            if (lastAdventurerSpawnTime < nextAdventurerSpawnInterval)
                return;

            // 대기열이 꽉 찼으면 랜덤 방문자 스폰을 보류한다. 타이머는 리셋하지 않아
            // 자리가 나면 다음 틱에 곧바로 스폰이 재개된다.
            if (IsVisitorCapReached)
                return;

            SpawnNextAdventurer();
        }

        /// <summary>
        /// 다음 스폰 간격을 재설정하고 모험가(또는 이벤트 NPC)를 스폰한다.
        /// </summary>
        private void SpawnNextAdventurer()
        {
            var (min, max) = ReputationManager.Instance.GetAdventurerSpawnInterval();
            nextAdventurerSpawnInterval = Random.Range(min, max);

            lastAdventurerSpawnTime = 0f;

            if (!TrySpawnReturningAdventurer())
                SpawnNewAdventurer();
        }

        /// <summary>
        /// 시간을 진행시키지 않고 현재 상태로 방문자 타이머/경고 이벤트를 다시 발화한다.
        /// 사이드바가 숨겨졌다 다시 켜질 때(minimal/hide 패널 닫힘) 얼어붙은 게이지를 즉시 갱신하는 용도.
        /// </summary>
        public void ForceRefreshVisitorUI() => UpdateVisitorUI();

        private void UpdateVisitorUI()
        {
            foreach (var visitor in activeVisitors)
            {
                if (visitor.isInteracting || visitor.isLeaving || visitor.StayDuration <= 0)
                    continue;

                float remaining = visitor.RemainingTime;
                float maxTime = visitor.BaseStayDuration;
                float ratio = Mathf.Clamp01(remaining / maxTime);

                OnVisitorTimerUpdated?.Invoke(visitor, remaining, ratio);

                bool isWarning = visitor.GetWarningState(Config.warningTimeThreshold);
                if (isWarning != visitor.wasWarningState)
                {
                    visitor.wasWarningState = isWarning;
                    OnVisitorWarningStateChanged?.Invoke(visitor, isWarning);
                }
            }
        }

        private void OnTimeChanged(int hour, int minute)
        {
            UpdateWalkerSpawn();

            EnsureHerald();
        }

        private void OnTimeSkipped(float gameMinutes)
        {
            // 스폰 체크는 실제 경과 분을 전달받는 OnTimeSkipped로 일원화한다.
            // 모든 AdvanceTime 경로(일반 틱/스킵)가 OnTimeSkipped를 발화하므로 커버리지는 동일하다
            if (isSpawningAdventurers && TimeManager.Instance.IsDaytime())
            {
                CheckAdventurerSpawnByGameTime(gameMinutes);
            }

            UpdateVisitorTimersByGameTime(gameMinutes);
            UpdateVisitorUI();

            EnsureHerald();
        }

        public void OnNewDayStarted(int day)
        {
            HasSpawnedMorningNPCs = false;
            isSpawningAdventurers = false;
            hasPromptedSkipMorning = false;
            hasPromptedSkipEvening = false;

            lastAdventurerSpawnTime = 0f;
            nextAdventurerSpawnInterval = 0f;
            ClearAllVisitors();
            ClearAllWalkers();

            dailyNormalVisitorPool.Clear();
            dailyNormalVisitorIndex = 0;
        }

        /// <summary>
        /// Phase 전환 시 호출
        /// </summary>
        private void OnPhaseChanged(TimePhase newPhase)
        {
            switch (newPhase)
            {
                case TimePhase.Morning:
                    OnEnterMorningPhase();
                    break;

                case TimePhase.Day:
                    OnEnterDaytimePhase();
                    if (!IsTutorialSuppressingFirstAdventurer && !TrySpawnGuaranteedAdventurer())
                    {
                        if (!TrySpawnReturningAdventurer())
                            SpawnNewAdventurer();
                    }
                    break;

                case TimePhase.Evening:
                    OnEnterEveningPhase();
                    break;

                case TimePhase.Night:
                    OnEnterNightPhase();
                    break;
            }
        }

        #endregion

        #region 페이즈 별 관리 메서드

        private void OnEnterMorningPhase()
        {
            if (!HasSpawnedMorningNPCs)
            {
                HasSpawnedMorningNPCs = true;
                GenerateDailyNormalVisitorPool();

                if (TutorialManager.Instance.IsFirstDay() && !TutorialManager.Instance.HasCompletedTutorial)
                {
                    TutorialManager.Instance.StartTutorial();
                }
                else
                {
                    bool hasRestoredWeaponShop = activeVisitors.Any(v => v.visitorType == VisitorType.WeaponShop);
                    bool hasRestoredBlacksmith = activeVisitors.Any(v => v.visitorType == VisitorType.Blacksmith);

                    if (!hasRestoredWeaponShop && WeaponShopManager.Instance.ShouldSpawnToday())
                        SpawnWeaponShop();
                    if (!hasRestoredBlacksmith) SpawnBlacksmith();

                    bool hasRestoredEventNPC = activeVisitors.Any(v => v.visitorType == VisitorType.EventNPC);
                    if (!hasRestoredEventNPC) CheckAndSpawnEventNPC();

                    // 투자자 결과 NPC (성공 시, 아직 복원된 NPC가 없을 때만 스폰)
                    var pd = GameManager.Instance.GameData;
                    bool hasRestoredInvestorResult = activeVisitors.Any(v => v.visitorType == VisitorType.InvestorResult);
                    if (pd.hasPendingInvestment && !hasRestoredInvestorResult)
                    {
                        if (pd.pendingInvestorReturnedGold > 0)
                            SpawnInvestorResultNPC(pd.pendingInvestorResultDialogueID, pd.pendingInvestorReturnedGold);
                        // 초기화는 NPC 상호작용 시작 시로 이동 (VisitorNPC.InvestorResultInteraction)
                    }
                }
            }
        }

        private void OnEnterDaytimePhase()
        {
            // 첫날(튜토리얼)에는 시간 경과 기반 모험가 스폰을 시작하지 않는다.
            // 튜토리얼 전용 모험가(5·8단계)와 무작위 스폰이 겹쳐 이후 단계가 꼬이는 문제 방지.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsFirstDay())
                return;

            StartAdventurerSpawning();
        }

        private void OnEnterEveningPhase()
        {
            StopAdventurerSpawning();
            EnsureHerald();
        }

        private void OnEnterNightPhase()
        {
            EnsureHerald();
        }

        /// <summary>
        /// 튜토리얼 9-A: 수면 마법으로 저녁에 진입한 직후 전령 스폰을 보장한다(멱등).
        /// 시간 스킵이 no-op(이미 18시 이후)이면 시간 틱이 오지 않아 EnsureHerald가 호출되지 않으므로 명시적 진입점이 필요하다.
        /// </summary>
        public void EnsureHeraldNow() => EnsureHerald();

        /// <summary>
        /// 저녁 진입 시 전령을 보장한다. 최초 진입이면 그 시점의 완료 모험을 스냅샷하고(이후 완료분은 다음날 이월)
        /// 보고할 게 없어도 무조건 스폰한다. 불러오기로 이미 스냅샷이 떠 있는 상태면 남은 보고가 있을 때만 다시 세운다.
        /// 페이즈 전환뿐 아니라 시간 틱/스킵마다 호출되어, 스킵 도중 스폰이 누락된 경우를 보정한다(멱등).
        /// </summary>
        private void EnsureHerald()
        {
            if (TimeManager.Instance == null) return;
            if (!TimeManager.Instance.IsEvening() && !TimeManager.Instance.IsDayEnded()) return;

            var advMgr = AdventureManager.Instance;
            if (advMgr == null) return;

            if (!advMgr.HeraldReportStartedToday)
                advMgr.StartHeraldReport();
            else if (!advMgr.HasPendingHeraldReport)
                return;   // 오늘 보고를 이미 끝냈다 - 다시 부르지 않는다

            if (CurrentHerald != null) return;

            SpawnHerald();
        }

        #endregion

        #region 방문자 관리

        private void RegisterVisitor(VisitorNPC visitor)
        {
            activeVisitors.Add(visitor);

            // 사이드바 버튼은 '상호작용 준비 완료'(슬롯/전령 포인트 도착) 시점에만 만든다.
            // 복원 skip으로 슬롯에 즉시 배치된 방문자는 이미 준비 상태 → 즉시 발행,
            // 걸어 들어오는/대기 중 방문자는 도착 시(OnEnterCompleted) 발행.
            if (visitor.IsReadyForSidebar)
                OnVisitorAdded?.Invoke(visitor);
            else
                visitor.OnEnterCompleted += HandleVisitorReady;
        }

        /// <summary>
        /// 방문자가 상호작용 슬롯(또는 전령 포인트)에 도착해 준비되면 사이드바 버튼을 생성한다.
        /// 방문자별로 1회만 발화하고 즉시 구독 해제한다.
        /// </summary>
        private void HandleVisitorReady(VisitorNPC visitor)
        {
            if (visitor != null)
                visitor.OnEnterCompleted -= HandleVisitorReady;

            if (visitor == null || visitor.isLeaving)
                return;

            OnVisitorAdded?.Invoke(visitor);
        }

        public void RemoveVisitor(VisitorNPC visitor)
        {
            if (visitor == null || visitor.isLeaving)
                return;

            if (visitor.visitorType == VisitorType.Adventurer && visitor.adventurerInstance != null)
            {
                visitor.adventurerInstance.isVisiting = false;

                // 모험 출발 없이 방문이 끝나면 배정만 된 아이템을 인벤토리로 반환한다
                if (!visitor.adventurerInstance.isAdventuring)
                {
                    ActiveItemManager.Instance?.TryReturnAssignedItem(
                        visitor.adventurerInstance.instanceID,
                        "visitor-left-without-adventure");
                }

                if (!visitor.adventurerInstance.isNamed && !visitor.adventurerInstance.isAdventuring)
                {
                    Log.Info($"[VisitorManager] 일반 모험가 '{visitor.adventurerInstance.Name}' 방문 종료 — 인스턴스 폐기");
                }
            }

            // 점유 해제 (StartLeaving 전에). 슬롯 점유자면 슬롯을 비우고,
            // 대기열에 있으면 줄에서 빼고 뒤 인원을 당긴다.
            int occupiedIndex = visitor.InteractionPointIndex;
            if (occupiedIndex >= 0)
                ReleaseInteractionPoint(occupiedIndex);
            else if (visitor.IsWaiting)
                LeaveWaitingQueue(visitor);

            // 스킵 중에는 걸어 나가는 연출 없이 즉시 제거한다
            // (스킵이 끝난 뒤 남아서 천천히 걸어 나가는 어색함 방지).
            if (TimeManager.Instance != null && TimeManager.Instance.IsSkippingTime)
            {
                visitor.isLeaving = true;
                OnVisitorRemoved?.Invoke(visitor);
                OnVisitorLeaveComplete(visitor);
                return;
            }

            visitor.StartLeaving();
            OnVisitorRemoved?.Invoke(visitor);

            // 전령 보고 종료·모험가 퇴장 모두 이 경로를 지나므로, 저녁 스킵 판정은 여기 한 곳에서 건다.
            CheckAndPromptSkipEvening();

            // 파괴는 다단계 퇴장 애니메이션이 끝났을 때(OnVisitorLeaveComplete)로 미룬다.
        }

        /// <summary>
        /// VisitorNPC의 퇴장 애니메이션이 끝났을 때 호출되어 실제로 목록에서 빼고 파괴한다.
        /// (고정 1초 대기 대신 완료 콜백 기반 — 새 다단계 퇴장이 1초를 넘기므로)
        /// </summary>
        public void OnVisitorLeaveComplete(VisitorNPC visitor)
        {
            if (visitor == null) return;

            activeVisitors.Remove(visitor);
            if (visitor.gameObject != null)
                Destroy(visitor.gameObject);
        }

        public void SetOtherVisitorsFaded(bool faded, VisitorNPC exclude)
        {
            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitor == exclude) continue;
                visitor.SetFaded(faded);
            }

            FadeWalkers(faded);
        }

        public void ClearAllVisitors()
        {
            if (activeVisitors == null)
            {
                activeVisitors = new List<VisitorNPC>();
                return;
            }

            foreach (var visitor in activeVisitors.ToList())
            {
                if (visitor != null && visitor.gameObject != null)
                {
                    OnVisitorRemoved?.Invoke(visitor);
                    Destroy(visitor.gameObject);
                }
            }
            activeVisitors.Clear();
            waitingQueue.Clear();

            if (interactionPointOccupants != null)
            {
                for (int i = 0; i < interactionPointOccupants.Length; i++)
                    interactionPointOccupants[i] = null;
            }
        }

        /// <summary>
        /// 빈 interactionPoint 인덱스 반환. 없으면 -1.
        /// </summary>
        public int GetAvailableInteractionPointIndex()
        {
            for (int i = 0; i < interactionPointOccupants.Length; i++)
            {
                if (interactionPointOccupants[i] == null)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// interactionPoint 위치 반환
        /// </summary>
        public Vector3 GetInteractionPointPosition(int index)
        {
            return interactionPoints[index].position;
        }

        /// <summary>
        /// interactionPoint 점유 등록
        /// </summary>
        public void OccupyInteractionPoint(int index, VisitorNPC visitor)
        {
            if (index < 0 || index >= interactionPointOccupants.Length) return;
            interactionPointOccupants[index] = visitor;
        }

        /// <summary>
        /// interactionPoint 점유 해제 후 대기 중인 방문자에게 알림
        /// </summary>
        public void ReleaseInteractionPoint(int index)
        {
            if (index < 0 || index >= interactionPointOccupants.Length) return;
            interactionPointOccupants[index] = null;

            // 줄 머리를 빈 슬롯으로 승격시키고, 나머지 대기자를 한 칸씩 당긴다.
            if (waitingQueue.Count > 0)
            {
                var head = waitingQueue[0];
                waitingQueue.RemoveAt(0);
                head?.OnInteractionPointReleased();
                RepositionQueue();
            }
        }

        /// <summary>
        /// 전령이 서는 위치. heraldPoint가 비어 있으면 queuePoint로 대체한다.
        /// </summary>
        public Vector3 GetHeraldPosition()
        {
            if (heraldPoint != null) return heraldPoint.position;

            Log.Warn("[VisitorManager] heraldPoint가 지정되지 않아 줄 머리(queuePoints[0])를 사용합니다.");
            return queuePoints[0].position;
        }

        #endregion

        #region 대기열 / 진입·퇴장 위치

        /// <summary>대기열 상한 도달 여부 — 랜덤 방문자 스폰을 보류할지 판단.</summary>
        public bool IsVisitorCapReached =>
            queuePoints == null || queuePoints.Count == 0 || waitingQueue.Count >= queuePoints.Count;

        /// <summary>줄 슬롯 위치. index가 슬롯 수를 넘으면 마지막 슬롯에 클램프(겹쳐 서기).</summary>
        public Vector3 GetQueuePointPosition(int index)
        {
            if (queuePoints == null || queuePoints.Count == 0)
            {
                Log.Warn("[VisitorManager] queuePoints가 비어 있습니다. 인스펙터에 대기열 슬롯을 배치하세요.");
                return heraldPoint != null ? heraldPoint.position : Vector3.zero;
            }

            int clamped = Mathf.Clamp(index, 0, queuePoints.Count - 1);
            return queuePoints[clamped].position;
        }

        /// <summary>방문자를 대기열 꼬리에 추가한다.</summary>
        public void JoinWaitingQueue(VisitorNPC visitor)
        {
            if (visitor != null && !waitingQueue.Contains(visitor))
                waitingQueue.Add(visitor);
        }

        /// <summary>대기열 내 순번(줄 머리=0). 없으면 -1.</summary>
        public int GetWaitingIndex(VisitorNPC visitor) => waitingQueue.IndexOf(visitor);

        /// <summary>대기열에서 제거하고 뒤 인원을 한 칸씩 당긴다.</summary>
        public void LeaveWaitingQueue(VisitorNPC visitor)
        {
            if (waitingQueue.Remove(visitor))
                RepositionQueue();
        }

        /// <summary>대기열 각 인원을 자기 순번 슬롯으로 걸어 전진시킨다.</summary>
        private void RepositionQueue()
        {
            for (int i = 0; i < waitingQueue.Count; i++)
                waitingQueue[i]?.MoveToQueueSlot(i);
        }

        /// <summary>구간(Top~Bottom) 사이 t 지점. 한쪽만 있으면 그 위치, 둘 다 없으면 zero.</summary>
        public static Vector3 LerpRoadSegment(Transform top, Transform bottom, float t)
        {
            if (top == null && bottom == null) return Vector3.zero;
            if (top == null) return bottom.position;
            if (bottom == null) return top.position;
            return Vector3.Lerp(top.position, bottom.position, t);
        }

        /// <summary>아래길 좌/우 끝 중 랜덤한 진입 위치(랜덤 y). 방문자·행인·전령 공용.</summary>
        public Vector3 GetBottomRoadEntryPosition()
        {
            return Random.value < 0.5f
                ? LerpRoadSegment(bottomRoadLeftTop, bottomRoadLeftBottom, Random.value)
                : LerpRoadSegment(bottomRoadRightTop, bottomRoadRightBottom, Random.value);
        }

        /// <summary>퇴장 위치 — 진입한 반대쪽 아래길 끝(랜덤 y).</summary>
        public Vector3 GetBottomRoadExitPosition(bool entryFromLeft)
        {
            return entryFromLeft
                ? LerpRoadSegment(bottomRoadRightTop, bottomRoadRightBottom, Random.value)
                : LerpRoadSegment(bottomRoadLeftTop, bottomRoadLeftBottom, Random.value);
        }

        #endregion

        #region 원근 정렬 (sortingOrder)

        private bool sortingYComputed;
        private float sortingYMin;
        private float sortingYMax;

        /// <summary>
        /// world y를 안전 대역 [npcSortingOrderMin, npcSortingOrderMax] 안의 sortingOrder로 변환한다.
        /// y가 높을수록(멀수록) 낮은 order(뒤). npcSortingSteps 단계로 양자화하고 대역을 벗어나지 않게 클램프한다.
        /// (배경 0 위 / PanelCanvas 100 아래를 벗어나 패널 위로 뜨거나 배경 뒤로 사라지는 것 방지)
        /// </summary>
        public int ComputeSortingOrder(float worldY)
        {
            if (!sortingYComputed) ComputeSortingYRange();

            var cfg = Config;
            int steps = Mathf.Max(1, cfg.npcSortingSteps);

            float t = sortingYMax > sortingYMin
                ? Mathf.Clamp01(Mathf.InverseLerp(sortingYMin, sortingYMax, worldY))
                : 0f;

            int bucket = Mathf.RoundToInt(t * (steps - 1));
            float f = steps > 1 ? (float)bucket / (steps - 1) : 0f;

            // f=0(앞/낮은 y) -> Max, f=1(뒤/높은 y) -> Min
            return Mathf.RoundToInt(Mathf.Lerp(cfg.npcSortingOrderMax, cfg.npcSortingOrderMin, f));
        }

        /// <summary>NPC가 도달할 수 있는 모든 포인트의 y 범위를 1회 산출해 캐시한다.</summary>
        private void ComputeSortingYRange()
        {
            sortingYComputed = true;
            sortingYMin = float.MaxValue;
            sortingYMax = float.MinValue;

            void Consider(Transform tr)
            {
                if (tr == null) return;
                float y = tr.position.y;
                if (y < sortingYMin) sortingYMin = y;
                if (y > sortingYMax) sortingYMax = y;
            }

            Consider(bottomRoadLeftTop); Consider(bottomRoadLeftBottom);
            Consider(bottomRoadRightTop); Consider(bottomRoadRightBottom);
            Consider(topRoadLeftTop); Consider(topRoadLeftBottom);
            Consider(topRoadRightTop); Consider(topRoadRightBottom);
            Consider(heraldPoint);
            if (entryPath != null) foreach (var tr in entryPath) Consider(tr);
            if (queuePoints != null) foreach (var tr in queuePoints) Consider(tr);
            if (interactionPoints != null) foreach (var tr in interactionPoints) Consider(tr);

            if (sortingYMin > sortingYMax) { sortingYMin = 0f; sortingYMax = 0f; }
        }

        #endregion

        #region 저장/로드

        public void SaveToGameData(GameData gameData)
        {
            if (gameData == null)
            {
                Log.Error("[VisitorManager] Cannot save: GameData is null");
                return;
            }

            if (namedAdventurerCache == null)
            {
                Log.Warn("[VisitorManager] namedAdventurerCache is null, initializing");
                gameData.namedAdventurerInstances = new List<AdventurerInstanceSaveData>();
                return;
            }

            if (normalAdventurerCache == null)
            {
                Log.Warn("[VisitorManager] normalAdventurerCache is null, initializing");
                gameData.normalAdventurerInstances = new List<AdventurerInstanceSaveData>();
                return;
            }

            gameData.namedAdventurerInstances = namedAdventurerCache.Values
                .Where(i => i != null)
                .Select(i => i.ToSaveData())
                .ToList();

            gameData.normalAdventurerInstances = normalAdventurerCache.Values
                .Where(i => i != null)
                .Select(i => i.ToSaveData())
                .ToList();

            gameData.dailyNormalVisitorPoolSaveData = dailyNormalVisitorPool
                .Where(i => i != null)
                .Select(i => i.ToSaveData())
                .ToList();
            gameData.dailyNormalVisitorIndex = dailyNormalVisitorIndex;

            gameData.tutorialAdventurerInstances = tutorialAdventurerCache.Values
                .Where(i => i != null)
                .Select(i => i.ToSaveData())
                .ToList();

            gameData.activeVisitorStates = activeVisitors
                .Where(v => v != null && !v.isLeaving)
                .Where(v => !(v.visitorType == VisitorType.InvestorResult && !GameManager.Instance.GameData.hasPendingInvestment))
                // 전령은 방문자 상태가 아니라 보고 목록(heraldPendingAdventureIDs)으로 복원한다 - 중복 스폰 방지
                .Where(v => v.visitorType != VisitorType.Herald)
                .Select(v => v.ToSaveData())
                .ToList();

            // 스폰 타이머 저장 - 낮 시간 저장/재접속 시 스폰 간격이 처음부터 다시 시작하지 않도록
            gameData.lastAdventurerSpawnTime = lastAdventurerSpawnTime;
            gameData.nextAdventurerSpawnInterval = nextAdventurerSpawnInterval;
        }

        private void LoadFromGameData(GameData gameData)
        {
            namedAdventurerCache.Clear();
            normalAdventurerCache.Clear();
            tutorialAdventurerCache.Clear();
            dailyNormalVisitorPool.Clear();
            dailyNormalVisitorIndex = gameData.dailyNormalVisitorIndex;

            // 스폰 타이머 복원 - 저장값이 없거나 비정상이면 기존처럼 0에서 새 간격을 뽑는다
            if (gameData.nextAdventurerSpawnInterval > 0f)
            {
                lastAdventurerSpawnTime = Mathf.Max(0f, gameData.lastAdventurerSpawnTime);
                nextAdventurerSpawnInterval = gameData.nextAdventurerSpawnInterval;
            }

            if (gameData.namedAdventurerInstances != null && gameData.namedAdventurerInstances.Count > 0)
            {
                foreach (var saveData in gameData.namedAdventurerInstances)
                {
                    var inst = new AdventurerInstance(saveData);
                    if (inst.adventurerData != null && !IsTutorialOnlyAdventurer(inst.adventurerData.StaticID))
                    {
                        inst.defaultWeapon = RestoreDefaultWeapon(saveData.defaultWeaponType);
                        namedAdventurerCache[inst.instanceID] = inst;
                    }
                }
            }
            else
            {
                // 신규 게임: 저장 데이터 없이 전체 네임드 모험가를 초기화
                InitializeAllAdventurers();
            }

            if (gameData.normalAdventurerInstances != null)
            {
                foreach (var saveData in gameData.normalAdventurerInstances)
                {
                    var inst = new AdventurerInstance(saveData);
                    if (inst.adventurerData != null)
                    {
                        inst.defaultWeapon = RestoreDefaultWeapon(saveData.defaultWeaponType);
                        normalAdventurerCache[inst.instanceID] = inst;
                    }
                }
            }

            if (gameData.dailyNormalVisitorPoolSaveData != null)
            {
                foreach (var saveData in gameData.dailyNormalVisitorPoolSaveData)
                {
                    var inst = new AdventurerInstance(saveData);
                    if (inst.adventurerData != null)
                    {
                        inst.defaultWeapon = RestoreDefaultWeapon(saveData.defaultWeaponType);
                        dailyNormalVisitorPool.Add(inst);
                    }
                }
            }

            if (gameData.tutorialAdventurerInstances != null)
            {
                foreach (var saveData in gameData.tutorialAdventurerInstances)
                {
                    var inst = new AdventurerInstance(saveData);
                    if (inst.adventurerData != null)
                    {
                        inst.defaultWeapon = RestoreDefaultWeapon(saveData.defaultWeaponType);
                        tutorialAdventurerCache[inst.instanceID] = inst;
                    }
                }
            }

            RestoreVisitorsFromSaveData(gameData);
        }

        private void RestoreVisitorsFromSaveData(GameData gameData)
        {
            if (gameData.activeVisitorStates == null || gameData.activeVisitorStates.Count == 0)
                return;

            // 튜토리얼 진행 중이면 활성 방문자를 복원하지 않는다.
            // TutorialManager가 로드 직후 모든 방문자를 정리하고 해당 단계를 처음부터 재생하므로(RestoreTutorialAtStep)
            // 여기서 복원하면 낭비일 뿐 아니라, 단계 전환 스냅샷이 상호작용 중(isInteracting)에 찍힌 경우
            // 아래 StartInteraction()이 카메라를 줌인한 채로 남겨(그리고 팬텀 패널을 열어) 재생된 단계에서
            // 클릭 시 ZoomIn이 무시되는 문제가 생긴다.
            if (gameData.tutorialStep >= 1)
            {
                gameData.activeVisitorStates.Clear();
                return;
            }

            int currentDay = gameData.currentDay;

            // 상호작용 중이던 1명을 맨 앞으로(슬롯 확보 보장), 나머지는 remainingTime 오름차순.
            // → 앞 3명이 슬롯, 나머지가 대기열 순서로 결정적으로 재구성된다.
            var validStates = gameData.activeVisitorStates
                .Where(s => s.savedDay == currentDay)
                .OrderByDescending(s => s.isInteracting)
                .ThenBy(s => s.remainingTime)
                .ToList();

            VisitorNPC interactingNpc = null;
            foreach (var saveData in validStates)
                RestoreVisitor(saveData, ref interactingNpc);

            if (interactingNpc != null)
                interactingNpc.StartInteraction();

            gameData.activeVisitorStates.Clear();
        }

        private void RestoreVisitor(VisitorNPCSaveData saveData, ref VisitorNPC interactingNpc)
        {
            if (saveData.visitorType == VisitorType.EventNPC && MorningEventManager.Instance.IsEventCompleted)
                return;

            AdventurerInstance adventurerInstance = null;
            if (saveData.visitorType == VisitorType.Adventurer)
            {
                if (string.IsNullOrEmpty(saveData.adventurerInstanceID))
                {
                    Log.Warn("[VisitorManager] Adventurer 복원 실패: instanceID 없음");
                    return;
                }
                adventurerInstance = GetAdventurerInstance(saveData.adventurerInstanceID);
                if (adventurerInstance == null)
                {
                    Log.Warn($"[VisitorManager] Adventurer 복원 실패: 인스턴스 없음 ({saveData.adventurerInstanceID})");
                    return;
                }

                // 정상 스폰 경로(SpawnAdventurer)와 달리 복원 경로는 isVisiting을 세우지 않아
                // 로드 직후 같은 인스턴스가 다시 스폰 후보에 들어가는 문제 방지
                adventurerInstance.isVisiting = true;
            }
            else if (saveData.visitorType == VisitorType.DeadEvent && !string.IsNullOrEmpty(saveData.adventurerInstanceID))
            {
                // NoVisitor는 대상 모험가가 없을 수 있으므로 null이어도 진행
                adventurerInstance = GetAdventurerInstance(saveData.adventurerInstanceID);
            }

            if (saveData.visitorType == VisitorType.Blacksmith)
            {
                BlacksmithManager.Instance?.RestoreBlacksmith(saveData.blacksmithType, saveData.blacksmithIsPremium);
            }

            GameObject npcObj = Instantiate(VisitorPrefab, visitorParent);
            // 위치는 Initialize(skipEnterAnimation:true)의 슬롯/대기 배치로 즉시 덮어써진다.
            npcObj.transform.position = GetBottomRoadEntryPosition();

            VisitorEventData restoredEventData = null;
            if (saveData.visitorType == VisitorType.EventNPC && !string.IsNullOrEmpty(saveData.eventDataStaticID))
                restoredEventData = DataManager.Instance.GetVisitorEvent(saveData.eventDataStaticID);

            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();
            npc.Initialize(saveData.visitorType, adventurerInstance, restoredEventData, true);

            if (saveData.visitorType == VisitorType.WeaponShop)
                npc.ApplyAppearance(weaponShopAppearance);
            else if (saveData.visitorType == VisitorType.Blacksmith)
                npc.ApplyAppearance(BlacksmithManager.Instance?.CurrentBlacksmith?.appearance);
            else if (saveData.visitorType == VisitorType.EventNPC && restoredEventData?.appearance != null)
                npc.ApplyAppearance(restoredEventData.appearance);
            else if (saveData.visitorType == VisitorType.InvestorResult)
            {
                VisitorEventData investorEventData = DataManager.Instance.GetEventDataByType(MorningEventType.SuspiciousInvestor);
                npc.ApplyAppearance(investorEventData?.appearance);
                npc.InitializeInvestorResult(saveData.investorDialogueID, saveData.investorReturnedGold);
            }
            else if (saveData.visitorType == VisitorType.DeadEvent)
            {
                npc.deadEventKind = saveData.deadEventKind;
                switch (saveData.deadEventKind)
                {
                    case DeadEventKind.NoVisitor:
                        npc.ApplyAppearance(noVisitorAppearance);
                        break;
                    case DeadEventKind.ReviveOffer:
                        npc.ApplyAppearance(reviveOfferAppearance);
                        break;
                    case DeadEventKind.MiracleRevive:
                        npc.ApplyAppearance(adventurerInstance?.appearance);
                        break;
                }
            }

            npc.RestoreFromSaveData(saveData);

            RegisterVisitor(npc);

            if (saveData.isInteracting)
            {
                interactingNpc = npc;
            }
        }

        #endregion

        #region 모험가 대사

        /// <summary>
        /// 모험가 방문 대사를 상황(호감도 100 달성 / 첫 방문 / 호감도 단계)과 성별에 따라 랜덤으로 1개 반환한다.
        /// 풀에는 Dialogue 테이블 키가 들어 있고, 여기서 현재 언어로 조회한다.
        /// 해당 풀이 비어 있으면 fallback 키를 사용한다.
        /// </summary>
        public string GetAdventurerDialogue(AdventurerInstance adventurer)
        {
            if (adventurer == null) return string.Empty;

            Gender gender = adventurer.adventurerData != null ? adventurer.adventurerData.gender : Gender.Male;
            var stat = adventurer.adventurerStatData;
            bool maxReached = stat != null && stat.hasReachedMaxAffection;
            int visitCount = stat != null ? stat.visitCount : 0;

            GenderedDialogueLines pool;
            string fallback;

            if (maxReached)
            {
                pool = Config.maxAffectionReachedLines;
                fallback = "Adv_Fallback_MaxReached";
            }
            // OnInteractionButtonClicked에서 이번 방문이 이미 visitCount에 반영되므로,
            // 첫 방문은 이 메서드 시점에 visitCount가 1이다. 1 이하를 첫 방문으로 판정한다.
            // (일반 모험가는 재방문이 없어 visitCount가 최대 1이라 항상 여기로 온다.)
            else if (visitCount <= 1)
            {
                pool = Config.firstVisitLines;
                fallback = "Adv_Fallback_First";
            }
            else
            {
                switch (adventurer.GetAffectionLevel())
                {
                    case AffectionLevel.Max:
                        pool = Config.affectionMaxLines;
                        fallback = "Adv_Fallback_Max";
                        break;
                    case AffectionLevel.High:
                        pool = Config.affectionHighLines;
                        fallback = "Adv_Fallback_High";
                        break;
                    case AffectionLevel.Medium:
                        pool = Config.affectionMediumLines;
                        fallback = "Adv_Fallback_Medium";
                        break;
                    default:
                        pool = Config.affectionLowLines;
                        fallback = "Adv_Fallback_Low";
                        break;
                }
            }

            return PickRandomLine(pool?.Get(gender), fallback);
        }

        /// <param name="keys">Dialogue 테이블 키 목록.</param>
        /// <param name="fallbackKey">풀이 비었을 때 쓸 Dialogue 키.</param>
        private static string PickRandomLine(List<string> keys, string fallbackKey)
        {
            string key = (keys == null || keys.Count == 0)
                ? fallbackKey
                : keys[Random.Range(0, keys.Count)];
            return LocalizationSettings.StringDatabase
                .GetLocalizedString("Dialogue", key)
                .Replace("\\n", "\n");
        }

        #endregion

#if UNITY_EDITOR
        #region Debug & Test Methods

        [ContextMenu("Test: Spawn Adventurer")]
        public void TestSpawnAdventurer()
        {
            if (!TimeManager.Instance.IsDaytime())
            {
                UIPopupController.Instance.ShowToast("낮 시간이 아닙니다. 9:00~18:00에 스폰 가능합니다.", type: PopupSfxType.Warning);
                return;
            }

            SpawnNewAdventurer();
            Log.Info("[VisitorManager] 테스트 모험가 스폰 완료");
        }

        [ContextMenu("Test: Spawn WeaponShop")]
        public void TestSpawnWeaponShop()
        {
            SpawnWeaponShop();
            Log.Info("[VisitorManager] 테스트 상인 스폰 완료");
        }

        [ContextMenu("Test: Spawn Blacksmith")]
        public void TestSpawnBlacksmith()
        {
            SpawnBlacksmith();
            Log.Info("[VisitorManager] 테스트 대장장이 스폰 완료");
        }

        [ContextMenu("Test: Spawn Event NPC (Random)")]
        public void TestSpawnEventNPC()
        {
            SpawnEventNPC();
            Log.Info("[VisitorManager] 테스트 이벤트 NPC 스폰 완료 (랜덤)");
        }

        [ContextMenu("Test: Spawn WeaponEnhance NPC")]
        public void TestSpawnWeaponEnhanceNPC()
        {
            SpawnEventNPC(MorningEventType.WeaponEnhance);
        }

        [ContextMenu("Test: Spawn WeaponExchange NPC")]
        public void TestSpawnWeaponExchangeNPC()
        {
            SpawnEventNPC(MorningEventType.WeaponExchange);
        }

        [ContextMenu("Test: Spawn SuspiciousInvestor NPC")]
        public void TestSpawnSuspiciousInvestorNPC()
        {
            SpawnEventNPC(MorningEventType.SuspiciousInvestor);
        }

        [ContextMenu("Test: Spawn WanderingBlacksmith NPC")]
        public void TestSpawnWanderingBlacksmithNPC()
        {
            SpawnEventNPC(MorningEventType.WanderingBlacksmith);
        }

        [ContextMenu("Test: Spawn GuildEnvoy NPC")]
        public void TestSpawnGuildEnvoyNPC()
        {
            SpawnEventNPC(MorningEventType.GuildEnvoy);
        }

        [ContextMenu("Test: Spawn MysteryBox NPC")]
        public void TestSpawnMysteryBoxNPC()
        {
            SpawnEventNPC(MorningEventType.MysteryBox);
        }

        [ContextMenu("Test: Spawn RefugeeHelp NPC")]
        public void TestSpawnRefugeeHelpNPC()
        {
            SpawnEventNPC(MorningEventType.RefugeeHelp);
        }

        [ContextMenu("Test: Spawn Collector NPC")]
        public void TestSpawnCollectorNPC()
        {
            SpawnEventNPC(MorningEventType.Collector);
        }

        [ContextMenu("Test: Spawn BlackMarket NPC")]
        public void TestSpawnBlackMarketNPC()
        {
            SpawnEventNPC(MorningEventType.BlackMarket);
        }

        [ContextMenu("Test: Kill Named Adventurer")]
        public void TestKillNamedAdventurer()
        {
            var alive = namedAdventurerCache.Values.FirstOrDefault(a => a.isAlive);
            if (alive == null)
            {
                UIPopupController.Instance?.ShowToast("살아있는 네임드 모험가가 없습니다.", type: PopupSfxType.Warning);
                return;
            }
            MarkAdventurerDead(alive.instanceID);
            UIPopupController.Instance?.ShowToast($"{alive.Name} 사망 처리");
        }

        public void TestSpawnDeadEventNPC(DeadEventKind kind)
        {
            var dead = namedAdventurerCache.Values.FirstOrDefault(a => !a.isAlive);
            if (dead == null && kind != DeadEventKind.NoVisitor)
            {
                UIPopupController.Instance?.ShowToast("죽은 네임드 모험가가 없습니다. 먼저 죽이세요.", type: PopupSfxType.Warning);
                return;
            }
            SpawnDeadEventNPC(kind, dead);
            Log.Info($"[VisitorManager] 테스트 사망 이벤트 NPC 스폰: {kind}");
        }

        [ContextMenu("Clear All Visitors")]
        public void DebugClearAllVisitors()
        {
            foreach (var visitor in activeVisitors.ToList())
            {
                RemoveVisitor(visitor);
            }
            Log.Info("[VisitorManager] 모든 방문자 제거 요청");
        }

        #endregion
#endif
    }
}
