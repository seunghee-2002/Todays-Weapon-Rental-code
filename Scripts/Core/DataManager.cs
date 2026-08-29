using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    public class DataManager : BaseManager<DataManager>
    {

        [Header("Static Data Arrays")]
        [SerializeField] private WeaponData[] weaponDataArray;
        [SerializeField] private AdventurerData[] adventurerDataArray;
        [SerializeField] private DungeonData[] dungeonDataArray;
        [SerializeField] private MaterialData[] materialDataArray;
        [SerializeField] private WeaponRecipeData[] weaponRecipeDataArray;
        [SerializeField] private ActiveItemData[] activeItemDataArray;
        [SerializeField] private ActiveItemRecipeData[] activeItemRecipeDataArray;
        [SerializeField] private WeeklyQuestData[] weeklyQuestDataArray;
        [SerializeField] private VisitorEventData[] visitorEventDataArray;
        [SerializeField] private DungeonEventData[] dungeonEventDataArray;
        [SerializeField] private WeaponEffectData[] weaponEffectDataArray;

        [SerializeField] private WeaponData[] defaultWeaponDataArray;
        [SerializeField] private DialogueData[] dialogueDataArray;


        // Static Data Dictionaries (staticID 기반)
        private Dictionary<string, WeaponData> weapons = new();
        private Dictionary<string, AdventurerData> adventurers = new();
        private Dictionary<string, DungeonData> dungeons = new();
        private Dictionary<string, MaterialData> materials = new();
        private Dictionary<string, WeaponRecipeData> weaponRecipes = new();
        private Dictionary<string, ActiveItemData> activeItems = new();
        private Dictionary<string, ActiveItemRecipeData> activeItemRecipes = new();
        private Dictionary<string, WeeklyQuestData> weeklyQuests = new();
        private Dictionary<string, VisitorEventData> visitorEvents = new();
        private Dictionary<string, DungeonEventData> dungeonEvents = new();
        private Dictionary<string, WeaponEffectData> weaponEffects = new();

        private Dictionary<WeaponType, WeaponData> defaultWeapons = new();
        private Dictionary<string, DialogueData> dialogues = new();

        // 역방향 인덱스 (materialID → 드롭 출처)
        private Dictionary<string, List<DungeonData>> dropSourcesByMaterial = new();
        private Dictionary<string, DungeonData> specialDropSourceByMaterial = new();

        public bool IsInitialized { get; private set; }

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            InitializeData();
        }

        private void InitializeData()
        {
            weapons.Clear();
            adventurers.Clear();
            dungeons.Clear();
            materials.Clear();
            weaponRecipes.Clear();
            activeItems.Clear();
            activeItemRecipes.Clear();
            weeklyQuests.Clear();
            visitorEvents.Clear();
            dungeonEvents.Clear();
            weaponEffects.Clear();
            defaultWeapons.Clear();
            dialogues.Clear();
            dropSourcesByMaterial.Clear();
            specialDropSourceByMaterial.Clear();
            
            ConvertToDictionary(weaponDataArray, weapons, "Weapons");
            ConvertToDictionary(adventurerDataArray, adventurers, "Adventurers");
            ConvertToDictionary(dungeonDataArray, dungeons, "Dungeons");
            ConvertToDictionary(materialDataArray, materials, "Materials");
            ConvertToDictionary(weaponRecipeDataArray, weaponRecipes, "WeaponRecipes");
            ConvertToDictionary(activeItemDataArray, activeItems, "ActiveItems");
            ConvertToDictionary(activeItemRecipeDataArray, activeItemRecipes, "ActiveItemRecipes");
            ConvertToDictionary(weeklyQuestDataArray, weeklyQuests, "WeeklyQuests");
            ConvertToDictionary(visitorEventDataArray, visitorEvents, "VisitorEvents");
            ConvertToDictionary(dungeonEventDataArray, dungeonEvents, "DungeonEvents");
            ConvertToDictionary(weaponEffectDataArray, weaponEffects, "WeaponEffects");

            if (defaultWeaponDataArray != null)
            {
                foreach (var data in defaultWeaponDataArray)
                {
                    if (data == null) continue;
                    if (defaultWeapons.ContainsKey(data.weaponType))
                        Log.Warn($"DataManager: DefaultWeapon 중복 타입 '{data.weaponType}'. 덮어씁니다.");
                    defaultWeapons[data.weaponType] = data;
                }
            }

            if (dialogueDataArray != null)
            {
                foreach (var data in dialogueDataArray)
                {
                    if (data == null || string.IsNullOrEmpty(data.StaticID)) continue;
                    if (dialogues.ContainsKey(data.StaticID))
                    {
                        Log.Warn($"DataManager: Duplicate StaticID '{data.StaticID}'. Skipping.");
                        continue;
                    }
                    dialogues[data.StaticID] = data;
                }
            }

            BuildMaterialDropIndex();

            IsInitialized = true;
        }

        private void BuildMaterialDropIndex()
        {
            foreach (var dungeon in dungeons.Values)
            {
                if (dungeon == null) continue;

                if (dungeon.dropMaterials != null)
                {
                    foreach (var material in dungeon.dropMaterials)
                    {
                        if (material == null || string.IsNullOrEmpty(material.StaticID)) continue;

                        if (!dropSourcesByMaterial.TryGetValue(material.StaticID, out var list))
                        {
                            list = new List<DungeonData>();
                            dropSourcesByMaterial[material.StaticID] = list;
                        }
                        if (!list.Contains(dungeon))
                            list.Add(dungeon);
                    }
                }

                if (dungeon.specialDropMaterial != null && !string.IsNullOrEmpty(dungeon.specialDropMaterial.StaticID))
                {
                    string specialID = dungeon.specialDropMaterial.StaticID;
                    if (specialDropSourceByMaterial.ContainsKey(specialID))
                        Log.Warn($"[DataManager] 특수 재료 '{specialID}' 드롭 던전 중복. '{dungeon.dungeonName}'으로 덮어씁니다.");
                    specialDropSourceByMaterial[specialID] = dungeon;
                }
            }
        }

        private void ConvertToDictionary<T>(T[] dataArray, Dictionary<string, T> dictionary, string typeName) where T : BaseData
        {
            if (dataArray == null || dataArray.Length == 0)
            {
                Log.Warn($"DataManager: {typeName} array is null or empty.");
                return;
            }

            foreach (T data in dataArray)
            {
                if (data == null)
                {
                    Log.Warn($"DataManager: Null entry in {typeName} array. Skipping.");
                    continue;
                }

                if (string.IsNullOrEmpty(data.StaticID))
                {
                    Log.Warn($"DataManager: {typeName} '{data.name}' has empty StaticID. Skipping.");
                    continue;
                }

                if (dictionary.ContainsKey(data.StaticID))
                {
                    Log.Warn($"DataManager: Duplicate StaticID '{data.StaticID}' in {typeName}. Overwriting with '{data.name}'.");
                }

                dictionary[data.StaticID] = data;
            }
        }

        #endregion

        #region 검색 메서드

        #region 대화 검색

        public DialogueData GetDialogue(string dialogueID)
        {
            if (dialogues.TryGetValue(dialogueID, out DialogueData data))
                return data;
            Log.Error($"DataManager: DialogueData '{dialogueID}' not found.");
            return null;
        }

        #endregion

        #region 무기 검색
        public WeaponData GetWeapon(string staticID)
        {
            if (weapons.TryGetValue(staticID, out WeaponData data))
                return data;
            
            Log.Error($"DataManager: WeaponData '{staticID}' not found.");
            return null;
        }

        public List<WeaponData> GetAllWeapons() => weapons.Values.ToList();
        
        public List<WeaponData> GetWeaponsByGrade(Grade grade)
            => weapons.Values.Where(w => w.baseGrade == grade).ToList();

        public WeaponData GetDefaultWeapon(WeaponType type)
        {
            if (defaultWeapons.TryGetValue(type, out WeaponData data))
                return data;

            Log.Error($"DataManager: DefaultWeapon '{type}' not found.");
            return null;
        }

        public List<WeaponData> GetAllDefaultWeapons() => defaultWeapons.Values.ToList();

        #endregion

        #region 모험가 검색

        public AdventurerData GetAdventurer(string staticID)
        {
            if (adventurers.TryGetValue(staticID, out AdventurerData data))
                return data;
            
            Log.Error($"DataManager: AdventurerData '{staticID}' not found.");
            return null;
        }

        public List<AdventurerData> GetAllAdventurers() => adventurers.Values.ToList();

        /// <summary>
        /// isNamed 여부로 모험가 데이터 필터링
        /// </summary>
        public List<AdventurerData> GetAdventurersByNamed(bool isNamed)
            => adventurers.Values.Where(a => a.isNamed == isNamed).ToList();

        #endregion

        #region 던전 검색

        public DungeonData GetDungeon(string staticID)
        {
            if (dungeons.TryGetValue(staticID, out DungeonData data))
                return data;
            
            Log.Error($"DataManager: DungeonData '{staticID}' not found.");
            return null;
        }

        public List<DungeonData> GetAllDungeons() => dungeons.Values.ToList();

        #endregion

        #region 재료 검색

        public MaterialData GetMaterial(string staticID)
        {
            if (materials.TryGetValue(staticID, out MaterialData data))
                return data;
            
            Log.Error($"DataManager: MaterialData '{staticID}' not found.");
            return null;
        }

        /// <summary>
        /// 특정 재료를 드롭하는 던전 목록 반환
        /// </summary>
        public List<DungeonData> GetDropSourcesForMaterial(string materialID)
        {
            if (dropSourcesByMaterial.TryGetValue(materialID, out var list))
                return new List<DungeonData>(list);

            return new List<DungeonData>();
        }

        /// <summary>
        /// 특수 재료를 드롭하는 던전 반환
        /// </summary>
        public DungeonData GetSpecialDropSourceForMaterial(string materialID)
        {
            if (specialDropSourceByMaterial.TryGetValue(materialID, out var dungeon))
            {
                Log.Info($"[DataManager] 특수 재료 {materialID} 드롭 던전: {dungeon.dungeonName}");
                return dungeon;
            }

            return null;
        }

        public List<MaterialData> GetAllMaterials() => materials.Values.ToList();

        #endregion

        #region 레시피 검색

        public List<WeaponRecipeData> GetAllWeaponRecipes() => weaponRecipes.Values.ToList();
        
        public List<ActiveItemRecipeData> GetAllActiveItemRecipes() => activeItemRecipes.Values.ToList();

        #endregion

        #region 액티브 아이템 검색

        public ActiveItemData GetActiveItem(string staticID)
        {
            if (activeItems.TryGetValue(staticID, out ActiveItemData data))
                return data;
            
            Log.Error($"DataManager: ActiveItemData '{staticID}' not found.");
            return null;
        }

        public List<ActiveItemData> GetAllActiveItems() => activeItems.Values.ToList();

        #endregion

        #region 퀘스트 검색

        public WeeklyQuestData GetQuestByWeek(int weekNumber)
        {
            var quest = weeklyQuests.Values.FirstOrDefault(q => q.weekNumber == weekNumber);
            
            if (quest == null)
                Log.Error($"DataManager: Quest for week {weekNumber} not found.");
            
            return quest;
        }

        public List<WeeklyQuestData> GetAllWeeklyQuests() => weeklyQuests.Values.ToList();

        public WeeklyQuestData GetQuestByID(string staticID)
        {
            if (!string.IsNullOrEmpty(staticID) && weeklyQuests.TryGetValue(staticID, out WeeklyQuestData data))
                return data;

            Log.Error($"DataManager: Quest '{staticID}' not found.");
            return null;
        }

        #endregion

        #region 이벤트 검색

        public VisitorEventData GetVisitorEvent(string staticID)
        {
            if (visitorEvents.TryGetValue(staticID, out VisitorEventData data))
                return data;
            
            Log.Error($"DataManager: VisitorEventData '{staticID}' not found.");
            return null;
        }

        public VisitorEventData GetEventDataByType(MorningEventType type)
            => visitorEvents.Values.FirstOrDefault(e => e.morningEventType == type);

        public DungeonEventData GetDungeonEvent(string staticID)
        {
            if (dungeonEvents.TryGetValue(staticID, out DungeonEventData data))
                return data;
            
            Log.Error($"DataManager: DungeonEventData '{staticID}' not found.");
            return null;
        }

        public DungeonEventData GetDungeonEventByType(DungeonEventType type)
            => dungeonEvents.Values.FirstOrDefault(e => e.eventType == type);

        /// <summary>해당 몬스터 카테고리를 가진 전투 계열(Battle/MiniBoss/Boss) 이벤트 중 첫 SO 반환. 없으면 null.</summary>
        public DungeonBattleEventData GetBattleEventByMonsterType(MonsterCategory category)
            => dungeonEvents.Values
                .OfType<DungeonBattleEventData>()
                .FirstOrDefault(e => e.monsterType == category);

        #endregion

        #region 무기 부가효과 검색

        public WeaponEffectData GetWeaponEffect(string staticID)
        {
            if (weaponEffects.TryGetValue(staticID, out var data))
                return data;

            Log.Error($"DataManager: WeaponEffectData '{staticID}' not found.");
            return null;
        }

        public List<WeaponEffectData> GetWeaponEffectsByGrade(Grade grade)
            => weaponEffects.Values.Where(e => e.grade == grade).ToList();

        /// <summary>
        /// 같은 effectType + targetStat/targetGrade/targetArmorType 조합 중 지정 grade의 데이터 반환
        /// </summary>
        public WeaponEffectData GetWeaponEffectUpgraded(string originalStaticID, Grade targetGrade)
        {
            if (!weaponEffects.TryGetValue(originalStaticID, out var original)) return null;

            return weaponEffects.Values.FirstOrDefault(d =>
                d.effectType      == original.effectType &&
                d.grade           == targetGrade &&
                d.targetStat      == original.targetStat &&
                d.targetGrade     == original.targetGrade &&
                d.targetArmorType == original.targetArmorType &&
                d.targetThreshold == original.targetThreshold
            );
        }

        /// <summary>
        /// effectType + targetStat + targetGrade + targetArmorType 조합 기준으로 그룹화하여
        /// 각 그룹의 대표 데이터(가장 낮은 grade)를 반환한다. WeaponShop 부가효과 목록 표시용.
        /// </summary>
        public List<WeaponEffectData> GetEffectGroups()
        {
            var groups = weaponEffects.Values
                .GroupBy(e => (e.effectType, e.targetStat, e.targetGrade, e.targetArmorType))
                .Select(g => g.OrderBy(e => e.grade).First())
                .OrderBy(e => e.effectType)
                .ThenBy(e => e.targetStat)
                .ThenBy(e => e.targetGrade)
                .ThenBy(e => e.targetArmorType)
                .ToList();
            return groups;
        }

        /// <summary>
        /// 특정 effectType + target 조합의 특정 grade 데이터를 반환한다.
        /// 해당 grade 데이터가 없으면 가장 가까운 grade의 데이터를 반환한다.
        /// </summary>
        public WeaponEffectData GetEffectByTypeAndGrade(WeaponEffectType type, Grade grade,
            int targetStat, int targetGradeVal, int targetArmorType)
        {
            var match = weaponEffects.Values.FirstOrDefault(e =>
                e.effectType      == type &&
                e.grade           == grade &&
                e.targetStat      == targetStat &&
                e.targetGrade     == targetGradeVal &&
                e.targetArmorType == targetArmorType);

            if (match != null) return match;

            // 해당 grade 없으면 같은 그룹 중 가장 가까운 grade 반환
            return weaponEffects.Values
                .Where(e =>
                    e.effectType      == type &&
                    e.targetStat      == targetStat &&
                    e.targetGrade     == targetGradeVal &&
                    e.targetArmorType == targetArmorType)
                .OrderBy(e => Mathf.Abs((int)e.grade - (int)grade))
                .FirstOrDefault();
        }

        #endregion

        #endregion
    }
}