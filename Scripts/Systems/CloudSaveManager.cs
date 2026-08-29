using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using UnityEngine;

namespace TodaysWeaponRental
{
    public enum RestoreResult { Success, AlreadyUsed, Expired, NotFound, SelfRestore, Error }

    public enum RestoreStatus { None, Active, Redeemed, Expired }

    public enum GenerateRestoreResult { Success, MustWipeFirst, NoSaveData, Error }

    public class CloudSaveManager : BaseManager<CloudSaveManager>
    {
        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
                DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region 저장

        private const string PendingUploadKey = "pendingUpload";
        private const string PendingCloudClearKey = "pendingCloudClear";
        // uploadSave가 반환한 서버 시각 스탬프 (동기화 우선순위 판단용)
        private const string LastGameUploadStampKey = "lastGameUploadServerMs";
        private const string LastLegacyUploadStampKey = "lastLegacyUploadServerMs";

        /// <summary>마지막으로 성공한 GameData 업로드의 서버 시각(ms). 없으면 0</summary>
        public long LastGameUploadServerMs
        {
            get => long.TryParse(PlayerPrefs.GetString(LastGameUploadStampKey, "0"), out long v) ? v : 0;
            set { PlayerPrefs.SetString(LastGameUploadStampKey, value.ToString()); PlayerPrefs.Save(); }
        }

        /// <summary>마지막으로 성공한 legacy 업로드의 서버 시각(ms). 없으면 0</summary>
        public long LastLegacyUploadServerMs
        {
            get => long.TryParse(PlayerPrefs.GetString(LastLegacyUploadStampKey, "0"), out long v) ? v : 0;
            set { PlayerPrefs.SetString(LastLegacyUploadStampKey, value.ToString()); PlayerPrefs.Save(); }
        }

        public async Task UploadAsync(GameData gameData, PlayerData playerData)
        {
            if (!IsReady())
            {
                PlayerPrefs.SetInt(PendingUploadKey, 1);
                PlayerPrefs.Save();
                return;
            }

            try
            {
                string gameJson = JsonUtility.ToJson(gameData);

                // legacy 손상 상태이거나 데이터가 없으면 legacyData 업로드를 건너뛴다.
                // 빈 legacy가 클라우드의 정상 진행을 덮어쓰는 것을 방지
                string legacyJson = "";
                if (playerData != null && !(LegacyManager.Instance?.IsLegacyCorrupted ?? false))
                    legacyJson = JsonUtility.ToJson(playerData);
                else
                    Log.Warn("[CloudSaveManager] legacy 손상/부재 상태 - legacyData 업로드 건너뜀");

                // 업로드는 Cloud Code(uploadSave)를 경유해 서버 시각 스탬프를 함께 기록한다
                var args = new Dictionary<string, object>
                {
                    { "playerDataJson", gameJson },
                    { "legacyDataJson", legacyJson }
                };
                var response = await CloudCodeService.Instance.CallEndpointAsync<UploadSaveResponse>("uploadSave", args);

                if (response != null)
                {
                    if (response.gameStampMs > 0) LastGameUploadServerMs = response.gameStampMs;
                    if (response.legacyStampMs > 0) LastLegacyUploadServerMs = response.legacyStampMs;
                }

                PlayerPrefs.SetInt(PendingUploadKey, 0);
                PlayerPrefs.Save();
                Log.Info($"[CloudSaveManager] 클라우드 저장 완료 (Day {gameData.currentDay}, serverStamp {response?.gameStampMs ?? 0})");
            }
            catch (Exception e)
            {
                PlayerPrefs.SetInt(PendingUploadKey, 1);
                PlayerPrefs.Save();
                Log.Warn($"[CloudSaveManager] 클라우드 저장 실패 — 재연결 시 재시도 예정: {e.Message}");
            }
        }

