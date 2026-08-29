using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 랭킹 팝업 뷰 — 1~100등 목록과 내 순위 표시
    /// </summary>
    public class LeaderboardPopupView : BaseView
    {
        protected override string GetThemeBgmKey() => "Ranking";

        [Header("팝업")]
        [SerializeField] private Button closeButton;

        [Header("상태")]
        [SerializeField] private GameObject loadingObject;
        [SerializeField] private TextMeshProUGUI errorText;

        [Header("목록")]
        [SerializeField] private Transform contentParent;
        [SerializeField] private LeaderboardEntryItem entryItemPrefab;

        [Header("내 순위 (하단 고정)")]
        [SerializeField] private GameObject myRankPanel;
        [SerializeField] private TextMeshProUGUI myRankText;
        [SerializeField] private TextMeshProUGUI myNameText;
        [SerializeField] private TextMeshProUGUI myScoreText;

        private LeaderboardPopupController controller;
        private readonly List<LeaderboardEntryItem> spawnedItems = new();

        public void SetController(LeaderboardPopupController ctrl) => controller = ctrl;

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
        }

        #region 상태 표시

        public void ShowLoading()
        {
            loadingObject?.SetActive(true);
            errorText?.gameObject.SetActive(false);
            myRankPanel?.SetActive(false);
            ClearList();
        }

        public void ShowError(string message)
        {
            loadingObject?.SetActive(false);
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
        }

        #endregion

        #region 목록 업데이트

        public void ShowEntries(List<RankEntry> entries, string myPlayerId)
        {
            loadingObject?.SetActive(false);
            errorText?.gameObject.SetActive(false);
            ClearList();

            foreach (var entry in entries)
            {
                var item = Instantiate(entryItemPrefab, contentParent);
                item.Setup(entry);
                spawnedItems.Add(item);
            }
        }

        public void ShowMyRank(string rankLabel, string playerName, int score)
        {
            if (myRankPanel == null) return;
            myRankPanel.SetActive(true);
            if (myRankText  != null) myRankText.text  = rankLabel;
            if (myNameText  != null) myNameText.text  = playerName;
            if (myScoreText != null) myScoreText.text = LocalizeScoreDays(score);
        }

        /// <summary>생존 일수 표시 문자열. 목록 행(LeaderboardEntryItem)과 같은 키를 쓴다.</summary>
        public static string LocalizeScoreDays(int score)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", "Leaderboard_ScoreDays",
                arguments: new object[] { new Dictionary<string, object> { { "days", score } } });
        }

        public void HideMyRank() => myRankPanel?.SetActive(false);

        #endregion

        #region 내부

        private void ClearList()
        {
            foreach (var item in spawnedItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            spawnedItems.Clear();
        }

        private void OnCloseClicked() => controller?.OnCloseClicked();

        #endregion
    }
}
