using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 진화 확인 팝업 View (WeaponPanel에서 진화 버튼 클릭 시 열림)
    /// </summary>
    public class BlacksmithEvolveConfirmView : BaseView
    {
        [Header("Before Weapon Info")]
        [SerializeField] private Image beforeWeaponIcon;
        [SerializeField] private TextMeshProUGUI beforeWeaponNameText;
        [SerializeField] private Image beforeBGImage;
        [SerializeField] private Image beforeFrameImage;
        [SerializeField] private TextMeshProUGUI beforeEnforceLevelText;

        [Header("After Weapon Info")]
        [SerializeField] private Image afterWeaponIcon;
        [SerializeField] private TextMeshProUGUI afterWeaponNameText;
        [SerializeField] private Image afterBGImage;
        [SerializeField] private Image afterFrameImage;
        [SerializeField] private TextMeshProUGUI afterEnforceLevelText;

        [Header("Effect List")]
        [SerializeField] private Transform effectListContainer;
        [SerializeField] private GameObject effectListItemPrefab;

        [Header("Cost Info")]
        [SerializeField] private TextMeshProUGUI goldCostText;
        [SerializeField] private ScrollRect requiredMaterialScrollRect;
        [SerializeField] private Transform requiredMaterialContainer;
        [SerializeField] private GameObject requiredMaterialItemPrefab;
        [Tooltip("개수를 충족한 재료 종류 수 / 전체 재료 종류 수")]
        [SerializeField] private TextMeshProUGUI materialSatisfiedText;

        [Header("ForgeStone Slot")]
        [SerializeField] private GameObject forgeStoneSlotFilled;
        [SerializeField] private Image forgeStoneIcon;
        [SerializeField] private Button forgeStoneSlotButton;
        [SerializeField] private Button forgeStoneRemoveButton;

        [Header("Success Rate")]
        [SerializeField] private TextMeshProUGUI successRateText;
        [SerializeField] private Slider successRateSlider;
        [SerializeField] private Slider successRateBonusSlider;

        [Header("Buttons")]
        [SerializeField] private Button evolveButton;
        [SerializeField] private Button closeButton;

        private BlacksmithController controller;
        private WeaponInstance selectedWeapon;
        private ActiveItemData lastUsedForgeStoneData;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = true;
            canEscape = true;
        }

        protected override void SubscribeEvents()
        {
            evolveButton?.onClick.AddListener(OnEvolveClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
            forgeStoneSlotButton?.onClick.AddListener(OnForgeStoneSlotClicked);
            forgeStoneRemoveButton?.onClick.AddListener(OnForgeStoneRemoveClicked);
        }

        protected override void UnsubscribeEvents()
        {
            evolveButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
            forgeStoneSlotButton?.onClick.RemoveAllListeners();
            forgeStoneRemoveButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(WeaponInstance weapon, BlacksmithController ctrl)
        {
            controller = ctrl;
            selectedWeapon = weapon;

            // 이전에 사용한 강화석이 인벤토리에 남아있으면 자동 재배정
            if (lastUsedForgeStoneData != null)
            {
                bool hasInInventory = InventoryManager.Instance.GetAllActiveItems()
                    .Any(i => i.activeItemData.StaticID == lastUsedForgeStoneData.StaticID
                              && i.activeItemData.usageContext == ActiveItemUsage.Blacksmith);
                if (hasInInventory)
                    ActiveItemManager.Instance.SetBlacksmithItem(lastUsedForgeStoneData);
                lastUsedForgeStoneData = null;
            }

            RefreshUI();

            // 튜토리얼 2-D: 진화 확인 화면이 열리면 진화 실행 버튼 하이라이트(강화석은 건너뜀)
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialEvolveConfirmOpened();
        }

        /// <summary>튜토리얼 하이라이트용 — 진화 실행 버튼 RectTransform.</summary>
        public RectTransform GetEvolveButtonRect()
        {
            var rect = evolveButton?.transform as RectTransform;
            // 버튼이 HorizontalLayoutGroup 안이라 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }

        #endregion

        #region UI 갱신

        private void RefreshUI()
        {
            if (selectedWeapon == null) return;

            var weapon = selectedWeapon;

            Grade afterGrade = weapon.currentGrade + 1;

            // Before Weapon Info
            if (beforeWeaponIcon != null && weapon.weaponData.icon != null)
                beforeWeaponIcon.sprite = weapon.weaponData.icon;
            if (beforeWeaponNameText != null)
            {
                beforeWeaponNameText.text = weapon.weaponData.DisplayName + $"+{weapon.enforceLevel}";
                beforeWeaponNameText.color = ColorManager.Instance.GetGradeAccentColor(weapon.currentGrade);
            }
            if (beforeBGImage != null)
                beforeBGImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
            if (beforeFrameImage != null)
                beforeFrameImage.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            if (beforeEnforceLevelText != null)
                beforeEnforceLevelText.text = $"+{weapon.enforceLevel}";

            // After Weapon Info
            if (afterWeaponIcon != null && weapon.weaponData.icon != null)
                afterWeaponIcon.sprite = weapon.weaponData.icon;
            if (afterWeaponNameText != null)
            {
                afterWeaponNameText.text = weapon.weaponData.DisplayName;
                afterWeaponNameText.color = ColorManager.Instance.GetGradeAccentColor(afterGrade);
            }
            if (afterBGImage != null)
                afterBGImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(afterGrade);
            if (afterFrameImage != null)
                afterFrameImage.sprite = IconManager.Instance.GetFrameByGrade(afterGrade);
            if (afterEnforceLevelText != null)
                afterEnforceLevelText.text = $"+{weapon.enforceLevel}";

            // 비용
            if (goldCostText != null)
            {
                int baseGoldCost = BlacksmithManager.Instance.CalculateEvolveCost(weapon.currentGrade);
                int legacyCost = Mathf.RoundToInt(baseGoldCost * (LegacyManager.Instance?.GetEvolveCostMultiplier() ?? 1f));
                int goldCost = BlacksmithManager.Instance.ApplyCostReduction(legacyCost);
                bool canAffordGold = EconomyManager.Instance.CurrentGold >= goldCost;

                goldCostText.color = canAffordGold ? ColorManager.Instance.GetBlackColor() : ColorManager.Instance.GetRedColor();
                goldCostText.text = UITranslator.GetGoldCostString(legacyCost, goldCost);
            }

            UpdateEffectList(weapon);
            UpdateMaterialList(weapon);
            RefreshForgeStoneSlot();
            RefreshSuccessRate(weapon);

            if (evolveButton != null)
                evolveButton.interactable = BlacksmithManager.Instance.CanAffordEvolve(weapon);
        }

        private void UpdateEffectList(WeaponInstance weapon)
        {
            if (effectListContainer == null || effectListItemPrefab == null) return;

            foreach (Transform child in effectListContainer)
                Destroy(child.gameObject);

            foreach (var effect in BlacksmithManager.Instance.GetSimulatedEvolvedEffects(weapon))
            {
                var obj = Instantiate(effectListItemPrefab, effectListContainer);
                obj.GetComponent<WeaponEffectListItem>()?.Initialize(effect, false);
            }
        }

        private void UpdateMaterialList(WeaponInstance weapon)
        {
            if (requiredMaterialContainer == null || requiredMaterialItemPrefab == null) return;

            foreach (Transform child in requiredMaterialContainer)
                Destroy(child.gameObject);

            bool isDiscounted = BlacksmithManager.Instance.IsMaterialDiscountActive;

            int total = 0;
            int satisfied = 0;

            foreach (var (material, count) in BlacksmithManager.Instance.GetEvolveMaterials(weapon.currentGrade))
            {
                var obj = Instantiate(requiredMaterialItemPrefab, requiredMaterialContainer);
                var item = obj.GetComponent<RequiredMaterialItem>();
                if (item == null) continue;

                total++;
                if (item.Initialize(material, count, isDiscounted,
                        (mat, cnt) => controller?.OnMaterialDetailClicked(mat)))
                    satisfied++;
            }

            if (materialSatisfiedText != null)
            {
                materialSatisfiedText.text = $"{satisfied}/{total}";
                materialSatisfiedText.color = satisfied >= total
                    ? ColorManager.Instance.GetGreenColor()
                    : ColorManager.Instance.GetRedColor();
            }

            // 스크롤 위치 초기화
            if (requiredMaterialScrollRect != null)
                requiredMaterialScrollRect.ResetPosition(this);
        }

        private void RefreshForgeStoneSlot()
        {
            var item = ActiveItemManager.Instance.GetBlacksmithItem();
            bool has = item != null;

            forgeStoneSlotFilled?.SetActive(has);
            forgeStoneRemoveButton?.gameObject.SetActive(has);

            if (has)
            {
                if (forgeStoneIcon != null && item.icon != null)
                    forgeStoneIcon.sprite = item.icon;
            }
            else
            {
                if (forgeStoneIcon != null) forgeStoneIcon.sprite = null;
            }

            if (selectedWeapon != null)
                RefreshSuccessRate(selectedWeapon);
        }

        private void RefreshSuccessRate(WeaponInstance weapon)
        {
            float baseRate = ConfigManager.Instance.Blacksmith.evolveSuccessRates[(int)weapon.currentGrade];
            float legacy = LegacyManager.Instance?.GetEvolveRateBonus() ?? 0f;
            float finalRate = BlacksmithManager.Instance.GetSuccessRate(baseRate, legacy, out float rawBonus);

            // 튜토리얼은 ExecuteEvolve가 성공을 강제하므로 표시도 100%로 맞춘다 (보너스 표기는 숨긴다)
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                baseRate = 100f;
                finalRate = 100f;
                rawBonus = 0f;
            }

            if (successRateSlider != null)
                successRateSlider.value = baseRate / 100f;

            if (successRateBonusSlider != null)
                successRateBonusSlider.value = finalRate / 100f;

            if (successRateText != null)
            {
                string goldHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGoldColor());
                string rateStr = finalRate >= 100f ? $"<color={goldHex}>100</color>" : $"{finalRate:F1}";
                if (rawBonus > 0f)
                {
                    float gainPct = finalRate - baseRate;
                    bool hasForge = ActiveItemManager.Instance.GetForgeStoneBonus() > 0f;
                    string greenHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor());
                    string bonusStr = hasForge
                        ? $"<color={greenHex}>+{gainPct:0.#}%</color>"
                        : $"+{gainPct:0.#}%";
                    successRateText.text = L("Blacksmith_ExpectedRateWithBonus",
                        ("rate", rateStr), ("bonus", bonusStr));
                }
                else
                {
                    successRateText.text = L("Blacksmith_ExpectedRate", ("rate", rateStr));
                }
            }
        }

        #endregion

        #region 버튼 이벤트

        private void OnEvolveClicked()
        {
            if (selectedWeapon == null) return;

            int baseGoldCost = BlacksmithManager.Instance.CalculateEvolveCost(selectedWeapon.currentGrade);
            int legacyCost = Mathf.RoundToInt(baseGoldCost * (LegacyManager.Instance?.GetEvolveCostMultiplier() ?? 1f));
            int goldCost = BlacksmithManager.Instance.ApplyCostReduction(legacyCost);

            EconomyManager.Instance.EnsureGold(goldCost, onReady: DoEvolve);
        }

        private void DoEvolve()
        {
            lastUsedForgeStoneData = ActiveItemManager.Instance.GetBlacksmithItem();
            controller?.OnEvolveWeapon(selectedWeapon);
            UIManager.Instance?.ClosePanel<BlacksmithEvolveConfirmView>();
        }

        private void OnCloseClicked()
        {
            ActiveItemManager.Instance?.ClearBlacksmithItem();
            lastUsedForgeStoneData = null;
            UIManager.Instance?.ClosePanel<BlacksmithEvolveConfirmView>();
        }

        private void OnForgeStoneSlotClicked()
        {
            if (selectedWeapon == null) return;

            float baseRate = ConfigManager.Instance.Blacksmith.evolveSuccessRates[(int)selectedWeapon.currentGrade];
            float legacy = LegacyManager.Instance?.GetEvolveRateBonus() ?? 0f;
            float currentRate = BlacksmithManager.Instance.GetSuccessRate(baseRate, legacy);

            if (currentRate >= 100f)
            {
                UIPopupController.Instance?.ShowToast(L("Blacksmith_AlreadyMaxRate"), type: PopupSfxType.Warning);
                return;
            }

            UIManager.Instance.GetOrInstantiatePanel<ForgeStoneSelectView>();
            UIControllerManager.Instance.GetController<ForgeStoneSelectController>()?.Open(RefreshForgeStoneSlot, baseRate, legacy);
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private void OnForgeStoneRemoveClicked()
        {
            ActiveItemManager.Instance.ClearBlacksmithItem();
            RefreshForgeStoneSlot();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
