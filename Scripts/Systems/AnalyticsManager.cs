using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// UGS Analytics 커스텀 이벤트 발행 허브. 이벤트 정의는 Documents/Analytics_이벤트_설계.md 참조.
    /// - 공통 파라미터(day, game_time_min)를 모든 이벤트에 자동 첨부한다.
    /// - 에디터에서는 실지표 오염 방지를 위해 전송하지 않고 로그만 출력한다.
    /// - StartCollection() 호출 전에는 아무것도 전송되지 않는다 (자동 지표 포함).
    /// </summary>
    public class AnalyticsManager : BaseManager<AnalyticsManager>
    {
        [Serializable]
        private class FirstTimeKeyList
        {
            public List<string> keys = new List<string>();
        }

        private const string FirstTimeKeysPrefKey = "analytics_first_time_keys";
        private const string OptOutPrefKey = "analytics_opt_out";
        private const string ConsentAskedPrefKey = "analytics_consent_asked";

        public bool IsCollecting { get; private set; }

        /// <summary>
        /// 데이터 수집 철회 여부. 기기 단위 설정이라 계정 초기화(WipeDataAsync)에도 유지된다.
        /// </summary>
        public bool IsOptedOut => PlayerPrefs.GetInt(OptOutPrefKey, 0) == 1;

        /// <summary>
        /// 동의 팝업(DataCollectionView)으로 의사를 물어본 적이 있는지.
        /// IsOptedOut만으로는 "동의함"과 "아직 안 물어봄"을 구분할 수 없어 별도 키로 둔다.
        /// </summary>
        public bool HasAskedConsent => PlayerPrefs.GetInt(ConsentAskedPrefKey, 0) == 1;

        private HashSet<string> firstTimeKeys = new HashSet<string>();
        private float dialogOpenRealtime = -1f;

        // 패널별 오픈 시각(panel 문자열 -> realtime). panel_closed의 duration_sec 계산용.
        // 패널 인스턴스가 타입당 1개이므로 문자열 키만으로 중첩/재오픈이 자연스럽게 처리된다.
        private readonly Dictionary<string, float> panelOpenRealtime = new Dictionary<string, float>();

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
            {
                DontDestroyOnLoad(gameObject);
                LoadFirstTimeKeys();
            }
        }

        #endregion

        #region 수집 제어

        /// <summary>
        /// 데이터 수집 시작 (DAU 등 자동 지표 + 커스텀 이벤트).
        /// UGSManager가 초기화 직후 호출하지만, 동의 전이거나 철회 상태면 아무것도 하지 않는다.
        /// 최초 실행에서는 동의 팝업이 SetConsent를 거쳐 이 메서드를 다시 호출한다.
        /// </summary>
        public void StartCollection()
        {
            if (IsCollecting) return;

            if (!HasAskedConsent)
            {
                Log.Info("[AnalyticsManager] 동의 확인 전 - 데이터 수집을 시작하지 않음");
                return;
            }

            if (IsOptedOut)
            {
                Log.Info("[AnalyticsManager] 수집 철회 상태 - 데이터 수집을 시작하지 않음");
                return;
            }

            try
            {
                AnalyticsService.Instance.StartDataCollection();
                IsCollecting = true;
                Log.Info("[AnalyticsManager] 데이터 수집 시작");
            }
            catch (Exception e)
            {
                Log.Warn($"[AnalyticsManager] 데이터 수집 시작 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 최초 실행 동의 팝업의 선택을 저장한다.
        /// 수집 여부보다 동의 확인 플래그를 먼저 기록해야 SetOptOut 안의 StartCollection이 가드에 걸리지 않는다.
        /// </summary>
        public void SetConsent(bool agreed)
        {
            PlayerPrefs.SetInt(ConsentAskedPrefKey, 1);
            PlayerPrefs.Save();

            SetOptOut(!agreed);
            Log.Info($"[AnalyticsManager] 데이터 수집 동의: {(agreed ? "동의" : "동의 안 함")}");
        }

        /// <summary>
        /// 데이터 수집 철회/재동의를 저장하고 즉시 반영한다.
        /// 철회해도 서버에 이미 쌓인 과거 데이터는 삭제하지 않는다 (수집 중단만).
        /// </summary>
        public void SetOptOut(bool optOut)
        {
            PlayerPrefs.SetInt(OptOutPrefKey, optOut ? 1 : 0);
            PlayerPrefs.Save();

            if (optOut)
                StopCollection();
            else
                StartCollection();
        }

        /// <summary>
        /// "최초 1회" 이벤트 발행 이력을 비운다 (계정 초기화용).
        /// 수집 동의 상태(IsOptedOut/HasAskedConsent)는 기기 단위 설정이라 건드리지 않는다.
        /// </summary>
        public void ResetFirstTimeKeys()
        {
            firstTimeKeys.Clear();
            PlayerPrefs.DeleteKey(FirstTimeKeysPrefKey);
            PlayerPrefs.Save();
            Log.Info("[AnalyticsManager] first_time 키 초기화");
        }

        private void StopCollection()
        {
            if (!IsCollecting) return;

            try
            {
                AnalyticsService.Instance.StopDataCollection();
                IsCollecting = false;
                Log.Info("[AnalyticsManager] 데이터 수집 중단");
            }
            catch (Exception e)
            {
                Log.Warn($"[AnalyticsManager] 데이터 수집 중단 실패: {e.Message}");
            }
        }

        #endregion

        #region 이벤트 발행

        /// <summary>
        /// 커스텀 이벤트 발행. 공통 파라미터(day, game_time_min)는 자동 첨부된다.
        /// 파라미터 값은 string/int/long/float/double/bool만 허용.
        /// </summary>
        public void Send(string eventName, Dictionary<string, object> parameters = null)
        {
            var merged = new Dictionary<string, object>();

            if (TimeManager.Instance != null)
            {
                merged["day"] = TimeManager.Instance.CurrentDay;
                merged["game_time_min"] = TimeManager.Instance.CurrentHour * 60 + TimeManager.Instance.CurrentMinute;
            }

            if (parameters != null)
            {
                foreach (var pair in parameters)
                    merged[pair.Key] = pair.Value;
            }

            if (Application.isEditor)
            {
                Log.Info($"[AnalyticsManager] (에디터-미전송) {eventName} {FormatParameters(merged)}");
                return;
            }

            if (!IsCollecting) return;

            try
            {
                var customEvent = new CustomEvent(eventName);
                foreach (var pair in merged)
                    customEvent.Add(pair.Key, pair.Value);

                AnalyticsService.Instance.RecordEvent(customEvent);
            }
            catch (Exception e)
            {
                Log.Warn($"[AnalyticsManager] 이벤트 발행 실패 ({eventName}): {e.Message}");
            }
        }

        /// <summary>
        /// 버퍼에 쌓인 이벤트를 즉시 업로드한다.
        /// SDK는 60초 주기로만 전송하므로, 게임오버처럼 직후에 앱이 꺼질 수 있는 시점에서만 호출한다.
        /// 오프라인이면 전송에 실패하지만 이벤트는 SDK 버퍼에 남아 다음 기회에 올라간다.
        /// </summary>
        public void Flush()
        {
            if (!IsCollecting) return;

            try
            {
                AnalyticsService.Instance.Flush();
            }
            catch (Exception e)
            {
                Log.Warn($"[AnalyticsManager] 즉시 전송 실패: {e.Message}");
            }
        }

        /// <summary>
        /// day_begin 발행. 새 게임 1일차 시작(GameManager)과 매일 아침(TimeManager.GoToNextDay) 두 곳에서 호출된다.
        /// </summary>
        public void SendDayBegin()
        {
            Send("day_begin", new Dictionary<string, object>
            {
                { "gold", EconomyManager.Instance?.CurrentGold ?? 0 },
                { "reputation", ReputationManager.Instance?.CurrentReputation ?? 0 },
                { "reputation_level", (ReputationManager.Instance?.CurrentLevel ?? ReputationLevel.Bronze).ToString() },
                { "insight", InsightManager.Instance != null ? InsightManager.Instance.CurrentInsight : 0 }
            });
        }

        /// <summary>
        /// Level 2 panel_opened. 오픈 시각을 기록해 panel_closed의 duration_sec에 사용한다.
        /// 패널별 추가 파라미터(blacksmith_type, delay_real_min)는 호출부가 extra로 넘긴다.
        /// </summary>
        public void SendPanelOpened(string panel, Dictionary<string, object> extra = null)
        {
            panelOpenRealtime[panel] = Time.realtimeSinceStartup;

            var parameters = new Dictionary<string, object>
            {
                { "panel", panel },
                { "is_first_time", CheckAndMarkFirstTime(panel) }
            };

            if (extra != null)
            {
                foreach (var pair in extra)
                    parameters[pair.Key] = pair.Value;
            }

            Send("panel_opened", parameters);
        }

        /// <summary>
        /// Level 2 panel_closed. 오픈 기록이 없으면 duration_sec을 생략한다(탭 가상 패널 등).
        /// </summary>
        public void SendPanelClosed(string panel)
        {
            var parameters = new Dictionary<string, object> { { "panel", panel } };

            if (panelOpenRealtime.TryGetValue(panel, out float openedAt))
            {
                parameters["duration_sec"] = Mathf.RoundToInt(Time.realtimeSinceStartup - openedAt);
                panelOpenRealtime.Remove(panel);
            }

            Send("panel_closed", parameters);
        }

        /// <summary>
        /// Level 3 btn_clicked. extra는 버튼별 추가 파라미터(Documents/Analytics_이벤트_설계.md Level 3 표).
        /// </summary>
        public void SendButtonClick(string panel, string button, Dictionary<string, object> extra = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "panel", panel },
                { "button", button },
                { "is_first_time", CheckAndMarkFirstTime($"{panel}_{button}") }
            };

            if (extra != null)
            {
                foreach (var pair in extra)
                    parameters[pair.Key] = pair.Value;
            }

            Send("btn_clicked", parameters);
        }

        /// <summary>
        /// G3 is_first_time: 해당 키가 처음이면 true를 반환하고 기록에 추가한다.
        /// 키 형식: "{panel}" 또는 "{panel}_{button}".
        /// </summary>
        public bool CheckAndMarkFirstTime(string key)
        {
            if (firstTimeKeys.Contains(key)) return false;

            firstTimeKeys.Add(key);
            SaveFirstTimeKeys();
            return true;
        }

        /// <summary>G1: 방문자 대화 패널이 열린 시각을 기록한다 (세션 내 측정이므로 realtime 사용).</summary>
        public void RecordDialogOpenTime()
        {
            dialogOpenRealtime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// G1: 대화 패널이 열린 뒤 경과한 실시간 초를 반환하고 기록을 지운다.
        /// 기록이 없으면 -1 (호출부에서 파라미터 제외).
        /// </summary>
        public int ConsumeDialogOpenDurationSec()
        {
            if (dialogOpenRealtime < 0f) return -1;

            int elapsed = Mathf.RoundToInt(Time.realtimeSinceStartup - dialogOpenRealtime);
            dialogOpenRealtime = -1f;
            return elapsed;
        }

        #endregion

        #region 내부 메서드

        private void LoadFirstTimeKeys()
        {
            firstTimeKeys.Clear();

            string json = PlayerPrefs.GetString(FirstTimeKeysPrefKey, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var list = JsonUtility.FromJson<FirstTimeKeyList>(json);
                if (list?.keys != null)
                {
                    foreach (string key in list.keys)
                        firstTimeKeys.Add(key);
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[AnalyticsManager] first_time 키 로드 실패: {e.Message}");
            }
        }

        private void SaveFirstTimeKeys()
        {
            var list = new FirstTimeKeyList();
            list.keys.AddRange(firstTimeKeys);
            PlayerPrefs.SetString(FirstTimeKeysPrefKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }

        private string FormatParameters(Dictionary<string, object> parameters)
        {
            if (parameters.Count == 0) return "{}";

            var parts = new List<string>();
            foreach (var pair in parameters)
                parts.Add($"{pair.Key}: {pair.Value}");
            return "{ " + string.Join(", ", parts) + " }";
        }

        #endregion
    }
}
