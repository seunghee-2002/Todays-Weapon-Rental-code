// Scripts/UI/Views/QuestView.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class QuestView : BaseView
    {
        [Header("Header")]
        [SerializeField] private Button closeButton;

        [Header("Quest Info")]
        [SerializeField] private TextMeshProUGUI questNameText;
        [SerializeField] private TextMeshProUGUI weekText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI reputationText;
        [SerializeField] private TextMeshProUGUI remainingDaysText;
        [SerializeField] private Button toggleRewardModeButton;

        [Header("Progress")]
        [SerializeField] private TextMeshProUGUI overallProgressText;
        [SerializeField] private Slider overallProgressSlider;

        [Header("Requirements")]
        [SerializeField] private ScrollRect requirementListScrollRect;
        [SerializeField] private Transform requirementContainer;
        [SerializeField] private GameObject requirementCardPrefab;

        [Header("Tutorial")]
        [Tooltip("10단계 하이라이트 영역 A - QuestInfo(퀘스트 정보 패널)")]
        [SerializeField] private RectTransform tutorialQuestInfoArea;
        [Tooltip("10단계 하이라이트 영역 B - MiniQuestInfo의 Content(요구 조건 목록). Size Fitter라 접근자에서 강제 리빌드 후 rect를 읽는다")]
        [SerializeField] private RectTransform tutorialRequirementContent;

        private List<QuestRequirementCard> requirementCards = new List<QuestRequirementCard>();
        private bool showSuccessMode = true;
        private WeeklyQuestInstance cachedQuest;
        private int cachedWeek = 1;

        [SerializeField] private QuestController controller;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;
            isCanClickOverlay = true;
            canEscape = true;
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
            toggleRewardModeButton?.onClick.AddListener(OnToggleRewardModeClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            toggleRewardModeButton?.onClick.RemoveAllListeners();
        }

        /// <param name="currentWeek">표시용 주차. 엔드리스 템플릿은 weekNumber가 풀 인덱스라 쓸 수 없다.</param>
        public void Initialize(WeeklyQuestInstance quest, int currentDay, int currentWeek)
        {
            if (quest == null || quest.questData == null) return;

            cachedQuest = quest;
            cachedWeek = currentWeek;

            UpdateTimer(quest, currentDay);
            UpdateQuestInfo(quest);
            UpdateRequirements(quest);
            UpdateOverallProgress(quest);
        }

        #endregion

        #region UI 업데이트 메서드

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private void UpdateTimer(WeeklyQuestInstance quest, int currentDay)
        {
            int remainingDays = quest.RemainingDays(currentDay);

            if (remainingDaysText != null)
            {
                string redHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetRedColor());
                remainingDaysText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "Quest_RemainingDays",
                    arguments: new object[] { new Dictionary<string, object> { { "color", redHex }, { "days", remainingDays } } });
            }
        }

        private void UpdateQuestInfo(WeeklyQuestInstance quest)
        {
            if (questNameText != null)
                questNameText.text = quest.questData.DisplayTitle;

            if (weekText != null)
                weekText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "Quest_Week",
                    arguments: new object[] { new Dictionary<string, object> { { "week", cachedWeek } } });

            if (showSuccessMode)
            {
                if (statusText != null)    statusText.text    = L("Quest_StatusSuccess");
                if (goldText != null)      goldText.text      = $"+{quest.questData.goldReward}";
                if (reputationText != null) reputationText.text = $"+{quest.questData.reputationReward}";
            }
            else
            {
                if (statusText != null)    statusText.text    = L("Quest_StatusFail");
                if (goldText != null)      goldText.text      = $"-{quest.questData.weeklyFine}";
                if (reputationText != null) reputationText.text = $"-{quest.questData.reputationPenalty}";
            }
        }

        private void UpdateRequirements(WeeklyQuestInstance quest)
        {
            foreach (var card in requirementCards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }
            requirementCards.Clear();

            for (int i = 0; i < quest.questData.requirements.Count; i++)
            {
                var req = quest.questData.requirements[i];
                GameObject cardObj = Instantiate(requirementCardPrefab, requirementContainer);
                QuestRequirementCard card = cardObj.GetComponent<QuestRequirementCard>();

                if (card != null)
                {
                    int current = quest.currentProgress[i];
                    int target = req.targetCount;
                    bool isCompleted = current >= target;

                    card.Initialize(req, current, target, isCompleted);
                    requirementCards.Add(card);
                }
            }

            requirementListScrollRect.ResetPosition(this);
        }

        private void UpdateOverallProgress(WeeklyQuestInstance quest)
        {
            float progress = quest.GetOverallProgress();

            if (overallProgressText != null)
                overallProgressText.text = $"{progress * 100:0.#}%";

            if (overallProgressSlider != null)
            {
                overallProgressSlider.minValue = 0f;
                overallProgressSlider.maxValue = 1f;
                overallProgressSlider.value = Mathf.Clamp01(progress);
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            controller?.OnCloseClicked();
        }

        private void OnToggleRewardModeClicked()
        {
            showSuccessMode = !showSuccessMode;
            if (cachedQuest != null)
                UpdateQuestInfo(cachedQuest);
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion

        #region 튜토리얼

        /// <summary>튜토리얼 하이라이트용(10단계) - 닫기 버튼 RectTransform.</summary>
        public RectTransform GetCloseButtonRect() => closeButton != null ? closeButton.transform as RectTransform : null;

        /// <summary>튜토리얼 하이라이트용(10단계) - 퀘스트 정보 패널(QuestInfo) 영역.</summary>
        public RectTransform GetQuestInfoAreaRect() => tutorialQuestInfoArea;

        /// <summary>튜토리얼 하이라이트용(10단계) - 요구 조건 목록(MiniQuestInfo Content). Size Fitter라 읽기 전 강제 리빌드로 rect를 확정한다.</summary>
        public RectTransform GetRequirementContentRect()
        {
            if (tutorialRequirementContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(tutorialRequirementContent);
            return tutorialRequirementContent;
        }

        #endregion
    }
}
