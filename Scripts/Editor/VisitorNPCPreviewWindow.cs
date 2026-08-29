// Assets/_Projects/Scripts/Editor/VisitorNPCPreviewWindow.cs
// 메뉴: Tools > Today's Weapon Rental > VisitorNPC Preview
//
// ─── 사용법 ──────────────────────────────────────────────────────────
//  1. 좌측 목록에서 FixedAppearanceData를 선택하거나, [직접 지정]에 에셋을 드롭
//  2. [VisitorNPC 프리팹]에 AdventurerAppearanceApplier가 붙은 프리팹 지정
//  3. [스폰 + 적용] → 우측 미리보기 창에 표시 (플레이모드 전용)
//  4. [Idle]/[걷기] 버튼으로 모션 확인
//
//  AdventurerPreviewWindow와 동일한 렌더 방식(전용 카메라 + RenderTexture,
//  SkeletonGraphic canvasRenderers 정리)을 사용한다.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LayerLab.ArtMaker;
using Spine.Unity;

namespace TodaysWeaponRental.Editor
{
    public class VisitorNPCPreviewWindow : EditorWindow
    {
        #region 상수

        private const float ListWidth    = 200f;
        private const float PreviewSize   = 340f;
        private const int   PreviewLayer  = 5;   // UI 레이어 (전용 카메라 cullingMask)

        #endregion

        #region 상태

        private List<FixedAppearanceData> allData = new();
        private FixedAppearanceData       selected;
        private FixedAppearanceData       manualOverride;   // [직접 지정] — 목록 선택보다 우선
        private Vector2                   listScroll;
        private Vector2                   detailScroll;
        private string                    searchQuery = "";

        // 미리보기 입력
        private GameObject visitorPrefab;
        private float      previewScale = 1f;

        // 스폰 / 렌더 리소스
        private GameObject                  spawnedRoot;
        private AdventurerAppearanceApplier spawnedApplier;
        private Camera                      previewCam;
        private RenderTexture               previewRT;
        private bool                        isTicking;

        /// <summary>[직접 지정]이 있으면 그것을, 없으면 목록 선택을 외형 소스로 사용한다.</summary>
        private FixedAppearanceData Current => manualOverride != null ? manualOverride : selected;

        #endregion

        #region 열기

        [MenuItem("Tools/Today's Weapon Rental/VisitorNPC Preview")]
        public static void Open()
        {
            var window = GetWindow<VisitorNPCPreviewWindow>("VisitorNPC Preview");
            window.minSize = new Vector2(780f, 480f);
        }

        #endregion

        #region Unity 이벤트

        private void OnEnable()
        {
            RefreshDataList();
            LoadSettings();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Cleanup();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // 플레이 종료 시 스폰 오브젝트는 파괴되므로 참조·재생 루프만 정리한다.
            if (state == PlayModeStateChange.ExitingPlayMode)
                RemoveSpawned();
        }

