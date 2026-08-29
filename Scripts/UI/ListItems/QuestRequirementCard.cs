// Scripts/UI/Components/QuestRequirementCard.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 퀘스트 요구사항 카드 UI 컴포넌트
    /// </summary>
    public class QuestRequirementCard : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image icon;
        [SerializeField] private Image cardBG;
        [SerializeField] private TextMeshProUGUI requirementText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private GameObject completedIcon;

        public void Initialize(QuestRequirement requirement, int current, int target, bool isCompleted)
        {
            if (requirementText != null)
                requirementText.text = UITranslator.QuestRequirementText(requirement);

            if (icon != null)
                icon.sprite = IconManager.Instance?.GetIconByQuestType(requirement.questType);

            if (isCompleted)
            {
                if (cardBG != null)
                    cardBG.color = ColorManager.Instance.GetMissionCardColor(true);

                if (progressSlider != null)
                    progressSlider.gameObject.SetActive(false);

                if (progressText != null)
                    progressText.gameObject.SetActive(false);

                if (completedIcon != null)
                    completedIcon.SetActive(true);
            }
            else
            {
                if (cardBG != null)
                    cardBG.color = ColorManager.Instance.GetMissionCardColor(false);

                if (progressSlider != null)
                {
                    progressSlider.gameObject.SetActive(true);
                    progressSlider.minValue = 0;
                    progressSlider.maxValue = target > 0 ? target : 1;
                    progressSlider.value = Mathf.Clamp(current, 0, progressSlider.maxValue);
                }

                if (progressText != null)
                {
                    progressText.gameObject.SetActive(true);
                    progressText.text = $"{current}/{target}";
                }

                if (completedIcon != null)
                    completedIcon.SetActive(false);
            }
        }
    }
}
