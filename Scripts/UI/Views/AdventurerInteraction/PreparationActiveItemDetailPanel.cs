using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 아이템 상세 패널 — lazy-init 단일 인스턴스로 관리.
    /// </summary>
    public class PreparationActiveItemDetailPanel : MonoBehaviour
    {
        [SerializeField] private Image activeItemDetailIcon;
        [SerializeField] private Image activeItemDetailBG;
        [SerializeField] private Image activeItemDetailFrame;
        [SerializeField] private TextMeshProUGUI activeItemDetailNameText;
        [SerializeField] private TextMeshProUGUI activeItemDetailDescriptionText;
        [SerializeField] private TextMeshProUGUI activeItemDetailTypeText;
        [SerializeField] private Button assignItemButton;
        [SerializeField] private Button backActiveItemDetailButton;

        private AdventurePreparationController controller;

        public void Initialize(AdventurePreparationController controller)
        {
            this.controller = controller;

            assignItemButton?.onClick.RemoveAllListeners();
            backActiveItemDetailButton?.onClick.RemoveAllListeners();

            assignItemButton?.onClick.AddListener(() => this.controller?.OnAssignItemClicked());
            backActiveItemDetailButton?.onClick.AddListener(() => this.controller?.OnBackActiveItemDetailClicked());
        }

        public void Show(ActiveItemData data, bool isAssigned, bool isViewOnly = false)
        {
            if (data == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            Grade grade = data.usageContext.ToGrade();

            if (activeItemDetailIcon != null)
                activeItemDetailIcon.sprite = data.icon;
            if (activeItemDetailBG != null)
                activeItemDetailBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);
            if (activeItemDetailFrame != null)
                activeItemDetailFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);
            if (activeItemDetailNameText != null)
            {
                activeItemDetailNameText.text = data.DisplayName;
                activeItemDetailNameText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }
            if (activeItemDetailDescriptionText != null)
                activeItemDetailDescriptionText.text = data.DisplayDescription;
            if (activeItemDetailTypeText != null)
                activeItemDetailTypeText.text = UITranslator.GetString(data.itemType);

            assignItemButton?.gameObject.SetActive(!isAssigned && !isViewOnly);
            backActiveItemDetailButton?.gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>튜토리얼 하이라이트용 — 선물(배정) 버튼 RectTransform.</summary>
        public RectTransform GetAssignButtonRect()
        {
            var rect = assignItemButton?.transform as RectTransform;
            // 버튼이 LayoutGroup 안이라 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }
    }
}
