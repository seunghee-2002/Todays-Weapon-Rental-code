// Scripts/Systems/AdventureManager/AdventureManager.Sequence.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    public partial class AdventureManager
    {
        #region 이벤트 시퀀스 생성

        /// <summary>
        /// 던전 등급에 따라 이벤트 시퀀스를 생성합니다.
        /// 마지막 이벤트는 반드시 Boss 고정.
        /// </summary>
        private List<AdventureEvent> GenerateEventSequence(DungeonData dungeon, WeaponInstance weapon, float startTime)
        {
            int maxCount = GetMaxEventCount(dungeon.grade);
            if (QuestBoardManager.Instance != null &&
                QuestBoardManager.Instance.IsHighlightedDungeon(dungeon.StaticID))
            {
                maxCount = (maxCount - 1) * 2 + 1;
            }

            // EventCountBonus 효과 적용
            if (weapon?.effects != null)
            {
                int countBonus = 0;
                foreach (var effect in weapon.effects)
                {
                    if (effect?.effectData == null) continue;
                    if (effect.effectData.effectType == WeaponEffectType.EventCountBonus)
                        countBonus += Mathf.RoundToInt(effect.currentValue);
                }
                maxCount += Mathf.Clamp(countBonus, 0, Config.eventCountBonusMax);
            }

            var events = new List<AdventureEvent>();

            // 첫 이벤트: 입장 이벤트
            var entranceData = DataManager.Instance.GetDungeonEventByType(DungeonEventType.Entrance);
            if (entranceData != null)
                events.Add(new AdventureEvent(entranceData));
            else
                Log.Warn($"[AdventureManager] Entrance 이벤트 데이터가 없습니다.");

            // 중간 이벤트
            var pool = dungeon.eventPool
                .Where(e => e != null && e.eventType != DungeonEventType.Boss)
                .ToList();

            var adjustedPool = ApplyEventChanceBonuses(pool, weapon);

            for (int i = 0; i < maxCount - 1; i++)
            {
                var selected = SelectWeightedRandom(adjustedPool);
                if (selected != null)
                    events.Add(new AdventureEvent(selected));
            }

            // DoubleReward 효과
            if (weapon?.effects != null)
            {
                foreach (var effect in weapon.effects)
                {
                    if (effect?.effectData == null) continue;
                    if (effect.effectData.effectType == WeaponEffectType.DoubleReward
                        && Random.value <= effect.currentValue)
                    {
                        var extras = events
                            .Where(e => e.eventData.eventType != DungeonEventType.Entrance)
                            .Select(e => new AdventureEvent(e.eventData))
                            .ToList();
                        events.AddRange(extras);
                        break;
                    }
                }
            }

            // 마지막: Boss
            var bossEvent = dungeon.eventPool
                .FirstOrDefault(e => e != null && e.eventType == DungeonEventType.Boss);
            if (bossEvent != null)
                events.Add(new AdventureEvent(bossEvent));
            else
                Log.Warn($"[AdventureManager] {dungeon.dungeonName}의 eventPool에 Boss 이벤트가 없습니다.");

            return events;
        }

        /// <summary>
        /// 등급별 최대 이벤트 수 반환 (Common=3, Uncommon=4, Rare=5, Epic=7, Legendary=10)
        /// </summary>
        private int GetMaxEventCount(Grade grade)
        {
            var counts = Config.maxEventCountByGrade;
            int index = (int)grade;
            if (counts == null || index < 0 || index >= counts.Length) return 3;
            return counts[index];
        }

        /// <summary>
        /// weapon.effects의 이벤트 확률 보정을 반영한 임시 풀 반환
        /// 원본 DungeonEventData는 건드리지 않고 probability만 보정한 래퍼를 사용하지 않으므로,
        /// weight 합산을 위해 Dictionary로 보정값을 누적한 뒤 SelectWeightedRandom에서 사용
        /// </summary>
        private List<(DungeonEventData data, float weight)> ApplyEventChanceBonuses(
            List<DungeonEventData> pool, WeaponInstance weapon)
        {
            var result = new List<(DungeonEventData data, float weight)>();
            foreach (var e in pool)
                result.Add((e, e.probability));

            if (weapon?.effects == null) return result;

            foreach (var effect in weapon.effects)
            {
                DungeonEventType targetType;

                switch (effect.effectData.effectType)
                {
                    case WeaponEffectType.RestChanceBonus:
                        targetType = DungeonEventType.Rest;         break;
                    case WeaponEffectType.TreasureChestChanceBonus:
                        targetType = DungeonEventType.TreasureChest; break;
                    case WeaponEffectType.RareDropChanceBonus:
                        targetType = DungeonEventType.RareDrop;     break;
                    default:
                        continue;
                }

                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i].data.eventType == targetType)
                        result[i] = (result[i].data, result[i].weight * (1f + effect.currentValue));
                }
            }

            return result;
        }

        /// <summary>
        /// (data, weight) 튜플 리스트 기반 가중치 랜덤 선택
        /// </summary>
        private DungeonEventData SelectWeightedRandom(List<(DungeonEventData data, float weight)> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            float total = 0f;
            foreach (var (data, weight) in pool) total += weight;

            float rand = Random.Range(0f, total);
            float cumulative = 0f;

            foreach (var (data, weight) in pool)
            {
                cumulative += weight;
                // 경계 미포함(<) - rand가 정확히 0일 때 weight 0 후보 당첨 방지
                if (rand < cumulative) return data;
            }

            return pool[0].data;
        }

        #endregion
    }
}
