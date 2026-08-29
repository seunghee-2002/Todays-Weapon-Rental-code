using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 퀘스트 UI 컨트롤러
    /// </summary>
    public class QuestController : BaseController<QuestView>
    {
        private WeeklyQuestInstance currentQuest;

        private static string M(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        #region 이벤트 구독

        protected override void SubscribeControllerEvents()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted         += HandleQuestStarted;
                QuestManager.Instance.OnQuestProgressUpdated += HandleProgressUpdated;
                QuestManager.Instance.OnQuestCompleted       += HandleQuestCompleted;
                QuestManager.Instance.OnQuestFailed          += HandleQuestFailed;
            }
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged += HandleDayChanged;
        }

        protected override void UnsubscribeControllerEvents()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted         -= HandleQuestStarted;
                QuestManager.Instance.OnQuestProgressUpdated -= HandleProgressUpdated;
                QuestManager.Instance.OnQuestCompleted       -= HandleQuestCompleted;
                QuestManager.Instance.OnQuestFailed          -= HandleQuestFailed;
            }
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged -= HandleDayChanged;
        }

        #endregion

        #region 이벤트 핸들러

        private void HandleQuestStarted(WeeklyQuestInstance quest)
        {
            currentQuest = quest;
            Log.Info($"[QuestController] 새 퀘스트 시작: {quest.questData.questTitle}");
        }

        private void HandleProgressUpdated(WeeklyQuestInstance quest, int requirementIndex)
        {
            currentQuest = quest;
            if (view != null && view.IsOpen)
                UpdateUI();
        }

        private void HandleQuestCompleted(WeeklyQuestInstance quest)
        {
            currentQuest = quest;
            UIPopupController.Instance?.ShowToast(M("Quest_CompletedToast"));
            if (view != null && view.IsOpen)
                UpdateUI();
        }

        private void HandleQuestFailed(WeeklyQuestInstance quest, int fineAmount)
        {
            currentQuest = quest;
            UIPopupController.Instance?.ShowToast(M("Quest_FailedToast"), type: PopupSfxType.Warning);
        }

        private void HandleDayChanged(int newDay)
        {
            if (view != null && view.IsOpen)
                UpdateUI();
        }

        #endregion

        #region Public Methods

        protected override void OnPanelOpen()
        {
            currentQuest = QuestManager.Instance.CurrentQuest;
            if (currentQuest == null)
            {
                Log.Warn("[QuestController] No active quest");
            }
            else
            {
                UpdateUI();
            }
            TutorialManager.Instance?.OnTutorialQuestViewOpened();   // 10단계 훅(가드는 TutorialManager 내부)
        }

        private void UpdateUI()
        {
            if (view == null || currentQuest == null) return;
            int currentDay = GameManager.Instance?.GameData?.currentDay ?? 1;
            int currentWeek = GameManager.Instance?.GameData?.currentWeek ?? 1;
            view.Initialize(currentQuest, currentDay, currentWeek);
        }

        public void OnCloseClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("quest", "close");
            UIManager.Instance?.ClosePanel<QuestView>();
            TutorialManager.Instance?.OnTutorialQuestClosed();   // 10단계 훅(가드는 TutorialManager 내부)
        }

        #endregion
    }
}
