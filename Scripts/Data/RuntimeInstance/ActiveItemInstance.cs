// Scripts/Runtime/ActiveItemInstance.cs
using System;

namespace TodaysWeaponRental
{
    [Serializable]
    public class ActiveItemInstance
    {
        public ActiveItemData activeItemData;
        public int quantity;

        public ActiveItemInstance(ActiveItemData data, int quantity = 1)
        {
            activeItemData = data;
            this.quantity = quantity;
        }

        public ActiveItemInstance(ActiveItemInstanceSaveData saveData)
        {
            activeItemData = DataManager.Instance.GetActiveItem(saveData.itemDataID);
            quantity = saveData.quantity;
        }

        public ActiveItemInstanceSaveData ToSaveData()
        {
            return new ActiveItemInstanceSaveData
            {
                itemDataID = activeItemData.StaticID,
                quantity = quantity
            };
        }
    }

    [Serializable]
    public class ActiveItemInstanceSaveData
    {
        public string itemDataID;
        public int quantity;
    }

    [Serializable]
    public class ActiveItemAssignmentSaveData
    {
        public string adventurerID;
        public string itemDataID;
    }
}
