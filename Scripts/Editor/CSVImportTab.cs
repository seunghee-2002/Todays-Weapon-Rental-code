// Assets/_Projects/Scripts/Editor/CSVTools/CSVImportTab.cs
// CSV → SO 변환 결과를 메모리에서 미리 계산 → diff 확인 → 승인 시 SO 반영

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LayerLab.ArtMaker;

namespace TodaysWeaponRental.Editor
{
    public class CSVImportTab
    {
        // ─── 경로 (CSVToolWindow 에서 주입) ──────────────────────────
        public string CsvFolder;
        public string LogFolder;
        public string SoWeapon, SoAdventurer, SoMaterial, SoDungeonEvent;
        public string SoWeaponEffect, SoActiveItem, SoVisitorEvent, SoDungeon;
        public string SoWeaponRecipe, SoActiveRecipe, SoWeeklyQuest;
        public string SoFixedAppearance, SoDialogue;

        // ─── 상태 ─────────────────────────────────────────────────────
        private List<DiffRow>                   diffRows      = new List<DiffRow>();
        private DiffFilter                      filter        = new DiffFilter();
        private Vector2                         scroll;
        private bool                            hasDiff;

        // 메모리 diff용: CSV에서 읽은 신규 값을 SO 현재 값과 비교
        // key = staticID, value = 컬럼명→값 딕셔너리
        private Dictionary<string, Dictionary<string, string>> pendingChanges
            = new Dictionary<string, Dictionary<string, string>>();

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

        // 행 단위 적용용: 파일별로 "적용 제외할 키"(미선택 추가/수정 행)
        private Dictionary<string, HashSet<string>> excludedKeys
            = new Dictionary<string, HashSet<string>>();

