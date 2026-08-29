using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    public class LegacyUpgradeView : BaseView
    {
        protected override string GetThemeBgmKey() => "LegacyUpgrade";

        [Header("헤더")]
        [SerializeField] private TextMeshProUGUI legacyPointsText;
        [SerializeField] private Button closeButton;

        [Header("카테고리 토글")]
        [SerializeField] private Toggle toggleStart;
        [SerializeField] private Toggle toggleBlacksmith;
        [SerializeField] private Toggle toggleAdventure;
        [SerializeField] private Toggle toggleSpecial;

        [Header("탭 포커스 — 시작")]
        [SerializeField] private GameObject tabFocusStart;
        [SerializeField] private TextMeshProUGUI tabTextStart;
        [SerializeField] private Image tabIconStart;

        [Header("탭 포커스 — 대장간")]
        [SerializeField] private GameObject tabFocusBlacksmith;
        [SerializeField] private TextMeshProUGUI tabTextBlacksmith;
        [SerializeField] private Image tabIconBlacksmith;

        [Header("탭 포커스 — 모험")]
        [SerializeField] private GameObject tabFocusAdventure;
        [SerializeField] private TextMeshProUGUI tabTextAdventure;
        [SerializeField] private Image tabIconAdventure;

        [Header("탭 포커스 — 특수")]
        [SerializeField] private GameObject tabFocusSpecial;
        [SerializeField] private TextMeshProUGUI tabTextSpecial;
        [SerializeField] private Image tabIconSpecial;

        [Header("스크롤 콘텐츠")]
        [SerializeField] private RectTransform scrollContent;
        [SerializeField] private LegacyUpgradeItem upgradeItemPrefab;

        private LegacyUpgradeController controller;
        private readonly List<LegacyUpgradeItem> activeItems = new();

        public void SetController(LegacyUpgradeController ctrl) => controller = ctrl;

        #region BaseView

        public override void Open()
        {
            base.Open();
            if (toggleStart != null)
            {
                toggleStart.SetIsOnWithoutNotify(false);
                toggleStart.isOn = true;
            }
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(() => controller?.OnCloseClicked());

            toggleStart?.onValueChanged.AddListener(on =>
            {
                ApplyTabFocus(on, tabFocusStart, tabTextStart, tabIconStart);
                if (on) controller?.OnCategorySelected(UpgradeCategory.Start);
            });
            toggleBlacksmith?.onValueChanged.AddListener(on =>
            {
                ApplyTabFocus(on, tabFocusBlacksmith, tabTextBlacksmith, tabIconBlacksmith);
                if (on) controller?.OnCategorySelected(UpgradeCategory.Blacksmith);
            });
            toggleAdventure?.onValueChanged.AddListener(on =>
            {
                ApplyTabFocus(on, tabFocusAdventure, tabTextAdventure, tabIconAdventure);
                if (on) controller?.OnCategorySelected(UpgradeCategory.Adventure);
            });
            toggleSpecial?.onValueChanged.AddListener(on =>
            {
                ApplyTabFocus(on, tabFocusSpecial, tabTextSpecial, tabIconSpecial);
                if (on) controller?.OnCategorySelected(UpgradeCategory.Special);
            });
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            toggleStart?.onValueChanged.RemoveAllListeners();
            toggleBlacksmith?.onValueChanged.RemoveAllListeners();
            toggleAdventure?.onValueChanged.RemoveAllListeners();
            toggleSpecial?.onValueChanged.RemoveAllListeners();
        }

        #endregion

        #region View 업데이트 메서드

        public void RefreshLegacyPoints(int points)
        {
            if (legacyPointsText != null)
                legacyPointsText.text = $"{points:N0}";
        }

        public void RefreshItems(IReadOnlyList<UpgradeItemData> items)
        {
            ClearItems();
            foreach (var data in items)
            {
                var item = Instantiate(upgradeItemPrefab, scrollContent);
                item.gameObject.SetActive(false);
                item.Setup(
                    data.key, data.icon, data.level, data.maxLevel, data.cost,
                    data.canPurchase, data.isLocked, data.prerequisiteDisplayName,
                    data.description, data.upgradeStepDescription,
                    key => controller?.OnBuyClicked(key)
                );
                item.gameObject.SetActive(true);
                activeItems.Add(item);
            }
        }

        public void UpdateItemStates(IReadOnlyList<UpgradeItemData> items)
        {
            if (items.Count != activeItems.Count)
            {
                RefreshItems(items);
                return;
            }
            for (int i = 0; i < activeItems.Count; i++)
            {
                var data = items[i];
                activeItems[i].Setup(
                    data.key, data.icon, data.level, data.maxLevel, data.cost,
                    data.canPurchase, data.isLocked, data.prerequisiteDisplayName,
                    data.description, data.upgradeStepDescription,
                    key => controller?.OnBuyClicked(key)
                );
            }
        }

        private void ClearItems()
        {
            foreach (var item in activeItems)
            {
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                    Destroy(item.gameObject);
                }
            }
            activeItems.Clear();
        }

        private void ApplyTabFocus(bool on, GameObject focus, TextMeshProUGUI text, Image icon)
        {
            focus?.SetActive(on);
            Color c = ColorManager.Instance.GetSubTabColor(on);
            if (text != null) text.color = c;
            if (icon != null) icon.color = c;
        }

        #endregion
    }
}
