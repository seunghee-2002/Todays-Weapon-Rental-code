using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 필수 약관(서비스 이용약관 + 개인정보 수집이용) 동의 여부를 기록한다.
    /// 선택 항목인 분석 데이터 수집 동의는 AnalyticsManager가 별도로 보관한다.
    /// 기기 단위 설정이라 계정 초기화(ResetAccountAsync)에도 유지된다.
    /// </summary>
    public static class TermsAgreement
    {
        private const string AgreedPrefKey = "terms_agreed";

        public static bool HasAgreed => PlayerPrefs.GetInt(AgreedPrefKey, 0) == 1;

        public static void MarkAgreed()
        {
            PlayerPrefs.SetInt(AgreedPrefKey, 1);
            PlayerPrefs.Save();
            Log.Info("[TermsAgreement] 필수 약관 동의 완료");
        }
    }
}
