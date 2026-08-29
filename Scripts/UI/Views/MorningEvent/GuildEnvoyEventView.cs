// Scripts/UI/Views/MorningEvent/GuildEnvoyEventView.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 길드 사절단 패널.
    /// 오프닝 대화 종료 후 즉시 실행하고 결과를 표시한다 (메인 패널 없음).
    /// </summary>
    public class GuildEnvoyEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.GuildEnvoy;
        protected override string OpeningDialogueID => "GuildEnvoy_Intro";

        private GuildEnvoyEventController Controller
            => UIControllerManager.Instance.GetController<GuildEnvoyEventController>();

        [Header("닫기")]
        [SerializeField] private Button resultCloseButton;

        [Header("결과 패널 텍스트")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private TextMeshProUGUI goldResultText;

        [Header("재료 섹션 (선물 시만 표시)")]
        [SerializeField] private GameObject materialSection;
        [SerializeField] private Transform materialContainer;
        [SerializeField] private MaterialInventoryCardItem materialCardPrefab;

        private readonly List<MaterialInventoryCardItem> spawnedCards = new();

        private bool lastIsGift;   // 결과 대화 분기용 (선물/강제납부)

        protected override void Awake()
        {
            base.Awake();
            resultCloseButton?.onClick.AddListener(OnResultCloseClicked);
        }

        public override void OnOpened()
        {
            base.OnOpened();
            var (isGift, goldAmount, materialIDs, _) = Controller.OnGuildEnvoyExecute();
            lastIsGift = isGift;
            PopulateResult(isGift, goldAmount, materialIDs);

            // 강제 납부(isGift == false)는 뺏기는 쪽이라 축하음을 울리지 않는다.
            if (isGift)
                SoundManager.Instance?.PlaySFX("GetGift");

            // 단일 결과 패널이므로 결과 표시 처리로 완료 + ESC/오버레이 닫기 동작을 설정한다.
            MarkResultShown(OnResultCloseClicked);
        }

        private void PopulateResult(bool isGift, int goldAmount, List<string> materialIDs)
        {
            if (titleText != null)
                titleText.text = L(isGift ? "GuildEnvoy_GiftTitle" : "GuildEnvoy_TaxTitle");

            if (descText != null)
            {
                // 튜토리얼(1일차)은 평판이 아직 없으므로 부임 선물로 안내한다.
                bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
                descText.text = isGift
                    ? L(isTutorial ? "GuildEnvoy_GiftDescTutorial" : "GuildEnvoy_GiftDesc")
                    : L("GuildEnvoy_TaxDesc");
            }

            if (goldResultText != null)
                goldResultText.text = isGift ? $"+{goldAmount:N0}G" : $"{goldAmount:N0}G";

            materialSection?.SetActive(isGift && materialIDs != null && materialIDs.Count > 0);

            foreach (var card in spawnedCards)
                if (card != null) Destroy(card.gameObject);
            spawnedCards.Clear();

            if (!isGift || materialIDs == null || materialContainer == null || materialCardPrefab == null)
                return;

            // 동일 재료는 한 장으로 합쳐 수량(xN)으로 표시
            foreach (var group in materialIDs.GroupBy(id => id))
            {
                var matData = DataManager.Instance.GetMaterial(group.Key);
                if (matData == null) continue;

                var card = Instantiate(materialCardPrefab, materialContainer);
                card.Initialize(matData, group.Count());
                card.OnCardClicked += OnMaterialCardClicked;
                spawnedCards.Add(card);
            }
        }

        #region 이벤트 핸들러

        private void OnResultCloseClicked()
        {
            SendButtonClick("result_close");

            // 패널을 닫은 뒤 결과(선물/강제납부)에 따른 대화를 출력한다.
            string id = lastIsGift ? "GuildEnvoy_Gift" : "GuildEnvoy_Force";
            PlayClosing(id);
        }

        private void OnMaterialCardClicked(MaterialInventoryCardItem card)
        {
            SendButtonClick("material_card");
            var popup = UIManager.Instance.GetOrInstantiatePanel<MaterialDetailPopup>();
            UIManager.Instance.OpenPanel<MaterialDetailPopup>();
            popup?.Initialize(card.MaterialData);
        }

        #endregion

        protected override void OnDestroy()
        {
            base.OnDestroy();
            resultCloseButton?.onClick.RemoveAllListeners();
            foreach (var card in spawnedCards)
                if (card != null) card.OnCardClicked -= OnMaterialCardClicked;
        }
    }
}
