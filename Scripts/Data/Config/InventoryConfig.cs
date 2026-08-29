// Scripts/Data/Config/InventoryConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "InventoryConfig", menuName = "TodaysWeaponRental/Config/InventoryConfig")]
    public class InventoryConfig : ScriptableObject
    {
        [Header("Inventory Capacity")]
        [Tooltip("인벤토리 슬롯 개수")]
        public int inventorySlots = 50;
    }
}