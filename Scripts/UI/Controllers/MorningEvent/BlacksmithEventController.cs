// Scripts/UI/Controllers/MorningEvent/BlacksmithEventController.cs
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class BlacksmithEventController : MorningEventControllerBase<BlacksmithEventView>
    {
        public (bool success, bool isEnhance, List<WeaponEffect> previousEffects, int enforceLevelDelta, List<string> enforcedEffectIDs, string message) OnBlacksmithSubmitWeapon(WeaponInstance weapon)
            => MorningEventManager.Instance.ExecuteWanderingBlacksmithAuto(weapon);
    }
}
