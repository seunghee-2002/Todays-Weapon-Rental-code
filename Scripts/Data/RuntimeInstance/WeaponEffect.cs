using System;
using UnityEngine;

namespace TodaysWeaponRental
{
    [Serializable]
    public class WeaponEffect
    {
        [NonSerialized] public WeaponEffectData effectData;
        public string effectDataID;

        public float baseValue;
        public float currentValue;

        public WeaponEffect(WeaponEffectData data)
        {
            effectData   = data;
            effectDataID = data.StaticID;

            float rolled = UnityEngine.Random.Range(data.baseValueRange.x, data.baseValueRange.y);
            baseValue    = IsIntegerType(data.effectType) ? Mathf.RoundToInt(rolled) : rolled;
            currentValue = baseValue;

            // 생성 시 이미 max이면 max값으로 고정
            float maxValue = IsIntegerType(data.effectType)
                ? Mathf.RoundToInt(data.baseValueRange.y)
                : data.baseValueRange.y;
            if (currentValue >= maxValue)
                currentValue = maxValue;
        }

        // 세이브 복원용 — 도메인 매니저가 effectData를 외부 주입
        public WeaponEffect(WeaponEffectSaveData saveData, WeaponEffectData data)
        {
            effectData   = data;
            effectDataID = saveData.effectDataID;
            baseValue    = saveData.baseValue;
            currentValue = saveData.currentValue;
        }

        // 런타임 복사용 (진화 시뮬레이션 등)
        public WeaponEffect(WeaponEffect source)
        {
            effectData   = source.effectData;
            effectDataID = source.effectDataID;
            baseValue    = source.baseValue;
            currentValue = source.currentValue;
        }

        public static bool IsIntegerType(WeaponEffectType type)
        {
            return type == WeaponEffectType.MaterialAmountBonus
                || type == WeaponEffectType.EnforceMaterialBonus
                || type == WeaponEffectType.EventCountBonus
                || type == WeaponEffectType.RetreatPrevention
                || type == WeaponEffectType.StatBonus
                || type == WeaponEffectType.AllStatBonus;
        }
    }

    [Serializable]
    public class WeaponEffectSaveData
    {
        public WeaponEffectType effectType;
        public string effectDataID;
        public Grade grade;
        public float baseValue;
        public float currentValue;
        public int targetGrade;
        public int targetStat;
        public int targetArmorType;
        public int targetThreshold;
    }
}
