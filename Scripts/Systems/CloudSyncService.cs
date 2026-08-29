using System;
using System.Collections;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// MainMenuScene 전용 클라우드 동기화 서비스.
    /// UGS 초기화 완료 후 클라우드 데이터와 로컬 데이터를 비교해 최신 데이터로 덮어씀.
    /// </summary>
    public class CloudSyncService : MonoBehaviour
    {
        [SerializeField] private float initTimeoutSeconds = 10f;
        [SerializeField] private float gateTimeoutSeconds = 10f;   // Start부터 동기화 종료까지의 전체 상한

        /// <summary>동기화 절차 종료 여부 (성공/실패/스킵 모두 포함). 이어하기 버튼 대기용</summary>
        public bool IsSyncCompleted { get; private set; }

        public event Action OnSyncCompleted;

        #region 초기화

        private IEnumerator Start()
        {
            // 오프라인은 동기화할 클라우드 데이터가 없다. 로컬이 정본이고 재연결 시
            // UGSManager가 UploadPendingAsync로 올리므로, 기다리지 않고 즉시 종료한다.
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Log.Info("[CloudSyncService] 오프라인 감지. 동기화 건너뜀.");
                CompleteSync();
                yield break;
            }

            // 연결은 잡혔지만 실제 통신이 안 되는 경우를 대비한 전체 상한
            StartCoroutine(GateTimeoutCoroutine());

            // UGSManager 초기화 대기 (타임아웃 포함).
            // 로그인이 실패로 끝나면 IsInitialized는 영영 false이므로, 시도 종료 신호도 함께 본다.
            // 그러지 않으면 네트워크 불량/UGS 장애 때마다 타임아웃을 꽉 채운 뒤에야 메뉴가 열린다
            float elapsed = 0f;
            while (elapsed < initTimeoutSeconds)
            {
                var ugs = UGSManager.Instance;
                if (ugs != null && (ugs.IsInitialized || ugs.SignInAttemptFinished))
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
            {
                Log.Warn($"[CloudSyncService] UGS 로그인 미완료 (대기 {elapsed:F1}s). 동기화 건너뜀.");
                CompleteSync();
                yield break;
            }

            _ = SyncAsync();
        }

        #endregion

        #region 내부 메서드

        // 상한 시간 내에 동기화가 끝나지 않으면 강제 종료해 메인 메뉴를 연다.
        private IEnumerator GateTimeoutCoroutine()
        {
            yield return new WaitForSecondsRealtime(gateTimeoutSeconds);

            if (IsSyncCompleted) yield break;

            Log.Warn($"[CloudSyncService] 동기화 상한 {gateTimeoutSeconds}s 초과. 강제 종료.");
            CompleteSync();
        }

        private async System.Threading.Tasks.Task SyncAsync()
        {
            try
            {
                // 게임오버 클라우드 정리가 보류 중이면 다운로드 전에 먼저 재시도한다.
                // 그렇지 않으면 게임오버 이전의 클라우드 세이브를 최신으로 오판해 회차가 부활한다
                await CloudSaveManager.Instance.ClearPendingCloudGameDataAsync();

                var (cloudPlayer, cloudLegacy, cloudGameStamp, cloudLegacyStamp) = await CloudSaveManager.Instance.DownloadAsync();

                // 상한 타임아웃이 먼저 걸렸다면 사용자가 이미 게임에 진입했을 수 있다.
                // 늦게 도착한 결과로 로컬을 덮어쓰지 않고 다음 실행에서 다시 동기화한다.
                if (IsSyncCompleted)
                {
                    Log.Warn("[CloudSyncService] 상한 타임아웃 이후 응답 도착. 결과 폐기.");
                    return;
                }

                GameData localPlayer = SaveManager.LoadGame();

                // GameData 비교 방향: 서버 시각 스탬프 기준
                int gameDirection = ResolveGameDirection(cloudPlayer, localPlayer, cloudGameStamp);

                // legacy는 GameData와 독립적으로 동기화한다.
                // 클라우드 playerData가 없어도(게임오버 직후/새 기기) legacy는 내려받아야 한다.
                // 단, 아래에서 UploadAsync가 legacy까지 함께 올리는 경로면 단독 업로드는 생략한다
                bool legacyUploadedWithGame = localPlayer != null && gameDirection < 0;
                await SyncLegacyAsync(cloudLegacy, cloudLegacyStamp, legacyUploadedWithGame);

                if (cloudPlayer == null)
                {
                    Log.Info("[CloudSyncService] 클라우드 GameData 없음. 로컬 데이터를 클라우드에 업로드.");
                    if (localPlayer != null)
                        await CloudSaveManager.Instance.UploadAsync(localPlayer, LegacyManager.Instance?.GetLegacyData());
                    return;
                }

                if (gameDirection > 0)
                {
                    SaveManager.SaveGame(cloudPlayer);
                    // 이 시점부터 로컬은 클라우드 본과 동일 - 다음 비교의 기준 스탬프를 맞춘다
                    if (cloudGameStamp > 0)
                        CloudSaveManager.Instance.LastGameUploadServerMs = cloudGameStamp;
                    Log.Info($"[CloudSyncService] 클라우드 데이터로 로컬 덮어쓰기 완료 (Day {cloudPlayer.currentDay}, serverStamp {cloudGameStamp})");
                }
                else
                {
                    Log.Info($"[CloudSyncService] 로컬이 정본. 클라우드에 업로드. (cloudStamp {cloudGameStamp} / localStamp {CloudSaveManager.Instance.LastGameUploadServerMs})");
                    if (localPlayer != null)
                        await CloudSaveManager.Instance.UploadAsync(localPlayer, LegacyManager.Instance?.GetLegacyData());
                }
            }
            catch (Exception e)
            {
                Log.Error($"[CloudSyncService] 동기화 중 오류: {e.Message}");
            }
            finally
            {
                CompleteSync();
            }
        }

        /// <summary>
        /// GameData 동기화 방향 판정 (+1 클라우드 채택 / -1 로컬 채택).
        /// 서버 스탬프가 있으면 "클라우드 스탬프 > 이 기기의 마지막 업로드 스탬프"일 때만 클라우드를 채택한다.
        /// 스탬프가 같으면 클라우드는 이 기기가 올린 본이므로, 오프라인 진행이 있을 수 있는 로컬 파일이 정본이다.
        /// </summary>
        private int ResolveGameDirection(GameData cloudPlayer, GameData localPlayer, long cloudStampMs)
        {
            if (cloudPlayer == null) return -1;
            if (localPlayer == null) return 1;

            long localStampMs = CloudSaveManager.Instance.LastGameUploadServerMs;
            return cloudStampMs > localStampMs ? 1 : -1;
        }

        /// <summary>
        /// legacy(영구 데이터) 독립 동기화.
        /// 서버 스탬프로 비교한다.
        /// 파일 갱신 시 LegacyManager 메모리도 함께 교체한다.
        /// legacyUploadedWithGame이 true면 호출부의 UploadAsync가 legacy를 함께 올리므로
        /// 여기서의 단독 업로드는 건너뛴다 (같은 데이터로 Cloud Code를 두 번 호출하지 않기 위함).
        /// </summary>
        private async System.Threading.Tasks.Task SyncLegacyAsync(PlayerData cloudLegacy, long cloudStampMs, bool legacyUploadedWithGame)
        {
            if (cloudLegacy == null) return;

            var legacyMgr = LegacyManager.Instance;

            // 로컬 legacy 손상 상태면 무조건 클라우드 본으로 복구
            if (legacyMgr != null && legacyMgr.IsLegacyCorrupted)
            {
                Log.Warn("[CloudSyncService] 로컬 legacy 손상 - 클라우드 legacy로 복구");
                legacyMgr.ReplaceLegacyData(cloudLegacy);
                if (cloudStampMs > 0)
                    CloudSaveManager.Instance.LastLegacyUploadServerMs = cloudStampMs;
                return;
            }

            long localStampMs = CloudSaveManager.Instance.LastLegacyUploadServerMs;

            // 서버 스탬프 기준: 다른 기기가 더 나중에 올렸을 때만 클라우드 채택.
            // 동률이면 로컬(오프라인 획득 포함)을 정본으로 보고 업로드한다
            bool adoptCloud = cloudStampMs > localStampMs;
            bool uploadLocal = !adoptCloud;

            if (adoptCloud)
            {
                Log.Info($"[CloudSyncService] 클라우드 legacy 채택 (cloudStamp {cloudStampMs} / localStamp {localStampMs})");
                if (legacyMgr != null)
                    legacyMgr.ReplaceLegacyData(cloudLegacy);
                else
                    SaveManager.SavePlayer(cloudLegacy);

                if (cloudStampMs > 0)
                    CloudSaveManager.Instance.LastLegacyUploadServerMs = cloudStampMs;
            }
            else if (uploadLocal && legacyMgr != null && !legacyUploadedWithGame)
            {
                Log.Info($"[CloudSyncService] 로컬 legacy가 정본 - 업로드 (cloudStamp {cloudStampMs} / localStamp {localStampMs})");
                await CloudSaveManager.Instance.UploadLegacyAsync(legacyMgr.GetLegacyData());
            }
        }

        private void CompleteSync()
        {
            if (IsSyncCompleted) return;

            IsSyncCompleted = true;
            OnSyncCompleted?.Invoke();
        }

        #endregion
    }
}
