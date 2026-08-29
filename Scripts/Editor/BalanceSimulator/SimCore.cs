#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 밸런스 시뮬레이터 데이터 번들 — 실제 Config/SO 에셋을 로드해 그대로 읽는다.
    /// 수식만 미러링하고 수치는 전부 에셋에서 온다. (설계: Documents/balance/reference/시뮬레이터_설계.md)
    /// </summary>
    public class SimBundle
    {
        public AdventureConfig adventure;
        public ReputationConfig reputation;
        public VisitorConfig visitor;
        public InsightConfig insight;
        public QuestBoardConfig questBoard;
        public WeaponShopConfig weaponShop;
        public BlacksmithConfig blacksmith;
        public WeaponConfig weaponCfg;
        public InventoryConfig inventory;
        public MorningEventConfig morningEvent;
        public LegacyConfig legacy;
        public TraitConfig trait;
        public SeerConfig seer;
        public PriceTierConfig priceTier;
        public EndlessQuestConfig endless;

        /// <summary>네임드 모험가 SO — VisitorManager.InitializeAllAdventurers 미러 (튜토리얼 전용 2종 제외)</summary>
        public List<AdventurerData> namedAdventurers;

        /// <summary>일반 모험가 SO — 스탯 range가 성별로 갈리므로 스폰 시 SO를 뽑아 그 범위로 롤한다</summary>
        public List<AdventurerData> normalAdventurers;

        /// <summary>무기 제작 레시피 (단계 5-3)</summary>
        public List<WeaponRecipeData> weaponRecipes;

        public List<DungeonData> dungeons;
        public List<WeaponData> weapons;
        public List<WeeklyQuestData> quests;   // weekNumber 오름차순
        public List<WeaponEffectData> weaponEffects;
        public List<MaterialData> materials;
        public List<ActiveItemData> activeItems;
        public List<ActiveItemRecipeData> itemRecipes;

        private Dictionary<Grade, List<WeaponEffectData>> effectsByGrade;
        private int[] enforceMatPrice;

        /// <summary>등급별 진화 재료 ID — BlacksmithManager.GetEnforceMaterialByGrade 미러 (index = Grade)</summary>
        public static readonly string[] EnforceMatIDs =
            { "MAT_ENF_001", "MAT_ENF_002", "MAT_ENF_003", "MAT_ENF_004", "MAT_ENF_005" };

        /// <summary>
        /// 등급별 진화 재료 1개 구매가 — BlacksmithManager.GetEnforceMaterialByGrade + MaterialData.buyPrice.
        /// 재료는 모험 결과 화면에서 골드로 사야 얻는다(AdventureResultView.InitializeMaterial).
        /// </summary>
        public int EnforceMatPrice(Grade g)
        {
            enforceMatPrice ??= EnforceMatIDs.Select(id =>
                materials.FirstOrDefault(m => m.StaticID == id)?.buyPrice ?? 0).ToArray();
            return enforceMatPrice[(int)g];
        }

        /// <summary>재료 1개의 구매가 기준 가치 — 아침 이벤트 보상 EV 환산용</summary>
        public int MaterialValue(string id) =>
            materials.FirstOrDefault(m => m.StaticID == id)?.buyPrice ?? 0;

        /// <summary>DataManager.GetWeaponEffectsByGrade 미러</summary>
        public List<WeaponEffectData> EffectsOfGrade(Grade g)
        {
            effectsByGrade ??= weaponEffects.GroupBy(e => e.grade)
                                            .ToDictionary(x => x.Key, x => x.ToList());
            return effectsByGrade.TryGetValue(g, out var list) ? list : new List<WeaponEffectData>();
        }

        public static SimBundle Load()
        {
            var b = new SimBundle
            {
                adventure  = LoadFirst<AdventureConfig>(),
                reputation = LoadFirst<ReputationConfig>(),
                visitor    = LoadFirst<VisitorConfig>(),
                insight    = LoadFirst<InsightConfig>(),
                questBoard = LoadFirst<QuestBoardConfig>(),
                weaponShop = LoadFirst<WeaponShopConfig>(),
                blacksmith = LoadFirst<BlacksmithConfig>(),
                weaponCfg  = LoadFirst<WeaponConfig>(),
                inventory  = LoadFirst<InventoryConfig>(),
                morningEvent = LoadFirst<MorningEventConfig>(),
                legacy     = LoadFirst<LegacyConfig>(),
                trait      = LoadFirst<TraitConfig>(),
                seer       = LoadFirst<SeerConfig>(),
                priceTier  = LoadFirst<PriceTierConfig>(),
                endless    = LoadFirst<EndlessQuestConfig>(),
                dungeons   = LoadAll<DungeonData>().Where(d => !string.IsNullOrEmpty(d.StaticID)).ToList(),
                weapons    = LoadAll<WeaponData>().Where(w => !string.IsNullOrEmpty(w.StaticID)).ToList(),
                quests     = LoadAll<WeeklyQuestData>().Where(q => q.weekNumber >= 1)
                                                       .OrderBy(q => q.weekNumber).ToList(),
                weaponEffects = LoadAll<WeaponEffectData>().Where(e => !string.IsNullOrEmpty(e.StaticID)).ToList(),
                materials  = LoadAll<MaterialData>().Where(m => !string.IsNullOrEmpty(m.StaticID)).ToList(),
                activeItems = LoadAll<ActiveItemData>().Where(a => !string.IsNullOrEmpty(a.StaticID)).ToList(),
                itemRecipes = LoadAll<ActiveItemRecipeData>().Where(r => r.resultItem != null).ToList(),
                weaponRecipes = LoadAll<WeaponRecipeData>().Where(r => r.resultWeapon != null)
                                                           .OrderBy(r => r.StaticID).ToList(),
            };

            // 네임드 풀 — 튜토리얼 전용 모험가는 스폰 풀에서 제외한다 (IsTutorialOnlyAdventurer 미러)
            var tut = LoadFirst<TutorialConfig>();
            b.namedAdventurers = LoadAll<AdventurerData>()
                .Where(a => a != null && a.isNamed && !string.IsNullOrEmpty(a.StaticID))
                .Where(a => tut == null || (a.StaticID != tut.TutorialAdventurer1ID && a.StaticID != tut.TutorialAdventurer2ID))
                .OrderBy(a => a.StaticID)   // 결정론 — AssetDatabase 순서에 의존하지 않게 고정
                .ToList();

            b.normalAdventurers = LoadAll<AdventurerData>()
                .Where(a => a != null && !a.isNamed && !string.IsNullOrEmpty(a.StaticID))
                .OrderBy(a => a.StaticID)
                .ToList();
            return b;
        }

        public bool IsValid(out string error)
        {
            var missing = new List<string>();
            if (adventure == null) missing.Add("AdventureConfig");
            if (reputation == null) missing.Add("ReputationConfig");
            if (visitor == null) missing.Add("VisitorConfig");
            if (insight == null) missing.Add("InsightConfig");
            if (questBoard == null) missing.Add("QuestBoardConfig");
            if (weaponShop == null) missing.Add("WeaponShopConfig");
            if (blacksmith == null) missing.Add("BlacksmithConfig");
            if (weaponCfg == null) missing.Add("WeaponConfig");
            if (inventory == null) missing.Add("InventoryConfig");
            if (morningEvent == null) missing.Add("MorningEventConfig");
            if (legacy == null || legacy.upgrades == null || legacy.upgrades.Count == 0) missing.Add("LegacyConfig");
            if (trait == null) missing.Add("TraitConfig");
            if (seer == null) missing.Add("SeerConfig");
            if (priceTier == null) missing.Add("PriceTierConfig");
            if (endless == null) missing.Add("EndlessQuestConfig");
            if (namedAdventurers == null || namedAdventurers.Count == 0) missing.Add("AdventurerData(네임드)");
            if (itemRecipes == null || itemRecipes.Count == 0) missing.Add("ActiveItemRecipeData");
            if (dungeons == null || dungeons.Count == 0) missing.Add("DungeonData");
            if (weapons == null || weapons.Count == 0) missing.Add("WeaponData");
            if (weaponEffects == null || weaponEffects.Count == 0) missing.Add("WeaponEffectData");
            if (materials == null || materials.Count == 0) missing.Add("MaterialData");
            error = missing.Count > 0 ? "에셋 누락: " + string.Join(", ", missing) : null;
            return missing.Count == 0;
        }

        /// <summary>
        /// 캠페인 마지막 주차. 이후가 엔드리스 템플릿 구간이다.
        /// 상수로 두면 캠페인 길이를 바꿀 때 조용히 어긋나므로(2026-07-29 40주 압축 때 겪음)
        /// EndlessQuestConfig에서 읽는다 - 런타임 QuestManager와 같은 출처다.
        /// </summary>
        public int CampaignWeeks => endless != null ? endless.campaignLastWeek : 60;

        /// <summary>주차 데이터 조회 — 없으면 마지막 주차 폴백 (QuestManager.IssueNewQuest 미러)</summary>
        public WeeklyQuestData QuestForWeek(int week)
        {
            if (quests.Count == 0) return null;
            var exact = quests.FirstOrDefault(q => q.weekNumber == week);
            return exact != null ? exact : quests[quests.Count - 1];
        }

        private static T LoadFirst<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(a => a != null);

        private static List<T> LoadAll<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).Distinct().ToList();
    }

    public class SimOptions
    {
        public int seeds = 100;
        public int weeks = 100;
        public bool eliteUsesTalkHint = true;   // 상급자: 무기 힌트 대화 사용 (통찰 게이트 이상일 때)
        public int eliteTalkInsightGate = 50;

        /// <summary>
        /// 아침 이벤트 강제 참여 (진단용) — 등장 확률을 무시하고 매일 스폰 + 전 페르소나가 유보금(주간 벌금)
        /// 한도 안에서 무조건 참여한다. "이득일 때만" 정책의 지출 0이 봇 결함인지 콘텐츠 사망인지 구분한다
        /// (밸런스_미적용_영역과_시뮬_로드맵.md 3부 원칙 5).
        /// </summary>
        public bool forceMorningEvents = false;

        /// <summary>
        /// 시드당 최대 회차 수 (단계 2: 유산 + 회차 반복). 폐업 시에만 유산을 얻고 다음 회차로 넘어가며,
        /// 생존 완주하면 커리어를 조기 종료한다. 1이면 기존과 동일한 단일 회차.
        /// </summary>
        public int runsPerSeed = 5;

        /// <summary>goldPerLegacy A/B용 오버라이드. 0이면 LegacyConfig.asset 값 사용.</summary>
        public int goldPerLegacyOverride = 0;

        /// <summary>
        /// 특성 16종 반영 (단계 4). 롤/Haggler 판정은 메인 rng와 분리된 전용 rng를 쓰므로
        /// OFF 런은 특성 반영 이전 코드와 결과가 완전히 동일하다 (무결성 검증용 A/B).
        /// </summary>
        public bool useTraits = true;

        /// <summary>
        /// 점술 반영 (단계 5-1). 상담 판정/운세 롤은 메인 rng와 분리된 전용 rng를 쓰므로
        /// OFF 런은 점술 반영 이전 코드와 결과가 완전히 동일하다 (무결성 검증 A/B).
        /// </summary>
        public bool useSeer = true;

        /// <summary>
        /// 네임드 모험가 + 호감도 반영 (단계 5-2). 전용 rng를 쓰므로 OFF 런은 반영 이전과 동일하다.
        /// </summary>
        public bool useNamed = true;

        /// <summary>
        /// 네임드 우대 정책 (단계 5-2). true = 봇이 네임드에게 최선의 무기·큰 던전을 우선 배정하고
        /// 점술도 더 적극적으로 본다. false = named-blind(네임드를 일반과 똑같이 대함).
        /// 네임드 반영 자체의 기여와 우대 정책의 기여를 분리 측정하기 위한 축이다.
        /// </summary>
        public bool preferNamed = true;

        /// <summary>무기 제작 + 재부여 반영 (단계 5-3). 전용 rng를 쓰므로 OFF 런은 반영 이전과 동일하다.</summary>
        public bool useCraft = true;

        /// <summary>
        /// 재부여 강제 시도 (진단용) — 비용을 무시하고(유보금 한도 안에서) 조건만 되면 무조건 재부여한다.
        /// "이득일 때만" 정책의 지출 0이 봇 결함인지 콘텐츠 사망인지 구분한다 (3부 원칙 5).
        /// </summary>
        public bool forceReroll = false;

        /// <summary>
        /// 유산 단독 효과 스윕 (단계 6, 로드맵 1-4) — None이 아니면 이 업그레이드 하나만 만렙으로 사전 주입한
        /// 커리어로 1회차를 돌린다. 선행 업그레이드가 있으면 함께 만렙으로 준다(에픽 무기 -> 희귀 무기).
        /// 21종의 "포인트당 가치"를 같은 축에서 비교하기 위한 측정 전용 옵션이며, 구매 정책은 개입하지 않는다.
        /// </summary>
        public UpgradeKey soloUpgrade = UpgradeKey.None;

        /// <summary>
        /// 엔드리스 템플릿 단독 난이도 스윕 (0 = 미사용) — 캠페인 종료 후 모든 주차에 이 주차의
        /// 템플릿 하나만 반복 출제해, 16종 각각의 통과율/생존을 같은 축에서 잰다.
        /// 캠페인 구간(1~60주)은 건드리지 않으므로 같은 시드의 60주차까지는 전 구성이 동일하다.
        /// </summary>
        public int endlessFixedWeek = 0;
    }

    /// <summary>
    /// 회차 간 영구 상태 — PlayerData + LegacyManager 미러 (단계 2: 유산 + 회차 반복).
    /// 업그레이드 비용 = RoundToInt(baseCost x scale^level) (LegacyManager.CalcCost 미러).
    /// </summary>
    public class SimCareer
    {
        public int legacyPoints;
        public int purchaseCount;

        private readonly Dictionary<UpgradeKey, int> levels = new Dictionary<UpgradeKey, int>();

        public int Level(UpgradeKey key) => levels.TryGetValue(key, out var v) ? v : 0;
        public int TotalLevels
        {
            get { int sum = 0; foreach (var v in levels.Values) sum += v; return sum; }
        }

        public LegacyUpgradeDefinition Def(SimBundle b, UpgradeKey key)
        {
            foreach (var d in b.legacy.upgrades)
                if (d.key == key) return d;
            return null;
        }

        /// <summary>LegacyManager.GetUpgradeCost 미러</summary>
        public int NextCost(SimBundle b, UpgradeKey key)
        {
            var def = Def(b, key);
            if (def == null) return 0;
            int lv = Level(key);
            if (lv >= def.maxLevel) return 0;
            return Mathf.RoundToInt(def.baseCost * Mathf.Pow(def.costScalePerLevel, lv));
        }

        /// <summary>LegacyManager.CanPurchaseUpgrade 미러 (만렙/선행/보유 포인트)</summary>
        public bool CanPurchase(SimBundle b, UpgradeKey key)
        {
            var def = Def(b, key);
            if (def == null) return false;
            int lv = Level(key);
            if (lv >= def.maxLevel) return false;
            if (def.prerequisite != UpgradeKey.None && Level(def.prerequisite) <= 0) return false;
            return legacyPoints >= NextCost(b, key);
        }

        public bool Purchase(SimBundle b, UpgradeKey key)
        {
            if (!CanPurchase(b, key)) return false;
            legacyPoints -= NextCost(b, key);
            levels[key] = Level(key) + 1;
            purchaseCount++;
            return true;
        }

        // ---- 효과 조회 — LegacyManager 효과 getter 미러 ----

        private float Eff(SimBundle b, UpgradeKey key) => Def(b, key)?.effectValue ?? 0f;

        public int   StartingGoldBonus(SimBundle b)    => Mathf.RoundToInt(Eff(b, UpgradeKey.StartingGold)    * Level(UpgradeKey.StartingGold));
        public int   StartingInsightBonus(SimBundle b) => Mathf.RoundToInt(Eff(b, UpgradeKey.StartingInsight) * Level(UpgradeKey.StartingInsight));
        public int   InventorySlotsBonus(SimBundle b)  => Mathf.RoundToInt(Eff(b, UpgradeKey.InventorySlots)  * Level(UpgradeKey.InventorySlots));
        public bool  HasWeaponRare                     => Level(UpgradeKey.WeaponRare) > 0;
        public bool  HasWeaponEpic                     => Level(UpgradeKey.WeaponEpic) > 0;
        public float EnforceRateBonus(SimBundle b)     => Eff(b, UpgradeKey.EnforceRate) / 100f * Level(UpgradeKey.EnforceRate);
        public float EvolveRateBonus(SimBundle b)      => Eff(b, UpgradeKey.EvolveRate)  / 100f * Level(UpgradeKey.EvolveRate);
        public float EnforceCostMult(SimBundle b)      => 1f - Eff(b, UpgradeKey.EnforceCost) / 100f * Level(UpgradeKey.EnforceCost);
        public float EvolveCostMult(SimBundle b)       => 1f - Eff(b, UpgradeKey.EvolveCost)  / 100f * Level(UpgradeKey.EvolveCost);
        public float MaterialReductionMult(SimBundle b) => Mathf.Pow(1f - Eff(b, UpgradeKey.MaterialReduction) / 100f, Level(UpgradeKey.MaterialReduction));
        public float DisassembleMult(SimBundle b)      => 1f + Eff(b, UpgradeKey.DisassembleBonus) / 100f * Level(UpgradeKey.DisassembleBonus);
        // 재부여 2종 (단계 5-3에서 시뮬 반영 -> 구매 대상에 포함)
        public int   RerollCountBonus(SimBundle b)     => Mathf.RoundToInt(Eff(b, UpgradeKey.RerollCount) * Level(UpgradeKey.RerollCount));
        public float RerollCostMult(SimBundle b)       => 1f - Eff(b, UpgradeKey.RerollCost) / 100f * Level(UpgradeKey.RerollCost);
        public float CommissionBonus(SimBundle b)      => Eff(b, UpgradeKey.CommissionRate) / 100f * Level(UpgradeKey.CommissionRate);
        public float TipBonus(SimBundle b)             => Eff(b, UpgradeKey.TipRate)        / 100f * Level(UpgradeKey.TipRate);
        public float GreatSuccessMult(SimBundle b)     => 1f + Eff(b, UpgradeKey.GreatSuccessRate) / 100f * Level(UpgradeKey.GreatSuccessRate);
        public float AdventureSpeedMult(SimBundle b)   => 1f - Eff(b, UpgradeKey.AdventureSpeed) / 100f * Level(UpgradeKey.AdventureSpeed);
        public int   ShopRefreshBonus(SimBundle b)     => Mathf.RoundToInt(Eff(b, UpgradeKey.ShopRefresh) * Level(UpgradeKey.ShopRefresh));
        public bool  HasMorningEventGuarantee          => Level(UpgradeKey.MorningEventGuarantee) > 0;
        public float NamedSpawnWeightBonus(SimBundle b) => Eff(b, UpgradeKey.NamedSpawnWeight) * Level(UpgradeKey.NamedSpawnWeight);

        /// <summary>
        /// 스윕용 — 이 키(와 선행 키)를 만렙으로 채우고 소모한 총 포인트를 돌려준다 (SimOptions.soloUpgrade).
        /// 구매 판정을 거치지 않으므로 rng를 소비하지 않는다 = 기준선(None) 런은 기존 결과와 동일하다.
        /// </summary>
        public int GrantMaxLevel(SimBundle b, UpgradeKey key)
        {
            if (key == UpgradeKey.None) return 0;
            var def = Def(b, key);
            if (def == null) return 0;

            int spent = def.prerequisite != UpgradeKey.None ? GrantMaxLevel(b, def.prerequisite) : 0;
            while (Level(key) < def.maxLevel)
            {
                spent += NextCost(b, key);
                levels[key] = Level(key) + 1;
                purchaseCount++;
            }
            return spent;
        }
    }

    #region 시뮬 상태 객체

    /// <summary>무기 부가효과 — WeaponEffect 미러 (값 롤은 시뮬 rng로 결정론 유지)</summary>
    public class SimEffect
    {
        public WeaponEffectData data;
        public float value;

        public float MaxValue => WeaponEffect.IsIntegerType(data.effectType)
            ? Mathf.RoundToInt(data.baseValueRange.y)
            : data.baseValueRange.y;
    }

    public class SimWeapon
    {
        public WeaponData data;
        public Grade grade;        // 진화 반영 현재 등급
        public int enforce;
        public int usage;          // 대여(모험) 사용 횟수 — 수집 보너스용
        public bool busy;
        public List<SimEffect> effects = new List<SimEffect>();
        public int rerollCount;    // 재부여 횟수 — 진화 시 0으로 리셋 (단계 5-3)

        public SimWeapon(WeaponData d) { data = d; grade = d.baseGrade; }
        public WeaponType Type => data.weaponType;

        /// <summary>WeaponInstance.MaxEnforceLevel 미러 — 최대 강화 = 효과 개수</summary>
        public int MaxEnforce => effects.Count;

        public float EffectSum(WeaponEffectType type)
        {
            float sum = 0f;
            foreach (var e in effects)
                if (e.data.effectType == type) sum += e.value;
            return sum;
        }
    }

    /// <summary>
    /// 네임드 모험가의 영속 인스턴스 (단계 5-2) — VisitorManager.namedAdventurerCache 미러.
    /// 일반 모험가와 달리 회차 내내 같은 개체가 재방문하며 호감도를 누적한다.
    /// 스탯은 SO의 range로 회차 시작 시 1회 롤(AdventurerInstance 생성자 미러), 특성은 SO 고정값.
    /// </summary>
    public class SimNamed
    {
        public AdventurerData data;
        public int[] stats = new int[4];
        public WeaponType trueBest;
        public WeaponType observedDefaultType;
        public int bestStatIndex;
        public TraitType trait;

        public int affection;
        public bool isAlive = true;
        public bool busy;              // 모험 중 (isAdventuring 미러 — 방문 중 상태는 시뮬이 즉시 처리하므로 불필요)
        public int lastVisitDay = -99;
        public bool reachedMaxAffection;   // adventurerStatData.hasReachedMaxAffection 미러 (한 번 도달하면 유지)

        /// <summary>IsHome 미러 — 살아있고 모험 중이 아니면 집에 있다</summary>
        public bool IsHome => isAlive && !busy;
    }

    public class SimVisitor
    {
        public int[] stats = new int[4];   // STR/DEX/INT/LUK
        public WeaponType trueBest;
        public WeaponType observedDefaultType;   // 기본 무기 타입 (70% best / 20% second / 10% random)
        public float arrival;
        public int bestStatIndex;          // 최고 스탯 — 통찰 70+에서 자동 공개되는 정보
        public TraitType trait;            // 16종 균등 롤 (AdventurerInstance 생성자 미러, 단계 4)
        public SimNamed named;             // null = 일반 모험가 (단계 5-2)
    }

    public class BoardEntry
    {
        public DungeonData dungeon;
        public ArmorType armor;
        public bool highlighted;
        public bool armorKnown;
    }

    public class ShopItem
    {
        public WeaponData data;
        public int price;
    }

    public class ScoutJob
    {
        public BoardEntry entry;
        public float knownAt;
        public int cost;
        public bool done;
    }

    public class PendingAdventure
    {
        public int completeDay;
        public float completeMin;
        public bool confirmed;

        public SimWeapon weapon;       // null = 기본 무기
        public bool isDefault;
        public bool typeMatch;         // 대여 무기가 모험가 최적 타입 (isAdventurerTypeMatch 미러)
        public Grade weaponGrade;
        public WeaponType weaponType;
        public float armorBonus;       // 팁 계산용 (무기 타입 vs 실제 방어구)
        public string dungeonID;
        public Grade dungeonGrade;

        public bool success;
        public bool death;
        public bool great;
        public int clearedEvents;      // 평판용 클리어 칸 수 (CountClearedEvents 미러)
        public int payoutGold;         // 성공 전액 / 후퇴 50% / 사망 0 (수수료 적용 전)
        public int matDrop;            // 진화 재료 (대성공 2배 반영)

        // 액티브 아이템 (단계 3)
        public Dictionary<string, int> craftDrops;   // 제작/특수 재료 드롭 (확인 시 구매 대상)
        public float fameScrollBonus;                // 성공 시 평판 배율 (명예의 두루마리, 1.0 = x2)

        // 특성 (단계 4)
        public TraitType trait;                      // 확인 시점 효과(수수료/평판/구매가) + 계측용
        public float durTotalMin;                    // 총 소요 (인게임 분) — 특성별 처리량 계측용

        // 네임드 (단계 5-2)
        public SimNamed named;                       // null = 일반. 확인 시 호감도 누적/사망 처리
        public int affLevelAtStart;                  // 출발 시점 호감도 등급 (0=Low~3=Max) — 계측용
    }

    public class WeekStat
    {
        public int week;
        public int attempts, successes, greats, deaths;
        public int visitors, served, left, defaultRuns;
        public int income, spent;
        public int goldEnd, repEnd, repLevel, insightEnd, weaponsOwned;
        public bool questPassed;
        public int finePaid;

        // 지출 분해 (합 = spent). 가격 밸런스 판정용
        public int spentWeapon;    // 무기 구매
        public int spentSmith;     // 강화 + 진화
        public int spentScout;     // 수색 파견 (미완료 환불분 차감 후)
        public int spentRefresh;   // 의뢰판 + 상점 새로고침
        public int spentMaterial;  // 모험 결과 재료 구매 (진화 재료만 — 제작/재부여 미구현)

        // 정보 행동 — 초급/중급/상급 구분선 판정용
        public int scoutCount;     // 수색 파견 횟수
        public int talkCount;      // 대화(스탯 테스트/무기 힌트) 횟수

        // R 기준 체계용 — R = 모험 1회 성공 순수입. 모든 가격을 R 배수로 읽기 위한 계측.
        public int incomeAdventure;   // 모험 수수료만 (퀘스트 보상·분해 골드 제외)
        public int weaponBuyCount;    // 무기 구매 횟수
        public int enforceCount;      // 강화 시도 횟수
        public int evolveCount;       // 진화 시도 횟수
        public int materialBuyCount;  // 재료를 구매한 모험 횟수

        // 아침 이벤트 계측 (단계 1 — 밸런스_미적용_영역과_시뮬_로드맵.md)
        public int spentMorning;       // 아침 이벤트 지출 (상자/기부/투자/암시장/강제납부)
        public int incomeMorning;      // 아침 이벤트 수입 (선물/상자 골드/수집가 판매/투자 회수)
        public int morningEventCount;  // 이벤트 참여 횟수
        public int boxBuyCount;        // 수수께끼 상자 구매 횟수
        public int spentMorningBox;    // 상자 구매 지출 (R 표 "상자 1개" 열용)

        // 점술 계측 (단계 5-1)
        public int seerCount;          // 상담 횟수
        public int spentSeer;          // 상담 지출

        // 무기 제작 / 재부여 계측 (단계 5-3)
        public int spentWeaponCraft;   // 무기 제작 골드 (재료는 spentCraftMat에 합산)
        public int weaponCraftCount;   // 무기 제작 횟수
        public int spentReroll;        // 재부여 골드
        public int rerollCount;        // 재부여 횟수

        // 액티브 아이템 계측 (단계 3)
        public int spentItemCraft;     // 제작 골드
        public int spentCraftMat;      // 제작 재료 구매 (모험 결과)
        public int craftCount;         // 제작 횟수
        public int itemUseCount;       // 아이템 사용 횟수 (모험 배정 + 강화석)
    }

    /// <summary>
    /// 유산 단독 효과 스윕의 한 구성 (단계 6, 로드맵 1-4).
    /// key = None이면 기준선(업그레이드 0), totalCost = 만렙까지 든 포인트(선행 포함).
    /// </summary>
    public class SweepArm
    {
        public UpgradeKey key;
        public int totalCost;
        public List<RunResult> results;
    }

    /// <summary>
    /// 엔드리스 템플릿 스윕의 한 구성. fixedWeek = 0이면 기준선(현행 순차 재생 + 마지막 주차 폴백).
    /// </summary>
    public class EndlessArm
    {
        public int fixedWeek;
        public string label;
        public List<RunResult> results;
    }

    public class RunResult
    {
        public string persona;
        public int seed;
        public List<WeekStat> weekly = new List<WeekStat>();
        public int bankruptWeek;       // 0 = 생존
        public int survivalDays;
        public int[] firstRepLevelWeek = new int[5];    // [1]=Silver.. 0=미도달
        public int[] firstGradeWeek = new int[5];       // 등급 무기 첫 보유 주차. 0=미보유

        // 유산 회차 반복 (단계 2)
        public int careerRun = 1;      // 회차 번호 (1부터)
        public int earnedLegacy;       // 이 회차에서 얻을 유산 (CalculateEarnedLegacyPoints 미러, 폐업 시에만 지급)
        public int legacyConverted;    // 벌금 긴급 환전으로 소모한 유산 포인트
        public int upgradesOwned;      // 회차 시작 시점 보유 업그레이드 레벨 합

        // 특성별 계측 (단계 4) — 확인된 모험 기준, 인덱스 = (int)TraitType.
        // 배정이 특성과 무관(trait-blind 봇)하므로 특성 간 차이가 곧 특성 자체의 기여다.
        public static readonly int TraitN = Enum.GetValues(typeof(TraitType)).Length;
        public int[] traitCount    = new int[TraitN];
        public int[] traitSuccess  = new int[TraitN];
        public int[] traitDeath    = new int[TraitN];
        public int[] traitIncome   = new int[TraitN];   // 수수료 수입 합
        public int[] traitMatSpend = new int[TraitN];   // 결과 화면 재료 구매 합 (Porter 반영 지점)
        public int[] traitRep      = new int[TraitN];   // 평판 증감 합
        public float[] traitDurMin = new float[TraitN]; // 소요 시간 합 (인게임 분)

        // 점술 계측 (단계 5-1) — 인덱스 = LUK 구간 (0: ~25, 1: 26~50, 2: 51~75, 3: 76~).
        // 봇은 LUK을 모르고 상담하므로(LUK-blind) 구간별 결과가 곧 운세 가중치의 실효값이다.
        public int[]   seerLukCount   = new int[4];   // 상담 건수
        public float[] seerLukModSum  = new float[4]; // 적용된 운세 보정 합 (평균 = 구간 EV)
        public int[]   seerLukSuccess = new int[4];   // 상담 후 완주 성공 건수

        // 네임드 + 호감도 계측 (단계 5-2)
        public int namedVisits;        // 네임드가 방문해 모험을 나간 건수
        public int namedDeaths;        // 네임드 사망 (부활 이벤트 미모델 -> 회차 내 영구 이탈)
        public int namedMaxAffection;  // 회차 종료 시 호감도 Max(100) 도달 네임드 수
        public int[] affLevelCount   = new int[4];   // 출발 시점 호감도 등급별 건수 (0=Low~3=Max)
        public int[] affLevelSuccess = new int[4];   // 같은 등급의 완주 성공 건수

        // 무기 제작 실패 사유 (단계 5-3 진단) — 시도 대비 무엇이 막고 있는지
        public int craftFailLocked;      // 해금일 미도달
        public int craftFailSlot;        // 인벤토리 만석
        public int craftFailGold;        // 골드 부족
        public int craftFailCraftMat;    // 일반 제작 재료(MAT_CRF) 부족
        public int craftFailSpecialMat;  // 특수 재료(MAT_SPC) 부족
    }

    #endregion

    /// <summary>
    /// 1회차(1시드 x 1페르소나) 시뮬레이션 월드.
    /// 하루 타임라인: 06:00=0분 ~ 21:00=900분. 아침 0~180, 낮 180~720, 저녁 720~900.
    /// </summary>
    public class SimWorld
    {
        public const float DAY_START = 180f;   // 09:00 낮 진입
        public const float EVENING = 720f;     // 18:00 전령
        public const float DAY_END = 900f;     // 21:00

        public SimBundle b;
        public SimOptions opt;
        public SimPersona persona;
        public System.Random rng;
        // 특성 롤/Haggler 판정 전용 — 메인 rng 스트림을 건드리지 않아 useTraits OFF 런이
        // 특성 반영 이전 결과와 완전히 일치한다 (단계 4 무결성 검증)
        private System.Random traitRng;
        // 점술 상담/운세 롤 전용 — 위와 같은 이유로 메인 rng와 분리 (단계 5-1 무결성 검증)
        private System.Random seerRng;
        // 네임드 스폰/재방문/스탯 롤 전용 (단계 5-2 무결성 검증)
        private System.Random namedRng;
        // 엔드리스 템플릿 추첨 전용 — 캠페인 구간(1~60주)은 이 rng를 한 번도 안 쓰므로
        // 엔드리스 도입 전후로 캠페인 결과가 완전히 동일하다 (무결성 검증)
        private System.Random questRng;

        /// <summary>네임드 영속 풀 (단계 5-2) — 회차 시작 시 생성, 회차 내내 유지</summary>
        public List<SimNamed> namedPool = new List<SimNamed>();

        // 회차 상태
        public int day = 1;
        public int week = 1;
        public int gold = 5000;                // GameData 기본 시작 골드
        public int rep = 0;
        public int insightScore = 0;
        public List<SimWeapon> weapons = new List<SimWeapon>();
        public int[] mats = new int[5];        // 등급별 진화 재료

        // 액티브 아이템 (단계 3) — 제작/특수 재료와 아이템 재고 (StaticID -> 수량)
        public readonly Dictionary<string, int> craftMats = new Dictionary<string, int>();
        public readonly Dictionary<string, int> items = new Dictionary<string, int>();

        // 영구 누적 미러 (1회차 기준)
        private readonly Dictionary<string, int> exploration = new Dictionary<string, int>();
        private readonly HashSet<string> greatReady = new HashSet<string>();
        private readonly Dictionary<string, int> clears = new Dictionary<string, int>();

        // 주간 퀘스트
        private WeeklyQuestData quest;
        private int[] questProgress;
        private int questDeadline;

        // 아침 이벤트 — 투자 결과 (다음날 아침 회수, GameData.hasPendingInvestment 미러)
        private int investReturnDay = -1;
        private int investReturnGold;

        // 유산 (단계 2) — 커리어 상태 + 회차 시작 시 캐시한 효과 (LegacyManager 효과 getter 미러)
        private SimCareer career;
        private int   lgSlotsBonus;
        private int   lgShopRefreshBonus;
        private float lgEnforceRateBonus, lgEvolveRateBonus;
        private float lgEnforceCostMult = 1f, lgEvolveCostMult = 1f;
        private float lgMaterialMult = 1f, lgDisassembleMult = 1f;
        private int   lgRerollCountBonus;
        private float lgRerollCostMult = 1f;
        private float lgCommissionBonus, lgTipBonus;
        private float lgGreatMult = 1f;
        private bool  lgMorningGuarantee;
        private float eventDurMin = 60f;   // 모험 이벤트 1칸 소요 (AdventureSpeed 유산 반영, 3분 내림)

        // 유산 획득 공식용 커리어 계측 (CalculateEarnedLegacyPoints 미러)
        private int totalPosRep;           // 누적 양수 평판 (ReputationManager.AddReputation 미러)
        private int totalAdventures;       // 확인된 모험 수 (성공+실패, 사망 포함)

        // 하루 상태
        public List<BoardEntry> board = new List<BoardEntry>();
        private readonly List<PendingAdventure> pending = new List<PendingAdventure>();
        private readonly List<ScoutJob> scouts = new List<ScoutJob>();
        private int boardRefreshes;
        private int shopRefreshesToday;
        private int lastShopDay;
        private int lastEnhanceDay = -99;      // 중급자 주1회 강화용

        // 결과
        public RunResult result = new RunResult();
        private WeekStat cur = new WeekStat { week = 1 };
        public bool bankrupt;

        public int LastEnhanceDay { get => lastEnhanceDay; set => lastEnhanceDay = value; }

        #region 실행

        public RunResult Run(SimBundle bundle, SimOptions options, SimPersona p, int seed,
                             SimCareer playerCareer = null, int runIndex = 1)
        {
            b = bundle; opt = options; persona = p;
            career = playerCareer ?? new SimCareer();
            rng = new System.Random(seed * 977 + p.Name[0] + (runIndex - 1) * 7919);   // 회차별 결정론 시드
            traitRng = new System.Random(seed * 733 + p.Name[0] + (runIndex - 1) * 7919);
            seerRng  = new System.Random(seed * 509 + p.Name[0] + (runIndex - 1) * 7919);
            namedRng = new System.Random(seed * 331 + p.Name[0] + (runIndex - 1) * 7919);
            questRng = new System.Random(seed * 199 + p.Name[0] + (runIndex - 1) * 7919);
            InitNamedPool();
            result.persona = p.Name;
            result.seed = seed;
            result.careerRun = runIndex;

            CacheLegacyEffects();

            // GameData 기본 시작 골드 + ApplyLegacyStartingBonuses 미러
            gold = 5000 + career.StartingGoldBonus(b);
            AddInsight(career.StartingInsightBonus(b));

            IssueQuest(1);

            // 시작 무기 — GameManager.ApplyStartingWeaponBonuses 미러
            if (career.HasWeaponRare) GrantWeapon(RandomWeaponOfGrade(Grade.Rare), 0);
            if (career.HasWeaponEpic) GrantWeapon(RandomWeaponOfGrade(Grade.Epic), 0);

            int maxDays = opt.weeks * 7;
            for (day = 1; day <= maxDays; day++)
            {
                RunDay();
                if (bankrupt) break;
                AdvanceDayBoundary();
                if (bankrupt) break;
            }

            PushWeek();
            result.survivalDays = Math.Min(day, maxDays);
            result.namedMaxAffection = namedPool.Count(n => n.reachedMaxAffection);
            // 폐업 시 획득 유산 — LegacyManager.CalculateEarnedLegacyPoints 미러 (지급 여부는 커리어 루프가 판단)
            result.earnedLegacy = Math.Min(day, maxDays) + totalPosRep / 50 + totalAdventures / 5;
            return result;
        }

        /// <summary>회차 시작 시 유산 효과 캐시 — LegacyManager 효과 getter 미러</summary>
        private void CacheLegacyEffects()
        {
            lgSlotsBonus        = career.InventorySlotsBonus(b);
            lgShopRefreshBonus  = career.ShopRefreshBonus(b);
            lgEnforceRateBonus  = career.EnforceRateBonus(b);
            lgEvolveRateBonus   = career.EvolveRateBonus(b);
            lgEnforceCostMult   = career.EnforceCostMult(b);
            lgEvolveCostMult    = career.EvolveCostMult(b);
            lgMaterialMult      = career.MaterialReductionMult(b);
            lgDisassembleMult   = career.DisassembleMult(b);
            lgRerollCountBonus  = career.RerollCountBonus(b);
            lgRerollCostMult    = career.RerollCostMult(b);
            lgCommissionBonus   = career.CommissionBonus(b);
            lgTipBonus          = career.TipBonus(b);
            lgGreatMult         = career.GreatSuccessMult(b);
            lgMorningGuarantee  = career.HasMorningEventGuarantee;
            // 이벤트 1칸 소요 — GetEventDuration의 3분 내림 미러
            eventDurMin = Mathf.Max(3f, Mathf.Floor(60f * career.AdventureSpeedMult(b) / 3f) * 3f);
        }

        /// <summary>환전율 — LegacyConfig.goldPerLegacy (A/B 오버라이드 우선)</summary>
        private int GoldPerLegacy => opt.goldPerLegacyOverride > 0 ? opt.goldPerLegacyOverride : b.legacy.goldPerLegacy;

        /// <summary>주차 계단 가격 배율 — ConfigManager.PriceMult 미러. 에셋이 없으면 1(무변화).</summary>
        private float PriceMult(Func<PriceTierConfig, float[]> selector) =>
            b.priceTier == null ? 1f : b.priceTier.At(selector(b.priceTier), week);

        /// <summary>제작 골드 — BlacksmithManager.GetCraftBaseGold 미러 (봇 여유 판단도 같은 값을 쓰도록 공개)</summary>
        public int WeaponCraftGold(WeaponRecipeData r) =>
            r == null ? 0 : Mathf.RoundToInt(r.requiredGold * PriceMult(p => p.weaponCraftCost));

        public int ItemCraftGold(ActiveItemRecipeData r) =>
            r == null ? 0 : Mathf.RoundToInt(r.requiredGold * PriceMult(p => p.itemCraftCost));

        private void RunDay()
        {
            boardRefreshes = 0;
            shopRefreshesToday = 0;
            scouts.Clear();

            // [아침] 이월 모험 캐치업 (밤 점프 흡수분은 스케줄러가 이미 완료 시각 0분으로 예약)
            ProcessUpTo(0f);

            // [아침] 1일차 튜토리얼: 무료 일반 무기 1자루 (TutorialManager 무기상 강제 스폰 미러)
            if (day == 1)
                GrantWeapon(RandomWeaponOfGrade(Grade.Common), 0);

            // [아침] 이벤트 NPC — 등장 확률(평판)·타입 추첨 미러, 하루 1회
            RunMorningEvent();

            // [아침] 투자 회수 — 다음날 아침 InvestorResult NPC 미러 (0G = 먹튀)
            if (investReturnDay == day && investReturnGold > 0)
            {
                MorningGain(investReturnGold);
                investReturnGold = 0;
            }

            // [아침] 대장장이 (매일 확정, 즉시 판정)
            persona.OnBlacksmith(this);

            // [아침] 무기상
            if (ShopAppearsToday())
            {
                var stock = GenerateStock();
                persona.OnShop(this, stock);
                lastShopDay = day;
            }

            // [09:00] 의뢰판 + 수색
            board = RollBoardPool();
            board = persona.SelectBoard(this, board);
            foreach (var e in board)
                if (rng.NextDouble() < 0.2) e.highlighted = true;   // QuestBoardManager 강조 확률 20%
            persona.OnScouts(this);

            ProcessUpTo(DAY_START);

            // [낮] 방문자 스트림
            float cursor = DAY_START;
            float t = DAY_START;
            while (true)
            {
                t += NextSpawnInterval();
                if (t >= EVENING) break;

                ProcessUpTo(Math.Max(cursor, t));
                if (t > cursor) cursor = t;

                var v = GenerateVisitor(t);
                cur.visitors++;

                float stayEnd = t + b.visitor.adventurerStayDuration;
                if (cursor > stayEnd) { cur.left++; continue; }   // 테스트로 시간을 쓰는 동안 떠남

                persona.OnVisitor(this, v, ref cursor);
                ProcessUpTo(cursor);
            }

            // [저녁] 완료 확인 + 전령 (미확인 결과 강제 수령)
            ProcessUpTo(DAY_END);
            ConfirmAllDueToday();

            // 미완료 수색 환불 (날짜 변경 시 환불 — ScoutManager 미러)
            foreach (var s in scouts)
                if (!s.done) { gold += s.cost; cur.spent -= s.cost; cur.spentScout -= s.cost; }
        }

        private void AdvanceDayBoundary()
        {
            // 주간 퀘스트 마감: currentDay > deadlineDay (WeeklyQuestInstance.IsExpired 미러)
            if (day + 1 > questDeadline)
            {
                bool passed = QuestComplete();
                if (passed)
                {
                    gold += quest.goldReward;
                    cur.income += quest.goldReward;
                    AddRep(quest.reputationReward);
                    AddInsight(quest.insightReward);
                    cur.questPassed = true;
                }
                else
                {
                    int fine = WeeklyFine();
                    if (gold < fine)
                    {
                        // 긴급 환전 — QuestResultController.OnPayFine -> EnsureGold 미러.
                        // 부족분만 유산으로 충전해 납부하고, 유산도 부족하면 폐업한다.
                        int need = Mathf.CeilToInt((fine - gold) / (float)GoldPerLegacy);
                        if (career.legacyPoints >= need)
                        {
                            career.legacyPoints -= need;
                            int gain = need * GoldPerLegacy;
                            gold += gain;
                            cur.income += gain;
                            result.legacyConverted += need;
                        }
                        else
                        {
                            bankrupt = true;
                            result.bankruptWeek = week;
                            return;
                        }
                    }
                    gold -= fine;
                    cur.spent += fine;
                    cur.finePaid = fine;
                    AddRep(-quest.reputationPenalty);
                }

                PushWeek();
                week++;
                cur = new WeekStat { week = week };
                IssueQuest(day + 1);   // 다음 날 아침 발급 기준
            }
        }

        private void PushWeek()
        {
            cur.goldEnd = gold;
            cur.repEnd = rep;
            cur.repLevel = RepLevel();
            cur.insightEnd = insightScore;
            cur.weaponsOwned = weapons.Count;
            result.weekly.Add(cur);
        }

        #endregion

        #region 시간 진행 / 확인

        /// <summary>t(분)까지 배경 프로세스 처리: 수색 완료, 완료 모험의 확인(즉시 확인 페르소나)</summary>
        public void ProcessUpTo(float t)
        {
            foreach (var s in scouts)
                if (!s.done && s.knownAt <= t && s.knownAt <= DAY_END)
                {
                    s.done = true;
                    s.entry.armorKnown = true;
                }

            foreach (var p in pending)
            {
                if (p.confirmed || p.completeDay != day) continue;
                float confirmAt = persona.ImmediateConfirm ? p.completeMin : Math.Max(p.completeMin, EVENING);
                if (confirmAt <= t) Confirm(p);
            }
            pending.RemoveAll(x => x.confirmed);
        }

        /// <summary>하루 끝: 오늘 완료된 모험 전부 확인 (전령 강제 — GoToNextDay의 HasPendingHeraldReport 가드 미러)</summary>
        private void ConfirmAllDueToday()
        {
            foreach (var p in pending)
                if (!p.confirmed && p.completeDay <= day)
                    Confirm(p);
            pending.RemoveAll(x => x.confirmed);
        }

        private void Confirm(PendingAdventure p)
        {
            p.confirmed = true;
            totalAdventures++;   // 유산 획득 공식용 (성공+실패, 사망 포함 — Calculations 미러)

            int ti = (int)p.trait;   // 특성별 계측 (단계 4)
            result.traitCount[ti]++;
            result.traitDurMin[ti] += p.durTotalMin;

            if (p.weapon != null)
            {
                p.weapon.busy = false;
                p.weapon.usage++;
            }
            if (p.named != null) p.named.busy = false;

            if (p.death)
            {
                AddRep(b.adventure.deathReputationChange);   // AdventureConfig.deathReputationChange
                result.traitDeath[ti]++;
                // 네임드 사망 — 부활 이벤트(HandleDeadAdventurerSelected)는 미모델이라 회차 내 영구 이탈
                if (p.named != null) { p.named.isAlive = false; result.namedDeaths++; }
                result.traitRep[ti] += b.adventure.deathReputationChange;
                cur.deaths++;
                if (p.weapon != null) weapons.Remove(p.weapon);   // 사망 시 무기 손실
                return;
            }

            // 수수료 — AdventureManager.ApplyCommission 미러 (유산 수수료/팁 + 특성 수수료 반영, 후퇴 지급에도 적용)
            float tip = 0f;
            if (p.armorBonus >= b.adventure.tipThreshold4) tip = b.adventure.tipRate4;
            else if (p.armorBonus >= b.adventure.tipThreshold3) tip = b.adventure.tipRate3;
            else if (p.armorBonus >= b.adventure.tipThreshold2) tip = b.adventure.tipRate2;
            else if (p.armorBonus >= b.adventure.tipThreshold1) tip = b.adventure.tipRate1;
            if (tip > 0f) tip += lgTipBonus;
            float rental = p.isDefault ? 0f : b.adventure.rentalCommissionRate + lgCommissionBonus;
            float baseTotal = b.adventure.baseCommissionRate + rental + tip;
            float commission = baseTotal + TraitCommissionRate(p.trait, baseTotal);
            // 지급은 양수일 때만 — ProcessRewards의 playerGoldReward > 0 가드 미러 (음수 수수료율이면 무지급)
            int income = Math.Max(0, Mathf.RoundToInt(p.payoutGold * commission));
            gold += income;
            cur.income += income;
            cur.incomeAdventure += income;   // R 계산용 — 퀘스트 보상·분해 골드는 제외
            result.traitIncome[ti] += income;
            BuyDroppedMaterials(p);
            BuyDroppedCraftMaterials(p);

            if (p.success)
            {
                cur.successes++;
                result.traitSuccess[ti]++;
                // 평판: ((클리어 칸/2) + 등급 보너스 + 타입 매칭 + 대성공 + 특성) x 두루마리 배율
                // (CalculateRewards 미러. 호감도는 시뮬 미추적)
                int r = p.clearedEvents / 2 + b.adventure.gradeRepBonus[(int)p.dungeonGrade]
                        + TraitReputationBonus(p.trait);
                if (p.typeMatch) r += b.adventure.typeMatchReputationGain;
                if (p.great) { r += b.adventure.greatSuccessReputationGain; cur.greats++; AddInsight(b.insight.greatSuccessInsightReward); }
                // 두루마리는 전 보너스 합산 뒤 곱한다 (CalculateRewards 미러)
                if (p.fameScrollBonus > 0f) r = Mathf.RoundToInt(r * (1f + p.fameScrollBonus));
                AddRep(r);
                result.traitRep[ti] += r;

                // 호감도 — CalculateRewards 미러 (성공 +3, 타입 매칭 +2). 네임드만 누적된다
                if (p.named != null)
                {
                    int aff = b.adventure.successAffectionGain;
                    if (p.typeMatch) aff += b.adventure.typeMatchAffectionGain;
                    AddAffection(p.named, aff);
                }

                // 퀘스트 갱신 — AdventureManager.Calculations 성공 블록 미러
                QuestUpdate(QuestType.SuccessfulAdventures);
                QuestUpdate(QuestType.RentSpecificGrade, grade: p.weaponGrade);
                QuestUpdate(QuestType.RentSpecificWeapon, weaponType: p.weaponType);
                QuestUpdate(QuestType.CompleteSpecificDungeon, dungeonID: p.dungeonID);
                if (p.great) QuestUpdate(QuestType.GreatSuccessCount);
                QuestUpdate(QuestType.GoldEarned, amount: income);
            }
            else
            {
                // 실패: (클리어 칸/2) - 등급별 감점 기준, 상한 -1 (CalculateRewards 미러)
                int fr = Math.Min(-1, p.clearedEvents / 2 - b.adventure.gradeRepFailBase[(int)p.dungeonGrade]);
                AddRep(fr);
                result.traitRep[ti] += fr;
                AddAffection(p.named, b.adventure.failAffectionLoss);   // -3 (후퇴)
            }
        }

        /// <summary>
        /// 모험 결과 재료 구매 — AdventureResultView 미러. 재료는 공짜가 아니라 buyPrice로 사야 얻는다.
        /// 봇은 제작/재부여를 하지 않으므로 자기가 쓰는 진화 재료만 산다(제작·특수 재료는 미구매).
        ///
        /// 두 가지 구매 조건은 봇이 자멸하지 않기 위한 최소 정책이다:
        /// 1) 진화 시도 EvolveStockTries회분 재고까지만 — 안 쓸 재료를 무한정 사면
        ///    "18000G 사서 2000G만 쓰는" 봇이 되어 밸런스가 아니라 봇 결함을 측정하게 된다
        /// 2) 이번 주 벌금을 남기고 살 수 있을 때만 — 벌금 미납 폐업이 재료 구매보다 우선한다
        ///
        /// need는 총 소비량 예산이 아니라 <b>재고 상한</b>이다. 상한에 닿으면 구매가 멈추고 소비한 만큼만 다시 사므로
        /// 장기 지출은 어차피 실제 소비량과 같아진다 — need가 정하는 건 재료에 묶여 죽는 골드의 크기뿐이다.
        /// 그래서 기대 시도 횟수(1/p)를 곱하면 안 된다. 성공률이 낮아지면 시도가 늘어 소비가 이미 자동으로 늘기 때문에
        /// 상한까지 부풀리면 이중 계상이 되고, 무기를 많이 쥔 중급자에서 수십만 골드가 재고로 굳는다.
        /// </summary>
        private void BuyDroppedMaterials(PendingAdventure p)
        {
            // 봇은 하루 1자루만 진화하므로 재고 상한은 무기 개수가 아니라 "그 등급 무기의 존재 여부"로 정한다
            const int EvolveStockTries = 2;

            if (p.matDrop <= 0) return;

            int gi = (int)p.dungeonGrade;
            var (mainNeed, nextNeed) = EvolveMatNeed();
            int need = 0;
            // 주력 수요 — 등급 gi 무기의 진화
            if (gi < (int)Grade.Legendary && weapons.Any(w => (int)w.grade == gi))
                need += mainNeed * EvolveStockTries;
            // 상위 수요 — 등급 gi-1 무기의 진화 연료
            if (gi > 0 && weapons.Any(w => (int)w.grade == gi - 1))
                need += nextNeed * EvolveStockTries;
            if (mats[gi] >= need) return;

            // 구매가 = 개당가 x 수량 x 특성 배율(Porter 0.5) — AdventureResultItem.InitializeMaterial 미러
            int cost = Mathf.RoundToInt(p.matDrop * b.EnforceMatPrice(p.dungeonGrade) * TraitMaterialPriceMult(p.trait));
            int reserve = WeeklyFine();
            if (cost > 0 && gold - cost < reserve) return;

            gold -= cost;
            cur.spent += cost;
            cur.spentMaterial += cost;
            cur.materialBuyCount++;
            result.traitMatSpend[(int)p.trait] += cost;
            mats[gi] += p.matDrop;
        }

        #endregion

        #region 액티브 아이템 — ExecuteCraft / 재료 드롭 미러 (단계 3)

        /// <summary>
        /// 제작 재료 구매 — AdventureResultView 미러. 페르소나의 제작 목표 레시피에 필요한 만큼만
        /// (2회 제작분 상한) 벌금 유보 하에 산다 (3부 원칙 1 — 무한 구매 봇 자멸 방지).
        /// </summary>
        private void BuyDroppedCraftMaterials(PendingAdventure p)
        {
            if (p.craftDrops == null || p.craftDrops.Count == 0) return;

            var need = new Dictionary<string, int>();
            foreach (var recipe in persona.CraftTargets(this))
            {
                if (recipe == null) continue;
                foreach (var m in recipe.requiredMaterials)
                {
                    if (m.material == null) continue;
                    string id = m.material.StaticID;
                    int want = ReducedMatCount(m.count) * 2;
                    need[id] = Math.Max(need.TryGetValue(id, out var w) ? w : 0, want);
                }
            }
            // 무기 레시피 재료도 목표에 포함 (단계 5-3). 빠지면 재료를 안 사서 제작이 영영 불가능하다.
            // 무기는 소모품이 아니므로 1회분만 (아이템은 2회분)
            if (opt.useCraft)
            {
                foreach (var recipe in persona.WeaponCraftTargets(this))
                {
                    if (recipe == null) continue;
                    foreach (var m in recipe.requiredMaterials)
                    {
                        if (m.material == null) continue;
                        string id = m.material.StaticID;
                        int want = ReducedMatCount(m.count);
                        need[id] = Math.Max(need.TryGetValue(id, out var w) ? w : 0, want);
                    }
                }
            }
            if (need.Count == 0) return;

            int reserve = WeeklyFine();
            foreach (var kv in p.craftDrops)
            {
                if (!need.TryGetValue(kv.Key, out int want)) continue;
                int buy = Math.Min(kv.Value, want - MatCountById(kv.Key));
                if (buy <= 0) continue;
                // 구매가 = 개당가 x 수량 x 특성 배율(Porter 0.5) — AdventureResultItem.InitializeMaterial 미러
                int cost = Mathf.RoundToInt(buy * b.MaterialValue(kv.Key) * TraitMaterialPriceMult(p.trait));
                if (cost <= 0 || gold - cost < reserve) continue;
                gold -= cost;
                cur.spent += cost;
                cur.spentCraftMat += cost;
                result.traitMatSpend[(int)p.trait] += cost;
                AddMatById(kv.Key, buy);
            }
        }

        /// <summary>아이템 제작 — BlacksmithManager.ExecuteCraft 미러 (해금일/재료/골드, 유산 재료 감소. 대장장이 타입 할인 제외)</summary>
        public bool TryCraftItem(ActiveItemRecipeData r)
        {
            if (r == null || r.resultItem == null || day < r.unlockedDay) return false;
            foreach (var m in r.requiredMaterials)
                if (m.material == null || MatCountById(m.material.StaticID) < ReducedMatCount(m.count)) return false;
            int cost = ItemCraftGold(r);
            if (gold < cost) return false;

            gold -= cost;
            cur.spent += cost;
            cur.spentItemCraft += cost;
            cur.craftCount++;
            foreach (var m in r.requiredMaterials)
                ConsumeMatById(m.material.StaticID, ReducedMatCount(m.count));
            items[r.resultItem.StaticID] = ItemCount(r.resultItem.StaticID) + 1;
            return true;
        }

        /// <summary>재료 감소 적용 개수 — ApplyMaterialReduction 미러 (유산만, 대장장이 타입 제외)</summary>
        public int ReducedMatCount(int count) => Math.Max(1, Mathf.RoundToInt(count * lgMaterialMult));

        /// <summary>해금된 레시피 중 해당 타입 최고 등급 (상급자용)</summary>
        public ActiveItemRecipeData BestUnlockedRecipe(ActiveItemType type) =>
            b.itemRecipes.Where(r => r.resultItem.itemType == type && day >= r.unlockedDay)
                         .OrderByDescending(r => r.unlockedDay)
                         .ThenByDescending(r => r.resultItem.effectValue).FirstOrDefault();

        /// <summary>해금된 레시피 중 해당 타입 기본 등급 (중급자용)</summary>
        public ActiveItemRecipeData FirstUnlockedRecipe(ActiveItemType type) =>
            b.itemRecipes.Where(r => r.resultItem.itemType == type && day >= r.unlockedDay)
                         .OrderBy(r => r.unlockedDay)
                         .ThenBy(r => r.resultItem.effectValue).FirstOrDefault();

        public int ItemCount(string id) => items.TryGetValue(id, out var v) ? v : 0;

        /// <summary>재고에서 해당 타입 최고 효과 아이템</summary>
        public ActiveItemData BestItemInStock(ActiveItemType type) =>
            b.activeItems.Where(a => a.itemType == type && ItemCount(a.StaticID) > 0)
                         .OrderByDescending(a => a.effectValue).FirstOrDefault();

        /// <summary>아이템 1개 소비 — 재고 없으면 false (효과 미적용)</summary>
        public bool ConsumeItem(ActiveItemData d)
        {
            if (d == null || ItemCount(d.StaticID) <= 0) return false;
            items[d.StaticID] = ItemCount(d.StaticID) - 1;
            cur.itemUseCount++;
            return true;
        }

        /// <summary>재료 보유량 — 진화 재료는 mats[], 그 외(제작/특수)는 craftMats</summary>
        public int MatCountById(string id)
        {
            int gi = Array.IndexOf(SimBundle.EnforceMatIDs, id);
            if (gi >= 0) return mats[gi];
            return craftMats.TryGetValue(id, out var v) ? v : 0;
        }

        private void AddMatById(string id, int n)
        {
            int gi = Array.IndexOf(SimBundle.EnforceMatIDs, id);
            if (gi >= 0) mats[gi] += n;
            else craftMats[id] = MatCountById(id) + n;
        }

        private void ConsumeMatById(string id, int n)
        {
            int gi = Array.IndexOf(SimBundle.EnforceMatIDs, id);
            if (gi >= 0) mats[gi] -= n;
            else craftMats[id] = Math.Max(0, MatCountById(id) - n);
        }

        /// <summary>재료 등급별 드롭 가중치 — GetMaterialDropWeight 미러</summary>
        private int DropWeight(MaterialData m)
        {
            var weights = b.adventure.materialDropWeightByGrade;
            int index = (int)m.grade;
            if (weights == null || index < 0 || index >= weights.Length) return 1;
            return weights[index];
        }

        /// <summary>제작 재료 가중 추첨 — SelectMaterialByWeight 미러 (가중치 = 등급별 materialDropWeightByGrade)</summary>
        private string RollDropMaterial(List<MaterialData> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            int total = 0;
            foreach (var m in pool) total += DropWeight(m);
            if (total <= 0) return null;
            int roll = rng.Next(0, total);
            int acc = 0;
            foreach (var m in pool)
            {
                acc += DropWeight(m);
                if (roll < acc) return m.StaticID;
            }
            return pool[pool.Count - 1].StaticID;
        }

        private void AddCraftDrops(Dictionary<string, int> drops, DungeonData d, int count)
        {
            for (int i = 0; i < count; i++)
            {
                string id = RollDropMaterial(d.dropMaterials);
                if (id == null) continue;
                drops[id] = (drops.TryGetValue(id, out var v) ? v : 0) + 1;
            }
        }

        /// <summary>보스 성공 제작/특수 재료 — CalculateMaterialDropsWithBonus 미러 (기분 제외. 특성은 기본량 가산 — 단계 4)</summary>
        private void BossCraftDrops(Dictionary<string, int> drops, DungeonData d, SimWeapon weapon, int knifeBonus, bool great,
                                    int traitMatBonus, float mapBonus)
        {
            var cfg = b.adventure;
            int baseAmount = rng.Next(cfg.materialDropMin, cfg.materialDropMax)    // Random.Range(int) 미러 — max 미포함
                             + traitMatBonus;                                      // Looter/Butcher (등급·대성공 배율 이전 가산)
            int bonusAmount = knifeBonus + Mathf.RoundToInt(weapon?.EffectSum(WeaponEffectType.MaterialAmountBonus) ?? 0f);
            float gradeMult = d.grade switch
            {
                Grade.Common => cfg.dungeonCommonMultiplier,
                Grade.Uncommon => cfg.dungeonUncommonMultiplier,
                Grade.Rare => cfg.dungeonRareMultiplier,
                Grade.Epic => cfg.dungeonEpicMultiplier,
                Grade.Legendary => cfg.dungeonLegendaryMultiplier,
                _ => 1f,
            };
            int baseFinal  = Mathf.RoundToInt(baseAmount * gradeMult);
            int bonusFinal = Mathf.RoundToInt(bonusAmount * gradeMult);
            if (great) { baseFinal *= cfg.greatSuccessMaterialMultiplier; bonusFinal *= cfg.greatSuccessMaterialMultiplier; }
            AddCraftDrops(drops, d, baseFinal + bonusFinal);

            // 특수 재료 — 등급별 확률 (무기 SpecialMaterialChance 가산)
            if (d.specialDropMaterial != null)
            {
                float chance = d.grade switch
                {
                    Grade.Common => cfg.specialDropCommon,
                    Grade.Uncommon => cfg.specialDropUncommon,
                    Grade.Rare => cfg.specialDropRare,
                    Grade.Epic => cfg.specialDropEpic,
                    Grade.Legendary => cfg.specialDropLegendary,
                    _ => cfg.specialDropCommon,
                };
                chance += weapon?.EffectSum(WeaponEffectType.SpecialMaterialChance) ?? 0f;
                chance *= 1f + mapBonus;   // 보물 지도 — 무기 효과 가산 뒤 곱한다
                if (Roll(chance))
                {
                    string id = d.specialDropMaterial.StaticID;
                    drops[id] = (drops.TryGetValue(id, out var v) ? v : 0) + cfg.specialMaterialDrop;
                }
            }
        }

        #endregion

        #region 특성 — AdventureManager.Calculations GetTrait* 미러 (단계 4)

        // useTraits OFF면 전부 중립값을 돌려줘 특성 반영 이전 코드와 동일하게 동작한다.

        /// <summary>성공률 가산 — GetTraitSuccessBonus (clamp 안쪽 합산)</summary>
        private float TraitSuccessBonus(TraitType t, Grade dungeonGrade) => !opt.useTraits ? 0f : t switch
        {
            TraitType.Focused      => b.trait.traitFocusedSuccessBonus,
            TraitType.BattleManiac => b.trait.traitBattleManiacSuccessBonus,
            TraitType.Rising       => ((int)dungeonGrade + 1) * b.trait.traitRisingBonusPerTier,
            TraitType.EasyExpert   => (int)dungeonGrade <= 1
                                        ? b.trait.traitEasyExpertLowTierBonus
                                        : b.trait.traitEasyExpertHighTierPenalty,
            _ => 0f,
        };

        /// <summary>성공률 배율 — GetTraitSuccessMultiplier (clamp 뒤 곱)</summary>
        private float TraitSuccessMult(TraitType t) => !opt.useTraits ? 1f : t switch
        {
            TraitType.Berserker => b.trait.traitBerserkerSuccessMultiplier,
            TraitType.Coward    => b.trait.traitCowardSuccessMultiplier,
            _ => 1f,
        };

        /// <summary>사망률 배율 — GetTraitDeathMultiplier (clamp·수호의 메달 뒤 곱)</summary>
        private float TraitDeathMult(TraitType t) => !opt.useTraits ? 1f : t switch
        {
            TraitType.Berserker => b.trait.traitBerserkerDeathMultiplier,
            TraitType.Coward    => b.trait.traitCowardDeathMultiplier,
            _ => 1f,
        };

        /// <summary>대성공 확률 가산 — GetTraitGreatSuccessBonus (유산 배율 안쪽)</summary>
        private float TraitGreatBonus(TraitType t) =>
            opt.useTraits && t == TraitType.Lucky ? b.trait.traitLuckyGreatSuccessBonus : 0f;

        /// <summary>제작 재료 기본량 가산 — GetTraitMaterialBonus (등급·대성공 배율 이전. 진화 재료엔 미적용)</summary>
        private int TraitMaterialBonus(TraitType t) => !opt.useTraits ? 0 : t switch
        {
            TraitType.Looter  => b.trait.traitLooterMaterialBonus,
            TraitType.Butcher => b.trait.traitButcherMaterialBonus,
            _ => 0,
        };

        /// <summary>소요 시간 배율 — GetTraitDurationMultiplier</summary>
        private float TraitDurationMult(TraitType t) => !opt.useTraits ? 1f : t switch
        {
            TraitType.Swift   => b.trait.traitSwiftDurationMultiplier,
            TraitType.Focused => b.trait.traitFocusedDurationMultiplier,
            _ => 1f,
        };

        /// <summary>탐험도 배율 — GetTraitExplorationMultiplier</summary>
        private float TraitExplorationMult(TraitType t) => !opt.useTraits ? 1f : t switch
        {
            TraitType.Swift        => 0f,
            TraitType.BattleManiac => 0f,
            TraitType.Veteran      => b.trait.traitVeteranExplorationMultiplier,
            _ => 1f,
        };

        /// <summary>초기 사망 보호 가산 — Enduring (StartAdventure 미러)</summary>
        private int TraitProtectionBonus(TraitType t) =>
            opt.useTraits && t == TraitType.Enduring ? b.trait.traitEnduringProtectionBonus : 0;

        /// <summary>수수료율 가산 — GetTraitCommissionRate (Butcher는 baseTotal 비례 차감, Haggler는 확인 시점 3지선다 롤)</summary>
        private float TraitCommissionRate(TraitType t, float baseTotal)
        {
            if (!opt.useTraits) return 0f;
            switch (t)
            {
                case TraitType.Rich:    return b.trait.traitRichTipRate;
                case TraitType.Butcher: return baseTotal * (b.trait.traitButcherFeeMultiplier - 1f);
                case TraitType.Haggler:
                    float[] rates = { b.trait.traitHagglerRateLow, b.trait.traitHagglerRateMid, b.trait.traitHagglerRateHigh };
                    return rates[traitRng.Next(rates.Length)];
                default: return 0f;
            }
        }

        /// <summary>성공 시 평판 가산 — GetTraitReputationBonus (Famous)</summary>
        private int TraitReputationBonus(TraitType t) =>
            opt.useTraits && t == TraitType.Famous ? b.trait.traitFamousReputationBonus : 0;

        /// <summary>결과 화면 재료 구매가 배율 — GetTraitMaterialPriceMultiplier (Porter)</summary>
        private float TraitMaterialPriceMult(TraitType t) =>
            opt.useTraits && t == TraitType.Porter ? b.trait.traitPorterMaterialPriceMultiplier : 1f;

        #endregion

        #region 무기 제작 / 재부여 — BlacksmithManager 미러 (단계 5-3)

        /// <summary>
        /// 무기 제작 — BlacksmithManager.ExecuteCraft(WeaponRecipeData) 미러.
        /// 해금일 + 인벤 슬롯 + 골드 + 재료(유산 감소 적용)를 검사하고 즉시 무기를 만든다.
        /// 대장장이 타입 할인(ApplyCostReduction)은 시뮬 미모델이라 제외 — 강화/진화와 같은 기준.
        /// </summary>
        public bool TryCraftWeapon(WeaponRecipeData r)
        {
            if (!opt.useCraft || r == null || r.resultWeapon == null) return false;

            // 실패 사유 분류 (진단) — 제작이 0에 수렴할 때 봇 결함인지 재료 병목인지 가른다
            int cost = WeaponCraftGold(r);
            if (day < r.unlockedDay) { result.craftFailLocked++; return false; }
            if (!CanAddWeapon())     { result.craftFailSlot++;   return false; }
            if (gold < cost)         { result.craftFailGold++;   return false; }

            bool lackCrf = false, lackSpc = false;
            foreach (var m in r.requiredMaterials)
            {
                if (m.material == null) continue;
                if (MatCountById(m.material.StaticID) >= ReducedMatCount(m.count)) continue;
                // 특수 재료(MAT_SPC_*)는 던전당 1종 x 3~10% 드롭이라 일반 제작 재료와 병목 성격이 다르다
                if (m.material.StaticID.StartsWith("MAT_SPC")) lackSpc = true; else lackCrf = true;
            }
            if (lackCrf || lackSpc)
            {
                if (lackSpc) result.craftFailSpecialMat++;
                if (lackCrf) result.craftFailCraftMat++;
                return false;
            }

            gold -= cost;
            cur.spent += cost;
            cur.spentWeaponCraft += cost;
            cur.weaponCraftCount++;
            foreach (var m in r.requiredMaterials)
                ConsumeMatById(m.material.StaticID, ReducedMatCount(m.count));

            GrantWeapon(r.resultWeapon, 0);
            QuestUpdate(QuestType.CraftComplete);
            return true;
        }

        /// <summary>재부여 비용 — GetRerollGoldCost 미러 (잠금 배율 x 유산 배율. 대장장이 할인 제외)</summary>
        public int RerollGoldCost(int lockedCount)
        {
            float mult = 1f;
            if (lockedCount > 0)
            {
                var arr = b.blacksmith.rerollLockCostMultipliers;
                if (arr != null && arr.Length > 0)
                    mult = arr[Mathf.Clamp(lockedCount - 1, 0, arr.Length - 1)];
            }
            int base_ = Mathf.RoundToInt(b.blacksmith.rerollBaseGold * mult * PriceMult(p => p.rerollCost));
            return Mathf.RoundToInt(base_ * lgRerollCostMult);
        }

        /// <summary>WeaponInstance.MaxRerollCount 미러 (유산 보너스 반영)</summary>
        public int MaxRerollCount => b.weaponCfg.MaxRerollCount + lgRerollCountBonus;

        /// <summary>WeaponInstance.CanReroll 미러 (추가 충전은 유산 단발 소비라 미모델)</summary>
        public bool CanReroll(SimWeapon w) => w != null && !w.busy && w.rerollCount < MaxRerollCount;

        /// <summary>
        /// 재부여 — ExecuteReroll + WeaponInstance.Reroll 미러.
        /// 잠그지 않은 슬롯만 같은 등급 풀에서 새로 뽑는다. 재료는 쓰지 않고 골드만 소모한다.
        /// 런타임은 결과를 보고 before/after를 고를 수 있지만(세이브스컴 차단됨), 봇은 항상 새 효과를 받는다 —
        /// "선택"까지 모델링하면 봇 정책이 결과를 지배하므로 최악 케이스(무조건 수용)로 하한을 잡는다.
        /// </summary>
        public bool TryReroll(SimWeapon w, int lockedCount)
        {
            if (!opt.useCraft || !CanReroll(w)) return false;
            int cost = RerollGoldCost(lockedCount);
            if (cost <= 0) return false;
            int reserve = WeeklyFine();
            if (gold - cost < reserve) return false;

            gold -= cost;
            cur.spent += cost;
            cur.spentReroll += cost;
            cur.rerollCount++;
            w.rerollCount++;

            // 잠금은 "값이 큰 효과부터 유지" — 런타임 UI의 잠금 선택을 근사한다
            int keep = Mathf.Clamp(lockedCount, 0, w.effects.Count);
            var kept = w.effects.OrderByDescending(e => e.value).Take(keep).ToList();
            int target = w.effects.Count;
            var rolled = new List<SimEffect>(kept);
            for (int i = kept.Count; i < target; i++)
            {
                var e = RollOneEffect(w.grade, rolled);
                if (e != null) rolled.Add(e);
            }
            w.effects = rolled;
            // CheckEnforceLevel 미러 — 강화는 최대치 효과 개수로 재산정
            w.enforce = w.effects.Count(e => e.value >= e.MaxValue);
            QuestUpdate(QuestType.RerollComplete);
            return true;
        }

        #endregion

        #region 네임드 + 호감도 — VisitorManager 미러 (단계 5-2)

        /// <summary>
        /// 회차 시작 시 네임드 영속 인스턴스 생성 — InitializeAllAdventurers 미러.
        /// 스탯은 SO의 range로 1회 롤(AdventurerInstance 생성자), 특성은 SO 고정값(Trait 프로퍼티).
        /// </summary>
        private void InitNamedPool()
        {
            namedPool.Clear();
            if (!opt.useNamed) return;

            foreach (var d in b.namedAdventurers)
            {
                var n = new SimNamed { data = d, trait = d.trait };
                n.stats[0] = namedRng.Next(d.strRange.x, d.strRange.y + 1);
                n.stats[1] = namedRng.Next(d.dexRange.x, d.dexRange.y + 1);
                n.stats[2] = namedRng.Next(d.intRange.x, d.intRange.y + 1);
                n.stats[3] = namedRng.Next(d.lukRange.x, d.lukRange.y + 1);

                float best = -1f, second = -1f;
                WeaponType bestT = WeaponType.Sword, secondT = WeaponType.Sword;
                for (int t = 0; t < 8; t++)
                {
                    float s = TypeAdvantage.GetStatScore(n.stats[0], n.stats[1], n.stats[2], n.stats[3], (WeaponType)t);
                    if (s > best) { second = best; secondT = bestT; best = s; bestT = (WeaponType)t; }
                    else if (s > second) { second = s; secondT = (WeaponType)t; }
                }
                n.trueBest = bestT;

                int bi = 0;
                for (int i = 1; i < 4; i++) if (n.stats[i] > n.stats[bi]) bi = i;
                n.bestStatIndex = bi;

                // 기본 무기는 인스턴스 생성 시 1회 확정 — CreateDefaultWeaponFor 미러 (70/20/10)
                double r = namedRng.NextDouble();
                n.observedDefaultType = r < 0.70 ? bestT : r < 0.90 ? secondT : (WeaponType)namedRng.Next(8);
                namedPool.Add(n);
            }
        }

        /// <summary>
        /// 이번 방문자가 네임드인지 결정하고 대상을 고른다 — SpawnNewAdventurer + 재방문 로직 미러.
        /// 재방문(호감도 50+)을 먼저 판정하고, 아니면 가중치 1:5로 네임드/일반을 정한다.
        /// null이면 일반 모험가.
        /// </summary>
        private SimNamed PickNamedVisitor()
        {
            if (!opt.useNamed || namedPool.Count == 0) return null;

            // 1) 재방문 — TrySpawnReturningAdventurer 미러
            var cfg = b.visitor;
            int maxAffCount = namedPool.Count(a => a.reachedMaxAffection && a.isAlive && !a.busy);
            float chance = cfg.baseRevisitChance;
            if (maxAffCount > 0)
                chance = Math.Min(chance + maxAffCount * cfg.maxAffectionBonusPerAdventurer, cfg.maxRevisitChance);
            chance = Math.Min(chance, cfg.maxRevisitChance);

            // 추첨 대상은 자격자로 한정한다 — TrySpawnReturningAdventurer 미러
            var eligible = namedPool.Where(a => a.affection >= cfg.minAffectionForRevisit && a.IsHome
                                                && day - a.lastVisitDay >= namedRng.Next(cfg.minRevisitDays, cfg.maxRevisitDays))
                                    .ToList();
            if (eligible.Count > 0 && namedRng.NextDouble() <= chance)
                return PickByAffectionWeight(eligible);

            // 2) 신규 스폰 추첨 — namedWeight 1 : normalWeight 5. 유산 NamedSpawnWeight 가산 포함
            //    (VisitorManager.Spawn.cs의 namedW = Config + GetNamedSpawnWeightBonus() 미러)
            float namedW = cfg.namedAdventurerSpawnWeight + career.NamedSpawnWeightBonus(b);
            float normalW = cfg.normalAdventurerSpawnWeight;
            if (namedW + normalW <= 0f) return null;
            if (namedRng.NextDouble() >= namedW / (namedW + normalW)) return null;

            // SpawnNamedAdventurer 미러 — 집에 있는 네임드에서 등급 가중 선택(전부 네임드라 균등)
            var candidates = namedPool.Where(a => a.IsHome).ToList();
            if (candidates.Count == 0) return null;   // 전원 모험 중이면 일반으로 대체
            return candidates[namedRng.Next(candidates.Count)];
        }

        /// <summary>재방문 대상 가중 추첨 — SpawnReturningAdventurer 미러 (Max 호감도 0.7 / 그 외 0.3)</summary>
        private SimNamed PickByAffectionWeight(List<SimNamed> candidates)
        {
            var cfg = b.visitor;
            float total = 0f;
            foreach (var a in candidates)
                total += a.reachedMaxAffection ? cfg.maxAffectionRevisitWeight : cfg.normalRevisitWeight;
            if (total <= 0f) return candidates[namedRng.Next(candidates.Count)];

            double roll = namedRng.NextDouble() * total;
            float acc = 0f;
            foreach (var a in candidates)
            {
                acc += a.reachedMaxAffection ? cfg.maxAffectionRevisitWeight : cfg.normalRevisitWeight;
                if (roll < acc) return a;
            }
            return candidates[candidates.Count - 1];
        }

        /// <summary>호감도 성공률 가산 — GetAffectionBonus 미러 (임계 25/50/75)</summary>
        private float AffectionBonus(SimNamed n)
        {
            if (n == null || !opt.useNamed) return 0f;
            var cfg = b.adventure;
            if (n.affection <= 25) return 0f;
            if (n.affection <= 50) return cfg.affectionMediumBonus;
            if (n.affection <= 75) return cfg.affectionHighBonus;
            return cfg.affectionMaxBonus;
        }

        /// <summary>호감도 증감 — AddAffection 미러 (0~100 클램프, Max 도달 플래그 유지)</summary>
        private void AddAffection(SimNamed n, int delta)
        {
            if (n == null) return;
            n.affection = Mathf.Clamp(n.affection + delta, 0, 100);
            if (n.affection >= 100) n.reachedMaxAffection = true;
        }

        #endregion

        #region 점술 — SeerManager 미러 (단계 5-1)

        /// <summary>상담 비용 — SeerManager.GetSeerCost 미러 (튜토리얼 면제는 제외)</summary>
        public int SeerCost() => Mathf.RoundToInt(b.seer.seerBaseCost * PriceMult(p => p.seerCost));

        /// <summary>LUK 구간 인덱스 — GenerateLuckLevel의 25/50/75 경계 미러</summary>
        public static int LukBucket(int luk) => luk <= 25 ? 0 : luk <= 50 ? 1 : luk <= 75 ? 2 : 3;

        /// <summary>
        /// 점술 상담 — Consult + GetLuckModifier 미러. 비용을 내고 LUK 가중 추첨으로 운세를 확정한다.
        /// 결과는 되돌릴 수 없다(흉이 나와도 그대로 적용) — 소비가 아니라 도박이다.
        /// 골드 부족이면 상담하지 않고 0을 돌려준다 (SpendGold 실패 분기 미러).
        /// </summary>
        private float ConsultSeer(SimVisitor v, out int bucket)
        {
            bucket = -1;
            if (!opt.useSeer) return 0f;
            int cost = SeerCost();
            if (cost > 0 && gold < cost) return 0f;

            gold -= cost;
            cur.spent += cost;
            cur.spentSeer += cost;
            cur.seerCount++;

            float mod = RollLuckModifier(v.stats[3]);   // stats[3] = LUK
            bucket = LukBucket(v.stats[3]);
            result.seerLukCount[bucket]++;
            result.seerLukModSum[bucket] += mod;
            return mod;
        }

        /// <summary>LUK 구간 가중 추첨 -> 운세 보정값 — GenerateLuckLevel + RollLuckLevel + GetLuckModifier 미러</summary>
        private float RollLuckModifier(int luk)
        {
            var c = b.seer;
            float wBad, wMinor, wGood, wGreat;
            switch (LukBucket(luk))
            {
                case 0:  wBad = c.luk0BadWeight; wMinor = c.luk0MinorWeight; wGood = c.luk0GoodWeight; wGreat = c.luk0GreatWeight; break;
                case 1:  wBad = c.luk1BadWeight; wMinor = c.luk1MinorWeight; wGood = c.luk1GoodWeight; wGreat = c.luk1GreatWeight; break;
                case 2:  wBad = c.luk2BadWeight; wMinor = c.luk2MinorWeight; wGood = c.luk2GoodWeight; wGreat = c.luk2GreatWeight; break;
                default: wBad = c.luk3BadWeight; wMinor = c.luk3MinorWeight; wGood = c.luk3GoodWeight; wGreat = c.luk3GreatWeight; break;
            }

            float total = wBad + wMinor + wGood + wGreat;
            if (total <= 0f) return 0f;
            float roll = (float)seerRng.NextDouble() * total;
            if (roll < wBad)                  return c.luckModifierBad;
            if (roll < wBad + wMinor)         return c.luckModifierMinor;
            if (roll < wBad + wMinor + wGood) return c.luckModifierGood;
            return c.luckModifierGreat;
        }

        /// <summary>
        /// 이벤트 1건의 판정 확률 — CalculateEventSuccessRate 미러.
        /// `기본성공률 + cumulativeModifier(점술 운세)` 후 클램프.
        /// 난이도 계수는 성공률이 아니라 요구 스탯 임계값에 적용되므로 rate 계산 쪽에서 이미 반영된다.
        /// 함정 누적 페널티와 기분 배율은 같은 함수에 있으나 이번 축이 아니라 미반영.
        /// </summary>
        private float EventRate(float rate, float luckMod) =>
            Mathf.Clamp(rate + luckMod, b.adventure.successRateMin, b.adventure.successRateMax);

        #endregion

        #region 모험 시작/판정 — CalculateSuccessRate / CalculateDeathRate / 시퀀스 미러

        /// <summary>
        /// 모험 출발 (즉시). 결과를 그 자리에서 판정하고 완료 시각만 예약한다.
        /// item = 배정 액티브 아이템(즉시 소비), consultSeer = 출발 전 점술 상담 여부(비용 즉시 지불)
        /// </summary>
        public void LaunchAdventure(SimVisitor v, SimWeapon weapon, BoardEntry entry, float startMin,
                                    ActiveItemData item = null, bool consultSeer = false)
        {
            bool isDefault = weapon == null;
            WeaponType type = isDefault ? v.observedDefaultType : weapon.Type;
            Grade wGrade = isDefault ? Grade.Common : weapon.grade;
            int enforce = isDefault ? 0 : weapon.enforce;
            var dungeon = entry.dungeon;
            var cfg = b.adventure;

            cur.attempts++;
            cur.served++;
            if (isDefault) cur.defaultRuns++;
            else weapon.busy = true;

            // 네임드는 모험 중 재스폰 대상에서 빠진다 (isAdventuring 미러)
            int affLevel = 0;
            if (v.named != null)
            {
                v.named.busy = true;
                result.namedVisits++;
                affLevel = v.named.affection <= 25 ? 0 : v.named.affection <= 50 ? 1 : v.named.affection <= 75 ? 2 : 3;
                result.affLevelCount[affLevel]++;
            }

            // 액티브 아이템 효과 캐싱 — StartAdventure 미러 (배정 즉시 소비). EscapeRope는 사용 정책 미정의로 제외
            float charmBonus = 0f, amuletBonus = 0f, deathWard = 0f, shoesMult = 1f;
            int potionProt = 0, knifeBonus = 0;
            float fameBonus = 0f, mapBonus = 0f;
            if (item != null && ConsumeItem(item))
            {
                switch (item.itemType)
                {
                    case ActiveItemType.Charm:            charmBonus = item.effectValue; break;
                    case ActiveItemType.Potion:           potionProt = (int)item.effectValue; break;
                    case ActiveItemType.SwiftShoes:       shoesMult = item.effectValue; break;
                    case ActiveItemType.DisassemblyKnife: knifeBonus = (int)item.effectValue; break;
                    case ActiveItemType.GoldAmulet:       amuletBonus = item.effectValue; break;
                    case ActiveItemType.TreasureMap:      mapBonus = item.effectValue; break;
                    case ActiveItemType.DeathWard:        deathWard = item.effectValue; break;
                    case ActiveItemType.FameScroll:       fameBonus = item.effectValue; break;
                }
            }

            // 점술 — StartAdventure의 cumulativeModifier 초기화 미러 (상담은 출발 직전, 비용 즉시 지불)
            int seerBucket = -1;
            float luckMod = consultSeer ? ConsultSeer(v, out seerBucket) : 0f;

            // 기본 성공률 — AdventureManager.Calculations.CalculateSuccessRate 미러.
            // 난이도 계수는 요구 스탯 임계값에 적용되므로 이벤트마다 값이 달라진다 -> Rate(난이도)로 그때그때 계산.
            float score = EffectStatScore(v, weapon, type);
            float armorBonus = TypeAdvantage.weaponArmorBonus[(int)type, (int)entry.armor];
            int clearCount = clears.TryGetValue(dungeon.StaticID, out int c) ? c : 0;
            float collection = (clearCount / cfg.dungeonClearMilestone) * cfg.dungeonClearMilestoneBonus
                             + Math.Min((isDefault ? 0 : weapon.usage) / cfg.weaponUsageMilestone, cfg.weaponUsageMilestoneMax)
                               * cfg.weaponUsageMilestoneBonus;
            // 부적(Charm)/호감도/특성 가산은 CalculateSuccessRate의 가산 보너스 미러, clamp 뒤 특성 배율 (런타임 순서 동일)
            float bonusSum = collection + ConditionBonus(weapon, dungeon, entry.armor) + charmBonus
                           + AffectionBonus(v.named) + TraitSuccessBonus(v.trait, dungeon.grade);
            float rateMult = TraitSuccessMult(v.trait) * (isDefault ? cfg.defaultWeaponSuccessMultiplier : 1f);

            float Rate(float difficultyMult)
            {
                float effectBase = Mathf.Clamp01(score * difficultyMult / dungeon.baseStatThreshold);
                return Mathf.Clamp(effectBase * (1f + armorBonus) + bonusSum,
                                   cfg.successRateMin, cfg.successRateMax) * rateMult;
            }

            // 시퀀스 — GenerateEventSequence 미러. 전 이벤트 60분 (에셋 전수 확인)
            int baseCount = cfg.maxEventCountByGrade[(int)dungeon.grade];
            int middles = entry.highlighted ? (baseCount - 1) * 2 : baseCount - 1;
            var middlePool = dungeon.eventPool.Where(e => e != null && e.eventType != DungeonEventType.Boss).ToList();
            var bossData = dungeon.eventPool.FirstOrDefault(e => e != null && e.eventType == DungeonEventType.Boss);

            int events = 1;   // 입장
            // 평판용 클리어 칸 — CountClearedEvents 미러. 종료 연출(귀환/후퇴)과
            // 모험을 끝낸 실패 전투를 뺀다. 보호/재도전은 칸을 차지하므로 포함(실패 전투와 상쇄)
            int cleared = 1;
            // StartAdventure 미러 — RetreatPrevention 효과 + 포션 + Enduring 특성이 초기 보호 횟수를 준다
            int protection = (isDefault ? 0 : Mathf.RoundToInt(weapon.EffectSum(WeaponEffectType.RetreatPrevention)))
                             + potionProt + TraitProtectionBonus(v.trait);
            int accumGold = 0;
            var craftDrops = new Dictionary<string, int>();
            bool success = false, death = false, great = false, ended = false;

            for (int i = 0; i < middles && !ended; i++)
            {
                var evt = WeightedPickEvent(middlePool);
                events++;
                if (evt == null) continue;
                cleared++;   // 기본은 클리어. 후퇴로 끝나는 전투만 아래에서 되돌린다

                switch (evt.eventType)
                {
                    case DungeonEventType.Battle:
                    case DungeonEventType.MiniBoss:
                        float mult = evt.eventType == DungeonEventType.Battle
                            ? cfg.battleRewardMultiplier : cfg.miniBossRewardMultiplier;
                        if (Roll(EventRate(Rate(evt.difficultyMultiplier), luckMod)))
                            accumGold += EventGold(dungeon, mult);
                        else if (protection > 0) { protection--; events++; }   // 보호 이벤트 삽입
                        else { events++; ended = true; cleared--; }             // 후퇴 — 실패 전투·후퇴 둘 다 미인정
                        break;
                    case DungeonEventType.TreasureChest:
                        accumGold += EventGold(dungeon, cfg.treasureChestRewardMultiplier);
                        break;
                    case DungeonEventType.Rest:
                        protection++;
                        break;
                    case DungeonEventType.RareDrop:
                        // CalculateRareDropMaterials 미러 — 제작 재료 드롭 (재료량 효과 가산)
                        AddCraftDrops(craftDrops, dungeon,
                            cfg.rareDropMaterialCount
                            + Mathf.RoundToInt(isDefault ? 0f : weapon.EffectSum(WeaponEffectType.MaterialAmountBonus)));
                        break;
                    case DungeonEventType.Trap:
                    {
                        // CalculatePendingTrap 미러 — 회피 소스의 여집합 곱으로 합성 확률을 만들어 1회 굴린다.
                        // 기분 배율(halfMult)과 탈출 로프는 미모델 — 둘 다 로드맵상 미반영 영역이다
                        float miss = 1f;
                        if (!isDefault)
                        {
                            foreach (var e in weapon.effects)
                                if (e.data.effectType == WeaponEffectType.TrapNegation)
                                    miss *= 1f - Mathf.Clamp01(e.value);
                        }
                        miss *= 1f - StatCurve.Evaluate(v.stats[1], cfg.dexTrapEvadeMax, cfg.dexTrapEvadeExponent);

                        if (Roll(1f - miss)) { events++; cleared++; }   // 회피 -> TrapEvade 삽입
                        else luckMod += cfg.trapSuccessPenalty;     // 피격 -> 이후 이벤트 성공률 누적 감소
                        break;
                    }
                    default:
                        break;
                }
            }

            int matDrop = 0;
            if (!ended)
            {
                float bossDiff = bossData != null ? bossData.difficultyMultiplier : 1f;
                while (true)
                {
                    events++;   // 보스
                    if (Roll(EventRate(Rate(bossDiff), luckMod)))
                    {
                        great = RollGreatSuccess(dungeon.StaticID, weapon, TraitGreatBonus(v.trait));
                        float goldMult = cfg.bossRewardMultiplier * (great ? cfg.greatSuccessGoldMultiplier : 1f);
                        accumGold += EventGold(dungeon, goldMult);
                        // 진화 재료 — 던전별 고정 개수. 대성공 배율은 기본량에만 곱한다(무기 효과분은 제외).
                        matDrop = dungeon.enforceDropCount * (great ? cfg.greatSuccessMaterialMultiplier : 1)
                                  + Mathf.RoundToInt(isDefault ? 0f : weapon.EffectSum(WeaponEffectType.EnforceMaterialBonus));

                        // 제작/특수 재료 드롭 — CalculateMaterialDropsWithBonus 미러 (해체용 단검/특성/보물 지도 반영)
                        BossCraftDrops(craftDrops, dungeon, isDefault ? null : weapon, knifeBonus, great,
                                       TraitMaterialBonus(v.trait), mapBonus);

                        events++;   // 귀환 (평판 미인정)
                        cleared++;  // 보스 클리어
                        success = true;
                        break;
                    }
                    // 재도전 삽입 후 보스 재판정 — 실패 보스는 빼고 재도전 칸을 넣어 상쇄
                    if (protection > 0) { protection--; events++; cleared++; continue; }

                    // 사망 판정 — CalculateDeathRate 미러 (수호의 메달 = 최종 확률 x (1-감소율), 마지막에 특성 배율)
                    float dr = cfg.baseDeathRate
                             - ((int)wGrade - (int)dungeon.grade) * cfg.weaponProtectionGradeDiff
                             - enforce * cfg.weaponProtectionEnforcement
                             + Math.Max(0f, 1f - score / dungeon.baseStatThreshold) * cfg.deathRateStatWeight;
                    death = Roll(Mathf.Clamp(dr / 100f, 0f, cfg.maxDeathRate) * (1f - deathWard)
                                 * TraitDeathMult(v.trait));

                    // STR 재굴림 — RollDeath 미러. 사망만 무효화하고 전투 실패는 그대로 둔다
                    if (death && Roll(StatCurve.Evaluate(v.stats[0], cfg.strDeathRerollMax, cfg.strDeathRerollExponent)))
                        death = false;

                    events++;   // 후퇴
                    break;
                }
            }

            // 탐험도 — ApplyStatisticsOnce 미러 (확정권 보유 중엔 증가 정지, 특성 배율: Veteran x2 / Swift·BattleManiac 0)
            if (!greatReady.Contains(dungeon.StaticID))
            {
                int gain = Mathf.RoundToInt((success ? cfg.explorationGainOnSuccess : cfg.explorationGainOnFail)
                                            * TraitExplorationMult(v.trait));
                exploration.TryGetValue(dungeon.StaticID, out int ex);
                ex += gain;
                exploration[dungeon.StaticID] = ex;
                if (ex >= 100) greatReady.Add(dungeon.StaticID);
            }
            if (success)
                clears[dungeon.StaticID] = clearCount + 1;
            if (seerBucket >= 0 && success)
                result.seerLukSuccess[seerBucket]++;
            if (v.named != null && success)
                result.affLevelSuccess[affLevel]++;

            // 완료 시각 예약 — 21:00 초과분은 다음날 06:00 첫 틱에 흡수 (GoToNextDay 점프 미러)
            // 칸당 소요 = eventDurMin(유산 반영) x 특성 배율(Swift 0.5/Focused 1.5) x 신발 배율, 3분 내림 (GetEventDuration 미러)
            float dur = Mathf.Max(3f, Mathf.Floor(eventDurMin * TraitDurationMult(v.trait) * shoesMult / 3f) * 3f);
            int cDay = day; float cMin = startMin;
            for (int i = 0; i < events; i++)
            {
                cMin += dur;
                if (cMin > DAY_END) { cDay++; cMin = 0f; }
            }

            // 황금 부적 — 성공 시 총 골드 x (1+보너스) (ProcessResults 미러: 성공 분기에만 적용)
            int payout = death ? 0
                       : success ? Mathf.RoundToInt(accumGold * (1f + amuletBonus))
                       : Mathf.RoundToInt(accumGold * cfg.retreatGoldRatio);

            pending.Add(new PendingAdventure
            {
                completeDay = cDay,
                completeMin = cMin,
                weapon = weapon,
                isDefault = isDefault,
                typeMatch = !isDefault && type == v.trueBest,
                weaponGrade = wGrade,
                weaponType = type,
                armorBonus = armorBonus,
                dungeonID = dungeon.StaticID,
                dungeonGrade = dungeon.grade,
                success = success,
                death = death,
                great = great,
                clearedEvents = cleared,
                payoutGold = payout,
                matDrop = success ? matDrop : 0,
                craftDrops = success ? craftDrops : null,   // 드롭 구매는 성공 확인 시에만
                fameScrollBonus = fameBonus,
                trait = v.trait,
                durTotalMin = events * dur,
                named = v.named,
                affLevelAtStart = affLevel,
            });
        }

        private bool RollGreatSuccess(string dungeonID, SimWeapon weapon, float traitBonus)
        {
            if (greatReady.Contains(dungeonID))
            {
                greatReady.Remove(dungeonID);
                exploration[dungeonID] = 0;
                return true;
            }
            float bonus = weapon?.EffectSum(WeaponEffectType.GreatSuccessBonus) ?? 0f;
            // (기본 + 특성(Lucky) + 무기 보너스) x 유산 배율 — Calculations의 대성공 판정 미러
            return Roll((b.adventure.baseGreatSuccessChance + traitBonus + bonus) * lgGreatMult);
        }

        private int EventGold(DungeonData d, float mult) =>
            Mathf.RoundToInt(rng.Next(d.baseRewardMin, d.baseRewardMax + 1) * mult);

        private DungeonEventData WeightedPickEvent(List<DungeonEventData> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            float total = pool.Sum(e => e.probability);
            if (total <= 0f) return pool[rng.Next(pool.Count)];
            float r = (float)rng.NextDouble() * total;
            float acc = 0f;
            foreach (var e in pool)
            {
                acc += e.probability;
                if (r < acc) return e;
            }
            return pool[pool.Count - 1];
        }

        #endregion

        #region 의뢰판 / 상점 / 대장간 / 수색

        public List<BoardEntry> RollBoardPool()
        {
            int lv = RepLevel();
            int poolSize = lv switch
            {
                0 => b.questBoard.bronzePoolSize,
                1 => b.questBoard.silverPoolSize,
                2 => b.questBoard.goldPoolSize,
                3 => b.questBoard.platinumPoolSize,
                _ => b.questBoard.diamondPoolSize,
            };

            // SelectDungeonsWeighted 미러 — questWeight x 주차 등급 배율(§4-2) 가중 비복원 추첨
            var remaining = b.dungeons.Where(d => d.questWeight > 0f).ToList();
            var picked = new List<BoardEntry>();
            float Weight(DungeonData d) =>
                d.questWeight * b.questBoard.GetQuestWeightMultiplier(d.grade, week);

            for (int i = 0; i < poolSize && remaining.Count > 0; i++)
            {
                float total = remaining.Sum(Weight);
                float r = (float)rng.NextDouble() * total;
                float acc = 0f;
                DungeonData sel = remaining[remaining.Count - 1];
                foreach (var d in remaining)
                {
                    acc += Weight(d);
                    if (r < acc) { sel = d; break; }
                }
                remaining.Remove(sel);
                picked.Add(new BoardEntry { dungeon = sel, armor = RollArmor(sel) });
            }
            return picked;
        }

        private ArmorType RollArmor(DungeonData d)
        {
            if (d.armorTypeVariants == null || d.armorTypeVariants.Count == 0) return d.armorType;
            float total = d.armorTypeVariants.Sum(v => v.weight);
            if (total <= 0f) return d.armorType;
            float r = (float)rng.NextDouble() * total;
            float acc = 0f;
            foreach (var v in d.armorTypeVariants)
            {
                acc += v.weight;
                if (r < acc) return v.armorType;
            }
            return d.armorType;
        }

        public int SelectCount()
        {
            int lv = RepLevel();
            return lv switch
            {
                0 => b.questBoard.bronzeSelectCount,
                1 => b.questBoard.silverSelectCount,
                2 => b.questBoard.goldSelectCount,
                3 => b.questBoard.platinumSelectCount,
                _ => b.questBoard.diamondSelectCount,
            };
        }

        /// <summary>새로고침 비용 — QuestBoardManager.GetRefreshCost 미러 (기본가 x 주차 계단)</summary>
        public int RefreshCost() =>
            Mathf.RoundToInt(b.questBoard.refreshBaseCost * PriceMult(p => p.boardRefreshCost));

        /// <summary>의뢰판 새로고침 — 최대 횟수 제한.</summary>
        public List<BoardEntry> TryRerollBoard()
        {
            int cost = RefreshCost();
            if (boardRefreshes >= b.questBoard.maxRefreshCount || gold < cost) return null;
            gold -= cost;
            cur.spent += cost;
            cur.spentRefresh += cost;
            boardRefreshes++;
            return RollBoardPool();
        }

        private bool ShopAppearsToday()
        {
            if (day == 1) return false;   // 1일차는 튜토리얼 무료 지급으로 대체
            if (b.weaponShop.fixedAppearanceDays.Contains(day)) return true;
            int lastFixed = b.weaponShop.fixedAppearanceDays.Length > 0 ? b.weaponShop.fixedAppearanceDays.Max() : 0;
            if (day <= lastFixed) return false;
            int daysSince = day - Math.Max(lastShopDay, lastFixed);
            float chance = b.weaponShop.spawnBaseChance
                         + b.weaponShop.spawnChancePerDay * (daysSince - 1)
                         + b.weaponShop.spawnChancePerReputationLevel * RepLevel();
            return Roll(chance);
        }

        public List<ShopItem> GenerateStock()
        {
            int count = rng.Next(b.weaponShop.minWeapons, b.weaponShop.maxWeapons + 1);
            var stock = new List<ShopItem>();
            for (int i = 0; i < count; i++)
            {
                var data = RandomWeaponOfGrade(RollShopGrade());
                if (data == null) continue;
                stock.Add(new ShopItem { data = data, price = data.basePrice });
            }
            return stock;
        }

        /// <summary>진열 등급 추첨 — WeaponShopManager.SelectGradeByWeight 미러 (주차 계단 가중치)</summary>
        private Grade RollShopGrade()
        {
            var weights = b.priceTier?.GetWeaponShopGradeWeights(week);
            if (weights == null || weights.Length == 0) return Grade.Common;

            float total = weights.Sum();
            float r = (float)rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r < acc) return (Grade)i;
            }
            return Grade.Common;
        }

        public WeaponData RandomWeaponOfGrade(Grade g)
        {
            var filtered = b.weapons.Where(w => w.baseGrade == g).ToList();
            if (filtered.Count == 0) filtered = b.weapons;
            return filtered.Count > 0 ? filtered[rng.Next(filtered.Count)] : null;
        }

        /// <summary>상점 새로고침 — 비용 고정(refreshCost), 하루 최대 횟수 제한 (유산 갱신 보너스 반영). 실패 시 null.</summary>
        public List<ShopItem> TryShopRefresh()
        {
            if (shopRefreshesToday >= b.weaponShop.maxRefreshCount + lgShopRefreshBonus || gold < b.weaponShop.refreshCost) return null;
            gold -= b.weaponShop.refreshCost;
            cur.spent += b.weaponShop.refreshCost;
            cur.spentRefresh += b.weaponShop.refreshCost;
            shopRefreshesToday++;
            return GenerateStock();
        }

        /// <summary>InventoryManager.CanAddWeapon 미러 (유산 슬롯 보너스 반영)</summary>
        public bool CanAddWeapon() => weapons.Count < b.inventory.inventorySlots + lgSlotsBonus;

        public bool BuyWeapon(ShopItem item)
        {
            if (gold < item.price || !CanAddWeapon()) return false;
            gold -= item.price;
            cur.spent += item.price;
            cur.spentWeapon += item.price;
            cur.weaponBuyCount++;
            GrantWeapon(item.data, item.price);
            QuestUpdate(QuestType.WeaponPurchase);
            return true;
        }

        /// <summary>
        /// 분해 — BlacksmithManager.CalcDisassembleReward 미러 (대장장이 부스트 제외, 유산 배율 반영).
        /// 골드와 등급별 진화 재료를 회수하고 무기를 제거한다.
        /// </summary>
        public void Disassemble(SimWeapon w)
        {
            if (w == null || w.busy) return;

            int gi = (int)w.grade;
            float enforceBonus = (1f + w.enforce * b.blacksmith.disassembleEnforceBonus) * lgDisassembleMult;
            int gainGold = Mathf.RoundToInt(b.blacksmith.disassembleGoldByGrade[gi] * enforceBonus);
            int gainMat  = Mathf.RoundToInt(b.blacksmith.disassembleMaterialByGrade[gi] * enforceBonus);

            gold += gainGold;
            cur.income += gainGold;
            mats[gi] += gainMat;
            weapons.Remove(w);
        }

        public void GrantWeapon(WeaponData data, int cost)
        {
            if (data == null) return;
            var w = new SimWeapon(data);
            GenerateEffects(w);
            weapons.Add(w);
            TrackFirstGrade(w.grade);
        }

        #region 무기 부가효과 — WeaponInstance 미러

        /// <summary>WeaponInstance.GenerateEffects 미러 — 등급별 개수만큼 롤</summary>
        private void GenerateEffects(SimWeapon w)
        {
            int count = b.weaponCfg.GetEffectCount(w.grade);
            for (int i = 0; i < count; i++)
            {
                var e = RollOneEffect(w.grade, w.effects);
                if (e != null) w.effects.Add(e);
            }
        }

        /// <summary>WeaponInstance.RollOneEffect 미러 — 효과 등급 롤 후 풀에서 가중 추첨, 타입 중복 배제</summary>
        private SimEffect RollOneEffect(Grade weaponGrade, List<SimEffect> existing)
        {
            float[] prob = b.weaponCfg.GetEffectGradeProbabilities(weaponGrade);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var pool = b.EffectsOfGrade(RollEffectGrade(prob));
                if (pool.Count == 0) continue;

                var candidates = pool
                    .Where(d => !existing.Any(e => e.data.effectType == d.effectType))
                    .ToList();
                if (candidates.Count == 0) continue;

                float total = candidates.Sum(d => d.weight);
                float r = (float)rng.NextDouble() * total;
                float acc = 0f;
                foreach (var d in candidates)
                {
                    acc += d.weight;
                    if (r < acc) return NewEffect(d);
                }
            }
            return null;
        }

        private Grade RollEffectGrade(float[] prob)
        {
            float r = (float)rng.NextDouble();
            float acc = 0f;
            for (int i = 0; i < prob.Length; i++)
            {
                acc += prob[i];
                if (r < acc) return (Grade)i;
            }
            return Grade.Common;
        }

        /// <summary>
        /// AdventureManager.GetEffectStatScore 미러 — StatBonus / WeaponTypeMatchBonus / AllStatBonus 반영.
        /// weapon이 null(기본 무기)이면 효과가 없으므로 원시 스탯 점수.
        /// </summary>
        private static float EffectStatScore(SimVisitor v, SimWeapon weapon, WeaponType type)
        {
            if (weapon == null || weapon.effects.Count == 0)
                return TypeAdvantage.GetStatScore(v.stats[0], v.stats[1], v.stats[2], v.stats[3], type);

            var statBonus = new int[4];
            float matchMultiplier = 1f;
            float allStatBonus = 0f;

            foreach (var e in weapon.effects)
            {
                switch (e.data.effectType)
                {
                    case WeaponEffectType.StatBonus:
                        statBonus[e.data.targetStat] += Mathf.RoundToInt(e.value);
                        break;
                    case WeaponEffectType.WeaponTypeMatchBonus:
                        matchMultiplier += e.value;
                        break;
                    case WeaponEffectType.AllStatBonus:
                        allStatBonus += Mathf.RoundToInt(e.value);
                        break;
                }
            }

            float score = TypeAdvantage.GetStatScore(
                v.stats[0] + statBonus[0], v.stats[1] + statBonus[1],
                v.stats[2] + statBonus[2], v.stats[3] + statBonus[3], type) * matchMultiplier;
            return score + allStatBonus;
        }

        /// <summary>AdventureManager.CalculateConditionBonus 미러 — 던전 등급 / 방어구 타입 조건부 가산</summary>
        private static float ConditionBonus(SimWeapon weapon, DungeonData dungeon, ArmorType armor)
        {
            if (weapon == null) return 0f;

            float bonus = 0f;
            foreach (var e in weapon.effects)
            {
                if (e.data.effectType == WeaponEffectType.DungeonGradeBonus && (int)dungeon.grade == e.data.targetGrade)
                    bonus += e.value;
                else if (e.data.effectType == WeaponEffectType.ArmorTypeBonus && (int)armor == e.data.targetArmorType)
                    bonus += e.value;
            }
            return bonus;
        }

        private SimEffect NewEffect(WeaponEffectData d)
        {
            float rolled = Mathf.Lerp(d.baseValueRange.x, d.baseValueRange.y, (float)rng.NextDouble());
            var e = new SimEffect
            {
                data  = d,
                value = WeaponEffect.IsIntegerType(d.effectType) ? Mathf.RoundToInt(rolled) : rolled,
            };
            if (e.value > e.MaxValue) e.value = e.MaxValue;
            return e;
        }

        #endregion

        private void TrackFirstGrade(Grade g)
        {
            int gi = (int)g;
            if (result.firstGradeWeek[gi] == 0) result.firstGradeWeek[gi] = week;
        }

        /// <summary>강화 골드 비용 — BlacksmithManager 미러 + 주차 계단 + 유산 비용 배율</summary>
        public int EnforceGoldCost(SimWeapon w) =>
            Mathf.RoundToInt(Mathf.RoundToInt(b.blacksmith.enforceBaseGoldByGrade[(int)w.grade] * (w.enforce + 1)
                                              * PriceMult(p => p.blacksmithCost)) * lgEnforceCostMult);

        /// <summary>강화 성공 확률(0~1) — TryEnforce의 판정식과 동일 (유산 + 강화석, 100% 캡)</summary>
        public float EnforceSuccessRate(SimWeapon w, ActiveItemData stone = null)
        {
            float forge = stone != null && stone.itemType == ActiveItemType.ForgeStone ? stone.effectValue : 0f;
            float baseRate = b.blacksmith.enforceSuccessRates[Math.Min(w.enforce, b.blacksmith.enforceSuccessRates.Length - 1)];
            return Mathf.Min(100f, baseRate * (1f + lgEnforceRateBonus + forge)) / 100f;
        }

        /// <summary>
        /// 강화 1회 성공까지의 기대 골드 — 봇의 여유 판단용.
        /// 비용 배수 가드(gold &lt; cost x N)는 성공률이 바뀌면 강도가 같이 바뀌므로 기대비용을 쓴다.
        /// </summary>
        public int EnforceExpectedGold(SimWeapon w, ActiveItemData stone = null)
        {
            float r = EnforceSuccessRate(w, stone);
            return r <= 0f ? int.MaxValue : Mathf.RoundToInt(EnforceGoldCost(w) / r);
        }

        /// <summary>강화 시도 — BlacksmithManager 미러 (대장장이 타입 혜택 제외, 유산 성공률/비용 + 강화석 반영. 재료는 쓰지 않는다)</summary>
        public bool TryEnforce(SimWeapon w, ActiveItemData forgeStone = null)
        {
            if (w.enforce >= w.MaxEnforce) return false;
            int cost = EnforceGoldCost(w);
            if (gold < cost) return false;
            gold -= cost;
            cur.spent += cost;
            cur.spentSmith += cost;
            cur.enforceCount++;
            // 성공률 x (1 + 유산 + 강화석), 100% 캡 — BlacksmithManager.GetSuccessRate 미러 (강화석은 시도 시 소비)
            float forge = forgeStone != null && forgeStone.itemType == ActiveItemType.ForgeStone && ConsumeItem(forgeStone)
                ? forgeStone.effectValue : 0f;
            float baseRate = b.blacksmith.enforceSuccessRates[Math.Min(w.enforce, b.blacksmith.enforceSuccessRates.Length - 1)];
            if (Roll(Mathf.Min(100f, baseRate * (1f + lgEnforceRateBonus + forge)) / 100f))
            {
                // WeaponInstance.Enforce 미러 — 미완성 효과 하나를 최대치로 올린다
                var candidates = w.effects.Where(e => e.value < e.MaxValue).ToList();
                if (candidates.Count == 0) candidates = w.effects;
                if (candidates.Count == 0) return false;
                var target = candidates[rng.Next(candidates.Count)];
                target.value = target.MaxValue;

                w.enforce++;
                QuestUpdate(QuestType.EnforceSuccess);
                return true;
            }
            return false;
        }

        /// <summary>진화 골드 비용 — BlacksmithManager 미러 + 주차 계단 + 유산 비용 배율</summary>
        public int EvolveGoldCost(SimWeapon w) =>
            Mathf.RoundToInt(Mathf.RoundToInt(b.blacksmith.evolveCostByGrade[(int)w.grade]
                                              * PriceMult(p => p.blacksmithCost)) * lgEvolveCostMult);

        /// <summary>진화 재료 소요 — 주력(현재 등급) + 상위(다음 등급). BlacksmithManager.GetEvolveMaterials 미러</summary>
        public (int main, int next) EvolveMatNeed() =>
            (ReducedMatCount(b.blacksmith.evolveMainMaterialCount),
             ReducedMatCount(b.blacksmith.evolveNextMaterialCount));

        /// <summary>진화 재료 보유 여부 — Legendary는 진화 불가라 false</summary>
        public bool HasEvolveMats(SimWeapon w)
        {
            if (w.grade >= Grade.Legendary) return false;
            int gi = (int)w.grade;
            var (main, next) = EvolveMatNeed();
            return mats[gi] >= main && mats[gi + 1] >= next;
        }

        /// <summary>진화 시도 — 풀강화 선행, Legendary 불가 (유산 성공률/비용/재료 + 강화석 반영. 재료는 실패해도 소모)</summary>
        public bool TryEvolve(SimWeapon w, ActiveItemData forgeStone = null)
        {
            if (w.enforce < w.MaxEnforce || w.grade >= Grade.Legendary) return false;
            int gi = (int)w.grade;
            int cost = EvolveGoldCost(w);
            var (matMain, matNext) = EvolveMatNeed();
            if (gold < cost || mats[gi] < matMain || mats[gi + 1] < matNext) return false;
            gold -= cost;
            cur.spent += cost;
            cur.spentSmith += cost;
            cur.evolveCount++;
            // 재료 차감은 판정 전 — 실패해도 소모한다
            mats[gi] -= matMain;
            mats[gi + 1] -= matNext;
            float forge = forgeStone != null && forgeStone.itemType == ActiveItemType.ForgeStone && ConsumeItem(forgeStone)
                ? forgeStone.effectValue : 0f;
            float baseRate = b.blacksmith.evolveSuccessRates[Math.Min(gi, b.blacksmith.evolveSuccessRates.Length - 1)];
            if (Roll(Mathf.Min(100f, baseRate * (1f + lgEvolveRateBonus + forge)) / 100f))
            {
                // WeaponInstance.Evolve 미러 — 등급 상승 + 효과 1개 추가.
                // BlacksmithManager.ApplyEvolveUpgrades의 기존 효과 등급 승급은 미반영.
                w.grade = w.grade + 1;
                w.enforce = 0;
                w.rerollCount = 0;   // WeaponInstance.Evolve 미러 — 진화하면 재부여 횟수가 리셋된다
                var added = RollOneEffect(w.grade, w.effects);
                if (added != null) w.effects.Add(added);
                TrackFirstGrade(w.grade);
                QuestUpdate(QuestType.EvolveSuccess);
                return true;
            }
            return false;
        }

        /// <summary>수색꾼 파견 (09:00 일괄). 완료 시각이 21시를 넘으면 하루 끝에 환불.</summary>
        /// <summary>파견 비용 — ScoutManager.CalcScoutCost 미러</summary>
        public int ScoutCost(DungeonData dungeon)
        {
            var ins = b.insight;
            int gi = Mathf.Clamp((int)dungeon.grade, 0, ins.scoutGradeMultipliers.Length - 1);
            return Mathf.RoundToInt(ins.scoutBaseCost * ins.scoutGradeMultipliers[gi]);
        }

        public void DispatchScout(BoardEntry entry)
        {
            var ins = b.insight;
            int gi = (int)entry.dungeon.grade;
            int cost = ScoutCost(entry.dungeon);
            if (gold < cost) return;
            gold -= cost;
            cur.spent += cost;
            cur.spentScout += cost;
            cur.scoutCount++;
            int baseDur = insightScore >= ins.scoutDurationTier75Threshold ? ins.scoutBaseDurationAt75
                        : insightScore >= ins.scoutDurationTier50Threshold ? ins.scoutBaseDurationAt50
                        : insightScore >= ins.scoutDurationTier25Threshold ? ins.scoutBaseDurationAt25
                        : ins.scoutBaseDurationDefault;
            float dur = baseDur * ins.scoutGradeMultipliers[gi]
                      * Mathf.Lerp(ins.scoutRandomMin, ins.scoutRandomMax, (float)rng.NextDouble());
            scouts.Add(new ScoutJob { entry = entry, knownAt = DAY_START + dur, cost = cost });
        }

        #endregion

        #region 아침 이벤트 — MorningEventManager / VisitorManager.Npc 미러

        /// <summary>
        /// 아침 이벤트 NPC — CheckAndSpawnEventNPC(평판 등급별 등장 확률 + 유산 주간 보장) +
        /// SelectRandomEventType(가중 추첨) 미러. 1일차는 튜토리얼이라 미스폰.
        /// </summary>
        private void RunMorningEvent()
        {
            if (day == 1) return;

            if (!opt.forceMorningEvents)
            {
                // 유산 주간 보장 — day % 7 == 0이면 확정 스폰 (CheckAndSpawnEventNPC 미러)
                bool guaranteed = lgMorningGuarantee && day % 7 == 0;
                if (!guaranteed)
                {
                    float chance = RepLevel() switch
                    {
                        0 => b.reputation.bronzeEventChance,
                        1 => b.reputation.silverEventChance,
                        2 => b.reputation.goldEventChance,
                        3 => b.reputation.platinumEventChance,
                        _ => b.reputation.diamondEventChance,
                    };
                    if (!Roll(chance)) return;
                }
            }

            // 투자 결과 미수령 아침에는 투자자 제외 — hasPendingInvestment 미러 (회수는 이벤트 스폰 후)
            var type = RollMorningEventType(blockInvestor: investReturnDay == day);
            if (opt.forceMorningEvents) ForcedMorningEvent(type);
            else persona.OnMorningEvent(this, type);
        }

        private MorningEventType RollMorningEventType(bool blockInvestor)
        {
            float[] weights = b.morningEvent.eventWeights;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (blockInvestor && i == (int)MorningEventType.SuspiciousInvestor) continue;
                total += weights[i];
            }
            float roll = (float)rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (blockInvestor && i == (int)MorningEventType.SuspiciousInvestor) continue;
                acc += weights[i];
                if (roll < acc) return (MorningEventType)i;
            }
            return MorningEventType.WeaponEnhance;
        }

        /// <summary>
        /// 강제 참여 정책 (진단용) — 페르소나와 무관하게 유보금(이번 주 벌금)을 남기는 한 전부 참여한다.
        /// "이득일 때만" 봇의 지출 0이 봇 결함인지 콘텐츠 사망인지 구분하는 A/B용 (로드맵 3부 원칙 1·5).
        /// </summary>
        private void ForcedMorningEvent(MorningEventType type)
        {
            int reserve = WeeklyFine();
            switch (type)
            {
                case MorningEventType.WeaponEnhance:
                {
                    var t = weapons.Where(x => !x.busy)
                                   .OrderByDescending(x => x.grade).ThenByDescending(x => x.enforce).FirstOrDefault();
                    if (t != null) MorningWeaponEnhance(t);
                    break;
                }
                case MorningEventType.WeaponExchange:
                {
                    var spares = SpareWeapons().OrderBy(x => x.grade).ThenBy(x => x.enforce).ToList();
                    if (spares.Count >= 2) MorningWeaponExchange(spares[0], spares[1]);
                    break;
                }
                case MorningEventType.SuspiciousInvestor:
                {
                    int amount = b.morningEvent.investMinGold;
                    if (gold - amount >= reserve) MorningInvest(amount);
                    break;
                }
                case MorningEventType.WanderingBlacksmith:
                {
                    var t = weapons.Where(x => !x.busy)
                                   .OrderByDescending(x => x.enforce < x.MaxEnforce)
                                   .ThenByDescending(x => x.grade).FirstOrDefault();
                    if (t != null) MorningWanderingBlacksmith(t);
                    break;
                }
                case MorningEventType.GuildEnvoy:
                    MorningGuildEnvoy();
                    break;
                case MorningEventType.MysteryBox:
                {
                    int tier = RollBoxTier();
                    if (gold - BoxCost(tier) >= reserve) BuyMysteryBox(tier);
                    break;
                }
                case MorningEventType.RefugeeHelp:
                    MorningRefugee(donate: gold - RefugeeCost() >= reserve);
                    break;
                case MorningEventType.Collector:
                {
                    var sell = SpareWeapons()
                        .Where(x => (int)x.grade >= (int)b.morningEvent.collectorMinGrade)
                        .OrderByDescending(x => x.data.basePrice).FirstOrDefault();
                    if (sell != null) MorningCollectorSell(sell);
                    break;
                }
                case MorningEventType.BlackMarket:
                {
                    var (data, price) = RollBlackMarketOffer();
                    if (data != null && CanAddWeapon() && gold - price >= reserve) BuyBlackMarket(data, price);
                    break;
                }
            }
        }

        private void MorningSpend(int amount)
        {
            gold -= amount;
            cur.spent += amount;
            cur.spentMorning += amount;
        }

        private void MorningGain(int amount)
        {
            gold += amount;
            cur.income += amount;
            cur.incomeMorning += amount;
        }

        /// <summary>1. 랜덤 무기 강화 — ExecuteWeaponEnhance 미러 (등급 -1~+3, 무료)</summary>
        public void MorningWeaponEnhance(SimWeapon w)
        {
            cur.morningEventCount++;
            var m = b.morningEvent;
            double roll = rng.NextDouble();
            float minus1 = m.enhanceMinus1Chance;
            float zero   = minus1 + m.enhanceZeroChance;
            float plus1  = zero + m.enhancePlus1Chance;
            float plus2  = plus1 + m.enhancePlus2Chance;
            int delta = roll < minus1 ? -1 : roll < zero ? 0 : roll < plus1 ? 1 : roll < plus2 ? 2 : 3;

            int newGrade = Mathf.Clamp((int)w.grade + delta, 0, 4);
            int actual = newGrade - (int)w.grade;
            if (actual < 0)
            {
                // WeaponInstance.Downgrade 미러 — 등급 하락 + 마지막 효과 제거 + 강화 재계산
                for (int i = 0; i < -actual; i++)
                {
                    w.grade = w.grade - 1;
                    if (w.effects.Count > 0) w.effects.RemoveAt(w.effects.Count - 1);
                }
                w.enforce = w.effects.Count(e => e.value >= e.MaxValue);   // CheckEnforceLevel 미러
            }
            else
            {
                // 상승 = 진화 미러 (TryEvolve 성공 블록과 동일: 등급+1, 효과+1, 강화 리셋)
                for (int i = 0; i < actual; i++)
                {
                    w.grade = w.grade + 1;
                    w.enforce = 0;
                    var added = RollOneEffect(w.grade, w.effects);
                    if (added != null) w.effects.Add(added);
                    TrackFirstGrade(w.grade);
                }
            }
        }

        /// <summary>2. 교환 상인 — ExecuteWeaponExchange 미러 (무기 2자루 소비, 등급합 테이블 롤)</summary>
        public void MorningWeaponExchange(SimWeapon a, SimWeapon second)
        {
            cur.morningEventCount++;
            int sum = (int)a.grade + (int)second.grade;
            float[] table = b.morningEvent.GetExchangeTable(sum);
            double roll = rng.NextDouble();
            float acc = 0f;
            Grade result = (Grade)(table.Length - 1);
            for (int i = 0; i < table.Length; i++)
            {
                acc += table[i];
                if (roll < acc) { result = (Grade)i; break; }
            }
            weapons.Remove(a);
            weapons.Remove(second);
            GrantWeapon(RandomWeaponOfGrade(result), 0);
        }

        /// <summary>3. 수상한 투자자 — ExecuteInvestment 미러 (결과 즉시 확정, 회수는 다음날 아침)</summary>
        public void MorningInvest(int amount)
        {
            cur.morningEventCount++;
            var m = b.morningEvent;
            MorningSpend(amount);

            double roll = rng.NextDouble();
            float lose    = m.investLoseChance;
            float success = lose + m.investSuccessChance;
            float big     = success + m.investBigChance;
            float multi = roll < lose ? 0f
                        : roll < success ? m.investSuccessMulti
                        : roll < big ? m.investBigMulti
                        : m.investJackpotMulti;

            investReturnDay = day + 1;
            investReturnGold = Mathf.RoundToInt(amount * multi);
        }

        /// <summary>투자 기대 배율 — 봇 정책용 (Config에서 계산: 현재 값이면 0.98)</summary>
        public float InvestEV()
        {
            var m = b.morningEvent;
            float jackpot = Mathf.Max(0f, 1f - m.investLoseChance - m.investSuccessChance - m.investBigChance);
            return m.investSuccessChance * m.investSuccessMulti
                 + m.investBigChance * m.investBigMulti
                 + jackpot * m.investJackpotMulti;
        }

        /// <summary>4. 떠돌이 대장장이 — ExecuteWanderingBlacksmithAuto 미러 (50% 무료 강화 / 50% 리롤)</summary>
        public void MorningWanderingBlacksmith(SimWeapon w)
        {
            cur.morningEventCount++;
            var m = b.morningEvent;
            bool doEnhance = w.enforce < w.MaxEnforce && rng.NextDouble() < 0.5;
            if (doEnhance)
            {
                double roll = rng.NextDouble();
                float p1 = m.blacksmithEnhancePlus1;
                float p2 = p1 + m.blacksmithEnhancePlus2;
                int plus = roll < p1 ? 1 : roll < p2 ? 2 : 3;
                for (int i = 0; i < plus && w.enforce < w.MaxEnforce; i++)
                {
                    // WeaponInstance.Enforce 미러 — 미완성 효과 하나를 최대치로 (TryEnforce 성공 블록과 동일)
                    var candidates = w.effects.Where(e => e.value < e.MaxValue).ToList();
                    if (candidates.Count == 0) candidates = w.effects;
                    if (candidates.Count == 0) break;
                    var target = candidates[rng.Next(candidates.Count)];
                    target.value = target.MaxValue;
                    w.enforce++;
                }
            }
            else
            {
                // WeaponInstance.Reroll(force) 근사 — 효과 개수 유지, 전부 재추첨
                int n = w.effects.Count;
                w.effects.Clear();
                for (int i = 0; i < n; i++)
                {
                    var e = RollOneEffect(w.grade, w.effects);
                    if (e != null) w.effects.Add(e);
                }
                w.enforce = w.effects.Count(e => e.value >= e.MaxValue);
            }
        }

        /// <summary>5. 길드 사절단 — ExecuteGuildEnvoy 미러 (선물 = day x 배율 + 재료 / 강제납부 = day x 배율)</summary>
        public void MorningGuildEnvoy()
        {
            cur.morningEventCount++;
            var m = b.morningEvent;
            if (Roll(m.guildGiftChance[RepLevel()]))
            {
                double roll = rng.NextDouble();
                int amount = roll < m.guildGoldLowChance ? m.guildGoldLow
                           : roll < m.guildGoldLowChance + m.guildGoldMidChance ? m.guildGoldMid
                           : m.guildGoldHigh;
                MorningGain(amount);
                AddRewardMaterials(m.guildMaterialCount);
            }
            else
            {
                int required = rng.NextDouble() < m.guildForceLowChance ? m.guildForceLow : m.guildForceHigh;
                MorningSpend(Math.Min(required, gold));   // 골드 부족 시 전액 납부 미러
            }
        }

        /// <summary>6. 수수께끼 상자 등급 롤 — GetRandomMysteryBoxTier 미러 (0=일반 1=희귀 2=신비)</summary>
        public int RollBoxTier()
        {
            float[] wts = b.morningEvent.boxTierWeights;
            float total = wts.Sum();
            float roll = (float)rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < wts.Length; i++)
            {
                acc += wts[i];
                if (roll < acc) return i;
            }
            return 0;
        }

        /// <summary>상자 가격 — GetMysteryBoxCost 미러 (고정가)</summary>
        public int BoxCost(int tier)
        {
            var m = b.morningEvent;
            return tier == 2 ? m.boxMythicCost : tier == 1 ? m.boxRareCost : m.boxNormalCost;
        }

        /// <summary>
        /// 상자 기대값(G) — 상급자 "이득일 때만" 정책용. 실게임에 없는 계산이지만 로드맵 1-1의 EV 표와
        /// 같은 환산이다 (재료 = 구매가, 무기 = basePrice 평균, 골드 = day x 배율 평균).
        /// Config에서 계산하므로 수치를 조정하면 봇 행동이 자동으로 따라온다.
        /// </summary>
        public float BoxEV(int tier)
        {
            var m = b.morningEvent;
            float pMat, pGold, pWpn, gMin, gMax, wpnVal;
            switch (tier)
            {
                case 2:
                    (pMat, pGold, pWpn, gMin, gMax) = (m.boxMythicMaterial, m.boxMythicGold, m.boxMythicWeapon,
                                                       m.boxMythicGoldMin, m.boxMythicGoldMax);
                    wpnVal = AvgBasePrice(Grade.Legendary);   // 신비 상자 무기 = 전설 확정
                    break;
                case 1:
                    (pMat, pGold, pWpn, gMin, gMax) = (m.boxRareMaterial, m.boxRareGold, m.boxRareWeapon,
                                                       m.boxRareGoldMin, m.boxRareGoldMax);
                    wpnVal = (AvgBasePrice(Grade.Rare) + AvgBasePrice(Grade.Epic)) * 0.5f;
                    break;
                default:
                    (pMat, pGold, pWpn, gMin, gMax) = (m.boxNormalMaterial, m.boxNormalGold, m.boxNormalWeapon,
                                                       m.boxNormalGoldMin, m.boxNormalGoldMax);
                    wpnVal = AvgBasePrice(Grade.Uncommon);
                    break;
            }
            float matVal = AvgRewardMaterialValue() * (tier + 1);
            return pMat * matVal + pGold * (gMin + gMax) * 0.5f + pWpn * wpnVal;
        }

        /// <summary>상자 구매·즉시 개봉 — ExecuteMysteryBox + OpenBox* 미러</summary>
        public void BuyMysteryBox(int tier)
        {
            cur.morningEventCount++;
            cur.boxBuyCount++;
            int cost = BoxCost(tier);
            cur.spentMorningBox += cost;
            MorningSpend(cost);

            var m = b.morningEvent;
            (float pMat, float pGold, float pWpn, int gMin, int gMax) = tier switch
            {
                2 => (m.boxMythicMaterial, m.boxMythicGold, m.boxMythicWeapon, m.boxMythicGoldMin, m.boxMythicGoldMax),
                1 => (m.boxRareMaterial, m.boxRareGold, m.boxRareWeapon, m.boxRareGoldMin, m.boxRareGoldMax),
                _ => (m.boxNormalMaterial, m.boxNormalGold, m.boxNormalWeapon, m.boxNormalGoldMin, m.boxNormalGoldMax),
            };

            double roll = rng.NextDouble();
            if (roll < pMat)
            {
                AddRewardMaterials(tier + 1);   // 재료 1/2/3개
                return;
            }
            if (roll < pMat + pGold)
            {
                MorningGain(rng.Next(gMin, gMax + 1));
                return;
            }
            if (roll < pMat + pGold + pWpn)
            {
                Grade g = tier == 2 ? Grade.Legendary   // 신비 상자 무기 = 전설 확정
                        : tier == 1 ? (rng.NextDouble() < 0.5 ? Grade.Rare : Grade.Epic)
                        : Grade.Uncommon;
                if (CanAddWeapon()) GrantWeapon(RandomWeaponOfGrade(g), 0);
                else MorningGain(rng.Next(gMin, gMax + 1));   // FallbackWeaponToGold 미러
                return;
            }
            // 꽝
        }

        /// <summary>난민 기부 비용 — GetRefugeeCost 미러 (고정가)</summary>
        public int RefugeeCost() => b.morningEvent.refugeeCost;

        /// <summary>7. 난민 돕기 — ExecuteRefugeeHelp 미러. 거절(또는 골드 부족)은 60% 확률 평판 감소</summary>
        public void MorningRefugee(bool donate)
        {
            cur.morningEventCount++;
            var m = b.morningEvent;
            int cost = RefugeeCost();
            if (donate && gold >= cost)
            {
                MorningSpend(cost);
                bool high = rng.NextDouble() >= m.refugeeDonateRepLowChance;
                AddRep(high ? m.refugeeDonateRepHigh : m.refugeeDonateRepLow);
            }
            else
            {
                if (Roll(m.refugeeRejectRepPenaltyChance)) AddRep(-m.refugeeRejectRepPenalty);
            }
        }

        /// <summary>8. 수집가 — ExecuteCollectorSell 미러. 판매가 = weaponData.basePrice(현재 등급 아님) x 3/4/5</summary>
        public void MorningCollectorSell(SimWeapon w)
        {
            cur.morningEventCount++;
            var m = b.morningEvent;
            double roll = rng.NextDouble();
            float mult = roll < m.collectorMult3Chance ? 3f
                       : roll < m.collectorMult3Chance + m.collectorMult4Chance ? 4f : 5f;
            weapons.Remove(w);
            MorningGain(Mathf.RoundToInt(w.data.basePrice * mult));
        }

        /// <summary>9. 암시장 제안 — GetBlackMarketOffer 미러 (Rare+ 전 무기에서 랜덤, 반값)</summary>
        public (WeaponData data, int price) RollBlackMarketOffer()
        {
            var m = b.morningEvent;
            var candidates = b.weapons.Where(x => (int)x.baseGrade >= (int)m.blackMarketMinGrade).ToList();
            if (candidates.Count == 0) return (null, 0);
            var picked = candidates[rng.Next(candidates.Count)];
            return (picked, Mathf.RoundToInt(picked.basePrice * m.blackMarketDiscount));
        }

        /// <summary>암시장 구매 — ExecuteBlackMarketBuy 미러 (평판 페널티)</summary>
        public void BuyBlackMarket(WeaponData data, int price)
        {
            cur.morningEventCount++;
            MorningSpend(price);
            GrantWeapon(data, price);
            AddRep(-b.morningEvent.blackMarketRepPenalty);
        }

        /// <summary>상자/사절단 재료 보상 — rewardMaterialPool 추첨. 시뮬은 진화 재료만 쓰므로 그 외 ID는 무시</summary>
        private void AddRewardMaterials(int count)
        {
            var pool = b.morningEvent.rewardMaterialPool;
            if (pool == null || pool.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                int gi = Array.IndexOf(SimBundle.EnforceMatIDs, pool[rng.Next(pool.Length)]);
                if (gi >= 0) mats[gi]++;
            }
        }

        /// <summary>보상 풀 재료의 평균 가치(구매가) — 상자 EV 환산용</summary>
        public float AvgRewardMaterialValue()
        {
            var pool = b.morningEvent.rewardMaterialPool;
            if (pool == null || pool.Length == 0) return 0f;
            float sum = 0f;
            foreach (var id in pool) sum += b.MaterialValue(id);
            return sum / pool.Length;
        }

        /// <summary>등급 기본가 평균 — 상자 EV 환산용</summary>
        public float AvgBasePrice(Grade g)
        {
            var list = b.weapons.Where(x => x.baseGrade == g).ToList();
            return list.Count == 0 ? 0f : (float)list.Average(x => x.basePrice);
        }

        /// <summary>
        /// 타입별 최고 WeaponTypeCap자루(대여 커버리지)를 제외한 유휴 여분 무기 — 수집가 판매/교환용.
        /// 유지 수가 페르소나 구매 정책과 어긋나면 그 주에 산 무기를 곧바로 팔아버린다.
        /// </summary>
        public List<SimWeapon> SpareWeapons()
        {
            int keepPerType = persona.WeaponTypeCap(this);
            var keep = new HashSet<SimWeapon>(
                weapons.GroupBy(x => x.Type)
                       .SelectMany(g => g.OrderByDescending(x => x.grade).ThenByDescending(x => x.enforce)
                                         .Take(keepPerType)));
            return weapons.Where(x => !keep.Contains(x) && !x.busy).ToList();
        }

        #endregion

        #region 통찰 / 대화 테스트

        /// <summary>개별 스탯/힌트 대화 소요 시간 — InsightManager.GetStatTalkTimeCost 미러</summary>
        public int TalkTimeCost()
        {
            var ins = b.insight;
            if (insightScore >= ins.normalRevealHighestStatThreshold) return ins.statTalkCostAt70;
            if (insightScore >= ins.normalReveal2StatThreshold) return ins.statTalkCostAt50;
            if (insightScore >= ins.normalRevealAverageThreshold) return ins.statTalkCostAt30;
            return ins.statTalkCostDefault;
        }

        /// <summary>대화 가능 검사 + 실행 시 시간 전진 — CanStartTalkAction 미러</summary>
        public bool TrySpendTalk(ref float cursor)
        {
            int cost = TalkTimeCost();
            if (cost >= DAY_END - cursor || cost > b.insight.maxTalkDurationMinutes) return false;
            cursor += cost;
            cur.talkCount++;
            return true;
        }

        /// <summary>
        /// 통찰 임계값 이상이면 일반 모험가의 최고 스탯 종류·수치가 무료로 공개된다.
        /// InsightManager.CanRevealNormalAdventurerHighestStat 미러 — 대화 없이 계열 무기를 고를 수 있다.
        /// </summary>
        public bool KnowsHighestStat => insightScore >= b.insight.normalRevealHighestStatThreshold;

        /// <summary>
        /// 첫 스탯 공개 성공 확률 — GetStatRevealSuccessRate 미러 (공개 0개 기준).
        /// INT 공개 시 붙는 성공률 보정은 미모델 — 시뮬은 어떤 스탯이 공개됐는지 추적하지 않는다.
        /// </summary>
        public float StatRevealChance()
        {
            var ins = b.insight;
            return Mathf.Clamp01((ins.statRevealBaseChance + insightScore * ins.statRevealInsightBonus) / 100f);
        }

        public void AddInsight(int amount) => insightScore = Mathf.Clamp(insightScore + amount, 0, 100);

        private void AddRep(int amount)
        {
            if (amount > 0) totalPosRep += amount;   // ReputationManager.totalCumulativeReputation 미러
            int oldLv = RepLevel();
            rep = Math.Max(0, rep + amount);         // 0 하한 — ReputationManager.AddReputation 미러
            int newLv = RepLevel();
            for (int lv = oldLv + 1; lv <= newLv; lv++)
            {
                if (lv < b.insight.reputationLevelUpInsightReward.Length)
                    AddInsight(b.insight.reputationLevelUpInsightReward[lv]);
                if (result.firstRepLevelWeek[lv] == 0) result.firstRepLevelWeek[lv] = week;
            }
        }

        public int RepLevel()
        {
            if (rep >= b.reputation.diamondThreshold) return 4;
            if (rep >= b.reputation.platinumThreshold) return 3;
            if (rep >= b.reputation.goldThreshold) return 2;
            if (rep >= b.reputation.silverThreshold) return 1;
            return 0;
        }

        #endregion

        #region 방문자 / 스폰

        private float NextSpawnInterval()
        {
            Vector2 range = RepLevel() switch
            {
                0 => b.reputation.bronzeSpawnInterval,
                1 => b.reputation.silverSpawnInterval,
                2 => b.reputation.goldSpawnInterval,
                3 => b.reputation.platinumSpawnInterval,
                _ => b.reputation.diamondSpawnInterval,
            };
            float min = Math.Max(range.x, b.visitor.adventurerSpawnMinInterval);
            float max = Math.Min(range.y, b.visitor.adventurerSpawnMaxInterval);
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        private SimVisitor GenerateVisitor(float arrival)
        {
            // 네임드 우선 판정 — 뽑히면 영속 인스턴스의 확정 스탯/특성을 그대로 쓴다 (단계 5-2)
            var named = PickNamedVisitor();
            if (named != null)
            {
                named.lastVisitDay = day;
                return new SimVisitor
                {
                    arrival = arrival,
                    stats = named.stats,
                    trueBest = named.trueBest,
                    observedDefaultType = named.observedDefaultType,
                    bestStatIndex = named.bestStatIndex,
                    trait = named.trait,
                    named = named,
                };
            }

            var v = new SimVisitor { arrival = arrival };

            // CreateNormalAdventurerInstance 미러 — SO를 하나 뽑아 그 range로 롤한다.
            // 일반 모험가 SO는 성별로 STR/LUK <-> DEX/INT 범위가 반전돼 있어(0~70 / 30~100)
            // VisitorConfig의 min/max를 쓰면 분포가 실제와 달라진다. SO range가 전부 zero일 때만 폴백.
            var data = b.normalAdventurers.Count > 0
                ? b.normalAdventurers[rng.Next(b.normalAdventurers.Count)]
                : null;
            bool hasStatRange = data != null
                && data.strRange != Vector2Int.zero && data.dexRange != Vector2Int.zero
                && data.intRange != Vector2Int.zero && data.lukRange != Vector2Int.zero;
            if (hasStatRange)
            {
                v.stats[0] = rng.Next(data.strRange.x, data.strRange.y + 1);
                v.stats[1] = rng.Next(data.dexRange.x, data.dexRange.y + 1);
                v.stats[2] = rng.Next(data.intRange.x, data.intRange.y + 1);
                v.stats[3] = rng.Next(data.lukRange.x, data.lukRange.y + 1);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    v.stats[i] = rng.Next(b.visitor.normalAdventurerStatMin, b.visitor.normalAdventurerStatMax + 1);
            }

            // best / second best 타입
            float best = -1f, second = -1f;
            WeaponType bestT = WeaponType.Sword, secondT = WeaponType.Sword;
            for (int t = 0; t < 8; t++)
            {
                float s = TypeAdvantage.GetStatScore(v.stats[0], v.stats[1], v.stats[2], v.stats[3], (WeaponType)t);
                if (s > best) { second = best; secondT = bestT; best = s; bestT = (WeaponType)t; }
                else if (s > second) { second = s; secondT = (WeaponType)t; }
            }
            v.trueBest = bestT;

            int bi = 0;
            for (int i = 1; i < 4; i++)
                if (v.stats[i] > v.stats[bi]) bi = i;
            v.bestStatIndex = bi;

            // 기본 무기 타입 — CreateDefaultWeaponFor 미러 (70/20/10)
            double r = rng.NextDouble();
            v.observedDefaultType = r < 0.70 ? bestT : r < 0.90 ? secondT : (WeaponType)rng.Next(8);

            // 특성 — AdventurerInstance 생성자 미러 (16종 균등). OFF여도 롤은 유지해 traitRng 스트림을 고정
            v.trait = (TraitType)traitRng.Next(RunResult.TraitN);
            return v;
        }

        #endregion

        #region 주간 퀘스트 — QuestManager 미러

        private void IssueQuest(int startDay)
        {
            if (opt.endlessFixedWeek > 0 && week > b.CampaignWeeks)
                quest = b.QuestForWeek(opt.endlessFixedWeek);   // 엔드리스 스윕(측정 전용) — 지정 템플릿만 반복
            else if (IsEndlessWeek(week))
                quest = DrawEndlessQuest();                     // QuestManager.DrawEndlessQuest 미러
            else
                quest = b.QuestForWeek(week);
            questProgress = quest != null ? new int[quest.requirements.Count] : new int[0];
            questDeadline = startDay + 6;   // WeeklyQuestInstance: 시작일 포함 7일
        }

        public WeeklyQuestData CurrentQuest => quest;
        public int[] QuestProgress => questProgress;

        #region 엔드리스 구간 미러 (QuestManager)

        /// <summary>추첨된 템플릿 이력 — 직전 N개 중복 회피용 (GameData.recentEndlessQuestIDs 미러)</summary>
        private readonly List<string> recentEndless = new List<string>();

        private bool IsEndlessWeek(int w) => b.endless != null && w > b.endless.campaignLastWeek;

        /// <summary>QuestManager.DrawEndlessQuest 미러. 전용 rng를 써서 엔드리스 미적용 런과 스트림을 분리한다.</summary>
        private WeeklyQuestData DrawEndlessQuest()
        {
            var cfg = b.endless;
            var pool = b.quests.Where(q => q.weekNumber > cfg.campaignLastWeek).ToList();
            if (pool.Count == 0) return b.QuestForWeek(week);

            var candidates = pool.Where(q => !recentEndless.Contains(q.StaticID)).ToList();
            if (candidates.Count == 0) candidates = pool;

            if (cfg.blockConsecutiveExtreme && recentEndless.Count > 0)
            {
                var last = pool.FirstOrDefault(q => q.StaticID == recentEndless[recentEndless.Count - 1]);
                if (last != null && last.difficulty == QuestDifficulty.Extreme)
                {
                    var nonExtreme = candidates.Where(q => q.difficulty != QuestDifficulty.Extreme).ToList();
                    if (nonExtreme.Count > 0) candidates = nonExtreme;
                }
            }

            var present = candidates.Select(q => q.difficulty).Distinct().ToList();
            float total = present.Sum(cfg.WeightOf);
            QuestDifficulty pick = present[0];
            if (total > 0f)
            {
                float roll = (float)questRng.NextDouble() * total;
                foreach (var d in present)
                {
                    roll -= cfg.WeightOf(d);
                    if (roll <= 0f) { pick = d; break; }
                }
            }

            var tier = candidates.Where(q => q.difficulty == pick).ToList();
            if (tier.Count == 0) tier = candidates;
            var chosen = tier[questRng.Next(tier.Count)];

            recentEndless.Add(chosen.StaticID);
            while (recentEndless.Count > Mathf.Max(0, cfg.noRepeatWindow))
                recentEndless.RemoveAt(0);

            return chosen;
        }

        /// <summary>QuestManager.CalculateWeeklyFine 미러 — 엔드리스는 SO 고정값 대신 주차 곡선</summary>
        private int WeeklyFine() =>
            IsEndlessWeek(week) ? b.endless.FineForWeek(week) : (quest?.weeklyFine ?? 0);

        #endregion

        private bool QuestComplete()
        {
            if (quest == null) return true;
            for (int i = 0; i < quest.requirements.Count; i++)
                if (questProgress[i] < quest.requirements[i].targetCount) return false;
            return true;
        }

        private void QuestUpdate(QuestType type, Grade? grade = null, WeaponType? weaponType = null,
                                 string dungeonID = null, int amount = 1)
        {
            if (quest == null) return;
            for (int i = 0; i < quest.requirements.Count; i++)
            {
                var req = quest.requirements[i];
                if (req.questType != type) continue;

                bool matches = type switch
                {
                    QuestType.SuccessfulAdventures => true,
                    QuestType.RentSpecificGrade => grade.HasValue && grade.Value >= req.minGrade,
                    QuestType.RentSpecificWeapon => weaponType.HasValue && weaponType.Value == req.specificWeaponType,
                    QuestType.CompleteSpecificDungeon => !string.IsNullOrEmpty(dungeonID) && dungeonID == req.specificDungeonID,
                    _ => true,   // 단순 카운트형
                };
                if (matches) questProgress[i] += amount;
            }
        }

        /// <summary>현재 퀘스트의 미달성 특정 던전 목표 ID 목록 (상급자 의뢰판 정책용)</summary>
        public List<string> UnmetQuestDungeonIDs()
        {
            var list = new List<string>();
            if (quest == null) return list;
            for (int i = 0; i < quest.requirements.Count; i++)
                if (quest.requirements[i].questType == QuestType.CompleteSpecificDungeon
                    && questProgress[i] < quest.requirements[i].targetCount)
                    list.Add(quest.requirements[i].specificDungeonID);
            return list;
        }

        #endregion

        public bool Roll(float chance) => rng.NextDouble() < chance;
    }
}
#endif
