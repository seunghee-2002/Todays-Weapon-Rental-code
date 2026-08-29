// Scripts/UI/Controllers/MorningEvent/InvestorEventController.cs
namespace TodaysWeaponRental
{
    public class InvestorEventController : MorningEventControllerBase<InvestorEventView>
    {
        public (bool success, string message) OnInvestConfirmed(int amount)
            => MorningEventManager.Instance.ExecuteInvestment(amount);

        /// <summary>
        /// InvestorResult NPC 대화 종료 후 호출 — 오프닝 대화/메인 패널 없이 결과만 표시한다.
        /// </summary>
        public void OpenResult(VisitorNPC npc, string dialogueID, int returnedGold)
        {
            // 대화 종료 후 결과 확인 시점에 투자 데이터 초기화 (저장/복원 시 중복 스폰 방지)
            var pd = GameManager.Instance.GameData;
            pd.hasPendingInvestment            = false;
            pd.pendingInvestorReturnedGold     = 0;
            pd.pendingInvestorResultDialogueID = null;

            currentNPC = npc;

            UIManager.Instance.OpenPanel<InvestorEventView>();
            // 직표시 경로도 베이스 초기화/리소스바를 거치도록 전용 진입 메서드 사용
            view.OpenResultDirect(dialogueID, returnedGold);
        }
    }
}