        private void OnGUI()
        {
            DrawTopBar();
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            DrawPreview();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 상단 바

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("VisitorNPC Preview", EditorStyles.boldLabel, GUILayout.Width(180f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                RefreshDataList();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 좌측 목록

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));

            EditorGUILayout.LabelField("FixedAppearanceData 목록", EditorStyles.boldLabel);
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
            EditorGUILayout.Space(2f);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            var filtered = string.IsNullOrEmpty(searchQuery)
                ? allData
                : allData.Where(d => d.StaticID.Contains(searchQuery)).ToList();

            foreach (var data in filtered)
            {
                bool isSelected = selected == data && manualOverride == null;
                GUIStyle style  = isSelected ? GetSelectedStyle() : EditorStyles.label;

                if (GUILayout.Button(data.StaticID, style, GUILayout.ExpandWidth(true)))
                {
                    selected = data;
                    manualOverride = null;   // 목록 선택 시 직접 지정 해제
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 가운데 상세 / 컨트롤

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField("── 외형 소스 ──", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            manualOverride = (FixedAppearanceData)EditorGUILayout.ObjectField(
                "직접 지정", manualOverride, typeof(FixedAppearanceData), false);
            if (EditorGUI.EndChangeCheck() && manualOverride != null)
                selected = null;

            var current = Current;
            if (current == null)
            {
                EditorGUILayout.HelpBox("좌측 목록에서 선택하거나 [직접 지정]에 FixedAppearanceData를 드롭하세요.", MessageType.None);
                DrawControls();
                EditorGUILayout.EndVertical();
                return;
            }

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

            EditorGUILayout.LabelField(current.StaticID, EditorStyles.boldLabel);

            var appearance = current.appearance;
            if (appearance != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("색상", EditorStyles.boldLabel);
                DrawColorField("피부색", appearance.skinColor);
                DrawColorField("머리색", appearance.hairColor);
                DrawColorField("수염색", appearance.beardColor);
                DrawColorField("눈썹색", appearance.browColor);

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("파츠 인덱스", EditorStyles.boldLabel);
                foreach (var entry in appearance.partsIndices)
                    DrawField(entry.partsType.ToString(), entry.index.ToString());
            }
            else
            {
                EditorGUILayout.HelpBox("appearance가 비어 있습니다.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();

            DrawControls();

            EditorGUILayout.EndVertical();
        }

        /// <summary>프리팹 지정 + 스폰 + 애니 컨트롤.</summary>
        private void DrawControls()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("── 프리팹 / 애니메이션 ──", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            visitorPrefab = (GameObject)EditorGUILayout.ObjectField(
                "VisitorNPC 프리팹", visitorPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) SaveSettings();

            EditorGUI.BeginChangeCheck();
            previewScale = EditorGUILayout.Slider("미리보기 스케일", previewScale, 0.1f, 3f);
            if (EditorGUI.EndChangeCheck() && spawnedRoot != null)
                ApplyPreviewScale();

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "▶ 플레이모드에서만 미리보기가 가능합니다.\n" +
                    "SkeletonGraphic(Multiple CanvasRenderers)이 플레이모드에서만 정상 초기화됩니다.",
                    MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = Application.isPlaying && visitorPrefab != null && Current != null;
            if (GUILayout.Button("스폰 + 적용", GUILayout.Height(26f))) Spawn();
            GUI.enabled = spawnedRoot != null;
            if (GUILayout.Button("제거", GUILayout.Height(26f), GUILayout.Width(60f))) RemoveSpawned();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUI.enabled = spawnedApplier != null && Current != null;
            if (GUILayout.Button("외형 다시 적용")) { ApplyToSpawned(); RenderPreview(); }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle")) PlayAnim("Idle", true);
            if (GUILayout.Button("걷기")) PlayAnim("Walk", true);
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;

            EditorGUILayout.HelpBox(
                "외형이 안 보이면 프리팹 SkeletonGraphic의 Multiple CanvasRenderers,\n" +
                "AdventurerAppearanceApplier/PartsManager 연결을 확인하세요.",
                MessageType.Info);
        }

        #endregion

        #region 우측 미리보기 (RenderTexture)

        private void DrawPreview()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(PreviewSize + 12f));
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);

            var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(false));
            if (previewRT != null)
                GUI.DrawTexture(rect, previewRT, ScaleMode.ScaleToFit, false);
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
                GUI.Label(rect, "스폰하면 여기에 표시됩니다", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 스폰 / 렌더

        /// <summary>전용 카메라 + RenderTexture를 보장한다(창 안 미리보기용).</summary>
        private void EnsureRenderTarget()
        {
            if (previewRT == null)
                previewRT = new RenderTexture(512, 512, 16) { hideFlags = HideFlags.HideAndDontSave };

            if (previewCam == null)
            {
                var camGo = new GameObject("__VisitorPreview_Cam") { hideFlags = HideFlags.HideAndDontSave };
                previewCam = camGo.AddComponent<Camera>();
                previewCam.clearFlags      = CameraClearFlags.SolidColor;
                previewCam.backgroundColor = new Color(0.2f, 0.2f, 0.22f, 1f);
                previewCam.cullingMask     = 1 << PreviewLayer;
                previewCam.orthographic    = true;
                previewCam.targetTexture   = previewRT;
                previewCam.transform.position = new Vector3(5000f, 0f, -100f);   // 씬 오브젝트와 격리(빈 공간)
            }
        }

        /// <summary>임시 Canvas + VisitorNPC 프리팹을 생성하고 외형을 적용한다.</summary>
        private void Spawn()
        {
            RemoveSpawned();
            if (visitorPrefab == null || Current == null) return;
            if (!Application.isPlaying) return;   // SkeletonGraphic은 플레이모드에서만 정상 초기화(canvasRenderers)

            EnsureRenderTarget();

            var canvasGo = new GameObject("__VisitorPreview_Canvas") { hideFlags = HideFlags.HideInHierarchy };
            canvasGo.SetActive(false);   // Awake 지연 — 깨진 canvasRenderers를 먼저 정리하기 위해
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = previewCam;
            canvas.planeDistance = 10f;

            var inst = Instantiate(visitorPrefab, canvasGo.transform);

            // SkeletonGraphic의 깨진 canvasRenderers({fileID: 0} 등) 제거 → Awake의 NRE 방지.
            // 비워두면 런타임에 Spine이 필요한 만큼 자동 재생성한다.
            foreach (var sg in inst.GetComponentsInChildren<SkeletonGraphic>(true))
                sg.canvasRenderers.RemoveAll(cr => cr == null);

            var rt = inst.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }

            SetLayerRecursive(canvasGo, PreviewLayer);
            canvasGo.SetActive(true);   // 이제 Awake 실행 (canvasRenderers 정리 완료)

            spawnedRoot = canvasGo;
            spawnedApplier = inst.GetComponent<AdventurerAppearanceApplier>()
                             ?? inst.GetComponentInChildren<AdventurerAppearanceApplier>();

            ApplyPreviewScale();
            ApplyToSpawned();
            RenderPreview();
        }

        private void RemoveSpawned()
        {
            StopTicking();
            if (spawnedRoot != null) DestroyImmediate(spawnedRoot);
            spawnedRoot = null;
            spawnedApplier = null;
            if (previewCam != null) previewCam.Render();   // 빈 배경으로 갱신
            Repaint();
        }

        /// <summary>스폰된 NPC에 외형을 적용한다.</summary>
        private void ApplyToSpawned()
        {
            if (spawnedApplier == null || Current == null) return;

            var pm = spawnedApplier.GetPartsManager();
            var graphic = pm != null ? pm.GetSkeletonGraphic() : null;
            if (graphic != null && !graphic.IsValid) graphic.Initialize(true);

            spawnedApplier.ApplyAppearance(Current);

            if (graphic != null) graphic.Update(0);
        }

        private void ApplyPreviewScale()
        {
            if (spawnedRoot == null) return;
            var inst = spawnedRoot.transform.childCount > 0 ? spawnedRoot.transform.GetChild(0) : null;
            if (inst != null) inst.localScale = Vector3.one * previewScale;
            RenderPreview();
        }

        /// <summary>한 프레임 렌더(정지 상태 갱신).</summary>
        private void RenderPreview()
        {
            if (previewCam == null) return;
            previewCam.Render();
            Repaint();
        }

        #endregion

        #region 애니메이션 재생 (에디터 루프)

        private void PlayAnim(string anim, bool loop)
        {
            var sg = spawnedApplier != null ? spawnedApplier.GetPartsManager()?.GetSkeletonGraphic() : null;
            if (sg == null || sg.AnimationState == null) return;

            sg.AnimationState.SetAnimation(0, anim, loop);
            StartTicking();
        }

        private void StartTicking()
        {
            if (isTicking) return;
            isTicking = true;
            EditorApplication.update += Tick;
        }

        private void StopTicking()
        {
            if (!isTicking) return;
            isTicking = false;
            EditorApplication.update -= Tick;
        }

        /// <summary>플레이모드에서 SkeletonGraphic이 자체 Update로 애니를 진행하므로 창만 다시 그린다.</summary>
        private void Tick()
        {
            if (spawnedApplier == null || previewCam == null) { StopTicking(); return; }
            Repaint();
        }

        #endregion

        #region 정리

        private void Cleanup()
        {
            RemoveSpawned();
            if (previewCam != null) { DestroyImmediate(previewCam.gameObject); previewCam = null; }
            if (previewRT != null)  { previewRT.Release(); DestroyImmediate(previewRT); previewRT = null; }
        }

        #endregion

        #region 내부 메서드

        private void RefreshDataList()
        {
            allData = CSVDiffCore.All<FixedAppearanceData>()
                .OrderBy(d => d.StaticID)
                .ToList();
            Repaint();
        }

        private const string PrefKeyPrefab = "TWR_VisitorPreview_Prefab";

        /// <summary>VisitorNPC 프리팹 선택을 EditorPrefs에 영구 저장(실행마다 다시 지정 불필요).</summary>
        private void SaveSettings()
        {
            EditorPrefs.SetString(PrefKeyPrefab, visitorPrefab != null ? AssetDatabase.GetAssetPath(visitorPrefab) : "");
        }

        private void LoadSettings()
        {
            var pp = EditorPrefs.GetString(PrefKeyPrefab, "");
            if (!string.IsNullOrEmpty(pp)) visitorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pp);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static void DrawField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(90f));
            EditorGUILayout.LabelField(value, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawColorField(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80f));
            EditorGUILayout.ColorField(GUIContent.none, color, false, false, false, GUILayout.Width(60f));
            EditorGUILayout.LabelField($"R:{color.r:F2}  G:{color.g:F2}  B:{color.b:F2}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static GUIStyle GetSelectedStyle()
        {
            var style = new GUIStyle(EditorStyles.label);
            style.normal.background = MakeTexture(2, 2, new Color(0.24f, 0.49f, 0.91f, 0.4f));
            style.normal.textColor  = Color.white;
            return style;
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex    = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        #endregion
    }
}
#endif
