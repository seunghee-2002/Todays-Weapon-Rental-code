// Scripts/UI/Views/QuestBoard/DungeonDetailPopup.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 던전 상세 팝업
    /// 의뢰판에서 슬롯 클릭 시 오픈
    /// AdventurePreparationView 탭B(던전 정보)와 동일한 구조 — 단일 화면에 5개 섹션 표시
    /// </summary>
    public class DungeonDetailPopup : BaseView
    {
        [Header("상시 표시")]
        [SerializeField] private Image dungeonIcon;
        [SerializeField] private Image dungeonIconBG;
        [SerializeField] private Image dungeonFrame;
        [SerializeField] private TextMeshProUGUI dungeonNameText;
        [SerializeField] private TextMeshProUGUI gradeText;

        [Header("ArmorTypes")]
        [SerializeField] private Image armorTypeImage;
        [SerializeField] private Image armorTypeBG;
        [SerializeField] private Image armorTypeGlow;
        [SerializeField] private Button armorTypeButton;
        [SerializeField] private Transform variantArmorTypeContainer;
        [SerializeField] private GameObject armorTypeIconPrefab;

        [Header("Exploration")]
        [SerializeField] private Slider explorationBar;
        [SerializeField] private TextMeshProUGUI explorationText;
        [SerializeField] private TextMeshProUGUI totalAttemptText;
        [SerializeField] private TextMeshProUGUI successCountText;

        [Header("Materials")]
        [SerializeField] private Image specialMaterialIcon;
        [SerializeField] private Image specialMaterialBG;
        [SerializeField] private Image specialMaterialGlow;
        [SerializeField] private Button specialMaterialButton;
        [SerializeField] private Transform normalMaterialContainer;
        [SerializeField] private GameObject materialItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        private DungeonData currentDungeon;
        private DungeonStatData currentStatData;


        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen   = false;
            isCanClickOverlay = true;
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(DungeonData dungeon, DungeonStatData statData)
        {
            currentDungeon  = dungeon;
            currentStatData = statData;

            UpdateNameAndGrade();
            UpdateArmorTypes();
            UpdateExploration();
            UpdateMaterials();
        }

        #endregion

        #region UI 업데이트

        private void UpdateNameAndGrade()
        {
            if (dungeonIcon != null)
                dungeonIcon.sprite = currentDungeon.dungeonIcon;
            if (dungeonIconBG != null)
                dungeonIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(currentDungeon.grade);
            if (dungeonFrame != null)
                dungeonFrame.sprite = IconManager.Instance.GetFrameByGrade(currentDungeon.grade);

            if (dungeonNameText != null)
                dungeonNameText.text = currentDungeon.DisplayName;

            if (gradeText != null)
            {
                gradeText.text  = UITranslator.GetString(currentDungeon.grade);
                gradeText.color = ColorManager.Instance.GetGradeAccentColor(currentDungeon.grade);
            }
        }

        private void UpdateArmorTypes()
        {
            // 주 armorType
            if (armorTypeImage != null)
                armorTypeImage.sprite = IconManager.Instance.GetIconByArmorType(currentDungeon.armorType);

            if (armorTypeBG != null)
                armorTypeBG.sprite = IconManager.Instance.GetArmorTypeGlowBG(currentDungeon.armorType);

            if (armorTypeGlow != null)
                armorTypeGlow.color = ColorManager.Instance.GetGlowByArmorType(currentDungeon.armorType);

            if (armorTypeButton != null)
            {
                armorTypeButton.onClick.RemoveAllListeners();
                armorTypeButton.onClick.AddListener(() => OpenArmorTypePopup(currentDungeon.armorType));
            }

            // 변형 armorType 아이콘 동적 생성
            if (variantArmorTypeContainer != null && armorTypeIconPrefab != null)
            {
                foreach (Transform child in variantArmorTypeContainer)
                    Destroy(child.gameObject);

                foreach (var variant in currentDungeon.armorTypeVariants)
                {
                    var iconObj = Instantiate(armorTypeIconPrefab, variantArmorTypeContainer);
                    var iconItem = iconObj.GetComponent<IconButton>();
                    if (iconItem != null)
                        iconItem.Initialize(
                            IconManager.Instance.GetIconByArmorType(variant.armorType),
                            IconManager.Instance.GetArmorTypeGlowBG(variant.armorType),
                            ColorManager.Instance.GetGlowByArmorType(variant.armorType),
                            () => OpenArmorTypePopup(variant.armorType));
                }
            }
        }

        private void UpdateExploration()
        {
            int progress = currentStatData != null ? currentStatData.explorationProgress : 0;

            if (explorationBar != null)
                explorationBar.value = progress / 100f;
            if (explorationText != null)
                explorationText.text = $"{progress}%";

            if (totalAttemptText != null)
                totalAttemptText.text = Count("DungeonDetail_TotalAttempts",
                    currentStatData != null ? currentStatData.totalAttempts : 0);
            if (successCountText != null)
                successCountText.text = Count("DungeonDetail_SuccessCount",
                    currentStatData != null ? currentStatData.successCount : 0);
        }

        private static string Count(string key, int count)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "count", count } } });

        private void UpdateMaterials()
        {
            // 특수 재료
            if (currentDungeon.specialDropMaterial != null)
            {
                var grade = currentDungeon.specialDropMaterial.grade;

                if (specialMaterialIcon != null)
                    specialMaterialIcon.sprite = currentDungeon.specialDropMaterial.icon;

                if (specialMaterialBG != null)
                    specialMaterialBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);

                if (specialMaterialGlow != null)
                    specialMaterialGlow.color = ColorManager.Instance.GetGradeGlowColor(grade);

                if (specialMaterialButton != null)
                {
                    specialMaterialButton.onClick.RemoveAllListeners();
                    specialMaterialButton.onClick.AddListener(() => OpenMaterialPopup(currentDungeon.specialDropMaterial));
                }
            }

            // 일반 재료 목록
            if (normalMaterialContainer != null)
            {
                foreach (Transform child in normalMaterialContainer)
                    Destroy(child.gameObject);

                if (materialItemPrefab != null)
                {
                    foreach (var material in currentDungeon.dropMaterials
                                 .Where(m => m != null)
                                 .OrderByDescending(m => m.grade))
                    {
                        var obj = Instantiate(materialItemPrefab, normalMaterialContainer);
                        var iconItem = obj.GetComponent<IconButton>();
                        if (material.icon != null && iconItem != null)
                            iconItem.Initialize(
                                material.icon,
                                ColorManager.Instance.GetGradeCardBackgroundColor(material.grade),
                                ColorManager.Instance.GetGradeGlowColor(material.grade),
                                () => OpenMaterialPopup(material));
                    }
                }
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel<DungeonDetailPopup>();
        }

        private void OpenArmorTypePopup(ArmorType armor)
        {
            UIManager.Instance.OpenPanel<ArmorTypeDetailPopup>();
            UIManager.Instance.GetOrInstantiatePanel<ArmorTypeDetailPopup>()?.Initialize(armor);
        }

        private void OpenMaterialPopup(MaterialData material)
        {
            if (material == null) return;

            UIManager.Instance.OpenPanel<MaterialDetailPopup>();
            UIManager.Instance.GetOrInstantiatePanel<MaterialDetailPopup>()?.Initialize(material);
        }

        #endregion
    }
}
