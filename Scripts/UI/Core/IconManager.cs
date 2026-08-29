using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 게임 전역에서 사용하는 아이콘 및 스프라이트를 관리하는 매니저
    /// </summary>
    public class IconManager : BaseManager<IconManager>
    {
        [Header("무기 타입 아이콘")]
        [SerializeField] private Sprite swordIcon;
        [SerializeField] private Sprite axeIcon;
        [SerializeField] private Sprite bowIcon;
        [SerializeField] private Sprite crossbowIcon;
        [SerializeField] private Sprite staffIcon;
        [SerializeField] private Sprite tomeIcon;
        [SerializeField] private Sprite daggerIcon;
        [SerializeField] private Sprite shurikenIcon;

        [Header("특성 아이콘")]
        [SerializeField] private Sprite luckyIcon;
        [SerializeField] private Sprite veteranIcon;
        [SerializeField] private Sprite looterIcon;
        [SerializeField] private Sprite swiftIcon;
        [SerializeField] private Sprite richIcon;
        [SerializeField] private Sprite porterIcon;
        [SerializeField] private Sprite berserkerIcon;
        [SerializeField] private Sprite enduringIcon;
        [SerializeField] private Sprite risingIcon;
        [SerializeField] private Sprite famousIcon;
        [SerializeField] private Sprite hagglerIcon;
        [SerializeField] private Sprite cowardIcon;
        [SerializeField] private Sprite focusedIcon;
        [SerializeField] private Sprite butcherIcon;
        [SerializeField] private Sprite easyExpertIcon;
        [SerializeField] private Sprite battleManiacIcon;

        [Header("방어구 타입 아이콘")]
        [SerializeField] private Sprite unknownArmorIcon;
        [SerializeField] private Sprite unarmoredIcon;
        [SerializeField] private Sprite lightArmorIcon;
        [SerializeField] private Sprite heavyArmorIcon;
        [SerializeField] private Sprite magicalArmorIcon;

        [Header("방어구 타입 타이틀 프레임")]
        [SerializeField] private Sprite unarmoredTitleFrame;
        [SerializeField] private Sprite lightArmorTitleFrame;
        [SerializeField] private Sprite heavyArmorTitleFrame;
        [SerializeField] private Sprite magicalArmorTitleFrame;

        [Header("던전 이벤트 타입 아이콘")]
        [SerializeField] private Sprite entranceIcon;
        [SerializeField] private Sprite battleIcon;
        [SerializeField] private Sprite miniBossIcon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Sprite treasureIcon;
        [SerializeField] private Sprite restIcon;
        [SerializeField] private Sprite trapIcon;
        [SerializeField] private Sprite trapEvadeIcon;
        [SerializeField] private Sprite rareDropIcon;
        [SerializeField] private Sprite retreatIcon;
        [SerializeField] private Sprite retryIcon;
        [SerializeField] private Sprite returnIcon;
        [SerializeField] private Sprite protectionIcon;

        [Header("평판 등급 아이콘")]
        [SerializeField] private Sprite bronzeReputationIcon;
        [SerializeField] private Sprite silverReputationIcon;
        [SerializeField] private Sprite goldReputationIcon;
        [SerializeField] private Sprite platinumReputationIcon;
        [SerializeField] private Sprite diamondReputationIcon;

        [Header("테스트 이미지")]
        [SerializeField] private Sprite strTestIcon;
        [SerializeField] private Sprite dexTestIcon;
        [SerializeField] private Sprite intTestIcon;
        [SerializeField] private Sprite lukTestIcon;
        [SerializeField] private Sprite allTestIcon;
        [SerializeField] private Sprite traitTestIcon;
        [SerializeField] private Sprite defaultWeaponTestIcon;

        [Header("수수께끼 상자 아이콘")]
        [SerializeField] private Sprite normalBoxClosedIcon;
        [SerializeField] private Sprite normalBoxOpenIcon;
        [SerializeField] private Sprite rareBoxClosedIcon;
        [SerializeField] private Sprite rareBoxOpenIcon;
        [SerializeField] private Sprite mythicBoxClosedIcon;
        [SerializeField] private Sprite mythicBoxOpenIcon;

        [Header("아이템 프레임 등급별 아이콘")]
        [SerializeField] private Sprite commonFrameIcon;
        [SerializeField] private Sprite uncommonFrameIcon;
        [SerializeField] private Sprite rareFrameIcon;
        [SerializeField] private Sprite epicFrameIcon;
        [SerializeField] private Sprite legendaryFrameIcon;

        [Header("인벤토리 탭 아이콘")]
        [SerializeField] private Sprite weaponTabIcon;
        [SerializeField] private Sprite materialTabIcon;
        [SerializeField] private Sprite activeItemTabIcon;

        [Header("퀘스트 타입 아이콘")]
        [SerializeField] private Sprite successfulAdventuresIcon;
        [SerializeField] private Sprite rentSpecificGradeIcon;
        [SerializeField] private Sprite rentSpecificWeaponIcon;
        [SerializeField] private Sprite completeSpecificDungeonIcon;
        [SerializeField] private Sprite greatSuccessCountIcon;
        [SerializeField] private Sprite craftCompleteIcon;
        [SerializeField] private Sprite enforceSuccessIcon;
        [SerializeField] private Sprite evolveSuccessIcon;
        [SerializeField] private Sprite rerollCompleteIcon;
        [SerializeField] private Sprite seerCompleteIcon;
        [SerializeField] private Sprite weaponPurchaseIcon;
        [SerializeField] private Sprite goldEarnedIcon;
        [SerializeField] private Sprite giftCompleteIcon;

        [Header("Glow 아이템 BG")]
        [SerializeField] private Sprite glowBrownBG;
        [SerializeField] private Sprite glowGreenBG;
        [SerializeField] private Sprite glowBlueBG;
        [SerializeField] private Sprite glowPurpleBG;
        [SerializeField] private Sprite glowRedBG;

        [Header("결과 아이템 아이콘(평판/호감도)")]
        [SerializeField] private Sprite reputationItemIcon;
        [SerializeField] private Sprite affectionItemIcon;
        [SerializeField] private Sprite insightItemIcon;

        [Header("순위 아이콘")]
        [SerializeField] private Sprite firstRankIcon;
        [SerializeField] private Sprite secondRankIcon;
        [SerializeField] private Sprite thirdRankIcon;

        [Header("잠금 아이콘")]
        [SerializeField] private Sprite lockIcon;
        [SerializeField] private Sprite unlockIcon;

        [Header("점술 수정구")]
        [SerializeField] private Sprite seerBallSprite;
        [SerializeField] private Sprite badLuckSprite;
        [SerializeField] private Sprite minorLuckSprite;
        [SerializeField] private Sprite goodLuckSprite;
        [SerializeField] private Sprite greatLuckSprite;

        [Header("방문자 타입 라벨(색 깃발)")]
        [SerializeField] private Sprite adventurerLabelImage;
        [SerializeField] private Sprite weaponShopLabelImage;
        [SerializeField] private Sprite blacksmithLabelImage;
        [SerializeField] private Sprite eventNPCLabelImage;
        [SerializeField] private Sprite investorResultLabelImage;
        [SerializeField] private Sprite deadEventLabelImage;
        [SerializeField] private Sprite heraldLabelImage;

        [Header("방문자 타입 아이콘")]
        [SerializeField] private Sprite adventurerVisitorIcon;
        [SerializeField] private Sprite weaponShopVisitorIcon;
        [SerializeField] private Sprite blacksmithVisitorIcon;
        [SerializeField] private Sprite eventNPCVisitorIcon;
        [SerializeField] private Sprite investorResultVisitorIcon;
        [SerializeField] private Sprite deadEventVisitorIcon;
        [SerializeField] private Sprite heraldVisitorIcon;

        [SerializeField] private Sprite defaultWeaponIcon;

        [Header("언어 국기 아이콘")]
        [SerializeField] private Sprite koreanFlagIcon;
        [SerializeField] private Sprite englishFlagIcon;
        [SerializeField] private Sprite japaneseFlagIcon;
        [SerializeField] private Sprite chineseFlagIcon;

        // 캐시용 딕셔너리
        private Dictionary<WeaponType, Sprite> weaponTypeIcons;
        private Dictionary<TraitType, Sprite> traitIcons;
        private Dictionary<DungeonEventType, Sprite> eventTypeIcons;
        private Dictionary<ArmorType, Sprite> armorTypeIcons;
        private Dictionary<ArmorType, Sprite> armorTypeTitleFrames;
        private Dictionary<ReputationLevel, Sprite> reputationLevelIcons;
        private Dictionary<string, Sprite> testIcons;
        private Dictionary<Grade, Sprite> frameIcons;
        private Dictionary<QuestType, Sprite> questTypeIcons;
        private Dictionary<LuckLevel, Sprite> luckLevelIcons;
        private Dictionary<VisitorType, Sprite> visitorLabelImages;
        private Dictionary<VisitorType, Sprite> visitorTypeIcons;
        private Dictionary<string, Sprite> languageFlagIcons;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            DontDestroyOnLoad(gameObject);
            InitializeDictionaries();
        }

        private void InitializeDictionaries()
        {
            // WeaponType 아이콘 매핑
            weaponTypeIcons = new Dictionary<WeaponType, Sprite>
            {
                { WeaponType.Sword, swordIcon },
                { WeaponType.Axe, axeIcon },
                { WeaponType.Bow, bowIcon },
                { WeaponType.Crossbow, crossbowIcon },
                { WeaponType.Staff, staffIcon },
                { WeaponType.Tome, tomeIcon },
                { WeaponType.Dagger, daggerIcon },
                { WeaponType.Shuriken, shurikenIcon }
            };

            // TraitType 아이콘 매핑
            traitIcons = new Dictionary<TraitType, Sprite>
            {
                { TraitType.Lucky,        luckyIcon },
                { TraitType.Veteran,      veteranIcon },
                { TraitType.Looter,       looterIcon },
                { TraitType.Swift,        swiftIcon },
                { TraitType.Rich,         richIcon },
                { TraitType.Porter,       porterIcon },
                { TraitType.Berserker,    berserkerIcon },
                { TraitType.Enduring,     enduringIcon },
                { TraitType.Rising,       risingIcon },
                { TraitType.Famous,       famousIcon },
                { TraitType.Haggler,      hagglerIcon },
                { TraitType.Coward,       cowardIcon },
                { TraitType.Focused,      focusedIcon },
                { TraitType.Butcher,      butcherIcon },
                { TraitType.EasyExpert,   easyExpertIcon },
                { TraitType.BattleManiac, battleManiacIcon },
            };

            // ArmorType 아이콘 매핑
            armorTypeIcons = new Dictionary<ArmorType, Sprite>
            {
                { ArmorType.Unarmored, unarmoredIcon },
                { ArmorType.LightArmor, lightArmorIcon },
                { ArmorType.HeavyArmor, heavyArmorIcon },
                { ArmorType.MagicalArmor, magicalArmorIcon }
            };

            armorTypeTitleFrames = new Dictionary<ArmorType, Sprite>
            {
                { ArmorType.Unarmored,    unarmoredTitleFrame    },
                { ArmorType.LightArmor,   lightArmorTitleFrame   },
                { ArmorType.HeavyArmor,   heavyArmorTitleFrame   },
                { ArmorType.MagicalArmor, magicalArmorTitleFrame },
            };

            // DungeonEventType 아이콘 매핑
            eventTypeIcons = new Dictionary<DungeonEventType, Sprite>
            {
                { DungeonEventType.Entrance,     entranceIcon  },
                { DungeonEventType.Battle,        battleIcon   },
                { DungeonEventType.MiniBoss,      miniBossIcon },
                { DungeonEventType.Boss,          bossIcon     },
                { DungeonEventType.TreasureChest, treasureIcon },
                { DungeonEventType.Rest,          restIcon     },
                { DungeonEventType.Trap,          trapIcon     },
                { DungeonEventType.TrapEvade,     trapEvadeIcon },
                { DungeonEventType.RareDrop,      rareDropIcon },
                { DungeonEventType.Retreat,       retreatIcon  },
                { DungeonEventType.Retry,         retryIcon    },
                { DungeonEventType.Return,        returnIcon   },
                { DungeonEventType.Protection,    protectionIcon }
            };

            reputationLevelIcons = new Dictionary<ReputationLevel, Sprite>
            {
                { ReputationLevel.Bronze,   bronzeReputationIcon   },
                { ReputationLevel.Silver,   silverReputationIcon   },
                { ReputationLevel.Gold,     goldReputationIcon     },
                { ReputationLevel.Platinum, platinumReputationIcon },
                { ReputationLevel.Diamond,  diamondReputationIcon  }
            };

            testIcons = new Dictionary<string, Sprite>
            {
                { "STR", strTestIcon },
                { "DEX", dexTestIcon },
                { "INT", intTestIcon },
                { "LUK", lukTestIcon },
                { "ALL", allTestIcon },
                { "Trait", traitTestIcon },
                { "DefaultWeapon", defaultWeaponTestIcon }
            };

            frameIcons = new Dictionary<Grade, Sprite>
            {
                { Grade.Common, commonFrameIcon },
                { Grade.Uncommon, uncommonFrameIcon },
                { Grade.Rare, rareFrameIcon },
                { Grade.Epic, epicFrameIcon },
                { Grade.Legendary, legendaryFrameIcon }
            };

            questTypeIcons = new Dictionary<QuestType, Sprite>
            {
                { QuestType.SuccessfulAdventures,   successfulAdventuresIcon   },
                { QuestType.RentSpecificGrade,       rentSpecificGradeIcon      },
                { QuestType.RentSpecificWeapon,      rentSpecificWeaponIcon     },
                { QuestType.CompleteSpecificDungeon, completeSpecificDungeonIcon },
                { QuestType.GreatSuccessCount,       greatSuccessCountIcon      },
                { QuestType.CraftComplete,           craftCompleteIcon          },
                { QuestType.EnforceSuccess,          enforceSuccessIcon         },
                { QuestType.EvolveSuccess,           evolveSuccessIcon          },
                { QuestType.RerollComplete,          rerollCompleteIcon         },
                { QuestType.SeerComplete,            seerCompleteIcon           },
                { QuestType.WeaponPurchase,          weaponPurchaseIcon         },
                { QuestType.GoldEarned,              goldEarnedIcon             },
                { QuestType.GiftComplete,            giftCompleteIcon           },
            };

            luckLevelIcons = new Dictionary<LuckLevel, Sprite>
            {
                { LuckLevel.흉,   badLuckSprite   },
                { LuckLevel.소길, minorLuckSprite },
                { LuckLevel.중길, goodLuckSprite  },
                { LuckLevel.대길, greatLuckSprite },
            };

            // VisitorType 라벨(색 깃발) 매핑
            visitorLabelImages = new Dictionary<VisitorType, Sprite>
            {
                { VisitorType.Adventurer,     adventurerLabelImage     },
                { VisitorType.WeaponShop,     weaponShopLabelImage     },
                { VisitorType.Blacksmith,     blacksmithLabelImage     },
                { VisitorType.EventNPC,       eventNPCLabelImage       },
                { VisitorType.InvestorResult, investorResultLabelImage },
                { VisitorType.DeadEvent,      deadEventLabelImage      },
                { VisitorType.Herald,         heraldLabelImage         },
            };

            // VisitorType 아이콘 매핑
            visitorTypeIcons = new Dictionary<VisitorType, Sprite>
            {
                { VisitorType.Adventurer,     adventurerVisitorIcon     },
                { VisitorType.WeaponShop,     weaponShopVisitorIcon     },
                { VisitorType.Blacksmith,     blacksmithVisitorIcon     },
                { VisitorType.EventNPC,       eventNPCVisitorIcon       },
                { VisitorType.InvestorResult, investorResultVisitorIcon },
                { VisitorType.DeadEvent,      deadEventVisitorIcon      },
                { VisitorType.Herald,         heraldVisitorIcon         },
            };

            // 로케일 코드 -> 국기 아이콘 매핑 (LocalizationManager.SupportedCodes와 동일 키)
            languageFlagIcons = new Dictionary<string, Sprite>
            {
                { "ko-KR",   koreanFlagIcon   },
                { "en",      englishFlagIcon  },
                { "ja",      japaneseFlagIcon },
                { "zh-Hans", chineseFlagIcon  },
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// WeaponType에 해당하는 아이콘 반환
        /// </summary>
        public Sprite GetIconByWeaponType(WeaponType weaponType)
        {
            if (weaponTypeIcons.TryGetValue(weaponType, out Sprite icon))
            {
                return icon;
            }

            Log.Warn($"IconManager: No icon found for WeaponType {weaponType}");
            return null;
        }

        /// <summary>
        /// 기본무기(미대여) 표시용 아이콘 반환
        /// </summary>
        public Sprite GetDefaultWeaponIcon() => defaultWeaponIcon;

        /// <summary>
        /// TraitType에 해당하는 아이콘 반환
        /// </summary>
        public Sprite GetIconByTraitType(TraitType traitType)
        {
            if (traitIcons.TryGetValue(traitType, out Sprite icon))
            {
                return icon;
            }

            Log.Warn($"IconManager: No icon found for TraitType {traitType}");
            return null;
        }

        /// <summary>
        /// ArmorType에 해당하는 아이콘 반환
        /// </summary>
        public Sprite GetIconByArmorType(ArmorType armorType)
        {
            if (armorTypeIcons.TryGetValue(armorType, out Sprite icon))
            {
                return icon;
            }

            Log.Warn($"IconManager: No icon found for ArmorType {armorType}");
            return null;
        }

        /// <summary>
        /// 이벤트 타입별 아이콘 반환
        /// </summary>
        public Sprite GetIconByEventType(DungeonEventType eventType)
        {
            if (eventTypeIcons.TryGetValue(eventType, out Sprite icon))
                return icon;

            Log.Warn($"IconManager: No icon found for DungeonEventType {eventType}");
            return null;
        }

        /// <summary>
        /// 잠금 상태 아이콘 반환
        /// isLocked가 true면 lockIcon, false면 unlockIcon 반환
        /// </summary>
        public Sprite GetLockIcon(bool isLocked) => isLocked ? lockIcon : unlockIcon;

        /// <summary>
        /// ReputationLevel에 해당하는 아이콘 반환
        /// </summary>
        public Sprite GetIconByReputationLevel(ReputationLevel level)
        {
            if (reputationLevelIcons.TryGetValue(level, out Sprite icon))
                return icon;

            Log.Warn($"[IconManager] No icon found for ReputationLevel {level}");
            return null;
        }

        /// <summary>
        /// 수수께끼 상자 아이콘 반환. tier: 0=일반, 1=희귀, 2=신비
        /// </summary>
        public Sprite GetMysteryBoxIcon(int tier, bool isOpen)
        {
            return (tier, isOpen) switch
            {
                (0, false) => normalBoxClosedIcon,
                (0, true)  => normalBoxOpenIcon,
                (1, false) => rareBoxClosedIcon,
                (1, true)  => rareBoxOpenIcon,
                (2, false) => mythicBoxClosedIcon,
                (2, true)  => mythicBoxOpenIcon,
                _          => normalBoxClosedIcon
            };
        }

        public Sprite GetUnknownArmorIcon()
        {
            return unknownArmorIcon;
        }

        public Sprite GetTitleFrameByArmorType(ArmorType armorType)
        {
            if (armorTypeTitleFrames.TryGetValue(armorType, out Sprite sprite))
                return sprite;

            Log.Warn($"[IconManager] No title frame found for ArmorType {armorType}");
            return null;
        }

        public Sprite GetIconByInventoryTab(InventoryView.InventoryTab tab)
        {
            return tab switch
            {
                InventoryView.InventoryTab.Weapon     => weaponTabIcon,
                InventoryView.InventoryTab.Material   => materialTabIcon,
                InventoryView.InventoryTab.ActiveItem => activeItemTabIcon,
                _                                     => weaponTabIcon
            };
        }

        public Sprite GetIconByQuestType(QuestType questType)
        {
            if (questTypeIcons.TryGetValue(questType, out Sprite icon))
                return icon;

            Log.Warn($"[IconManager] No icon found for QuestType {questType}");
            return null;
        }

        public Sprite GetFrameByGrade(Grade grade)
        {
            if (frameIcons.TryGetValue(grade, out Sprite icon))
                return icon;

            Log.Warn($"[IconManager] No frame icon found for Grade {grade}");
            return null;
        }

        /// <summary>
        /// 모험 결과 아이템 컨테이너에서 사용하는 BG 스프라이트 반환
        /// </summary>
        public Sprite GetResultItemBG(ResultItemType type)
        {
            return type switch
            {
                ResultItemType.Reputation       => glowBrownBG,
                ResultItemType.Affection        => glowRedBG,
                ResultItemType.Insight          => glowBlueBG,
                ResultItemType.MaterialCraft    => glowGreenBG,
                ResultItemType.MaterialEnforce  => glowBlueBG,
                ResultItemType.MaterialSpecial  => glowPurpleBG,
                _ => null
            };
        }

        public Sprite GetArmorTypeGlowBG(ArmorType armorType)
        {
            return armorType switch
            {
                ArmorType.Unarmored => glowBrownBG,
                ArmorType.LightArmor => glowGreenBG,
                ArmorType.HeavyArmor => glowPurpleBG,
                ArmorType.MagicalArmor => glowBlueBG,
                _ => null
            };
        }

        public Sprite GetReputationItemIcon() => reputationItemIcon;
        public Sprite GetAffectionItemIcon() => affectionItemIcon;
        public Sprite GetInsightItemIcon() => insightItemIcon;

        public Sprite GetRankIcon(int rank)
        {
            return rank switch
            {
                1 => firstRankIcon,
                2 => secondRankIcon,
                3 => thirdRankIcon,
                _ => null
            };
        }

        /// <summary>
        /// 테스트용 아이콘 반환
        /// </summary>
        public Sprite GetIconByTest(string key)
        {
            if (testIcons.TryGetValue(key, out Sprite icon))
            {
                return icon;
            }

            Log.Warn($"IconManager: No test icon found for key {key}");
            return null;
        }

        /// <summary>
        /// 점술 수정구 기본 스프라이트(SeerBall) 반환
        /// </summary>
        public Sprite GetCrystalBallBaseSprite() => seerBallSprite;

        /// <summary>
        /// LuckLevel에 해당하는 점술 결과 수정구 스프라이트 반환
        /// </summary>
        public Sprite GetIconByLuckLevel(LuckLevel luckLevel)
        {
            if (luckLevelIcons.TryGetValue(luckLevel, out Sprite icon))
                return icon;

            Log.Warn($"[IconManager] No icon found for LuckLevel {luckLevel}");
            return null;
        }

        /// <summary>
        /// VisitorType에 해당하는 라벨(색 깃발) 스프라이트 반환
        /// </summary>
        public Sprite GetVisitorLabelImage(VisitorType visitorType)
        {
            if (visitorLabelImages.TryGetValue(visitorType, out Sprite sprite))
                return sprite;

            Log.Warn($"[IconManager] No label image found for VisitorType {visitorType}");
            return null;
        }

        /// <summary>
        /// VisitorType에 해당하는 타입 아이콘 반환
        /// </summary>
        public Sprite GetIconByVisitorType(VisitorType visitorType)
        {
            if (visitorTypeIcons.TryGetValue(visitorType, out Sprite icon))
                return icon;

            Log.Warn($"[IconManager] No icon found for VisitorType {visitorType}");
            return null;
        }

        /// <summary>
        /// 로케일 코드(ko-KR/en/ja/zh-Hans)에 해당하는 국기 아이콘 반환
        /// </summary>
        public Sprite GetIconByLocaleCode(string localeCode)
        {
            if (languageFlagIcons.TryGetValue(localeCode, out Sprite icon))
                return icon;

            Log.Warn($"[IconManager] No flag icon found for locale {localeCode}");
            return null;
        }

        #endregion
    }
}
