using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>튜토리얼 12단계 식별자. 값은 GameData.tutorialStep(int)에 저장된다(중도 복원 단위).</summary>
    public enum TutorialStep
    {
        None = 0,
        WeaponShop = 1,      // 무기상점 (구현 완료)
        Blacksmith = 2,      // 대장장이
        MorningEvent = 3,    // 아침 이벤트
        QuestBoard = 4,      // 의뢰판
        PrepEnter = 5,       // 모험 준비 진입
        SendAdventure = 6,   // 모험 보내기
        Inventory = 7,       // 가방 확인
        SecondGuest = 8,     // 두 번째 손님
        AdventureResult = 9, // 모험 결과
        Quest = 10,          // 퀘스트 소개
        RemainingUI = 11,    // 나머지 UI
        Finish = 12          // 마무리
    }

    /// <summary>
    /// 튜토리얼 시스템 관리 (단계 오케스트레이터).
    /// 1~5단계(무기상점·대장장이·아침이벤트·의뢰판·모험 준비 진입) 구현 완료. 6단계(모험 보내기)는 6-A(조사·테스트)·6-B(지원 이동·무기 대여·부적 선물)·
    /// 6-C(던전 비교 + 던전 A 선택)·6-D(모험 정보·점술)·6-E(모험 출발)·7단계(가방 확인)·8단계(두 번째 손님)·9단계(모험 결과) 전체(9-A~9-C)·10단계(퀘스트 소개)·11단계(나머지 UI)·12단계(마무리)까지 전 단계 구현 완료.
    /// 8단계는 실패 던전(B)·경갑 시뮬레이션을 다룬다. 9-A는 진행 화면을 소개한 뒤 두 모험을 즉시 완료(판정 고정: A 성공/B 실패)하고 저녁(18:00)으로 넘겨 전령(해럴드)을 세운다. 9-B/9-C는 전령 보고로 성공(A)·실패(B) 결과를 확인한다(리플레이·수수료·재료 매입 / 활 파괴·교훈). 10단계는 퀘스트 화면 소개(조회 전용), 11단계는 상단바 요소 순차 하이라이트(조회 전용), 12단계는 안목 언급 + 작별 + 20:00 스킵 자막 연출 후 정식 완주(CompleteFullTutorial).
    /// 전체 완주 여부는 영구(PlayerData/LegacyManager), 1단계 무료 무기 수령은 회차별(GameData).
    /// </summary>
    public class TutorialManager : BaseManager<TutorialManager>
    {
        // 튜토리얼 무기 ID / 전령 대사는 TutorialConfig(SO)에 있다.
        private TutorialConfig Config => ConfigManager.Instance.Tutorial;

        private bool isTutorialActive = false;

        // 대장간 하위 단계 라우팅 — 무기 패널 훅(선택/확인 등)이 어느 동작을 유도할지 결정.
        private enum BlacksmithPhase { None, Enforce, Evolve, Reroll }
        private BlacksmithPhase blacksmithPhase = BlacksmithPhase.None;

        // 3단계에서만 아침 스킵 버튼을 허용한다(그 외 튜토리얼 구간은 VisitorManager.CanSkipMorning이 막는다).
        private bool isMorningSkipAllowed = false;
        public bool IsMorningSkipAllowed => isMorningSkipAllowed;

        public bool IsTutorialActive => isTutorialActive;
        // 이번 회차 1단계(무료 무기 수령) 완료 여부 — 회차별(GameData). 무료 상점·방문자 로직의 가드로 쓰인다.
        public bool HasCompletedTutorial => GameManager.Instance?.GameData.hasReceivedTutorialWeapons ?? false;
        // 계정이 전체 튜토리얼(2~12단계)을 한 번이라도 완주했는지 — 영구(PlayerData). 완주 계정은 이후 회차에 1단계만 진행한다.
        public bool HasEverCompletedTutorial => LegacyManager.Instance?.HasEverCompletedTutorial ?? false;

        #region 초기화

        public void Initialize(GameData gameData)
        {
            if (TimeManager.Instance?.CurrentDay != 1) return;

            // 튜토리얼 진행 중 상태에서 앱이 재시작된 경우 — 진행 중이던 단계의 진입 스냅샷으로 복원한다.
            // 저장은 단계 진입 시점에만 되므로(SaveGame(force:true)), 로드된 GameData는 항상 "단계 진입 상태"다.
            int restoreStep = gameData.tutorialStep;

            // 안전망: 아침 NPC는 스폰됐는데 단계 기록이 0이면(비정상) 1단계로 간주.
            // 단, 이미 무료무기를 받은(=1단계 완료 후 정상 진행 or 튜토리얼 종료) 상태면 복원하지 않는다.
            if (restoreStep < 1 && gameData.hasSpawnedMorningNPCs && !gameData.hasReceivedTutorialWeapons)
                restoreStep = 1;

            if (restoreStep < 1) return;   // 진행 중 아님(미시작 또는 완료 — FinishTutorial이 tutorialStep=0으로 리셋)

            isTutorialActive = true;
            TimeManager.Instance?.ForcePause();
            StartCoroutine(RestoreTutorialAtStep(restoreStep));
            Log.Info($"[TutorialManager] 튜토리얼 {restoreStep}단계 복원");
        }

        /// <summary>
        /// 저장된 단계의 진입 스냅샷 상태에서 해당 단계를 진입점부터 깨끗이 재구성한다.
        /// 로드 시 복원된 방문자/패널을 정리한 뒤 재스폰하므로 중복 스폰이 없다(디버그 점프와 동일 방식).
        /// </summary>
        private IEnumerator RestoreTutorialAtStep(int step)
        {
            // 모든 매니저 Initialize + 방문자 복원 + UI 준비를 한 프레임 대기
            yield return null;
            if (!isTutorialActive) yield break;

            ShowDebugBar();

            // 로드 시 각 매니저가 복원/재오픈한 방문자·패널을 정리하고 해당 단계를 처음부터 재생한다.
            UIManager.Instance?.CloseAllPanels();
            VisitorManager.Instance?.ClearAllVisitors();

            // 6단계(모험 보내기)는 독립 진입점이 없어(5단계에서 열린 준비 화면을 이어받음) 5단계 소개부터 재생한다.
            var restoreStep = (TutorialStep)step;
            if (restoreStep == TutorialStep.SendAdventure)
            {
                GameManager.Instance.GameData.tutorialStep = (int)TutorialStep.PrepEnter;
                restoreStep = TutorialStep.PrepEnter;
            }

            switch (restoreStep)
            {
                case TutorialStep.WeaponShop:
                    // 1단계 스냅샷은 구매 전 상태 — 상점을 재스폰하고 인트로부터 재생(상점 매물은 오픈 시 재생성).
                    VisitorManager.Instance?.SpawnTutorialWeaponShop();
                    BeginStep1Intro();
                    break;

                case TutorialStep.QuestBoard:
                    // 의뢰판은 이미 생성돼 있으면 EnsureTodayBoardGenerated가 패널을 안 여니 명시적으로 오픈한다.
                    StartStep(TutorialStep.QuestBoard);
                    UIManager.Instance?.OpenPanel<QuestBoardView>();
                    break;

                default:
                    StartStep(restoreStep);
                    break;
            }
        }

        #endregion

        #region 튜토리얼 진행 (1단계: 무기상점)

        /// <summary>
        /// 1일차 아침에 튜토리얼 진입. VisitorManager에서 호출.
        /// 초회차면 바로 진행하고, 재도전자면 스킵 여부를 먼저 묻는다.
        /// </summary>
        public void StartTutorial()
        {
            if (TimeManager.Instance?.CurrentDay != 1)
            {
                Log.Warn("TutorialManager: Tutorial only available on Day 1");
                return;
            }

            if (HasCompletedTutorial)
            {
                Log.Warn("TutorialManager: Tutorial already completed");
                return;
            }

            // 완주 계정도 시작은 동일하게 진행한다. 건너뛰기는 대화창의 상시 스킵 버튼으로만(SkipRemainingTutorial).
            RunTutorial();
        }

        /// <summary>튜토리얼 1단계 본체: 무기 상점 스폰 + 시간 정지 + 전령 인트로.</summary>
        private void RunTutorial()
        {
            isTutorialActive = true;
            GameManager.Instance.GameData.tutorialStep = (int)TutorialStep.WeaponShop;
            SendTutorialStepAnalytics(TutorialStep.WeaponShop);

            VisitorManager.Instance?.SpawnTutorialWeaponShop();
            TimeManager.Instance?.ForcePause();
            ShowDebugBar();

            SnapshotStepEntry();   // 1단계 진입 스냅샷(구매 전 상태) — 중도 강제종료 시 복원 기준점

            BeginStep1Intro();
        }

        private void BeginStep1Intro()
        {
            OpenDialogue(Config.GetLines(TutorialLineKey.WeaponShopIntro), onComplete: HighlightShopNPC, actionFollows: true);
        }

        /// <summary>인트로 후 무기 상점 NPC를 하이라이트해 클릭을 유도한다.</summary>
        private void HighlightShopNPC()
        {
            var shop = GetTutorialShop();
            if (shop == null)
            {
                Log.Warn("[TutorialManager] 튜토리얼 상점 NPC를 찾지 못했습니다.");
                return;
            }

            // 입장 애니메이션 중이면 완료 후 하이라이트(버튼이 아직 클릭 불가일 수 있음)
            if (shop.isEntering)
            {
                shop.OnEnterCompleted += OnShopEntered;
                return;
            }

            DoHighlightShop(shop);
        }

        private void OnShopEntered(VisitorNPC npc)
        {
            npc.OnEnterCompleted -= OnShopEntered;
            DoHighlightShop(npc);
        }

        private void DoHighlightShop(VisitorNPC shop)
        {
            // 대화창과 동일 정책(상단바만)으로 열어 대화↔하이라이트 전환 시 하단바 깜빡임을 막는다.
            UIManager.Instance.OpenPanel<TutorialHighlightView>(() =>
                UIManager.Instance.GetPanel<TutorialHighlightView>()?.SetUIVisibility(hideUI: false, minimalUI: true));
            UIManager.Instance.GetPanel<TutorialHighlightView>()?.SetTarget(shop.InteractionButtonRect);
        }

        /// <summary>무기 상점이 열렸을 때 호출. WeaponShopController.Initialize에서 호출.</summary>
        public void OnTutorialShopOpened()
        {
            if (!isTutorialActive) return;

            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            // 상점 패널이 최상단에 올라온 다음 프레임에 안내 대화를 띄운다.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.WeaponShopFree), onComplete: null, actionFollows: true);
        }

        /// <summary>무기 구매 시 호출. WeaponShopDetailController에서 호출. 4종 보유 시 완료.</summary>
        public void OnTutorialWeaponPurchased()
        {
            TryCompleteStep1();
        }

        /// <summary>상점을 닫으려 할 때 호출. WeaponShopController.OnCloseShop에서 호출.</summary>
        public void OnTutorialShopClosed()
        {
            if (!isTutorialActive) return;

            // 전 종류 미만이면 닫기를 막고 안내(OnCloseShop이 HasCompletedTutorial=false로 조기 return).
            if (!HasAllTutorialWeapons())
                ShowTutorialWarning();
            // 전 종류를 다 샀다면 아무것도 하지 않는다 — 완료 처리는 TryCompleteStep1이 담당.
        }

        /// <summary>4종을 모두 보유했으면 1단계를 완료한다(멱등).</summary>
        private void TryCompleteStep1()
        {
            if (!isTutorialActive) return;

            var gameData = GameManager.Instance.GameData;
            if (gameData.hasReceivedTutorialWeapons) return;   // 이미 완료 진행 중
            if (!HasAllTutorialWeapons()) return;

            gameData.hasReceivedTutorialWeapons = true;         // 완료 확정 → 상점 닫힘 허용

            // 상점이 아직 열려 있으면 컨트롤러 경유로 정상 종료(상점 정리·NPC 퇴장)
            UIControllerManager.Instance?.GetController<WeaponShopController>()?.OnCloseShop();

            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.WeaponShopClosing), onComplete: OnStep1Completed);
        }

        /// <summary>1단계(무기상점) 완료 → 2단계로 진행. 스킵을 택하지 않고 시작한 이상 완주 계정도 전체를 진행한다.</summary>
        private void OnStep1Completed()
        {
            AdvanceTo(TutorialStep.Blacksmith);
        }

        /// <summary>다음 단계로 진행 — 진행 단계를 저장(복원 단위)하고 진입점을 호출한다.</summary>
        private void AdvanceTo(TutorialStep step)
        {
            var gameData = GameManager.Instance.GameData;
            gameData.tutorialStep = (int)step;
            SendTutorialStepAnalytics(step);
            // 이 단계로 진입한다는 것은 앞 단계 결과물이 정상 플레이로 모두 실현됐다는 뜻 - catch-up 기준선 갱신.
            gameData.tutorialOutcomeAppliedThrough = Mathf.Max(gameData.tutorialOutcomeAppliedThrough, (int)step - 1);
            SnapshotStepEntry();   // 이 단계 진입 스냅샷 — StartStep이 NPC/UI를 열기 전(깨끗한 진입 상태)에 저장한다.
            StartStep(step);
        }

        /// <summary>tutorial_step 이벤트 발행 (G27 튜토리얼 이탈 구간). 복원(RestoreTutorialAtStep) 경로에서는 보내지 않는다.</summary>
        private void SendTutorialStepAnalytics(TutorialStep step)
        {
            AnalyticsManager.Instance?.Send("tutorial_step", new Dictionary<string, object>
            {
                { "step", (int)step },
                { "step_name", step.ToString() }
            });
        }

        /// <summary>단계별 진입점 디스패치. (2~12단계 콘텐츠는 후속 작업에서 채운다.)</summary>
        private void StartStep(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Blacksmith:
                    StartStepBlacksmith();
                    break;
                case TutorialStep.MorningEvent:
                    StartStepMorningEvent();
                    break;
                case TutorialStep.QuestBoard:
                    StartStepQuestBoard();
                    break;
                case TutorialStep.PrepEnter:
                    StartStepPrepEnter();
                    break;
                case TutorialStep.SendAdventure:
                    StartStepSendAdventure();
                    break;
                case TutorialStep.Inventory:
                    StartStepInventory();
                    break;
                case TutorialStep.SecondGuest:
                    StartStepSecondGuest();
                    break;
                case TutorialStep.AdventureResult:
                    StartStepAdventureResult();
                    break;
                case TutorialStep.Quest:
                    StartStepQuest();
                    break;
                case TutorialStep.RemainingUI:
                    StartStepRemainingUI();
                    break;
                case TutorialStep.Finish:
                    StartStepFinish();
                    break;
                default:
                    Log.Warn($"[TutorialManager] 미구현 단계: {step} — 완주 처리 후 종료");
                    CompleteFullTutorial();
                    break;
            }
        }

        /// <summary>전체 튜토리얼 완주 — 영구 완주 플래그를 저장하고 종료한다. (후속 작업에서 12단계가 호출)</summary>
        private void CompleteFullTutorial()
        {
            // 스킵/완주 어느 경로로 끝나든 정상 완주와 동일하게 시각을 20:00로 확정한다.
            // 정상 12단계는 FarewellSequence가 이미 20:00로 스킵해 no-op, 스킵(09:00/18:00)만 20:00로 통일된다.
            TimeManager.Instance?.SkipToHour(FarewellSkipHour);
            int legacyReward = LegacyManager.Instance?.MarkTutorialCompleted() ?? 0;
            FinishTutorial();

            // 보상은 계정 최초 완주에만 나온다(재완주는 0) - 팝업도 그때만 띄운다.
            if (legacyReward > 0)
            {
                UIPopupController.Instance?.ShowPopup(L("Tutorial_ClearReward", ("legacy", legacyReward)),
                    type: PopupSfxType.Notify);
            }
        }

        private void FinishTutorial()
        {
            isTutorialActive = false;
            blacksmithPhase = BlacksmithPhase.None;
            adventureResultPhase = AdventureResultPhase.None;
            questPhase = QuestPhase.None;
            DestroySleepOverlay();        // 수면 연출 도중 스킵/종료돼도 암전 오버레이가 남지 않게 정리
            // 상호작용(줌인) 도중 스킵하면 EndInteraction/OnClose를 거치지 않아 카메라가 줌인 상태로 고착된다.
            // 정상 완주 시엔 이미 줌아웃 상태라 no-op.
            CameraZoomController.Instance?.ZoomOut();
            isMorningSkipAllowed = false;
            SetPanelInputEnabled(true);   // 대기 코루틴이 도중에 끊겨 raycaster가 꺼진 채 남는 경우 대비 복구
            TimeManager.Instance.ForceResume();

            // 진행 중 표식을 해제한다(tutorialStep=0) — 이후 재접속 시 오복원(완료된 튜토리얼 재생)을 막는다.
            // 이 시점부터 mid-step 저장 억제도 풀리므로, 완료 상태를 즉시 1회 확정 저장한다.
            GameManager.Instance.GameData.tutorialStep = 0;
            GameManager.Instance.SaveGame();

            Log.Info("[TutorialManager] 튜토리얼 종료, 시간 재개");
        }

        /// <summary>
        /// 완주 계정용 스킵: 어느 단계에서든 튜토리얼 전체를 한 번에 종료한다.
        /// 열린 패널·튜토리얼 스폰 NPC를 모두 정리하고, 무료 무기 미보유분을 지급한 뒤 완주 처리한다.
        /// 단계마다 스폰물이 다르므로 정리는 이 단일 진입점에서 전역으로 수행한다.
        /// </summary>
        public void SkipRemainingTutorial()
        {
            if (!isTutorialActive) return;

            // 완주 스킵도 밀린 1~4단계 결과물을 반영해 세이브 상태를 일관되게 만든다
            // (무기 지급 / 부적 제작 + 무기 강화 / 아침 보상 + 아침 스킵 / 던전 수색 파견).
            // isTutorialActive가 아직 true라 각 매니저의 튜토리얼 가드(0원·성공 강제·선물 고정)가 적용된다.
            // 순서 주의: 패널 정리보다 먼저 돌려야 9-B 결과창이 열린 채 스킵한 경우에도 재료 매입이 재현된다.
            ApplyCatchUpOutcomes(HighestOutcomeStep);
            UIManager.Instance?.CloseAllPanels();   // catch-up이 연 의뢰판 + 튜토리얼 대화창·게임 패널 정리
            VisitorManager.Instance?.ClearAllVisitors();
            CompleteFullTutorial();                 // 영구 완주 저장 + 시간 재개

            Log.Info("[TutorialManager] 튜토리얼 스킵 - catch-up 적용 후 완주 처리");
        }

        /// <summary>튜토리얼 무료 무기 4종 중 아직 보유하지 않은 종류만 지급하고 1단계 완료로 표시한다.</summary>
        private void GrantMissingTutorialWeapons()
        {
            if (DataManager.Instance == null)
            {
                Log.Error("TutorialManager: DataManager not initialized");
                return;
            }

            var inventory = InventoryManager.Instance;
            var owned = inventory?.GetAllWeapons();

            foreach (string weaponID in Config.TutorialWeaponIDs)
            {
                bool alreadyOwned = owned != null &&
                    owned.Any(w => w?.weaponData != null && w.weaponData.StaticID == weaponID);
                if (alreadyOwned) continue;

                WeaponData weaponData = DataManager.Instance.GetWeapon(weaponID);
                if (weaponData == null)
                {
                    Log.Error($"TutorialManager: Tutorial weapon {weaponID} not found!");
                    continue;
                }
                // 강화 대상 무기는 2단계 catch-up 강화가 성립하도록 CanEnforce 상태로 지급한다.
                WeaponInstance instance = (weaponID == Config.EnforceWeaponID)
                    ? CreateEnforceableWeaponInstance(weaponData)
                    : new WeaponInstance(weaponData);
                ApplyRentWeaponFixedEffect(instance);   // 대여용 검이면 마법갑옷 일치 효과로 고정(6-C 학습 전제)
                inventory?.AddWeapon(instance);
            }

            GameManager.Instance.GameData.hasReceivedTutorialWeapons = true;
        }

        /// <summary>
        /// 튜토리얼 무기 상점용 아이템 생성 (0골드). WeaponShopManager에서 호출.
        /// </summary>
        public List<WeaponShopItemData> GenerateTutorialWeaponShop()
        {
            List<WeaponShopItemData> tutorialItems = new List<WeaponShopItemData>();

            if (DataManager.Instance == null)
            {
                Log.Error("TutorialManager: DataManager not initialized");
                return tutorialItems;
            }

            foreach (string weaponID in Config.TutorialWeaponIDs)
            {
                WeaponData weaponData = DataManager.Instance.GetWeapon(weaponID);
                if (weaponData == null)
                {
                    Log.Error($"TutorialManager: Tutorial weapon {weaponID} not found!");
                    continue;
                }

                if (weaponID == Config.EnforceWeaponID)
                {
                    tutorialItems.Add(GenerateEnforceableTutorialWeapon(weaponData));
                }
                else
                {
                    var item = new WeaponShopItemData(weaponData, 0);
                    ApplyRentWeaponFixedEffect(item.WeaponInstance);   // 대여용 검이면 마법갑옷 일치 효과로 고정(구매 시 인벤토리로 그대로 이동)
                    tutorialItems.Add(item);
                }
            }

            return tutorialItems;
        }

        /// <summary>
        /// 2-C 강화 대상(조잡한 단검)은 Common 등급 = 효과 1개뿐이라, 생성 시 굴림이 우연히 최댓값이면
        /// 즉시 강화 최대 상태로 태어나 CanEnforce가 false가 될 수 있다. 강화 가능한 상태가 나올 때까지 재굴림한다.
        /// </summary>
        private WeaponShopItemData GenerateEnforceableTutorialWeapon(WeaponData weaponData)
        {
            WeaponShopItemData item = new WeaponShopItemData(weaponData, 0);
            int attempts = 1;
            const int maxAttempts = 30;

            while (!item.WeaponInstance.CanEnforce && attempts < maxAttempts)
            {
                item = new WeaponShopItemData(weaponData, 0);
                attempts++;
            }

            if (!item.WeaponInstance.CanEnforce)
                Log.Warn($"[TutorialManager] 강화 대상 무기({weaponData.StaticID})가 {maxAttempts}회 재시도에도 강화 가능 상태로 생성되지 않았습니다.");

            return item;
        }

        /// <summary>
        /// 대여용 무기의 부가효과를 학습 서사에 맞는 방어타입 일치로 고정한다. 그 외 무기는 건드리지 않는다.
        /// - 연습용 검(주특기 검) → 마법갑옷 일치 (6-C "검 마법갑옷 부가효과 일치" 학습)
        /// - 물푸레나무 활 → 경장갑 일치 (8단계 "경갑 가정 시뮬레이션" 학습)
        /// WeaponInstance 생성 시 효과가 랜덤 롤이라, 학습이 성립하도록 지정 효과 1개로 덮어쓴다.
        /// </summary>
        private void ApplyRentWeaponFixedEffect(WeaponInstance weapon)
        {
            if (weapon?.weaponData == null) return;

            string effectID = null;
            if (weapon.weaponData.StaticID == GetTutorialRentWeaponID())   effectID = Config.RentWeaponFixedEffectID;   // 검 → 마법갑옷
            else if (weapon.weaponData.StaticID == GetTutorialRentBowID()) effectID = Config.RentBowFixedEffectID;      // 활 → 경장갑
            if (string.IsNullOrEmpty(effectID)) return;

            var effectData = DataManager.Instance?.GetWeaponEffect(effectID);
            if (effectData == null)
            {
                Log.Warn($"[TutorialManager] 대여 무기 고정 부가효과({effectID})를 찾지 못했습니다.");
                return;
            }
            weapon.effects = new List<WeaponEffect> { new WeaponEffect(effectData) };
            weapon.enforceLevel = 0;   // 미강화 Common 상태로 표시(효과 교체 후 이전 롤 기준 강화레벨이 남지 않도록)
        }

        #endregion

        #region 튜토리얼 진행 (2단계: 대장장이)

        /// <summary>2단계 진입: 대장장이 스폰 + 인트로 대화 → 대장장이 하이라이트. (재료 증정은 대장간 진입 후.)</summary>
        private void StartStepBlacksmith()
        {
            VisitorManager.Instance?.SpawnTutorialBlacksmith(Config.BlacksmithIntroNPCID);
            OpenDialogue(Config.GetLines(TutorialLineKey.BlacksmithIntro), onComplete: HighlightBlacksmithNPC, actionFollows: true);
        }

        /// <summary>2단계 재료 증정: 행운의 부적 레시피 재료 일체 + Common 진화 재료 일체. (정상 진행·catch-up 공용, 1회만 지급)</summary>
        private void GrantBlacksmithMaterials()
        {
            var gameData = GameManager.Instance.GameData;
            if (gameData.hasGrantedTutorialBlacksmithMaterials) return;   // catch-up 중복 지급 방지

            var inventory = InventoryManager.Instance;
            if (inventory == null || DataManager.Instance == null) return;

            // 행운의 부적 레시피 재료 (2-B 제작용)
            var charmRecipe = DataManager.Instance.GetAllActiveItemRecipes()
                .FirstOrDefault(r => r?.resultItem != null && r.resultItem.itemType == ActiveItemType.Charm);
            if (charmRecipe != null)
            {
                foreach (var req in charmRecipe.requiredMaterials)
                    if (req?.material != null)
                        inventory.AddMaterial(req.material.StaticID, req.count);
            }
            else Log.Warn("[TutorialManager] 행운의 부적 레시피를 찾지 못했습니다.");

            // Common -> Uncommon 진화 재료 (2-D 조잡한 단검 진화용). 강화는 재료를 쓰지 않는다.
            var evolveMats = BlacksmithManager.Instance?.GetEvolveMaterials(Grade.Common);
            if (evolveMats != null && evolveMats.Count > 0)
            {
                foreach (var (material, count) in evolveMats)
                    inventory.AddMaterial(material.StaticID, count);
            }
            else Log.Warn("[TutorialManager] Common 진화 재료를 찾지 못했습니다.");

            gameData.hasGrantedTutorialBlacksmithMaterials = true;
        }

        /// <summary>인트로 후 대장장이 NPC를 하이라이트해 클릭을 유도한다.</summary>
        private void HighlightBlacksmithNPC()
        {
            var blacksmith = GetTutorialBlacksmith();
            if (blacksmith == null)
            {
                Log.Warn("[TutorialManager] 튜토리얼 대장장이 NPC를 찾지 못했습니다.");
                return;
            }

            // 입장 애니메이션 중이면 완료 후 하이라이트(버튼이 아직 클릭 불가일 수 있음)
            if (blacksmith.isEntering)
            {
                blacksmith.OnEnterCompleted += OnBlacksmithEntered;
                return;
            }

            DoHighlightBlacksmith(blacksmith);
        }

        private void OnBlacksmithEntered(VisitorNPC npc)
        {
            npc.OnEnterCompleted -= OnBlacksmithEntered;
            DoHighlightBlacksmith(npc);
        }

        private void DoHighlightBlacksmith(VisitorNPC blacksmith)
        {
            // 대화창과 동일 정책(상단바만)으로 열어 대화↔하이라이트 전환 시 하단바 깜빡임을 막는다.
            UIManager.Instance.OpenPanel<TutorialHighlightView>(() =>
                UIManager.Instance.GetPanel<TutorialHighlightView>()?.SetUIVisibility(hideUI: false, minimalUI: true));
            UIManager.Instance.GetPanel<TutorialHighlightView>()?.SetTarget(blacksmith.InteractionButtonRect);
        }

        /// <summary>대장간이 열렸을 때 호출. BlacksmithController.Initialize에서 호출.</summary>
        public void OnTutorialBlacksmithOpened()
        {
            if (!isTutorialActive) return;

            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            GrantBlacksmithMaterials();   // 대장간 진입 후 재료 증정 (선물 대사와 시점 일치)
            // 대장간 패널이 최상단에 올라온 다음 프레임에 안내 대화를 띄우고, 종료 시 2-B(제작 유도)로 진행.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.BlacksmithCraftIntro),
                onComplete: HighlightRecipeTypeToggle, actionFollows: true);
        }

        #endregion

        #region 튜토리얼 진행 (2-B: 제작)

        /// <summary>2-B: 제작 안내 후 ActiveItem 레시피 토글을 하이라이트한다.</summary>
        private void HighlightRecipeTypeToggle()
        {
            ShowHighlight(GetTutorialCraftPanel()?.GetRecipeTypeToggleRect());
        }

        /// <summary>2-B: ActiveItem 탭이 열리면 부적 레시피 카드를 하이라이트한다. BlacksmithCraftPanelView에서 호출.</summary>
        public void OnTutorialCraftTabToActiveItem()
        {
            if (!isTutorialActive) return;

            string charmID = GetCharmRecipeStaticID();
            if (string.IsNullOrEmpty(charmID)) return;

            var panel = GetTutorialCraftPanel();
            if (panel == null) return;

            // 부적 카드는 목록 맨 아래라 스크롤 상단 상태에선 뷰포트 밖이다.
            // 목록 로딩 -> 스크롤 최하단 -> 레이아웃 확정이 끝난 뒤에 잠그고 하이라이트한다(잠금 = ScrollRect 비활성화라 이동보다 뒤에).
            panel.FocusActiveItemRecipeCard(charmID, cardRect =>
            {
                panel.SetActiveItemScrollLocked(true);   // 스크롤로 대상 카드가 밀려나지 않도록 잠금
                ShowHighlight(cardRect);
            });
        }

        /// <summary>2-B: 부적 레시피가 선택되면 제작 버튼을 하이라이트한다. BlacksmithCraftPanelView에서 호출.</summary>
        public void OnTutorialCharmRecipeSelected()
        {
            if (!isTutorialActive) return;

            var panel = GetTutorialCraftPanel();
            panel?.SetActiveItemScrollLocked(false);   // 선택 완료 → 잠금 해제
            ShowHighlight(panel?.GetCraftButtonRect());
        }

        /// <summary>2-B: 부적 제작이 시작되면 하이라이트를 닫는다(진행은 제작 연출 완료 후 2-C). BlacksmithController에서 호출.</summary>
        public void OnTutorialCharmCraftStarted()
        {
            if (!isTutorialActive) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        private BlacksmithCraftPanelView GetTutorialCraftPanel()
        {
            return UIManager.Instance.GetPanel<BlacksmithView>()?.GetCraftPanelView();
        }

        private string GetCharmRecipeStaticID()
        {
            var charmRecipe = DataManager.Instance.GetAllActiveItemRecipes()
                .FirstOrDefault(r => r?.resultItem != null && r.resultItem.itemType == ActiveItemType.Charm);
            return charmRecipe?.StaticID;
        }

        #endregion

        #region 튜토리얼 진행 (2-C: 강화)

        /// <summary>2-C 진입: 부적 제작 연출 완료 → 강화 안내 대사 → 무기 패널 전환 버튼 하이라이트. BlacksmithController에서 호출.</summary>
        public void OnTutorialCharmCraftCompleted()
        {
            if (!isTutorialActive) return;
            blacksmithPhase = BlacksmithPhase.Enforce;
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.BlacksmithEnforceIntro),
                onComplete: HighlightWeaponPanelButton, actionFollows: true);
        }

        private void HighlightWeaponPanelButton()
        {
            ShowHighlight(UIManager.Instance.GetPanel<BlacksmithView>()?.GetNextButtonRect());
        }

        /// <summary>2-C: 무기 패널이 열리면 조잡한 단검 카드를 하이라이트한다. BlacksmithView에서 호출.</summary>
        public void OnTutorialWeaponPanelOpened()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Enforce) return;
            var panel = UIManager.Instance.GetPanel<BlacksmithView>()?.GetWeaponPanelView();
            panel?.SetWeaponScrollLocked(true);   // 스크롤로 대상 카드가 밀려나지 않도록 잠금
            ShowHighlight(panel?.GetWeaponCardRect(Config.EnforceWeaponID));
        }

        /// <summary>2-C~2-E: 조잡한 단검 선택 시 현재 하위 단계에 맞는 버튼을 하이라이트한다. BlacksmithWeaponPanelView에서 호출.</summary>
        public void OnTutorialBlacksmithWeaponSelected()
        {
            if (!isTutorialActive) return;

            // 무기 선택이 끝났으므로 목록 스크롤 잠금 해제(이후 하이라이트 대상은 고정 버튼).
            UIManager.Instance.GetPanel<BlacksmithView>()?.GetWeaponPanelView()?.SetWeaponScrollLocked(false);

            switch (blacksmithPhase)
            {
                case BlacksmithPhase.Enforce:
                case BlacksmithPhase.Evolve:
                    HighlightUpgradeButton();
                    break;
                case BlacksmithPhase.Reroll:
                    HighlightRerollButton();
                    break;
            }
        }

        /// <summary>Upgrade 버튼 하이라이트(강화 단계면 "강화", 진화 단계면 "진화" 라벨).</summary>
        private void HighlightUpgradeButton()
        {
            var panel = UIManager.Instance.GetPanel<BlacksmithView>()?.GetWeaponPanelView();
            ShowHighlight(panel?.GetUpgradeButtonRect());
        }

        /// <summary>재부여(Reroll) 버튼 하이라이트.</summary>
        private void HighlightRerollButton()
        {
            var panel = UIManager.Instance.GetPanel<BlacksmithView>()?.GetWeaponPanelView();
            ShowHighlight(panel?.GetRerollButtonRect());
        }

        /// <summary>2-C: 강화 확인 화면이 열리면 강화 실행 버튼을 하이라이트한다. BlacksmithEnforceConfirmView에서 호출.</summary>
        public void OnTutorialEnforceConfirmOpened()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Enforce) return;
            var confirm = UIManager.Instance.GetPanel<BlacksmithEnforceConfirmView>();
            ShowHighlight(confirm?.GetEnforceButtonRect());
        }

        /// <summary>2-C: 강화 실행이 시작되면 하이라이트를 닫는다. BlacksmithController에서 호출.</summary>
        public void OnTutorialEnforceStarted()
        {
            if (!isTutorialActive) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        /// <summary>2-C 완료 → 2-D 진입: 진화 안내 대사 후 Upgrade(진화) 버튼 하이라이트. BlacksmithController에서 호출.</summary>
        public void OnTutorialEnforceCompleted()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Enforce) return;
            blacksmithPhase = BlacksmithPhase.Evolve;
            // 강화 결과창이 닫히며 무기 패널이 갱신돼 Upgrade 버튼이 "진화"로 바뀐 상태.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.BlacksmithEvolveIntro),
                onComplete: HighlightUpgradeButton, actionFollows: true);
        }

        #endregion

        #region 튜토리얼 진행 (2-D: 진화)

        /// <summary>2-D: 진화 확인 화면이 열리면 진화 실행 버튼을 하이라이트한다. BlacksmithEvolveConfirmView에서 호출.</summary>
        public void OnTutorialEvolveConfirmOpened()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Evolve) return;
            var confirm = UIManager.Instance.GetPanel<BlacksmithEvolveConfirmView>();
            ShowHighlight(confirm?.GetEvolveButtonRect());
        }

        /// <summary>2-D: 진화 실행이 시작되면 하이라이트를 닫는다. BlacksmithController에서 호출.</summary>
        public void OnTutorialEvolveStarted()
        {
            if (!isTutorialActive) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        /// <summary>2-D 완료 → 2-E 진입: 재부여 안내 대사 후 Reroll 버튼 하이라이트. BlacksmithController에서 호출.</summary>
        public void OnTutorialEvolveCompleted()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Evolve) return;
            blacksmithPhase = BlacksmithPhase.Reroll;
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.BlacksmithRerollIntro),
                onComplete: HighlightRerollButton, actionFollows: true);
        }

        #endregion

        #region 튜토리얼 진행 (2-E: 재부여 / 2-F: 마무리)

        /// <summary>2-E: 재부여 확인 화면이 열리면 재부여 실행 버튼을 하이라이트한다. BlacksmithRerollConfirmView에서 호출.</summary>
        public void OnTutorialRerollConfirmOpened()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Reroll) return;
            var confirm = UIManager.Instance.GetPanel<BlacksmithRerollConfirmView>();
            ShowHighlight(confirm?.GetRerollButtonRect());
        }

        /// <summary>2-E: 재부여가 실행되면 하이라이트를 닫는다(이전/새 효과 선택은 자유). BlacksmithController에서 호출.</summary>
        public void OnTutorialRerollStarted()
        {
            if (!isTutorialActive) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        /// <summary>
        /// 2-E: 재부여 결과(이전/새 효과 선택)가 뜨면 안내 대사 후 "이전 선택"을 하이라이트로 유도한다
        /// (이후 선택도 여전히 가능, 강제는 아님). BlacksmithRerollConfirmView에서 호출.
        /// </summary>
        public void OnTutorialRerollResultShown()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Reroll) return;
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.BlacksmithRerollResult), onComplete: HighlightSelectBeforeButton, actionFollows: true);
        }

        /// <summary>2-E: 이전/새 효과 선택 대사 후 "이전 선택" 버튼을 하이라이트한다.</summary>
        private void HighlightSelectBeforeButton()
        {
            var confirm = UIManager.Instance.GetPanel<BlacksmithRerollConfirmView>();
            ShowHighlight(confirm?.GetSelectBeforeButtonRect());
        }

        /// <summary>2-E 완료(이전/이후 선택 클릭) → 하이라이트를 닫고 2-F(마무리) 대사 후 3단계로. BlacksmithController에서 호출.</summary>
        public void OnTutorialRerollCompleted()
        {
            if (!isTutorialActive || blacksmithPhase != BlacksmithPhase.Reroll) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            blacksmithPhase = BlacksmithPhase.None;
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.BlacksmithOutro),
                onComplete: FinishBlacksmithStep);
        }

        /// <summary>2-F: 대장간을 정리(타입 선택 대화 생략, 대장장이 퇴장)하고 3단계로 진행한다.</summary>
        private void FinishBlacksmithStep()
        {
            UIControllerManager.Instance?.GetController<BlacksmithController>()?.CloseForTutorial();
            AdvanceTo(TutorialStep.MorningEvent);
        }

        #endregion

        #region 튜토리얼 진행 (3단계: 아침 이벤트)

        /// <summary>3단계 진입: 길드 사절단 고정 스폰 + 인트로 대사 → 사절단 하이라이트.</summary>
        private void StartStepMorningEvent()
        {
            VisitorManager.Instance?.SpawnTutorialGuildEnvoy();
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.MorningEventIntro), onComplete: HighlightGuildEnvoyNPC, actionFollows: true);
        }

        private void HighlightGuildEnvoyNPC()
        {
            var envoy = GetTutorialGuildEnvoy();
            if (envoy == null)
            {
                Log.Warn("[TutorialManager] 튜토리얼 길드 사절단 NPC를 찾지 못했습니다.");
                return;
            }

            // 입장 애니메이션 중이면 완료 후 하이라이트(버튼이 아직 클릭 불가일 수 있음)
            if (envoy.isEntering)
            {
                envoy.OnEnterCompleted += OnGuildEnvoyEntered;
                return;
            }

            ShowHighlight(envoy.InteractionButtonRect);
        }

        private void OnGuildEnvoyEntered(VisitorNPC npc)
        {
            npc.OnEnterCompleted -= OnGuildEnvoyEntered;
            ShowHighlight(npc.InteractionButtonRect);
        }

        /// <summary>3단계: 사절단을 클릭해 이벤트가 열리면 하이라이트를 걷는다. VisitorNPC에서 호출.</summary>
        public void OnTutorialMorningEventOpened()
        {
            if (!isTutorialActive) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        /// <summary>3단계: 사절단 상호작용(선물 수령 + 마무리 대화)이 끝나면 여운 대사 → 아침 스킵 유도. VisitorNPC에서 호출.</summary>
        public void OnTutorialMorningEventCompleted()
        {
            if (!isTutorialActive) return;

            // 사절단 선물은 이 시점 이전(사절단 상호작용)에 이미 지급됨 - catch-up 중복 지급을 막기 위해 기록.
            GameManager.Instance.GameData.hasClaimedTutorialGuildGift = true;

            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.MorningEventGift), onComplete: HighlightMorningSkipButton, actionFollows: true);
        }

        /// <summary>3단계: 아침 스킵 버튼을 열고(가드 해제) 하이라이트한다.</summary>
        private void HighlightMorningSkipButton()
        {
            isMorningSkipAllowed = true;   // 여기서부터 CanSkipMorning 허용

            var topBar = GetTopBarView();
            // 시간이 강제 정지라 OnTimeChanged가 오지 않으므로 버튼 모드를 수동 갱신해야 아침 스킵 아이콘이 뜬다.
            topBar?.RefreshTimeButtonMode();
            ShowHighlight(topBar?.GetTimeButtonRect());
        }

        /// <summary>3단계: 아침 스킵으로 낮에 진입 → 4단계로. TopBarView에서 호출.</summary>
        public void OnTutorialMorningSkipped()
        {
            if (!isTutorialActive || !isMorningSkipAllowed) return;

            isMorningSkipAllowed = false;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            AdvanceTo(TutorialStep.QuestBoard);
        }

        private VisitorNPC GetTutorialGuildEnvoy()
        {
            return VisitorManager.Instance?.ActiveVisitors?
                .FirstOrDefault(v => v != null && v.visitorType == VisitorType.EventNPC);
        }

        // TopBarView는 BaseView가 아닌 씬 상주 오브젝트라 UIManager.GetPanel로 못 얻는다(기획 7.0).
        private TopBarView topBarCache;
        private TopBarView GetTopBarView()
        {
            if (topBarCache == null) topBarCache = FindObjectOfType<TopBarView>(true);
            return topBarCache;
        }

        #endregion

        #region 튜토리얼 진행 (4단계: 의뢰판)

        // 2배 던전 설명 중복 재생 방지(Phase 2 진입 훅은 재입장 시에도 호출될 수 있음).
        private bool hasExplainedDoubleDungeon = false;

        /// <summary>
        /// 4단계 진입: 의뢰판은 아침 스킵으로 인한 낮 진입 시 QuestBoardManager가 자동 생성·오픈한다
        /// (고정 매물 A/B/C 강제는 QuestBoardManager 내부 가드). 디버그 단계 점프 등으로 아직 생성 전이면
        /// 강제 생성을 보장한 뒤 안내 대사만 얹는다. 풀=필요선택수=3이라 선택 자체는 하이라이트 없이 자유 진행된다.
        /// </summary>
        private void StartStepQuestBoard()
        {
            hasExplainedDoubleDungeon = false;
            QuestBoardManager.Instance?.EnsureTodayBoardGenerated();
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.QuestBoardIntro), onComplete: null, showPortrait: false, actionFollows: true);
        }

        /// <summary>
        /// 4단계: 확정 → 2배 던전 하이라이트 연출이 끝나고 Phase 2(수색)에 진입하면 호출된다. QuestBoardView에서 호출.
        /// 던전 C(2배)를 하이라이트하며 설명 대사를 띄운다.
        /// </summary>
        public void OnTutorialQuestBoardScoutPhaseEntered()
        {
            if (!isTutorialActive || hasExplainedDoubleDungeon) return;
            hasExplainedDoubleDungeon = true;

            var boardView = UIManager.Instance.GetPanel<QuestBoardView>();
            boardView?.SetDungeonScrollLocked(true);   // 이번 단계 동안 목록이 밀려 하이라이트가 어긋나지 않도록 잠금
            ShowHighlight(boardView?.GetDungeonSlotRect(Config.TutorialDungeonCID));
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.QuestBoardDoubleDungeon), onComplete: BeginScoutGuide, showPortrait: false);
        }

        /// <summary>
        /// 2배 던전(C) 설명이 끝나면 하이라이트를 걷고, 던전 몬스터가 매일 바뀐다는 일반 개념을 안내한다
        /// (이 대사는 특정 던전을 가리키지 않으므로 하이라이트 없음).
        /// </summary>
        private void BeginScoutGuide()
        {
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.QuestBoardScoutIntro), onComplete: HighlightScoutDungeonAndPromptSend, showPortrait: false);
        }

        /// <summary>던전 A를 하이라이트하고, 그 위에 "이 던전에 보내볼까요?" 파견 유도 대사를 얹는다.</summary>
        private void HighlightScoutDungeonAndPromptSend()
        {
            var boardView = UIManager.Instance.GetPanel<QuestBoardView>();
            ShowHighlight(boardView?.GetDungeonSlotRect(Config.TutorialDungeonAID));
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.QuestBoardScoutSend), onComplete: null, showPortrait: false, actionFollows: true);
        }

        /// <summary>4단계: 던전 A가 수색 대상으로 선택되면 파견 버튼을 하이라이트한다. QuestBoardController에서 호출.</summary>
        public void OnTutorialScoutDungeonSelected()
        {
            if (!isTutorialActive) return;
            var boardView = UIManager.Instance.GetPanel<QuestBoardView>();
            ShowHighlight(boardView?.GetScoutButtonRect());
        }

        /// <summary>4단계: 수색꾼 파견 완료 → 마무리 대사 후 5단계로. QuestBoardController에서 호출.</summary>
        public void OnTutorialScoutDispatched()
        {
            if (!isTutorialActive) return;

            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            UIManager.Instance.GetPanel<QuestBoardView>()?.SetDungeonScrollLocked(false);
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.QuestBoardOutro), onComplete: FinishQuestBoardStep, showPortrait: true);
        }

        /// <summary>4단계 완료 → 5단계로.</summary>
        private void FinishQuestBoardStep()
        {
            AdvanceTo(TutorialStep.PrepEnter);
        }

        #endregion

        #region 튜토리얼 진행 (5단계: 모험 준비 진입)

        /// <summary>5단계 진입: 첫 번째 튜토리얼 전용 모험가 스폰 + 인트로 대사 → 모험가 하이라이트.</summary>
        private void StartStepPrepEnter()
        {
            VisitorManager.Instance?.SpawnTutorialAdventurer(Config.TutorialAdventurer1ID);
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.PrepEnterIntro), onComplete: HighlightTutorialAdventurer, actionFollows: true);
        }

        /// <summary>인트로 후 튜토리얼 모험가를 하이라이트해 클릭을 유도한다.</summary>
        private void HighlightTutorialAdventurer()
        {
            var adventurer = GetTutorialAdventurerNPC();
            if (adventurer == null)
            {
                Log.Warn("[TutorialManager] 튜토리얼 모험가 NPC를 찾지 못했습니다.");
                return;
            }

            // 입장 애니메이션 중이면 완료 후 하이라이트(버튼이 아직 클릭 불가일 수 있음)
            if (adventurer.isEntering)
            {
                adventurer.OnEnterCompleted += OnTutorialAdventurerEntered;
                return;
            }

            ShowHighlight(adventurer.InteractionButtonRect);
        }

        private void OnTutorialAdventurerEntered(VisitorNPC npc)
        {
            npc.OnEnterCompleted -= OnTutorialAdventurerEntered;
            ShowHighlight(npc.InteractionButtonRect);
        }

        /// <summary>5단계: 모험가를 클릭하면 즉시 하이라이트를 걷는다(카메라 줌 애니메이션 중 위치가 어긋나 보이는 것 방지). VisitorNPC에서 호출.</summary>
        public void OnTutorialAdventurerClicked()
        {
            if (!isTutorialActive) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        /// <summary>5단계: 모험가 준비 화면이 열리면 구조 소개 대사 후 6단계로. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialAdventurePrepOpened()
        {
            if (!isTutorialActive) return;

            // 8단계: 두 번째 모험가의 준비 화면이 열리면 무기 조사 유도로 진행(5단계 구조 소개는 생략).
            if (secondGuestPhase == SecondGuestPhase.AwaitClick)
            {
                BeginSecondGuestWeaponHint();
                return;
            }

            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.PrepEnterStructure), onComplete: FinishPrepEnterStep);
        }

        /// <summary>5단계 완료 → 6단계로.</summary>
        private void FinishPrepEnterStep()
        {
            AdvanceTo(TutorialStep.SendAdventure);
        }

        private VisitorNPC GetTutorialAdventurerNPC()
        {
            return VisitorManager.Instance?.ActiveVisitors?
                .FirstOrDefault(v => v != null && v.visitorType == VisitorType.Adventurer);
        }

        #endregion

        #region 튜토리얼 진행 (6단계: 모험 보내기 — 6-A 조사·테스트)

        // 6-A의 두 대화 액션(무기 조사/종합 테스트) 중 무엇을 기다리는지 — 완료 훅은 두 액션에 공용이라 분기 필요.
        private enum PrepTestPhase { None, WeaponHint, AllStats }
        private PrepTestPhase prepTestPhase = PrepTestPhase.None;

        /// <summary>
        /// 6단계 진입: 5단계에서 이미 열려 있는 모험 준비 화면(Tab1)에 이어서 진행한다.
        /// 기본 무기 조사 유도 대사 → weaponHintButton 하이라이트.
        /// </summary>
        private void StartStepSendAdventure()
        {
            prepTestPhase = PrepTestPhase.WeaponHint;
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureWeaponHintIntro), onComplete: HighlightWeaponHintButton, actionFollows: true);
        }

        private void HighlightWeaponHintButton()
        {
            var tab1 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab1Panel();
            ShowHighlight(tab1?.GetWeaponHintButtonRect());
        }

        /// <summary>6-A: 기본 무기 조사/종합 테스트 확인 팝업이 열리면 진행 버튼을 하이라이트한다(고급 판정 토글도 함께 가려 일반 판정이 강제됨). AdventurePreparationController에서 호출.</summary>
        public void OnTutorialStatTestConfirmOpened()
        {
            if (!isTutorialActive || prepTestPhase == PrepTestPhase.None) return;
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            ShowHighlight(prepView?.GetStatTestProceedButtonRect());
        }

        /// <summary>6-A: 진행 버튼 클릭 시 하이라이트를 걷는다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialStatTestStarted()
        {
            if (!isTutorialActive || prepTestPhase == PrepTestPhase.None) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        /// <summary>6-A: 대화 액션(무기 조사/종합 테스트) 결과 연출이 끝나면 다음 단계로. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialTalkActionCompleted()
        {
            if (!isTutorialActive) return;

            // 8단계: 무기 조사(주특기 검 발견)가 끝나면 활 추천 대사 후 지원(Tab2) 이동을 유도한다.
            if (secondGuestPhase == SecondGuestPhase.AwaitWeaponHint)
            {
                OnSecondGuestWeaponHintDone();
                return;
            }

            switch (prepTestPhase)
            {
                case PrepTestPhase.WeaponHint:
                    prepTestPhase = PrepTestPhase.AllStats;
                    OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureAllStatsIntro), onComplete: HighlightAllStatsRevealButton, actionFollows: true);
                    break;
                case PrepTestPhase.AllStats:
                    prepTestPhase = PrepTestPhase.None;
                    OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureTestFollowup), onComplete: StartSupportGuide, actionFollows: true);
                    break;
            }
        }

        private void HighlightAllStatsRevealButton()
        {
            var tab1 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab1Panel();
            ShowHighlight(tab1?.GetAllStatsRevealButtonRect());
        }

        // ── 6-B: 지원(무기 대여 · 부적 선물) ──────────────────────────────
        // 카드 클릭/실행 훅이 여러 조작에 공용이라, 지금 어느 조작을 기다리는지 내부 상태로 분기한다(6-A의 PrepTestPhase와 동일 패턴).
        private enum PrepSupportPhase { None, AwaitTabMove, AwaitWeaponRent, AwaitCharmGift }
        private PrepSupportPhase prepSupportPhase = PrepSupportPhase.None;

        /// <summary>6-B 진입: 6-A 마무리 대사(602) 종료 후 "다음 단계로" 버튼을 하이라이트해 지원 단계로 이동을 유도한다.</summary>
        private void StartSupportGuide()
        {
            prepSupportPhase = PrepSupportPhase.AwaitTabMove;
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetNextTabButtonRect());
        }

        /// <summary>6-B: 준비 화면의 진행 단계(탭)가 바뀌면 호출된다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialPrepTabChanged(int tabIndex)
        {
            if (!isTutorialActive) return;

            // 8단계: 지원(Tab2)/최종(Tab3) 진입 시 각각 활 대여·던전 유도를 시작한다.
            if (secondGuestPhase == SecondGuestPhase.AwaitTab2 && tabIndex == 1)
            {
                BeginSecondGuestBowRentGuide();
                return;
            }
            if (secondGuestPhase == SecondGuestPhase.AwaitTab3 && tabIndex == 2)
            {
                BeginSecondGuestDungeonGuide();
                return;
            }

            // 6-B: 지원(Tab2)에 진입하면 무기 대여 유도를 시작한다.
            if (prepSupportPhase == PrepSupportPhase.AwaitTabMove && tabIndex == 1)
                BeginWeaponRentGuide();
            // 6-C: 최종 준비(Tab3)에 진입하면 던전 비교 안내를 시작한다.
            else if (prepFinalPhase == PrepFinalPhase.AwaitTab3Move && tabIndex == 2)
                BeginDungeonCompareGuide();
        }

        /// <summary>지원 단계 진입 → 무기 대여 유도 대사 후 연습용 검 카드를 하이라이트한다.</summary>
        private void BeginWeaponRentGuide()
        {
            prepSupportPhase = PrepSupportPhase.AwaitWeaponRent;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();   // "다음 단계로" 하이라이트 해제
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureRentWeaponIntro), onComplete: HighlightRentWeaponCard, actionFollows: true);
        }

        private void HighlightRentWeaponCard()
        {
            var tab2 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab2Panel();
            if (tab2 == null) return;

            tab2.SelectWeaponSubTab();               // 무기 서브탭 확정(기본값이지만 방어적으로 선택)
            tab2.SetWeaponScrollLocked(true);        // 스크롤로 대상 카드가 밀려나지 않도록 잠금
            ShowHighlight(tab2.GetWeaponCardRect(GetTutorialRentWeaponID()));
        }

        /// <summary>6-B/8단계: 무기 상세 패널이 열리면(대여 대상 카드 클릭) 대여 버튼을 하이라이트한다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialWeaponDetailOpened()
        {
            if (!isTutorialActive) return;
            if (prepSupportPhase != PrepSupportPhase.AwaitWeaponRent && secondGuestPhase != SecondGuestPhase.AwaitBowRent) return;
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetWeaponDetailRentButtonRect());
        }

        /// <summary>6-B/8단계: 무기 대여가 확정되면 다음 유도로 진행한다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialWeaponRented()
        {
            if (!isTutorialActive) return;

            // 8단계: 활 대여가 끝나면 최종 준비(Tab3) 이동을 유도한다.
            if (secondGuestPhase == SecondGuestPhase.AwaitBowRent)
            {
                OnSecondGuestBowRented();
                return;
            }

            if (prepSupportPhase != PrepSupportPhase.AwaitWeaponRent) return;

            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.GetTab2Panel()?.SetWeaponScrollLocked(false);   // 대여 완료 → 잠금 해제
            ShowHighlight(prepView?.GetStrStatGroupRect());
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureStatArrowInfo),
                onComplete: BeginCharmGiftGuide, showPortrait: false);
        }

        /// <summary>
        /// 스탯 화살표 설명 후 선물(아이템) 서브탭으로 전환하고, 부적 선물 유도 대사(605)를 띄운 뒤 대사 종료 시 부적 카드를 하이라이트한다.
        /// 서브탭 전환 직후엔 스크롤 리셋(`ResetPosition`)이 프레임 끝(`WaitForEndOfFrame`)에 실행돼 같은 프레임에 읽은 카드 위치가
        /// 어긋나므로, 하이라이트는 대사 종료 후(정착 이후)로 미룬다.
        /// </summary>
        private void BeginCharmGiftGuide()
        {
            prepSupportPhase = PrepSupportPhase.AwaitCharmGift;

            var tab2 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab2Panel();
            if (tab2 == null) return;

            UIManager.Instance.ClosePanel<TutorialHighlightView>();   // 이전(STR 스탯행) 하이라이트 정리
            tab2.SelectActiveItemSubTab();           // 선물(아이템) 서브탭으로 전환 → 목록 갱신(스크롤 리셋은 프레임 끝)
            tab2.SetActiveItemScrollLocked(true);    // 스크롤로 대상 카드가 밀려나지 않도록 잠금
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureCharmGiftIntro),
                onComplete: HighlightCharmCard, actionFollows: true);
        }

        private void HighlightCharmCard()
        {
            var tab2 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab2Panel();
            ShowHighlight(tab2?.GetCharmCardRect());
        }

        /// <summary>6-B: 아이템 상세 패널이 열리면(부적 카드 클릭) 선물 버튼을 하이라이트한다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialActiveItemDetailOpened()
        {
            if (!isTutorialActive || prepSupportPhase != PrepSupportPhase.AwaitCharmGift) return;
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetActiveItemDetailAssignButtonRect());
        }

        /// <summary>6-B 완료: 부적 선물이 끝나면 하이라이트/잠금을 정리하고 6-C(던전 비교)로 진행한다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialItemGifted()
        {
            if (!isTutorialActive || prepSupportPhase != PrepSupportPhase.AwaitCharmGift) return;

            prepSupportPhase = PrepSupportPhase.None;
            UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab2Panel()?.SetActiveItemScrollLocked(false);
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            StartFinalGuide();
        }

        /// <summary>튜토리얼 무료 무기 4종 중 검(주특기 검과 일치하는 대여 대상)의 StaticID. 배열 순서에 의존하지 않도록 타입으로 찾는다.</summary>
        private string GetTutorialRentWeaponID()
        {
            foreach (var id in Config.TutorialWeaponIDs)
            {
                var data = DataManager.Instance.GetWeapon(id);
                if (data != null && data.weaponType == WeaponType.Sword)
                    return id;
            }
            Log.Warn("[TutorialManager] 튜토리얼 대여 대상 검을 찾지 못했습니다.");
            return null;
        }

        /// <summary>튜토리얼 무료 무기 4종 중 활(8단계 물푸레나무 활 대여 대상)의 StaticID.</summary>
        private string GetTutorialRentBowID()
        {
            foreach (var id in Config.TutorialWeaponIDs)
            {
                var data = DataManager.Instance.GetWeapon(id);
                if (data != null && data.weaponType == WeaponType.Bow)
                    return id;
            }
            Log.Warn("[TutorialManager] 튜토리얼 대여 대상 활을 찾지 못했습니다.");
            return null;
        }

        #endregion

        #region 튜토리얼 진행 (6단계: 모험 보내기 — 6-C 던전 비교·시뮬레이션)

        private enum PrepFinalPhase { None, AwaitTab3Move, Comparing, AwaitDungeonOpen, AwaitDungeonSelect }
        private PrepFinalPhase prepFinalPhase = PrepFinalPhase.None;

        /// <summary>6-C 진입: 부적 선물 후 "다음 단계로" 버튼을 하이라이트해 최종 준비(Tab3) 이동을 유도한다.</summary>
        private void StartFinalGuide()
        {
            prepFinalPhase = PrepFinalPhase.AwaitTab3Move;

            // 6-C 학습 전제: 던전 A 수색 결과(마법갑)가 도착해 있어야 한다. 튜토리얼은 시간 정지라 자연 완료되지 않으므로 즉시 완료 처리.
            // (OnScoutComplete 이벤트로 준비 화면의 던전 A 카드가 노란색 마법갑 아이콘으로 갱신된다.)
            var dungeonA = DataManager.Instance?.GetDungeon(Config.TutorialDungeonAID);
            if (dungeonA != null) ScoutManager.Instance?.CompleteScoutImmediate(dungeonA);

            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetNextTabButtonRect());
        }

        /// <summary>6-C: 최종 준비(Tab3) 진입 → 던전 A 방어타입 아이콘(노란색=확정)부터 순차 비교 안내. OnTutorialPrepTabChanged에서 분기.</summary>
        private void BeginDungeonCompareGuide()
        {
            prepFinalPhase = PrepFinalPhase.Comparing;
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.SetDungeonScrollLocked(true);   // 비교 동안 목록 고정
            ShowHighlight(prepView?.GetDungeonArmorIconRect(Config.TutorialDungeonAID));
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureCompareConfirmed),
                onComplete: CompareStepUnknown, showPortrait: false);
        }

        /// <summary>던전 B 방어타입 아이콘('?'=미확정) 하이라이트 + 대사.</summary>
        private void CompareStepUnknown()
        {
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            ShowHighlight(prepView?.GetDungeonArmorIconRect(Config.TutorialDungeonBID));
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureCompareUnknown),
                onComplete: CompareStepDouble, showPortrait: false);
        }

        /// <summary>던전 C '2배' 표시 하이라이트 + 대사.</summary>
        private void CompareStepDouble()
        {
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            ShowHighlight(prepView?.GetDungeonDoubleRewardIconRect(Config.TutorialDungeonCID));
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureCompareDouble),
                onComplete: CompareStepWeaponBonus, showPortrait: false);
        }

        /// <summary>검 마법갑옷 부가효과 일치 설명 — 던전 A(마법갑) 하이라이트 + 대사.</summary>
        private void CompareStepWeaponBonus()
        {
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            ShowHighlight(prepView?.GetDungeonArmorIconRect(Config.TutorialDungeonAID));
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureWeaponBonusMatch),
                onComplete: BeginSelectDungeonGuide, showPortrait: false);
        }

        /// <summary>비교가 끝나면 던전 A 카드를 하이라이트하며 선택을 유도한다(시뮬레이션은 8단계 실패 던전에서 다룬다).</summary>
        private void BeginSelectDungeonGuide()
        {
            prepFinalPhase = PrepFinalPhase.AwaitDungeonOpen;
            // 비교 설명 하이라이트(던전 A 방어타입 아이콘)를 걷고, 선택 지시 대사 중엔 하이라이트 없이 진행한다(종료 후 던전 A 카드 하이라이트).
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureSelectDungeon),
                onComplete: HighlightSelectDungeonA, showPortrait: false, actionFollows: true);
        }

        private void HighlightSelectDungeonA()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetDungeonCardRect(Config.TutorialDungeonAID));
        }

        /// <summary>6-C/8단계: 던전 상세가 열리면 다음 유도로 진행한다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialDungeonDetailOpened(string dungeonStaticID)
        {
            if (!isTutorialActive) return;

            // 8단계: 던전 B 상세가 열리면 경장갑 토글만 활성화하고 그 토글을 하이라이트한다(경갑 가정 시뮬레이션).
            if (secondGuestPhase == SecondGuestPhase.AwaitDungeonOpen && dungeonStaticID == Config.TutorialDungeonBID)
            {
                OnSecondGuestDungeonBOpened();
                return;
            }

            if (prepFinalPhase != PrepFinalPhase.AwaitDungeonOpen) return;
            if (dungeonStaticID != Config.TutorialDungeonAID) return;

            prepFinalPhase = PrepFinalPhase.AwaitDungeonSelect;
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetDungeonDetailSelectButtonRect());
        }

        /// <summary>6-C/8단계 완료: 던전이 선택되면 다음 유도로 진행한다. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialDungeonSelected(string dungeonStaticID)
        {
            if (!isTutorialActive) return;

            // 8단계: 던전 B가 선택되면 출발 유도로 진행한다.
            if (secondGuestPhase == SecondGuestPhase.AwaitSelect && dungeonStaticID == Config.TutorialDungeonBID)
            {
                OnSecondGuestDungeonSelected();
                return;
            }

            if (prepFinalPhase != PrepFinalPhase.AwaitDungeonSelect) return;
            if (dungeonStaticID != Config.TutorialDungeonAID) return;

            prepFinalPhase = PrepFinalPhase.None;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            UIManager.Instance.GetPanel<AdventurePreparationView>()?.SetDungeonScrollLocked(false);
            StartInfoSeerGuide();
        }

        #endregion

        #region 튜토리얼 진행 (6단계: 모험 보내기 — 6-D 모험 정보·점술)

        private enum PrepInfoSeerPhase { None, AwaitInfoView, AwaitSeer }
        private PrepInfoSeerPhase prepInfoSeerPhase = PrepInfoSeerPhase.None;

        /// <summary>6-D 진입: 던전 A 선택 후 모험 정보 칩 확인을 유도한다(칩 하이라이트 + 안내 대사).</summary>
        private void StartInfoSeerGuide()
        {
            prepInfoSeerPhase = PrepInfoSeerPhase.AwaitInfoView;
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            // 칩 클릭(툴팁 열람)을 '정보 확인'으로 감지하기 위해 콜백을 배선한다.
            prepView?.SetInfoChipsTutorialCallback(OnTutorialAdventureInfoViewed);
            // 대사 중엔 하이라이트를 띄우지 않고, 대사 종료 후 모험 정보 컨테이너를 하이라이트한다(8단계와 동일 패턴).
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureInfoIntro),
                onComplete: HighlightAdventureInfoContainer, showPortrait: false, actionFollows: true);
        }

        private void HighlightAdventureInfoContainer()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetAdventureInfoContainerRect());
        }

        /// <summary>6-D: 모험 정보 칩을 클릭(확인)하면 점술 유도로 넘어간다. AdventureInfoCardItem 콜백에서 호출.</summary>
        public void OnTutorialAdventureInfoViewed()
        {
            if (!isTutorialActive || prepInfoSeerPhase != PrepInfoSeerPhase.AwaitInfoView) return;

            prepInfoSeerPhase = PrepInfoSeerPhase.AwaitSeer;
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.SetInfoChipsTutorialCallback(null);   // 콜백 해제(중복 진행 방지)
            UIManager.Instance.ClosePanel<TutorialHighlightView>();   // 정보 하이라이트 해제(대사 중엔 하이라이트 없음)
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureSeerIntro),
                onComplete: HighlightSeerButton, actionFollows: true);
        }

        /// <summary>점술 버튼을 하이라이트한다. 클릭 시 SeerView(모달 팝업)가 자체 흐름으로 진행된다.</summary>
        private void HighlightSeerButton()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetSeerButtonRect());
        }

        /// <summary>6-D 완료: 점술 상담이 실제로 완료되면(SeerView 닫힘) 6-E(모험 출발)로 진행. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialSeerConsulted(AdventurerInstance adventurer, DungeonData dungeon)
        {
            if (!isTutorialActive || prepInfoSeerPhase != PrepInfoSeerPhase.AwaitSeer) return;
            // 상담 없이 팝업만 열었다 닫은 경우는 진행하지 않는다(점술 버튼 하이라이트 유지 → 재시도).
            if (adventurer == null || dungeon == null) return;
            if (SeerManager.Instance.CanConsult(adventurer, dungeon)) return;

            prepInfoSeerPhase = PrepInfoSeerPhase.None;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            StartDepartGuide();
        }

        #endregion

        #region 튜토리얼 진행 (6단계: 모험 보내기 — 6-E 모험 출발)

        private enum PrepDepartPhase { None, AwaitDepart }
        private PrepDepartPhase prepDepartPhase = PrepDepartPhase.None;

        /// <summary>6-E 진입: 점술 완료 후 모험 출발(시작 버튼)을 유도한다(안내 대사 + 시작 버튼 하이라이트).</summary>
        private void StartDepartGuide()
        {
            prepDepartPhase = PrepDepartPhase.AwaitDepart;
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SendAdventureDepartIntro),
                onComplete: HighlightStartAdventureButton, actionFollows: true);
        }

        private void HighlightStartAdventureButton()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetStartAdventureButtonRect());
        }

        /// <summary>6-E/8단계 완료: 모험이 출발하면(준비화면 닫힘) 다음 단계로. AdventurePreparationController에서 호출.</summary>
        public void OnTutorialAdventureStarted()
        {
            if (!isTutorialActive) return;

            // 8단계: 두 번째 모험이 출발하면 임시 완주(9단계 미구현).
            if (secondGuestPhase == SecondGuestPhase.AwaitDepart)
            {
                OnSecondGuestDeparted();
                return;
            }

            if (prepDepartPhase != PrepDepartPhase.AwaitDepart) return;

            prepDepartPhase = PrepDepartPhase.None;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            // 준비화면이 이미 닫혔으므로 대사는 배경(상시UI) 위에 뜬다. 종료 후 7단계로.
            OpenDialogue(Config.GetLines(TutorialLineKey.SendAdventureDepartOutro),
                onComplete: () => AdvanceTo(TutorialStep.Inventory));
        }

        #endregion

        #region 튜토리얼 진행 (8단계: 두 번째 손님)

        // 8단계 하위 진행 상태 — 6단계 준비 화면 훅(무기조사/대여/던전/출발)을 phase 분기로 재사용한다.
        private enum SecondGuestPhase { None, AwaitClick, AwaitWeaponHint, AwaitTab2, AwaitBowRent, AwaitTab3, AwaitDungeonOpen, AwaitSim, AwaitSelect, AwaitDepart }
        private SecondGuestPhase secondGuestPhase = SecondGuestPhase.None;

        /// <summary>8단계 진입: 두 번째 모험가를 먼저 스폰하고, 입장이 끝나면 인사 대사 후 하이라이트한다(첫 모험은 진행 중).</summary>
        private void StartStepSecondGuest()
        {
            secondGuestPhase = SecondGuestPhase.AwaitClick;
            VisitorManager.Instance?.SpawnTutorialAdventurer(Config.TutorialAdventurer2ID);

            // 모험가가 입장한 뒤에 인사 대사를 띄워 자연스럽게 만든다(입장 애니메이션 중이면 완료 후).
            var npc = GetTutorialAdventurerNPC();
            if (npc != null && npc.isEntering)
            {
                npc.OnEnterCompleted += OnSecondGuestEntered;
                return;
            }
            GreetSecondGuest();
        }

        private void OnSecondGuestEntered(VisitorNPC npc)
        {
            npc.OnEnterCompleted -= OnSecondGuestEntered;
            GreetSecondGuest();
        }

        private void GreetSecondGuest()
        {
            OpenDialogue(Config.GetLines(TutorialLineKey.SecondGuestGreeting),
                onComplete: HighlightTutorialAdventurer, actionFollows: true);
        }

        /// <summary>8단계: 준비 화면이 열리면 조사 유도 대사 후 기본 무기 조사 버튼을 하이라이트한다. OnTutorialAdventurePrepOpened에서 분기.</summary>
        private void BeginSecondGuestWeaponHint()
        {
            secondGuestPhase = SecondGuestPhase.AwaitWeaponHint;
            prepTestPhase = PrepTestPhase.WeaponHint;   // 6-A 확인팝업/진행 훅을 그대로 재사용
            // 인트로 대사를 Deferred로 띄워(패널 오픈 창 보호 + 한 프레임 정착) 종료 후 조사 버튼을 하이라이트한다.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestWeaponHintIntro),
                onComplete: HighlightSecondGuestWeaponHintButton, actionFollows: true);
        }

        private void HighlightSecondGuestWeaponHintButton()
        {
            var tab1 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab1Panel();
            ShowHighlight(tab1?.GetWeaponHintButtonRect());
        }

        /// <summary>8단계: 무기 조사 완료 → 활 추천 대사 후 지원(Tab2) 이동을 유도한다. OnTutorialTalkActionCompleted에서 분기.</summary>
        private void OnSecondGuestWeaponHintDone()
        {
            prepTestPhase = PrepTestPhase.None;
            secondGuestPhase = SecondGuestPhase.AwaitTab2;
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestWeaponFound),
                onComplete: HighlightNextTabForSecondGuest, actionFollows: true);
        }

        private void HighlightNextTabForSecondGuest()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetNextTabButtonRect());
        }

        /// <summary>8단계: 지원(Tab2) 진입 → "검은 대여 중, 활 추천" 대사 후 물푸레나무 활 카드를 하이라이트한다. OnTutorialPrepTabChanged에서 분기.</summary>
        private void BeginSecondGuestBowRentGuide()
        {
            secondGuestPhase = SecondGuestPhase.AwaitBowRent;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();   // "다음 단계로" 하이라이트 해제

            var tab2 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab2Panel();
            tab2?.SelectWeaponSubTab();              // 무기 서브탭 확정(스크롤 리셋은 프레임 끝 → 대사 재생 중 정착)
            tab2?.SetWeaponScrollLocked(true);
            // Tab2 도착 시점에 활 추천 대사를 먼저 띄우고(Tab1이 아니라 여기서), 종료 후 활 카드를 하이라이트한다.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestBowSuggest),
                onComplete: HighlightSecondGuestBowCard, actionFollows: true);
        }

        private void HighlightSecondGuestBowCard()
        {
            var tab2 = UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetTab2Panel();
            ShowHighlight(tab2?.GetWeaponCardRect(GetTutorialRentBowID()));
        }

        /// <summary>8단계: 활 대여 완료 → 최종 준비(Tab3) 이동을 유도한다. OnTutorialWeaponRented에서 분기.</summary>
        private void OnSecondGuestBowRented()
        {
            secondGuestPhase = SecondGuestPhase.AwaitTab3;
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.GetTab2Panel()?.SetWeaponScrollLocked(false);
            ShowHighlight(prepView?.GetNextTabButtonRect());
        }

        /// <summary>8단계: 최종 준비(Tab3) 진입 → 미확정·시뮬레이션 설명 대사 후 던전 B 카드를 하이라이트한다. OnTutorialPrepTabChanged에서 분기.</summary>
        private void BeginSecondGuestDungeonGuide()
        {
            secondGuestPhase = SecondGuestPhase.AwaitDungeonOpen;
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.SetDungeonScrollLocked(true);
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestSimulationIntro),
                onComplete: HighlightSecondGuestDungeonB, actionFollows: true);
        }

        private void HighlightSecondGuestDungeonB()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetDungeonCardRect(Config.TutorialDungeonBID));
        }

        /// <summary>8단계: 던전 B 상세가 열리면 경갑 가정 유도 대사 후 경장갑 토글(만 활성)을 하이라이트한다. OnTutorialDungeonDetailOpened에서 분기.</summary>
        private void OnSecondGuestDungeonBOpened()
        {
            secondGuestPhase = SecondGuestPhase.AwaitSim;
            UIManager.Instance.GetPanel<AdventurePreparationView>()?.SetDungeonDetailSimulationOnly(ArmorType.LightArmor);   // 경갑만 클릭 가능
            UIManager.Instance.ClosePanel<TutorialHighlightView>();   // 던전 B 카드 하이라이트 해제(대사 중엔 하이라이트 없음)
            // 상세 진입 후 "경장갑으로 가정해볼까요?" 대사 → 종료 후 경갑 토글 하이라이트.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestSimulationAssume),
                onComplete: HighlightSecondGuestArmorToggle, actionFollows: true);
        }

        private void HighlightSecondGuestArmorToggle()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetDungeonDetailArmorToggleRect(ArmorType.LightArmor));
        }

        /// <summary>8단계: 경장갑 시뮬레이션이 적용되면 파란색=가정 설명 대사 후 던전 선택 버튼을 하이라이트한다. OnTutorialSimulated에서 호출.</summary>
        public void OnTutorialSimulated(string dungeonStaticID, ArmorType armorType)
        {
            if (!isTutorialActive || secondGuestPhase != SecondGuestPhase.AwaitSim) return;
            if (dungeonStaticID != Config.TutorialDungeonBID || armorType != ArmorType.LightArmor) return;

            secondGuestPhase = SecondGuestPhase.AwaitSelect;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            // 왜 잘 어울리는지 = 성공률 영역(경갑 가정 시 오른 성공률)을 하이라이트하며 설명 → 종료 후 선택 버튼으로 재지정.
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetDungeonDetailSuccessRateAreaRect());
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestSimulationResult),
                onComplete: HighlightSecondGuestSelectButton, showPortrait: false, actionFollows: true);
        }

        private void HighlightSecondGuestSelectButton()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventurePreparationView>()?.GetDungeonDetailSelectButtonRect());
        }

        /// <summary>8단계: 던전 B가 선택되면 모험 정보의 '(미확정)' 표시를 짚어준 뒤 출발 유도로 넘어간다. OnTutorialDungeonSelected에서 분기.</summary>
        private void OnSecondGuestDungeonSelected()
        {
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.SetDungeonScrollLocked(false);
            prepView?.HideDungeonDetail();   // 상세를 닫아 모험 정보가 가려지지 않게 한다(선택은 상세 안에서 이뤄짐)
            // 모험 정보가 대사창 뒤라 대사 중엔 안 보인다 → 대사를 먼저 띄우고, 종료 후에 모험 정보를 하이라이트한다.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestInfoUnconfirmed),
                onComplete: HighlightSecondGuestInfo, actionFollows: true);
        }

        private void HighlightSecondGuestInfo()
        {
            // 대사 종료 후 모험 정보를 하이라이트한다. 칩을 클릭(확인)하면 출발 유도로 진행(6-D 칩 콜백 재사용).
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.SetInfoChipsTutorialCallback(OnSecondGuestInfoViewed);
            ShowHighlight(prepView?.GetAdventureInfoContainerRect());
        }

        private void OnSecondGuestInfoViewed()
        {
            var prepView = UIManager.Instance.GetPanel<AdventurePreparationView>();
            prepView?.SetInfoChipsTutorialCallback(null);   // 콜백 해제(중복 진행 방지)
            StartSecondGuestDepartGuide();
        }

        private void StartSecondGuestDepartGuide()
        {
            secondGuestPhase = SecondGuestPhase.AwaitDepart;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();   // 모험 정보 하이라이트 해제
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.SecondGuestDepartWhisper),
                onComplete: HighlightStartAdventureButton, actionFollows: true);
        }

        /// <summary>8단계 완료: 두 번째 모험이 출발하면 9단계(모험 결과)로 진행. OnTutorialAdventureStarted에서 분기.</summary>
        private void OnSecondGuestDeparted()
        {
            secondGuestPhase = SecondGuestPhase.None;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            AdvanceTo(TutorialStep.AdventureResult);
        }

        #endregion

        #region 튜토리얼 진행 (9단계: 모험 결과 — 9-A: 진행 관찰 + 수면 마법)

        // 9단계 하위 진행 상태. 두 모험(A/B)이 진행 중인 배경 상태에서 시작한다. 결과 확인은 전령(해럴드) 보고로 진행된다.
        // 9-A: AwaitProgressOpen~Sleeping / 9-B: AwaitSuccessReport~AwaitResultClose / 9-C: AwaitFailReport~AwaitFailResultClose.
        private enum AdventureResultPhase
        {
            None, AwaitProgressOpen, Observing, Sleeping,
            AwaitSuccessReport, AwaitMaterialClick, AwaitMaterialPurchase, AwaitResultClose,
            AwaitFailReport, AwaitFailResultClose
        }
        private AdventureResultPhase adventureResultPhase = AdventureResultPhase.None;

        // 진행 관찰 시간(강제 정지 해제) / 암전 페이드·유지 시간 — 런타임 조정용.
        private const float ObserveSeconds = 3f;
        private const float SleepFadeSeconds = 1.2f;
        private const float SleepHoldSeconds = 0.5f;

        // 수면 마법 암전 오버레이(코드 생성). 스킵 등으로 흐름이 끊겨도 남지 않게 참조를 보관해 종료 시 정리한다.
        private GameObject sleepOverlayGO;

        /// <summary>9-A 진입: 모험 버튼 안내 대사 후 하단바 모험 버튼을 하이라이트한다(minimalUI:false, 7단계 가방 패턴).</summary>
        private void StartStepAdventureResult()
        {
            adventureResultPhase = AdventureResultPhase.AwaitProgressOpen;
            OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultIntro),
                onComplete: HighlightAdventureButton, actionFollows: true);
        }

        private void HighlightAdventureButton()
        {
            ShowHighlight(GetBottomBarView()?.GetAdventureButtonRect(), minimalUI: false);
        }

        /// <summary>9-A: 모험 진행 화면이 열리면 하이라이트를 걷고 진행 관찰 → 수면 마법으로 이어간다. AdventureProgressController에서 호출.</summary>
        public void OnTutorialAdventureProgressOpened()
        {
            if (!isTutorialActive || adventureResultPhase != AdventureResultPhase.AwaitProgressOpen) return;

            adventureResultPhase = AdventureResultPhase.Observing;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            StartCoroutine(ObserveThenSleep());
        }

        /// <summary>
        /// 잠시 강제 정지를 풀어 두 모험이 진행되는 모습을 보여준 뒤(빈틈이라 배경 입력은 차단됨) 다시 정지하고,
        /// 진행 화면의 역할(진행 상황만 표시, 결과는 저녁에 전령이 보고)을 설명한 다음 수면 마법을 건다.
        /// </summary>
        private IEnumerator ObserveThenSleep()
        {
            // 관찰 구간엔 유도 클릭이 없으므로 모험 패널(PanelCanvas) 입력도 잠가 닫기 버튼 오조작을 막는다(짧은 구간이라 안전).
            SetPanelInputEnabled(false);
            TimeManager.Instance?.ForceResume();
            yield return new WaitForSeconds(ObserveSeconds);
            TimeManager.Instance?.ForcePause();
            SetPanelInputEnabled(true);

            OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultProgressInfo), onComplete: CastSleepMagic);
        }

        /// <summary>수면 마법 대사 후 암전 연출로 두 모험을 즉시 완료하고 저녁(18:00)으로 넘긴다.</summary>
        private void CastSleepMagic()
        {
            adventureResultPhase = AdventureResultPhase.Sleeping;
            // 902는 조작이 아니라 자동 연출(암전)로 이어지므로 actionFollows:false(Next 인디케이터).
            OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultSleepMagic),
                onComplete: () => StartCoroutine(SleepSequence()));
        }

        private IEnumerator SleepSequence()
        {
            var group = CreateSleepOverlay();

            // 암전(0 -> 1)
            yield return FadeOverlay(group, 0f, 1f, SleepFadeSeconds);

            // 검은 화면 뒤에서 진행 화면을 닫는다. 열어둔 채로 기상하면 전령(NPC 캔버스)이 패널 뒤에 깔려
            // 9-B 하이라이트가 가려지고 클릭도 패널이 먹어 진행이 멈춘다.
            // 완료 처리보다 먼저 닫아야 완료 이벤트가 닫힌 카드에 손대지 않는다.
            UIManager.Instance.ClosePanel<TutorialDialogueView>();
            UIManager.Instance.ClosePanel<AdventureProgressView>();

            // 같은 세션에서 9단계를 다시 재생(디버그 점프)하면 오늘 스냅샷이 이미 떠 있어 전령이 안 온다 - 초기화 후 다시 뜬다.
            AdventureManager.Instance?.ResetHeraldReport();
            // 완료를 먼저 해야 저녁 진입 시 전령이 두 모험을 보고 목록에 담는다(18:00 스냅샷).
            CompleteTutorialAdventures();
            TimeManager.Instance?.SkipToEvening();
            // 이미 18시 이후면 위 스킵이 no-op이라 시간 틱이 오지 않는다 - 전령 스폰을 명시적으로 보장한다(멱등).
            VisitorManager.Instance?.EnsureHeraldNow();

            yield return new WaitForSeconds(SleepHoldSeconds);

            // 기상(1 -> 0)
            yield return FadeOverlay(group, 1f, 0f, SleepFadeSeconds);
            DestroySleepOverlay();

            // 기상 대사(저녁 + 전령 도착 안내) → 9-B(전령 클릭 유도)로.
            OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultWakeUp),
                onComplete: StartSuccessResultGuide, actionFollows: true);
        }

        /// <summary>진행 중인 모험(튜토리얼 A/B)을 즉시 완료 처리한다. 판정은 출발 시 이미 고정(A 성공/B 실패).</summary>
        private void CompleteTutorialAdventures()
        {
            var ongoing = AdventureManager.Instance?.OngoingAdventures;
            if (ongoing == null) return;

            foreach (var adventure in ongoing.ToArray())   // CompleteAdventure가 목록에서 제거하므로 복사본 순회
                AdventureManager.Instance.CompleteAdventure(adventure);
        }

        // === 9-B: 전령 클릭 → 성공(A) 보고 리플레이 + 수수료 + 재료 매입 ===

        /// <summary>9-B 진입: 전령을 하이라이트해 보고(리플레이-결과) 흐름으로 유도한다.</summary>
        private void StartSuccessResultGuide()
        {
            adventureResultPhase = AdventureResultPhase.AwaitSuccessReport;

            // 전령이 없거나(비정상) 보고할 모험이 없으면 클릭 유도가 불가능해 진행이 멈춘다.
            // 보고 0건은 진행 중 모험이 복원에 실패해 드롭된 경우에 발생한다.
            var herald = VisitorManager.Instance?.CurrentHerald;
            if (herald == null || (AdventureManager.Instance?.PendingHeraldReportCount ?? 0) == 0)
            {
                Log.Warn("[TutorialManager] 9-B 전령/보고 대상이 없어 결과를 자동 확정하고 10단계로 건너뜁니다.");
                FallbackFinishAdventureResult();
                return;
            }

            // 입장 애니메이션 중이면 완료 후 하이라이트(걸어오는 중이라 위치가 어긋나고 버튼도 아직 클릭 불가).
            if (herald.isEntering)
            {
                herald.OnEnterCompleted += OnHeraldEntered;
                return;
            }

            ShowHighlight(herald.InteractionButtonRect);
            // 전령 클릭 → 보고 대사 → 리플레이(스킵 불가) → 결과창 자동 진입 → OnTutorialResultSequenceComplete로 이어짐.
        }

        private void OnHeraldEntered(VisitorNPC npc)
        {
            npc.OnEnterCompleted -= OnHeraldEntered;
            ShowHighlight(npc.InteractionButtonRect);
        }

        /// <summary>9-B: 전령을 클릭하면 하이라이트를 걷는다(대화창 위에 남지 않도록). VisitorNPC에서 호출.</summary>
        public void OnTutorialHeraldClicked()
        {
            if (!isTutorialActive) return;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
        }

        /// <summary>9-B/9-C: 결과 연출이 끝나면 성공(A)이면 수수료·매입, 실패(B)면 교훈으로 분기한다. AdventureResultView에서 호출(성공·실패 공용).</summary>
        public void OnTutorialResultSequenceComplete()
        {
            if (!isTutorialActive) return;

            if (adventureResultPhase == AdventureResultPhase.AwaitSuccessReport)
            {
                adventureResultPhase = AdventureResultPhase.AwaitMaterialClick;
                UIManager.Instance.ClosePanel<TutorialHighlightView>();
                OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultSuccessCommission),
                    onComplete: HighlightFirstMaterialItem, actionFollows: true);
            }
            else if (adventureResultPhase == AdventureResultPhase.AwaitFailReport)
            {
                // 9-C: 실패 결과창은 열릴 때 이미 활이 파괴됨(ConfirmAdventureResult). 교훈 대사 후 닫기 유도.
                adventureResultPhase = AdventureResultPhase.AwaitFailResultClose;
                UIManager.Instance.ClosePanel<TutorialHighlightView>();
                OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultFailureLesson),
                    onComplete: HighlightResultCloseButton, actionFollows: true);
            }
        }

        private void HighlightFirstMaterialItem()
        {
            var rect = UIManager.Instance.GetPanel<AdventureResultView>()?.GetFirstMaterialItemRect();
            if (rect == null)
            {
                // 안전장치: 성공 모험에 매입 가능한 재료가 없으면(드랍 롤 실패 등) 매입 단계를 건너뛰고 마무리로 간다.
                adventureResultPhase = AdventureResultPhase.AwaitResultClose;
                OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultDailyRoutine),
                    onComplete: HighlightResultCloseButton, actionFollows: true);
                return;
            }
            ShowHighlight(rect);
        }

        /// <summary>9-B: 재료 매입 팝업이 열리면 구매 버튼을 하이라이트한다. MaterialPurchasePopup에서 호출.</summary>
        public void OnTutorialMaterialPopupOpened()
        {
            if (!isTutorialActive || adventureResultPhase != AdventureResultPhase.AwaitMaterialClick) return;

            adventureResultPhase = AdventureResultPhase.AwaitMaterialPurchase;
            // 팝업이 하이라이트보다 나중에 열려 위에 있으므로, 하이라이트를 닫고 다시 열어 팝업 위로 올린다.
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            ShowHighlight(UIManager.Instance.GetPanel<MaterialPurchasePopup>()?.GetPurchaseButtonRect());
        }

        /// <summary>9-B: 재료를 매입하면 하루 요약 대사 후 결과창 닫기 버튼을 하이라이트한다. AdventureResultView에서 호출.</summary>
        public void OnTutorialMaterialPurchased()
        {
            if (!isTutorialActive || adventureResultPhase != AdventureResultPhase.AwaitMaterialPurchase) return;

            adventureResultPhase = AdventureResultPhase.AwaitResultClose;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            OpenDialogue(Config.GetLines(TutorialLineKey.AdventureResultDailyRoutine),
                onComplete: HighlightResultCloseButton, actionFollows: true);
        }

        private void HighlightResultCloseButton()
        {
            ShowHighlight(UIManager.Instance.GetPanel<AdventureResultView>()?.GetCloseButtonRect());
        }

        /// <summary>
        /// 9-B/9-C: 결과창을 닫으면 성공(A)이면 다음 보고 예고로, 실패(B)면 전령을 퇴장시키고 10단계로 넘어간다.
        /// 튜토리얼 중에는 AdventureResultController가 다음 보고를 직접 시작하지 않고 이 훅에 맡기므로,
        /// 두 분기 모두에서 ContinueHeraldReport를 반드시 이어줘야 보고가 진행되고 전령도 퇴장한다.
        /// </summary>
        public void OnTutorialResultClosed()
        {
            if (!isTutorialActive) return;

            if (adventureResultPhase == AdventureResultPhase.AwaitResultClose)
            {
                UIManager.Instance.ClosePanel<TutorialHighlightView>();
                StartFailureResultGuide();
            }
            else if (adventureResultPhase == AdventureResultPhase.AwaitFailResultClose)
            {
                adventureResultPhase = AdventureResultPhase.None;
                UIManager.Instance.ClosePanel<TutorialHighlightView>();
                ContinueHeraldReport();   // 남은 보고 0 → 전령이 마무리하고 퇴장한다
                AdvanceTo(TutorialStep.Quest);
            }
        }

        /// <summary>
        /// 9-C 진입: 성공 결과창을 닫으면 다음 모험을 예고하는 안내 대사를 먼저 띄우고,
        /// 대사가 끝난 뒤에 전령의 실패(B) 보고를 시작한다(안내 대사와 해럴드 보고 대사가 겹치지 않게 순서 제어).
        /// 이후 실패 결과창(활 파괴) → OnTutorialResultSequenceComplete(실패 분기)로 이어짐.
        /// </summary>
        private void StartFailureResultGuide()
        {
            adventureResultPhase = AdventureResultPhase.AwaitFailReport;

            // 전령이 사라졌거나 남은 보고가 없으면 실패(B) 보고가 이어지지 않아 진행이 멈춘다.
            if (VisitorManager.Instance?.CurrentHerald == null
                || (AdventureManager.Instance?.PendingHeraldReportCount ?? 0) == 0)
            {
                Log.Warn("[TutorialManager] 9-C 전령/보고 대상이 없어 결과를 자동 확정하고 10단계로 건너뜁니다.");
                FallbackFinishAdventureResult();
                return;
            }

            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.AdventureResultNextReport),
                onComplete: ContinueHeraldReport);
        }

        /// <summary>전령의 다음 보고를 시작한다. 남은 보고가 없으면 전령이 상호작용을 끝내고 퇴장한다.</summary>
        private void ContinueHeraldReport()
        {
            VisitorManager.Instance?.CurrentHerald?.ReportNextAdventure();
        }

        /// <summary>
        /// 안전장치(9-B/9-C): 전령이 없어 보고 유도가 불가능할 때, 진행 정지 대신
        /// 두 모험을 자동 완료·확정(catch-up과 동일, 멱등)하고 10단계로 진행한다.
        /// </summary>
        private void FallbackFinishAdventureResult()
        {
            adventureResultPhase = AdventureResultPhase.None;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            ApplyAdventureResultOutcome();   // A 보상·검 반납 / B 활 파괴 (CompletedAdventures 제거로 멱등)
            AdvanceTo(TutorialStep.Quest);
        }

        // === 암전 오버레이(코드 생성, 프리팹/인스펙터 불요) — 9단계 수면 마법 + 12단계 마무리 자막 공용 ===

        private CanvasGroup CreateSleepOverlay(string subtitle = null)
        {
            DestroySleepOverlay();   // 혹시 남아 있으면 정리(재진입 안전)

            sleepOverlayGO = new GameObject("TutorialSleepOverlay");
            var canvas = sleepOverlayGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;   // 팝업(500)보다 위 — 전체를 확실히 덮는다
            sleepOverlayGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();   // 암전 중 입력 흡수

            var group = sleepOverlayGO.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var imageGO = new GameObject("Black");
            imageGO.transform.SetParent(sleepOverlayGO.transform, false);
            var image = imageGO.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 12단계 마무리: 자막이 있으면 중앙에 텍스트를 얹는다(폰트 미지정 = TMP 기본 폰트 Maplestory, 한글 지원).
            if (!string.IsNullOrEmpty(subtitle))
            {
                var textGO = new GameObject("Subtitle");
                textGO.transform.SetParent(sleepOverlayGO.transform, false);
                var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
                text.text = WrapKoreanByWord(subtitle);   // 한글 어절 중간 줄바꿈 방지
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.enableWordWrapping = true;
                text.fontSize = 60f;
                text.color = Color.white;
                var trect = text.rectTransform;
                trect.anchorMin = new Vector2(0.1f, 0.4f);
                trect.anchorMax = new Vector2(0.9f, 0.6f);
                trect.offsetMin = Vector2.zero;
                trect.offsetMax = Vector2.zero;
            }

            return group;
        }

        /// <summary>
        /// 한글은 TMP에서 CJK로 취급돼 어절 중간(예: "시|간")에서도 줄바꿈된다.
        /// 공백으로 나눈 어절을 &lt;nobr&gt;로 묶어 공백(어절 경계)에서만 줄바꿈되게 한다.
        /// </summary>
        private static string WrapKoreanByWord(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains(' ')) return text;
            return string.Join(" ", text.Split(' ')
                .Select(w => w.Length == 0 ? w : $"<nobr>{w}</nobr>"));
        }

        private IEnumerator FadeOverlay(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        private void DestroySleepOverlay()
        {
            if (sleepOverlayGO != null)
            {
                Destroy(sleepOverlayGO);
                sleepOverlayGO = null;
            }
        }

        #endregion

        #region 튜토리얼 진행 (10단계: 퀘스트 소개)

        // 10단계 하위 진행 상태 — 조회 전용(퀘스트 화면 열기/닫기). 영구 결과물 없음.
        private enum QuestPhase { None, AwaitOpen, AwaitClose }
        private QuestPhase questPhase = QuestPhase.None;

        /// <summary>10단계 진입: 퀘스트 개념 안내 후 하단바 퀘스트 버튼을 하이라이트한다(minimalUI:false, 7·9단계 하단바 패턴).</summary>
        private void StartStepQuest()
        {
            questPhase = QuestPhase.AwaitOpen;
            OpenDialogue(Config.GetLines(TutorialLineKey.QuestIntro),
                onComplete: HighlightQuestButton, actionFollows: true);
        }

        private void HighlightQuestButton()
        {
            ShowHighlight(GetBottomBarView()?.GetQuestButtonRect(), minimalUI: false);
        }

        /// <summary>10단계: 퀘스트 화면이 열리면 요구/진행/보상 + 벌금·게임오버 경고를 설명한 뒤 닫기를 유도한다. QuestController에서 호출.</summary>
        public void OnTutorialQuestViewOpened()
        {
            if (!isTutorialActive || questPhase != QuestPhase.AwaitOpen) return;

            questPhase = QuestPhase.AwaitClose;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            // 퀘스트 화면이 막 열렸으니 Deferred로 조기 클릭을 막으며 안내(3박스). 끝나면 콘텐츠 영역을 짚어준다.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.QuestExplain),
                onComplete: HighlightQuestContent);
        }

        /// <summary>10단계: 설명 대사가 끝나면 퀘스트 콘텐츠 영역을 하이라이트로 짚어주고(짧은 안내 대사 동반), 그 대사 후 닫기를 유도한다.</summary>
        private void HighlightQuestContent()
        {
            // QuestInfo + 요구 조건 목록 영역을 바운딩 박스 합집합으로 하이라이트한 채 짧은 안내 대사를 얹는다
            // (초상은 콘텐츠를 가리지 않게 숨김). 대사 종료 후 닫기 버튼으로 재지정.
            var quest = UIManager.Instance.GetPanel<QuestView>();
            ShowHighlight(quest?.GetQuestInfoAreaRect(), quest?.GetRequirementContentRect());
            OpenDialogue(Config.GetLines(TutorialLineKey.QuestContentInfo),
                onComplete: HighlightQuestCloseButton, showPortrait: false, actionFollows: true);
        }

        private void HighlightQuestCloseButton()
        {
            ShowHighlight(UIManager.Instance.GetPanel<QuestView>()?.GetCloseButtonRect());
        }

        /// <summary>10단계 완료: 퀘스트 화면을 닫으면 11단계(나머지 UI)로 진행. QuestController에서 호출.</summary>
        public void OnTutorialQuestClosed()
        {
            if (!isTutorialActive || questPhase != QuestPhase.AwaitClose) return;

            questPhase = QuestPhase.None;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            AdvanceTo(TutorialStep.RemainingUI);
        }

        #endregion

        #region 튜토리얼 진행 (11단계: 나머지 UI)

        // 11단계는 상단바 요소(날짜 -> 시간 -> 골드 -> 평판 -> 배속 -> 유산)를 순서대로 하이라이트하며 설명하는 조회 전용 단계다(조작 0).
        // 외부 컨트롤러 훅 없이 대사 onComplete로만 다음 하이라이트로 넘어가는 순수 내부 체인이라 phase enum이 필요 없다.
        // minimalUI는 기본값(true) — minimal 모드가 상단바만 남기고 하단바/디버그를 숨기므로 상단바만 노출된다.

        /// <summary>11단계 진입: 상단바 첫 요소(날짜)부터 순차 하이라이트를 시작한다.</summary>
        private void StartStepRemainingUI()
        {
            HighlightTopBarDay();
            // 상단바(상시 UI, sortingOrder 300)를 가리키는 화살표가 상단바 뒤로 묻히지 않도록 이 단계에서만 화살표를 그 위로 올린다.
            // FinishRemainingUIStep의 ClosePanel -> Clear에서 자동 원복된다.
            UIManager.Instance.GetPanel<TutorialHighlightView>()?.SetArrowSortingOverride(301);
        }

        private void HighlightTopBarDay()
        {
            ShowHighlight(GetTopBarView()?.GetDayRect());
            OpenDialogue(Config.GetLines(TutorialLineKey.RemainingUIDay), onComplete: HighlightTopBarTime);
        }

        private void HighlightTopBarTime()
        {
            ShowHighlight(GetTopBarView()?.GetTimeRect());
            OpenDialogue(Config.GetLines(TutorialLineKey.RemainingUITime), onComplete: HighlightTopBarGold);
        }

        private void HighlightTopBarGold()
        {
            ShowHighlight(GetTopBarView()?.GetGoldRect());
            OpenDialogue(Config.GetLines(TutorialLineKey.RemainingUIGold), onComplete: HighlightTopBarReputation);
        }

        private void HighlightTopBarReputation()
        {
            ShowHighlight(GetTopBarView()?.GetReputationGroupRect());
            OpenDialogue(Config.GetLines(TutorialLineKey.RemainingUIReputation), onComplete: HighlightTopBarSpeed);
        }

        private void HighlightTopBarSpeed()
        {
            ShowHighlight(GetTopBarView()?.GetTimeButtonRect());
            OpenDialogue(Config.GetLines(TutorialLineKey.RemainingUISpeed), onComplete: HighlightTopBarLegacy);
        }

        private void HighlightTopBarLegacy()
        {
            ShowHighlight(GetTopBarView()?.GetLegacyGroupRect());
            OpenDialogue(Config.GetLines(TutorialLineKey.RemainingUILegacy), onComplete: FinishRemainingUIStep);
        }

        /// <summary>11단계 완료: 상단바 안내를 마치면 12단계(마무리)로 진행.</summary>
        private void FinishRemainingUIStep()
        {
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            AdvanceTo(TutorialStep.Finish);
        }

        #endregion

        #region 튜토리얼 진행 (12단계: 마무리)

        // 12단계(마지막)는 통찰(안목) 짧은 언급 + 작별 대사(2박스) 후 20:00으로 시간을 스킵하며 자막 연출을 거쳐 정식 완주한다.
        // 조작 0, 하이라이트 없음(안목은 화면에 보이는 스탯이 아니라 언급만). 외부 훅 없는 내부 대사 체인이라 phase enum 불필요.
        private const int FarewellSkipHour = 20;                  // 마무리 자막과 함께 스킵할 목표 시각(오후 8시)
        private const float FarewellSubtitleHoldSeconds = 2.0f;   // 자막을 읽을 여운(암전 홀드)

        /// <summary>12단계 진입: 안목(통찰) 언급 대사 후 작별 인사로 이어간다.</summary>
        private void StartStepFinish()
        {
            OpenDialogue(Config.GetLines(TutorialLineKey.FarewellInsight), onComplete: ShowFarewellGoodbye);
        }

        private void ShowFarewellGoodbye()
        {
            OpenDialogue(Config.GetLines(TutorialLineKey.FarewellGoodbye),
                onComplete: () => StartCoroutine(FarewellSequence()));
        }

        /// <summary>마무리 연출: 암전 오버레이 + 자막을 띄우고 20:00으로 스킵한 뒤 정식 완주(시간 재개)한다. 9단계 SleepSequence 미러.</summary>
        private IEnumerator FarewellSequence()
        {
            string subtitle = Config.GetLines(TutorialLineKey.FarewellSubtitle)?.FirstOrDefault();
            var group = CreateSleepOverlay(subtitle);

            // 암전 + 자막(0 -> 1)
            yield return FadeOverlay(group, 0f, 1f, SleepFadeSeconds);

            // 검은 화면 뒤에서: 작별 대사 닫기 + 20:00으로 스킵.
            UIManager.Instance.ClosePanel<TutorialDialogueView>();
            TimeManager.Instance?.SkipToHour(FarewellSkipHour);

            yield return new WaitForSeconds(FarewellSubtitleHoldSeconds);

            // 밝아짐(1 -> 0)
            yield return FadeOverlay(group, 1f, 0f, SleepFadeSeconds);
            DestroySleepOverlay();

            // 정식 완주 — 시간 재개(20:00부터 자유 플레이, 곧 21시 밤 진입).
            CompleteFullTutorial();
        }

        #endregion

        #region 튜토리얼 진행 (7단계: 가방 확인)

        // 7단계 하위 진행 상태 — 완료 훅(인벤토리 오픈/토글/카드 클릭/닫기)이 서로 다른 컨트롤러 콜백이라 phase로 순서를 강제한다.
        private enum InventoryPhase { None, AwaitBagOpen, AwaitRentedToggle, AwaitCardClick, AwaitClose }
        private InventoryPhase inventoryPhase = InventoryPhase.None;

        /// <summary>
        /// 7단계 진입: 첫 모험이 진행 중인 배경 상태에서 시작한다.
        /// 하단바 가방 버튼을 가리키려면 하단바가 보여야 하므로 대사/하이라이트를 minimalUI:false로 띄운다.
        /// </summary>
        private void StartStepInventory()
        {
            inventoryPhase = InventoryPhase.AwaitBagOpen;
            // 인트로 대사는 하단바를 숨긴 채(minimalUI 기본) 띄우고, 하단바는 다음 가방 버튼 하이라이트 딤에서만 노출한다.
            OpenDialogue(Config.GetLines(TutorialLineKey.InventoryIntro),
                onComplete: HighlightInventoryButton, actionFollows: true);
        }

        private void HighlightInventoryButton()
        {
            ShowHighlight(GetBottomBarView()?.GetInventoryButtonRect(), minimalUI: false);
        }

        /// <summary>7단계: 가방 버튼으로 인벤토리(무기 탭)가 열리면 관리 설명 + 대여중 토글을 유도한다. InventoryController에서 호출.</summary>
        public void OnTutorialInventoryOpened()
        {
            if (!isTutorialActive || inventoryPhase != InventoryPhase.AwaitBagOpen) return;

            inventoryPhase = InventoryPhase.AwaitRentedToggle;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            // 인벤토리가 막 열렸으니 Deferred로 조기 클릭을 막으며 안내(2박스). 마지막 박스가 토글 조작을 유도.
            OpenDialogueDeferred(Config.GetLines(TutorialLineKey.InventoryManageIntro),
                onComplete: HighlightRentedToggle, actionFollows: true);
        }

        private void HighlightRentedToggle()
        {
            ShowHighlight(UIManager.Instance.GetPanel<InventoryView>()?.GetShowRentedToggleRect());
        }

        /// <summary>7단계: 대여중 토글을 켜면 확인 대사 후 대여 무기 카드를 하이라이트해 클릭(상세 열기)을 유도한다. InventoryController에서 호출.</summary>
        public void OnTutorialRentedToggleOn()
        {
            if (!isTutorialActive || inventoryPhase != InventoryPhase.AwaitRentedToggle) return;

            inventoryPhase = InventoryPhase.AwaitCardClick;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            // 대화창이 카드를 가리므로, 먼저 대사로 설명하고 종료 후에 카드를 하이라이트한다(대사 종료 = 목록 스크롤 정착).
            OpenDialogue(Config.GetLines(TutorialLineKey.InventoryRentedWeapon),
                onComplete: HighlightRentedWeaponCard, actionFollows: true);
        }

        private void HighlightRentedWeaponCard()
        {
            // 대여 무기 카드를 클릭 가능한 대상으로 하이라이트(접근자에서 강제 리빌드). 클릭하면 상세 정보 패널이 열린다.
            ShowHighlight(UIManager.Instance.GetPanel<InventoryView>()?.GetRentedWeaponCardRect());
        }

        /// <summary>7단계: 대여 무기 카드를 눌러 상세 정보가 열리면 나머지 메뉴 안내로 넘어간다. InventoryController에서 호출.</summary>
        public void OnTutorialRentedWeaponDetailOpened()
        {
            if (!isTutorialActive || inventoryPhase != InventoryPhase.AwaitCardClick) return;

            inventoryPhase = InventoryPhase.AwaitClose;
            ShowInventoryOtherMenusGuide();
        }

        private void ShowInventoryOtherMenusGuide()
        {
            // 대여 카드 하이라이트를 걷고, 나머지 메뉴 언급 대사 후 닫기 버튼을 하이라이트한다.
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            OpenDialogue(Config.GetLines(TutorialLineKey.InventoryOtherMenus),
                onComplete: HighlightInventoryCloseButton, actionFollows: true);
        }

        private void HighlightInventoryCloseButton()
        {
            ShowHighlight(UIManager.Instance.GetPanel<InventoryView>()?.GetCloseButtonRect());
        }

        /// <summary>7단계 완료: 인벤토리를 닫으면 임시 완주(8단계 미구현). InventoryController에서 호출.</summary>
        public void OnTutorialInventoryClosed()
        {
            if (!isTutorialActive || inventoryPhase != InventoryPhase.AwaitClose) return;

            inventoryPhase = InventoryPhase.None;
            UIManager.Instance.ClosePanel<TutorialHighlightView>();
            AdvanceTo(TutorialStep.SecondGuest);
        }

        // BottomBarView는 BaseView가 아닌 상시 UI 상주 오브젝트라 UIManager.GetPanel로 못 얻는다(GetTopBarView와 동일 패턴).
        private BottomBarView bottomBarCache;
        private BottomBarView GetBottomBarView()
        {
            if (bottomBarCache == null) bottomBarCache = FindObjectOfType<BottomBarView>(true);
            return bottomBarCache;
        }

        #endregion

        #region 대화창 헬퍼

        /// <summary>
        /// 튜토리얼 밖에서 길드 전령 대화창을 띄운다 (시세 계단 공지 등). TutorialDialogueView를 그대로 재사용한다.
        /// 튜토리얼과 다른 점 두 가지:
        /// 다른 점은 스킵 버튼뿐이다 — 그 버튼은 튜토리얼 전체를 종료하므로 인게임에서 눌리면 안 된다.
        /// UI 노출은 튜토리얼 1~6단계와 동일하게 minimalUI(TopBar만 남김)를 쓴다.
        /// 대화창 자체는 시간을 멈추지 않으므로(튜토리얼은 이미 정지 상태 전제) 정지/재개는 호출 측이 감싼다.
        /// </summary>
        public void ShowHeraldNotice(IList<string> lines, HeraldPortrait portrait, Action onComplete = null)
        {
            if (lines == null || lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }
            OpenDialogue(lines, onComplete, showPortrait: true, actionFollows: false,
                         minimalUI: true, allowSkip: false, portraitOverride: portrait);
        }

        // minimalUI=true면 TopBar만 남기고 하단바·사이드바를 숨긴다(기본값, 1~6단계 공통).
        // 상시 UI 자체를 가리키는 단계(7=하단바 가방, 11=상단바)만 minimalUI:false로 대상 UI를 보이게 한다.
        // portraitOverride는 대사가 Config.steps에 없는 경로(시세 계단 공지 등)에서 초상을 직접 지정할 때 쓴다.
        private void OpenDialogue(IList<string> lines, Action onComplete, bool showPortrait = true, bool actionFollows = false, bool minimalUI = true, bool allowSkip = true, HeraldPortrait? portraitOverride = null)
        {
            UIManager.Instance.OpenPanel<TutorialDialogueView>(() =>
            {
                var view = UIManager.Instance.GetPanel<TutorialDialogueView>();
                view?.SetUIVisibility(hideUI: false, minimalUI: minimalUI);
                // 완주 계정만 상시 스킵 버튼 노출 (대화창은 12단계 공유 단일 패널이라 매 열림마다 갱신).
                // allowSkip=false는 튜토리얼 밖 사용(시세 공지 등) — 스킵 버튼은 튜토리얼 전체를 끝내므로 반드시 숨긴다.
                view?.SetSkipButtonVisible(allowSkip && HasEverCompletedTutorial);
                // 초상이 뒤 화면 요소를 가리는 단계(예: 4단계 하이라이트)는 showPortrait=false로 숨긴다.
                view?.SetPortraitVisible(showPortrait);
                // 대사 묶음별 상황 초상으로 교체(설정 없으면 GetPortraitSprite가 Default를, 매칭 실패 시 null→기존 유지).
                view?.SetPortrait(portraitOverride.HasValue
                    ? Config.GetPortraitSprite(portraitOverride.Value)
                    : Config.GetPortraitSprite(lines));
                // actionFollows면 대사 묶음 마지막 줄에 EndIndicator(조작 유도 직전), 그 외엔 NextIndicator.
                UIControllerManager.Instance.GetController<TutorialDialogueController>()
                    ?.ShowLines(lines, onComplete, actionFollows);
            });
        }

        // 상점 패널 open/close 직후에는 정렬 순서가 밀릴 수 있어, 한 프레임 뒤에 대화창을 띄운다.
        // 단, 직전 대사의 onComplete에서 곧바로 이어지는 대사(조작 없는 연속 대사)는 그 한 프레임 동안
        // 대화창이 꺼진 채로 렌더돼 깜빡인다(하단바도 함께 번쩍인다). 이 경우엔 패널이 갓 열린 상황이 아니라
        // 대기의 목적(정렬 밀림·조기 클릭 방지)이 없으므로 같은 프레임에 띄운다 - 닫기·열기가 한 프레임 안에
        // 끝나 화면에 드러나지 않는다.
        private void OpenDialogueDeferred(IList<string> lines, Action onComplete, bool showPortrait = true, bool actionFollows = false, bool minimalUI = true)
        {
            if (IsDialogueChaining())
            {
                OpenDialogue(lines, onComplete, showPortrait, actionFollows, minimalUI);
                return;
            }

            StartCoroutine(OpenDialogueNextFrame(lines, onComplete, showPortrait, actionFollows, minimalUI));
        }

        /// <summary>직전 대사의 종료 콜백을 실행하는 중인가(= 조작 없이 이어지는 대사).</summary>
        private bool IsDialogueChaining()
        {
            // 대화창이 아직 만들어지지 않았으면 이어지는 대사일 수 없다(컨트롤러 미등록 경고도 피한다).
            if (UIManager.Instance?.GetPanel<TutorialDialogueView>() == null) return false;
            return UIControllerManager.Instance?.GetController<TutorialDialogueController>()?.IsCompletingSequence ?? false;
        }

        private IEnumerator OpenDialogueNextFrame(IList<string> lines, Action onComplete, bool showPortrait, bool actionFollows, bool minimalUI = true)
        {
            // 방금 열린 패널(예: 모험 준비)의 버튼으로 버퍼/연타 클릭이 새는 것을 막는다.
            // 대화를 실제로 띄운(오버레이가 덮은) 뒤에 복구하므로, 재개 시점에 노출 프레임이 생기지 않는다.
            // 결과창 등은 이 대기 구간에 클릭하지 않으므로 영향받지 않는다.
            SetPanelInputEnabled(false);
            yield return null;
            OpenDialogue(lines, onComplete, showPortrait, actionFollows, minimalUI);
            SetPanelInputEnabled(true);
        }

        // PanelCanvas raycaster 제어는 TutorialInputBlocker(같은 오브젝트)에 위임한다. raycaster 참조는 blocker가 소유.
        private TutorialInputBlocker inputBlocker;
        private TutorialInputBlocker InputBlocker
        {
            get
            {
                if (inputBlocker == null)
                    inputBlocker = GetComponent<TutorialInputBlocker>() ?? FindObjectOfType<TutorialInputBlocker>();
                return inputBlocker;
            }
        }

        /// <summary>PanelCanvas 입력을 켜고 끈다(TutorialInputBlocker 경유). 대화 대기 동안 조기 클릭 차단용.</summary>
        private void SetPanelInputEnabled(bool enabled)
        {
            InputBlocker?.SetPanelInputBlocked(!enabled);
        }

        private void ShowTutorialWarning()
        {
            UIPopupController.Instance?.ShowPopup(L("Tutorial_BuyAllFour"), type: PopupSfxType.Warning);
        }

        /// <summary>지정 RectTransform을 하이라이트한다(대화창과 동일 상시 UI 정책, 이미 열려 있으면 대상만 교체).</summary>
        /// <param name="minimalUI">상시 UI 자체를 가리키는 단계(7=하단바)는 false로 대상 UI를 보이게 한다.</param>
        private void ShowHighlight(RectTransform target, bool minimalUI = true)
        {
            if (target == null)
            {
                Log.Warn("[TutorialManager] 하이라이트 대상 RectTransform이 null입니다.");
                return;
            }

            UIManager.Instance.OpenPanel<TutorialHighlightView>(() =>
                UIManager.Instance.GetPanel<TutorialHighlightView>()?.SetUIVisibility(hideUI: false, minimalUI: minimalUI));
            // 패널이 열리며 상시 UI가 방금 활성화됐을 수 있다(minimalUI:false면 하단바가 이때 SetActive(true)).
            // 활성화 직후 같은 프레임엔 레이아웃이 잡히지 않아 대상 좌표가 어긋나므로, 위치를 읽기 전에 대기 레이아웃을 즉시 확정한다.
            Canvas.ForceUpdateCanvases();
            var view = UIManager.Instance.GetPanel<TutorialHighlightView>();
            // 상시 UI(하단바 등, sortingOrder 300) 요소를 가리키는 단계(minimalUI:false)는 오버레이 전체를
            // 상시 UI 위(301)로 올려, 대상 버튼을 뺀 나머지 상시 UI 클릭까지 블로커가 흡수하게 한다.
            view?.SetOverlaySortingOverride(minimalUI ? 0 : ConsistenceUIAboveSortingOrder);
            view?.SetTarget(target);
        }

        /// <summary>두 대상의 바운딩 박스 합집합을 하이라이트한다(10단계 QuestInfo + 요구 조건 목록 영역).</summary>
        private void ShowHighlight(RectTransform primary, RectTransform secondary, bool minimalUI = true)
        {
            if (primary == null && secondary == null)
            {
                Log.Warn("[TutorialManager] 하이라이트 대상 두 RectTransform이 모두 null입니다.");
                return;
            }

            UIManager.Instance.OpenPanel<TutorialHighlightView>(() =>
                UIManager.Instance.GetPanel<TutorialHighlightView>()?.SetUIVisibility(hideUI: false, minimalUI: minimalUI));
            Canvas.ForceUpdateCanvases();
            var view = UIManager.Instance.GetPanel<TutorialHighlightView>();
            view?.SetOverlaySortingOverride(minimalUI ? 0 : ConsistenceUIAboveSortingOrder);
            // 한쪽이 null이면 나머지 단일 대상으로 폴백.
            if (primary != null && secondary != null) view?.SetTargets(primary, secondary);
            else view?.SetTarget(primary ?? secondary);
        }

        // 상시 UI 캔버스(ConsistenceCanvas)의 sortingOrder(300) 바로 위 값. 상시 UI 대상 하이라이트 시 오버레이를 이 위로 올린다.
        private const int ConsistenceUIAboveSortingOrder = 301;

        #endregion

        #region 밀린 단계 결과물 (catch-up)

        // catch-up이 적용하는 최상위 단계 = 9단계(모험 결과). 스킵/점프 결과가 정상 완주와 동일해지도록 두 모험을 모두 만들고 확정한다.
        // 5단계(준비 진입)·7단계(가방 확인)는 스폰/조회 전용이라 영구 결과물이 없고,
        // 6단계 = 첫 모험 시작(검 + 부적 + 점술 + 던전 A), 8단계 = 둘째 모험 시작(활 + 던전 B),
        // 9단계 = 두 모험 완료·확정(A 성공 보상·검 반납 / B 실패·활 파괴)이라는 영구 게임 상태를 만든다.
        private const int HighestOutcomeStep = (int)TutorialStep.AdventureResult;

        /// <summary>
        /// 건너뛴 앞 단계들의 게임 상태 결과물을 catch-up으로 적용한다(디버그 점프/완주 스킵 공용).
        /// [기록된 적용완료 다음 단계 .. throughStep] 범위를 순서대로 적용하고, 적용 범위를 GameData에 기록해 중복 적용을 막는다.
        /// </summary>
        private void ApplyCatchUpOutcomes(int throughStep)
        {
            var gameData = GameManager.Instance.GameData;
            int from = gameData.tutorialOutcomeAppliedThrough + 1;
            int to   = Mathf.Min(throughStep, HighestOutcomeStep);

            for (int s = from; s <= to; s++)
                ApplyStepOutcome((TutorialStep)s);

            gameData.tutorialOutcomeAppliedThrough = Mathf.Max(gameData.tutorialOutcomeAppliedThrough, to);
        }

        /// <summary>단일 단계의 게임 상태 결과물을 적용한다. 각 결과물은 자체적으로 멱등(중복 안전)하다.</summary>
        private void ApplyStepOutcome(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.WeaponShop:    GrantMissingTutorialWeapons(); break;  // 무료 무기 4종
                case TutorialStep.Blacksmith:    ApplyBlacksmithOutcome();      break;  // 재료 + 부적 제작 + 무기 강화/진화/재부여
                case TutorialStep.MorningEvent:  ApplyMorningEventOutcome();     break;  // 사절단 선물 + 아침 스킵
                case TutorialStep.QuestBoard:    ApplyQuestBoardOutcome();       break;  // 의뢰판 생성 + 던전 A 수색 파견
                case TutorialStep.PrepEnter:     break;                                  // 모험가 스폰 전용 - 영구 결과물 없음
                case TutorialStep.SendAdventure: ApplyFirstAdventureOutcome();   break;  // 첫 모험 시작(검 대여 + 부적 배정 + 점술 + 던전 A)
                case TutorialStep.Inventory:     break;                                  // 가방 조회 전용 - 영구 결과물 없음
                case TutorialStep.SecondGuest:   ApplySecondAdventureOutcome();  break;  // 둘째 모험 시작(활 대여 + 던전 B)
                case TutorialStep.AdventureResult: ApplyAdventureResultOutcome(); break;  // 두 모험 완료·확정(A 보상·검 반납 / B 활 파괴)
            }
            Log.Info($"[TutorialManager] catch-up 결과물 적용: {step}");
        }

        /// <summary>2단계 결과물: 재료 지급 -> 행운의 부적 제작 -> 조잡한 단검 강화·진화·재부여.</summary>
        private void ApplyBlacksmithOutcome()
        {
            GrantBlacksmithMaterials();   // 내부 플래그로 중복 지급 방지

            var bm = BlacksmithManager.Instance;
            var inventory = InventoryManager.Instance;
            if (bm == null || inventory == null || DataManager.Instance == null) return;

            // 행운의 부적 제작(이미 보유 중이면 건너뜀).
            bool hasCharm = inventory.GetAllActiveItems()
                .Any(i => i?.activeItemData != null && i.activeItemData.itemType == ActiveItemType.Charm);
            if (!hasCharm)
            {
                var charmRecipe = DataManager.Instance.GetAllActiveItemRecipes()
                    .FirstOrDefault(r => r?.resultItem != null && r.resultItem.itemType == ActiveItemType.Charm);
                if (charmRecipe != null) bm.ExecuteCraft(charmRecipe);
                else Log.Warn("[TutorialManager] 행운의 부적 레시피를 찾지 못했습니다.");
            }

            // 조잡한 단검 강화 -> 진화 -> 재부여. 각 단계 조건이 될 때만 실행하므로 일부만 진행된 상태에서도 중복 없이 이어서 완성된다.
            // 정상 진행(2-C)이 강화를 1회만 가르치므로 catch-up도 1회만 강화한다(강화레벨을 정상 완주와 일치).
            var dagger = inventory.GetAllWeapons()
                .FirstOrDefault(w => w?.weaponData != null && w.weaponData.StaticID == Config.EnforceWeaponID);
            if (dagger != null)
            {
                if (dagger.CanEnforce && bm.CanAffordEnforce(dagger)) bm.ExecuteEnforce(dagger);
                if (dagger.CanEvolve && bm.CanAffordEvolve(dagger)) bm.ExecuteEvolve(dagger);
                if (dagger.CanReroll) bm.ExecuteReroll(dagger, new HashSet<int>());
            }
        }

        /// <summary>3단계 결과물: 길드 사절단 선물(중복 방지) + 아침 스킵(낮 진입).</summary>
        private void ApplyMorningEventOutcome()
        {
            var gameData = GameManager.Instance.GameData;
            if (!gameData.hasClaimedTutorialGuildGift)
            {
                MorningEventManager.Instance?.ExecuteGuildEnvoy();   // 튜토리얼 가드: 선물 고정(강제납부 없음)
                gameData.hasClaimedTutorialGuildGift = true;
            }

            // 정상 3단계는 GuildEnvoyEventView가 MarkEventCompleted를 호출한다 - catch-up도 동일하게 오늘 아침 이벤트 완료로 표시(멱등).
            MorningEventManager.Instance?.MarkEventCompleted();

            TimeManager.Instance?.SkipMorning();   // 내부 IsMorning 가드라 낮이면 no-op(중복 안전)
        }

        /// <summary>4단계 결과물: 오늘 의뢰판 생성 보장 + 던전 A 수색꾼 파견.</summary>
        private void ApplyQuestBoardOutcome()
        {
            var qb = QuestBoardManager.Instance;
            if (qb == null) return;
            qb.EnsureTodayBoardGenerated();

            // 디버그 점프/스킵 경로: 정상 4단계 확정을 안 거쳤으면 고정 풀(A/B/C)로 확정한다.
            // (확정해야 GetAvailableDungeons가 A/B/C를 반환해 5·6단계 준비화면 던전 목록이 채워지고, C가 2배로 표식된다.)
            if (!qb.IsConfirmed)
                qb.ConfirmBoard(new List<string> { Config.TutorialDungeonAID, Config.TutorialDungeonBID, Config.TutorialDungeonCID });

            var dungeon = DataManager.Instance?.GetDungeon(Config.TutorialDungeonAID);
            var scout = ScoutManager.Instance;
            if (dungeon != null && scout != null)
            {
                if (scout.CanSendScout(dungeon)) scout.SendScout(dungeon);   // 튜토리얼 가드: 0원
                // 정상 6단계(StartFinalGuide)가 CompleteScoutImmediate로 A 수색을 완료시키므로 catch-up도 동일하게 완료(멱등: known이면 no-op).
                scout.CompleteScoutImmediate(dungeon);
            }
        }

        /// <summary>
        /// 6단계 결과물: 첫 모험을 실제로 시작한다(검 대여 + 부적 배정 + 던전 A 출발).
        /// 스킵/점프로 6단계를 건너뛰거나 6단계 도중 스킵한 경우에 실현한다. 이미 첫 모험이 진행 중이면
        /// (정상 완료 후 7·8단계, 또는 중복 호출) no-op. 준비화면 도중 스킵이면 부적이 유령 모험가에 배정된 채
        /// 남아 있으므로, 인벤토리로 회수한 뒤 새 모험가에 다시 배정해 첫 모험에 실현되게 한다.
        /// </summary>
        private void ApplyFirstAdventureOutcome()
        {
            var inv = InventoryManager.Instance;
            var advMgr = AdventureManager.Instance;
            if (inv == null || advMgr == null || DataManager.Instance == null) return;

            // 검이 이미 대여 중이면 첫 모험이 이미 진행 중 - 재생성하지 않는다(멱등).
            if (inv.GetAllWeapons().Any(w => w?.weaponData != null && w.weaponData.weaponType == WeaponType.Sword && w.isRented)) return;

            // 이 시점엔 실제 진행 중 모험이 없어 배정된 아이템은 모두 dangling(준비화면 유령 모험가 귀속) - 인벤 회수.
            ActiveItemManager.Instance?.ReturnAllAssignedItems();

            var advData = DataManager.Instance.GetAdventurer(Config.TutorialAdventurer1ID);
            var dungeon = DataManager.Instance.GetDungeon(Config.TutorialDungeonAID);
            var sword = inv.GetAllWeapons().FirstOrDefault(w => w?.weaponData != null
                && w.weaponData.weaponType == WeaponType.Sword && !w.isRented);

            if (advData == null || dungeon == null || sword == null)
            {
                Log.Warn("[TutorialManager] 첫 모험 catch-up 실패: 모험가/던전/검 데이터 누락.");
                return;
            }

            var adventurer = VisitorManager.Instance?.CreateTutorialAdventurer(advData);
            if (adventurer == null) return;

            // 행운의 부적을 배정 - StartAdventure가 소비하며 첫 모험에 실현된다.
            var charm = inv.GetAllActiveItems()
                .FirstOrDefault(i => i?.activeItemData != null && i.activeItemData.itemType == ActiveItemType.Charm);
            if (charm != null)
                ActiveItemManager.Instance?.AssignItem(charm.activeItemData, adventurer.instanceID);

            // 점술 상담(정상 6-D 재현) - StartAdventure가 운세 modifier를 읽으므로 반드시 출발 전에 상담한다. 튜토리얼 가드: 0원.
            if (SeerManager.Instance != null && SeerManager.Instance.CanConsult(adventurer, dungeon))
                SeerManager.Instance.Consult(adventurer, dungeon);

            advMgr.StartAdventure(adventurer, sword, dungeon);   // 내부에서 RentWeapon(검 대여) + ConsumeAssignedItem(부적) + 점술 운세 반영
            Log.Info("[TutorialManager] catch-up 첫 모험 시작(검 대여 + 부적 배정 + 점술 + 던전 A)");
        }

        /// <summary>
        /// 8단계 결과물: 둘째 모험을 시작한다(모험가2 → 던전 B, 물푸레나무 활 대여). 정상 완주가 모험 2개를 남기므로 스킵도 동일하게 맞춘다.
        /// 던전 B는 실패 설계(활 파괴 예정)로, 점술·부적 없이 출발한다. 이미 활이 대여 중이면 no-op(멱등).
        /// </summary>
        private void ApplySecondAdventureOutcome()
        {
            var inv = InventoryManager.Instance;
            var advMgr = AdventureManager.Instance;
            if (inv == null || advMgr == null || DataManager.Instance == null) return;

            // 활이 이미 대여 중이면 둘째 모험이 이미 진행 중 - 재생성하지 않는다(멱등).
            if (inv.GetAllWeapons().Any(w => w?.weaponData != null && w.weaponData.weaponType == WeaponType.Bow && w.isRented)) return;

            var advData = DataManager.Instance.GetAdventurer(Config.TutorialAdventurer2ID);
            var dungeon = DataManager.Instance.GetDungeon(Config.TutorialDungeonBID);
            var bow = inv.GetAllWeapons().FirstOrDefault(w => w?.weaponData != null
                && w.weaponData.weaponType == WeaponType.Bow && !w.isRented);

            if (advData == null || dungeon == null || bow == null)
            {
                Log.Warn("[TutorialManager] 둘째 모험 catch-up 실패: 모험가2/던전B/활 데이터 누락.");
                return;
            }

            var adventurer = VisitorManager.Instance?.CreateTutorialAdventurer(advData);
            if (adventurer == null) return;

            advMgr.StartAdventure(adventurer, bow, dungeon);   // 활 대여 + 던전 B 출발(점술·부적 없음, 정상 8단계와 동일)
            Log.Info("[TutorialManager] catch-up 둘째 모험 시작(활 대여 + 던전 B)");
        }

        /// <summary>
        /// 9단계 결과물: 두 모험을 완료·확정한다(정상 완주 최종 상태).
        /// 진행 중이면 완료 처리(판정 baked: A 성공/B 실패)하고, 완료된 모험을 확정(보상 지급 + A 검 반납 / B 활 파괴)한다.
        /// 멱등: 확정된 모험은 CompletedAdventures에서 제거되므로 재확정되지 않는다(이중 보상 없음).
        /// isTutorialActive 상태라 실패 던전(B)의 활 파괴 가드(CalculateRewards/ProcessRewards)가 그대로 적용된다.
        /// 9-B 재료 매입은 정상 진행에서 하이라이트로 강제되는 필수 단계이므로 catch-up도 첫 재료 1종을 동일 가격으로 매입한다.
        /// </summary>
        private void ApplyAdventureResultOutcome()
        {
            var advMgr = AdventureManager.Instance;
            if (advMgr == null) return;

            // 1) 진행 중이면 완료 처리(CompleteAdventure가 목록에서 제거하므로 복사본 순회).
            foreach (var adventure in advMgr.OngoingAdventures.ToArray())
                advMgr.CompleteAdventure(adventure);

            // 2) 9-B 필수 재료 매입 재현 - 확정 전(결과·모험가 접근 가능)에 성공(A)의 첫 재료를 매입한다.
            ApplyTutorialMaterialPurchase();

            // 3) 완료된 모험 확정(보상/반납/파괴). 확정 시 CompletedAdventures에서 제거되므로 복사본 순회 + 자동 멱등.
            foreach (var adventure in advMgr.CompletedAdventures.ToArray())
                advMgr.ConfirmAdventureResult(adventure);

            // 4) 오늘 전령 보고를 끝낸 상태로 표시 - 정상 완주(9-B/9-C에서 전령이 보고하고 퇴장)와 동일한 최종 상태.
            //    없으면 이어지는 20:00 스킵이 18시를 지날 때 EnsureHerald가 빈 목록으로 전령을 다시 부른다.
            advMgr.MarkHeraldReportFinishedToday();

            Log.Info("[TutorialManager] catch-up 두 모험 완료·확정(A 보상·검 반납 / B 활 파괴)");
        }

        /// <summary>
        /// 9-B 필수 재료 매입을 catch-up으로 재현한다. 성공(A) 완료 모험의 첫 재료(정상 결과창과 동일 정렬:
        /// Enforce->Special->Craft, buyPrice 내림차순)를 정상과 동일 가격(round(buyPrice x 수량 x 특성배율))으로
        /// 매입한다 - 골드 차감 + 인벤 추가. 이미 매입됐거나(정상 진행 후 스킵) A가 없으면 no-op(멱등).
        /// 결과창이 열린 상태에서 스킵한 경우까지 커버하려면 SkipRemainingTutorial이 CloseAllPanels보다 먼저
        /// catch-up을 돌려야 한다(컨트롤러가 살아 있어야 폴백 조회가 성립).
        /// </summary>
        private void ApplyTutorialMaterialPurchase()
        {
            var advMgr = AdventureManager.Instance;
            var inv = InventoryManager.Instance;
            var eco = EconomyManager.Instance;
            if (advMgr == null || inv == null || eco == null) return;

            // 성공(A) 던전의 완료(미확정) 모험 - 확정 전이라 CompletedAdventures에 있다.
            var aAdventure = advMgr.CompletedAdventures
                .FirstOrDefault(a => a?.dungeon?.StaticID == Config.TutorialDungeonAID);

            // 9-B 결과창을 연 뒤 스킵하면 A는 그 순간 이미 확정돼(OpenAndPlay -> ConfirmAdventureResult)
            // CompletedAdventures에서 빠진다. 결과창이 아직 들고 있는 모험으로 폴백해야 매입이 정상 완주와 동일하게 재현된다.
            if (aAdventure == null)
            {
                var openAdventure = UIControllerManager.Instance?.GetController<AdventureResultController>()?.CurrentAdventure;
                if (openAdventure?.dungeon?.StaticID == Config.TutorialDungeonAID) aAdventure = openAdventure;
            }
            if (aAdventure == null) return;

            var result = advMgr.CompletedResults.FirstOrDefault(r => r != null && r.adventureID == aAdventure.instanceID);
            if (result?.materialDrops == null || result.materialDrops.Count == 0) return;   // 드롭 없으면 매입할 것 없음
            if (result.purchasedMaterials != null && result.purchasedMaterials.Count > 0) return;   // 이미 매입(중복 방지)

            var first = result.materialDrops
                .Where(m => m?.materialData != null)
                .OrderBy(m => MaterialTypeSortRank(m.materialData.materialType))
                .ThenByDescending(m => m.materialData.buyPrice)
                .FirstOrDefault();
            if (first?.materialData == null) return;

            // 가격 = round(buyPrice x 수량 x 특성배율) - 정상 AdventureResultItem.TotalPrice와 동일 계산.
            float mult = advMgr.GetTraitMaterialPriceMultiplier(aAdventure.adventurer);
            int price = Mathf.RoundToInt(first.materialData.buyPrice * first.quantity * mult);

            // 차감 성공 시에만 지급한다 - 반환을 무시하면 골드 부족 시 무료 획득이 된다
            if (!eco.SpendGold(price, $"{first.materialData.materialName} 구매"))
            {
                Log.Warn($"[TutorialManager] catch-up 재료 매입 실패: 골드 부족(필요 {price}G)");
                return;
            }

            inv.AddMaterial(first.materialData.StaticID, first.quantity);
            result.purchasedMaterials.Add(new MaterialInstance(first.materialData, first.quantity));
            Log.Info($"[TutorialManager] catch-up 재료 매입: {first.materialData.materialName} x{first.quantity} ({price}G)");
        }

        /// <summary>정상 결과창(AdventureResultView)과 동일한 재료 정렬 우선순위(Enforce->Special->Craft).</summary>
        private static int MaterialTypeSortRank(MaterialType type) => type switch
        {
            MaterialType.Enforce => 0,
            MaterialType.Special => 1,
            MaterialType.Craft   => 2,
            _ => 3
        };

        /// <summary>강화 가능한(CanEnforce) WeaponInstance를 만든다 - Common 무기가 최댓값으로 굴려지면 재굴림.</summary>
        private WeaponInstance CreateEnforceableWeaponInstance(WeaponData data)
        {
            var weapon = new WeaponInstance(data);
            int attempts = 1;
            const int maxAttempts = 30;
            while (!weapon.CanEnforce && attempts < maxAttempts)
            {
                weapon = new WeaponInstance(data);
                attempts++;
            }
            return weapon;
        }

        #endregion

        #region 디버그

