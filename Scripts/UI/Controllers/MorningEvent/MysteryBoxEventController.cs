// Scripts/UI/Controllers/MorningEvent/MysteryBoxEventController.cs
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class MysteryBoxEventController : MorningEventControllerBase<MysteryBoxEventView>
    {
        public (bool success, MysteryBoxRewardType rewardType, int goldAmount, List<string> materialIDs, WeaponInstance weapon, string message) OnBoxBuyClicked(int tier)
            => MorningEventManager.Instance.ExecuteMysteryBox(tier);

        public void ResetMysteryBoxTier()
        {
            GameManager.Instance.GameData.mysteryBoxTier = -1;
        }
    }
}
