// Scripts/Data/DungeonData.cs
using UnityEngine;
using System;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    [Serializable]
    public class ArmorTypeVariant
    {
        public ArmorType armorType;
        public float weight; // 가중치 (합계 1.0이 되도록 Inspector에서 설정)
    }

    [CreateAssetMenu(fileName = "DungeonData", menuName = "TodaysWeaponRental/DungeonData")]
    public class DungeonData : BaseData
    {
        public string dungeonName;

        /// <summary>화면에 보일 이름. 한국어는 위 필드가 원본이고, 다른 언어는 Data 테이블에서 온다.</summary>
        public string DisplayName => DataLocalizer.DungeonName(this);

        public Sprite dungeonIcon;
        public Sprite mapBackground;
        public Grade grade;
        public ArmorType armorType; // 고정값 겸 fallback
        public List<ArmorTypeVariant> armorTypeVariants = new List<ArmorTypeVariant>();
        public int baseStatThreshold;

        public int baseRewardMin;
        public int baseRewardMax;
        public MaterialData specialDropMaterial;
        public List<MaterialData> dropMaterials = new List<MaterialData>();

        // 진화 재료 드롭 개수 - 던전별 고정. 재료 종류는 던전 등급이 정한다(GetEnforceMaterialByGrade).
        public int enforceDropCount = 2;

        public float baseDuration = 60f;

        public float questWeight = 1f; // 퀘스트 보드에서의 가중치 (기본값 1)

        public List<DungeonEventData> eventPool = new List<DungeonEventData>();
    }
}