#if UNITY_EDITOR
        /// <summary>
        /// [디버그] 지정 단계부터 튜토리얼을 강제 시작한다(DebugMenuView에서 호출).
        /// 각 단계는 앞 단계 산출물을 전제하므로, 현재는 구현된 1(무기상점)/2(대장장이)단계만 온전히 동작한다.
        /// 단계 추가 시 DebugMenuView에 버튼을 한 줄 늘리면 된다.
        /// </summary>
        public void DebugStartFromStep(TutorialStep step)
        {
            isTutorialActive = true;
            TimeManager.Instance?.ForcePause();

            // 앞 단계의 잔여물 정리 — 남은 아침 NPC(무기상점 등)는 CanSkipMorning을 막고, 열린 대화창은 겹친다.
            UIManager.Instance?.CloseAllPanels();
            VisitorManager.Instance?.ClearAllVisitors();

            // 건너뛴 앞 단계 결과물을 catch-up으로 반영(무기·부적/강화·아침보상+아침스킵·수색 파견·첫 모험).
            // 7단계 이상은 '첫 모험 진행 중(검 대여)' 상태를 전제하는데, catch-up이 6단계(첫 모험)를 포함하므로 자동 재현된다.
            ApplyCatchUpOutcomes((int)step - 1);

            // 3단계 아침 스킵이 의뢰판을 자동으로 여는데, 목표가 의뢰판이 아니면 겹치므로 닫는다.
            if (step != TutorialStep.QuestBoard)
                UIManager.Instance?.ClosePanel<QuestBoardView>();

            GameManager.Instance.GameData.tutorialStep = (int)step;
            SnapshotStepEntry();   // 점프 지점 진입 스냅샷 — 이후 강제종료 시 이 단계로 복원된다.

            if (step == TutorialStep.WeaponShop)
            {
                VisitorManager.Instance?.SpawnTutorialWeaponShop();
                BeginStep1Intro();
            }
            else
            {
                StartStep(step);
            }

            Log.Info($"[TutorialManager] 디버그 단계 시작: {step}");
        }
