using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;
using Spine.Unity;

namespace TodaysWeaponRental
{
    public class VisitorNPC : MonoBehaviour
    {
        [Header("Visitor Settings")]
        public VisitorType visitorType;
        public AdventurerInstance adventurerInstance;
        public VisitorEventData eventData;
        public DeadEventKind deadEventKind;   // DeadEvent 타입일 때 세부 종류
        public BlacksmithData blacksmithData;
        
        [Header("UI")]
        [SerializeField] private Button interactionButton;

        /// <summary>튜토리얼 하이라이트가 지목할 상호작용 버튼의 RectTransform.</summary>
        public RectTransform InteractionButtonRect =>
            interactionButton != null ? interactionButton.transform as RectTransform : null;
        
        [Header("State")]
        public bool isInteracting;
        public bool hasBeenClicked;
        public bool isEffected;

        public event System.Action<VisitorNPC> OnEnterCompleted;
         
        [Header("Stay System (게임 시간 분 단위)")]
        [SerializeField] private float stayDuration;
        [SerializeField] private float baseStayDuration;
        [SerializeField] private float elapsedTime;
        public bool wasWarningState = false;
        public float RemainingTime => Mathf.Max(0, stayDuration - elapsedTime);

        /// <summary>
        /// 입장 보정 전 원래 체류 시간 — 사이드바 게이지의 최대치 분모로 사용
        /// </summary>
        public float BaseStayDuration => baseStayDuration;
        
        [Header("Animation")]
        public bool isEntering;
        public bool isLeaving;
        public bool IsWaiting;
        public int InteractionPointIndex { get; private set; } = -1; // 점유 중인 포인트 인덱스

        private AdventurerAppearanceApplier appearanceApplier;

        // 아래길 좌/우 어느 쪽에서 들어왔는지 (퇴장은 반대쪽으로 나간다)
        private bool entryFromLeft;

        // point0(문) 통과 후 가게 안에서는 무조건 왼쪽을 본다
        private bool faceLeftLock;

        // 현재 진행 중인 이동 트윈 (게임 배속 변경 시 timeScale 실시간 갱신용)
        private Tweener moveTween;

        // 원근 정렬: y가 높을수록 sortingOrder를 낮춰 뒤로 (이동으로 y가 바뀌므로 매 프레임 갱신)
        private MeshRenderer spineRenderer;
        private bool depthSortingSuspended;

        private VisitorConfig Config => ConfigManager.Instance.Visitor;

        private bool IsTutorial => TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;

        /// <summary>방문자 이동 속도. 튜토리얼 중에는 대기 시간을 줄이려 전 구간을 튜토리얼 속도로 고정한다.</summary>
        private float MoveSpeed => IsTutorial ? Config.tutorialMoveSpeed : Config.visitorMoveSpeed;

        /// <summary>
        /// 사이드바 버튼을 만들어도 되는 상태 — 입장/대기/퇴장이 아닌 '응대 준비 완료'.
        /// 상호작용 슬롯 점유자와 전령 포인트 도착자가 여기 해당한다.
        /// </summary>
        public bool IsReadyForSidebar => !isEntering && !IsWaiting && !isLeaving;

        /// <summary>
        /// 이 방문자에 적용된 Spine 외형 데이터. 사이드바 버튼 등에서 동일 외형을 재현하는 데 사용한다.
        /// </summary>
        public AdventurerAppearanceData Appearance { get; private set; }

        public float StayDuration => stayDuration;

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        private static string LArg(string key, string argName, object value)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Messages", key,
                   arguments: new object[] { new Dictionary<string, object> { { argName, value } } });

