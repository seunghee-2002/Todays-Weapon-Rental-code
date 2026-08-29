using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using System.Threading.Tasks;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 메인 메뉴 컨트롤러
    /// </summary>
    // Start()에서 등급표시 패널을 여는데, UIManager.panelParent는 씬 인스펙터가 아니라
    // UISceneRoot가 런타임에 주입한다. UISceneRoot.Awake는 UIManager.Instance가 아직 null이라
    // 실패하고 Start에서야 주입에 성공하는데, 기본 실행 순서로는 이 Start가 UISceneRoot.Start보다
    // 먼저 돌아 패널이 부모 없이 Instantiate된다(= 화면에서 사라짐). 실행 순서를 뒤로 밀어 방지한다.
    [DefaultExecutionOrder(10)]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MainMenuView view;
        [SerializeField] private CloudSyncService cloudSyncService;

        // 게임 소개 페이지(세계관/시스템 설명/공략). 외부 브라우저로 연다.
        private const string GuideUrl = "https://seunghee-2002.github.io/todays-weapon-rental/";

        // 등급표시는 앱 실행당 1회만 노출한다. 게임오버/옵션 등으로 메인 메뉴에 복귀할 때마다
        // 3초를 다시 기다리지 않도록 static으로 세션 전체를 기억한다.
        private static bool ratingNoticeShown;

        // 강제 업데이트 팝업 중복 표시 방지 (캐시 선판정 + 서버 응답이 둘 다 도착하는 경우)
        private bool updatePopupShown;

        // 현재 떠 있는 강제 업데이트 팝업을 닫는 함수. 재검사로 차단이 풀렸을 때 사용한다.
        private Action closeUpdatePopup;

        // 강제 업데이트 안내를 띄우기까지 갖춰져야 하는 신호들.
        // 판정 확정 전에 띄우면 서버가 통과라고 답할 때 팝업이 떴다 닫히고,
        // 등급표시 중에 띄우면 법정 노출 시간을 팝업이 가린다.
        private bool updateVerdictDone;
        private bool ratingNoticeDone;

        // 메뉴 노출 조건. 클라우드 동기화와 버전 판정이 모두 끝나야 연다.
        // 판정 전에 열면 캐시가 차단 상태인데도 새 게임으로 진입할 수 있다.
        private bool cloudSyncDone;

        // 버전 판정 대기 상한. 로그인 자체가 실패해 서버 검사가 시작조차 못 한 경우에만 여기까지 온다.
        private const float UpdateVerdictTimeoutSeconds = 5f;

        // Enter Play Mode(도메인 리로드 off) 대비 정적 상태 초기화.
        // 없으면 에디터에서 2회차 Play부터 등급표시가 통째로 건너뛰어진다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ratingNoticeShown = false;
        }

        // 동기화 완료 구독은 Awake에서 한다. CloudSyncService.Start()는 오프라인일 때 첫 yield 전에
        // CompleteSync()로 OnSyncCompleted를 즉시 발행하는데, 이 클래스는 실행 순서가 뒤로 밀려 있어
        // Start에서 구독하면 그 이벤트를 놓친다.
        private void Awake()
        {
            if (cloudSyncService != null)
                cloudSyncService.OnSyncCompleted += OnCloudSyncCompleted;
        }

        private void Start()
        {
            InitializeMenu();

            ShowRatingNoticeThenTerms();

            // 밴 상태 안내: UGSManager 가 로그인 직후 checkBanStatus 를 비동기로 호출한다.
            // 본 시점에 이미 결과가 도착했을 수도(빠른 응답), 아직일 수도 있으므로 둘 다 처리한다.
            if (BanManager.Instance != null)
            {
                BanManager.Instance.OnBanDetected += HandleBanDetected;
                BanManager.Instance.OnBanCleared += HandleBanCleared;
                if (BanManager.Instance.IsBanned)
                    HandleBanDetected(BanManager.Instance.CurrentBan);
            }

            // 강제 업데이트 안내: 판정이 확정될 때까지 게이트가 표시를 미룬다.
            // 이벤트 구독은 게이트를 넘긴 뒤 뒤늦게 상태가 바뀌는 경우를 위한 것이다.
            if (AppUpdateManager.Instance != null)
            {
                AppUpdateManager.Instance.OnUpdateRequired += TryShowForceUpdatePopup;
                AppUpdateManager.Instance.OnUpdateCleared += HandleUpdateCleared;
            }
            StartCoroutine(UpdateVerdictGateRoutine());
        }

        private void OnDestroy()
        {
            if (cloudSyncService != null)
                cloudSyncService.OnSyncCompleted -= OnCloudSyncCompleted;

            if (BanManager.Instance != null)
            {
                BanManager.Instance.OnBanDetected -= HandleBanDetected;
                BanManager.Instance.OnBanCleared -= HandleBanCleared;
            }

            if (AppUpdateManager.Instance != null)
            {
                AppUpdateManager.Instance.OnUpdateRequired -= TryShowForceUpdatePopup;
                AppUpdateManager.Instance.OnUpdateCleared -= HandleUpdateCleared;
            }
        }

        private void HandleBanDetected(BanInfo ban) => ShowBanPopup(ban);

        private void HandleBanCleared()
        {
            UIPopupController.Instance?.ShowPopup(
                L("MainMenu_BanLifted"),
                type: PopupSfxType.Notify);
        }

        #region 초기화

        private void InitializeMenu()
        {
            // 메인 메뉴에 도달한 시점에는 새 게임 의도가 진행 중이지 않다.
            // 이전 세션의 await 도중 강제 종료 등으로 남았을 수 있는 PendingNewGame 플래그를 정리한다.
            GameManager.ClearPendingNewGame();

            if (view == null)
            {
                view = GetComponent<MainMenuView>();
                if (view == null)
                {
                    Log.Error("[MainMenuController] MainMenuView를 찾을 수 없습니다.");
                    return;
                }
            }

            view.NewGameButton?.onClick.AddListener(OnNewGameClicked);
            view.LoadGameButton?.onClick.AddListener(OnLoadGameClicked);
            view.LeaderboardButton?.onClick.AddListener(OnLeaderboardClicked);
            view.UpgradeButton?.onClick.AddListener(OnUpgradeClicked);
            view.OptionButton?.onClick.AddListener(OnOptionClicked);
            view.TermsOfServiceButton?.onClick.AddListener(OnTermsOfServiceClicked);
            view.PrivacyPolicyButton?.onClick.AddListener(OnPrivacyPolicyClicked);
            view.GuideButton?.onClick.AddListener(OnGuideClicked);

            UpdateMenuUI();

            // 동기화 서비스가 없거나 이미 끝났으면 그쪽 조건은 충족된 것으로 본다.
            // (null 참조를 처리하지 않으면 메뉴가 영영 열리지 않는다)
            if (cloudSyncService == null || cloudSyncService.IsSyncCompleted)
            {
                cloudSyncDone = true;
                TryOpenMenu();
            }
        }

        // 클라우드 동기화와 버전 판정이 모두 끝나야 메뉴를 연다.
        // 어느 한쪽만 보고 열면, 판정이 아직인 사이에 구버전으로 게임에 진입할 수 있다.
        private void TryOpenMenu()
        {
            if (!cloudSyncDone || !updateVerdictDone) return;

            view?.SetSyncing(false);
        }

        // 강제 업데이트 판정이 확정될 때까지 안내를 미루는 게이트.
        // 오프라인이면 AppUpdateManager가 Awake에서 이미 확정해 두므로 대기 없이 통과한다.
        private IEnumerator UpdateVerdictGateRoutine()
        {
            var updateManager = AppUpdateManager.Instance;
            if (updateManager == null)
            {
                updateVerdictDone = true;
                TryOpenMenu();
                yield break;
            }

            float elapsed = 0f;
            while (!updateManager.CheckFinished && elapsed < UpdateVerdictTimeoutSeconds)
            {
                // 로그인이 실패로 끝나면 서버 검사는 시작조차 되지 않는다. 상한을 다 기다리는 사이
                // CloudSyncService가 먼저 메뉴를 열어버리므로, 같은 신호를 보고 즉시 캐시 판정으로 넘어간다.
                var ugs = UGSManager.Instance;
                if (ugs != null && ugs.SignInAttemptFinished && !ugs.IsInitialized)
                {
                    Log.Info("[MainMenuController] 로그인 실패 확정 - 캐시된 버전 판정을 사용한다");
                    break;
                }

                // 등급표시가 timeScale을 건드려도 멈추지 않도록 실시간 기준으로 센다.
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!updateManager.CheckFinished)
                Log.Warn($"[MainMenuController] 버전 판정 대기 종료 ({elapsed:F1}s) - 캐시 판정으로 진행");

            updateVerdictDone = true;
            TryOpenMenu();
            TryShowForceUpdatePopup();
        }

        private void UpdateMenuUI()
        {
            bool hasGameData = SaveManager.HasGameData();
            bool canUpgrade  = LegacyManager.Instance?.HasEverGameOver ?? false;
            view.UpdateUI(hasGameData, canUpgrade);

            TryShowLegacyUnlockNotice();
        }

        // 강화가 방금 해금됐고(게임오버 이력 O) 아직 안내한 적 없으면 1회 팝업.
        private void TryShowLegacyUnlockNotice()
        {
            var legacy = LegacyManager.Instance;
            if (legacy == null) return;
            if (!legacy.HasEverGameOver) return;
            if (legacy.HasShownLegacyUnlockPopup) return;

            legacy.MarkLegacyUnlockPopupShown(); // 즉시 저장 → 중복 방지
            UIPopupController.Instance?.ShowPopup(L("MainMenu_UpgradeUnlocked"), type: PopupSfxType.Notify);
        }

        // 게임산업법 제33조에 따른 등급표시를 먼저 띄우고, 표시가 끝난 뒤에 약관 동의로 이어간다.
        // 등급표시를 열지 못하는 상황(프리팹 미등록 등)에서도 약관 동의가 막히지 않도록 즉시 폴백한다.
        private void ShowRatingNoticeThenTerms()
        {
            if (ratingNoticeShown)
            {
                OnRatingNoticeFinished();
                return;
            }

            ratingNoticeShown = true;

            UIManager.Instance?.OpenPanel<RatingNoticeView>();

            var noticeController = UIControllerManager.Instance?.GetController<RatingNoticeController>();
            if (noticeController == null)
            {
                Log.Warn("[MainMenuController] RatingNoticeController를 찾을 수 없어 등급표시를 건너뜁니다.");
                OnRatingNoticeFinished();
                return;
            }

            noticeController.BeginNotice(OnRatingNoticeFinished);
        }

        // 등급표시가 끝났거나 건너뛴 시점. 약관 동의로 이어가고,
        // 법정 노출 시간을 가리지 않으려고 미뤄뒀던 강제 업데이트 안내가 있으면 여기서 띄운다.
        private void OnRatingNoticeFinished()
        {
            ratingNoticeDone = true;

            TryShowTermsAgreement();
            TryShowForceUpdatePopup();
        }

        // 최초 실행이면 약관 동의를 받는다. 필수 동의 전에는 게임에 진입할 수 없다.
        // 동의 전에는 분석 수집도 시작되지 않으므로 클라우드 동기화 완료를 기다리지 않고 곧바로 띄운다.
        private void TryShowTermsAgreement()
        {
            if (TermsAgreement.HasAgreed) return;

            UIManager.Instance?.OpenPanel<TermsAgreementView>();
        }

        private void OnCloudSyncCompleted()
        {
            UpdateMenuUI();

            // 최종 문구가 반영된 뒤에 메뉴를 등장시킨다 (버전 판정도 끝나야 실제로 열린다)
            cloudSyncDone = true;
            TryOpenMenu();

            // 닉네임 로드는 동기화 완료 후에 건다. 클라우드 legacy 채택이 끝나야
            // 로컬 미러(playerNickname)가 정확하고, 여기서 미리 읽어둬야 랭킹의 본인 항목과
            // 인게임 옵션의 무료 변경 횟수가 처음부터 올바르게 표시된다
            NicknameManager.Instance?.LoadNickname();

            // 클라우드 복구까지 끝났는데도 legacy가 손상 상태면 사용자에게 안내한다
            if (LegacyManager.Instance != null && LegacyManager.Instance.IsLegacyCorrupted)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("MainMenu_LegacyCorrupted"),
                    type: PopupSfxType.Warning);
            }
        }

        #endregion

        #region 버튼 이벤트

        private void OnNewGameClicked()
        {
            // 메모리 캐시 확인만 — 추가 Cloud Code 호출은 하지 않는다.
            // (밴 검사는 UGSManager 가 앱 실행 시점에 이미 수행했고,
            //  랭킹 등록 시점에 submitLeaderboardScore 가 다시 검사한다.)
            var ban = BanManager.Instance?.CurrentBan;
            if (ban != null)
            {
                ShowBanPopup(ban);
                return;
            }

            // 초회차(세이브 없음)면 삭제할 데이터가 없으므로 확인 팝업 없이 바로 새 게임 시작.
            if (!SaveManager.HasGameData())
            {
                _ = StartNewGameAsync();
                return;
            }

            UIPopupController.Instance?.ShowPopup(
                L("MainMenu_NewGameConfirm"),
                onConfirm: async () =>
                {
                    await StartNewGameAsync();
                },
                onCancel: () => { });
        }

        private async Task StartNewGameAsync()
        {
            // 중복 실행 방지: cloud clear await 구간에 다시 눌리지 않도록 버튼 비활성화.
            // 성공/실패 무관하게 마지막에 LoadGameScene으로 씬이 전환되므로 재활성화는 불필요.
            if (view?.NewGameButton != null)
                view.NewGameButton.interactable = false;

            // 새 게임 의도 플래그 — InGameScene 진입 후 GameManager가 cloud sync 복원보다 우선해서 사용.
            GameManager.MarkPendingNewGame();

            SaveManager.DeleteGameData();

            // 클라우드 GameData도 비워서 CloudSyncService가 옛 데이터를 복원하지 못하게 한다.
            // 네트워크가 끊겨 실패해도 PendingNewGame 플래그가 InGameScene에서 새 게임 분기를 강제한다.
            if (CloudSaveManager.Instance != null)
                await CloudSaveManager.Instance.ClearCloudGameDataAsync();

            SceneController.Instance?.LoadGameScene();
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        private void ShowBanPopup(BanInfo ban)
        {
            string message;
            if (ban.until > 0)
            {
                var local = DateTimeOffset.FromUnixTimeMilliseconds(ban.until).LocalDateTime;
                message = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Messages", "MainMenu_BanUntil",
                    arguments: new object[] { new Dictionary<string, object> { { "until", $"{local:yyyy-MM-dd HH:mm}" } } });
            }
            else
            {
                message = L("MainMenu_BanPermanent");
            }

            if (!string.IsNullOrWhiteSpace(ban.reason))
                message += "\n" + LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Messages", "MainMenu_BanReason",
                    arguments: new object[] { new Dictionary<string, object> { { "reason", ban.reason } } });

            UIPopupController.Instance?.ShowPopup(message, type: PopupSfxType.Warning);
        }

        // 강제 업데이트 안내를 띄울 수 있는 상태인지 확인하고 띄운다.
        // 세 신호가 모두 갖춰져야 한다: 버전 판정 확정 / 등급표시 종료 / 아직 안 띄웠음.
        // 신호가 도착하는 지점마다 이 메서드를 다시 불러 마지막 신호가 표시를 트리거한다.
        private void TryShowForceUpdatePopup()
        {
            if (updatePopupShown) return;
            if (!updateVerdictDone || !ratingNoticeDone) return;
            if (AppUpdateManager.Instance == null || !AppUpdateManager.Instance.IsUpdateRequired) return;

            updatePopupShown = true;
            ShowForceUpdatePopup();
        }

        // 닫을 수 없는 팝업. 확인을 누르면 스토어로 보낸 뒤 곧바로 다시 띄워,
        // 사용자가 업데이트하지 않고 돌아와도 게임에 진입할 수 없게 한다.
        private void ShowForceUpdatePopup()
        {
            closeUpdatePopup = UIPopupController.Instance?.ShowPopup(
                L("MainMenu_UpdateRequired"),
                onConfirm: OnForceUpdateConfirmed,
                type: PopupSfxType.Warning,
                confirmLabel: LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", "Common_Update"),
                dismissable: false);
        }

        // 스토어로 보낸 뒤 서버 값을 다시 확인한다. 여기서 차단이 풀리는 경우는
        // 운영자가 최소 버전을 되돌렸을 때뿐이다 - 앱이 실제로 업데이트되면 Android가
        // 프로세스를 종료하므로 이 경로를 타지 않고, Application.version도 실행 중에는 불변이다.
        private void OnForceUpdateConfirmed()
        {
            AppUpdateManager.Instance?.OpenStore();

            // 확인 버튼에 팝업이 이미 닫혔다. 응답을 기다리는 사이 메뉴가 노출되면
            // 구버전으로 게임에 진입할 수 있으므로 먼저 다시 띄우고 검사한다.
            // 차단이 풀리면 OnUpdateCleared -> HandleUpdateCleared 가 이 팝업을 거둔다.
            ShowForceUpdatePopup();

            _ = AppUpdateManager.Instance?.CheckVersionAsync();
        }

        // 서버 응답이 캐시 선판정을 뒤집었을 때. 떠 있는 안내 팝업을 닫고 정상 진입을 허용한다.
        private void HandleUpdateCleared()
        {
            closeUpdatePopup?.Invoke();
            closeUpdatePopup = null;
            updatePopupShown = false;
        }

        private void OnLoadGameClicked()
        {
            // 클라우드 동기화 완료 전 이어하기를 막는다.
            // 오래된 로컬 저장으로 진입하면 이후 자동 저장이 더 최신인 클라우드 진행을 덮어쓴다
            if (cloudSyncService != null && !cloudSyncService.IsSyncCompleted)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("MainMenu_SyncInProgress"),
                    type: PopupSfxType.Notify);
                return;
            }

            // 초회차(세이브 없음)면 새 게임 버튼과 동일하게 처리 (팝업 없이 바로 새 게임 시작).
            if (!SaveManager.HasGameData())
            {
                OnNewGameClicked();
                return;
            }

            // 손상 세이브 검증: 로드 불가(본 파일/백업 모두 실패)면 진입 대신 안내
            if (SaveManager.LoadGame() == null)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("MainMenu_SaveCorrupted"),
                    type: PopupSfxType.Warning);
                return;
            }

            SceneController.Instance?.LoadGameScene();
        }

        private void OnLeaderboardClicked()
        {
            UIManager.Instance?.OpenPanel<LeaderboardPopupView>();
        }

        private void OnUpgradeClicked()
        {
            UIManager.Instance?.OpenPanel<LegacyUpgradeView>();
        }

        private void OnOptionClicked()
        {
            UIManager.Instance?.OpenPanel<MainMenuOptionView>();
        }

        private void OnTermsOfServiceClicked()
        {
            UIManager.Instance?.OpenPanel<DataCollectionView>(() =>
                UIManager.Instance.GetOrInstantiatePanel<DataCollectionView>()?.SetDocument(PolicyDocumentType.TermsOfService));
        }

        private void OnPrivacyPolicyClicked()
        {
            UIManager.Instance?.OpenPanel<DataCollectionView>(() =>
                UIManager.Instance.GetOrInstantiatePanel<DataCollectionView>()?.SetDocument(PolicyDocumentType.PrivacyPolicy));
        }

        private void OnGuideClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("main_menu", "guide");
            Application.OpenURL(GuideUrl);
        }

        #endregion
    }
}
