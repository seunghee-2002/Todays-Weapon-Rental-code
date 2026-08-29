using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace TodaysWeaponRental
{
    public enum NicknameValidationResult { Valid, Empty, TooShort, TooLong, InvalidCharacters, ContainsProfanity }
    public enum NicknameChangeResult     { Success, Empty, TooShort, TooLong, InvalidCharacters, ContainsProfanity, InsufficientLegacy, ServerError }

    /// <summary>
    /// 닉네임 관리 매니저.
    /// 로컬: PlayerData.playerNickname에 미러 저장 (오프라인 표시용 캐시).
    /// 서버: 주 저장소는 Cloud Save Private access class(클라이언트 접근 불가) - 모든 읽기/쓰기가
    ///       Cloud Code(getNicknameInfo/changeNickname)를 경유해 검증 우회가 원천 차단된다.
    /// 비용: nicknameChangeCount &lt; freeChangeCount면 무료, 이후 nicknameChangeCost 유산 차감.
    /// </summary>
    public class NicknameManager : BaseManager<NicknameManager>
    {
        [SerializeField] private NicknameConfig config;

        private int changeCount = 0;
        private bool changeCountLoaded = false;
        private bool changeCountLoading = false;

        public string CurrentNickname { get; private set; } = "";
        public event Action<string> OnNicknameChanged;

        /// <summary>
        /// 서버 닉네임 정보 로드가 끝났을 때 발화 (성공/타임아웃/실패 모두).
        /// 로드 전에는 GetNextChangeCost가 유료로 폴백하므로, 비용을 표시하는 UI는
        /// 이 이벤트로 다시 갱신해야 무료 횟수가 뒤늦게라도 반영된다.
        /// </summary>
        public event Action OnChangeCountLoaded;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            DontDestroyOnLoad(gameObject);
        }

        public void Initialize(GameData gameData) => LoadNickname();

        /// <summary>
        /// 로컬 미러를 읽고, 아직 서버 정보를 못 읽었으면 서버 로드를 시작한다.
        /// InGameScene은 GameManager가, MainMenuScene은 클라우드 동기화 완료 시점이 호출한다.
        /// 이미 읽어둔 상태면 서버를 다시 호출하지 않는다 - 같은 세션의 씬 전환 중에
        /// 다른 기기의 닉네임 변경까지 따라갈 이유는 없다.
        /// </summary>
        public void LoadNickname()
        {
            CurrentNickname = LegacyManager.Instance?.PlayerData?.playerNickname ?? "";

            if (!changeCountLoaded)
                _ = LoadChangeCountFromCloudAsync();

            Log.Info($"[NicknameManager] LoadNickname. CurrentNickname: '{CurrentNickname}', loaded={changeCountLoaded}");
        }

        public void SaveToGameData(GameData gameData)
        {
            // 닉네임은 영구 데이터 — PlayerData(legacy.json)에 미러 저장
            var playerData = LegacyManager.Instance?.PlayerData;
            if (playerData != null)
                playerData.playerNickname = CurrentNickname ?? "";
        }

        /// <summary>
        /// 계정 초기화 시 로컬 닉네임 상태를 비운다.
        /// 비우지 않으면 새 계정 로그인 후 LoadNicknameAsync가 옛 닉네임을 서버로 이관한다.
        /// </summary>
        public void ResetLocalState()
        {
            CurrentNickname = "";
            changeCount = 0;
            changeCountLoaded = false;
            OnNicknameChanged?.Invoke(CurrentNickname);
            Log.Info("[NicknameManager] 로컬 닉네임 상태 초기화");
        }

        /// <summary>
        /// 서버에서 닉네임/변경 횟수를 다시 읽어 로컬 상태를 갱신한다.
        /// Initialize는 InGameScene에서만 호출되므로, 계정 초기화 직후처럼
        /// MainMenuScene에서 갱신이 필요한 시점에는 이 진입점을 쓴다.
        /// </summary>
        public Task RefreshFromCloudAsync() => LoadChangeCountFromCloudAsync();

        #endregion

        #region View로부터 호출되는 메서드

        /// <summary>현재 닉네임이 있으면 그대로, 없으면 마스킹된 PlayerId를 반환한다.</summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(CurrentNickname)) return CurrentNickname;
            string pid = UGSManager.Instance?.PlayerId ?? "";
            if (string.IsNullOrEmpty(pid)) return "-";
            int visible = Mathf.Min(4, pid.Length);
            return pid.Substring(0, visible) + new string('*', pid.Length - visible);
        }

        /// <summary>다음 변경 비용 (changeCount &lt; freeChangeCount면 0, 아니면 nicknameChangeCost).</summary>
        public int GetNextChangeCost()
        {
            if (config == null) return 0;
            // Cloud Save 로드 전엔 안전하게 유료로 취급
            if (!changeCountLoaded) return config.nicknameChangeCost;
            return changeCount < config.freeChangeCount ? 0 : config.nicknameChangeCost;
        }

        /// <summary>클라 1차 검증: 빈값/길이/정규식/욕설 단어 검사.</summary>
        public NicknameValidationResult ValidateLocal(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) return NicknameValidationResult.Empty;
            if (config == null) return NicknameValidationResult.Valid;

            string trimmed = nickname.Trim();
            if (trimmed.Length < config.minLength) return NicknameValidationResult.TooShort;
            if (trimmed.Length > config.maxLength) return NicknameValidationResult.TooLong;

            if (!string.IsNullOrEmpty(config.allowedPattern) && !Regex.IsMatch(trimmed, config.allowedPattern))
                return NicknameValidationResult.InvalidCharacters;

            if (config.profanityWords != null)
            {
                foreach (var word in config.profanityWords)
                {
                    if (string.IsNullOrEmpty(word)) continue;
                    if (trimmed.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                        return NicknameValidationResult.ContainsProfanity;
                }
            }

            return NicknameValidationResult.Valid;
        }

        /// <summary>
        /// 닉네임 변경 전체 흐름: 로컬 검증 → 유산 확인 → 서버 검증/카운트 증가 → 유산 차감 → 로컬 반영.
        /// </summary>
        public async Task<NicknameChangeResult> ChangeNicknameAsync(string newNickname)
        {
            var localResult = ValidateLocal(newNickname);
            if (localResult != NicknameValidationResult.Valid)
                return ToChangeResult(localResult);

            string trimmed = newNickname.Trim();
            int cost = GetNextChangeCost();

            if (cost > 0 && (LegacyManager.Instance == null || !LegacyManager.Instance.HasEnoughLegacyPoints(cost)))
                return NicknameChangeResult.InsufficientLegacy;

            NicknameChangeResponse response;
            try
            {
                var args = new Dictionary<string, object> { { "nickname", trimmed } };
                response = await CloudCodeService.Instance.CallEndpointAsync<NicknameChangeResponse>("changeNickname", args);
            }
            catch (Exception e)
            {
                Log.Warn($"[NicknameManager] 서버 변경 요청 실패: {e.Message}");
                return NicknameChangeResult.ServerError;
            }

            if (response == null || !response.success)
            {
                if (response != null && response.reason == "PROFANITY")
                    return NicknameChangeResult.ContainsProfanity;
                return NicknameChangeResult.ServerError;
            }

            if (cost > 0)
            {
                if (!LegacyManager.Instance.SpendLegacyPoints(cost, "nickname_change"))
                {
                    Log.Warn("[NicknameManager] 서버 변경 성공 후 유산 차감 실패 — 이론상 도달 불가");
                    return NicknameChangeResult.InsufficientLegacy;
                }
            }

            changeCount = response.changeCount;
            changeCountLoaded = true;
            CurrentNickname = trimmed;

            if (LegacyManager.Instance?.PlayerData != null)
            {
                LegacyManager.Instance.PlayerData.playerNickname = trimmed;
                LegacyManager.Instance.SaveLegacy();
            }

            OnNicknameChanged?.Invoke(trimmed);
            Log.Info($"[NicknameManager] 닉네임 변경 완료: '{trimmed}' (changeCount={changeCount})");
            return NicknameChangeResult.Success;
        }

        #endregion

        #region 내부 메서드

        /// <summary>
        /// 서버 로드 1회 실행을 보장하는 래퍼. 진행 중 재호출은 무시하고,
        /// 어떻게 끝나든 OnChangeCountLoaded를 발화해 대기 중인 UI가 갱신되게 한다.
        /// </summary>
        private async Task LoadChangeCountFromCloudAsync()
        {
            if (changeCountLoading) return;
            changeCountLoading = true;

            try
            {
                await LoadNicknameInfoAsync();
            }
            finally
            {
                changeCountLoading = false;
                OnChangeCountLoaded?.Invoke();
            }
        }

        private async Task LoadNicknameInfoAsync()
        {
            // UGS는 UGSManager.Start()에서 비동기로 초기화되므로,
            // 게임 시작 직후엔 IsInitialized=false일 수 있다. 일정 시간 대기 후 진행.
            const int MaxWaitMs = 5000;
            int waitedMs = 0;
            while ((UGSManager.Instance == null || !UGSManager.Instance.IsInitialized) && waitedMs < MaxWaitMs)
            {
                await Task.Delay(100);
                waitedMs += 100;
            }

            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
            {
                Log.Warn($"[NicknameManager] UGS 초기화 대기 타임아웃 ({waitedMs}ms) — Cloud Save 로드 포기");
                changeCountLoaded = false;
                return;
            }

            Log.Info($"[NicknameManager] UGS 준비됨 (대기 {waitedMs}ms, playerId={UGSManager.Instance.PlayerId}) — 닉네임 정보 로드 시작");

            try
            {
                // 닉네임/횟수는 Private 저장소(클라이언트 접근 불가)에 있어 Cloud Code로만 조회한다
                var info = await CloudCodeService.Instance.CallEndpointAsync<NicknameInfoResponse>(
                    "getNicknameInfo", new Dictionary<string, object>());

                changeCount = info?.changeCount ?? 0;

                string cloudNick = info?.nickname ?? "";
                bool hasCloudNickname = !string.IsNullOrWhiteSpace(cloudNick);

                // 서버에 닉네임이 있으면 서버 본을 정본으로 채택
                // (다른 기기에서 설정한 경우, 로컬 데이터 초기화 후 재설치 시 등)
                if (hasCloudNickname && CurrentNickname != cloudNick)
                {
                    CurrentNickname = cloudNick;
                    if (LegacyManager.Instance?.PlayerData != null)
                        LegacyManager.Instance.PlayerData.playerNickname = cloudNick;
                    OnNicknameChanged?.Invoke(cloudNick);
                    Log.Info($"[NicknameManager] 서버에서 닉네임 복원: '{cloudNick}'");
                }

                // 서버에 닉네임 없음 → 기본(또는 로컬 미러) 닉네임을 isInitial로 부여.
                // isInitial은 서버에서 횟수를 증가시키지 않고, 이미 있으면 기존 값을 반환한다 (멱등)
                if (!hasCloudNickname)
                {
                    string initialNick = !string.IsNullOrWhiteSpace(CurrentNickname)
                        ? CurrentNickname   // 구버전 유저의 로컬 미러를 서버로 이관
                        : GenerateDefaultNickname(UGSManager.Instance.PlayerId);

                    string applied = await TrySetInitialNicknameAsync(initialNick);

                    // 로컬 미러가 서버 검증(확장된 금칙어 등)에 걸리면 기본 닉네임으로 재시도
                    if (applied == null && initialNick != GenerateDefaultNickname(UGSManager.Instance.PlayerId))
                        applied = await TrySetInitialNicknameAsync(GenerateDefaultNickname(UGSManager.Instance.PlayerId));

                    if (applied != null && CurrentNickname != applied)
                    {
                        CurrentNickname = applied;
                        if (LegacyManager.Instance?.PlayerData != null)
                            LegacyManager.Instance.PlayerData.playerNickname = applied;
                        OnNicknameChanged?.Invoke(applied);
                        Log.Info($"[NicknameManager] 첫 접속 — 기본 닉네임 부여: '{applied}'");
                    }
                }

                changeCountLoaded = true;
                Log.Info($"[NicknameManager] 닉네임 정보 로드 완료: changeCount={changeCount}, nickname='{CurrentNickname}'");
            }
            catch (Exception e)
            {
                Log.Error($"[NicknameManager] 닉네임 정보 로드 예외: {e.GetType().Name}: {e.Message}");
                changeCountLoaded = false;
            }
        }

        /// <summary>
        /// 기본 닉네임을 changeNickname(isInitial)로 서버에 등록한다. 성공 시 적용된 닉네임, 실패 시 null
        /// </summary>
        private async Task<string> TrySetInitialNicknameAsync(string nickname)
        {
            try
            {
                var args = new Dictionary<string, object>
                {
                    { "nickname", nickname },
                    { "isInitial", true }
                };
                var response = await CloudCodeService.Instance.CallEndpointAsync<NicknameChangeResponse>("changeNickname", args);

                if (response == null || !response.success) return null;
                // 다른 기기가 먼저 설정했다면 서버가 그 닉네임을 돌려준다
                return string.IsNullOrWhiteSpace(response.nickname) ? nickname : response.nickname;
            }
            catch (Exception e)
            {
                Log.Warn($"[NicknameManager] 기본 닉네임 등록 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// PlayerId 기반 기본 닉네임 생성. "Player" + PlayerId 앞 6자 (총 최대 12자, maxLength와 일치).
        /// LeaderboardManager 가 닉네임 미등록 PlayerId 의 fallback 표시명으로도 사용한다.
        /// </summary>
        public static string GenerateDefaultNickname(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return "Player";
            int suffixLen = Mathf.Min(6, playerId.Length);
            return "Player" + playerId.Substring(0, suffixLen);
        }

        private static NicknameChangeResult ToChangeResult(NicknameValidationResult validation) => validation switch
        {
            NicknameValidationResult.Empty             => NicknameChangeResult.Empty,
            NicknameValidationResult.TooShort          => NicknameChangeResult.TooShort,
            NicknameValidationResult.TooLong           => NicknameChangeResult.TooLong,
            NicknameValidationResult.InvalidCharacters => NicknameChangeResult.InvalidCharacters,
            NicknameValidationResult.ContainsProfanity => NicknameChangeResult.ContainsProfanity,
            _                                          => NicknameChangeResult.Success,
        };

        #endregion

        #region 내부 타입

        [Serializable]
        private class NicknameChangeResponse
        {
            public bool success;
            public string reason;
            public int changeCount;
            public string nickname;
        }

        [Serializable]
        private class NicknameInfoResponse
        {
            public string nickname;
            public int changeCount;
        }

        #endregion
    }
}
