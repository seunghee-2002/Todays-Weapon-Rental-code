using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상점 상세 패널 Controller.
    /// WeaponShopController가 OpenPanel 후 Initialize를 호출하며,
    /// 구매/취소 결과를 WeaponShopController에 전달한다.
    /// </summary>
    public class WeaponShopDetailController : BaseController<WeaponShopDetailView>
    {
        private WeaponShopItemData selectedItem;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            view?.SetController(this);
        }

        public void Initialize(WeaponShopItemData item)
        {
            selectedItem = item;
            view?.Initialize(item);
        }

        protected override void SubscribeControllerEvents()
        {
            if (EconomyManager.Instance != null)
                EconomyManager.Instance.OnGoldChanged += HandleGoldChanged;
        }

        protected override void UnsubscribeControllerEvents()
        {
            if (EconomyManager.Instance != null)
                EconomyManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }

        #endregion

        #region View로부터 호출되는 메서드

        public void OnPurchaseWeapon()
        {
            if (WeaponShopManager.Instance == null || selectedItem == null) return;

            AnalyticsManager.Instance?.SendButtonClick("weapon_shop", "purchase", new Dictionary<string, object>
            {
                { "weapon_id", selectedItem.WeaponData.StaticID },
                { "price", selectedItem.Price }
            });

            EconomyManager.Instance.EnsureGold(selectedItem.Price, onReady: DoPurchaseWeapon);
        }

        public void OnCancel()
        {
            AnalyticsManager.Instance?.SendButtonClick("weapon_shop", "cancel");
            selectedItem = null;
            UIManager.Instance.ClosePanel<WeaponShopDetailView>();
        }

        #endregion

        #region 이벤트 핸들러

        private void HandleGoldChanged(int gold)
        {
            if (view != null && view.gameObject.activeInHierarchy && selectedItem != null)
                view.UpdatePurchaseButton(selectedItem);
        }

        #endregion

        #region 내부 메서드

        private void DoPurchaseWeapon()
        {
            if (selectedItem == null) return;

            bool success = WeaponShopManager.Instance.PurchaseWeapon(selectedItem);

            if (success)
            {
                Log.Info($"[WeaponShopDetailController] 구매 완료: {selectedItem.WeaponData.weaponName}");
                SoundManager.Instance?.PlaySFX("Buy");

                var shopCtrl = UIControllerManager.Instance.GetController<WeaponShopController>();
                shopCtrl?.OnWeaponPurchased(selectedItem);

                if (TutorialManager.Instance.IsTutorialActive)
                    TutorialManager.Instance.OnTutorialWeaponPurchased();

                selectedItem = null;
                UIManager.Instance.ClosePanel<WeaponShopDetailView>();
            }
        }

        #endregion
    }
}
