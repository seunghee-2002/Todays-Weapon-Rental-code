using UnityEngine;
using UnityEngine.Localization.Settings;
using System;
using System.Collections.Generic;

namespace TodaysWeaponRental

{
    /// <summary>
    /// 시간대 Phase enum
    /// </summary>
    public enum TimePhase
    {
        Morning,    // 아침 (6:00 ~ 9:00)
        Day,    // 낮 (9:00 ~ 18:00)
        Evening,    // 저녁 (18:00 ~ 21:00)
        Night       // 밤 (21:00 ~)
    }

    public class TimeManager : BaseManager<TimeManager>
    {
        [Header("Settings")]
        [SerializeField] private bool startPaused = false;

        // 시간 상태
        private int currentHour = 6;
        private int currentMinute = 0;
        private int currentDay = 1;
        private float elapsedSeconds = 0f;
        private bool isTimePaused = false;
        private bool isUserPaused = false;
        private float currentTimeScale = 1f;
        private bool canTimeChange = true;
        private bool isSkippingTime = false;

        // Phase 추적
        [SerializeField] private TimePhase currentPhase = TimePhase.Morning;

        private TimeConfig config => ConfigManager.Instance.Time;

        // 프로퍼티
        public int CurrentDay => currentDay;
        public int CurrentHour => currentHour;
        public int CurrentMinute => currentMinute;
        public int CurrentTime => (currentDay - 1) * 900 + (currentHour - 6) * 60 + currentMinute;
        public bool IsTimePaused => isTimePaused;
        public float CurrentTimeScale => currentTimeScale;
        public TimePhase CurrentPhase => currentPhase;
        public bool IsSkippingTime => isSkippingTime;

        // 이벤트
        public event Action<int, int> OnTimeChanged;
        public event Action<float> OnTimeSkipped;
        public event Action<int> OnDayChanged;
        public event Action<bool> OnTimePausedChanged;
        public event Action<TimePhase> OnPhaseChanged; // (새로운 Phase)
        public event Action<float> OnTimeScaleChanged; // 배속 변경 (새 배속)
        public event Action<float> OnTimeSkipStarted;  // 스킵 시작 (실제 진행할 총 게임 분)

        #region 시간 관리

        public void Initialize(GameData gameData)
        {
            // PlayerData에서 시간 로드
            if (gameData != null)
            {
                currentHour = gameData.currentHour;
                currentMinute = gameData.currentMinute;
                currentDay = gameData.currentDay;
                isUserPaused = gameData.isUserPaused;
                currentTimeScale = gameData.currentTimeScale;
            }
            else
            {
                currentHour = config.morningStartHour;
                currentMinute = 0;
                currentDay = 1;
                isUserPaused = false;
                currentTimeScale = config.timeScaleOptions[0]; // 1x
            }

            // dayEndHour(21시)를 초과한 잘못된 저장값 보정
            if (currentHour >= config.dayEndHour)
            {
                currentHour = config.dayEndHour;
                currentMinute = 0;
            }

            // isTimePaused는 isUserPaused와 21시 이후 여부로부터 파생 복원
            if (isUserPaused || currentHour >= config.dayEndHour)
                isTimePaused = true;
            else
                isTimePaused = startPaused;

            currentPhase = GetPhaseFromTime(currentHour);

            Log.Info($"TimeManager: Initialized at {GetCurrentTimeString()}");
        }

        public void SaveToGameData(GameData gameData)
        {
            if (gameData == null) return;
            gameData.isUserPaused = isUserPaused;
            gameData.currentTimeScale = currentTimeScale;
        }

        private void Update()
        {
            if (isTimePaused) return;

            // 실시간 경과 (배속 적용)
            elapsedSeconds += Time.deltaTime * currentTimeScale;

            // 1초 경과 시 게임 내 3분 증가.
            // 저사양+고배속에서 한 프레임에 1초 이상 누적되면 백로그가 계속 밀리므로 루프로 소진한다.
            // 폭주 방지를 위해 프레임당 최대 틱 수를 두고, 초과분은 버린다
            int ticks = 0;
            while (elapsedSeconds >= 1.0f && ticks < 8)
            {
                elapsedSeconds -= 1.0f;
                AdvanceTime(3);
                OnTimeSkipped?.Invoke(3f);
                ticks++;

                if (isTimePaused) break;   // AdvanceTime이 하루 종료로 정지시켰으면 중단
            }

            if (ticks >= 8)
                elapsedSeconds = Mathf.Min(elapsedSeconds, 1.0f);
        }

