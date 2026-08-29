using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 준비 Tab 1 — 탐색(정보 + 대화).
    /// AdventurePreparationView 하위 패널.
    /// </summary>
    public class PreparationTab1Panel : MonoBehaviour
    {
        [Header("시간 & 외형")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private AdventurerAppearanceApplier adventurerAppearanceApplier;
        [SerializeField] private TextMeshProUGUI adventurerNameText;
        [SerializeField] private TextMeshProUGUI adventurerGradeText;

        [Header("스탯 (Slider)")]
        [SerializeField] private Slider strSlider;
        [SerializeField] private Slider strBonusSlider;
        [SerializeField] private TextMeshProUGUI strValueText;
        [SerializeField] private GameObject strLockIndicator;
        [SerializeField] private Slider dexSlider;
        [SerializeField] private Slider dexBonusSlider;
        [SerializeField] private TextMeshProUGUI dexValueText;
        [SerializeField] private GameObject dexLockIndicator;
        [SerializeField] private Slider intSlider;
        [SerializeField] private Slider intBonusSlider;
        [SerializeField] private TextMeshProUGUI intValueText;
        [SerializeField] private GameObject intLockIndicator;
        [SerializeField] private Slider lukSlider;
        [SerializeField] private Slider lukBonusSlider;
        [SerializeField] private TextMeshProUGUI lukValueText;
        [SerializeField] private GameObject lukLockIndicator;
        [SerializeField] private TextMeshProUGUI avgStatText;
        [SerializeField] private int statDisplayMax = 100;

        [Header("무기 정보 (카드형)")]
        [SerializeField] private GameObject tab1WeaponInfoPanel;
        [SerializeField] private Image tab1WeaponCardBG;
        [SerializeField] private Image tab1WeaponCardFrame;
        [SerializeField] private Image tab1WeaponIcon;
        [SerializeField] private Button tab1WeaponButton;

        [Header("액티브아이템 (카드형)")]
        [SerializeField] private GameObject tab1ActiveItemPanel;
        [SerializeField] private Image tab1ActiveItemCardBG;
        [SerializeField] private Image tab1ActiveItemCardFrame;
        [SerializeField] private Image tab1ActiveItemIcon;
        [SerializeField] private Button tab1ActiveItemButton;

        [Header("특성 (직접 표시)")]
        [SerializeField] private GameObject traitNoneIndicator;
        [SerializeField] private GameObject traitPanel;
        [SerializeField] private Image traitIconImage;
        [SerializeField] private TextMeshProUGUI traitNameText;
        [SerializeField] private TextMeshProUGUI traitEffectText;

        [Header("대화 버튼 — 스탯")]
        [SerializeField] private Button strRevealButton;
        [SerializeField] private Button dexRevealButton;
        [SerializeField] private Button intRevealButton;
        [SerializeField] private Button lukRevealButton;
        [SerializeField] private Button allStatsRevealButton;
        [SerializeField] private Button traitRevealButton;
        [SerializeField] private Button weaponHintButton;

        private AdventurePreparationController controller;

        #region 초기화

        public void Initialize(AdventurePreparationController controller)
        {
            this.controller = controller;

            strRevealButton?.onClick.RemoveAllListeners();
            dexRevealButton?.onClick.RemoveAllListeners();
            intRevealButton?.onClick.RemoveAllListeners();
            lukRevealButton?.onClick.RemoveAllListeners();
            allStatsRevealButton?.onClick.RemoveAllListeners();
            traitRevealButton?.onClick.RemoveAllListeners();
            weaponHintButton?.onClick.RemoveAllListeners();
            tab1WeaponButton?.onClick.RemoveAllListeners();
            tab1ActiveItemButton?.onClick.RemoveAllListeners();

            strRevealButton?.onClick.AddListener(() => this.controller?.OnRevealStatClicked(AdventurerStat.STR));
            dexRevealButton?.onClick.AddListener(() => this.controller?.OnRevealStatClicked(AdventurerStat.DEX));
            intRevealButton?.onClick.AddListener(() => this.controller?.OnRevealStatClicked(AdventurerStat.INT));
            lukRevealButton?.onClick.AddListener(() => this.controller?.OnRevealStatClicked(AdventurerStat.LUK));
            allStatsRevealButton?.onClick.AddListener(() => this.controller?.OnRevealAllStatsClicked());
            traitRevealButton?.onClick.AddListener(() => this.controller?.OnRevealTraitClicked());
            weaponHintButton?.onClick.AddListener(() => this.controller?.OnWeaponTypeHintClicked());
            tab1WeaponButton?.onClick.AddListener(() => this.controller?.OnMiniInfoWeaponClicked());
            tab1ActiveItemButton?.onClick.AddListener(() => this.controller?.OnTab1ActiveItemClicked());
        }

        #endregion

        #region 외부 호출

        public void UpdateTimeDisplay()
        {
            if (timeText != null)
                timeText.text = TimeManager.Instance.GetCurrentTimeString();
        }

        public void UpdateAdventurer(AdventurerInstance adventurer, WeaponInstance weapon = null)
        {
            if (adventurer == null) return;

            if (adventurer.appearance != null)
                adventurerAppearanceApplier?.ApplyAppearance(adventurer.appearance);

            if (adventurerNameText != null)
                adventurerNameText.text = adventurer.Name;
            if (adventurerGradeText != null)
                adventurerGradeText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Common", adventurer.isNamed ? "AdventurerGrade_Named" : "AdventurerGrade_Normal");

            UpdateStatDisplay(adventurer, weapon);
            UpdateTraitDisplay(adventurer);
        }

        public void UpdateWeaponInfo(WeaponInstance weapon, bool revealWeaponType)
        {
            bool showCard = weapon != null && revealWeaponType;

            tab1WeaponInfoPanel?.SetActive(showCard);

            if (!showCard) return;

            if (tab1WeaponCardBG != null)
                tab1WeaponCardBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);

            if (tab1WeaponCardFrame != null)
                tab1WeaponCardFrame.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);

            if (tab1WeaponIcon != null)
                tab1WeaponIcon.sprite = weapon.weaponData.icon;
        }

        public void UpdateActiveItemIcon(ActiveItemData data)
        {
            if (tab1ActiveItemPanel == null) return;
            bool hasIcon = data?.icon != null;
            tab1ActiveItemPanel.SetActive(hasIcon);
            if (!hasIcon) return;

            Grade grade = data.usageContext.ToGrade();

            if (tab1ActiveItemCardBG != null)
                tab1ActiveItemCardBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);

            if (tab1ActiveItemCardFrame != null)
                tab1ActiveItemCardFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);

            if (tab1ActiveItemIcon != null)
                tab1ActiveItemIcon.sprite = data.icon;
        }

        public void UpdateTalkButtons(AdventurerInstance adventurer)
        {
            if (adventurer == null) return;

            if (timeText != null)
                timeText.text = TimeManager.Instance.GetCurrentTimeString();

            var visibility = InsightManager.Instance.GetStatVisibility(adventurer);

            // 시간 조건은 interactable에서 제외한다. 시간이 부족할 때도 버튼을 누를 수 있어야
            // 컨트롤러가 "행동할 시간이 부족합니다." 토스트를 띄울 수 있다.
            if (strRevealButton != null) strRevealButton.interactable = !visibility.IsVisible(AdventurerStat.STR);
            if (dexRevealButton != null) dexRevealButton.interactable = !visibility.IsVisible(AdventurerStat.DEX);
            if (intRevealButton != null) intRevealButton.interactable = !visibility.IsVisible(AdventurerStat.INT);
            if (lukRevealButton != null) lukRevealButton.interactable = !visibility.IsVisible(AdventurerStat.LUK);

            if (allStatsRevealButton != null)
                allStatsRevealButton.interactable = !adventurer.isStatsFullyRevealed;

            if (traitRevealButton != null)
                traitRevealButton.interactable = !InsightManager.Instance.IsTraitKnown(adventurer);

            bool canHint = InsightManager.Instance.CanRevealWeaponTypeHint();
            if (weaponHintButton != null)
                weaponHintButton.interactable = !adventurer.isWeaponTypeHinted && canHint;
        }

        #endregion

        #region 튜토리얼 하이라이트용 접근자

        public RectTransform GetWeaponHintButtonRect() => weaponHintButton?.transform as RectTransform;
        public RectTransform GetAllStatsRevealButtonRect() => allStatsRevealButton?.transform as RectTransform;

        #endregion

        #region 내부 메서드

        private void UpdateStatDisplay(AdventurerInstance adventurer, WeaponInstance weapon = null)
        {
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

            if (avgStatText != null)
            {
                avgStatText.text = visibility.ShowAverage
                    ? LocalizationSettings.StringDatabase.GetLocalizedString(
                          "UI_Screens", "Preparation_AvgStat",
                          arguments: new object[] { new Dictionary<string, object> {
                              { "value", (adventurer.STR + adventurer.DEX + adventurer.INT + adventurer.LUK) / 4 } } })
                    : LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", "Preparation_AvgStatHidden");
            }

            SetStatBar(strSlider, strBonusSlider, strValueText, strLockIndicator,
                AdventurerStat.STR, adventurer.STR, bonusStr, visibility);
            SetStatBar(dexSlider, dexBonusSlider, dexValueText, dexLockIndicator,
                AdventurerStat.DEX, adventurer.DEX, bonusDex, visibility);
            SetStatBar(intSlider, intBonusSlider, intValueText, intLockIndicator,
                AdventurerStat.INT, adventurer.INT, bonusInt, visibility);
            SetStatBar(lukSlider, lukBonusSlider, lukValueText, lukLockIndicator,
                AdventurerStat.LUK, adventurer.LUK, bonusLuk, visibility);
        }

        private void SetStatBar(Slider slider, Slider bonusSlider, TextMeshProUGUI label,
            GameObject lockIndicator, AdventurerStat stat, int baseVal, int bonus, AdventurerStatVisibility visibility)
        {
            bool isVisible = visibility.IsVisible(stat);
            lockIndicator?.SetActive(!isVisible);

            if (isVisible)
            {
                if (slider != null) slider.value = Mathf.Clamp01((float)baseVal / statDisplayMax);
                if (bonusSlider != null)
                {
                    bonusSlider.gameObject.SetActive(bonus > 0);
                    if (bonus > 0)
                        bonusSlider.value = Mathf.Clamp01((float)(baseVal + bonus) / statDisplayMax);
                }
                if (label != null)
                {
                    string greenHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor());
                    label.text = bonus > 0 ? $"{baseVal}<color={greenHex}>+{bonus}</color>" : baseVal.ToString();
                }
            }
            else
            {
                if (slider != null) slider.value = 0f;
                if (bonusSlider != null) bonusSlider.gameObject.SetActive(false);
                if (label != null)
                {
                    string grayHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGrayColor());
                    label.text = $"<color={grayHex}>??</color>";
                }
            }
        }

        private void UpdateTraitDisplay(AdventurerInstance adventurer)
        {
            if (traitPanel == null) return;

            if (InsightManager.Instance.IsTraitKnown(adventurer))
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

        #endregion
    }
}
