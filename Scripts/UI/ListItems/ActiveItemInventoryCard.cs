using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 보기 전용 액티브 아이템 표시 카드
    /// </summary>
    public class ActiveItemInventoryCard : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private Image itemBG;
        [SerializeField] private Image itemFrame;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private Button cardButton;

        public event Action<ActiveItemInventoryCard> OnCardClicked;

        private ActiveItemData data;
        public ActiveItemData Item => data;

        private void Awake()
        {
            cardButton?.onClick.AddListener(OnClicked);
        }

        public void Initialize(ActiveItemInstance activeItem)
        {
            data = activeItem.activeItemData;
            UpdateUI(activeItem.quantity);
        }

        private void UpdateUI(int quantity)
        {
            if (data == null) return;

            Grade grade = data.usageContext.ToGrade();

            if (itemIcon != null && data.icon != null)
                itemIcon.sprite = data.icon;

            if (itemBG != null)
                itemBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);

            if (itemFrame != null)
                itemFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);

            if (quantityText != null)
                quantityText.text = $"x{quantity}";
        }

        public void SetSelected(bool selected)
        {
            selectedIndicator?.SetActive(selected);
        }

        private void OnClicked()
        {
            OnCardClicked?.Invoke(this);
        }

        private void OnDestroy()
        {
            cardButton?.onClick.RemoveAllListeners();
        }
    }
}
