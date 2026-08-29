// Scripts/UI/Views/MorningEvent/RefugeeEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 난민 돕기 패널.
    /// 오프닝 대화 → 기부/거절 버튼 → 결과 텍스트.
    /// </summary>
    public class RefugeeEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.RefugeeHelp;
        protected override string OpeningDialogueID => "Refugee_Intro";

        private RefugeeEventController Controller
            => UIControllerManager.Instance.GetController<RefugeeEventController>();

        [Header("기부 UI")]
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button donateButton;
        [SerializeField] private Button rejectButton;

        protected override void Awake()
        {
            base.Awake();
            donateButton?.onClick.AddListener(OnDonateClicked);
            rejectButton?.onClick.AddListener(OnRejectClicked);
        }

        public override void OnOpened()
        {
            base.OnOpened();

            UIPopupController.Instance?.ShowPlayerResourceBar();

            // 닫기 시도(ESC·오버레이) 모두 취소 확인 팝업 → 확인 시 거절(페널티). 닫기 버튼 없음
            mainEscapeAction = () => RequestClose(OnRejectClicked);
            escapeCancelledAction = mainEscapeAction;

            int cost = MorningEventManager.Instance.GetRefugeeCost();
            if (costText != null)
            {
                costText.text = L("Refugee_CostLabel", ("gold", cost.ToString("N0")));
                costText.color = EconomyManager.Instance.HasEnoughGold(cost)
                    ? ColorManager.Instance.GetGoldColor()
                    : ColorManager.Instance.GetRedColor();
            }
        }

        private void OnDonateClicked()
        {
            SendButtonClick("donate");
            var (success, actuallyDonated, message) = Controller.OnDonateClicked();
            if (!success) { ShowPopupMessage(message); return; }
            MorningEventManager.Instance?.MarkEventCompleted();
            escapeCancelledAction = () => { };   // 확정 후 결과 팝업이 ESC로 닫혀도 기부/거절이 중복 실행되지 않도록 차단
            // 골드 부족 시 매니저가 거절로 처리하므로, 실제 결과(actuallyDonated)에 맞는 대화를 출력한다.
            string dialogueID = actuallyDonated ? "Refugee_Donate" : "Refugee_Reject";
            UIPopupController.Instance?.ShowPopup(
                message,
                onConfirm: () => PlayClosing(dialogueID),
                onCancel: null,
                type: PopupSfxType.Notify);
        }

        private void OnRejectClicked()
        {
            SendButtonClick("reject");
            var (_, message) = Controller.OnRejectClicked();
            MorningEventManager.Instance?.MarkEventCompleted();
            escapeCancelledAction = () => { };   // 확정 후 결과 팝업이 ESC로 닫혀도 거절이 중복 실행되지 않도록 차단
            // 결과 표시 후 확인 시 패널을 닫고 거절 대화를 출력한다.
            UIPopupController.Instance?.ShowPopup(
                message,
                onConfirm: () => PlayClosing("Refugee_Reject"),
                onCancel: null,
                type: PopupSfxType.Notify);
        }

        public override void Close()
        {
            base.Close();
            UIPopupController.Instance?.HidePlayerResourceBar();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            donateButton?.onClick.RemoveAllListeners();
            rejectButton?.onClick.RemoveAllListeners();
        }
    }
}