        // ─── GUI ──────────────────────────────────────────────────────
        public void OnGUI()
        {
            EditorGUILayout.LabelField("CSV → SO  변환 미리보기 + 적용", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            CSVDiffCore.DrawTypeCheckboxes(TypeNames, typeSelected);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("① 변환 결과 미리보기  (diff 계산)", GUILayout.Height(28)))
                ComputeDiff();

            EditorGUILayout.Space(2);

            GUI.enabled = hasDiff && diffRows.Count > 0;
            if (GUILayout.Button("② 로그 저장  (.log)", GUILayout.Height(24)))
                CSVDiffCore.SaveLog(diffRows, LogFolder, "선택 타입", "CSV→SO");
            GUI.enabled = true;

            EditorGUILayout.Space(2);

            GUI.enabled = hasDiff;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = hasDiff ? new Color(0.8f, 0.2f, 0.2f) : Color.gray;
            if (GUILayout.Button("③ SO 반영  (승인 시 실제 변경)", GUILayout.Height(28)))
                ApplyToSO();
            GUI.backgroundColor = prevBg;
            GUI.enabled = true;

            EditorGUILayout.Space(8);

            if (diffRows.Count > 0)
            {
                EditorGUILayout.LabelField("[ Diff 결과 ]", EditorStyles.boldLabel);
                CSVDiffCore.DrawFilterBar(filter, diffRows);
                EditorGUILayout.Space(4);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            CSVDiffCore.DrawDiffRows(diffRows, filter, selectable: true);
            EditorGUILayout.EndScrollView();
        }

        // ─── diff 계산 (SO 현재값 → rows, CSV → rows 비교) ───────────

        #region diff 계산

        // 해당 타입이 체크되었는지
        private bool Want(string name)
        {
            int idx = Array.IndexOf(TypeNames, name);
            return idx > 0 && typeSelected[idx];
        }

        private void ComputeDiff()
        {
            diffRows.Clear();
            pendingChanges.Clear();

            if (Want("WeaponData"))           Diff_WeaponData();
            if (Want("AdventurerData"))       Diff_AdventurerData();
            if (Want("MaterialData"))         Diff_MaterialData();
            if (Want("DungeonEventData"))     Diff_DungeonEventData();
            if (Want("WeaponEffectData"))     Diff_WeaponEffectData();
            if (Want("ActiveItemData"))       Diff_ActiveItemData();
            if (Want("VisitorEventData"))     Diff_VisitorEventData();
            if (Want("DungeonData"))          Diff_DungeonData();
            if (Want("WeaponRecipeData"))     Diff_WeaponRecipeData();
            if (Want("ActiveItemRecipeData")) Diff_ActiveItemRecipeData();
            if (Want("WeeklyQuestData"))      Diff_WeeklyQuestData();
            if (Want("FixedAppearanceData"))  Diff_FixedAppearanceData();
            if (Want("DialogueData"))         Diff_DialogueData();

            hasDiff = true;
            var (a, r, m, u) = CSVDiffCore.Summary(diffRows);
            Debug.Log($"[CSVImportTab] diff 계산 완료 — 추가:{a} 삭제:{r} 수정:{m} 동일:{u}");
        }

        // CSV에서 읽은 rows와 SO 현재값으로 만든 rows를 비교
        private void DiffFile(string csvName,
                              List<string[]> soRows,
                              List<string[]> csvRows)
        {
            if (csvRows == null)
            {
                Debug.LogWarning($"[CSVImportTab] CSV 없음: {CsvPath(csvName)}");
                return;
            }
            diffRows.AddRange(CSVDiffCore.Compute(csvName, soRows, csvRows));
        }

        #endregion

        #region 개별 diff 메서드

        private void Diff_WeaponData()
        {
            var soRows = CSVDiffCore.Hdr("staticID","weaponName","description",
                                         "baseGrade","weaponType","basePrice","iconPath","gearRightIndex","inGamePath");
            foreach (var d in CSVDiffCore.All<WeaponData>())
                soRows.Add(CSVDiffCore.R(d.StaticID, d.weaponName, d.description,
                           d.baseGrade.ToString(), d.weaponType.ToString(),
                           d.basePrice.ToString(), CSVDiffCore.SPath(d.icon),
                           d.gearRightIndex.ToString(), CSVDiffCore.SPath(d.inGame)));
            DiffFile("WeaponData", soRows, ReadCsv("WeaponData"));
        }

        private void Diff_AdventurerData()
        {
            var soMain = CSVDiffCore.Hdr(
                "staticID","adventurerName","isNamed","trait","gender",
                "strMin","strMax","dexMin","dexMax",
                "intMin","intMax","lukMin","lukMax",
                "skinColorR","skinColorG","skinColorB",
                "hairColorR","hairColorG","hairColorB",
                "beardColorR","beardColorG","beardColorB",
                "browColorR","browColorG","browColorB");
            var soParts = CSVDiffCore.Hdr("adventurerID","partsType","index");

            foreach (var d in CSVDiffCore.All<AdventurerData>())
            {
                var a = d.appearance;
                soMain.Add(CSVDiffCore.R(
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

                if (d.appearance?.partsIndices != null)
                    foreach (var entry in d.appearance.partsIndices)
                        soParts.Add(CSVDiffCore.R(d.StaticID, entry.partsType.ToString(), entry.index.ToString()));
            }

            DiffFile("AdventurerData",            soMain,  ReadCsv("AdventurerData"));
            DiffFile("AdventurerData_FixedParts", soParts, ReadCsv("AdventurerData_FixedParts"));
        }

        private void Diff_MaterialData()
        {
            var soRows = CSVDiffCore.Hdr("staticID","materialName","description",
                                         "grade","materialType","baseValue","buyPrice","iconPath");
            foreach (var d in CSVDiffCore.All<MaterialData>())
                soRows.Add(CSVDiffCore.R(d.StaticID, d.materialName, d.description,
                           d.grade.ToString(), d.materialType.ToString(),
                           d.baseValue.ToString(), d.buyPrice.ToString(),
                           CSVDiffCore.SPath(d.icon)));
            DiffFile("MaterialData", soRows, ReadCsv("MaterialData"));
        }

        private void Diff_DungeonEventData()
        {
            var soRows = CSVDiffCore.Hdr("staticID","eventType","probability","description",
                                         "summary","intervalHours","difficultyMultiplier",
                                         "monsterPrefabPath","monsterType",
                                         "propSpritePath","particlePrefabPath",
                                         "resultSuccessIconPath","resultFailIconPath");
            foreach (var d in CSVDiffCore.All<DungeonEventData>())
            {
                var battle = d as DungeonBattleEventData;
                var nb = d as DungeonNonBattleEventData;
                soRows.Add(CSVDiffCore.R(d.StaticID, d.eventType.ToString(),
                           d.probability.ToString(), d.description, d.summary,
                           d.intervalHours.ToString(), d.difficultyMultiplier.ToString(),
                           CSVDiffCore.GPath(battle?.monsterPrefab),
                           battle != null ? battle.monsterType.ToString() : "",
                           CSVDiffCore.SPath(nb?.propSprite), CSVDiffCore.GPath(nb?.particlePrefab),
                           CSVDiffCore.SPath(nb?.resultSuccessIcon), CSVDiffCore.SPath(nb?.resultFailIcon)));
            }
            DiffFile("DungeonEventData", soRows, ReadCsv("DungeonEventData"));
        }

        private void Diff_WeaponEffectData()
        {
            var soRows = CSVDiffCore.Hdr("staticID","grade","effectType",
                                         "baseValueMin","baseValueMax",
                                         "targetStat","targetGrade","targetArmorType",
                                         "targetThreshold","weight");
            foreach (var d in CSVDiffCore.All<WeaponEffectData>())
                soRows.Add(CSVDiffCore.R(d.StaticID, d.grade.ToString(), d.effectType.ToString(),
                           d.baseValueRange.x.ToString(), d.baseValueRange.y.ToString(),
                           d.targetStat.ToString(), d.targetGrade.ToString(),
                           d.targetArmorType.ToString(), d.targetThreshold.ToString(),
                           d.weight.ToString()));
            DiffFile("WeaponEffectData", soRows, ReadCsv("WeaponEffectData"));
        }

        private void Diff_ActiveItemData()
        {
            var soRows = CSVDiffCore.Hdr("staticID","itemName","description","itemType",
                                         "usageContext","effectValue","iconPath");
            foreach (var d in CSVDiffCore.All<ActiveItemData>())
                soRows.Add(CSVDiffCore.R(d.StaticID, d.itemName, d.description,
                           d.itemType.ToString(), d.usageContext.ToString(),
                           d.effectValue.ToString(), CSVDiffCore.SPath(d.icon)));
            DiffFile("ActiveItemData", soRows, ReadCsv("ActiveItemData"));
        }

        private void Diff_VisitorEventData()
        {
            var soRows = CSVDiffCore.Hdr("staticID","eventName","description",
                                         "morningEventType","appearanceID");
            foreach (var d in CSVDiffCore.All<VisitorEventData>())
                soRows.Add(CSVDiffCore.R(d.StaticID, d.eventName, d.description,
                           d.morningEventType.ToString(), d.appearance?.StaticID ?? ""));
            DiffFile("VisitorEventData", soRows, ReadCsv("VisitorEventData"));
        }

        private void Diff_DungeonData()
        {
            var soMain     = CSVDiffCore.Hdr("staticID","dungeonName","grade","armorType",
                                             "baseStatThreshold","baseRewardMin","baseRewardMax",
                                             "baseDuration","questWeight",
                                             "specialDropMaterialID","enforceDropCount","dungeonIconPath","mapBackgroundPath");
            var soVariants = CSVDiffCore.Hdr("dungeonID","armorType","weight");
            var soDrop     = CSVDiffCore.Hdr("dungeonID","materialID");
            var soPool     = CSVDiffCore.Hdr("dungeonID","eventID");

            foreach (var d in CSVDiffCore.All<DungeonData>())
            {
                soMain.Add(CSVDiffCore.R(d.StaticID, d.dungeonName,
                           d.grade.ToString(), d.armorType.ToString(),
                           d.baseStatThreshold.ToString(),
                           d.baseRewardMin.ToString(), d.baseRewardMax.ToString(),
                           d.baseDuration.ToString(), d.questWeight.ToString(),
                           d.specialDropMaterial?.StaticID ?? "",
                           d.enforceDropCount.ToString(),
                           CSVDiffCore.SPath(d.dungeonIcon),
                           CSVDiffCore.SPath(d.mapBackground)));

                if (d.armorTypeVariants != null)
                    foreach (var v in d.armorTypeVariants)
                        soVariants.Add(CSVDiffCore.R(d.StaticID, v.armorType.ToString(), v.weight.ToString()));

                if (d.dropMaterials != null)
                    foreach (var m in d.dropMaterials)
                        if (m != null) soDrop.Add(CSVDiffCore.R(d.StaticID, m.StaticID));

                if (d.eventPool != null)
                    foreach (var e in d.eventPool)
                        if (e != null) soPool.Add(CSVDiffCore.R(d.StaticID, e.StaticID));
            }

            DiffFile("DungeonData",                soMain,     ReadCsv("DungeonData"));
            DiffFile("DungeonData_ArmorVariants",  soVariants, ReadCsv("DungeonData_ArmorVariants"));
            DiffFile("DungeonData_DropMaterials",  soDrop,     ReadCsv("DungeonData_DropMaterials"));
            DiffFile("DungeonData_EventPool",      soPool,     ReadCsv("DungeonData_EventPool"));
        }

        private void Diff_WeaponRecipeData()
        {
            var soMain = CSVDiffCore.Hdr("staticID","resultWeaponID","requiredGold",
                                         "craftingTime","unlockedDay");
            var soMats = CSVDiffCore.Hdr("recipeID","materialID","count");

            foreach (var d in CSVDiffCore.All<WeaponRecipeData>())
            {
                soMain.Add(CSVDiffCore.R(d.StaticID, d.resultWeapon?.StaticID ?? "",
                           d.requiredGold.ToString(), d.craftingTime.ToString(),
                           d.unlockedDay.ToString()));

                if (d.requiredMaterials != null)
                    foreach (var req in d.requiredMaterials)
                        if (req?.material != null)
                            soMats.Add(CSVDiffCore.R(d.StaticID,
                                       req.material.StaticID, req.count.ToString()));
            }

            DiffFile("WeaponRecipeData",          soMain, ReadCsv("WeaponRecipeData"));
            DiffFile("WeaponRecipeData_Materials", soMats, ReadCsv("WeaponRecipeData_Materials"));
        }

        private void Diff_ActiveItemRecipeData()
        {
            var soMain = CSVDiffCore.Hdr("staticID","resultItemID","requiredGold",
                                         "craftingTime","unlockedDay");
            var soMats = CSVDiffCore.Hdr("recipeID","materialID","count");

            foreach (var d in CSVDiffCore.All<ActiveItemRecipeData>())
            {
                soMain.Add(CSVDiffCore.R(d.StaticID, d.resultItem?.StaticID ?? "",
                           d.requiredGold.ToString(), d.craftingTime.ToString(),
                           d.unlockedDay.ToString()));

                if (d.requiredMaterials != null)
                    foreach (var req in d.requiredMaterials)
                        if (req?.material != null)
                            soMats.Add(CSVDiffCore.R(d.StaticID,
                                       req.material.StaticID, req.count.ToString()));
            }

            DiffFile("ActiveItemRecipeData",          soMain, ReadCsv("ActiveItemRecipeData"));
            DiffFile("ActiveItemRecipeData_Materials", soMats, ReadCsv("ActiveItemRecipeData_Materials"));
        }

        private void Diff_WeeklyQuestData()
        {
            var soMain = CSVDiffCore.Hdr("staticID","questTitle","description","weekNumber",
                                         "goldReward","reputationReward","insightReward",
                                         "weeklyFine","reputationPenalty","difficulty");
            var soReqs = CSVDiffCore.Hdr("questID","questType","targetCount","minGrade",
                                         "specificWeaponType","specificDungeonID","requirementText");

            foreach (var d in CSVDiffCore.All<WeeklyQuestData>())
            {
                soMain.Add(CSVDiffCore.R(d.StaticID, d.questTitle, d.description,
                           d.weekNumber.ToString(), d.goldReward.ToString(),
                           d.reputationReward.ToString(), d.insightReward.ToString(),
                           d.weeklyFine.ToString(), d.reputationPenalty.ToString(),
                           d.difficulty.ToString()));

                if (d.requirements != null)
                    foreach (var req in d.requirements)
                        if (req != null)
                            soReqs.Add(CSVDiffCore.R(d.StaticID,
                                       req.questType.ToString(), req.targetCount.ToString(),
                                       req.minGrade.ToString(), req.specificWeaponType.ToString(),
                                       req.specificDungeonID ?? "", req.requirementText ?? ""));
            }

            DiffFile("WeeklyQuestData",             soMain, ReadCsv("WeeklyQuestData"));
            DiffFile("WeeklyQuestData_Requirements", soReqs, ReadCsv("WeeklyQuestData_Requirements"));
        }

        private void Diff_FixedAppearanceData()
        {
            var soMain  = CSVDiffCore.Hdr("staticID",
                                          "skinColorR","skinColorG","skinColorB",
                                          "hairColorR","hairColorG","hairColorB",
                                          "beardColorR","beardColorG","beardColorB",
                                          "browColorR","browColorG","browColorB");
            var soParts = CSVDiffCore.Hdr("appearanceID","partsType","index");

            foreach (var d in CSVDiffCore.All<FixedAppearanceData>())
            {
                var a = d.appearance;
                soMain.Add(CSVDiffCore.R(d.StaticID,
                    a.skinColor.r.ToString("F3"),  a.skinColor.g.ToString("F3"),  a.skinColor.b.ToString("F3"),
                    a.hairColor.r.ToString("F3"),  a.hairColor.g.ToString("F3"),  a.hairColor.b.ToString("F3"),
                    a.beardColor.r.ToString("F3"), a.beardColor.g.ToString("F3"), a.beardColor.b.ToString("F3"),
                    a.browColor.r.ToString("F3"),  a.browColor.g.ToString("F3"),  a.browColor.b.ToString("F3")));

                if (d.appearance?.partsIndices != null)
                    foreach (var entry in d.appearance.partsIndices)
                        soParts.Add(CSVDiffCore.R(d.StaticID, entry.partsType.ToString(), entry.index.ToString()));
            }

            DiffFile("FixedAppearanceData",       soMain,  ReadCsv("FixedAppearanceData"));
            DiffFile("FixedAppearanceData_Parts", soParts, ReadCsv("FixedAppearanceData_Parts"));
        }

        private void Diff_DialogueData()
        {
            var soMain    = CSVDiffCore.Hdr("staticID","dialogueName","isSkippable",
                                            "defaultSpeakerName");
            var soNodes   = CSVDiffCore.Hdr("dialogueID","nodeIndex","nodeType","dialogueText",
                                            "speakerName","nextNodeIndex");
            var soChoices = CSVDiffCore.Hdr("dialogueID","nodeIndex","choiceIndex","choiceText",
                                            "nextNodeIndex","requireGoldCheck","requiredGold",
                                            "requireItemCheck","requiredItemID",
                                            "giveGold","goldAmount","giveItem","itemID","itemCount");

            foreach (var d in CSVDiffCore.All<DialogueData>())
            {
                soMain.Add(CSVDiffCore.R(d.StaticID, d.dialogueName ?? "",
                           d.isSkippable.ToString(),
                           d.defaultSpeakerName ?? ""));

                if (d.nodes == null) continue;
                for (int i = 0; i < d.nodes.Count; i++)
                {
                    var n = d.nodes[i];
                    if (n == null) continue;
                    soNodes.Add(CSVDiffCore.R(d.StaticID, i.ToString(),
                               n.nodeType.ToString(), n.dialogueText ?? "",
                               n.speakerName ?? "",
                               n.nextNodeIndex.ToString()));

                    if (n.choices == null) continue;
                    for (int ci = 0; ci < n.choices.Count; ci++)
                    {
                        var c = n.choices[ci];
                        if (c == null) continue;
                        soChoices.Add(CSVDiffCore.R(d.StaticID, i.ToString(), ci.ToString(),
                                   c.choiceText ?? "", c.nextNodeIndex.ToString(),
                                   c.requireGoldCheck.ToString(), c.requiredGold.ToString(),
                                   c.requireItemCheck.ToString(), c.requiredItemID ?? "",
                                   c.giveGold.ToString(), c.goldAmount.ToString(),
                                   c.giveItem.ToString(), c.itemID ?? "",
                                   c.itemCount.ToString()));
                    }
                }
            }

            DiffFile("DialogueData",         soMain,    ReadCsv("DialogueData"));
            DiffFile("DialogueData_Nodes",   soNodes,   ReadCsv("DialogueData_Nodes"));
            DiffFile("DialogueData_Choices", soChoices, ReadCsv("DialogueData_Choices"));
        }

        #endregion

        // ─── SO 반영 ──────────────────────────────────────────────────

        #region SO 반영

        private void ApplyToSO()
        {
            int changed = diffRows.Count(r => r.Kind != DiffKind.Unchanged && r.Selected);
            bool confirm = EditorUtility.DisplayDialog(
                "SO 반영",
                $"체크된 CSV 행을 ScriptableObject에 반영합니다.\n" +
                $"선택된 변경 항목: {changed}건\n\n계속하시겠습니까?",
                "반영", "취소");
            if (!confirm) return;

            BuildExclusions();   // 미선택 추가/수정 행을 Read 에서 제외하도록 준비

            // import 순서 준수 (기존 SOCSVConverter 와 동일)
            if (Want("MaterialData"))         Apply_MaterialData();
            if (Want("DungeonEventData"))     Apply_DungeonEventData();
            if (Want("WeaponData"))           Apply_WeaponData();
            if (Want("ActiveItemData"))       Apply_ActiveItemData();
            if (Want("VisitorEventData"))     Apply_VisitorEventData();
            if (Want("WeaponEffectData"))     Apply_WeaponEffectData();
            if (Want("AdventurerData"))       Apply_AdventurerData();
            if (Want("DungeonData"))          Apply_DungeonData();
            if (Want("WeaponRecipeData"))     Apply_WeaponRecipeData();
            if (Want("ActiveItemRecipeData")) Apply_ActiveItemRecipeData();
            if (Want("WeeklyQuestData"))      Apply_WeeklyQuestData();
            if (Want("FixedAppearanceData"))  Apply_FixedAppearanceData();
            if (Want("DialogueData"))         Apply_DialogueData();

            DeleteSelectedRemovals();   // 체크된 [-]삭제 행의 메인 SO 에셋 삭제

            excludedKeys.Clear();
            diffRows.Clear();
            hasDiff = false;
            Debug.Log("[CSVImportTab] SO 반영 완료");
        }

        // 미선택(체크 해제)된 추가/수정 행의 키를 파일별로 모은다 → Read 에서 제외
        private void BuildExclusions()
        {
            excludedKeys.Clear();
            foreach (var d in diffRows)
            {
                if (d.Selected) continue;
                if (d.Kind != DiffKind.Added && d.Kind != DiffKind.Modified) continue;
                if (!excludedKeys.TryGetValue(d.FileName, out var set))
                    excludedKeys[d.FileName] = set = new HashSet<string>();
                set.Add(d.RowKey);
            }
        }

        // 체크된 [-]삭제 행에 해당하는 메인 테이블 SO 에셋을 실제 삭제
        private void DeleteSelectedRemovals()
        {
            int deleted = 0;
            deleted += DeleteRemoved<WeaponData>("WeaponData");
            deleted += DeleteRemoved<AdventurerData>("AdventurerData");
            deleted += DeleteRemoved<MaterialData>("MaterialData");
            deleted += DeleteRemoved<DungeonEventData>("DungeonEventData");
            deleted += DeleteRemoved<WeaponEffectData>("WeaponEffectData");
            deleted += DeleteRemoved<ActiveItemData>("ActiveItemData");
            deleted += DeleteRemoved<VisitorEventData>("VisitorEventData");
            deleted += DeleteRemoved<DungeonData>("DungeonData");
            deleted += DeleteRemoved<WeaponRecipeData>("WeaponRecipeData");
            deleted += DeleteRemoved<ActiveItemRecipeData>("ActiveItemRecipeData");
            deleted += DeleteRemoved<WeeklyQuestData>("WeeklyQuestData");
            deleted += DeleteRemoved<FixedAppearanceData>("FixedAppearanceData");
            deleted += DeleteRemoved<DialogueData>("DialogueData");

            if (deleted > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[CSVImportTab] SO 삭제 — {deleted}건");
            }
        }

        private int DeleteRemoved<T>(string fileName) where T : BaseData
        {
            if (!Want(fileName)) return 0;
            var keys = new HashSet<string>(diffRows
                .Where(d => d.FileName == fileName && d.Kind == DiffKind.Removed && d.Selected)
                .Select(d => d.RowKey));
            if (keys.Count == 0) return 0;

            int n = 0;
            foreach (var so in CSVDiffCore.All<T>())
            {
                if (!keys.Contains(so.StaticID)) continue;
                string path = AssetDatabase.GetAssetPath(so);
                if (AssetDatabase.DeleteAsset(path))
                {
                    Debug.Log($"[CSVImportTab] SO 삭제: {path}");
                    n++;
                }
            }
            return n;
        }

        // ─── 개별 Apply ───────────────────────────────────────────────

        private void Apply_WeaponData()
        {
            if (Validate("WeaponData", 9,
                new[]{"str","str","str","enum:Grade","enum:WeaponType","int","str","int","str"}) > 0) return;
            foreach (var r in Read("WeaponData", 9))
            {
                var so = Get<WeaponData>(r[0], SoWeapon);
                so.StaticID    = r[0]; so.weaponName  = r[1]; so.description = r[2];
                so.baseGrade   = E<Grade>(r[3]); so.weaponType = E<WeaponType>(r[4]);
                so.basePrice   = I(r[5]);
                if (r[6] != "") so.icon = Spr(r[6]);
                so.gearRightIndex = I(r[7]);
                if (r[8] != "") so.inGame = Spr(r[8]);
                Dirty(so);
            }
            Save();
        }

        private void Apply_AdventurerData()
        {
            // 메인 CSV: 25컬럼
            if (Validate("AdventurerData", 25,
                new[]{"str","str","bool","enum:TraitType","enum:Gender",
                    "int","int","int","int","int","int","int","int",
                    "float","float","float","float","float","float",
                    "float","float","float","float","float","float"}) > 0) return;

            foreach (var r in Read("AdventurerData", 25))
            {
                var so = Get<AdventurerData>(r[0], SoAdventurer);
                so.StaticID      = r[0]; so.adventurerName = r[1];
                so.isNamed       = bool.Parse(r[2]);
                so.trait         = E<TraitType>(r[3]);
                so.gender        = E<Gender>(r[4]);
                so.strRange      = new Vector2Int(I(r[5]),  I(r[6]));
                so.dexRange      = new Vector2Int(I(r[7]),  I(r[8]));
                so.intRange      = new Vector2Int(I(r[9]),  I(r[10]));
                so.lukRange      = new Vector2Int(I(r[11]), I(r[12]));
                so.appearance.skinColor   = new Color(F(r[13]), F(r[14]), F(r[15]));
                so.appearance.hairColor   = new Color(F(r[16]), F(r[17]), F(r[18]));
                so.appearance.beardColor  = new Color(F(r[19]), F(r[20]), F(r[21]));
                so.appearance.browColor   = new Color(F(r[22]), F(r[23]), F(r[24]));
                so.appearance.partsIndices = new List<PartsIndexEntry>(); // 서브 CSV에서 채움
                Dirty(so);
            }
            Save();

            // 서브 CSV: AdventurerData_FixedParts
            if (File.Exists(CsvPath("AdventurerData_FixedParts")))
            {
                var advMap   = CSVDiffCore.All<AdventurerData>().ToDictionary(a => a.StaticID);
                foreach (var r in Read("AdventurerData_FixedParts", 3))
                {
                    if (!advMap.TryGetValue(r[0], out var adv)) continue;
                    adv.appearance.partsIndices.Add(new PartsIndexEntry(E<PartsType>(r[1]), I(r[2])));
                    Dirty(adv);
                }
                Save();
            }
        }

        private void Apply_MaterialData()
        {
            if (Validate("MaterialData", 8,
                new[]{"str","str","str","enum:Grade","enum:MaterialType","int","int","str"}) > 0) return;
            foreach (var r in Read("MaterialData", 8))
            {
                var so = Get<MaterialData>(r[0], SoMaterial);
                so.StaticID = r[0]; so.materialName = r[1]; so.description = r[2];
                so.grade = E<Grade>(r[3]); so.materialType = E<MaterialType>(r[4]);
                so.baseValue = I(r[5]); so.buyPrice = I(r[6]);
                if (r[7] != "") so.icon = Spr(r[7]);
                Dirty(so);
            }
            Save();
        }

        private void Apply_DungeonEventData()
        {
            if (Validate("DungeonEventData", 13,
                new[]{"str","enum:DungeonEventType","float","str","str","float","float","str","enum:MonsterCategory","str","str","str","str"}) > 0) return;
            foreach (var r in Read("DungeonEventData", 13))
            {
                var type = E<DungeonEventType>(r[1]);
                bool isBattle = type == DungeonEventType.Battle || type == DungeonEventType.MiniBoss || type == DungeonEventType.Boss;
                if (isBattle)
                {
                    var so = Get<DungeonBattleEventData>(r[0], SoDungeonEvent);
                    so.StaticID = r[0]; so.eventType = type;
                    so.probability = F(r[2]); so.description = r[3]; so.summary = r[4];
                    so.intervalHours = F(r[5]); so.difficultyMultiplier = F(r[6]);
                    if (r[7] != "") so.monsterPrefab = Pf(r[7]);
                    if (r[8] != "") so.monsterType = E<MonsterCategory>(r[8]);
                    Dirty(so);
                }
                else
                {
                    var so = Get<DungeonNonBattleEventData>(r[0], SoDungeonEvent);
                    so.StaticID = r[0]; so.eventType = type;
                    so.probability = F(r[2]); so.description = r[3]; so.summary = r[4];
                    so.intervalHours = F(r[5]); so.difficultyMultiplier = F(r[6]);
                    if (r[9] != "") so.propSprite = Spr(r[9]);
                    if (r[10] != "") so.particlePrefab = Pf(r[10]);
                    if (r[11] != "") so.resultSuccessIcon = Spr(r[11]);
                    if (r[12] != "") so.resultFailIcon = Spr(r[12]);
                    Dirty(so);
                }
            }
            Save();
        }

        private void Apply_WeaponEffectData()
        {
            if (Validate("WeaponEffectData", 10,
                new[]{"str","enum:Grade","enum:WeaponEffectType","float","float","int","int","int","int","float"}) > 0) return;
            foreach (var r in Read("WeaponEffectData", 10))
            {
                var so = Get<WeaponEffectData>(r[0], SoWeaponEffect, "WED_");
                so.StaticID = r[0]; so.grade = E<Grade>(r[1]); so.effectType = E<WeaponEffectType>(r[2]);
                so.baseValueRange  = new Vector2(F(r[3]), F(r[4]));
                so.targetStat      = I(r[5]); so.targetGrade = I(r[6]);
                so.targetArmorType = I(r[7]); so.targetThreshold = I(r[8]);
                so.weight          = F(r[9]);
                Dirty(so);
            }
            Save();
        }

        private void Apply_ActiveItemData()
        {
            if (Validate("ActiveItemData", 7,
                new[]{"str","str","str","enum:ActiveItemType","enum:ActiveItemUsage","float","str"}) > 0) return;
            foreach (var r in Read("ActiveItemData", 7))
            {
                var so = Get<ActiveItemData>(r[0], SoActiveItem);
                so.StaticID = r[0]; so.itemName = r[1]; so.description = r[2];
                so.itemType = E<ActiveItemType>(r[3]); so.usageContext = E<ActiveItemUsage>(r[4]);
                so.effectValue = F(r[5]);
                if (r[6] != "") so.icon = Spr(r[6]);
                Dirty(so);
            }
            Save();
        }

        private void Apply_VisitorEventData()
        {
            if (Validate("VisitorEventData", 5,
                new[]{"str","str","str","enum:MorningEventType","str"}) > 0) return;
            var appearanceMap = CSVDiffCore.All<FixedAppearanceData>()
                .Where(a => !string.IsNullOrEmpty(a.StaticID))
                .GroupBy(a => a.StaticID)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var r in Read("VisitorEventData", 5))
            {
                var so = Get<VisitorEventData>(r[0], SoVisitorEvent);
                so.StaticID = r[0]; so.eventName = r[1]; so.description = r[2];
                so.morningEventType = E<MorningEventType>(r[3]);
                so.appearance = r[4] != "" && appearanceMap.TryGetValue(r[4], out var ap) ? ap : null;
                Dirty(so);
            }
            Save();
        }

        private void Apply_DungeonData()
        {
            if (Validate("DungeonData", 13,
                new[]{"str","str","enum:Grade","enum:ArmorType","int","int","int","float","float","str","int","str","str"}) > 0) return;
            var matMap = CSVDiffCore.All<MaterialData>().ToDictionary(m => m.StaticID);
            var evtMap = CSVDiffCore.All<DungeonEventData>().ToDictionary(e => e.StaticID);

            foreach (var r in Read("DungeonData", 13))
            {
                var so = Get<DungeonData>(r[0], SoDungeon);
                so.StaticID          = r[0]; so.dungeonName = r[1];
                so.grade             = E<Grade>(r[2]); so.armorType = E<ArmorType>(r[3]);
                so.baseStatThreshold = I(r[4]); so.baseRewardMin = I(r[5]);
                so.baseRewardMax     = I(r[6]); so.baseDuration  = F(r[7]);
                so.questWeight       = F(r[8]);
                so.specialDropMaterial = r[9] != "" && matMap.TryGetValue(r[9], out var sm) ? sm : null;
                so.enforceDropCount    = I(r[10]);
                if (r[11] != "") so.dungeonIcon = Spr(r[11]);
                if (r[12] != "") so.mapBackground = Spr(r[12]);
                so.armorTypeVariants = new List<ArmorTypeVariant>();
                so.dropMaterials     = new List<MaterialData>();
                so.eventPool         = new List<DungeonEventData>();
                Dirty(so);
            }
            Save();

            if (File.Exists(CsvPath("DungeonData_ArmorVariants")))
            {
                var dmap = CSVDiffCore.All<DungeonData>().ToDictionary(d => d.StaticID);
                foreach (var r in Read("DungeonData_ArmorVariants", 3))
                {
                    if (!dmap.TryGetValue(r[0], out var dun)) continue;
                    dun.armorTypeVariants.Add(new ArmorTypeVariant
                    {
                        armorType = E<ArmorType>(r[1]),
                        weight    = F(r[2])
                    });
                    Dirty(dun);
                }
                Save();
            }

            if (File.Exists(CsvPath("DungeonData_DropMaterials")))
            {
                var dmap = CSVDiffCore.All<DungeonData>().ToDictionary(d => d.StaticID);
                foreach (var r in Read("DungeonData_DropMaterials", 2))
                {
                    if (!dmap.TryGetValue(r[0], out var dun)) continue;
                    if (!matMap.TryGetValue(r[1], out var mat)) continue;
                    if (!dun.dropMaterials.Contains(mat)) dun.dropMaterials.Add(mat);
                    Dirty(dun);
                }
                Save();
            }

            if (File.Exists(CsvPath("DungeonData_EventPool")))
            {
                var dmap = CSVDiffCore.All<DungeonData>().ToDictionary(d => d.StaticID);
                foreach (var r in Read("DungeonData_EventPool", 2))
                {
                    if (!dmap.TryGetValue(r[0], out var dun)) continue;
                    if (!evtMap.TryGetValue(r[1], out var evt)) continue;
                    if (!dun.eventPool.Contains(evt)) dun.eventPool.Add(evt);
                    Dirty(dun);
                }
                Save();
            }
        }

        private void Apply_WeaponRecipeData()
        {
            if (Validate("WeaponRecipeData", 5,
                new[]{"str","str","int","int","int"}) > 0) return;
            var weaponMap = CSVDiffCore.All<WeaponData>().ToDictionary(w => w.StaticID);
            var matMap    = CSVDiffCore.All<MaterialData>().ToDictionary(m => m.StaticID);

            foreach (var r in Read("WeaponRecipeData", 5))
            {
                var so = Get<WeaponRecipeData>(r[0], SoWeaponRecipe);
                so.StaticID     = r[0];
                so.resultWeapon = r[1] != "" && weaponMap.TryGetValue(r[1], out var w) ? w : null;
                so.requiredGold = I(r[2]); so.craftingTime = I(r[3]); so.unlockedDay = I(r[4]);
                so.requiredMaterials = new List<MaterialRequirement>();
                Dirty(so);
            }
            Save();

            if (File.Exists(CsvPath("WeaponRecipeData_Materials")))
            {
                var recipeMap = CSVDiffCore.All<WeaponRecipeData>().ToDictionary(r => r.StaticID);
                foreach (var r in Read("WeaponRecipeData_Materials", 3))
                {
                    if (!recipeMap.TryGetValue(r[0], out var recipe)) continue;
                    if (!matMap.TryGetValue(r[1], out var mat)) continue;
                    recipe.requiredMaterials.Add(new MaterialRequirement { material = mat, count = I(r[2]) });
                    Dirty(recipe);
                }
                Save();
            }
        }

        private void Apply_ActiveItemRecipeData()
        {
            if (Validate("ActiveItemRecipeData", 5,
                new[]{"str","str","int","int","int"}) > 0) return;
            var itemMap = CSVDiffCore.All<ActiveItemData>().ToDictionary(i => i.StaticID);
            var matMap  = CSVDiffCore.All<MaterialData>().ToDictionary(m => m.StaticID);

            foreach (var r in Read("ActiveItemRecipeData", 5))
            {
                var so = Get<ActiveItemRecipeData>(r[0], SoActiveRecipe);
                so.StaticID   = r[0];
                so.resultItem = r[1] != "" && itemMap.TryGetValue(r[1], out var item) ? item : null;
                so.requiredGold = I(r[2]); so.craftingTime = I(r[3]); so.unlockedDay = I(r[4]);
                so.requiredMaterials = new List<MaterialRequirement>();
                Dirty(so);
            }
            Save();

            if (File.Exists(CsvPath("ActiveItemRecipeData_Materials")))
            {
                var recipeMap = CSVDiffCore.All<ActiveItemRecipeData>().ToDictionary(r => r.StaticID);
                foreach (var r in Read("ActiveItemRecipeData_Materials", 3))
                {
                    if (!recipeMap.TryGetValue(r[0], out var recipe)) continue;
                    if (!matMap.TryGetValue(r[1], out var mat)) continue;
                    recipe.requiredMaterials.Add(new MaterialRequirement { material = mat, count = I(r[2]) });
                    Dirty(recipe);
                }
                Save();
            }
        }

        private void Apply_WeeklyQuestData()
        {
            if (Validate("WeeklyQuestData", 10,
                new[]{"str","str","str","int","int","int","int","int","int","enum:QuestDifficulty"}) > 0) return;
            foreach (var r in Read("WeeklyQuestData", 10))
            {
                var so = Get<WeeklyQuestData>(r[0], SoWeeklyQuest);
                so.StaticID = r[0]; so.questTitle = r[1]; so.description = r[2];
                so.weekNumber = I(r[3]); so.goldReward = I(r[4]);
                so.reputationReward = I(r[5]); so.insightReward = I(r[6]);
                so.weeklyFine = I(r[7]); so.reputationPenalty = I(r[8]);
                so.difficulty = E<QuestDifficulty>(r[9]);
                so.requirements = new List<QuestRequirement>();
                Dirty(so);
            }
            Save();

            if (File.Exists(CsvPath("WeeklyQuestData_Requirements")))
            {
                var questMap = CSVDiffCore.All<WeeklyQuestData>().ToDictionary(q => q.StaticID);
                foreach (var r in Read("WeeklyQuestData_Requirements", 7))
                {
                    if (!questMap.TryGetValue(r[0], out var quest)) continue;
                    quest.requirements.Add(new QuestRequirement
                    {
                        questType          = E<QuestType>(r[1]),
                        targetCount        = I(r[2]),
                        minGrade           = E<Grade>(r[3]),
                        specificWeaponType = E<WeaponType>(r[4]),
                        specificDungeonID  = r[5],
                        requirementText    = r[6]
                    });
                    Dirty(quest);
                }
                Save();
            }
        }

        private void Apply_FixedAppearanceData()
        {
            if (Validate("FixedAppearanceData", 13,
                new[]{"str",
                      "float","float","float", "float","float","float",
                      "float","float","float", "float","float","float"}) > 0) return;

            foreach (var r in Read("FixedAppearanceData", 13))
            {
                var so = Get<FixedAppearanceData>(r[0], SoFixedAppearance);
                so.StaticID = r[0];
                so.appearance ??= new AdventurerAppearanceData();
                so.appearance.skinColor  = new Color(F(r[1]),  F(r[2]),  F(r[3]));
                so.appearance.hairColor  = new Color(F(r[4]),  F(r[5]),  F(r[6]));
                so.appearance.beardColor = new Color(F(r[7]),  F(r[8]),  F(r[9]));
                so.appearance.browColor  = new Color(F(r[10]), F(r[11]), F(r[12]));
                so.appearance.partsIndices = new List<PartsIndexEntry>();
                Dirty(so);
            }
            Save();

            if (File.Exists(CsvPath("FixedAppearanceData_Parts")))
            {
                // StaticID 중복(예: Player가 PlayerAppearance/DefaultAppearance 둘 다)에 견디도록 첫 항목 우선.
                // 메인 단계의 Get<>(FirstOrDefault)와 동일 의미.
                var fixMap = CSVDiffCore.All<FixedAppearanceData>()
                    .GroupBy(a => a.StaticID)
                    .ToDictionary(g => g.Key, g => g.First());
                foreach (var r in Read("FixedAppearanceData_Parts", 3))
                {
                    if (!fixMap.TryGetValue(r[0], out var fx)) continue;
                    fx.appearance.partsIndices.Add(new PartsIndexEntry(E<PartsType>(r[1]), I(r[2])));
                    Dirty(fx);
                }
                Save();
            }
        }

        private void Apply_DialogueData()
        {
            if (Validate("DialogueData", 4,
                new[]{"str","str","bool","str"}) > 0) return;

            foreach (var r in Read("DialogueData", 4))
            {
                var so = Get<DialogueData>(r[0], SoDialogue);
                so.StaticID            = r[0];
                so.dialogueName        = r[1];
                so.isSkippable         = bool.Parse(r[2]);
                so.defaultSpeakerName  = r[3];
                so.nodes = new List<DialogueNode>();
                Dirty(so);
            }
            Save();

            // 2단계: 노드 빌드 (nodeIndex 순서대로 채움)
            if (File.Exists(CsvPath("DialogueData_Nodes")))
            {
                var dlgMap = CSVDiffCore.All<DialogueData>().ToDictionary(d => d.StaticID);
                // 사전에 nodeIndex의 최대값으로 List 확장
                var rows = Read("DialogueData_Nodes", 6).ToList();
                foreach (var grp in rows.GroupBy(r => r[0]))
                {
                    if (!dlgMap.TryGetValue(grp.Key, out var dlg)) continue;
                    int maxIdx = grp.Max(r => I(r[1]));
                    while (dlg.nodes.Count <= maxIdx) dlg.nodes.Add(new DialogueNode());
                    foreach (var r in grp)
                    {
                        int idx = I(r[1]);
                        var n = dlg.nodes[idx];
                        n.nodeType      = E<DialogueNodeType>(r[2]);
                        n.dialogueText  = r[3];
                        n.speakerName   = r[4];
                        n.nextNodeIndex = I(r[5]);
                        n.choices       = new List<DialogueChoice>();
                    }
                    Dirty(dlg);
                }
                Save();
            }

            // 3단계: 선택지 빌드
            if (File.Exists(CsvPath("DialogueData_Choices")))
            {
                var dlgMap = CSVDiffCore.All<DialogueData>().ToDictionary(d => d.StaticID);
                var rows = Read("DialogueData_Choices", 14).ToList();
                foreach (var grp in rows
                    .GroupBy(r => (dialogueID: r[0], nodeIndex: I(r[1])))
                    .OrderBy(g => g.Key.dialogueID).ThenBy(g => g.Key.nodeIndex))
                {
                    if (!dlgMap.TryGetValue(grp.Key.dialogueID, out var dlg)) continue;
                    if (grp.Key.nodeIndex < 0 || grp.Key.nodeIndex >= dlg.nodes.Count) continue;
                    var node = dlg.nodes[grp.Key.nodeIndex];
                    int maxCi = grp.Max(r => I(r[2]));
                    while (node.choices.Count <= maxCi) node.choices.Add(new DialogueChoice());
                    foreach (var r in grp)
                    {
                        int ci = I(r[2]);
                        var c = node.choices[ci];
                        c.choiceText       = r[3];
                        c.nextNodeIndex    = I(r[4]);
                        c.requireGoldCheck = bool.Parse(r[5]);
                        c.requiredGold     = I(r[6]);
                        c.requireItemCheck = bool.Parse(r[7]);
                        c.requiredItemID   = r[8];
                        c.giveGold         = bool.Parse(r[9]);
                        c.goldAmount       = I(r[10]);
                        c.giveItem         = bool.Parse(r[11]);
                        c.itemID           = r[12];
                        c.itemCount        = I(r[13]);
                    }
                    Dirty(dlg);
                }
                Save();
            }
        }

        #endregion

        // ─── 검증 (기존 SOCSVConverter.Validate 와 동일) ─────────────

        #region 검증

        private int Validate(string name, int expectedCols, string[] colNames = null)
        {
            string path = CsvPath(name);
            if (!File.Exists(path))
            {
                Debug.LogError($"[CSVImportTab] CSV 없음: {path}"); return 1;
            }
            var rows = CSVDiffCore.ReadCsvRows(path);
            if (rows == null || rows.Count < 2)
            {
                Debug.LogWarning($"[CSVImportTab] {name}: 데이터 행 없음"); return 0;
            }
            string[] header = rows[0];
            int errors = 0;
            for (int i = 1; i < rows.Count; i++)
            {
                var cols = rows[i];
                if (cols.Length < expectedCols)
                {
                    Debug.LogError($"[CSVImportTab] {name} {i+1}행: 컬럼 부족 ({cols.Length}/{expectedCols})");
                    errors++; continue;
                }
                if (colNames == null) continue;
                for (int c = 0; c < colNames.Length && c < cols.Length; c++)
                {
                    string rule = colNames[c];
                    string val  = cols[c].Trim();
                    string col  = c < header.Length ? header[c] : $"col{c}";
                    bool ok = true; string detail = "";
                    if (rule == "int"   && !int.TryParse(val, System.Globalization.NumberStyles.Integer, CSVDiffCore.IC, out _))
                        { ok = false; detail = "정수 필요"; }
                    else if (rule == "float" && !float.TryParse(val, System.Globalization.NumberStyles.Float, CSVDiffCore.IC, out _))
                        { ok = false; detail = "실수 필요"; }
                    else if (rule == "bool"  && !bool.TryParse(val, out _))
                        { ok = false; detail = "True/False 필요"; }
                    else if (rule.StartsWith("enum:"))
                    {
                        if (val != "" && !IsValidEnum(rule.Substring(5), val))
                            { ok = false; detail = $"{rule.Substring(5)} 열거형 아님"; }
                    }
                    if (!ok) { Debug.LogError($"[CSVImportTab] {name} {i+1}행 [{col}]: {detail} 실제값=\"{val}\""); errors++; }
                }
            }
            if (errors == 0) Debug.Log($"[CSVImportTab] {name}: 검증 통과 ({rows.Count-1}행)");
            else             Debug.LogError($"[CSVImportTab] {name}: 오류 {errors}건 — import 중단");
            return errors;
        }

        private bool IsValidEnum(string enumName, string value)
        {
            var type = enumName switch
            {
                "Grade"            => typeof(Grade),
                "WeaponType"       => typeof(WeaponType),
                "ArmorType"        => typeof(ArmorType),
                "MaterialType"     => typeof(MaterialType),
                "DungeonEventType" => typeof(DungeonEventType),
                "MonsterCategory"  => typeof(MonsterCategory),
                "WeaponEffectType" => typeof(WeaponEffectType),
                "ActiveItemType"   => typeof(ActiveItemType),
                "TraitType"        => typeof(TraitType),
                "QuestType"        => typeof(QuestType),
                "QuestDifficulty"  => typeof(QuestDifficulty),
                "MorningEventType" => typeof(MorningEventType),
                "DialogueNodeType" => typeof(DialogueNodeType),
                _                  => null
            };
            if (type == null) return true;
            return Enum.IsDefined(type, value);
        }

        #endregion

        // ─── 유틸 ─────────────────────────────────────────────────────

        #region 유틸

        private List<string[]> ReadCsv(string name)
            => CSVDiffCore.ReadCsvRows(CsvPath(name));

        private IEnumerable<string[]> Read(string name, int min)
        {
            string path = CsvPath(name);
            if (!File.Exists(path)) yield break;
            var rows = CSVDiffCore.ReadCsvRows(path);
            if (rows == null) yield break;

            excludedKeys.TryGetValue(name, out var excl);
            var seen = new Dictionary<string, int>();   // BuildDict 와 동일한 __N 키 부여
            for (int i = 1; i < rows.Count; i++)
            {
                var cols = rows[i];
                if (cols == null || cols.Length == 0) continue;

                // diff 와 동일한 키 계산 (첫 컬럼 중복 시 __2, __3 …)
                string baseKey = cols[0];
                int cnt = seen.TryGetValue(baseKey, out var c) ? c : 0;
                string key = cnt == 0 ? baseKey : $"{baseKey}__{cnt + 1}";
                seen[baseKey] = cnt + 1;

                if (cols.Length < min)
                {
                    Debug.LogError($"[CSVImportTab] {name} {i+1}행 컬럼 부족 ({cols.Length}/{min}), 스킵");
                    continue;
                }
                if (excl != null && excl.Contains(key)) continue;   // 미선택 행 제외

                yield return cols;
            }
        }

        private T Get<T>(string staticID, string folder, string prefix = "") where T : BaseData
        {
            var hit = CSVDiffCore.All<T>().FirstOrDefault(x => x.StaticID == staticID);
            if (hit != null) return hit;
            CSVDiffCore.EnsureDir(folder);
            string assetName = string.IsNullOrEmpty(prefix) ? staticID : $"{prefix}{staticID}";
            string assetPath = $"{folder}/{assetName}.asset";
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, assetPath);
            Debug.Log($"[CSVImportTab] 신규 생성: {assetPath}");
            return so;
        }

        private string CsvPath(string name)
            => Path.Combine(CsvFolder, $"{name}.csv").Replace("\\", "/");

        private Sprite Spr(string p) => AssetDatabase.LoadAssetAtPath<Sprite>(p);
        private GameObject Pf(string p) => AssetDatabase.LoadAssetAtPath<GameObject>(p);
        private void Dirty(ScriptableObject so) => EditorUtility.SetDirty(so);
        private void Save() { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
        private static int   I(string s) => CSVDiffCore.ParseInt(s);
        private static float F(string s) => CSVDiffCore.ParseFloat(s);
        private static TEnum E<TEnum>(string s) where TEnum : struct
            => CSVDiffCore.ParseEnum<TEnum>(s);

        #endregion
    }
}
#endif
