using System.Collections.Generic;
using UnityEngine;
using LayerLab.ArtMaker;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험가 외형 랜덤 생성 유틸리티
    /// AppearanceGeneratorData의 인덱스 풀과 확률 규칙을 기반으로 AdventurerAppearanceData 생성
    /// </summary>
    public static class AppearanceGenerator
    {
        #region 장착 확률 상수

        // Helmet 장착 확률
        private const float HelmetChanceStr = 0.80f;
        private const float HelmetChanceOther = 0.50f;

        // Back 장착 확률
        private const float BackChanceDex = 0.80f;
        private const float BackChanceStr = 0.10f;
        private const float BackChanceOther = 0.30f;

        // Boots 미착용 확률 (맨발)
        private const float BootsSkipChanceLuk = 0.45f;
        private const float BootsSkipChanceOther = 0.12f;

        // Beard 장착 확률 (남성 전용)
        private const float BeardChanceMaleInt = 0.80f;
        private const float BeardChanceMaleOther = 0.20f;

        // Eyewear 장착 확률
        private const float EyewearChance = 0.10f;

        #endregion

        #region 외형 생성

        /// <summary>
        /// 랜덤 외형 생성 (일반 모험가용)
        /// </summary>
        public static AdventurerAppearanceData Generate(
            AppearanceGeneratorData generatorData,
            Gender gender,
            AdventurerJobType jobType)
        {
            var parts = new Dictionary<PartsType, int>();

            // 필수 파츠
            parts[PartsType.Skin] = PickRandom(generatorData.GetSkinPool());
            parts[PartsType.Eyes] = PickRandom(generatorData.GetEyesPool());
            parts[PartsType.Mouth] = PickRandom(generatorData.GetMouthPool());
            parts[PartsType.Brow] = PickRandom(generatorData.GetBrowPool(gender));
            parts[PartsType.Hair_Short] = PickRandom(generatorData.GetHairPool(gender));
            parts[PartsType.Top] = PickRandom(generatorData.GetTopPool(gender, jobType));
            parts[PartsType.Bottom] = PickRandom(generatorData.GetBottomPool(gender, jobType));
            parts[PartsType.Gloves] = PickRandom(generatorData.GetGlovesPool(gender, jobType));

            // Boots (LUK 45% / 나머지 12% 미착용)
            float bootsSkipChance = jobType == AdventurerJobType.LUK ? BootsSkipChanceLuk : BootsSkipChanceOther;
            parts[PartsType.Boots] = ShouldSkip(bootsSkipChance)
                ? -1
                : PickRandom(generatorData.GetBootsPool(gender, jobType));

            // Helmet (직업군별 확률)
            float helmetChance = jobType == AdventurerJobType.STR ? HelmetChanceStr : HelmetChanceOther;
            parts[PartsType.Helmet] = ShouldEquip(helmetChance)
                ? PickRandom(generatorData.GetHelmetPool(gender, jobType))
                : -1;

            // Back (직업군별 확률)
            float backChance = jobType switch
            {
                AdventurerJobType.DEX => BackChanceDex,
                AdventurerJobType.STR => BackChanceStr,
                _ => BackChanceOther
            };
            parts[PartsType.Back] = ShouldEquip(backChance)
                ? PickRandom(generatorData.GetBackPool(gender, jobType))
                : -1;

            // Beard (남성 전용)
            if (gender == Gender.Male)
            {
                float beardChance = jobType == AdventurerJobType.INT ? BeardChanceMaleInt : BeardChanceMaleOther;
                parts[PartsType.Beard] = ShouldEquip(beardChance)
                    ? PickRandom(generatorData.GetBeardPool())
                    : -1;
            }
            else
            {
                parts[PartsType.Beard] = -1;
            }

            // Eyewear (성별 무관, 10%)
            parts[PartsType.Eyewear] = ShouldEquip(EyewearChance)
                ? PickRandom(generatorData.GetEyewearPool())
                : -1;

            // Gear 미사용
            parts[PartsType.Gear_Left] = -1;
            parts[PartsType.Gear_Right] = -1;

            // 색상
            Color skinColor = generatorData.GetRandomSkinColor();
            Color hairColor = generatorData.GetRandomHairColor();
            Color beardColor = hairColor;
            Color browColor = new Color(hairColor.r * 0.8f, hairColor.g * 0.8f, hairColor.b * 0.8f);

            return AdventurerAppearanceData.FromPartsDictionary(parts, skinColor, hairColor, beardColor, browColor);
        }

        /// <summary>
        /// Named 모험가용: SO에 직접 입력된 고정 외형 그대로 반환
        /// </summary>
        public static AdventurerAppearanceData GenerateFromFixed(
            Dictionary<PartsType, int> fixedIndices,
            Color skinColor,
            Color hairColor,
            Color beardColor,
            Color browColor)
        {
            return AdventurerAppearanceData.FromPartsDictionary(
                fixedIndices, skinColor, hairColor, beardColor, browColor);
        }

        #endregion

        #region 내부 유틸리티

        private static int PickRandom(List<int> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                Log.Warn("[AppearanceGenerator] 파츠 풀이 비어있음. -1 반환.");
                return -1;
            }
            return pool[Random.Range(0, pool.Count)];
        }

        private static bool ShouldEquip(float chance) => Random.value < chance;
        private static bool ShouldSkip(float skipChance) => Random.value < skipChance;

        #endregion
    }
}