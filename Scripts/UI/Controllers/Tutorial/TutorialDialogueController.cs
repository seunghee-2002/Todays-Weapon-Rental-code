using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 튜토리얼 전령 대화창 Controller.
    /// 주어진 대사 목록을 터치마다 한 줄씩 진행하고, 마지막 줄 이후 onComplete를 실행한다.
    /// </summary>
    public class TutorialDialogueController : BaseController<TutorialDialogueView>
    {
        private IList<string> lines;
        private int index;
        private Action onComplete;
        // 이 대사 묶음의 마지막 줄 뒤에 플레이어 조작이 이어지는가(EndIndicator 표시 여부).
        private bool actionFollows;
        // 직전 대사가 끝나 onComplete를 실행하는 중인가. 이 구간에서 열리는 대화창은 조작 없이 이어지는 대사이므로,
        // TutorialManager가 한 프레임 대기 없이 즉시 띄워 대화창이 꺼졌다 켜지는 깜빡임을 없앤다.
        private bool isCompletingSequence;
        public bool IsCompletingSequence => isCompletingSequence;

        #region View로부터 호출되는 메서드

        /// <summary>대사 시퀀스를 시작한다. 패널이 열린 상태에서 호출한다. actionFollows면 마지막 줄에 EndIndicator를 켠다.</summary>
        public void ShowLines(IList<string> lines, Action onComplete, bool actionFollows)
        {
            this.lines = lines;
            this.onComplete = onComplete;
            this.actionFollows = actionFollows;
            index = 0;
            ShowCurrent();
        }

        /// <summary>터치(다음) 시 View가 호출.</summary>
        public void OnAdvance()
        {
            index++;
            if (lines != null && index < lines.Count)
                ShowCurrent();
            else
                Finish();
        }

        /// <summary>완주 계정용 스킵 버튼 클릭 시 View가 호출. 튜토리얼 전체를 종료한다.</summary>
        public void OnSkip()
        {
            TutorialManager.Instance?.SkipRemainingTutorial();
        }

        #endregion

        #region 내부 메서드

        private void ShowCurrent()
        {
            if (lines == null || lines.Count == 0)
            {
                Finish();
                return;
            }

            bool isLast = index >= lines.Count - 1;
            view?.ShowLine(lines[index], isLast, actionFollows);
        }

        private void Finish()
        {
            // 콜백이 새 대화를 시작할 수 있으므로 필드를 먼저 비우고 호출한다.
            var complete = onComplete;
            onComplete = null;
            lines = null;
            index = 0;

            // 닫기는 콜백보다 먼저 한다 - 콜백의 하이라이트가 상시 UI(하단바)를 대상으로 할 때
            // 대화창이 빠진 상태로 상시 UI가 확정돼야 대상 좌표가 맞는다.
            UIManager.Instance?.ClosePanel<TutorialDialogueView>();

            isCompletingSequence = true;
            complete?.Invoke();
            isCompletingSequence = false;
        }

        #endregion
    }
}
