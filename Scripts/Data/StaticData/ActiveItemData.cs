// Scripts/Data/ActiveItemData.cs
using System;
using UnityEngine;

namespace TodaysWeaponRental
{
    public enum ActiveItemUsage
    {
        Immediate,   // 선물 즉시 효과 (인형)
        Adventure,   // 다음 모험 1회 적용 후 소비
        Blacksmith   // 대장장이 작업 1회 적용 후 소비
    }

    public enum ActiveItemType
    {
        // Adventure 귀속
        Charm            = 0,   // 모험 성공률 +(effectValue %)
        Potion           = 1,   // 보호(후퇴방지) +(int)effectValue
        EscapeRope       = 2,   // 함정 회피 확률 +effectValue
        SwiftShoes       = 3,   // 진행시간 배율 ×effectValue (예: 0.8 = -20%)
        DisassemblyKnife = 4,   // 재료 획득 +(int)effectValue 개
        GoldAmulet       = 5,   // 골드 획득 ×(1+effectValue)
        TreasureMap      = 6,   // 던전 특수 재료 드롭률 ×(1+effectValue)
        DeathWard        = 7,   // 사망률 ×(1-effectValue)
        FameScroll       = 8,   // 성공 시 평판 배율 (1.0 = x2)

        // Immediate 귀속
        Doll             = 9,   // 호감도 즉시 +(int)effectValue

        // Blacksmith 귀속
        ForgeStone       = 10,  // 강화/진화 성공률 ×(1+effectValue)
    }

    [Serializable]
    [CreateAssetMenu(fileName = "ActiveItemData", menuName = "TodaysWeaponRental/ActiveItemData")]
    public class ActiveItemData : BaseData
    {
        public string itemName;
        [TextArea(2, 4)]
        public string description;

        /// <summary>화면에 보일 이름. 한국어는 위 필드가 원본이고, 다른 언어는 Data 테이블에서 온다.</summary>
        public string DisplayName => DataLocalizer.ActiveItemName(this);

        /// <summary>화면에 보일 설명. 규칙은 <see cref="DisplayName"/>과 같다.</summary>
        public string DisplayDescription => DataLocalizer.ActiveItemDescription(this);

        public Sprite icon;
        public ActiveItemType itemType;
        public ActiveItemUsage usageContext;
        [Tooltip("Charm: 성공률(0~1), Doll: 호감도, Potion: 보호 횟수, EscapeRope: 회피 확률(0~1), " +
                 "SwiftShoes: 시간 배율(예:0.8), DisassemblyKnife: 재료 수, GoldAmulet: 골드 배율 보너스(예:0.25), " +
                 "TreasureMap: 특수 재료 드롭률 배율(예:1.0=x2), DeathWard: 사망률 감소(예:0.1), FameScroll: 평판 배율(예:1.0=x2), " +
                 "ForgeStone: 성공률 배율 보너스(예:0.1)")]
        public float effectValue;
    }
}
