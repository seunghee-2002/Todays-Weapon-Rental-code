// Scripts/UI/Views/QuestBoard/QuestBoardView.cs
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 의뢰판 View — Phase 1(선택) / Phase 2(수색 파견) 두 단계를 하나의 패널에서 운영.
    /// 낮 진입 시 자동 오픈, 확정 시 Phase 2로 전환.
    /// </summary>
    public class QuestBoardView : BaseView
    {
        public enum Phase { Selection, Scout }

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;             // Phase 1: "던전 선택" / Phase 2: "수색 선택"
        [SerializeField] private TextMeshProUGUI selectionCountText;    // "0 / 3"
        [SerializeField] private TextMeshProUGUI scoutCostSummaryText;  // Phase 2: "총 비용: 300G"
        [SerializeField] private TextMeshProUGUI selectionDescText;     // Phase 1: "던전을 N개 선택해주세요" / Phase 2: "수색할 던전을 선택해주세요"

        [Header("Dungeon List")]
        [SerializeField] private ScrollRect dungeonScrollRect;
        [SerializeField] private Transform dungeonSlotContainer;
        [SerializeField] private GameObject dungeonSlotPrefab;

        [Header("Selection Button Group (Phase 1)")]
        [SerializeField] private GameObject selectionButtonGroup;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TextMeshProUGUI refreshCountText;
        [SerializeField] private Button selectAllButton;
        [SerializeField] private TextMeshProUGUI selectAllButtonText;   // 전체 선택 ⇄ 전체 해제
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;

        [Header("Scout Button Group (Phase 2)")]
        [SerializeField] private GameObject scoutButtonGroup;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button selectAllScoutButton;
        [SerializeField] private TextMeshProUGUI selectAllScoutButtonText;  // 전체선택 ⇄ 전체해제
        [SerializeField] private Button scoutButton;
        [SerializeField] private TextMeshProUGUI scoutButtonText;

        [Header("스킵 오버레이")]
        [SerializeField] private GameObject skipOverlayObject;
        [SerializeField] private Button skipOverlayButton;

        [Header("Controller")]
        [SerializeField] private QuestBoardController controller;

        private List<QuestBoardDungeonSlot> slots = new();
        private Coroutine activeSequence;
        private bool skipRequested;
        private Phase currentPhase = Phase.Selection;
        private int cachedScoutMax;

        public Phase CurrentPhase => currentPhase;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen   = true;
            isCanClickOverlay = false;
            canEscape         = false;
        }

        public override void Open()
        {
            base.Open();
            UIPopupController.Instance?.ShowPlayerResourceBar();
        }

        public override void Close()
        {
            UIPopupController.Instance?.HidePlayerResourceBar();
            base.Close();
        }

        protected override void SubscribeEvents()
        {
            selectAllButton?.onClick.AddListener(OnSelectAllClicked);
            selectAllScoutButton?.onClick.AddListener(OnSelectAllScoutClicked);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            scoutButton?.onClick.AddListener(OnSendScoutClicked);
            refreshButton?.onClick.AddListener(OnRefreshClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
        }

        protected override void UnsubscribeEvents()
        {
            StopActiveSequence();
            selectAllButton?.onClick.RemoveAllListeners();
            selectAllScoutButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
            scoutButton?.onClick.RemoveAllListeners();
            refreshButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region 슬롯 생성 (Phase 1)

        public void SetupSlots(List<DungeonData> poolDungeons)
        {
            StopActiveSequence();
            ClearSlots();

            currentPhase = Phase.Selection;
            ApplySelectionPhaseButtons();

            poolDungeons = poolDungeons.OrderByDescending(d => d.grade).ToList();

            foreach (var dungeon in poolDungeons)
            {
                var slotObj = Instantiate(dungeonSlotPrefab, dungeonSlotContainer);
                var slot    = slotObj.GetComponentOrNull<QuestBoardDungeonSlot>();

                if (slot != null)
                {
                    slot.Initialize(
                        dungeon,
                        onInfoClicked:   () => controller?.OnInfoClicked(dungeon),
                        onSelectClicked: () => controller?.OnSelectClicked(dungeon)
                    );
                    slot.SetDuration(0); // Phase 1에서는 수색 시간 숨김
                    slot.transform.localScale = Vector3.zero;
                    slots.Add(slot);
                }
            }

            dungeonScrollRect?.ResetPosition(this);

            activeSequence = StartCoroutine(SlotAppearRoutine());
        }

        private IEnumerator SlotAppearRoutine()
        {
            skipRequested = false;
            SetSkipOverlayActive(true);

            const float interval = 0.08f;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                if (skipRequested)
                {
                    slot.SnapToAppearedState();
                    continue;
                }
                slot.PlayAppearAnimation();
                yield return new WaitForSecondsRealtime(interval);
            }

            if (!skipRequested)
                yield return new WaitForSecondsRealtime(0.22f);

            SetSkipOverlayActive(false);
            activeSequence = null;
        }

        private void ClearSlots()
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            slots.Clear();
        }

        #endregion

        #region Phase 1 UI 업데이트

        public void UpdateSelectionCount(int current, int required)
        {
            if (selectionCountText != null)
                selectionCountText.text = $"{current}/{required}";

            bool canConfirm = (current == required);

            if (selectionDescText != null)
            {
                selectionDescText.gameObject.SetActive(!canConfirm);
                if (!canConfirm) selectionDescText.text = SelectDungeonsDesc(required);
            }

            // 전체선택 ⇄ 전체해제 토글 (버튼은 항상 활성, 무효 클릭은 Controller가 Toast로 안내)
            if (selectAllButtonText != null)
                selectAllButtonText.text = L((current >= required) ? "QuestBoard_DeselectAll" : "QuestBoard_SelectAll");
        }

        public void UpdateSlotSelection(List<string> selectedIDs)
        {
            foreach (var slot in slots)
                slot.SetSelected(selectedIDs.Contains(slot.DungeonData.StaticID));
        }

        public void UpdateRefreshButton(int refreshCount, int maxRefresh)
        {
            if (refreshCountText != null)
                refreshCountText.text = $"({refreshCount}/{maxRefresh})";
        }

        #endregion

        #region 확정 → Phase 2 전환

        /// <summary>
        /// 확정 완료 상태 적용. 애니메이션이 끝나면 Phase 2로 진입한다.
        /// scoutMaxAvailable은 Phase 2 헤더 카운트의 분모(전체 확정 던전 수).
        /// skipAnimation=true는 저장 불러오기 시 사용.
        /// </summary>
        public void ApplyConfirmedState(List<string> selectedIDs, List<string> highlightedIDs, int scoutMaxAvailable, bool skipAnimation = false)
        {
            StopActiveSequence();

            // 슬롯 등장 코루틴이 중간에 끊겼을 때 scale 0 상태로 남는 것을 방지
            foreach (var slot in slots)
                slot?.SnapToAppearedState();

            cachedScoutMax = scoutMaxAvailable;

            foreach (var slot in slots)
                slot.SetDisabled(!selectedIDs.Contains(slot.DungeonData.StaticID));

            bool hasHighlight = highlightedIDs != null && highlightedIDs.Count > 0;

            if (skipAnimation || !hasHighlight)
            {
                if (hasHighlight)
                {
                    foreach (var slot in slots.Where(s => highlightedIDs.Contains(s.DungeonData.StaticID)))
                        slot.SetHighlight(true);
                }
                EnterScoutPhase();
                return;
            }

            var highlightedSlots = slots
                .Where(s => highlightedIDs.Contains(s.DungeonData.StaticID))
                .ToList();

            activeSequence = StartCoroutine(HighlightRevealRoutine(highlightedSlots));
        }

        private IEnumerator HighlightRevealRoutine(List<QuestBoardDungeonSlot> highlightedSlots)
        {
            skipRequested = false;
            SetSkipOverlayActive(true);

            // 버튼 그룹은 애니메이션 동안 숨김 (확정 직후 시각 정리)
            selectionButtonGroup?.SetActive(false);
            scoutButtonGroup?.SetActive(false);

            yield return new WaitForSecondsRealtime(0.3f);

            foreach (var slot in highlightedSlots)
            {
                if (slot == null) continue;
                if (skipRequested)
                {
                    slot.SetHighlight(true);
                    SoundManager.Instance?.StopAllSFX();   // 직전 슬롯의 강조음 잔향까지 끊는다
                    continue;
                }
                slot.SetHighlight(true);
                slot.PlayHighlightAnimation();
                SoundManager.Instance?.PlaySFX("DoubleDungeon");
                yield return new WaitForSecondsRealtime(0.18f);
            }

            if (!skipRequested)
                yield return new WaitForSecondsRealtime(0.25f);

            SetSkipOverlayActive(false);
            activeSequence = null;
            EnterScoutPhase();
        }

        #endregion

        #region Phase 2 진입 / UI 업데이트

        /// <summary>
        /// Phase 2(수색 파견) 진입. 슬롯의 Phase 1 선택 표시를 모두 클리어하고
        /// 선택 버튼 콜백을 수색 타겟 토글로 재바인딩한다. 하이라이트는 유지.
        /// </summary>
        public void EnterScoutPhase()
        {
            currentPhase = Phase.Scout;

            // Level 2 가상 패널 - 독립 패널이 아닌 의뢰판 Phase 2라 panel_opened만 발행한다
            AnalyticsManager.Instance?.SendPanelOpened("scout_dispatch");

            foreach (var slot in slots)
            {
                slot.SetSelected(false);
                var dungeon = slot.DungeonData;
                slot.RebindSelectAction(() => controller?.OnDungeonSelectedForScout(dungeon));

                // 확정된 던전에만 수색 시간 노출 (비확정 슬롯은 disabled 상태)
                if (QuestBoardManager.Instance != null &&
                    ScoutManager.Instance != null &&
                    QuestBoardManager.Instance.IsDungeonAvailableToday(dungeon.StaticID))
                {
                    slot.SetDuration(ScoutManager.Instance.GetScoutDuration(dungeon));
                }
                else
                {
                    slot.SetDuration(0);
                }
            }

            ApplyScoutPhaseButtons();
            UpdateScoutSelectionCount(0, cachedScoutMax, 0);
            RefreshSlotScoutStates(new List<string>());

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialQuestBoardScoutPhaseEntered();
        }

        public void UpdateScoutSelectionCount(int current, int max, int totalCost)
        {
            if (selectionCountText != null)
                selectionCountText.text = $"{current} / {max}";

            bool hasAny = current > 0;

            if (selectionDescText != null)
            {
                selectionDescText.gameObject.SetActive(!hasAny);
                if (!hasAny) selectionDescText.text = L("QuestBoard_SelectScoutDesc");
            }

            if (scoutCostSummaryText != null)
            {
                scoutCostSummaryText.gameObject.SetActive(hasAny);
                if (hasAny)
                {
                    scoutCostSummaryText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", "QuestBoard_TotalCost",
                arguments: new object[] { new Dictionary<string, object> { { "cost", totalCost.ToString("N0") } } });
                    if (ColorManager.Instance != null && EconomyManager.Instance != null)
                    {
                        scoutCostSummaryText.color = EconomyManager.Instance.CurrentGold >= totalCost
                            ? ColorManager.Instance.GetGreenColor()
                            : ColorManager.Instance.GetRedColor();
                    }
                }
            }

            // 전체선택 ⇄ 전체해제 토글 (버튼은 항상 활성, 무효 클릭은 Controller가 Toast로 안내)
            if (selectAllScoutButtonText != null)
                selectAllScoutButtonText.text = L((max > 0 && current >= max) ? "QuestBoard_DeselectAll" : "QuestBoard_SelectAll");
        }

        public void UpdateScoutSlotSelection(List<string> selectedScoutIDs)
        {
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                bool isSelected = selectedScoutIDs.Contains(slot.DungeonData.StaticID);
                slot.SetSelected(isSelected);
            }
        }

        /// <summary>
        /// 확정된 슬롯들의 수색 상태(진행 중/파견 가능)를 갱신.
        /// 진행 중이거나 결과 미확인 상태면 선택 버튼 비활성, ongoingIndicator 표시.
        /// </summary>
        public void RefreshSlotScoutStates(List<string> selectedScoutIDs)
        {
            if (ScoutManager.Instance == null) return;

            foreach (var slot in slots)
            {
                if (slot == null || slot.DungeonData == null) continue;

                string id = slot.DungeonData.StaticID;

                if (!QuestBoardManager.Instance.IsDungeonAvailableToday(id))
                {
                    slot.SetOngoing(false);
                    continue;
                }

                bool ongoing = ScoutManager.Instance.IsScoutOngoing(id);
                bool canSend = ScoutManager.Instance.CanSendScout(slot.DungeonData);

                slot.SetOngoing(ongoing);
                slot.SetSelectInteractable(canSend);

                if (!canSend)
                    slot.SetSelected(false);
                else if (selectedScoutIDs != null)
                    slot.SetSelected(selectedScoutIDs.Contains(id));
            }
        }

        #endregion

        #region 튜토리얼 하이라이트용 접근자

        /// <summary>튜토리얼 하이라이트용 — 지정 DungeonData StaticID를 가진 슬롯의 RectTransform.</summary>
        public RectTransform GetDungeonSlotRect(string dungeonStaticID)
        {
            // GridLayoutGroup 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (dungeonSlotContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            var slot = slots.FirstOrDefault(s => s?.DungeonData != null && s.DungeonData.StaticID == dungeonStaticID);
            return slot?.transform as RectTransform;
        }

        /// <summary>튜토리얼 하이라이트용 — 수색 파견(Phase 2) 버튼.</summary>
        public RectTransform GetScoutButtonRect() => scoutButton?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트 중 카드 위 드래그로 목록이 밀리지 않도록 스크롤을 잠근다.</summary>
        public void SetDungeonScrollLocked(bool locked)
        {
            if (dungeonScrollRect != null) dungeonScrollRect.enabled = !locked;
        }

        #endregion

        #region 버튼 그룹 토글

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        /// <summary>"던전을 N개 선택해주세요". 컨트롤러의 토스트와 같은 키를 쓴다.</summary>
        public static string SelectDungeonsDesc(int required)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", "QuestBoard_SelectDungeonsDesc",
                   arguments: new object[] { new Dictionary<string, object> { { "count", required } } });

        private void ApplySelectionPhaseButtons()
        {
            if (titleText != null) titleText.text = L("QuestBoard_TitleSelect");

            selectionButtonGroup?.SetActive(true);
            scoutButtonGroup?.SetActive(false);

            // 1차 버튼은 비활성화하지 않고 항상 초록(활성 느낌). 준비 여부는 헤더 카운트/안내가 담당.
            if (confirmButtonText != null && ColorManager.Instance != null)
                confirmButtonText.color = ColorManager.Instance.GetGreenButtonTextColor();

            if (scoutCostSummaryText != null) scoutCostSummaryText.gameObject.SetActive(false);
        }

        private void ApplyScoutPhaseButtons()
        {
            if (titleText != null) titleText.text = L("QuestBoard_TitleScout");

            selectionButtonGroup?.SetActive(false);
            scoutButtonGroup?.SetActive(true);

            if (scoutButtonText != null && ColorManager.Instance != null)
                scoutButtonText.color = ColorManager.Instance.GetGreenButtonTextColor();
        }

        #endregion

        #region 스킵 오버레이

        private void SetSkipOverlayActive(bool active)
        {
            if (skipOverlayObject != null) skipOverlayObject.SetActive(active);

            if (skipOverlayButton == null) return;

            skipOverlayButton.onClick.RemoveAllListeners();
            if (active)
                skipOverlayButton.onClick.AddListener(OnSkipClicked);
        }

        private void OnSkipClicked()
        {
            if (activeSequence == null) return;
            skipRequested = true;
        }

        private void StopActiveSequence()
        {
            if (activeSequence != null)
            {
                StopCoroutine(activeSequence);
                activeSequence = null;
            }
            skipRequested = false;
            SetSkipOverlayActive(false);
        }

        #endregion

        #region 버튼 이벤트

        private void OnSelectAllClicked() => controller?.OnSelectAllClicked();

        private void OnSelectAllScoutClicked() => controller?.OnSelectAllScoutClicked();

        private void OnConfirmClicked()
        {
            controller?.OnConfirmClicked();
        }

        private void OnSendScoutClicked()
        {
            controller?.OnSendScoutClicked();
        }

        private void OnRefreshClicked() => controller?.OnRefreshClicked();

        // 닫기 버튼·ESC 공통 진입점(Scout 단계). 수색 나가기 확인 팝업 → 확인 시 실제 종료.
        private void OnCloseClicked()
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.GuardBack()) return;

            if (currentPhase == Phase.Scout)
                UIPopupController.Instance?.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "QuestBoard_StopScoutConfirm"),
                () => controller?.OnCloseScoutClicked(), () => { });
            else
                UIManager.Instance.ClosePanel<QuestBoardView>();
        }

        public override void OnEscapeCancelled()
        {
            if (activeSequence != null)
            {
                OnSkipClicked();
                return;
            }

            if (currentPhase == Phase.Selection)
            {
                UIPopupController.Instance?.ShowToast(
                    LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "QuestBoard_SelectDungeonToast"),
                    type: PopupSfxType.Warning);
                return;
            }

            OnCloseClicked();   // Phase.Scout: 닫기 버튼과 동일하게 수색 나가기 확인
        }

        #endregion
    }
}
