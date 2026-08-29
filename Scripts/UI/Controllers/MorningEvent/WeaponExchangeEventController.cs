// Scripts/UI/Controllers/MorningEvent/WeaponExchangeEventController.cs
namespace TodaysWeaponRental
{
    public class WeaponExchangeEventController : MorningEventControllerBase<WeaponExchangeEventView>
    {
        public (bool success, WeaponInstance result, string message) OnExchangeConfirmed(WeaponInstance a, WeaponInstance b)
            => MorningEventManager.Instance.ExecuteWeaponExchange(a, b);
    }
}
