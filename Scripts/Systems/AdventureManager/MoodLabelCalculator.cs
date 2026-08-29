// Scripts/Systems/AdventureManager/MoodLabelCalculator.cs
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 준비 화면의 정성적 mood 라벨 계산.
    /// 칩 목록을 받아 (E, V_plus, V_minus) → (MoodBand, VarianceState) → 라벨 텍스트/색 매핑.
    /// 자세한 규칙은 Documents/Segment바_구조와_개선안.md §8 참조.
    /// </summary>
    public static class MoodLabelCalculator
    {
        public struct Result
        {
            public MoodBand band;
            public VarianceState state;
            public Color color;
            public Color textColor;
        }

        public static Result Calculate(List<AdventureInfoCardData> cards, AdventureInfoConfig cfg)
        {
            float e = 0f;
            float vPlus = 0f;
            float vMinus = 0f;
            float totalMag = 0f;

            if (cards != null)
            {
                foreach (var c in cards)
                {
                    totalMag += Mathf.Abs(c.value);
                    if (c.isConfirmed)
                    {
                        e += c.value;
                    }
                    else
                    {
                        if (c.value > 0f) vPlus += c.value;
                        else              vMinus += Mathf.Abs(c.value);
                    }
                }
            }

            float v    = vPlus + vMinus;
            float tLow = Mathf.Max(cfg.varianceLowFloor, totalMag * cfg.varianceLowRatio);

            MoodBand band;
            if (e <= cfg.bandThresholdVeryDark)        band = MoodBand.VeryDark;
            else if (e <= cfg.bandThresholdDark)       band = MoodBand.Dark;
            else if (e <  cfg.bandThresholdBright)     band = MoodBand.Neutral;
            else if (e <  cfg.bandThresholdVeryBright) band = MoodBand.Bright;
            else                                       band = MoodBand.VeryBright;

            VarianceState state;
            if (v < tLow)
            {
                state = VarianceState.None;
            }
            else
            {
                float posRatio = v > 0f ? vPlus / v : 0f;
                if (posRatio >= cfg.varianceLeanThreshold)            state = VarianceState.Positive;
                else if ((1f - posRatio) >= cfg.varianceLeanThreshold) state = VarianceState.Negative;
                else                                                  state = VarianceState.Mixed;
            }

            var entry = cfg.GetMoodLabel(band, state);
            return new Result
            {
                band      = band,
                state     = state,
                color     = entry?.color ?? Color.white,
                textColor = entry?.textColor ?? Color.black,
            };
        }
    }
}
