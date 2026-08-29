using UnityEngine;

namespace TodaysWeaponRental
{
    public class GameOverPopupController : BaseController<GameOverPopupView>
    {
        #region View로부터 호출되는 메서드

        public void OnConfirmClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("game_over_popup", "confirm");
            UIManager.Instance?.ClosePanel<GameOverPopupView>();
            SceneController.Instance?.LoadMainMenu();
        }

        #endregion

        #region 내부 메서드

        public void InitializeResult(int days, int totalCumulativeReputation, int totalAdventures,
                                    int earnedLegacy, int previousTotal)
        {
            view?.Initialize(days, totalCumulativeReputation, totalAdventures, earnedLegacy, previousTotal);
        }

        #endregion
    }
}
