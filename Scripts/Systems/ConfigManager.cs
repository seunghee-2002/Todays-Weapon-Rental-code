// Scripts/Core/ConfigManager.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    public class ConfigManager : BaseManager<ConfigManager>
    {
        [Header("Config References")]
        [SerializeField] private VisitorConfig visitorConfig;
        [SerializeField] private BlacksmithConfig blacksmithConfig;
        [SerializeField] private WeaponShopConfig weaponShopConfig;
        [SerializeField] private AdventureConfig adventureConfig;
        [SerializeField] private TraitConfig traitConfig;
        [SerializeField] private ReputationConfig reputationConfig;
        [SerializeField] private TimeConfig timeConfig;
        [SerializeField] private InventoryConfig inventoryConfig;
        [SerializeField] private QuestBoardConfig questBoardConfig;
        [SerializeField] private EndlessQuestConfig endlessQuestConfig;
        [SerializeField] private WeaponConfig weaponConfig;
        [SerializeField] private InsightConfig insightConfig;
        [SerializeField] private SeerConfig seerConfig;
        [SerializeField] private MorningEventConfig morningEventConfig;
        [SerializeField] private AdventureInfoConfig adventureInfoConfig;
        [SerializeField] private AdventureResultMotionConfig adventureResultMotionConfig;
        [SerializeField] private AdventureProgressAnimConfig adventureProgressAnimConfig;
        [SerializeField] private MonsterStageConfig monsterStageConfig;
        [SerializeField] private TutorialConfig tutorialConfig;
        [SerializeField] private PriceTierConfig priceTierConfig;

        // Public accessors
        public VisitorConfig Visitor => visitorConfig;
        public BlacksmithConfig Blacksmith => blacksmithConfig;
        public WeaponShopConfig WeaponShop => weaponShopConfig;
        public AdventureConfig Adventure => adventureConfig;
        public TraitConfig Trait => traitConfig;
        public ReputationConfig Reputation => reputationConfig;
        public TimeConfig Time => timeConfig;
        public InventoryConfig Inventory => inventoryConfig;
        public QuestBoardConfig QuestBoard => questBoardConfig;
        public EndlessQuestConfig EndlessQuest => endlessQuestConfig;
        public WeaponConfig Weapon => weaponConfig;
        public InsightConfig Insight => insightConfig;
        public SeerConfig Seer => seerConfig;
        public MorningEventConfig MorningEvent => morningEventConfig;
        public AdventureInfoConfig AdventureInfo => adventureInfoConfig;
        public AdventureResultMotionConfig AdventureResultMotion => adventureResultMotionConfig;
        public AdventureProgressAnimConfig AdventureProgressAnim => adventureProgressAnimConfig;
        public MonsterStageConfig MonsterStage => monsterStageConfig;
        public TutorialConfig Tutorial => tutorialConfig;
        public PriceTierConfig PriceTier => priceTierConfig;

        /// <summary>
        /// 주차 계단 가격 배율. PriceTierConfig가 없으면 1(무변화)을 돌려 기존 가격을 유지한다.
        /// week 인자를 받지 않는 오버로드는 현재 주차를 쓴다.
        /// </summary>
        public float PriceMult(System.Func<PriceTierConfig, float[]> selector, int week)
        {
            if (priceTierConfig == null) return 1f;
            return priceTierConfig.At(selector(priceTierConfig), week);
        }

        public float PriceMult(System.Func<PriceTierConfig, float[]> selector) =>
            PriceMult(selector, CurrentWeek);

        /// <summary>현재 주차. GameData가 아직 없으면 1주차로 본다.</summary>
        public static int CurrentWeek => GameManager.Instance?.GameData?.currentWeek ?? 1;

        protected override void Awake()
        {
            base.Awake();
            ValidateConfigs();
        }

        private void ValidateConfigs()
        {
            if (visitorConfig == null)
                Log.Error("ConfigManager: VisitorConfig is not assigned!");
            if (blacksmithConfig == null)
                Log.Error("ConfigManager: BlacksmithConfig is not assigned!");
            if (weaponShopConfig == null)
                Log.Error("ConfigManager: WeaponShopConfig is not assigned!");
            if (adventureConfig == null)
                Log.Error("ConfigManager: AdventureConfig is not assigned!");
            if (traitConfig == null)
                Log.Error("ConfigManager: TraitConfig is not assigned!");
            if (reputationConfig == null)
                Log.Error("ConfigManager: ReputationConfig is not assigned!");
            if (timeConfig == null)
                Log.Error("ConfigManager: TimeConfig is not assigned!");
            if (inventoryConfig == null)
                Log.Error("ConfigManager: InventoryConfig is not assigned!");
            if (questBoardConfig == null)
                Log.Error("ConfigManager: QuestBoardConfig is not assigned!");
            if (endlessQuestConfig == null)
                Log.Error("ConfigManager: EndlessQuestConfig is not assigned!");
            if (weaponConfig == null)
                Log.Error("ConfigManager: WeaponConfig is not assigned!");
            if (insightConfig == null)
                Log.Error("ConfigManager: InsightConfig is not assigned!");
            if (seerConfig == null)
                Log.Error("ConfigManager: SeerConfig is not assigned!");
            if (morningEventConfig == null)
                Log.Error("ConfigManager: MorningEventConfig is not assigned!");
            if (adventureInfoConfig == null)
                Log.Error("ConfigManager: AdventureInfoConfig is not assigned!");
            if (adventureResultMotionConfig == null)
                Log.Error("ConfigManager: AdventureResultMotionConfig is not assigned!");
            if (adventureProgressAnimConfig == null)
                Log.Error("ConfigManager: AdventureProgressAnimConfig is not assigned!");
            if (monsterStageConfig == null)
                Log.Error("ConfigManager: MonsterStageConfig is not assigned!");
            if (tutorialConfig == null)
                Log.Error("ConfigManager: TutorialConfig is not assigned!");
            // 미연결이면 무기 상점 진열이 전부 Common으로 떨어진다 (SelectGradeByWeight)
            if (priceTierConfig == null)
                Log.Error("ConfigManager: PriceTierConfig is not assigned!");
        }
    }
}