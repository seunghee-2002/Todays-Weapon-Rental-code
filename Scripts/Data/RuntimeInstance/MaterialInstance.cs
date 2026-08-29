// Scripts/Runtime/MaterialInstance.cs
using System;

namespace TodaysWeaponRental
{
    [Serializable]
    public class MaterialInstance
    {
        [NonSerialized] public MaterialData materialData;
        // 직렬화 fallback — List<MaterialInstance>이 그대로 저장되는 경로 대응
        public string materialDataID;
        public int quantity;

        public MaterialInstance(MaterialData data, int quantity)
        {
            materialData = data;
            materialDataID = data?.StaticID;
            this.quantity = quantity;
        }

        public void Add()
        {
            quantity++;
        }
    }

    [Serializable]
    public class MaterialInstanceSaveData
    {
        public string materialDataID;
        public int quantity;
    }
}
