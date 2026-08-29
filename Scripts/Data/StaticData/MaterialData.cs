// Scripts/Data/MaterialData.cs
using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "MaterialData", menuName = "TodaysWeaponRental/MaterialData")]
    public class MaterialData : BaseData
    {
        public string materialName;
        [TextArea(2, 4)]
        public string description;

        /// <summary>화면에 보일 이름. 한국어는 위 필드가 원본이고, 다른 언어는 Data 테이블에서 온다.</summary>
        public string DisplayName => DataLocalizer.MaterialName(this);

        /// <summary>화면에 보일 설명. 규칙은 <see cref="DisplayName"/>과 같다.</summary>
        public string DisplayDescription => DataLocalizer.MaterialDescription(this);

        public Sprite icon;
        public Grade grade = Grade.Common;
        public int baseValue;
        public int buyPrice;
        public MaterialType materialType;
    }
}