        /// <summary>
        /// legacy(영구 데이터)만 단독 업로드한다. legacy 독립 동기화용
        /// </summary>
        public async Task UploadLegacyAsync(PlayerData playerData)
        {
            if (playerData == null || !IsReady()) return;
            if (LegacyManager.Instance?.IsLegacyCorrupted ?? false)
            {
                Log.Warn("[CloudSaveManager] legacy 손상 상태 - 단독 업로드 차단");
                return;
            }

            try
            {
                // Cloud Code(uploadSave) 경유 - 서버 시각 스탬프 기록
                var args = new Dictionary<string, object>
                {
                    { "playerDataJson", "" },
                    { "legacyDataJson", JsonUtility.ToJson(playerData) }
                };
                var response = await CloudCodeService.Instance.CallEndpointAsync<UploadSaveResponse>("uploadSave", args);

                if (response != null && response.legacyStampMs > 0)
                    LastLegacyUploadServerMs = response.legacyStampMs;

                Log.Info($"[CloudSaveManager] legacy 단독 업로드 완료 (serverStamp {response?.legacyStampMs ?? 0})");
            }
            catch (Exception e)
            {
                Log.Warn($"[CloudSaveManager] legacy 단독 업로드 실패: {e.Message}");
            }
        }

        /// <summary>
        /// pendingUpload 플래그가 있을 때 로컬 데이터를 클라우드에 업로드한다.
        /// 네트워크 재연결 시 UGSManager에서 호출한다.
        /// </summary>
        public async Task UploadPendingAsync()
        {
            if (PlayerPrefs.GetInt(PendingUploadKey, 0) == 0) return;

            Log.Info("[CloudSaveManager] 미업로드 데이터 감지 — 클라우드 업로드 시도");

            var gameData = SaveManager.LoadGame();
            var playerData = SaveManager.LoadPlayer();

            if (gameData == null)
            {
                Log.Info("[CloudSaveManager] 업로드할 로컬 데이터 없음. 플래그 초기화.");
                PlayerPrefs.SetInt(PendingUploadKey, 0);
                PlayerPrefs.Save();
                return;
            }

            await UploadAsync(gameData, playerData);
        }

        /// <summary>
        /// 게임오버 시 클라우드의 GameData 키만 빈 문자열로 덮어쓴다.
        /// PlayerData(영구)는 유지. 메인 메뉴 동기화 시 클라우드에서 GameData가 복원되어
        /// 게임오버 상태로 이어 진행되는 버그를 막는다.
        /// 오프라인 등으로 실패하면 pendingCloudClear 플래그를 유지해 재연결 시 재시도한다
        /// </summary>
        public async Task ClearCloudGameDataAsync()
        {
            PlayerPrefs.SetInt(PendingUploadKey, 0);
            PlayerPrefs.SetInt(PendingCloudClearKey, 1);
            PlayerPrefs.Save();

            if (!IsReady()) return;

            try
            {
                var data = new Dictionary<string, object>
                {
                    { "playerData", "" }
                };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);

                PlayerPrefs.SetInt(PendingCloudClearKey, 0);
                PlayerPrefs.Save();
                Log.Info("[CloudSaveManager] 게임오버 - 클라우드 GameData 정리 완료");
            }
            catch (Exception e)
            {
                Log.Warn($"[CloudSaveManager] 게임오버 - 클라우드 GameData 정리 실패. 재연결 시 재시도: {e.Message}");
            }
        }

        /// <summary>
        /// 보류 중인 게임오버 클라우드 정리를 재시도한다.
        /// 동기화/업로드보다 먼저 호출해야 게임오버된 회차가 클라우드에서 부활하지 않는다
        /// </summary>
        public async Task ClearPendingCloudGameDataAsync()
        {
            if (PlayerPrefs.GetInt(PendingCloudClearKey, 0) == 0) return;

            Log.Info("[CloudSaveManager] 보류된 게임오버 클라우드 정리 감지 - 재시도");
            await ClearCloudGameDataAsync();
        }

