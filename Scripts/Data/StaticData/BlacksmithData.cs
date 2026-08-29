// Scripts/Data/StaticData/BlacksmithData.cs
using System;
using UnityEngine;

namespace TodaysWeaponRental
{
    [Serializable]
    public class BlacksmithData
    {
        public string StaticID;  // "blacksmith_001"
        public BlacksmithType type;
        public string blacksmithName;  // "절약형 대장장이 톰"
        public bool isPremium;

        [Header("Appearance")]
        public FixedAppearanceData appearance;  // 대장장이별 Spine 외형 (FixedAppearanceData)

        [Header("Dialogue Messages")]
        [Tooltip("요청 성공 시 인사말. 주차 구간별 1개씩 (인덱스 = PriceTierConfig.TierIndex, 길이 5). " +
                 "맡기는 일이 무거워지는 흐름을 캐릭터 성격 그대로 담는다.")]
        [TextArea(2, 4)]
        public string[] greetingsOnSuccess = new string[5];
        [TextArea(2, 4)]
        public string greetingOnFailure;  // 요청 실패 시 인사말 (변명). 상황 반응이라 주차와 무관
    }
}