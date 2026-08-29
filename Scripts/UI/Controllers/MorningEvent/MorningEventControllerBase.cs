// Scripts/UI/Controllers/MorningEvent/MorningEventControllerBase.cs
namespace TodaysWeaponRental
{
    /// <summary>
    /// 모닝 이벤트 Controller의 공통 기반.
    /// NPC 추적 + 오프닝 대화 후 패널 열기 + 닫기를 제공한다.
    /// 각 이벤트는 이 클래스를 상속해 결과 반환형 메서드만 추가한다.
    /// </summary>
    public abstract class MorningEventControllerBase<TView> : BaseController<TView>
        where TView : MorningEventViewBase
    {
        protected VisitorNPC currentNPC;

        protected override void Awake()
        {
            base.Awake();
            view?.SetCloseHandler(CloseEvent);
            view?.SetCloseDeferHandler(CloseEventDeferLeave);
        }

        #region 진입 / 종료

        /// <summary>MorningEventManager.StartEvent()에서 호출. 오프닝 대화 → 패널 표시.</summary>
        public virtual void OpenEvent(VisitorNPC npc)
        {
            currentNPC = npc;
            view.SetEventNpc(npc);
            view.PlayOpening(OpenPanelAndShow);
        }

        protected void OpenPanelAndShow()
        {
            // 인트로 대화 후 — 전제 자원(무기 등)이 없으면 패널 대신 안내 대화만 재생하고 종료한다.
            // 안내 대화(TryHandleEmpty)가 끝난 뒤 NPC가 퇴장하도록 leave를 미룬다.
            if (view.TryHandleEmpty())
            {
                CloseEventDeferLeave();
                return;
            }

            UIManager.Instance.OpenPanel<TView>();
            view.OnOpened();
        }

        /// <summary>패널 닫기 + NPC 퇴장 + 완료 통지 (즉시 전체 종료).</summary>
        public void CloseEvent()
        {
            var npc = currentNPC;
            currentNPC = null;

            UIManager.Instance.ClosePanel<TView>();
            npc?.EndInteraction();
            VisitorManager.Instance?.OnEventNPCInteractionCompleted();
        }

        /// <summary>
        /// 패널 닫기 + 완료 통지만 하고 NPC 퇴장은 보류한다.
        /// 결과/마무리 대화(SpineDialogueView)를 패널 위에 겹치지 않게 띄운 뒤,
        /// 대화 종료 시 View.DismissEventNpc로 퇴장 애니메이션을 발동시킨다.
        /// </summary>
        public void CloseEventDeferLeave()
        {
            currentNPC = null;

            UIManager.Instance.ClosePanel<TView>();
            VisitorManager.Instance?.OnEventNPCInteractionCompleted();
        }

        #endregion
    }
}
