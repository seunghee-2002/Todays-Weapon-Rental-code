using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 최초 실행 약관 동의 View.
    /// 필수 항목에 동의해야만 게임에 진입할 수 있으며, 거부하면 게임을 종료한다.
    /// </summary>
    public class TermsAgreementView : BaseView
    {
        [Header("동의 체크박스")]
        [SerializeField] private Toggle requiredToggle;
        [SerializeField] private Toggle optionalToggle;

        [Header("Buttons")]
        [SerializeField] private Button termsViewButton;
        [SerializeField] private Button privacyViewButton;
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI startButtonText;
        [SerializeField] private Button declineButton;

        // 필수 항목을 아직 체크하지 않았으면 버튼이 전체 동의 역할을 겸한다
        private const string StartWithAllAgreeKey = "Terms_StartWithAllAgree";
        private const string StartKey = "Terms_StartButton";

        private TermsAgreementController controller;

        /// <summary>필수 항목(서비스 이용약관 및 개인정보 수집이용) 동의 여부.</summary>
        public bool IsRequiredAgreed => requiredToggle != null && requiredToggle.isOn;

        /// <summary>선택 항목(분석 데이터 수집) 동의 여부.</summary>
        public bool IsOptionalAgreed => optionalToggle != null && optionalToggle.isOn;

        public void SetController(TermsAgreementController ctrl) => controller = ctrl;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;    // MainMenuScene에는 게임 시간이 흐르지 않는다
            isCanClickOverlay = false;  // 오버레이 클릭으로 닫지 않는다
            canEscape = false;          // 필수 동의 전에는 닫을 수 없다
        }

        public override void Open()
        {
            base.Open();

            // 선택 항목을 미리 체크해두면 유효한 동의로 인정되지 않으므로 항상 해제 상태로 시작한다
            requiredToggle?.SetIsOnWithoutNotify(false);
            optionalToggle?.SetIsOnWithoutNotify(false);

            UpdateStartButtonLabel();
        }

        protected override void SubscribeEvents()
        {
            // 버튼 라벨은 필수 항목에만 좌우되므로 선택 토글은 구독하지 않는다
            requiredToggle?.onValueChanged.AddListener(OnRequiredToggleChanged);
            termsViewButton?.onClick.AddListener(() => controller?.OnViewTermsClicked());
            privacyViewButton?.onClick.AddListener(() => controller?.OnViewPrivacyClicked());
            startButton?.onClick.AddListener(() => controller?.OnStartClicked());
            declineButton?.onClick.AddListener(() => controller?.OnDeclineClicked());
        }

        protected override void UnsubscribeEvents()
        {
            requiredToggle?.onValueChanged.RemoveAllListeners();
            termsViewButton?.onClick.RemoveAllListeners();
            privacyViewButton?.onClick.RemoveAllListeners();
            startButton?.onClick.RemoveAllListeners();
            declineButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region 이벤트 핸들러

        private void OnRequiredToggleChanged(bool _) => UpdateStartButtonLabel();

        #endregion

        #region 내부 메서드

        private void UpdateStartButtonLabel()
        {
            if (startButtonText != null)
                startButtonText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", IsRequiredAgreed ? StartKey : StartWithAllAgreeKey);
        }

        #endregion
    }
}
