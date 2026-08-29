// Scripts/UI/Views/MorningEvent/BlackMarketEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class BlackMarketEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.BlackMarket;
        protected override string OpeningDialogueID => "BlackMarket_Intro";

        private BlackMarketEventController Controller
            => UIControllerManager.Instance.GetController<BlackMarketEventController>();

        [Header("닫기")]
        [SerializeField] private Button closeButton;

        [Header("상품 표시")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image weaponIconBG;
        [SerializeField] private Image weaponIconFrame;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private TextMeshProUGUI priceText;

        [Header("버튼")]
        [SerializeField] private Button weaponIconButton;
        [SerializeField] private Button buyButton;

        private WeaponInstance offer;
        private int price;

        protected override void Awake()
        {
            base.Awake();
            closeButton?.onClick.AddListener(() => RequestClose(OnRejectClicked));   // 닫기 = 거절(제안 삭제)
            weaponIconButton?.onClick.AddListener(OnWeaponIconClicked);
            buyButton?.onClick.AddListener(OnBuyClicked);
        }

        public override void OnOpened()
        {
            base.OnOpened();

            // 닫기 시도(ESC·오버레이·X) 모두 취소 확인 팝업 → 확인 시 거절(제안 삭제)
            mainEscapeAction = () => RequestClose(OnRejectClicked);
            escapeCancelledAction = mainEscapeAction;

            (offer, price) = MorningEventManager.Instance.GetBlackMarketOffer();

            if (offer == null)
            {
                // 재고 없음 — 대화만 표시 후 닫기
                DialogueManager.Instance.StartDialogue("BlackMarket_NoStock", () => Controller.CloseEvent(), spineTarget: eventNpc);
                return;
            }

            UpdateShopInfo();
        }

        private void UpdateShopInfo()
        {
            if (weaponIcon   != null && offer.weaponData?.icon != null) weaponIcon.sprite = offer.weaponData.icon;
            if (weaponIconBG != null) weaponIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(offer.weaponData.baseGrade);
            if (weaponIconFrame != null && offer.weaponData?.icon != null) weaponIconFrame.sprite = IconManager.Instance.GetFrameByGrade(offer.weaponData.baseGrade);
            if (weaponNameText != null)
            {
                weaponNameText.text = offer.weaponData?.DisplayName ?? "-";
                weaponNameText.color = ColorManager.Instance.GetGradeAccentColor(offer.weaponData.baseGrade);
            }
            if (priceText    != null)
            {
                priceText.text = $"{price:N0}G";
                priceText.color = EconomyManager.Instance.HasEnoughGold(price)
                    ? ColorManager.Instance.GetBlackColor()
                    : ColorManager.Instance.GetRedColor();
            }
            if (buyButton    != null) buyButton.interactable = true;
        }

        private void OnWeaponIconClicked()
        {
            SendButtonClick("weapon_icon");
            if (offer == null) return;

            int revealedCount = InsightManager.Instance.GetBlackMarketRevealCount(offer.effects?.Count ?? 0);
            var popup = UIManager.Instance.GetOrInstantiatePanel<WeaponDetailPopup>();
            UIManager.Instance.OpenPanel<WeaponDetailPopup>();
            popup?.Initialize(offer, revealedCount);
        }

        private void OnBuyClicked()
        {
            SendButtonClick("buy");
            int repPenalty = ConfigManager.Instance.MorningEvent.blackMarketRepPenalty;
            string weaponName = offer.weaponData?.DisplayName
                                ?? LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", "Common_Weapon");
            string coloredName = Colored(weaponName, ColorManager.Instance.GetGradeAccentColor(offer.weaponData.baseGrade));
            string confirmMessage = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", "BlackMarket_PurchaseConfirm",
                arguments: new object[] { new System.Collections.Generic.Dictionary<string, object> {
                    { "name", coloredName }, { "penalty", repPenalty } } });

            UIPopupController.Instance.ShowPopup(confirmMessage, onConfirm: ExecuteBuy, onCancel: () => { });
        }

        private void ExecuteBuy()
        {
            EconomyManager.Instance.EnsureGold(price, onReady: DoBlackMarketBuy, onCancel: OnNoGold);
        }

        private void OnNoGold()
        {
            DialogueManager.Instance.StartDialogue("BlackMarket_NoGold", null, spineTarget: eventNpc);
        }

        private void DoBlackMarketBuy()
        {
            var (success, _) = Controller.OnBlackMarketBuyConfirmed(offer, price);
            if (!success) return;

            SoundManager.Instance?.PlaySFX("Buy");

            var purchasedWeapon = offer;
            offer = null;

            MorningEventManager.Instance.MarkEventCompleted();
            // 패널만 닫고 NPC 퇴장은 결과 대화 종료 후로 미룬다(대화 중 캐릭터가 나가지 않도록).
            Controller.CloseEventDeferLeave();

            var popup = UIManager.Instance.GetOrInstantiatePanel<WeaponDetailPopup>();
            UIManager.Instance.OpenPanel<WeaponDetailPopup>();
            popup?.Initialize(purchasedWeapon);
            // 결과 무기 팝업을 닫으면 결과와 무관하게 랜덤 3택 대화를 출력하고, 대화 종료 후 NPC가 퇴장한다.
            string talkID = $"BlackMarket_Talk{Random.Range(1, 4)}";
            popup?.SetOnClose(() => PlayDialogue(talkID, DismissEventNpc));
        }

        private void OnRejectClicked()
        {
            SendButtonClick("reject");
            GameManager.Instance.GameData.blackMarketOfferSaveData = null;
            Controller.CloseEvent();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            closeButton?.onClick.RemoveAllListeners();
            weaponIconButton?.onClick.RemoveAllListeners();
            buyButton?.onClick.RemoveAllListeners();
        }
    }
}
