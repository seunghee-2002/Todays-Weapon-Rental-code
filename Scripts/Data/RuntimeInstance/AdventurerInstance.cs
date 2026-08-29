using System;
using UnityEngine;
using UnityEngine.Localization.Settings;
using System.Linq;
using System.Collections.Generic;

namespace TodaysWeaponRental{
    [Serializable]
    public class AdventurerInstance
    {
        // 핵심 데이터
        public AdventurerData adventurerData;
        public string instanceID;
        public WeaponInstance rentedWeapon;
        public WeaponInstance defaultWeapon;
        public int affection;
        /// <summary>일반 모험가 이름의 Names 테이블 키. 네임드와 구버전 세이브는 비어 있고 instanceName을 쓴다.</summary>
        public string nameKey;
        public string instanceName;

        // 외형
        public AdventurerAppearanceData appearance;

        // 스탯
        public bool isNamed;
        public TraitType instanceTrait;
        public int STR;
        public int DEX;
        public int INT;
        public int LUK;
        
        // 공개 상태
        public List<AdventurerStat> autoRevealedStatIndices = new List<AdventurerStat>();
        public List<int> revealedStatIndices = new List<int>();
        public bool isStatsFullyRevealed;
        public bool isTraitRevealed;
        public bool isWeaponTypeHinted;

        /// <summary>네임드 전용 — TalkTestType별 마지막 일반 판정 시도 날짜. 0 = 미시도.</summary>
        public int[] lastTalkDayByTest = new int[TalkTestCount];

        public const int TalkTestCount = 5;

        // 상태
        public bool isAlive;
        public bool isVisiting;
        public bool isAdventuring;

        [NonSerialized]
        public VisitorNPC currentVisitor;
        
        // 통계 데이터
        public AdventurerStatData adventurerStatData;

        // 프로퍼티
        /// <summary>
        /// 표시용 이름. 일반 모험가는 nameKey를 현재 언어로 조회하므로 언어를 바꾸면 이름도 함께 바뀐다.
        /// 네임드(SO 이름)와 nameKey가 없는 구버전 세이브는 instanceName을 그대로 쓴다.
        /// </summary>
        public string Name => string.IsNullOrEmpty(nameKey)
            ? instanceName
            : LocalizationSettings.StringDatabase.GetLocalizedString("Names", nameKey);
        public TraitType Trait => isNamed ? adventurerData.trait : instanceTrait;
        public WeaponInstance ActiveWeapon => rentedWeapon ?? defaultWeapon;

        public bool IsHome
        {
            get
            {
                if (!isAlive) return false;
                if (isAdventuring) return false;
                if (isVisiting) return false;
                
                // completedAdventures에 있는지 확인
                return !AdventureManager.Instance.CompletedAdventures
                    .Any(a => a.adventurer.instanceID == instanceID);
            }
        }

        public int GetStat(AdventurerStat stat)
        {
            return stat switch
            {
                AdventurerStat.STR => STR,
                AdventurerStat.DEX => DEX,
                AdventurerStat.INT => INT,
                AdventurerStat.LUK => LUK,
                _ => 0
            };
        }

        public float GetStatScore(WeaponType weaponType, int[] statBonus = null)
        {
            statBonus ??= new int[4];
            return TypeAdvantage.GetStatScore(STR + statBonus[0], DEX + statBonus[1], INT + statBonus[2], LUK + statBonus[3], weaponType);
        }

        public AdventurerStat GetHighestStat()
        {
            int best = STR;
            AdventurerStat result = AdventurerStat.STR;

            if (DEX > best)      { best = DEX;       result = AdventurerStat.DEX; }
            if (INT > best) { best = INT;  result = AdventurerStat.INT; }
            if (LUK > best)         { result = AdventurerStat.LUK; }

            return result;
        }

        public WeaponType GetBestWeaponType()
        {
            float bestScore = -1f;
            WeaponType bestType = WeaponType.Sword;

            foreach (WeaponType wt in Enum.GetValues(typeof(WeaponType)))
            {
                float score = GetStatScore(wt);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestType = wt;
                }
            }

            return bestType;
        }

        public AdventurerInstance(AdventurerData data, int currentDay)
        {
            adventurerData = data;
            instanceID = Guid.NewGuid().ToString();
            isNamed = data.isNamed;
            // 네임드는 StaticID로 이름 키를 만든다(Names 테이블). 일반은 스폰 시 이름 풀에서 키를 채움.
            // instanceName은 키 조회가 안 될 때의 폴백 겸 에디터 확인용으로 SO 원본 이름을 복사한다.
            nameKey = isNamed ? "Name_" + data.StaticID : null;
            instanceName = isNamed ? data.adventurerName : null;
            appearance = null;

            var traitValues = (TraitType[])Enum.GetValues(typeof(TraitType));
            instanceTrait = isNamed
                ? data.trait
                : traitValues[UnityEngine.Random.Range(0, traitValues.Length)];

            // 스탯 랜덤 초기화
            STR     = UnityEngine.Random.Range(data.strRange.x, data.strRange.y + 1);
            DEX      = UnityEngine.Random.Range(data.dexRange.x, data.dexRange.y + 1);
            INT = UnityEngine.Random.Range(data.intRange.x, data.intRange.y + 1);
            LUK         = UnityEngine.Random.Range(data.lukRange.x, data.lukRange.y + 1);

            isAdventuring = false;
            isVisiting = false;
            rentedWeapon = null;
            affection = 0;
            isAlive = true;
            currentVisitor = null;
            
            // 일반/네임드 공통 — 통찰 임계값 미달 시 InsightManager가 숨김
            autoRevealedStatIndices = GenerateRandomStatIndices(2);

            adventurerStatData = new AdventurerStatData
            {
                hasReachedMaxAffection = false,
                hasDeathProtection = false,
                visitCount = 0,
                adventureSuccessCount = 0,
                adventureFailCount = 0,
                adventurerWeaponGoodCount = 0,
                lastVisitDay = currentDay,
                giftedCount = 0,
                totalGoldEarned = 0
            };
        }
        
