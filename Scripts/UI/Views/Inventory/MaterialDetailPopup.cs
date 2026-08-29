using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 재료 상세정보 팝업
    /// </summary>
    public class MaterialDetailPopup : BaseView
    {
        [Header("UI Elements")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image materialIcon;
        [SerializeField] private TextMeshProUGUI materialNameText;
        [SerializeField] private TextMeshProUGUI materialQuantityText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI dungeonListText;
        [SerializeField] private Button closeButton;
        
        private MaterialData currentMaterial;
        private int currentQuantity;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            
            pauseTimeOnOpen = false;
            isCanClickOverlay = true;
            canEscape = true;
        }
        
        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
        }
        
        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
        }
        
        /// <summary>
        /// 재료 상세정보를 표시한다.
        /// 표시 수량은 항상 현재 보유 개수를 내부에서 직접 조회(InventoryManager.GetMaterialCount)해
        /// "보유 개수: N" 형태로 보여준다. 호출 측은 수량을 넘기지 않는다.
        /// </summary>
        public void Initialize(MaterialData material)
        {
            currentMaterial = material;
            currentQuantity = material != null
                ? InventoryManager.Instance.GetMaterialCount(material.StaticID)
                : 0;
            UpdateUI();
        }

        #endregion

        #region UI 업데이트 메서드
        
        private void UpdateUI()
        {
            if (currentMaterial == null) return;

            if (backgroundImage != null)
                backgroundImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(currentMaterial.grade);

            if (frameImage != null)
                frameImage.sprite = IconManager.Instance.GetFrameByGrade(currentMaterial.grade);

            // 아이콘
            if (materialIcon != null && currentMaterial.icon != null)
                materialIcon.sprite = currentMaterial.icon;

            // 이름
            if (materialNameText != null)
            {
                materialNameText.color = ColorManager.Instance.GetGradeAccentColor(currentMaterial.grade);
                materialNameText.text = currentMaterial.DisplayName;
            }

            // 보유 개수
            if (materialQuantityText != null)
                materialQuantityText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "Material_QuantityOwned",
                    arguments: new object[] { new Dictionary<string, object> { { "count", currentQuantity } } });

            // 타입
            if (typeText != null)
                typeText.text = UITranslator.GetString(currentMaterial.materialType);
            
            // 획득 가능 던전 목록
            if (dungeonListText != null)
                dungeonListText.text = BuildDungeonListText(currentMaterial);
        }

        private string BuildDungeonListText(MaterialData material)
        {
            if (material.materialType == MaterialType.Craft)
            {
                List<DungeonData> dropSources = DataManager.Instance.GetDropSourcesForMaterial(material.StaticID);
                if (dropSources != null)
                {
                    List<string> gradeLines = dropSources
                        .Where(dungeon => dungeon != null)
                        .OrderBy(dungeon => dungeon.grade)
                        .GroupBy(dungeon => dungeon.grade)
                        .Select(group => string.Join(", ", group.Select(GetColoredDungeonName)))
                        .ToList();

                    if (gradeLines.Count > 0)
                        return Localize("Material_DungeonListHeader") + "\n" + string.Join("\n", gradeLines);
                }
                return Localize("Material_DungeonNone");
            }

            if (material.materialType == MaterialType.Enforce)
            {
                string gradeKey = material.StaticID switch
                {
                    "MAT_ENF_001" => "Material_DungeonAllCommon",
                    "MAT_ENF_002" => "Material_DungeonAllUncommon",
                    "MAT_ENF_003" => "Material_DungeonAllRare",
                    "MAT_ENF_004" => "Material_DungeonAllEpic",
                    "MAT_ENF_005" => "Material_DungeonAllLegendary",
                    _ => null
                };
                return gradeKey != null ? "\n" + Localize(gradeKey) : Localize("Material_DungeonNone");
            }

            var specialDungeon = DataManager.Instance.GetSpecialDropSourceForMaterial(material.StaticID);
            return specialDungeon != null ? "\n" + GetColoredDungeonName(specialDungeon) : Localize("Material_DungeonNone");
        }

        private static string Localize(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private string GetColoredDungeonName(DungeonData dungeon)
        {
            Color color = ColorManager.Instance.GetGradeColor(dungeon.grade);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{dungeon.DisplayName}</color>";
        }

        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel<MaterialDetailPopup>();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
