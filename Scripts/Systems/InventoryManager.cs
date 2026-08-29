using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    public class InventoryManager : BaseManager<InventoryManager>
    {
        // 런타임 데이터
        [SerializeField] private List<WeaponInstance> ownedWeapons = new List<WeaponInstance>();
        [SerializeField] private SerializableDictionary<string, int> ownedMaterials = new SerializableDictionary<string, int>();
        [SerializeField] private List<ActiveItemInstance> ownedActiveItems = new List<ActiveItemInstance>();

        // 이벤트
        public event Action<WeaponInstance> OnWeaponAdded;
        public event Action<WeaponInstance> OnWeaponRemoved;
        public event Action<string, int> OnMaterialAdded;
        public event Action<string, int> OnMaterialRemoved;
        public event Action<ActiveItemInstance> OnActiveItemAdded;
        public event Action<string> OnActiveItemRemoved;

        private InventoryConfig Config => ConfigManager.Instance.Inventory;

        #region 초기화
        
        public void Initialize(GameData gameData)
        {
            LoadFromGameData(gameData);
        }

        private void LoadFromGameData(GameData gameData)
        {
            ownedWeapons.Clear();
            ownedMaterials.Clear();
            ownedActiveItems.Clear();

            // 무기 로드
            foreach (var saveData in gameData.ownedWeapons)
            {
                if (saveData == null) continue;

                var weaponData = DataManager.Instance.GetWeapon(saveData.weaponDataID);
                if (weaponData == null) continue;

                var effects = ReifyEffectsFromSave(saveData.effects);
                ownedWeapons.Add(new WeaponInstance(weaponData, effects, saveData));
            }

            // 재료 로드
            foreach (var saveData in gameData.ownedMaterials)
            {
                if (saveData == null || string.IsNullOrEmpty(saveData.materialDataID)) continue;
                var materialData = DataManager.Instance.GetMaterial(saveData.materialDataID);
                if (materialData != null)
                {
                    ownedMaterials[saveData.materialDataID] = saveData.quantity;
                }
            }

            // 액티브 아이템 로드: 모든 아이템 로드 (활성/비활성 모두)
            // 활성화된 아이템은 나중에 ActiveItemManager에서 처리
            // 폐기된 ID는 무기/재료와 동일하게 스킵 - null 데이터가 남으면 SaveToGameData에서 NRE로 저장 전체가 실패한다
            foreach (var saveData in gameData.ownedActiveItems)
            {
                if (saveData == null) continue;

                var itemData = DataManager.Instance.GetActiveItem(saveData.itemDataID);
                if (itemData == null)
                {
                    Log.Warn($"[InventoryManager] 알 수 없는 액티브 아이템 스킵: {saveData.itemDataID}");
                    continue;
                }

                ownedActiveItems.Add(new ActiveItemInstance(itemData, saveData.quantity));
            }
        }

        public void SaveToGameData(GameData gameData)
        {
            gameData.ownedWeapons = ownedWeapons.ToSaveDataList();
            gameData.ownedMaterials = ownedMaterials
                .Select(kvp => new MaterialInstanceSaveData { materialDataID = kvp.Key, quantity = kvp.Value })
                .ToList();
            gameData.ownedActiveItems = ownedActiveItems.ToSaveDataList();
        }

        public static List<WeaponEffect> ReifyEffectsFromSave(List<WeaponEffectSaveData> saveList)
        {
            var effects = new List<WeaponEffect>();
            if (saveList == null) return effects;
            foreach (var es in saveList)
            {
                if (es == null) continue;
                var effData = DataManager.Instance.GetWeaponEffect(es.effectDataID);
                if (effData == null)
                {
                    // 폐기된 효과 ID - 소비처가 effect.effectData에 직접 접근하므로 로드 단계에서 스킵
                    Log.Warn($"[InventoryManager] 알 수 없는 무기 효과 스킵: {es.effectDataID}");
                    continue;
                }
                effects.Add(new WeaponEffect(es, effData));
            }
            return effects;
        }

        #endregion

        #region 무기 관리

        public int GetMaxWeaponCount()
        {
            return Config.inventorySlots + (LegacyManager.Instance?.GetInventorySlotsBonus() ?? 0);
        }

        public bool CanAddWeapon()
        {
            return ownedWeapons.Count < GetMaxWeaponCount();
        }

        public void AddWeapon(WeaponInstance weapon)
        {
            if (weapon == null)
            {
                Log.Warn("Cannot add null weapon");
                return;
            }

            ownedWeapons.Add(weapon);
            OnWeaponAdded?.Invoke(weapon);
        }

        public bool RemoveWeapon(WeaponInstance weapon)
        {
            if (weapon == null) return false;

            bool removed = ownedWeapons.Remove(weapon);
            if (removed)
            {
                OnWeaponRemoved?.Invoke(weapon);
            }
            return removed;
        }

        public WeaponInstance GetWeaponInstance(string instanceID)
        {
            return ownedWeapons.FirstOrDefault(w => w.instanceID == instanceID);
        }

        public List<WeaponInstance> GetAvailableWeapons()
        {
            return ownedWeapons.Where(w => !w.isRented).ToList();
        }

        public List<WeaponInstance> GetAllWeapons()
        {
            return new List<WeaponInstance>(ownedWeapons);
        }

        #endregion

        #region 재료 관리

        public void AddMaterial(string materialID, int count)
        {
            if (string.IsNullOrEmpty(materialID) || count <= 0)
            {
                Log.Warn($"Invalid material add: {materialID}, count: {count}");
                return;
            }

            if (!ownedMaterials.ContainsKey(materialID))
                ownedMaterials[materialID] = count;
            else
                ownedMaterials[materialID] += count;

            OnMaterialAdded?.Invoke(materialID, count);
        }

        public bool RemoveMaterial(string materialID, int count)
        {
            if (string.IsNullOrEmpty(materialID) || count <= 0)
                return false;

            if (!ownedMaterials.ContainsKey(materialID))
                return false;

            if (ownedMaterials[materialID] < count)
                return false;

            ownedMaterials[materialID] -= count;

            if (ownedMaterials[materialID] == 0)
            {
                ownedMaterials.Remove(materialID);
            }

            OnMaterialRemoved?.Invoke(materialID, count);
            return true;
        }

        public int GetMaterialCount(string materialID)
        {
            return ownedMaterials.TryGetValue(materialID, out int count) ? count : 0;
        }

        public Dictionary<string, int> GetAllMaterials()
        {
            return new Dictionary<string, int>(ownedMaterials);
        }

        #endregion

        #region 액티브 아이템 관리

        public void AddActiveItem(ActiveItemInstance item)
        {
            if (item == null)
            {
                Log.Warn("Cannot add null active item");
                return;
            }

            var existing = ownedActiveItems.FirstOrDefault(i => i.activeItemData != null && i.activeItemData.StaticID == item.activeItemData.StaticID);
            if (existing != null)
            {
                existing.quantity += item.quantity;
                OnActiveItemAdded?.Invoke(existing);
            }
            else
            {
                ownedActiveItems.Add(item);
                OnActiveItemAdded?.Invoke(item);
            }
        }

        public bool RemoveActiveItem(string itemDataID)
        {
            var item = ownedActiveItems.FirstOrDefault(i => i.activeItemData != null && i.activeItemData.StaticID == itemDataID);
            if (item == null) return false;

            item.quantity--;
            if (item.quantity <= 0)
            {
                ownedActiveItems.Remove(item);
                OnActiveItemRemoved?.Invoke(itemDataID);
            }
            else
            {
                OnActiveItemAdded?.Invoke(item);
            }
            return true;
        }

        public ActiveItemInstance GetActiveItemByDataID(string itemDataID)
        {
            return ownedActiveItems.FirstOrDefault(i => i.activeItemData != null && i.activeItemData.StaticID == itemDataID);
        }

        public List<ActiveItemInstance> GetAllActiveItems()
        {
            return new List<ActiveItemInstance>(ownedActiveItems);
        }

        /// <summary>
        /// 배정 취소된 아이템을 인벤토리로 반환
        /// ActiveItemManager에서 호출됨
        /// </summary>
        public void ReturnActiveItemToInventory(ActiveItemData data)
        {
            if (data == null) return;

            var existing = ownedActiveItems.FirstOrDefault(i => i.activeItemData != null && i.activeItemData.StaticID == data.StaticID);
            if (existing != null)
            {
                existing.quantity++;
                OnActiveItemAdded?.Invoke(existing);
            }
            else
            {
                var newItem = new ActiveItemInstance(data, 1);
                ownedActiveItems.Add(newItem);
                OnActiveItemAdded?.Invoke(newItem);
            }
        }

        #endregion
    }
}
