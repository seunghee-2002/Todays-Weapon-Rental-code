using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 진행/완료 모험을 카드 캐러셀로 표시
    /// </summary>
    public class AdventureProgressView : BaseView
    {
        [Header("Header")]
        [SerializeField] private Button closeButton;

        [Header("Content State")]
        [SerializeField] private GameObject carouselPanel;       // 모험이 있을 때만 활성 (카드+화살표+네비)
        [SerializeField] private TextMeshProUGUI noAdventureTxt;
        [SerializeField] private TextMeshProUGUI indexIndicatorText;  // "현재 / 전체" (예: 3 / 5)
        [SerializeField] private GameObject completedCountObject;
        [SerializeField] private TextMeshProUGUI completedCountText;  // "완료 N" (완료 0개면 숨김)

        [Header("Card Carousel")]
        [SerializeField] private Transform cardContainer;        // 카드 슬롯 부모
        [SerializeField] private GameObject cardPrefab;          // AdventureProgressCard 프리팹
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [Header("Coverflow Layout")]
        [SerializeField] private float sideX = 420f;             // ±1 카드 X 오프셋
        [SerializeField] private float farX = 960f;              // ±2 카드 X 오프셋 (화면 밖)
        [SerializeField] private float sideScale = 0.8f;
        [SerializeField] private float farScale = 0.6f;
        [SerializeField] private float sideAlpha = 0.4f;
        [SerializeField] private float slideDuration = 0.3f;

        [Header("Bottom Navigator")]
        [SerializeField] private ScrollRect navScrollRect;
        [SerializeField] private Transform navContainer;
        [SerializeField] private GameObject navItemPrefab;       // 확장된 ProgressNavButton 프리팹

        [Header("Controller")]
        [SerializeField] private AdventureProgressController Controller;

        private const int SlotCount = 5;
        private const int CenterSlot = 2;   // offset 0 슬롯

        private readonly List<AdventureProgressCard> cards = new List<AdventureProgressCard>();
        private readonly int[] cardOffsets = new int[SlotCount];
        private readonly List<ProgressNavButton> navPool = new List<ProgressNavButton>();
        private readonly Dictionary<string, ProgressNavButton> navButtons = new Dictionary<string, ProgressNavButton>();
        private List<AdventureInstance> sortedAdventures = new List<AdventureInstance>();
        private int selectedIndex = 0;
        private bool isAnimating = false;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;
            isCanClickOverlay = false;
            canEscape = true;
            hideUIOnOpen = false;     // TopBar는 유지하고
            minimalUIOnOpen = true;   // 나머지 바/디버그 버튼만 숨긴다 (minimal 모드)
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
            prevButton?.onClick.AddListener(OnPrevClicked);
            nextButton?.onClick.AddListener(OnNextClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            prevButton?.onClick.RemoveAllListeners();
            nextButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region View로부터 호출되는 메서드 (Controller 진입점)

        /// <summary>
        /// 정렬된 모험 리스트로 캐러셀을 구성하고 focusIndex 카드를 중앙에 둔다. (즉시, 애니메이션 없음)
        /// </summary>
        public void BuildCarousel(List<AdventureInstance> sorted, int focusIndex)
        {
            sortedAdventures = sorted ?? new List<AdventureInstance>();

            bool hasAdventures = sortedAdventures.Count > 0;
            carouselPanel?.SetActive(hasAdventures);
            if (noAdventureTxt != null) noAdventureTxt.gameObject.SetActive(!hasAdventures);

            EnsureCards();
            BuildNavigator();
            UpdateCompletedCount();

            if (!hasAdventures)
            {
                foreach (var card in cards) card.Clear();
                UpdateIndexIndicator();
                return;
            }

            selectedIndex = Mathf.Clamp(focusIndex, 0, sortedAdventures.Count - 1);
            JumpToSelected();
        }

        /// <summary>네비게이터 스크롤 위치 보정 (레이아웃 확정 후 호출).</summary>
        public void ResetNavScroll() => navScrollRect?.ResetPosition(this);

        /// <summary>instanceID에 해당하는 카드가 현재 렌더링(5슬롯)되어 있으면 반환, 아니면 null.</summary>
        private AdventureProgressCard GetCard(string instanceID)
        {
            if (string.IsNullOrEmpty(instanceID)) return null;
            foreach (var card in cards)
                if (card.BoundAdventure != null && card.InstanceID == instanceID)
                    return card;
            return null;
        }

        /// <summary>해당 모험 카드의 현재 이벤트 표시 갱신 (화면 밖이면 무시).</summary>
        public void UpdateCardEvent(AdventureInstance adventure)
        {
            GetCard(adventure?.instanceID)?.UpdateCurrentEventDisplay();
        }

        /// <summary>모험 완료 시 카드 + 네비게이터에 완료 표시.</summary>
        public void ShowCardCompleted(AdventureInstance adventure)
        {
            if (adventure == null) return;
            GetCard(adventure.instanceID)?.ShowCompleted();
            if (navButtons.TryGetValue(adventure.instanceID, out var navBtn))
                navBtn.SetCompleted(true);

            UpdateCompletedCount();
        }

        /// <summary>완료된 모험 개수 표시 갱신 (0개면 숨김).</summary>
        private void UpdateCompletedCount()
        {
            if (completedCountText == null || completedCountObject == null) return;

            int completed = 0;
            foreach (var adventure in sortedAdventures)
                if (adventure != null && adventure.isCompleted) completed++;

            completedCountObject.SetActive(completed > 0);
            completedCountText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", "AdventureProgress_Completed",
                arguments: new object[] { new Dictionary<string, object> { { "count", completed } } });
        }

        #endregion

        #region 카드 캐러셀

        private void EnsureCards()
        {
            for (int i = cards.Count; i < SlotCount; i++)
            {
                var obj = Instantiate(cardPrefab, cardContainer);
                var card = obj.GetComponentOrNull<AdventureProgressCard>();
                if (card == null)
                {
                    Log.Error("[AdventureProgressView] cardPrefab에 AdventureProgressCard가 없습니다.");
                    Destroy(obj);
                    return;
                }
                cards.Add(card);
            }
        }

        /// <summary>현재 selectedIndex 기준으로 5개 슬롯을 즉시 재배치 (애니메이션 없음).</summary>
        private void JumpToSelected()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                cardOffsets[i] = i - CenterSlot;
                BindCardByOffset(i);
                ApplySlot(i, instant: true);
            }
            UpdateArrowButtons();
            UpdateNavSelection();
        }

        /// <summary>현재 selectedIndex로 5슬롯을 진입 방향(dir)에서 한 번에 슬라이드 인. dir: +1=다음 쪽, -1=이전 쪽.</summary>
        private void JumpToSelectedAnimated(int dir)
        {
            isAnimating = true;

            for (int i = 0; i < SlotCount; i++)
            {
                cardOffsets[i] = i - CenterSlot;
                BindCardByOffset(i);
                ApplySlotTo(i, cardOffsets[i] + dir, instant: true);  // 진입 방향으로 한 칸 밀어 즉시 배치
                ApplySlot(i, instant: false);                         // 실제 위치로 슬라이드
            }

            UpdateArrowButtons();
            UpdateNavSelection();

            DOVirtual.DelayedCall(slideDuration, () => isAnimating = false).SetLink(gameObject);
        }

        /// <summary>한 칸 이동 (DOTween 슬라이드). dir: +1=다음, -1=이전.</summary>
        private void Step(int dir)
        {
            if (isAnimating || sortedAdventures.Count == 0) return;
            int target = selectedIndex + dir;
            if (target < 0 || target >= sortedAdventures.Count) return;

            isAnimating = true;
            selectedIndex = target;

            for (int i = 0; i < SlotCount; i++)
            {
                cardOffsets[i] -= dir;

                bool recycled = false;
                if (cardOffsets[i] < -CenterSlot) { cardOffsets[i] += SlotCount; recycled = true; }
                else if (cardOffsets[i] > CenterSlot) { cardOffsets[i] -= SlotCount; recycled = true; }

                if (recycled)
                {
                    // 반대편 끝으로 점프한 카드: 새 데이터 바인딩 + 화면 밖에 즉시 배치 (안 보이므로 팝 없음)
                    BindCardByOffset(i);
                    ApplySlot(i, instant: true);
                }
                else
                {
                    // 데이터는 그대로(offset만 이동) → 위치만 슬라이드
                    ApplySlot(i, instant: false);
                }
            }

            UpdateArrowButtons();
            UpdateNavSelection();

            DOVirtual.DelayedCall(slideDuration, () => isAnimating = false).SetLink(gameObject);
        }

        private void BindCardByOffset(int slot)
        {
            int dataIndex = selectedIndex + cardOffsets[slot];
            if (dataIndex >= 0 && dataIndex < sortedAdventures.Count)
                cards[slot].Bind(sortedAdventures[dataIndex]);
            else
                cards[slot].Clear();
        }

        private void ApplySlot(int slot, bool instant) => ApplySlotTo(slot, cardOffsets[slot], instant);

        /// <summary>슬롯을 visualOffset 위치/스케일/알파로 배치. 상호작용·정렬은 항상 실제 offset(cardOffsets) 기준.</summary>
        private void ApplySlotTo(int slot, int visualOffset, bool instant)
        {
            var card = cards[slot];
            if (card.BoundAdventure == null) return;   // 빈 슬롯(비활성)은 위치 갱신 불필요

            GetCoverflow(visualOffset, out float posX, out float scale, out float alpha);

            var rt = card.RectTransform;
            var cg = card.CanvasGroup;

            DOTween.Kill(rt);
            if (cg != null) DOTween.Kill(cg);

            if (instant)
            {
                rt.anchoredPosition = new Vector2(posX, 0f);
                rt.localScale = Vector3.one * scale;
                if (cg != null) cg.alpha = alpha;
            }
            else
            {
                rt.DOAnchorPos(new Vector2(posX, 0f), slideDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
                rt.DOScale(scale, slideDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
                if (cg != null) cg.DOFade(alpha, slideDuration).SetLink(gameObject);
            }

            int realOffset = cardOffsets[slot];
            if (cg != null) cg.blocksRaycasts = (realOffset == 0);   // 중앙 카드만 상호작용
            card.SetStageActive(realOffset == 0);                    // 중앙 카드만 연출 활성
            if (realOffset == 0) card.transform.SetAsLastSibling();  // 중앙 카드를 맨 앞으로
        }

        private void GetCoverflow(int offset, out float posX, out float scale, out float alpha)
        {
            int abs = Mathf.Abs(offset);
            int sign = offset == 0 ? 0 : (offset > 0 ? 1 : -1);

            if (abs == 0)      { posX = 0f;           scale = 1f;        alpha = 1f; }
            else if (abs == 1) { posX = sign * sideX; scale = sideScale; alpha = sideAlpha; }
            else               { posX = sign * farX;  scale = farScale;  alpha = 0f; }  // ±2: 화면 밖, 미리 렌더링만
        }

        private void UpdateArrowButtons()
        {
            int count = sortedAdventures.Count;
            prevButton?.gameObject.SetActive(count > 0 && selectedIndex > 0);
            nextButton?.gameObject.SetActive(count > 0 && selectedIndex < count - 1);
        }

        #endregion

        #region 하단 네비게이터

        private void BuildNavigator()
        {
            navButtons.Clear();

            for (int i = 0; i < sortedAdventures.Count; i++)
            {
                var navBtn = GetNavButton(i);
                if (navBtn == null) continue;

                navBtn.gameObject.SetActive(true);
                navBtn.Initialize(sortedAdventures[i], OnNavClicked);
                navButtons[sortedAdventures[i].instanceID] = navBtn;
            }

            for (int i = sortedAdventures.Count; i < navPool.Count; i++)
                navPool[i].gameObject.SetActive(false);

            navScrollRect?.ResetPosition(this);
        }

        /// <summary>네비 버튼은 파괴/재생성하지 않고 풀에서 재사용한다 (버튼 1개가 Spine 캐릭터 1개라 생성 비용이 크다).
        /// 풀 인덱스가 곧 자식 순서이므로 별도 정렬은 필요 없다.</summary>
        private ProgressNavButton GetNavButton(int index)
        {
            if (index < navPool.Count) return navPool[index];

            var obj = Instantiate(navItemPrefab, navContainer);
            var navBtn = obj.GetComponentOrNull<ProgressNavButton>();
            if (navBtn == null)
            {
                Log.Error("[AdventureProgressView] navItemPrefab에 ProgressNavButton이 없습니다.");
                Destroy(obj);
                return null;
            }

            navPool.Add(navBtn);
            return navBtn;
        }

        private void UpdateNavSelection()
        {
            UpdateIndexIndicator();
            if (sortedAdventures.Count == 0) return;
            string selectedID = sortedAdventures[selectedIndex].instanceID;
            foreach (var kvp in navButtons)
                kvp.Value.SetSelected(kvp.Key == selectedID);

            ScrollNavToSelected(selectedID);
        }

        /// <summary>"현재 / 전체" 인덱스 표시 갱신 (모험 0개면 숨김).</summary>
        private void UpdateIndexIndicator()
        {
            if (indexIndicatorText == null) return;

            int total = sortedAdventures.Count;
            if (total == 0)
            {
                indexIndicatorText.gameObject.SetActive(false);
                return;
            }

            indexIndicatorText.gameObject.SetActive(true);
            indexIndicatorText.text = $"{selectedIndex + 1} / {total}";
        }

        /// <summary>선택된 네비 버튼이 뷰포트 중앙에 오도록 가로 스크롤을 슬라이드.</summary>
        private void ScrollNavToSelected(string selectedID)
        {
            if (navScrollRect == null) return;
            if (!navButtons.TryGetValue(selectedID, out var navBtn) || navBtn == null) return;

            var content = navScrollRect.content;
            if (content == null) return;
            var viewport = navScrollRect.viewport != null ? navScrollRect.viewport : (RectTransform)navScrollRect.transform;

            Canvas.ForceUpdateCanvases();

            float viewportW = viewport.rect.width;
            float scrollable = content.rect.width - viewportW;
            if (scrollable <= 0f) return;   // 스크롤 불필요(내용이 뷰포트보다 작음)

            var target = (RectTransform)navBtn.transform;
            Vector3 targetWorld = target.TransformPoint(target.rect.center);
            float targetXInContent = content.InverseTransformPoint(targetWorld).x - content.rect.xMin;

            float normalized = Mathf.Clamp01((targetXInContent - viewportW * 0.5f) / scrollable);

            DOTween.Kill(navScrollRect);
            DOTween.To(() => navScrollRect.horizontalNormalizedPosition,
                       x => navScrollRect.horizontalNormalizedPosition = x,
                       normalized, slideDuration)
                   .SetEase(Ease.OutCubic)
                   .SetTarget(navScrollRect)
                   .SetLink(gameObject);
        }

        #endregion

        #region 이동 (네비/화살표)

        /// <summary>해당 인덱스로 이동. 한 칸이면 슬라이드, 그 외엔 즉시 점프.</summary>
        private void MoveToIndex(int targetIndex)
        {
            if (isAnimating || sortedAdventures.Count == 0) return;
            targetIndex = Mathf.Clamp(targetIndex, 0, sortedAdventures.Count - 1);
            int delta = targetIndex - selectedIndex;
            if (delta == 0) return;

            if (Mathf.Abs(delta) == 1)
            {
                Step(delta);
            }
            else
            {
                selectedIndex = targetIndex;
                JumpToSelectedAnimated(delta > 0 ? 1 : -1);
            }
        }

        private void OnNavClicked(AdventureInstance adventure)
        {
            if (adventure == null) return;
            int idx = sortedAdventures.FindIndex(a => a.instanceID == adventure.instanceID);
            if (idx >= 0) MoveToIndex(idx);
        }

        private void OnPrevClicked() => Step(-1);
        private void OnNextClicked() => Step(+1);

        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            Controller?.OnCloseClicked();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