        #endregion

        #region 불러오기

        public async Task<(GameData gameData, PlayerData playerData, long gameStampMs, long legacyStampMs)> DownloadAsync()
        {
            if (!IsReady()) return (null, null, 0, 0);

            try
            {
                var keys = new HashSet<string> { "playerData", "legacyData", "gameServerSavedAtMs", "legacyServerSavedAtMs" };
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                GameData gameData = null;
                PlayerData playerData = null;
                long gameStampMs = 0;
                long legacyStampMs = 0;

                if (result.TryGetValue("playerData", out var gameItem))
                {
                    string json = gameItem.Value.GetAsString();
                    if (!string.IsNullOrEmpty(json))
                        gameData = JsonUtility.FromJson<GameData>(json);
                }

                if (result.TryGetValue("legacyData", out var playerItem))
                {
                    string json = playerItem.Value.GetAsString();
                    if (!string.IsNullOrEmpty(json))
                        playerData = JsonUtility.FromJson<PlayerData>(json);
                }

                // 서버 시각 스탬프 (uploadSave 기록, 구버전 클라우드 데이터에는 없음 = 0)
                if (result.TryGetValue("gameServerSavedAtMs", out var gameStampItem))
                    long.TryParse(gameStampItem.Value.GetAsString(), out gameStampMs);
                if (result.TryGetValue("legacyServerSavedAtMs", out var legacyStampItem))
                    long.TryParse(legacyStampItem.Value.GetAsString(), out legacyStampMs);

                Log.Info($"[CloudSaveManager] 클라우드 불러오기 완료 (gameData: {gameData != null}, playerData: {playerData != null}, gameStamp: {gameStampMs}, legacyStamp: {legacyStampMs})");
                return (gameData, playerData, gameStampMs, legacyStampMs);
            }
            catch (Exception e)
            {
                Log.Error($"[CloudSaveManager] 클라우드 불러오기 실패: {e.Message}");
                return (null, null, 0, 0);
            }
        }

        #endregion

        #region 복원 코드 — Cloud Code 호출

        /// <summary>
        /// 복원 코드 발급. 코드는 "PlayerId-secret" 형태의 난수 코드다 -
        /// PlayerId만 알아서는 복원이 불가능하다.
        /// </summary>
        public async Task<(GenerateRestoreResult result, string restoreCode)> GenerateRestoreCodeAsync()
        {
            if (!IsReady()) return (GenerateRestoreResult.Error, null);

            try
            {
                var response = await CloudCodeService.Instance.CallEndpointAsync<CloudCodeGenerateResponse>(
                    "generateRestoreCode",
                    new Dictionary<string, object>()
                );

                if (string.IsNullOrEmpty(response?.restoreCode))
                    return (GenerateRestoreResult.Error, null);

                return (GenerateRestoreResult.Success, response.restoreCode);
            }
            catch (CloudCodeException e) when (e.Reason == CloudCodeExceptionReason.ScriptError)
            {
                switch (e.ErrorCode)
                {
                    case 1003: return (GenerateRestoreResult.NoSaveData, null);
                    case 1005: return (GenerateRestoreResult.MustWipeFirst, null); // 이전 복원 사용 완료 - 초기화 선행
                    default:
                        Log.Error($"[CloudSaveManager] generateRestoreCode 스크립트 오류: {e.Message}");
                        return (GenerateRestoreResult.Error, null);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[CloudSaveManager] generateRestoreCode 실패: {e.Message}");
                return (GenerateRestoreResult.Error, null);
            }
        }

        /// <summary>
        /// 활성 복원 코드 재조회 (앱 재시작 후 재표시용).
        /// secret은 본인 Cloud Save에만 있으므로 본인만 코드를 복원할 수 있다
        /// </summary>
        public async Task<string> GetActiveRestoreCodeAsync()
        {
            if (!IsReady()) return null;

            try
            {
                var keys = new HashSet<string> { "restoreSecret" };
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (!result.TryGetValue("restoreSecret", out var secretItem))
                    return null;

                string secret = secretItem.Value.GetAsString();
                string playerId = UGSManager.Instance?.PlayerId;
                if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(playerId))
                    return null;

                return playerId + "-" + secret;
            }
            catch (Exception e)
            {
                Log.Warn($"[CloudSaveManager] 활성 복원 코드 조회 실패: {e.Message}");
                return null;
            }
        }

