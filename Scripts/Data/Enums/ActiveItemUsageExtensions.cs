namespace TodaysWeaponRental
{
    /// <summary>
    /// 액티브 아이템의 용도(usageContext)를 카드/상세 표시용 등급으로 매핑한다.
    /// Adventure=Common, Immediate=Uncommon, Blacksmith=Rare.
    /// </summary>
    public static class ActiveItemUsageExtensions
    {
        public static Grade ToGrade(this ActiveItemUsage usage)
        {
            return usage switch
            {
                ActiveItemUsage.Adventure  => Grade.Common,
                ActiveItemUsage.Immediate  => Grade.Uncommon,
                ActiveItemUsage.Blacksmith => Grade.Rare,
                _ => Grade.Common
            };
        }
    }
}
