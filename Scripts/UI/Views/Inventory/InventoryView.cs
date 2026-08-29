using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 인벤토리 UI View - 무기/재료/액티브 아이템 관리
    /// </summary>
    public class InventoryView : BaseView
    {
        [Header("Tab Buttons")]
        [SerializeField] private Toggle weaponTabButton;
        [SerializeField] private Toggle materialTabButton;
        [SerializeField] private Toggle activeItemTabButton;

        [Header("탭 포커스 — 무기")]
        [SerializeField] private GameObject tabFocusWeapon;
        [SerializeField] private TextMeshProUGUI tabTextWeapon;
        [SerializeField] private Image tabIconWeapon;

        [Header("탭 포커스 — 재료")]
        [SerializeField] private GameObject tabFocusMaterial;
        [SerializeField] private TextMeshProUGUI tabTextMaterial;
        [SerializeField] private Image tabIconMaterial;

        [Header("탭 포커스 — 액티브")]
        [SerializeField] private GameObject tabFocusActiveItem;
        [SerializeField] private TextMeshProUGUI tabTextActiveItem;
        [SerializeField] private Image tabIconActiveItem;

        [Header("Tab Panels")]
        [SerializeField] private GameObject weaponPanel;
        [SerializeField] private GameObject materialPanel;
        [SerializeField] private GameObject activeItemPanel;

        [Header("Weapon Tab")]
        [SerializeField] private TextMeshProUGUI weaponCountText;
        [SerializeField] private Toggle showRentedToggle;
        [SerializeField] private ScrollRect weaponScrollRect;
        [SerializeField] private Transform weaponGridContainer;
        [SerializeField] private GameObject weaponCardPrefab;
        [SerializeField] private GameObject rentedToggleOnIndicator;
        [SerializeField] private GameObject rentedToggleOffIndicator;

        [Header("Weapon Type Filter Toggles")]
        [SerializeField] private List<Toggle> weaponTypeFilterToggles;
        [SerializeField] private List<GameObject> weaponTypeFilterOnIndicators;

        [Header("Material Tab")]
        [SerializeField] private ScrollRect materialScrollRect;
        [SerializeField] private Transform materialListContainer;
        [SerializeField] private GameObject materialItemPrefab;

        [Header("Material Type Filter Toggles")]
        [SerializeField] private List<Toggle> materialTypeFilterToggles;
        [SerializeField] private List<GameObject> materialTypeFilterOnIndicators;
        [Header("Active Item Tab")]
        [SerializeField] private ScrollRect activeItemScrollRect;
        [SerializeField] private Transform activeItemListContainer;
        [SerializeField] private GameObject activeItemDisplayPrefab;

        [Header("Active Item Type Filter Toggles")]
        [SerializeField] private List<Toggle> activeItemTypeFilterToggles;
        [SerializeField] private List<GameObject> activeItemTypeFilterOnIndicators;

        [Header("List Empty Text")]
        [SerializeField] private TextMeshProUGUI listEmptyText;

        [Header("Detail Panels")]
        [SerializeField] private GameObject weaponDetailPanel;
        [SerializeField] private GameObject materialDetailPanel;
        [SerializeField] private GameObject activeItemDetailPanel;

        [Header("Empty Detail Panel")]
        [SerializeField] private GameObject emptyDetailPanel;
        [SerializeField] private Image emptyIcon;
        [SerializeField] private TextMeshProUGUI emptyText;

        [Header("Weapon Detail UI")]
        [SerializeField] private Image weaponDetailIcon;
        [SerializeField] private Image weaponDetailIconBG;
        [SerializeField] private Image weaponDetailFrame;
        [SerializeField] private TextMeshProUGUI weaponDetailNameText;
        [SerializeField] private TextMeshProUGUI weaponDetailTypeText;
        [SerializeField] private TextMeshProUGUI weaponDetailEnforceLevelText;
        [SerializeField] private GameObject weaponDetailRentedIndicator;
        [SerializeField] private TextMeshProUGUI weaponDetailStatusText;
        [SerializeField] private Transform weaponDetailEffectListContainer;
        [SerializeField] private WeaponEffectListItem weaponDetailEffectItemPrefab;

        [Header("Material Detail UI")]
        [SerializeField] private Image materialDetailIcon;
        [SerializeField] private Image materialDetailBackground;
        [SerializeField] private Image materialDetailTypeFrame;
        [SerializeField] private TextMeshProUGUI materialDetailNameQuantityText;
        [SerializeField] private TextMeshProUGUI materialDetailTypeText;
        [SerializeField] private TextMeshProUGUI materialDetailDungeonListText;

        [Header("Active Item Detail UI")]
        [SerializeField] private Image activeItemDetailIcon;
        [SerializeField] private Image activeItemDetailBG;
        [SerializeField] private Image activeItemDetailFrame;
        [SerializeField] private TextMeshProUGUI activeItemDetailNameQuantityText;
        [SerializeField] private TextMeshProUGUI activeItemDetailDescriptionText;
        [SerializeField] private TextMeshProUGUI activeItemDetailTypeText;

        [Header("Close Button")]
        [SerializeField] private Button closeButton;

        [Header("Controller")]
        [SerializeField] private InventoryController inventoryController;

        private InventoryController Controller => inventoryController;

        // 카드/아이템 리스트
        private List<WeaponInventoryCardItem> weaponCards = new List<WeaponInventoryCardItem>();
        private List<MaterialInventoryCardItem> materialItems = new List<MaterialInventoryCardItem>();
        private List<ActiveItemInventoryCard> activeItemCards = new List<ActiveItemInventoryCard>();

        // 선택 카드 추적
        private WeaponInventoryCardItem selectedWeaponCard;
        private MaterialInventoryCardItem selectedMaterialCard;
        private ActiveItemInventoryCard selectedActiveItemCard;

        public WeaponInstance SelectedWeapon => selectedWeaponCard?.Weapon;

        public enum InventoryTab
        {
            Weapon,
            Material,
            ActiveItem
        }

        public InventoryTab currentTab = InventoryTab.Weapon;

        #region 초기화

        // Initialize()의 토글 세팅이 btn_clicked로 잘못 집계되지 않도록 하는 억제 플래그
        private bool suppressClickAnalytics;

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;
            isCanClickOverlay = true;
            canEscape = false;
        }

        protected override void SubscribeEvents()
        {
            weaponTabButton?.onValueChanged.AddListener((value) => { if (value) OnTabClicked(InventoryTab.Weapon); });
            materialTabButton?.onValueChanged.AddListener((value) => { if (value) OnTabClicked(InventoryTab.Material); });
            activeItemTabButton?.onValueChanged.AddListener((value) => { if (value) OnTabClicked(InventoryTab.ActiveItem); });

            showRentedToggle?.onValueChanged.AddListener(OnShowRentedToggleChanged);

            SubscribeFilterToggles(weaponTypeFilterToggles, weaponTypeFilterOnIndicators, index => Controller?.OnWeaponTypeFilterChanged(index));
            SubscribeFilterToggles(materialTypeFilterToggles, materialTypeFilterOnIndicators, index => Controller?.OnMaterialTypeFilterChanged(index));
            SubscribeFilterToggles(activeItemTypeFilterToggles, activeItemTypeFilterOnIndicators, index => Controller?.OnActiveItemTypeFilterChanged(index));

            closeButton?.onClick.AddListener(HandleBackRequest);
        }

        protected override void UnsubscribeEvents()
        {
            weaponTabButton?.onValueChanged.RemoveAllListeners();
            materialTabButton?.onValueChanged.RemoveAllListeners();
            activeItemTabButton?.onValueChanged.RemoveAllListeners();

            showRentedToggle?.onValueChanged.RemoveAllListeners();

            UnsubscribeFilterToggles(weaponTypeFilterToggles);
            UnsubscribeFilterToggles(materialTypeFilterToggles);
            UnsubscribeFilterToggles(activeItemTypeFilterToggles);

            closeButton?.onClick.RemoveAllListeners();
        }

        private void SubscribeFilterToggles(List<Toggle> toggles, List<GameObject> OnIndicators, System.Action<int> onSelected)
        {
            if (toggles == null) return;
            for (int i = 0; i < toggles.Count; i++)
            {
                int index = i;
                toggles[i]?.onValueChanged.AddListener(on => { if (on) onSelected(index); });
                toggles[i]?.onValueChanged.AddListener(isOn => OnFilterToggleChanged(isOn, OnIndicators?[index]));
            }
        }

        private void UnsubscribeFilterToggles(List<Toggle> toggles)
        {
            if (toggles == null) return;
            foreach (var toggle in toggles)
                toggle?.onValueChanged.RemoveAllListeners();
        }

        public void Initialize()
        {
            // 초기화용 토글 세팅이 사용자 클릭으로 집계되지 않도록 억제한다(패널을 열 때마다 발생).
            suppressClickAnalytics = true;

            ResetAllFilterToggles();

            showRentedToggle.isOn = false;

            materialTabButton.isOn = false;
            activeItemTabButton.isOn = false;
            weaponTabButton.isOn = true;

            OnTabClicked(InventoryTab.Weapon);

            suppressClickAnalytics = false;
        }

        private void ResetAllFilterToggles()
        {
            ResetFilterToggles(weaponTypeFilterToggles, weaponTypeFilterOnIndicators);
            ResetFilterToggles(materialTypeFilterToggles, materialTypeFilterOnIndicators);
            ResetFilterToggles(activeItemTypeFilterToggles, activeItemTypeFilterOnIndicators);
        }

        private void ResetFilterToggles(List<Toggle> toggles, List<GameObject> onIndicators)
        {
            if (toggles == null) return;
            for (int i = 0; i < toggles.Count; i++)
            {
                bool shouldBeOn = (i == 0);
                toggles[i]?.SetIsOnWithoutNotify(shouldBeOn);
                if (onIndicators != null && i < onIndicators.Count)
                    onIndicators[i]?.SetActive(shouldBeOn);
            }
        }

