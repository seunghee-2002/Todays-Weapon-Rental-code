using UnityEngine;
using UnityEngine.Localization.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    public class AdventurePreparationController : BaseController<AdventurePreparationView>
    {
        [SerializeField] private AdventurerInstance currentAdventurer;
        [SerializeField] private WeaponInstance selectedWeapon;
        [SerializeField] private DungeonData selectedDungeon;

        private ActiveItemData assignedItem;
        private bool hasCharm;
        private float charmBonus;

        private bool isWeaponConfirmed;
        private bool isDungeonSelected;
        private bool hasEverRentedWeapon;

        public ArmorType lastSimulatedArmorType = ArmorType.Unarmored;

        private string simulatedDungeonID;
        private ArmorType simulatedArmorType = ArmorType.Unarmored;

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private static string M(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        private DungeonData pendingDungeon;
        private int currentWeaponTypeFilter  = -1;   // -1 = 전체
        private int currentActiveItemFilter  = -1;   // -1 = 전체
        private int currentActiveTab;

        // 스탯 테스트 확인 패널 — 대기 중인 테스트 정보
        private Sprite pendingTestIcon;
        private string pendingTestTitle;
        private string pendingTestDesc;
        private int pendingTestTimeCost;
        private float pendingTestSuccessRate;
        private int pendingTestLegacyCost;
        private bool pendingTestIsPremium;
        private TalkTestType? pendingTestType;   // null = 하루 1회 제한 대상 아님(무기 조사/종합 테스트)
        private bool pendingTestIsAllStats;
        private Func<(bool, string)> pendingNormalExecute;
        private Func<(bool, string)> pendingPremiumExecute;

        #region 초기화

        protected override void SubscribeControllerEvents()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnTimeChanged += OnTimeChanged;
            if (ScoutManager.Instance != null)
                ScoutManager.Instance.OnScoutComplete += OnScoutComplete;
        }

        protected override void UnsubscribeControllerEvents()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnTimeChanged -= OnTimeChanged;
            if (ScoutManager.Instance != null)
                ScoutManager.Instance.OnScoutComplete -= OnScoutComplete;
        }

        public void Initialize(AdventurerInstance adventurer, List<DungeonData> dungeonChoices)
        {
            currentAdventurer = adventurer;
            selectedWeapon    = adventurer.defaultWeapon;

            isWeaponConfirmed    = false;
            isDungeonSelected    = false;
            hasEverRentedWeapon  = false;
            pendingDungeon       = null;
            selectedDungeon      = null;

            currentWeaponTypeFilter = -1;
            currentActiveItemFilter = -1;
            currentActiveTab        = 0;

            lastSimulatedArmorType = ArmorType.Unarmored;
            simulatedDungeonID  = null;
            simulatedArmorType  = ArmorType.Unarmored;
            assignedItem = null;
            hasCharm     = false;
            charmBonus   = 0f;

            if (ActiveItemManager.Instance != null)
            {
                assignedItem = ActiveItemManager.Instance.GetAssignedItem(adventurer.instanceID);
                charmBonus   = ActiveItemManager.Instance.GetCharmBonus(adventurer.instanceID);
                hasCharm     = charmBonus > 0f;
            }

            ClearPendingTest();

            view?.Initialize(adventurer, dungeonChoices, selectedWeapon, assignedItem);
            view?.UpdateWeaponGrid(BuildWeaponList());
            view?.UpdateActiveItemList(InventoryManager.Instance.GetAllActiveItems(), adventurer.isNamed,
                assignedItem?.StaticID ?? string.Empty);
            view?.UpdateDungeonList(dungeonChoices, adventurer);

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialAdventurePrepOpened();
        }

        private void ResetState()
        {
            currentAdventurer   = null;
            selectedWeapon      = null;
            selectedDungeon     = null;
            assignedItem        = null;
            hasCharm            = false;
            charmBonus          = 0f;
            isWeaponConfirmed    = false;
            isDungeonSelected    = false;
            hasEverRentedWeapon  = false;
            pendingDungeon       = null;
            currentWeaponTypeFilter = -1;
            currentActiveItemFilter = -1;
            currentActiveTab    = 0;
            lastSimulatedArmorType  = ArmorType.Unarmored;
            simulatedDungeonID  = null;
            simulatedArmorType  = ArmorType.Unarmored;
            ClearPendingTest();
        }
        
        #endregion

        #region 진행도 & 탭 전환

        public void OnProgressStepClicked(int stepIndex)
        {
            // 진행도 클릭/화살표 이동 모두 이 경로를 타므로 여기서만 발행한다(중복 방지).
            AnalyticsManager.Instance?.SendButtonClick("adventure_preparation", GetTabAnalyticsName(stepIndex));
            SwitchToTab(stepIndex);
        }

        /// <summary>탭 전환 실행부. 자동 이동(무기 대여 후 등)은 사용자 클릭이 아니므로 이쪽을 직접 호출한다.</summary>
        private void SwitchToTab(int stepIndex)
        {
            currentActiveTab = stepIndex;
            view?.SwitchTab(stepIndex);
            view?.UpdateProgressBar(hasEverRentedWeapon, isDungeonSelected);

            if (stepIndex == 2)
                UpdateAdventureInfoChips(GetSelectedDungeonEffectiveArmor());

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialPrepTabChanged(stepIndex);
        }

        public void OnNextTabClicked()
        {
            if (currentActiveTab < 2)
                OnProgressStepClicked(currentActiveTab + 1);
        }

        public void OnPrevTabClicked()
        {
            if (currentActiveTab > 0)
                OnProgressStepClicked(currentActiveTab - 1);
        }

        private void UpdateProgressBar()
        {
            view?.UpdateProgressBar(hasEverRentedWeapon, isDungeonSelected);
        }

        /// <summary>btn_clicked의 button 값 (Documents/Analytics_이벤트_설계.md Level 3).</summary>
        private static string GetTabAnalyticsName(int stepIndex)
        {
            switch (stepIndex)
            {
                case 1: return "tab_support";
                case 2: return "tab_dungeon";
                default: return "tab_explore";
            }
        }

        public void OnMiniInfoStatClicked(AdventurerStat stat) => OnRevealStatClicked(stat);
        public void OnMiniInfoTraitClicked() => OnRevealTraitClicked();
        public void OnMiniInfoWeaponHintClicked() => OnWeaponTypeHintClicked();

        #endregion

        #region Tab 1 — 탐색 (대화 액션)

        public void OnRevealStatClicked(AdventurerStat stat)
        {
            if (currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("adventurer_talk", "reveal_stat", new Dictionary<string, object>
            {
                { "stat", stat.ToString() }
            });

            int cost = InsightManager.Instance.GetStatTalkTimeCost();
            if (!InsightManager.Instance.CanStartTalkAction(cost))
            {
                UIPopupController.Instance?.ShowToast(M("Insight_NotEnoughTime"), type: PopupSfxType.Warning);
                return;
            }

            float rate        = InsightManager.Instance.GetStatRevealSuccessRate(currentAdventurer, stat);
            string statName   = UITranslator.GetString(stat);
            Sprite icon       = IconManager.Instance.GetIconByTest(stat.ToString());
            string title      = L("Preparation_StatTestTitle", ("stat", statName));
            string desc       = L("Preparation_StatTestDesc");

            string StatResult() => L("Preparation_StatTestSuccess",
                ("stat", statName), ("value", currentAdventurer.GetStat(stat)));

            Func<(bool, string)> normalExecute = () =>
            {
                bool success = InsightManager.Instance.RevealStat(currentAdventurer, stat);
                string msg = success
                    ? StatResult()
                    : L("Preparation_StatTestFail", ("stat", statName));
                return (success, msg);
            };

            Func<(bool, string)> premiumExecute = () =>
            {
                InsightManager.Instance.RevealStatGuaranteed(currentAdventurer, stat);
                return (true, StatResult());
            };

            OpenStatTestConfirmPanel(title, desc, icon, cost, rate, normalExecute, premiumExecute,
                testType: (TalkTestType)(int)stat);
        }

        public void OnRevealAllStatsClicked()
        {
            if (currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("adventurer_talk", "reveal_all_stats");

            // 네임드는 종합 테스트에 응하지 않는다 - 확인 패널을 열기 전에 안내한다
            if (!InsightManager.Instance.CanRevealAllStats(currentAdventurer))
            {
                UIPopupController.Instance?.ShowToast(M("Insight_Refused"), type: PopupSfxType.Warning);
                return;
            }

            int cost = InsightManager.Instance.GetAllStatTalkTimeCost();
            if (!InsightManager.Instance.CanStartTalkAction(cost))
            {
                UIPopupController.Instance?.ShowToast(M("Insight_NotEnoughTime"), type: PopupSfxType.Warning);
                return;
            }

            float rate   = InsightManager.Instance.GetAllStatRevealSuccessRate(currentAdventurer);
            Sprite icon  = IconManager.Instance.GetIconByTest("ALL");
            string title = L("Preparation_AllStatTestTitle");
            string desc  = L("Preparation_AllStatTestDesc");

            string AllStatResult() => L("Preparation_AllStatTestSuccess",
                ("strName", UITranslator.GetString(AdventurerStat.STR)), ("str", currentAdventurer.GetStat(AdventurerStat.STR)),
                ("dexName", UITranslator.GetString(AdventurerStat.DEX)), ("dex", currentAdventurer.GetStat(AdventurerStat.DEX)),
                ("intName", UITranslator.GetString(AdventurerStat.INT)), ("int", currentAdventurer.GetStat(AdventurerStat.INT)),
                ("lukName", UITranslator.GetString(AdventurerStat.LUK)), ("luk", currentAdventurer.GetStat(AdventurerStat.LUK)));

            Func<(bool, string)> normalExecute = () =>
            {
                bool success = InsightManager.Instance.RevealAllStats(currentAdventurer);
                string msg = success ? AllStatResult() : L("Preparation_AllStatTestFail");
                return (success, msg);
            };

            Func<(bool, string)> premiumExecute = () =>
            {
                InsightManager.Instance.RevealAllStatsGuaranteed(currentAdventurer);
                return (true, AllStatResult());
            };

            OpenStatTestConfirmPanel(title, desc, icon, cost, rate, normalExecute, premiumExecute,
                isAllStats: true);

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialStatTestConfirmOpened();
        }

        public void OnRevealTraitClicked()
        {
            if (currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("adventurer_talk", "reveal_trait");

            int cost = InsightManager.Instance.GetStatTalkTimeCost();
            if (!InsightManager.Instance.CanStartTalkAction(cost))
            {
                UIPopupController.Instance?.ShowToast(M("Insight_NotEnoughTime"), type: PopupSfxType.Warning);
                return;
            }

            float rate   = InsightManager.Instance.GetTraitRevealSuccessRate(currentAdventurer);
            Sprite icon  = IconManager.Instance.GetIconByTest("Trait");
            string title = L("Preparation_TraitTestTitle");
            string desc  = L("Preparation_TraitTestDesc");

            string TraitResult() => L("Preparation_TraitTestSuccess",
                ("trait", UITranslator.GetString(currentAdventurer.Trait)));

            Func<(bool, string)> normalExecute = () =>
            {
                bool success = InsightManager.Instance.RevealTrait(currentAdventurer);
                string msg = success ? TraitResult() : L("Preparation_TraitTestFail");
                return (success, msg);
            };

            Func<(bool, string)> premiumExecute = () =>
            {
                InsightManager.Instance.RevealTraitGuaranteed(currentAdventurer);
                return (true, TraitResult());
            };

            OpenStatTestConfirmPanel(title, desc, icon, cost, rate, normalExecute, premiumExecute,
                testType: TalkTestType.Trait);
        }

        public void OnWeaponTypeHintClicked()
        {
            if (currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("adventurer_talk", "weapon_type_hint");

            if (!InsightManager.Instance.CanRevealWeaponTypeHint()) return;

            int cost = InsightManager.Instance.GetStatTalkTimeCost();
            if (!InsightManager.Instance.CanStartTalkAction(cost))
            {
                UIPopupController.Instance?.ShowToast(M("Insight_NotEnoughTime"), type: PopupSfxType.Warning);
                return;
            }

            Sprite icon  = IconManager.Instance.GetIconByTest("DefaultWeapon");
            string title = L("Preparation_WeaponHintTitle");
            string desc  = L("Preparation_WeaponHintDesc");

            (bool, string) execute()
            {
                InsightManager.Instance.RevealWeaponTypeHint(currentAdventurer);
                WeaponType type = currentAdventurer.defaultWeapon.weaponData.weaponType;
                return (true, L("Preparation_WeaponHintSuccess", ("weapon", UITranslator.GetString(type))));
            }

            // 무기 타입 힌트는 항상 100% 성공 — 프리미엄은 시간 절약만
            OpenStatTestConfirmPanel(title, desc, icon, cost, 1f, execute, execute);

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialStatTestConfirmOpened();
        }

        #endregion

        #region Tab 1 — 스탯 테스트 확인 패널

        private void OpenStatTestConfirmPanel(string title, string desc, Sprite icon,
            int timeCost, float successRate,
            Func<(bool, string)> normalExecute, Func<(bool, string)> premiumExecute,
            TalkTestType? testType = null, bool isAllStats = false)
        {
            pendingTestTitle       = title;
            pendingTestDesc        = desc;
            pendingTestIcon        = icon;
            pendingTestTimeCost    = timeCost;
            pendingTestSuccessRate = successRate;
            pendingTestLegacyCost  = LegacyManager.Instance.GetAdventurerTalkLegacyCost(timeCost, currentAdventurer?.isNamed ?? false);
            pendingNormalExecute   = normalExecute;
            pendingPremiumExecute  = premiumExecute;
            pendingTestIsPremium   = false;
            pendingTestType        = testType;
            pendingTestIsAllStats  = isAllStats;

            view?.ShowStatTestConfirmPanel(title, timeCost, successRate, pendingTestLegacyCost);
        }

        public void OnStatTestPremiumToggled(bool isPremium)
        {
            AnalyticsManager.Instance?.SendButtonClick("adventurer_talk", "test_premium_toggle", new Dictionary<string, object>
            {
                { "is_on", isPremium }
            });

            pendingTestIsPremium = isPremium;
            int displayTimeCost  = isPremium ? 0 : pendingTestTimeCost;
            float displayRate    = isPremium ? 1f : pendingTestSuccessRate;
            view?.UpdateStatTestValues(isPremium, pendingTestTitle, displayTimeCost, displayRate, pendingTestLegacyCost);
        }

        public void OnStatTestProceedClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("adventurer_talk", "test_proceed", new Dictionary<string, object>
            {
                { "is_on", pendingTestIsPremium },
                { "legacy_cost", pendingTestLegacyCost }
            });

            view?.HideStatTestConfirmPanel();

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialStatTestStarted();

            if (pendingNormalExecute == null) return;

            // 네임드 종합 테스트는 프리미엄으로도 우회 불가
            if (pendingTestIsAllStats && !InsightManager.Instance.CanRevealAllStats(currentAdventurer))
            {
                UIPopupController.Instance?.ShowToast(M("Insight_Refused"), type: PopupSfxType.Warning);
                ClearPendingTest();
                return;
            }

            // 네임드 하루 1회 제한 — 프리미엄이면 우회 가능
            if (!pendingTestIsPremium && pendingTestType.HasValue
                && !InsightManager.Instance.CanTalkTestToday(currentAdventurer, pendingTestType.Value))
            {
                UIPopupController.Instance?.ShowToast(M("Insight_Refused"), type: PopupSfxType.Warning);
                ClearPendingTest();
                return;
            }

            if (pendingTestIsPremium)
            {
                if (!LegacyManager.Instance.HasEnoughLegacyPoints(pendingTestLegacyCost))
                {
                    UIPopupController.Instance?.ShowToast(M("Economy_NotEnoughLegacy"), type: PopupSfxType.Warning);
                    ClearPendingTest();
                    return;
                }
                if (!LegacyManager.Instance.SpendLegacyPoints(pendingTestLegacyCost, "insight_talk"))
                {
                    UIPopupController.Instance?.ShowToast(M("Economy_NotEnoughLegacy"), type: PopupSfxType.Warning);
                    ClearPendingTest();
                    return;
                }
                OpenTalkAnimationPopup(0, pendingTestIcon, pendingTestTitle, pendingTestDesc, pendingPremiumExecute);
            }
            else
            {
                if (pendingTestSuccessRate <= 0f)
                {
                    UIPopupController.Instance?.ShowToast(M("Insight_NoMoreInfo"), type: PopupSfxType.Warning);
                    ClearPendingTest();
                    return;
                }
                if (!InsightManager.Instance.CanStartTalkAction(pendingTestTimeCost))
                {
                    UIPopupController.Instance?.ShowToast(M("Insight_NotEnoughTime"), type: PopupSfxType.Warning);
                    ClearPendingTest();
                    return;
                }
                if (pendingTestType.HasValue)
                    InsightManager.Instance.MarkTalkTestAttempted(currentAdventurer, pendingTestType.Value);

                OpenTalkAnimationPopup(pendingTestTimeCost, pendingTestIcon, pendingTestTitle, pendingTestDesc, pendingNormalExecute);
            }

            ClearPendingTest();
        }

        private void ClearPendingTest()
        {
            pendingTestIcon        = null;
            pendingTestTitle       = null;
            pendingTestDesc        = null;
            pendingTestTimeCost    = 0;
            pendingTestSuccessRate = 0f;
            pendingTestLegacyCost  = 0;
            pendingTestIsPremium   = false;
            pendingTestType        = null;
            pendingTestIsAllStats  = false;
            pendingNormalExecute   = null;
            pendingPremiumExecute  = null;
        }

        #endregion

        #region Tab 2 — 지원 (무기)

        public void OnWeaponSubTabSelected()
        {
            // Level 2 가상 패널 - 독립 패널이 아닌 서브탭이라 panel_opened만 발행한다
            AnalyticsManager.Instance?.SendPanelOpened("weapon_selection");
            view?.UpdateWeaponGrid(BuildWeaponList());
        }

        public void OnWeaponTypeFilterChanged(int filterIndex)
        {
            AnalyticsManager.Instance?.SendButtonClick("weapon_selection", "type_filter", new Dictionary<string, object>
            {
                { "filter_index", filterIndex }
            });

            // filterIndex 0 = 전체, 1+ = WeaponType enum(filterIndex - 1)
            currentWeaponTypeFilter = filterIndex == 0 ? -1 : filterIndex - 1;
            view?.UpdateWeaponGrid(BuildWeaponList());
        }

        private WeaponInstance pendingRentWeapon;

        public void OnWeaponCardClicked(WeaponInstance weapon)
        {
            if (weapon == null || currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("weapon_selection", "weapon_card_click", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "weapon_grade", (int)weapon.currentGrade }
            });

            pendingRentWeapon = weapon;
            bool isConfirmed = isWeaponConfirmed && weapon == selectedWeapon;
            view?.ShowWeaponDetail(weapon, isConfirmed, currentAdventurer.defaultWeapon);

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialWeaponDetailOpened();
        }

        public void OnRentWeaponClicked()
        {
            if (currentAdventurer == null || pendingRentWeapon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("weapon_selection", "weapon_selected", new Dictionary<string, object>
            {
                { "weapon_id", pendingRentWeapon.weaponData.StaticID },
                { "weapon_grade", (int)pendingRentWeapon.currentGrade }
            });

            bool isFirstRent  = !hasEverRentedWeapon;
            selectedWeapon    = pendingRentWeapon;
            isWeaponConfirmed = true;
            hasEverRentedWeapon = true;

            view?.RefreshWeaponDetailButtons(true, currentAdventurer.defaultWeapon);
            view?.UpdateWeaponInfoTab1(selectedWeapon, InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon));
            view?.UpdateTab1Adventurer(currentAdventurer, selectedWeapon);

            view?.UpdateMiniInfoPanels(currentAdventurer, selectedWeapon, assignedItem, InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon));

            view?.HideWeaponDetailPanel();
            view?.ClearWeaponCardSelection();
            UpdateProgressBar();
            Log.Info($"[AdventurePreparationController] 무기 대여 확정: {selectedWeapon.weaponData.weaponName}");

            bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;

            // 튜토리얼 중에는 지원(Tab2)에 머물러 이어서 부적 선물을 유도한다(자동 Tab3 이동 억제).
            if (isFirstRent && !isTutorial)
                SwitchToTab(2);   // 자동 이동 - 클릭 이벤트를 발행하지 않는다

            if (isTutorial)
                TutorialManager.Instance.OnTutorialWeaponRented();
        }

        public void OnBackWeaponRentClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("weapon_selection", "back");
            pendingRentWeapon = null;
            view?.HideWeaponDetailPanel();
            view?.ClearWeaponCardSelection();
        }

        public void OnMiniInfoWeaponClicked()
        {
            if (currentAdventurer == null || selectedWeapon == null) return;

            if (selectedWeapon == currentAdventurer.defaultWeapon)
            {
                OnProgressStepClicked(1);
                view?.SelectWeaponSubTab();
                UIPopupController.Instance?.ShowToast(L("Preparation_WeaponTypeToast",
                    ("weapon", UITranslator.GetString(selectedWeapon.weaponData.weaponType))));
                return;
            }

            if (!isWeaponConfirmed) return;

            pendingRentWeapon = selectedWeapon;
            view?.ShowWeaponDetail(selectedWeapon, true, currentAdventurer.defaultWeapon);
        }

        public void OnCancelWeaponRentClicked()
        {
            if (currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("weapon_selection", "cancel_weapon");

            selectedWeapon    = currentAdventurer.defaultWeapon;
            isWeaponConfirmed = false;
            pendingRentWeapon = null;

            view?.RefreshWeaponDetailButtons(false, currentAdventurer.defaultWeapon);
            view?.UpdateWeaponInfoTab1(selectedWeapon, InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon));
            view?.UpdateTab1Adventurer(currentAdventurer, selectedWeapon);

            view?.UpdateMiniInfoPanels(currentAdventurer, selectedWeapon, assignedItem, InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon));

            UpdateAdventureInfoChips(GetSelectedDungeonEffectiveArmor());

            UpdateProgressBar();
            view?.HideWeaponDetailPanel();
            view?.UpdateStartButton(CanStartAdventure());
            Log.Info($"[AdventurePreparationController] 무기 대여 취소 → 기본 무기로 복귀");
        }

        #endregion

        #region Tab 2 — 지원 (아이템)

        public void OnActiveItemSubTabSelected()
        {
            // Level 2 가상 패널 - 독립 패널이 아닌 서브탭이라 panel_opened만 발행한다
            AnalyticsManager.Instance?.SendPanelOpened("active_item_selection");
            view?.UpdateActiveItemList(
                InventoryManager.Instance.GetAllActiveItems(),
                currentAdventurer?.isNamed ?? false,
                assignedItem?.StaticID ?? string.Empty);
        }

        public void OnActiveItemTypeFilterChanged(int filterIndex)
        {
            AnalyticsManager.Instance?.SendButtonClick("active_item_selection", "type_filter", new Dictionary<string, object>
            {
                { "filter_index", filterIndex }
            });

            currentActiveItemFilter = filterIndex == 0 ? -1 : filterIndex - 1;
            view?.UpdateActiveItemList(
                BuildFilteredActiveItemList(),
                currentAdventurer?.isNamed ?? false,
                assignedItem?.StaticID ?? string.Empty);
        }

        public void OnActiveItemCardClicked(ActiveItemData data)
        {
            if (data == null) return;

            AnalyticsManager.Instance?.SendButtonClick("active_item_selection", "item_show", new Dictionary<string, object>
            {
                { "item_id", data.StaticID }
            });

            pendingAssignItem = data;
            bool isAssigned = assignedItem != null && assignedItem.StaticID == data.StaticID;
            view?.ShowActiveItemDetail(data, isAssigned);

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialActiveItemDetailOpened();
        }

        public void OnAssignItemClicked()
        {
            if (currentAdventurer == null) return;

            if (pendingAssignItem == null) return;

            AnalyticsManager.Instance?.SendButtonClick("active_item_selection", "item_selected", new Dictionary<string, object>
            {
                { "item_id", pendingAssignItem.StaticID }
            });

            if (pendingAssignItem.usageContext == ActiveItemUsage.Immediate)
            {
                int affectionIncrease = (int)pendingAssignItem.effectValue;
                currentAdventurer.AddAffection(affectionIncrease);
                currentAdventurer.adventurerStatData.giftedCount++;
                InventoryManager.Instance.RemoveActiveItem(pendingAssignItem.StaticID);
                QuestManager.Instance?.UpdateProgress(QuestType.GiftComplete);
                Log.Info($"[AdventurePreparationController] 호감도 아이템 즉시 사용: +{affectionIncrease}");
            }
            else if (pendingAssignItem.usageContext == ActiveItemUsage.Adventure)
            {
                if (assignedItem != null)
                    ActiveItemManager.Instance.UnassignItem(currentAdventurer.instanceID);

                ActiveItemManager.Instance.AssignItem(pendingAssignItem, currentAdventurer.instanceID);
                currentAdventurer.adventurerStatData.giftedCount++;
                assignedItem = pendingAssignItem;
                charmBonus   = ActiveItemManager.Instance.GetCharmBonus(currentAdventurer.instanceID);
                hasCharm     = charmBonus > 0f;
                Log.Info($"[AdventurePreparationController] 아이템 배정: {pendingAssignItem.itemName}");
            }

            view?.UpdateMiniInfoPanels(currentAdventurer, selectedWeapon, assignedItem, InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon));
            view?.UpdateActiveItemTab1(assignedItem);
            view?.UpdateActiveItemList(
                InventoryManager.Instance.GetAllActiveItems(),
                currentAdventurer.isNamed,
                assignedItem?.StaticID ?? string.Empty);
            view?.HideActiveItemDetailPanel();
            pendingAssignItem = null;

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialItemGifted();
        }

        public void OnBackActiveItemDetailClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("active_item_selection", "item_cancel");
            pendingAssignItem = null;
            view?.HideActiveItemDetailPanel();
        }

        public void OnMiniInfoActiveItemClicked()
        {
            if (assignedItem == null) return;
            view?.ShowActiveItemDetail(assignedItem, isAssigned: true, isViewOnly: true);
        }

        public void OnMiniInfoEmptyActiveItemClicked()
        {
            OnProgressStepClicked(1);
            view?.SelectActiveItemSubTab();
        }

        public void OnTab1ActiveItemClicked()
        {
            if (assignedItem != null)
                OnMiniInfoActiveItemClicked();
            else
                OnMiniInfoEmptyActiveItemClicked();
        }

        private ActiveItemData pendingAssignItem;

        #endregion

        #region 뒤로가기 (거절)

        public void OnDeclineAdventureClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("adventure_preparation", "decline_adventure");

            var adventurer = currentAdventurer;
            UIManager.Instance.ClosePanel<AdventurePreparationView>();
            VisitorManager.Instance.SetOtherVisitorsFaded(false, adventurer?.currentVisitor);
            adventurer?.currentVisitor?.EndInteraction();
            CameraZoomController.Instance?.ZoomOut();
            ResetState();
            Log.Info($"[AdventurePreparationController] 모험 거절 — 패널 닫기");
        }

        #endregion

        #region Tab 3 — 최종 준비

        public void OnDungeonCardClicked(DungeonData dungeon)
        {
            if (dungeon == null || currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("adventure_preparation", "dungeon_card_click", new Dictionary<string, object>
            {
                { "dungeon_id", dungeon.StaticID }
            });

            pendingDungeon = dungeon;
            PresentDungeonDetail(dungeon);

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialDungeonDetailOpened(dungeon.StaticID);
        }

        /// <summary>성공률 영역 클릭 - 이벤트별 세부 성공률 툴팁을 연다.</summary>
        public void OnSuccessRateAreaClicked()
        {
            if (pendingDungeon == null || currentAdventurer == null || selectedWeapon == null) return;

            bool isArmorTypeKnown = ScoutManager.Instance.IsArmorTypeKnown(pendingDungeon.StaticID);
            if (!InsightManager.Instance.IsSuccessRateBreakdownVisible(currentAdventurer, selectedWeapon, isArmorTypeKnown))
                return;

            ArmorType effectiveArmor = GetDetailEffectiveArmor(pendingDungeon, isArmorTypeKnown);
            view?.ShowPanelEventRateTooltip(BuildEventRateTooltipData(pendingDungeon, effectiveArmor));
        }

        public void OnBackDungeonDetailClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("adventure_preparation", "back_dungeon_detail");
            pendingDungeon = null;
            view?.HideDungeonDetail();
        }

        public void OnSelectDungeonClicked()
        {
            if (pendingDungeon == null || currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("adventure_preparation", "dungeon_selected", new Dictionary<string, object>
            {
                { "dungeon_id", pendingDungeon.StaticID }
            });

            selectedDungeon   = pendingDungeon;
            isDungeonSelected = true;

            view?.HighlightSelectedDungeon(selectedDungeon);

            // 시뮬레이션 카드 갱신 — known 던전은 추적 대상 아님
            if (!ScoutManager.Instance.IsArmorTypeKnown(selectedDungeon.StaticID))
            {
                if (!string.IsNullOrEmpty(simulatedDungeonID) && simulatedDungeonID != selectedDungeon.StaticID)
                    view?.ClearDungeonCardSimulation(simulatedDungeonID);

                simulatedDungeonID = selectedDungeon.StaticID;
                simulatedArmorType = lastSimulatedArmorType;
                view?.UpdateDungeonCardSimulation(selectedDungeon, lastSimulatedArmorType);
            }

            view?.UpdateStartButton(CanStartAdventure());
            UpdateAdventureInfoChips(GetSelectedDungeonEffectiveArmor());
            RefreshSeerResult();
            UpdateProgressBar();
            Log.Info($"[AdventurePreparationController] 던전 선택 확정: {selectedDungeon.dungeonName}");

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialDungeonSelected(selectedDungeon.StaticID);
        }

        private void UpdateAdventureInfoChips(ArmorType armorType)
        {
            if (currentAdventurer == null || selectedWeapon == null)
            {
                view?.ResetAdventureInfo();
                return;
            }

            var cards = BuildAdventureInfoCards(armorType);
            view?.UpdateAdventureInfo(cards);
        }

        private List<AdventureInfoCardData> BuildAdventureInfoCards(ArmorType armorType)
        {
            var cards = new List<AdventureInfoCardData>();

            // 1. 호감도 (항상 확정)
            float affBonus = GetAffectionBonusForUI(currentAdventurer);
            if (!Mathf.Approximately(affBonus, 0f))
                cards.Add(new AdventureInfoCardData { type = BonusType.Affection, value = affBonus, isConfirmed = true });

            // 2. 부적 (항상 확정) — effectBase에 곱해지는 곱연산 보정
            if (hasCharm && !Mathf.Approximately(charmBonus, 0f))
                cards.Add(new AdventureInfoCardData { type = BonusType.Charm, value = charmBonus, isConfirmed = true, isMultiplier = true });

            // 3. 특성 — 특성이 공개된 경우에만 칩 추가 (통찰 80+면 자동 공개로 간주)
            // Berserker/Coward는 성공률 곱배율(전체 결과 x) → x배수 표기, 나머지는 가산 %p
            float traitBonus = GetTraitBonusForUI(currentAdventurer, selectedDungeon);
            bool traitIsMultiplier = currentAdventurer.Trait == TraitType.Berserker
                                     || currentAdventurer.Trait == TraitType.Coward;
            if (InsightManager.Instance.IsTraitKnown(currentAdventurer)
                && !Mathf.Approximately(traitBonus, 0f))
                cards.Add(new AdventureInfoCardData { type = BonusType.Trait, value = traitBonus, isConfirmed = true, isMultiplier = traitIsMultiplier });

            // 던전 미선택 시 던전 종속 칩은 추가하지 않음
            if (selectedDungeon == null) return cards;

            // 던전 등급 (항상 확정) — baseline 척도
            var infoCfg = ConfigManager.Instance.AdventureInfo;
            int gradeIndex = (int)selectedDungeon.grade;
            if (infoCfg != null && infoCfg.dungeonGradeSegments != null
                && gradeIndex >= 0 && gradeIndex < infoCfg.dungeonGradeSegments.Length)
            {
                float gradeValue = infoCfg.dungeonGradeSegments[gradeIndex] * infoCfg.dungeonGradeUnit;
                cards.Add(new AdventureInfoCardData { type = BonusType.DungeonGrade, value = gradeValue, isConfirmed = true });
            }

            var bd = AdventureManager.Instance.CalculateSuccessRateBreakdown(currentAdventurer, selectedWeapon, selectedDungeon, armorType);
            bool isWeaponTypeKnown = InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon);
            bool armorKnown        = ScoutManager.Instance.IsArmorTypeKnown(selectedDungeon.StaticID);

            // 4. 던전 상성 (armorBonus) — 무기 타입 공개 시. effectBase에 곱해지는 곱연산 보정
            if (isWeaponTypeKnown && !Mathf.Approximately(bd.armorBonus, 0f))
                cards.Add(new AdventureInfoCardData { type = BonusType.DungeonArmor, value = bd.armorBonus, isConfirmed = armorKnown, isMultiplier = true });

            // 5. 무기 조건 (DungeonGradeBonus + ArmorTypeBonus)
            if (!Mathf.Approximately(bd.conditionBonus, 0f))
            {
                bool hasArmorTypeBonus = !Mathf.Approximately(bd.armorTypeBonusOnly, 0f);
                bool weaponCondConfirmed = !hasArmorTypeBonus || armorKnown;
                cards.Add(new AdventureInfoCardData { type = BonusType.WeaponCondition, value = bd.conditionBonus, isConfirmed = weaponCondConfirmed });
            }

            // 6. 컬렉션 (항상 확정)
            float collectionBonus = GetCollectionBonusForUI(selectedDungeon, selectedWeapon);
            if (!Mathf.Approximately(collectionBonus, 0f))
                cards.Add(new AdventureInfoCardData { type = BonusType.Collection, value = collectionBonus, isConfirmed = true });

            // 7. 점술 (완료 시 항상 확정)
            float seerMod = SeerManager.Instance.GetLuckModifier(currentAdventurer, selectedDungeon);
            if (!Mathf.Approximately(seerMod, 0f))
                cards.Add(new AdventureInfoCardData { type = BonusType.Seer, value = seerMod, isConfirmed = true });

            return cards;
        }

        private float GetAffectionBonusForUI(AdventurerInstance adv)
        {
            return adv.GetAffectionLevel() switch
            {
                AffectionLevel.Max    => ConfigManager.Instance.Adventure.affectionMaxBonus,
                AffectionLevel.High   => ConfigManager.Instance.Adventure.affectionHighBonus,
                AffectionLevel.Medium => ConfigManager.Instance.Adventure.affectionMediumBonus,
                _ => 0f
            };
        }

        private float GetTraitBonusForUI(AdventurerInstance adv, DungeonData dungeon)
        {
            float bonus = 0f;
            if (dungeon != null)
                bonus += AdventureManager.Instance.GetTraitSuccessBonus(adv, dungeon);
            float mult = AdventureManager.Instance.GetTraitSuccessMultiplier(adv);
            bonus += (mult - 1f);
            return bonus;
        }

        private float GetCollectionBonusForUI(DungeonData dungeon, WeaponInstance weapon)
        {
            float bonus = 0f;
            var playerData = LegacyManager.Instance.PlayerData;
            var cfg = ConfigManager.Instance.Adventure;

            if (playerData.dungeonStats.TryGetValue(dungeon.StaticID, out var clearStat))
            {
                int milestones = clearStat.successCount / cfg.dungeonClearMilestone;
                bonus += milestones * cfg.dungeonClearMilestoneBonus;
            }

            if (playerData.weaponDiscoveryProgress.TryGetValue(weapon.weaponData.StaticID, out int progress))
            {
                if (progress >= cfg.weaponUsageMilestone)
                {
                    int milestone = Mathf.Min(progress / cfg.weaponUsageMilestone, cfg.weaponUsageMilestoneMax);
                    bonus += milestone * cfg.weaponUsageMilestoneBonus;
                }
            }
            return bonus;
        }

        public void OnDetailPrevClicked()
        {
            view?.ShowDetailTab(0);
        }

        public void OnDetailNextClicked()
        {
            view?.ShowDetailTab(1);
        }

        public void OnArmorTypeSimulationChanged(ArmorType armorType)
        {
            lastSimulatedArmorType = armorType;
            DungeonData dungeon = pendingDungeon ?? selectedDungeon;
            if (dungeon == null || currentAdventurer == null || selectedWeapon == null) return;

            bool isArmorTypeKnown   = ScoutManager.Instance.IsArmorTypeKnown(dungeon.StaticID);
            ArmorType effectiveArmor = isArmorTypeKnown
                ? ScoutManager.Instance.GetKnownArmorType(dungeon.StaticID)
                : armorType;

            RefreshArmorAffectedSections(dungeon, effectiveArmor, isArmorTypeKnown);

            // 8단계: 던전 B를 경장갑으로 가정 시뮬레이션하면 튜토리얼이 다음 단계로 진행한다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialSimulated(dungeon.StaticID, armorType);
        }

        public void OnMaterialInfoClicked(MaterialData material)
        {
            if (material == null) return;

            UIManager.Instance.OpenPanel<MaterialDetailPopup>();
            UIManager.Instance.GetOrInstantiatePanel<MaterialDetailPopup>()?.Initialize(material);
        }

        public void OnArmorInfoClicked(ArmorType armor)
        {
            UIManager.Instance.OpenPanel<ArmorTypeDetailPopup>();
            UIManager.Instance.GetOrInstantiatePanel<ArmorTypeDetailPopup>()?.Initialize(armor);
        }

        public void OnSeerButtonClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("adventure_preparation", "seer");

            if (pendingDungeon == null && selectedDungeon == null)
            {
                UIPopupController.Instance?.ShowToast(M("Preparation_SelectDungeonFirst"), type: PopupSfxType.Warning);
                return;
            }

            DungeonData targetDungeon = pendingDungeon ?? selectedDungeon;
            UIManager.Instance.OpenPanel<SeerView>(() =>
                UIControllerManager.Instance.GetController<SeerController>()
                    ?.Initialize(currentAdventurer, targetDungeon));
        }

        public void RefreshSeerResult()
        {
            if (!view.IsOpen) return;
            RefreshSeerIndicator();
            var targetDungeon = pendingDungeon ?? selectedDungeon;
            if (targetDungeon == null) return;

            view?.RefreshDungeonSeerGlow(targetDungeon);
            UpdateAdventureInfoChips(GetSelectedDungeonEffectiveArmor());

            // 6-D: 점술 상담이 실제로 완료됐으면(SeerView 닫힘 시점) 튜토리얼 진행 훅 호출.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialSeerConsulted(currentAdventurer, targetDungeon);
        }

        private void RefreshSeerIndicator()
        {
            bool done = selectedDungeon != null && currentAdventurer != null
                && !SeerManager.Instance.CanConsult(currentAdventurer, selectedDungeon);
            view?.UpdateSeerIndicator(done);
        }

        public void OnStartAdventure()
        {
            AnalyticsManager.Instance?.SendButtonClick("adventure_preparation", "start_adventure", new Dictionary<string, object>
            {
                { "dungeon_id", selectedDungeon != null ? selectedDungeon.StaticID : string.Empty },
                { "weapon_id", selectedWeapon != null ? selectedWeapon.weaponData.StaticID : string.Empty },
                { "weapon_grade", selectedWeapon != null ? (int)selectedWeapon.currentGrade : -1 }
            });

            if (!CanStartAdventure())
            {
                Log.Warn("[AdventurePreparationController] 모험 시작 조건 미충족");
                return;
            }

            StartAdventureConfirmed();
        }

        private void StartAdventureConfirmed()
        {
            var adventure = AdventureManager.Instance.StartAdventure(
                currentAdventurer,
                selectedWeapon,
                selectedDungeon);

            if (adventure == null) return;

            SoundManager.Instance?.PlaySFX("AdventureStart");

            Log.Info($"[AdventurePreparationController] 모험 시작: {selectedDungeon.dungeonName}");

            currentAdventurer?.currentVisitor?.EndInteraction();
            VisitorManager.Instance.SetOtherVisitorsFaded(false, currentAdventurer?.currentVisitor);
            
            UIManager.Instance.ClosePanel<AdventurePreparationView>();
            CameraZoomController.Instance?.ZoomOut();
            ResetState();

            // 6-E: 준비화면이 닫힌 뒤 마무리 대사를 배경 위에 띄운다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialAdventureStarted();
        }

        private bool CanStartAdventure()
        {
            return isDungeonSelected && selectedWeapon != null && selectedDungeon != null;
        }

        #endregion

        #region 이벤트 핸들러

        private void OnTimeChanged(int hour, int minute) => view?.UpdateTimeDisplay();

        // 준비 패널을 열어둔 채 시간이 흘러 수색이 완료되면(스탯 테스트 등) 던전 카드 아이콘을 공개된 값으로 갱신.
        private void OnScoutComplete(string dungeonStaticID, ArmorType armorType)
        {
            if (view == null || !view.IsOpen) return;

            // ? 아이콘 → 공개된 방어타입 아이콘으로 카드 갱신
            view.RefreshDungeonCardArmorType(dungeonStaticID);

            // 이 던전을 시뮬레이션 추적 중이었다면 이제 확정값이므로 추적 해제
            if (simulatedDungeonID == dungeonStaticID)
            {
                simulatedDungeonID = null;
                simulatedArmorType = ArmorType.Unarmored;
            }

            // 선택된 던전이면 정보 칩 재계산 (확정 여부/유효 방어타입 반영)
            if (selectedDungeon != null && selectedDungeon.StaticID == dungeonStaticID)
                UpdateAdventureInfoChips(GetSelectedDungeonEffectiveArmor());
        }

        #endregion

        #region 내부 메서드

        private void OpenTalkAnimationPopup(int cost, Sprite icon, string title, string desc,
            Func<(bool, string)> execute)
        {
            UIManager.Instance.OpenPanel<AdventureTalkAnimationPopup>();
            UIManager.Instance.GetOrInstantiatePanel<AdventureTalkAnimationPopup>()
                ?.Initialize(cost, icon, title, desc, execute, OnTalkActionComplete);
        }

        private void OnTalkActionComplete(bool success, string message)
        {
            if (currentAdventurer == null || !view.IsOpen) return;

            view?.UpdateTab1Adventurer(currentAdventurer, selectedWeapon);
            view?.UpdateTalkButtons(currentAdventurer);
            view?.UpdateWeaponInfoTab1(selectedWeapon, InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon));
            view?.UpdateMiniInfoPanels(currentAdventurer, selectedWeapon, assignedItem, InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon));

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialTalkActionCompleted();
        }

        private List<WeaponInstance> BuildWeaponList()
        {
            if (currentAdventurer == null) return new List<WeaponInstance>();

            // 보유 중이고 대여 중이 아닌 무기 (defaultWeapon 제외)
            var available = InventoryManager.Instance.GetAvailableWeapons()
                .Where(w => w != currentAdventurer.defaultWeapon);

            if (currentWeaponTypeFilter >= 0)
            {
                WeaponType filterType = (WeaponType)currentWeaponTypeFilter;
                available = available.Where(w => w.weaponData.weaponType == filterType);
            }

            return available.OrderByDescending(w => w.currentGrade).ToList();
        }

        private List<ActiveItemInstance> BuildFilteredActiveItemList()
        {
            var all = InventoryManager.Instance.GetAllActiveItems();
            if (currentActiveItemFilter < 0) return all;

            ActiveItemType filterType = (ActiveItemType)currentActiveItemFilter;
            return all.Where(i => i.activeItemData.itemType == filterType).ToList();
        }

        #endregion

        #region 던전 상세 패널 — 빌더/호출

        /// <summary>
        /// 던전 상세 패널을 열고 5개 영역(armor toggle, effect list, success rate rows,
        /// duration, dungeon info tab) + effect hint를 모두 갱신한다.
        /// </summary>
        private void PresentDungeonDetail(DungeonData dungeon)
        {
            if (dungeon == null || currentAdventurer == null) return;

            string dungeonID         = dungeon.StaticID;
            bool isArmorTypeKnown    = ScoutManager.Instance.IsArmorTypeKnown(dungeonID);
            ArmorType effectiveArmor = GetDetailEffectiveArmor(dungeon, isArmorTypeKnown);
            if (!isArmorTypeKnown)
                lastSimulatedArmorType = effectiveArmor;

            view?.ShowDungeonDetailPanel(dungeon);

            view?.UpdatePanelArmorToggles(
                isArmorTypeKnown,
                effectiveArmor,
                DungeonHasArmorType(dungeon, ArmorType.Unarmored),
                DungeonHasArmorType(dungeon, ArmorType.LightArmor),
                DungeonHasArmorType(dungeon, ArmorType.HeavyArmor),
                DungeonHasArmorType(dungeon, ArmorType.MagicalArmor));

            if (selectedWeapon != null)
            {
                view?.UpdatePanelEffectList(
                    selectedWeapon,
                    BuildEffectStates(selectedWeapon, currentAdventurer, dungeon, effectiveArmor));
                UpdateSuccessRateRowsSection(dungeon, effectiveArmor, isArmorTypeKnown);
            }

            bool isWeaponSelected = selectedWeapon != null && selectedWeapon != currentAdventurer.defaultWeapon;
            view?.UpdatePanelEffectHint(isWeaponSelected);

            UpdateDurationSection(dungeon);

            var stat = AdventureManager.Instance.GetDungeonStat(dungeonID);
            view?.UpdatePanelDungeonInfoTab(dungeon, stat);
        }

        /// <summary>armor 토글 변경 시 영향 받는 2개 영역만 재갱신.</summary>
        private void RefreshArmorAffectedSections(DungeonData dungeon, ArmorType effectiveArmor, bool isArmorTypeKnown)
        {
            if (selectedWeapon == null || currentAdventurer == null) return;
            view?.UpdatePanelEffectList(
                selectedWeapon,
                BuildEffectStates(selectedWeapon, currentAdventurer, dungeon, effectiveArmor));
            UpdateSuccessRateRowsSection(dungeon, effectiveArmor, isArmorTypeKnown);
        }

        private void UpdateSuccessRateRowsSection(DungeonData dungeon, ArmorType effectiveArmor, bool isArmorTypeKnown)
        {
            var bd = AdventureManager.Instance.CalculateSuccessRateBreakdown(currentAdventurer, selectedWeapon, dungeon, effectiveArmor);
            string traitNote = BuildTraitSuccessNote(currentAdventurer, dungeon);
            // 실제 판정(CalculateEventSuccessRate)의 입력값. 이벤트 난이도·운세·기분 배율은 출발 후에 정해지므로 제외된다.
            float expectedRate = AdventureManager.Instance.CalculateSuccessRate(currentAdventurer, selectedWeapon, dungeon, effectiveArmor);
            view?.UpdatePanelSuccessRateRows(
                showBaseRate:       InsightManager.Instance.IsSuccessRateBreakdownVisible(currentAdventurer, selectedWeapon, isArmorTypeKnown),
                expectedRate:       expectedRate,
                baseRate:           bd.baseRate,
                statEffectBonus:    bd.statEffectBonus,
                showArmorBonusRow:  InsightManager.Instance.IsWeaponTypeKnown(currentAdventurer, selectedWeapon),
                armorBonus:         bd.armorBonus,
                conditionBonus:     bd.conditionBonus,
                showCharmRow:       hasCharm,
                charmBonus:         hasCharm ? charmBonus : 0f,
                showTraitRow:       InsightManager.Instance.IsTraitKnown(currentAdventurer),
                traitNote:          traitNote);
        }

        /// <summary>
        /// 상세 패널이 보고 있는 던전의 표시용 방어 타입.
        /// 공개됐으면 실제값, 아니면 이 던전을 시뮬레이션한 값(없으면 주 방어타입 - 항상 활성 토글)을 쓴다.
        /// 전역 lastSimulatedArmorType을 fallback으로 쓰면 다른 던전에서 고른 잠긴 타입이 새어든다.
        /// </summary>
        private ArmorType GetDetailEffectiveArmor(DungeonData dungeon, bool isArmorTypeKnown)
        {
            string dungeonID = dungeon.StaticID;
            if (isArmorTypeKnown)
                return ScoutManager.Instance.GetKnownArmorType(dungeonID);
            return simulatedDungeonID == dungeonID ? simulatedArmorType : dungeon.armorType;
        }

        /// <summary>
        /// 이벤트별 성공률 툴팁에 넘길 값 묶음. 전투 성공률은 이벤트 난이도 계수를 반영하고,
        /// 기분 배율과 운세는 출발 시점에 정해지므로 범위/확정값만 따로 전달한다.
        /// </summary>
        private EventRateTooltipData BuildEventRateTooltipData(DungeonData dungeon, ArmorType effectiveArmor)
        {
            var mgr = AdventureManager.Instance;
            var cfg = ConfigManager.Instance.Adventure;
            var visibility = InsightManager.Instance.GetStatVisibility(currentAdventurer);
            string adventurerID = currentAdventurer.instanceID;

            float escapeRope = ActiveItemManager.Instance != null
                ? ActiveItemManager.Instance.GetEscapeRopeBonus(adventurerID) : 0f;
            float deathWard  = ActiveItemManager.Instance != null
                ? ActiveItemManager.Instance.GetDeathWardBonus(adventurerID) : 0f;

            float battleDiff   = mgr.GetEventDifficultyMultiplier(dungeon, DungeonEventType.Battle, out _);
            float miniBossDiff = mgr.GetEventDifficultyMultiplier(dungeon, DungeonEventType.MiniBoss, out bool hasMiniBoss);
            float bossDiff     = mgr.GetEventDifficultyMultiplier(dungeon, DungeonEventType.Boss, out _);
            mgr.GetEventDifficultyMultiplier(dungeon, DungeonEventType.Trap, out bool hasTrap);

            float luck = SeerManager.Instance.GetLuckModifier(currentAdventurer, dungeon);
            float greatSuccess = mgr.GetGreatSuccessChance(dungeon, currentAdventurer, selectedWeapon, out bool guaranteed);

            return new EventRateTooltipData
            {
                battleRate   = GetEventRate(dungeon, effectiveArmor, battleDiff),
                hasMiniBoss  = hasMiniBoss,
                miniBossRate = GetEventRate(dungeon, effectiveArmor, miniBossDiff),
                bossRate     = GetEventRate(dungeon, effectiveArmor, bossDiff),

                hasTrap       = hasTrap,
                trapEvadeRate = mgr.CalculateTrapEvadeChance(currentAdventurer, selectedWeapon, escapeRope),
                isDexKnown    = visibility.IsVisible(AdventurerStat.DEX),
                deathRate     = mgr.CalculateDeathRate(currentAdventurer, selectedWeapon, dungeon, deathWard),
                survivalRate  = mgr.GetStrengthSurvivalChance(currentAdventurer),
                isStrKnown    = visibility.IsVisible(AdventurerStat.STR),

                greatSuccessRate       = greatSuccess,
                greatSuccessGuaranteed = guaranteed,

                moodMin = Mathf.Min(cfg.moodNormalRange.x, cfg.moodMoodyLowRange.x, cfg.moodDepressedRange.x,
                                    cfg.moodOverconfidentRange.x, cfg.moodConfidentRange.x),
                moodMax = Mathf.Max(cfg.moodNormalRange.y, cfg.moodMoodyHighRange.y, cfg.moodDepressedRange.y,
                                    cfg.moodOverconfidentRange.y, cfg.moodConfidentRange.y),
                hasLuck      = !Mathf.Approximately(luck, 0f),
                luckModifier = luck,
                trapPenalty  = cfg.trapSuccessPenalty,
            };
        }

        /// <summary>이벤트 난이도 계수를 반영한 표시용 성공률. 실제 판정과 동일하게 마지막에 한 번 자른다.</summary>
        private float GetEventRate(DungeonData dungeon, ArmorType effectiveArmor, float difficultyMultiplier)
        {
            var cfg = ConfigManager.Instance.Adventure;
            float rate = AdventureManager.Instance.CalculateSuccessRate(
                currentAdventurer, selectedWeapon, dungeon, effectiveArmor, difficultyMultiplier);
            return Mathf.Clamp(rate, cfg.successRateMin, cfg.successRateMax);
        }

        private void UpdateDurationSection(DungeonData dungeon)
        {
            bool isRevealed = InsightManager.Instance.CanRevealEstimatedDuration();
            if (!isRevealed)
            {
                view?.UpdatePanelDuration(false, 0f, 1f);
                return;
            }

            int eventCount = ConfigManager.Instance.Adventure.maxEventCountByGrade != null
                ? ConfigManager.Instance.Adventure.maxEventCountByGrade[(int)dungeon.grade]
                : 3;
            float baseDuration = eventCount * ConfigManager.Instance.Adventure.eventIntervalHours
                * (LegacyManager.Instance?.GetAdventureSpeedMultiplier() ?? 1f);
            bool isDoubleEvent = QuestBoardManager.Instance.IsHighlightedDungeon(dungeon.StaticID);

            float traitMultiplier = 1f;
            if (currentAdventurer != null && InsightManager.Instance.IsTraitKnown(currentAdventurer)
                && (currentAdventurer.Trait == TraitType.Swift || currentAdventurer.Trait == TraitType.Focused))
                traitMultiplier = AdventureManager.Instance.GetTraitDurationMultiplier(currentAdventurer);

            float shoesMultiplier = 1f;
            if (currentAdventurer != null && ActiveItemManager.Instance != null)
                shoesMultiplier = ActiveItemManager.Instance.GetSwiftShoesMultiplier(currentAdventurer.instanceID);

            float totalMultiplier = (isDoubleEvent ? 2f : 1f) * traitMultiplier * shoesMultiplier;
            float displayTime     = baseDuration * totalMultiplier;
            view?.UpdatePanelDuration(true, displayTime, totalMultiplier);
        }

        private IReadOnlyList<EffectDisplayState> BuildEffectStates(WeaponInstance weapon, AdventurerInstance adv,
            DungeonData dungeon, ArmorType effectiveArmor)
        {
            if (weapon?.effects == null) return System.Array.Empty<EffectDisplayState>();
            var list = new List<EffectDisplayState>(weapon.effects.Count);
            foreach (var effect in weapon.effects)
                list.Add(AdventureManager.Instance.GetEffectDisplayState(effect, adv, dungeon, effectiveArmor));
            return list;
        }

        private ArmorType GetSelectedDungeonEffectiveArmor()
        {
            if (selectedDungeon == null) return ArmorType.Unarmored;
            string id = selectedDungeon.StaticID;
            if (ScoutManager.Instance.IsArmorTypeKnown(id))
                return ScoutManager.Instance.GetKnownArmorType(id);
            if (simulatedDungeonID == id) return simulatedArmorType;
            return ArmorType.Unarmored;
        }

        private bool DungeonHasArmorType(DungeonData dungeon, ArmorType armor)
        {
            if (dungeon == null) return false;
            if (dungeon.armorType == armor) return true;
            if (dungeon.armorTypeVariants != null)
                foreach (var v in dungeon.armorTypeVariants)
                    if (v.armorType == armor) return true;
            return false;
        }

        private string BuildTraitSuccessNote(AdventurerInstance adv, DungeonData dungeon)
        {
            if (adv == null || dungeon == null) return null;
            var mgr = AdventureManager.Instance;
            switch (adv.Trait)
            {
                case TraitType.Berserker:
                case TraitType.Coward:
                {
                    // 성공률·사망률 모두 전체 결과에 곱해지는 곱배율 → x배수로 표기
                    float successMult = mgr.GetTraitSuccessMultiplier(adv);
                    float deathMult   = mgr.GetTraitDeathMultiplier(adv);
                    return L("Preparation_TraitNoteMultiplier",
                        ("trait",   UITranslator.GetString(adv.Trait)),
                        ("success", $"{successMult:0.##}"),
                        ("death",   $"{deathMult:0.##}"));
                }
                case TraitType.Rising:
                case TraitType.EasyExpert:
                case TraitType.BattleManiac:
                case TraitType.Focused:
                {
                    float bonus = mgr.GetTraitSuccessBonus(adv, dungeon);
                    string sign = bonus >= 0f ? "+" : "";
                    return $"{UITranslator.GetString(adv.Trait)} {sign}{bonus * 100f:F0}%";
                }
                default:
                    return null;
            }
        }

        #endregion
    }
}
