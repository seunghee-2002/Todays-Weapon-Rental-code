using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 앱 강제 업데이트 여부를 관리한다.
    /// 진실의 소스(source of truth)는 Cloud Code checkAppVersion 이 돌려주는 minVersion 이며,
    /// 마지막으로 받은 값을 PlayerPrefs 에 기록해 오프라인 실행에서도 같은 판정을 재현한다.
    ///
    /// 검사 시점:
    ///   - 앱 실행: Awake 에서 PlayerPrefs 캐시로 선판정 (네트워크 불필요)
    ///   - UGSManager 로그인 직후 / 네트워크 재연결 시 CheckVersionAsync 호출
    ///
    /// "차단됨" 불리언이 아니라 minVersion 문자열을 기록하는 이유:
    /// 사용자가 실제로 업데이트하면 Application.version 이 올라가 캐시를 지우지 않아도 자동 통과한다.
    /// </summary>
    public class AppUpdateManager : BaseManager<AppUpdateManager>
    {
        // 마지막으로 서버에서 받은 최소 요구 버전. 기기 단위 값이라 계정 초기화에도 유지된다.
        private const string MinVersionPrefKey = "required_min_version";

        private const string StoreUrl = "https://play.google.com/store/apps/details?id=com.IdeaBank.TodaysWeaponRental";

        public bool IsUpdateRequired { get; private set; }

        /// <summary>
        /// 이번 실행의 판정이 확정됐는지. 오프라인이면 캐시 판정이 곧 최종이라 Awake 에서 즉시 true,
        /// 온라인이면 서버 응답이 끝난 시점(성공/실패 무관)에 true 가 된다.
        /// UI 는 이 신호를 기다렸다가 안내해야 캐시와 서버 판정이 엇갈리는 순간의 깜빡임을 피할 수 있다.
        /// </summary>
        public bool CheckFinished { get; private set; }

        public event Action OnUpdateRequired;

        /// <summary>
        /// 차단 상태였다가 서버 응답으로 풀렸을 때 발화한다. 캐시 선판정이 서버보다 앞서므로,
        /// 이미 떠 있는 안내 팝업을 거둬들이려면 이 이벤트가 필요하다.
        /// </summary>
        public event Action OnUpdateCleared;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
                DontDestroyOnLoad(gameObject);

            OnUpdateRequired = null;
            OnUpdateCleared = null;

            // 오프라인 실행 대비 선판정. 온라인이면 서버 응답이 도착한 뒤 그 값으로 다시 판정한다.
            IsUpdateRequired = IsBelow(PlayerPrefs.GetString(MinVersionPrefKey, string.Empty));
            if (IsUpdateRequired)
                Log.Info($"[AppUpdateManager] 캐시된 최소 버전 미달 - 업데이트 필요 (current={Application.version})");

            // 오프라인은 서버에 물어볼 방법이 없으므로 캐시 판정이 곧 최종이다.
            // 여기서 확정해야 오프라인 차단이 게이트 대기만큼 늦어지지 않는다.
            if (Application.internetReachability == NetworkReachability.NotReachable)
                CheckFinished = true;
        }

        #endregion

        #region 외부 호출

        /// <summary>
        /// UGSManager 가 로그인 직후/재연결 시 호출한다. 응답을 캐시에 기록하고 판정한다.
        /// 오프라인/실패 시 캐시 판정을 유지한 채 조용히 리턴.
        /// </summary>
        public async Task CheckVersionAsync()
        {
            try
            {
                if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
                {
                    Log.Warn("[AppUpdateManager] CheckVersionAsync: UGS 미초기화 - 스킵");
                    return;
                }

                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Log.Info("[AppUpdateManager] CheckVersionAsync: 오프라인 - 스킵");
                    return;
                }

                var response = await CloudCodeService.Instance.CallEndpointAsync<VersionCheckResponse>(
                    "checkAppVersion", new Dictionary<string, object>());

                if (response == null || string.IsNullOrWhiteSpace(response.minVersion))
                {
                    Log.Warn("[AppUpdateManager] checkAppVersion 응답이 비어 있음");
                    return;
                }

                // 통과 응답도 기록한다. 운영자가 minVersion 을 되돌렸을 때 캐시가 옛 값에 묶이지 않도록.
                PlayerPrefs.SetString(MinVersionPrefKey, response.minVersion);
                PlayerPrefs.Save();

                bool required = IsBelow(response.minVersion);
                Log.Info($"[AppUpdateManager] 버전 검사: current={Application.version}, min={response.minVersion}, 업데이트필요={required}");

                if (!required)
                {
                    // 캐시 선판정으로 이미 안내 중이었다면 그 팝업을 거둬야 한다.
                    if (IsUpdateRequired)
                    {
                        IsUpdateRequired = false;
                        Log.Info("[AppUpdateManager] 최소 버전 충족 - 차단 해제");
                        OnUpdateCleared?.Invoke();
                    }
                    return;
                }

                // 캐시 선판정으로 이미 안내 중이면 이벤트를 다시 발화하지 않는다.
                if (IsUpdateRequired) return;

                IsUpdateRequired = true;
                OnUpdateRequired?.Invoke();
            }
            catch (Exception e)
            {
                Log.Warn($"[AppUpdateManager] checkAppVersion 호출 실패 (네트워크 일시 장애 시 정상): {e.Message}");
            }
            finally
            {
                // 성공/실패/스킵을 가리지 않고 "이번 시도가 끝났다"를 알린다.
                // 실패로 끝났다면 캐시 판정이 그대로 최종이 된다.
                CheckFinished = true;
            }
        }

        /// <summary>
        /// Play 스토어의 이 게임 페이지를 연다. 이미 설치된 상태라 업데이트 버튼이 노출된다.
        /// </summary>
        public void OpenStore()
        {
            Log.Info("[AppUpdateManager] 스토어 페이지 열기");
            Application.OpenURL(StoreUrl);
        }

        #endregion

        #region 내부 메서드

        // 현재 앱 버전이 minVersion 보다 낮은가. 값이 없으면 false(통과).
        private static bool IsBelow(string minVersion)
        {
            if (string.IsNullOrWhiteSpace(minVersion)) return false;
            return CompareVersion(Application.version, minVersion) < 0;
        }

        // "0.0.2" 형식 비교. 자리 수가 다르면 없는 자리는 0으로 본다.
        // 숫자가 아닌 조각도 0으로 처리해, 서버 값이 깨져도 멀쩡한 버전을 막지 않는다.
        private static int CompareVersion(string a, string b)
        {
            string[] left  = a.Split('.');
            string[] right = b.Split('.');
            int count = Mathf.Max(left.Length, right.Length);

            for (int i = 0; i < count; i++)
            {
                int lv = ParsePart(left, i);
                int rv = ParsePart(right, i);
                if (lv != rv) return lv < rv ? -1 : 1;
            }
            return 0;
        }

        private static int ParsePart(string[] parts, int index)
        {
            if (index >= parts.Length) return 0;
            return int.TryParse(parts[index].Trim(), out int value) ? value : 0;
        }

        #endregion

        #region 내부 타입

        [Serializable]
        private class VersionCheckResponse
        {
            public string minVersion;
        }

        #endregion
    }
}
