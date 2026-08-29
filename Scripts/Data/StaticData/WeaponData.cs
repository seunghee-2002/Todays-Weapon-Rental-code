// Scripts/Data/WeaponData.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "TodaysWeaponRental/WeaponData")]
    public class WeaponData : BaseData
    {
        public string weaponName;
        [TextArea(3, 5)]
        public string description;

        /// <summary>화면에 보일 이름. 한국어는 위 필드가 원본이고, 다른 언어는 Data 테이블에서 온다.</summary>
        public string DisplayName => DataLocalizer.WeaponName(this);

        /// <summary>화면에 보일 설명. 규칙은 <see cref="DisplayName"/>과 같다.</summary>
        public string DisplayDescription => DataLocalizer.WeaponDescription(this);

        public Sprite icon;            // UI 표시용 아이콘
        public Sprite inGame;          // 대여 시 모험가 손(gear_right)에 입힐 인게임 무기 스프라이트
        public Grade baseGrade;
        public WeaponType weaponType;
        public int basePrice;
        public int gearRightIndex = -1; // 손 무기 정렬 템플릿으로 쓸 Spine gear_right 스킨 인덱스 (-1 = 미지정/맨손)
    }
}