        public async Task<(RestoreResult result, GameData gameData, PlayerData legacyData)> RedeemRestoreCodeAsync(string restoreCode)
        {
            if (!IsReady()) return (RestoreResult.Error, null, null);

            try
            {
                // 난수 복원 코드("PlayerId-secret")를 그대로 전달한다
                var args = new Dictionary<string, object> { { "restoreCode", restoreCode } };

                var response = await CloudCodeService.Instance.CallEndpointAsync<CloudCodeRedeemResponse>(
                    "redeemRestoreCode",
                    args
                );

                if (string.IsNullOrEmpty(response?.playerDataJson))
                    return (RestoreResult.Error, null, null);

                var gameData = JsonUtility.FromJson<GameData>(response.playerDataJson);

                // legacyData(유산/영구)도 함께 복원. 구버전 스냅샷에는 없을 수 있으므로 옵션 처리
                PlayerData legacyData = null;
                if (!string.IsNullOrEmpty(response.legacyDataJson))
                {
                    try { legacyData = JsonUtility.FromJson<PlayerData>(response.legacyDataJson); }
                    catch (Exception parseEx)
                    {
                        Log.Warn($"[CloudSaveManager] 복원 legacyData 파싱 실패 (무시): {parseEx.Message}");
                    }
                }

                return (RestoreResult.Success, gameData, legacyData);
            }
            catch (CloudCodeException e) when (e.Reason == CloudCodeExceptionReason.ScriptError)
            {
                switch (e.ErrorCode)
                {
                    case 1001: return (RestoreResult.AlreadyUsed, null, null);
                    case 1002: return (RestoreResult.Expired, null, null);
                    case 1003: return (RestoreResult.NotFound, null, null);
                    case 1004: return (RestoreResult.SelfRestore, null, null);
                    default:
                        Log.Error($"[CloudSaveManager] redeemRestoreCode 스크립트 오류: {e.Message}");
                        return (RestoreResult.Error, null, null);
                }
            }
            catch (CloudCodeException e)
            {
                Log.Error($"[CloudSaveManager] redeemRestoreCode 실패: {e.Message}");
                return (RestoreResult.Error, null, null);
            }
        }

        #endregion

        #region 복원 상태 조회 & 초기화

        /// <summary>
        /// 자신의 복원 코드 상태를 조회한다. 로그인 시 및 폴링에서 사용.
        /// </summary>
        public async Task<RestoreStatus> GetRestoreStatusAsync()
        {
            if (!IsReady()) return RestoreStatus.None;

            try
            {
                var keys = new HashSet<string> { "restoreGeneratedAt", "isRestoreUsed" };
                var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (!result.TryGetValue("restoreGeneratedAt", out var tsItem))
                    return RestoreStatus.None;

                string tsStr = tsItem.Value.GetAsString();
                if (string.IsNullOrEmpty(tsStr) || tsStr == "0")
                    return RestoreStatus.None;

                if (result.TryGetValue("isRestoreUsed", out var usedItem) &&
                    usedItem.Value.GetAsString() == "true")
                    return RestoreStatus.Redeemed;

                if (long.TryParse(tsStr, out long generatedAtMs))
                {
                    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (nowMs - generatedAtMs > 24L * 60 * 60 * 1000)
                        return RestoreStatus.Expired;
                }

                return RestoreStatus.Active;
            }
            catch (Exception e)
            {
                Log.Warn($"[CloudSaveManager] 복원 상태 조회 실패: {e.Message}");
                return RestoreStatus.None;
            }
        }

