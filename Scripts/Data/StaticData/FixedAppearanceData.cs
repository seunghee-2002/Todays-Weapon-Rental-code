using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 고정 외형 데이터 (Blacksmith, WeaponShop 등 미리 지정된 외형용 ScriptableObject)
    /// Inspector에서 파츠 인덱스/색상을 직접 입력해 관리
    /// </summary>
    [CreateAssetMenu(fileName = "FixedAppearanceData", menuName = "TodaysWeaponRental/Appearance/FixedAppearanceData")]
    public class FixedAppearanceData : BaseData
    {
        public AdventurerAppearanceData appearance = new();
    }
}