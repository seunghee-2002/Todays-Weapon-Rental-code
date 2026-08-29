// Scripts/UI/Controllers/MorningEvent/WeaponEnhanceEventController.cs
namespace TodaysWeaponRental
{
    public class WeaponEnhanceEventController : MorningEventControllerBase<WeaponEnhanceEventView>
    {
        public (bool success, int gradeDelta, string message) OnEnhanceConfirmed(WeaponInstance weapon)
            => MorningEventManager.Instance.ExecuteWeaponEnhance(weapon);
    }
}
