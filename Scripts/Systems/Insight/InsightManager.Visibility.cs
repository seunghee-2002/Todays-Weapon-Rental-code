using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험가에 대한 통찰 기반 스탯 공개 가시성 스냅샷.
    /// -1 sentinel 없이 ShowAverage와 IsVisible(stat)을 분리해 노출.
    /// </summary>
    public readonly struct AdventurerStatVisibility
    {
        private readonly int visibleMask; // bit 0~3 = STR/DEX/INT/LUK
        public bool ShowAverage { get; }

        public AdventurerStatVisibility(int visibleMask, bool showAverage)
        {
            this.visibleMask = visibleMask;
            ShowAverage = showAverage;
        }

        public bool IsVisible(AdventurerStat stat) => (visibleMask & (1 << (int)stat)) != 0;
        public bool IsVisible(int statIndex) => statIndex >= 0 && statIndex < 4 && (visibleMask & (1 << statIndex)) != 0;

        /// <summary>공개 스탯 비트마스크 원본. bit 0~3 = STR/DEX/INT/LUK</summary>
        public int Mask => visibleMask;

        public int VisibleStatCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < 4; i++)
                    if ((visibleMask & (1 << i)) != 0) c++;
                return c;
            }
        }
    }

    public partial class InsightManager
    {
        #region 스탯 가시성

        /// <summary>
        /// 모험가의 이번 방문에 표시할 스탯 가시성 스냅샷.
        /// 1차 자동 공개(autoRevealedStatIndices) + 2차 대화 공개(revealedStatIndices) + 전체 공개 + 평균 표시 여부를 합산.
        /// </summary>
        public AdventurerStatVisibility GetStatVisibility(AdventurerInstance adventurer)
        {
            if (adventurer == null) return new AdventurerStatVisibility(0, false);

            int mask = 0;

            if (adventurer.isStatsFullyRevealed)
                return new AdventurerStatVisibility(0b1111, true);

            // 2차 대화로 공개된 스탯 (영구)
            foreach (int idx in adventurer.revealedStatIndices)
            {
                if (idx >= 0 && idx < 4) mask |= (1 << idx);
            }

            // 자동 공개 — 일반 모험가만 통찰 임계값에 따라 단계 적용. 네임드는 자동 공개 없음.
            if (!adventurer.isNamed)
            {
                int count = GetNormalAdventurerRevealCount();
                var autoStats = adventurer.autoRevealedStatIndices;
                if (autoStats != null)
                {
                    if (count >= 1 && autoStats.Count > 0) mask |= (1 << (int)autoStats[0]);
                    if (count >= 2 && autoStats.Count > 1) mask |= (1 << (int)autoStats[1]);
                }

                if (CanRevealNormalAdventurerHighestStat())
                    mask |= (1 << (int)adventurer.GetHighestStat());
            }

            bool showAvg = !adventurer.isNamed
                           && CurrentInsight >= ConfigManager.Instance.Insight.normalRevealAverageThreshold;

            return new AdventurerStatVisibility(mask, showAvg);
        }

        #endregion

        #region UI gating 술어

        /// <summary>
        /// 무기 타입이 알려져 있는가.
        /// 기본 무기가 아닌 무기를 대여한 상태이거나, 대화로 무기 타입 힌트를 얻은 경우 true.
        /// </summary>
        public bool IsWeaponTypeKnown(AdventurerInstance adventurer, WeaponInstance weapon)
        {
            if (adventurer == null || weapon == null) return false;
            return weapon != adventurer.defaultWeapon || adventurer.isWeaponTypeHinted;
        }

        /// <summary>
        /// 모험 준비 화면에서 기본/최종 성공률 분해 행을 표시할지.
        /// 규칙: 무기 타입 공개 AND (통찰 80+ OR (무기별 스탯 조건 충족 AND 방어 타입 공개)).
        /// 무기 타입이 공개되지 않은 기본 무기 상태에서 표시하면, 공개 판정이 기본 무기 타입에
        /// 좌우돼 타입이 새어나가고 같은 화면의 무기-던전 상성 행과도 기준이 어긋난다.
        /// 무기별 스탯 조건은 TypeAdvantage의 required/anyOf 마스크가 정의한다.
        /// 방어 타입 공개 여부는 호출자가 ScoutManager로부터 받아 외부 주입.
        /// </summary>
        public bool IsSuccessRateBreakdownVisible(AdventurerInstance adventurer, WeaponInstance weapon, bool armorTypeKnown)
        {
            if (adventurer == null || weapon == null) return false;
            if (!IsWeaponTypeKnown(adventurer, weapon)) return false;

            int threshold = ConfigManager.Instance.Insight.successRateRevealThreshold;
            if (CurrentInsight >= threshold) return true;

            if (!armorTypeKnown) return false;

            var visibility = GetStatVisibility(adventurer);
            return TypeAdvantage.IsStatMaskSatisfied(weapon.weaponData.weaponType, visibility.Mask);
        }

        /// <summary>
        /// 모험가의 특성 정보가 공개되어 있는가.
        /// 명시적으로 공개됐거나(isTraitRevealed), 일반 모험가가 통찰 80+에 도달한 경우.
        /// 네임드는 통찰 자동 공개 대상 아님 — isTraitRevealed만으로 판단.
        /// </summary>
        public bool IsTraitKnown(AdventurerInstance adventurer)
        {
            if (adventurer == null) return false;
            if (adventurer.isTraitRevealed) return true;
            if (adventurer.isNamed) return false;
            int threshold = ConfigManager.Instance.Insight.successRateRevealThreshold;
            return CurrentInsight >= threshold;
        }

        #endregion
    }
}
