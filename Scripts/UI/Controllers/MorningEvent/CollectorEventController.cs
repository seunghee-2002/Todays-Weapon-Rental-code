// Scripts/UI/Controllers/MorningEvent/CollectorEventController.cs
namespace TodaysWeaponRental
{
    public class CollectorEventController : MorningEventControllerBase<CollectorEventView>
    {
        public float OnRollMultiplier()
            => MorningEventManager.Instance.RollCollectorMultiplier();

        public (bool success, int goldAmount, float multiplier, string message) OnSellConfirmed(WeaponInstance weapon, float multiplier)
            => MorningEventManager.Instance.ExecuteCollectorSell(weapon, multiplier);
    }
}
