using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class QuestResultController : BaseController<QuestResultView>
    {
        private static string M(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        #region 초기화

        protected override void SubscribeControllerEvents() { }
        protected override void UnsubscribeControllerEvents() { }

        protected override void OnPanelOpen()
        {
            var quest = QuestManager.Instance?.LastResultQuest;
            if (quest == null)
            {
                Log.Warn("[QuestResultController] LastResultQuest 없음");
                return;
            }
            view?.Initialize(quest, QuestManager.Instance.LastResultWeek, QuestManager.Instance.LastResultFine);
        }

        #endregion

        #region View로부터 호출되는 메서드

        // 성공 케이스: 보상은 CheckQuestDeadline에서 이미 자동 지급됨. 확인 버튼은 단순 닫기.
        public void OnClaimReward()
        {
            AnalyticsManager.Instance?.SendButtonClick("quest", "claim_reward");
            UIManager.Instance?.ClosePanel<QuestResultView>();
        }

        public void OnPayFine()
        {
            AnalyticsManager.Instance?.SendButtonClick("quest", "pay_fine");

            var quest = QuestManager.Instance?.LastResultQuest;
            if (quest == null || quest.status != QuestStatus.Failed)
            {
                Log.Warn("[QuestResultController] Quest not failed");
                return;
            }
            int fineAmount = QuestManager.Instance.LastResultFine;
            // 환전 취소/유산 부족이 곧바로 게임오버로 이어지지 않도록 전용 확인 팝업을 거친다
            EconomyManager.Instance.EnsureGold(fineAmount, onReady: DoPayFine, onCancel: ConfirmGameOverByBankruptcy);
        }

        #endregion

        #region 내부 메서드

        private void DoPayFine()
        {
            // 검증·골드/평판 차감·멱등은 매니저가 책임. UI는 결과만 표현한다.
            if (QuestManager.Instance.PayFine())
                UIPopupController.Instance?.ShowToast(M("QuestResult_FinePaid"));
            UIManager.Instance?.ClosePanel<QuestResultView>();
        }

        // 오클릭 한 번으로 회차가 전손되지 않도록, 폐업(게임오버)은 반드시 확인 팝업을 거친다.
        // 취소하면 벌금 화면에 남아 다시 납부를 시도할 수 있다.
        private void ConfirmGameOverByBankruptcy()
        {
            UIPopupController.Instance?.ShowPopup(
                M("QuestResult_BankruptcyConfirm"),
                onConfirm: DoGameOver,
                onCancel: () => { },
                type: PopupSfxType.Warning);
        }

        private void DoGameOver()
        {
            UIManager.Instance?.ClosePanel<QuestResultView>();
            GameManager.Instance?.OnGameOver();
        }

        #endregion
    }
}
