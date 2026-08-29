namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 결과 아이템 컨테이너에서 표시되는 항목의 종류
    /// </summary>
    public enum ResultItemType
    {
        Reputation,
        Affection,
        Insight,
        MaterialCraft,
        MaterialEnforce,
        MaterialSpecial
    }

    public static class ResultItemTypeExtensions
    {
        public static ResultItemType FromMaterialType(MaterialType materialType)
        {
            return materialType switch
            {
                MaterialType.Craft   => ResultItemType.MaterialCraft,
                MaterialType.Enforce => ResultItemType.MaterialEnforce,
                MaterialType.Special => ResultItemType.MaterialSpecial,
                _ => ResultItemType.MaterialCraft
            };
        }
    }
}
