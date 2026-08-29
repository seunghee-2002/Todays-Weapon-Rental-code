using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 스탯 테스트 확인 패널 — 풀링 대신 lazy-init 단일 인스턴스로 관리.
    /// </summary>
    public class PreparationStatTestConfirmPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statTestConfirmTitleText;
        [SerializeField] private TextMeshProUGUI statTestDescriptionText;
        [SerializeField] private TextMeshProUGUI statTestTimeText;
        [SerializeField] private TextMeshProUGUI statTestSuccessRateText;
        [SerializeField] private Toggle statTestPremiumToggle;
        [SerializeField] private Button statTestProceedButton;
        [SerializeField] private GameObject legacyIcon;
        [SerializeField] private TextMeshProUGUI statTestProceedButtonText;
        [SerializeField] private Button statTestCancelButton;

        private AdventurePreparationController controller;

        public void Initialize(AdventurePreparationController controller)
        {
            this.controller = controller;

            statTestPremiumToggle?.onValueChanged.RemoveAllListeners();
            statTestProceedButton?.onClick.RemoveAllListeners();
            statTestCancelButton?.onClick.RemoveAllListeners();

            statTestPremiumToggle?.onValueChanged.AddListener(on => this.controller?.OnStatTestPremiumToggled(on));
            statTestProceedButton?.onClick.AddListener(() => this.controller?.OnStatTestProceedClicked());
            statTestCancelButton?.onClick.AddListener(Hide);
        }

        public void Show(string title, int timeCost, float successRate, int legacyCost)
        {
            gameObject.SetActive(true);

            if (statTestConfirmTitleText != null)
                statTestConfirmTitleText.text = title;

            if (statTestPremiumToggle != null && statTestPremiumToggle.isOn)
                statTestPremiumToggle.isOn = false;
            else
                UpdateValues(false, title, timeCost, successRate, legacyCost);

            UIPopupController.Instance?.ShowPlayerResourceBar();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            UIPopupController.Instance?.HidePlayerResourceBar();
        }

        public void UpdateValues(bool isPremium, string title, int timeCost, float successRate, int legacyCost)
        {
            if (statTestTimeText != null)
                statTestTimeText.text = isPremium
                    ? L("Preparation_TestTimeInstant")
                    : L("Preparation_TestTimeCost", ("time", UITranslator.FormatDuration(timeCost)));
            if (statTestSuccessRateText != null)
                statTestSuccessRateText.text = isPremium
                    ? L("Preparation_TestSuccessRateFull")
                    : L("Preparation_TestSuccessRate", ("rate", $"{successRate * 100f:0.#}"));
            if (statTestDescriptionText != null)
            {
                string greenHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor());
                statTestDescriptionText.text = isPremium
                    ? L("Preparation_TestConfirmPremium", ("color", greenHex), ("title", title))
                    : L("Preparation_TestConfirm", ("title", title));
            }

            legacyIcon?.SetActive(isPremium);
            if (statTestProceedButtonText != null)
                statTestProceedButtonText.text = isPremium
                    ? legacyCost.ToString()
                    : L("Preparation_TestProceedButton");
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        /// <summary>튜토리얼 하이라이트용 — 진행 버튼.</summary>
        public RectTransform GetProceedButtonRect()
        {
            var rect = statTestProceedButton?.transform as RectTransform;
            // 무기 조사/종합 테스트마다 제목·설명 길이가 달라 레이아웃그룹 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }
    }
}
