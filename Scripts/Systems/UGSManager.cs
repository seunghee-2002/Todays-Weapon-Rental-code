using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class UGSManager : BaseManager<UGSManager>
    {
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 앱 실행 시의 최초 로그인 시도가 끝났는지 (성공/실패 무관).
        /// 로그인이 실패로 끝나면 IsInitialized는 영영 false라, 이 신호가 없으면
        /// CloudSyncService가 초기화 타임아웃(10초)을 꽉 채우고 나서야 메뉴를 연다.
        /// </summary>
        public bool SignInAttemptFinished { get; private set; }

        /// <summary>
        /// 현재 PlayerId. UGS 초기화/로그인 실패 상태에서는 예외 대신 빈 문자열을 반환한다.
        /// 호출부는 빈 값이면 "-" 등으로 표시할 것.
        /// </summary>
        public string PlayerId
        {
            get
            {
                try
                {
                    if (!IsInitialized || AuthenticationService.Instance == null || !AuthenticationService.Instance.IsSignedIn)
                        return string.Empty;
                    return AuthenticationService.Instance.PlayerId ?? string.Empty;
                }
                catch (Exception e)
                {
                    Log.Warn($"[UGSManager] PlayerId 조회 실패: {e.Message}");
                    return string.Empty;
                }
            }
        }

        private const float PollingInterval = 30f;
        private bool isPolling;
        private Coroutine pollingCoroutine;

        private const float NetworkCheckInterval = 5f;
        private NetworkReachability lastReachability;
        private Coroutine networkMonitorCoroutine;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
                DontDestroyOnLoad(gameObject);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (networkMonitorCoroutine != null)
                StopCoroutine(networkMonitorCoroutine);
        }

        private async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();
                Log.Info("[UGSManager] 초기화 완료");
            }
            catch (Exception e)
            {
                Log.Error($"[UGSManager] 초기화 실패: {e.Message}");
            }

            // 저장된 동의 상태 복원. 최초 실행(동의 확인 전)이거나 철회 상태면 내부 가드로 무시된다.
            // 초기화가 끝나기 전에 동의하면 SetConsent 쪽 호출이 실패하는데 이 줄이 다시 시작시키므로,
            // 동의 시점으로 옮기거나 지우면 그 세션은 수집이 누락된다.
            // 초기화 성패와 무관하게, 그리고 로그인보다 먼저 호출해야 한다.
            // IsCollecting이 false로 굳으면 Send()가 SDK 버퍼에 넣지도 못해 오프라인 캐시로도 복구되지 않고,
            // 오프라인 로그인은 타임아웃까지 오래 걸려 그 사이 초반 이벤트를 통째로 놓친다.
            AnalyticsManager.Instance?.StartCollection();

            try
            {
                await SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                Log.Error($"[UGSManager] 로그인/초기 동기화 실패: {e.Message}");
            }

            SignInAttemptFinished = true;

            lastReachability = Application.internetReachability;
            networkMonitorCoroutine = StartCoroutine(NetworkMonitorCoroutine());
        }

        #endregion

        #region 네트워크 모니터링

        private IEnumerator NetworkMonitorCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(NetworkCheckInterval);

                NetworkReachability current = Application.internetReachability;
                if (lastReachability == NetworkReachability.NotReachable &&
                    current != NetworkReachability.NotReachable)
                {
                    Log.Info("[UGSManager] 네트워크 재연결 감지");
                    OnNetworkReconnected();
                }
                lastReachability = current;
            }
        }

        private async void OnNetworkReconnected()
        {
            if (!IsInitialized)
            {
                Log.Info("[UGSManager] 재연결 — 로그인 재시도");
                await SignInAnonymouslyAsync();
            }

            // 오프라인으로 시작해 수집을 못 켰다면 여기서 복구한다 (이미 켜졌으면 내부 가드로 무시).
            AnalyticsManager.Instance?.StartCollection();

            // 게임오버 클라우드 정리 재시도를 업로드보다 먼저 수행한다.
            // 순서가 뒤바뀌면 게임오버된 회차가 클라우드에서 부활할 수 있다
            if (CloudSaveManager.Instance != null)
            {
                await CloudSaveManager.Instance.ClearPendingCloudGameDataAsync();
                await CloudSaveManager.Instance.UploadPendingAsync();
            }

            if (LeaderboardManager.Instance != null)
                await LeaderboardManager.Instance.TrySubmitPendingScore();

            // 재연결 시 밴 상태 재검사 (오프라인 동안 운영자가 적용/해제했을 수 있음)
            if (BanManager.Instance != null)
                await BanManager.Instance.CheckBanStatusAsync();

            // 오프라인으로 시작해 버전 검사를 못 했다면 여기서 수행한다
            if (AppUpdateManager.Instance != null)
                await AppUpdateManager.Instance.CheckVersionAsync();
        }

        #endregion

        #region 폴링

        /// <summary>
        /// 복원 코드 생성 후 호출. 30초 간격으로 isRestoreUsed 키를 폴링한다.
        /// </summary>
        public void StartRestoreCodePolling()
        {
            if (isPolling) return;

            isPolling = true;
            pollingCoroutine = StartCoroutine(RestoreCodePollingCoroutine());
            Log.Info("[UGSManager] 복원 코드 폴링 시작");
        }

        public void StopRestoreCodePolling()
        {
            if (!isPolling) return;

            isPolling = false;
            if (pollingCoroutine != null)
            {
                StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }
            Log.Info("[UGSManager] 복원 코드 폴링 중단");
        }

        private IEnumerator RestoreCodePollingCoroutine()
        {
            while (isPolling)
            {
                yield return new WaitForSeconds(PollingInterval);

                if (!isPolling) yield break;

                // 비동기 조회를 코루틴 안에서 처리
                bool detected = false;
                bool done = false;

                CheckRestoreUsedAsync(result =>
                {
                    detected = result;
                    done = true;
                });

                yield return new WaitUntil(() => done);

                if (detected)
                {
                    StopRestoreCodePolling();
                    ShowRestoreNotification();
                    yield break;
                }
            }
        }

        private async void CheckRestoreUsedAsync(Action<bool> callback)
        {
            try
            {
                var status = await CloudSaveManager.Instance.GetRestoreStatusAsync();
                callback?.Invoke(status == RestoreStatus.Redeemed);
            }
            catch (Exception e)
            {
                Log.Warn($"[UGSManager] 폴링 조회 실패: {e.Message}");
                callback?.Invoke(false);
            }
        }

        #endregion

        #region 팝업 트리거

        /// <summary>
        /// isRestoreUsed = true 감지 시 현재 활성화된 씬의 팝업을 띄운다.
        /// </summary>
        public void ShowRestoreNotification()
        {
            Log.Info("[UGSManager] 복원 사용 감지 — 초기화 알림 표시");

            string message = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Messages", "Restore_MigratedToOtherDevice");

            // UIPopupController는 DontDestroyOnLoad 싱글톤이라 두 씬(MainMenu/InGame) 모두에서 동작한다.
            UIPopupController.Instance?.ShowPopup(message, async () =>
            {
                await CloudSaveManager.Instance.WipeDataAsync();
                SceneController.Instance?.LoadMainMenu();
            });
        }

        #endregion

        #region 계정 초기화

        /// <summary>
        /// 이 기기의 데이터를 모두 지우고 새 계정(PlayerId)을 발급받는다.
        /// 옛 계정은 서버 데이터(리더보드 점수/닉네임/클라우드 세이브)를 정리한 뒤 영구 삭제된다.
        /// 기기 단위 설정인 사운드/데이터 수집 동의/언어/필수 약관 동의만 유지된다.
        /// 재로그인에 실패해도 로컬 초기화는 이미 끝난 상태이며, 재연결 시 자동 로그인된다.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> ResetAccountAsync()
        {
            Log.Info("[UGSManager] 계정 초기화 시작");

            StopRestoreCodePolling();

            // 1. 옛 계정의 서버 흔적(리더보드 점수/닉네임) 정리 - 반드시 계정 삭제 전에 수행한다.
            //    지우지 않으면 옛 PlayerId의 기록이 랭킹에 남고, getPlayerNicknames가
            //    옛 닉네임을 계속 반환해 초기화 후에도 옛 닉네임이 노출된다
            await ResetServerAccountDataAsync();

            // 2. 로컬 파일 + 옛 계정 클라우드 데이터 + 복원 키 + 보류 플래그 정리
            if (CloudSaveManager.Instance != null)
                await CloudSaveManager.Instance.WipeDataAsync();

            // 3. 메모리 상태 초기화 - LegacyManager는 DontDestroyOnLoad라 씬을 다시 로드해도 남는다.
            //    볼륨은 기기 설정이므로 새 PlayerData에 옮겨 담아 유지한다.
            if (LegacyManager.Instance != null)
            {
                LegacyManager.Instance.ReplaceLegacyData(new PlayerData());
                SoundManager.Instance?.SyncSettingsToPlayerData();
                LegacyManager.Instance.SaveLegacy();
            }

            // 4. 닉네임 로컬 미러 정리 - 비우지 않으면 새 계정 로그인 시
            //    옛 닉네임이 서버로 이관된다
            NicknameManager.Instance?.ResetLocalState();

            // 5. 옛 계정을 서버에서 영구 삭제. 세션 토큰만 지우면(SignOut) 익명 계정이 서버에 그대로
            //    남아 초기화를 반복할수록 유령 계정이 쌓인다.
            //    실패해도 초기화는 계속한다 - 옛 계정이 남을 뿐, 이 기기는 새 계정으로 전환되고
            //    리더보드 점수/닉네임은 1단계에서 이미 지웠다
            try
            {
                await AuthenticationService.Instance.DeleteAccountAsync();
                Log.Info("[UGSManager] 옛 계정 영구 삭제 완료");
            }
            catch (Exception e)
            {
                Log.Warn($"[UGSManager] 옛 계정 삭제 실패 (계정이 서버에 남는다): {e.Message}");
            }

            // 6. 새 계정 발급: 세션 토큰까지 지워야 다음 익명 로그인이 새 PlayerId를 만든다.
            //    계정 삭제가 성공했다면 DeleteAccountAsync가 내부에서 이미 SignOut(true)를 마쳤으므로
            //    로그인 상태가 남아 있는 경우(삭제 실패)에만 정리한다
            try
            {
                if (AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut(true);

                IsInitialized = false;
                await SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                Log.Error($"[UGSManager] 계정 재발급 실패: {e.Message}");
                return false;
            }

            // 7. 새 계정 기준으로 메모리 상태 갱신. 밴은 옛 계정의 판정이므로 즉시 해제하고,
            //    닉네임은 서버에서 다시 읽어 새 기본 닉네임을 부여받는다.
            //    (NicknameManager.Initialize는 InGameScene에서만 호출되므로 여기서 직접 갱신해야
            //     초기화 직후 메인메뉴 리더보드에도 새 닉네임이 반영된다)
            BanManager.Instance?.ClearBan();
            if (NicknameManager.Instance != null)
                await NicknameManager.Instance.RefreshFromCloudAsync();

            Log.Info($"[UGSManager] 계정 초기화 완료. 새 PlayerId: {PlayerId}");
            return IsInitialized;
        }

        /// <summary>
        /// 옛 계정의 서버 데이터(리더보드 점수/닉네임)를 Cloud Code로 정리한다.
        /// 실패해도 초기화는 계속 진행한다 - 로컬은 곧 지워지고 새 계정이 발급되므로 되돌릴 수 없고,
        /// 남은 옛 기록은 랭킹 표시에만 영향을 준다
        /// </summary>
        private async System.Threading.Tasks.Task ResetServerAccountDataAsync()
        {
            if (!IsInitialized || Application.internetReachability == NetworkReachability.NotReachable)
            {
                Log.Warn("[UGSManager] 옛 계정 서버 데이터 정리 스킵 (미인증/오프라인)");
                return;
            }

            try
            {
                var response = await CloudCodeService.Instance.CallEndpointAsync<ResetAccountResponse>(
                    "resetAccount", new Dictionary<string, object>());

                if (response == null || !response.success)
                    Log.Warn("[UGSManager] 옛 계정 닉네임 정리 실패 - 랭킹에 옛 닉네임이 남을 수 있다");
                else
                    Log.Info("[UGSManager] 옛 계정 서버 데이터 정리 완료");
            }
            catch (Exception e)
            {
                Log.Warn($"[UGSManager] 옛 계정 서버 데이터 정리 실패: {e.Message}");
            }
        }

        #endregion

        #region 내부 타입

        [Serializable]
        private class ResetAccountResponse
        {
            public bool success;
        }

        #endregion

        #region 내부 메서드

        private async System.Threading.Tasks.Task SignInAnonymouslyAsync(bool isRetry = false)
        {
            try
            {
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    Log.Info($"[UGSManager] 이미 로그인됨. PlayerId: {PlayerId}");
                    IsInitialized = true;
                    return;
                }

                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                IsInitialized = true;
                Log.Info($"[UGSManager] 익명 로그인 성공. PlayerId: {PlayerId}");

                // 앱 실행 시 밴 상태 검사 (사용자에게 즉시 안내하기 위함)
                if (BanManager.Instance != null)
                    _ = BanManager.Instance.CheckBanStatusAsync();

                // 강제 업데이트 검사 — 응답은 캐시되어 다음 오프라인 실행에서도 적용된다
                if (AppUpdateManager.Instance != null)
                    _ = AppUpdateManager.Instance.CheckVersionAsync();

                if (LeaderboardManager.Instance != null)
                    _ = LeaderboardManager.Instance.TrySubmitPendingScore();

                // 게임오버 클라우드 정리가 보류 중이면 가장 먼저 재시도
                await CloudSaveManager.Instance.ClearPendingCloudGameDataAsync();

                // 복원 상태 확인: 앱이 꺼진 사이에 다른 기기가 복원을 완료한 경우 초기화
                var status = await CloudSaveManager.Instance.GetRestoreStatusAsync();
                if (status == RestoreStatus.Redeemed)
                {
                    Log.Info("[UGSManager] 재시작 시 Redeemed 감지 — 초기화 알림 표시");
                    ShowRestoreNotification();
                }
                else if (status == RestoreStatus.Active)
                {
                    // 앱 재시작 후에도 활성 복원 코드의 사용 감지를 재개한다
                    Log.Info("[UGSManager] 재시작 시 Active 복원 코드 감지 — 폴링 재개");
                    StartRestoreCodePolling();
                }
                else if (status == RestoreStatus.Expired)
                {
                    // 방금 조회한 status를 그대로 쓴다. 다시 조회하면 같은 답을 받으려고
                    // Cloud Save 왕복을 한 번 더 하게 되고, 하필 메인 메뉴의 DownloadAsync와 겹친다
                    _ = CloudSaveManager.Instance.ResetRestoreKeysAsync();
                }
            }
            catch (AuthenticationException e)
            {
                // 세션 토큰이 가리키는 계정이 서버에 없으면(운영자 삭제, 환경 전환 등) SDK가
                // 토큰을 지우고 SignedOut 상태로 되돌린다. 그 상태에서 한 번 더 호출하면
                // 새 익명 계정이 발급된다. 재시도하지 않으면 이번 실행 내내 비로그인으로 남아
                // 클라우드 저장/랭킹/닉네임이 전부 죽는다.
                if (e.ErrorCode == AuthenticationErrorCodes.InvalidSessionToken && !isRetry)
                {
                    Log.Warn("[UGSManager] 세션 토큰 무효 - 새 계정으로 재로그인 시도");
                    await SignInAnonymouslyAsync(isRetry: true);
                    return;
                }

                Log.Error($"[UGSManager] 로그인 실패 (AuthenticationException): {e.Message}");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"[UGSManager] 로그인 실패 (RequestFailed): {e.Message}");
            }
        }

        #endregion
    }
}
