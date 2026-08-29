// Assets/_Projects/Scripts/Editor/PresetToAdventurerDataConverter.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using LayerLab.ArtMaker;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public class PresetToAdventurerDataConverter : EditorWindow
    {
        #region 필드

        private PresetData presetData;
        private string outputFolder = "Assets/_Projects/Data/Adventurers";
        private string staticIdPrefix = "ADV_";

        private Vector2 scroll;
        private List<PresetConvertEntry> entries = new();
        private bool entriesBuilt;

        #endregion

        #region 초기화

        [MenuItem("Tools/Today's Weapon Rental/Preset → AdventurerData Converter")]
        public static void Open() => GetWindow<PresetToAdventurerDataConverter>("Preset → AdventurerData");

        #endregion

        #region GUI

        private void OnGUI()
        {
            EditorGUILayout.LabelField("PresetData → AdventurerData 변환기", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            presetData = (PresetData)EditorGUILayout.ObjectField("PresetData", presetData, typeof(PresetData), false);
            outputFolder = EditorGUILayout.TextField("출력 폴더", outputFolder);
            staticIdPrefix = EditorGUILayout.TextField("StaticID 접두사", staticIdPrefix);

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(presetData == null))
            {
                if (GUILayout.Button("프리셋 목록 불러오기"))
                    BuildEntries();
            }

            if (!entriesBuilt || entries.Count == 0)
            {
                if (entriesBuilt)
                    EditorGUILayout.HelpBox("프리셋 항목이 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"프리셋 항목: {entries.Count}개", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var entry in entries)
                DrawEntry(entry);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            if (GUILayout.Button("선택 항목 변환", GUILayout.Height(28)))
                ConvertSelected();
        }

        private void DrawEntry(PresetConvertEntry entry)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(20));
            EditorGUILayout.LabelField($"Preset #{entry.presetIndex}", EditorStyles.boldLabel, GUILayout.Width(90));
            entry.adventurerName = EditorGUILayout.TextField("이름", entry.adventurerName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24);
            entry.staticId = EditorGUILayout.TextField("StaticID", entry.staticId);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24);
            entry.gender = (Gender)EditorGUILayout.EnumPopup("성별", entry.gender);
            entry.isNamed = EditorGUILayout.Toggle("네임드", entry.isNamed);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24);
            EditorGUILayout.LabelField($"파츠: {entry.partCount}개  색상: {entry.colorCount}개", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 내부 메서드

        private void BuildEntries()
        {
            entries.Clear();
            entriesBuilt = true;

            if (presetData == null || presetData.presetItems == null)
                return;

            foreach (var item in presetData.presetItems)
            {
                entries.Add(new PresetConvertEntry
                {
                    selected = true,
                    presetIndex = item.index,
                    adventurerName = $"Adventurer_{item.index}",
                    staticId = $"{staticIdPrefix}{item.index:D3}",
                    gender = Gender.Male,
                    isNamed = false,
                    partCount = item.parts?.Count ?? 0,
                    colorCount = item.colors?.Count ?? 0,
                });
            }
        }

        private void ConvertSelected()
        {
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                Debug.LogError($"[PresetToAdventurerDataConverter] 출력 폴더가 없습니다: {outputFolder}");
                return;
            }

            int created = 0;
            int skipped = 0;

            foreach (var entry in entries)
            {
                if (!entry.selected) continue;

                string assetPath = $"{outputFolder}/{entry.staticId}.asset";

                if (File.Exists($"{Application.dataPath.Replace("Assets", "")}{assetPath}"))
                {
                    Debug.LogWarning($"[PresetToAdventurerDataConverter] 이미 존재하여 스킵: {assetPath}");
                    skipped++;
                    continue;
                }

                var presetItem = presetData.presetItems.Find(p => p.index == entry.presetIndex);
                if (presetItem == null) continue;

                var so = ScriptableObject.CreateInstance<AdventurerData>();
                ApplyPresetToAdventurerData(so, entry, presetItem);

                AssetDatabase.CreateAsset(so, assetPath);
                EditorUtility.SetDirty(so);
                created++;

                Debug.Log($"[PresetToAdventurerDataConverter] 생성: {assetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "변환 완료",
                $"생성: {created}개\n스킵(이미 존재): {skipped}개",
                "확인"
            );
        }

        private void ApplyPresetToAdventurerData(AdventurerData so, PresetConvertEntry entry, PresetItem presetItem)
        {
            so.StaticID       = entry.staticId;
            so.adventurerName = entry.adventurerName;
            so.isNamed        = entry.isNamed;
            so.gender         = entry.gender;

            so.appearance.partsIndices = new List<PartsIndexEntry>();
            if (presetItem.parts != null)
                foreach (var part in presetItem.parts)
                    so.appearance.partsIndices.Add(new PartsIndexEntry(part.partType, part.value));

            if (presetItem.colors != null)
            {
                foreach (var colorItem in presetItem.colors)
                {
                    string slot = colorItem.slotName.ToLower();
                    if (slot.StartsWith("hair"))        so.appearance.hairColor  = colorItem.color;
                    else if (slot.StartsWith("beard"))  so.appearance.beardColor = colorItem.color;
                    else if (slot.StartsWith("brow"))   so.appearance.browColor  = colorItem.color;
                    else if (slot.StartsWith("body"))   so.appearance.skinColor  = colorItem.color;
                }
            }
        }

        #endregion
    }

    internal class PresetConvertEntry
    {
        public bool selected;
        public int presetIndex;
        public string adventurerName;
        public string staticId;
        public Gender gender;
        public bool isNamed;
        public int partCount;
        public int colorCount;
    }
}
#endif