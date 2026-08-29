using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;

namespace TodaysWeaponRental
{
    public class AdventureResultItemTooltip : AdventureResultItem
    {
        [SerializeField] private GameObject tooltipObject;
        [SerializeField] private TextMeshProUGUI descText;

        #region 초기화

        public void InitializeReputation(int amount)
        {
            resultItemType = ResultItemType.Reputation;
            ApplyVisualByType();
            if (itemIcon != null) itemIcon.sprite = IconManager.Instance.GetReputationItemIcon();
            SetCountTextSigned(amount);
            SetupTooltip(resultItemType, amount);
            itemFocus?.SetActive(false);
            PrepareForAppear();
        }

        public void InitializeAffection(int amount)
        {
            resultItemType = ResultItemType.Affection;
            ApplyVisualByType();
            if (itemIcon != null) itemIcon.sprite = IconManager.Instance.GetAffectionItemIcon();
            SetCountTextSigned(amount);
            SetupTooltip(resultItemType, amount);
            itemFocus?.SetActive(false);
            PrepareForAppear();
        }

        public void InitializeInsight(int amount)
        {
            resultItemType = ResultItemType.Insight;
            ApplyVisualByType();
            if (itemIcon != null) itemIcon.sprite = IconManager.Instance.GetInsightItemIcon();
            SetCountTextSigned(amount);
            SetupTooltip(resultItemType, amount);
            itemFocus?.SetActive(false);
            PrepareForAppear();
        }

        #endregion

        #region 내부 메서드

        protected override void OnInteractClicked()
        {
            if (tooltipObject != null)
                tooltipObject.SetActive(!tooltipObject.activeSelf);
        }

        private void SetupTooltip(ResultItemType type, int amount)
        {
            string key = type switch
            {
                ResultItemType.Reputation => "ResultItemType_Reputation",
                ResultItemType.Affection => "ResultItemType_Affection",
                ResultItemType.Insight => "ResultItemType_Insight",
                _ => null
            };
            string desc = key != null
                ? LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", key)
                : "";
            string sign = amount >= 0 ? "+" : "";

            if (descText != null) descText.text = $"{desc} {sign}{amount}";
            if (tooltipObject != null) tooltipObject.SetActive(false);
            SetInteractButtonActive(true);
            if (interactButton != null)
            {
                interactButton.onClick.RemoveAllListeners();
                interactButton.onClick.AddListener(OnInteractClicked);
            }
        }

        #endregion
    }
}
