using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 튜토리얼 전용 대화창 View. 길드 전령의 대사를 초상 + 텍스트로 보여준다.
    /// 기존 SpineDialogueView와 달리 줌·VisitorNPC 의존이 없고, 터치로 한 줄씩 진행한다.
    /// 실제 진행/시퀀스는 TutorialDialogueController가 담당한다.
    /// </summary>
    public class TutorialDialogueView : BaseView
    {
        [Header("Portrait")]
        [SerializeField] private Image portraitImage;

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Header("Buttons")]
        [SerializeField] private Button advanceButton;   // 전체 화면 터치 진행 버튼
        [SerializeField] private Button skipButton;       // 완주 계정 전용 스킵 버튼 (기본 비활성)

        [Header("Indicators")]
        [SerializeField] private GameObject canNextIndicator;
        [SerializeField] private GameObject endIndicator;

        [Header("Controller")]
        [SerializeField] private TutorialDialogueController controller;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            // 튜토리얼은 이미 강제 정지 상태이므로 패널이 시간을 건드리지 않는다.
            pauseTimeOnOpen = false;
            // 게임 화면(상점 NPC 등)이 뒤에 보여야 하므로 UI를 숨기지 않는다.
            hideUIOnOpen = false;
            isCanClickOverlay = false;
            canEscape = false;
        }

        #endregion

        #region Controller로부터 호출되는 메서드

        /// <summary>
        /// 한 줄을 표시한다. 종료 인디케이터(EndIndicator)는 그 대사 묶음의 마지막 줄이면서
        /// 뒤에 플레이어 조작이 이어질 때(actionFollows)만 보여주고, 그 외에는 NextIndicator를 켠다.
        /// (묶음 중간 줄, 또는 자동으로 다음 대사/단계로 이어지는 마지막 줄은 NextIndicator.)
        /// </summary>
        public void ShowLine(string text, bool isLast, bool actionFollows)
        {
            if (dialogueText != null) dialogueText.text = text;

            bool showEnd = isLast && actionFollows;
            canNextIndicator?.SetActive(!showEnd);
            endIndicator?.SetActive(showEnd);
        }

        /// <summary>완주 계정일 때만 상시 스킵 버튼을 노출한다. TutorialManager가 대화창을 열 때 호출.</summary>
        public void SetSkipButtonVisible(bool visible)
        {
            skipButton?.gameObject.SetActive(visible);
        }

        /// <summary>초상 표시 여부. 뒤에 있는 화면 요소를 가릴 때 등 상황에 따라 TutorialManager가 호출.</summary>
        public void SetPortraitVisible(bool visible)
        {
            portraitImage?.gameObject.SetActive(visible);
        }

        /// <summary>전령 초상 스프라이트를 교체한다(대사 묶음별 상황 초상). null이면 기존 스프라이트를 유지한다.</summary>
        public void SetPortrait(Sprite sprite)
        {
            if (sprite != null && portraitImage != null) portraitImage.sprite = sprite;
        }

        #endregion

        #region 이벤트 핸들러

        protected override void SubscribeEvents()
        {
            advanceButton?.onClick.AddListener(OnAdvanceClicked);
            skipButton?.onClick.AddListener(OnSkipClicked);
        }

        protected override void UnsubscribeEvents()
        {
            advanceButton?.onClick.RemoveListener(OnAdvanceClicked);
            skipButton?.onClick.RemoveListener(OnSkipClicked);
        }

        private void OnAdvanceClicked() => controller?.OnAdvance();
        private void OnSkipClicked() => controller?.OnSkip();

        #endregion
    }
}
