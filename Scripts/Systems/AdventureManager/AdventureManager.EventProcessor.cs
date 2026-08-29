// Scripts/Systems/AdventureManager/AdventureManager.EventProcessor.cs
using System;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    public partial class AdventureManager
    {
        #region 이벤트 업데이트

        /// <summary>
        /// 진행 중인 모든 모험의 이벤트를 처리합니다.
        /// TimeManager.OnTimeChanged에서 매 인게임 1시간마다 호출됩니다.
        /// </summary>
        private void UpdateAdventures()
        {
            for (int i = ongoingAdventures.Count - 1; i >= 0; i--)
            {
                var adventure = ongoingAdventures[i];
                if (!adventure.isCompleted)
                    TryProcessNextEvent(adventure);
            }
        }

        private void TryProcessNextEvent(AdventureInstance adventure)
        {
            var prog = adventure.progress;
            int idx = prog.currentEventIndex;
            if (idx >= prog.events.Count) return;   // 이미 완료(안전장치)

            var evt = prog.events[idx];

            // 단계 1: 현재 이벤트 시작 (startTime 1회 설정). 결과는 모험 시작 시 이미 확정됨.
            if (evt.startTime < 0f)
            {
                evt.startTime = TimeManager.Instance.CurrentTime;
                OnAdventureEventTriggerStarted?.Invoke(adventure, evt);
                SaveOngoingAdventures();
                return;
            }

            // 단계 2: duration 경과 후 완료
            float duration = GetEventDuration(evt);
            if (TimeManager.Instance.CurrentTime < evt.startTime + duration) return;

            prog.currentEventIndex++;

            // 마지막(종료) 이벤트 완료 → 모험 완료 + 보상 확정 (보상 누적은 시작 시 이미 끝남)
            // CompleteAdventure 내부에서 SaveOngoingAdventures 호출됨.
            if (prog.currentEventIndex >= prog.events.Count)
            {
                CompleteAdventure(adventure);
                return;
            }

            // 다음 이벤트 즉시 시작 (3분 gap 제거)
            var next = prog.events[prog.currentEventIndex];
            next.startTime = TimeManager.Instance.CurrentTime;
            OnAdventureEventTriggerStarted?.Invoke(adventure, next);
            SaveOngoingAdventures();
        }

        /// <summary>
        /// 모험 시작 시 events[fromIndex..]를 순차 시뮬레이션으로 전체 해결한다.
        /// 각 이벤트의 result를 이벤트와 1:1로 확정하고 보상/누적/삽입을 즉시 반영하며,
        /// 종료 이벤트(Retreat/Return) 도달 시 뒤따르는 미실행 이벤트를 잘라낸다.
        /// 플레이어 지급/완료(CompleteAdventure)는 하지 않는다 — 런타임이 시간 도달 시 수행.
        /// 호출 순서(StartAdventure 최후미)가 중요: Calculate*가 읽는 charm/아이템/mood/cumulativeModifier가
        /// 소비·결정된 뒤여야 per-event 모델과 동일한 상태를 본다.
        /// </summary>
        private void ResolveSequence(AdventureInstance adventure, int fromIndex)
        {
            var prog = adventure.progress;
            int i = fromIndex;
            while (i < prog.events.Count)
            {
                var evt = prog.events[i];
                if (evt.result == null)
                {
                    // 삽입 위치(currentEventIndex+1)의 기준점으로 커서 사용
                    prog.currentEventIndex = i;
                    evt.cachedDurationMultiplier = CalculateDurationMultiplier(adventure);
                    evt.result = CalculatePendingResult(adventure, evt);   // 삽입/isDeath 결정
                    bool terminal = ApplyPendingResult(adventure, evt);    // 누적 (완료 안 함)
                    if (terminal)
                    {
                        // 종료 이후 미실행 이벤트 제거 → 마지막 이벤트 = 종료 이벤트
                        if (i + 1 < prog.events.Count)
                            prog.events.RemoveRange(i + 1, prog.events.Count - (i + 1));
                        break;
                    }
                }
                i++;
            }
            prog.currentEventIndex = fromIndex;   // 런타임은 fromIndex부터 전진
        }

        /// <summary>
        /// 이벤트 1건의 진행 시간(인게임 분)을 계산. TryProcessNextEvent의 완료 판정과
        /// 모험진행 카드 연출의 진행률(frac = (CurrentTime - startTime) / duration) 계산에 함께 쓰인다.
        /// </summary>
        public float GetEventDuration(AdventureEvent evt)
        {
            if (evt == null) return 0f;
            float baseInterval  = evt.eventData != null && evt.eventData.intervalHours > 0f
                ? evt.eventData.intervalHours : Config.eventIntervalHours;
            float intervalHours = baseInterval * (LegacyManager.Instance?.GetAdventureSpeedMultiplier() ?? 1f);
            return Mathf.Floor(intervalHours * evt.cachedDurationMultiplier * 60f / 3f) * 3f;
        }

        /// <summary>
        /// 현재 이벤트 직후에 새 이벤트 삽입
        /// </summary>
        private void InsertEventAfterCurrent(AdventureInstance adventure, DungeonEventData eventData)
        {
            var newEvt = new AdventureEvent(eventData);
            int insertIdx = adventure.progress.currentEventIndex + 1;
            adventure.progress.events.Insert(insertIdx, newEvt);
        }

        /// <summary>
        /// 9단계 판정 고정: 튜토리얼 중 고정 던전 A/B로 향하는 모험이면 전투/보스 성공 여부를 강제한다.
        /// (던전 A=성공, 던전 B=실패). 다른 매니저 내부 튜토리얼 가드(BlacksmithManager.ApplyCostReduction 등)와 동일 패턴.
        /// 판정은 모험 시작(ResolveSequence) 시점에 baked되므로, A는 6-E·B는 8단계 출발 시 이 가드가 걸린다.
        /// </summary>
        private bool TryGetTutorialForcedBattleSuccess(AdventureInstance adventure, out bool forceSuccess)
        {
            forceSuccess = false;
            if (!(TutorialManager.Instance?.IsTutorialActive ?? false)) return false;

            var config = ConfigManager.Instance?.Tutorial;
            string dungeonID = adventure?.dungeon?.StaticID;
            if (config == null || string.IsNullOrEmpty(dungeonID)) return false;

            if (dungeonID == config.TutorialDungeonAID) { forceSuccess = true;  return true; }
            if (dungeonID == config.TutorialDungeonBID) { forceSuccess = false; return true; }
            return false;
        }

        private EventResult CalculatePendingResult(AdventureInstance adventure, AdventureEvent evt)
        {
            var type = evt.eventData.eventType;

            switch (type)
            {
                case DungeonEventType.Entrance:     return CalculatePendingEntrance(type);
                case DungeonEventType.TreasureChest:return CalculatePendingTreasureChest(adventure, type);
                case DungeonEventType.RareDrop:     return CalculatePendingRareDrop(adventure, type);
                case DungeonEventType.Rest:         return CalculatePendingRest(type);
                case DungeonEventType.Trap:         return CalculatePendingTrap(adventure, type);
                case DungeonEventType.TrapEvade:    return CalculatePendingTrapEvade(type);
                case DungeonEventType.Battle:
                case DungeonEventType.MiniBoss:     return CalculatePendingBattleOrMiniBoss(adventure, evt, type);
                case DungeonEventType.Boss:         return CalculatePendingBoss(adventure, evt, type);
                case DungeonEventType.Retreat:      return CalculatePendingRetreat(type);
                case DungeonEventType.Retry:        return CalculatePendingRetry(adventure, type);
                case DungeonEventType.Return:       return CalculatePendingReturn(type);
                case DungeonEventType.Protection:   return CalculatePendingProtection(type);
                default:
                    Log.Warn($"[AdventureManager] 등록되지 않은 이벤트 타입: {type}");
                    return new EventResult { eventType = type, isSuccess = false };
            }
        }

        #region CalculatePendingResult 개별 메서드

        private EventResult CalculatePendingEntrance(DungeonEventType type)
        {
            return new EventResult { eventType = type, isSuccess = true };
        }

        private EventResult CalculatePendingTreasureChest(AdventureInstance adventure, DungeonEventType type)
        {
            float treasureMultiplier = Config.treasureChestRewardMultiplier;
            float bonusMultiplier = 0f;
            foreach (var effect in adventure.weapon.effects)
                if (effect.effectData.effectType == WeaponEffectType.TreasureGoldBonus)
                    bonusMultiplier += effect.currentValue;

            var (baseGold, bonusGold) = CalculateEventGold(adventure, treasureMultiplier, bonusMultiplier);
            return new EventResult { eventType = type, isSuccess = true, goldReward = baseGold, bonusGold = bonusGold, successRateAtTime = 1f };
        }

        private EventResult CalculatePendingRareDrop(AdventureInstance adventure, DungeonEventType type)
        {
            var (materials, bonusMaterials) = CalculateRareDropMaterials(adventure);
            return new EventResult { eventType = type, isSuccess = true, materialDrops = materials, bonusMaterials = bonusMaterials, successRateAtTime = 1f };
        }

        private EventResult CalculatePendingRest(DungeonEventType type)
        {
            return new EventResult { eventType = type, isSuccess = true, successRateAtTime = 1f };
        }

        private EventResult CalculatePendingTrap(AdventureInstance adventure, DungeonEventType type)
        {
            // 이벤트 단위로 1회 굴린 뒤 모든 TrapNegation에 같은 배율 적용 (절반 강도)
            float rawMult  = RollMoodMultiplier(adventure.mood);
            float halfMult = 1f + (rawMult - 1f) * Config.moodHalfStrength;

            // 소스별로 따로 굴리지 않고 합성 확률 1회로 굴려야 툴팁에 표시하는 값과 실제가 일치한다.
            // 탈출 로프는 mood 영향을 받지 않는다 (헬퍼가 TrapNegation에만 배율을 곱한다).
            float evadeChance = CalculateTrapEvadeChance(
                adventure.adventurer, adventure.weapon, adventure.escapeRopeBonus, halfMult);
            bool negated = UnityEngine.Random.value <= evadeChance;

            if (negated)
            {
                // Protection/Retry와 동일한 패턴으로 TrapEvade 이벤트 삽입
                var evadeData = DataManager.Instance.GetDungeonEventByType(DungeonEventType.TrapEvade);
                if (evadeData != null) InsertEventAfterCurrent(adventure, evadeData);
            }

            return new EventResult
            {
                eventType         = type,
                isSuccess         = negated,
                successRateAtTime = evadeChance,
                moodMultiplier    = halfMult
            };
        }

        private EventResult CalculatePendingTrapEvade(DungeonEventType type)
        {
            return new EventResult { eventType = type, isSuccess = true, successRateAtTime = 1f };
        }

        private EventResult CalculatePendingBattleOrMiniBoss(AdventureInstance adventure, AdventureEvent evt, DungeonEventType type)
        {
            float rate = CalculateEventSuccessRate(adventure, evt.eventData, out float moodMult);
            bool success = UnityEngine.Random.value <= rate;
            if (TryGetTutorialForcedBattleSuccess(adventure, out bool forcedBattle)) success = forcedBattle;   // 9단계 판정 고정(던전 A 성공/B 실패)
            float typeMultiplier = type == DungeonEventType.MiniBoss
                ? Config.miniBossRewardMultiplier
                : Config.battleRewardMultiplier;

            // 이벤트별 골드 보너스 적용
            if (type == DungeonEventType.Battle)
            {
                foreach (var effect in adventure.weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.BattleGoldBonus)
                        typeMultiplier += effect.currentValue;
            }
            else // MiniBoss
            {
                foreach (var effect in adventure.weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.MiniBossGoldBonus)
                        typeMultiplier += effect.currentValue;
            }

            int baseGold = 0;
            int bonusGold = 0;
            if (success)
            {
                // 기본 배율만 적용
                float baseMultiplier = type == DungeonEventType.MiniBoss
                    ? Config.miniBossRewardMultiplier
                    : Config.battleRewardMultiplier;
                var (_baseGold, _bonusGold) = CalculateEventGold(adventure, baseMultiplier, typeMultiplier - baseMultiplier);
                baseGold = _baseGold;
                bonusGold = _bonusGold;
            }
            var result = new EventResult
            {
                eventType = type, isSuccess = success,
                goldReward = baseGold + bonusGold,
                bonusGold = bonusGold,
                successRateAtTime = rate,
                moodMultiplier = moodMult
            };

            if (!success)
            {
                if (adventure.HasProtection)
                {
                    var protectionData = DataManager.Instance.GetDungeonEventByType(DungeonEventType.Protection);
                    if (protectionData != null) InsertEventAfterCurrent(adventure, protectionData);
                }
                else
                {
                    adventure.isDeath = RollDeath(adventure, out bool survived);
                    result.survivedByStrength = survived;
                    InsertRetreatEvent(adventure);
                }
            }
            return result;
        }

        private EventResult CalculatePendingBoss(AdventureInstance adventure, AdventureEvent evt, DungeonEventType type)
        {
            float rate = CalculateEventSuccessRate(adventure, evt.eventData, out float moodMult);
            bool success = UnityEngine.Random.value <= rate;
            if (TryGetTutorialForcedBattleSuccess(adventure, out bool forcedBoss)) success = forcedBoss;   // 9단계 판정 고정(던전 A 성공/B 실패)
            var result = new EventResult { eventType = type, isSuccess = success, successRateAtTime = rate, moodMultiplier = moodMult };

            if (success)
            {
                // 대성공 판정을 보상 계산보다 먼저 수행해 재료/골드 배수가 실제 지급에 반영되게 한다.
                // 판정 결과는 EventResult에 저장하고 ApplyResultBoss는 재판정 없이 이 값을 사용한다.
                bool isGreatSuccess = CalculateGreatSuccess(adventure.dungeon, adventure.adventurer);
                result.isGreatSuccess = isGreatSuccess;

                float bossMultiplier = Config.bossRewardMultiplier;
                float bonusMultiplier = 0f;
                foreach (var effect in adventure.weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.BossGoldBonus)
                        bonusMultiplier += effect.currentValue;

                var (gold, bonusGold) = CalculateEventGold(adventure, bossMultiplier, bonusMultiplier);
                var (materials, bonusMaterials) = CalculateMaterialDropsWithBonus(adventure, isGreatSuccess);

                if (isGreatSuccess)
                {
                    gold      = Mathf.RoundToInt(gold * Config.greatSuccessGoldMultiplier);
                    bonusGold = Mathf.RoundToInt(bonusGold * Config.greatSuccessGoldMultiplier);
                }

                result.goldReward = gold;
                result.bonusGold = bonusGold;
                result.materialDrops = materials;
                result.bonusMaterials = bonusMaterials;

                var returnData = DataManager.Instance.GetDungeonEventByType(DungeonEventType.Return);
                if (returnData != null) InsertEventAfterCurrent(adventure, returnData);
            }
            else
            {
                if (adventure.HasProtection)
                {
                    result.protectionActivated = true;
                    var retryData = DataManager.Instance.GetDungeonEventByType(DungeonEventType.Retry);
                    if (retryData != null) InsertEventAfterCurrent(adventure, retryData);
                }
                else
                {
                    adventure.isDeath = RollDeath(adventure, out bool survived);
                    result.survivedByStrength = survived;
                    InsertRetreatEvent(adventure);
                }
            }
            return result;
        }

        /// <summary>
        /// 전투/보스 패배 후의 사망 굴림. 사망이 떠도 STR 기반으로 1회 재굴림해 살아남을 수 있다
        /// (전투 실패 자체는 뒤집지 않는다).
        /// 9단계: 튜토리얼 고정 모험(A/B)은 사망 금지(기획: B는 실패하되 사망하지 않음).
        /// </summary>
        private bool RollDeath(AdventureInstance adventure, out bool survivedByStrength)
        {
            survivedByStrength = false;
            if (TryGetTutorialForcedBattleSuccess(adventure, out _)) return false;

            float deathRate = CalculateDeathRate(
                adventure.adventurer, adventure.weapon, adventure.dungeon, adventure.deathWardBonus);
            if (UnityEngine.Random.value > deathRate) return false;

            float rerollChance = GetStrengthSurvivalChance(adventure.adventurer);
            if (UnityEngine.Random.value <= rerollChance)
            {
                survivedByStrength = true;
                return false;
            }

            return true;
        }

        private EventResult CalculatePendingRetreat(DungeonEventType type)
        {
            return new EventResult { eventType = type, isSuccess = false };
        }

        private EventResult CalculatePendingRetry(AdventureInstance adventure, DungeonEventType type)
        {
            var bossData = DataManager.Instance.GetDungeonEventByType(DungeonEventType.Boss);
            if (bossData != null) InsertEventAfterCurrent(adventure, bossData);
            return new EventResult { eventType = type, isSuccess = true };
        }

        private EventResult CalculatePendingReturn(DungeonEventType type)
        {
            return new EventResult { eventType = type, isSuccess = true };
        }

        private EventResult CalculatePendingProtection(DungeonEventType type)
        {
            return new EventResult { eventType = type, isSuccess = true, protectionActivated = true };
        }

        private float CalculateDurationMultiplier(AdventureInstance adventure)
        {
            float reduction = 0f;
            if (adventure?.weapon?.effects != null)
                foreach (var effect in adventure.weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.AdventureTimeReduction)
                        reduction += effect.currentValue;

            float multiplier = 1f - Mathf.Min(reduction, Config.adventureTimeReductionMax);
            multiplier *= GetTraitDurationMultiplier(adventure.adventurer);
            // 신속한 신발: 진행 시간 배율 (기본 1f, 예: 0.8 = -20%)
            multiplier *= adventure.swiftShoesMultiplier;
            return multiplier;
        }

        #endregion

        /// <summary>결과 누적을 적용한다. 모험이 종료(Retreat/Return)되는 이벤트면 true 반환(완료는 호출측이 수행).</summary>
        private bool ApplyPendingResult(AdventureInstance adventure, AdventureEvent evt)
        {
            var result = evt.result;
            if (result == null) return false;

            switch (result.eventType)
            {
                case DungeonEventType.TreasureChest: ApplyResultTreasureChest(adventure, result); break;
                case DungeonEventType.RareDrop:      ApplyResultRareDrop(adventure, result);      break;
                case DungeonEventType.Rest:          ApplyResultRest(adventure);                   break;
                case DungeonEventType.Trap:          ApplyResultTrap(adventure, result);           break;
                case DungeonEventType.Battle:
                case DungeonEventType.MiniBoss:      ApplyResultBattleOrMiniBoss(adventure, result);break;
                case DungeonEventType.Boss:          ApplyResultBoss(adventure, result);           break;
                case DungeonEventType.Protection:    ApplyResultProtection(adventure);             break;
                case DungeonEventType.Retry:         ApplyResultRetry(adventure);                  break;
                case DungeonEventType.Retreat:       ApplyResultRetreat(adventure);    return true;
                case DungeonEventType.Return:        ApplyResultReturn(adventure);     return true;
                case DungeonEventType.TrapEvade:
                case DungeonEventType.Entrance:
                    break; // 별도 이벤트로 처리, 결과 없음
            }
            return false;
        }

        #region ApplyPendingResult 개별 메서드

        private void ApplyResultTreasureChest(AdventureInstance adventure, EventResult result)
        {
            adventure.accumulatedGold += result.goldReward;
            if (result.bonusGold > 0)
                adventure.accumulatedBonusGold += result.bonusGold;
        }

        private void ApplyResultRareDrop(AdventureInstance adventure, EventResult result)
        {
            foreach (var m in result.materialDrops)
            {
                var existing = adventure.accumulatedMaterials
                    .FirstOrDefault(x => x.materialDataID == m.materialDataID);
                if (existing != null) existing.quantity += m.quantity;
                else adventure.accumulatedMaterials.Add(m);
            }

            foreach (var m in result.bonusMaterials)
            {
                var existing = adventure.accumulatedBonusMaterials
                    .FirstOrDefault(x => x.materialDataID == m.materialDataID);
                if (existing != null) existing.quantity += m.quantity;
                else adventure.accumulatedBonusMaterials.Add(m);
            }
        }

        private void ApplyResultRest(AdventureInstance adventure)
        {
            adventure.ProtectionCharges(1);
        }

        private void ApplyResultTrap(AdventureInstance adventure, EventResult result)
        {
            if (!result.isSuccess)
                adventure.cumulativeModifier += Config.trapSuccessPenalty;
        }

        private void ApplyResultBattleOrMiniBoss(AdventureInstance adventure, EventResult result)
        {
            if (result.isSuccess)
            {
                adventure.accumulatedGold += result.goldReward;
                if (result.bonusGold > 0)
                    adventure.accumulatedBonusGold += result.bonusGold;
            }  
        }

        private void ApplyResultBoss(AdventureInstance adventure, EventResult result)
        {
            if (result.isSuccess)
            {
                adventure.accumulatedGold += result.goldReward;
                if (result.bonusGold > 0)
                    adventure.accumulatedBonusGold += result.bonusGold;

                foreach (var m in result.materialDrops ?? new())
                {
                    var existing = adventure.accumulatedMaterials
                        .FirstOrDefault(x => x.materialDataID == m.materialDataID);
                    if (existing != null) existing.quantity += m.quantity;
                    else adventure.accumulatedMaterials.Add(m);
                }
                foreach (var m in result.bonusMaterials)
                {
                    var existing = adventure.accumulatedBonusMaterials
                        .FirstOrDefault(x => x.materialDataID == m.materialDataID);
                    if (existing != null) existing.quantity += m.quantity;
                    else adventure.accumulatedBonusMaterials.Add(m);
                }

                adventure.isSuccess = true;
                // 재판정하지 않는다 - 판정은 CalculatePendingBoss에서 보상 계산 전에 완료됨
                adventure.isGreatSuccess = result.isGreatSuccess;
            }
        }

        private void ApplyResultProtection(AdventureInstance adventure)
        {
            adventure.ProtectionCharges(-1);
        }

        private void ApplyResultRetry(AdventureInstance adventure)
        {
            adventure.ProtectionCharges(-1);
        }

        private void ApplyResultRetreat(AdventureInstance adventure)
        {
            // 종료 플래그만 설정. 실제 완료(CompleteAdventure)는 런타임이 종료 이벤트 시간 도달 시 수행.
            adventure.isRetreated = true;
            adventure.isSuccess = false;
        }

        private void ApplyResultReturn(AdventureInstance adventure)
        {
            // 보스 성공 시 isSuccess/보상은 ApplyResultBoss에서 이미 누적됨. 완료는 런타임이 수행.
        }

        #endregion

        #endregion

        private void InsertRetreatEvent(AdventureInstance adventure)
        {
            var retreatData = DataManager.Instance.GetDungeonEventByType(DungeonEventType.Retreat);
            if (retreatData != null)
            {
                InsertEventAfterCurrent(adventure, retreatData);
            }
            else
            {
                // 안전망: Retreat 데이터가 없으면 현재 이벤트를 종료점으로 만들어 모험을 끝낸다.
                // (resolve 중 호출되므로 currentEventIndex = 현재 커서. 완료는 런타임이 수행.)
                Log.Warn("[AdventureManager] Retreat 이벤트 데이터가 없어 현재 이벤트에서 모험을 종료합니다.");
                adventure.isRetreated = true;
                adventure.isSuccess = false;
                var prog = adventure.progress;
                int cut = prog.currentEventIndex + 1;
                if (cut < prog.events.Count)
                    prog.events.RemoveRange(cut, prog.events.Count - cut);
            }
        }
    }
}
