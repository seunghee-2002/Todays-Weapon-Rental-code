using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    public class MaterialSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image iconBG;
        [SerializeField] private Image iconFrame;
        [SerializeField] private TextMeshProUGUI countText;

        public void Initialize(MaterialData material)
        {
            if (iconImage != null) iconImage.sprite = material.icon;
            if (iconBG != null) iconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(material.grade);
            if (iconFrame != null) iconFrame.sprite = IconManager.Instance.GetFrameByGrade(material.grade);
            if (countText != null) countText.text = "x0";
        }

        public void SetCount(int count)
        {
            if (countText != null) countText.text = $"x{count}";
        }
    }
}
