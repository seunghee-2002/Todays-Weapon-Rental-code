using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 던전 상세 패널 — lazy-init 단일 인스턴스로 관리.
    /// 도메인 매니저 호출은 Controller가 담당하고, 이 패널은 영역별 Update 메서드로 표시만 갱신한다.
    /// </summary>
    public class PreparationDungeonDetailPanel : MonoBehaviour
    {
        [Header("기본")]
        [SerializeField] private Image dungeonDetailIcon;
        [SerializeField] private Image dungeonDetailIconBG;
        [SerializeField] private Image dungeonDetailFrame;
        [SerializeField] private TextMeshProUGUI dungeonDetailTabLabel;
        [SerializeField] private Button selectDungeonButton;
        [SerializeField] private Button backDungeonDetailButton;

        [Header("내부 탭 내비게이션")]
        [SerializeField] private Button detailPrevButton;
        [SerializeField] private Button detailNextButton;
        [SerializeField] private GameObject adventureInfoTab;
        [SerializeField] private GameObject dungeonInfoTab;

        [Header("탭 A — 모험 정보")]
        [SerializeField] private Toggle unarmoredToggle;
        [SerializeField] private Toggle lightArmorToggle;
        [SerializeField] private Toggle heavyArmorToggle;
        [SerializeField] private Toggle magicalArmorToggle;
        [SerializeField] private GameObject unarmoredInactiveIndicator;
        [SerializeField] private GameObject lightArmorInactiveIndicator;
        [SerializeField] private GameObject heavyArmorInactiveIndicator;
        [SerializeField] private GameObject magicalArmorInactiveIndicator;
        [SerializeField] private TextMeshProUGUI dungeonDetailDurationText;
        [SerializeField] private Transform dungeonDetailEffectListContainer;
        [SerializeField] private GameObject dungeonDetailEffectItemPrefab;
        [SerializeField] private TextMeshProUGUI expectedRateText;
        [SerializeField] private TextMeshProUGUI baseRateText;
        [SerializeField] private TextMeshProUGUI armorBonusRow;
        [SerializeField] private TextMeshProUGUI conditionBonusRow;
        [SerializeField] private TextMeshProUGUI charmBonusRow;
        [SerializeField] private TextMeshProUGUI traitSuccessRow;
        [SerializeField] private TextMeshProUGUI dungeonDetailEffectHintText;
        [SerializeField] private TextMeshProUGUI dungeonDetailSuccessRateHintText;
        [Tooltip("성공률 영역 전체를 덮는 버튼. 누르면 이벤트별 성공률 툴팁이 열린다.")]
        [SerializeField] private Button successRateAreaButton;
        [SerializeField] private PreparationEventRateTooltip eventRateTooltip;

        [Header("튜토리얼 하이라이트(8단계)")]
        [Tooltip("시뮬 결과 하이라이트 대상(성공률 영역). 미지정 시 baseRateText의 부모로 폴백.")]
        [SerializeField] private RectTransform successRateArea;

        [Header("탭 B — 던전 정보")]
        [SerializeField] private TextMeshProUGUI dungeonInfoNameText;
        [SerializeField] private TextMeshProUGUI dungeonInfoGradeText;
        [SerializeField] private Image dungeonInfoArmorTypeImage;
        [SerializeField] private Image dungeonInfoArmorTypeBG;
        [SerializeField] private Image dungeonInfoArmorTypeGlow;
        [SerializeField] private Button dungeonInfoArmorTypeButton;
        [SerializeField] private Transform dungeonInfoVariantArmorTypeContainer;
        [SerializeField] private GameObject dungeonInfoArmorTypeIconPrefab;
        [SerializeField] private Slider dungeonInfoExplorationBar;
        [SerializeField] private TextMeshProUGUI dungeonInfoExplorationText;
        [SerializeField] private TextMeshProUGUI dungeonInfoTotalAttemptText;
        [SerializeField] private TextMeshProUGUI dungeonInfoSuccessCountText;
        [SerializeField] private Image dungeonInfoSpecialMaterialIcon;
        [SerializeField] private Image dungeonInfoSpecialMaterialBG;
        [SerializeField] private Image dungeonInfoSpecialMaterialGlow;
        [SerializeField] private Button dungeonInfoSpecialMaterialButton;
        [SerializeField] private Transform dungeonInfoNormalMaterialContainer;
        [SerializeField] private GameObject dungeonInfoMaterialItemPrefab;

        private AdventurePreparationController controller;

        private int currentDetailTabIndex = 0;
        private ArmorType currentArmorToggle = ArmorType.Unarmored; // 현재 켜진 토글 — reset용
        private bool isArmorToggleResetting = false;
        private bool armorToggleInteractable = true;

        #region 초기화

        public void Initialize(AdventurePreparationController controller)
        {
            this.controller = controller;

            selectDungeonButton?.onClick.RemoveAllListeners();
            backDungeonDetailButton?.onClick.RemoveAllListeners();
            detailPrevButton?.onClick.RemoveAllListeners();
            detailNextButton?.onClick.RemoveAllListeners();
            successRateAreaButton?.onClick.RemoveAllListeners();
            unarmoredToggle?.onValueChanged.RemoveAllListeners();
            lightArmorToggle?.onValueChanged.RemoveAllListeners();
            heavyArmorToggle?.onValueChanged.RemoveAllListeners();
            magicalArmorToggle?.onValueChanged.RemoveAllListeners();

            selectDungeonButton?.onClick.AddListener(OnSelectDungeonClicked);
            backDungeonDetailButton?.onClick.AddListener(() => controller?.OnBackDungeonDetailClicked());

            detailPrevButton?.onClick.AddListener(() => this.controller?.OnDetailPrevClicked());
            detailNextButton?.onClick.AddListener(() => this.controller?.OnDetailNextClicked());
            successRateAreaButton?.onClick.AddListener(() => this.controller?.OnSuccessRateAreaClicked());

            eventRateTooltip?.Hide();

            unarmoredToggle?.onValueChanged.AddListener(on => { if (on) OnArmorToggleClicked(ArmorType.Unarmored, unarmoredInactiveIndicator); });
            lightArmorToggle?.onValueChanged.AddListener(on => { if (on) OnArmorToggleClicked(ArmorType.LightArmor, lightArmorInactiveIndicator); });
            heavyArmorToggle?.onValueChanged.AddListener(on => { if (on) OnArmorToggleClicked(ArmorType.HeavyArmor, heavyArmorInactiveIndicator); });
            magicalArmorToggle?.onValueChanged.AddListener(on => { if (on) OnArmorToggleClicked(ArmorType.MagicalArmor, magicalArmorInactiveIndicator); });
        }

        private void OnSelectDungeonClicked()
        {
            controller?.OnSelectDungeonClicked();
            Hide();
        }

        #endregion

        #region Show / Hide / Tab

        public void Show(Sprite dungeonIcon, Color iconBGColor, Sprite frameSprite)
        {
            gameObject.SetActive(true);
            eventRateTooltip?.Hide();

            if (dungeonDetailIcon != null)
                dungeonDetailIcon.sprite = dungeonIcon;

            if (dungeonDetailIconBG != null)
                dungeonDetailIconBG.color = iconBGColor;
            if (dungeonDetailFrame != null)
                dungeonDetailFrame.sprite = frameSprite;

            ShowDetailTab(0);
        }

        public void Hide()
        {
            eventRateTooltip?.Hide();
            gameObject.SetActive(false);
        }

        /// <summary>이벤트별 성공률 툴팁 표시. 값은 컨트롤러가 계산해 넘긴다.</summary>
        public void ShowEventRateTooltip(EventRateTooltipData data)
        {
            eventRateTooltip?.Show(data);
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        public void ShowDetailTab(int tabIndex)
        {
            currentDetailTabIndex = tabIndex;
            dungeonDetailTabLabel.text = tabIndex switch
            {
                0 => L("Preparation_TabAdventureInfo"),
                1 => L("DungeonDetail_Title"),
                _ => ""
            };
            adventureInfoTab?.SetActive(tabIndex == 0);
            dungeonInfoTab?.SetActive(tabIndex == 1);
            detailPrevButton?.gameObject.SetActive(tabIndex > 0);
            detailNextButton?.gameObject.SetActive(tabIndex < 1);
        }

        #endregion

        #region 영역별 Update

        /// <summary>
        /// 방어 타입 토글 영역 갱신.
        /// </summary>
        public void UpdateArmorToggles(bool isArmorTypeKnown, ArmorType current, bool unarmoredInDungeon,
            bool lightInDungeon, bool heavyInDungeon, bool magicalInDungeon)
        {
            armorToggleInteractable = !isArmorTypeKnown;
            currentArmorToggle = current;

            if (unarmoredToggle != null) unarmoredToggle.interactable = armorToggleInteractable;
            if (lightArmorToggle != null) lightArmorToggle.interactable = armorToggleInteractable;
            if (heavyArmorToggle != null) heavyArmorToggle.interactable = armorToggleInteractable;
            if (magicalArmorToggle != null) magicalArmorToggle.interactable = armorToggleInteractable;

            unarmoredInactiveIndicator?.SetActive(!unarmoredInDungeon);
            lightArmorInactiveIndicator?.SetActive(!lightInDungeon);
            heavyArmorInactiveIndicator?.SetActive(!heavyInDungeon);
            magicalArmorInactiveIndicator?.SetActive(!magicalInDungeon);

            isArmorToggleResetting = true;
            switch (current)
            {
                case ArmorType.Unarmored:    if (unarmoredToggle    != null) unarmoredToggle.isOn    = true; break;
                case ArmorType.LightArmor:   if (lightArmorToggle   != null) lightArmorToggle.isOn   = true; break;
                case ArmorType.HeavyArmor:   if (heavyArmorToggle   != null) heavyArmorToggle.isOn   = true; break;
                case ArmorType.MagicalArmor: if (magicalArmorToggle != null) magicalArmorToggle.isOn = true; break;
            }
            isArmorToggleResetting = false;
        }

        /// <summary>
        /// 무기 부가효과 리스트 갱신. states는 weapon.effects와 같은 순서.
        /// </summary>
        public void UpdateEffectList(WeaponInstance weapon, IReadOnlyList<EffectDisplayState> states)
        {
            if (dungeonDetailEffectListContainer == null) return;

            foreach (Transform child in dungeonDetailEffectListContainer)
                Destroy(child.gameObject);

            if (weapon?.effects == null || states == null) return;

            int count = Mathf.Min(weapon.effects.Count, states.Count);
            for (int i = 0; i < count; i++)
            {
                if (dungeonDetailEffectItemPrefab == null) break;
                var obj = Instantiate(dungeonDetailEffectItemPrefab, dungeonDetailEffectListContainer);
                obj.GetComponent<WeaponEffectListItem>()?.Initialize(weapon.effects[i], true, false, states[i]);
            }
        }

        /// <summary>
        /// 성공률 분해 행 갱신.
        /// </summary>
        public void UpdateSuccessRateRows(
            bool showBaseRate, float expectedRate, float baseRate, float statEffectBonus,
            bool showArmorBonusRow, float armorBonus,
            float conditionBonus,
            bool showCharmRow, float charmBonus,
            bool showTraitRow, string traitNote)
        {
            // 실제 판정은 여기에 이벤트 난이도·운세·기분 배율이 더 곱해지므로 "약"을 붙여 근사치임을 알린다.
            if (expectedRateText != null)
            {
                expectedRateText.gameObject.SetActive(showBaseRate);
                if (showBaseRate)
                    expectedRateText.text = L("Preparation_ExpectedBattleRate",
                        ("rate", $"{expectedRate * 100f:0}"));
            }

            // 상성·부적은 effectBase에 곱해지는 곱연산 보정 → 배수(x1.4배)로 표기한다.
            if (baseRateText != null)
            {
                baseRateText.gameObject.SetActive(showBaseRate);
                if (showBaseRate)
                {
                    if (!Mathf.Approximately(statEffectBonus, 0f))
                    {
                        string sign  = statEffectBonus > 0f ? "+" : "";
                        string colorHex = statEffectBonus > 0f
                            ? "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor())
                            : "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetRedColor());
                        baseRateText.text = L("Preparation_BaseRateWithBonus",
                            ("rate",  $"{baseRate * 100f:0.#}"),
                            ("color", colorHex),
                            ("sign",  sign),
                            ("bonus", $"{statEffectBonus * 100f:0.#}"));
                    }
                    else
                    {
                        baseRateText.text = L("Preparation_BaseRate", ("rate", $"{baseRate * 100f:0.#}"));
                    }
                }
            }

            if (armorBonusRow != null)
            {
                if (showArmorBonusRow) SetMultiplierRow(armorBonusRow, L("Preparation_RowArmorMatch"), armorBonus);
                else armorBonusRow.gameObject.SetActive(false);
            }

            SetBonusRow(conditionBonusRow, L("Preparation_RowWeaponEffect"), conditionBonus);

            if (charmBonusRow != null)
            {
                if (showCharmRow) SetMultiplierRow(charmBonusRow, L("Preparation_RowCharm"), charmBonus);
                else charmBonusRow.gameObject.SetActive(false);
            }

            if (traitSuccessRow != null)
            {
                bool show = showTraitRow && traitNote != null;
                traitSuccessRow.gameObject.SetActive(show);
                if (show) traitSuccessRow.text = traitNote;
            }

            // 성공률 분해가 통찰로 가려진 상태면 이벤트별 툴팁도 열 수 없다
            if (successRateAreaButton != null)
                successRateAreaButton.interactable = showBaseRate;

            RefreshSuccessRateHintVisibility();
        }

        /// <summary>
        /// 탐험 시간 영역 갱신.
        /// </summary>
        public void UpdateDuration(bool isRevealed, float displayTime, float totalMultiplier)
        {
            if (dungeonDetailDurationText == null) return;

            if (!isRevealed)
            {
                dungeonDetailDurationText.text = L("Preparation_DurationUnknown");
                dungeonDetailDurationText.color = ColorManager.Instance.GetGrayColor();
                return;
            }

            string durationStr = UITranslator.FormatDuration(Mathf.RoundToInt(displayTime * 60f));
            dungeonDetailDurationText.text = Mathf.Approximately(totalMultiplier, 1f)
                ? durationStr
                : $"{durationStr}(x{totalMultiplier:0.##})";
            dungeonDetailDurationText.color = totalMultiplier > 1f
                ? ColorManager.Instance.GetGoldColor()
                : (totalMultiplier < 1f ? ColorManager.Instance.GetGreenColor() : ColorManager.Instance.GetWhiteColor());
        }

        /// <summary>
        /// 무기 미선택 시 부가효과 영역의 안내 문구 토글.
        /// </summary>
        public void UpdateEffectHint(bool isWeaponSelected)
        {
            if (dungeonDetailEffectHintText != null)
                dungeonDetailEffectHintText.text = isWeaponSelected ? "" : L("Preparation_EffectHintNeedWeapon");
        }

        /// <summary>
        /// 던전 정보 탭 갱신. 정적 정보 + 누적 통계.
        /// </summary>
        public void UpdateDungeonInfoTab(DungeonData dungeon, DungeonStatData stat)
        {
            if (dungeon == null) return;

            if (dungeonInfoNameText != null)
                dungeonInfoNameText.text = dungeon.DisplayName;

            if (dungeonInfoGradeText != null)
            {
                dungeonInfoGradeText.text = UITranslator.GetString(dungeon.grade);
                dungeonInfoGradeText.color = ColorManager.Instance.GetGradeAccentColor(dungeon.grade);
            }

            if (dungeonInfoArmorTypeImage != null)
                dungeonInfoArmorTypeImage.sprite = IconManager.Instance.GetIconByArmorType(dungeon.armorType);

            if (dungeonInfoArmorTypeBG != null)
                dungeonInfoArmorTypeBG.sprite = IconManager.Instance.GetArmorTypeGlowBG(dungeon.armorType);

            if (dungeonInfoArmorTypeGlow != null)
                dungeonInfoArmorTypeGlow.color = ColorManager.Instance.GetGlowByArmorType(dungeon.armorType);

            if (dungeonInfoArmorTypeButton != null)
            {
                dungeonInfoArmorTypeButton.onClick.RemoveAllListeners();
                dungeonInfoArmorTypeButton.onClick.AddListener(() => controller?.OnArmorInfoClicked(dungeon.armorType));
            }

            if (dungeonInfoVariantArmorTypeContainer != null && dungeonInfoArmorTypeIconPrefab != null)
            {
                foreach (Transform child in dungeonInfoVariantArmorTypeContainer)
                    Destroy(child.gameObject);

                foreach (var variant in dungeon.armorTypeVariants)
                {
                    var iconObj = Instantiate(dungeonInfoArmorTypeIconPrefab, dungeonInfoVariantArmorTypeContainer);
                    var iconItem = iconObj.GetComponent<IconButton>();
                    if (iconItem != null)
                        iconItem.Initialize(IconManager.Instance.GetIconByArmorType(variant.armorType), IconManager.Instance.GetArmorTypeGlowBG(variant.armorType), ColorManager.Instance.GetGlowByArmorType(variant.armorType), () => controller?.OnArmorInfoClicked(variant.armorType));
                }
            }

            int progress = stat != null ? stat.explorationProgress : 0;
            if (dungeonInfoExplorationBar != null)
                dungeonInfoExplorationBar.value = progress / 100f;
            if (dungeonInfoExplorationText != null)
                dungeonInfoExplorationText.text = $"{progress}%";

            if (dungeonInfoTotalAttemptText != null)
                dungeonInfoTotalAttemptText.text = L("DungeonDetail_TotalAttempts", ("count", stat?.totalAttempts ?? 0));
            if (dungeonInfoSuccessCountText != null)
                dungeonInfoSuccessCountText.text = L("DungeonDetail_SuccessCount", ("count", stat?.successCount ?? 0));

            // 특수 재료 블록 전체를 null 가드로 묶는다 - 특수 재료 없는 던전에서 NRE 방지
            var special = dungeon.specialDropMaterial;
            bool hasSpecial = special != null;

            if (dungeonInfoSpecialMaterialIcon != null)
                dungeonInfoSpecialMaterialIcon.gameObject.SetActive(hasSpecial);
            if (dungeonInfoSpecialMaterialBG != null)
                dungeonInfoSpecialMaterialBG.gameObject.SetActive(hasSpecial);
            if (dungeonInfoSpecialMaterialGlow != null)
                dungeonInfoSpecialMaterialGlow.gameObject.SetActive(hasSpecial);
            if (dungeonInfoSpecialMaterialButton != null)
            {
                dungeonInfoSpecialMaterialButton.gameObject.SetActive(hasSpecial);
                // null이어도 이전 던전의 리스너가 남지 않도록 항상 제거
                dungeonInfoSpecialMaterialButton.onClick.RemoveAllListeners();
            }

            if (hasSpecial)
            {
                if (dungeonInfoSpecialMaterialIcon != null)
                    dungeonInfoSpecialMaterialIcon.sprite = special.icon;
                if (dungeonInfoSpecialMaterialBG != null)
                    dungeonInfoSpecialMaterialBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(special.grade);
                if (dungeonInfoSpecialMaterialGlow != null)
                    dungeonInfoSpecialMaterialGlow.color = ColorManager.Instance.GetGradeGlowColor(special.grade);
                if (dungeonInfoSpecialMaterialButton != null)
                    dungeonInfoSpecialMaterialButton.onClick.AddListener(() => controller?.OnMaterialInfoClicked(special));
            }

            if (dungeonInfoNormalMaterialContainer != null)
            {
                foreach (Transform child in dungeonInfoNormalMaterialContainer)
                    Destroy(child.gameObject);

                if (dungeonInfoMaterialItemPrefab != null)
                {
                    foreach (var material in dungeon.dropMaterials
                                 .Where(m => m != null)
                                 .OrderByDescending(m => m.grade))
                    {
                        var obj = Instantiate(dungeonInfoMaterialItemPrefab, dungeonInfoNormalMaterialContainer);
                        var iconItem = obj.GetComponent<IconButton>();
                        if (material.icon != null && iconItem != null)
                            iconItem.Initialize(material.icon, ColorManager.Instance.GetGradeCardBackgroundColor(material.grade), ColorManager.Instance.GetGradeGlowColor(material.grade), () => controller?.OnMaterialInfoClicked(material));
                    }
                }
            }
        }

        #endregion

        #region 방어타입 토글 이벤트

        private void OnArmorToggleClicked(ArmorType armorType, GameObject inactiveIndicator)
        {
            if (isArmorToggleResetting) return;

            if (inactiveIndicator != null && inactiveIndicator.activeSelf)
            {
                isArmorToggleResetting = true;
                switch (currentArmorToggle)
                {
                    case ArmorType.Unarmored:    if (unarmoredToggle    != null) unarmoredToggle.isOn    = true; break;
                    case ArmorType.LightArmor:   if (lightArmorToggle   != null) lightArmorToggle.isOn   = true; break;
                    case ArmorType.HeavyArmor:   if (heavyArmorToggle   != null) heavyArmorToggle.isOn   = true; break;
                    case ArmorType.MagicalArmor: if (magicalArmorToggle != null) magicalArmorToggle.isOn = true; break;
                }
                isArmorToggleResetting = false;
                return;
            }

            currentArmorToggle = armorType;
            controller?.OnArmorTypeSimulationChanged(armorType);
        }

        #endregion

        #region 튜토리얼 하이라이트/제한

        /// <summary>튜토리얼 하이라이트용 — 던전 선택 버튼 RectTransform.</summary>
        public RectTransform GetSelectDungeonButtonRect()
        {
            var rect = selectDungeonButton?.transform as RectTransform;
            // 상세 패널이 방금 활성화됐을 수 있어, 위치를 읽기 전에 부모 레이아웃을 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }

        /// <summary>튜토리얼 시뮬레이션 유도(8단계) — 지정 방어타입 토글만 조작 가능하게 제한한다(그 외 토글 비활성).</summary>
        public void SetTutorialSimulationOnly(ArmorType allowed)
        {
            if (unarmoredToggle != null)    unarmoredToggle.interactable    = allowed == ArmorType.Unarmored;
            if (lightArmorToggle != null)   lightArmorToggle.interactable   = allowed == ArmorType.LightArmor;
            if (heavyArmorToggle != null)   heavyArmorToggle.interactable   = allowed == ArmorType.HeavyArmor;
            if (magicalArmorToggle != null) magicalArmorToggle.interactable = allowed == ArmorType.MagicalArmor;
        }

        /// <summary>튜토리얼 하이라이트용(8단계) — 성공률 영역 RectTransform(왜 잘 어울리는지 표시). 미지정 시 성공률 행들의 부모로 폴백.</summary>
        public RectTransform GetSuccessRateAreaRect()
        {
            var target = successRateArea != null ? successRateArea : baseRateText?.transform.parent as RectTransform;
            if (target?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return target;
        }

        /// <summary>튜토리얼 하이라이트용(8단계) — 지정 방어타입 토글 RectTransform.</summary>
        public RectTransform GetArmorToggleRect(ArmorType armorType)
        {
            Toggle t = armorType switch
            {
                ArmorType.Unarmored    => unarmoredToggle,
                ArmorType.LightArmor   => lightArmorToggle,
                ArmorType.HeavyArmor   => heavyArmorToggle,
                ArmorType.MagicalArmor => magicalArmorToggle,
                _ => null
            };
            var rect = t?.transform as RectTransform;
            // 상세 패널이 방금 활성화됐을 수 있어, 위치를 읽기 전에 부모 레이아웃을 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }

        #endregion

        #region 내부 헬퍼

        private void SetBonusRow(TextMeshProUGUI label, string title, float value)
        {
            if (label == null) return;
            if (Mathf.Approximately(value, 0f)) { label.gameObject.SetActive(false); return; }
            label.gameObject.SetActive(true);
            string sign  = value > 0f ? "+" : "";
            string colorHex = value > 0f
                ? "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor())
                : "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetRedColor());
            label.text = $"{title} <color={colorHex}>{sign}{value * 100f:0.#}%</color>";
        }

        /// <summary>
        /// 곱연산 보정 행(상성·부적) 갱신. effectBase에 곱해지는 배수이므로 x1.4배 형태로 표시.
        /// </summary>
        private void SetMultiplierRow(TextMeshProUGUI label, string title, float value)
        {
            if (label == null) return;
            if (Mathf.Approximately(value, 0f)) { label.gameObject.SetActive(false); return; }
            label.gameObject.SetActive(true);
            string colorHex = value > 0f
                ? "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor())
                : "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetRedColor());
            label.text = L("Preparation_MultiplierRow",
                ("title", title), ("color", colorHex), ("value", $"{1f + value:0.##}"));
        }

        private void RefreshSuccessRateHintVisibility()
        {
            bool anyRateRowVisible = (expectedRateText != null && expectedRateText.gameObject.activeSelf)
                || (baseRateText != null && baseRateText.gameObject.activeSelf)
                || (armorBonusRow != null && armorBonusRow.gameObject.activeSelf)
                || (conditionBonusRow != null && conditionBonusRow.gameObject.activeSelf)
                || (charmBonusRow != null && charmBonusRow.gameObject.activeSelf)
                || (traitSuccessRow != null && traitSuccessRow.gameObject.activeSelf);
            if (dungeonDetailSuccessRateHintText != null)
                dungeonDetailSuccessRateHintText.gameObject.SetActive(!anyRateRowVisible);
        }

        #endregion
    }
}
