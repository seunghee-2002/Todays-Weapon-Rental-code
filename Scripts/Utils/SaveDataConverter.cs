// Scripts/Utilities/SaveDataConverter.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    public static class SaveDataConverter
    {
        // WeaponInstance → SaveData
        public static WeaponInstanceSaveData ToSaveData(this WeaponInstance instance)
        {
            return new WeaponInstanceSaveData
            {
                weaponDataID = instance.weaponData.StaticID,
                instanceID = instance.instanceID,
                effects = instance.effects?.Select(e => new WeaponEffectSaveData
                {
                    effectType      = e.effectData != null ? e.effectData.effectType      : default,
                    effectDataID    = e.effectDataID,
                    grade           = e.effectData != null ? e.effectData.grade           : default,
                    baseValue       = e.baseValue,
                    currentValue    = e.currentValue,
                    targetGrade     = e.effectData != null ? e.effectData.targetGrade     : 0,
                    targetStat      = e.effectData != null ? e.effectData.targetStat      : 0,
                    targetArmorType = e.effectData != null ? e.effectData.targetArmorType : 0,
                    targetThreshold = e.effectData != null ? e.effectData.targetThreshold : 0,
                }).ToList(),

                currentGrade = instance.currentGrade,
                enforceLevel = instance.enforceLevel,

                purchasedDay = instance.purchasedDay,
                isRented = instance.isRented,
                rerollCount = instance.rerollCount,
                isExtraRerolled = instance.isExtraRerolled,
                hasExtraRerollCharge = instance.hasExtraRerollCharge,
                isDefaultWeapon = instance.isDefaultWeapon,
                rentedToAdventurerID = instance.rentedToAdventurerID,
                totalRentalCount = instance.totalRentalCount                
            };
        }
 
        // AdventurerInstance → SaveData
        public static AdventurerInstanceSaveData ToSaveData(this AdventurerInstance instance)
        { 
            return new AdventurerInstanceSaveData
            {
                adventurerDataID     = instance.adventurerData.StaticID,
                instanceID           = instance.instanceID,
                isNamed              = instance.isNamed,
                instanceTrait        = instance.instanceTrait,
                nameKey              = instance.nameKey,
                instanceName         = instance.instanceName,
                appearance           = instance.appearance,
                STR                  = instance.STR,
                DEX                  = instance.DEX,
                INT                  = instance.INT,
                LUK                  = instance.LUK,
                autoRevealedStatIndices = instance.autoRevealedStatIndices ?? new List<AdventurerStat>(),
                revealedStatIndices     = instance.revealedStatIndices     ?? new List<int>(),
                isStatsFullyRevealed    = instance.isStatsFullyRevealed,
                isTraitRevealed         = instance.isTraitRevealed,
                isWeaponTypeHinted      = instance.isWeaponTypeHinted,
                lastTalkDayByTest       = instance.lastTalkDayByTest,
                isAdventuring        = instance.isAdventuring,
                affection            = instance.affection,
                isAlive              = instance.isAlive,
                rentedWeaponInstanceID = instance.rentedWeapon?.instanceID,
                defaultWeaponType    = instance.defaultWeapon?.weaponData.weaponType ?? WeaponType.Sword,
                // 일반 모험가라도 진행 중 모험/대여에 묶여 있으면 저장/로드 왕복 후에도
                // 사망보호 등 스탯 상태가 유지되어야 하므로 statData를 함께 저장한다
                adventurerStatData   = (instance.isNamed || instance.isAdventuring || instance.rentedWeapon != null)
                                        ? instance.adventurerStatData : null
            };
        }

        // AdventureInstance → SaveData
        public static AdventureInstanceSaveData ToSaveData(this AdventureInstance instance)
        {
            if (instance == null)
            {
                Log.Warn("[SaveDataConverter] null AdventureInstance 저장 요청 - 스킵");
                return null;
            }

            return new AdventureInstanceSaveData
            {
                instanceID           = instance.instanceID ?? "unknown",
                adventurerInstanceID = instance.adventurer?.instanceID ?? "unknown",
                weaponInstanceID     = instance.weapon?.instanceID ?? "unknown",
                dungeonDataID        = instance.dungeon?.StaticID ?? "unknown",
                effectiveArmorType   = (int)instance.effectiveArmorType,
                startTime            = instance.startTime,
                isCompleted          = instance.isCompleted,
                isSuccess            = instance.isSuccess,
                isDeath              = instance.isDeath,
                isGreatSuccess       = instance.isGreatSuccess,
                isRetreated          = instance.isRetreated,
                isUsingDefaultWeapon = instance.weapon?.isDefaultWeapon ?? false,
                completedDay         = instance.completedDay,
                seerUsed             = instance.seerUsed,
                completedAtUtcTicks  = instance.completedAtUtcTicks,
                accumulatedGold      = instance.accumulatedGold,
                accumulatedBonusGold = instance.accumulatedBonusGold,
                accumulatedMaterials = instance.accumulatedMaterials,
                accumulatedBonusMaterials = instance.accumulatedBonusMaterials,
                goldPreservationTriggered = instance.goldPreservationTriggered,
                protectionCharges    = instance.protectionCharges,
                initialProtectionCharges = instance.initialProtectionCharges,
                cumulativeModifier   = instance.cumulativeModifier,

                // 액티브 아이템 효과 캐싱 필드
                goldAmuletBonus       = instance?.goldAmuletBonus ?? 0f,
                fameScrollBonus       = instance?.fameScrollBonus ?? 0,
                disassemblyKnifeBonus = instance?.disassemblyKnifeBonus ?? 0,
                deathWardBonus        = instance?.deathWardBonus ?? 0f,
                escapeRopeBonus       = instance?.escapeRopeBonus ?? 0f,
                swiftShoesMultiplier  = instance?.swiftShoesMultiplier ?? 1f,
                treasureMapBonus      = instance?.treasureMapBonus ?? 0f,
                mood                  = (int)(instance?.mood ?? 0),

                progress             = instance?.progress
            };
        }

        // VisitorNPC → SaveData
        public static VisitorNPCSaveData ToSaveData(this VisitorNPC instance)
        {
            return new VisitorNPCSaveData
            {
                visitorType = instance.visitorType,
                savedDay = GameManager.Instance.GameData.currentDay,
                remainingTime = instance.RemainingTime,
                isInteracting = instance.isInteracting,
                adventurerInstanceID = instance.adventurerInstance?.instanceID,
                blacksmithType = instance.blacksmithData?.type ?? BlacksmithType.None,
                blacksmithIsPremium = instance.blacksmithData?.isPremium ?? false,
                eventDataStaticID = instance.eventData?.StaticID,
                investorDialogueID = instance.InvestorDialogueID,
                investorReturnedGold = instance.InvestorReturnedGold,
                deadEventKind = instance.deadEventKind
            };
        }

        // List 변환
        public static List<WeaponInstanceSaveData> ToSaveDataList(this List<WeaponInstance> instances)
        {
            return instances.Select(i => i.ToSaveData()).ToList();
        }

        public static List<AdventurerInstanceSaveData> ToSaveDataList(this List<AdventurerInstance> instances)
        {
            return instances.Select(i => i.ToSaveData()).ToList();
        }

        public static List<AdventureInstanceSaveData> ToSaveDataList(this List<AdventureInstance> instances)
        {
            return instances
                .Where(i => i != null)
                .Select(i => i.ToSaveData())
                .Where(s => s != null)
                .ToList();
        }

        public static List<ActiveItemInstanceSaveData> ToSaveDataList(this List<ActiveItemInstance> instances)
        {
            return instances.Select(i => i.ToSaveData()).ToList();
        }
    }
}