        private void AdvanceTime(int minutes)
        {
            currentMinute += minutes;

            // 60분 넘으면 시간 증가
            while (currentMinute >= 60)
            {
                currentMinute -= 60;
                currentHour++;
            }

            // PM 9:00(dayEndHour)을 넘기지 않도록 캡 — 임의 분 단위 진행(AdvanceGameTime) 시 초과 방지
            if (config != null && currentHour >= config.dayEndHour)
            {
                currentHour = config.dayEndHour;
                currentMinute = 0;
            }
            else if (currentHour >= 24)
            {
                currentHour = 0;
            }

            // Phase 변경 체크
            CheckAndUpdatePhase();

            // 시간 변경 이벤트 발생
            OnTimeChanged?.Invoke(currentHour, currentMinute);

            // PM 9:00 도달 시 자동 정지 및 다음날 진행 팝업
            if (config != null && currentHour >= config.dayEndHour && !isTimePaused)
            {
                PauseTime();
                Log.Info("TimeManager: Day ended. Time paused at PM 9:00.");
                ShowDayEndPopup();
            }

            // GameData 업데이트
            UpdateGameData();
        }

        public void PauseTime(bool fromUser = false)
        {
            if (isTimePaused) return;
            if (!canTimeChange) return;

            if (fromUser) isUserPaused = true;
            isTimePaused = true;
            OnTimePausedChanged?.Invoke(true);
        }

        public void ResumeTime()
        {
            if (!isTimePaused) return;
            if (!canTimeChange) return;
            if (currentHour >= config.dayEndHour) return;
            if (isUserPaused) return;

            isTimePaused = false;
            OnTimePausedChanged?.Invoke(false);
        }

        public void UserResumeTime()
        {
            isUserPaused = false;
            ResumeTime();
        }

        public void SetTimeScale(float scale)
        {
            if (!canTimeChange) return;
            // config.timeScaleOptions에 있는 값만 허용 TODO: 나중에 활성화
            // bool isValidScale = false;
            // foreach (float validScale in config.timeScaleOptions)
            // {
            //     if (Mathf.Approximately(scale, validScale))
            //     {
            //         isValidScale = true;
            //         break;
            //     }
            // }

            // if (!isValidScale)
            // {
            //     Log.Warn($"TimeManager: Invalid time scale {scale}. Using 1x.");
            //     scale = 1f;
            // }

            if (Mathf.Approximately(currentTimeScale, scale)) return;

            currentTimeScale = scale;
            OnTimeScaleChanged?.Invoke(scale);

            AnalyticsManager.Instance?.Send("speed_changed", new Dictionary<string, object>
            {
                { "speed_multiplier", scale }
            });
        }

        /// <summary>
        /// 전령 보고가 끝난 뒤 등 외부에서 다음날 진행 팝업을 다시 띄울 때 호출한다.
        /// 아직 21시가 아니거나 보고가 남아 있으면 아무것도 하지 않는다.
        /// </summary>
        public void PromptNextDayIfDayEnded()
        {
            if (!IsDayEnded()) return;
            if (HasPendingHeraldReport) return;

            ShowDayEndPopup();
        }

        /// <summary>
        /// 21시 팝업. 전령이 보고할 모험이 남아 있으면 다음날로 넘어갈 수 없다.
        /// </summary>
        private void ShowDayEndPopup()
        {
            if (HasPendingHeraldReport)
            {
                UIPopupController.Instance.ShowPopup(L("Time_HeraldReportRequired"), () => {},
                    type: PopupSfxType.Warning);
                return;
            }

            UIPopupController.Instance.ShowPopup(L("Time_NextDayConfirm"), () => GoToNextDay(), () => {});
        }

        /// <summary>전령이 아직 보고하지 않은 모험이 남아 있는지 (남아 있으면 다음날로 넘어갈 수 없다).</summary>
        private bool HasPendingHeraldReport =>
            AdventureManager.Instance != null && AdventureManager.Instance.HasPendingHeraldReport;

