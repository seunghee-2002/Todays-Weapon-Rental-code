using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상점 Controller — WeaponGrid 및 상점 전체 흐름 담당.
    /// 카드 클릭 시 WeaponShopDetailView를 열고, 구매 결과를 받아 Grid를 갱신한다.
    /// </summary>
    public class WeaponShopController : BaseController<WeaponShopView>
    {
        [SerializeField] private VisitorNPC currentVisitor;
        [SerializeField] private WeaponShopItemData selectedItem;

        #region 초기화

        public void Initialize(VisitorNPC visitor)
        {
            currentVisitor = visitor;

            var items = WeaponShopManager.Instance.CurrentShopItems.Count > 0
                ? WeaponShopManager.Instance.CurrentShopItems
                : WeaponShopManager.Instance.GenerateShopItems();

            view?.Initialize(items);

            // 튜토리얼: 상점이 열리면 하이라이트를 걷고 안내 대화를 진행한다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialShopOpened();
        }

        protected override void SubscribeControllerEvents()
        {
        }

        protected override void UnsubscribeControllerEvents()
        {
        }

        #endregion

        #region 이벤트 핸들러

        public void OnWeaponSelected(WeaponShopItemData item)
        {
            if (item == null) return;

            AnalyticsManager.Instance?.SendButtonClick("weapon_shop", "weapon_selected", new Dictionary<string, object>
            {
                { "weapon_id", item.WeaponData.StaticID },
                { "price", item.Price }
            });

            selectedItem = item;
            UIManager.Instance.OpenPanel<WeaponShopDetailView>();
            UIPopupController.Instance?.BringPlayerResourceBarToFront();
            UIControllerManager.Instance.GetController<WeaponShopDetailController>()?.Initialize(item);
        }

        /// <summary>WeaponShopDetailController에서 구매 성공 후 호출된다.</summary>
        public void OnWeaponPurchased(WeaponShopItemData item)
        {
            view?.RemoveWeaponCard(item);
            view?.UpdateRefreshButton();
            selectedItem = null;

            if (WeaponShopManager.Instance.CurrentShopItems.Count == 0 && !WeaponShopManager.Instance.CanRefresh)
                OnCloseShop();
        }

        public void OnRefreshShop()
        {
            if (WeaponShopManager.Instance == null) return;

            AnalyticsManager.Instance?.SendButtonClick("weapon_shop", "refresh_shop", new Dictionary<string, object>
            {
                { "cost", WeaponShopManager.Instance.GetRefreshCost() }
            });

            if (!WeaponShopManager.Instance.CanRefresh)
            {
                UIPopupController.Instance?.ShowToast(
                    LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "WeaponShop_RefreshExhausted"),
                    type: PopupSfxType.Warning);
                return;
            }

            int cost = WeaponShopManager.Instance.GetRefreshCost();
            UIPopupController.Instance?.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Messages", "WeaponShop_RefreshConfirm",
                    arguments: new object[] { new Dictionary<string, object> { { "cost", cost.ToString("N0") } } }),
                onConfirm: () => EconomyManager.Instance.EnsureGold(cost, onReady: DoRefreshShop),
                onCancel: () => { }
            );
        }

        private void DoRefreshShop()
        {
            if (WeaponShopManager.Instance.RefreshShop())
            {
                SoundManager.Instance?.PlaySFX("Buy");
                selectedItem = null;
                view?.Initialize(WeaponShopManager.Instance.CurrentShopItems);
            }
        }

        public void OnCloseShop()
        {
            AnalyticsManager.Instance?.SendButtonClick("weapon_shop", "close");

            if (currentVisitor != null && TutorialManager.Instance.IsTutorialActive)
            {
                TutorialManager.Instance.OnTutorialShopClosed();

                if (!TutorialManager.Instance.HasCompletedTutorial)
                    return;
            }

            var npc = currentVisitor;
            currentVisitor = null;
            selectedItem = null;

            WeaponShopManager.Instance.CloseShop();
            UIManager.Instance.ClosePanel<WeaponShopView>();

            npc?.EndInteraction();
        }

        #endregion

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}
