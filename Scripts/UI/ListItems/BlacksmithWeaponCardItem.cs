using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 대장장이 무기 카드 아이템
    /// </summary>
    public class BlacksmithWeaponCardItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image BGImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TextMeshProUGUI enforceLevelText;
        [SerializeField] private Button cardButton;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private GameObject checkIndicator;

        private WeaponInstance weapon;
        private Action<WeaponInstance> onSelected;

        public WeaponInstance Weapon => weapon;

        private void OnDestroy()
        {
            cardButton?.onClick.RemoveAllListeners();
        }
        
        public void Initialize(WeaponInstance weaponInstance, Action<WeaponInstance> onClickAction)
        {
            weapon = weaponInstance;
            onSelected = onClickAction;
            
            UpdateUI();

            cardButton?.onClick.RemoveAllListeners();
            cardButton?.onClick.AddListener(() => onSelected?.Invoke(weapon));
        }
        
        public void SetSelected(bool selected)
        {
            selectedIndicator?.SetActive(selected);
        }

        public void SetChecked(bool isChecked)
        {
            checkIndicator?.SetActive(isChecked);
        }

        public void UpdateUI()
        {
            if (weapon == null) return;
            
            // 무기 아이콘
            if (weaponIcon != null && weapon.weaponData.icon != null)
                weaponIcon.sprite = weapon.weaponData.icon;

            if (BGImage != null)
                BGImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);

            if (frameImage != null)
                frameImage.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            
            // 강화 레벨
            if (enforceLevelText != null)
            {
                enforceLevelText.text = $"+{weapon.enforceLevel}";
                enforceLevelText.gameObject.SetActive(weapon.enforceLevel > 0);
            }   
        }
    }
}