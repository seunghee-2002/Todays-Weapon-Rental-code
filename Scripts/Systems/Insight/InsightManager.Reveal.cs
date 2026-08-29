using UnityEngine;

namespace TodaysWeaponRental
{
    public partial class InsightManager
    {
        #region 개별 스탯 공개

        /// <summary>
        /// 개별 스탯 공개 시도 — 현재 통찰값 기반 성공률로 RNG 굴림.
        /// 성공 시 revealedStatIndices에 추가. 이미 공개된 스탯이면 false.
        /// </summary>
        public bool RevealStat(AdventurerInstance adventurer, AdventurerStat stat)
        {
            if (adventurer == null) return false;
            int idx = (int)stat;
            if (adventurer.revealedStatIndices.Contains(idx)) return false;

            float rate = GetStatRevealSuccessRate(adventurer, stat);
            if (Random.value >= rate) return false;

            adventurer.revealedStatIndices.Add(idx);
            Log.Info($"[InsightManager] 스탯 공개 성공 — {adventurer.Name}, {stat}");
            return true;
        }

        /// <summary>프리미엄 — 성공률 무시하고 무조건 공개.</summary>
        public void RevealStatGuaranteed(AdventurerInstance adventurer, AdventurerStat stat)
        {
            if (adventurer == null) return;
            int idx = (int)stat;
            if (adventurer.revealedStatIndices.Contains(idx)) return;
            adventurer.revealedStatIndices.Add(idx);
            Log.Info($"[InsightManager] 스탯 공개 (Guaranteed) — {adventurer.Name}, {stat}");
        }

        #endregion

        #region 전체 스탯 공개

        public bool RevealAllStats(AdventurerInstance adventurer)
        {
            if (adventurer == null || adventurer.isStatsFullyRevealed) return false;

            // 6단계 종합 테스트는 일반 판정이지만 성공을 보장한다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                RevealAllStatsGuaranteed(adventurer);
                return true;
            }

            float rate = GetAllStatRevealSuccessRate(adventurer);
            if (Random.value >= rate) return false;

            adventurer.isStatsFullyRevealed = true;
            Log.Info($"[InsightManager] 전체 스탯 공개 성공 — {adventurer.Name}");
            return true;
        }

        public void RevealAllStatsGuaranteed(AdventurerInstance adventurer)
        {
            if (adventurer == null || adventurer.isStatsFullyRevealed) return;
            adventurer.isStatsFullyRevealed = true;
            Log.Info($"[InsightManager] 전체 스탯 공개 (Guaranteed) — {adventurer.Name}");
        }

        #endregion

        #region 특성 공개

        public bool RevealTrait(AdventurerInstance adventurer)
        {
            if (adventurer == null || adventurer.isTraitRevealed) return false;

            float rate = GetTraitRevealSuccessRate(adventurer);
            if (Random.value >= rate) return false;

            adventurer.isTraitRevealed = true;
            Log.Info($"[InsightManager] 특성 공개 성공 — {adventurer.Name}, {adventurer.Trait}");
            return true;
        }

        public void RevealTraitGuaranteed(AdventurerInstance adventurer)
        {
            if (adventurer == null || adventurer.isTraitRevealed) return;
            adventurer.isTraitRevealed = true;
            Log.Info($"[InsightManager] 특성 공개 (Guaranteed) — {adventurer.Name}, {adventurer.Trait}");
        }

        #endregion

        #region 무기 타입 힌트

        /// <summary>무기 타입 힌트는 항상 100% 성공. 단순 mutation.</summary>
        public void RevealWeaponTypeHint(AdventurerInstance adventurer)
        {
            if (adventurer == null || adventurer.isWeaponTypeHinted) return;
            adventurer.isWeaponTypeHinted = true;
            Log.Info($"[InsightManager] 무기 타입 힌트 공개 — {adventurer.Name}");
        }

        #endregion
    }
}
