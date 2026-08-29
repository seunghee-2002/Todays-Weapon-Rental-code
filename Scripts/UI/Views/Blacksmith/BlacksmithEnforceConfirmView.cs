using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 강화 확인 팝업 View (WeaponPanel에서 강화 버튼 클릭 시 열림)
    /// </summary>
    public class BlacksmithEnforceConfirmView : BaseView
    {
        [Header("Weapon Info")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private Image BGImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TextMeshProUGUI enforceLevelText;

        [Header("Effect List")]
        [SerializeField] private Transform effectListContainer;
        [SerializeField] private GameObject effectListItemPrefab;

        [Header("Cost Info")]
        [SerializeField] private TextMeshProUGUI goldCostText;

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
        [SerializeField] private Button enforceButton;
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
            enforceButton?.onClick.AddListener(OnEnforceClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
            forgeStoneSlotButton?.onClick.AddListener(OnForgeStoneSlotClicked);
            forgeStoneRemoveButton?.onClick.AddListener(OnForgeStoneRemoveClicked);
        }

        protected override void UnsubscribeEvents()
        {
            enforceButton?.onClick.RemoveAllListeners();
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

            // 튜토리얼 2-C: 강화 확인 화면이 열리면 강화 실행 버튼 하이라이트(강화석은 건너뜀)
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialEnforceConfirmOpened();
        }

        /// <summary>튜토리얼 하이라이트용 — 강화 실행 버튼 RectTransform.</summary>
        public RectTransform GetEnforceButtonRect()
        {
            var rect = enforceButton?.transform as RectTransform;
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

            if (weaponIcon != null && weapon.weaponData.icon != null)
                weaponIcon.sprite = weapon.weaponData.icon;
            if (weaponNameText != null)
            {
                weaponNameText.text = weapon.weaponData.DisplayName + $"+{weapon.enforceLevel}";
                weaponNameText.color = ColorManager.Instance.GetGradeAccentColor(weapon.currentGrade);
            }
            if (BGImage != null)
                BGImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
            if (frameImage != null)
                frameImage.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            if (enforceLevelText != null)
                enforceLevelText.text = $"+{weapon.enforceLevel}";

            UpdateEffectList(weapon);
            UpdateGoldCost(weapon);
            RefreshForgeStoneSlot();
            RefreshSuccessRate(weapon);
            UpdateEnforceButton(weapon);
        }

        private void UpdateEffectList(WeaponInstance weapon)
        {
            if (effectListContainer == null || effectListItemPrefab == null) return;

            foreach (Transform child in effectListContainer)
                Destroy(child.gameObject);

            foreach (var effect in weapon.effects)
            {
                var effectData = effect.effectData;
                float maxValue = effectData?.baseValueRange.y ?? effect.currentValue;

                var obj = Instantiate(effectListItemPrefab, effectListContainer);
                var item = obj.GetComponent<WeaponEffectListItem>();

                if (Mathf.Approximately(effect.currentValue, maxValue))
                    item?.Initialize(effect, false);
                else
                    item?.InitializeWithMax(effect, maxValue);
            }
        }

        private void UpdateGoldCost(WeaponInstance weapon)
        {
            if (goldCostText != null)
            {
                int baseGoldCost = BlacksmithManager.Instance.CalculateEnforceCost(weapon);
                int legacyCost = Mathf.RoundToInt(baseGoldCost * (LegacyManager.Instance?.GetEnforceCostMultiplier() ?? 1f));
                int goldCost = BlacksmithManager.Instance.ApplyCostReduction(legacyCost);
                bool canAffordGold = EconomyManager.Instance.CurrentGold >= goldCost;

                goldCostText.color = canAffordGold ? ColorManager.Instance.GetBlackColor() : ColorManager.Instance.GetRedColor();
                goldCostText.text = UITranslator.GetGoldCostString(legacyCost, goldCost);
            }
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
            float baseRate = ConfigManager.Instance.Blacksmith.enforceSuccessRates[weapon.enforceLevel];
            float legacy = LegacyManager.Instance?.GetEnforceRateBonus() ?? 0f;
            float finalRate = BlacksmithManager.Instance.GetSuccessRate(baseRate, legacy, out float rawBonus);

            // 튜토리얼은 ExecuteEnforce가 성공을 강제하므로 표시도 100%로 맞춘다 (보너스 표기는 숨긴다)
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

        private void UpdateEnforceButton(WeaponInstance weapon)
        {
            if (enforceButton == null) return;
            // 강화 가능 여부(무기 상태)는 매니저가 단일 책임. 골드는 EnsureGold가 보충하므로 제외.
            enforceButton.interactable = BlacksmithManager.Instance.CanAffordEnforce(weapon);
        }

        #endregion

        #region 버튼 이벤트

        private void OnEnforceClicked()
        {
            if (selectedWeapon == null) return;

            int baseGoldCost = BlacksmithManager.Instance.CalculateEnforceCost(selectedWeapon);
            int legacyCost = Mathf.RoundToInt(baseGoldCost * (LegacyManager.Instance?.GetEnforceCostMultiplier() ?? 1f));
            int goldCost = BlacksmithManager.Instance.ApplyCostReduction(legacyCost);

            EconomyManager.Instance.EnsureGold(goldCost, onReady: DoEnforce);
        }

        private void DoEnforce()
        {
            lastUsedForgeStoneData = ActiveItemManager.Instance.GetBlacksmithItem();
            controller?.OnEnforceWeapon(selectedWeapon);
            UIManager.Instance?.ClosePanel<BlacksmithEnforceConfirmView>();
        }

        private void OnCloseClicked()
        {
            ActiveItemManager.Instance?.ClearBlacksmithItem();
            lastUsedForgeStoneData = null;
            UIManager.Instance?.ClosePanel<BlacksmithEnforceConfirmView>();
        }

        private void OnForgeStoneSlotClicked()
        {
            if (selectedWeapon == null) return;

            float baseRate = ConfigManager.Instance.Blacksmith.enforceSuccessRates[selectedWeapon.enforceLevel];
            float legacy = LegacyManager.Instance?.GetEnforceRateBonus() ?? 0f;
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
