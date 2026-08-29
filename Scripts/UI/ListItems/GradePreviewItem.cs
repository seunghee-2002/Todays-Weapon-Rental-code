// Scripts/UI/ListItems/GradePreviewItem.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 교환 결과 등급 확률 한 줄 표시용 아이템
    /// </summary>
    public class GradePreviewItem : MonoBehaviour
    {
        [SerializeField] private Image gradeColorImage;
        [SerializeField] private TextMeshProUGUI gradeNameText;
        [SerializeField] private TextMeshProUGUI probabilityText;

        public void Initialize(Grade grade, float probability)
        {
            if (gradeColorImage != null)
                gradeColorImage.color = ColorManager.Instance.GetGradeColor(grade);

            if (gradeNameText != null)
                gradeNameText.text = UITranslator.GetString(grade);

            if (probabilityText != null)
                probabilityText.text = $"{probability * 100f:F0}%";
        }
    }
}
