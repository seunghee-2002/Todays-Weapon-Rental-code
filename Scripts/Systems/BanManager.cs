using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 운영자에 의한 계정 차단 상태를 관리한다.
    /// 진실의 소스(source of truth)는 서버 측 Cloud Save 의 banned/banReason/banUntil 키이며,
    /// 본 매니저는 메모리에만 상태를 보관해 UI 분기에 사용한다.
    ///
    /// 검사 시점:
    ///   - 앱 실행: UGSManager 로그인 직후 CheckBanStatusAsync 호출 (본 매니저가 자체 수행)
    ///   - 랭킹 등록: submitLeaderboardScore Cloud Code 가 자체 검사 후 2006 응답,
    ///                LeaderboardManager 가 NotifyBanned 호출
    /// </summary>
    public class BanManager : BaseManager<BanManager>
    {
        public BanInfo CurrentBan { get; private set; }
        public bool IsBanned => CurrentBan != null;

        public event Action<BanInfo> OnBanDetected;
        public event Action          OnBanCleared;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
                DontDestroyOnLoad(gameObject);

            CurrentBan = null;
            OnBanDetected = null;
            OnBanCleared = null;
        }

        public void Initialize(GameData gameData) { }

        #endregion

        #region 외부 호출

        /// <summary>
        /// 앱 실행 시 UGSManager 가 호출한다. checkBanStatus Cloud Code 의 응답에 따라
        /// NotifyBanned 또는 ClearBan 을 발화한다. 오프라인/실패 시 조용히 리턴.
        /// </summary>
        public async Task CheckBanStatusAsync()
        {
            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
            {
                Log.Warn("[BanManager] CheckBanStatusAsync: UGS 미초기화 — 스킵");
                return;
            }

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Log.Info("[BanManager] CheckBanStatusAsync: 오프라인 — 스킵");
                return;
            }

            try
            {
                var response = await CloudCodeService.Instance.CallEndpointAsync<BanCheckResponse>(
                    "checkBanStatus", new Dictionary<string, object>());

                if (response == null)
                {
                    Log.Warn("[BanManager] checkBanStatus 응답이 null");
                    return;
                }

                if (response.banned)
                    NotifyBanned(new BanInfo { reason = response.reason, until = response.until });
                else
                    ClearBan();
            }
            catch (Exception e)
            {
                Log.Warn($"[BanManager] checkBanStatus 호출 실패 (네트워크 일시 장애 시 정상): {e.Message}");
            }
        }

        /// <summary>
        /// LeaderboardManager 가 Cloud Code 2006 BANNED 를 받았을 때 호출한다.
        /// </summary>
        public void NotifyBanned(BanInfo info)
        {
            if (info == null)
            {
                Log.Warn("[BanManager] NotifyBanned(null) — 무시");
                return;
            }

            CurrentBan = info;
            Log.Info($"[BanManager] 밴 감지: reason='{info.reason}', until={info.until}");
            OnBanDetected?.Invoke(info);
        }

        /// <summary>
        /// 비밴 응답을 받았을 때 호출한다. 운영자가 해제한 경우 UI 가 즉시 정상화된다.
        /// </summary>
        public void ClearBan()
        {
            if (CurrentBan == null) return;

            CurrentBan = null;
            Log.Info("[BanManager] 밴 해제 감지");
            OnBanCleared?.Invoke();
        }

        #endregion

        #region 내부 타입

        [Serializable]
        private class BanCheckResponse
        {
            public bool   banned;
            public string reason;
            public long   until;
        }

        #endregion
    }

    /// <summary>
    /// Cloud Code 응답의 ban 페이로드 매핑.
    /// reason: 운영자가 입력한 사유. until: 0 이면 영구, 그 외 ms epoch.
    /// </summary>
    [Serializable]
    public class BanInfo
    {
        public string reason;
        public long   until;
    }
}
