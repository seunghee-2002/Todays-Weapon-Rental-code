// Assets/_Projects/Scripts/Editor/CSVTools/CSVToolWindow.cs
// 메뉴: Tools > TodaysWeaponRental > CSV Tool
//
// 탭 구성:
//  [Export]  SO → _preview.csv 생성 + diff 표시
//  [Applier] _preview.csv → 원본 CSV 덮어쓰기
//  [Import]  CSV → SO 변환 결과 diff 확인 → SO 반영

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public class CSVToolWindow : EditorWindow
    {
        // ─── 경로 설정 ────────────────────────────────────────────────
        private string csvFolder     = "Assets/_Projects/Data/CSV";
        private string previewFolder = "Assets/_Projects/Data/CSV/Preview";
        private string logFolder     = "Assets/_Projects/Data/CSV/Logs";

        private string soWeapon       = "Assets/_Projects/Data/Weapons";
        private string soAdventurer   = "Assets/_Projects/Data/Adventurers";
        private string soMaterial     = "Assets/_Projects/Data/Materials";
        private string soDungeonEvent = "Assets/_Projects/Data/DungeonEvents";
        private string soWeaponEffect = "Assets/_Projects/Data/WeaponEffects";
        private string soActiveItem   = "Assets/_Projects/Data/ActiveItems";
        private string soVisitorEvent = "Assets/_Projects/Data/VisitorEvents";
        private string soDungeon      = "Assets/_Projects/Data/Dungeons";
        private string soWeaponRecipe    = "Assets/_Projects/Data/WeaponRecipes";
        private string soActiveRecipe    = "Assets/_Projects/Data/ActiveItemRecipes";
        private string soWeeklyQuest     = "Assets/_Projects/Data/WeeklyQuests";
        private string soFixedAppearance = "Assets/_Projects/Data/Appearance";
        private string soDialogue        = "Assets/_Projects/Data/Dialogue";

        // ─── 탭 ───────────────────────────────────────────────────────
        private int            tabIndex = 0;
        private CSVExportTab   exportTab;
        private CSVApplierTab  applierTab;
        private CSVImportTab   importTab;

        private static readonly string[] TabLabels = { "Export  (SO→CSV)", "Applier  (preview→원본)", "Import  (CSV→SO)" };

        [MenuItem("Tools/Today's Weapon Rental/CSV Tool")]
        public static void Open() => GetWindow<CSVToolWindow>("CSV Tool");

        private void OnEnable()
        {
            exportTab  = new CSVExportTab();
            applierTab = new CSVApplierTab();
            importTab  = new CSVImportTab();
            InjectPaths();
        }

        private void OnGUI()
        {
            // 경로가 바뀔 수 있으므로 매 프레임 주입
            InjectPaths();

            DrawGlobalPathToggle();
            EditorGUILayout.Space(4);

            tabIndex = GUILayout.Toolbar(tabIndex, TabLabels, GUILayout.Height(26));
            EditorGUILayout.Space(6);

            switch (tabIndex)
            {
                case 0: exportTab.OnGUI();  break;
                case 1: applierTab.OnGUI(); break;
                case 2: importTab.OnGUI();  break;
            }
        }

        // ─── 경로 설정 토글 ───────────────────────────────────────────
        private bool showPaths;

        private void DrawGlobalPathToggle()
        {
            showPaths = EditorGUILayout.Foldout(showPaths, "경로 설정");
            if (!showPaths) return;

            EditorGUI.indentLevel++;
            csvFolder     = EditorGUILayout.TextField("CSV 폴더",     csvFolder);
            previewFolder = EditorGUILayout.TextField("Preview 폴더", previewFolder);
            logFolder     = EditorGUILayout.TextField("Log 폴더",     logFolder);
            EditorGUILayout.Space(2);
            soWeapon       = EditorGUILayout.TextField("Weapon",       soWeapon);
            soAdventurer   = EditorGUILayout.TextField("Adventurer",   soAdventurer);
            soMaterial     = EditorGUILayout.TextField("Material",     soMaterial);
            soDungeonEvent = EditorGUILayout.TextField("DungeonEvent", soDungeonEvent);
            soWeaponEffect = EditorGUILayout.TextField("WeaponEffect", soWeaponEffect);
            soActiveItem   = EditorGUILayout.TextField("ActiveItem",   soActiveItem);
            soVisitorEvent = EditorGUILayout.TextField("VisitorEvent", soVisitorEvent);
            soDungeon      = EditorGUILayout.TextField("Dungeon",      soDungeon);
            soWeaponRecipe    = EditorGUILayout.TextField("WeaponRecipe",    soWeaponRecipe);
            soActiveRecipe    = EditorGUILayout.TextField("ActiveRecipe",    soActiveRecipe);
            soWeeklyQuest     = EditorGUILayout.TextField("WeeklyQuest",     soWeeklyQuest);
            soFixedAppearance = EditorGUILayout.TextField("FixedAppearance", soFixedAppearance);
            soDialogue        = EditorGUILayout.TextField("Dialogue",        soDialogue);
            EditorGUI.indentLevel--;
        }

        // ─── 경로 주입 ────────────────────────────────────────────────
        private void InjectPaths()
        {
            // Export 탭
            exportTab.CsvFolder     = csvFolder;
            exportTab.PreviewFolder = previewFolder;
            exportTab.LogFolder     = logFolder;
            exportTab.SoWeapon       = soWeapon;
            exportTab.SoAdventurer   = soAdventurer;
            exportTab.SoMaterial     = soMaterial;
            exportTab.SoDungeonEvent = soDungeonEvent;
            exportTab.SoWeaponEffect = soWeaponEffect;
            exportTab.SoActiveItem   = soActiveItem;
            exportTab.SoVisitorEvent = soVisitorEvent;
            exportTab.SoDungeon      = soDungeon;
            exportTab.SoWeaponRecipe    = soWeaponRecipe;
            exportTab.SoActiveRecipe    = soActiveRecipe;
            exportTab.SoWeeklyQuest     = soWeeklyQuest;
            exportTab.SoFixedAppearance = soFixedAppearance;
            exportTab.SoDialogue        = soDialogue;

            // Applier 탭
            applierTab.CsvFolder     = csvFolder;
            applierTab.PreviewFolder = previewFolder;

            // Import 탭
            importTab.CsvFolder     = csvFolder;
            importTab.LogFolder     = logFolder;
            importTab.SoWeapon       = soWeapon;
            importTab.SoAdventurer   = soAdventurer;
            importTab.SoMaterial     = soMaterial;
            importTab.SoDungeonEvent = soDungeonEvent;
            importTab.SoWeaponEffect = soWeaponEffect;
            importTab.SoActiveItem   = soActiveItem;
            importTab.SoVisitorEvent = soVisitorEvent;
            importTab.SoDungeon      = soDungeon;
            importTab.SoWeaponRecipe    = soWeaponRecipe;
            importTab.SoActiveRecipe    = soActiveRecipe;
            importTab.SoWeeklyQuest     = soWeeklyQuest;
            importTab.SoFixedAppearance = soFixedAppearance;
            importTab.SoDialogue        = soDialogue;
        }
    }
}
#endif