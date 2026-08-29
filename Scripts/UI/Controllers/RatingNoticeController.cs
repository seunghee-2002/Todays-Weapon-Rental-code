using System;
using System.Collections;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 등급표시 컨트롤러.
    /// 게임산업법 시행령 별표3이 요구하는 최소 노출 시간(3초)을 채운 뒤 자동으로 닫고,
    /// 다음 흐름(약관 동의)을 콜백으로 이어준다.
    /// </summary>
    public class RatingNoticeController : BaseController<RatingNoticeView>
    {
        // 법정 최소 노출 시간. 인스펙터 값이 이보다 작아도 이 값이 강제된다.
        private const float LegalMinimumSeconds = 3f;

        [SerializeField] private float displaySeconds = 3.5f;

        private Action onClosed;

        #region 외부에서 호출되는 메서드

        /// <summary>
        /// 표시를 시작한다. 패널을 연 직후에 호출해야 한다.
        /// onClosed는 표시가 끝나고 패널이 닫힌 뒤 호출된다.
        /// </summary>
        public void BeginNotice(Action onClosed)
        {
            this.onClosed = onClosed;
            StartCoroutine(NoticeRoutine());
        }

        #endregion

        #region 내부 메서드

        private IEnumerator NoticeRoutine()
        {
            float duration = Mathf.Max(LegalMinimumSeconds, displaySeconds);
            Log.Info($"[RatingNoticeController] 등급표시 시작 ({duration}초)");

            // 페이드아웃은 이 대기가 끝난 뒤에 시작하므로 온전한 노출 시간이 보장된다
            yield return new WaitForSecondsRealtime(duration);

            if (view != null)
                view.PlayFadeOut(CloseNotice);
            else
                CloseNotice();
        }

        private void CloseNotice()
        {
            UIManager.Instance?.ClosePanel<RatingNoticeView>();

            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        #endregion
    }
}
