// Scripts/Data/AdventurerData.cs
using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "AdventurerData", menuName = "TodaysWeaponRental/AdventurerData")]
    public class AdventurerData : BaseData
    {
        public string adventurerName;
        public bool isNamed;
        
        public Vector2Int strRange;
        public Vector2Int dexRange;
        public Vector2Int intRange;
        public Vector2Int lukRange;
        public TraitType trait;

        public Gender gender;
        public AdventurerAppearanceData appearance = new();
    }   
}