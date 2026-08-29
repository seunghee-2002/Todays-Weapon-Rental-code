using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 던전 선택 카드 UI 컴포넌트
    /// </summary>
    public class DungeonChoiceCardItem : MonoBehaviour
    {
        [Header("공통")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject selectedIndicator;

        [Header("컨텐츠")]
        [SerializeField] private Image armorTypeImage;
        [SerializeField] private GameObject doubleRewardIcon;
        [SerializeField] private Image seerIndicator;
        [SerializeField] private Image dungeonIcon;
        [SerializeField] private TextMeshProUGUI dungeonNameText;

        private DungeonData choice;
        private Action onSelected;

        public void Initialize(DungeonData dungeonChoice, Action onSelectedCallback, bool isDoubleReward, LuckLevel? luckLevel)
        {
            choice = dungeonChoice;
            onSelected = onSelectedCallback;

            UpdateUI();
            SetDoubleRewardIcon(isDoubleReward);
            SetSeerLuckLevel(luckLevel);

            selectButton?.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            selectButton?.onClick.RemoveAllListeners();
        }

        private void UpdateUI()
        {
            if (choice == null) return;
            cardBackground.color = ColorManager.Instance.GetGradeCardBackgroundColor(choice.grade);
            dungeonNameText.text = choice.DisplayName;
            dungeonIcon.sprite = choice.dungeonIcon;

            UpdateArmorTypeDisplay();
        }

        private void UpdateArmorTypeDisplay()
        {
            if (armorTypeImage == null) return;
            bool isKnown = ScoutManager.Instance.IsArmorTypeKnown(choice.StaticID);

            if (isKnown)
            {
                armorTypeImage.sprite = IconManager.Instance.GetIconByArmorType(
                    ScoutManager.Instance.GetKnownArmorType(choice.StaticID));
                armorTypeImage.color = ColorManager.Instance.GetGoldColor();
            }
            else
            {
                armorTypeImage.sprite = IconManager.Instance.GetUnknownArmorIcon();
                armorTypeImage.color = ColorManager.Instance.GetWhiteColor();
            }
        }

        public void SetSimulatedArmorType(ArmorType armorType)
        {
            if (armorTypeImage == null || choice == null) return;
            if (ScoutManager.Instance.IsArmorTypeKnown(choice.StaticID)) return;
            armorTypeImage.sprite = IconManager.Instance.GetIconByArmorType(armorType);
            armorTypeImage.color  = ColorManager.Instance.GetSkyBlueColor();
        }

        public void ClearSimulationDisplay()
        {
            if (choice == null) return;
            UpdateArmorTypeDisplay();
        }

        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null)
                selectedIndicator.SetActive(selected);
        }

        private void OnClicked()
        {
            onSelected?.Invoke();
        }

        public DungeonData GetChoice()
        {
            return choice;
        }

        /// <summary>튜토리얼 하이라이트용 — 방어타입 아이콘 RectTransform.</summary>
        public RectTransform GetArmorIconRect() => armorTypeImage?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트용 — '2배' 표시 아이콘 RectTransform(비활성이면 null).</summary>
        public RectTransform GetDoubleRewardIconRect()
            => (doubleRewardIcon != null && doubleRewardIcon.activeSelf) ? doubleRewardIcon.transform as RectTransform : null;

        private void SetDoubleRewardIcon(bool active)
        {
            if (doubleRewardIcon != null)
                doubleRewardIcon.SetActive(active);
        }

        /// <summary>
        /// 점술 결과에 따른 indicator 표시.
        /// null: 미상담 → 비활성
        /// 흉: 빨강 alpha 1.0
        /// 소길/중길/대길: 파랑 alpha 0.5/0.75/1.0
        /// </summary>
        public void SetSeerLuckLevel(LuckLevel? luckLevel)
        {
            if (seerIndicator == null) return;

            if (luckLevel == null)
            {
                seerIndicator.gameObject.SetActive(false);
                return;
            }

            seerIndicator.gameObject.SetActive(true);

            switch (luckLevel.Value)
            {
                case LuckLevel.흉:
                    seerIndicator.color = WithAlpha(ColorManager.Instance.GetRedColor(), 1.0f);
                    break;
                case LuckLevel.소길:
                    seerIndicator.color = WithAlpha(ColorManager.Instance.GetSkyBlueColor(), 0.5f);
                    break;
                case LuckLevel.중길:
                    seerIndicator.color = WithAlpha(ColorManager.Instance.GetSkyBlueColor(), 0.75f);
                    break;
                case LuckLevel.대길:
                    seerIndicator.color = WithAlpha(ColorManager.Instance.GetSkyBlueColor(), 1.0f);
                    break;
            }
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
