using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 방어구 타입별 무기 유불리 항목.
    /// ArmorTypeDetailPopup에서 WeaponType 수만큼 배치된다.
    /// </summary>
    public class WeaponTypeAdvantageItem : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject advantagePanel;
        [SerializeField] private GameObject disadvantagePanel;

        [Header("공통 요소")]
        [SerializeField] private Image weaponTypeIcon;
        [SerializeField] private TextMeshProUGUI valueText;

        #region 초기화

        public void Initialize(WeaponType weaponType, float bonus)
        {
            Sprite icon = IconManager.Instance.GetIconByWeaponType(weaponType);
            if (weaponTypeIcon != null)
                weaponTypeIcon.sprite = icon;

            bool isAdvantage    = bonus > 0f;

            if (advantagePanel != null)
                advantagePanel.SetActive(isAdvantage);
            if (disadvantagePanel != null)
                disadvantagePanel.SetActive(!isAdvantage);

            Color textColor = ColorManager.Instance.GetAdvantageTextColor(isAdvantage);
            Color iconColor = ColorManager.Instance.GetAdvantageIconColor(isAdvantage);

            if (valueText != null)
            {
                string sign = bonus > 0f ? "+" : "";
                valueText.text  = $"{sign}{bonus * 100f:F0}%";
                valueText.color = textColor;
            }

            if (weaponTypeIcon != null)
                weaponTypeIcon.color = iconColor;
        }

        #endregion
    }
}
