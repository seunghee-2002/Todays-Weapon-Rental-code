using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 인벤토리 재료 리스트 아이템
    /// </summary>
    public class MaterialInventoryCardItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image materialIcon;
        [SerializeField] private Image BGImage;
        [SerializeField] private Image TypeFrame;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private Button cardButton;

        public event Action<MaterialInventoryCardItem> OnCardClicked;
        
        private MaterialData materialData;
        private int quantity;

        public MaterialData MaterialData => materialData;
        public int Quantity => quantity;

        private void Awake() {
            cardButton?.onClick.AddListener(OnClicked);
        }
        
        public void Initialize(MaterialData material, int count)
        {
            materialData = material;
            quantity = count;

            UpdateUI();
        }
        
        private void UpdateUI()
        {
            if (materialData == null) return;
            
            if (materialIcon != null && materialData.icon != null)
                materialIcon.sprite = materialData.icon;
            
            if (BGImage != null)
                BGImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(materialData.grade);

            if (quantityText != null)
                quantityText.text = $"x{quantity}";
            
            if  (TypeFrame != null)
                TypeFrame.sprite = IconManager.Instance.GetFrameByGrade(materialData.grade);
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
            if (cardButton != null)
                cardButton.onClick.RemoveAllListeners();
        }
    }
}