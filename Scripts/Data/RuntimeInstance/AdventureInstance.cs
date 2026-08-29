using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    [Serializable]
    public class AdventureInstance
    {
        public string instanceID;
        public AdventurerInstance adventurer;
        public WeaponInstance weapon;
        public DungeonData dungeon;
        public ArmorType effectiveArmorType;
        public float startTime;
        public bool isCompleted;
        public bool isSuccess;
        public bool isDeath;
        public bool isGreatSuccess;
        public bool isRetreated;
        public bool isUsingDefaultWeapon;
        public int completedDay;

        // Analytics용 (Documents/Analytics_이벤트_설계.md)
        public bool seerUsed;             // 출발 전 점술 상담 여부. seerResults가 일 단위로 초기화되므로 출발 시점에 스냅샷
        public long completedAtUtcTicks;  // 완료 시각 UTC Ticks (G26 결과 확인 지연 측정용, 앱 재시작에도 유지)

        public int accumulatedGold;
        public int accumulatedBonusGold;
        public List<MaterialInstance> accumulatedMaterials;
        public List<MaterialInstance> accumulatedBonusMaterials;
        public bool goldPreservationTriggered; // FailGoldBonus 발동 여부
        public int protectionCharges;
        // 출발 시점의 초기 보호 횟수 (무기 효과+특성+포션 합산). 리플레이 표시용
        public int initialProtectionCharges;
        public float cumulativeModifier;
        public AdventurerMood mood;

        public AdventureProgress progress;

        // 액티브 아이템 효과 캐싱 (StartAdventure 시점에 값 설정, 이후 Manager에서 소비됨)
        public float goldAmuletBonus = 0f;
        public float fameScrollBonus = 0f;   // 평판 배율 (1.0 = x2)
        public int disassemblyKnifeBonus = 0;
        public float deathWardBonus = 0f;
        public float escapeRopeBonus = 0f;
        public float swiftShoesMultiplier = 1f;  // 기본값 1f (곱셈 방식)
        public float treasureMapBonus = 0f;  // 특수 재료 드롭률 배율 (1.0 = x2)

        // 프로퍼티
        public bool HasProtection => protectionCharges > 0;
        public void ProtectionCharges(int amount) => protectionCharges = Mathf.Clamp(protectionCharges + amount, 0, ConfigManager.Instance.Adventure.maxProtectionCharges);

        // 신규 모험 생성
        public AdventureInstance(AdventurerInstance adventurer, WeaponInstance weapon, DungeonData dungeon,
            int initialProtectionCharges, List<AdventureEvent> events)
        {
            instanceID              = Guid.NewGuid().ToString();
            this.adventurer         = adventurer;
            this.weapon             = weapon;
            this.dungeon            = dungeon;
            this.effectiveArmorType = QuestBoardManager.Instance.GetTodayArmorType(dungeon.StaticID);
            this.startTime          = TimeManager.Instance.CurrentTime;
            isCompleted          = false;
            isSuccess            = false;
            isDeath              = false;
            isGreatSuccess       = false;
            isRetreated          = false;
            isUsingDefaultWeapon = weapon.isDefaultWeapon;
            accumulatedGold      = 0;
            accumulatedBonusGold = 0;
            accumulatedMaterials = new List<MaterialInstance>();
            accumulatedBonusMaterials = new List<MaterialInstance>();
            goldPreservationTriggered = false;
            protectionCharges    = initialProtectionCharges;
            this.initialProtectionCharges = initialProtectionCharges;
            cumulativeModifier   = 0f;
            mood                 = AdventurerMood.Normal; // StartAdventure에서 DetermineMood()로 덮어씀
            progress = new AdventureProgress
            {
                events             = events,
                currentEventIndex  = 0
            };
        }

        // 세이브 데이터 복원
        public AdventureInstance(AdventureInstanceSaveData saveData)
        {
            instanceID     = saveData.instanceID;
            adventurer     = VisitorManager.Instance.GetAdventurerInstance(saveData.adventurerInstanceID);
            weapon         = saveData.isUsingDefaultWeapon
                ? adventurer?.defaultWeapon
                : InventoryManager.Instance.GetWeaponInstance(saveData.weaponInstanceID);
            dungeon             = DataManager.Instance.GetDungeon(saveData.dungeonDataID);
            effectiveArmorType  = (ArmorType)saveData.effectiveArmorType;
            startTime           = saveData.startTime;
            isCompleted    = saveData.isCompleted;
            isSuccess      = saveData.isSuccess;
            isDeath        = saveData.isDeath;
            isGreatSuccess = saveData.isGreatSuccess;
            isRetreated    = saveData.isRetreated;
            isUsingDefaultWeapon = saveData.isUsingDefaultWeapon;
            completedDay   = saveData.completedDay;
            seerUsed             = saveData.seerUsed;
            completedAtUtcTicks  = saveData.completedAtUtcTicks;

            accumulatedGold      = saveData.accumulatedGold;
            accumulatedBonusGold = saveData.accumulatedBonusGold;
            accumulatedMaterials = ReifyMaterialsFromSave(saveData.accumulatedMaterials);
            accumulatedBonusMaterials = ReifyMaterialsFromSave(saveData.accumulatedBonusMaterials);
            goldPreservationTriggered = saveData.goldPreservationTriggered;
            protectionCharges    = saveData.protectionCharges;
            initialProtectionCharges = saveData.initialProtectionCharges;
            cumulativeModifier   = saveData.cumulativeModifier;
            mood                 = (AdventurerMood)saveData.mood;

            goldAmuletBonus       = saveData.goldAmuletBonus;
            fameScrollBonus       = saveData.fameScrollBonus;
            disassemblyKnifeBonus = saveData.disassemblyKnifeBonus;
            deathWardBonus        = saveData.deathWardBonus;
            escapeRopeBonus       = saveData.escapeRopeBonus;
            swiftShoesMultiplier  = saveData.swiftShoesMultiplier == 0f ? 1f : saveData.swiftShoesMultiplier;
            treasureMapBonus      = saveData.treasureMapBonus;

            progress = new AdventureProgress
            {
                currentEventIndex  = saveData.progress?.currentEventIndex ?? 0,
                events             = saveData.progress?.events
                    .Where(evt => evt != null && !string.IsNullOrEmpty(evt.eventDataID))
                    .Select(evt =>
                    {
                        var data = DataManager.Instance.GetDungeonEvent(evt.eventDataID);
                        return data != null
                            ? new AdventureEvent(data)
                            {
                                startTime   = evt.startTime,
                                cachedDurationMultiplier = evt.cachedDurationMultiplier,
                                result = evt.result
                            }
                            : null;
                    })
                    .Where(evt => evt != null)
                    .ToList() ?? new List<AdventureEvent>()
            };

            // result(이벤트별 확정 결과) 내부 재료도 SO 참조 복원 — accumulatedMaterials와 동일 규칙
            foreach (var e in progress.events)
            {
                if (e.result == null) continue;
                ReifyMaterialsFromSave(e.result.materialDrops);
                ReifyMaterialsFromSave(e.result.bonusMaterials);
            }
        }

        private static List<MaterialInstance> ReifyMaterialsFromSave(List<MaterialInstance> list)
        {
            if (list == null) return new List<MaterialInstance>();
            foreach (var m in list)
            {
                if (m != null && m.materialData == null && !string.IsNullOrEmpty(m.materialDataID))
                    m.materialData = DataManager.Instance.GetMaterial(m.materialDataID);
            }
            return list;
        }
    }

    [Serializable]
    public class AdventureProgress
    {
        public List<AdventureEvent> events = new List<AdventureEvent>();
        public int currentEventIndex;

        /// <summary>현재 진행 중(시작됨·미완료) 이벤트. 없으면 null.</summary>
        public AdventureEvent CurrentEvent =>
            (currentEventIndex >= 0 && currentEventIndex < events.Count && events[currentEventIndex].startTime >= 0f)
                ? events[currentEventIndex] : null;
    }

    [Serializable]
    public class AdventureEvent
    {
        public DungeonEventData eventData;
        public string eventDataID; // JsonUtility 직렬화용 ID (ScriptableObject 레퍼런스 대체)
        public float startTime;   // 현재 이벤트가 된 시점의 CurrentTime (미시작 = -1)
        public float cachedDurationMultiplier = 1f; // 기본 1, TimeReduction 적용 시 0.85 등
        public EventResult result; // 모험 시작 시 시뮬레이션으로 확정한 결과 (이벤트와 1:1)

        public AdventureEvent(DungeonEventData eventData)
        {
            this.eventData = eventData;
            this.eventDataID = eventData?.StaticID;
            startTime  = -1f;
            cachedDurationMultiplier = 1f;
            result = null;
        }
    }

    [Serializable]
    public class EventResult
    {
        public DungeonEventType eventType;
        public bool isSuccess;
        // 보스 이벤트 전용: 보상 계산 전에 확정된 대성공 여부
        public bool isGreatSuccess;
        public int goldReward;
        public int bonusGold;
        public List<MaterialInstance> materialDrops = new();
        public List<MaterialInstance> bonusMaterials = new();
        public float successRateAtTime;
        public bool protectionActivated;
        public float moodMultiplier = 1f; // 이벤트에 적용된 기분 배율 (디버그용)
        // 사망 굴림이 떴지만 STR 재굴림으로 살아남은 경우 (실패는 그대로)
        public bool survivedByStrength;
    }

    [Serializable]
    public class AdventureInstanceSaveData
    {
        public string instanceID;
        public string adventurerInstanceID;
        public string weaponInstanceID;
        public string dungeonDataID;
        public int effectiveArmorType;
        public float startTime;
        public bool isCompleted;
        public bool isSuccess;
        public bool isDeath;
        public bool isGreatSuccess;
        public bool isRetreated;
        public bool isUsingDefaultWeapon;
        public int completedDay;
        public bool seerUsed;
        public long completedAtUtcTicks;

        public int accumulatedGold;
        public int accumulatedBonusGold;
        public List<MaterialInstance> accumulatedMaterials;
        public List<MaterialInstance> accumulatedBonusMaterials;
        public bool goldPreservationTriggered;
        public int protectionCharges;
        public int initialProtectionCharges;
        public float cumulativeModifier;
        public int mood;

        // 액티브 아이템 캐싱 필드
        public float goldAmuletBonus;
        public float fameScrollBonus;
        public int disassemblyKnifeBonus;
        public float deathWardBonus;
        public float escapeRopeBonus;
        public float swiftShoesMultiplier = 1f;
        public float treasureMapBonus;

        public AdventureProgress progress;
    }
}
