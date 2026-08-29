// Scripts/Systems/AdventureManager.Calculations.cs
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// AdventureManager의 계산 로직 부분
    /// </summary>
    public partial class AdventureManager
    {
        #region 모험 관리
        
        /// <summary>
        /// 던전 통계 반환. 기록 없으면 null.
        /// </summary>
        public DungeonStatData GetDungeonStat(string dungeonID)
        {
            var playerData = LegacyManager.Instance.PlayerData;
            playerData.dungeonStats.TryGetValue(dungeonID, out var stat);
            return stat;
        }
        
        /// <summary>
        /// 모험 시작
        /// </summary>
        public AdventureInstance StartAdventure(AdventurerInstance adventurer, WeaponInstance weapon, DungeonData dungeon)
        {
            if (adventurer == null || weapon == null || dungeon == null)
            {
                Log.Error("StartAdventure: null parameter");
                return null;
            }

            adventurer.RentWeapon(weapon);
            adventurer.isAdventuring = true;

            List<AdventureEvent> events = GenerateEventSequence(dungeon, weapon, TimeManager.Instance.CurrentTime);

            // 보호 횟수 초기화
            int initialProtection = 0;
            foreach (var effect in weapon.effects)
            {
                if (effect.effectData.effectType == WeaponEffectType.RetreatPrevention)
                    initialProtection += Mathf.RoundToInt(effect.currentValue);
            }
            // 생존가 특성: 보호 +1
            if (adventurer.Trait == TraitType.Enduring)
                initialProtection += TraitCfg.traitEnduringProtectionBonus;

            // 포션: 보호 횟수 추가
            initialProtection += ActiveItemManager.Instance.GetPotionProtection(adventurer.instanceID);

            AdventureInstance adventure = new(adventurer, weapon, dungeon, initialProtection, events);

            // 액티브 아이템 효과값 캐싱 (ConsumeAssignedItem 전에 읽어야 함)
            adventure.goldAmuletBonus       = ActiveItemManager.Instance.GetGoldAmuletBonus(adventurer.instanceID);
            adventure.fameScrollBonus       = ActiveItemManager.Instance.GetFameScrollBonus(adventurer.instanceID);
            adventure.disassemblyKnifeBonus = ActiveItemManager.Instance.GetDisassemblyKnifeBonus(adventurer.instanceID);
            adventure.deathWardBonus        = ActiveItemManager.Instance.GetDeathWardBonus(adventurer.instanceID);
            adventure.escapeRopeBonus       = ActiveItemManager.Instance.GetEscapeRopeBonus(adventurer.instanceID);
            adventure.swiftShoesMultiplier  = ActiveItemManager.Instance.GetSwiftShoesMultiplier(adventurer.instanceID);
            adventure.treasureMapBonus      = ActiveItemManager.Instance.GetTreasureMapBonus(adventurer.instanceID);

            // 배정 아이템 소비
            ActiveItemManager.Instance.ConsumeAssignedItem(adventurer.instanceID);

            // 점술 운세 버프/디버프 적용
            adventure.cumulativeModifier = SeerManager.Instance.GetLuckModifier(adventurer, dungeon);
            adventure.seerUsed = SeerManager.Instance.GetExistingResult(adventurer, dungeon) != null;

            // 기분 결정 (모험 출발 시 1회)
            adventure.mood = DetermineMood(adventurer, weapon);

            // §9: 전체 시퀀스를 시작 시점에 시뮬레이션으로 해결 (결과 1:1 확정·삽입·누적).
            // 반드시 RentWeapon·아이템 캐싱·ConsumeAssignedItem·Seer·mood 이후에 호출해야
            // per-event 모델과 동일한 상태(charm/아이템/mood/cumulativeModifier)를 본다.
            ResolveSequence(adventure, 0);

            // ⚠ 아래부터는 adventure의 보상 계산 입력(charm/아이템/mood/cumulativeModifier)을 바꾸지 말 것.
            //   ResolveSequence가 위에서 전 이벤트 결과를 이미 확정했으므로, 상태 변경 코드는 반드시 그 호출 "위"에 둔다.
            ongoingAdventures.Add(adventure);
            if (!adventurer.isNamed)
                VisitorManager.Instance.RegisterNormalAdventurer(adventurer);
            SaveOngoingAdventures();
            // 출발 확정 - 강제 종료 후 재파견으로 RNG를 다시 굴리는 것 차단
            GameManager.Instance?.SaveAfterCommittedAction("Adventure.Start");

            OnAdventureStarted?.Invoke(adventure);

            SendAdventureStartedAnalytics(adventure);

            Log.Info($"Adventure started: {adventurer.Name} → {dungeon.dungeonName} (Events: {events.Count})");

            return adventure;
        }

        /// <summary>adventure_started 이벤트 발행 (Documents/Analytics_이벤트_설계.md Level 4)</summary>
        private void SendAdventureStartedAnalytics(AdventureInstance adventure)
        {
            var adventurer = adventure.adventurer;
            var parameters = new Dictionary<string, object>
            {
                { "dungeon_id", adventure.dungeon.StaticID },
                { "weapon_id", adventure.weapon.weaponData.StaticID },
                { "weapon_grade", (int)adventure.weapon.currentGrade },
                { "adventurer_id", adventurer.adventurerData != null ? adventurer.adventurerData.StaticID : "unknown" },
                { "seer_used", adventure.seerUsed },
                { "revealed_stat_count", adventurer.isStatsFullyRevealed ? 4 : adventurer.revealedStatIndices.Count },
                { "trait_revealed", adventurer.isTraitRevealed },
                { "weapon_is_player_selected", !adventure.isUsingDefaultWeapon },
                { "armor_type_match", GetWeaponArmorBonus(adventure.weapon.weaponData.weaponType, adventure.effectiveArmorType) > 0 },
                { "is_named_adventurer", adventurer.isNamed }
            };

            // 대화 패널을 거치지 않은 경로(복원 등)에서는 기록이 없으므로 파라미터 제외
            int dialogOpenSec = AnalyticsManager.Instance?.ConsumeDialogOpenDurationSec() ?? -1;
            if (dialogOpenSec >= 0)
                parameters["dialog_open_duration_sec"] = dialogOpenSec;

            AnalyticsManager.Instance?.Send("adventure_started", parameters);
        }
                
#if UNITY_EDITOR
        /// <summary>
        /// 디버그용: 모든 MonsterCategory 배틀을 순서대로 전부 승리하는 완료된 모험 즉시 생성.
        /// 리플레이로 카테고리별 몬스터 렌더(MonsterStageConfig 프레이밍) 검증.
        /// </summary>
        public void CreateAllMonsterCategoryCompletedDebugAdventure()
        {
            var setup = SetupAllMonsterCategoryDebug();
            if (setup == null) return;
            var (adventurer, dungeon, weapon, battleEvents) = setup.Value;

            // 시퀀스: 입장 → 카테고리별 배틀(전부 성공) → 보스(성공) → 귀환
            var sequence = new List<(AdventureEvent evt, bool success)>();
            void Add(DungeonEventData data, bool isSuccess)
            {
                if (data == null) return;
                sequence.Add((new AdventureEvent(data), isSuccess));
            }

            Add(DataManager.Instance.GetDungeonEventByType(DungeonEventType.Entrance), true);
            foreach (var battle in battleEvents) Add(battle, true);
            Add(dungeon.eventPool.FirstOrDefault(e => e != null && e.eventType == DungeonEventType.Boss)
                ?? DataManager.Instance.GetDungeonEventByType(DungeonEventType.Boss), true);
            Add(DataManager.Instance.GetDungeonEventByType(DungeonEventType.Return), true);

            var events = sequence.Select(s => s.evt).ToList();
            var adventure = new AdventureInstance(adventurer, weapon, dungeon, Config.maxProtectionCharges, events);

            ongoingAdventures.Add(adventure);
            OnAdventureStarted?.Invoke(adventure);

            for (int i = 0; i < sequence.Count; i++)
            {
                var (evt, isSuccess) = sequence[i];
                evt.result = BuildDebugEventResult(evt.eventData.eventType, isSuccess, adventure);
                adventure.progress.currentEventIndex++;

                bool terminal = ApplyPendingResult(adventure, evt);
                if (terminal) break;
            }
            CompleteAdventure(adventure);

            Log.Info($"[AdventureManager] 전 몬스터 카테고리 완료 모험 생성: {adventurer.Name} → {dungeon.dungeonName} (배틀 {battleEvents.Count}종)");
        }

        /// <summary>
        /// 디버그용: 모험가가 사망하는 "완료된" 모험 즉시 생성.
        /// 사망 리플레이 연출(Hit → Die) 검증용. 시퀀스: 입장 → 배틀(성공) → 배틀(실패) → 후퇴.
        /// 보호 0으로 시작해 실패가 재도전 없이 종료되게 하고, isDeath는 강제 설정한다.
        /// </summary>
        public void CreateDeathDebugAdventure()
        {
            var setup = SetupAllMonsterCategoryDebug();
            if (setup == null) return;
            var (adventurer, dungeon, weapon, battleEvents) = setup.Value;
            var battle = battleEvents[0];

            var sequence = new List<(AdventureEvent evt, bool success)>();
            void Add(DungeonEventData data, bool isSuccess)
            {
                if (data == null) return;
                sequence.Add((new AdventureEvent(data), isSuccess));
            }

            Add(DataManager.Instance.GetDungeonEventByType(DungeonEventType.Entrance), true);
            Add(battle, true);
            Add(battle, false);   // 사망 유발 전투 — 마지막 전투 노드에서 Hit 재생
            Add(DataManager.Instance.GetDungeonEventByType(DungeonEventType.Retreat), false);

            var events = sequence.Select(s => s.evt).ToList();
            var adventure = new AdventureInstance(adventurer, weapon, dungeon, 0, events);

            ongoingAdventures.Add(adventure);
            OnAdventureStarted?.Invoke(adventure);

            for (int i = 0; i < sequence.Count; i++)
            {
                var (evt, isSuccess) = sequence[i];
                evt.result = BuildDebugEventResult(evt.eventData.eventType, isSuccess, adventure);
                adventure.progress.currentEventIndex++;

                bool terminal = ApplyPendingResult(adventure, evt);
                if (terminal) break;
            }

            adventure.isDeath = true;   // 디버그: 강제 사망 (실제 판정은 ResolveSequence에서만 수행됨)
            CompleteAdventure(adventure);

            Log.Info($"[AdventureManager] 사망 모험 생성: {adventurer.Name} → {dungeon.dungeonName}");
        }

        /// <summary>
        /// 디버그용: 모든 MonsterCategory 배틀을 순서대로 거치는 "진행 중" 모험 즉시 생성.
        /// AdventureProgressView 진행 카드에서 카테고리별 몬스터가 시간 진행에 따라 순서대로 표시된다.
        /// 완료 처리는 하지 않고 ongoingAdventures에만 추가 — 런타임이 시간 도달 시 다음 이벤트로 전진.
        /// </summary>
        public void CreateAllMonsterCategoryOngoingDebugAdventure()
        {
            var setup = SetupAllMonsterCategoryDebug();
            if (setup == null) return;
            var (adventurer, dungeon, weapon, battleEvents) = setup.Value;

            // 시퀀스: 입장 → 카테고리별 배틀 → 보스. 각 이벤트 결과를 강제 성공으로 미리 확정해
            // 런타임(TryProcessNextEvent)이 종료 이벤트로 잘라내지 않고 끝까지 순서대로 전진하게 한다.
            var events = new List<AdventureEvent>();
            void Add(DungeonEventData data)
            {
                if (data != null) events.Add(new AdventureEvent(data));
            }

            Add(DataManager.Instance.GetDungeonEventByType(DungeonEventType.Entrance));
            foreach (var battle in battleEvents) Add(battle);
            Add(dungeon.eventPool.FirstOrDefault(e => e != null && e.eventType == DungeonEventType.Boss)
                ?? DataManager.Instance.GetDungeonEventByType(DungeonEventType.Boss));

            var adventure = new AdventureInstance(adventurer, weapon, dungeon, Config.maxProtectionCharges, events);

            // 모든 이벤트를 강제 성공 결과로 확정 (완료 처리·보상 누적은 하지 않음 — 렌더 확인 목적)
            foreach (var evt in events)
                evt.result = BuildDebugEventResult(evt.eventData.eventType, true, adventure);

            // 첫 이벤트(입장)부터 진행 시작: startTime 설정해 진행 카드가 현재 이벤트를 렌더하도록
            var first = events[0];
            first.startTime = TimeManager.Instance.CurrentTime;

            ongoingAdventures.Add(adventure);
            OnAdventureStarted?.Invoke(adventure);
            OnAdventureEventTriggerStarted?.Invoke(adventure, first);
            SaveOngoingAdventures();

            Log.Info($"[AdventureManager] 전 몬스터 카테고리 진행 모험 생성: {adventurer.Name} → {dungeon.dungeonName} (배틀 {battleEvents.Count}종)");
        }

        /// <summary>
        /// 전 몬스터 카테고리 디버그 모험의 공통 준비: 모험가/던전/무기 확보 + 카테고리별 배틀 이벤트 수집.
        /// 하나라도 실패하면 null.
        /// </summary>
        private (AdventurerInstance adventurer, DungeonData dungeon, WeaponInstance weapon, List<DungeonBattleEventData> battleEvents)?
            SetupAllMonsterCategoryDebug()
        {
            var adventurer = CreateDebugAdventurer();
            if (adventurer == null) return null;

            var dungeon = DataManager.Instance.GetAllDungeons()
                .OrderByDescending(d => (int)d.grade)
                .FirstOrDefault();
            if (dungeon == null)
            {
                Log.Error("[AdventureManager] 전 몬스터 카테고리 디버그: 던전 데이터가 없습니다.");
                return null;
            }

            var weapon = CreateDebugWeapon();
            if (weapon == null) return null;

            var battleEvents = new List<DungeonBattleEventData>();
            foreach (MonsterCategory category in System.Enum.GetValues(typeof(MonsterCategory)))
            {
                var battle = DataManager.Instance.GetBattleEventByMonsterType(category);
                if (battle != null) battleEvents.Add(battle);
                else Log.Warn($"[AdventureManager] 전 몬스터 카테고리 디버그: '{category}' 배틀 이벤트 없음 (건너뜀).");
            }
            if (battleEvents.Count == 0)
            {
                Log.Error("[AdventureManager] 전 몬스터 카테고리 디버그: 배틀 이벤트가 하나도 없습니다.");
                return null;
            }

            return (adventurer, dungeon, weapon, battleEvents);
        }

        /// <summary>
        /// 디버그용: 모든 비전투 이벤트 타입을 한 번씩 거치는 "완료된" 모험 즉시 생성.
        /// 결과창에서 비전투 이벤트별 노드/보상 렌더를 검증. 보스가 없어 isSuccess가 잡히지 않으므로
        /// 완료 직전 수동으로 성공 처리해 누적 골드/재료가 결과에 반영되게 한다.
        /// Retreat은 Return과 종료 상호배타라 완료 변종에서는 제외(진행 변종에서 확인).
        /// </summary>
        public void CreateAllNonBattleCompletedDebugAdventure()
        {
            var setup = SetupNonBattleDebug();
            if (setup == null) return;
            var (adventurer, dungeon, weapon) = setup.Value;

            var sequence = new List<(AdventureEvent evt, bool success)>();

            DungeonEventData FromPool(DungeonEventType t) =>
                dungeon.eventPool.FirstOrDefault(e => e != null && e.eventType == t)
                ?? DataManager.Instance.GetDungeonEventByType(t);
            DungeonEventData Global(DungeonEventType t) =>
                DataManager.Instance.GetDungeonEventByType(t);

            void Add(DungeonEventData data, bool isSuccess)
            {
                if (data == null) { Log.Warn($"[AdventureManager] 이벤트 데이터 없음 (success={isSuccess})"); return; }
                sequence.Add((new AdventureEvent(data), isSuccess));
            }

            Add(Global(DungeonEventType.Entrance),        true);
            Add(FromPool(DungeonEventType.Rest),           true);
            Add(FromPool(DungeonEventType.TreasureChest),  true);
            Add(FromPool(DungeonEventType.RareDrop),       true);
            Add(FromPool(DungeonEventType.Trap),           true);   // 함정 회피
            Add(Global(DungeonEventType.TrapEvade),        true);
            Add(FromPool(DungeonEventType.Trap),           false);  // 함정 피격
            Add(Global(DungeonEventType.Protection),       true);
            Add(Global(DungeonEventType.Retry),            true);
            Add(Global(DungeonEventType.Return),           true);   // 종료(귀환)

            var events = sequence.Select(s => s.evt).ToList();
            var adventure = new AdventureInstance(adventurer, weapon, dungeon, Config.maxProtectionCharges, events);

            ongoingAdventures.Add(adventure);
            OnAdventureStarted?.Invoke(adventure);

            for (int i = 0; i < sequence.Count; i++)
            {
                var (evt, isSuccess) = sequence[i];
                evt.result = BuildDebugEventResult(evt.eventData.eventType, isSuccess, adventure);
                adventure.progress.currentEventIndex++;

                bool terminal = ApplyPendingResult(adventure, evt);
                if (terminal) break;
            }

            // 비전투 전용: 보스 성공 이벤트가 없어 isSuccess가 잡히지 않으므로 수동으로 성공 처리
            adventure.isSuccess = true;
            CompleteAdventure(adventure);

            Log.Info($"[AdventureManager] 전 비전투 완료 모험 생성: {adventurer.Name} → {dungeon.dungeonName} ({sequence.Count}개 이벤트)");
        }

        /// <summary>
        /// 디버그용: 모든 비전투 이벤트 타입을 순서대로 거치는 "진행 중" 모험 즉시 생성.
        /// AdventureProgressView 진행 카드에서 비전투 이벤트가 시간 진행에 따라 순서대로 렌더된다.
        /// 런타임은 ApplyPendingResult를 거치지 않고 시간 경과로만 전진하므로 종료(Retreat/Return)
        /// 이벤트도 잘리지 않고 노드로 표시된다. Trap은 회피/피격 둘 다 포함.
        /// </summary>
        public void CreateAllNonBattleOngoingDebugAdventure()
        {
            var setup = SetupNonBattleDebug();
            if (setup == null) return;
            var (adventurer, dungeon, weapon) = setup.Value;

            var sequence = new List<(AdventureEvent evt, bool success)>();

            DungeonEventData FromPool(DungeonEventType t) =>
                dungeon.eventPool.FirstOrDefault(e => e != null && e.eventType == t)
                ?? DataManager.Instance.GetDungeonEventByType(t);
            DungeonEventData Global(DungeonEventType t) =>
                DataManager.Instance.GetDungeonEventByType(t);

            void Add(DungeonEventData data, bool isSuccess)
            {
                if (data == null) { Log.Warn($"[AdventureManager] 이벤트 데이터 없음 (success={isSuccess})"); return; }
                sequence.Add((new AdventureEvent(data), isSuccess));
            }

            Add(Global(DungeonEventType.Entrance),        true);
            Add(FromPool(DungeonEventType.Rest),           true);
            Add(FromPool(DungeonEventType.TreasureChest),  true);
            Add(FromPool(DungeonEventType.RareDrop),       true);
            Add(FromPool(DungeonEventType.Trap),           true);   // 함정 회피
            Add(Global(DungeonEventType.TrapEvade),        true);
            Add(FromPool(DungeonEventType.Trap),           false);  // 함정 피격
            Add(Global(DungeonEventType.Protection),       true);
            Add(Global(DungeonEventType.Retry),            true);
            Add(Global(DungeonEventType.Retreat),          false);  // 종료(후퇴) — 진행 카드 노드 확인용
            Add(Global(DungeonEventType.Return),           true);   // 종료(귀환) — 진행 카드 노드 확인용

            var events = sequence.Select(s => s.evt).ToList();
            if (events.Count == 0)
            {
                Log.Error("[AdventureManager] 전 비전투 진행 디버그: 이벤트가 하나도 없습니다.");
                return;
            }
            var adventure = new AdventureInstance(adventurer, weapon, dungeon, Config.maxProtectionCharges, events);

            // 모든 이벤트를 강제 결과로 확정 (완료 처리·보상 누적은 하지 않음 — 렌더 확인 목적)
            foreach (var (evt, isSuccess) in sequence)
                evt.result = BuildDebugEventResult(evt.eventData.eventType, isSuccess, adventure);

            // 첫 이벤트(입장)부터 진행 시작: startTime 설정해 진행 카드가 현재 이벤트를 렌더하도록
            var first = events[0];
            first.startTime = TimeManager.Instance.CurrentTime;

            ongoingAdventures.Add(adventure);
            OnAdventureStarted?.Invoke(adventure);
            OnAdventureEventTriggerStarted?.Invoke(adventure, first);
            SaveOngoingAdventures();

            Log.Info($"[AdventureManager] 전 비전투 진행 모험 생성: {adventurer.Name} → {dungeon.dungeonName} ({sequence.Count}개 이벤트)");
        }

        /// <summary>
        /// 전 비전투 디버그 모험의 공통 준비: 모험가/던전/무기 확보. 하나라도 실패하면 null.
        /// </summary>
        private (AdventurerInstance adventurer, DungeonData dungeon, WeaponInstance weapon)? SetupNonBattleDebug()
        {
            var adventurer = CreateDebugAdventurer();
            if (adventurer == null) return null;

            var dungeon = DataManager.Instance.GetAllDungeons()
                .OrderByDescending(d => (int)d.grade)
                .FirstOrDefault();
            if (dungeon == null)
            {
                Log.Error("[AdventureManager] 전 비전투 디버그: 던전 데이터가 없습니다.");
                return null;
            }

            var weapon = CreateDebugWeapon();
            if (weapon == null) return null;

            return (adventurer, dungeon, weapon);
        }

        /// <summary>
        /// 디버그용(밸런스 측정): 랜덤 모험가 x 랜덤 던전 1건을 실제 확률 판정으로 즉시 해결하고
        /// AdventureResult만 completedResults에 남긴다.
        /// ongoing/completedAdventures에 넣지 않으므로 전령 보고 대상이 되지 않고,
        /// ProcessRewards/ApplyStatisticsOnce도 호출하지 않아 골드·평판·도감·탐험도가 오염되지 않는다.
        /// useRandomWeapon=true면 랜덤 무기를 즉석 생성해 대여한다(인벤토리에 넣지 않으므로 소지 상한과 무관).
        /// </summary>
        public void CreateRandomDebugResult(bool useRandomWeapon)
        {
            var adventurer = VisitorManager.Instance?.CreateNormalAdventurerInstance();
            if (adventurer == null)
            {
                Log.Error("[AdventureManager] CreateRandomDebugResult: 일반 모험가 생성 실패.");
                return;
            }

            WeaponInstance weapon;
            if (useRandomWeapon)
            {
                var allWeapons = DataManager.Instance.GetAllWeapons();
                if (allWeapons == null || allWeapons.Count == 0)
                {
                    Log.Error("[AdventureManager] CreateRandomDebugResult: 무기 데이터가 없습니다.");
                    return;
                }
                // 생성자가 baseGrade에 맞는 효과까지 굴려준다. 인벤토리에 넣지 않아 소지 상한을 타지 않는다.
                weapon = new WeaponInstance(allWeapons[Random.Range(0, allWeapons.Count)], false);
            }
            else
            {
                weapon = adventurer.defaultWeapon;
            }

            if (weapon == null)
            {
                Log.Error("[AdventureManager] CreateRandomDebugResult: 무기가 없습니다.");
                return;
            }

            var dungeons = DataManager.Instance.GetAllDungeons();
            if (dungeons == null || dungeons.Count == 0)
            {
                Log.Error("[AdventureManager] CreateRandomDebugResult: 던전 데이터가 없습니다.");
                return;
            }
            var dungeon = dungeons[Random.Range(0, dungeons.Count)];

            // 아래는 StartAdventure에서 판정에 영향을 주는 입력만 재현한 것이다.
            // 리스트 등록/세이브/이벤트 발행은 의도적으로 생략한다.
            adventurer.RentWeapon(weapon);

            int initialProtection = 0;
            foreach (var effect in weapon.effects)
            {
                if (effect.effectData.effectType == WeaponEffectType.RetreatPrevention)
                    initialProtection += Mathf.RoundToInt(effect.currentValue);
            }
            if (adventurer.Trait == TraitType.Enduring)
                initialProtection += TraitCfg.traitEnduringProtectionBonus;

            var events = GenerateEventSequence(dungeon, weapon, TimeManager.Instance.CurrentTime);
            var adventure = new AdventureInstance(adventurer, weapon, dungeon, initialProtection, events);
            adventure.mood = DetermineMood(adventurer, weapon);

            ResolveSequence(adventure, 0);   // 실제 성공률/사망 판정 수행
            adventure.isCompleted = true;

            var result = new AdventureResult(adventure)
            {
                isAdventurerTypeMatch = !weapon.isDefaultWeapon &&
                    weapon.weaponData.weaponType == adventurer.GetBestWeaponType(),
                isDungeonTypeMatch = GetWeaponArmorBonus(
                    weapon.weaponData.weaponType, adventure.effectiveArmorType) > 0,
                statisticsApplied = true   // 통계는 이미 처리된 것으로 표시해 도감/탐험도 오염을 막는다
            };
            CalculateRewards(adventure, result);

            completedResults.Add(result);

            string outcome = result.isDeath ? "사망" : result.isSuccess ? "성공" : "실패";
            Log.Info($"[AdventureManager] 디버그 결과 생성: {dungeon.dungeonName}({dungeon.grade}) / " +
                      $"{weapon.weaponData.DisplayName}({weapon.currentGrade}, 기본무기={weapon.isDefaultWeapon}) " +
                      $"-> {outcome}, 무기파괴={result.weaponLost} (누적 {completedResults.Count}건)");
        }

        private EventResult BuildDebugEventResult(DungeonEventType type, bool isSuccess, AdventureInstance adventure, bool bossFailureUsesProtection = true)
        {
            var result = new EventResult
            {
                eventType         = type,
                isSuccess         = isSuccess,
                successRateAtTime = isSuccess ? 1f : 0f,
                materialDrops     = new List<MaterialInstance>(),
                bonusMaterials    = new List<MaterialInstance>(),
            };

            switch (type)
            {
                case DungeonEventType.Battle:
                    if (isSuccess)
                    {
                        var (gold, bonus) = CalculateEventGold(adventure, Config.battleRewardMultiplier, 0f);
                        result.goldReward = gold + bonus;
                        result.bonusGold  = bonus;
                    }
                    break;
                case DungeonEventType.MiniBoss:
                    if (isSuccess)
                    {
                        var (gold, bonus) = CalculateEventGold(adventure, Config.miniBossRewardMultiplier, 0f);
                        result.goldReward = gold + bonus;
                        result.bonusGold  = bonus;
                    }
                    break;
                case DungeonEventType.TreasureChest:
                {
                    var (gold, bonus) = CalculateEventGold(adventure, Config.treasureChestRewardMultiplier, 0f);
                    result.goldReward = gold + bonus;
                    result.bonusGold  = bonus;
                }
                    break;
                case DungeonEventType.RareDrop:
                {
                    var (mats, bonusMats) = CalculateRareDropMaterials(adventure);
                    result.materialDrops  = mats;
                    result.bonusMaterials = bonusMats;
                }
                    break;
                case DungeonEventType.Boss:
                    if (isSuccess)
                    {
                        var (gold, bonus) = CalculateEventGold(adventure, Config.bossRewardMultiplier, 0f);
                        result.goldReward = gold + bonus;
                        result.bonusGold  = bonus;
                        var (mats, bonusMats) = CalculateMaterialDropsWithBonus(adventure, false);
                        result.materialDrops  = mats;
                        result.bonusMaterials = bonusMats;
                    }
                    else
                    {
                        result.protectionActivated = bossFailureUsesProtection;
                    }
                    break;
                case DungeonEventType.Protection:
                    result.protectionActivated = true;
                    break;
            }

            return result;
        }

        /// <summary>
        /// 디버그 모험용 모험가: 랜덤 일반 모험가를 매번 새로 만들어 파견 등록까지 한다.
        /// 네임드 풀(집에 있는 살아있는 모험가)에 의존하지 않으므로 개수 제한 없이 생성할 수 있다.
        /// 등록은 세이브/로드 시 instanceID로 모험가를 다시 찾기 위해 필요하다(StartAdventure와 동일 규칙).
        /// </summary>
        private AdventurerInstance CreateDebugAdventurer()
        {
            var adventurer = VisitorManager.Instance.CreateNormalAdventurerInstance();
            if (adventurer == null)
            {
                Log.Error("[AdventureManager] 디버그 모험: 일반 모험가 생성에 실패했습니다.");
                return null;
            }

            VisitorManager.Instance.RegisterNormalAdventurer(adventurer);
            return adventurer;
        }

        private WeaponInstance CreateDebugWeapon()
        {
            var allWeapons = DataManager.Instance.GetAllWeapons();
            if (allWeapons.Count == 0)
            {
                Log.Error("[AdventureManager] CreateDebugWeapon: 무기 데이터가 없습니다.");
                return null;
            }
            var weaponData = allWeapons[Random.Range(0, allWeapons.Count)];

            var effectData = DataManager.Instance.GetEffectByTypeAndGrade(
                WeaponEffectType.RetreatPrevention, Grade.Legendary, 0, 0, 0);
            if (effectData == null)
            {
                Log.Error("[AdventureManager] CreateDebugWeapon: RetreatPrevention 효과 데이터를 찾을 수 없습니다.");
                return null;
            }

            float maxValue = WeaponEffect.IsIntegerType(effectData.effectType)
                ? Mathf.RoundToInt(effectData.baseValueRange.y)
                : effectData.baseValueRange.y;

            var saveData = new WeaponInstanceSaveData
            {
                weaponDataID = weaponData.StaticID,
                instanceID   = System.Guid.NewGuid().ToString(),
                currentGrade = Grade.Legendary,
                effects = new List<WeaponEffectSaveData>
                {
                    new WeaponEffectSaveData
                    {
                        effectType   = effectData.effectType,
                        effectDataID = effectData.StaticID,
                        grade        = effectData.grade,
                        baseValue    = effectData.baseValueRange.x,
                        currentValue = maxValue,
                    }
                }
            };

            var effects = InventoryManager.ReifyEffectsFromSave(saveData.effects);
            return new WeaponInstance(weaponData, effects, saveData);
        }
#endif

        /// <summary>
        /// 모험 완료 처리
        /// </summary>
        public AdventureResult CompleteAdventure(AdventureInstance adventure)
        {
            if (adventure == null || adventure.isCompleted)
                return null;

            adventure.isCompleted = true;
            adventure.completedDay = TimeManager.Instance.CurrentDay;
            adventure.completedAtUtcTicks = System.DateTime.UtcNow.Ticks;

            AdventureResult result = new AdventureResult(adventure)
            {
                // 대여 무기가 모험가의 최적 무기 타입일 때만 매칭. 기본 무기는 70% 확률로
                // 최적 타입이 배정되므로(CreateDefaultWeaponFor) 제외해야 플레이어 선택에만 보상이 붙는다.
                isAdventurerTypeMatch = !adventure.weapon.isDefaultWeapon &&
                    adventure.weapon.weaponData.weaponType == adventure.adventurer.GetBestWeaponType(),
                isDungeonTypeMatch = GetWeaponArmorBonus(
                    adventure.weapon.weaponData.weaponType, adventure.effectiveArmorType) > 0
            };

            // 사망 보호 체크 (기존 유지)
            if (adventure.isDeath && adventure.adventurer.adventurerStatData.hasDeathProtection)
            {
                adventure.isDeath = false;
                result.isDeath = false;
                result.deathProtectionUsed = true;
                adventure.adventurer.adventurerStatData.hasDeathProtection = false;
                adventure.adventurer.affection = Mathf.RoundToInt(
                    adventure.adventurer.affection * Config.deathProtectionAffectionRatio);

                Log.Info($"Death protection activated for {adventure.adventurer.Name}");
            }

            CalculateRewards(adventure, result);

            // 통계는 결과 확인이 아니라 완료 시점에 확정한다.
            // 확인 전 게임오버/세이브 드롭에도 탐험도/도감이 보존된다
            ApplyStatisticsOnce(adventure, result);

            ongoingAdventures.Remove(adventure);
            completedAdventures.Add(adventure);
            completedResults.Add(result);

            SaveOngoingAdventures();
            GameManager.Instance?.SaveAfterCommittedAction("Adventure.Complete");
            OnAdventureCompleted?.Invoke(adventure, result);

            SendAdventureCompletedAnalytics(adventure, result);

            return result;
        }

        /// <summary>adventure_completed 이벤트 발행 (Documents/Analytics_이벤트_설계.md Level 4)</summary>
        private void SendAdventureCompletedAnalytics(AdventureInstance adventure, AdventureResult result)
        {
            var adventurer = adventure.adventurer;
            AnalyticsManager.Instance?.Send("adventure_completed", new Dictionary<string, object>
            {
                { "dungeon_id", adventure.dungeon.StaticID },
                { "weapon_id", adventure.weapon.weaponData.StaticID },
                { "weapon_grade", (int)adventure.weapon.currentGrade },
                { "outcome", GetOutcomeAnalyticsName(result) },
                { "duration_min", Mathf.RoundToInt(TimeManager.Instance.CurrentTime - adventure.startTime) },
                { "seer_used", adventure.seerUsed },
                { "weapon_is_player_selected", !adventure.isUsingDefaultWeapon },
                { "is_named_adventurer", adventurer.isNamed },
                { "revealed_stat_count", adventurer.isStatsFullyRevealed ? 4 : adventurer.revealedStatIndices.Count },
                { "trait_revealed", adventurer.isTraitRevealed },
                { "armor_type_match", result.isDungeonTypeMatch }
            });
        }

        /// <summary>Analytics adventure_completed.outcome 표기 (great_success/success/failure/death/retreat)</summary>
        private static string GetOutcomeAnalyticsName(AdventureResult result)
        {
            if (result.isDeath) return "death";
            if (result.isRetreated) return "retreat";
            if (result.isGreatSuccess) return "great_success";
            if (result.isSuccess) return "success";
            return "failure";
        }

        /// <summary>
        /// 모험 결과 확인 (클릭 시 보상 지급)
        /// </summary>
        public void ConfirmAdventureResult(AdventureInstance adventure)
        {
            if (adventure == null || !adventure.isCompleted)
            {
                Log.Warn("Invalid adventure for result confirmation");
                return;
            }

            // completedResults에서 해당 결과 찾기
            AdventureResult result = completedResults.Find(r => r.adventureID == adventure.instanceID);
            if (result == null)
            {
                Log.Error($"AdventureResult not found for {adventure.instanceID}");
                return;
            }

            // 보상 지급
            ProcessRewards(adventure, result);
            
            if (adventure.adventurer != null)
            {
                if (!adventure.adventurer.isNamed)
                    VisitorManager.Instance.UnregisterNormalAdventurer(adventure.adventurer.instanceID);
                else
                    adventure.adventurer.isAdventuring = false;
            }  
            
            // completedAdventures에서 제거
            completedAdventures.Remove(adventure);

            // completedResults는 통계용이므로 유지
            SaveOngoingAdventures();
            // 보상 지급 확정 - 강제 종료로 보상 재수령/회수하는 세이브스컴 차단
            GameManager.Instance?.SaveAfterCommittedAction("Adventure.ConfirmResult");
        }

#if UNITY_EDITOR
        /// <summary>
        /// 디버그용: 완료된 모험을 결과 확인(보상 지급) 없이 모두 제거한다.
        /// 모험가 상태만 해제해 집으로 복귀시키고, 보상은 지급하지 않는다.
        /// </summary>
        public void RemoveCompletedDebugAdventures()
        {
            if (completedAdventures.Count == 0)
            {
                Log.Info("[AdventureManager] RemoveCompletedDebugAdventures: 제거할 완료된 모험이 없습니다.");
                return;
            }

            int count = completedAdventures.Count;

            // 보상은 지급하지 않고 모험가 상태만 해제 (ConfirmAdventureResult와 동일한 해제 처리)
            foreach (AdventureInstance adventure in completedAdventures)
            {
                if (adventure.adventurer != null)
                {
                    if (!adventure.adventurer.isNamed)
                        VisitorManager.Instance.UnregisterNormalAdventurer(adventure.adventurer.instanceID);
                    else
                        adventure.adventurer.isAdventuring = false;
                }
            }

            completedAdventures.Clear();

            // completedResults는 통계용이므로 유지
            SaveOngoingAdventures();

            Log.Info($"[AdventureManager] 완료된 모험 {count}개를 확인 없이 제거했습니다.");
        }
#endif

        /// <summary>
        /// 보상 계산 (지급하지 않고 계산만)
        /// </summary>
        private void CalculateRewards(AdventureInstance adventure, AdventureResult result)
        {
            // 사망: 무기 손실, 골드 없음
            if (result.isDeath)
            {
                // 기본 무기는 ProcessRewards에서 파괴 대상이 아니므로 손실로 기록하지 않는다
                result.weaponLost = !adventure.weapon.isDefaultWeapon;
                result.totalGoldReward = 0;
                result.goldReward = 0;
                result.playerGoldReward = 0;
                result.reputationChange = Config.deathReputationChange;
                result.affectionChange = 0;
                return;
            }

            // 후퇴 (= 모험 실패): 누적 보상 일부 지급, 평판/호감도 감소
            if (adventure.isRetreated)
            {
                float failRatio = Config.retreatGoldRatio;
                bool hasFailBonus = false;
                foreach (var effect in adventure.weapon.effects)
                {
                    if (effect.effectData.effectType == WeaponEffectType.FailGoldBonus)
                    {
                        failRatio += effect.currentValue;
                        hasFailBonus = true;
                    }
                }
                failRatio = Mathf.Min(failRatio, 1f);
                result.totalGoldReward = Mathf.RoundToInt((adventure.accumulatedGold + adventure.accumulatedBonusGold) * failRatio);

                if (hasFailBonus && result.totalGoldReward > 0)
                    adventure.goldPreservationTriggered = true;

                result.materialDrops = MergeMaterialLists(adventure.accumulatedMaterials, adventure.accumulatedBonusMaterials);
                result.reputationChange = Mathf.Min(-1,
                    CountClearedEvents(adventure) / 2 - GetGradeRepFailBase(adventure.dungeon.grade));
                result.affectionChange = Config.failAffectionLoss;
                ApplyCommission(adventure, result);
                // 9단계 튜토리얼: 실패 던전(B)은 대여 무기(활)를 파괴한다(일반 실패는 반납만). ProcessRewards가 weaponLost로 처리.
                if (TryGetTutorialForcedBattleSuccess(adventure, out bool forcedOutcome) && !forcedOutcome)
                    result.weaponLost = !adventure.weapon.isDefaultWeapon;
                return;
            }

            // 보스 클리어 (성공)
            if (adventure.isSuccess)
            {
                result.totalGoldReward = adventure.accumulatedGold + adventure.accumulatedBonusGold;
                // 황금 부적: 골드 배율 보너스
                if (adventure.goldAmuletBonus > 0f)
                    result.totalGoldReward = Mathf.RoundToInt(result.totalGoldReward * (1f + adventure.goldAmuletBonus));
                result.materialDrops = MergeMaterialLists(adventure.accumulatedMaterials, adventure.accumulatedBonusMaterials);
                result.reputationChange = CountClearedEvents(adventure) / 2
                                          + GetGradeRepBonus(adventure.dungeon.grade);
                if (result.isAdventurerTypeMatch) result.reputationChange += Config.typeMatchReputationGain;
                if (adventure.isGreatSuccess) result.reputationChange += Config.greatSuccessReputationGain;
                result.reputationChange += GetTraitReputationBonus(adventure.adventurer);
                // 명예의 두루마리: 평판 배율. 다른 보너스를 전부 합산한 뒤 곱해야
                // 타입 매칭·대성공까지 함께 증폭된다
                if (adventure.fameScrollBonus > 0f)
                    result.reputationChange = Mathf.RoundToInt(
                        result.reputationChange * (1f + adventure.fameScrollBonus));
                result.affectionChange = Config.successAffectionGain;
                if (result.isAdventurerTypeMatch) result.affectionChange += Config.typeMatchAffectionGain;
                if (adventure.isGreatSuccess)
                    result.insightChange = ConfigManager.Instance.Insight.greatSuccessInsightReward;
                ApplyCommission(adventure, result);
            }
        }

        /// <summary>
        /// 수수료 계산 후 result에 반영
        /// </summary>
        private void ApplyCommission(AdventureInstance adventure, AdventureResult result)
        {
            result.baseCommissionRate = Config.baseCommissionRate;
            result.rentalCommissionRate = adventure.weapon.isDefaultWeapon ? 0f : Config.rentalCommissionRate;
            if (!adventure.weapon.isDefaultWeapon)
                result.rentalCommissionRate += LegacyManager.Instance?.GetCommissionRateBonus() ?? 0f;

            float armorBonus = TypeAdvantage.weaponArmorBonus[
                (int)adventure.weapon.weaponData.weaponType,
                (int)adventure.effectiveArmorType];

            if (armorBonus >= Config.tipThreshold4)
                result.tipCommissionRate = Config.tipRate4;
            else if (armorBonus >= Config.tipThreshold3)
                result.tipCommissionRate = Config.tipRate3;
            else if (armorBonus >= Config.tipThreshold2)
                result.tipCommissionRate = Config.tipRate2;
            else if (armorBonus >= Config.tipThreshold1)
                result.tipCommissionRate = Config.tipRate1;
            else
                result.tipCommissionRate = 0f;

            if (result.tipCommissionRate > 0f)
                result.tipCommissionRate += LegacyManager.Instance?.GetTipRateBonus() ?? 0f;

            // 특성 수수료: 다른 수수료와 동일하게 totalGold × flatRate 방식
            float baseTotal = result.baseCommissionRate + result.rentalCommissionRate + result.tipCommissionRate;
            // 흥정꾼(-15%)처럼 플랫 차감 특성은 기본 수수료(7%)보다 커서 총 수수료율을 음수로 만들 수 있다.
            // 차감 하한을 -baseTotal로 묶어 총 수수료율 0%(수입 0G)까지만 떨어지게 한다.
            result.traitCommissionRate = Mathf.Max(-baseTotal,
                GetTraitCommissionRate(adventure.adventurer, baseTotal));

            result.totalCommissionRate = baseTotal + result.traitCommissionRate;
            result.playerGoldReward = Mathf.RoundToInt(result.totalGoldReward * result.totalCommissionRate);
            result.goldReward = result.totalGoldReward; // 하위 호환
        }

        /// <summary>
        /// 실제 보상 지급 처리
        /// </summary>
        private void ProcessRewards(AdventureInstance adventure, AdventureResult result)
        {
            // 통계는 완료 시점(CompleteAdventure)에 확정된다. 구버전 세이브의 미적용 결과만 여기서 보정
            ApplyStatisticsOnce(adventure, result);

            if (result.isDeath)
            {
                VisitorManager.Instance.MarkAdventurerDead(adventure.adventurer.instanceID);
                if (!adventure.weapon.isDefaultWeapon)
                    InventoryManager.Instance.RemoveWeapon(adventure.weapon);
                ReputationManager.Instance.AddReputation(result.reputationChange, "모험가 사망");
                Log.Info($"{adventure.adventurer.Name} died in {adventure.dungeon.dungeonName}");
                return;
            }

            // 공통 보상 지급 (후퇴/보스패배/성공 모두)
            if (result.playerGoldReward > 0)
                EconomyManager.Instance.AddGold(result.playerGoldReward,
                    adventure.isSuccess ? "모험 성공" : adventure.isRetreated ? "모험 후퇴" : "보스 패배");

            ReputationManager.Instance.AddReputation(result.reputationChange,
                adventure.isSuccess ? "모험 성공" : "모험 실패");
            adventure.adventurer.AddAffection(result.affectionChange);

            if (result.insightChange > 0)
                InsightManager.Instance?.AddInsight(result.insightChange);

            if (!adventure.weapon.isDefaultWeapon)
            {
                if (result.weaponLost)
                    InventoryManager.Instance.RemoveWeapon(adventure.weapon);   // 9단계 튜토리얼: 활 파괴(반납 아님)
                else
                    adventure.weapon.Return();
            }

            // 퀘스트는 성공 시에만
            if (adventure.isSuccess && QuestManager.Instance != null)
            {
                QuestManager.Instance.UpdateProgress(QuestType.SuccessfulAdventures);
                QuestManager.Instance.UpdateProgress(QuestType.RentSpecificGrade,
                    grade: adventure.weapon.currentGrade);
                QuestManager.Instance.UpdateProgress(QuestType.RentSpecificWeapon,
                    weaponType: adventure.weapon.weaponData.weaponType);
                QuestManager.Instance.UpdateProgress(QuestType.CompleteSpecificDungeon,
                    dungeonID: adventure.dungeon.StaticID);
                if (result.isGreatSuccess)
                    QuestManager.Instance.UpdateProgress(QuestType.GreatSuccessCount);
            }
        }
        
        private List<MaterialInstance> MergeMaterialLists(List<MaterialInstance> baseList, List<MaterialInstance> bonusList)
        {
            var merged = new List<MaterialInstance>();
            foreach (var m in baseList)
            {
                var existing = merged.FirstOrDefault(x => x.materialDataID == m.materialDataID);
                if (existing != null) existing.quantity += m.quantity;
                else merged.Add(new MaterialInstance(m.materialData, m.quantity));
            }
            foreach (var m in bonusList)
            {
                var existing = merged.FirstOrDefault(x => x.materialDataID == m.materialDataID);
                if (existing != null) existing.quantity += m.quantity;
                else merged.Add(new MaterialInstance(m.materialData, m.quantity));
            }
            return merged;
        }

        #endregion

        #region 성공률 계산
        
        /// <summary>
        /// 성공률 계산. difficultyMultiplier(이벤트 난이도 계수)는 최종 성공률이 아니라
        /// 요구 스탯 임계값에 적용된다 - 계수 c는 "임계값 1/c배"를 뜻하므로(보스 0.7 = 1.43배),
        /// 스탯/상성/부가효과에 충분히 투자하면 보스도 100%까지 도달할 수 있다.
        /// 특정 이벤트가 정해지지 않은 호출(UI 표시 등)은 기본값 1f = 일반 전투 기준을 쓴다.
        /// </summary>
        public float CalculateSuccessRate(AdventurerInstance adventurer, WeaponInstance weapon, DungeonData dungeon, ArmorType effectiveArmorType, float difficultyMultiplier = 1f)
        {
            WeaponInstance activeWeapon = weapon;

            // 1. 효과 포함 스탯 기반 기본 성공률 (이벤트 난이도만큼 요구 임계값이 올라간다)
            float effectStatScore = GetEffectStatScore(adventurer, activeWeapon);
            float effectBase = Mathf.Clamp(effectStatScore * difficultyMultiplier / dungeon.baseStatThreshold, 0f, 1f);

            // 2. 무기-던전 상성 + 부적을 기본 성공률에 곱
            //    여기서 1로 자르지 않는다 - 초과분은 3번의 가산 보정(음수 가능)과 합산된 뒤 한 번만 클램프된다.
            float armorBonus = TypeAdvantage.weaponArmorBonus[
                (int)activeWeapon.weaponData.weaponType,
                (int)effectiveArmorType
            ];
            float charmRate = GetActiveItemBonus(adventurer);

            float baseRate = effectBase * (1f + armorBonus + charmRate);

            // 3. 조건부 직접 보정 합산
            float bonusSum = 0f;
            bonusSum += CalculateConditionBonus(activeWeapon, dungeon, effectiveArmorType);
            bonusSum += GetCollectionBonus(dungeon, adventurer, activeWeapon);
            bonusSum += GetAffectionBonus(adventurer);
            bonusSum += GetTraitSuccessBonus(adventurer, dungeon);

            float result = Mathf.Clamp(baseRate + bonusSum, Config.successRateMin, Config.successRateMax);
            result *= GetTraitSuccessMultiplier(adventurer);

            // 기본 무기는 파괴 리스크가 없고 스탯 궁합도 자동으로 맞춰지므로 성공률에 페널티를 준다
            if (activeWeapon.isDefaultWeapon)
                result *= Config.defaultWeaponSuccessMultiplier;

            return result;
        }
        
        public float CalculateDeathRate(AdventurerInstance adventurer, WeaponInstance weapon, DungeonData dungeon, float deathWardBonus = 0f)
        {
            float baseRate = Config.baseDeathRate;

            // 무기 보호 효과 (등급 + 강화)
            int weaponGradeDiff = (int)weapon.currentGrade - (int)dungeon.grade;
            float weaponProtection = weaponGradeDiff * Config.weaponProtectionGradeDiff;
            float enforcementProtection = weapon.enforceLevel * Config.weaponProtectionEnforcement;

            baseRate -= weaponProtection + enforcementProtection;

            // 모험가 스탯이 던전 요구치에 미달한 만큼 사망률 가산 (초과 달성분은 무시)
            float statRatio = GetEffectStatScore(adventurer, weapon) / dungeon.baseStatThreshold;
            baseRate += Mathf.Max(0f, 1f - statRatio) * Config.deathRateStatWeight;

            float rate = Mathf.Clamp(baseRate / 100f, 0f, Config.maxDeathRate);
            rate *= 1f - deathWardBonus;
            return rate * GetTraitDeathMultiplier(adventurer);
        }

        /// <summary>
        /// 사망 굴림이 떴을 때 STR로 되살아나는 재굴림 확률.
        /// </summary>
        public float GetStrengthSurvivalChance(AdventurerInstance adventurer)
        {
            if (adventurer == null) return 0f;
            return StatCurve.Evaluate(adventurer.STR, Config.strDeathRerollMax, Config.strDeathRerollExponent);
        }

        /// <summary>
        /// 함정 회피 확률. 회피 소스(무기 TrapNegation, 탈출 로프, DEX)는 서로 독립이라
        /// "전부 실패할 확률"의 여집합이 최종 회피 확률이 된다.
        /// moodMultiplier는 TrapNegation에만 곱해진다 - 런타임 판정은 절반 강도 배율을 넘기고,
        /// UI 표시는 기분이 아직 정해지지 않았으므로 기본값 1f로 부른다.
        /// </summary>
        public float CalculateTrapEvadeChance(AdventurerInstance adventurer, WeaponInstance weapon,
            float escapeRopeBonus, float moodMultiplier = 1f)
        {
            if (adventurer == null) return 0f;

            float missChance = 1f;

            if (weapon?.effects != null)
                foreach (var effect in weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.TrapNegation)
                        missChance *= 1f - Mathf.Clamp01(effect.currentValue * moodMultiplier);

            missChance *= 1f - Mathf.Clamp01(escapeRopeBonus);
            missChance *= 1f - StatCurve.Evaluate(
                adventurer.DEX, Config.dexTrapEvadeMax, Config.dexTrapEvadeExponent);

            return 1f - missChance;
        }

        // 대성공 확률 계산
        private bool CalculateGreatSuccess(DungeonData dungeon, AdventurerInstance adventurer)
        {
            var playerData = LegacyManager.Instance.PlayerData;

            // 1. 탐험도 100% 확정권 체크
            if (playerData.dungeonStats.TryGetValue(dungeon.StaticID, out var stat))
            {
                if (stat.hasGreatSuccessReady)
                {
                    stat.hasGreatSuccessReady = false;
                    stat.explorationProgress = 0;
                    return true;
                }
            }
            
            // 2. 확률 판정 (확정권은 위에서 이미 소모돼 여기서는 걸리지 않는다)
            return Random.value <= GetGreatSuccessChance(dungeon, adventurer, adventurer.ActiveWeapon, out _);
        }

        /// <summary>
        /// 대성공 확률 조회 (UI 표시용). CalculateGreatSuccess와 달리 확정권을 소모하지 않는다.
        /// 탐험도 100% 확정권을 들고 있으면 guaranteed=true, 확률 1f.
        /// </summary>
        public float GetGreatSuccessChance(DungeonData dungeon, AdventurerInstance adventurer,
            WeaponInstance weapon, out bool guaranteed)
        {
            guaranteed = false;
            if (dungeon == null || adventurer == null) return 0f;

            var playerData = LegacyManager.Instance?.PlayerData;
            if (playerData != null
                && playerData.dungeonStats.TryGetValue(dungeon.StaticID, out var stat)
                && stat.hasGreatSuccessReady)
            {
                guaranteed = true;
                return 1f;
            }

            float weaponBonus = 0f;
            if (weapon?.effects != null)
                foreach (var effect in weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.GreatSuccessBonus)
                        weaponBonus += effect.currentValue;

            // 유산 보너스는 곱계산
            float legacyMult = LegacyManager.Instance?.GetGreatSuccessRateMultiplier() ?? 1f;
            return (Config.baseGreatSuccessChance + GetTraitGreatSuccessBonus(adventurer) + weaponBonus) * legacyMult;
        }

        #endregion
        
        #region Bonus 계산

        /// <summary>
        /// 성공률 분해 반환 (UI 표시용)
        /// </summary>
        public SuccessRateBreakdown CalculateSuccessRateBreakdown(
    AdventurerInstance adventurer, WeaponInstance weapon, DungeonData dungeon, ArmorType effectiveArmorType)
    {
        // 무기-던전 상성 테이블 상수
        float armorBonus = TypeAdvantage.weaponArmorBonus[
            (int)weapon.weaponData.weaponType, (int)effectiveArmorType];

        // 순수 스탯 기반 성공률 (효과 없음)
        float pureStatScore = adventurer.GetStatScore(weapon.weaponData.weaponType);
        float pureBase = Mathf.Clamp(pureStatScore / dungeon.baseStatThreshold, 0f, 1f);

        // StatBonus/AllStatBonus/WeaponTypeMatchBonus 적용 후 스탯 기반 성공률 증가분
        float effectStatScore = GetEffectStatScore(adventurer, weapon);
        float statEffectBonus = Mathf.Clamp(effectStatScore / dungeon.baseStatThreshold, 0f, 1f) - pureBase;

        // 조건부 직접 효과 (DungeonGradeBonus, ArmorTypeBonus)
        float conditionBonus = 0f;
        float armorTypeBonusOnly = 0f;
        if (weapon.effects != null)
        {
            foreach (var effect in weapon.effects)
            {
                switch (effect.effectData.effectType)
                {
                    case WeaponEffectType.DungeonGradeBonus:
                        if ((int)dungeon.grade == effect.effectData.targetGrade)
                            conditionBonus += effect.currentValue;
                        break;
                    case WeaponEffectType.ArmorTypeBonus:
                        if ((int)effectiveArmorType == effect.effectData.targetArmorType)
                        {
                            conditionBonus += effect.currentValue;
                            armorTypeBonusOnly += effect.currentValue;
                        }
                        break;
                }
            }
        }

        return new SuccessRateBreakdown
        {
            baseRate           = pureBase,
            armorBonus         = armorBonus,       // 테이블 상수 (곱 보정 표시용)
            statEffectBonus    = statEffectBonus,
            conditionBonus     = conditionBonus,
            armorTypeBonusOnly = armorTypeBonusOnly,
        };
    }

        /// <summary>
        /// 조건 충족 여부를 고려하지 않고 효과 보정값만 반환 (UI 필터링용)
        /// dungeon이 null이면 던전 조건부 효과는 0 반환
        /// </summary>
        public bool IsEffectConditionMet(
            WeaponEffect effect, AdventurerInstance adventurer, DungeonData dungeon, ArmorType effectiveArmorType)
        {
            return effect.effectData.effectType switch
            {
                WeaponEffectType.DungeonGradeBonus =>
                    dungeon != null && (int)dungeon.grade == effect.effectData.targetGrade,
                WeaponEffectType.ArmorTypeBonus =>
                    dungeon != null && (int)effectiveArmorType == effect.effectData.targetArmorType,
                WeaponEffectType.StatBonus or WeaponEffectType.AllStatBonus =>
                    false,
                // 조건 없는 효과들은 항상 true
                _ => true
            };
        }

        /// <summary>
        /// 모험 준비 화면에서 효과 한 줄의 표시 상태(Active/Inactive)를 결정한다.
        /// ArmorTypeBonus는 effectiveArmorType(실제 또는 시뮬레이션 방어 타입)과 타깃 일치 여부로 판정.
        /// </summary>
        public EffectDisplayState GetEffectDisplayState(
            WeaponEffect effect, AdventurerInstance adventurer, DungeonData dungeon,
            ArmorType effectiveArmorType)
        {
            switch (effect.effectData.effectType)
            {
                case WeaponEffectType.StatBonus:
                case WeaponEffectType.AllStatBonus:
                case WeaponEffectType.WeaponTypeMatchBonus:
                    return EffectDisplayState.Active;
                case WeaponEffectType.ArmorTypeBonus:
                    return (dungeon != null && (int)effectiveArmorType == effect.effectData.targetArmorType)
                        ? EffectDisplayState.Active : EffectDisplayState.Inactive;
                default:
                    return IsEffectConditionMet(effect, adventurer, dungeon, effectiveArmorType)
                        ? EffectDisplayState.Active : EffectDisplayState.Inactive;
            }
        }

        public float GetWeaponArmorBonus(WeaponType wpnType, ArmorType armorType)
        {
            return TypeAdvantage.weaponArmorBonus[(int)wpnType, (int)armorType];
        }

        /// <summary>
        /// 던전 이벤트 풀에서 해당 타입 이벤트의 난이도 계수를 찾는다 (UI 표시용).
        /// 풀에 없으면 hasEvent=false, 계수는 기본값 1f.
        /// </summary>
        public float GetEventDifficultyMultiplier(DungeonData dungeon, DungeonEventType type, out bool hasEvent)
        {
            var data = dungeon?.eventPool?.FirstOrDefault(e => e != null && e.eventType == type);
            hasEvent = data != null;
            return data != null ? data.difficultyMultiplier : 1f;
        }
        
        private float GetAffectionBonus(AdventurerInstance adventurer)
        {
            return adventurer.GetAffectionLevel() switch
            {
                AffectionLevel.Max => Config.affectionMaxBonus,
                AffectionLevel.High => Config.affectionHighBonus,
                AffectionLevel.Medium => Config.affectionMediumBonus,
                _ => 0f
            };
        }
        
        #region 특성 계산 (GetTrait*)

        /// <summary>성공률 가산 보너스 — Focused, BattleManiac, Rising, EasyExpert</summary>
        public float GetTraitSuccessBonus(AdventurerInstance adventurer, DungeonData dungeon)
        {
            return adventurer.Trait switch
            {
                TraitType.Focused      => TraitCfg.traitFocusedSuccessBonus,
                TraitType.BattleManiac => TraitCfg.traitBattleManiacSuccessBonus,
                TraitType.Rising       => ((int)dungeon.grade + 1) * TraitCfg.traitRisingBonusPerTier,
                TraitType.EasyExpert   => (int)dungeon.grade <= 1
                                            ? TraitCfg.traitEasyExpertLowTierBonus
                                            : TraitCfg.traitEasyExpertHighTierPenalty,
                _ => 0f
            };
        }

        /// <summary>성공률 곱배율 — Berserker, Coward</summary>
        public float GetTraitSuccessMultiplier(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Berserker => TraitCfg.traitBerserkerSuccessMultiplier,
                TraitType.Coward    => TraitCfg.traitCowardSuccessMultiplier,
                _ => 1f
            };
        }

        /// <summary>사망률 곱배율 — Berserker, Coward</summary>
        public float GetTraitDeathMultiplier(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Berserker => TraitCfg.traitBerserkerDeathMultiplier,
                TraitType.Coward    => TraitCfg.traitCowardDeathMultiplier,
                _ => 1f
            };
        }

        /// <summary>대성공 확률 가산 — Lucky</summary>
        private float GetTraitGreatSuccessBonus(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Lucky => TraitCfg.traitLuckyGreatSuccessBonus,
                _ => 0f
            };
        }

        /// <summary>재료 드롭 가산 개수 — Looter, Butcher</summary>
        private int GetTraitMaterialBonus(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Looter  => TraitCfg.traitLooterMaterialBonus,
                TraitType.Butcher => TraitCfg.traitButcherMaterialBonus,
                _ => 0
            };
        }

        /// <summary>소요 시간 곱배율 — Swift, Focused</summary>
        public float GetTraitDurationMultiplier(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Swift   => TraitCfg.traitSwiftDurationMultiplier,
                TraitType.Focused => TraitCfg.traitFocusedDurationMultiplier,
                _ => 1f
            };
        }

        /// <summary>탐험도 곱배율 — Veteran x2, Swift/BattleManiac = 0</summary>
        public float GetTraitExplorationMultiplier(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Swift        => 0f,
                TraitType.BattleManiac => 0f,
                TraitType.Veteran      => TraitCfg.traitVeteranExplorationMultiplier,
                _ => 1f
            };
        }

        /// <summary>특성 수수료율 — totalGold × rate 방식으로 다른 수수료와 동일</summary>
        private float GetTraitCommissionRate(AdventurerInstance adventurer, float baseTotal)
        {
            return adventurer.Trait switch
            {
                TraitType.Rich    => TraitCfg.traitRichTipRate,
                // 도축업자는 "수수료 배율"이므로 기존 수수료 합에 비례한 차감분으로 환산한다.
                // 배율값을 그대로 플랫 차감하면 팁 없는 모험(합 0.17)에서 총 수수료율이 음수가 되어 수입이 0이 된다.
                TraitType.Butcher => baseTotal * (TraitCfg.traitButcherFeeMultiplier - 1f),
                TraitType.Haggler => PickHagglerCommissionRate(),
                _ => 0f
            };
        }

        private float PickHagglerCommissionRate()
        {
            float[] rates = { TraitCfg.traitHagglerRateLow, TraitCfg.traitHagglerRateMid, TraitCfg.traitHagglerRateHigh };
            return rates[Random.Range(0, rates.Length)];
        }

        /// <summary>
        /// 평판 계산용 클리어 칸 수. 실제로 소요된 이벤트 칸에서
        /// 종료 연출(귀환/후퇴)과 모험을 끝낸 실패 전투를 뺀다.
        /// 보호/재도전은 칸을 차지하므로 포함한다 — 직전 실패 전투가 빠진 만큼 상쇄된다.
        /// 강조 던전(중간 칸 2배)과 EventCountBonus 무기는 칸이 늘어난 만큼 자동 반영된다.
        /// </summary>
        private int CountClearedEvents(AdventureInstance adventure)
        {
            var events = adventure?.progress?.events;
            if (events == null) return 0;

            int count = 0;
            foreach (var evt in events)
            {
                if (evt?.eventData == null) continue;

                DungeonEventType type = evt.eventData.eventType;
                if (type == DungeonEventType.Retreat || type == DungeonEventType.Return) continue;

                bool isBattle = type == DungeonEventType.Battle
                             || type == DungeonEventType.MiniBoss
                             || type == DungeonEventType.Boss;
                if (isBattle && evt.result != null && !evt.result.isSuccess) continue;

                count++;
            }
            return count;
        }

        /// <summary>모험 성공 시 등급별 평판 가산 (일반 2 / 고급 3 / 희귀 5 / 영웅 7 / 전설 11)</summary>
        private int GetGradeRepBonus(Grade grade)
        {
            int[] bonuses = Config.gradeRepBonus;
            int index = (int)grade;
            if (bonuses == null || index < 0 || index >= bonuses.Length) return 2;
            return bonuses[index];
        }

        /// <summary>모험 실패 시 등급별 감점 기준 (일반 3 / 고급 3 / 희귀 4 / 영웅 4 / 전설 5)</summary>
        private int GetGradeRepFailBase(Grade grade)
        {
            int[] bases = Config.gradeRepFailBase;
            int index = (int)grade;
            if (bases == null || index < 0 || index >= bases.Length) return 3;
            return bases[index];
        }

        /// <summary>평판 가산 — Famous</summary>
        private int GetTraitReputationBonus(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Famous => TraitCfg.traitFamousReputationBonus,
                _ => 0
            };
        }

        /// <summary>재료 구매가 배율 — Porter (짐꾼)</summary>
        public float GetTraitMaterialPriceMultiplier(AdventurerInstance adventurer)
        {
            return adventurer.Trait switch
            {
                TraitType.Porter => TraitCfg.traitPorterMaterialPriceMultiplier,
                _ => 1f
            };
        }

        #endregion
        
        private float GetCollectionBonus(DungeonData dungeon, AdventurerInstance adventurer, WeaponInstance weapon)
        {
            float bonus = 0f;
            var playerData = LegacyManager.Instance.PlayerData;

            if (playerData.dungeonStats.TryGetValue(dungeon.StaticID, out var clearStat))
            {
                int clearCount = clearStat.successCount;
                int milestones = clearCount / Config.dungeonClearMilestone;
                bonus += milestones * Config.dungeonClearMilestoneBonus;
            }

            if (playerData.weaponDiscoveryProgress.TryGetValue(weapon.weaponData.StaticID, out int progress))
            {
                if (progress >= Config.weaponUsageMilestone)
                {
                    int milestone = Mathf.Min(progress / Config.weaponUsageMilestone, Config.weaponUsageMilestoneMax);
                    bonus += milestone * Config.weaponUsageMilestoneBonus;
                }
            }
            
            return bonus;
        }

        private float CalculateConditionBonus(WeaponInstance weapon, DungeonData dungeon, ArmorType effectiveArmorType)
        {
            if (weapon.effects == null || weapon.effects.Count == 0) return 0f;

            float bonus = 0f;
            foreach (var effect in weapon.effects)
            {
                switch (effect.effectData.effectType)
                {
                    case WeaponEffectType.DungeonGradeBonus:
                        if ((int)dungeon.grade == effect.effectData.targetGrade)
                            bonus += effect.currentValue;
                        break;

                    case WeaponEffectType.ArmorTypeBonus:
                        if ((int)effectiveArmorType == effect.effectData.targetArmorType)
                            bonus += effect.currentValue;
                        break;
                }
            }

            return bonus;
        }

        /// <summary>
        /// StatBonus / WeaponTypeMatchBonus / AllStatBonus 적용 후 statScore 반환
        /// </summary>
        private float GetEffectStatScore(AdventurerInstance adventurer, WeaponInstance weapon)
        {
            if (weapon.effects == null || weapon.effects.Count == 0)
                return adventurer.GetStatScore(weapon.weaponData.weaponType);

            // StatBonus: 스탯별 flat 추가
            int[] statBonus = new int[4];
            float matchMultiplier = 1f;
            float allStatBonus = 0f;

            foreach (var effect in weapon.effects)
            {
                switch (effect.effectData.effectType)
                {
                    case WeaponEffectType.StatBonus:
                        statBonus[effect.effectData.targetStat] += Mathf.RoundToInt(effect.currentValue);
                        break;
                    case WeaponEffectType.WeaponTypeMatchBonus:
                        matchMultiplier += effect.currentValue;
                        break;
                    case WeaponEffectType.AllStatBonus:
                        allStatBonus += Mathf.RoundToInt(effect.currentValue);
                        break;
                }
            }

            // GetStatScore에 statBonus 전달, matchMultiplier 적용
            float score = adventurer.GetStatScore(weapon.weaponData.weaponType, statBonus) * matchMultiplier;
            score += allStatBonus;
            return score;
        }
                
        private float GetActiveItemBonus(AdventurerInstance adventurer)
        {
            if (ActiveItemManager.Instance == null || adventurer == null)
                return 0f;
            
            // 부적(Charm) 효과: 모험 성공률 보너스 (특정 모험가에게만)
            float bonus = ActiveItemManager.Instance.GetCharmBonus(adventurer.instanceID);
            
            return bonus;
        }
        
        #endregion
        
        #region 재료/보상 계산

        /// <summary>
        /// 보스 클리어 재료 계산 (기본/보너스 분리)
        /// </summary>
        private (List<MaterialInstance> total, List<MaterialInstance> bonus) CalculateMaterialDropsWithBonus(
            AdventureInstance adventure, bool isGreatSuccess)
        {
            var dungeon = adventure.dungeon;
            var adventurer = adventure.adventurer;

            int baseAmount = Random.Range(Config.materialDropMin, Config.materialDropMax);
            baseAmount += GetTraitMaterialBonus(adventurer);

            int bonusAmount = 0;
            foreach (var effect in adventure.weapon.effects)
                if (effect.effectData.effectType == WeaponEffectType.MaterialAmountBonus)
                    bonusAmount += Mathf.RoundToInt(effect.currentValue);
            // 해체용 단검: 보너스 재료 획득 추가
            bonusAmount += adventure.disassemblyKnifeBonus;

            float gradeMultiplier = GetDungeonGradeMultiplier(dungeon.grade);
            int baseFinal  = Mathf.RoundToInt(baseAmount  * gradeMultiplier);
            int bonusFinal = Mathf.RoundToInt(bonusAmount  * gradeMultiplier);

            if (isGreatSuccess)
            {
                baseFinal  *= Config.greatSuccessMaterialMultiplier;
                bonusFinal *= Config.greatSuccessMaterialMultiplier;
            }

            var drops      = new List<MaterialInstance>();
            var bonusDrops = new List<MaterialInstance>();

            // 진화 재료
            MaterialData enforceMaterial = BlacksmithManager.Instance.GetEnforceMaterialByGrade(dungeon.grade);
            if (enforceMaterial != null)
            {
                int baseEnforce = dungeon.enforceDropCount;
                if (isGreatSuccess) baseEnforce *= Config.greatSuccessMaterialMultiplier;

                int bonusEnforce = 0;
                foreach (var effect in adventure.weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.EnforceMaterialBonus)
                        bonusEnforce += Mathf.RoundToInt(effect.currentValue);

                drops.Add(new MaterialInstance(enforceMaterial, baseEnforce));
                if (bonusEnforce > 0)
                    bonusDrops.Add(new MaterialInstance(enforceMaterial, bonusEnforce));
            }

            // 제작 재료 — 기본
            if (dungeon.dropMaterials != null && dungeon.dropMaterials.Count > 0)
            {
                for (int i = 0; i < baseFinal; i++)
                {
                    string selected = SelectMaterialByWeight(dungeon.dropMaterials);
                    if (selected == null) continue;
                    int idx = drops.FindIndex(d => d.materialDataID == selected);
                    if (idx >= 0) drops[idx].Add();
                    else drops.Add(new MaterialInstance(DataManager.Instance.GetMaterial(selected), 1));
                }

                // 제작 재료 — 보너스
                for (int i = 0; i < bonusFinal; i++)
                {
                    string selected = SelectMaterialByWeight(dungeon.dropMaterials);
                    if (selected == null) continue;
                    int idx = bonusDrops.FindIndex(d => d.materialDataID == selected);
                    if (idx >= 0) bonusDrops[idx].Add();
                    else bonusDrops.Add(new MaterialInstance(DataManager.Instance.GetMaterial(selected), 1));
                }
            }

            // 특수 재료
            if (dungeon.specialDropMaterial != null)
            {
                float dropChance = GetSpecialDropChance(dungeon.grade);
                foreach (var effect in adventure.weapon.effects)
                    if (effect.effectData.effectType == WeaponEffectType.SpecialMaterialChance)
                        dropChance += effect.currentValue;
                // 보물 지도: 무기 효과까지 더한 뒤 곱한다 - 특수 재료 특화 무기와 시너지가 나게
                dropChance *= 1f + adventure.treasureMapBonus;

                if (Random.value <= dropChance)
                    drops.Add(new MaterialInstance(dungeon.specialDropMaterial, Config.specialMaterialDrop));
            }

            // total에 bonus 합산
            foreach (var m in bonusDrops)
            {
                int idx = drops.FindIndex(d => d.materialDataID == m.materialDataID);
                if (idx >= 0) drops[idx].quantity += m.quantity;
                else drops.Add(new MaterialInstance(m.materialData, m.quantity));
            }

            return (drops, bonusDrops);
        }

        private float GetDungeonGradeMultiplier(Grade grade)
        {
            return grade switch
            {
                Grade.Common => Config.dungeonCommonMultiplier,
                Grade.Uncommon => Config.dungeonUncommonMultiplier,
                Grade.Rare => Config.dungeonRareMultiplier,
                Grade.Epic => Config.dungeonEpicMultiplier,
                Grade.Legendary => Config.dungeonLegendaryMultiplier,
                _ => 1.0f,
            };
        }

        private float GetSpecialDropChance(Grade grade)
        {
            return grade switch
            {
                Grade.Common => Config.specialDropCommon,// 35%
                Grade.Uncommon => Config.specialDropUncommon,// 30%
                Grade.Rare => Config.specialDropRare,// 25%
                Grade.Epic => Config.specialDropEpic,// 20%
                Grade.Legendary => Config.specialDropLegendary,// 15%
                _ => Config.specialDropCommon,
            };
        }

        /// <summary>재료 등급별 드롭 가중치. 등급이 높을수록 낮다</summary>
        private int GetMaterialDropWeight(MaterialData material)
        {
            var weights = Config.materialDropWeightByGrade;
            int index = (int)material.grade;
            if (weights == null || index < 0 || index >= weights.Length) return 1;
            return weights[index];
        }

        private string SelectMaterialByWeight(List<MaterialData> materials)
        {
            if (materials == null || materials.Count == 0) return null;

            // 전체 가중치 합 계산
            int totalWeight = 0;
            foreach (var material in materials)
            {
                totalWeight += GetMaterialDropWeight(material);
            }

            if (totalWeight <= 0) return null;

            // 랜덤 값 선택
            int randomValue = Random.Range(0, totalWeight);

            // 가중치 기반 선택
            int currentWeight = 0;
            foreach (var material in materials)
            {
                currentWeight += GetMaterialDropWeight(material);
                if (randomValue < currentWeight)
                {
                    return material.StaticID;
                }
            }
            
            Log.Info("계산 이상");
            // 안전장치 (도달하지 않아야 함)
            return materials[^1].StaticID;
        }
        
        #endregion

        #region 결과 계산

        /// <summary>통계 1회 적용 가드 - 완료 시점과 결과 확인 시점 양쪽에서 호출해도 중복 적립되지 않는다</summary>
        private void ApplyStatisticsOnce(AdventureInstance adventure, AdventureResult result)
        {
            if (result.statisticsApplied) return;
            UpdateStatistics(adventure, result);
            result.statisticsApplied = true;
        }

        private void UpdateStatistics(AdventureInstance adventure, AdventureResult result)
        {
            var gameData = GameManager.Instance.GameData;
            var playerData = LegacyManager.Instance.PlayerData;

            // 던전 통계 (도감 — 영구 데이터)
            if (!playerData.dungeonStats.TryGetValue(adventure.dungeon.StaticID, out var dungeonStat))
            {
                dungeonStat = new DungeonStatData();
                playerData.dungeonStats[adventure.dungeon.StaticID] = dungeonStat;
            }
            
            dungeonStat.totalAttempts++;
            if (result.isSuccess)
            {
                dungeonStat.successCount++;

                if (result.isGreatSuccess)
                    dungeonStat.greatSuccessCount++;
            }
            else
            {
                dungeonStat.failCount++;
            }

            // 모험가 통계
            var advStat = adventure.adventurer.adventurerStatData;
            if (result.isSuccess) advStat.adventureSuccessCount++; else advStat.adventureFailCount++;
            advStat.totalGoldEarned += result.totalGoldReward;
            if (adventure.weapon.weaponData.weaponType == adventure.adventurer.GetBestWeaponType())
                advStat.adventurerWeaponGoodCount++;

            // 탐험도 증가
            if (!dungeonStat.hasGreatSuccessReady)
            {
                int baseGain = result.isSuccess ? Config.explorationGainOnSuccess : Config.explorationGainOnFail;
                float multiplier = GetTraitExplorationMultiplier(adventure.adventurer);
                int gain = Mathf.RoundToInt(baseGain * multiplier);

                dungeonStat.explorationProgress = Mathf.Min(dungeonStat.explorationProgress + gain, 100);

                if (dungeonStat.explorationProgress >= 100)
                    dungeonStat.hasGreatSuccessReady = true;
            }
            
            // 전체 통계
            if (result.isSuccess)
                gameData.totalSuccessfulAdventures++;
            else
                gameData.totalFailedAdventures++;
            
            if (result.isAdventurerTypeMatch)
                gameData.totalBonusMatches++;
            
            // 무기 도감 진행도
            if (result.isSuccess)
            {
                string weaponID = adventure.weapon.weaponData.StaticID;
                if (!playerData.weaponDiscoveryProgress.ContainsKey(weaponID))
                    playerData.weaponDiscoveryProgress[weaponID] = 0;
                playerData.weaponDiscoveryProgress[weaponID]++;
            }
        }
        
        #endregion
        
        #region 이벤트 계산

        /// <summary>
        /// 이벤트 성공률 = 기본 성공률(난이도 계수를 요구 임계값에 반영) + 누적 보정 × 기분 배율
        /// moodMultiplier: 실제 적용된 배율 (EventResult 기록용)
        /// </summary>
        private float CalculateEventSuccessRate(AdventureInstance adventure, DungeonEventData eventData, out float moodMultiplier)
        {
            float rate = CalculateSuccessRate(adventure.adventurer, adventure.weapon, adventure.dungeon,
                                              adventure.effectiveArmorType, eventData.difficultyMultiplier);
            rate += adventure.cumulativeModifier;

            float mult = RollMoodMultiplier(adventure.mood);
            if (eventData.eventType == DungeonEventType.Boss)
                mult = 1f + (mult - 1f) * Config.moodHalfStrength;
            rate *= mult;
            moodMultiplier = mult;

            return Mathf.Clamp(rate, Config.successRateMin, Config.successRateMax);
        }

        private (int baseGold, int bonusGold) CalculateEventGold(
            AdventureInstance adventure, float baseMultiplier, float bonusMultiplier)
        {
            int baseReward = Random.Range(
                adventure.dungeon.baseRewardMin,
                adventure.dungeon.baseRewardMax + 1);

            float allBonus = 1f;
            foreach (var effect in adventure.weapon.effects)
                if (effect.effectData.effectType == WeaponEffectType.AllGoldBonus)
                    allBonus += effect.currentValue;

            float totalMultiplier = baseMultiplier + bonusMultiplier;
            int baseGold  = Mathf.RoundToInt(baseReward * baseMultiplier);
            int totalGold = Mathf.RoundToInt(baseReward * totalMultiplier * allBonus);
            int bonusGold = totalGold - baseGold;

            return (baseGold, bonusGold);
        }

        /// <summary>
        /// UI 표시용 골드 보상 = 던전 기본 보상 범위 랜덤 × 일반 전투
        /// </summary>
        public int CalculateEventGold(DungeonData dungeon)
        {
            int baseReward = Random.Range(
                dungeon.baseRewardMin,
                dungeon.baseRewardMax + 1);
                
            return Mathf.RoundToInt(baseReward * Config.battleRewardMultiplier);
        }

        /// <summary>
        /// RareDrop 재료 계산
        /// 진화재료/특수재료 없음
        /// </summary>
        private (List<MaterialInstance> total, List<MaterialInstance> bonus) CalculateRareDropMaterials(AdventureInstance adventure)
        {
            var drops = new List<MaterialInstance>();
            var bonusDrops = new List<MaterialInstance>();
            var dungeon = adventure.dungeon;

            if (dungeon.dropMaterials == null || dungeon.dropMaterials.Count == 0)
                return (drops, bonusDrops);

            int baseCount = Config.rareDropMaterialCount;
            int bonusCount = 0;

            foreach (var effect in adventure.weapon.effects)
                if (effect.effectData.effectType == WeaponEffectType.MaterialAmountBonus)
                    bonusCount += Mathf.RoundToInt(effect.currentValue);

            int totalCount = baseCount + bonusCount;

            for (int i = 0; i < totalCount; i++)
            {
                string selected = SelectMaterialByWeight(dungeon.dropMaterials);
                if (selected == null) continue;

                var targetList = i < baseCount ? drops : bonusDrops;
                int existingIndex = targetList.FindIndex(d => d.materialDataID == selected);
                if (existingIndex >= 0)
                    targetList[existingIndex].Add();
                else
                    targetList.Add(new MaterialInstance(DataManager.Instance.GetMaterial(selected), 1));
            }

            // total에는 bonus 포함
            foreach (var m in bonusDrops)
            {
                int idx = drops.FindIndex(d => d.materialDataID == m.materialDataID);
                if (idx >= 0) drops[idx].quantity += m.quantity;
                else drops.Add(new MaterialInstance(m.materialData, m.quantity));
            }

            return (drops, bonusDrops);
        }

        #endregion
    }

    public struct SuccessRateBreakdown
    {
        public float baseRate;            // 스탯 기반 기본 성공률 (StatBonus/AllStatBonus/WeaponTypeMatchBonus 포함)
        public float armorBonus;          // 무기-던전 상성 보정
        public float statEffectBonus;     // StatBonus/AllStatBonus/WeaponTypeMatchBonus로 인한 성공률 증가분
        public float conditionBonus;      // DungeonGradeBonus + ArmorTypeBonus (조건 충족 시)
        public float armorTypeBonusOnly;  // conditionBonus 중 ArmorTypeBonus 합산만 분리 (UI 게이팅용)
    }
}
