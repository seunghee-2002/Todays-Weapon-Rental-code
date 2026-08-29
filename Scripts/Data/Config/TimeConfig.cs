// Scripts/Data/Config/TimeConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "TimeConfig", menuName = "TodaysWeaponRental/Config/TimeConfig")]
    public class TimeConfig : ScriptableObject
    {
        [Header("Time Phase Settings")]
        [Tooltip("아침 시작 시간 (시)")]
        public int morningStartHour = 6;
        [Tooltip("낮 시작 시간 (시)")]
        public int daytimeStartHour = 9;
        [Tooltip("저녁 시작 시간 (시)")]
        public int eveningStartHour = 18;
        [Tooltip("하루 종료 시간 (시)")]
        public int dayEndHour = 21;

        [Header("Time Scale Options")]
        [Tooltip("시간 배속 옵션")]
        public float[] timeScaleOptions = { 1f, 2f, 4f };
    }
}