// Scripts/Data/GameData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    [Serializable]
    public class GameData
    {
        [Header("클라우드 동기화")]
        public long lastSavedAt = 0;

        [Header("기본 정보")]
        public int currentDay = 1;
        public int currentHour = 6;
        public int currentMinute = 0;
        public bool isUserPaused = false;
        public float currentTimeScale = 1f;
        public int gold = 5000;
        public int reputation = 0;
        public int totalCumulativeReputation = 0;
        public int playerInsight = 0;
        // 이번 회차 누적 실플레이 시간(초). Analytics game_over.total_playtime_sec용
        public int totalPlaytimeSec = 0;

        [Header("일회용 체크")]
        public bool hasReceivedTutorialWeapons = false; // 튜토리얼 아이템을 받았는지(=1단계/튜토리얼 완료 플래그, 회차별)
        public int tutorialStep = 0; // 진행 중인 튜토리얼 단계 (0=미시작, 1=무기상점 단계). 중도 복원용
        public int tutorialOutcomeAppliedThrough = 0; // 스킵/점프 catch-up으로 결과물을 적용 완료한 최상위 단계(중복 적용 방지)
        public bool hasClaimedTutorialGuildGift = false; // 3단계 길드 사절단 선물 수령 여부(catch-up 중복 지급 방지)
        public bool hasGrantedTutorialBlacksmithMaterials = false; // 2단계 대장장이 재료 지급 여부(catch-up 중복 지급 방지)

        [Header("평판 시스템")]
        public int totalSuccessfulAdventures = 0;
        public int totalFailedAdventures = 0;
        public int totalBonusMatches = 0;

        [Header("인벤토리")]
        public List<WeaponInstanceSaveData> ownedWeapons = new List<WeaponInstanceSaveData>();
        public List<MaterialInstanceSaveData> ownedMaterials = new List<MaterialInstanceSaveData>();
        public List<ActiveItemInstanceSaveData> ownedActiveItems = new List<ActiveItemInstanceSaveData>();

        [Header("액티브 아이템 배정 상태")]
        public List<ActiveItemAssignmentSaveData> pendingAssignedItems = new List<ActiveItemAssignmentSaveData>();
        // 대장간 슬롯에 배정된 강화석 (null/빈 문자열 = 없음). 저장 시 인벤 회수 대신 상태 그대로 직렬화
        public string pendingBlacksmithItemDataID = null;

        [Header("모험가 데이터")]
        public List<AdventurerInstanceSaveData> namedAdventurerInstances = new List<AdventurerInstanceSaveData>();
        public List<AdventurerInstanceSaveData> normalAdventurerInstances = new List<AdventurerInstanceSaveData>();
        public List<AdventurerInstanceSaveData> dailyNormalVisitorPoolSaveData = new List<AdventurerInstanceSaveData>();
        public int dailyNormalVisitorIndex = 0;
        // 튜토리얼 전용 모험가 - 네임드지만 스폰 풀(namedAdventurerInstances)에서 제외되므로 별도 저장한다.
        // 이게 없으면 튜토리얼 중 강제종료 시 진행 중 모험이 모험가를 찾지 못해 통째로 드롭된다
        public List<AdventurerInstanceSaveData> tutorialAdventurerInstances = new List<AdventurerInstanceSaveData>();

        [Header("방문자 상태")]
        public List<VisitorNPCSaveData> activeVisitorStates = new List<VisitorNPCSaveData>();
        public List<WeaponShopItemSaveData> savedShopItems = new List<WeaponShopItemSaveData>();
        public int savedShopRefreshCount = 0;
        public int lastWeaponShopDay = 0;
        public bool hasSpawnedMorningNPCs = false;
        // 모험가 스폰 타이머 (낮 시간 저장/재접속 시 스폰 간격 유지용, 0 이하면 새 간격 추첨)
        public float lastAdventurerSpawnTime = 0f;
        public float nextAdventurerSpawnInterval = 0f;

        [Header("모험 데이터")]
        public List<AdventureInstanceSaveData> ongoingAdventures = new List<AdventureInstanceSaveData>();
        public List<AdventureInstanceSaveData> completedAdventures = new List<AdventureInstanceSaveData>();
        public List<AdventureResult> completedAdventureResults = new List<AdventureResult>();
        public QuestBoardSaveData dailyQuestBoardData = new QuestBoardSaveData();

        [Header("전령 보고")]
        // 저녁 6시 전령이 아직 보고하지 않은 모험 instanceID (오래된 순). 18:00 시점 스냅샷이며 이후 완료분은 다음날로 이월된다.
        public List<string> heraldPendingAdventureIDs = new List<string>();
        // 오늘 18:00 스냅샷을 이미 떴는지 (날짜 변경 시 초기화)
        public bool heraldReportStartedToday = false;

        [Header("점술가 데이터")]
        // 키: "instanceID_dungeonStaticID", 값: 해당 상담 결과 (날짜 변경 시 초기화)
        public SerializableDictionary<string, SeerResult> seerResults = new();

        [Header("수색꾼 데이터")]
        // 오늘의 수색 임무 목록 (날짜 변경 시 초기화)
        public List<ScoutMission> scoutMissions = new List<ScoutMission>();

        [Header("이벤트 NPC")]
        // 오늘 모닝 이벤트 상호작용 완료 여부 (날짜 변경 시 초기화)
        public bool morningEventCompleted;
        // 오늘 등장한 수수께끼 상자 등급 (-1=미결정, 날짜 변경 시 초기화)
        public int mysteryBoxTier = -1;
        // 오늘 암시장 제안 무기 (null=없음, 패널 열릴 때 저장·떠날 때 초기화)
        public WeaponInstanceSaveData blackMarketOfferSaveData = null;
        // 투자 결과 (투자 즉시 확정, 다음날 Morning에 NPC 소환 후 소비)
        // pendingInvestorReturnedGold: 0=먹튀(NPC없음), >0=성공(NPC등장)
        // lastInvestorDay: 투자한 날짜 (OnDayChanged에서 2일 경과 시 자동 초기화 판단용)
        public bool hasPendingInvestment;
        public int pendingInvestorReturnedGold;
        public string pendingInvestorResultDialogueID;
        public int lastInvestorDay = -1;

        [Header("대장장이 데이터")]
        public BlacksmithType lastRequestedBlacksmithType = BlacksmithType.None;

        [Header("퀘스트 관련")]
        public int currentWeek = 1;
        public int[] currentQuestProgress = null;
        public int questStartDay = 1;
        public int totalFinePaid = 0;
        // 현재 주간 퀘스트 상태 (재접속 시 완료/실패 상태 복원용)
        public QuestStatus currentQuestStatus = QuestStatus.Active;
        // 엔드리스 구간 추첨 결과. 캠페인 구간에서는 비어 있고 주차로 조회한다.
        // 추첨은 회차마다 달라야 하므로 시드 재현이 아니라 결과를 저장해 복원한다.
        public string currentEndlessQuestID = null;
        // 직전에 나온 엔드리스 템플릿들 (최근 것이 뒤). 연속 중복 회피용
        public List<string> recentEndlessQuestIDs = new List<string>();

        public void ResetForNewGame()
        {
            // 기본 정보
            currentDay = 1;
            currentHour = 6;
            currentMinute = 0;
            isUserPaused = false;
            currentTimeScale = 1f;
            gold = 5000;
            reputation = 0;
            totalCumulativeReputation = 0;
            playerInsight = 0;
            totalPlaytimeSec = 0;

            // 사운드 설정은 보존 (플레이어 환경 설정)

            // 일회용 체크
            hasReceivedTutorialWeapons = false;
            tutorialStep = 0;
            tutorialOutcomeAppliedThrough = 0;
            hasClaimedTutorialGuildGift = false;
            hasGrantedTutorialBlacksmithMaterials = false;

            // 평판 시스템
            totalSuccessfulAdventures = 0;
            totalFailedAdventures = 0;
            totalBonusMatches = 0;

            // 인벤토리
            ownedWeapons.Clear();
            ownedMaterials.Clear();
            ownedActiveItems.Clear();
            pendingAssignedItems.Clear();
            pendingBlacksmithItemDataID = null;

            // 모험가 데이터
            namedAdventurerInstances.Clear();
            normalAdventurerInstances.Clear();
            dailyNormalVisitorPoolSaveData.Clear();
            dailyNormalVisitorIndex = 0;
            tutorialAdventurerInstances.Clear();

            // 방문자 상태
            activeVisitorStates.Clear();
            savedShopItems.Clear();
            savedShopRefreshCount = 0;
            lastWeaponShopDay = 0;
            hasSpawnedMorningNPCs = false;
            lastAdventurerSpawnTime = 0f;
            nextAdventurerSpawnInterval = 0f;

            // 모험 데이터
            ongoingAdventures.Clear();
            completedAdventures.Clear();
            completedAdventureResults.Clear();
            dailyQuestBoardData = new QuestBoardSaveData();

            // 전령 보고
            heraldPendingAdventureIDs.Clear();
            heraldReportStartedToday = false;

            // 점술가 데이터
            seerResults.Clear();

            // 수색꾼 데이터
            scoutMissions.Clear();

            // 이벤트 NPC
            morningEventCompleted = false;
            mysteryBoxTier = -1;
            blackMarketOfferSaveData = null;
            hasPendingInvestment = false;
            pendingInvestorReturnedGold = 0;
            pendingInvestorResultDialogueID = null;
            lastInvestorDay = -1;

            // 대장장이 데이터
            lastRequestedBlacksmithType = BlacksmithType.None;

            // 퀘스트 관련
            currentWeek = 1;
            currentQuestProgress = null;
            questStartDay = 1;
            totalFinePaid = 0;
            currentQuestStatus = QuestStatus.Active;
            currentEndlessQuestID = null;
            recentEndlessQuestIDs = new List<string>();
        }
    }
}
