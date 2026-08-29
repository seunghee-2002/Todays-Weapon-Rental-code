// Assets/_Projects/Scripts/Editor/CSVTools/CSVDiffCore.cs
// 공통 유틸: diff 계산 / 로그 저장 / diff UI 렌더링
// CSVExportTab, CSVImportTab, CSVApplierTab 에서 공유

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    // ─── Diff 종류 ────────────────────────────────────────────────────
    public enum DiffKind { Added, Removed, Modified, Unchanged }

    // ─── Diff 결과 한 행 ─────────────────────────────────────────────
    public class DiffRow
    {
        public string   FileName;
        public string   RowKey;
        public DiffKind Kind;
        public string   Detail;

        // 행별 선택 적용용
        public bool      Selected = true;   // 적용 대상 체크 상태 (기본 켜짐)
        public string[]  OrigCols;          // 기존(orig) 행 데이터
        public string[]  NewCols;           // 신규(new) 행 데이터
    }

    // ─── Diff 필터 상태 ───────────────────────────────────────────────
    public class DiffFilter
    {
        public bool ShowAdded     = true;
        public bool ShowRemoved   = true;
        public bool ShowModified  = true;
        public bool ShowUnchanged = false;
    }

    public static class CSVDiffCore
    {
        // ─── 색상 ─────────────────────────────────────────────────────
        public static readonly Color ColorAdded     = new Color(0.2f, 0.85f, 0.3f);
        public static readonly Color ColorRemoved   = new Color(0.9f, 0.3f,  0.3f);
        public static readonly Color ColorModified  = new Color(1.0f, 0.8f,  0.2f);
        public static readonly Color ColorUnchanged = new Color(0.65f, 0.65f, 0.65f);
        public static readonly Color ColorFileHeader = new Color(0.5f, 0.85f, 1.0f);

        // ─── diff 계산 ────────────────────────────────────────────────
        // origRows : 기존 데이터 (헤더 포함 List<string[]>), null 이면 파일 없음 취급
        // newRows  : 새 데이터  (헤더 포함 List<string[]>)
        public static List<DiffRow> Compute(string fileName,
                                            List<string[]> origRows,
                                            List<string[]> newRows)
        {
            var results = new List<DiffRow>();

            string[] origHeader = origRows != null && origRows.Count > 0 ? origRows[0] : null;
            string[] newHeader  = newRows  != null && newRows.Count  > 0 ? newRows[0]  : null;

            var origDict = BuildDict(origRows);
            var newDict  = BuildDict(newRows);

            var allKeys = origDict.Keys.Union(newDict.Keys).OrderBy(k => k).ToList();

            foreach (var key in allKeys)
            {
                bool inOrig = origDict.TryGetValue(key, out var origRow);
                bool inNew  = newDict.TryGetValue(key,  out var newRow);

                DiffKind kind;
                string   detail = "";

                if (!inOrig)
                {
                    kind   = DiffKind.Added;
                    detail = FormatRow(newRow);
                }
                else if (!inNew)
                {
                    kind   = DiffKind.Removed;
                    detail = FormatRow(origRow);
                }
                else
                {
                    var changed = new List<string>();
                    int maxLen  = Math.Max(origRow.Length, newRow.Length);
                    for (int c = 0; c < maxLen; c++)
                    {
                        string ov = c < origRow.Length ? NormalizeNewline(origRow[c]).Trim() : "";
                        string nv = c < newRow.Length  ? NormalizeNewline(newRow[c]).Trim()  : "";
                        if (ov == nv) continue;

                        string colName = ColName(c, origHeader, newHeader);
                        changed.Add($"{colName}: \"{ov}\"→\"{nv}\"");
                    }

                    if (changed.Count > 0)
                    {
                        kind   = DiffKind.Modified;
                        detail = string.Join(" | ", changed);
                    }
                    else
                    {
                        kind = DiffKind.Unchanged;
                    }
                }

                results.Add(new DiffRow
                {
                    FileName = fileName,
                    RowKey   = key,
                    Kind     = kind,
                    Detail   = detail,
                    OrigCols = inOrig ? origRow : null,
                    NewCols  = inNew  ? newRow  : null
                });
            }

            return results;
        }

        // ─── 요약 ─────────────────────────────────────────────────────
        public static (int added, int removed, int modified, int unchanged)
            Summary(List<DiffRow> rows) => (
                rows.Count(r => r.Kind == DiffKind.Added),
                rows.Count(r => r.Kind == DiffKind.Removed),
                rows.Count(r => r.Kind == DiffKind.Modified),
                rows.Count(r => r.Kind == DiffKind.Unchanged));

        // ─── 로그 저장 ────────────────────────────────────────────────
        public static void SaveLog(List<DiffRow> rows, string logFolder,
                                   string targetLabel, string direction)
        {
            EnsureDir(logFolder);
            string fileName = $"diff_{direction}_{DateTime.Now:yyyy-MM-dd_HH-mm}.log";
            string path     = Path.Combine(logFolder, fileName).Replace("\\", "/");

            var sb = new StringBuilder();
            sb.AppendLine("=== CSV Diff Log ===");
            sb.AppendLine($"생성 시각 : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"방향      : {direction}");
            sb.AppendLine($"대상      : {targetLabel}");
            sb.AppendLine();

            var (a, r, m, u) = Summary(rows);
            sb.AppendLine($"[요약] 추가:{a}  삭제:{r}  수정:{m}  동일:{u}");
            sb.AppendLine(new string('-', 60));

            string currentFile = null;
            foreach (var row in rows)
            {
                if (row.Kind == DiffKind.Unchanged) continue;

                if (row.FileName != currentFile)
                {
                    currentFile = row.FileName;
                    sb.AppendLine();
                    sb.AppendLine($"▶ {currentFile}");
                }

                string prefix = row.Kind switch
                {
                    DiffKind.Added    => "[+]",
                    DiffKind.Removed  => "[-]",
                    DiffKind.Modified => "[~]",
                    _                 => "[ ]"
                };

                sb.Append($"  {prefix} {row.RowKey}");
                if (!string.IsNullOrEmpty(row.Detail))
                    sb.Append($"  →  {row.Detail}");
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[CSVTools] 로그 저장: {path}");
            EditorUtility.RevealInFinder(path);
        }

        // ─── 변환 대상 타입 체크박스 ──────────────────────────────────
        // names[0] 은 "전체" 마스터 토글. sel 길이는 names 와 동일(인덱스 0 은 미사용).
        public static void DrawTypeCheckboxes(string[] names, bool[] sel)
        {
            EditorGUILayout.LabelField("변환 대상", EditorStyles.boldLabel);

            bool all = true;
            for (int i = 1; i < sel.Length; i++) if (!sel[i]) { all = false; break; }

            bool newAll = EditorGUILayout.ToggleLeft("전체", all);
            if (newAll != all)
                for (int i = 1; i < sel.Length; i++) sel[i] = newAll;

            const int perRow = 3;
            for (int i = 1; i < names.Length; i++)
            {
                if ((i - 1) % perRow == 0) EditorGUILayout.BeginHorizontal();
                sel[i] = EditorGUILayout.ToggleLeft(names[i], sel[i], GUILayout.Width(180));
                if ((i - 1) % perRow == perRow - 1 || i == names.Length - 1)
                    EditorGUILayout.EndHorizontal();
            }
        }

        public static bool AnySelected(bool[] sel)
        {
            for (int i = 1; i < sel.Length; i++) if (sel[i]) return true;
            return false;
        }

        // 전체 체크 상태의 선택 배열 생성 (도구 열 때 기본값)
        public static bool[] NewAllSelected(int len)
        {
            var a = new bool[len];
            for (int i = 0; i < len; i++) a[i] = true;
            return a;
        }

        // ─── diff UI 렌더링 ───────────────────────────────────────────
        public static void DrawFilterBar(DiffFilter filter, List<DiffRow> rows)
        {
            var (a, r, m, u) = Summary(rows);
            EditorGUILayout.BeginHorizontal();
            DrawToggle(ref filter.ShowAdded,     $"추가  {a}건",  ColorAdded);
            DrawToggle(ref filter.ShowRemoved,   $"삭제  {r}건",  ColorRemoved);
            DrawToggle(ref filter.ShowModified,  $"수정  {m}건",  ColorModified);
            DrawToggle(ref filter.ShowUnchanged, $"동일  {u}건",  ColorUnchanged);
            EditorGUILayout.EndHorizontal();
        }

        // selectable=true 면 각 행 앞에 체크박스(row.Selected) + 파일별 전체/해제 버튼을 그린다.
        public static void DrawDiffRows(List<DiffRow> rows, DiffFilter filter, bool selectable = false)
        {
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("미리보기를 생성하면 diff 결과가 여기에 표시됩니다.",
                                        MessageType.Info);
                return;
            }

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = ColorFileHeader } };
            var rowStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };

            string currentFile = null;
            foreach (var row in rows)
            {
                bool visible = row.Kind switch
                {
                    DiffKind.Added     => filter.ShowAdded,
                    DiffKind.Removed   => filter.ShowRemoved,
                    DiffKind.Modified  => filter.ShowModified,
                    DiffKind.Unchanged => filter.ShowUnchanged,
                    _                  => false
                };
                if (!visible) continue;

                if (row.FileName != currentFile)
                {
                    currentFile = row.FileName;
                    EditorGUILayout.Space(6);
                    if (selectable) DrawFileHeaderWithSelectAll(currentFile, rows, filter, headerStyle);
                    else            EditorGUILayout.LabelField($"▶ {currentFile}", headerStyle);
                }

                rowStyle.normal.textColor = row.Kind switch
                {
                    DiffKind.Added    => ColorAdded,
                    DiffKind.Removed  => ColorRemoved,
                    DiffKind.Modified => ColorModified,
                    _                 => ColorUnchanged
                };

                string prefix = row.Kind switch
                {
                    DiffKind.Added    => "[+]",
                    DiffKind.Removed  => "[-]",
                    DiffKind.Modified => "[~]",
                    _                 => "[ ]"
                };

                string line = $"{prefix} {row.RowKey}";
                if (!string.IsNullOrEmpty(row.Detail))
                    line += $"  →  {row.Detail}";

                // 동일(Unchanged) 행은 적용 대상이 아니므로 체크박스를 그리지 않는다.
                if (selectable && row.Kind != DiffKind.Unchanged)
                {
                    EditorGUILayout.BeginHorizontal();
                    row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(16));
                    EditorGUILayout.LabelField(line, rowStyle);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField(line, rowStyle);
                }
            }
        }

        // 파일 헤더 옆에 "전체 선택/해제" 버튼 (현재 필터로 보이는 변경 행 대상)
        private static void DrawFileHeaderWithSelectAll(string file, List<DiffRow> rows,
                                                        DiffFilter filter, GUIStyle headerStyle)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"▶ {file}", headerStyle);
            if (GUILayout.Button("전체 선택", EditorStyles.miniButton, GUILayout.Width(70)))
                SetSelectionForFile(file, rows, filter, true);
            if (GUILayout.Button("전체 해제", EditorStyles.miniButton, GUILayout.Width(70)))
                SetSelectionForFile(file, rows, filter, false);
            EditorGUILayout.EndHorizontal();
        }

        private static void SetSelectionForFile(string file, List<DiffRow> rows,
                                                 DiffFilter filter, bool value)
        {
            foreach (var r in rows)
            {
                if (r.FileName != file || r.Kind == DiffKind.Unchanged) continue;
                bool visible = r.Kind switch
                {
                    DiffKind.Added    => filter.ShowAdded,
                    DiffKind.Removed  => filter.ShowRemoved,
                    DiffKind.Modified => filter.ShowModified,
                    _                 => false
                };
                if (visible) r.Selected = value;
            }
        }

        // ─── 선택 행 병합 (Export 방향 적용용) ────────────────────────
        // origRows(헤더 포함)를 기준으로, 같은 파일의 diffRows 중 Selected 인 것만
        // 추가/삭제/수정 반영한 최종 rows 를 만든다. 미선택/Unchanged 는 orig 유지.
        public static List<string[]> BuildMergedRows(List<string[]> origRows, List<DiffRow> fileDiffRows)
        {
            // 키 → 현재 행 (BuildDict 와 동일한 __N 규칙)
            var merged = new Dictionary<string, string[]>();
            var order  = new List<string>();   // 출력 순서 보존
            string[] header = origRows != null && origRows.Count > 0 ? origRows[0] : null;

            if (origRows != null)
            {
                for (int i = 1; i < origRows.Count; i++)
                {
                    if (origRows[i] == null || origRows[i].Length == 0) continue;
                    string key = DedupKey(merged, origRows[i][0]);
                    merged[key] = origRows[i];
                    order.Add(key);
                }
            }

            foreach (var d in fileDiffRows)
            {
                if (!d.Selected || d.Kind == DiffKind.Unchanged) continue;
                switch (d.Kind)
                {
                    case DiffKind.Added:
                        if (d.NewCols != null && !merged.ContainsKey(d.RowKey))
                        {
                            merged[d.RowKey] = d.NewCols;
                            order.Add(d.RowKey);
                        }
                        break;
                    case DiffKind.Removed:
                        if (merged.Remove(d.RowKey)) order.Remove(d.RowKey);
                        break;
                    case DiffKind.Modified:
                        if (d.NewCols != null && merged.ContainsKey(d.RowKey))
                            merged[d.RowKey] = d.NewCols;
                        break;
                }
            }

            var result = new List<string[]>();
            if (header != null) result.Add(header);
            foreach (var key in order) result.Add(merged[key]);
            return result;
        }

        private static string DedupKey(Dictionary<string, string[]> dict, string baseKey)
        {
            if (!dict.ContainsKey(baseKey)) return baseKey;
            int n = 2;
            while (dict.ContainsKey($"{baseKey}__{n}")) n++;
            return $"{baseKey}__{n}";
        }

        // ─── 공통 유틸 ────────────────────────────────────────────────
        public static List<string[]> ReadCsvRows(string path)
        {
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path, Encoding.UTF8);
            var rows    = new List<string[]>();
            var row     = new List<string>();
            var sb      = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuote)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') inQuote = false;
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuote = true;
                    else if (c == ',') { row.Add(sb.ToString()); sb.Clear(); }
                    else if (c == '\r')
                    {
                        // CRLF / lone CR 모두 행 종료
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                        row.Add(sb.ToString()); sb.Clear();
                        if (!IsEmptyRow(row)) rows.Add(row.ToArray());
                        row.Clear();
                    }
                    else if (c == '\n')
                    {
                        row.Add(sb.ToString()); sb.Clear();
                        if (!IsEmptyRow(row)) rows.Add(row.ToArray());
                        row.Clear();
                    }
                    else sb.Append(c);
                }
            }
            // 마지막 셀/행 flush
            if (sb.Length > 0 || row.Count > 0)
            {
                row.Add(sb.ToString());
                if (!IsEmptyRow(row)) rows.Add(row.ToArray());
            }
            return rows;
        }

        private static bool IsEmptyRow(List<string> row)
        {
            if (row.Count == 0) return true;
            for (int i = 0; i < row.Count; i++)
                if (!string.IsNullOrWhiteSpace(row[i])) return false;
            return true;
        }

        public static void WriteCsv(string path, List<string[]> rows)
        {
            EnsureDir(Path.GetDirectoryName(path));
            var sb = new StringBuilder();
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(Esc)));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // ─── 파일 잠금 / 헤더 비교 ────────────────────────────────────
        // 파일이 다른 프로세스(예: Excel)에 잠겨 읽을 수 없으면 true. 없는 파일은 false.
        private static bool IsFileLocked(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                // FileShare.None = 배타적 열기. 외부(예: Excel)가 핸들을 잡고 있으면 실패하므로 잠금을 확실히 감지.
                // FileAccess.Read 라 읽기전용 파일에서도 UnauthorizedAccessException 없이 동작.
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        // 잠긴 파일이 하나라도 있으면 경고 다이얼로그를 띄우고 true(작업 중단) 반환.
        public static bool WarnIfAnyLocked(IEnumerable<string> paths)
        {
            var locked = paths.Where(IsFileLocked).Distinct().ToList();
            if (locked.Count == 0) return false;

            string list = string.Join("\n", locked.Select(p => "  • " + Path.GetFileName(p)));
            EditorUtility.DisplayDialog(
                "파일 잠김",
                "다음 CSV가 다른 프로그램(예: Excel)에서 열려 있어 작업을 중단했습니다:\n\n" +
                list + "\n\n해당 파일을 닫은 뒤 다시 시도하세요.",
                "확인");
            return true;
        }

        // 헤더(컬럼 개수/이름)가 동일한지 비교. 다르면 스키마 변경으로 간주.
        public static bool HeaderEquals(string[] a, string[] b)
        {
            if (a == null || b == null) return ReferenceEquals(a, b);
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (NormalizeNewline(a[i]).Trim() != NormalizeNewline(b[i]).Trim()) return false;
            return true;
        }

        public static void EnsureDir(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static string SPath(Sprite s)
            => s != null ? AssetDatabase.GetAssetPath(s) : "";

        public static string GPath(GameObject g)
            => g != null ? AssetDatabase.GetAssetPath(g) : "";

        public static List<T> All<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
               .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
               .Where(x => x != null)
               .OrderBy(x => (x as BaseData)?.StaticID ?? x.name)
               .ToList();

        public static List<string[]> Hdr(params string[] h) => new List<string[]> { h };
        public static string[] R(params string[] v) => v;


        public static readonly System.Globalization.CultureInfo IC =
            System.Globalization.CultureInfo.InvariantCulture;

        public static int   ParseInt(string s)   => int.Parse(s, IC);
        public static float ParseFloat(string s) => float.Parse(s, IC);
        public static TEnum ParseEnum<TEnum>(string s) where TEnum : struct
            => Enum.Parse<TEnum>(s);

        // ─── private ──────────────────────────────────────────────────
        private static Dictionary<string, string[]> BuildDict(List<string[]> rows)
        {
            var dict = new Dictionary<string, string[]>();
            if (rows == null) return dict;
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].Length == 0) continue;
                string key = rows[i][0];
                // 관계 테이블처럼 key 중복 시 index 붙임
                if (dict.ContainsKey(key))
                {
                    int n = 2;
                    while (dict.ContainsKey($"{key}__{n}")) n++;
                    key = $"{key}__{n}";
                }
                dict[key] = rows[i];
            }
            return dict;
        }

        // 셀 안 멀티라인 줄바꿈을 \n 으로 통일 (CRLF vs LF 차이로 인한 오탐 방지)
        private static string NormalizeNewline(string s)
            => s == null ? "" : s.Replace("\r\n", "\n").Replace("\r", "\n");

        private static string ColName(int c, string[] origHeader, string[] newHeader)
        {
            if (origHeader != null && c < origHeader.Length) return origHeader[c];
            if (newHeader  != null && c < newHeader.Length)  return newHeader[c];
            return $"col{c}";
        }

        private static string FormatRow(string[] cols)
            => cols != null ? string.Join(", ", cols) : "";

        private static void DrawToggle(ref bool value, string label, Color color)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = color;
            value = GUILayout.Toggle(value, label, GUILayout.Width(95));
            GUI.contentColor = prev;
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
    }
}
#endif