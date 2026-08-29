using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 상단바 UI 뷰
    /// - 시간, 날짜, 골드, 평판 표시
    /// - 배속/정지 순환 버튼, 스킵 버튼 제어
    /// </summary>
    public class TopBarView : MonoBehaviour
    {
        [Header("Info Texts")]
        [SerializeField] private TextMeshProUGUI nicknameText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI legacyText;
        
        [Header("Reputation")]
        [SerializeField] private TextMeshProUGUI reputationText;
        [SerializeField] private Button reputationButton;
        [SerializeField] private Image reputationLevelIcon;
        [SerializeField] private Slider reputationSlider;
        [SerializeField] private ReputationDetailPopup reputationDetailPopup;

        private OverlayController currentReputationOverlay;

        [Header("Time Control Button")]
        [SerializeField] private Button timeButton;
        [SerializeField] private TextMeshProUGUI timeScaleText; // "x1", "x2", "x4" 표시
        [SerializeField] private GameObject pauseImage; // 정지 상태 아이콘
        [SerializeField] private GameObject nextDayImage; // 다음 날 아이콘
        [SerializeField] private GameObject skipMorningImage; // 시간 스킵 아이콘 (아침/저녁 공용)

        [Header("Tutorial Highlight Groups")]
        // 11단계(나머지 UI) 하이라이트용 그룹 컨테이너. 각 영역을 감싸는 부모 RectTransform을 인스펙터에서 배선한다.
        [SerializeField] private RectTransform tutorialDayGroup;         // 날짜(DayInfo)
        [SerializeField] private RectTransform tutorialTimeGroup;        // 시간(TimeInfo)
        [SerializeField] private RectTransform tutorialGoldGroup;        // 보유 골드(Gold)
        [SerializeField] private RectTransform tutorialReputationGroup;  // 평판 게이지(아이콘+슬라이더+수치)
        [SerializeField] private RectTransform tutorialLegacyGroup;      // 유산 포인트(아이콘+수치)

        // 현재 버튼 모드
        private enum TimeButtonMode
        {
            TimeScale,  // x1/x2/x4/정지 순환
            SkipMorning, // 아침 스킵
            SkipEvening, // 저녁 스킵
            NextDay     // 다음 날 버튼
        }
        private TimeButtonMode currentMode = TimeButtonMode.TimeScale;

        private void Start()
        {
            // 초기 UI 업데이트
            UpdateAllUI();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged += OnTimeChanged;
                TimeManager.Instance.OnPhaseChanged += OnPhaseChanged;
                TimeManager.Instance.OnTimePausedChanged += OnTimePausedChanged;
                TimeManager.Instance.OnDayChanged += OnDayChanged;
            }

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnGoldChanged += OnGoldChanged;
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.OnReputationChanged += OnReputationChanged;
            }

            if (LegacyManager.Instance != null)
            {
                LegacyManager.Instance.OnLegacyPointsChanged += OnLegacyPointsUpdated;
            }

            if (NicknameManager.Instance != null)
            {
                NicknameManager.Instance.OnNicknameChanged += OnNicknameChanged;
            }

            timeButton?.onClick.AddListener(OnTimeButtonClicked);
            reputationButton?.onClick.AddListener(OnReputationButtonClicked);
        }

        private void UnsubscribeEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged -= OnTimeChanged;
                TimeManager.Instance.OnPhaseChanged -= OnPhaseChanged;
                TimeManager.Instance.OnTimePausedChanged -= OnTimePausedChanged;
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
            }

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnGoldChanged -= OnGoldChanged;
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.OnReputationChanged -= OnReputationChanged;
            }

            if (LegacyManager.Instance != null)
            {
                LegacyManager.Instance.OnLegacyPointsChanged -= OnLegacyPointsUpdated;
            }

            if (NicknameManager.Instance != null)
            {
                NicknameManager.Instance.OnNicknameChanged -= OnNicknameChanged;
            }

            timeButton?.onClick.RemoveAllListeners();
            reputationButton?.onClick.RemoveAllListeners();
        }

        #region Event Handlers

        private void OnDayChanged(int newDay)
        {
            UpdateDayDisplay();
        }

        private void OnTimeChanged(int hour, int minute)
        {
            UpdateTimeDisplay();
            UpdateTimeButtonMode();
        }

        private void OnPhaseChanged(TimePhase newPhase)
        {
            UpdateDayDisplay();
            UpdateTimeButtonMode();

            // Phase 전환 알림
            string phaseKey = newPhase switch
            {
                TimePhase.Morning => "Time_PhaseMorning",
                TimePhase.Day     => "Time_PhaseDay",
                TimePhase.Evening => "Time_PhaseEvening",
                TimePhase.Night   => "Time_PhaseNight",
                _ => null
            };
            string phaseMessage = phaseKey != null
                ? LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", phaseKey)
                : "";
            
            if (!string.IsNullOrEmpty(phaseMessage))
            {
                UIPopupController.Instance.ShowToast(phaseMessage);
            }
        }

        private void OnTimePausedChanged(bool isPaused)
        {
            UpdateTimeButton();
        }

        private void OnGoldChanged(int newGold)
        {
            UpdateGoldDisplay();
        }

        private void OnReputationChanged(int newReputation)
        {
            UpdateReputationDisplay();
        }

        private void OnLegacyPointsUpdated(int newLegacyPoints)
        {
            UpdateLegacyDisplay();
        }

        private void OnNicknameChanged(string newNickname)
        {
            UpdateNicknameDisplay();
        }

        #endregion

        #region UI Update Methods

        private void UpdateAllUI()
        {
            UpdateNicknameDisplay();
            UpdateTimeDisplay();
            UpdateDayDisplay();
            UpdateGoldDisplay();
            UpdateLegacyDisplay();
            UpdateReputationDisplay();
            UpdateTimeButton();
            UpdateTimeButtonMode();
        }

        private void UpdateNicknameDisplay()
        {
            if (nicknameText == null || NicknameManager.Instance == null) return;
            nicknameText.text = NicknameManager.Instance.GetDisplayName();
        }

        private void UpdateTimeDisplay()
        {
            if (timeText == null || TimeManager.Instance == null) return;

            string timeString = TimeManager.Instance.GetCurrentTimeString();
            timeText.text = timeString;
            timeText.color = TimeManager.Instance.IsEvening() ? ColorManager.Instance.GetOrangeColor() : ColorManager.Instance.GetWhiteColor(); // 저녁이면 주황색, 아니면 흰색
        }

        private void UpdateDayDisplay()
        {
            if (dayText == null || TimeManager.Instance == null) return;

            dayText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", "TopBar_Day",
                arguments: new object[] { new Dictionary<string, object> { { "day", TimeManager.Instance.CurrentDay } } });
        }

        private void UpdateGoldDisplay()
        {
            if (goldText == null || EconomyManager.Instance == null) return;
            goldText.text = $"{EconomyManager.Instance.CurrentGold:N0}";
        }

        private void UpdateLegacyDisplay()
        {
            if (legacyText == null || LegacyManager.Instance == null) return;
            legacyText.text = $"{LegacyManager.Instance.LegacyPoints:N0}";
        }

        private void UpdateReputationDisplay()
        {
            if (reputationText == null || ReputationManager.Instance == null) return;

            int reputation = ReputationManager.Instance.CurrentReputation;
            bool isMaxLevel = ReputationManager.Instance.CurrentLevel == ReputationLevel.Diamond;

            if (isMaxLevel)
            {
                reputationText.text = $"{reputation:N0}/MAX";
                if (reputationSlider != null)
                {
                    reputationSlider.maxValue = 1f;
                    reputationSlider.value = 1f;
                }
            }
            else
            {
                int nextThreshold = ReputationManager.Instance.GetNextLevelThreshold();
                reputationText.text = $"{reputation:N0}/{nextThreshold:N0}";
                if (reputationSlider != null)
                {
                    reputationSlider.maxValue = nextThreshold;
                    reputationSlider.value = reputation;
                }
            }

            if (reputationLevelIcon != null && IconManager.Instance != null)
                reputationLevelIcon.sprite = IconManager.Instance.GetIconByReputationLevel(ReputationManager.Instance.CurrentLevel);
        }

        /// <summary>튜토리얼 하이라이트용 — 시간/아침스킵 버튼 RectTransform.</summary>
        public RectTransform GetTimeButtonRect() => timeButton?.transform as RectTransform;

        /// <summary>11단계 하이라이트용 — 날짜(DayInfo) RectTransform.</summary>
        public RectTransform GetDayRect() => tutorialDayGroup;

        /// <summary>11단계 하이라이트용 — 시간(TimeInfo) RectTransform.</summary>
        public RectTransform GetTimeRect() => tutorialTimeGroup;

        /// <summary>11단계 하이라이트용 — 보유 골드(Gold) RectTransform.</summary>
        public RectTransform GetGoldRect() => tutorialGoldGroup;

        /// <summary>11단계 하이라이트용 — 평판 게이지 묶음 RectTransform.</summary>
        public RectTransform GetReputationGroupRect() => tutorialReputationGroup;

        /// <summary>11단계 하이라이트용 — 유산 포인트 묶음 RectTransform.</summary>
        public RectTransform GetLegacyGroupRect() => tutorialLegacyGroup;

        /// <summary>
        /// 버튼 모드를 즉시 갱신한다. 평소엔 OnTimeChanged가 갱신하지만,
        /// 튜토리얼처럼 시간이 강제 정지된 동안엔 이벤트가 오지 않으므로 외부에서 호출한다.
        /// </summary>
        public void RefreshTimeButtonMode() => UpdateTimeButtonMode();

        private void UpdateTimeButtonMode()
        {
            if (TimeManager.Instance == null) return;

            bool isDayEnded = TimeManager.Instance.IsDayEnded();
            bool canSkipMorning = VisitorManager.Instance.CanSkipMorning;

            if (isDayEnded)
            {
                // PM 9:00 이후 -> 다음 날 버튼으로 전환
                currentMode = TimeButtonMode.NextDay;
            }
            else if (canSkipMorning && TimeManager.Instance.IsMorning())
            {
                currentMode = TimeButtonMode.SkipMorning;
            }
            else if (VisitorManager.Instance.CanSkipEvening)
            {
                currentMode = TimeButtonMode.SkipEvening;
            }
            else
            {
                // 낮 시간 -> 배속 버튼
                currentMode = TimeButtonMode.TimeScale;
            }

            UpdateTimeButton();
        }

        private void UpdateTimeButton()
        {
            if (!timeButton) return;

            // 모든 아이콘 초기화
            timeScaleText?.gameObject.SetActive(currentMode == TimeButtonMode.TimeScale && !TimeManager.Instance.IsTimePaused);
            pauseImage?.SetActive(currentMode == TimeButtonMode.TimeScale && TimeManager.Instance.IsTimePaused);
            skipMorningImage?.SetActive(currentMode == TimeButtonMode.SkipMorning || currentMode == TimeButtonMode.SkipEvening);
            nextDayImage?.SetActive(currentMode == TimeButtonMode.NextDay);

            // 배속 텍스트 갱신
            if (timeScaleText && timeScaleText.gameObject.activeSelf)
            {
                timeScaleText.text = $"x{TimeManager.Instance.CurrentTimeScale:F0}";
            }
        }

        #endregion

        #region Button Handlers

        private void OnTimeButtonClicked()
        {
            var time = TimeManager.Instance;
            if (!time) return;

            switch (currentMode)
            {
                case TimeButtonMode.NextDay: time.GoToNextDay(); break;
                case TimeButtonMode.SkipMorning:
                    time.SkipMorning();
                    // 튜토리얼 3단계: 아침 스킵으로 낮 진입 → 4단계로
                    if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                        TutorialManager.Instance.OnTutorialMorningSkipped();
                    break;
                case TimeButtonMode.SkipEvening: time.SkipEvening(); break;
                case TimeButtonMode.TimeScale: CycleTimeScale(); break;
            }
        }

        private void CycleTimeScale()
        {
            if (TimeManager.Instance == null) return;

            bool isPaused = TimeManager.Instance.IsTimePaused;
            float currentScale = TimeManager.Instance.CurrentTimeScale;

            if (isPaused)
            {
                // 정지 -> x1
                TimeManager.Instance.UserResumeTime();
                TimeManager.Instance.SetTimeScale(1f);
                Log.Info("Time resumed at x1");
            }
            else if (Mathf.Approximately(currentScale, 1f))
            {
                // x1 -> x2
                TimeManager.Instance.SetTimeScale(2f);
                Log.Info("TimeScale changed to x2");
            }
            else if (Mathf.Approximately(currentScale, 2f))
            {
                // x2 -> x4
                TimeManager.Instance.SetTimeScale(4f);
                Log.Info("TimeScale changed to x4");
            }
            else if (Mathf.Approximately(currentScale, 4f))
            {
                // x4 -> 정지
                TimeManager.Instance.PauseTime(fromUser: true);
                Log.Info("Time paused");
            }
            else
            {
                // 예외 상황 -> x1로 리셋
                TimeManager.Instance.SetTimeScale(1f);
                Log.Info("TimeScale reset to x1");
            }

            UpdateTimeButton();
        }

        private void OnReputationButtonClicked()
        {
            if (reputationDetailPopup == null) return;
            if (currentReputationOverlay != null) return; // 이미 열려 있음

            currentReputationOverlay = UIPopupController.Instance?.ShowOverlay(CloseReputationDetail);
            reputationDetailPopup.Show();
        }

        private void CloseReputationDetail()
        {
            reputationDetailPopup?.Hide();
            if (currentReputationOverlay != null)
            {
                Destroy(currentReputationOverlay.gameObject);
                currentReputationOverlay = null;
            }
        }

        #endregion
    }
}