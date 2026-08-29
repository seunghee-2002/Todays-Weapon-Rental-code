using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace TodaysWeaponRental
{
    public class LeaderboardManager : BaseManager<LeaderboardManager>
    {
        private const string SessionTokenKey            = "leaderboardSessionToken";
        private const string PendingScoreKey            = "pendingLeaderboardScore";
        private const string PendingSessionTokenKey     = "pendingLeaderboardSessionToken";
        private const string LegacyPendingNicknameKey   = "pendingLeaderboardNickname"; // 기존 디바이스의 잔여 키 cleanup 용
        private const int    NoPendingScore             = -1;

        private const string LeaderboardId            = "final-days";
        private const double NicknameCacheTtlSeconds  = 60 * 60; // 1시간

        private readonly NicknameCache nicknameCache = new NicknameCache();

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
                DontDestroyOnLoad(gameObject);
        }

        public void Initialize(GameData gameData)
        {
            // 닉네임을 metadata 에 담던 시절의 잔여 키 정리 (현재 시점 dead key)
            if (PlayerPrefs.HasKey(LegacyPendingNicknameKey))
            {
                PlayerPrefs.DeleteKey(LegacyPendingNicknameKey);
                PlayerPrefs.Save();
            }
        }

        #endregion

        #region View로부터 호출되는 메서드

        /// <summary>
        /// 새 게임 시작 시 호출. Cloud Code에서 세션 토큰을 발급받아 PlayerPrefs에 저장한다.
        /// 오프라인이면 조용히 실패 — 세션 토큰 없이 진행되며 제출 시 서버에서 거부된다.
        /// </summary>
        public async Task StartGameSessionAsync()
        {
            Log.Info($"[LeaderboardManager] StartGameSessionAsync 진입 (prevToken={Truncate(PlayerPrefs.GetString(SessionTokenKey, ""), 8)})");
            if (!IsReady())
            {
                Log.Warn("[LeaderboardManager] StartGameSession: IsReady=false 라 종료. 토큰 미갱신.");
                return;
            }

            try
            {
                var response = await CloudCodeService.Instance.CallEndpointAsync<SessionStartResponse>(
                    "startGameSession",
                    new Dictionary<string, object>()
                );

                if (!string.IsNullOrEmpty(response?.sessionToken))
                {
                    PlayerPrefs.SetString(SessionTokenKey, response.sessionToken);
                    PlayerPrefs.Save();
                    Log.Info($"[LeaderboardManager] 게임 세션 시작됨 (newToken={Truncate(response.sessionToken, 8)})");
                }
                else
                {
                    Log.Warn("[LeaderboardManager] StartGameSession 응답에 sessionToken 없음");
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[LeaderboardManager] 세션 시작 실패 (오프라인 시 정상): {e.Message}");
            }
        }

        private static string Truncate(string s, int len) =>
            string.IsNullOrEmpty(s) ? "(empty)" : (s.Length <= len ? s : s.Substring(0, len) + "...");

        /// <summary>
        /// 게임오버 시 호출. Cloud Code를 통해 검증 후 리더보드에 제출한다.
        /// 오프라인이면 점수와 세션 토큰을 함께 보류 저장하여 재연결 시 제출한다.
        /// </summary>
        public async Task SubmitScoreAsync(int score)
        {
            string token = PlayerPrefs.GetString(SessionTokenKey, "");
            Log.Info($"[LeaderboardManager] SubmitScoreAsync 시도: score={score}, token={Truncate(token, 8)}");
            await SubmitScoreInternalAsync(score, token);
        }

        /// <summary>
        /// 상위 limit개 점수를 조회한다. 오프라인이거나 실패 시 빈 리스트 반환.
        /// entry 의 PlayerId 들을 모아 getPlayerNicknames Cloud Code 로 닉네임 일괄 매핑한다 (TTL 1h 캐시).
        /// </summary>
        public async Task<List<RankEntry>> FetchTopScoresAsync(int limit = 100)
        {
            if (!IsReady()) return new List<RankEntry>();

            try
            {
                var options  = new GetScoresOptions { Limit = limit, IncludeMetadata = false };
                var response = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, options);

                if (response?.Results == null || response.Results.Count == 0)
                    return new List<RankEntry>();

                string myPlayerId = UGSManager.Instance?.PlayerId ?? "";
                var idsToFetch = new List<string>();
                foreach (var entry in response.Results)
                {
                    if (string.IsNullOrEmpty(entry.PlayerId)) continue;
                    if (entry.PlayerId == myPlayerId) continue;             // 본인은 CurrentNickname 사용
                    if (nicknameCache.TryGet(entry.PlayerId, out _)) continue;
                    idsToFetch.Add(entry.PlayerId);
                }

                if (idsToFetch.Count > 0)
                    await EnsureNicknamesCachedAsync(idsToFetch);

                var result = new List<RankEntry>(response.Results.Count);
                foreach (var entry in response.Results)
                {
                    result.Add(new RankEntry(
                        entry.Rank + 1,
                        ResolveDisplayName(entry.PlayerId),
                        (int)entry.Score
                    ));
                }
                return result;
            }
            catch (Exception e)
            {
                Log.Warn($"[LeaderboardManager] 상위 점수 조회 실패: {e.Message}");
                return new List<RankEntry>();
            }
        }

        /// <summary>
        /// 내 순위를 조회한다. 등록된 점수가 없거나 실패 시 null 반환.
        /// 본인 닉네임은 NicknameManager.CurrentNickname 을 우선 사용 (변경 직후도 즉시 반영).
        /// </summary>
        public async Task<RankEntry> FetchMyRankAsync()
        {
            if (!IsReady()) return null;

            try
            {
                var options = new GetPlayerScoreOptions { IncludeMetadata = false };
                LeaderboardEntry entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId, options);
                return new RankEntry(
                    entry.Rank + 1,
                    ResolveDisplayName(entry.PlayerId),
                    (int)entry.Score
                );
            }
            catch (Exception e)
            {
                Log.Warn($"[LeaderboardManager] 내 순위 조회 실패 (등록 점수 없음 시 정상): {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 네트워크 재연결 시 UGSManager에서 호출. 보류 점수가 있으면 제출한다.
        /// </summary>
        public async Task TrySubmitPendingScore()
        {
            int pending = PlayerPrefs.GetInt(PendingScoreKey, NoPendingScore);
            if (pending == NoPendingScore) return;

            string pendingToken = PlayerPrefs.GetString(PendingSessionTokenKey, "");
            Log.Info($"[LeaderboardManager] 보류 점수 감지({pending}일) — 제출 시도");
            await SubmitScoreInternalAsync(pending, pendingToken);
        }

        #endregion

        #region 내부 메서드

        private async Task SubmitScoreInternalAsync(int score, string sessionToken)
        {
            // 요청 전에 먼저 보류 저장 - 요청 도중 앱이 강제 종료되어도
            // 다음 실행의 TrySubmitPendingScore가 재시도한다. 성공/영구 실패 시에만 정리된다
            SavePendingScore(score, sessionToken);

            if (!IsReady())
                return;

            try
            {
                var args = new Dictionary<string, object>
                {
                    { "claimedDays",  score        },
                    { "sessionToken", sessionToken }
                };

                await CloudCodeService.Instance.CallEndpointAsync("submitLeaderboardScore", args);
                Log.Info($"[LeaderboardManager] 리더보드 제출 완료: {score}일");
                ClearPendingScore();
                ClearSessionToken();
            }
            catch (CloudCodeException e) when (e.Reason == CloudCodeExceptionReason.ScriptError)
            {
                // applicationErrorCode 가 SDK 단계에서 9009 로 매핑되는 경우가 있어
                // message 에 ban payload 가 포함됐는지로도 분기한다.
                if (TryExtractBanInfo(e.Message, out var banInfo))
                {
                    HandleBannedInternal(banInfo, "SubmitScore");
                    return;
                }

                switch (e.ErrorCode)
                {
                    case 2001:
                        Log.Warn($"[LeaderboardManager] INVALID_SCORE — 점수 범위 오류 (score={score})");
                        ClearPendingScore();
                        break;
                    case 2002:
                        Log.Warn($"[LeaderboardManager] INVALID_SESSION — 세션 토큰 오류 또는 HMAC 위조");
                        ClearPendingScore();
                        ClearSessionToken();
                        break;
                    case 2003:
                        Log.Warn($"[LeaderboardManager] SESSION_REPLAYED — 이미 사용된 세션 토큰");
                        ClearPendingScore();
                        ClearSessionToken();
                        break;
                    case 2004:
                        Log.Warn($"[LeaderboardManager] TIME_VIOLATION — 플레이 시간 부족 (score={score})");
                        ClearPendingScore();
                        break;
                    case 2005:
                        // 서버는 step 5에서 이미 sessionTokenUsed=true를 마쳤으므로,
                        // 같은 토큰으로 재시도하면 SESSION_REPLAYED. 다음 게임이 깨끗하게 시작되도록 세션도 정리.
                        Log.Warn("[LeaderboardManager] LEADERBOARD_ERROR — Leaderboards API 실패. 대시보드에서 final-days 리더보드 생성 여부 확인 필요");
                        ClearSessionToken();
                        break;
                    case 2006:
                        // 도달 시 message 에서 추출 (SDK 가 매핑하면 진입, 아니면 위쪽 TryExtractBanInfo 가 이미 처리)
                        if (TryExtractBanInfo(e.Message, out var ban))
                            HandleBannedInternal(ban, "SubmitScore");
                        else
                            HandleBannedInternal(new BanInfo { reason = "", until = 0 }, "SubmitScore");
                        break;
                    default:
                        Log.Warn($"[LeaderboardManager] Cloud Code 오류 (code={e.ErrorCode}): {e.Message}");
                        ClearPendingScore();
                        ClearSessionToken();
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[LeaderboardManager] 리더보드 제출 실패 — 보류 저장: {e.Message}");
                SavePendingScore(score, sessionToken);
            }
        }

        /// <summary>
        /// 주어진 PlayerId 들을 Cloud Code 로 일괄 조회해 캐시에 채운다.
        /// 호출 실패 시 캐시는 갱신되지 않고, ResolveDisplayName 이 MaskPlayerId fallback 으로 처리한다.
        /// </summary>
        private async Task EnsureNicknamesCachedAsync(List<string> playerIds)
        {
            if (playerIds == null || playerIds.Count == 0) return;

            try
            {
                var args = new Dictionary<string, object> { { "playerIds", playerIds } };
                var response = await CloudCodeService.Instance.CallEndpointAsync<GetPlayerNicknamesResponse>(
                    "getPlayerNicknames", args);

                int resolved = 0;
                if (response?.nicknames != null)
                {
                    foreach (var kvp in response.nicknames)
                    {
                        if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value)) continue;
                        nicknameCache.Set(kvp.Key, kvp.Value);
                        resolved++;
                    }
                }
                Log.Info($"[LeaderboardManager] 닉네임 캐시 갱신: requested={playerIds.Count}, resolved={resolved}");
            }
            catch (Exception e)
            {
                Log.Warn($"[LeaderboardManager] getPlayerNicknames 호출 실패: {e.Message}");
            }
        }

        /// <summary>
        /// PlayerId 의 표시명 결정.
        /// 우선순위: 본인이면 CurrentNickname > 캐시 > "Player + PlayerId6자" (default 닉네임과 동일 형식).
        /// </summary>
        private string ResolveDisplayName(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return "Player";

            string myPlayerId = UGSManager.Instance?.PlayerId ?? "";
            if (playerId == myPlayerId)
            {
                string mine = NicknameManager.Instance?.CurrentNickname;
                if (!string.IsNullOrWhiteSpace(mine)) return mine;
            }

            if (nicknameCache.TryGet(playerId, out string cached)) return cached;
            return NicknameManager.GenerateDefaultNickname(playerId);
        }

        private bool IsReady()
        {
            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
            {
                Log.Warn("[LeaderboardManager] UGS 초기화 안 됨. 점수 보류.");
                return false;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Log.Warn("[LeaderboardManager] 오프라인 상태. 점수 보류.");
                return false;
            }

            return true;
        }

        private void SavePendingScore(int score, string sessionToken)
        {
            PlayerPrefs.SetInt(PendingScoreKey, score);
            PlayerPrefs.SetString(PendingSessionTokenKey, sessionToken);
            PlayerPrefs.Save();
            Log.Info($"[LeaderboardManager] 보류 점수 저장: {score}일");
        }

        private void ClearPendingScore()
        {
            PlayerPrefs.DeleteKey(PendingScoreKey);
            PlayerPrefs.DeleteKey(PendingSessionTokenKey);
            PlayerPrefs.Save();
        }

        private void ClearSessionToken()
        {
            PlayerPrefs.DeleteKey(SessionTokenKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 로컬 리더보드 상태(세션 토큰/보류 점수/닉네임 캐시) 전체 정리.
        /// 기기 이전 wipe 등 이전 계정/회차의 점수가 제출되면 안 되는 시점에 호출한다.
        /// 닉네임 캐시(TTL 1h)를 비우지 않으면 계정 초기화 직후 랭킹에 옛 닉네임이 그대로 보인다
        /// </summary>
        public void ClearLocalLeaderboardState()
        {
            ClearPendingScore();
            ClearSessionToken();
            PlayerPrefs.DeleteKey(LegacyPendingNicknameKey);
            PlayerPrefs.Save();
            nicknameCache.Clear();
        }

        /// <summary>
        /// Cloud Code 가 throw 한 ban 페이로드를 e.Message 에서 추출한다.
        /// SDK 가 applicationErrorCode 를 9009 로 매핑해도 message 안의 JSON 으로 ban 판별 가능.
        /// 페이로드 형식: {"reason":"...","until":N}
        /// </summary>
        private static readonly Regex BanPayloadRegex =
            new Regex("\\{\"reason\":\"[^\"]*\",\"until\":\\d+\\}", RegexOptions.Compiled);

        private bool TryExtractBanInfo(string message, out BanInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(message)) return false;

            var match = BanPayloadRegex.Match(message);
            if (!match.Success) return false;

            try
            {
                info = JsonUtility.FromJson<BanInfo>(match.Value);
                return info != null;
            }
            catch (Exception parseEx)
            {
                Log.Warn($"[LeaderboardManager] BAN 페이로드 파싱 실패: {parseEx.Message}. raw='{match.Value}'");
                return false;
            }
        }

        /// <summary>
        /// 보류 점수/세션 토큰을 영구 폐기하고 BanManager 에 통보한다.
        /// </summary>
        private void HandleBannedInternal(BanInfo info, string source)
        {
            Log.Warn($"[LeaderboardManager] BANNED ({source}): reason='{info?.reason}', until={info?.until}");
            ClearPendingScore();
            ClearSessionToken();
            BanManager.Instance?.NotifyBanned(info);
        }

        #endregion

        #region 내부 타입

        [Serializable]
        private class SessionStartResponse
        {
            public string sessionToken;
        }

        // Cloud Code 응답 deserializer 는 Newtonsoft.Json 기반이라 Dictionary<string, string> 직접 매핑 가능.
        // (Unity JsonUtility 였다면 List<{playerId, nickname}> 로 풀어야 했음)
        private class GetPlayerNicknamesResponse
        {
            public Dictionary<string, string> nicknames;
        }

        /// <summary>
        /// PlayerId → 닉네임 메모리 캐시. TTL 1시간, Lazy Loading, 게임 세션 단위 (PlayerPrefs 미저장).
        /// </summary>
        private class NicknameCache
        {
            private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

            public bool TryGet(string playerId, out string nickname)
            {
                nickname = null;
                if (string.IsNullOrEmpty(playerId)) return false;
                if (!entries.TryGetValue(playerId, out var e)) return false;
                if ((DateTime.UtcNow - e.FetchedAt).TotalSeconds > NicknameCacheTtlSeconds)
                {
                    entries.Remove(playerId);
                    return false;
                }
                nickname = e.Nickname;
                return true;
            }

            public void Set(string playerId, string nickname)
            {
                if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(nickname)) return;
                entries[playerId] = new Entry { Nickname = nickname, FetchedAt = DateTime.UtcNow };
            }

            public void Clear() => entries.Clear();

            private struct Entry
            {
                public string Nickname;
                public DateTime FetchedAt;
            }
        }

        #endregion
    }

    /// <summary>
    /// 리더보드 항목 데이터 (순위, 이름, 점수)
    /// </summary>
    public class RankEntry
    {
        public int    Rank       { get; }
        public string PlayerName { get; }
        public int    Score      { get; }

        public RankEntry(int rank, string playerName, int score)
        {
            Rank       = rank;
            PlayerName = playerName;
            Score      = score;
        }
    }
}