        public void GoToNextDay()
        {
            if (!IsDayEnded())
            {
                Log.Warn("TimeManager: Cannot go to next day. Current time is not PM 9:00.");
                return;
            }

            // TopBar의 다음날 버튼 등 팝업을 거치지 않는 경로도 여기서 막는다.
            if (HasPendingHeraldReport)
            {
                UIPopupController.Instance?.ShowToast(L("Time_HeraldReportRequired"), type: PopupSfxType.Warning);
                return;
            }

            if (currentDay == 1 && (TutorialManager.Instance?.ShouldBlockTimeResume() ?? false))
            {
                ResetForIncompleteTutorial();
                return;
            }

            currentDay++;
            currentHour = config != null ? config.morningStartHour : 6;
            currentMinute = 0;
            elapsedSeconds = 0f;

            TimePhase oldPhase = currentPhase;
            currentPhase = TimePhase.Morning;

            OnDayChanged?.Invoke(currentDay);
            if (oldPhase != currentPhase) OnPhaseChanged?.Invoke(currentPhase);
            OnTimeChanged?.Invoke(currentHour, currentMinute);
            UpdateGameData();

            AnalyticsManager.Instance?.SendDayBegin();

            // OnDayChanged에서 열린 패널(퀘스트 결과 등)이 시간 정지를 요구하면 재개하지 않는다.
            // 무조건 ResumeTime()을 부르면 그 패널이 방금 건 정지가 풀려 뒤에서 시간이 흐른다.
            // 재개는 패널이 닫힐 때 UIManager.CheckResumeTime이 맡는다.
            if (UIManager.Instance == null || !UIManager.Instance.HasTimePausingPanel())
                ResumeTime();

            GameManager.Instance.SaveGame();

            Log.Info($"{currentDay}일이 시작되었습니다.");
        }

        /// <summary>
        /// 아침 시간을 스킵하고 9시로 이동
        /// </summary>
        public void SkipMorning()
        {
            if (!IsMorning())
            {
                Log.Warn("TimeManager: 아침 시간이 아닙니다.");
                return;
            }

            int daytimeStartHour = config != null ? config.daytimeStartHour : 9;
            AdvanceGameTime((daytimeStartHour - currentHour) * 60 - currentMinute);

            Log.Info("TimeManager: 9:00으로 스킵 완료");
        }

        /// <summary>
        /// 저녁 시간을 스킵하고 하루 끝(21:00)으로 이동
        /// 정지 상태로 스킵하면 AdvanceTime의 21시 자동 팝업 조건(!isTimePaused)에 걸리지 않으므로 그때만 직접 띄운다.
        /// </summary>
        public void SkipEvening()
        {
            if (!IsEvening())
            {
                Log.Warn("TimeManager: 저녁 시간이 아닙니다.");
                return;
            }

            bool wasPaused = isTimePaused;
            AdvanceGameTime(GetRemainingMinutesUntilDayEnd());
            if (wasPaused) PromptNextDayIfDayEnded();

            Log.Info("TimeManager: 21:00으로 스킵 완료");
        }

