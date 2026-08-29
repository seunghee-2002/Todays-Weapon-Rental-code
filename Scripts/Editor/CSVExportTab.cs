// Assets/_Projects/Scripts/Editor/CSVTools/CSVExportTab.cs
// SO → _preview.csv 생성 + 기존 CSV와 diff 표시
// 승인(Applier 탭)은 CSVApplierTab 에서 처리

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public class CSVExportTab
    {
        // ─── 경로 (CSVToolWindow 에서 주입) ──────────────────────────
        public string CsvFolder;
        public string PreviewFolder;
        public string LogFolder;
        public string SoWeapon, SoAdventurer, SoMaterial, SoDungeonEvent;
        public string SoWeaponEffect, SoActiveItem, SoVisitorEvent, SoDungeon;
        public string SoWeaponRecipe, SoActiveRecipe, SoWeeklyQuest;
        public string SoFixedAppearance, SoDialogue;

        // ─── 상태 ─────────────────────────────────────────────────────
        private List<DiffRow> diffRows   = new List<DiffRow>();
        private DiffFilter    filter     = new DiffFilter();
        private Vector2       scroll;
        private string        lastTime   = "-";
        private bool          HasPreview { get; set; }

        private static readonly string[] TypeNames =
        {
            "전체",
            "WeaponData", "AdventurerData", "MaterialData",
            "DungeonEventData", "WeaponEffectData", "ActiveItemData",
            "VisitorEventData", "DungeonData", "WeaponRecipeData",
            "ActiveItemRecipeData", "WeeklyQuestData",
            "FixedAppearanceData", "DialogueData"
        };
        private bool[] typeSelected = CSVDiffCore.NewAllSelected(TypeNames.Length);   // 기본 전체 체크

        // ─── GUI ──────────────────────────────────────────────────────
        public void OnGUI()
        {
            EditorGUILayout.LabelField("SO → CSV  미리보기 생성", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"마지막 생성: {lastTime}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            CSVDiffCore.DrawTypeCheckboxes(TypeNames, typeSelected);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("① 미리보기 생성  (SO → _preview.csv)", GUILayout.Height(28)))
                GeneratePreview();

            EditorGUILayout.Space(2);

            GUI.enabled = HasPreview && diffRows.Count > 0;
            if (GUILayout.Button("② 로그 저장  (.log)", GUILayout.Height(24)))
                CSVDiffCore.SaveLog(diffRows, LogFolder, "선택 타입", "SO→CSV");
            GUI.enabled = true;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("[ Diff 결과 ]", EditorStyles.boldLabel);

            if (diffRows.Count > 0)
                CSVDiffCore.DrawFilterBar(filter, diffRows);

            EditorGUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            CSVDiffCore.DrawDiffRows(diffRows, filter);
            EditorGUILayout.EndScrollView();
        }

        // ─── 미리보기 생성 ────────────────────────────────────────────

        #region 미리보기 생성

        private void GeneratePreview()
        {
            if (!CSVDiffCore.AnySelected(typeSelected))
            {
                EditorUtility.DisplayDialog("변환 대상 없음", "변환 대상 타입을 하나 이상 선택하세요.", "확인");
                return;
            }

            CSVDiffCore.EnsureDir(PreviewFolder);
            diffRows.Clear();

            var targets = GetTargets();

            // 잠금 사전 점검: 읽을 원본 CSV + 쓸 preview 파일
            var lockCheck = new List<string>();
            foreach (var (name, _) in targets)
            {
                lockCheck.Add(CsvPath(name));
                lockCheck.Add(PreviewPath(name));
            }
            if (CSVDiffCore.WarnIfAnyLocked(lockCheck)) return;

            foreach (var (name, rows) in targets)
                ProcessOne(name, rows);

            HasPreview = true;
            lastTime   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            AssetDatabase.Refresh();

            var (a, r, m, u) = CSVDiffCore.Summary(diffRows);
            Debug.Log($"[CSVExportTab] 미리보기 완료 — 추가:{a} 삭제:{r} 수정:{m} 동일:{u}");
        }

        private void ProcessOne(string name, List<string[]> newRows)
        {
            // preview 저장
            string previewPath = PreviewPath(name);
            CSVDiffCore.WriteCsv(previewPath, newRows);

            // 기존 CSV 로드
            var origRows = CSVDiffCore.ReadCsvRows(CsvPath(name));

            // diff 계산
            diffRows.AddRange(CSVDiffCore.Compute(name, origRows, newRows));
        }

        private List<(string, List<string[]>)> GetTargets()
        {
            var result = new List<(string, List<string[]>)>();
            for (int i = 1; i < TypeNames.Length; i++)
                if (typeSelected[i]) result.AddRange(TargetsForType(TypeNames[i]));
            return result;
        }

        private List<(string, List<string[]>)> TargetsForType(string name)
        {
            return name switch
            {
                "WeaponData"          => Single("WeaponData",          BuildRows_WeaponData()),
                "AdventurerData" => new List<(string, List<string[]>)>
                {
                    ("AdventurerData",           BuildRows_AdventurerData()),
                    ("AdventurerData_FixedParts", BuildRows_AdventurerFixedParts()),
                },
                "MaterialData"        => Single("MaterialData",        BuildRows_MaterialData()),
                "DungeonEventData"    => Single("DungeonEventData",    BuildRows_DungeonEventData()),
                "WeaponEffectData"    => Single("WeaponEffectData",    BuildRows_WeaponEffectData()),
                "ActiveItemData"      => Single("ActiveItemData",      BuildRows_ActiveItemData()),
                "VisitorEventData"    => Single("VisitorEventData",    BuildRows_VisitorEventData()),
                "DungeonData"         => new List<(string, List<string[]>)>
                {
                    ("DungeonData",                 BuildRows_DungeonData()),
                    ("DungeonData_ArmorVariants",    BuildRows_DungeonArmorVariants()),
                    ("DungeonData_DropMaterials",    BuildRows_DungeonDropMaterials()),
                    ("DungeonData_EventPool",        BuildRows_DungeonEventPool()),
                },
                "WeaponRecipeData"    => new List<(string, List<string[]>)>
                {
                    ("WeaponRecipeData",            BuildRows_WeaponRecipeData()),
                    ("WeaponRecipeData_Materials",   BuildRows_WeaponRecipeMaterials()),
                },
                "ActiveItemRecipeData" => new List<(string, List<string[]>)>
                {
                    ("ActiveItemRecipeData",         BuildRows_ActiveItemRecipeData()),
                    ("ActiveItemRecipeData_Materials",BuildRows_ActiveItemRecipeMaterials()),
                },
                "WeeklyQuestData"     => new List<(string, List<string[]>)>
                {
                    ("WeeklyQuestData",              BuildRows_WeeklyQuestData()),
                    ("WeeklyQuestData_Requirements", BuildRows_WeeklyQuestRequirements()),
                },
                "FixedAppearanceData" => new List<(string, List<string[]>)>
                {
                    ("FixedAppearanceData",       BuildRows_FixedAppearanceData()),
                    ("FixedAppearanceData_Parts", BuildRows_FixedAppearanceParts()),
                },
                "DialogueData"        => new List<(string, List<string[]>)>
                {
                    ("DialogueData",         BuildRows_DialogueData()),
                    ("DialogueData_Nodes",   BuildRows_DialogueNodes()),
                    ("DialogueData_Choices", BuildRows_DialogueChoices()),
                },
                _ => new List<(string, List<string[]>)>()
            };
        }

        private List<(string, List<string[]>)> Single(string n, List<string[]> r)
            => new List<(string, List<string[]>)> { (n, r) };

        #endregion

        // ─── rows 빌더 ────────────────────────────────────────────────

        #region rows 빌더

        private List<string[]> BuildRows_WeaponData()
        {
            var rows = CSVDiffCore.Hdr("staticID","weaponName","description",
                                       "baseGrade","weaponType","basePrice","iconPath","gearRightIndex","inGamePath");
            foreach (var d in CSVDiffCore.All<WeaponData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.weaponName, d.description,
                           d.baseGrade.ToString(), d.weaponType.ToString(),
                           d.basePrice.ToString(), CSVDiffCore.SPath(d.icon),
                           d.gearRightIndex.ToString(), CSVDiffCore.SPath(d.inGame)));
            return rows;
        }

        private List<string[]> BuildRows_AdventurerData()
        {
            var rows = CSVDiffCore.Hdr(
                "staticID","adventurerName","isNamed","trait","gender",
                "strMin","strMax","dexMin","dexMax",
                "intMin","intMax","lukMin","lukMax",
                "skinColorR","skinColorG","skinColorB",
                "hairColorR","hairColorG","hairColorB",
                "beardColorR","beardColorG","beardColorB",
                "browColorR","browColorG","browColorB");
            foreach (var d in CSVDiffCore.All<AdventurerData>())
            {
                var a = d.appearance;
                rows.Add(CSVDiffCore.R(
                    d.StaticID, d.adventurerName,
                    d.isNamed.ToString(), d.trait.ToString(), d.gender.ToString(),
                    d.strRange.x.ToString(), d.strRange.y.ToString(),
                    d.dexRange.x.ToString(), d.dexRange.y.ToString(),
                    d.intRange.x.ToString(), d.intRange.y.ToString(),
                    d.lukRange.x.ToString(), d.lukRange.y.ToString(),
                    a.skinColor.r.ToString("F3"),   a.skinColor.g.ToString("F3"),   a.skinColor.b.ToString("F3"),
                    a.hairColor.r.ToString("F3"),   a.hairColor.g.ToString("F3"),   a.hairColor.b.ToString("F3"),
                    a.beardColor.r.ToString("F3"),  a.beardColor.g.ToString("F3"),  a.beardColor.b.ToString("F3"),
                    a.browColor.r.ToString("F3"),   a.browColor.g.ToString("F3"),   a.browColor.b.ToString("F3")));
            }
            return rows;
        }

        private List<string[]> BuildRows_AdventurerFixedParts()
        {
            var rows = CSVDiffCore.Hdr("adventurerID","partsType","index");
            foreach (var d in CSVDiffCore.All<AdventurerData>())
                if (d.appearance?.partsIndices != null)
                    foreach (var entry in d.appearance.partsIndices)
                        rows.Add(CSVDiffCore.R(d.StaticID, entry.partsType.ToString(), entry.index.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_MaterialData()
        {
            var rows = CSVDiffCore.Hdr("staticID","materialName","description",
                                       "grade","materialType","baseValue","buyPrice","iconPath");
            foreach (var d in CSVDiffCore.All<MaterialData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.materialName, d.description,
                           d.grade.ToString(), d.materialType.ToString(),
                           d.baseValue.ToString(), d.buyPrice.ToString(),
                           CSVDiffCore.SPath(d.icon)));
            return rows;
        }

        private List<string[]> BuildRows_DungeonEventData()
        {
            var rows = CSVDiffCore.Hdr("staticID","eventType","probability","description",
                                       "summary","intervalHours","difficultyMultiplier",
                                       "monsterPrefabPath","monsterType",
                                       "propSpritePath","particlePrefabPath",
                                       "resultSuccessIconPath","resultFailIconPath");
            foreach (var d in CSVDiffCore.All<DungeonEventData>())
            {
                var battle = d as DungeonBattleEventData;
                var nb = d as DungeonNonBattleEventData;
                rows.Add(CSVDiffCore.R(d.StaticID, d.eventType.ToString(),
                           d.probability.ToString(), d.description, d.summary,
                           d.intervalHours.ToString(), d.difficultyMultiplier.ToString(),
                           CSVDiffCore.GPath(battle?.monsterPrefab),
                           battle != null ? battle.monsterType.ToString() : "",
                           CSVDiffCore.SPath(nb?.propSprite), CSVDiffCore.GPath(nb?.particlePrefab),
                           CSVDiffCore.SPath(nb?.resultSuccessIcon), CSVDiffCore.SPath(nb?.resultFailIcon)));
            }
            return rows;
        }

        private List<string[]> BuildRows_WeaponEffectData()
        {
            var rows = CSVDiffCore.Hdr("staticID","grade","effectType",
                                       "baseValueMin","baseValueMax",
                                       "targetStat","targetGrade","targetArmorType",
                                       "targetThreshold","weight");
            foreach (var d in CSVDiffCore.All<WeaponEffectData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.grade.ToString(), d.effectType.ToString(),
                           d.baseValueRange.x.ToString(), d.baseValueRange.y.ToString(),
                           d.targetStat.ToString(), d.targetGrade.ToString(),
                           d.targetArmorType.ToString(), d.targetThreshold.ToString(),
                           d.weight.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_ActiveItemData()
        {
            var rows = CSVDiffCore.Hdr("staticID","itemName","description","itemType",
                                       "usageContext","effectValue","iconPath");
            foreach (var d in CSVDiffCore.All<ActiveItemData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.itemName, d.description,
                           d.itemType.ToString(), d.usageContext.ToString(),
                           d.effectValue.ToString(), CSVDiffCore.SPath(d.icon)));
            return rows;
        }

        private List<string[]> BuildRows_VisitorEventData()
        {
            var rows = CSVDiffCore.Hdr("staticID","eventName","description",
                                       "morningEventType","appearanceID");
            foreach (var d in CSVDiffCore.All<VisitorEventData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.eventName, d.description,
                           d.morningEventType.ToString(), d.appearance?.StaticID ?? ""));
            return rows;
        }

        private List<string[]> BuildRows_DungeonData()
        {
            var rows = CSVDiffCore.Hdr("staticID","dungeonName","grade","armorType",
                                       "baseStatThreshold","baseRewardMin","baseRewardMax",
                                       "baseDuration","questWeight",
                                       "specialDropMaterialID","enforceDropCount","dungeonIconPath","mapBackgroundPath");
            foreach (var d in CSVDiffCore.All<DungeonData>())
            {
                string specID = d.specialDropMaterial?.StaticID ?? "";
                rows.Add(CSVDiffCore.R(d.StaticID, d.dungeonName,
                           d.grade.ToString(), d.armorType.ToString(),
                           d.baseStatThreshold.ToString(),
                           d.baseRewardMin.ToString(), d.baseRewardMax.ToString(),
                           d.baseDuration.ToString(), d.questWeight.ToString(),
                           specID, d.enforceDropCount.ToString(),
                           CSVDiffCore.SPath(d.dungeonIcon), CSVDiffCore.SPath(d.mapBackground)));
            }
            return rows;
        }

        private List<string[]> BuildRows_DungeonArmorVariants()
        {
            var rows = CSVDiffCore.Hdr("dungeonID","armorType","weight");
            foreach (var d in CSVDiffCore.All<DungeonData>())
                if (d.armorTypeVariants != null)
                    foreach (var v in d.armorTypeVariants)
                        rows.Add(CSVDiffCore.R(d.StaticID, v.armorType.ToString(), v.weight.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_DungeonDropMaterials()
        {
            var rows = CSVDiffCore.Hdr("dungeonID","materialID");
            foreach (var d in CSVDiffCore.All<DungeonData>())
                if (d.dropMaterials != null)
                    foreach (var m in d.dropMaterials)
                        if (m != null) rows.Add(CSVDiffCore.R(d.StaticID, m.StaticID));
            return rows;
        }

        private List<string[]> BuildRows_DungeonEventPool()
        {
            var rows = CSVDiffCore.Hdr("dungeonID","eventID");
            foreach (var d in CSVDiffCore.All<DungeonData>())
                if (d.eventPool != null)
                    foreach (var e in d.eventPool)
                        if (e != null) rows.Add(CSVDiffCore.R(d.StaticID, e.StaticID));
            return rows;
        }

        private List<string[]> BuildRows_WeaponRecipeData()
        {
            var rows = CSVDiffCore.Hdr("staticID","resultWeaponID","requiredGold",
                                       "craftingTime","unlockedDay");
            foreach (var d in CSVDiffCore.All<WeaponRecipeData>())
            {
                string resultID = d.resultWeapon?.StaticID ?? "";
                rows.Add(CSVDiffCore.R(d.StaticID, resultID,
                           d.requiredGold.ToString(), d.craftingTime.ToString(),
                           d.unlockedDay.ToString()));
            }
            return rows;
        }

        private List<string[]> BuildRows_WeaponRecipeMaterials()
        {
            var rows = CSVDiffCore.Hdr("recipeID","materialID","count");
            foreach (var d in CSVDiffCore.All<WeaponRecipeData>())
                if (d.requiredMaterials != null)
                    foreach (var req in d.requiredMaterials)
                        if (req?.material != null)
                            rows.Add(CSVDiffCore.R(d.StaticID,
                                       req.material.StaticID, req.count.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_ActiveItemRecipeData()
        {
            var rows = CSVDiffCore.Hdr("staticID","resultItemID","requiredGold",
                                       "craftingTime","unlockedDay");
            foreach (var d in CSVDiffCore.All<ActiveItemRecipeData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.resultItem?.StaticID ?? "",
                           d.requiredGold.ToString(), d.craftingTime.ToString(),
                           d.unlockedDay.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_ActiveItemRecipeMaterials()
        {
            var rows = CSVDiffCore.Hdr("recipeID","materialID","count");
            foreach (var d in CSVDiffCore.All<ActiveItemRecipeData>())
                if (d.requiredMaterials != null)
                    foreach (var req in d.requiredMaterials)
                        if (req?.material != null)
                            rows.Add(CSVDiffCore.R(d.StaticID,
                                       req.material.StaticID, req.count.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_WeeklyQuestData()
        {
            var rows = CSVDiffCore.Hdr("staticID","questTitle","description","weekNumber",
                                       "goldReward","reputationReward","insightReward",
                                       "weeklyFine","reputationPenalty","difficulty");
            foreach (var d in CSVDiffCore.All<WeeklyQuestData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.questTitle, d.description,
                           d.weekNumber.ToString(), d.goldReward.ToString(),
                           d.reputationReward.ToString(), d.insightReward.ToString(),
                           d.weeklyFine.ToString(), d.reputationPenalty.ToString(),
                           d.difficulty.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_WeeklyQuestRequirements()
        {
            var rows = CSVDiffCore.Hdr("questID","questType","targetCount","minGrade",
                                       "specificWeaponType","specificDungeonID","requirementText");
            foreach (var d in CSVDiffCore.All<WeeklyQuestData>())
                if (d.requirements != null)
                    foreach (var req in d.requirements)
                        if (req != null)
                            rows.Add(CSVDiffCore.R(d.StaticID,
                                       req.questType.ToString(), req.targetCount.ToString(),
                                       req.minGrade.ToString(), req.specificWeaponType.ToString(),
                                       req.specificDungeonID ?? "", req.requirementText ?? ""));
            return rows;
        }

        private List<string[]> BuildRows_FixedAppearanceData()
        {
            var rows = CSVDiffCore.Hdr("staticID",
                                       "skinColorR","skinColorG","skinColorB",
                                       "hairColorR","hairColorG","hairColorB",
                                       "beardColorR","beardColorG","beardColorB",
                                       "browColorR","browColorG","browColorB");
            foreach (var d in CSVDiffCore.All<FixedAppearanceData>())
            {
                var a = d.appearance;
                rows.Add(CSVDiffCore.R(d.StaticID,
                    a.skinColor.r.ToString("F3"),  a.skinColor.g.ToString("F3"),  a.skinColor.b.ToString("F3"),
                    a.hairColor.r.ToString("F3"),  a.hairColor.g.ToString("F3"),  a.hairColor.b.ToString("F3"),
                    a.beardColor.r.ToString("F3"), a.beardColor.g.ToString("F3"), a.beardColor.b.ToString("F3"),
                    a.browColor.r.ToString("F3"),  a.browColor.g.ToString("F3"),  a.browColor.b.ToString("F3")));
            }
            return rows;
        }

        private List<string[]> BuildRows_FixedAppearanceParts()
        {
            var rows = CSVDiffCore.Hdr("appearanceID","partsType","index");
            foreach (var d in CSVDiffCore.All<FixedAppearanceData>())
                if (d.appearance?.partsIndices != null)
                    foreach (var entry in d.appearance.partsIndices)
                        rows.Add(CSVDiffCore.R(d.StaticID, entry.partsType.ToString(), entry.index.ToString()));
            return rows;
        }

        private List<string[]> BuildRows_DialogueData()
        {
            var rows = CSVDiffCore.Hdr("staticID","dialogueName","isSkippable",
                                       "defaultSpeakerName");
            foreach (var d in CSVDiffCore.All<DialogueData>())
                rows.Add(CSVDiffCore.R(d.StaticID, d.dialogueName ?? "",
                           d.isSkippable.ToString(),
                           d.defaultSpeakerName ?? ""));
            return rows;
        }

        private List<string[]> BuildRows_DialogueNodes()
        {
            var rows = CSVDiffCore.Hdr("dialogueID","nodeIndex","nodeType","dialogueText",
                                       "speakerName","nextNodeIndex");
            foreach (var d in CSVDiffCore.All<DialogueData>())
            {
                if (d.nodes == null) continue;
                for (int i = 0; i < d.nodes.Count; i++)
                {
                    var n = d.nodes[i];
                    if (n == null) continue;
                    rows.Add(CSVDiffCore.R(d.StaticID, i.ToString(),
                               n.nodeType.ToString(), n.dialogueText ?? "",
                               n.speakerName ?? "",
                               n.nextNodeIndex.ToString()));
                }
            }
            return rows;
        }

        private List<string[]> BuildRows_DialogueChoices()
        {
            var rows = CSVDiffCore.Hdr("dialogueID","nodeIndex","choiceIndex","choiceText",
                                       "nextNodeIndex","requireGoldCheck","requiredGold",
                                       "requireItemCheck","requiredItemID",
                                       "giveGold","goldAmount","giveItem","itemID","itemCount");
            foreach (var d in CSVDiffCore.All<DialogueData>())
            {
                if (d.nodes == null) continue;
                for (int ni = 0; ni < d.nodes.Count; ni++)
                {
                    var n = d.nodes[ni];
                    if (n?.choices == null) continue;
                    for (int ci = 0; ci < n.choices.Count; ci++)
                    {
                        var c = n.choices[ci];
                        if (c == null) continue;
                        rows.Add(CSVDiffCore.R(d.StaticID, ni.ToString(), ci.ToString(),
                                   c.choiceText ?? "", c.nextNodeIndex.ToString(),
                                   c.requireGoldCheck.ToString(), c.requiredGold.ToString(),
                                   c.requireItemCheck.ToString(), c.requiredItemID ?? "",
                                   c.giveGold.ToString(), c.goldAmount.ToString(),
                                   c.giveItem.ToString(), c.itemID ?? "",
                                   c.itemCount.ToString()));
                    }
                }
            }
            return rows;
        }

        #endregion

        // ─── 경로 유틸 ────────────────────────────────────────────────
        private string CsvPath(string name)
            => Path.Combine(CsvFolder, $"{name}.csv").Replace("\\", "/");

        private string PreviewPath(string name)
            => Path.Combine(PreviewFolder, $"{name}_preview.csv").Replace("\\", "/");
    }
}
#endif
