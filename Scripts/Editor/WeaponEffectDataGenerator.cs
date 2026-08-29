#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental
{
    public static class WeaponEffectDataGenerator
    {
        private const string BASE_PATH = "Assets/_Projects/Data/WeaponEffects";

        [MenuItem("Tools/Generate Weapon Effect Data")]
        public static void Generate()
        {
            // 등급별 폴더 생성
            foreach (Grade grade in System.Enum.GetValues(typeof(Grade)))
            {
                string folderPath = $"{BASE_PATH}/{grade}";
                if (!AssetDatabase.IsValidFolder(folderPath))
                    AssetDatabase.CreateFolder(BASE_PATH, grade.ToString());
            }

            int created = 0;
            int skipped = 0;

            // 등급별 생성
            foreach (Grade grade in System.Enum.GetValues(typeof(Grade)))
            {
                // 해당 등급에서 사용 가능한 효과 목록
                var effects = GetAvailableEffects(grade);

                foreach (var effectType in effects)
                {
                    var targets = GetTargets(effectType);

                    if (targets == null || targets.Length == 0)
                    {
                        // 타겟 없는 효과 (단일 에셋)
                        bool result = CreateAsset(grade, effectType, null, ref created, ref skipped);
                    }
                    else
                    {
                        foreach (var target in targets)
                        {
                            CreateAsset(grade, effectType, target, ref created, ref skipped);
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WeaponEffectDataGenerator] 완료 — 생성: {created}, 스킵(이미 존재): {skipped}");
        }

        private static bool CreateAsset(Grade grade, WeaponEffectType effectType, TargetInfo target, ref int created, ref int skipped)
        {
            string fileName = BuildFileName(grade, effectType, target);
            string path = $"{BASE_PATH}/{grade}/{fileName}.asset";

            // 이미 존재하면 스킵
            if (File.Exists($"{Application.dataPath.Replace("Assets", "")}{path}"))
            {
                skipped++;
                return false;
            }

            var data = ScriptableObject.CreateInstance<WeaponEffectData>();
            data.grade = grade;
            data.effectType = effectType;
            data.baseValueRange = GetBaseValueRange(effectType, grade);

            if (target != null)
            {
                data.targetStat      = target.targetStat;
                data.targetGrade     = target.targetGrade;
                data.targetArmorType = target.targetArmorType;
                data.targetThreshold = target.targetThreshold;
            }

            AssetDatabase.CreateAsset(data, path);

            // StaticID를 파일명으로 설정 (BaseData.staticID 필드)
            var so = new SerializedObject(data);
            var prop = so.FindProperty("staticID");
            if (prop != null)
            {
                prop.stringValue = fileName;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            created++;
            return true;
        }

        // ──────────────────────────────────────────────
        // 등급별 사용 가능한 효과 목록 (누적)
        // ──────────────────────────────────────────────
        private static WeaponEffectType[] GetAvailableEffects(Grade grade)
        {
            // StatBonus 계열은 기존 에셋(StatThresholdBonus_*)과 네이밍 충돌로 Generator에서 제외
            // 기존 에셋은 CSV에서 수동 관리
            return grade switch
            {
                Grade.Common => new[]
                {
                    WeaponEffectType.DungeonGradeBonus,
                    WeaponEffectType.ArmorTypeBonus,
                    WeaponEffectType.WeaponTypeMatchBonus,
                    // 신규
                    WeaponEffectType.BattleGoldBonus,
                    WeaponEffectType.TreasureGoldBonus,
                    WeaponEffectType.MaterialAmountBonus,
                    WeaponEffectType.RestChanceBonus,
                    WeaponEffectType.FailGoldBonus,
                    WeaponEffectType.EnforceMaterialBonus,
                },
                Grade.Uncommon => new[]
                {
                    WeaponEffectType.DungeonGradeBonus,
                    WeaponEffectType.ArmorTypeBonus,
                    WeaponEffectType.WeaponTypeMatchBonus,
                    // 신규
                    WeaponEffectType.BattleGoldBonus,
                    WeaponEffectType.MiniBossGoldBonus,
                    WeaponEffectType.TreasureGoldBonus,
                    WeaponEffectType.MaterialAmountBonus,
                    WeaponEffectType.RestChanceBonus,
                    WeaponEffectType.TreasureChestChanceBonus,
                    WeaponEffectType.RareDropChanceBonus,
                    WeaponEffectType.FailGoldBonus,
                    WeaponEffectType.EnforceMaterialBonus,
                },
                Grade.Rare => new[]
                {
                    WeaponEffectType.DungeonGradeBonus,
                    WeaponEffectType.ArmorTypeBonus,
                    WeaponEffectType.WeaponTypeMatchBonus,
                    WeaponEffectType.AllStatBonus,
                    // 신규
                    WeaponEffectType.BattleGoldBonus,
                    WeaponEffectType.MiniBossGoldBonus,
                    WeaponEffectType.BossGoldBonus,
                    WeaponEffectType.TreasureGoldBonus,
                    WeaponEffectType.MaterialAmountBonus,
                    WeaponEffectType.RestChanceBonus,
                    WeaponEffectType.TreasureChestChanceBonus,
                    WeaponEffectType.RareDropChanceBonus,
                    WeaponEffectType.FailGoldBonus,
                    WeaponEffectType.TrapNegation,
                    WeaponEffectType.SpecialMaterialChance,
                    WeaponEffectType.EnforceMaterialBonus,
                    WeaponEffectType.AdventureTimeReduction,
                },
                Grade.Epic => new[]
                {
                    WeaponEffectType.DungeonGradeBonus,
                    WeaponEffectType.ArmorTypeBonus,
                    WeaponEffectType.WeaponTypeMatchBonus,
                    WeaponEffectType.AllStatBonus,
                    WeaponEffectType.RetreatPrevention,
                    WeaponEffectType.GreatSuccessBonus,
                    // 신규
                    WeaponEffectType.BattleGoldBonus,
                    WeaponEffectType.MiniBossGoldBonus,
                    WeaponEffectType.BossGoldBonus,
                    WeaponEffectType.TreasureGoldBonus,
                    WeaponEffectType.AllGoldBonus,
                    WeaponEffectType.MaterialAmountBonus,
                    WeaponEffectType.RestChanceBonus,
                    WeaponEffectType.TreasureChestChanceBonus,
                    WeaponEffectType.RareDropChanceBonus,
                    WeaponEffectType.FailGoldBonus,
                    WeaponEffectType.TrapNegation,
                    WeaponEffectType.SpecialMaterialChance,
                    WeaponEffectType.EnforceMaterialBonus,
                    WeaponEffectType.EventCountBonus,
                    WeaponEffectType.AdventureTimeReduction,
                },
                Grade.Legendary => new[]
                {
                    WeaponEffectType.DungeonGradeBonus,
                    WeaponEffectType.ArmorTypeBonus,
                    WeaponEffectType.WeaponTypeMatchBonus,
                    WeaponEffectType.AllStatBonus,
                    WeaponEffectType.RetreatPrevention,
                    WeaponEffectType.GreatSuccessBonus,
                    WeaponEffectType.DoubleReward,
                    // 신규
                    WeaponEffectType.BattleGoldBonus,
                    WeaponEffectType.MiniBossGoldBonus,
                    WeaponEffectType.BossGoldBonus,
                    WeaponEffectType.TreasureGoldBonus,
                    WeaponEffectType.AllGoldBonus,
                    WeaponEffectType.MaterialAmountBonus,
                    WeaponEffectType.RestChanceBonus,
                    WeaponEffectType.TreasureChestChanceBonus,
                    WeaponEffectType.RareDropChanceBonus,
                    WeaponEffectType.FailGoldBonus,
                    WeaponEffectType.TrapNegation,
                    WeaponEffectType.SpecialMaterialChance,
                    WeaponEffectType.EnforceMaterialBonus,
                    WeaponEffectType.EventCountBonus,
                    WeaponEffectType.AdventureTimeReduction,
                },
                _ => new WeaponEffectType[0]
            };
        }

        // ──────────────────────────────────────────────
        // effectType별 타겟 목록
        // ──────────────────────────────────────────────
        private static TargetInfo[] GetTargets(WeaponEffectType effectType)
        {
            switch (effectType)
            {
                case WeaponEffectType.DungeonGradeBonus:
                    return new[]
                    {
                        new TargetInfo { targetGrade = (int)Grade.Common,    name = "Common"    },
                        new TargetInfo { targetGrade = (int)Grade.Uncommon,  name = "Uncommon"  },
                        new TargetInfo { targetGrade = (int)Grade.Rare,      name = "Rare"      },
                        new TargetInfo { targetGrade = (int)Grade.Epic,      name = "Epic"      },
                        new TargetInfo { targetGrade = (int)Grade.Legendary, name = "Legendary" },
                    };

                case WeaponEffectType.ArmorTypeBonus:
                    return new[]
                    {
                        new TargetInfo { targetArmorType = (int)ArmorType.Unarmored, name = "Unarmored" },
                        new TargetInfo { targetArmorType = (int)ArmorType.LightArmor,     name = "Light"     },
                        new TargetInfo { targetArmorType = (int)ArmorType.HeavyArmor,     name = "Heavy"     },
                        new TargetInfo { targetArmorType = (int)ArmorType.MagicalArmor,   name = "Magical"   },
                    };

                // StatBonus 계열은 기존 에셋(StatThresholdBonus_*)과 네이밍 충돌로 Generator에서 제외
                // 타겟 없는 효과들 (기존)
                case WeaponEffectType.WeaponTypeMatchBonus:
                case WeaponEffectType.AllStatBonus:
                case WeaponEffectType.RetreatPrevention:
                case WeaponEffectType.GreatSuccessBonus:
                case WeaponEffectType.DoubleReward:
                // 타겟 없는 효과들 (신규)
                case WeaponEffectType.BattleGoldBonus:
                case WeaponEffectType.MiniBossGoldBonus:
                case WeaponEffectType.BossGoldBonus:
                case WeaponEffectType.TreasureGoldBonus:
                case WeaponEffectType.AllGoldBonus:
                case WeaponEffectType.MaterialAmountBonus:
                case WeaponEffectType.RestChanceBonus:
                case WeaponEffectType.TreasureChestChanceBonus:
                case WeaponEffectType.RareDropChanceBonus:
                case WeaponEffectType.FailGoldBonus:
                case WeaponEffectType.TrapNegation:
                case WeaponEffectType.SpecialMaterialChance:
                case WeaponEffectType.EnforceMaterialBonus:
                case WeaponEffectType.EventCountBonus:
                case WeaponEffectType.AdventureTimeReduction:
                default:
                    return null;
            }
        }

        // ──────────────────────────────────────────────
        // effectType + grade별 baseValueRange 기본값
        // ──────────────────────────────────────────────
        private static Vector2 GetBaseValueRange(WeaponEffectType effectType, Grade grade)
        {
            switch (effectType)
            {
                // ── 기존 효과 (CSV 기준, grade별 분리) ──────────────────────
                case WeaponEffectType.DungeonGradeBonus:
                    return grade switch
                    {
                        Grade.Common    => new Vector2(0.03f, 0.05f),
                        Grade.Uncommon  => new Vector2(0.04f, 0.08f),
                        Grade.Rare      => new Vector2(0.05f, 0.10f),
                        Grade.Epic      => new Vector2(0.10f, 0.20f),
                        Grade.Legendary => new Vector2(0.20f, 0.30f),
                        _               => new Vector2(0.03f, 0.05f),
                    };

                case WeaponEffectType.ArmorTypeBonus:
                    return grade switch
                    {
                        Grade.Common    => new Vector2(0.05f, 0.10f),
                        Grade.Uncommon  => new Vector2(0.07f, 0.12f),
                        Grade.Rare      => new Vector2(0.10f, 0.15f),
                        Grade.Epic      => new Vector2(0.20f, 0.30f),
                        Grade.Legendary => new Vector2(0.30f, 0.50f),
                        _               => new Vector2(0.05f, 0.10f),
                    };

                case WeaponEffectType.WeaponTypeMatchBonus:
                    return grade switch
                    {
                        Grade.Common    => new Vector2(0.10f, 0.20f),
                        Grade.Uncommon  => new Vector2(0.10f, 0.20f),
                        Grade.Rare      => new Vector2(0.15f, 0.30f),
                        Grade.Epic      => new Vector2(0.30f, 0.40f),
                        Grade.Legendary => new Vector2(0.50f, 1.00f),
                        _               => new Vector2(0.10f, 0.20f),
                    };

                case WeaponEffectType.AllStatBonus:
                    return grade switch
                    {
                        Grade.Rare      => new Vector2(5f,  10f),
                        Grade.Epic      => new Vector2(10f, 20f),
                        Grade.Legendary => new Vector2(20f, 30f),
                        _               => new Vector2(5f,  10f),
                    };

                case WeaponEffectType.RetreatPrevention:
                    return grade switch
                    {
                        Grade.Epic      => new Vector2(1f, 2f),
                        Grade.Legendary => new Vector2(2f, 4f),
                        _               => new Vector2(1f, 2f),
                    };

                case WeaponEffectType.GreatSuccessBonus:
                    return grade switch
                    {
                        Grade.Epic      => new Vector2(0.05f, 0.10f),
                        Grade.Legendary => new Vector2(0.10f, 0.20f),
                        _               => new Vector2(0.05f, 0.10f),
                    };

                case WeaponEffectType.DoubleReward:
                    return new Vector2(0.10f, 0.25f);

                // ── 신규 효과 (grade별 분리) ──────────────────────────────
                case WeaponEffectType.BattleGoldBonus:
                case WeaponEffectType.MiniBossGoldBonus:
                    return grade switch
                    {
                        Grade.Common    => new Vector2(0.10f, 0.20f),
                        Grade.Uncommon  => new Vector2(0.10f, 0.25f),
                        Grade.Rare      => new Vector2(0.15f, 0.30f),
                        Grade.Epic      => new Vector2(0.20f, 0.35f),
                        Grade.Legendary => new Vector2(0.25f, 0.40f),
                        _               => new Vector2(0.10f, 0.20f),
                    };

                case WeaponEffectType.BossGoldBonus:
                case WeaponEffectType.TreasureGoldBonus:
                    return grade switch
                    {
                        Grade.Common    => new Vector2(0.10f, 0.20f),
                        Grade.Uncommon  => new Vector2(0.10f, 0.25f),
                        Grade.Rare      => new Vector2(0.15f, 0.30f),
                        Grade.Epic      => new Vector2(0.20f, 0.40f),
                        Grade.Legendary => new Vector2(0.30f, 0.50f),
                        _               => new Vector2(0.10f, 0.20f),
                    };

                case WeaponEffectType.AllGoldBonus:
                    return grade switch
                    {
                        Grade.Epic      => new Vector2(0.05f, 0.15f),
                        Grade.Legendary => new Vector2(0.10f, 0.20f),
                        _               => new Vector2(0.05f, 0.15f),
                    };

                case WeaponEffectType.MaterialAmountBonus:
                case WeaponEffectType.EnforceMaterialBonus:
                    return new Vector2(1f, 2f); // 정수

                case WeaponEffectType.RestChanceBonus:
                case WeaponEffectType.TreasureChestChanceBonus:
                case WeaponEffectType.RareDropChanceBonus:
                    return grade switch
                    {
                        Grade.Common    => new Vector2(0.5f, 1.0f),
                        Grade.Uncommon  => new Vector2(0.5f, 1.5f),
                        Grade.Rare      => new Vector2(1.0f, 2.0f),
                        Grade.Epic      => new Vector2(1.0f, 2.5f),
                        Grade.Legendary => new Vector2(1.5f, 3.0f),
                        _               => new Vector2(0.5f, 1.0f),
                    };

                case WeaponEffectType.FailGoldBonus:
                    return grade switch
                    {
                        Grade.Common    => new Vector2(0.05f, 0.15f),
                        Grade.Uncommon  => new Vector2(0.10f, 0.20f),
                        Grade.Rare      => new Vector2(0.10f, 0.25f),
                        Grade.Epic      => new Vector2(0.15f, 0.30f),
                        Grade.Legendary => new Vector2(0.20f, 0.40f),
                        _               => new Vector2(0.05f, 0.15f),
                    };

                case WeaponEffectType.TrapNegation:
                    return grade switch
                    {
                        Grade.Rare      => new Vector2(0.20f, 0.35f),
                        Grade.Epic      => new Vector2(0.30f, 0.45f),
                        Grade.Legendary => new Vector2(0.40f, 0.60f),
                        _               => new Vector2(0.20f, 0.35f),
                    };

                case WeaponEffectType.SpecialMaterialChance:
                    return grade switch
                    {
                        Grade.Rare      => new Vector2(0.05f, 0.10f),
                        Grade.Epic      => new Vector2(0.08f, 0.15f),
                        Grade.Legendary => new Vector2(0.10f, 0.20f),
                        _               => new Vector2(0.05f, 0.10f),
                    };

                case WeaponEffectType.EventCountBonus:
                    return new Vector2(1f, 2f); // 정수

                case WeaponEffectType.AdventureTimeReduction:
                    return grade switch
                    {
                        Grade.Rare      => new Vector2(0.05f, 0.10f),
                        Grade.Epic      => new Vector2(0.08f, 0.15f),
                        Grade.Legendary => new Vector2(0.10f, 0.20f),
                        _               => new Vector2(0.05f, 0.10f),
                    };

                default:
                    return Vector2.zero;
            }
        }

        // ──────────────────────────────────────────────
        // 파일명 생성
        // ──────────────────────────────────────────────
        private static string BuildFileName(Grade grade, WeaponEffectType effectType, TargetInfo target)
        {
            string baseName = $"WED_{grade}_{effectType}";
            if (target != null)
                return $"{baseName}_{target.name}";
            return baseName;
        }

        // ──────────────────────────────────────────────
        // 내부 타겟 정보 구조체
        // ──────────────────────────────────────────────
        private class TargetInfo
        {
            public string name;
            public int targetStat;
            public int targetGrade;
            public int targetArmorType;
            public int targetThreshold;
        }
    }
}
#endif