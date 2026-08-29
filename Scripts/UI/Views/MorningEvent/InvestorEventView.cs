// Scripts/UI/Views/MorningEvent/InvestorEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 수상한 투자자 패널.
    /// 투자 확정 즉시 결과가 확정되며, 실제 골드는 다음 날 NPC 재등장 시 지급된다.
    /// </summary>
    public class InvestorEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.SuspiciousInvestor;
        protected override string OpeningDialogueID => "Investor_Intro";
        protected override string EmptyDialogueID => "Investor_Empty";

        // 최소 투자 금액도 못 내면 투자 자체가 불가능
        protected override bool HasRequiredResource =>
            EconomyManager.Instance.HasEnoughGold(ConfigManager.Instance.MorningEvent.investMinGold);

        private InvestorEventController Controller
            => UIControllerManager.Instance.GetController<InvestorEventController>();

        [Header("닫기 / 패널")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resultCloseButton;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject resultPanel;

        [Header("투자 UI")]
        [SerializeField] private Slider investSlider;
        [SerializeField] private TextMeshProUGUI investAmountText;
        [SerializeField] private TextMeshProUGUI investMinText;
        [SerializeField] private TextMeshProUGUI investMaxText;
        [SerializeField] private Button confirmButton;

        [Header("결과 패널 UI")]
        [SerializeField] private TextMeshProUGUI resultTypeText;
        [SerializeField] private TextMeshProUGUI resultGoldText;

        private int currentAmount;

        protected override void Awake()
        {
            base.Awake();
            closeButton?.onClick.AddListener(RequestClose);
            resultCloseButton?.onClick.AddListener(CloseImmediately);
            investSlider?.onValueChanged.AddListener(OnSliderChanged);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        public override void OnOpened()
        {
            base.OnOpened();

            UIPopupController.Instance?.ShowPlayerResourceBar();

            if (mainPanel != null) mainPanel.SetActive(true);
            if (resultPanel != null) resultPanel.SetActive(false);

            int day = GameManager.Instance.GameData.currentDay;
            int min = ConfigManager.Instance.MorningEvent.investMinGold;
            int max = min + day * ConfigManager.Instance.MorningEvent.investMaxGoldPerDay;

            if (investSlider != null)
            {
                investSlider.minValue = min;
                investSlider.maxValue = max;
                investSlider.value    = min;
            }
            if (investMinText != null) investMinText.text = $"{min:N0}G";
            if (investMaxText != null) investMaxText.text = $"{max:N0}G";
            currentAmount = min;
            UpdateAmountText(min);
        }

        private void OnSliderChanged(float value)
        {
            // 슬라이더 범위는 min~max 그대로 두고, 핸들만 소지 금액에서 막는다.
            // HasRequiredResource가 소지 금액 >= min을 보장하므로 min 아래로 잘리지 않는다.
            int gold = EconomyManager.Instance.CurrentGold;
            if (value > gold)
            {
                value = gold;
                investSlider.SetValueWithoutNotify(value);
            }

            currentAmount = (int)value;
            UpdateAmountText(currentAmount);
        }

        private void UpdateAmountText(int amount)
        {
            if (investAmountText != null) investAmountText.text = $"{amount:N0}G";
        }

        private void OnConfirmClicked()
        {
            SendButtonClick("confirm");
            var (success, message) = Controller.OnInvestConfirmed(currentAmount);
            if (!success) { ShowPopupMessage(message); return; }
            MorningEventManager.Instance?.MarkEventCompleted();
            // 패널을 닫은 뒤 투자액 비율에 따른 대화를 출력한다.
            PlayClosing(GetInvestAmountDialogueID());
        }

        /// <summary>투자액을 min~max 비율로 정규화해 5구간 대화 ID를 고른다.</summary>
        private string GetInvestAmountDialogueID()
        {
            int day = GameManager.Instance.GameData.currentDay;
            int min = ConfigManager.Instance.MorningEvent.investMinGold;
            int max = min + day * ConfigManager.Instance.MorningEvent.investMaxGoldPerDay;
            float ratio = max > min ? (float)(currentAmount - min) / (max - min) : 0f;

            string[] ids = { "Investor_Amount_Min", "Investor_Amount_Low", "Investor_Amount_Mid", "Investor_Amount_High", "Investor_Amount_Max" };
            float[] thresholds = ConfigManager.Instance.MorningEvent.investAmountDialogueThresholds;

            int last = ids.Length - 1;
            for (int i = 0; i < thresholds.Length && i < last; i++)
                if (ratio < thresholds[i]) return ids[i];
            return ids[last];
        }

        /// <summary>
        /// InvestorResult NPC 대화 종료 후 결과만 표시하는 전용 진입 경로.
        /// OnOpened를 거치지 않는 직표시 경로라 베이스 초기화(ESC 라우팅)와 리소스바 표시를 명시적으로 수행한다.
        /// MarkResultShown은 오전 이벤트 완료(MarkEventCompleted)를 부르므로 여기서는 쓰지 않는다.
        /// </summary>
        public void OpenResultDirect(string dialogueID, int returnedGold)
        {
            base.OnOpened();
            UIPopupController.Instance?.ShowPlayerResourceBar();
            ShowResultDirect(dialogueID, returnedGold);
        }

        /// <summary>
        /// InvestorResult NPC 대화 종료 후 Controller에서 직접 호출.
        /// 대화·메인패널 없이 ResultPanel만 표시한다.
        /// </summary>
        public void ShowResultDirect(string dialogueID, int returnedGold)
        {
            if (mainPanel != null) mainPanel.SetActive(false);

            if (resultTypeText != null)
            {
                resultTypeText.text = dialogueID switch
                {
                    "Investor_Jackpot"      => L("Investor_ResultJackpot"),
                    "Investor_GreatSuccess" => L("Investor_ResultGreat"),
                    "Investor_Success"      => L("Investor_ResultSuccess"),
                    _                       => L("Investor_ResultLoss")
                };
            }

            if (resultGoldText != null)
            {
                string multiply = dialogueID switch
                {
                    "Investor_Jackpot"      => $"x{ConfigManager.Instance.MorningEvent.investJackpotMulti}",
                    "Investor_GreatSuccess" => $"x{ConfigManager.Instance.MorningEvent.investBigMulti}",
                    "Investor_Success"      => $"x{ConfigManager.Instance.MorningEvent.investSuccessMulti}",
                    _                       => "x0"
                };
                resultGoldText.text = returnedGold > 0
                    ? $"+{returnedGold:N0}G ({multiply})"
                    : "0G";
            }

            // InvestorResult 확인은 오전 이벤트가 아니므로 MarkEventCompleted 호출 안 함
            if (resultPanel != null) resultPanel.SetActive(true);
            resultShowing = true;
            canEscape = false;
            escapeCancelledAction = CloseImmediately;
        }

        public override void Close()
        {
            base.Close();
            UIPopupController.Instance?.HidePlayerResourceBar();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            closeButton?.onClick.RemoveAllListeners();
            resultCloseButton?.onClick.RemoveAllListeners();
            investSlider?.onValueChanged.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
        }
    }
}
