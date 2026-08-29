using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;
using LayerLab.ArtMaker;

namespace TodaysWeaponRental
{
    public partial class VisitorManager
    {
        #region 기본 무기 생성

        /// <summary>
        /// 모험가의 스탯 분포에 따라 default weapon을 선택하여 생성.
        /// 70% best 타입, 20% second best 타입, 10% 무작위.
        /// </summary>
        private WeaponInstance CreateDefaultWeaponFor(AdventurerInstance adventurer)
        {
            if (DataManager.Instance == null)
            {
                Log.Error("[VisitorManager] DataManager not initialized. Cannot create defaultWeapon.");
                return null;
            }

            float roll = Random.value;
            WeaponData selectedData;

            if (roll < 0.70f)
            {
                selectedData = DataManager.Instance.GetDefaultWeapon(adventurer.GetBestWeaponType());
            }
            else if (roll < 0.90f)
            {
                selectedData = DataManager.Instance.GetDefaultWeapon(GetSecondBestWeaponType(adventurer));
            }
            else
            {
                var all = DataManager.Instance.GetAllDefaultWeapons();
                if (all == null || all.Count == 0)
                {
                    Log.Error("[VisitorManager] No default weapons registered.");
                    return null;
                }
                selectedData = all[Random.Range(0, all.Count)];
            }

            if (selectedData == null) return null;

            return new WeaponInstance(selectedData, true);
        }

        /// <summary>
        /// SaveData의 defaultWeaponType으로 default weapon 인스턴스를 복원.
        /// </summary>
        private WeaponInstance RestoreDefaultWeapon(WeaponType weaponType)
        {
            var data = DataManager.Instance?.GetDefaultWeapon(weaponType);
            if (data == null)
            {
                Log.Error($"[VisitorManager] defaultWeapon 복원 실패 - WeaponType '{weaponType}' not found.");
                return null;
            }
            return new WeaponInstance(data, true);
        }

        private WeaponType GetSecondBestWeaponType(AdventurerInstance adventurer)
        {
            float bestScore = -1f;
            float secondScore = -1f;
            WeaponType bestType = WeaponType.Sword;
            WeaponType secondType = WeaponType.Sword;

            foreach (WeaponType wt in Enum.GetValues(typeof(WeaponType)))
            {
                float score = adventurer.GetStatScore(wt);
                if (score > bestScore)
                {
                    secondScore = bestScore;
                    secondType = bestType;
                    bestScore = score;
                    bestType = wt;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                    secondType = wt;
                }
            }

            return secondType;
        }

        #endregion

        #region 모험가 생성

        /// <summary>
        /// 아침 페이즈 진입 시 당일 방문할 일반 모험가 인스턴스를 미리 생성
        /// </summary>
        private void GenerateDailyNormalVisitorPool()
        {
            dailyNormalVisitorPool.Clear();
            dailyNormalVisitorIndex = 0;

            int count = Config.normalAdventurerDailyPoolSize;
            for (int i = 0; i < count; i++)
            {
                var instance = CreateNormalAdventurerInstance();
                if (instance != null)
                    dailyNormalVisitorPool.Add(instance);
            }

            Log.Info($"[VisitorManager] 당일 일반 모험가 풀 생성 완료: {dailyNormalVisitorPool.Count}명");
        }

