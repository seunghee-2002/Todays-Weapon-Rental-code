using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 2차 대화 실행 시 보여주는 애니메이션 + 결과 팝업.
    /// 애니메이션 페이즈: 게이지를 채우며 게임 시간을 점진적으로 진행.
    /// 결과 페이즈: 성공/실패 결과를 표시하고 확인 버튼으로 닫힘.
    /// BlacksmithProcessView와 동일한 구조.
    /// </summary>
    public class AdventureTalkAnimationPopup : BaseView
    {
        [Header("애니메이션 페이즈")]
        [SerializeField] private GameObject animationPanel;
        [SerializeField] private Image actionImage;
        [SerializeField] private TextMeshProUGUI actionTitleText;
        [SerializeField] private TextMeshProUGUI actionDescText;
        [SerializeField] private Slider gaugeSlider;

        [Header("결과 페이즈")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultMessageText;
        [SerializeField] private Button confirmButton;

        [SerializeField] private Button skipButton;

        private Action<bool, string> onComplete;
        private int timeCostMinutes;
        private bool pendingSuccess;
        private string pendingMessage;
        private bool isProcessing = false;
        private bool skipRequested = false;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;
            canEscape = false;
        }

        protected override void SubscribeEvents()
        {
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        protected override void UnsubscribeEvents()
        {
            confirmButton?.onClick.RemoveAllListeners();
        }

        // 코루틴 도중 예외/ESC/씬 전환으로 팝업이 닫혀도 SFX 억제가 남지 않도록 하는 안전망
        protected override void OnDisable()
        {
            base.OnDisable();
            SoundManager.Instance?.SuppressSFX(false);
        }

        /// <summary>
        /// 대화 애니메이션 팝업 초기화 및 실행.
        /// </summary>
        /// <param name="timeCost">소모될 게임 내 시간 (분)</param>
        /// <param name="actionIcon">액션 아이콘 (IconManager에서 호출 측이 조회해 전달)</param>
        /// <param name="actionTitle">애니메이션 페이즈 제목 텍스트</param>
        /// <param name="actionDesc">애니메이션 페이즈 설명 텍스트</param>
        /// <param name="executeFunc">시간 소모 후 실행할 실제 로직 (성공 여부, 결과 메시지 반환)</param>
        /// <param name="onCompleteCallback">확인 버튼 클릭 후 호출되는 콜백 (성공 여부, 결과 메시지)</param>
        public void Initialize(int timeCost, Sprite actionIcon, string actionTitle, string actionDesc,
            Func<(bool, string)> executeFunc, Action<bool, string> onCompleteCallback)
        {
            timeCostMinutes = timeCost;
            onComplete = onCompleteCallback;
            isProcessing = false;
            skipRequested = false;
            canEscape = false;   // 애니메이션 페이즈에서는 ESC 무시. 재사용(2회차 대화) 대비 초기화.

            animationPanel?.SetActive(true);
            resultPanel?.SetActive(false);

            if (actionImage != null)
                actionImage.sprite = actionIcon;
            if (actionTitleText != null)
                actionTitleText.text = actionTitle;
            if (actionDescText != null)
                actionDescText.text = actionDesc;
            if (gaugeSlider != null)
            {
                gaugeSlider.minValue = 0f;
                gaugeSlider.maxValue = 1f;
                gaugeSlider.value = 0f;
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() => skipRequested = true);
            }

            StartCoroutine(ProcessCoroutine(executeFunc));
        }

        #endregion

        #region 프로세스 실행

        private IEnumerator ProcessCoroutine(Func<(bool, string)> executeFunc)
        {
            if (isProcessing) yield break;
            isProcessing = true;

            // 연출음은 억제 구간에 진입하기 전에 재생한다 - 억제는 새 PlaySFX만 막고
            // 이미 재생 중인 소스는 건드리지 않으므로, 게이지가 도는 동안 계속 들린다.
            SoundManager.Instance?.PlaySFX("Upgrading");

            SoundManager.Instance?.SuppressSFX(true);

            float animDuration = ConfigManager.Instance.Insight.talkAnimDuration;
            int minutesAdvanced = 0;
            float elapsed = 0f;

            // 게이지 채우면서 게임 시간 점진적으로 진행
            while (elapsed < animDuration)
            {
                if (skipRequested) break;

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / animDuration);

                if (gaugeSlider != null)
                    gaugeSlider.value = progress;
 
                int targetMinutes = Mathf.FloorToInt(progress * timeCostMinutes / 3) * 3;
                if (targetMinutes > minutesAdvanced)
                {
                    TimeManager.Instance.AdvanceGameTime(targetMinutes - minutesAdvanced);
                    minutesAdvanced = targetMinutes;
                }

                yield return null;
            }

            if (gaugeSlider != null)
                gaugeSlider.value = 1f;

            // 남은 분 마저 처리
            if (minutesAdvanced < timeCostMinutes)
                TimeManager.Instance.AdvanceGameTime(timeCostMinutes - minutesAdvanced);

            if (skipRequested)
                SoundManager.Instance?.StopAllSFX();   // 망치질(Upgrading)이 연출보다 길게 남지 않도록
            else
                yield return new WaitForSeconds(0.3f);

            SoundManager.Instance?.SuppressSFX(false);

            // 로직 실행 - 예외가 나도 팝업/억제 상태가 복구되도록 감싼다
            bool success = false;
            string message = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Talk_Error");
            try
            {
                (success, message) = executeFunc.Invoke();
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }
            pendingSuccess = success;
            pendingMessage = message;

            if (skipButton != null)
                skipButton.onClick.RemoveAllListeners();

            // 결과 페이즈로 전환
            animationPanel?.SetActive(false);
            resultPanel?.SetActive(true);
            canEscape = true;   // 결과 페이즈에서는 ESC로도 확인(닫기) 가능

            if (resultTitleText != null)
                resultTitleText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", success ? "Talk_ResultSuccess" : "Talk_ResultFail");
            if (resultMessageText != null)
                resultMessageText.text = message;

            isProcessing = false;
        }

        #endregion

        #region 이벤트 핸들러

        private void OnConfirmClicked()
        {
            UIManager.Instance.ClosePanel<AdventureTalkAnimationPopup>();
            onComplete?.Invoke(pendingSuccess, pendingMessage);
        }

        public override void OnEscapeClicked()
        {
            // 애니메이션 진행 중에는 ESC 무시, 결과 페이즈에서는 확인 버튼과 동일하게 처리
            if (isProcessing) return;
            OnConfirmClicked();
        }

        #endregion
    }
}
