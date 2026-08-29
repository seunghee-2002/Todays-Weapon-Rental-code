using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace TodaysWeaponRental
{
    [DefaultExecutionOrder(-100)]   // 매니저 부팅 토대: GameManager.Start()가 다른 매니저들의 Start()보다 먼저 실행되도록 보장 (Documents/오늘도장비대여/Development/아키텍처.md 참조)
    public class GameManager : BaseManager<GameManager>
    {
        [Header("References")]
        [SerializeField] private GameData gameData;
        
        public GameData GameData => gameData;
        public bool IsGameActive { get; private set; }

        // 메인 메뉴 "새 게임" 클릭과 InGameScene 진입 사이에서 의도를 보존하기 위한 플래그.
        // CloudSyncService가 비동기로 cloud 데이터를 복원해도 새 게임 분기를 강제하기 위해 사용한다.
        private const string PendingNewGameKey = "PendingNewGame";

        public static void MarkPendingNewGame()
        {
            PlayerPrefs.SetInt(PendingNewGameKey, 1);
            PlayerPrefs.Save();
        }

        public static void ClearPendingNewGame()
        {
            PlayerPrefs.SetInt(PendingNewGameKey, 0);
            PlayerPrefs.Save();
        }

        #region 초기화

        private void Start()
        {
            // 이것이 첫 번째 씬 로드인 경우 게임 초기화
            // (씬 재로드는 OnSceneLoaded가 처리함)
            if (SceneManager.GetActiveScene().name == SceneController.GAME_SCENE)
            {
                InitializeGame();
            }
        }

        private void InitializeGame()
        {
            Log.Info("[GameManager] InitializeGame() called");

            // 이전 게임의 방문객 초기화
            VisitorManager.Instance?.ClearAllVisitors();

            // 플레이어 데이터 로드 또는 생성
            // PendingNewGame 플래그가 있으면 cloud sync가 gamedata.json을 복원했더라도 새 게임 분기를 강제한다.
            bool pendingNewGame = PlayerPrefs.GetInt(PendingNewGameKey, 0) == 1;
            if (pendingNewGame)
            {
                PlayerPrefs.SetInt(PendingNewGameKey, 0);
                PlayerPrefs.Save();
                SaveManager.DeleteGameData();
            }
            bool isNewGame = pendingNewGame || !SaveManager.HasGameData();
            if (isNewGame)
            {
                CreateNewGame();
            }
            else if (!LoadGame())
            {
                // 손상 세이브 (본 파일/백업 모두 복구 실패) - 회차를 조용히 버리거나 크래시하지 않고
                // 메인 메뉴로 복귀한다. 메인 메뉴의 클라우드 동기화가 복구 기회를 제공한다
                Log.Error("[GameManager] 세이브 로드 실패 - 메인 메뉴로 복귀");
                SceneController.Instance?.LoadMainMenu();
                return;
            }

            // 모든 매니저 초기화
            InitializeManagers();

            // 이어하기 로드 직후 참조 무결성 복구 (드롭된 모험 잠금 해제, 고아 배정 아이템 반환 등)
            if (!isNewGame)
                RepairLoadedGameData();

            // 퀘스트 마감 재판정(결과창 재오픈 가능)은 전 매니저 초기화와 참조 복구가 끝난 뒤에 수행한다.
            // 결과창이 복구 전 상태 위에 뜨는 것을 막는다.
            QuestManager.Instance?.CheckDeadlineAfterLoad();

            // 신규 게임 전용: 무기 지급은 InventoryManager 초기화 이후에 적용
            if (isNewGame)
                ApplyStartingWeaponBonuses();

            // 신규 게임의 첫 저장은 매니저 초기화 후에 수행해야
            // 매니저들의 초기화된 상태가 gameData에 정확히 반영된다.
            if (isNewGame)
                SaveGame();

            IsGameActive = true;

            AnalyticsManager.Instance?.Send("game_start", new Dictionary<string, object>
            {
                { "is_new_game", isNewGame }
            });
            // 1일차 day_begin은 GoToNextDay를 거치지 않으므로 여기서 발행 (이어하기는 제외 — 같은 날 중복 방지)
            if (isNewGame)
                AnalyticsManager.Instance?.SendDayBegin();
        }

        // 플레이타임 누적 (1초 단위로만 GameData에 반영)
        private float playtimeBuffer;

        private void Update()
        {
            if (!IsGameActive || gameData == null) return;

            playtimeBuffer += Time.unscaledDeltaTime;
            if (playtimeBuffer >= 1f)
            {
                int seconds = (int)playtimeBuffer;
                gameData.totalPlaytimeSec += seconds;
                playtimeBuffer -= seconds;
            }
        }

        private void InitializeManagers()
        {
            if (!DataManager.Instance.IsInitialized)
            {
                Log.Error("DataManager not initialized!");
                return;
            }

            NicknameManager.Instance.Initialize(gameData);
            BanManager.Instance?.Initialize(gameData);
            TimeManager.Instance.Initialize(gameData);
            EconomyManager.Instance.Initialize(gameData);
            DialogueManager.Instance.Initialize(gameData);
            InventoryManager.Instance.Initialize(gameData);
            ReputationManager.Instance.Initialize(gameData);
            InsightManager.Instance.Initialize(gameData);
            QuestBoardManager.Instance.Initialize(gameData);
            SeerManager.Instance.Initialize(gameData);
            ScoutManager.Instance.Initialize(gameData);
            VisitorManager.Instance.Initialize(gameData);
            AdventureManager.Instance.Initialize(gameData);
            WeaponShopManager.Instance.Initialize(gameData);
            ActiveItemManager.Instance.Initialize(gameData);
            BlacksmithManager.Instance.Initialize(gameData);
            QuestManager.Instance.Initialize(gameData);  
            MorningEventManager.Instance.Initialize(gameData);
            SoundManager.Instance.Initialize(gameData);
            LeaderboardManager.Instance.Initialize(gameData);
            TutorialManager.Instance.Initialize(gameData);
        }

        #endregion

        #region 게임관리

        public void CreateNewGame()
        {
            Log.Info("[GameManager] CreateNewGame() started");

            // 1. 새 플레이어 데이터 생성
            gameData = new GameData();

            // 2. 새로운 게임 상태로 재설정
            gameData.ResetForNewGame();

            // 3. 유산 시작 보너스 적용
            ApplyLegacyStartingBonuses(gameData);

            // 네임드 모험가 캐시는 여기서 만들지 않는다.
            // InitializeManagers() -> VisitorManager.Initialize -> LoadFromGameData가
            // 캐시를 비운 뒤 저장 데이터가 없으면(=신규 게임) InitializeAllAdventurers()를 직접 호출한다.

            // 초기 SaveGame은 InitializeGame()이 InitializeManagers() 이후에 호출한다.
            // (매니저 초기화 이전 시점의 빈/디폴트 상태로 gameData를 덮어쓰지 않도록.)

            // 리더보드 세션 시작 (서버에서 세션 토큰 발급)
            _ = LeaderboardManager.Instance?.StartGameSessionAsync();

            Log.Info("[CreateNewGame] Completed!");
        }

        public bool LoadGame()
        {
            gameData = SaveManager.LoadGame();
            if (gameData == null)
            {
                Log.Error("[GameManager] LoadGame: 세이브 파일 로드 실패 (손상/백업 복구 불가)");
                return false;
            }

            Log.Info($"게임 로드됨. 일: {gameData.currentDay}, 골드: {gameData.gold}");

            if (gameData.namedAdventurerInstances == null)
            {
                Log.Error("LoadGame: activeAdventurerInstances가 NULL입니다!");
            }
            return true;
        }

        /// <summary>
        /// 이어하기 로드 직후 1회 실행하는 참조 무결성 복구 단계 (sanity pass).
        /// 복원 실패로 드롭된 모험의 모험가/무기 잠금 해제, 고아 배정 아이템 반환 등을 수행한다
        /// </summary>
        private void RepairLoadedGameData()
        {
            AdventureManager.Instance?.RepairDroppedAdventureReferences();
            ActiveItemManager.Instance?.RepairAssignmentsAfterLoad();
        }

        /// <summary>
        /// 게임 저장. 튜토리얼 단계 진행 중(tutorialStep >= 1)에는 mid-step 저장을 건너뛰고
        /// 단계 진입 스냅샷(force: true)만 기록한다. 불러오면 진행 중이던 단계의 진입 상태로 복원된다.
        /// </summary>
        public bool SaveGame(bool force = false)
        {
            if (gameData == null)
            {
                Log.Error("저장할 수 없음: PlayerData가 null입니다");
                return false;
            }

            // 튜토리얼 진행 중 자동/수동 저장(OnApplicationPause/Quit, 옵션 저장 등)은 억제한다.
            // 단계 진입 스냅샷(TutorialManager)만 force로 통과시켜 gamedata.json이 항상 "단계 진입 상태"를 갖게 한다.
            if (!force && gameData.tutorialStep >= 1)
            {
                Log.Info("[GameManager] 튜토리얼 진행 중 — mid-step 저장 건너뜀");
                return false;
            }

            SyncManagersToGameData();

            long previousSavedAt = gameData.lastSavedAt;
            gameData.lastSavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (!SaveManager.SaveGame(gameData))
            {
                // 파일 저장 실패 시 lastSavedAt을 되돌리고 클라우드 업로드도 보류한다
                gameData.lastSavedAt = previousSavedAt;
                Log.Error("[GameManager] 게임 저장 실패 - 클라우드 업로드 보류");
                UIPopupController.Instance?.ShowPopup(
                    LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Save_Failed"),
                    type: PopupSfxType.Warning);
                return false;
            }

            // 사운드 설정(볼륨/뮤트)을 PlayerData에 동기화 후, 영구 데이터를 로컬 flush (legacy.json)
            SoundManager.Instance?.SyncSettingsToPlayerData();
            LegacyManager.Instance?.SaveLegacy();

            _ = CloudSaveManager.Instance?.UploadAsync(gameData, LegacyManager.Instance?.GetLegacyData());
            return true;
        }

        /// <summary>
        /// 도박성 결과(강화/상자/투자/구매/모험 출발·완료 등)가 확정된 직후 호출하는 저장 규약.
        /// 강제 종료로 결과를 되돌리는 세이브스컴을 차단한다
        /// </summary>
        public bool SaveAfterCommittedAction(string reason)
        {
            Log.Info($"[GameManager] committed action save: {reason}");
            return SaveGame();
        }

        private void SyncManagersToGameData()
        {
            // 각 매니저의 현재 상태를 PlayerData에 저장
            TimeManager.Instance?.SaveToGameData(gameData);
            ActiveItemManager.Instance?.SaveToGameData(gameData);
            InventoryManager.Instance?.SaveToGameData(gameData);
            AdventureManager.Instance?.SaveToGameData(gameData);
            VisitorManager.Instance?.SaveToGameData(gameData);
            QuestManager.Instance?.SaveToGameData(gameData);
            WeaponShopManager.Instance?.SaveToGameData(gameData);
            SeerManager.Instance?.SaveToGameData(gameData);
            ScoutManager.Instance?.SaveToGameData(gameData);
            NicknameManager.Instance?.SaveToGameData(gameData);
            // 4. 기타 매니저들...
        }

        public void OnGameOver()
        {
            IsGameActive = false;
            TimeManager.Instance?.PauseTime();

            AnalyticsManager.Instance?.Send("game_over", new Dictionary<string, object>
            {
                { "gold", gameData.gold },
                { "reputation", ReputationManager.Instance?.CurrentReputation ?? 0 },
                { "reputation_level", (ReputationManager.Instance?.CurrentLevel ?? ReputationLevel.Bronze).ToString() },
                { "cause", "bankruptcy" },
                { "total_playtime_sec", gameData.totalPlaytimeSec }
            });

            // 게임오버 직후 앱을 끄는 경우가 많아 60초 주기를 기다리지 않고 즉시 올린다
            AnalyticsManager.Instance?.Flush();

            int earned       = 0;
            int previousTotal = LegacyManager.Instance?.LegacyPoints ?? 0;

            if (LegacyManager.Instance != null)
            {
                earned = LegacyManager.Instance.CalculateEarnedLegacyPoints(gameData);
                LegacyManager.Instance.AddLegacyPoints(earned, "game_over");
                LegacyManager.Instance.MarkGameOver();
                LegacyManager.Instance.SaveLegacy();
                Log.Info($"[GameManager] 게임오버. 유산 {earned} 획득. 총 누적: {LegacyManager.Instance.LegacyPoints}");
            }

            SaveManager.DeleteGameData();
            _ = FinalizeGameOverAsync(gameData.currentDay);

            int days      = gameData.currentDay;
            int rep       = gameData.totalCumulativeReputation;
            int adv       = gameData.totalSuccessfulAdventures + gameData.totalFailedAdventures;
            UIManager.Instance?.OpenPanel<GameOverPopupView>(() =>
                UIControllerManager.Instance?.GetController<GameOverPopupController>()
                    ?.InitializeResult(days, rep, adv, earned, previousTotal));
        }

        /// <summary>
        /// 게임오버 후속 처리를 명시적 순서로 수행한다.
        /// 점수 제출을 클라우드 clear보다 먼저 수행한다 - 서버(submitLeaderboardScore)가
        /// 클라우드 playerData.currentDay와 교차 검증하므로 clear가 먼저면 검증 데이터가 사라진다.
        /// 각 단계는 실패 시 자체 pending 플래그(pending score, pendingCloudClear)로 재시도된다.
        /// </summary>
        private async System.Threading.Tasks.Task FinalizeGameOverAsync(int finalDays)
        {
            try
            {
                if (LeaderboardManager.Instance != null)
                    await LeaderboardManager.Instance.SubmitScoreAsync(finalDays);
            }
            catch (Exception e)
            {
                Log.Warn($"[GameManager] 게임오버 점수 제출 실패 (pending 재시도 예정): {e.Message}");
            }

            try
            {
                if (CloudSaveManager.Instance != null)
                    await CloudSaveManager.Instance.ClearCloudGameDataAsync();
            }
            catch (Exception e)
            {
                Log.Warn($"[GameManager] 게임오버 클라우드 정리 실패 (pending 재시도 예정): {e.Message}");
            }
        }

        #endregion

        private void ApplyLegacyStartingBonuses(GameData data)
        {
            if (LegacyManager.Instance == null) return;

            int goldBonus    = LegacyManager.Instance.GetStartingGoldBonus();
            int insightBonus = LegacyManager.Instance.GetStartingInsightBonus();

            data.gold          += goldBonus;
            data.playerInsight += insightBonus;

            Log.Info($"[GameManager] 유산 시작 보너스: 골드+{goldBonus}, 통찰+{insightBonus}");
        }

        // InitializeManagers() 이후 호출 — InventoryManager가 초기화된 뒤에 무기 지급
        private void ApplyStartingWeaponBonuses()
        {
            if (LegacyManager.Instance == null) return;

            if (LegacyManager.Instance.HasWeaponRare())
                AddStartingWeapon(Grade.Rare);

            if (LegacyManager.Instance.HasWeaponEpic())
                AddStartingWeapon(Grade.Epic);
        }

        private void AddStartingWeapon(Grade grade)
        {
            var pool = DataManager.Instance.GetWeaponsByGrade(grade);
            if (pool == null || pool.Count == 0)
            {
                Log.Warn($"[GameManager] 시작 무기 풀 없음: {grade}");
                return;
            }

            var weaponData = pool[UnityEngine.Random.Range(0, pool.Count)];
            var instance   = new WeaponInstance(weaponData);
            InventoryManager.Instance.AddWeapon(instance);
            Log.Info($"[GameManager] 유산 시작 무기 지급: {weaponData.weaponName} ({grade})");
        }

        private void OnApplicationQuit()
        {
            // 게임이 활성 상태일 때만 저장
            if (IsGameActive)
            {
                SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // 앱이 일시정지될 때 게임 저장
            if (pauseStatus && IsGameActive)
            {
                SaveGame();
            }
        }
    }
}
