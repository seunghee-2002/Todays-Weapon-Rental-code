using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 결과창에서 재료를 구매할 때 사용하는 팝업.
    /// MaterialDetailPopup 구조 + 구매 버튼 + 가격 텍스트.
    /// </summary>
    public class MaterialPurchasePopup : BaseView
    {
        [Header("UI Elements")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image materialIcon;
        [SerializeField] private TextMeshProUGUI materialNameQuantityText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI dungeonListText;
        [SerializeField] private Button closeButton;

        [Header("Purchase Elements")]
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TextMeshProUGUI purchaseButtonPriceText;

        private MaterialData currentMaterial;
        private int currentQuantity;
        private int currentTotalPrice;
        private Action onConfirmCallback;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;
            isCanClickOverlay = true;
            canEscape = true;
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
            closeButton?.onClick.AddListener(OnCloseClicked);
            purchaseButton?.onClick.AddListener(OnPurchaseClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            purchaseButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(MaterialData material, int quantity, int totalPrice, Action onConfirm)
        {
            currentMaterial = material;
            currentQuantity = quantity;
            currentTotalPrice = totalPrice;
            onConfirmCallback = onConfirm;

            UpdateUI();
            RefreshAffordability();

            TutorialManager.Instance?.OnTutorialMaterialPopupOpened();   // 9-B 훅(가드는 TutorialManager 내부)
        }

        /// <summary>튜토리얼 하이라이트용(9-B) - 구매 버튼 RectTransform.</summary>
        public RectTransform GetPurchaseButtonRect() => purchaseButton != null ? purchaseButton.transform as RectTransform : null;

        #endregion

        #region UI 업데이트 메서드

        private void UpdateUI()
        {
            if (currentMaterial == null) return;

            if (backgroundImage != null)
                backgroundImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(currentMaterial.grade);

            if (frameImage != null)
                frameImage.sprite = IconManager.Instance.GetFrameByGrade(currentMaterial.grade);

            if (materialIcon != null && currentMaterial.icon != null)
                materialIcon.sprite = currentMaterial.icon;

            if (materialNameQuantityText != null)
            {
                materialNameQuantityText.color = ColorManager.Instance.GetGradeAccentColor(currentMaterial.grade);
                materialNameQuantityText.text = $"{currentMaterial.DisplayName} x{currentQuantity}";
            }

            if (typeText != null)
                typeText.text = UITranslator.GetString(currentMaterial.materialType);

            if (dungeonListText != null)
                dungeonListText.text = BuildDungeonText();

            if (purchaseButtonPriceText != null)
                purchaseButtonPriceText.text = $"{currentTotalPrice:N0}";
        }

        private string BuildDungeonText()
        {
            if (currentMaterial.materialType == MaterialType.Craft)
            {
                List<DungeonData> dropSources = DataManager.Instance.GetDropSourcesForMaterial(currentMaterial.StaticID);
                if (dropSources != null && dropSources.Count > 0)
                {
                    List<string> dungeonNames = new List<string>();
                    foreach (var dungeon in dropSources)
                    {
                        if (dungeon != null)
                            dungeonNames.Add(dungeon.DisplayName);
                    }
                    return L("Material_DungeonListHeader") + " " + string.Join(", ", dungeonNames);
                }
                return L("Material_DungeonNone");
            }
            else if (currentMaterial.materialType == MaterialType.Enforce)
            {
                return currentMaterial.StaticID switch
                {
                    "MAT_ENF_001" => L("Material_DungeonAllCommon"),
                    "MAT_ENF_002" => L("Material_DungeonAllUncommon"),
                    "MAT_ENF_003" => L("Material_DungeonAllRare"),
                    "MAT_ENF_004" => L("Material_DungeonAllEpic"),
                    "MAT_ENF_005" => L("Material_DungeonAllLegendary"),
                    _ => L("Material_DungeonNone")
                };
            }
            else
            {
                var src = DataManager.Instance.GetSpecialDropSourceForMaterial(currentMaterial.StaticID);
                return src != null ? src.DisplayName : L("Material_DungeonNone");
            }
        }

        private void RefreshAffordability()
        {
            if (purchaseButton == null) return;

            // 골드가 부족해도 유산으로 결제(EnsureGold)할 수 있으므로 버튼은 항상 활성. 부족 시 가격만 빨간색 안내.
            purchaseButton.interactable = true;

            if (purchaseButtonPriceText != null)
            {
                bool hasGold = EconomyManager.Instance != null
                    && EconomyManager.Instance.HasEnoughGold(currentTotalPrice);
                purchaseButtonPriceText.color = hasGold ? ColorManager.Instance.GetBlackColor() : ColorManager.Instance.GetRedColor();
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnPurchaseClicked()
        {
            onConfirmCallback?.Invoke();
            UIManager.Instance.ClosePanel<MaterialPurchasePopup>();
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

        private void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel<MaterialPurchasePopup>();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
