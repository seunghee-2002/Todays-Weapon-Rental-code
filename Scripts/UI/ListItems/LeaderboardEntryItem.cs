using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 랭킹 팝업의 각 행 — 순위, 플레이어명, 점수 표시
    /// </summary>
    public class LeaderboardEntryItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private Image rankIcon;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI scoreText;

        public void Setup(RankEntry entry)
        {
            var icon = IconManager.Instance.GetRankIcon(entry.Rank);
            var isTopThree = icon != null;

            rankText.gameObject.SetActive(!isTopThree);
            rankIcon.gameObject.SetActive(isTopThree);

            if (isTopThree)
                rankIcon.sprite = icon;
            else
                rankText.text = $"{entry.Rank}";

            playerNameText.text = entry.PlayerName;
            scoreText.text      = LeaderboardPopupView.LocalizeScoreDays(entry.Score);
        }
    }
}