        private void ResetForIncompleteTutorial()
        {
            SaveManager.DeleteGameData();
            _ = CloudSaveManager.Instance?.ClearCloudGameDataAsync();
            UIPopupController.Instance?.ShowPopup(
                L("Time_TutorialNotDone"),
                onConfirm: () => SceneController.Instance?.LoadMainMenu(),
                type: PopupSfxType.Notify);
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        #endregion

        #region 강제 시간 설정

        /// <summary>
        /// 플레이어가 시간을 조작할 수 없도록 강제 정지
        /// 튜토리얼 등 시스템에서만 호출
        /// </summary>
        public void ForcePause()
        {
            canTimeChange = false;
            if (!isTimePaused)
            {
                isTimePaused = true;
                OnTimePausedChanged?.Invoke(true);
            }
        }

        /// <summary>
        /// 9단계 수면 마법: 현재(낮) 시각을 저녁(18:00) 시작으로 즉시 건너뛴다. 튜토리얼 전용(정지 중에도 동작).
        /// </summary>
        public void SkipToEvening()
        {
            int eveningHour = config != null ? config.eveningStartHour : 18;
            int minutes = (eveningHour - currentHour) * 60 - currentMinute;
            if (minutes > 0) AdvanceGameTime(minutes);
        }

        /// <summary>
        /// 현재 시각을 지정 시(정각)로 즉시 건너뛴다. 튜토리얼 마무리(20:00 스킵)용. 정지 중에도 동작하고, 이미 지났으면 무시한다.
        /// </summary>
        public void SkipToHour(int targetHour)
        {
            int minutes = (targetHour - currentHour) * 60 - currentMinute;
            if (minutes > 0) AdvanceGameTime(minutes);
        }

        /// <summary>
        /// 강제 정지 해제 후 시간 재개
        /// </summary>
        public void ForceResume()
        {
            canTimeChange = true;
            if (isTimePaused && currentHour < config.dayEndHour)
            {
                isTimePaused = false;
                OnTimePausedChanged?.Invoke(false);
            }
        }

        #endregion

        #region Phase 관리

        /// <summary>
        /// 현재 시간에서 Phase 전환 체크 및 업데이트
        /// </summary>
        private void CheckAndUpdatePhase()
        {
            TimePhase newPhase = GetPhaseFromTime(currentHour);

            if (newPhase != currentPhase)
            {
                TimePhase oldPhase = currentPhase;

                if (currentDay == 1 && oldPhase == TimePhase.Morning && newPhase == TimePhase.Day
                    && (TutorialManager.Instance?.ShouldBlockTimeResume() ?? false))
                {
                    ResetForIncompleteTutorial();
                    return;
                }

                currentPhase = newPhase;

                OnPhaseChanged?.Invoke(newPhase);

                AnalyticsManager.Instance?.Send("phase_changed", new Dictionary<string, object>
                {
                    { "phase", GetPhaseAnalyticsName(newPhase) }
                });
            }
        }

        /// <summary>Analytics phase_changed 파라미터 표기 (Documents/Analytics_이벤트_설계.md)</summary>
        private static string GetPhaseAnalyticsName(TimePhase phase)
        {
            switch (phase)
            {
                case TimePhase.Morning: return "morning";
                case TimePhase.Day: return "daytime";
                case TimePhase.Evening: return "evening";
                default: return "night";
            }
        }

        /// <summary>
        /// 시간으로부터 Phase 계산
        /// </summary>
        private TimePhase GetPhaseFromTime(int hour)
        {
            if (config == null)
            {
                if (hour >= 6 && hour < 9) return TimePhase.Morning;
                if (hour >= 9 && hour < 18) return TimePhase.Day;
                if (hour >= 18 && hour < 21) return TimePhase.Evening;
                return TimePhase.Night;
            }
            
            if (hour >= config.morningStartHour && hour < config.daytimeStartHour)
                return TimePhase.Morning;
            if (hour >= config.daytimeStartHour && hour < config.eveningStartHour)
                return TimePhase.Day;
            if (hour >= config.eveningStartHour && hour < config.dayEndHour)
                return TimePhase.Evening;
            
            return TimePhase.Night;
        }

        #endregion
        
        #region 헬퍼 메서드

        /// <summary>
        /// 21:00까지 남은 게임 분 반환
        /// </summary>
        public int GetRemainingMinutesUntilDayEnd()
        {
            int dayEndHour = config != null ? config.dayEndHour : 21;
            int remaining = (dayEndHour - currentHour) * 60 - currentMinute;
            return Mathf.Max(0, remaining);
        }

        /// <summary>
        /// 게임 시간을 지정 분만큼 즉시 앞당김 (대화 등 이벤트 시간 소모용)
        /// 실행 전에 21시 캡을 반영한 총량·틱 개수를 먼저 계산한 뒤, 3분 단위 틱으로 나눠 진행한다.
        /// </summary>
        public void AdvanceGameTime(int gameMinutes)
        {
            if (gameMinutes <= 0) return;

            int cappedMinutes = Mathf.Min(gameMinutes, GetRemainingMinutesUntilDayEnd());
            int tickCount = Mathf.CeilToInt(cappedMinutes / 3f);

            // 스킵 크기를 미리 알려, 구독자가 스킵 시작 시점에 즉시 정리할 수 있게 한다(행인 등).
            OnTimeSkipStarted?.Invoke(cappedMinutes);

            // 스킵 루프 동안 스폰되는 방문자는 걷는 연출(실프레임 필요) 대신 즉시 배치되도록 플래그를 켠다.
            isSkippingTime = true;
            try
            {
                for (int i = 0; i < tickCount; i++)
                {
                    int step = Mathf.Min(3, cappedMinutes - i * 3);
                    AdvanceTime(step);
                    OnTimeSkipped?.Invoke(step);
                }
            }
            finally
            {
                isSkippingTime = false;
            }
        }

        // 시간대 판별
        public bool IsMorning()
        {
            return currentPhase == TimePhase.Morning;
        }

        public bool IsDaytime()
        {
            return currentPhase == TimePhase.Day;
        }

        public bool IsEvening()
        {
            return currentPhase == TimePhase.Evening;
        }

        public bool IsDayEnded()
        {
            return currentPhase == TimePhase.Night;
        }

        // 시간 문자열
        public string GetCurrentTimeString()
        {
            return $"{currentHour:00}:{currentMinute:00}";
        }

        #endregion

        private void UpdateGameData()
        {
            if (GameManager.Instance.GameData != null)
            {
                GameManager.Instance.GameData.currentHour = currentHour;
                GameManager.Instance.GameData.currentMinute = currentMinute;
                GameManager.Instance.GameData.currentDay = currentDay;
            }
        }

#if UNITY_EDITOR
        #region 디버그 메서드

        /// <summary>
        /// 디버그: 지정한 날짜의 하루 끝(21:00)으로 이동한다. 거기서 다음날로 넘기면
        /// 아침 흐름(퀘스트 결과창 -> 6:03 시세 공지)을 실제 경로 그대로 탈 수 있다.
        ///
        /// 주의: `currentWeek`는 날짜에서 파생되지 않고 퀘스트 정산 때만 오른다.
        /// 가격 계단(PriceTierConfig)은 주차 기준이므로 날짜만 옮기면 배율이 어긋난다.
        /// 그래서 주차를 함께 맞추고, 주간 퀘스트도 그 주 시작일 기준으로 다시 발급한다
        /// (마감일이 target day가 되어 다음날 아침에 결과창이 뜬다).
        /// 건너뛴 날들의 OnDayChanged는 발행하지 않는다.
        /// </summary>
        public void DebugJumpToDayEnd(int day)
        {
            day = Mathf.Max(1, day);
            int week         = (day - 1) / 7 + 1;
            int weekStartDay = (week - 1) * 7 + 1;

            var gameData = GameManager.Instance?.GameData;
            if (gameData != null)
            {
                gameData.currentWeek = week;
                currentDay = weekStartDay;      // IssueNewQuest가 startDay로 읽는 값
                gameData.currentDay = weekStartDay;
                QuestManager.Instance?.IssueNewQuest();
            }

            currentDay     = day;
            currentHour    = config != null ? config.dayEndHour : 21;
            currentMinute  = 0;
            elapsedSeconds = 0f;

            TimePhase oldPhase = currentPhase;
            currentPhase = TimePhase.Night;

            if (oldPhase != currentPhase) OnPhaseChanged?.Invoke(currentPhase);
            OnTimeChanged?.Invoke(currentHour, currentMinute);
            UpdateGameData();
            PauseTime();

            Log.Info($"[TimeManager] 디버그: Day {day} 21:00 이동 (주차 {week}, 퀘스트 마감일 {day})");
        }

        public void DebugSkipToNextDayAt9()
        {
            currentDay++;
            currentHour = 9;
            currentMinute = 3;
            elapsedSeconds = 0f;
            isTimePaused = false;
            isUserPaused = false;

            TimePhase oldPhase = currentPhase;
            currentPhase = TimePhase.Day;

            OnDayChanged?.Invoke(currentDay);
            if (oldPhase != currentPhase)
                OnPhaseChanged?.Invoke(currentPhase);
            OnTimeChanged?.Invoke(currentHour, currentMinute);
            UpdateGameData();

            QuestBoardManager.Instance.DebugForceConfirm3RandomDungeons();
            if (UIManager.Instance.GetPanel<QuestBoardView>()?.IsOpen ?? false)
                UIManager.Instance.ClosePanel<QuestBoardView>();

            Log.Info($"[TimeManager] 디버그: Day {currentDay} 9:00으로 이동, 던전 3개 확정");
        }

        public void SkipThreeHours()
        {
            if (IsDayEnded())
            {
                Log.Warn("TimeManager: Cannot skip time. Day already ended.");
                return;
            }

            AdvanceGameTime(180);

            Log.Info($"TimeManager: Skipped to {GetCurrentTimeString()}");
        }

        #endregion
#endif
    }
}
