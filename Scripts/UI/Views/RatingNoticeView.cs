using System;
using UnityEngine;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 게임산업법 제33조 등급표시 View.
    /// 상호, 이용등급, 게임물내용정보, 자체등급분류 표시를 담으며, 표시 문구와 등급 이미지는
    /// 모두 프리팹 인스펙터에서 직접 설정한다(등급 확정 시 코드 수정 없이 교체하기 위함).
    /// </summary>
    public class RatingNoticeView : BaseView
    {
        [Header("페이드")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeOutDuration = 0.4f;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;  // MainMenuScene에는 게임 시간이 흐르지 않는다
            canEscape = false;        // 뒤로가기로 법정 표시 시간을 건너뛸 수 없다
            hideUIOnOpen = false;     // 상시 UI를 껐다 켜면 메인 메뉴 타이틀 애니메이션이 끊긴다
        }

        public override void Open()
        {
            base.Open();

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        protected override void SubscribeEvents() { }
        protected override void UnsubscribeEvents() { }

        #endregion

        #region Controller로부터 호출되는 메서드

        /// <summary>표시 시간이 끝난 뒤 페이드아웃하고 완료를 알린다.</summary>
        public void PlayFadeOut(Action onComplete)
        {
            if (canvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            canvasGroup.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke())
                .SetLink(gameObject);
        }

        #endregion
    }
}
