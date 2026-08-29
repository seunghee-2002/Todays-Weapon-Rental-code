using UnityEngine;

namespace TodaysWeaponRental{
    /// <summary>
    /// 모험가 스탯(0~100)을 보정값으로 바꾸는 공용 곡선.
    /// 저스탯 구간은 거의 0이고 고스탯 구간에서 급격히 커진다 (exponent 2.32 기준 stat 50 = max의 20%).
    /// STR 사망 재굴림 / DEX 함정 회피 / INT 테스트 성공률 보정이 공유한다.
    /// </summary>
    public static class StatCurve
    {
        private const float StatMax = 100f;

        /// <summary>보정값 = max * (stat / 100) ^ exponent</summary>
        public static float Evaluate(int stat, float max, float exponent)
        {
            if (stat <= 0 || max <= 0f) return 0f;
            return Mathf.Pow(Mathf.Clamp01(stat / StatMax), exponent) * max;
        }
    }
}
