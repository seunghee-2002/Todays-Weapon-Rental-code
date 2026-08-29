using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 인벤토리 Controller - View와 Manager 사이의 중재자
    /// </summary>
    public class InventoryController : BaseController<InventoryView>
    {
        // 무기 필터링 옵션
        private int currentWeaponTypeFilter = 0; // 0: 전체, 1~5: WeaponType
        private bool showRentedWeapons = false;
        
        private int currentMaterialTypeFilter = 0; // 0: 전체, 1: 강화, 2: 제작, 3: 특수
        private int currentActiveItemTypeFilter = 0; // 0: 전체, 1: 즉시(Immediate), 2: 모험(Adventure), 3: 대장장이(Blacksmith)
        
        protected override void OnPanelOpen()
        {
            currentWeaponTypeFilter = 0;
            showRentedWeapons = false;
            currentMaterialTypeFilter = 0;
            currentActiveItemTypeFilter = 0;
            view?.Initialize();

            // 7단계: 가방 버튼으로 인벤토리가 열렸음을 알린다(가드는 TutorialManager 내부).
            TutorialManager.Instance?.OnTutorialInventoryOpened();
        }

        protected override void SubscribeControllerEvents()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnWeaponAdded        += HandleWeaponAdded;
                InventoryManager.Instance.OnWeaponRemoved      += HandleWeaponRemoved;
                InventoryManager.Instance.OnMaterialAdded      += HandleMaterialChanged;
                InventoryManager.Instance.OnMaterialRemoved    += HandleMaterialChanged;
                InventoryManager.Instance.OnActiveItemAdded    += HandleActiveItemChanged;
                InventoryManager.Instance.OnActiveItemRemoved  += HandleActiveItemRemoved;
            }
        }

        protected override void UnsubscribeControllerEvents()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnWeaponAdded        -= HandleWeaponAdded;
                InventoryManager.Instance.OnWeaponRemoved      -= HandleWeaponRemoved;
                InventoryManager.Instance.OnMaterialAdded      -= HandleMaterialChanged;
                InventoryManager.Instance.OnMaterialRemoved    -= HandleMaterialChanged;
                InventoryManager.Instance.OnActiveItemAdded    -= HandleActiveItemChanged;
                InventoryManager.Instance.OnActiveItemRemoved  -= HandleActiveItemRemoved;
            }
        }
        
        #region 무기 탭
        
        /// <summary>
        /// 무기 목록을 다시 그린다.
        /// resetSelected=false면 갱신 후에도 같은 무기를 다시 선택한다(필터 결과에 남아 있을 때만).
        /// </summary>
        public void RefreshWeaponTab(bool resetSelected = true)
        {
            WeaponInstance selectedWeapon = resetSelected ? null : view?.SelectedWeapon;
            List<WeaponInstance> weapons = GetFilteredWeapons();
            view?.UpdateWeaponGrid(weapons);
            view?.UpdateWeaponTab(InventoryManager.Instance.GetAllWeapons().Count);
            if (selectedWeapon != null)
            {
                view?.ReselectWeapon(selectedWeapon);
            }
        }

        private List<WeaponInstance> GetFilteredWeapons()
        {
            List<WeaponInstance> weapons = InventoryManager.Instance.GetAllWeapons();

            // 대여중 무기 필터
            if (!showRentedWeapons)
                weapons = weapons.Where(w => !w.isRented).ToList();
            
            // 타입 필터
            if (currentWeaponTypeFilter > 0)
            {
                WeaponType targetType = (WeaponType)(currentWeaponTypeFilter - 1);
                weapons = weapons.Where(w => w.weaponData.weaponType == targetType).ToList();
            }
            
            // 등급 내림차순 고정
            weapons = weapons.OrderByDescending(w => w.currentGrade).ToList();
            
            return weapons;
        }
        
        public void ResetFilters()
        {
            currentWeaponTypeFilter = 0;
            currentMaterialTypeFilter = 0;
            currentActiveItemTypeFilter = 0;
        }

        public void OnWeaponTypeFilterChanged(int filterIndex)
        {
            AnalyticsManager.Instance?.SendButtonClick("inventory", "weapon_type_filter", new Dictionary<string, object>
            {
                { "filter_index", filterIndex }
            });

            currentWeaponTypeFilter = filterIndex;
            // 타입을 바꿔도 고른 무기가 목록에 남아 있으면 선택을 유지한다(빠지면 자동 해제).
            RefreshWeaponTab(false);
        }

        public void OnShowRentedToggleChanged(bool isOn)
        {
            showRentedWeapons = isOn;
            RefreshWeaponTab(false);

            // 7단계: '대여중' 토글을 켰을 때만 진행(가드는 TutorialManager 내부).
            if (isOn) TutorialManager.Instance?.OnTutorialRentedToggleOn();
        }

        /// <summary>무기 카드를 눌러 상세가 열렸음을 알린다. 7단계 대여 무기 확인 유도용(가드는 TutorialManager 내부).</summary>
        public void OnWeaponCardSelected()
        {
            TutorialManager.Instance?.OnTutorialRentedWeaponDetailOpened();
        }

        #endregion

        #region 재료/액티브 아이템 탭

        public void RefreshMaterialTab()
        {
            Dictionary<string, int> allMaterials = InventoryManager.Instance.GetAllMaterials();
            Dictionary<string, int> filteredMaterials = new Dictionary<string, int>();
            
            foreach (var kvp in allMaterials)
            {
                MaterialData materialData = DataManager.Instance.GetMaterial(kvp.Key);
                if (materialData == null) continue;
                
                // 타입 필터 적용
                if (currentMaterialTypeFilter == 0) // 전체
                {
                    filteredMaterials[kvp.Key] = kvp.Value;
                }
                else
                {
                    MaterialType targetType = (MaterialType)(currentMaterialTypeFilter - 1);
                    if (materialData.materialType == targetType)
                    {
                        filteredMaterials[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 1순위: materialType 오름차순 (Enforce → Craft → Special)
            // 2순위: baseValue 오름차순 (낮은 가격 먼저)
            filteredMaterials = filteredMaterials
                .OrderBy(kvp => DataManager.Instance.GetMaterial(kvp.Key)?.materialType ?? MaterialType.Enforce)
                .ThenBy(kvp => DataManager.Instance.GetMaterial(kvp.Key)?.baseValue ?? 0)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            view?.UpdateMaterialList(filteredMaterials);
        }

        public void OnMaterialTypeFilterChanged(int filterIndex)
        {
            AnalyticsManager.Instance?.SendButtonClick("inventory", "material_type_filter", new Dictionary<string, object>
            {
                { "filter_index", filterIndex }
            });

            currentMaterialTypeFilter = filterIndex;
            RefreshMaterialTab();
        }

        public void RefreshActiveItemTab()
        {
            List<ActiveItemInstance> allItems = InventoryManager.Instance.GetAllActiveItems();
            List<ActiveItemInstance> filteredItems = new List<ActiveItemInstance>();
            
            foreach (var item in allItems)
            {
                if (currentActiveItemTypeFilter == 0) // 전체
                {
                    filteredItems.Add(item);
                }
                else
                {
                    ActiveItemUsage targetUsage = (ActiveItemUsage)(currentActiveItemTypeFilter - 1);
                    if (item.activeItemData.usageContext == targetUsage)
                        filteredItems.Add(item);
                }
            }

            filteredItems = filteredItems
            .OrderBy(item => item.activeItemData.usageContext)
            .ThenBy(item => item.activeItemData.effectValue)
            .ToList();
            
            view?.UpdateActiveItemList(filteredItems);
        }

        public void OnActiveItemTypeFilterChanged(int filterIndex)
        {
            AnalyticsManager.Instance?.SendButtonClick("inventory", "active_item_type_filter", new Dictionary<string, object>
            {
                { "filter_index", filterIndex }
            });

            currentActiveItemTypeFilter = filterIndex;
            RefreshActiveItemTab();
        }

        #endregion

        #region 이벤트 핸들러

        // 인벤토리 구성이 바뀌면(획득/소멸) 선택을 해제한다.
        private void HandleWeaponAdded(WeaponInstance weapon)
        {
            if (view?.currentTab == InventoryView.InventoryTab.Weapon)
            {
                RefreshWeaponTab();
            }
        }

        private void HandleWeaponRemoved(WeaponInstance weapon)
        {
            if (view?.currentTab == InventoryView.InventoryTab.Weapon)
            {
                RefreshWeaponTab();
            }
        }
        
        private void HandleMaterialChanged(string materialID, int count)
        {
            if (view?.currentTab == InventoryView.InventoryTab.Material)
            {
                RefreshMaterialTab();
            }
        }
        
        private void HandleActiveItemChanged(ActiveItemInstance item)
        {
            if (view?.currentTab == InventoryView.InventoryTab.ActiveItem)
            {
                RefreshActiveItemTab();
            }
        }
        
        private void HandleActiveItemRemoved(string instanceID)
        {
            if (view?.currentTab == InventoryView.InventoryTab.ActiveItem)
            {
                RefreshActiveItemTab();
            }
        }

        #endregion

        #region 패널 닫기

        public void OnInventoryCloseClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("inventory", "close");
            UIManager.Instance.ClosePanel<InventoryView>();

            // 7단계: 인벤토리를 닫으면 단계 마무리(가드는 TutorialManager 내부).
            TutorialManager.Instance?.OnTutorialInventoryClosed();
        }


        #endregion

    }
}