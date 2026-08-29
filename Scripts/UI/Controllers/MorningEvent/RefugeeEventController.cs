// Scripts/UI/Controllers/MorningEvent/RefugeeEventController.cs
namespace TodaysWeaponRental
{
    public class RefugeeEventController : MorningEventControllerBase<RefugeeEventView>
    {
        public (bool success, bool actuallyDonated, string message) OnDonateClicked()
        {
            var (success, actuallyDonated, _, message) = MorningEventManager.Instance.ExecuteRefugeeHelp(true);
            return (success, actuallyDonated, message);
        }

        public (bool success, string message) OnRejectClicked()
        {
            var (_, _, _, message) = MorningEventManager.Instance.ExecuteRefugeeHelp(false);
            return (true, message);
        }
    }
}
