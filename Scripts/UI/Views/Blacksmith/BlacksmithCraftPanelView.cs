using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 대장간 제작 패널 View
    /// 좌측: 레시피 타입 토글 + WeaponType/ActiveItemType별 필터 + 레시피 스크롤뷰
    /// 우측: 선택된 레시피의 ItemDetailPanel (고정 표시)
    /// </summary>
    public class BlacksmithCraftPanelView : MonoBehaviour
    {
        [Header("Recipe Type Toggle")]
        [Tooltip("Off: 무기 레시피, On: 활성 아이템 레시피")]
        [SerializeField] private Toggle recipeTypeToggle;
        [SerializeField] private Image recipeTypeIcon;

        [Header("Weapon Recipe Panel")]
        [SerializeField] private GameObject weaponRecipePanel;
        [Tooltip("[0]=All, [1]=Sword, [2]=Axe, [3]=Bow, [4]=Crossbow, [5]=Staff, [6]=Tome, [7]=Dagger, [8]=Shuriken")]
        [SerializeField] private Toggle[] weaponTypeFilterToggles;
        [Tooltip("weaponTypeFilterToggles와 동일 인덱스로 대응하는 Focus 오브젝트")]
        [SerializeField] private GameObject[] weaponTypeFocusObjects;
        [SerializeField] private ScrollRect weaponRecipeScrollRect;
        [SerializeField] private Transform weaponRecipeContainer;

        [Header("ActiveItem Recipe Panel")]
        [SerializeField] private GameObject activeItemRecipePanel;
        [Tooltip("[0]=All, [1]~[N]=ActiveItemType 순서")]
        [SerializeField] private Toggle[] activeItemTypeFilterToggles;
        [Tooltip("activeItemTypeFilterToggles와 동일 인덱스로 대응하는 Focus 오브젝트")]
        [SerializeField] private GameObject[] activeItemTypeFocusObjects;
        [SerializeField] private ScrollRect activeItemRecipeScrollRect;
        [SerializeField] private Transform activeItemRecipeContainer;

        [Header("Recipe Item Prefab")]
        [SerializeField] private GameObject recipeItemPrefab;

        [Header("Item Detail Panel")]
        [SerializeField] private TextMeshProUGUI itemTypeText;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [Tooltip("활성 아이템 레시피 선택 시에만 효과 설명을 표시한다.")]
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private Image itemIconImage;
        [SerializeField] private Image itemBGColor;
        [SerializeField] private Image itemFrameColor;
        [SerializeField] private TextMeshProUGUI goldCostText;
        [Tooltip("골드 아이콘 + 골드 비용 텍스트 묶음. 제작 가능 시 활성화.")]
        [SerializeField] private GameObject goldCostGroup;
        [Tooltip("제작 차단 사유 묶음. 제작 불가 시 활성화.")]
        [SerializeField] private GameObject banGroup;
        [Tooltip("banGroup 안의 사유 텍스트.")]
        [SerializeField] private TextMeshProUGUI banText;
        [SerializeField] private GameObject requiredMaterialPanel;
        [SerializeField] private Transform requiredMaterialContainer;
        [SerializeField] private GameObject requiredMaterialItemPrefab;
        [Tooltip("개수를 충족한 재료 종류 수 / 전체 재료 종류 수")]
        [SerializeField] private TextMeshProUGUI materialSatisfiedText;
        [SerializeField] private Button craftButton;

        private BlacksmithController controller;

        private enum RecipeTab { Weapon, ActiveItem }
        private RecipeTab currentRecipeTab = RecipeTab.Weapon;

        // 현재 선택된 레시피
        private WeaponRecipeData selectedWeaponRecipe;
        private ActiveItemRecipeData selectedItemRecipe;

        // 현재 선택된 카드
        private BlacksmithRecipeItemCard currentSelectedCard;

        // 타입 필터
        private WeaponType? currentWeaponTypeFilter = null;
        private ActiveItemType? currentActiveItemTypeFilter = null;

        // 현재 목록에 표시 중인 카드 → 레시피 StaticID (캐시 갱신 시 도트만 갱신용)
        private readonly Dictionary<BlacksmithRecipeItemCard, string> cardRecipeIDs = new Dictionary<BlacksmithRecipeItemCard, string>();

        // itemNameText는 레시피 등급에 따라 색이 바뀌므로, 빈 상태로 되돌릴 때 사용할 인스펙터 원래 색을 최초 1회 캐싱
        private Color defaultNameColor;
        private bool defaultNameColorCached = false;

        #region 초기화

        private void Awake()
        {
            craftButton?.onClick.AddListener(OnCraftButtonClicked);
        }

        private void OnDestroy()
        {
            if (BlacksmithManager.Instance != null)
                BlacksmithManager.Instance.OnCraftableCacheChanged -= OnCraftableCacheChanged;

            craftButton?.onClick.RemoveAllListeners();
            recipeTypeToggle?.onValueChanged.RemoveAllListeners();

            if (weaponTypeFilterToggles != null)
            {
                foreach (var toggle in weaponTypeFilterToggles)
                    toggle?.onValueChanged.RemoveAllListeners();
            }

            if (activeItemTypeFilterToggles != null)
            {
                foreach (var toggle in activeItemTypeFilterToggles)
                    toggle?.onValueChanged.RemoveAllListeners();
            }
        }

        public void Initialize(BlacksmithController ctrl)
        {
            controller = ctrl;

            SetActiveItemScrollLocked(false);   // 튜토리얼 잔여 잠금 방어

            if (!defaultNameColorCached && itemNameText != null)
            {
                defaultNameColor = itemNameText.color;
                defaultNameColorCached = true;
            }

            // 패널 최초 열림 시 제작 가능 캐시 재계산 + 갱신 구독 (중복 방지)
            BlacksmithManager.Instance.OnCraftableCacheChanged -= OnCraftableCacheChanged;
            BlacksmithManager.Instance.OnCraftableCacheChanged += OnCraftableCacheChanged;
            BlacksmithManager.Instance.RecalculateCraftableCache();

            selectedWeaponRecipe = null;
            selectedItemRecipe = null;
            ShowEmptyDetail();

            // 레시피 타입 토글 이벤트 (Initialize 중복 방지)
            recipeTypeToggle?.onValueChanged.RemoveAllListeners();
            recipeTypeToggle?.onValueChanged.AddListener(isOn =>
                SwitchRecipeTab(isOn ? RecipeTab.ActiveItem : RecipeTab.Weapon));

            SubscribeWeaponTypeFilterToggles();
            SubscribeActiveItemTypeFilterToggles();

            // 무기 탭으로 시작
            currentRecipeTab = RecipeTab.Weapon;
            if (recipeTypeToggle != null) recipeTypeToggle.isOn = false;
            SwitchRecipeTab(RecipeTab.Weapon);
        }

        private void SubscribeWeaponTypeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;

            var weaponTypes = (WeaponType[])System.Enum.GetValues(typeof(WeaponType));
            for (int i = 0; i < weaponTypeFilterToggles.Length; i++)
            {
                var toggle = weaponTypeFilterToggles[i];
                if (toggle == null) continue;

                toggle.onValueChanged.RemoveAllListeners();

                int capturedIndex = i;
                if (i == 0)
                {
                    // All 토글
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        SetWeaponFocusObject(capturedIndex, isOn);
                        if (!isOn) return;
                        currentWeaponTypeFilter = null;
                        RefreshWeaponRecipeList();
                        UpdateRecipeTypeIcon();
                    });
                }
                else
                {
                    int typeIndex = i - 1;
                    if (typeIndex < weaponTypes.Length)
                    {
                        WeaponType type = weaponTypes[typeIndex];
                        toggle.onValueChanged.AddListener(isOn =>
                        {
                            SetWeaponFocusObject(capturedIndex, isOn);
                            if (!isOn) return;
                            currentWeaponTypeFilter = type;
                            RefreshWeaponRecipeList();
                            UpdateRecipeTypeIcon();
                        });
                    }
                }
            }
        }

        private void SetWeaponFocusObject(int index, bool isOn)
        {
            if (weaponTypeFocusObjects == null || index >= weaponTypeFocusObjects.Length) return;
            weaponTypeFocusObjects[index]?.SetActive(isOn);
        }

        private void SubscribeActiveItemTypeFilterToggles()
        {
            if (activeItemTypeFilterToggles == null) return;

            var itemTypes = (ActiveItemType[])System.Enum.GetValues(typeof(ActiveItemType));
            for (int i = 0; i < activeItemTypeFilterToggles.Length; i++)
            {
                var toggle = activeItemTypeFilterToggles[i];
                if (toggle == null) continue;

                toggle.onValueChanged.RemoveAllListeners();

                int capturedIndex = i;
                if (i == 0)
                {
                    // All 토글
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        SetActiveItemFocusObject(capturedIndex, isOn);
                        if (!isOn) return;
                        currentActiveItemTypeFilter = null;
                        RefreshActiveItemRecipeList();
                        UpdateRecipeTypeIcon();
                    });
                }
                else
                {
                    int typeIndex = i - 1;
                    if (typeIndex < itemTypes.Length)
                    {
                        ActiveItemType type = itemTypes[typeIndex];
                        toggle.onValueChanged.AddListener(isOn =>
                        {
                            SetActiveItemFocusObject(capturedIndex, isOn);
                            if (!isOn) return;
                            currentActiveItemTypeFilter = type;
                            RefreshActiveItemRecipeList();
                            UpdateRecipeTypeIcon();
                        });
                    }
                }
            }
        }

        private void SetActiveItemFocusObject(int index, bool isOn)
        {
            if (activeItemTypeFocusObjects == null || index >= activeItemTypeFocusObjects.Length) return;
            activeItemTypeFocusObjects[index]?.SetActive(isOn);
        }

        #endregion

        #region 레시피 탭 전환

        private void SwitchRecipeTab(RecipeTab tab)
        {
            currentRecipeTab = tab;

            // 선택 초기화
            if (currentSelectedCard != null)
                currentSelectedCard.SetSelected(false);
            currentSelectedCard = null;
            selectedWeaponRecipe = null;
            selectedItemRecipe = null;
            ShowEmptyDetail();

            weaponRecipePanel?.SetActive(tab == RecipeTab.Weapon);
            activeItemRecipePanel?.SetActive(tab == RecipeTab.ActiveItem);

            if (tab == RecipeTab.Weapon)
            {
                currentWeaponTypeFilter = null;
                if (weaponTypeFilterToggles != null && weaponTypeFilterToggles.Length > 0 && weaponTypeFilterToggles[0] != null)
                    weaponTypeFilterToggles[0].isOn = true;
                RefreshWeaponRecipeList();
            }
            else
            {
                currentActiveItemTypeFilter = null;
                if (activeItemTypeFilterToggles != null && activeItemTypeFilterToggles.Length > 0 && activeItemTypeFilterToggles[0] != null)
                    activeItemTypeFilterToggles[0].isOn = true;
                RefreshActiveItemRecipeList();
            }

            UpdateRecipeTypeIcon();

            // 튜토리얼 2-B: ActiveItem 탭이 열리면(목록 생성 완료) 부적 레시피 카드 하이라이트로 진행
            if (tab == RecipeTab.ActiveItem
                && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialCraftTabToActiveItem();
        }

        private void UpdateRecipeTypeIcon()
        {
            if (recipeTypeIcon == null) return;

            Sprite icon = currentRecipeTab == RecipeTab.Weapon ? IconManager.Instance?.GetIconByInventoryTab(InventoryView.InventoryTab.Weapon) : IconManager.Instance?.GetIconByInventoryTab(InventoryView.InventoryTab.ActiveItem);

            if (icon != null)
            {
                recipeTypeIcon.sprite = icon;
                recipeTypeIcon.gameObject.SetActive(true);
            }
        }

        #endregion

        #region 레시피 목록

        private void RefreshWeaponRecipeList()
        {
            if (weaponRecipeContainer == null) return;

            currentSelectedCard = null;
            cardRecipeIDs.Clear();
            for (int i = weaponRecipeContainer.childCount - 1; i >= 0; i--)
            {
                var child = weaponRecipeContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            var recipes = DataManager.Instance.GetAllWeaponRecipes();

            if (currentWeaponTypeFilter.HasValue)
                recipes = recipes.Where(r => r.resultWeapon.weaponType == currentWeaponTypeFilter.Value).ToList();

            recipes = recipes
                .OrderByDescending(r => r.resultWeapon.baseGrade)
                .ThenBy(r => r.resultWeapon.weaponType)
                .ToList();

            foreach (var recipe in recipes)
                CreateWeaponRecipeItem(recipe);

            // 선택된 레시피가 필터된 목록에 없으면 detail 숨김
            if (currentSelectedCard == null)
                ShowEmptyDetail();

            weaponRecipeScrollRect?.ResetPosition(this);
        }

        private void RefreshActiveItemRecipeList()
        {
            if (activeItemRecipeContainer == null) return;

            currentSelectedCard = null;
            cardRecipeIDs.Clear();
            for (int i = activeItemRecipeContainer.childCount - 1; i >= 0; i--)
            {
                var child = activeItemRecipeContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            var recipes = DataManager.Instance.GetAllActiveItemRecipes();

            if (currentActiveItemTypeFilter.HasValue)
                recipes = recipes.Where(r => r.resultItem.itemType == currentActiveItemTypeFilter.Value).ToList();

            recipes = recipes
                .OrderByDescending(r => r.resultItem.itemType)
                .ToList();

            foreach (var recipe in recipes)
                CreateActiveItemRecipeItem(recipe);

            // 선택된 레시피가 필터된 목록에 없으면 detail 숨김
            if (currentSelectedCard == null)
                ShowEmptyDetail();

            activeItemRecipeScrollRect?.ResetPosition(this);
        }

        private void CreateWeaponRecipeItem(WeaponRecipeData recipe)
        {
            if (recipeItemPrefab == null || weaponRecipeContainer == null) return;

            var obj = Instantiate(recipeItemPrefab, weaponRecipeContainer);
            var card = obj.GetOrAddComponent<BlacksmithRecipeItemCard>();
            bool isCraftable = BlacksmithManager.Instance.IsRecipeCraftable(recipe.StaticID);
            card.Initialize(recipe.resultWeapon.icon, () => OnWeaponRecipeSelected(recipe, card), isCraftable, recipe.resultWeapon.baseGrade);
            cardRecipeIDs[card] = recipe.StaticID;

            if (selectedWeaponRecipe == recipe)
            {
                currentSelectedCard = card;
                card.SetSelected(true);
            }
        }

        private void CreateActiveItemRecipeItem(ActiveItemRecipeData recipe)
        {
            if (recipeItemPrefab == null || activeItemRecipeContainer == null) return;

            var obj = Instantiate(recipeItemPrefab, activeItemRecipeContainer);
            var card = obj.GetOrAddComponent<BlacksmithRecipeItemCard>();
            bool isCraftable = BlacksmithManager.Instance.IsRecipeCraftable(recipe.StaticID);
            card.Initialize(recipe.resultItem.icon, () => OnActiveItemRecipeSelected(recipe, card), isCraftable, recipe.resultItem.usageContext.ToGrade());
            cardRecipeIDs[card] = recipe.StaticID;

            if (selectedItemRecipe == recipe)
            {
                currentSelectedCard = card;
                card.SetSelected(true);
            }
        }

        private void OnWeaponRecipeSelected(WeaponRecipeData recipe, BlacksmithRecipeItemCard card)
        {
            SelectCard(card);
            selectedWeaponRecipe = recipe;
            selectedItemRecipe = null;
            ShowWeaponRecipeDetail(recipe);
        }

        private void OnActiveItemRecipeSelected(ActiveItemRecipeData recipe, BlacksmithRecipeItemCard card)
        {
            SelectCard(card);
            selectedItemRecipe = recipe;
            selectedWeaponRecipe = null;
            ShowActiveItemRecipeDetail(recipe);

            // 튜토리얼 2-B: 부적 선택 시 제작 버튼 하이라이트로 진행
            if (recipe?.resultItem != null && recipe.resultItem.itemType == ActiveItemType.Charm
                && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialCharmRecipeSelected();
        }

        private void SelectCard(BlacksmithRecipeItemCard card)
        {
            if (currentSelectedCard != null)
                currentSelectedCard.SetSelected(false);
            currentSelectedCard = card;
            if (currentSelectedCard != null)
                currentSelectedCard.SetSelected(true);
        }

        /// <summary>제작 가능 캐시 갱신 시 — 목록 재생성 없이 현재 카드들의 도트와 선택된 상세를 갱신.</summary>
        private void OnCraftableCacheChanged()
        {
            foreach (var pair in cardRecipeIDs)
                pair.Key?.SetCraftable(BlacksmithManager.Instance.IsRecipeCraftable(pair.Value));

            // 자원 변동(제작/분해 등) 시 현재 선택된 레시피 상세(버튼/골드/재료)도 최신화
            if (currentRecipeTab == RecipeTab.Weapon && selectedWeaponRecipe != null)
                ShowWeaponRecipeDetail(selectedWeaponRecipe);
            else if (currentRecipeTab == RecipeTab.ActiveItem && selectedItemRecipe != null)
                ShowActiveItemRecipeDetail(selectedItemRecipe);
        }

        #endregion

        #region Item Detail Panel

        private void ShowEmptyDetail()
        {
            ClearMaterialList();
            if (itemTypeText != null) itemTypeText.text = "";
            if (itemNameText != null)
            {
                itemNameText.text = L("Blacksmith_SelectItemHint");
                itemNameText.color = defaultNameColor;
            }
            itemBGColor?.gameObject.SetActive(false);
            itemDescriptionText?.gameObject.SetActive(false);
            requiredMaterialPanel?.SetActive(false);
            craftButton?.gameObject.SetActive(false);
        }

        private void ShowWeaponRecipeDetail(WeaponRecipeData recipe)
        {
            ClearMaterialList();
            itemBGColor?.gameObject.SetActive(true);
            itemDescriptionText?.gameObject.SetActive(false);
            requiredMaterialPanel?.SetActive(true);
            craftButton?.gameObject.SetActive(true);

            if (itemTypeText != null)
                itemTypeText.text = UITranslator.GetString(recipe.resultWeapon.weaponType);
            if (itemNameText != null)
            {
                itemNameText.text = recipe.resultWeapon.DisplayName;
                itemNameText.color = ColorManager.Instance.GetGradeAccentColor(recipe.resultWeapon.baseGrade);
            }
            if (itemIconImage != null && recipe.resultWeapon.icon != null)
                itemIconImage.sprite = recipe.resultWeapon.icon;

            UpdateGradeColors(recipe.resultWeapon.baseGrade);
            UpdateGoldCostText(BlacksmithManager.Instance.GetCraftBaseGold(recipe));
            UpdateMaterialList(recipe.requiredMaterials);
            UpdateCraftButton(recipe);
        }

        private void ShowActiveItemRecipeDetail(ActiveItemRecipeData recipe)
        {
            ClearMaterialList();
            itemIconImage?.gameObject.SetActive(true);
            itemBGColor?.gameObject.SetActive(true);
            itemFrameColor?.gameObject.SetActive(true);
            requiredMaterialPanel?.SetActive(true);
            craftButton?.gameObject.SetActive(true);

            Grade grade = recipe.resultItem.usageContext.ToGrade();

            if (itemTypeText != null)
                itemTypeText.text = UITranslator.GetString(recipe.resultItem.itemType);
            if (itemNameText != null)
            {
                itemNameText.text = recipe.resultItem.DisplayName;
                itemNameText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }
            if (itemIconImage != null && recipe.resultItem.icon != null)
                itemIconImage.sprite = recipe.resultItem.icon;
            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = recipe.resultItem.DisplayDescription;
                itemDescriptionText.gameObject.SetActive(true);
            }

            UpdateGradeColors(grade);
            UpdateGoldCostText(BlacksmithManager.Instance.GetCraftBaseGold(recipe));
            UpdateMaterialList(recipe.requiredMaterials);
            UpdateCraftButton(recipe);
        }

        private void UpdateGradeColors(Grade grade)
        {
            if (itemBGColor != null)
                itemBGColor.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);                
            if (itemFrameColor != null)
                itemFrameColor.sprite = IconManager.Instance?.GetFrameByGrade(grade);
            else
            {
                if (itemBGColor != null) itemBGColor.sprite = null;
                if (itemFrameColor != null) itemFrameColor.sprite = null;
            }
        }

        private void UpdateGoldCostText(int baseGoldCost)
        {
            if (goldCostText == null) return;

            int goldCost = BlacksmithManager.Instance.ApplyCostReduction(baseGoldCost);
            goldCostText.text = UITranslator.GetGoldCostString(baseGoldCost, goldCost);

            if (!EconomyManager.Instance.HasEnoughGold(goldCost))
                goldCostText.color = ColorManager.Instance.GetRedColor();
            else
                goldCostText.color = ColorManager.Instance.GetBlackColor();
        }

        #endregion

        #region Material List

        private void UpdateMaterialList(List<MaterialRequirement> requiredMaterials)
        {
            ClearMaterialList();

            int total = 0;
            int satisfied = 0;

            foreach (var mat in requiredMaterials)
            {
                int reducedCount = BlacksmithManager.Instance.ApplyMaterialReduction(mat.count);
                bool isDiscounted = BlacksmithManager.Instance.IsMaterialDiscountActive;

                total++;
                if (CreateMaterialItem(mat.material, reducedCount, isDiscounted))
                    satisfied++;
            }

            if (materialSatisfiedText != null)
            {
                materialSatisfiedText.text = $"{satisfied}/{total}";
                materialSatisfiedText.color = satisfied >= total
                    ? ColorManager.Instance.GetGreenColor()
                    : ColorManager.Instance.GetRedColor();
            }
        }

        private void ClearMaterialList()
        {
            if (materialSatisfiedText != null)
                materialSatisfiedText.text = "";

            if (requiredMaterialContainer == null) return;
            for (int i = requiredMaterialContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(requiredMaterialContainer.GetChild(i).gameObject);
        }

        /// <summary>재료 항목을 생성한다. 보유량이 필요 개수를 충족하면 true.</summary>
        private bool CreateMaterialItem(MaterialData material, int count, bool isDiscounted = false)
        {
            if (requiredMaterialItemPrefab == null || requiredMaterialContainer == null) return false;

            var obj = Instantiate(requiredMaterialItemPrefab, requiredMaterialContainer);
            return obj.GetOrAddComponent<RequiredMaterialItem>().Initialize(
                material, count, isDiscounted,
                (mat, cnt) => controller?.OnMaterialDetailClicked(mat)
            );
        }

        #endregion

        #region Craft Button

        // 자원/슬롯 검증은 매니저(GetCraftBlockReason)가 단일 책임. View는 결과로 버튼 상태와 사유만 표현.
        private void UpdateCraftButton(WeaponRecipeData recipe)
        {
            ApplyCraftBlockReason(BlacksmithManager.Instance.GetCraftBlockReason(recipe));
        }

        private void UpdateCraftButton(ActiveItemRecipeData recipe)
        {
            ApplyCraftBlockReason(BlacksmithManager.Instance.GetCraftBlockReason(recipe));
        }

        /// <summary>
        /// 차단 사유를 버튼 활성/비활성과 비용/사유 묶음 토글로 반영한다.
        /// 가능: goldCostGroup 활성. 차단: banGroup 활성 + banText에 사유 표시.
        /// </summary>
        private void ApplyCraftBlockReason(BlacksmithManager.CraftBlockReason reason)
        {
            bool canCraft = reason == BlacksmithManager.CraftBlockReason.None;

            if (craftButton != null) craftButton.interactable = canCraft;
            if (goldCostGroup != null) goldCostGroup.SetActive(canCraft);
            if (banGroup != null) banGroup.SetActive(!canCraft);

            if (!canCraft && banText != null)
            {
                banText.text = reason == BlacksmithManager.CraftBlockReason.InventoryFull
                    ? L("Blacksmith_BanInventoryFull")
                    : L("Blacksmith_BanNotEnoughMaterial");
            }
        }

        private void OnCraftButtonClicked()
        {
            int goldCost = 0;
            if (currentRecipeTab == RecipeTab.Weapon && selectedWeaponRecipe != null)
                goldCost = BlacksmithManager.Instance.ApplyCostReduction(BlacksmithManager.Instance.GetCraftBaseGold(selectedWeaponRecipe));
            else if (currentRecipeTab == RecipeTab.ActiveItem && selectedItemRecipe != null)
                goldCost = BlacksmithManager.Instance.ApplyCostReduction(BlacksmithManager.Instance.GetCraftBaseGold(selectedItemRecipe));

            // 연타 차단: 클릭 즉시 비활성화. 성공 경로는 DoCraft의 ShowXxxRecipeDetail이 재평가,
            // 취소/실패 경로는 ReevaluateCraftButton으로 복구.
            if (craftButton != null) craftButton.interactable = false;

            UIPopupController.Instance?.ShowPopup(
                BuildCraftConfirmMessage(),
                onConfirm: () => EconomyManager.Instance.EnsureGold(goldCost, onReady: DoCraft, onCancel: ReevaluateCraftButton),
                onCancel: ReevaluateCraftButton);
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private string BuildCraftConfirmMessage()
        {
            string name = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", "Common_Item");
            Grade grade = Grade.Common;

            if (currentRecipeTab == RecipeTab.Weapon && selectedWeaponRecipe != null)
            {
                name = selectedWeaponRecipe.resultWeapon.DisplayName;
                grade = selectedWeaponRecipe.resultWeapon.baseGrade;
            }
            else if (currentRecipeTab == RecipeTab.ActiveItem && selectedItemRecipe != null)
            {
                name = selectedItemRecipe.resultItem.DisplayName;
            }

            string hex = ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGradeColor(grade));
            return L("Blacksmith_CraftConfirm", ("color", "#" + hex), ("name", name));
        }

        private void ReevaluateCraftButton()
        {
            if (currentRecipeTab == RecipeTab.Weapon && selectedWeaponRecipe != null)
                UpdateCraftButton(selectedWeaponRecipe);
            else if (currentRecipeTab == RecipeTab.ActiveItem && selectedItemRecipe != null)
                UpdateCraftButton(selectedItemRecipe);
        }

        private void DoCraft()
        {
            // 실제 제작·자원차감은 ProcessView 코루틴에서 수행되며,
            // 차감 후 OnCraftableCacheChanged가 상세를 최신화한다.
            if (currentRecipeTab == RecipeTab.Weapon && selectedWeaponRecipe != null)
                controller?.OnCraftWeapon(selectedWeaponRecipe);
            else if (currentRecipeTab == RecipeTab.ActiveItem && selectedItemRecipe != null)
                controller?.OnCraftItem(selectedItemRecipe);
        }

        #endregion

        #region 튜토리얼 하이라이트 접근자

        /// <summary>튜토리얼 하이라이트용 — ActiveItem 레시피 토글 RectTransform.</summary>
        public RectTransform GetRecipeTypeToggleRect() => recipeTypeToggle?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트 중 목록이 스크롤돼 대상 카드가 어긋나지 않도록 잠근다.</summary>
        public void SetActiveItemScrollLocked(bool locked)
        {
            if (activeItemRecipeScrollRect != null)
                activeItemRecipeScrollRect.enabled = !locked;
        }

        /// <summary>튜토리얼 하이라이트용 — 제작 버튼 RectTransform.</summary>
        public RectTransform GetCraftButtonRect() => craftButton?.transform as RectTransform;

        /// <summary>
        /// 튜토리얼 하이라이트용 — 대상 레시피 카드가 뷰포트 안에 들어오도록 목록을 맨 아래로 내린 뒤 카드 RectTransform을 콜백으로 넘긴다.
        /// (부적 레시피는 정렬상 목록 맨 마지막이라, 스크롤이 맨 위인 상태에선 카드가 뷰포트 밖이라 하이라이트가 잘린다.)
        /// </summary>
        public void FocusActiveItemRecipeCard(string recipeStaticID, Action<RectTransform> onReady)
        {
            if (!gameObject.activeInHierarchy)
            {
                onReady?.Invoke(GetActiveItemRecipeCardRect(recipeStaticID));
                return;
            }
            StartCoroutine(FocusActiveItemRecipeCardRoutine(recipeStaticID, onReady));
        }

        private IEnumerator FocusActiveItemRecipeCardRoutine(string recipeStaticID, Action<RectTransform> onReady)
        {
            // 목록 갱신이 건 ResetPosition(맨 위로)은 프레임 끝에 실행되므로, 그 뒤로 미뤄야 스크롤이 덮이지 않는다.
            yield return null;

            if (activeItemRecipeScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();                                   // 방금 생성된 카드까지 반영해 content 크기 확정
                activeItemRecipeScrollRect.verticalNormalizedPosition = 0f;     // 대상 카드가 있는 맨 아래로
                Canvas.ForceUpdateCanvases();                                   // 스크롤 이동 결과 확정(하이라이트가 읽을 좌표)
            }

            onReady?.Invoke(GetActiveItemRecipeCardRect(recipeStaticID));
        }

        /// <summary>튜토리얼 하이라이트용 — 지정 StaticID 레시피 카드의 RectTransform(동적 생성 카드).</summary>
        public RectTransform GetActiveItemRecipeCardRect(string recipeStaticID)
        {
            // GridLayoutGroup 배치는 다음 레이아웃 패스에 반영되므로, 방금 생성된 카드의
            // 월드 좌표를 읽기 전에 컨테이너 레이아웃을 즉시 리빌드해야 하이라이트 위치가 맞다.
            if (activeItemRecipeContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            foreach (var pair in cardRecipeIDs)
                if (pair.Value == recipeStaticID)
                    return pair.Key?.transform as RectTransform;
            return null;
        }

        #endregion
    }
}
