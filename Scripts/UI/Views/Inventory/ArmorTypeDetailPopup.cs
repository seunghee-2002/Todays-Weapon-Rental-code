using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 방어구 타입 상세 팝업.
    /// 해당 ArmorType에 대한 각 WeaponType의 유불리를 표시한다.
    /// </summary>
    public class ArmorTypeDetailPopup : BaseView
    {
        [Header("헤더")]
        [SerializeField] private Image armorTypeIcon;
        [SerializeField] private Image armorTypeBG;
        [SerializeField] private Image armorTypeFrame;
        [SerializeField] private TextMeshProUGUI armorTypeNameText;

        [Header("무기 유불리 아이템")]
        [SerializeField] private WeaponTypeAdvantageItem[] advantageItems;

        [Header("버튼")]
        [SerializeField] private Button closeButton;

        private ArmorType currentArmorType;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen  = false;
            isCanClickOverlay = true;
            canEscape        = true;
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(ArmorType armorType)
        {
            currentArmorType = armorType;
            UpdateUI();
        }

        #endregion

        #region UI 업데이트 메서드

        private void UpdateUI()
        {
            if (armorTypeIcon != null)
                armorTypeIcon.sprite = IconManager.Instance.GetIconByArmorType(currentArmorType);

            if (armorTypeBG != null)
                armorTypeBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(GetGradeByArmorType());

            if (armorTypeFrame != null)
                armorTypeFrame.sprite = IconManager.Instance.GetFrameByGrade(GetGradeByArmorType());

            if (armorTypeNameText != null)
            {
                armorTypeNameText.text = UITranslator.GetString(currentArmorType);
                armorTypeNameText.color = ColorManager.Instance.GetGradeAccentColor(GetGradeByArmorType());
            }
                
            int armorIndex = (int)currentArmorType;
            int weaponCount = Enum.GetValues(typeof(WeaponType)).Length;

            for (int i = 0; i < weaponCount; i++)
            {
                if (advantageItems == null || i >= advantageItems.Length) break;
                if (advantageItems[i] == null) continue;

                float bonus = TypeAdvantage.weaponArmorBonus[i, armorIndex];
                advantageItems[i].Initialize((WeaponType)i, bonus);
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel<ArmorTypeDetailPopup>();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion

        // ArmorType에 따라 ui 요소의 색상이나 아이콘을 결정하기 위한 헬퍼 메서드
        private Grade GetGradeByArmorType()
        {
            return currentArmorType switch
            {
                ArmorType.Unarmored => Grade.Common,
                ArmorType.LightArmor => Grade.Uncommon,
                ArmorType.HeavyArmor => Grade.Epic,
                ArmorType.MagicalArmor => Grade.Rare,
                _ => Grade.Common,
            };
        }
    }
}