        /// <summary>
        /// isNamed=false인 AdventurerData 중 랜덤 선택 후 임시 AdventurerInstance 생성.
        /// instanceCache에 등록하지 않음.
        /// </summary>
        public AdventurerInstance CreateNormalAdventurerInstance()
        {
            var normalDataList = DataManager.Instance?.GetAdventurersByNamed(false);
            if (normalDataList == null || normalDataList.Count == 0)
            {
                Log.Warn("[VisitorManager] isNamed=false인 AdventurerData가 없습니다.");
                return null;
            }

            var data = normalDataList[Random.Range(0, normalDataList.Count)];
            Log.Info($"[VisitorManager] 일반 모험가 데이터 선택: {data.StaticID}");
            var instance = new AdventurerInstance(data, TimeManager.Instance.CurrentDay);

            bool hasStatRange = data.strRange != Vector2Int.zero
                 && data.dexRange != Vector2Int.zero
                 && data.intRange != Vector2Int.zero
                 && data.lukRange != Vector2Int.zero;
            if (hasStatRange)
            {
                instance.STR = Random.Range(data.strRange.x, data.strRange.y + 1);
                instance.DEX = Random.Range(data.dexRange.x, data.dexRange.y + 1);
                instance.INT = Random.Range(data.intRange.x, data.intRange.y + 1);
                instance.LUK = Random.Range(data.lukRange.x, data.lukRange.y + 1);
            }
            else
            {
                int statMin = Config.normalAdventurerStatMin;
                int statMax = Config.normalAdventurerStatMax;
                instance.STR = Random.Range(statMin, statMax + 1);
                instance.DEX = Random.Range(statMin, statMax + 1);
                instance.INT = Random.Range(statMin, statMax + 1);
                instance.LUK = Random.Range(statMin, statMax + 1);
            }

            // 기본 무기는 스탯 확정 이후에 정해야 GetBestWeaponType이 최종 스탯을 본다.
            // (스탯 재추첨보다 먼저 호출하면 버려질 값으로 무기 타입이 결정된다)
            instance.defaultWeapon = CreateDefaultWeaponFor(instance);

            // 이름 부여 (성별 전용 풀 + 중립 풀에서 선택). 키만 저장하고 표시할 때 현재 언어로 조회한다.
            instance.nameKey = PickAdventurerNameKey(data.gender);

            // 외형 생성
            if (appearanceGeneratorData != null)
            {
                var jobType = instance.GetHighestStat() switch
                {
                    AdventurerStat.STR => AdventurerJobType.STR,
                    AdventurerStat.DEX => AdventurerJobType.DEX,
                    AdventurerStat.INT => AdventurerJobType.INT,
                    AdventurerStat.LUK => AdventurerJobType.LUK,
                    _ => AdventurerJobType.STR
                };
                instance.appearance = AppearanceGenerator.Generate(appearanceGeneratorData, data.gender, jobType);
            }

            Log.Info($"[VisitorManager] 일반 모험가 생성: {instance.Name} STR:{instance.STR} DEX:{instance.DEX} INT:{instance.INT} LUK:{instance.LUK}");
            return instance;
        }

        /// <summary>
        /// 성별 전용 이름 풀과 중립 이름 풀을 합쳐 균등 추첨해 Names 테이블 키를 반환한다.
        /// 두 풀을 이어붙인 하나의 범위에서 뽑으므로 이름 하나하나가 같은 확률을 갖는다.
        /// </summary>
        private string PickAdventurerNameKey(Gender gender)
        {
            List<string> genderPool = gender == Gender.Male
                ? maleAdventurerNamePool
                : femaleAdventurerNamePool;

            int total = genderPool.Count + neutralAdventurerNamePool.Count;
            if (total == 0)
            {
                Log.Warn("[VisitorManager] 이름 풀이 비어 있습니다. 인스펙터의 NamePool을 확인하세요.");
                return null;
            }

            int pick = Random.Range(0, total);
            return pick < genderPool.Count
                ? genderPool[pick]
                : neutralAdventurerNamePool[pick - genderPool.Count];
        }

        private void StartAdventurerSpawning()
        {
            isSpawningAdventurers = true;

            var (min, max) = ReputationManager.Instance.GetAdventurerSpawnInterval();
            min = Mathf.Max(min, Config.adventurerSpawnMinInterval);
            max = Mathf.Min(max, Config.adventurerSpawnMaxInterval);
            nextAdventurerSpawnInterval = Random.Range(min, max);
            lastAdventurerSpawnTime = 0f;
        }

        private void StopAdventurerSpawning()
        {
            isSpawningAdventurers = false;
            lastAdventurerSpawnTime = 0f;
            nextAdventurerSpawnInterval = 0f;
        }

