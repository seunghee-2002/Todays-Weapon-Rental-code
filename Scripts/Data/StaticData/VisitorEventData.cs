// Scripts/Data/VisitorEventData.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "VisitorEventData", menuName = "TodaysWeaponRental/VisitorEventData")]
    public class VisitorEventData : BaseData
    {
        public string eventName;
        /// <summary>에디터 메모용. 화면에 뜨지 않으므로 번역하지 않는다.</summary>
        [TextArea(3, 5)]
        public string description;

        /// <summary>화면에 보일 이름. 한국어는 위 필드가 원본이고, 다른 언어는 Data 테이블에서 온다.</summary>
        public string DisplayName => DataLocalizer.VisitorEventName(this);
        public MorningEventType morningEventType;
        public FixedAppearanceData appearance;
    }
}