#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public class NicknameProfanityImporterWindow : EditorWindow
    {
        private const string DefaultConfigPath = "Assets/_Projects/Data/Config/NicknameConfig.asset";

        private NicknameConfig targetConfig;
        private TextAsset wordFile;
        private string externalFilePath = "";
        private WordFileFormat fileFormat = WordFileFormat.Auto;
        private bool appendToExisting;
        private bool sortWords = true;
        private Vector2 previewScroll;
        private List<string> previewWords = new List<string>();
        private string status = "";
        private bool statusIsError;
        private string lastReadFormatLabel = "TXT";

        private enum WordFileFormat
        {
            Auto,
            TXT,
            CSV
        }

        [MenuItem("Tools/Today's Weapon Rental/Nickname Profanity Importer")]
        public static void Open()
        {
            GetWindow<NicknameProfanityImporterWindow>("Profanity Importer");
        }

        private void OnEnable()
        {
            if (targetConfig == null)
                targetConfig = AssetDatabase.LoadAssetAtPath<NicknameConfig>(DefaultConfigPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Nickname Profanity Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            targetConfig = (NicknameConfig)EditorGUILayout.ObjectField(
                "Nickname Config",
                targetConfig,
                typeof(NicknameConfig),
                false);

            EditorGUILayout.Space(6);
            wordFile = (TextAsset)EditorGUILayout.ObjectField(
                "TXT/CSV TextAsset",
                wordFile,
                typeof(TextAsset),
                false);

            using (new EditorGUILayout.HorizontalScope())
            {
                externalFilePath = EditorGUILayout.TextField("External TXT/CSV", externalFilePath);
                if (GUILayout.Button("Select", GUILayout.Width(72)))
                {
                    string startFolder = string.IsNullOrEmpty(externalFilePath) || string.IsNullOrEmpty(Path.GetDirectoryName(externalFilePath))
                        ? Application.dataPath
                        : Path.GetDirectoryName(externalFilePath);

                    string selected = EditorUtility.OpenFilePanelWithFilters(
                        "Select profanity TXT or CSV",
                        startFolder,
                        new[] { "Text and CSV files", "txt,csv", "All files", "*" });

                    if (!string.IsNullOrEmpty(selected))
                    {
                        externalFilePath = selected;
                        wordFile = null;
                    }
                }
            }

            EditorGUILayout.Space(6);
            fileFormat = (WordFileFormat)EditorGUILayout.EnumPopup("File Format", fileFormat);
            appendToExisting = EditorGUILayout.ToggleLeft("Append to existing words", appendToExisting);
            sortWords = EditorGUILayout.ToggleLeft("Sort imported words", sortWords);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview", GUILayout.Height(28)))
                    RefreshPreview();

                if (GUILayout.Button("Import to profanityWords", GUILayout.Height(28)))
                    ImportWords();
            }

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(status, statusIsError ? MessageType.Error : MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Preview ({previewWords.Count})", EditorStyles.boldLabel);

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll);
            int previewCount = Mathf.Min(previewWords.Count, 200);
            for (int i = 0; i < previewCount; i++)
                EditorGUILayout.LabelField(previewWords[i]);

            if (previewWords.Count > previewCount)
                EditorGUILayout.LabelField($"... and {previewWords.Count - previewCount} more");
            EditorGUILayout.EndScrollView();
        }

        private void RefreshPreview()
        {
            try
            {
                previewWords = ReadWords();
                status = $"Loaded {previewWords.Count} word(s) from {lastReadFormatLabel}.";
                statusIsError = false;
            }
            catch (Exception e)
            {
                previewWords.Clear();
                status = $"Preview failed: {e.Message}";
                statusIsError = true;
            }
        }

        private void ImportWords()
        {
            if (targetConfig == null)
            {
                status = "Nickname Config is not assigned.";
                statusIsError = true;
                return;
            }

            List<string> words;
            try
            {
                words = ReadWords();
            }
            catch (Exception e)
            {
                status = $"Import failed: {e.Message}";
                statusIsError = true;
                return;
            }

            if (words.Count == 0)
            {
                status = "Selected file has no importable words.";
                statusIsError = true;
                return;
            }

            Undo.RecordObject(targetConfig, "Import profanity words");

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (appendToExisting && targetConfig.profanityWords != null)
            {
                foreach (string existing in targetConfig.profanityWords)
                    AddNormalizedWord(existing, result, seen);
            }

            int beforeCount = result.Count;
            foreach (string word in words)
                AddNormalizedWord(word, result, seen);

            if (sortWords)
                result.Sort(StringComparer.OrdinalIgnoreCase);

            targetConfig.profanityWords = result;
            EditorUtility.SetDirty(targetConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = targetConfig;

            previewWords = words;
            int addedCount = result.Count - beforeCount;
            status = appendToExisting
                ? $"Imported {addedCount} new word(s). Total: {result.Count}."
                : $"Replaced profanityWords with {result.Count} word(s).";
            statusIsError = false;
        }

        private List<string> ReadWords()
        {
            string text = ReadSourceText();

            var words = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ShouldParseAsCsv())
            {
                lastReadFormatLabel = "CSV";
                AddCsvCells(text, words, seen);
            }
            else
            {
                lastReadFormatLabel = "TXT";
                using (var reader = new StringReader(text))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        AddNormalizedWord(line, words, seen);
                }
            }

            if (sortWords)
                words.Sort(StringComparer.OrdinalIgnoreCase);

            return words;
        }

        private string ReadSourceText()
        {
            if (wordFile != null)
                return wordFile.text;

            if (string.IsNullOrWhiteSpace(externalFilePath))
                throw new InvalidOperationException("Select a TXT/CSV TextAsset or external TXT/CSV file first.");

            if (!File.Exists(externalFilePath))
                throw new FileNotFoundException("TXT file was not found.", externalFilePath);

            byte[] bytes = File.ReadAllBytes(externalFilePath);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        private bool ShouldParseAsCsv()
        {
            switch (fileFormat)
            {
                case WordFileFormat.CSV:
                    return true;
                case WordFileFormat.TXT:
                    return false;
            }

            if (wordFile != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(wordFile);
                return string.Equals(Path.GetExtension(assetPath), ".csv", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(externalFilePath) &&
                string.Equals(Path.GetExtension(externalFilePath), ".csv", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static void AddCsvCells(string text, List<string> words, HashSet<string> seen)
        {
            var cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (c == ',' || c == '\n' || c == '\r')
                {
                    AddNormalizedWord(cell.ToString(), words, seen);
                    cell.Length = 0;

                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    continue;
                }

                cell.Append(c);
            }

            AddNormalizedWord(cell.ToString(), words, seen);
        }

        private static void AddNormalizedWord(string rawWord, List<string> words, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(rawWord))
                return;

            string word = rawWord.Trim().TrimStart('\uFEFF');
            if (word.Length == 0 || !seen.Add(word))
                return;

            words.Add(word);
        }
    }
}
#endif
