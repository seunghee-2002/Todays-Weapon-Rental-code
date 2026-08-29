using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 빌드 로그 정책. 씬보다 먼저 실행되어 두 씬 모두를 덮는다.
    ///
    /// - 스택 트레이스 수준 조정 (릴리즈 빌드 전용)
    /// - 서드파티(DOTween, Spine, UGS SDK) 정보성 로그 차단 (릴리즈 빌드 전용)
    ///   프로젝트 코드의 로그는 Log 래퍼가 컴파일 단계에서 이미 제거하므로 여기서 다룰 필요가 없다.
    /// - Error/Exception을 Analytics의 client_error 이벤트로 리포팅
    /// </summary>
    public static class LogPolicy
    {
        private const int MaxReportsPerSession = 20;
        private const int MessageMaxLength = 200;
        private const int StackMaxLength = 300;

        private static readonly HashSet<string> reportedMessages = new HashSet<string>();
        private static int reportCount;
        private static bool isReporting;   // 리포팅 도중 발생한 에러가 다시 리포팅을 부르는 것을 막는다

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Enter Play Mode(도메인 리로드 off) 대비 정적 상태 초기화
            reportedMessages.Clear();
            reportCount = 0;
            isReporting = false;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // 에디터와 개발 빌드에서는 건드리지 않는다.
            // 스택 트레이스가 있어야 콘솔에서 코드로 점프할 수 있기 때문이다.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);

            // Log만 차단하고 Warning/Error/Exception은 통과시킨다.
            Debug.unityLogger.filterLogType = LogType.Warning;
#endif

            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        #region 에러 리포팅

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (isReporting) return;
            if (reportCount >= MaxReportsPerSession) return;

            // Instance가 아직 없는 초기화 이전 에러는 버린다.
            // 중복 판정보다 먼저 확인해야 같은 에러가 나중에 다시 발생했을 때 전송될 수 있다.
            var analytics = AnalyticsManager.Instance;
            if (analytics == null) return;

            isReporting = true;
            try
            {
                string message = Truncate(condition, MessageMaxLength);
                if (!reportedMessages.Add(message)) return;   // 같은 메시지는 세션당 1회만

                reportCount++;
                analytics.Send("client_error", new Dictionary<string, object>
                {
                    ["log_type"] = type.ToString(),
                    ["message"] = message,
                    ["stack_head"] = Truncate(stackTrace, StackMaxLength),
                });
            }
            catch
            {
                // 리포팅 실패가 게임 진행에 영향을 주면 안 된다.
            }
            finally
            {
                isReporting = false;
            }
        }

        #endregion

        #region 내부 메서드

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        #endregion
    }
}
