using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    public class LegacyUpgradeItem : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Slider levelSlider;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject legacyIcon;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private TextMeshProUGUI lockedText;

        private UpgradeKey upgradeKey;
        private Action<UpgradeKey> onBuyClicked;

        private void Awake()
        {
            buyButton?.onClick.AddListener(() => onBuyClicked?.Invoke(upgradeKey));
        }

        public void Setup(UpgradeKey key, Sprite icon, int level, int maxLevel,
                          int cost, bool canPurchase, bool isLocked, string prerequisiteDisplayName,
                          string description, string upgradeStepDescription, Action<UpgradeKey> onBuy)
        {
            upgradeKey   = key;
            onBuyClicked = onBuy;

            if (iconImage != null)
                iconImage.sprite = icon;

            if (levelText != null)
                levelText.text = $"{level}/{maxLevel}";

            if (levelSlider != null)
            {
                levelSlider.minValue = 0;
                levelSlider.maxValue = maxLevel;
                levelSlider.value    = level;
            }

            bool isMax = level >= maxLevel;

            if (descText != null)
            {
                if (isMax || string.IsNullOrEmpty(upgradeStepDescription))
                    descText.text = description;
                else
                {
                    string greenHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor());
                    descText.text = $"{description} <color={greenHex}>({upgradeStepDescription})</color>";
                }
            }

            if (isMax)
            {
                costText.text          = LocalizationSettings.StringDatabase.GetLocalizedString(
                                             "UI_Screens", "LegacyUpgrade_MaxLabel");
                buyButton.interactable = false;
                legacyIcon?.SetActive(false);
            }
            else
            {
                costText.text          = cost.ToString("N0");
                buyButton.interactable = !isLocked && canPurchase;
                legacyIcon?.SetActive(true);
            }

            bool showLocked = isLocked && !isMax;
            lockedOverlay?.SetActive(showLocked);
            if (lockedText != null)
            {
                lockedText.text = showLocked
                    ? LocalizationSettings.StringDatabase.GetLocalizedString(
                          "UI_Screens", "LegacyUpgrade_UnlockCondition",
                          arguments: new object[] { new Dictionary<string, object> { { "name", prerequisiteDisplayName } } })
                    : "";
            }
        }
    }
}
