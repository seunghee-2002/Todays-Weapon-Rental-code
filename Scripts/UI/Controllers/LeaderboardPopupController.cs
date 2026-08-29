using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 랭킹 팝업 컨트롤러
    /// </summary>
    public class LeaderboardPopupController : BaseController<LeaderboardPopupView>
    {
        protected override void Awake()
        {
            base.Awake();
            view?.SetController(this);
        }

        protected override async void OnEnable()
        {
            base.OnEnable();
            await LoadLeaderboardAsync();
        }

        #region View로부터 호출되는 메서드

        public void OnCloseClicked()
        {
            UIManager.Instance?.ClosePanel<LeaderboardPopupView>();
        }

        #endregion

        #region 외부에서 호출되는 메서드

        public void OpenPanel()
        {
            UIManager.Instance?.OpenPanel<LeaderboardPopupView>();
        }

        #endregion

        #region 내부 메서드

        private async System.Threading.Tasks.Task LoadLeaderboardAsync()
        {
            view?.ShowLoading();

            var manager = LeaderboardManager.Instance;
            if (manager == null)
            {
                view?.ShowError(Localize("Leaderboard_ErrorUnavailable"));
                return;
            }

            var topScores = await manager.FetchTopScoresAsync(100);
            var myRank    = await manager.FetchMyRankAsync();

            string myPlayerId = UGSManager.Instance?.PlayerId ?? "";

            if (topScores.Count == 0)
                view?.ShowError(Localize("Leaderboard_ErrorNoData"));
            else
                view?.ShowEntries(topScores, myPlayerId);

            ShowMyRankOrOngoing(myRank);
        }

        /// <summary>
        /// 진행 중인 회차의 일수가 등록된 최고기록보다 높으면 그쪽을 보여준다.
        /// 게임오버 전에는 리더보드에 올라가지 않으므로 순위는 "-"로 표시한다.
        /// </summary>
        private void ShowMyRankOrOngoing(RankEntry myRank)
        {
            int ongoingDays = GetOngoingDays();
            string myName   = NicknameManager.Instance?.GetDisplayName() ?? "-";

            if (ongoingDays > (myRank?.Score ?? 0))
                view?.ShowMyRank("-", myName, ongoingDays);
            else if (myRank != null)
                view?.ShowMyRank(LocalizeRank(myRank.Rank), myName, myRank.Score);
            else
                view?.HideMyRank();
        }

        private static string Localize(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string LocalizeRank(int rank)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", "Leaderboard_RankLabel",
                   arguments: new object[] { new Dictionary<string, object> { { "rank", rank } } });

        /// <summary>
        /// 진행 중인 회차의 일수. 저장 데이터가 없거나 손상됐으면 0.
        /// </summary>
        private int GetOngoingDays()
        {
            if (!SaveManager.HasGameData()) return 0;
            return SaveManager.LoadGame()?.currentDay ?? 0;
        }

        #endregion
    }
}
