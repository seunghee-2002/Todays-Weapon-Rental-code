// Scripts/Data/WeaponRecipeData.cs
using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    [System.Serializable]
    public class MaterialRequirement
    {
        public MaterialData material;
        public int count;
    }

    [CreateAssetMenu(fileName = "WeaponRecipeData", menuName = "TodaysWeaponRental/WeaponRecipeData")]
    public class WeaponRecipeData : BaseData
    {
        public WeaponData resultWeapon;
        public List<MaterialRequirement> requiredMaterials = new List<MaterialRequirement>();
        public int requiredGold;
        public int craftingTime;
        public int unlockedDay = 1;
    }
}