        public void RentWeapon(WeaponInstance weapon)
        {
            rentedWeapon = weapon;
            if (!weapon.isDefaultWeapon)
                weapon.RentTo(instanceID);
        }
        
        public void AddAffection(int amount)
        {
            affection += amount;
            if (affection > 100) affection = 100;
            if (affection < 0) affection = 0;
            
            // 호감도 100 최초 도달 시에만 사망보호 1회 부여 — 소진 후 재충전 불가
            if (affection >= 100 && !adventurerStatData.hasReachedMaxAffection)
            {
                adventurerStatData.hasReachedMaxAffection = true;
                adventurerStatData.hasDeathProtection = true;
            }
        }
        
        public AffectionLevel GetAffectionLevel()
        {
            if (affection <= 25) return AffectionLevel.Low;
            if (affection <= 50) return AffectionLevel.Medium;
            if (affection <= 75) return AffectionLevel.High;
            return AffectionLevel.Max;
        }
        
        public void MarkAsDead()
        {
            isAlive = false;
        }

        /// <summary>
        /// STR/DEX/INT/LUK 중 count개를 중복 없이 랜덤 선택
        /// </summary>
        private static List<AdventurerStat> GenerateRandomStatIndices(int count)
        {
            var stats = new List<AdventurerStat>
            {
                AdventurerStat.STR, AdventurerStat.DEX, AdventurerStat.INT, AdventurerStat.LUK
            };
            // Fisher-Yates shuffle
            for (int i = stats.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (stats[i], stats[j]) = (stats[j], stats[i]);
            }
            return stats.GetRange(0, Mathf.Min(count, stats.Count));
        }
        
        // SaveData에서 복원
        public AdventurerInstance(AdventurerInstanceSaveData saveData)
        {
            adventurerData = DataManager.Instance.GetAdventurer(saveData.adventurerDataID);
            instanceID = saveData.instanceID;
            isNamed = saveData.isNamed;
            instanceTrait = saveData.instanceTrait;
            // nameKey가 있으면 Name이 그걸 우선 조회한다.
            // 네임드는 생성자에서, 일반은 스폰 시(VisitorManager) 채워지므로 항상 값이 있다.
            nameKey = saveData.nameKey;
            instanceName = saveData.instanceName;
            appearance = saveData.appearance;
            STR     = saveData.STR;
            DEX      = saveData.DEX;
            INT = saveData.INT;
            LUK         = saveData.LUK;
            autoRevealedStatIndices = saveData.autoRevealedStatIndices ?? new List<AdventurerStat>();
            revealedStatIndices     = saveData.revealedStatIndices     ?? new List<int>();
            isStatsFullyRevealed    = saveData.isStatsFullyRevealed;
            isTraitRevealed         = saveData.isTraitRevealed;
            isWeaponTypeHinted      = saveData.isWeaponTypeHinted;
            // 구버전 세이브 호환 — 필드가 없거나 길이가 다르면 새 배열로 보정
            lastTalkDayByTest = saveData.lastTalkDayByTest != null && saveData.lastTalkDayByTest.Length == TalkTestCount
                ? saveData.lastTalkDayByTest
                : new int[TalkTestCount];
            isAdventuring = saveData.isAdventuring;
            isVisiting = false;
            affection = saveData.affection;
            isAlive = saveData.isAlive;

            if (!string.IsNullOrEmpty(saveData.rentedWeaponInstanceID))
            {
                rentedWeapon = InventoryManager.Instance.GetWeaponInstance(saveData.rentedWeaponInstanceID);
            }

            adventurerStatData = saveData.adventurerStatData ?? new AdventurerStatData();
        }
    }

    [Serializable]
    public class AdventurerInstanceSaveData
    {
        public string adventurerDataID;
        public string instanceID;
        public bool isNamed;
        public TraitType instanceTrait;
        public string nameKey;
        public string instanceName;
        public AdventurerAppearanceData appearance;
        public int STR;
        public int DEX;
        public int INT;
        public int LUK;
        public List<AdventurerStat> autoRevealedStatIndices;
        public List<int> revealedStatIndices;
        public bool isStatsFullyRevealed;
        public bool isTraitRevealed;
        public bool isWeaponTypeHinted;
        public int[] lastTalkDayByTest;
        public bool isAdventuring;
        public int affection;
        public bool isAlive;
        public string rentedWeaponInstanceID;
        public WeaponType defaultWeaponType;
        public AdventurerStatData adventurerStatData;
    }
}

