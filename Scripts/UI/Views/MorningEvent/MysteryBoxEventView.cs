// Scripts/UI/Views/MorningEvent/MysteryBoxEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class MysteryBoxEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.MysteryBox;
        protected override string OpeningDialogueID => "MysteryBox_Intro";

        private MysteryBoxEventController Controller
            => UIControllerManager.Instance.GetController<MysteryBoxEventController>();

        [Header("닫기 / 패널")]
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject resultPanel;

        [Header("상자 이미지")]
        [SerializeField] private Image boxImage;
        [SerializeField] private Image[] glowImages;   // 상자 뒤 3겹 glow 레이어

        [Header("메인 패널 UI")]
        [SerializeField] private TextMeshProUGUI tierNameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button oddsButton;   // 확률 보기 버튼

        [Header("확률 안내 팝업")]
        [SerializeField] private GameObject oddsPanel;
        [SerializeField] private Button oddsCloseButton;
        [SerializeField] private Button oddsOverlay;   // 뒤 클릭 시 닫기
        [SerializeField] private TextMeshProUGUI oddsTitleText;
        [SerializeField] private Transform oddsContainer;
        [SerializeField] private MysteryBoxOddsItem oddsItemPrefab;

        [Header("결과 패널 UI")]
        [SerializeField] private TextMeshProUGUI resultMessageText;
        [SerializeField] private Button resultCloseButton;   // 결과 패널 닫기 버튼
        [SerializeField] private Button resultOverlay;   // 결과 패널 뒤 전체 오버레이 (클릭 시 닫기)
        [SerializeField] private Transform resultContainer;
        [SerializeField] private MaterialInventoryCardItem materialCardPrefab;
        [SerializeField] private WeaponInventoryCardItem weaponCardPrefab;

        private int currentTier;
        // 서로 다른 재료가 나올 수 있어 종류별 카드 목록으로 관리
        private readonly List<MaterialInventoryCardItem> spawnedMaterialCards = new List<MaterialInventoryCardItem>();
        private WeaponInventoryCardItem spawnedWeaponCard;

        protected override void Awake()
        {
            base.Awake();
            closeButton?.onClick.AddListener(() => RequestClose(OnPassClicked));   // 닫기 = 패스(상자 등급 리셋)
            buyButton?.onClick.AddListener(OnBuyClicked);
            resultCloseButton?.onClick.AddListener(OnResultCloseClicked);
            resultOverlay?.onClick.AddListener(OnResultCloseClicked);
            oddsButton?.onClick.AddListener(OpenOddsPanel);
            oddsCloseButton?.onClick.AddListener(CloseOddsPanel);
            oddsOverlay?.onClick.AddListener(CloseOddsPanel);
        }

        #region 확률 안내 팝업

        private void OpenOddsPanel()
        {
            if (oddsTitleText != null)
                oddsTitleText.text = L("MysteryBox_OddsTitleFor", ("box", GetTierName(currentTier)));

            MysteryBoxOddsItem.Rebuild(oddsContainer, oddsItemPrefab,
                MorningEventManager.Instance.GetMysteryBoxOdds(currentTier));

            oddsPanel?.SetActive(true);
            SetSubPanelEscape(true, CloseOddsPanel);
        }

        private void CloseOddsPanel()
        {
            oddsPanel?.SetActive(false);
            SetSubPanelEscape(false);
        }

        #endregion

        private void OnResultCloseClicked()
        {
            SendButtonClick("result_close");

            // 상자 등급(tier)에 따른 대화 출력 후 닫는다.
            string id = currentTier switch
            {
                0 => "MysteryBox_Normal",
                1 => "MysteryBox_Rare",
                2 => "MysteryBox_Mythic",
                _ => "MysteryBox_Normal"
            };
            PlayClosing(id);
        }

        public override void OnOpened()
        {
            base.OnOpened();

            UIPopupController.Instance?.ShowPlayerResourceBar();

            if (mainPanel != null) mainPanel.SetActive(true);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (oddsPanel != null) oddsPanel.SetActive(false);

            // 닫기 시도(ESC·오버레이·X) 모두 취소 확인 팝업 → 확인 시 Pass(상자 등급 리셋). 결과 표시 시 MarkResultShown에서 복원
            mainEscapeAction = () => RequestClose(OnPassClicked);
            escapeCancelledAction = mainEscapeAction;

            if (buyButton != null) buyButton.interactable = true;
            if (closeButton != null) closeButton.interactable = true;

            currentTier = MorningEventManager.Instance.GetRandomMysteryBoxTier();

            // 2회차 재방문 시 이전 오픈 애니메이션의 스케일/회전이 남아있지 않도록 초기화
            if (boxImage != null)
            {
                boxImage.transform.DOKill();
                boxImage.transform.localScale = Vector3.one;
                boxImage.transform.localRotation = Quaternion.identity;
                boxImage.sprite = IconManager.Instance.GetMysteryBoxIcon(currentTier, false);
            }

            // glow 3겹에 tier 색을 입히고 alpha 0으로 숨겨둔다 (오픈 애니메이션에서 페이드인)
            Color glowColor = ColorManager.Instance.GetMysteryBoxColor(currentTier);
            if (glowImages != null)
                foreach (var glow in glowImages)
                    if (glow != null)
                    {
                        glow.color = glowColor;
                    }

            if (tierNameText != null) tierNameText.text = GetTierName(currentTier);

            int cost = MorningEventManager.Instance.GetMysteryBoxCost(currentTier);
            if (costText != null)
            {
                costText.text = $"{cost:N0}G";
                costText.color = EconomyManager.Instance.HasEnoughGold(cost)
                    ? ColorManager.Instance.GetBlackColor()
                    : ColorManager.Instance.GetRedColor();
            }
        }

        private void OnBuyClicked()
        {
            SendButtonClick("buy");
            string coloredBox = Colored(GetTierName(currentTier), ColorManager.Instance.GetMysteryBoxColor(currentTier));
            UIPopupController.Instance?.ShowPopup(
                L("MysteryBox_PurchaseConfirm", ("box", coloredBox)),
                onConfirm: ProceedBoxBuy,
                onCancel: () => { });
        }

        private void ProceedBoxBuy()
        {
            buyButton.interactable = false;
            closeButton.interactable = false;

            int cost = MorningEventManager.Instance.GetMysteryBoxCost(currentTier);
            EconomyManager.Instance.EnsureGold(cost,
                onReady: DoBoxBuy,
                onCancel: () =>
                {
                    buyButton.interactable = true;
                    closeButton.interactable = true;
                });
        }

        private string GetTierName(int tier) => tier switch
        {
            0 => L("MysteryBox_TierNormal"),
            1 => L("MysteryBox_TierRare"),
            2 => L("MysteryBox_TierMythic"),
            _ => L("MysteryBox_TierFallback")
        };

        private void DoBoxBuy()
        {
            var result = Controller.OnBoxBuyClicked(currentTier);
            if (!result.success)
            {
                buyButton.interactable = true;
                closeButton.interactable = true;
                ShowPopupMessage(result.message);
                return;
            }

            SoundManager.Instance?.PlaySFX("Buy");

            // 구매 완료~결과 표시 사이(오픈 애니메이션 중)에는 ESC로 패스(취소) 처리되지 않도록 차단
            escapeCancelledAction = () => { };
            PlayOpenAnimation(() => ShowBoxResult(result.rewardType, result.materialIDs, result.weapon, result.message));
        }

        private void OnPassClicked()
        {
            SendButtonClick("pass");
            Controller.ResetMysteryBoxTier();
            Controller.CloseEvent();
        }

        private void PlayOpenAnimation(System.Action onComplete)
        {
            var seq = DOTween.Sequence().SetLink(gameObject);

            // 1) 긴장감 — 좌우로 떨리며 흔들림
            seq.Append(boxImage.transform.DOShakePosition(0.5f, 10f, 18));
            seq.Join(boxImage.transform.DOShakeRotation(0.5f, 12f, 18));

            // 2) 살짝 눌렸다가
            seq.Append(boxImage.transform.DOScale(0.85f, 0.1f).SetEase(Ease.InQuad));

            // 3) 펑 튀어오르며 열림
            seq.Append(boxImage.transform.DOScale(1.3f, 0.35f).SetEase(Ease.OutBack));
            seq.AppendCallback(() =>
            {
                if (boxImage != null)
                    boxImage.sprite = IconManager.Instance.GetMysteryBoxIcon(currentTier, true);
            });

            // 4) glow 3겹 페이드인 + 펄스 (스프라이트 교체 직후 동시에)
            bool firstGlow = true;
            if (glowImages != null)
            {
                foreach (var glow in glowImages)
                {
                    if (glow == null) continue;
                    var fade = glow.DOFade(1f, 0.3f);
                    if (firstGlow) { seq.Append(fade); firstGlow = false; }
                    else seq.Join(fade);
                    seq.Join(glow.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 6, 0.5f));
                }
            }

            seq.AppendInterval(0.3f);
            seq.OnComplete(() => onComplete?.Invoke());
        }

        private void ShowBoxResult(MysteryBoxRewardType rewardType, List<string> materialIDs, WeaponInstance weapon, string message)
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(true);
            MarkResultShown(OnResultCloseClicked);

            // 보상 종류(골드/재료/무기)와 무관하게 결과 공개 시 1회
            SoundManager.Instance?.PlaySFX("GetGift");

            if (resultMessageText != null)
                resultMessageText.text = message;

            ClearResultCards();

            if (rewardType == MysteryBoxRewardType.Material && materialIDs != null && materialIDs.Count > 0)
            {
                // 개별 랜덤 지급이라 서로 다른 재료가 섞일 수 있다.
                // 첫 재료 하나에 xN으로 뭉뚱그리지 않고 종류별로 카드를 만든다
                if (materialCardPrefab != null && resultContainer != null)
                {
                    foreach (var group in materialIDs.GroupBy(id => id))
                    {
                        var matData = DataManager.Instance.GetMaterial(group.Key);
                        if (matData == null) continue;

                        var card = Instantiate(materialCardPrefab, resultContainer);
                        card.Initialize(matData, group.Count());
                        card.OnCardClicked += OnMaterialCardClicked;
                        spawnedMaterialCards.Add(card);
                    }
                }
            }
            else if (rewardType == MysteryBoxRewardType.Weapon && weapon != null)
            {
                if (weaponCardPrefab != null && resultContainer != null)
                {
                    spawnedWeaponCard = Instantiate(weaponCardPrefab, resultContainer);
                    spawnedWeaponCard.Initialize(weapon);
                    spawnedWeaponCard.OnCardClicked += OnWeaponCardClicked;
                }
            }
            // 레이아웃은 GuildEnvoy 결과 패널과 동일하게 HorizontalLayoutGroup + ContentSizeFitter가
            // 다음 프레임에 자동 정렬한다. ForceRebuild는 지연 Destroy된 이전 카드 잔상까지 포함해
            // 정렬을 굳혀버리므로 사용하지 않는다.
        }

        private void ClearResultCards()
        {
            // Destroy는 프레임 끝에 처리되어 같은 프레임의 레이아웃에 잔상으로 남으므로,
            // 즉시 비활성화해 화면/레이아웃에서 바로 제외한다.
            foreach (var card in spawnedMaterialCards)
            {
                if (card == null) continue;
                card.OnCardClicked -= OnMaterialCardClicked;
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            spawnedMaterialCards.Clear();
            if (spawnedWeaponCard != null)
            {
                spawnedWeaponCard.OnCardClicked -= OnWeaponCardClicked;
                spawnedWeaponCard.gameObject.SetActive(false);
                Destroy(spawnedWeaponCard.gameObject);
                spawnedWeaponCard = null;
            }
        }

        private void OnMaterialCardClicked(MaterialInventoryCardItem card)
        {
            SendButtonClick("material_card");
            var popup = UIManager.Instance.GetOrInstantiatePanel<MaterialDetailPopup>();
            UIManager.Instance.OpenPanel<MaterialDetailPopup>();
            popup?.Initialize(card.MaterialData);
        }

        private void OnWeaponCardClicked(WeaponInventoryCardItem card)
        {
            SendButtonClick("weapon_card");
            var popup = UIManager.Instance.GetOrInstantiatePanel<WeaponDetailPopup>();
            UIManager.Instance.OpenPanel<WeaponDetailPopup>();
            popup?.Initialize(card.Weapon);
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
            buyButton?.onClick.RemoveAllListeners();
            resultCloseButton?.onClick.RemoveAllListeners();
            resultOverlay?.onClick.RemoveAllListeners();
            oddsButton?.onClick.RemoveAllListeners();
            oddsCloseButton?.onClick.RemoveAllListeners();
            oddsOverlay?.onClick.RemoveAllListeners();
            ClearResultCards();
        }
    }
}
