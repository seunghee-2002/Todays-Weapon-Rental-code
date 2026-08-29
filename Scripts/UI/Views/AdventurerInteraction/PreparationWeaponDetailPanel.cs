using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상세 패널 — lazy-init 단일 인스턴스로 관리.
    /// </summary>
    public class PreparationWeaponDetailPanel : MonoBehaviour
    {
        [SerializeField] private Image weaponDetailIcon;
        [SerializeField] private Image weaponDetailIconBG;
        [SerializeField] private Image weaponDetailFrame;
        [SerializeField] private TextMeshProUGUI weaponDetailNameText;
        [SerializeField] private TextMeshProUGUI weaponDetailTypeText;
        [SerializeField] private TextMeshProUGUI weaponDetailEnforceLevelText;
        [SerializeField] private Transform weaponDetailEffectListContainer;
        [SerializeField] private WeaponEffectListItem weaponDetailEffectItemPrefab;

        [Header("Stat Affinity Arrows")]
        [SerializeField] private Transform strArrowContainer;
        [SerializeField] private Transform dexArrowContainer;
        [SerializeField] private Transform intArrowContainer;
        [SerializeField] private Transform lukArrowContainer;

        [Header("Buttons")]
        [SerializeField] private Button rentWeaponButton;
        [SerializeField] private TextMeshProUGUI rentWeaponButtonText;
        [SerializeField] private Button cancelWeaponRentButton;
        [SerializeField] private Button backWeaponRentButton;

        private AdventurePreparationController controller;
        private WeaponInstance pendingWeapon;

        public void Initialize(AdventurePreparationController controller)
        {
            this.controller = controller;

            rentWeaponButton?.onClick.RemoveAllListeners();
            cancelWeaponRentButton?.onClick.RemoveAllListeners();
            backWeaponRentButton?.onClick.RemoveAllListeners();

            rentWeaponButton?.onClick.AddListener(() => this.controller?.OnRentWeaponClicked());
            cancelWeaponRentButton?.onClick.AddListener(() => this.controller?.OnCancelWeaponRentClicked());
            backWeaponRentButton?.onClick.AddListener(() => this.controller?.OnBackWeaponRentClicked());
        }

        public void Show(WeaponInstance weapon, bool isConfirmedWeapon, WeaponInstance defaultWeapon)
        {
            if (weapon == null)
            {
                Hide();
                return;
            }

            pendingWeapon = weapon;
            gameObject.SetActive(true);

            if (weaponDetailIconBG != null)
                weaponDetailIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
            if (weaponDetailFrame != null)
                weaponDetailFrame.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            if (weaponDetailIcon != null && weapon.weaponData.icon != null)
                weaponDetailIcon.sprite = weapon.weaponData.icon;
            if (weaponDetailNameText != null)
            {
                string enforcePrefix = weapon.enforceLevel > 0 ? $"+{weapon.enforceLevel} " : "";
                weaponDetailNameText.text = weapon.weaponData.DisplayName + enforcePrefix;
                weaponDetailNameText.color = ColorManager.Instance.GetGradeAccentColor(weapon.currentGrade);
            }
            if (weaponDetailEnforceLevelText != null)
                weaponDetailEnforceLevelText.text = $"+{weapon.enforceLevel}";
            if (weaponDetailTypeText != null)
                weaponDetailTypeText.text = UITranslator.GetString(weapon.weaponData.weaponType);

            RefreshEffectList(weapon);
            RefreshAffinityArrows(weapon);

            bool isCurrentlySelected = isConfirmedWeapon;
            bool isDefault = weapon == defaultWeapon;

            if (rentWeaponButton != null)
            {
                rentWeaponButton.gameObject.SetActive(!isCurrentlySelected);
                if (rentWeaponButtonText != null)
                    rentWeaponButtonText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                        "UI_Screens", isDefault ? "Preparation_DefaultWeaponButton" : "Preparation_RentWeaponButton");
            }
            if (cancelWeaponRentButton != null)
                cancelWeaponRentButton.gameObject.SetActive(isCurrentlySelected);
            backWeaponRentButton?.gameObject.SetActive(true);
        }

        public void Hide()
        {
            pendingWeapon = null;
            gameObject.SetActive(false);
        }

        /// <summary>튜토리얼 하이라이트용 — 대여 버튼 RectTransform.</summary>
        public RectTransform GetRentButtonRect()
        {
            var rect = rentWeaponButton?.transform as RectTransform;
            // 버튼이 LayoutGroup 안이라 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }

        public void RefreshButtons(bool isConfirmedWeapon, WeaponInstance defaultWeapon)
        {
            if (pendingWeapon == null) return;
            Show(pendingWeapon, isConfirmedWeapon, defaultWeapon);
        }

        private void RefreshEffectList(WeaponInstance weapon)
            => WeaponEffectListItem.Rebuild(weaponDetailEffectListContainer, weaponDetailEffectItemPrefab, weapon.effects);

        private void RefreshAffinityArrows(WeaponInstance weapon)
        {
            if (weapon?.weaponData == null)
            {
                SetArrowCount(strArrowContainer, 0);
                SetArrowCount(dexArrowContainer, 0);
                SetArrowCount(intArrowContainer, 0);
                SetArrowCount(lukArrowContainer, 0);
                return;
            }

            int wi = (int)weapon.weaponData.weaponType;
            SetArrowCount(strArrowContainer, MultiplierToArrowCount(TypeAdvantage.weaponStatMultipliers[wi, 0]));
            SetArrowCount(dexArrowContainer, MultiplierToArrowCount(TypeAdvantage.weaponStatMultipliers[wi, 1]));
            SetArrowCount(intArrowContainer, MultiplierToArrowCount(TypeAdvantage.weaponStatMultipliers[wi, 2]));
            SetArrowCount(lukArrowContainer, MultiplierToArrowCount(TypeAdvantage.weaponStatMultipliers[wi, 3]));
        }

        private void SetArrowCount(Transform container, int count)
        {
            if (container == null) return;
            container.gameObject.SetActive(count > 0);
            for (int i = 0; i < container.childCount; i++)
                container.GetChild(i).gameObject.SetActive(i < count);
        }

        private static int MultiplierToArrowCount(float mul)
        {
            if (mul >= 1.5f) return 4;
            if (mul >= 1.0f) return 3;
            if (mul >= 0.5f) return 2;
            if (mul >= 0.3f) return 1;
            return 0;
        }
    }
}
