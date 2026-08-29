// Scripts/Systems/AdventureManager/AdventureManager.Mood.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    public partial class AdventureManager
    {
        #region 기분 시스템

        /// <summary>
        /// 모험 출발 시 1회 호출. 모험가-무기 매칭 점수를 기반으로 기분을 결정한다.
        /// </summary>
        private AdventurerMood DetermineMood(AdventurerInstance adventurer, WeaponInstance weapon)
        {
            float rawScore  = GetEffectStatScore(adventurer, weapon);
            float matchScore = Mathf.Clamp01(rawScore / Config.moodScoreThreshold);

            // Overconfident 확률 (matchScore 0→0.5→1 구간 선형보간)
            float pOver = matchScore <= 0.5f
                ? Mathf.Lerp(Config.moodOverconfidentProbAtZero, Config.moodOverconfidentProbAtMid, matchScore * 2f)
                : Mathf.Lerp(Config.moodOverconfidentProbAtMid,  Config.moodOverconfidentProbAtMax, (matchScore - 0.5f) * 2f);

            // Confident 확률 (Overconfident의 미러)
            float pConf = matchScore <= 0.5f
                ? Mathf.Lerp(Config.moodOverconfidentProbAtMax,  Config.moodOverconfidentProbAtMid, matchScore * 2f)
                : Mathf.Lerp(Config.moodOverconfidentProbAtMid,  Config.moodOverconfidentProbAtZero, (matchScore - 0.5f) * 2f);

            // 설정 오입력(pOver+pConf > 1)으로 기본 기분 확률이 음수가 되지 않도록 방어
            float reserved = Mathf.Clamp01(pOver + pConf);
            float pBaseEach = (1f - reserved) / 3f;

            float rand = Random.value;
            AdventurerMood mood;

            if (rand < pOver)
                mood = AdventurerMood.Overconfident;
            else if (rand < pOver + pConf)
                mood = AdventurerMood.Confident;
            else if (rand < pOver + pConf + pBaseEach)
                mood = AdventurerMood.Normal;
            else if (rand < pOver + pConf + pBaseEach * 2f)
                mood = AdventurerMood.Moody;
            else
                mood = AdventurerMood.Depressed;

            Log.Info($"[Mood] {adventurer.Name} → {mood} (matchScore={matchScore:F2})");
            return mood;
        }

        /// <summary>
        /// 이벤트 단위로 한 번 굴려 기분 배율을 반환한다.
        /// </summary>
        private float RollMoodMultiplier(AdventurerMood mood)
        {
            return mood switch
            {
                AdventurerMood.Normal        => Random.Range(Config.moodNormalRange.x,        Config.moodNormalRange.y),
                AdventurerMood.Moody         => Random.value < 0.5f
                                                    ? Random.Range(Config.moodMoodyLowRange.x,  Config.moodMoodyLowRange.y)
                                                    : Random.Range(Config.moodMoodyHighRange.x, Config.moodMoodyHighRange.y),
                AdventurerMood.Depressed     => Random.Range(Config.moodDepressedRange.x,     Config.moodDepressedRange.y),
                AdventurerMood.Overconfident => Random.Range(Config.moodOverconfidentRange.x, Config.moodOverconfidentRange.y),
                AdventurerMood.Confident     => Random.Range(Config.moodConfidentRange.x,     Config.moodConfidentRange.y),
                _                            => 1f
            };
        }

        #endregion
    }
}
