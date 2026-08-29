using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class ForgeStoneSelectController : BaseController<ForgeStoneSelectView>
    {
        private static readonly string[] ForgeStoneIDs =
        {
            "ITEM_FORGE_STONE_01",
            "ITEM_FORGE_STONE_02",
            "ITEM_FORGE_STONE_03"
        };

        private Action onSlotUpdated;
        private float simulationBaseRate;
        private float simulationLegacyBonus;

        #region View로부터 호출되는 메서드

        public void Open(Action onSlotUpdated, float baseRate = 0f, float legacyBonus = 0f)
        {
            this.onSlotUpdated = onSlotUpdated;
            simulationBaseRate = baseRate;
            simulationLegacyBonus = legacyBonus;

            var assignedItem = ActiveItemManager.Instance.GetBlacksmithItem();
            var datas = new ActiveItemData[ForgeStoneIDs.Length];
            var quantities = new int[ForgeStoneIDs.Length];

            for (int i = 0; i < ForgeStoneIDs.Length; i++)
            {
                var data = DataManager.Instance.GetActiveItem(ForgeStoneIDs[i]);
                datas[i] = data;

                var instance = InventoryManager.Instance.GetActiveItemByDataID(ForgeStoneIDs[i]);
                int qty = instance?.quantity ?? 0;
                if (assignedItem != null && assignedItem.StaticID == ForgeStoneIDs[i])
                    qty += 1;
                quantities[i] = qty;
            }

            view?.Initialize(datas, quantities);
            UIManager.Instance.OpenPanel<ForgeStoneSelectView>();
        }

        public void OnItemSelected(ActiveItemData data)
        {
            if (data == null) return;

            if (simulationBaseRate > 0f)
            {
                float simRate = BlacksmithManager.Instance.GetSuccessRateSimulated(
                    simulationBaseRate, simulationLegacyBonus, data.effectValue);

                if (simRate >= 105f)
                {
                    UIPopupController.Instance?.ShowPopup(
                        LocalizationSettings.StringDatabase.GetLocalizedString(
                            "UI_Screens", "ForgeStone_OverflowConfirm"),
                        onConfirm: () => DoSelectItem(data),
                        onCancel: () => { });
                    return;
                }
            }

            DoSelectItem(data);
        }

        private void DoSelectItem(ActiveItemData data)
        {
            ActiveItemManager.Instance.SetBlacksmithItem(data);
            UIManager.Instance.ClosePanel<ForgeStoneSelectView>();
            onSlotUpdated?.Invoke();

            Log.Info($"[ForgeStoneSelectController] 강화석 배정: {data.itemName}");
        }

        public void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel<ForgeStoneSelectView>();
        }

        #endregion
    }
}
