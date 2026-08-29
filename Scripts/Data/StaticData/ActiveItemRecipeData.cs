// Scripts/Data/ActiveItemRecipeData.cs
using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "ActiveItemRecipeData", menuName = "TodaysWeaponRental/ActiveItemRecipeData")]
    public class ActiveItemRecipeData : BaseData
    {
        public ActiveItemData resultItem;
        public List<MaterialRequirement> requiredMaterials = new List<MaterialRequirement>();
        public int requiredGold;
        public int craftingTime;
        public int unlockedDay = 1;
    }
}