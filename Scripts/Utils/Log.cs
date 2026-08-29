using System.Diagnostics;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 로그 래퍼. Debug.Log 대신 이 클래스를 사용한다.
    ///
    /// Info/Warn은 [Conditional]에 의해 릴리즈 빌드에서 호출 구문 자체가 컴파일 단계에서 사라진다.
    /// 인자로 넘긴 문자열 보간($"...")도 함께 사라지므로 GC 할당과 정보 노출이 모두 없어진다.
    /// 조건은 OR로 동작하여 에디터거나 개발 빌드면 살아있고, Development Build 체크를 해제한
    /// 릴리즈 빌드에서만 제거된다. 별도의 define 심볼을 손으로 관리할 필요가 없다.
    ///
    /// Error/Exception은 출시 후 크래시 진단에 필요하므로 릴리즈 빌드에서도 유지한다.
    /// 스택 트레이스 수준과 에러 리포팅은 LogPolicy가 담당한다.
    /// </summary>
    public static class Log
    {
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Info(object message) => UnityEngine.Debug.Log(message);

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Warn(object message) => UnityEngine.Debug.LogWarning(message);

        public static void Error(object message) => UnityEngine.Debug.LogError(message);

        public static void Exception(System.Exception exception) => UnityEngine.Debug.LogException(exception);
    }
}