        /// <summary>
        /// 다른 기기에서 복원 코드 사용이 감지됐을 때 이 기기의 데이터를 완전 초기화한다.
        /// 클라우드에는 빈 문자열이 아닌 초기 데이터 구조를 덮어써 서버가 정상 인식하도록 한다.
        /// </summary>
        public async Task WipeDataAsync()
        {
            try
            {
                // 1. 로컬 파일 삭제
                SaveManager.DeleteAllData();

                // 2. Cloud Save playerData / legacyData를 초기 구조로 덮어쓰기
                if (IsReady())
                {
                    var freshGameData = new GameData();
                    var freshPlayerData = new PlayerData();

                    var initial = new Dictionary<string, object>
                    {
                        { "playerData", JsonUtility.ToJson(freshGameData) },
                        { "legacyData", JsonUtility.ToJson(freshPlayerData) },
                        { "gameServerSavedAtMs", "0" },
                        { "legacyServerSavedAtMs", "0" }
                    };
                    await CloudSaveService.Instance.Data.Player.SaveAsync(initial);
                }

                // 3. 복원 키 sentinel 초기화
                await ResetRestoreKeysAsync();

                // 4. 로컬 상태 플래그 전체 정리 - 이전 회차의 세션 토큰/보류 점수/보류 플래그가
                //    남아 있으면 이전 계정 점수 제출이 재시도될 수 있다
                LeaderboardManager.Instance?.ClearLocalLeaderboardState();
                AnalyticsManager.Instance?.ResetFirstTimeKeys();
                PlayerPrefs.DeleteKey("PendingNewGame");
                PlayerPrefs.DeleteKey(LastGameUploadStampKey);
                PlayerPrefs.DeleteKey(LastLegacyUploadStampKey);
                PlayerPrefs.SetInt(PendingUploadKey, 0);
                PlayerPrefs.SetInt(PendingCloudClearKey, 0);
                PlayerPrefs.Save();

                Log.Info("[CloudSaveManager] 데이터 초기화 완료 (진정한 이전)");
            }
            catch (Exception e)
            {
                Log.Error($"[CloudSaveManager] 데이터 초기화 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 복원 스냅샷 키를 sentinel 값으로 덮어써 재사용 불가 상태로 만든다.
        /// </summary>
        public async Task ResetRestoreKeysAsync()
        {
            if (!IsReady()) return;

            try
            {
                var sentinel = new Dictionary<string, object>
                {
                    { "restorePlayerData",  "" },
                    { "restoreLegacyData",  "" },
                    { "restoreSecret",      "" },
                    { "restoreGeneratedAt", "0" },
                    { "isRestoreUsed",      "true" }
                };
                await CloudSaveService.Instance.Data.Player.SaveAsync(sentinel);
                Log.Info("[CloudSaveManager] 복원 키 초기화 완료");
            }
            catch (Exception e)
            {
                Log.Warn($"[CloudSaveManager] 복원 키 초기화 실패 (무시): {e.Message}");
            }
        }

        #endregion

        #region 내부 메서드

        private bool IsReady()
        {
            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
            {
                Log.Warn("[CloudSaveManager] UGS 초기화 안 됨. 작업 건너뜀.");
                return false;
            }
            return true;
        }

        #endregion

        #region 내부 타입

        [Serializable]
        private class CloudCodeRedeemResponse
        {
            public string playerDataJson;
            public string legacyDataJson;
        }

        [Serializable]
        private class CloudCodeGenerateResponse
        {
            public string restoreCode;
        }

        [Serializable]
        private class UploadSaveResponse
        {
            public long gameStampMs;
            public long legacyStampMs;
        }

        #endregion
    }
}
