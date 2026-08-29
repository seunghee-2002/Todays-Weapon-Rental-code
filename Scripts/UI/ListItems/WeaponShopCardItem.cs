using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상점의 개별 무기 카드 UI 컴포넌트
    /// </summary>
    public class WeaponShopCardItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image weaponTypeIcon;
        [SerializeField] private TextMeshProUGUI weaponNameText;        
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Button selectButton;

        private WeaponShopItemData item;
        private Action onSelected;
        
        public WeaponShopItemData Item => item;
        
        public void Initialize(WeaponShopItemData shopItem, Action onSelectedCallback)
        {
            item = shopItem;
            onSelected = onSelectedCallback;
            
            UpdateUI();

            selectButton?.onClick.AddListener(OnClicked);
        }
        
        private void OnDestroy()
        {
            selectButton?.onClick.RemoveListener(OnClicked);
        }
        
        private void UpdateUI()
        {
            if (item == null) return;
            
            // 무기 아이콘
            if (weaponIcon != null)
                weaponIcon.sprite = item.WeaponData.icon;
            
            // 무기 타입 아이콘
            if (weaponTypeIcon != null && IconManager.Instance != null)
                weaponTypeIcon.sprite = IconManager.Instance.GetIconByWeaponType(item.WeaponData.weaponType);
            
            // 무기 이름
            if (weaponNameText != null)
                weaponNameText.text = item.WeaponData.DisplayName;
            
            // 배경 색상 (등급별)
            if (backgroundImage != null)
                backgroundImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(item.WeaponData.baseGrade);

            if (borderImage != null && IconManager.Instance != null)
            {
                borderImage.sprite = IconManager.Instance.GetFrameByGrade(item.WeaponData.baseGrade);
            }
        }
        
        private void OnClicked()
        {
            onSelected?.Invoke();
        }
    }
}