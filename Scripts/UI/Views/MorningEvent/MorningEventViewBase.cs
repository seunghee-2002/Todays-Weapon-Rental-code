// Scripts/UI/Views/MorningEvent/MorningEventViewBase.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모닝 이벤트 패널의 공통 기반 View.
    /// 오프닝 대화 → 패널 흐름 + ESC/닫기 확인 처리를 제공한다.
    /// 패널(목록/상세/결과)과 닫기 버튼은 각 이벤트가 직접 소유·토글한다.
    /// </summary>
    public abstract class MorningEventViewBase : BaseView
    {
        /// <summary>취소 확인 메시지에 사용할 이벤트 타입</summary>
        protected static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        protected static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        protected abstract MorningEventType EventType { get; }

        // 이벤트 종류별 테마 BGM (clipName == MorningEventType 이름)
        protected override string GetThemeBgmKey() => EventType.ToString();

        /// <summary>DataManager에서 조회할 오프닝 대화 ID</summary>
        protected abstract string OpeningDialogueID { get; }

        /// <summary>
        /// 이벤트 진행에 필요한 전제 자원(무기 등)을 보유했는지. 기본 true.
        /// false면 인트로 대화 후 패널 대신 EmptyDialogueID 안내 대화만 재생하고 종료한다.
        /// </summary>
        protected virtual bool HasRequiredResource => true;

        /// <summary>전제 자원이 없을 때 재생할 안내 대화 ID. HasRequiredResource를 오버라이드한 이벤트만 지정한다.</summary>
        protected virtual string EmptyDialogueID => null;

        /// <summary>닫기 동작. Controller가 바인딩한다.</summary>
        private Action closeHandler;

        /// <summary>"패널 닫기 + 완료 통지(NPC 퇴장은 보류)" 동작. Controller가 바인딩. 결과 대화 종료 후 DismissEventNpc로 퇴장시킨다.</summary>
        private Action closeDeferHandler;

        /// <summary>이벤트를 발생시킨 월드 NPC. 대화 중 spine 강조 대상으로 사용한다. Controller가 OpenEvent에서 바인딩.</summary>
        protected VisitorNPC eventNpc;

        /// <summary>canEscape=false일 때 ESC 입력 시 실행할 액션</summary>
        protected Action escapeCancelledAction;

        /// <summary>메인/단일 화면에서 ESC·오버레이 클릭 시 실행할 기본 동작. 기본은 취소 확인 팝업(RequestClose).
        /// 거절/패스가 기본인 이벤트는 OnOpened에서 재설정한다.</summary>
        protected Action mainEscapeAction;

        /// <summary>결과 화면 표시 중 — 닫기 시 취소 확인을 건너뛴다.</summary>
        protected bool resultShowing;

        #region 초기화

        protected override void SubscribeEvents() { }
        protected override void UnsubscribeEvents() { }

        /// <summary>Controller가 닫기 동작(CloseEvent)을 바인딩한다.</summary>
        public void SetCloseHandler(Action handler) => closeHandler = handler;

        /// <summary>Controller가 이벤트 NPC를 바인딩한다(대화 중 spine 강조용).</summary>
        public void SetEventNpc(VisitorNPC npc) => eventNpc = npc;

        /// <summary>Controller가 "패널 닫기(+완료 통지, NPC 퇴장 보류)" 동작을 바인딩한다.</summary>
        public void SetCloseDeferHandler(Action handler) => closeDeferHandler = handler;

        /// <summary>보류했던 NPC 퇴장 애니메이션을 발동한다. 결과/마무리 대화 종료 시 호출된다.</summary>
        protected void DismissEventNpc() => eventNpc?.EndInteraction();

        protected virtual void OnDestroy() { }

        #endregion

        #region 열기 / 흐름

        /// <summary>오프닝 대화를 시작하고 종료 시 onEnd를 호출한다. (패널은 아직 열지 않음)</summary>
        public void PlayOpening(Action onEnd) => PlayDialogue(OpeningDialogueID, onEnd);

        /// <summary>
        /// 전제 자원이 없으면 안내 대화를 재생하고 이벤트를 완료 처리한 뒤 true를 반환한다.
        /// Controller가 패널을 열기 직전에 호출한다. true면 패널을 열지 않고 종료한다.
        /// </summary>
        public bool TryHandleEmpty()
        {
            if (HasRequiredResource) return false;

            MorningEventManager.Instance?.MarkEventCompleted();
            PlayDialogue(EmptyDialogueID, DismissEventNpc, escSkippable: true);
            return true;
        }

        /// <summary>
        /// 임의의 대화 ID로 대화를 재생하고 종료 시 onEnd를 호출한다.
        /// 결과에 따른 분기 대화 출력에 사용한다. ID가 없으면 즉시 onEnd.
        /// escSkippable=true면 마무리 대화처럼 ESC 시 확인 없이 즉시 종료된다.
        /// </summary>
        protected void PlayDialogue(string dialogueID, Action onEnd, bool escSkippable = false)
        {
            var dialogue = DataManager.Instance.GetDialogue(dialogueID);
            if (dialogue != null)
                DialogueManager.Instance.StartDialogue(dialogue.StaticID, () => onEnd?.Invoke(), spineTarget: eventNpc, escSkippable: escSkippable);
            else
                onEnd?.Invoke();
        }

        /// <summary>
        /// 이벤트 패널을 먼저 닫은 뒤 결과 대화를 출력한다.
        /// 대화창(SpineDialogueView)이 이벤트 패널 위에 겹쳐 보이지 않게 한다.
        /// NPC 퇴장 애니메이션은 결과 대화가 끝난 뒤에 발동한다(대화 중 캐릭터가 나가지 않도록).
        /// </summary>
        protected void PlayClosing(string dialogueID)
        {
            closeDeferHandler?.Invoke();            // 패널 닫기 + 완료 통지 (NPC 퇴장은 보류)
            PlayDialogue(dialogueID, DismissEventNpc, escSkippable: true); // 결과 대화 종료 후 NPC 퇴장 (ESC 시 즉시 종료)
        }

        /// <summary>
        /// 패널이 열린 직후 호출. ESC 기본값만 설정한다.
        /// 서브클래스는 base 호출 후 자신의 초기 패널을 활성화한다.
        /// </summary>
        public virtual void OnOpened()
        {
            canEscape = false;                  // 항상 false: 강제 닫힘 방지, ESC·오버레이를 escapeCancelledAction으로 라우팅
            mainEscapeAction = RequestClose;    // 메인/단일 화면 기본 동작: 취소 확인 팝업
            escapeCancelledAction = mainEscapeAction;
            resultShowing = false;
        }

        /// <summary>
        /// 결과 표시 처리: 이벤트 완료 + ESC 복원 + 닫기 확인 생략 플래그 설정.
        /// 실제 패널 전환(메인 끄고 결과 켜기)은 서브클래스가 직접 한다.
        /// escapeAction을 지정하면 ESC가 그 동작을 실행한다(결과별 마무리 대화 등). 미지정 시 확인 없이 즉시 닫기.
        /// </summary>
        protected void MarkResultShown(Action escapeAction = null)
        {
            MorningEventManager.Instance?.MarkEventCompleted();
            resultShowing = true;
            canEscape = false;
            escapeCancelledAction = escapeAction ?? CloseImmediately;
        }

        #endregion

        #region ESC 처리

        /// <summary>서브패널(상세 등) 열기/닫기 시 ESC 동작을 동기화한다.</summary>
        protected void SetSubPanelEscape(bool subPanelOpen, Action onBack = null)
        {
            canEscape = false;                                          // 항상 false 유지
            escapeCancelledAction = subPanelOpen ? onBack : mainEscapeAction;
        }

        public override void OnEscapeClicked() => RequestClose();
        public override void OnEscapeCancelled() => escapeCancelledAction?.Invoke();

        #endregion

        #region 공용 헬퍼

        /// <summary>
        /// Level 3 btn_clicked 발행. 아침 이벤트는 유형 구분이 필수라 event_type을 항상 동봉한다.
        /// 컨트롤러가 도메인 실행기 역할만 해서 버튼 콜백이 View에 있으므로 여기에 헬퍼를 둔다.
        /// </summary>
        protected void SendButtonClick(string button, Dictionary<string, object> extra = null)
        {
            var parameters = extra ?? new Dictionary<string, object>();
            parameters["event_type"] = MorningEventManager.GetEventTypeAnalyticsName(EventType);
            AnalyticsManager.Instance?.SendButtonClick("morning_event", button, parameters);
        }

        protected void ShowPopupMessage(string message)
        {
            UIPopupController.Instance?.ShowPopup(message, onConfirm: () => { }, onCancel: null,
                type: PopupSfxType.Notify);
        }

        /// <summary>임의 색으로 텍스트를 감싸는 리치텍스트 헬퍼. 무기/상자 이름에만 적용한다.</summary>
        protected static string Colored(string text, Color color)
            => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        /// <summary>
        /// 닫기 요청. 아직 완료되지 않은 이벤트는 "취소하시겠습니까?" 확인 팝업을 먼저 띄운다.
        /// 확인 시 confirmedClose(미지정 시 단순 닫기 closeHandler)를 실행한다. 거절/패스로
        /// 닫아야 하는 이벤트는 그 동작을 넘겨, 닫을 때 제안·상자 등을 함께 초기화한다.
        /// 이미 완료됐거나 결과 표시 중이면 확인 없이 실행한다.
        /// </summary>
        protected void RequestClose() => RequestClose(null);

        protected void RequestClose(Action confirmedClose)
        {
            if (closeHandler == null) return;
            if (TutorialManager.Instance != null && TutorialManager.Instance.GuardBack()) return;
            Action onClose = confirmedClose ?? closeHandler;

            // "이 패널이 결과/완료 화면인지"로만 판단한다. 전역 morningEventCompleted(오늘 이벤트 완료)를
            // 보면 직전 이벤트의 완료가 잔존해, 다음 이벤트 메인 화면 닫기에서도 확인 팝업을 건너뛴다.
            if (resultShowing)
            {
                onClose.Invoke();
                return;
            }

            var eventData = DataManager.Instance.GetEventDataByType(EventType);
            string eventName = eventData?.DisplayName ?? EventType.ToString();
            UIPopupController.Instance?.ShowPopup(
                UIPopupController.SendBackEventConfirmMessage(eventName),
                onConfirm: () => onClose.Invoke(),
                onCancel: () => { });
        }

        /// <summary>확인 없이 즉시 닫는다. 결과 화면의 닫기 버튼이 바인딩한다.</summary>
        protected void CloseImmediately() => closeHandler?.Invoke();

        #endregion
    }
}
