using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class AdventureResultController : BaseController<AdventureResultView>
    {
        private AdventureInstance currentAdventure;

        /// <summary>
        /// 현재 결과창이 보여주는 모험. 결과창을 여는 순간 확정(ConfirmAdventureResult)돼
        /// CompletedAdventures에서 빠지므로, 튜토리얼 catch-up이 그 모험을 다시 찾는 유일한 경로다.
        /// </summary>
        public AdventureInstance CurrentAdventure => currentAdventure;

        #region View로부터 호출되는 메서드

        public void OpenAndPlay(AdventureInstance adventure)
        {
            if (adventure == null || !adventure.isCompleted)
            {
                Log.Error("[AdventureResultController] 유효하지 않은 모험 인스턴스입니다.");
                return;
            }

            currentAdventure = adventure;
            AdventureManager.Instance.ConfirmAdventureResult(adventure);
            view?.Initialize();
            UIManager.Instance.OpenPanel<AdventureResultView>();
            view?.InitAfter(adventure);
        }

        /// <summary>
        /// 결과 아이템(재료) 클릭 시 호출. MaterialPurchasePopup을 열어 사용자 확인을 받는다.
        /// </summary>
        public void OnMaterialPurchaseRequested(AdventureResultItem item, AdventureResult result)
        {
            if (item == null || item.MaterialData == null) return;

            UIManager.Instance.OpenPanel<MaterialPurchasePopup>();
            UIManager.Instance.GetOrInstantiatePanel<MaterialPurchasePopup>()?.Initialize(
                item.MaterialData,
                item.Quantity,
                item.TotalPrice,
                onConfirm: () => ExecuteSinglePurchase(item, result));
        }

        /// <summary>
        /// "재료 전부 구매" 버튼 클릭 시 호출.
        /// 미구매 항목 합산 후 confirm 팝업 → 일괄 구매.
        /// </summary>
        public void OnAllMaterialsPurchaseClicked(AdventureResult result)
        {
            if (view == null) return;

            var unpurchased = new List<AdventureResultItem>();
            int totalPrice = 0;
            foreach (var item in view.ResultItems)
            {
                if (item == null || item.MaterialData == null) continue;
                if (item.IsPurchased) continue;
                unpurchased.Add(item);
                totalPrice += item.TotalPrice;
            }

            if (unpurchased.Count == 0) return;

            string message = BuildBuyAllMessage(unpurchased, totalPrice);
            UIPopupController.Instance?.ShowPopup(message,
                onConfirm: () =>
                {
                    ExecuteBuyAll(unpurchased, totalPrice, result);
                    UIPopupController.Instance?.HidePlayerResourceBar();
                },
                onCancel: () => UIPopupController.Instance?.HidePlayerResourceBar());

            UIPopupController.Instance?.ShowPlayerResourceBar();
        }

        public void OnResultCloseClicked()
        {
            // 튜토리얼(9-B): 재료는 1개만 유도하므로 미구매 재료가 남아도 확인 팝업 없이 바로 닫는다.
            if (TutorialManager.Instance?.IsTutorialActive ?? false)
            {
                CloseResult();
                return;
            }

            if (HasUnpurchasedMaterials())
            {
                UIPopupController.Instance?.ShowPopup(
                    LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "AdventureResult_UnpurchasedConfirm"),
                    onConfirm: CloseResult,
                    onCancel: () => { });
                return;
            }

            CloseResult();
        }

        #endregion

        #region 내부 메서드

        private void CloseResult()
        {
            // 리플레이를 스킵했으면 ReplayView가 열려있지 않을 수 있으나, ClosePanel은 미오픈 패널에 안전(no-op).
            UIManager.Instance.ClosePanel<AdventureResultView>();
            UIManager.Instance.ClosePanel<AdventureReplayView>();

            // 전령 보고 중이면 이 모험을 보고 완료로 기록하고 다음 모험으로 이어간다.
            var herald = VisitorManager.Instance?.CurrentHerald;
            if (herald != null)
            {
                AdventureManager.Instance.MarkHeraldReported(currentAdventure?.instanceID);

                // 튜토리얼(9-B/9-C)은 다음 보고 앞에 안내 대사를 끼우므로, 보고 재개 시점을 TutorialManager가 잡는다.
                if (TutorialManager.Instance?.IsTutorialActive ?? false)
                {
                    TutorialManager.Instance.OnTutorialResultClosed();
                    return;
                }

                herald.ReportNextAdventure();
                return;
            }

            UIControllerManager.Instance.GetController<AdventureProgressController>()?.OnResultClosed(currentAdventure);
            TutorialManager.Instance?.OnTutorialResultClosed();   // 9-B 훅(가드는 TutorialManager 내부)
        }

        private bool HasUnpurchasedMaterials()
        {
            if (view == null) return false;
            foreach (var item in view.ResultItems)
            {
                if (item == null || item.MaterialData == null) continue;
                if (!item.IsPurchased) return true;
            }
            return false;
        }

        private void ExecuteSinglePurchase(AdventureResultItem item, AdventureResult result)
        {
            if (item == null || item.MaterialData == null) return;

            int price = item.TotalPrice;
            EconomyManager.Instance.EnsureGold(price, onReady: () =>
            {
                EconomyManager.Instance.SpendGold(price, $"{item.MaterialData.materialName} 구매");
                SoundManager.Instance?.PlaySFX("Buy");
                InventoryManager.Instance.AddMaterial(item.MaterialData.StaticID, item.Quantity);
                result?.purchasedMaterials.Add(new MaterialInstance(item.MaterialData, item.Quantity));
                view?.OnMaterialPurchaseCompleted(item);
                Log.Info($"[AdventureResultController] 재료 구매: {item.MaterialData.materialName} x{item.Quantity} ({price}G)");
            });
        }

        private void ExecuteBuyAll(List<AdventureResultItem> items, int totalPrice, AdventureResult result)
        {
            EconomyManager.Instance.EnsureGold(totalPrice, onReady: () =>
            {
                EconomyManager.Instance.SpendGold(totalPrice, "재료 일괄 구매");
                SoundManager.Instance?.PlaySFX("Buy");   // 일괄이므로 품목 수와 무관하게 1회
                foreach (var item in items)
                {
                    if (item == null || item.MaterialData == null) continue;
                    InventoryManager.Instance.AddMaterial(item.MaterialData.StaticID, item.Quantity);
                    result?.purchasedMaterials.Add(new MaterialInstance(item.MaterialData, item.Quantity));
                    view?.OnMaterialPurchaseCompleted(item);
                }
                Log.Info($"[AdventureResultController] 재료 일괄 구매: {items.Count}종 ({totalPrice}G)");
            });
        }

        private string BuildBuyAllMessage(List<AdventureResultItem> items, int totalPrice)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (i > 0) sb.Append(", ");
                sb.Append(LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Messages", "AdventureResult_MaterialCount",
                    arguments: new object[] { new Dictionary<string, object> {
                        { "name", item.MaterialData.DisplayName }, { "count", item.Quantity } } }));
            }
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Messages", "AdventureResult_BuyAllConfirm",
                arguments: new object[] { new Dictionary<string, object> {
                    { "items", sb.ToString() }, { "gold", totalPrice.ToString("N0") } } });
        }

        #endregion
    }
}
