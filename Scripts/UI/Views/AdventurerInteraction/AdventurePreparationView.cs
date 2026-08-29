using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 3탭 구조 모험 준비 패널.
    /// 공통 요소(진행도, 탭 내비, 미니인포)만 보유하고
    /// 각 탭/상세 패널은 별도 컴포넌트로 분리해 위임한다.
    /// Tab1/Tab2/Tab3 패널은 미리 배치, 4개 상세 패널은 lazy-init으로 1회 생성 후 재사용.
    /// </summary>
    public class AdventurePreparationView : BaseView
    {
        // 변형 그룹 키. AdventurePrepare1~4 중 어느 곡을 쓸지는 SoundManager의 변형 선택(기본값 랜덤)이 정한다.
        protected override string GetThemeBgmKey() => "AdventurePrepare";

        // ─────────────────────────────────────────────────────────────────────
        // 공통 요소
        // ─────────────────────────────────────────────────────────────────────

        [Header("Progress Step Bar")]
        [SerializeField] private Toggle step1Toggle;
        [SerializeField] private Toggle step2Toggle;
        [SerializeField] private Toggle step3Toggle;
        [SerializeField] private Image step2BG;
        [SerializeField] private Image step2Border;
        [SerializeField] private Image step3BG;
        [SerializeField] private Image step3Border;
        [SerializeField] private GameObject step2CheckMark;
        [SerializeField] private GameObject step3CheckMark;

        [Header("Tab Navigation")]
        [SerializeField] private Button prevTabButton;
        [SerializeField] private Button nextTabButton;
        [SerializeField] private Button declineBackButton;

        [Header("Mini Info Panel")]
        [SerializeField] private AdventurerMiniInfoPanel miniInfoPanel;

        // ─────────────────────────────────────────────────────────────────────
        // 미리 배치된 탭 서브패널
        // ─────────────────────────────────────────────────────────────────────

        [Header("Tab Panels")]
        [SerializeField] private PreparationTab1Panel tab1Panel;
        [SerializeField] private PreparationTab2Panel tab2Panel;
        [SerializeField] private PreparationTab3Panel tab3Panel;

        // ─────────────────────────────────────────────────────────────────────
        // Lazy-init 상세 패널 (프리팹 + 부모 + 인스턴스 캐시)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Detail Panel Prefabs (Lazy)")]
        [SerializeField] private PreparationStatTestConfirmPanel statTestConfirmPanelPrefab;
        [SerializeField] private PreparationWeaponDetailPanel weaponDetailPanelPrefab;
        [SerializeField] private PreparationActiveItemDetailPanel activeItemDetailPanelPrefab;
        [SerializeField] private PreparationDungeonDetailPanel dungeonDetailPanelPrefab;

        [Header("Detail Panel Containers")]
        [SerializeField] private Transform panelContainer;

        private PreparationStatTestConfirmPanel statTestConfirmPanelInstance;
        private PreparationWeaponDetailPanel weaponDetailPanelInstance;
        private PreparationActiveItemDetailPanel activeItemDetailPanelInstance;
        private PreparationDungeonDetailPanel dungeonDetailPanelInstance;

        // ─────────────────────────────────────────────────────────────────────
        // 컨트롤러
        // ─────────────────────────────────────────────────────────────────────

        [Header("Controller")]
        [SerializeField] private AdventurePreparationController preparationController;

        private AdventurePreparationController Controller => preparationController;

        // 탭 전환 중 토글 리스너 재진입 방지
        private bool isTabSwitching = false;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = true;
            isCanClickOverlay = false;
            canEscape = false;
        }

        public void Initialize(AdventurerInstance adventurer, List<DungeonData> dungeonChoices,
            WeaponInstance selectedWeapon, ActiveItemData assignedItem)
        {
            SwitchTab(0);
            UpdateProgressBar(false, false);
            tab1Panel?.UpdateAdventurer(adventurer, selectedWeapon);
            tab1Panel?.UpdateTalkButtons(adventurer);
            bool revealWeaponType = adventurer.isWeaponTypeHinted || selectedWeapon != adventurer.defaultWeapon;
            tab1Panel?.UpdateWeaponInfo(selectedWeapon, revealWeaponType);
            tab1Panel?.UpdateActiveItemIcon(assignedItem);
            tab3Panel?.UpdateDungeonList(dungeonChoices, adventurer);
            tab3Panel?.UpdateStartButton(false);
            tab3Panel?.UpdateSeerIndicator(false);
            tab3Panel?.ResetAdventureInfo();
            UpdateMiniInfoPanels(adventurer, selectedWeapon, assignedItem, revealWeaponType);

            // 상세 패널 닫기 (인스턴스 있을 때만)
            statTestConfirmPanelInstance?.Hide();
            weaponDetailPanelInstance?.Hide();
            activeItemDetailPanelInstance?.Hide();
            dungeonDetailPanelInstance?.Hide();
        }

        protected override void SubscribeEvents()
        {
            step1Toggle?.onValueChanged.AddListener(on => { if (on && !isTabSwitching) Controller?.OnProgressStepClicked(0); });
            step2Toggle?.onValueChanged.AddListener(on => { if (on && !isTabSwitching) Controller?.OnProgressStepClicked(1); });
            step3Toggle?.onValueChanged.AddListener(on => { if (on && !isTabSwitching) Controller?.OnProgressStepClicked(2); });

            prevTabButton?.onClick.AddListener(() => Controller?.OnPrevTabClicked());
            nextTabButton?.onClick.AddListener(() => Controller?.OnNextTabClicked());
            declineBackButton?.onClick.AddListener(HandleBackRequest);

            tab1Panel?.Initialize(Controller);
            tab2Panel?.Initialize(Controller);
            tab3Panel?.Initialize(Controller);

            if (miniInfoPanel != null)
            {
                miniInfoPanel.OnWeaponInfoClicked += OnMiniInfoWeaponPanelClicked;
                miniInfoPanel.OnActiveItemInfoClicked += OnMiniInfoActiveItemPanelClicked;
                miniInfoPanel.OnEmptyActiveItemClicked += OnMiniInfoEmptyActiveItemPanelClicked;
                miniInfoPanel.OnStatClicked += OnMiniInfoStatClicked;
                miniInfoPanel.OnTraitClicked += OnMiniInfoTraitClicked;
                miniInfoPanel.OnWeaponHintClicked += OnMiniInfoWeaponHintClicked;
            }
        }

        protected override void UnsubscribeEvents()
        {
            step1Toggle?.onValueChanged.RemoveAllListeners();
            step2Toggle?.onValueChanged.RemoveAllListeners();
            step3Toggle?.onValueChanged.RemoveAllListeners();

            prevTabButton?.onClick.RemoveAllListeners();
            nextTabButton?.onClick.RemoveAllListeners();
            declineBackButton?.onClick.RemoveAllListeners();

            // 서브패널은 자체 Initialize에서 RemoveAllListeners 후 다시 등록하므로
            // 여기서 별도로 해제할 필요가 없다. (Lazy 인스턴스는 1회 Initialize만 호출됨)

            if (miniInfoPanel != null)
            {
                miniInfoPanel.OnWeaponInfoClicked -= OnMiniInfoWeaponPanelClicked;
                miniInfoPanel.OnActiveItemInfoClicked -= OnMiniInfoActiveItemPanelClicked;
                miniInfoPanel.OnEmptyActiveItemClicked -= OnMiniInfoEmptyActiveItemPanelClicked;
                miniInfoPanel.OnStatClicked -= OnMiniInfoStatClicked;
                miniInfoPanel.OnTraitClicked -= OnMiniInfoTraitClicked;
                miniInfoPanel.OnWeaponHintClicked -= OnMiniInfoWeaponHintClicked;
            }
        }

        private void OnMiniInfoWeaponPanelClicked()  => Controller?.OnMiniInfoWeaponClicked();
        private void OnMiniInfoActiveItemPanelClicked() => Controller?.OnMiniInfoActiveItemClicked();
        private void OnMiniInfoEmptyActiveItemPanelClicked() => Controller?.OnMiniInfoEmptyActiveItemClicked();
        private void OnMiniInfoStatClicked(AdventurerStat stat) => Controller?.OnMiniInfoStatClicked(stat);
        private void OnMiniInfoTraitClicked() => Controller?.OnMiniInfoTraitClicked();
        private void OnMiniInfoWeaponHintClicked() => Controller?.OnMiniInfoWeaponHintClicked();

        #endregion

        #region Lazy-init 헬퍼

        private PreparationStatTestConfirmPanel GetStatTestConfirmPanel()
        {
            if (statTestConfirmPanelInstance == null && statTestConfirmPanelPrefab != null)
            {
                statTestConfirmPanelInstance = Instantiate(statTestConfirmPanelPrefab, panelContainer);
                statTestConfirmPanelInstance.Initialize(Controller);
                statTestConfirmPanelInstance.gameObject.SetActive(false);
            }
            return statTestConfirmPanelInstance;
        }

        private PreparationWeaponDetailPanel GetWeaponDetailPanel()
        {
            if (weaponDetailPanelInstance == null && weaponDetailPanelPrefab != null)
            {
                weaponDetailPanelInstance = Instantiate(weaponDetailPanelPrefab, panelContainer);
                weaponDetailPanelInstance.Initialize(Controller);
                weaponDetailPanelInstance.gameObject.SetActive(false);
            }
            return weaponDetailPanelInstance;
        }

        private PreparationActiveItemDetailPanel GetActiveItemDetailPanel()
        {
            if (activeItemDetailPanelInstance == null && activeItemDetailPanelPrefab != null)
            {
                activeItemDetailPanelInstance = Instantiate(activeItemDetailPanelPrefab, panelContainer);
                activeItemDetailPanelInstance.Initialize(Controller);
                activeItemDetailPanelInstance.gameObject.SetActive(false);
            }
            return activeItemDetailPanelInstance;
        }

        private PreparationDungeonDetailPanel GetDungeonDetailPanel()
        {
            if (dungeonDetailPanelInstance == null && dungeonDetailPanelPrefab != null)
            {
                dungeonDetailPanelInstance = Instantiate(dungeonDetailPanelPrefab, panelContainer);
                dungeonDetailPanelInstance.Initialize(Controller);
                dungeonDetailPanelInstance.gameObject.SetActive(false);
            }
            return dungeonDetailPanelInstance;
        }

        #endregion

        #region 탭 전환 & 진행도

        public void SwitchTab(int tabIndex)
        {
            tab1Panel?.gameObject.SetActive(tabIndex == 0);
            tab2Panel?.gameObject.SetActive(tabIndex == 1);
            tab3Panel?.gameObject.SetActive(tabIndex == 2);

            prevTabButton?.gameObject.SetActive(tabIndex > 0);
            nextTabButton?.gameObject.SetActive(tabIndex < 2);

            isTabSwitching = true;
            if (step1Toggle != null) step1Toggle.isOn = tabIndex == 0;
            if (step2Toggle != null) step2Toggle.isOn = tabIndex == 1;
            if (step3Toggle != null) step3Toggle.isOn = tabIndex == 2;
            isTabSwitching = false;

            miniInfoPanel?.gameObject.SetActive(tabIndex != 0);

            if (tabIndex == 1)
            {
                tab2Panel?.RefreshSubTabFocus();
                tab2Panel?.ResetWeaponTypeFilter();
            }

            HideStatTestConfirmPanel();
        }

        public void UpdateProgressBar(bool weaponDone, bool dungeonDone)
        {
            step2CheckMark?.SetActive(weaponDone);
            step2BG.color = ColorManager.Instance.GetStepBGColor(weaponDone);
            step2Border.color = ColorManager.Instance.GetStepBorderColor(weaponDone);

            step3CheckMark?.SetActive(dungeonDone);
            step3BG.color = ColorManager.Instance.GetStepBGColor(dungeonDone);
            step3Border.color = ColorManager.Instance.GetStepBorderColor(dungeonDone);
        }

        #endregion

        #region Tab 1 위임 (탐색 / 대화 / 스탯 테스트)

        public void UpdateTimeDisplay()
        {
            tab1Panel?.UpdateTimeDisplay();
            miniInfoPanel?.UpdateTimeDisplay();
        }
        public void UpdateTab1Adventurer(AdventurerInstance adventurer, WeaponInstance weapon = null)
            => tab1Panel?.UpdateAdventurer(adventurer, weapon);
        public void UpdateTalkButtons(AdventurerInstance adventurer) => tab1Panel?.UpdateTalkButtons(adventurer);
        public void UpdateWeaponInfoTab1(WeaponInstance weapon, bool revealWeaponType)
            => tab1Panel?.UpdateWeaponInfo(weapon, revealWeaponType);
        public void UpdateActiveItemTab1(ActiveItemData item)
            => tab1Panel?.UpdateActiveItemIcon(item);

        public void ShowStatTestConfirmPanel(string title, int timeCost, float successRate, int legacyCost)
            => GetStatTestConfirmPanel()?.Show(title, timeCost, successRate, legacyCost);
        public void HideStatTestConfirmPanel()
            => statTestConfirmPanelInstance?.Hide();
        public void UpdateStatTestValues(bool isPremium, string title, int timeCost, float successRate, int legacyCost)
            => statTestConfirmPanelInstance?.UpdateValues(isPremium, title, timeCost, successRate, legacyCost);

        #endregion

        #region 튜토리얼 하이라이트용 접근자

        public PreparationTab1Panel GetTab1Panel() => tab1Panel;
        public PreparationTab2Panel GetTab2Panel() => tab2Panel;
        public RectTransform GetStatTestProceedButtonRect() => statTestConfirmPanelInstance?.GetProceedButtonRect();
        public RectTransform GetNextTabButtonRect() => nextTabButton?.transform as RectTransform;
        public RectTransform GetStrStatGroupRect() => miniInfoPanel?.GetStrStatGroupRect();
        public RectTransform GetWeaponDetailRentButtonRect() => weaponDetailPanelInstance?.GetRentButtonRect();
        public RectTransform GetActiveItemDetailAssignButtonRect() => activeItemDetailPanelInstance?.GetAssignButtonRect();

        // 6-C: 던전 비교 + 던전 A 선택
        public RectTransform GetDungeonArmorIconRect(string dungeonStaticID) => tab3Panel?.GetDungeonArmorIconRect(dungeonStaticID);
        public RectTransform GetDungeonDoubleRewardIconRect(string dungeonStaticID) => tab3Panel?.GetDungeonDoubleRewardIconRect(dungeonStaticID);
        public RectTransform GetDungeonCardRect(string dungeonStaticID) => tab3Panel?.GetDungeonCardRect(dungeonStaticID);
        public void SetDungeonScrollLocked(bool locked) => tab3Panel?.SetDungeonScrollLocked(locked);
        public RectTransform GetDungeonDetailSelectButtonRect() => dungeonDetailPanelInstance?.GetSelectDungeonButtonRect();
        // 8단계: 던전 B 경장갑 가정 시뮬레이션
        public RectTransform GetDungeonDetailArmorToggleRect(ArmorType armorType) => dungeonDetailPanelInstance?.GetArmorToggleRect(armorType);
        public void SetDungeonDetailSimulationOnly(ArmorType allowed) => dungeonDetailPanelInstance?.SetTutorialSimulationOnly(allowed);
        public RectTransform GetDungeonDetailSuccessRateAreaRect() => dungeonDetailPanelInstance?.GetSuccessRateAreaRect();

        // 6-D: 모험 정보 · 점술
        public RectTransform GetAdventureInfoContainerRect() => tab3Panel?.GetAdventureInfoContainerRect();
        public RectTransform GetSeerButtonRect() => tab3Panel?.GetSeerButtonRect();
        public void SetInfoChipsTutorialCallback(System.Action callback) => tab3Panel?.SetInfoChipsTutorialCallback(callback);

        // 6-E: 모험 출발
        public RectTransform GetStartAdventureButtonRect() => tab3Panel?.GetStartAdventureButtonRect();

        #endregion

        #region Tab 2 위임 (무기 / 아이템 / 상세)

        public void UpdateWeaponGrid(List<WeaponInstance> weapons)
        {
            tab2Panel?.UpdateWeaponGrid(weapons);
            weaponDetailPanelInstance?.Hide();
        }

        public void UpdateActiveItemList(List<ActiveItemInstance> items, bool isNamed, string assignedItemDataID)
        {
            tab2Panel?.UpdateActiveItemList(items, isNamed, assignedItemDataID);
            activeItemDetailPanelInstance?.Hide();
        }

        public void ShowWeaponDetail(WeaponInstance weapon, bool isConfirmedWeapon, WeaponInstance defaultWeapon)
            => GetWeaponDetailPanel()?.Show(weapon, isConfirmedWeapon, defaultWeapon);

        public void HideWeaponDetailPanel() => weaponDetailPanelInstance?.Hide();

        public void RefreshWeaponDetailButtons(bool isConfirmedWeapon, WeaponInstance defaultWeapon)
            => weaponDetailPanelInstance?.RefreshButtons(isConfirmedWeapon, defaultWeapon);

        public void ClearWeaponCardSelection() => tab2Panel?.ClearWeaponCardSelection();

        public void SelectWeaponSubTab() => tab2Panel?.SelectWeaponSubTab();
        public void SelectActiveItemSubTab() => tab2Panel?.SelectActiveItemSubTab();

        public void ShowActiveItemDetail(ActiveItemData data, bool isAssigned, bool isViewOnly = false)
            => GetActiveItemDetailPanel()?.Show(data, isAssigned, isViewOnly);

        public void HideActiveItemDetailPanel()
        {
            activeItemDetailPanelInstance?.Hide();
            tab2Panel?.ClearActiveItemCardSelection();
        }

        #endregion

        #region Tab 3 위임 (던전 / 상세 / 성공률 / 점술)

        public void UpdateDungeonList(List<DungeonData> choices, AdventurerInstance adventurer) => tab3Panel?.UpdateDungeonList(choices, adventurer);
        public void RefreshDungeonSeerGlow(DungeonData dungeon) => tab3Panel?.RefreshSeerGlow(dungeon);
        public void UpdateSeerIndicator(bool done) => tab3Panel?.UpdateSeerIndicator(done);
        public void HighlightSelectedDungeon(DungeonData selected) => tab3Panel?.HighlightSelectedDungeon(selected);
        public void UpdateDungeonCardSimulation(DungeonData dungeon, ArmorType armorType)
            => tab3Panel?.UpdateDungeonCardSimulation(dungeon, armorType);
        public void ClearDungeonCardSimulation(string dungeonStaticID)
            => tab3Panel?.ClearDungeonCardSimulation(dungeonStaticID);
        public void RefreshDungeonCardArmorType(string dungeonStaticID)
            => tab3Panel?.RefreshDungeonCardArmorType(dungeonStaticID);

        public void ShowDungeonDetailPanel(DungeonData dungeon)
        {
            if (dungeon == null) { HideDungeonDetail(); return; }
            var panel = GetDungeonDetailPanel();
            panel?.Show(
                dungeon.dungeonIcon,
                ColorManager.Instance.GetGradeCardBackgroundColor(dungeon.grade),
                IconManager.Instance.GetFrameByGrade(dungeon.grade));
        }

        public void HideDungeonDetail() => dungeonDetailPanelInstance?.Hide();

        public void ShowDetailTab(int tabIndex) => dungeonDetailPanelInstance?.ShowDetailTab(tabIndex);

        public void UpdatePanelArmorToggles(bool isArmorTypeKnown, ArmorType current,
            bool unarmoredInDungeon, bool lightInDungeon, bool heavyInDungeon, bool magicalInDungeon)
            => dungeonDetailPanelInstance?.UpdateArmorToggles(isArmorTypeKnown, current,
                unarmoredInDungeon, lightInDungeon, heavyInDungeon, magicalInDungeon);

        public void UpdatePanelEffectList(WeaponInstance weapon, IReadOnlyList<EffectDisplayState> states)
            => dungeonDetailPanelInstance?.UpdateEffectList(weapon, states);

        public void UpdatePanelSuccessRateRows(
            bool showBaseRate, float expectedRate, float baseRate, float statEffectBonus,
            bool showArmorBonusRow, float armorBonus,
            float conditionBonus,
            bool showCharmRow, float charmBonus,
            bool showTraitRow, string traitNote)
            => dungeonDetailPanelInstance?.UpdateSuccessRateRows(
                showBaseRate, expectedRate, baseRate, statEffectBonus,
                showArmorBonusRow, armorBonus,
                conditionBonus,
                showCharmRow, charmBonus,
                showTraitRow, traitNote);

        public void ShowPanelEventRateTooltip(EventRateTooltipData data)
            => dungeonDetailPanelInstance?.ShowEventRateTooltip(data);

        public void UpdatePanelDuration(bool isRevealed, float displayTime, float totalMultiplier)
            => dungeonDetailPanelInstance?.UpdateDuration(isRevealed, displayTime, totalMultiplier);

        public void UpdatePanelEffectHint(bool isWeaponSelected)
            => dungeonDetailPanelInstance?.UpdateEffectHint(isWeaponSelected);

        public void UpdatePanelDungeonInfoTab(DungeonData dungeon, DungeonStatData stat)
            => dungeonDetailPanelInstance?.UpdateDungeonInfoTab(dungeon, stat);

        public void ResetAdventureInfo()                => tab3Panel?.ResetAdventureInfo();
        public void UpdateAdventureInfo(List<AdventureInfoCardData> cards) => tab3Panel?.UpdateAdventureInfo(cards);
        public void UpdateStartButton(bool canStart)    => tab3Panel?.UpdateStartButton(canStart);

        #endregion

        #region Mini Info

        public void UpdateMiniInfoPanels(AdventurerInstance adventurer, WeaponInstance weapon, ActiveItemData activeItem, bool revealWeaponType)
        {
            miniInfoPanel?.SetAdventurer(adventurer);
            miniInfoPanel?.RefreshStats(adventurer, weapon);
            miniInfoPanel?.RefreshTraitDisplay(adventurer);
            miniInfoPanel?.UpdateWeaponIcon(weapon, revealWeaponType);
            miniInfoPanel?.UpdateActiveItemIcon(activeItem);
            miniInfoPanel?.UpdateTimeDisplay();
        }

        #endregion

        #region ESC(뒤로가기)

        public override void OnEscapeCancelled() => HandleBackRequest();

        // 닫기(뒤로)·ESC 공통 진입점: 열린 상세 sub-panel이 있으면 그것만 닫고, 없으면 돌려보내기 확인 팝업.
        private void HandleBackRequest()
        {
            if (dungeonDetailPanelInstance != null && dungeonDetailPanelInstance.gameObject.activeSelf) { HideDungeonDetail(); return; }
            if (activeItemDetailPanelInstance != null && activeItemDetailPanelInstance.gameObject.activeSelf) { HideActiveItemDetailPanel(); return; }
            if (weaponDetailPanelInstance != null && weaponDetailPanelInstance.gameObject.activeSelf) { HideWeaponDetailPanel(); return; }
            if (statTestConfirmPanelInstance != null && statTestConfirmPanelInstance.gameObject.activeSelf) { HideStatTestConfirmPanel(); return; }

            if (TutorialManager.Instance != null && TutorialManager.Instance.GuardBack()) return;

            UIPopupController.Instance?.ShowPopup(
                UIPopupController.SendBackConfirmMessage(UITranslator.GetString(VisitorType.Adventurer)),
                onConfirm: () => Controller?.OnDeclineAdventureClicked(),
                onCancel: () => { });
        }

        #endregion
    }
}
