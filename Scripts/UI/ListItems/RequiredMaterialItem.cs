using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    public class RequiredMaterialItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private GameObject saleIcon;
        [SerializeField] private Button button;

        /// <summary>재료 항목을 표시한다. 보유량이 필요 개수를 충족하면 true.</summary>
        public bool Initialize(MaterialData material, int requiredCount, bool isDiscounted = false, Action<MaterialData, int> onClickCallback = null)
        {
            if (material == null) return false;

            if (icon != null)
                icon.sprite = material.icon;

            if (nameText != null)
            {
                nameText.text = material.DisplayName;
                nameText.color = isDiscounted ? ColorManager.Instance.GetGoldColor() : ColorManager.Instance.GetWhiteColor();
            }

            int owned = InventoryManager.Instance.GetMaterialCount(material.StaticID);
            bool isSatisfied = owned >= requiredCount;

            if (countText != null)
            {
                countText.text = $"{owned}/{requiredCount}";
                countText.color = isSatisfied ? ColorManager.Instance.GetGreenColor() : ColorManager.Instance.GetRedColor();
            }

            saleIcon?.SetActive(isDiscounted);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClickCallback != null)
                    button.onClick.AddListener(() => onClickCallback(material, requiredCount));
            }

            return isSatisfied;
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveAllListeners();
        }
    }
}