        /// <summary>
        /// 재방문 판정 후 자격을 갖춘 네임드를 스폰한다. 스폰했으면 true.
        /// 자격 = 호감도 minAffectionForRevisit 이상 + 집에 있음 + 마지막 방문 후 min~maxRevisitDays 경과.
        /// 추첨 대상도 이 자격자로 한정한다 - 집에 있는 네임드 전원을 대상으로 하면
        /// 자격자가 소모되지 않아 재방문 게이트가 계속 열린 채로 유지된다.
        /// </summary>
        private bool TrySpawnReturningAdventurer()
        {
            if (namedAdventurerCache.Count == 0) return false;

            if (ActiveItemManager.Instance == null)
                return false;

            float baseChance = Config.baseRevisitChance;

            int maxAffectionCount = namedAdventurerCache.Values
                .Count(a => a.adventurerStatData.hasReachedMaxAffection && a.isAlive && !a.isVisiting);

            if (maxAffectionCount > 0)
            {
                float maxBonus = maxAffectionCount * Config.maxAffectionBonusPerAdventurer;
                baseChance = Mathf.Min(baseChance + maxBonus, Config.maxRevisitChance);
            }

            var returnCandidates = namedAdventurerCache.Values
                .Where(a => a.affection >= Config.minAffectionForRevisit && a.IsHome)
                .ToList();

            if (returnCandidates.Count == 0) return false;

            int currentDay = TimeManager.Instance.CurrentDay;
            var eligibleReturners = returnCandidates
                .Where(a => currentDay - a.adventurerStatData.lastVisitDay >= Random.Range(Config.minRevisitDays, Config.maxRevisitDays))
                .ToList();

            if (eligibleReturners.Count == 0) return false;

            float finalChance = Mathf.Min(baseChance, Config.maxRevisitChance);

            if (Random.value > finalChance) return false;

            SpawnReturningAdventurer(eligibleReturners);
            return true;
        }

        /// <summary>
        /// 재방문 자격자 중 호감도 가중 추첨으로 1명을 스폰한다.
        /// </summary>
        private void SpawnReturningAdventurer(List<AdventurerInstance> candidates)
        {
            float totalWeight = 0f;
            var weights = new List<float>();

            foreach (var adventurer in candidates)
            {
                float weight = adventurer.adventurerStatData.hasReachedMaxAffection ? Config.maxAffectionRevisitWeight : Config.normalRevisitWeight;
                weights.Add(weight);
                totalWeight += weight;
            }

            float randomValue = Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                // 경계 미포함(<) - randomValue가 정확히 0일 때 weight 0 후보 당첨 방지
                if (randomValue < cumulative)
                {
                    SpawnAdventurer(candidates[i]);
                    return;
                }
            }

            SpawnAdventurer(candidates[^1]);
        }

        /// <summary>
        /// 단골 모험가 유산 업그레이드: Day % 7 == 0 일 때 네임드 모험가 확정 스폰.
        /// 스폰 성공 시 true 반환.
        /// </summary>
        private bool TrySpawnGuaranteedAdventurer()
        {
            if (LegacyManager.Instance?.HasRegularAdventurer() != true) return false;

            int currentDay = TimeManager.Instance.CurrentDay;
            if (currentDay % 7 != 0 || currentDay <= 0) return false;

            // 집에 있고 살아있는 네임드 중 애정도 높은 순으로 선택
            var candidates = namedAdventurerCache.Values
                .Where(a => a.isAlive && a.IsHome)
                .OrderByDescending(a => a.affection)
                .ToList();

            if (candidates.Count == 0)
            {
                Log.Info($"[VisitorManager] 단골 모험가 보장 발동 (Day {currentDay}) — 집에 있는 네임드 없음, 일반 스폰으로 대체");
                return false;
            }

            var selected = candidates[0];
            Log.Info($"[VisitorManager] 단골 모험가 보장 발동 (Day {currentDay}) — {selected.Name} 확정 방문");
            SpawnAdventurer(selected);
            return true;
        }

        private void SpawnNewAdventurer()
        {
            float namedW  = Config.namedAdventurerSpawnWeight + (LegacyManager.Instance?.GetNamedSpawnWeightBonus() ?? 0f);
            float normalW = Config.normalAdventurerSpawnWeight;
            bool spawnNamed = Random.value < namedW / (namedW + normalW);

            if (spawnNamed)
                SpawnNamedAdventurer();
            else
                SpawnNormalAdventurer();
        }

        /// <summary>
        /// 네임드 모험가 스폰
        /// </summary>
        private void SpawnNamedAdventurer()
        {
            int maxRetries = 5;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                // 집에 있는 산 자 + 죽은 네임드(부활 대기)를 후보로 — 죽은 자가 뽑히면 사망 이벤트 발동
                var candidates = namedAdventurerCache.Values
                    .Where(i => i.IsHome || !i.isAlive)
                    .ToList();

                if (candidates.Count == 0)
                {
                    // 살아있는 네임드가 모두 모험/방문 중
                    float randomValue = Random.value;
                    if (randomValue <= 0.70f)
                    {
                        Log.Info($"[VisitorManager] 네임드 모험가가 집에 없음. 재선택 시도 ({retry + 1}/{maxRetries})");
                        continue;
                    }
                    else
                    {
                        TriggerNoAdventurerAtHomeEvent();
                        return;
                    }
                }

                var selected = SelectWithGradeWeight(candidates);

                if (selected == null)
                {
                    Log.Warn("[VisitorManager] 선택된 네임드 모험가가 없습니다.");
                    return;
                }

                if (!selected.isAlive)
                {
                    HandleDeadAdventurerSelected(selected);
                    return;
                }

                SpawnAdventurer(selected);
                return;
            }

