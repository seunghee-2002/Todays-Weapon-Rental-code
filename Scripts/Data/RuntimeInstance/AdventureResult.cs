using System;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    [Serializable]
    public class AdventureResult
    {
        public string adventureID;
        public string adventurerID;
        public string weaponID;
        public string dungeonID;
        
        public bool isSuccess;
        public bool isDeath;
        public bool isGreatSuccess;
        public bool isRetreated;

        public bool isAdventurerTypeMatch;
        public bool isDungeonTypeMatch;
        public bool deathProtectionUsed;
        public bool weaponLost;          // 실제로 파괴된 경우만 true (기본 무기는 파괴되지 않으므로 false)
        public bool isDefaultWeapon;     // 대여 무기 없이 기본 무기로 떠난 모험인지 (밸런스 분석용)

        // 통계(탐험도/도감/전체 카운트) 반영 완료 여부 - 모험 완료 시점 1회 적용, 중복 적립 방지
        public bool statisticsApplied;
        
        // 수수료 내역
        public float baseCommissionRate;
        public float rentalCommissionRate;
        public float tipCommissionRate;
        public float traitCommissionRate;  // 특성 수수료 비율
        public float totalCommissionRate;

        // 골드 내역
        public int totalGoldReward;     // 모험 총 보상 (수수료 계산 전)
        public int playerGoldReward;    // 플레이어 실제 수령 골드

        public int goldReward;          // 하위 호환용 (totalGoldReward와 동일)
        public int reputationChange;
        public int affectionChange;
        public int insightChange;
        public List<MaterialInstance> materialDrops = new List<MaterialInstance>();
        public List<MaterialInstance> purchasedMaterials = new List<MaterialInstance>();

        public AdventureResult(AdventureInstance adventure)
        {
            adventureID = adventure.instanceID;
            adventurerID = adventure.adventurer?.instanceID ?? "unknown adventurer";
            weaponID = adventure.weapon?.instanceID ?? "unknown weapon";
            dungeonID = adventure.dungeon?.StaticID ?? "unknown dungeon";
            isSuccess = adventure.isSuccess;
            isDeath = adventure.isDeath;
            isGreatSuccess = adventure.isGreatSuccess;
            isRetreated = adventure.isRetreated;

            isAdventurerTypeMatch = false;
            isDungeonTypeMatch = false;
            deathProtectionUsed = false;
            weaponLost = false;
            isDefaultWeapon = adventure.weapon?.isDefaultWeapon ?? false;
            goldReward = 0;
            reputationChange = 0;
            affectionChange = 0;
            insightChange = 0;
            materialDrops = new List<MaterialInstance>();
        }
    }
}