#endif

        /// <summary>
        /// 현재 단계의 진입 스냅샷을 gamedata.json에 강제 저장한다(mid-step 억제를 우회하는 유일한 저장 지점).
        /// 각 단계 진입 시점에만 호출되므로 저장 파일은 항상 "단계 진입 상태"를 유지하고, 중도 강제종료는 이 지점으로 복원된다.
        /// </summary>
        private void SnapshotStepEntry()
        {
            GameManager.Instance.SaveGame(force: true);
            Log.Info($"[TutorialManager] 단계 진입 스냅샷 저장 (tutorialStep={GameManager.Instance.GameData.tutorialStep})");
        }

        /// <summary>튜토리얼 시작 시 에디터 전용 디버그 바를 띄운다. 빌드에서는 빈 메서드.</summary>
        private void ShowDebugBar()
        {
#if UNITY_EDITOR
            TutorialDebugBar.Show();
#endif
        }

        #endregion

        #region 유틸리티

        /// <summary>1일차인지 확인</summary>
        public bool IsFirstDay() => TimeManager.Instance.CurrentDay == 1;

        /// <summary>튜토리얼 미완료로 시간 진행을 막아야 하는지</summary>
        public bool ShouldBlockTimeResume() => isTutorialActive && !HasCompletedTutorial;

        /// <summary>
        /// 닫기/뒤로가기(ESC·버튼) 가드. 튜토리얼 중이면 안내 토스트를 띄우고 true를 반환한다.
        /// </summary>
        public bool GuardBack()
        {
            if (!isTutorialActive) return false;
            UIPopupController.Instance?.ShowToast(L("Tutorial_CompleteFirst"), type: PopupSfxType.Warning);
            return true;
        }

        private VisitorNPC GetTutorialShop()
        {
            return VisitorManager.Instance?.ActiveVisitors?
                .FirstOrDefault(v => v != null && v.visitorType == VisitorType.WeaponShop);
        }

        private VisitorNPC GetTutorialBlacksmith()
        {
            return VisitorManager.Instance?.ActiveVisitors?
                .FirstOrDefault(v => v != null && v.visitorType == VisitorType.Blacksmith);
        }

        /// <summary>TutorialConfig의 튜토리얼 무기를 전 종류 보유 중인지 여부(1단계 완료 조건).</summary>
        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private bool HasAllTutorialWeapons()
        {
            var inventory = InventoryManager.Instance;
            if (inventory == null) return false;

            var owned = inventory.GetAllWeapons();
            if (owned == null) return false;

            foreach (string id in Config.TutorialWeaponIDs)
            {
                if (!owned.Any(w => w?.weaponData != null && w.weaponData.StaticID == id))
                    return false;
            }
            return true;
        }

        #endregion
    }
}
