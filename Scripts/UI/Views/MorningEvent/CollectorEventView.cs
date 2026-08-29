// Scripts/UI/Views/MorningEvent/CollectorEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class CollectorEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.Collector;
        protected override string OpeningDialogueID => "Collector_Intro";
        protected override string EmptyDialogueID => "Collector_Empty";

        // 수집가에게 팔 수 있는 무기(최소 등급 이상)가 하나도 없으면 진행 불가
        protected override bool HasRequiredResource =>
            MorningEventManager.Instance.GetCollectorEligibleWeapons().Count > 0;

        private CollectorEventController Controller
            => UIControllerManager.Instance.GetController<CollectorEventController>();

        [Header("닫기")]
        [SerializeField] private Button closeButton;

        // ── 무기 목록 ────────────────────────────────────────
        [Header("무기 목록")]
        [SerializeField] private ScrollRect weaponListScrollRect;
        [SerializeField] private Transform weaponListContainer;
        [SerializeField] private WeaponInventoryCardItem weaponCardItemPrefab;

        [Header("타입 필터 토글")]
        [SerializeField] private List<Toggle> weaponTypeFilterToggles;        // 0=전체, 1~N=WeaponType
        [SerializeField] private List<GameObject> weaponTypeFilterOnIndicators;

        [Header("빈 목록 텍스트")]
        [SerializeField] private TextMeshProUGUI listEmptyText;

        // ── 상세 패널 ─────────────────────────────────────────
        [Header("상세 패널")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailWeaponIcon;
        [SerializeField] private Image detailIconBG;
        [SerializeField] private Image detailFrame;
        [SerializeField] private TextMeshProUGUI detailWeaponNameText;
        [SerializeField] private TextMeshProUGUI detailWeaponTypeText;
        [SerializeField] private TextMeshProUGUI detailEnforceLevelText;
        [SerializeField] private Transform detailEffectContainer;
        [SerializeField] private WeaponEffectListItem effectListItemPrefab;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button backButton;

        [Header("오버레이")]
        [SerializeField] private Button detailOverlay;

        // ── 미니게임 (카드 3장 뽑기) ───────────────────────────
        [Header("미니게임 — 카드 3장")]
        [SerializeField] private GameObject minigamePanel;
        [SerializeField] private Button minigameOverlay;                 // 카드 선택 전 클릭 시 취소
        [SerializeField] private List<Button> cardButtons;              // 3장
        [SerializeField] private List<GameObject> cardBackFaces;        // 3장 뒷면 커버 (공개 시 비활성)
        [SerializeField] private List<TextMeshProUGUI> cardResultTexts; // 공개 시 ×N 표시 (초기 비활성)
        [SerializeField] private List<GameObject> cardSelectIndicators; // 선택한 카드 강조 표시 (초기 비활성)

        private WeaponInstance selectedWeapon;
        private int currentTypeFilter = 0;

        private float resultMultiplier;        // 클릭 전 확정된 결과 배수
        private float[] cardMultipliers;        // 클릭 시점에 카드별 표시값 배치
        private bool cardPicked;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            closeButton?.onClick.AddListener(RequestClose);
            sellButton?.onClick.AddListener(OnSellClicked);
            backButton?.onClick.AddListener(OnBackClicked);
            detailOverlay?.onClick.AddListener(OnBackClicked);
            minigameOverlay?.onClick.AddListener(OnMinigameBack);
            SubscribeCardButtons();
            SubscribeTypeFilterToggles();
        }

        public override void OnOpened()
        {
            base.OnOpened();
            currentTypeFilter = 0;
            ResetTypeFilterToggles();
            ShowList();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            closeButton?.onClick.RemoveAllListeners();
            sellButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            detailOverlay?.onClick.RemoveAllListeners();
            minigameOverlay?.onClick.RemoveAllListeners();
            if (cardButtons != null)
                foreach (var b in cardButtons)
                {
                    if (b == null) continue;
                    b.onClick.RemoveAllListeners();
                }
            UnsubscribeTypeFilterToggles();
        }

        #endregion

        #region 목록 뷰

        private void ShowList()
        {
            selectedWeapon = null;
            detailPanel?.SetActive(false);
            minigamePanel?.SetActive(false);
            RefreshWeaponList();
        }

        private void RefreshWeaponList()
        {
            foreach (Transform child in weaponListContainer)
                Destroy(child.gameObject);

            var weapons = GetFilteredSortedWeapons();

            ShowListEmptyText(weapons.Count == 0
                ? L(currentTypeFilter == 0 ? "Inventory_EmptyWeaponAll" : "Inventory_EmptyWeaponFiltered")
                : null);

            foreach (var weapon in weapons)
            {
                var card = Instantiate(weaponCardItemPrefab, weaponListContainer);
                card.Initialize(weapon);
                card.OnCardClicked += OnWeaponCardClicked;
            }

            weaponListScrollRect?.ResetPosition(this);
        }

        private void OnWeaponCardClicked(WeaponInventoryCardItem card)
        {
            SendButtonClick("weapon_card");
            OpenDetail(card.Weapon);
        }

        private List<WeaponInstance> GetFilteredSortedWeapons()
        {
            var weapons = MorningEventManager.Instance.GetCollectorEligibleWeapons();

            if (currentTypeFilter > 0)
            {
                WeaponType target = (WeaponType)(currentTypeFilter - 1);
                weapons = weapons.Where(w => w.weaponData.weaponType == target).ToList();
            }

            weapons = weapons.OrderByDescending(w => w.currentGrade).ToList();

            return weapons;
        }

        private void OnTypeFilterChanged(int index)
        {
            currentTypeFilter = index;
            RefreshWeaponList();
        }

        private void SubscribeTypeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;
            for (int i = 0; i < weaponTypeFilterToggles.Count; i++)
            {
                int index = i;
                weaponTypeFilterToggles[i]?.onValueChanged.AddListener(on => { if (on) OnTypeFilterChanged(index); });
                weaponTypeFilterToggles[i]?.onValueChanged.AddListener(on => SetTypeFilterIndicator(index, on));
            }
        }

        private void UnsubscribeTypeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;
            foreach (var toggle in weaponTypeFilterToggles)
                toggle?.onValueChanged.RemoveAllListeners();
        }

        private void ResetTypeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;
            for (int i = 0; i < weaponTypeFilterToggles.Count; i++)
            {
                bool on = (i == 0);
                weaponTypeFilterToggles[i]?.SetIsOnWithoutNotify(on);
                SetTypeFilterIndicator(i, on);
            }
        }

        private void SetTypeFilterIndicator(int index, bool on)
        {
            if (weaponTypeFilterOnIndicators != null && index < weaponTypeFilterOnIndicators.Count)
                weaponTypeFilterOnIndicators[index]?.SetActive(on);
        }

        // 공용 빈 목록 텍스트. message가 null이면 숨기고, 값이 있으면 해당 문구로 표시한다.
        private void ShowListEmptyText(string message)
        {
            if (listEmptyText == null) return;

            if (string.IsNullOrEmpty(message))
            {
                listEmptyText.gameObject.SetActive(false);
                return;
            }

            listEmptyText.text = message;
            listEmptyText.gameObject.SetActive(true);
        }

        #endregion

        #region 상세 뷰

        private void OpenDetail(WeaponInstance weapon)
        {
            selectedWeapon = weapon;
            detailPanel?.SetActive(true);
            SetSubPanelEscape(true, OnBackClicked);
            UpdateDetailUI();
        }

        private void UpdateDetailUI()
        {
            if (selectedWeapon == null) return;

            var data = selectedWeapon.weaponData;
            Grade grade = selectedWeapon.currentGrade;

            if (detailWeaponIcon != null && data?.icon != null)
                detailWeaponIcon.sprite = data.icon;
            if (detailIconBG != null)
                detailIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);
            if (detailFrame != null)
                detailFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);
            if (detailWeaponNameText != null)
            {
                string enforce = selectedWeapon.enforceLevel > 0 ? $" +{selectedWeapon.enforceLevel}" : "";
                detailWeaponNameText.text = (data?.DisplayName ?? "-") + enforce;
                detailWeaponNameText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }
            if (detailWeaponTypeText != null)
                detailWeaponTypeText.text = UITranslator.GetString(data?.weaponType ?? WeaponType.Sword);
            if (detailEnforceLevelText != null)
                detailEnforceLevelText.text = $"+{selectedWeapon.enforceLevel}";

            WeaponEffectListItem.Rebuild(detailEffectContainer, effectListItemPrefab, selectedWeapon.effects);
        }

        private void OnBackClicked()
        {
            SendButtonClick("back");
            detailPanel?.SetActive(false);
            selectedWeapon = null;
            SetSubPanelEscape(false);
        }

        #endregion

        #region 판매 실행

        private void OnSellClicked()
        {
            SendButtonClick("sell");
            if (selectedWeapon == null) return;

            var data = selectedWeapon.weaponData;
            int basePrice = data?.basePrice ?? 0;
            string enforce = selectedWeapon.enforceLevel > 0 ? $" +{selectedWeapon.enforceLevel}" : "";
            string coloredName = Colored((data?.DisplayName ?? "-") + enforce, ColorManager.Instance.GetGradeAccentColor(selectedWeapon.currentGrade));

            UIPopupController.Instance?.ShowPopup(
                L("Collector_SellConfirm", ("name", coloredName), ("base", basePrice.ToString("N0")),
                    ("min", (basePrice * 3).ToString("N0")), ("max", (basePrice * 5).ToString("N0"))),
                onConfirm: OpenMinigame,
                onCancel: () => { }
            );
        }

        #endregion

        #region 미니게임 (카드 3장 뽑기)

        private void OpenMinigame()
        {
            if (selectedWeapon == null) return;

            cardPicked = false;
            resultMultiplier = Controller.OnRollMultiplier();
            cardMultipliers = null;

            ResetCards();
            minigamePanel?.SetActive(true);
            SetSubPanelEscape(true, OnMinigameBack);
        }

        private void SubscribeCardButtons()
        {
            if (cardButtons == null) return;
            for (int i = 0; i < cardButtons.Count; i++)
            {
                int index = i;
                cardButtons[i]?.onClick.AddListener(() => OnCardClicked(index));
            }
        }

        private void ResetCards()
        {
            if (cardButtons == null) return;
            for (int i = 0; i < cardButtons.Count; i++)
            {
                if (cardButtons[i] != null)
                {
                    cardButtons[i].interactable = true;
                    cardButtons[i].transform.DOKill();
                    cardButtons[i].transform.localScale = Vector3.one;
                }
                if (cardBackFaces != null && i < cardBackFaces.Count)
                    cardBackFaces[i]?.SetActive(true);
                if (cardResultTexts != null && i < cardResultTexts.Count && cardResultTexts[i] != null)
                {
                    cardResultTexts[i].text = "";
                    cardResultTexts[i].gameObject.SetActive(false);
                }
                if (cardSelectIndicators != null && i < cardSelectIndicators.Count)
                    cardSelectIndicators[i]?.SetActive(false);
            }
        }

        private void OnCardClicked(int index)
        {
            SendButtonClick("minigame_card");
            if (cardPicked || cardButtons == null || index >= cardButtons.Count) return;
            cardPicked = true;

            SetCardSelectIndicator(index);

            foreach (var b in cardButtons)
                if (b != null) b.interactable = false;

            // 클릭한 칸 = 확정 결과, 나머지 칸 = {3,4,5}\{결과} 셔플 배치
            cardMultipliers = BuildCardLayout(index);

            // 선택 카드 먼저 공개 → 나머지 공개 → 결과 처리
            var seq = DOTween.Sequence().SetLink(gameObject);
            seq.AppendCallback(() => FlipCard(index));
            seq.AppendInterval(0.3f);
            for (int i = 0; i < cardButtons.Count; i++)
            {
                if (i == index) continue;
                int captured = i;
                seq.AppendCallback(() => FlipCard(captured));
            }
            seq.AppendInterval(0.6f);
            seq.OnComplete(() => ExecuteSell(resultMultiplier));
        }

        /// <summary>클릭한 칸에 결과 배수를, 나머지 칸에 남은 배수를 무작위로 채운 배열을 만든다.</summary>
        private float[] BuildCardLayout(int chosenIndex)
        {
            int count = cardButtons.Count;
            var layout = new float[count];

            // 전체 배수 풀 {3, 4, ..., 3+count-1} 에서 결과를 뺀 나머지
            var remaining = new List<float>();
            for (int m = 3; m < 3 + count; m++)
                if (m != (int)resultMultiplier) remaining.Add(m);

            for (int i = remaining.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (remaining[i], remaining[j]) = (remaining[j], remaining[i]);
            }

            int r = 0;
            for (int i = 0; i < count; i++)
                layout[i] = (i == chosenIndex) ? resultMultiplier : remaining[r++];

            return layout;
        }

        private void SetCardSelectIndicator(int selectedIndex)
        {
            if (cardSelectIndicators == null) return;
            for (int i = 0; i < cardSelectIndicators.Count; i++)
                cardSelectIndicators[i]?.SetActive(i == selectedIndex);
        }

        private void FlipCard(int index)
        {
            if (cardButtons == null || index >= cardButtons.Count || cardButtons[index] == null) return;
            if (cardMultipliers == null || index >= cardMultipliers.Length) return;

            var t = cardButtons[index].transform;
            t.DOKill();
            t.DOScaleX(0f, 0.1f).SetLink(gameObject).OnComplete(() =>
            {
                if (cardBackFaces != null && index < cardBackFaces.Count)
                    cardBackFaces[index]?.SetActive(false);
                if (cardResultTexts != null && index < cardResultTexts.Count && cardResultTexts[index] != null)
                {
                    cardResultTexts[index].text = $"{(int)cardMultipliers[index]}";
                    cardResultTexts[index].gameObject.SetActive(true);
                }
                t.DOScaleX(1f, 0.1f).SetLink(gameObject);
            });
        }

        private void OnMinigameBack()
        {
            SendButtonClick("minigame_back");
            if (cardPicked) return;   // 카드를 뽑은 뒤에는 취소 불가
            minigamePanel?.SetActive(false);
            SetSubPanelEscape(true, OnBackClicked);   // 상세 패널로 복귀
        }

        private void ExecuteSell(float multiplier)
        {
            var (success, _, _, message) = Controller.OnSellConfirmed(selectedWeapon, multiplier);
            if (!success) { ShowPopupMessage(message); return; }

            MorningEventManager.Instance?.MarkEventCompleted();
            // 결과 표시 후 확인 시 패널을 닫고 판매 배율(×3/4/5)에 따른 대화를 출력한다.
            string id = $"Collector_Mult{(int)multiplier}";
            UIPopupController.Instance?.ShowPopup(
                message,
                onConfirm: () => PlayClosing(id),
                onCancel: null,
                type: PopupSfxType.Notify);
        }

        #endregion
    }
}
