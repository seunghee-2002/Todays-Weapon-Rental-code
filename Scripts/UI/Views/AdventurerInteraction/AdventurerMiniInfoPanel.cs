using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// Tab 2, Tab 3 상단에 고정 표시되는 축소형 모험가 정보 컴포넌트.
    /// 모험가 외형, 이름, 스탯 슬라이더, 특성, 현재 무기 카드를 표시한다.
    /// </summary>
    public class AdventurerMiniInfoPanel : MonoBehaviour
    {
        [Header("Time")]
        [SerializeField] private TextMeshProUGUI timeText;

        [Header("Adventurer")]
        [SerializeField] private AdventurerAppearanceApplier appearanceApplier;
        [SerializeField] private GameObject namedIndicator;

        [Header("Stats")]
        [SerializeField] private Slider strSlider;
        [SerializeField] private Slider strBonusSlider;
        [SerializeField] private GameObject strLockIndicator;
        [SerializeField] private Slider dexSlider;
        [SerializeField] private Slider dexBonusSlider;
        [SerializeField] private GameObject dexLockIndicator;
        [SerializeField] private Slider intSlider;
        [SerializeField] private Slider intBonusSlider;
        [SerializeField] private GameObject intLockIndicator;
        [SerializeField] private Slider lukSlider;
        [SerializeField] private Slider lukBonusSlider;
        [SerializeField] private GameObject lukLockIndicator;
        [SerializeField] private int statDisplayMax = 100;

        [Header("Stat Affinity Arrows")]
        [SerializeField] private Transform strArrowContainer;
        [SerializeField] private Transform dexArrowContainer;
        [SerializeField] private Transform intArrowContainer;
        [SerializeField] private Transform lukArrowContainer;

        [Header("Trait")]
        [SerializeField] private GameObject traitNoneIndicator;
        [SerializeField] private GameObject traitPanel;
        [SerializeField] private Image traitIconImage;
        [SerializeField] private TextMeshProUGUI traitNameText;
        [SerializeField] private TextMeshProUGUI traitEffectText;

        [Header("Weapon")]
        [SerializeField] private GameObject weaponInfoPanel;
        [SerializeField] private Button weaponInfoButton;
        [SerializeField] private Image weaponCardBG;
        [SerializeField] private Image weaponCardFrame;
        [SerializeField] private Image weaponIcon;

        public event System.Action OnWeaponInfoClicked;

        [Header("Active Item")]
        [SerializeField] private Button activeItemButton;
        [SerializeField] private Image activeItemCardBG;
        [SerializeField] private Image activeItemCardFrame;
        [SerializeField] private Image activeItemIcon;
        [SerializeField] private GameObject activeItemIconRoot;
        [SerializeField] private Button emptyActiveItemButton;

        public event System.Action OnActiveItemInfoClicked;
        public event System.Action OnEmptyActiveItemClicked;

        [Header("Test Buttons")]
        [SerializeField] private Button strButton;
        [SerializeField] private Button dexButton;
        [SerializeField] private Button intButton;
        [SerializeField] private Button lukButton;
        [SerializeField] private Button traitButton;
        [SerializeField] private Button weaponHintButton;

        public event System.Action<AdventurerStat> OnStatClicked;
        public event System.Action OnTraitClicked;
        public event System.Action OnWeaponHintClicked;

        #region 초기화

        private void Awake()
        {
            weaponInfoButton?.onClick.AddListener(() => OnWeaponInfoClicked?.Invoke());
            activeItemButton?.onClick.AddListener(() => OnActiveItemInfoClicked?.Invoke());
            emptyActiveItemButton?.onClick.AddListener(() => OnEmptyActiveItemClicked?.Invoke());

            strButton?.onClick.AddListener(() => OnStatClicked?.Invoke(AdventurerStat.STR));
            dexButton?.onClick.AddListener(() => OnStatClicked?.Invoke(AdventurerStat.DEX));
            intButton?.onClick.AddListener(() => OnStatClicked?.Invoke(AdventurerStat.INT));
            lukButton?.onClick.AddListener(() => OnStatClicked?.Invoke(AdventurerStat.LUK));
            traitButton?.onClick.AddListener(() => OnTraitClicked?.Invoke());
            weaponHintButton?.onClick.AddListener(() => OnWeaponHintClicked?.Invoke());
        }

        private void OnDestroy()
        {
            weaponInfoButton?.onClick.RemoveAllListeners();
            activeItemButton?.onClick.RemoveAllListeners();
            emptyActiveItemButton?.onClick.RemoveAllListeners();

            strButton?.onClick.RemoveAllListeners();
            dexButton?.onClick.RemoveAllListeners();
            intButton?.onClick.RemoveAllListeners();
            lukButton?.onClick.RemoveAllListeners();
            traitButton?.onClick.RemoveAllListeners();
            weaponHintButton?.onClick.RemoveAllListeners();
        }

        public void SetAdventurer(AdventurerInstance adventurer)
        {
            if (adventurer == null) return;

            if (adventurer.appearance != null)
                appearanceApplier?.ApplyAppearance(adventurer.appearance);

            namedIndicator?.SetActive(adventurer.isNamed);

            RefreshStats(adventurer);
            RefreshTraitDisplay(adventurer);
        }

        #endregion

        #region 갱신 메서드

        public void UpdateTimeDisplay()
        {
            if (timeText != null)
                timeText.text = TimeManager.Instance.GetCurrentTimeString();
        }

        public void RefreshStats(AdventurerInstance adventurer, WeaponInstance weapon = null)
        {
            if (adventurer == null) return;

            int bonusStr = 0, bonusDex = 0, bonusInt = 0, bonusLuk = 0;

            if (weapon?.effects != null)
            {
                foreach (var effect in weapon.effects)
                {
                    if (effect.effectData.effectType == WeaponEffectType.StatBonus)
                    {
                        switch ((AdventurerStat)effect.effectData.targetStat)
                        {
                            case AdventurerStat.STR: bonusStr += Mathf.RoundToInt(effect.currentValue); break;
                            case AdventurerStat.DEX: bonusDex += Mathf.RoundToInt(effect.currentValue); break;
                            case AdventurerStat.INT: bonusInt += Mathf.RoundToInt(effect.currentValue); break;
                            case AdventurerStat.LUK: bonusLuk += Mathf.RoundToInt(effect.currentValue); break;
                        }
                    }
                    else if (effect.effectData.effectType == WeaponEffectType.AllStatBonus)
                    {
                        int flat = Mathf.RoundToInt(effect.currentValue);
                        bonusStr += flat; bonusDex += flat; bonusInt += flat; bonusLuk += flat;
                    }
                }
            }

            var visibility = InsightManager.Instance.GetStatVisibility(adventurer);

            SetStatBar(strSlider, strBonusSlider, strLockIndicator, strButton,
                AdventurerStat.STR, adventurer.STR, bonusStr, visibility);
            SetStatBar(dexSlider, dexBonusSlider, dexLockIndicator, dexButton,
                AdventurerStat.DEX, adventurer.DEX, bonusDex, visibility);
            SetStatBar(intSlider, intBonusSlider, intLockIndicator, intButton,
                AdventurerStat.INT, adventurer.INT, bonusInt, visibility);
            SetStatBar(lukSlider, lukBonusSlider, lukLockIndicator, lukButton,
                AdventurerStat.LUK, adventurer.LUK, bonusLuk, visibility);

            if (weaponHintButton != null)
                weaponHintButton.interactable = !adventurer.isWeaponTypeHinted && InsightManager.Instance.CanRevealWeaponTypeHint();
        }

        public void RefreshTraitDisplay(AdventurerInstance adventurer)
        {
            bool traitKnown = InsightManager.Instance.IsTraitKnown(adventurer);
            if (traitButton != null) traitButton.interactable = !traitKnown;

            if (traitPanel == null) return;

            if (traitKnown)
            {
                traitNoneIndicator?.SetActive(false);
                traitPanel.SetActive(true);
                if (traitIconImage != null)
                    traitIconImage.sprite = IconManager.Instance.GetIconByTraitType(adventurer.Trait);
                if (traitNameText != null)
                    traitNameText.text = UITranslator.GetString(adventurer.Trait);
                if (traitEffectText != null)
                    traitEffectText.text = UITranslator.GetTraitEffectString(adventurer.Trait);
            }
            else
            {
                traitNoneIndicator?.SetActive(true);
                traitPanel.SetActive(false);
            }
        }

        public void UpdateWeaponIcon(WeaponInstance weapon, bool revealWeaponType)
        {
            if (weaponInfoPanel == null) return;

            bool showCard = weapon != null && revealWeaponType;
            weaponInfoPanel.SetActive(showCard);

            RefreshAffinityArrows(showCard ? weapon : null);

            if (!showCard) return;

            if (weaponCardBG != null)
                weaponCardBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);

            if (weaponCardFrame != null)
                weaponCardFrame.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);

            if (weaponIcon != null)
                weaponIcon.sprite = weapon.weaponData?.icon;
        }

        public void UpdateActiveItemIcon(ActiveItemData data)
        {
            if (activeItemIconRoot == null) return;
            bool hasIcon = data?.icon != null;
            activeItemIconRoot.SetActive(hasIcon);
            if (!hasIcon) return;

            Grade grade = data.usageContext.ToGrade();

            if (activeItemCardBG != null)
                activeItemCardBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);

            if (activeItemCardFrame != null)
                activeItemCardFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);

            if (activeItemIcon != null)
                activeItemIcon.sprite = data.icon;
        }

        #endregion

        #region 튜토리얼 하이라이트용 접근자

        /// <summary>
        /// 튜토리얼 하이라이트용 — 힘(STR) 스탯 행(StatGroup) RectTransform.
        /// 화살표 컨테이너(ArrowGroup=strArrowContainer)는 화살표가 켜지는 순간 레이아웃그룹 배치가 다음 패스에
        /// 반영돼 위치가 어긋난다. 항상 배치돼 있는 부모 StatGroup을 대상으로 하고, 상위 레이아웃(StatInfo)을 즉시 리빌드한다.
        /// (계층: StatInfo/STR/StatGroup/ArrowGroup)
        /// </summary>
        public RectTransform GetStrStatGroupRect()
        {
            var statGroup = strArrowContainer?.parent as RectTransform;   // ArrowGroup -> StatGroup
            if (statGroup == null) return null;
            if (statGroup.parent?.parent is RectTransform statInfo)       // StatGroup -> STR -> StatInfo
                LayoutRebuilder.ForceRebuildLayoutImmediate(statInfo);
            return statGroup;
        }

        #endregion

        #region 내부 메서드

        private void SetStatBar(Slider slider, Slider bonusSlider, GameObject lockIndicator, Button testButton,
            AdventurerStat stat, int baseVal, int bonus, AdventurerStatVisibility visibility)
        {
            bool isVisible = visibility.IsVisible(stat);
            lockIndicator?.SetActive(!isVisible);
            if (testButton != null) testButton.interactable = !isVisible;

            if (isVisible)
            {
                if (slider != null) slider.value = Mathf.Clamp01((float)baseVal / statDisplayMax);
                if (bonusSlider != null)
                {
                    bonusSlider.gameObject.SetActive(bonus > 0);
                    if (bonus > 0)
                        bonusSlider.value = Mathf.Clamp01((float)(baseVal + bonus) / statDisplayMax);
                }
            }
            else
            {
                if (slider != null) slider.value = 0f;
                if (bonusSlider != null) bonusSlider.gameObject.SetActive(false);
            }
        }

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

        #endregion
    }
}
