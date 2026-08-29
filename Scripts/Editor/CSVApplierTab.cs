// Assets/_Projects/Scripts/Editor/CSVTools/CSVApplierTab.cs
// preview CSV → 원본 CSV 덮어쓰기 전담

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public class CSVApplierTab
    {
        // ─── 경로 (CSVToolWindow 에서 주입) ──────────────────────────
        public string CsvFolder;
        public string PreviewFolder;

        // ─── 상태 ─────────────────────────────────────────────────────
        private List<DiffRow> diffRows = new List<DiffRow>();
        private DiffFilter    filter   = new DiffFilter();
        private Vector2       scroll;
        private List<string>  previewFiles = new List<string>();
        // 파일명(확장자/_preview 제외) → 적용 대상 체크 상태 (기본 켜짐)
        private Dictionary<string, bool> fileSelected = new Dictionary<string, bool>();

        // ─── GUI ──────────────────────────────────────────────────────
        public void OnGUI()
        {
            EditorGUILayout.LabelField("_preview.csv → 원본 CSV 덮어쓰기", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            RefreshPreviewList();
            DrawPreviewList();
            EditorGUILayout.Space(6);

            // diff 확인 버튼
            GUI.enabled = previewFiles.Count > 0;
            if (GUILayout.Button("① diff 확인  (preview vs 원본)", GUILayout.Height(26)))
                ComputeDiff();
            GUI.enabled = true;

            EditorGUILayout.Space(2);

            // 덮어쓰기 버튼
            GUI.enabled = previewFiles.Count > 0;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = previewFiles.Count > 0
                ? new Color(0.8f, 0.2f, 0.2f) : Color.gray;
            if (GUILayout.Button("② 덮어쓰기 적용  (preview → 원본)", GUILayout.Height(28)))
                ApplyPreview();
            GUI.backgroundColor = prevBg;
            GUI.enabled = true;

            EditorGUILayout.Space(2);

            // 미리보기 삭제
            GUI.enabled = previewFiles.Count > 0;
            if (GUILayout.Button("③ 미리보기 파일 삭제", GUILayout.Height(22)))
                DeletePreview();
            GUI.enabled = true;

            EditorGUILayout.Space(8);

            // diff 결과
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

        // ─── preview 파일 목록 ────────────────────────────────────────
        private void RefreshPreviewList()
        {
            previewFiles.Clear();
            if (!Directory.Exists(PreviewFolder)) return;
            previewFiles.AddRange(
                Directory.GetFiles(PreviewFolder, "*_preview.csv")
                         .Select(p => p.Replace("\\", "/")));
        }

        private void DrawPreviewList()
        {
            if (previewFiles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "preview 파일이 없습니다.\nExport 탭에서 미리보기를 먼저 생성하세요.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"대기 중인 preview 파일  ({previewFiles.Count}개) — 적용할 파일 체크:",
                                       EditorStyles.miniLabel);
            foreach (var f in previewFiles)
            {
                string name = OrigName(f);
                if (!fileSelected.ContainsKey(name)) fileSelected[name] = true;
                fileSelected[name] = EditorGUILayout.ToggleLeft("  " + Path.GetFileName(f), fileSelected[name]);
            }
        }

        // preview 경로 → 원본 파일명(_preview/확장자 제거)
        private static string OrigName(string previewPath)
            => Path.GetFileNameWithoutExtension(previewPath).Replace("_preview", "");

        private bool IsFileSelected(string name)
            => !fileSelected.TryGetValue(name, out var v) || v;   // 미등록은 켜짐 취급

        // 선택된 preview + 대응 원본 CSV 경로 (잠금 점검용)
        private List<string> SelectedPaths()
        {
            var paths = new List<string>();
            foreach (var previewPath in previewFiles)
            {
                string fileName = OrigName(previewPath);
                if (!IsFileSelected(fileName)) continue;
                paths.Add(previewPath);
                paths.Add(Path.Combine(CsvFolder, $"{fileName}.csv").Replace("\\", "/"));
            }
            return paths;
        }

        // ─── diff 확인 ────────────────────────────────────────────────
        private void ComputeDiff()
        {
            if (CSVDiffCore.WarnIfAnyLocked(SelectedPaths())) return;

            diffRows.Clear();
            foreach (var previewPath in previewFiles)
            {
                string fileName = OrigName(previewPath);
                if (!IsFileSelected(fileName)) continue;   // 체크된 파일만 diff

                string origPath = Path.Combine(CsvFolder, $"{fileName}.csv")
                                      .Replace("\\", "/");

                var origRows    = CSVDiffCore.ReadCsvRows(origPath);
                var previewRows = CSVDiffCore.ReadCsvRows(previewPath);

                diffRows.AddRange(CSVDiffCore.Compute(fileName, origRows, previewRows));
            }

            var (a, r, m, u) = CSVDiffCore.Summary(diffRows);
            Debug.Log($"[CSVApplierTab] diff 계산 완료 — 추가:{a} 삭제:{r} 수정:{m} 동일:{u}");
        }

        // ─── 덮어쓰기 적용 (선택 파일/행만 병합) ──────────────────────
        private void ApplyPreview()
        {
            if (CSVDiffCore.WarnIfAnyLocked(SelectedPaths())) return;

            // 행 단위 병합을 위해 diff 가 필요. 아직 계산 전이면 먼저 계산.
            if (diffRows.Count == 0) ComputeDiff();

            int changed = diffRows.Count(r => r.Kind != DiffKind.Unchanged && r.Selected);
            bool confirm = EditorUtility.DisplayDialog(
                "덮어쓰기 적용",
                $"선택된 변경 항목: {changed}건\n\n" +
                "체크된 파일/행만 원본 CSV에 병합합니다.\n계속하시겠습니까?",
                "적용", "취소");
            if (!confirm) return;

            CSVDiffCore.EnsureDir(CsvFolder);
            int applied = 0;
            foreach (var previewPath in previewFiles)
            {
                string fileName = OrigName(previewPath);
                if (!IsFileSelected(fileName)) continue;

                string origPath = Path.Combine(CsvFolder, $"{fileName}.csv")
                                      .Replace("\\", "/");

                var origRows = CSVDiffCore.ReadCsvRows(origPath);
                if (origRows == null)
                {
                    // 원본이 없으면 병합 기준이 없으므로 preview 를 그대로 사용(헤더 보존)
                    File.Copy(previewPath, origPath, overwrite: true);
                    Debug.Log($"[CSVApplierTab] 신규 파일 적용: {origPath}");
                    applied++;
                    continue;
                }

                // 헤더(스키마)가 바뀌면 행 단위 병합으로는 헤더가 갱신되지 않고 컬럼이 어긋난다.
                // 이 경우 행 선택을 무시하고 SO 기준 preview 로 전체 교체한다.
                var previewRows = CSVDiffCore.ReadCsvRows(previewPath);
                if (previewRows != null && !CSVDiffCore.HeaderEquals(
                        origRows.Count > 0 ? origRows[0] : null,
                        previewRows.Count > 0 ? previewRows[0] : null))
                {
                    File.Copy(previewPath, origPath, overwrite: true);
                    Debug.Log($"[CSVApplierTab] 스키마(헤더) 변경 감지 → 전체 교체: {origPath}");
                    applied++;
                    continue;
                }

                var fileDiffRows = diffRows.Where(d => d.FileName == fileName).ToList();
                var merged       = CSVDiffCore.BuildMergedRows(origRows, fileDiffRows);

                CSVDiffCore.WriteCsv(origPath, merged);
                Debug.Log($"[CSVApplierTab] 적용: {origPath}");
                applied++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CSVApplierTab] 병합 완료 — {applied}개 파일 적용");

            diffRows.Clear();
        }

        // ─── 미리보기 삭제 ────────────────────────────────────────────
        private void DeletePreview()
        {
            if (!Directory.Exists(PreviewFolder)) return;

            int count = 0;
            foreach (var f in previewFiles)
            {
                File.Delete(f);
                string meta = f + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
                count++;
            }

            diffRows.Clear();
            AssetDatabase.Refresh();
            Debug.Log($"[CSVApplierTab] 미리보기 {count}개 삭제 완료");
        }
    }
}
#endif