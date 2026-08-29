using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 인벤토리 무기 카드 아이템
    /// </summary>
    public class WeaponInventoryCardItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image BGImage;
        [SerializeField] private Image gradeFrame;
        [SerializeField] private TextMeshProUGUI enforceLevelText;
        [SerializeField] private GameObject rentedIndicator;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private Button cardButton;

        public event Action<WeaponInventoryCardItem> OnCardClicked;
        
        private WeaponInstance weapon;
        public WeaponInstance Weapon => weapon;
        
        private void Awake()
        {
            if (cardButton != null)
                cardButton.onClick.AddListener(OnClicked);
        }
        
        private void OnDestroy()
        {
            if (cardButton != null)
                cardButton.onClick.RemoveListener(OnClicked);
        }
        
        public void Initialize(WeaponInstance weaponInstance)
        {
            weapon = weaponInstance;
            UpdateUI();
        }
        
        private void UpdateUI()
        {
            if (weapon == null) return;
            
            // 무기 아이콘
            if (weaponIcon != null && weapon.weaponData.icon != null)
                weaponIcon.sprite = weapon.weaponData.icon;

            if (BGImage != null)
                BGImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
            
            // 등급
            if (gradeFrame != null)
                gradeFrame.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            
            // 강화 레벨
            if (enforceLevelText != null)
            {
                enforceLevelText.text = $"+{weapon.enforceLevel}";
                enforceLevelText.gameObject.SetActive(weapon.enforceLevel > 0);
            }
            
            // 대여 중 뱃지
            if (rentedIndicator != null)
                rentedIndicator.SetActive(weapon.isRented);
        }
        
        public void SetSelected(bool selected)
        {
            selectedIndicator?.SetActive(selected);
        }

        private void OnClicked()
        {
            OnCardClicked?.Invoke(this);
        }
    }
}