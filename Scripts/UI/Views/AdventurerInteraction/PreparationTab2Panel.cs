using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 준비 Tab 2 — 지원(무기 대여 + 아이템 배정).
    /// </summary>
    public class PreparationTab2Panel : MonoBehaviour
    {
        [Header("서브탭")]
        [SerializeField] private Toggle weaponSubTabToggle;
        [SerializeField] private Toggle activeItemSubTabToggle;
        [SerializeField] private GameObject weaponSubPanel;
        [SerializeField] private GameObject activeItemSubPanel;

        [Header("서브탭 포커스 — 무기")]
        [SerializeField] private GameObject subTabFocusWeapon;
        [SerializeField] private TextMeshProUGUI subTabTextWeapon;
        [SerializeField] private Image subTabIconWeapon;

        [Header("서브탭 포커스 — 아이템")]
        [SerializeField] private GameObject subTabFocusActiveItem;
        [SerializeField] private TextMeshProUGUI subTabTextActiveItem;
        [SerializeField] private Image subTabIconActiveItem;

        [Header("무기 목록")]
        [SerializeField] private List<Toggle> weaponTypeFilterToggles;
        [SerializeField] private List<GameObject> weaponTypeFilterOnIndicators;
        [SerializeField] private ScrollRect weaponScrollRect;
        [SerializeField] private Transform weaponGridContainer;
        [SerializeField] private GameObject weaponCardPrefab;

        [Header("아이템 목록")]
        [SerializeField] private List<Toggle> activeItemTypeFilterToggles;
        [SerializeField] private List<GameObject> activeItemTypeFilterOnIndicators;
        [SerializeField] private ScrollRect activeItemScrollRect;
        [SerializeField] private Transform activeItemListContainer;
        [SerializeField] private GameObject activeItemCardPrefab;

        [Header("빈 목록 텍스트 (무기/아이템 공유)")]
        [SerializeField] private TextMeshProUGUI listEmptyText;

        private AdventurePreparationController controller;

        private List<WeaponInventoryCardItem> weaponCards = new List<WeaponInventoryCardItem>();
        private List<ActiveItemInventoryCard> activeItemCards = new List<ActiveItemInventoryCard>();

        private WeaponInventoryCardItem selectedWeaponCard;
        private ActiveItemInventoryCard selectedActiveItemCard;

        #region 초기화

        public void Initialize(AdventurePreparationController controller)
        {
            this.controller = controller;

            weaponSubTabToggle?.onValueChanged.RemoveAllListeners();
            activeItemSubTabToggle?.onValueChanged.RemoveAllListeners();
            UnsubscribeFilterToggles(weaponTypeFilterToggles);
            UnsubscribeFilterToggles(activeItemTypeFilterToggles);

            weaponSubTabToggle?.onValueChanged.AddListener(on =>
            {
                weaponSubPanel?.SetActive(on);
                ApplySubTabFocus(on, subTabFocusWeapon, subTabTextWeapon, subTabIconWeapon);
                if (on) this.controller?.OnWeaponSubTabSelected();
            });
            activeItemSubTabToggle?.onValueChanged.AddListener(on =>
            {
                activeItemSubPanel?.SetActive(on);
                ApplySubTabFocus(on, subTabFocusActiveItem, subTabTextActiveItem, subTabIconActiveItem);
                if (on) this.controller?.OnActiveItemSubTabSelected();
            });

            SubscribeFilterToggles(weaponTypeFilterToggles, weaponTypeFilterOnIndicators, i => this.controller?.OnWeaponTypeFilterChanged(i));
            SubscribeFilterToggles(activeItemTypeFilterToggles, activeItemTypeFilterOnIndicators, i => this.controller?.OnActiveItemTypeFilterChanged(i));

            SyncFilterIndicators(weaponTypeFilterToggles, weaponTypeFilterOnIndicators);
            SyncFilterIndicators(activeItemTypeFilterToggles, activeItemTypeFilterOnIndicators);
        }

        /// <summary>
        /// 무기 타입 필터를 [0](전체)으로 되돌린다. 토글 변경 이벤트가 컨트롤러 필터/그리드를 함께 갱신한다.
        /// </summary>
        public void ResetWeaponTypeFilter()
        {
            if (weaponTypeFilterToggles == null || weaponTypeFilterToggles.Count == 0) return;
            if (weaponTypeFilterToggles[0] != null) weaponTypeFilterToggles[0].isOn = true;
        }

        public void SelectWeaponSubTab()
        {
            if (weaponSubTabToggle != null) weaponSubTabToggle.isOn = true;
        }

        public void SelectActiveItemSubTab()
        {
            if (activeItemSubTabToggle != null) activeItemSubTabToggle.isOn = true;
        }

        public void RefreshSubTabFocus()
        {
            bool weaponOn = weaponSubTabToggle != null && weaponSubTabToggle.isOn;
            ApplySubTabFocus(weaponOn,   subTabFocusWeapon,     subTabTextWeapon,     subTabIconWeapon);
            ApplySubTabFocus(!weaponOn,  subTabFocusActiveItem, subTabTextActiveItem, subTabIconActiveItem);
        }

        private void ApplySubTabFocus(bool on, GameObject focus, TextMeshProUGUI text, Image icon)
        {
            focus?.SetActive(on);
            Color c = ColorManager.Instance.GetSubTabColor(on);
            if (text != null) text.color = c;
            if (icon != null) icon.color = c;
        }

        private void SubscribeFilterToggles(List<Toggle> toggles, List<GameObject> onIndicators, System.Action<int> onSelected)
        {
            if (toggles == null) return;
            for (int i = 0; i < toggles.Count; i++)
            {
                int index = i;
                toggles[i]?.onValueChanged.AddListener(on => { if (on) onSelected(index); });
                toggles[i]?.onValueChanged.AddListener(isOn => OnFilterToggleChanged(isOn, onIndicators?[index]));
            }
        }

        private void UnsubscribeFilterToggles(List<Toggle> toggles)
        {
            if (toggles == null) return;
            foreach (var t in toggles) t?.onValueChanged.RemoveAllListeners();
        }

        private void OnFilterToggleChanged(bool isOn, GameObject onIndicator)
        {
            onIndicator?.SetActive(isOn);
        }

        // indicator를 현재 토글 상태에 맞춰 초기 동기화한다.
        private void SyncFilterIndicators(List<Toggle> toggles, List<GameObject> onIndicators)
        {
            if (toggles == null || onIndicators == null) return;
            for (int i = 0; i < toggles.Count && i < onIndicators.Count; i++)
                onIndicators[i]?.SetActive(toggles[i] != null && toggles[i].isOn);
        }

        #endregion

        #region 무기 목록

        public void UpdateWeaponGrid(List<WeaponInstance> weapons)
        {
            ClearWeaponCards();

            if (weapons == null || weapons.Count == 0)
            {
                bool allTabSelected = weaponTypeFilterToggles != null
                    && weaponTypeFilterToggles.Count > 0
                    && weaponTypeFilterToggles[0].isOn;
                UpdateSharedEmptyText(weaponSubPanel, LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", allTabSelected ? "Inventory_EmptyWeaponAll" : "Inventory_EmptyWeaponFiltered"));
                weaponScrollRect.ResetPosition(this);
                return;
            }

            UpdateSharedEmptyText(weaponSubPanel, null);

            foreach (var weapon in weapons)
            {
                var cardObj = Instantiate(weaponCardPrefab, weaponGridContainer);
                var card = cardObj.GetComponentOrNull<WeaponInventoryCardItem>();
                if (card != null)
                {
                    card.Initialize(weapon);
                    card.OnCardClicked += OnWeaponCardClicked;
                    weaponCards.Add(card);
                }
            }

            weaponScrollRect.ResetPosition(this);
        }

        private void OnWeaponCardClicked(WeaponInventoryCardItem card)
        {
            if (selectedWeaponCard != null) selectedWeaponCard.SetSelected(false);
            selectedWeaponCard = card;
            selectedWeaponCard.SetSelected(true);
            controller?.OnWeaponCardClicked(card.Weapon);
        }

        public void ClearWeaponCardSelection()
        {
            if (selectedWeaponCard != null) selectedWeaponCard.SetSelected(false);
            selectedWeaponCard = null;
        }

        private void ClearWeaponCards()
        {
            foreach (var card in weaponCards)
            {
                if (card != null) card.OnCardClicked -= OnWeaponCardClicked;
                if (card != null) Destroy(card.gameObject);
            }
            weaponCards.Clear();
            selectedWeaponCard = null;
        }

        #endregion

        #region 아이템 목록

        public void UpdateActiveItemList(List<ActiveItemInstance> items, bool isNamed, string assignedItemDataID)
        {
            ClearActiveItemCards();

            var filtered = (items ?? new List<ActiveItemInstance>())
                .Where(i => i.activeItemData.usageContext != ActiveItemUsage.Blacksmith)
                .Where(i => isNamed || i.activeItemData.itemType != ActiveItemType.Doll)
                .OrderBy(i => i.activeItemData.usageContext)
                .ThenByDescending(i => i.activeItemData.effectValue)
                .ToList();

            if (filtered.Count == 0)
            {
                bool allTabSelected = activeItemTypeFilterToggles != null
                    && activeItemTypeFilterToggles.Count > 0
                    && activeItemTypeFilterToggles[0].isOn;
                UpdateSharedEmptyText(activeItemSubPanel, LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", allTabSelected ? "Inventory_EmptyGiftAll" : "Inventory_EmptyGiftFiltered"));
                activeItemScrollRect.ResetPosition(this);
                return;
            }

            UpdateSharedEmptyText(activeItemSubPanel, null);

            foreach (var item in filtered)
            {
                var cardObj = Instantiate(activeItemCardPrefab, activeItemListContainer);
                var card = cardObj.GetComponentOrNull<ActiveItemInventoryCard>();
                if (card != null)
                {
                    card.Initialize(item);
                    card.OnCardClicked += OnActiveItemCardClicked;
                    activeItemCards.Add(card);

                    if (item.activeItemData.StaticID == assignedItemDataID)
                    {
                        card.SetSelected(true);
                        selectedActiveItemCard = card;
                    }
                }
            }

            activeItemScrollRect.ResetPosition(this);
        }

        private void OnActiveItemCardClicked(ActiveItemInventoryCard card)
        {
            if (selectedActiveItemCard != null) selectedActiveItemCard.SetSelected(false);
            selectedActiveItemCard = card;
            selectedActiveItemCard.SetSelected(true);
            controller?.OnActiveItemCardClicked(card.Item);
        }

        public void ClearActiveItemCardSelection()
        {
            if (selectedActiveItemCard != null) selectedActiveItemCard.SetSelected(false);
            selectedActiveItemCard = null;
        }

        private void ClearActiveItemCards()
        {
            foreach (var card in activeItemCards)
            {
                if (card != null) card.OnCardClicked -= OnActiveItemCardClicked;
                if (card != null) Destroy(card.gameObject);
            }
            activeItemCards.Clear();
            selectedActiveItemCard = null;
        }

        // 무기/선물 서브탭이 하나의 listEmptyText를 공유하므로, 지금 보고 있는 서브탭(subPanel 활성)일
        // 때만 갱신한다. 다른 서브탭이면 건드리지 않아 화면과 다른 문구가 뜨는 것을 막는다. message가 null이면 숨긴다.
        private void UpdateSharedEmptyText(GameObject subPanel, string message)
        {
            if (listEmptyText == null) return;
            if (subPanel != null && !subPanel.activeSelf) return;

            if (string.IsNullOrEmpty(message))
            {
                listEmptyText.gameObject.SetActive(false);
                return;
            }

            listEmptyText.text = message;
            listEmptyText.gameObject.SetActive(true);
        }

        #endregion

        #region 튜토리얼 하이라이트 접근자

        /// <summary>튜토리얼 하이라이트 중 목록이 스크롤돼 대상 카드가 어긋나지 않도록 잠근다.</summary>
        public void SetWeaponScrollLocked(bool locked)
        {
            if (weaponScrollRect != null)
                weaponScrollRect.enabled = !locked;
        }

        /// <summary>튜토리얼 하이라이트 중 목록이 스크롤돼 대상 카드가 어긋나지 않도록 잠근다.</summary>
        public void SetActiveItemScrollLocked(bool locked)
        {
            if (activeItemScrollRect != null)
                activeItemScrollRect.enabled = !locked;
        }

        /// <summary>튜토리얼 하이라이트용 — 지정 WeaponData StaticID를 가진 첫 무기 카드의 RectTransform.</summary>
        public RectTransform GetWeaponCardRect(string weaponDataStaticID)
        {
            // GridLayoutGroup 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (weaponGridContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            var card = weaponCards.FirstOrDefault(c => c != null && c.Weapon?.weaponData != null
                && c.Weapon.weaponData.StaticID == weaponDataStaticID);
            return card?.transform as RectTransform;
        }

        /// <summary>튜토리얼 하이라이트용 — 부적(Charm) 타입 아이템 카드의 RectTransform.</summary>
        public RectTransform GetCharmCardRect()
        {
            if (activeItemListContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            var card = activeItemCards.FirstOrDefault(c => c != null && c.Item != null
                && c.Item.itemType == ActiveItemType.Charm);
            return card?.transform as RectTransform;
        }

        #endregion
    }
}
