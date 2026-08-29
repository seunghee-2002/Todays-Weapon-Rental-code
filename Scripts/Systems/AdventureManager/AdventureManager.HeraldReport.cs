// Scripts/Systems/AdventureManager/AdventureManager.HeraldReport.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 저녁 6시 보고 전령이 보고할 모험 목록 관리. (튜토리얼 안내역 '길드 전령'과는 별개 인물)
    /// 목록은 18:00 시점의 completedAdventures 스냅샷이며, 이후 완료된 모험은 다음 날 보고 전령이 보고한다(이월).
    /// 그러므로 "미확정 모험 전체"가 아니라 항상 이 목록을 기준으로 판정해야 한다.
    /// </summary>
    public partial class AdventureManager
    {
        #region 전령 보고 목록

        private List<string> PendingHeraldIDs
        {
            get
            {
                var gameData = GameManager.Instance.GameData;
                if (gameData.heraldPendingAdventureIDs == null)
                    gameData.heraldPendingAdventureIDs = new List<string>();
                return gameData.heraldPendingAdventureIDs;
            }
        }

        /// <summary>오늘 18:00 스냅샷을 이미 떴는지.</summary>
        public bool HeraldReportStartedToday
        {
            get => GameManager.Instance.GameData.heraldReportStartedToday;
            private set => GameManager.Instance.GameData.heraldReportStartedToday = value;
        }

        /// <summary>보고 전령이 아직 보고하지 않은 (실재하는) 모험이 남아 있는지.</summary>
        public bool HasPendingHeraldReport => GetNextHeraldAdventure() != null;

        /// <summary>남은 보고 건수.</summary>
        public int PendingHeraldReportCount =>
            PendingHeraldIDs.Count(id => FindCompletedAdventure(id) != null);

        /// <summary>
        /// 저녁 진입 시 1회: 그 시점에 완료돼 있던 모험을 오래된 순으로 스냅샷한다.
        /// </summary>
        public void StartHeraldReport()
        {
            if (HeraldReportStartedToday) return;

            var ids = completedAdventures
                .Where(a => a != null)
                .OrderBy(a => a.startTime)
                .Select(a => a.instanceID)
                .ToList();

            PendingHeraldIDs.Clear();
            PendingHeraldIDs.AddRange(ids);
            HeraldReportStartedToday = true;

            Log.Info($"[AdventureManager] 전령 보고 목록 생성: {ids.Count}건");
        }

        /// <summary>
        /// 다음에 보고할 모험. 이미 확정됐거나 유실된 ID는 목록에서 정리하며 건너뛴다.
        /// </summary>
        public AdventureInstance GetNextHeraldAdventure()
        {
            var pending = PendingHeraldIDs;

            while (pending.Count > 0)
            {
                var adventure = FindCompletedAdventure(pending[0]);
                if (adventure != null) return adventure;

                pending.RemoveAt(0);
            }

            return null;
        }

        /// <summary>해당 모험의 보고가 끝났음을 기록한다.</summary>
        public void MarkHeraldReported(string instanceID)
        {
            if (string.IsNullOrEmpty(instanceID)) return;

            PendingHeraldIDs.Remove(instanceID);
            SaveOngoingAdventures();
        }

        /// <summary>날짜 변경 시 초기화. 남은 보고 대상은 다음 날 18:00 스냅샷에 다시 포함된다.</summary>
        public void ResetHeraldReport()
        {
            PendingHeraldIDs.Clear();
            HeraldReportStartedToday = false;
        }

        /// <summary>
        /// 튜토리얼 catch-up 전용: 오늘 보고 전령의 보고를 이미 끝낸 상태로 만든다(정상 완주와 동일한 최종 상태).
        /// 이게 없으면 튜토리얼 스킵 후 20:00 스킵이 18시를 지날 때 EnsureHerald가 빈 목록으로 보고 전령을 다시 부른다.
        /// </summary>
        public void MarkHeraldReportFinishedToday()
        {
            PendingHeraldIDs.Clear();
            HeraldReportStartedToday = true;
        }

        private AdventureInstance FindCompletedAdventure(string instanceID)
        {
            if (string.IsNullOrEmpty(instanceID)) return null;
            return completedAdventures.Find(a => a != null && a.instanceID == instanceID);
        }

        #endregion
    }
}
