// Scripts/UI/Views/QuestResultView.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class QuestResultView : BaseView
    {
        [Header("Completed Panel")]
        [SerializeField] private GameObject completedPanel;
        [SerializeField] private TextMeshProUGUI completedTitleText;
        [SerializeField] private TextMeshProUGUI completedWeekText;
        [SerializeField] private TextMeshProUGUI completedGoldText;
        [SerializeField] private TextMeshProUGUI reputationRewardText;
        [SerializeField] private TextMeshProUGUI completedInsightText;
        [SerializeField] private Button claimRewardButton;

        [Header("Failed Panel")]
        [SerializeField] private GameObject failedPanel;
        [SerializeField] private TextMeshProUGUI failedTitleText;
        [SerializeField] private TextMeshProUGUI failedWeekText;
        [SerializeField] private TextMeshProUGUI failedGoldText;
        [SerializeField] private TextMeshProUGUI reputationPenaltyText;
        [SerializeField] private Button payFineButton;

        [SerializeField] private QuestResultController controller;

        private bool isSuccess;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = false;
            canEscape = false;
        }

        protected override void SubscribeEvents()
        {
            claimRewardButton?.onClick.AddListener(OnClaimRewardClicked);
            payFineButton?.onClick.AddListener(OnPayFineClicked);
        }

        protected override void UnsubscribeEvents()
        {
            claimRewardButton?.onClick.RemoveAllListeners();
            payFineButton?.onClick.RemoveAllListeners();
        }

        /// <param name="displayWeek">결과가 속한 주차. 엔드리스 템플릿은 weekNumber가 풀 인덱스라 표시에 쓸 수 없다.</param>
        /// <param name="fineAmount">실패 시 청구되는 벌금. 엔드리스는 SO 고정값이 아니라 주차 곡선이다.</param>
        public void Initialize(WeeklyQuestInstance quest, int displayWeek, int fineAmount)
        {
            if (quest == null || quest.questData == null) return;

            isSuccess = quest.status == QuestStatus.Completed;
            if (isSuccess)
                ShowCompletedPanel(quest, displayWeek);
            else
                ShowFailedPanel(quest, displayWeek, fineAmount);
        }

        #endregion

        #region UI 업데이트 메서드

        /// <summary>퀘스트 제목은 SO 값이라 아직 한국어다(Phase 5). 감싸는 문구만 번역된다.</summary>
        private static string Title(string key, string questTitle)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "title", questTitle } } });

        private void ShowCompletedPanel(WeeklyQuestInstance quest, int displayWeek)
        {
            completedPanel?.SetActive(true);
            failedPanel?.SetActive(false);

            if (completedTitleText != null)
                completedTitleText.text = Title("QuestResult_CompletedTitle", quest.questData.DisplayTitle);

            if (completedWeekText != null)
                completedWeekText.text = displayWeek.ToString();

            if (completedGoldText != null)
                completedGoldText.text = quest.questData.goldReward.ToString("N0");

            if (reputationRewardText != null)
                reputationRewardText.text = quest.questData.reputationReward.ToString("N0");

            if (completedInsightText != null)
                completedInsightText.text = quest.questData.insightReward.ToString();
        }

        private void ShowFailedPanel(WeeklyQuestInstance quest, int displayWeek, int fineAmount)
        {
            completedPanel?.SetActive(false);
            failedPanel?.SetActive(true);

            if (failedTitleText != null)
                failedTitleText.text = Title("QuestResult_FailedTitle", quest.questData.DisplayTitle);

            if (failedWeekText != null)
                failedWeekText.text = displayWeek.ToString();

            if (failedGoldText != null)
                failedGoldText.text = $"-{fineAmount:N0}";

            if (reputationPenaltyText != null)
                reputationPenaltyText.text = $"-{quest.questData.reputationPenalty:N0}";
        }

        #endregion

        #region 이벤트 핸들러

        private void OnClaimRewardClicked()
        {
            controller?.OnClaimReward();
        }

        private void OnPayFineClicked()
        {
            controller?.OnPayFine();
        }

        public override void OnEscapeCancelled()
        {
            UIPopupController.Instance?.ShowToast(
                LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Messages", isSuccess ? "QuestResult_ClaimRewardWarn" : "QuestResult_PayFineWarn"),
                type: PopupSfxType.Warning);
        }

        #endregion
    }
}