// Scripts/UI/Controllers/MorningEvent/BlackMarketEventController.cs
namespace TodaysWeaponRental
{
    public class BlackMarketEventController : MorningEventControllerBase<BlackMarketEventView>
    {
        public (bool success, string message) OnBlackMarketBuyConfirmed(WeaponInstance weapon, int price)
            => MorningEventManager.Instance.ExecuteBlackMarketBuy(weapon, price);
    }
}
