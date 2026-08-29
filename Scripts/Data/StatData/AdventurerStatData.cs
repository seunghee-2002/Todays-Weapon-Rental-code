using System;

namespace TodaysWeaponRental
{
    [Serializable]
    public class AdventurerStatData
    {
        public bool hasReachedMaxAffection;
        public bool hasDeathProtection;
        public int visitCount;
        public int adventureSuccessCount;
        public int adventureFailCount;
        public int adventurerWeaponGoodCount;
        public int lastVisitDay;
        public int giftedCount;
        public int totalGoldEarned;
    }
}