        private static string LCommon(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", key);

        #region 초기화
        
        private void Awake()
        {
            interactionButton?.onClick.AddListener(OnInteractionButtonClicked);
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeScaleChanged += HandleTimeScaleChanged;
                TimeManager.Instance.OnTimePausedChanged += HandleTimePausedChanged;
            }
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeScaleChanged -= HandleTimeScaleChanged;
                TimeManager.Instance.OnTimePausedChanged -= HandleTimePausedChanged;
            }
            interactionButton?.onClick.RemoveAllListeners();
        }

        // 게임 배속에 맞춰 진행 중인 이동 트윈의 재생 속도를 실시간으로 맞춘다.
        private void HandleTimeScaleChanged(float scale)
        {
            if (moveTween != null && moveTween.IsActive())
                moveTween.timeScale = scale;
        }

        /// <summary>
        /// 시간 정지에 반응하지 않는 방문자.
        /// - 튜토리얼: 시간을 강제 정지한 채 NPC 연출을 진행한다.
        /// - 전령: 21시 자동 정지 시점에 이동 중이면 도착하지 못하고, 21시 이후에는 시간 재개도 막혀 있어
        ///   보고를 받을 수 없게 된다(다음날 진행 불가).
        /// - 밤(21시 자동 정지): 전령만 움직이고 나머지가 통째로 얼어붙으면 어색하므로 가던 길은 마저 가게 둔다.
        /// </summary>
        private bool IgnoresTimePause => IsTutorial || visitorType == VisitorType.Herald
            || (TimeManager.Instance != null && TimeManager.Instance.IsDayEnded());

        /// <summary>
        /// 시간 정지 동기화. 이동 트윈은 실시간으로 도는 DOTween이라 정지에 반응하지 않으므로 직접 멈춘다.
        /// 이동 중일 때만 Spine 재생도 함께 멈춘다(대기·상호작용 중 Idle이 얼어붙지 않도록).
        /// </summary>
        private void HandleTimePausedChanged(bool paused)
        {
            if (paused && IgnoresTimePause) return;

            bool isMoving = moveTween != null && moveTween.IsActive();
            if (isMoving)
            {
                if (paused) moveTween.Pause();
                else moveTween.Play();
            }

            if (paused && !isMoving) return;
            SetSpineTimeScale(paused ? 0f : 1f);
        }

        private void SetSpineTimeScale(float scale)
        {
            var skeletonAnim = GetComponentInChildren<SkeletonAnimation>();
            if (skeletonAnim != null)
            {
                skeletonAnim.timeScale = scale;
                return;
            }

            var skeletonGraphic = GetComponentInChildren<SkeletonGraphic>();
            if (skeletonGraphic != null)
                skeletonGraphic.timeScale = scale;
        }

        // DOMove 트윈에 현재 배속을 적용하고 참조를 보관한다(이후 배속 변경 반영용).
        private Tweener ApplyScale(Tweener tween)
        {
            if (tween == null) return null;
            tween.timeScale = TimeManager.Instance != null ? TimeManager.Instance.CurrentTimeScale : 1f;
            tween.SetLink(gameObject);
            moveTween = tween;

            // 정지 중에 시작된 이동(퇴장 등)은 곧바로 멈춘 상태로 둔다.
            if (TimeManager.Instance != null && TimeManager.Instance.IsTimePaused && !IgnoresTimePause)
            {
                tween.Pause();
                SetSpineTimeScale(0f);
            }
            return tween;
        }

        private void Update() => ApplyDepthSorting();

        // y가 높을수록(멀수록) sortingOrder를 낮춰 뒤로. 안전 대역 안으로 클램프(매니저가 계산).
        private void ApplyDepthSorting()
        {
            // 대화 등 외부가 sortingOrder를 직접 제어하는 동안에는 건드리지 않는다.
            if (depthSortingSuspended) return;

            if (spineRenderer == null)
            {
                var sa = GetComponentInChildren<SkeletonAnimation>();
                if (sa == null) return;
                spineRenderer = sa.GetComponent<MeshRenderer>();
                if (spineRenderer == null) return;
            }

            var mgr = VisitorManager.Instance;
            if (mgr == null) return;
            spineRenderer.sortingOrder = mgr.ComputeSortingOrder(transform.position.y);
        }

        /// <summary>대화 등에서 sortingOrder를 직접 제어할 동안 자동 원근 정렬을 멈춘다.</summary>
        public void SuspendDepthSorting() => depthSortingSuspended = true;
        public void ResumeDepthSorting() => depthSortingSuspended = false;
        
        public void Initialize(VisitorType type, AdventurerInstance adventurer = null, VisitorEventData evt = null, bool skipEnterAnimation = false)
        {
            visitorType = type;
            adventurerInstance = adventurer;
            eventData = evt;
            appearanceApplier = GetComponentInChildren<AdventurerAppearanceApplier>();

            if (type == VisitorType.Blacksmith)
            {
                blacksmithData = BlacksmithManager.Instance?.CurrentBlacksmith;
            }
            
            SetStayDuration();
            elapsedTime = 0f;
            wasWarningState = false;
            hasBeenClicked = false;
            isInteracting = false;
            isEffected = false;
            isEntering = true;
            isLeaving = false;
            IsWaiting = false;
            InteractionPointIndex = -1;

            if (adventurer != null)
            {
                adventurer.currentVisitor = this;
            }
            
            if (interactionButton != null)
            {
                interactionButton.interactable = false;
            }

            if (type == VisitorType.Adventurer && adventurer?.appearance != null)
            {
                ApplyAppearance(adventurer.appearance);
            }

            // 전령은 상호작용 슬롯 점유 시스템을 쓰지 않는다. 자리가 없어 대기열로 밀리면 클릭이 불가능해져
            // 21시 게이트가 영구히 잠기기 때문에, 항상 전용 포인트에 선다.
            if (type == VisitorType.Herald)
            {
                if (skipEnterAnimation)
                {
                    entryFromLeft = Random.value < 0.5f;   // 퇴장 방향용
                    transform.position = VisitorManager.Instance.GetHeraldPosition();
                    isEntering = false;
                    faceLeftLock = true;   // 가게 안 배치 - 왼쪽 보기
                    FaceLeft();
                    if (interactionButton != null)
                        interactionButton.interactable = true;
                }
                else
                {
                    PlayHeraldEnterAnimation();
                }
                return;
            }

            if (skipEnterAnimation)
            {
                // 복원/시간스킵 즉시 배치. 슬롯 있으면 점유, 없으면 대기열에 줄세운다.
                entryFromLeft = Random.value < 0.5f;   // 퇴장 방향용(입장 애니가 없어 방향 정보가 없음)
                isEntering = false;
                faceLeftLock = true;   // 가게 안 배치 - 왼쪽 보기
                FaceLeft();

                var manager = VisitorManager.Instance;
                int index = manager.GetAvailableInteractionPointIndex();
                if (index >= 0)
                {
                    manager.OccupyInteractionPoint(index, this);
                    InteractionPointIndex = index;
                    transform.position = manager.GetInteractionPointPosition(index);
                    if (interactionButton != null)
                        interactionButton.interactable = true;
                }
                else
                {
                    manager.JoinWaitingQueue(this);
                    IsWaiting = true;
                    int qIndex = manager.GetWaitingIndex(this);
                    transform.position = manager.GetQueuePointPosition(qIndex);
                    if (interactionButton != null)
                        interactionButton.interactable = false;   // 대기자는 클릭 불가
                }
            }
            else
            {
                isEntering = true;
                if (interactionButton != null)
                    interactionButton.interactable = false;
                PlayEnterAnimation();
            }
        }

        /// <summary>
        /// 타이머 업데이트 (게임 시간 분 단위)
        /// </summary>
        public void UpdateTimer(float gameMinutes)
        {
            if (isInteracting || isLeaving || IsWaiting)
                return;

            // 입장 중에는 흐른 만큼 stayDuration도 함께 늘려 RemainingTime을 유지한다.
            // 입장이 끝나는 순간 RemainingTime은 정확히 baseStayDuration으로 남는다.
            if (isEntering)
                stayDuration += gameMinutes;

            elapsedTime += gameMinutes;
        }

        /// <summary>
        /// SaveData로부터 상태 복원 (입장 애니메이션 없이)
        /// </summary>
        public void RestoreFromSaveData(VisitorNPCSaveData saveData)
        {
            float duration = StayDuration;
            elapsedTime = Mathf.Max(0, duration - saveData.remainingTime);

            isEntering = false;
            isLeaving = false;
            isInteracting = false;
            // IsWaiting은 Initialize(skipEnterAnimation:true)에서 자리 유무에 따라 이미 올바르게 설정됨 — 덮어쓰지 않음
            hasBeenClicked = saveData.isInteracting;

            if (interactionButton != null)
                interactionButton.interactable = !IsWaiting;   // 대기자는 클릭 불가 유지
        }

        #endregion

        #region 상호작용
        
        public void StartInteraction()
        {
            isInteracting = true;
            TimeManager.Instance.PauseTime();

            // 줌은 상호작용 전체 동안 유지된다(시작대화 → 패널 → 종료대화). 모험가는 자체 줌 로직 사용.
            if (visitorType != VisitorType.Adventurer)
                CameraZoomController.Instance?.ZoomIn(GetZoomTargetPosition());

            switch (visitorType)
            {
                case VisitorType.Adventurer:
                    AdventurerInteraction();
                    break;
                case VisitorType.WeaponShop:
                    WeaponShopInteraction();
                    break;
                case VisitorType.Blacksmith:
                    BlacksmithInteraction();
                    break;
                case VisitorType.EventNPC:
                    EventNPCInteraction();
                    break;
                case VisitorType.InvestorResult:
                    InvestorResultInteraction();
                    break;
                case VisitorType.DeadEvent:
                    DeadEventInteraction();
                    break;
                case VisitorType.Herald:
                    BeginHeraldReport();
                    break;
            }
        }

        private void AdventurerInteraction()
        {
            // 튜토리얼 중엔 줌 애니메이션 동안 하이라이트 위치가 못 따라가 어색해지므로 클릭 즉시 걷는다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialAdventurerClicked();

            Vector2 targetXY = transform.position;

            if (CameraZoomController.Instance == null || CameraZoomController.Instance.IsAnimating)
            {
                EndInteraction();
                return;
            }

            VisitorManager.Instance.SetOtherVisitorsFaded(true, this);

            CameraZoomController.Instance.ZoomIn(targetXY, () =>
            {
                // NPC 대화 스킵이 켜져 있거나 튜토리얼 중이면 인사 화면을 건너뛰고 바로 모험 준비 화면을 연다.
                // (무기상점/대장장이 인사 대화 스킵과 동일. 줌은 이미 위에서 실행됨.)
                bool skipGreeting = (LegacyManager.Instance?.PlayerData != null
                    && !LegacyManager.Instance.PlayerData.isNPCDialogueEnabled)
                    || (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive);

                if (skipGreeting)
                {
                    UIManager.Instance.OpenPanel<AdventurePreparationView>(() =>
                        UIControllerManager.Instance.GetController<AdventurePreparationController>()
                            ?.Initialize(adventurerInstance, QuestBoardManager.Instance.GetAvailableDungeons()));
                }
                else
                {
                    UIManager.Instance.OpenPanel<AdventureDialogueView>(() =>
                        UIControllerManager.Instance.GetController<AdventureDialogueController>()
                            ?.Initialize(adventurerInstance));
                }
            });
        }

        private void WeaponShopInteraction()
        {
            // 튜토리얼 중에는 전령이 내레이션을 맡으므로 상점 인사 대화를 생략하고 바로 상점을 연다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                UIManager.Instance.OpenPanel<WeaponShopView>(() =>
                    UIControllerManager.Instance.GetController<WeaponShopController>()
                        ?.Initialize(this));
                return;
            }

            // 인사말은 주차 구간별로 다르다 (WeaponShopConfig.merchantGreetings)
            var textReplacements = new Dictionary<string, string>
            {
                { "TIER_GREETING", WeaponShopManager.Instance.GetGreetingLine() }
            };

            DialogueManager.Instance.StartDialogue("WeaponShopGreeting", () =>
            {
                // 대화 종료 후 무기 상점 열기
                UIManager.Instance.OpenPanel<WeaponShopView>(() =>
                    UIControllerManager.Instance.GetController<WeaponShopController>()
                        ?.Initialize(this));
            }, textReplacements, spineTarget: this);
        }

        private void BlacksmithInteraction()
        {
            // 튜토리얼 중에는 전령이 내레이션을 맡으므로 대장장이 인사 대화를 생략하고 바로 대장간을 연다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                UIManager.Instance?.OpenPanel<BlacksmithView>(() =>
                    UIControllerManager.Instance.GetController<BlacksmithController>()
                        ?.Initialize(this));
                return;
            }

            // 동적 대화 ID 생성
            string dialogueID = BlacksmithManager.Instance?.GetGreetingDialogueID() ?? "Blacksmith_DefaultGreeting";

            // 동적 치환 데이터 준비
            var textReplacements = new Dictionary<string, string>
            {
                { "BLACKSMITH_NAME", BlacksmithManager.Instance.GetBlacksmithName() },
                { "GREETING", BlacksmithManager.Instance.GetGreetingText() }
            };

            // 대화 시작 (동적 치환 포함). 돌려보내기 확인은 화자 이름 대신 "대장장이"로 통일.
            DialogueManager.Instance.StartDialogue(dialogueID, () =>
            {
                UIManager.Instance?.OpenPanel<BlacksmithView>(() =>
                    UIControllerManager.Instance.GetController<BlacksmithController>()
                        ?.Initialize(this));
            }, textReplacements, spineTarget: this,
            escCancelMessage: UIPopupController.SendBackConfirmMessage(
                UITranslator.GetString(VisitorType.Blacksmith)));
        }

        private void EventNPCInteraction()
        {
            if (eventData == null)
            {
                Log.Warn("[VisitorNPC] EventNPC에 eventData가 없습니다.");
                EndInteraction();
                return;
            }

            // 튜토리얼 3단계: 사절단을 클릭했으므로 NPC 하이라이트를 걷는다(이벤트 창 위에 남지 않도록).
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialMorningEventOpened();

            MorningEventManager.Instance?.StartEvent(eventData.morningEventType, this);
        }

        // ── 투자자 결과 NPC ────────────────────────────────
        private string investorResultDialogueID;
        private int investorReturnedGold;

        public string InvestorDialogueID  => investorResultDialogueID;
        public int    InvestorReturnedGold => investorReturnedGold;

        public void InitializeInvestorResult(string dialogueID, int returnedGold)
        {
            investorResultDialogueID = dialogueID;
            investorReturnedGold = returnedGold;
        }

        private void InvestorResultInteraction()
        {
            DialogueManager.Instance?.StartDialogue(investorResultDialogueID, OnInvestorResultDialogueEnd, spineTarget: this);
        }

        private void OnInvestorResultDialogueEnd()
        {
            if (investorReturnedGold > 0)
                EconomyManager.Instance?.AddGold(investorReturnedGold, "투자 결과");

            // 컨트롤러 등록을 보장하기 위해 패널을 먼저 인스턴스화한 뒤 결과창을 연다
            UIManager.Instance?.GetOrInstantiatePanel<InvestorEventView>();
            var ctrl = UIControllerManager.Instance?.GetController<InvestorEventController>();
            if (ctrl != null)
                ctrl.OpenResult(this, investorResultDialogueID, investorReturnedGold);
            else
                EndInteraction();
        }

        // ── 사망 모험가 이벤트 NPC ────────────────────────────────

        private void DeadEventInteraction()
        {
            switch (deadEventKind)
            {
                case DeadEventKind.NoVisitor:     NoVisitorInteraction();     break;
                case DeadEventKind.ReviveOffer:   ReviveOfferInteraction();   break;
                case DeadEventKind.MiracleRevive: MiracleReviveInteraction(); break;
            }
        }

        // 청소 이벤트: "일 한번 할라우?" → 수락 시 시간 경과 + 골드
        private void NoVisitorInteraction()
        {
            var dialogue = VisitorManager.Instance.NoVisitorDialogue;
            if (dialogue == null)
            {
                Log.Error("[VisitorNPC] noVisitorDialogue가 설정되지 않았습니다!");
                VisitorManager.Instance.SkipToNextSpawn();
                EndInteraction();
                return;
            }

            int reward = VisitorManager.Instance.CleaningReward;
            var textReplacements = new Dictionary<string, string>
            {
                { "REWARD", reward.ToString() }
            };

            bool accepted = false;
            SoundManager.Instance?.EnterTheme("NoVisitor");
            DialogueManager.Instance.StartDialogue(
                dialogue.StaticID,
                onComplete: () =>
                {
                    SoundManager.Instance?.ExitTheme("NoVisitor");
                    if (accepted)
                    {
                        EconomyManager.Instance?.AddGold(reward, "잡일 보상");
                        VisitorManager.Instance.SkipToNextSpawn();
                        UIPopupController.Instance?.ShowToast(LArg("Visitor_ChoreRewardToast", "gold", reward.ToString("N0")));
                    }
                    EndInteraction();
                },
                textReplacements: textReplacements,
                spineTarget: this,
                onChoiceSelected: idx => { if (idx == 0) accepted = true; });
        }

        // 부활 제안: 수락(골드 지불) / 거절
        private void ReviveOfferInteraction()
        {
            var dialogue = VisitorManager.Instance.ReviveOfferDialogue;
            if (dialogue == null || adventurerInstance == null)
            {
                Log.Error("[VisitorNPC] reviveOfferDialogue 또는 대상 모험가가 없습니다!");
                EndInteraction();
                return;
            }

            int reviveCost = VisitorManager.Instance.ReviveCost;

            // 부활 비용을 낼 수 없으면 제안 대신 NoVisitor 이벤트로 전환한다.
            // 기존에는 수락해도 안내 없이 조용히 끝나 유저가 이유를 알 수 없었다
            if (EconomyManager.Instance.CurrentGold < reviveCost)
            {
                Log.Info($"[VisitorNPC] 부활 비용 부족({reviveCost}G) - NoVisitor 이벤트로 전환");
                deadEventKind = DeadEventKind.NoVisitor;
                NoVisitorInteraction();
                return;
            }

            var textReplacements = new Dictionary<string, string>
            {
                { "ADVENTURER_NAME", adventurerInstance.Name },
                { "REVIVE_COST", reviveCost.ToString() }
            };

            bool accepted = false;
            SoundManager.Instance?.EnterTheme("ReviveOffer");
            DialogueManager.Instance.StartDialogue(
                dialogue.StaticID,
                onComplete: () =>
                {
                    SoundManager.Instance?.ExitTheme("ReviveOffer");
                    if (accepted)
                    {
                        // 차감 성공 시에만 부활 - 실패하면 안내 후 종료
                        if (EconomyManager.Instance.SpendGold(reviveCost, "모험가 부활"))
                        {
                            VisitorManager.Instance.ReviveAdventurer(adventurerInstance.instanceID);
                            UIPopupController.Instance?.ShowToast(LArg("Visitor_RevivedToast", "name", adventurerInstance.Name));
                        }
                        else
                        {
                            UIPopupController.Instance?.ShowToast(L("Visitor_ReviveNotEnoughGold"), type: PopupSfxType.Warning);
                        }
                    }
                    EndInteraction();
                },
                textReplacements: textReplacements,
                spineTarget: this,
                onChoiceSelected: idx => { if (idx == 0) accepted = true; });
        }

        // 기적의 생환: 죽은 모험가 본인이 무료로 귀환
        private void MiracleReviveInteraction()
        {
            var dialogue = VisitorManager.Instance.MiracleReviveDialogue;
            if (dialogue == null || adventurerInstance == null)
            {
                Log.Error("[VisitorNPC] miracleReviveDialogue 또는 대상 모험가가 없습니다!");
                if (adventurerInstance != null)
                    VisitorManager.Instance.ReviveAdventurer(adventurerInstance.instanceID);
                EndInteraction();
                return;
            }

            var textReplacements = new Dictionary<string, string>
            {
                { "ADVENTURER_NAME", adventurerInstance.Name }
            };

            SoundManager.Instance?.EnterTheme("MiracleRevive");
            DialogueManager.Instance.StartDialogue(
                dialogue.StaticID,
                onComplete: () =>
                {
                    SoundManager.Instance?.ExitTheme("MiracleRevive");
                    VisitorManager.Instance.ReviveAdventurer(adventurerInstance.instanceID);
                    UIPopupController.Instance?.ShowToast(LArg("Visitor_MiracleRevivedToast", "name", adventurerInstance.Name));
                    EndInteraction();
                },
                textReplacements: textReplacements,
                spineTarget: this,
                escSkippable: true);   // 선택지 없는 순수 대화 — ESC로도 즉시 완료(부활) 처리
        }

        // ── 모험 결과 보고 전령 ────────────────────────────────

        private const string HeraldGreetingDialogueID = "HERALD_REPORT_GREETING";
        private const string HeraldReportDialogueID = "HERALD_REPORT_ADVENTURE";
        private const string HeraldDoneDialogueID   = "HERALD_REPORT_DONE";
        private const string HeraldNoneDialogueID   = "HERALD_REPORT_NONE";

        // 이번 상호작용에서 보고를 한 건이라도 진행했는지 (마무리 대화 분기용)
        private bool hasReportedAny;

        /// <summary>
        /// 다음 미보고 모험의 결과를 연다. 결과창이 닫힐 때마다 다시 호출되어 전부 확인할 때까지 이어진다.
        /// 남은 보고가 없으면 마무리 대화 후 퇴장한다.
        /// </summary>
        /// <summary>전령 클릭 진입점. 인사 대사로 보고 건수를 알린 뒤 첫 모험 결과로 넘어간다.</summary>
        private void BeginHeraldReport()
        {
            // 튜토리얼 9-B: 전령을 클릭했으므로 하이라이트를 걷는다(대화창 위에 남지 않도록).
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialHeraldClicked();

            int count = AdventureManager.Instance?.PendingHeraldReportCount ?? 0;
            if (count <= 0)
            {
                ReportNextAdventure();   // 보고할 모험이 없으면 인사를 건너뛰고 마무리 대사로
                return;
            }

            var textReplacements = new Dictionary<string, string>
            {
                { "COUNT", count.ToString() }
            };

            StartHeraldDialogue(HeraldGreetingDialogueID, textReplacements, ReportNextAdventure);
        }

        public void ReportNextAdventure()
        {
            var advMgr = AdventureManager.Instance;
            var adventure = advMgr?.GetNextHeraldAdventure();

            if (adventure == null)
            {
                // 튜토리얼 중에는 마무리 대화가 다음 단계 대사와 겹치므로 생략하고 바로 퇴장한다.
                if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                {
                    EndInteraction();
                    return;
                }

                string dialogueID = hasReportedAny ? HeraldDoneDialogueID : HeraldNoneDialogueID;
                StartHeraldDialogue(dialogueID, null, EndInteraction);
                return;
            }

            hasReportedAny = true;

            int total = advMgr.PendingHeraldReportCount;
            var textReplacements = new Dictionary<string, string>
            {
                { "ADVENTURER_NAME", adventure.adventurer?.Name ?? LCommon("Common_Adventurer") },
                { "DUNGEON_NAME", adventure.dungeon?.DisplayName ?? LCommon("Common_Dungeon") },
                { "TOTAL", total.ToString() }
            };

            StartHeraldDialogue(HeraldReportDialogueID, textReplacements, () => OpenAdventureResult(adventure));
        }

        /// <summary>
        /// 전령 대화를 시작한다. 대화 SO가 없으면 DialogueManager가 조용히 리턴해 onComplete가 영영 호출되지 않고
        /// 보고 시퀀스(따라서 21시 게이트)가 잠기므로, 그때는 대화를 건너뛰고 다음 단계로 바로 넘어간다.
        /// </summary>
        private void StartHeraldDialogue(string dialogueID, Dictionary<string, string> textReplacements, System.Action onComplete)
        {
            if (DataManager.Instance.GetDialogue(dialogueID) == null)
            {
                Log.Warn($"[VisitorNPC] 전령 대화 '{dialogueID}'를 찾지 못해 대화를 건너뜁니다. DialogueData SO를 생성하세요.");
                onComplete?.Invoke();
                return;
            }

            DialogueManager.Instance.StartDialogue(dialogueID,
                onComplete: onComplete,
                textReplacements: textReplacements,
                spineTarget: this,
                escSkippable: true);
        }

        /// <summary>모험 스킵 옵션이 켜져 있으면 리플레이를 건너뛰고 바로 결과창을 연다.</summary>
        private void OpenAdventureResult(AdventureInstance adventure)
        {
            if (LegacyManager.Instance.PlayerData.isAdventureSkipEnabled)
            {
                UIManager.Instance.GetOrInstantiatePanel<AdventureResultView>();
                UIControllerManager.Instance.GetController<AdventureResultController>()?.OpenAndPlay(adventure);
                return;
            }

            UIManager.Instance.GetOrInstantiatePanel<AdventureReplayView>();
            UIControllerManager.Instance.GetController<AdventureReplayController>()?.OpenAndPlay(adventure);
        }

        /// <summary>
        /// 전령 상호작용 종료 처리. 보고가 남았는데 중단됐다면(대화 ESC 등) 퇴장시키지 않고
        /// 다시 클릭할 수 있게 되돌린다 - 전령이 사라지면 21시 게이트가 영구히 잠기기 때문.
        /// 남은 보고가 없으면 정상 퇴장하고, 밤이면 다음날 진행 팝업을 다시 띄운다.
        /// </summary>
        private void EndHeraldInteraction()
        {
            isInteracting = false;
            CameraZoomController.Instance?.ZoomOut();

            if (AdventureManager.Instance != null && AdventureManager.Instance.HasPendingHeraldReport)
            {
                hasBeenClicked = false;
                if (interactionButton != null)
                    interactionButton.interactable = true;
                return;
            }

            VisitorManager.Instance.RemoveVisitor(this);
            TimeManager.Instance?.PromptNextDayIfDayEnded();
        }

        public void EndInteraction()
        {
            if (visitorType == VisitorType.Herald)
            {
                EndHeraldInteraction();
                return;
            }

            isInteracting = false;

            // 상호작용 종료 시 줌 세션도 종료. 모험가는 자체 줌 로직 사용(여기서 관여하지 않음).
            if (visitorType != VisitorType.Adventurer)
                CameraZoomController.Instance?.ZoomOut();

            if (TutorialManager.Instance.IsTutorialActive && !TutorialManager.Instance.HasCompletedTutorial)
                return;

            switch (visitorType)
            {
                case VisitorType.WeaponShop:
                    VisitorManager.Instance.OnWeaponShopInteractionCompleted();
                    break;
                case VisitorType.Blacksmith:
                    VisitorManager.Instance.OnBlacksmithInteractionCompleted();
                    break;
                case VisitorType.EventNPC:
                    VisitorManager.Instance.OnEventNPCInteractionCompleted();
                    // 튜토리얼 3단계: 사절단의 마무리 대화까지 끝난 시점 — 전령 대사로 이어간다.
                    if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                        TutorialManager.Instance.OnTutorialMorningEventCompleted();
                    break;
                default:
                    break;
            }

            // 일반 방문자는 항상 제거
            VisitorManager.Instance.RemoveVisitor(this);
        }

        /// <summary>
        /// 대기 중 빈 포인트가 생겼을 때 VisitorManager에서 호출
        /// </summary>
        /// <summary>
        /// 대기열 머리로서 빈 슬롯으로 승격될 때 VisitorManager가 호출한다.
        /// (호출 시점엔 이미 waitingQueue에서 빠져 있고 슬롯이 하나 비어 있음)
        /// </summary>
        public void OnInteractionPointReleased()
        {
            if (isLeaving) return;

            int index = VisitorManager.Instance.GetAvailableInteractionPointIndex();
            if (index < 0) return; // 정상적으론 방금 비었으므로 항상 있음

            IsWaiting = false;
            EnterSlot(index);
        }
        
        public void StartLeaving()
        {
            isLeaving = true;
            IsWaiting = false;
            InteractionPointIndex = -1;

            if (interactionButton != null)
                interactionButton.interactable = false;

            PlayLeaveAnimation();
        }

        #endregion

        #region 애니메이션
        
        // ── 입장(방문자): 아래길 랜덤 접근 -> point0(문·SFX·감속) -> point1 -> 슬롯/대기열 ──
        private void PlayEnterAnimation()
        {
            var manager = VisitorManager.Instance;
            if (!manager.PathReady)
            {
                Log.Warn("[VisitorNPC] 경로 포인트가 설정되지 않아 입장 애니메이션을 건너뜁니다.");
                OnEnterComplete();
                return;
            }

            Vector3 p0 = manager.EntryPoint0;
            faceLeftLock = false;                          // 접근 구간은 이동 방향대로 본다
            entryFromLeft = transform.position.x < p0.x;   // 진입 방향 저장(퇴장은 반대쪽)

            // 문 앞까지는 x만 맞춘다 - 스폰 y를 유지해 문 y로 내려갔다 되올라오는 움직임 방지
            Vector3 doorPos = new Vector3(p0.x, transform.position.y, transform.position.z);

            // 접근 구간: 행인과 동일한 랜덤 속도 + Walk/Run (튜토리얼 중에는 고정 속도)
            float approachSpeed = IsTutorial
                ? Config.tutorialMoveSpeed
                : Random.Range(Config.walkerMinSpeed, Config.walkerMaxSpeed);
            SetFacingDirection(doorPos - transform.position);
            SetSpineMove(approachSpeed);

            float duration = Vector3.Distance(transform.position, doorPos) / Mathf.Max(0.01f, approachSpeed);
            DOTween.Kill(transform);
            ApplyScale(transform.DOMove(doorPos, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(false))
                .OnComplete(OnReachedDoor);
        }

        // point0(문) 도착: 방문 SFX + 방문자 속도로 감속하여 point1로. 이후 가게 안이라 무조건 왼쪽.
        private void OnReachedDoor()
        {
            SoundManager.Instance.PlaySFX("VisitorEnter");
            faceLeftLock = true;
            WalkTo(VisitorManager.Instance.EntryPoint1, MoveSpeed, OnReachedPoint1);
        }

        // point1 도착: 슬롯 판단. 빈 슬롯 있으면 곧장 상호작용 포인트로, 없으면 대기열로.
        private void OnReachedPoint1()
        {
            var manager = VisitorManager.Instance;
            int index = manager.GetAvailableInteractionPointIndex();

            if (index >= 0)
            {
                EnterSlot(index);
            }
            else
            {
                manager.JoinWaitingQueue(this);
                IsWaiting = true;
                int qIndex = manager.GetWaitingIndex(this);
                WalkTo(manager.GetQueuePointPosition(qIndex), MoveSpeed, StopSpineWalk);
                Log.Info($"[VisitorNPC] {name} 대기열 {qIndex}번에서 대기");
            }
        }

        // 빈 슬롯을 즉시 예약(경합 방지)하고 상호작용 포인트로 곧장 이동한다.
        private void EnterSlot(int index)
        {
            var manager = VisitorManager.Instance;
            manager.OccupyInteractionPoint(index, this);
            InteractionPointIndex = index;
            MoveToInteractionPoint(index);
        }

        // 상호작용 포인트로 Y축 -> X축 순서 이동. 슬롯 점유는 EnterSlot에서 이미 처리됨.
        private void MoveToInteractionPoint(int index)
        {
            float speed = MoveSpeed;
            Vector3 targetPos = VisitorManager.Instance.GetInteractionPointPosition(index);
            Vector3 currentPos = transform.position;

            Vector3 midPos = new Vector3(currentPos.x, targetPos.y, currentPos.z);
            float durationY = Mathf.Abs(targetPos.y - currentPos.y) / speed;
            float durationX = Mathf.Abs(targetPos.x - currentPos.x) / speed;

            SetFacingDirection(midPos - currentPos);
            SetSpineMove(speed);

            DOTween.Kill(transform);
            void MoveX()
            {
                if (durationX > 0.01f)
                {
                    SetFacingDirection(targetPos - transform.position);
                    SetSpineMove(speed);
                    ApplyScale(transform.DOMove(targetPos, durationX)
                        .SetEase(Ease.Linear)
                        .SetUpdate(false))
                        .OnComplete(OnEnterComplete);
                }
                else
                {
                    transform.position = targetPos;
                    OnEnterComplete();
                }
            }

            if (durationY > 0.01f)
            {
                ApplyScale(transform.DOMove(midPos, durationY)
                    .SetEase(Ease.Linear)
                    .SetUpdate(false))
                    .OnComplete(MoveX);
            }
            else
            {
                MoveX();
            }
        }

        /// <summary>대기열 재정렬 시 자기 순번 슬롯으로 걸어 전진 (VisitorManager.RepositionQueue에서 호출).</summary>
        public void MoveToQueueSlot(int index)
        {
            if (isLeaving || isInteracting) return;

            Vector3 target = VisitorManager.Instance.GetQueuePointPosition(index);
            if (Vector3.Distance(transform.position, target) < 0.01f) return;

            WalkTo(target, MoveSpeed, StopSpineWalk);
        }

        /// <summary>전령 입장: 아래길 -> point0(문·SFX) -> point1 -> 전령 포인트. 속도 3 고정.</summary>
        private void PlayHeraldEnterAnimation()
        {
            var manager = VisitorManager.Instance;
            float speed = Config.heraldMoveSpeed;

            if (!manager.PathReady)
            {
                SoundManager.Instance.PlaySFX("VisitorEnter");
                WalkTo(manager.GetHeraldPosition(), speed, OnEnterComplete);
                return;
            }

            Vector3 p0 = manager.EntryPoint0;
            faceLeftLock = false;
            entryFromLeft = transform.position.x < p0.x;

            // 방문자와 동일하게 문 앞까지는 x만 맞춘다
            Vector3 doorPos = new Vector3(p0.x, transform.position.y, transform.position.z);

            SetFacingDirection(doorPos - transform.position);
            SetSpineMove(speed);
            float duration = Vector3.Distance(transform.position, doorPos) / Mathf.Max(0.01f, speed);
            DOTween.Kill(transform);
            ApplyScale(transform.DOMove(doorPos, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(false))
                .OnComplete(() =>
                {
                    SoundManager.Instance.PlaySFX("VisitorEnter");
                    faceLeftLock = true;
                    WalkTo(manager.EntryPoint1, speed, () =>
                        WalkTo(manager.GetHeraldPosition(), speed, OnEnterComplete));
                });
        }

        // ── 퇴장(전원): point1 -> point0(문·SFX) -> 진입 반대쪽 화면 밖. 방문자 속도 고정. ──
        private void PlayLeaveAnimation()
        {
            var manager = VisitorManager.Instance;
            float speed = MoveSpeed;

            if (!manager.PathReady)
            {
                OnLeaveComplete();
                return;
            }

            faceLeftLock = false;   // 퇴장은 이동 방향대로 본다(가게 밖으로 나감)
            Vector3 exitPos = manager.GetBottomRoadExitPosition(entryFromLeft);

            // 문 통과 지점의 y를 퇴장 지점에 맞춘다 - 문 y까지 내려갔다 되올라가는 움직임 방지
            Vector3 doorPos = new Vector3(manager.EntryPoint0.x, exitPos.y, exitPos.z);

            WalkTo(manager.EntryPoint1, speed, () =>
                WalkTo(doorPos, speed, () =>
                {
                    SoundManager.Instance.PlaySFX("VisitorLeave");
                    WalkTo(exitPos, speed, OnLeaveComplete);
                }));
        }

        /// <summary>target까지 speed(유닛/초)로 이동하고 완료 시 onComplete. 애니메이션은 속도에 따라 Walk/Run.</summary>
        private void WalkTo(Vector3 target, float speed, TweenCallback onComplete)
        {
            SetFacingDirection(target - transform.position);
            SetSpineMove(speed);

            float duration = Vector3.Distance(transform.position, target) / Mathf.Max(0.01f, speed);
            DOTween.Kill(transform);
            ApplyScale(transform.DOMove(target, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(false))
                .OnComplete(onComplete);
        }

        /// <summary>
        /// 이동 방향에 따라 캐릭터 좌우 반전
        /// </summary>
        private void SetFacingDirection(Vector3 direction)
        {
            // 가게 안(point0 통과 후)에서는 이동 방향과 무관하게 항상 왼쪽을 본다.
            if (faceLeftLock)
            {
                FaceLeft();
                return;
            }

            if (Mathf.Abs(direction.x) < 0.001f) return;

            Vector3 scale = transform.localScale;
            scale.x = direction.x < 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        // 왼쪽 보기 = scale.x 양수 (SetFacingDirection에서 direction.x < 0일 때와 동일)
        private void FaceLeft()
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        // 속도에 따라 Walk/Run 선택 (모든 이동 구간 공용)
        private void SetSpineMove(float speed)
        {
            if (speed > Config.walkerRunSpeedThreshold)
                SetSpineRun();
            else
                SetSpineWalk();
        }

        private void SetSpineWalk()
        {
            var partsManager = appearanceApplier?.GetPartsManager();
            partsManager?.PlayAnimation("Walk");
        }

        private void SetSpineRun()
        {
            var partsManager = appearanceApplier?.GetPartsManager();
            partsManager?.PlayAnimation("Run");
        }

        private void StopSpineWalk()
        {
            var partsManager = appearanceApplier?.GetPartsManager();
            partsManager?.PlayAnimation("Idle");
        }

        #endregion

        #region 이벤트 핸들러

        public void OnInteractionButtonClicked()
        {
            if (isInteracting || hasBeenClicked || isEntering || isLeaving || IsWaiting) return;
            if (CameraZoomController.Instance != null && CameraZoomController.Instance.IsAnimating) return;

            hasBeenClicked = true;

            // 방문 횟수 누적 — hasBeenClicked 게이트로 같은 방문 중복 카운트 방지
            if (visitorType == VisitorType.Adventurer && adventurerInstance != null)
            {
                adventurerInstance.adventurerStatData.visitCount++;
                adventurerInstance.adventurerStatData.lastVisitDay = TimeManager.Instance.CurrentDay;
            }
            
            if (interactionButton != null)
                interactionButton.interactable = false;
            
            StartInteraction();
        }
        
        private void OnEnterComplete()
        {
            isEntering = false;
            StopSpineWalk();

            if (interactionButton != null)
                interactionButton.interactable = true;

            OnEnterCompleted?.Invoke(this);
        }
        
        private void OnLeaveComplete()
        {
            StopSpineWalk();
            VisitorManager.Instance?.OnVisitorLeaveComplete(this);
        }

        #endregion

        #region 헬퍼 메서드

        /// <summary>
        /// Spine 외형을 적용하고, 동일 외형을 재현할 수 있도록 Appearance에 보관한다.
        /// </summary>
        public void ApplyAppearance(AdventurerAppearanceData appearance)
        {
            Appearance = appearance;

            if (appearanceApplier == null)
                appearanceApplier = GetComponentInChildren<AdventurerAppearanceApplier>();

            appearanceApplier?.ApplyAppearance(appearance);
        }

        public void ApplyAppearance(FixedAppearanceData fixedData)
            => ApplyAppearance(fixedData?.appearance);

        /// <summary>
        /// 체류 시간 설정 (게임 시간 분 단위)
        /// </summary>
        private void SetStayDuration()
        {
            var config = ConfigManager.Instance.Visitor;

            stayDuration = visitorType switch
            {
                VisitorType.WeaponShop => config.weaponShopStayDuration,
                VisitorType.Blacksmith => config.blacksmithStayDuration,
                VisitorType.Adventurer => adventurerInstance != null && adventurerInstance.adventurerStatData.hasReachedMaxAffection
                    ? config.maxAffectionStayDuration
                    : config.adventurerStayDuration,
                VisitorType.EventNPC      => config.eventNPCStayDuration,
                VisitorType.InvestorResult => config.eventNPCStayDuration,
                VisitorType.DeadEvent      => config.eventNPCStayDuration,
                VisitorType.Herald         => config.heraldStayDuration,
                _ => config.adventurerStayDuration
            };

            baseStayDuration = stayDuration;
        }

        public bool GetWarningState(float threshold)
        {
            return RemainingTime <= threshold;
        }

        /// <summary>
        /// 카메라 줌인 대상 월드 좌표 (NPC 본체 위치).
        /// </summary>
        public Vector2 GetZoomTargetPosition()
        {
            return transform.position;
        }

        public int GetSortingOrder()
        {
            var skeletonAnim = GetComponentInChildren<SkeletonAnimation>();
            var meshRenderer = skeletonAnim != null ? skeletonAnim.GetComponent<MeshRenderer>() : null;
            return meshRenderer != null ? meshRenderer.sortingOrder : 0;
        }

        public void SetSortingOrder(int order)
        {
            var skeletonAnim = GetComponentInChildren<SkeletonAnimation>();
            var meshRenderer = skeletonAnim != null ? skeletonAnim.GetComponent<MeshRenderer>() : null;
            if (meshRenderer != null)
                meshRenderer.sortingOrder = order;
        }

        public void SetFaded(bool faded, float fadedAlpha = 0.2f)
        {
            float targetAlpha = faded ? fadedAlpha : 1f;

            var skeletonAnim = GetComponentInChildren<SkeletonAnimation>();
            if (skeletonAnim != null)
            {
                DOTween.To(
                    () => skeletonAnim.skeleton.A,
                    x => skeletonAnim.skeleton.A = x,
                    targetAlpha, 0.3f).SetLink(gameObject);
                return;
            }

            var skeletonGraphic = GetComponentInChildren<SkeletonGraphic>();
            if (skeletonGraphic != null)
            {
                skeletonGraphic.DOFade(targetAlpha, 0.3f).SetLink(gameObject);
            }
        }

        #endregion
    }

    [System.Serializable]
    public class VisitorNPCSaveData
    {
        public VisitorType visitorType;
        public int savedDay;
        public float remainingTime;
        public bool isInteracting;
        public string adventurerInstanceID;  // Adventurer
        public BlacksmithType blacksmithType; // Blacksmith
        public bool blacksmithIsPremium;
        public string eventDataStaticID;      // EventNPC 복원용
        public string investorDialogueID;     // InvestorResult 복원용
        public int investorReturnedGold;      // InvestorResult 복원용
        public DeadEventKind deadEventKind;   // DeadEvent 복원용
    }
}