#endregion

        #region 탭 관리

        private void OnTabClicked(InventoryTab tab)
        {
            currentTab = tab;

            if (!suppressClickAnalytics)
                AnalyticsManager.Instance?.SendButtonClick("inventory", GetTabAnalyticsName(tab));

            weaponPanel?.SetActive(tab == InventoryTab.Weapon);
            materialPanel?.SetActive(tab == InventoryTab.Material);
            activeItemPanel?.SetActive(tab == InventoryTab.ActiveItem);

            ApplyTabFocus(tab == InventoryTab.Weapon,     tabFocusWeapon,     tabTextWeapon,     tabIconWeapon);
            ApplyTabFocus(tab == InventoryTab.Material,   tabFocusMaterial,   tabTextMaterial,   tabIconMaterial);
            ApplyTabFocus(tab == InventoryTab.ActiveItem, tabFocusActiveItem, tabTextActiveItem, tabIconActiveItem);

            weaponDetailPanel?.SetActive(false);
            materialDetailPanel?.SetActive(false);
            activeItemDetailPanel?.SetActive(false);

            weaponCountText?.gameObject.SetActive(tab == InventoryTab.Weapon);
            showRentedToggle?.gameObject.SetActive(tab == InventoryTab.Weapon);

            ClearSelection();

            ResetAllFilterToggles();
            Controller?.ResetFilters();

            switch (tab)
            {
                case InventoryTab.Weapon:
                    Controller?.RefreshWeaponTab();
                    break;
                case InventoryTab.Material:
                    Controller?.RefreshMaterialTab();
                    break;
                case InventoryTab.ActiveItem:
                    Controller?.RefreshActiveItemTab();
                    break;
            }

            RefreshEmptyDetailPanel();
        }

        /// <summary>btn_clicked의 button 값 (Documents/Analytics_이벤트_설계.md Level 3).</summary>
        private static string GetTabAnalyticsName(InventoryTab tab)
        {
            switch (tab)
            {
                case InventoryTab.Material: return "tab_material";
                case InventoryTab.ActiveItem: return "tab_active_item";
                default: return "tab_weapon";
            }
        }

        private void ApplyTabFocus(bool on, GameObject focus, TextMeshProUGUI text, Image icon)
        {
            focus?.SetActive(on);
            Color c = ColorManager.Instance.GetSubTabColor(on);
            if (text != null) text.color = c;
            if (icon != null) icon.color = c;
        }

        // 목록을 다시 그리면 기존 카드가 파괴되므로 선택 상태는 항상 초기화한다.
        // 갱신 후에도 선택을 이어가려면 호출 측에서 ReselectWeapon()으로 다시 지정한다.
        public void UpdateWeaponGrid(List<WeaponInstance> weapons)
        {
            ClearWeaponCards();
            ClearWeaponSelection();
            weaponDetailPanel?.SetActive(false);
            RefreshEmptyDetailPanel();

            if (weapons == null || weapons.Count == 0)
            {
                bool allTabSelected = weaponTypeFilterToggles != null
                    && weaponTypeFilterToggles.Count > 0
                    && weaponTypeFilterToggles[0].isOn;
                ShowListEmptyText(Localize(allTabSelected ? "Inventory_EmptyWeaponAll" : "Inventory_EmptyWeaponFiltered"));
                return;
            }

            ShowListEmptyText(null);

            foreach (var weapon in weapons)
            {
                GameObject cardObj = Instantiate(weaponCardPrefab, weaponGridContainer);
                WeaponInventoryCardItem card = cardObj.GetComponentOrNull<WeaponInventoryCardItem>();

                if (card != null)
                {
                    card.Initialize(weapon);
                    card.OnCardClicked += OnWeaponCardClicked;
                    weaponCards.Add(card);
                }
            }

            // 위 empty 분기와 동일하게 배열/원소 null 가드
            if (weaponTypeFilterToggles != null && weaponTypeFilterToggles.Count > 0
                && weaponTypeFilterToggles[0] != null && !weaponTypeFilterToggles[0].isOn)
                weaponScrollRect.ResetPosition(this);
        }

        public void UpdateWeaponTab(int ownedWeaponCount)
        {
            if (weaponCountText != null)
                weaponCountText.text = $"{ownedWeaponCount}/{InventoryManager.Instance.GetMaxWeaponCount()}";
        }

        private void ClearWeaponCards()
        {
            foreach (var card in weaponCards)
            {
                if (card != null)
                {
                    card.OnCardClicked -= OnWeaponCardClicked;
                    Destroy(card.gameObject);
                }
            }
            weaponCards.Clear();
        }

        public void UpdateMaterialList(Dictionary<string, int> materials)
        {
            ClearMaterialItems();
            materialDetailPanel?.SetActive(false);

            if (materials == null || materials.Count == 0)
            {
                bool allTabSelected = materialTypeFilterToggles != null
                    && materialTypeFilterToggles.Count > 0
                    && materialTypeFilterToggles[0].isOn;
                ShowListEmptyText(Localize(allTabSelected ? "Inventory_EmptyMaterialAll" : "Inventory_EmptyMaterialFiltered"));
                return;
            }

            ShowListEmptyText(null);

            foreach (var kvp in materials)
            {
                MaterialData materialData = DataManager.Instance.GetMaterial(kvp.Key);
                if (materialData == null) continue;

                GameObject itemObj = Instantiate(materialItemPrefab, materialListContainer);
                MaterialInventoryCardItem item = itemObj.GetComponentOrNull<MaterialInventoryCardItem>();

                if (item != null)
                {
                    item.Initialize(materialData, kvp.Value);
                    item.OnCardClicked += OnMaterialCardClicked;
                    materialItems.Add(item);
                }
            }

            materialScrollRect.ResetPosition(this);
        }

        private void ClearMaterialItems()
        {
            foreach (var item in materialItems)
            {
                if (item != null)
                {
                    item.OnCardClicked -= OnMaterialCardClicked;
                    Destroy(item.gameObject);
                }
            }
            materialItems.Clear();
        }

        public void UpdateActiveItemList(List<ActiveItemInstance> items)
        {
            ClearActiveItemCards();
            activeItemDetailPanel?.SetActive(false);

            if (items == null || items.Count == 0)
            {
                bool allTabSelected = activeItemTypeFilterToggles != null
                    && activeItemTypeFilterToggles.Count > 0
                    && activeItemTypeFilterToggles[0].isOn;
                ShowListEmptyText(Localize(allTabSelected ? "Inventory_EmptyGiftAll" : "Inventory_EmptyGiftFiltered"));
                return;
            }

            ShowListEmptyText(null);

            foreach (var item in items)
            {
                GameObject cardObj = Instantiate(activeItemDisplayPrefab, activeItemListContainer);
                ActiveItemInventoryCard card = cardObj.GetComponentOrNull<ActiveItemInventoryCard>();

                if (card != null)
                {
                    card.Initialize(item);
                    card.OnCardClicked += OnActiveItemCardClicked;
                    activeItemCards.Add(card);
                }
            }

            activeItemScrollRect.ResetPosition(this);
        }

        private void ClearActiveItemCards()
        {
            foreach (var card in activeItemCards)
            {
                if (card != null)
                {
                    card.OnCardClicked -= OnActiveItemCardClicked;
                    Destroy(card.gameObject);
                }
            }
            activeItemCards.Clear();
        }

        // 공용 빈 목록 텍스트. message가 null이면 숨기고, 값이 있으면 해당 문구로 표시한다.
        private void ShowListEmptyText(string message)
        {
            if (listEmptyText == null) return;

            if (string.IsNullOrEmpty(message))
            {
                listEmptyText.gameObject.SetActive(false);
                return;
            }

            listEmptyText.text = message;
            listEmptyText.gameObject.SetActive(true);
        }

        #endregion

        #region 필터 토글 핸들러

        private void OnFilterToggleChanged(bool isOn, GameObject OnIndicator)
        {
            OnIndicator?.SetActive(isOn);
        }

        #endregion

        #region 카드 선택 관리

        private void SelectWeaponCard(WeaponInventoryCardItem card)
        {
            if (selectedWeaponCard != null) selectedWeaponCard.SetSelected(false);
            selectedWeaponCard = card;
            if (selectedWeaponCard != null) selectedWeaponCard.SetSelected(true);
            weaponDetailPanel?.SetActive(true);
            ShowWeaponDetail(card.Weapon);
            RefreshEmptyDetailPanel();
        }

        private void SelectMaterialCard(MaterialInventoryCardItem card)
        {
            if (selectedMaterialCard != null) selectedMaterialCard.SetSelected(false);
            selectedMaterialCard = card;
            if (selectedMaterialCard != null) selectedMaterialCard.SetSelected(true);
            materialDetailPanel?.SetActive(true);
            ShowMaterialDetail(card.MaterialData, card.Quantity);
            RefreshEmptyDetailPanel();
        }

        private void SelectActiveItemCard(ActiveItemInventoryCard card)
        {
            if (selectedActiveItemCard != null) selectedActiveItemCard.SetSelected(false);
            selectedActiveItemCard = card;
            if (selectedActiveItemCard != null) selectedActiveItemCard.SetSelected(true);
            activeItemDetailPanel?.SetActive(true);
            ShowActiveItemDetail(card.Item);
            RefreshEmptyDetailPanel();
        }

        private void ClearWeaponSelection()
        {
            if (selectedWeaponCard != null) selectedWeaponCard.SetSelected(false);
            selectedWeaponCard = null;
        }

        private void ClearSelection()
        {
            ClearWeaponSelection();
            if (selectedMaterialCard != null) selectedMaterialCard.SetSelected(false);
            selectedMaterialCard = null;
            if (selectedActiveItemCard != null) selectedActiveItemCard.SetSelected(false);
            selectedActiveItemCard = null;
        }

        /// <summary>
        /// 목록 갱신 후 이전 선택을 복원한다.
        /// 필터 결과에서 빠져 카드가 없으면 UpdateWeaponGrid가 만들어 둔 해제 상태를 그대로 유지한다.
        /// </summary>
        public void ReselectWeapon(WeaponInstance weapon)
        {
            if (weapon == null) return;

            WeaponInventoryCardItem cardToSelect = weaponCards.FirstOrDefault(c => c.Weapon == weapon);
            if (cardToSelect != null)
                SelectWeaponCard(cardToSelect);
        }

        #endregion

        #region 카드 클릭 핸들러

        // 인벤토리 상세는 인라인 패널이라 panel_opened를 타지 않는다. btn_clicked로 대체 추적한다.
        // 갱신용 재선택(ReselectWeapon)이 이벤트를 발행하지 않도록 Select*Card가 아닌 클릭 핸들러에서 발행한다.
        private void OnWeaponCardClicked(WeaponInventoryCardItem card)
        {
            AnalyticsManager.Instance?.SendButtonClick("inventory", "weapon_item_click", new Dictionary<string, object>
            {
                { "weapon_id", card.Weapon.weaponData.StaticID },
                { "weapon_grade", (int)card.Weapon.currentGrade }
            });

            SelectWeaponCard(card);
            Controller?.OnWeaponCardSelected();
        }

        private void OnMaterialCardClicked(MaterialInventoryCardItem card)
        {
            AnalyticsManager.Instance?.SendButtonClick("inventory", "material_item_click", new Dictionary<string, object>
            {
                { "material_id", card.MaterialData.StaticID }
            });

            SelectMaterialCard(card);
        }

        private void OnActiveItemCardClicked(ActiveItemInventoryCard card)
        {
            AnalyticsManager.Instance?.SendButtonClick("inventory", "active_item_click", new Dictionary<string, object>
            {
                { "item_id", card.Item.StaticID }
            });

            SelectActiveItemCard(card);
        }

        #endregion

        #region 내장 Detail 패널 업데이트

        private void ShowWeaponDetail(WeaponInstance weapon)
        {
            if (weapon == null) return;

            if (weaponDetailIconBG != null)
                weaponDetailIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);

            if (weaponDetailFrame != null)
            {
                weaponDetailFrame.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            }

            if (weaponDetailIcon != null && weapon.weaponData.icon != null)
                weaponDetailIcon.sprite = weapon.weaponData.icon;

            if (weaponDetailNameText != null)
            {
                string enforceLevel = weapon.enforceLevel > 0 ? $"+{weapon.enforceLevel} " : "";
                weaponDetailNameText.text = weapon.weaponData.DisplayName + enforceLevel;
                weaponDetailNameText.color = ColorManager.Instance.GetGradeAccentColor(weapon.currentGrade);
            }

            if (weaponDetailEnforceLevelText != null)
                weaponDetailEnforceLevelText.text = $"+{weapon.enforceLevel}";

            if (weaponDetailTypeText != null)
                weaponDetailTypeText.text = UITranslator.GetString(weapon.weaponData.weaponType);

            weaponDetailRentedIndicator?.SetActive(weapon.isRented);

            if (weaponDetailStatusText != null)
            {
                string statusText = "";
                if (weapon.isRented)
                {
                    var adventurer = VisitorManager.Instance.GetAdventurerInstance(weapon.rentedToAdventurerID);
                    if (adventurer != null)
                        statusText = LocalizationSettings.StringDatabase.GetLocalizedString(
                            "UI_Screens", "Inventory_RentedBy",
                            arguments: new object[] { new Dictionary<string, object> { { "name", adventurer.Name } } });
                }
                weaponDetailStatusText.text = statusText;
            }

            RefreshWeaponDetailEffectList(weapon);
        }

        private void RefreshWeaponDetailEffectList(WeaponInstance weapon)
            => WeaponEffectListItem.Rebuild(weaponDetailEffectListContainer, weaponDetailEffectItemPrefab, weapon.effects);

        private void ShowMaterialDetail(MaterialData material, int quantity)
        {
            if (material == null) return;

            if (materialDetailIcon != null && material.icon != null)
                materialDetailIcon.sprite = material.icon;

            if (materialDetailBackground != null)
            {                
                materialDetailBackground.color = ColorManager.Instance.GetGradeCardBackgroundColor(material.grade);
            }

            if (materialDetailTypeFrame != null)
                materialDetailTypeFrame.sprite = IconManager.Instance.GetFrameByGrade(material.grade);

            if (materialDetailNameQuantityText != null)
            {
                materialDetailNameQuantityText.color = ColorManager.Instance.GetGradeAccentColor(material.grade);
                string whiteHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetWhiteColor());
                materialDetailNameQuantityText.text = $"{material.DisplayName} <color={whiteHex}>x{quantity}</color>";
            }

            if (materialDetailTypeText != null)
                materialDetailTypeText.text = UITranslator.GetString(material.materialType);

            if (materialDetailDungeonListText != null)
                materialDetailDungeonListText.text = BuildDungeonListText(material);
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

        private void ShowActiveItemDetail(ActiveItemData data)
        {
            if (data == null) return;

            Grade grade = data.usageContext.ToGrade();

            if (activeItemDetailIcon != null && data.icon != null)
                activeItemDetailIcon.sprite = data.icon;

            if (activeItemDetailBG != null)
                activeItemDetailBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);

            if (activeItemDetailFrame != null)
                activeItemDetailFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);

            if (activeItemDetailNameQuantityText != null)
            {
                activeItemDetailNameQuantityText.color = ColorManager.Instance.GetGradeAccentColor(grade);
                int quantity = InventoryManager.Instance.GetActiveItemByDataID(data.StaticID).quantity;
                string whiteHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetWhiteColor());
                activeItemDetailNameQuantityText.text = $"{data.DisplayName} <color={whiteHex}>x{quantity}</color>";
            }

            if (activeItemDetailDescriptionText != null)
                activeItemDetailDescriptionText.text = data.DisplayDescription;

            if (activeItemDetailTypeText != null)
                activeItemDetailTypeText.text = UITranslator.GetString(data.itemType);
        }

        // 상세 패널이 하나도 열려 있지 않으면 현재 탭에 맞는 빈 상세 패널을 표시한다.
        private void RefreshEmptyDetailPanel()
        {
            bool anyDetailOpen =
                (weaponDetailPanel != null && weaponDetailPanel.activeSelf) ||
                (materialDetailPanel != null && materialDetailPanel.activeSelf) ||
                (activeItemDetailPanel != null && activeItemDetailPanel.activeSelf);

            emptyDetailPanel?.SetActive(!anyDetailOpen);

            if (anyDetailOpen) return;

            if (emptyIcon != null)
                emptyIcon.sprite = IconManager.Instance.GetIconByInventoryTab(currentTab);

            if (emptyText != null)
                emptyText.text = Localize(GetTabSelectKey(currentTab));
        }

        // 조사(를/을)가 단어에 붙어 있어 문장 전체를 탭별 키로 나눈다 (조합하면 다른 언어에서 어순이 깨진다)
        private string GetTabSelectKey(InventoryTab tab)
        {
            return tab switch
            {
                InventoryTab.Weapon     => "Inventory_SelectWeapon",
                InventoryTab.Material   => "Inventory_SelectMaterial",
                InventoryTab.ActiveItem => "Inventory_SelectGift",
                _                       => "Inventory_SelectItem"
            };
        }

        #endregion

        #region 이벤트 핸들러

        private void OnShowRentedToggleChanged(bool isOn)
        {
            if (!suppressClickAnalytics)
            {
                AnalyticsManager.Instance?.SendButtonClick("inventory", "show_rented_toggle", new Dictionary<string, object>
                {
                    { "is_on", isOn }
                });
            }

            Controller?.OnShowRentedToggleChanged(isOn);
            rentedToggleOnIndicator?.SetActive(isOn);
            rentedToggleOffIndicator?.SetActive(!isOn);
        }

        private void OnCloseClicked()
        {
            Controller?.OnInventoryCloseClicked();
        }

        // 닫기 버튼·ESC 공통 진입점: 열린 인라인 상세 패널이 있으면 그것만 닫고, 없으면 인벤토리를 닫는다.
        private void HandleBackRequest()
        {
            OnCloseClicked();
        }

        public override void OnEscapeCancelled() => HandleBackRequest();

        #endregion

        #region 튜토리얼 하이라이트 접근자

        /// <summary>튜토리얼 하이라이트용(7단계) - '대여중' 토글 RectTransform.</summary>
        public RectTransform GetShowRentedToggleRect() => showRentedToggle?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트용(7단계) - 닫기 버튼 RectTransform.</summary>
        public RectTransform GetCloseButtonRect() => closeButton?.transform as RectTransform;

        /// <summary>
        /// 튜토리얼 하이라이트용(7단계) - 대여 중인 첫 무기 카드 RectTransform.
        /// 대여중 토글 ON 직후 목록이 새로고침되며 GridLayout 배치가 다음 패스에 반영되므로, 위치를 읽기 전에 즉시 리빌드한다.
        /// </summary>
        public RectTransform GetRentedWeaponCardRect()
        {
            if (weaponGridContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            var card = weaponCards.FirstOrDefault(c => c != null && c.Weapon != null && c.Weapon.isRented);
            return card?.transform as RectTransform;
        }

        #endregion
    }
}
