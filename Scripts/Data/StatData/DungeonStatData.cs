// Scripts/Runtime/DungeonStatData.cs
using System;

namespace TodaysWeaponRental
{
    [Serializable]
    public class DungeonStatData
    {
        public int explorationProgress; // 0~100
        public bool hasGreatSuccessReady;
        public int totalAttempts;
        public int successCount;
        public int failCount;
        public int greatSuccessCount;
    }
}