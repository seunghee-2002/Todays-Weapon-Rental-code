// Scripts/UI/Controllers/MorningEvent/GuildEnvoyEventController.cs
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class GuildEnvoyEventController : MorningEventControllerBase<GuildEnvoyEventView>
    {
        public (bool isGift, int goldAmount, List<string> materialIDs, string message) OnGuildEnvoyExecute()
            => MorningEventManager.Instance.ExecuteGuildEnvoy();
    }
}
