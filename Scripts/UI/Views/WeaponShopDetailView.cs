using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상점 상세 패널 View.
    /// WeaponGrid에서 카드 클릭 시 별도 패널로 열리며, 구매 또는 취소 시 닫힌다.
    /// 부가효과 범위 목록은 첫 열기 시 한 번만 생성하고 이후 텍스트만 갱신한다.
    /// </summary>
    public class WeaponShopDetailView : BaseView
    {
        [Header("무기 정보")]
        [SerializeField] private Image weaponImage;
        [SerializeField] private Image weaponBG;
        [SerializeField] private Image weaponFrameBorder;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private TextMeshProUGUI weaponTypeText;
        [SerializeField] private TextMeshProUGUI weaponPriceText;
        [SerializeField] private TextMeshProUGUI weaponEffectCountText;

        [Header("버튼")]
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TextMeshProUGUI purchaseButtonText;
        [SerializeField] private GameObject costGroup;
        [SerializeField] private Button cancelButton;

        [Header("부가효과 범위 목록")]
        [SerializeField] private ScrollRect effectListScrollRect;
        [SerializeField] private Transform effectListContainer;
        [SerializeField] private WeaponShopEffectRangeItem effectRangeItemPrefab;

        [Header("Controller")]
        [SerializeField] private WeaponShopDetailController detailController;

        // 무기 등급별 효과 등급 범위 - 하드코딩 대신 WeaponConfig 확률 배열에서
        // 확률 > 0인 최소/최대 등급을 계산한다. 설정 변경이 표시에 즉시 반영된다
        private static (Grade min, Grade max) GetEffectGradeRange(Grade weaponGrade)
        {
            float[] prob = ConfigManager.Instance.Weapon.GetEffectGradeProbabilities(weaponGrade);
            int min = -1, max = -1;
            for (int i = 0; i < prob.Length; i++)
            {
                if (prob[i] > 0f)
                {
                    if (min < 0) min = i;
                    max = i;
                }
            }
            return min < 0 ? (weaponGrade, weaponGrade) : ((Grade)min, (Grade)max);
        }

        private List<WeaponShopEffectRangeItem> cachedRangeItems = new List<WeaponShopEffectRangeItem>();
        private List<WeaponEffectData> effectGroups = new List<WeaponEffectData>();
        private bool isEffectListBuilt = false;

        private WeaponShopDetailController Controller => detailController;

        public void SetController(WeaponShopDetailController controller)
        {
            detailController = controller;
        }

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen    = false;
            isCanClickOverlay  = true;
            canEscape          = true;
        }

        protected override void SubscribeEvents()
        {
            purchaseButton?.onClick.AddListener(OnPurchaseClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);
        }

        protected override void UnsubscribeEvents()
        {
            purchaseButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region UI 업데이트

        public void Initialize(WeaponShopItemData item)
        {
            if (item == null) return;

            UpdateWeaponInfo(item);
            UpdatePurchaseButton(item);
            BuildEffectListIfNeeded();
            UpdateEffectValues(item.WeaponData.baseGrade);
        }

        private void UpdateWeaponInfo(WeaponShopItemData item)
        {
            if (weaponImage != null)
                weaponImage.sprite = item.WeaponData.icon;

            if (weaponBG != null)  
                weaponBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(item.WeaponData.baseGrade);

            if (weaponNameText != null)
            {
                string hexColor = GetGradeColor(item.WeaponData.baseGrade);
                weaponNameText.text = string.IsNullOrEmpty(hexColor)
                    ? item.WeaponData.DisplayName
                    : $"<color={hexColor}>{item.WeaponData.DisplayName}</color>";
            }

            if (weaponFrameBorder != null)
                weaponFrameBorder.sprite = IconManager.Instance.GetFrameByGrade(item.WeaponData.baseGrade);

            if (weaponTypeText != null)
                weaponTypeText.text = UITranslator.GetString(item.WeaponData.weaponType);

            if (weaponPriceText != null)
            {
                weaponPriceText.text = item.Price.ToString("N0");
                weaponPriceText.color = EconomyManager.Instance.HasEnoughGold(item.Price)
                    ? ColorManager.Instance.GetBlackColor()
                    : ColorManager.Instance.GetRedColor();
            }

            if (weaponEffectCountText != null)
            {
                // 실제 생성 규칙(WeaponConfig 등급별 개수)과 동일한 값을 표시
                int effectCount = ConfigManager.Instance.Weapon.GetEffectCount(item.WeaponData.baseGrade);
                weaponEffectCountText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "WeaponShop_EffectCount",
                    arguments: new object[] { new Dictionary<string, object> { { "count", effectCount } } });
            }
        }

        public void UpdatePurchaseButton(WeaponShopItemData item)
        {
            if (purchaseButton == null) return;

            bool canPurchase = item != null && WeaponShopManager.Instance.CanPurchase(item);
            purchaseButton.interactable = canPurchase;

            if (purchaseButtonText != null)
                purchaseButtonText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", canPurchase ? "WeaponShop_PurchaseButton" : "WeaponShop_BagFull");

            // 가방이 꽉 차 구매할 수 없으면 가격 표시를 숨긴다
            if (costGroup != null)
                costGroup.SetActive(canPurchase);
        }

        private void BuildEffectListIfNeeded()
        {
            if (isEffectListBuilt) return;
            if (effectListContainer == null || effectRangeItemPrefab == null) return;
            if (DataManager.Instance == null) return;

            effectGroups = DataManager.Instance.GetEffectGroups();

            foreach (var groupData in effectGroups)
            {
                var item = Instantiate(effectRangeItemPrefab, effectListContainer);
                item.Initialize(groupData);
                cachedRangeItems.Add(item);
            }

            effectListScrollRect.ResetPosition(this);
            isEffectListBuilt = true;
        }

        private void UpdateEffectValues(Grade weaponGrade)
        {
            var range = GetEffectGradeRange(weaponGrade);

            for (int i = 0; i < cachedRangeItems.Count && i < effectGroups.Count; i++)
            {
                var group = effectGroups[i];
                var minData = DataManager.Instance.GetEffectByTypeAndGrade(
                    group.effectType, range.min,
                    group.targetStat, group.targetGrade, group.targetArmorType);
                var maxData = DataManager.Instance.GetEffectByTypeAndGrade(
                    group.effectType, range.max,
                    group.targetStat, group.targetGrade, group.targetArmorType);

                cachedRangeItems[i].UpdateValue(minData, maxData);
            }
        }

        private string GetGradeColor(Grade grade)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGradeAccentColor(grade));
        }

        #endregion

        #region 이벤트 핸들러

        private void OnPurchaseClicked()
        {
            Controller?.OnPurchaseWeapon();
        }

        private void OnCancelClicked()
        {
            Controller?.OnCancel();
        }

        public override void OnEscapeClicked()
        {
            Controller?.OnCancel();
        }

        #endregion
    }

}
