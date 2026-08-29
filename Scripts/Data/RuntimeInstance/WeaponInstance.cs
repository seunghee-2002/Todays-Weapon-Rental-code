// Scripts/Runtime/WeaponInstance.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace TodaysWeaponRental
{
    [Serializable]
    public class WeaponInstance
    {
        public WeaponData weaponData;
        public string instanceID;
        public List<WeaponEffect> effects = new List<WeaponEffect>();

        public Grade currentGrade { get; private set; }
        public int enforceLevel;

        public int purchasedDay;
        public string rentedToAdventurerID;
        public int totalRentalCount;

        public bool isRented;
        public int rerollCount;
        public bool isExtraRerolled;
        public bool hasExtraRerollCharge;
        public bool isDefaultWeapon;

        public WeaponInstance(WeaponData data, bool isDefault = false)
        {
            weaponData = data;
            instanceID = Guid.NewGuid().ToString();
            
            currentGrade = data.baseGrade;
            enforceLevel = 0;

            purchasedDay = 1;
            isRented = false;
            rentedToAdventurerID = null;
            totalRentalCount = 0;
            rerollCount = 0;
            isDefaultWeapon = isDefault;

            effects = GenerateEffects(data.baseGrade);
            CheckEnforceLevel();
        }

        public int MaxEnforceLevel => effects.Count;
        public int MaxRerollCount => ConfigManager.Instance.Weapon.MaxRerollCount + (LegacyManager.Instance?.GetRerollCountBonus() ?? 0);
        /// <summary>UI 표시용 최대 재부여 횟수. 유산으로 산 추가 1회를 포함한다</summary>
        public int DisplayMaxRerollCount => Mathf.Max(MaxRerollCount, rerollCount) + (hasExtraRerollCharge ? 1 : 0);
        public bool CanEnforce      => enforceLevel < MaxEnforceLevel && !isDefaultWeapon;
        public bool CanEvolve       => enforceLevel >= MaxEnforceLevel && currentGrade < Grade.Legendary && !isDefaultWeapon;
        public bool CanReroll       => (rerollCount < MaxRerollCount || hasExtraRerollCharge) && !isDefaultWeapon;
        public bool CanExtraReroll  => !CanReroll && !isExtraRerolled && !isDefaultWeapon;

        public void RentTo(string adventurerID)
        {
            isRented = true;
            rentedToAdventurerID = adventurerID;
            totalRentalCount++;
        }

        public void Return()
        {
            isRented = false;
            rentedToAdventurerID = null;
        }

        public string Enforce()
        {
            if (!CanEnforce) return null;

            var candidates = effects
                .Where(e => e.effectData != null && e.currentValue < e.effectData.baseValueRange.y)
                .ToList();

            if (candidates.Count == 0)
                candidates = effects;

            if (candidates.Count == 0) return null;

            var target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var effectData = target.effectData;
            if (effectData != null)
            {
                target.currentValue = WeaponEffect.IsIntegerType(effectData.effectType)
                    ? Mathf.RoundToInt(effectData.baseValueRange.y)
                    : effectData.baseValueRange.y;
            }

            enforceLevel++;
            return target.effectDataID;
        }

        /// <summary>
        /// 등급·레벨 갱신 + 신규 효과 추가. 기존 효과의 등급 업그레이드는
        /// `BlacksmithManager.ApplyEvolveUpgrades`가 사전에 처리한다. 매니저 경유 호출 필수.
        /// </summary>
        public void Evolve()
        {
            if (!CanEvolve) return;

            currentGrade++;
            enforceLevel = 0;
            rerollCount = 0;

            AddRandomEffect();
            CheckEnforceLevel();
        }

        /// <summary>
        /// 잠기지 않은 슬롯의 새 효과를 생성해 반환. effects는 변경하지 않음.
        /// 사용자가 확인하면 ApplyRerolledEffects()를 호출해 실제 적용.
        /// </summary>
        public void AddExtraRerollCharge()
        {
            hasExtraRerollCharge = true;
            isExtraRerolled = true;
        }

        public List<WeaponEffect> Reroll(HashSet<int> lockedIndices, bool free = false)
        {
            if (!free && !CanReroll) return null;

            int targetCount = effects.Count;

            var lockedEffects = effects
                .Where((e, i) => lockedIndices.Contains(i))
                .ToList();

            var newEffects = new List<WeaponEffect>();
            int attempts = 0;
            int newEffectCount = targetCount - lockedEffects.Count;

            while (newEffects.Count < newEffectCount && attempts < 50)
            {
                attempts++;
                var existing = lockedEffects.Concat(newEffects).ToList();
                var rolled = RollOneEffect(currentGrade, existing);
                if (rolled != null) newEffects.Add(rolled);
            }

            var result = new WeaponEffect[targetCount];
            var newEffectQueue = new Queue<WeaponEffect>(newEffects);

            for (int i = 0; i < targetCount; i++)
            {
                if (lockedIndices.Contains(i))
                    result[i] = effects[i];
                else
                    result[i] = newEffectQueue.Count > 0 ? newEffectQueue.Dequeue() : null;
            }

            // 무료 재부여(떠돌이 대장장이)는 재부여 횟수도 충전분도 쓰지 않는다
            if (!free)
            {
                rerollCount++;
                hasExtraRerollCharge = false;
            }

            return result.Where(e => e != null).ToList();
        }

        /// <summary>
        /// Reroll()이 반환한 새 효과를 실제로 적용.
        /// </summary>
        public void ApplyRerolledEffects(List<WeaponEffect> newEffects)
        {
            effects = newEffects;
            CheckEnforceLevel();
        }

        private Grade RollGrade(float[] prob)
        {
            float rand = UnityEngine.Random.Range(0f, 1f);
            float cumulative = 0f;

            for (int i = 0; i < prob.Length; i++)
            {
                cumulative += prob[i];
                if (rand < cumulative) return (Grade)i;
            }

            // 정상 확률표(합=1)는 부동소수 오차가 아니면 도달하지 않는다.
            // 합이 1 미만인 깨진 표가 최고 등급으로 흐르지 않도록 최저 등급으로 보수적 폴백
            if (Mathf.Abs(cumulative - 1f) > 0.001f)
                Log.Error($"[WeaponInstance] 확률표 합계 오류: sum={cumulative} - Common으로 폴백");
            return Grade.Common;
        }

        private WeaponEffect RollOneEffect(Grade weaponGrade, List<WeaponEffect> existing)
        {
            var config = ConfigManager.Instance.Weapon;
            float[] prob = weaponGrade switch
            {
                Grade.Common    => config.commonProb,
                Grade.Uncommon  => config.uncommonProb,
                Grade.Rare      => config.rareProb,
                Grade.Epic      => config.epicProb,
                Grade.Legendary => config.legendaryProb,
                _               => config.commonProb
            };

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Grade rolledGrade = RollGrade(prob);
                var pool = DataManager.Instance.GetWeaponEffectsByGrade(rolledGrade);
                if (pool == null || pool.Count == 0) continue;

                var candidates = pool
                    .Where(d => !existing.Any(e => e.effectData != null && e.effectData.effectType == d.effectType))
                    .ToList();
                if (candidates.Count == 0) continue;

                float total = 0f;
                foreach (var d in candidates) total += d.weight;

                float rand = UnityEngine.Random.Range(0f, total);
                float cumulative = 0f;

                foreach (var d in candidates)
                {
                    cumulative += d.weight;
                    // 경계 미포함(<) - rand가 정확히 0일 때 weight 0 후보 당첨 방지
                    if (rand < cumulative) return new WeaponEffect(d);
                }
            }

            return null;
        }

        private List<WeaponEffect> GenerateEffects(Grade weaponGrade)
        {
            var result = new List<WeaponEffect>();
            if (isDefaultWeapon) return result;

            var config = ConfigManager.Instance.Weapon;
            int count = weaponGrade switch
            {
                Grade.Common    => config.commonEffectCount,
                Grade.Uncommon  => config.uncommonEffectCount,
                Grade.Rare      => config.rareEffectCount,
                Grade.Epic      => config.epicEffectCount,
                Grade.Legendary => config.legendaryEffectCount,
                _               => 1
            };

            for (int i = 0; i < count; i++)
            {
                var effect = RollOneEffect(weaponGrade, result);
                if (effect != null) result.Add(effect);
            }

            return result;
        }

        private void AddRandomEffect()
        {
            var effect = RollOneEffect(currentGrade, effects);
            if (effect == null) return;

            effects.Add(effect);
        }

        /// <summary>
        /// 등급을 1 낮추고 마지막 부가효과를 제거한 뒤 enforceLevel을 재계산한다.
        /// </summary>
        public void Downgrade()
        {
            if ((int)currentGrade <= 0) return;
            currentGrade--;
            if (effects.Count > 0)
                effects.RemoveAt(effects.Count - 1);
            CheckEnforceLevel();
        }

        private void CheckEnforceLevel()
        {
            int count = 0;
            foreach (var effect in effects)
            {
                var effectData = effect.effectData;
                if (effectData == null) continue;
                float maxValue = WeaponEffect.IsIntegerType(effectData.effectType)
                    ? Mathf.Round(effectData.baseValueRange.y)
                    : effectData.baseValueRange.y;
                if (Mathf.Approximately(effect.currentValue, maxValue))
                    count++;
            }
            enforceLevel = count;
        }

        // 세이브 복원용 — 도메인 매니저가 weaponData와 effects를 외부 주입
        public WeaponInstance(WeaponData data, List<WeaponEffect> effects, WeaponInstanceSaveData saveData)
        {
            weaponData = data;
            instanceID = saveData.instanceID;
            this.effects = effects ?? new List<WeaponEffect>();

            currentGrade = saveData.currentGrade;
            enforceLevel = saveData.enforceLevel;

            purchasedDay          = saveData.purchasedDay;
            isRented              = saveData.isRented;
            rerollCount           = saveData.rerollCount;
            isExtraRerolled       = saveData.isExtraRerolled;
            hasExtraRerollCharge  = saveData.hasExtraRerollCharge;
            isDefaultWeapon       = saveData.isDefaultWeapon;
            rentedToAdventurerID = saveData.rentedToAdventurerID;
            totalRentalCount = saveData.totalRentalCount;
        }
    }

    [Serializable]
    public class WeaponInstanceSaveData
    {
        public string weaponDataID;
        public string instanceID;
        public List<WeaponEffectSaveData> effects;

        public Grade currentGrade;
        public int enforceLevel;
        
        public int purchasedDay;
        public bool isRented;
        public int rerollCount;
        public bool isExtraRerolled;
        public bool hasExtraRerollCharge;
        public bool isDefaultWeapon;
        public string rentedToAdventurerID;
        public int totalRentalCount;
    }
}