            Log.Info($"[VisitorManager] 네임드 모험가 {maxRetries}번 재시도 실패. 일반 모험가로 대체.");
            SpawnNormalAdventurer();
        }

        /// <summary>
        /// 일반 모험가 스폰 — 사전 생성된 풀에서 순서대로 소비
        /// </summary>
        private void SpawnNormalAdventurer()
        {
            if (dailyNormalVisitorPool.Count == 0 || dailyNormalVisitorIndex >= dailyNormalVisitorPool.Count)
            {
                var fallback = CreateNormalAdventurerInstance();
                if (fallback != null)
                {
                    dailyNormalVisitorPool.Add(fallback);
                    dailyNormalVisitorIndex++;
                    SpawnAdventurer(fallback);
                }
                else
                    Log.Warn("[VisitorManager] 일반 모험가 인스턴스 생성 실패.");
                return;
            }

            var instance = dailyNormalVisitorPool[dailyNormalVisitorIndex];
            dailyNormalVisitorIndex++;
            SpawnAdventurer(instance);
        }

        private AdventurerInstance SelectWithGradeWeight(List<AdventurerInstance> instances)
        {
            if (instances == null || instances.Count == 0) return null;

            const float namedWeight  = 1f;
            const float normalWeight = 5f;

            float totalWeight = 0f;
            var weights = new List<float>();

            foreach (var instance in instances)
            {
                float w = instance.isNamed ? namedWeight : normalWeight;
                weights.Add(w);
                totalWeight += w;
            }

            if (totalWeight <= 0) return instances[Random.Range(0, instances.Count)];

            float randomValue = Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;

            for (int i = 0; i < instances.Count; i++)
            {
                cumulativeWeight += weights[i];
                // 경계 미포함(<) - weight 0 후보 당첨 방지
                if (randomValue < cumulativeWeight) return instances[i];
            }

            return instances[Random.Range(0, instances.Count)];
        }

        private void SpawnAdventurer(AdventurerInstance instance)
        {
            if (instance == null)
            {
                Log.Error("[VisitorManager] SpawnAdventurer - instance is null");
                return;
            }

            instance.isVisiting = true;

            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);
            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();

            if (instance.appearance == null)
            {
                if (instance.isNamed && instance.adventurerData.appearance != null
                    && instance.adventurerData.appearance.partsIndices.Count > 0)
                {
                    instance.appearance = AppearanceGenerator.GenerateFromFixed(
                        instance.adventurerData.appearance.ToPartsDictionary(),
                        instance.adventurerData.appearance.skinColor,
                        instance.adventurerData.appearance.hairColor,
                        instance.adventurerData.appearance.beardColor,
                        instance.adventurerData.appearance.browColor);
                }
                else if (appearanceGeneratorData != null)
                {
                    // 일반 모험가: 랜덤 생성
                    var jobType = instance.GetHighestStat() switch
                    {
                        AdventurerStat.STR => AdventurerJobType.STR,
                        AdventurerStat.DEX => AdventurerJobType.DEX,
                        AdventurerStat.INT => AdventurerJobType.INT,
                        AdventurerStat.LUK => AdventurerJobType.LUK,
                        _ => AdventurerJobType.STR
                    };
                    instance.appearance = AppearanceGenerator.Generate(
                        appearanceGeneratorData,
                        instance.adventurerData.gender,
                        jobType);
                }
            }

            // 시간 스킵 중 스폰이면 걷는 연출 없이 즉시 배치 — 입장 애니메이션이 실프레임을 못 받아
            // isEntering이 스킵 내내 true로 남으면 체류 타이머가 얼어붙는 문제 방지
            npc.Initialize(VisitorType.Adventurer, instance, null, TimeManager.Instance.IsSkippingTime);

            RegisterVisitor(npc);

            AnalyticsManager.Instance?.Send("visitor_spawned", new Dictionary<string, object>
            {
                { "adventurer_id", instance.adventurerData != null ? instance.adventurerData.StaticID : "unknown" },
                { "is_named", instance.isNamed }
            });

            Log.Info($"[VisitorManager] {instance.Name} 방문");
        }

        #endregion
    }
}
