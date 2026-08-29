using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace TodaysWeaponRental
{
    public partial class VisitorManager
    {
        #region 모험가 인스턴스 관리

        public void InitializeAllAdventurers()
        {
            var allAdventurerData = DataManager.Instance?.GetAllAdventurers();

            foreach (var data in allAdventurerData)
            {
                if (!data.isNamed) continue;
                if (IsTutorialOnlyAdventurer(data.StaticID)) continue;

                var instance = new AdventurerInstance(data, 1);
                instance.defaultWeapon = CreateDefaultWeaponFor(instance);

                if (data.appearance != null && data.appearance.partsIndices.Count > 0)
                {
                    instance.appearance = AppearanceGenerator.GenerateFromFixed(
                        data.appearance.ToPartsDictionary(),
                        data.appearance.skinColor,
                        data.appearance.hairColor,
                        data.appearance.beardColor,
                        data.appearance.browColor);
                }

                namedAdventurerCache[instance.instanceID] = instance;
            }
        }

        /// <summary>
        /// 튜토리얼 전용 모험가(TutorialConfig에 등록된 ID)인지 여부.
        /// 이들은 네임드지만 일반 네임드 스폰 풀(namedAdventurerCache)에서 제외한다.
        /// 튜토리얼 스폰은 SpawnTutorialAdventurer가 DataManager에서 별도 인스턴스를 직접 생성하므로 영향 없음.
        /// </summary>
        private bool IsTutorialOnlyAdventurer(string staticID)
        {
            var tutorial = ConfigManager.Instance?.Tutorial;
            if (tutorial == null || string.IsNullOrEmpty(staticID)) return false;
            return staticID == tutorial.TutorialAdventurer1ID
                || staticID == tutorial.TutorialAdventurer2ID;
        }

        /// <summary>
        /// 튜토리얼 전용 모험가 인스턴스를 생성하고 영속 캐시에 등록한다.
        /// 스폰 풀(namedAdventurerCache)과 분리된 캐시라 이후 날짜의 네임드 추첨에는 영향이 없고,
        /// 진행 중 모험이 저장/복원될 때 adventurerInstanceID를 해석할 수 있게 된다.
        /// 튜토리얼 스폰과 스킵 catch-up 양쪽이 이 메서드로 생성해야 한다.
        /// </summary>
        public AdventurerInstance CreateTutorialAdventurer(AdventurerData data)
        {
            if (data == null)
            {
                Log.Error("[VisitorManager] CreateTutorialAdventurer - data is null");
                return null;
            }

            var instance = new AdventurerInstance(data, TimeManager.Instance.CurrentDay);
            tutorialAdventurerCache[instance.instanceID] = instance;
            return instance;
        }

        public void MarkAdventurerDead(string instanceID)
        {
            // 사망한 모험가에게 출발 전 배정 아이템이 남아 있으면 폐기한다
            ActiveItemManager.Instance?.TryDiscardAssignedItem(instanceID, "adventurer-dead");

            if (namedAdventurerCache.TryGetValue(instanceID, out AdventurerInstance instance))
            {
                instance.MarkAsDead();
                Log.Info($"[VisitorManager] 모험가 사망 처리 (ID: {instanceID.Substring(0, 8)}...)");
            }
        }

        public AdventurerInstance GetNamedAdventurerInstance(string instanceID)
        {
            namedAdventurerCache.TryGetValue(instanceID, out AdventurerInstance instance);
            return instance;
        }

        public AdventurerInstance GetNormalAdventurerInstance(string instanceID)
        {
            if (normalAdventurerCache.TryGetValue(instanceID, out AdventurerInstance instance))
                return instance;

            return dailyNormalVisitorPool.FirstOrDefault(i => i.instanceID == instanceID);
        }

        public AdventurerInstance GetAdventurerInstance(string instanceID)
        {
            var instance = GetNamedAdventurerInstance(instanceID) ?? GetNormalAdventurerInstance(instanceID);
            if (instance != null) return instance;

            // 튜토리얼 전용 모험가는 스폰 풀 밖이라 마지막에 조회한다
            tutorialAdventurerCache.TryGetValue(instanceID, out instance);
            return instance;
        }

        public void RegisterNormalAdventurer(AdventurerInstance instance)
        {
            if (instance == null || instance.isNamed) return;
            normalAdventurerCache[instance.instanceID] = instance;
            Log.Info($"[VisitorManager] 일반 모험가 파견 등록: {instance.Name}");
        }

        public void UnregisterNormalAdventurer(string instanceID)
        {
            if (normalAdventurerCache.Remove(instanceID))
                Log.Info($"[VisitorManager] 일반 모험가 캐시 해제: {instanceID}");
        }

        /// <summary>
        /// 모험가 부활 처리
        /// </summary>
        public void ReviveAdventurer(string instanceID)
        {
            if (namedAdventurerCache.TryGetValue(instanceID, out AdventurerInstance instance))
            {
                instance.isAlive = true;
                Log.Info($"[VisitorManager] {instance.Name} 부활 완료");
            }
        }

        #endregion

        #region 사망 모험가 처리

        /// <summary>
        /// 사망한 모험가가 선택되었을 때 이벤트 처리
        /// </summary>
        private void HandleDeadAdventurerSelected(AdventurerInstance deadAdventurer)
        {
            float randomValue = Random.value;

            if (randomValue <= 0.70f) // 70%: 같은 등급 내 살아있는 모험가 재선택
            {
                var sameGradeCandidates = namedAdventurerCache.Values
                    .Where(i => i.IsHome && i.isNamed == deadAdventurer.isNamed)
                    .ToList();

                var excluded = new HashSet<string>();
                AdventurerInstance selected = null;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    var pool = sameGradeCandidates
                        .Where(i => !excluded.Contains(i.instanceID))
                        .ToList();

                    if (pool.Count == 0) break;

                    var candidate = pool[Random.Range(0, pool.Count)];

                    if (candidate.isAlive)
                    {
                        selected = candidate;
                        break;
                    }

                    excluded.Add(candidate.instanceID);
                }

                string category = deadAdventurer.isNamed ? "네임드" : "일반";
                if (selected != null)
                {
                    Log.Info($"[VisitorManager] 70% - 같은 분류({category}) 재선택 성공: {selected.Name}");
                    SpawnAdventurer(selected);
                }
                else
                {
                    Log.Info($"[VisitorManager] 70% - 같은 분류({category}) 내 살아있는 모험가 없음 → NoVisitor 처리");
                    SpawnDeadEventNPC(DeadEventKind.NoVisitor, deadAdventurer);
                }
            }
            else if (randomValue <= 0.85f) // 15%: 청소 이벤트
            {
                Log.Info($"[VisitorManager] 15% 발동 - NoVisitor 이벤트");
                SpawnDeadEventNPC(DeadEventKind.NoVisitor, deadAdventurer);
            }
            else if (randomValue <= 0.95f) // 10%: 부활 제안
            {
                Log.Info($"[VisitorManager] 10% 발동 - ReviveOffer 이벤트");
                SpawnDeadEventNPC(DeadEventKind.ReviveOffer, deadAdventurer);
            }
            else // 5%: 기적의 생환
            {
                Log.Info($"[VisitorManager] 5% 발동 - MiracleRevive 이벤트");
                SpawnDeadEventNPC(DeadEventKind.MiracleRevive, deadAdventurer);
            }
        }

        /// <summary>
        /// 30% 이벤트: 모험가가 모두 집에 없음 → 청소(NoVisitor) NPC로 통일
        /// </summary>
        private void TriggerNoAdventurerAtHomeEvent()
        {
            Log.Info("[VisitorManager] NoAdventurerAtHome 이벤트 발생");
            SpawnDeadEventNPC(DeadEventKind.NoVisitor, null);
        }

        /// <summary>
        /// 다음 스폰 시각까지 게임 시간을 즉시 스킵하고 모험가를 스폰한다.
        /// 스킵 도중 낮 페이즈를 벗어나면(저녁/밤) 스폰하지 않는다.
        /// </summary>
        public void SkipToNextSpawn()
        {
            if (TimeManager.Instance == null) return;

            float remaining = nextAdventurerSpawnInterval - lastAdventurerSpawnTime;
            if (remaining > 0)
                TimeManager.Instance.AdvanceGameTime(Mathf.CeilToInt(remaining));

            // 스폰은 AdvanceGameTime의 틱 루프 중 OnTimeSkipped -> CheckAdventurerSpawnByGameTime이
            // 이미 수행한다. 여기서 다시 호출하면 모험가가 중복 스폰된다.
        }

        #endregion
    }
}
