// Assets/_Projects/Scripts/Editor/AdventurerPreviewWindow.cs
// 메뉴: Tools > Today's Weapon Rental > Adventurer Preview
//
// ─── 사용법 ──────────────────────────────────────────────────────────
//  1. 좌측 목록에서 AdventurerData(네임드 등) 선택
//  2. [모험가 프리팹]에 Charator 계열 프리팹, [무기]에 WeaponData 지정
//  3. [스폰 + 적용] → 우측 미리보기 창에 표시
//  4. [Idle]/[걷기]/[싸우기] 버튼으로 모션 확인 (창 안에서 재생)

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LayerLab.ArtMaker;
using Spine.Unity;

namespace TodaysWeaponRental.Editor
{
    public class AdventurerPreviewWindow : EditorWindow
    {
        #region 상수

        private const float ListWidth    = 200f;
        private const float PreviewSize   = 340f;
        private const int   PreviewLayer  = 5;   // UI 레이어 (전용 카메라 cullingMask)

        #endregion

        #region 상태

        private List<AdventurerData> allData     = new();
        private AdventurerData       selected;
        private Vector2              listScroll;
        private Vector2              detailScroll;
        private string              searchQuery  = "";

        // 무기 / 미리보기 입력
        private WeaponData          selectedWeapon;
        private GameObject          adventurerPrefab;
        private float               previewScale = 1f;

        // 스폰 / 렌더 리소스
        private GameObject          spawnedRoot;
        private AdventurerAppearanceApplier spawnedApplier;
        private Camera              previewCam;
        private RenderTexture       previewRT;
        private bool                isTicking;

        #endregion

        #region 열기

        [MenuItem("Tools/Today's Weapon Rental/Adventurer Preview")]
        public static void Open()
        {
            var window = GetWindow<AdventurerPreviewWindow>("Adventurer Preview");
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
            GUILayout.Label("Adventurer Preview", EditorStyles.boldLabel, GUILayout.Width(180f));
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

            EditorGUILayout.LabelField("AdventurerData 목록", EditorStyles.boldLabel);
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
            EditorGUILayout.Space(2f);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            var filtered = string.IsNullOrEmpty(searchQuery)
                ? allData
                : allData.Where(d =>
                    d.adventurerName.Contains(searchQuery) ||
                    d.StaticID.Contains(searchQuery)).ToList();

            foreach (var data in filtered)
            {
                bool isSelected = selected == data;
                GUIStyle style  = isSelected ? GetSelectedStyle() : EditorStyles.label;

                string label = $"{data.adventurerName}  [{(data.isNamed ? "네임드" : "일반")}]";
                if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true)))
                    selected = data;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 가운데 상세 / 컨트롤

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical();

            if (selected == null)
            {
                EditorGUILayout.HelpBox("좌측 목록에서 AdventurerData를 선택하세요.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

            EditorGUILayout.LabelField(selected.adventurerName, EditorStyles.boldLabel);
            DrawField("StaticID", selected.StaticID);
            DrawField("네임드",    selected.isNamed ? "O" : "X");
            DrawField("성별",      selected.gender.ToString());
            DrawField("특성",      selected.trait.ToString());

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("색상", EditorStyles.boldLabel);
            DrawColorField("피부색", selected.appearance.skinColor);
            DrawColorField("머리색", selected.appearance.hairColor);
            DrawColorField("수염색", selected.appearance.beardColor);
            DrawColorField("눈썹색", selected.appearance.browColor);

            EditorGUILayout.EndScrollView();

            DrawControls();

            EditorGUILayout.EndVertical();
        }

        /// <summary>모험가 프리팹/무기 지정 + 스폰 + 애니 컨트롤.</summary>
        private void DrawControls()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("── 무기 / 애니메이션 ──", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            adventurerPrefab = (GameObject)EditorGUILayout.ObjectField(
                "모험가 프리팹", adventurerPrefab, typeof(GameObject), false);
            selectedWeapon = (WeaponData)EditorGUILayout.ObjectField(
                "무기 (WeaponData)", selectedWeapon, typeof(WeaponData), false);
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
            GUI.enabled = Application.isPlaying && adventurerPrefab != null && selected != null;
            if (GUILayout.Button("스폰 + 적용", GUILayout.Height(26f))) Spawn();
            GUI.enabled = spawnedRoot != null;
            if (GUILayout.Button("제거", GUILayout.Height(26f), GUILayout.Width(60f))) RemoveSpawned();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUI.enabled = spawnedApplier != null;
            if (GUILayout.Button("외형 + 무기 다시 적용")) { ApplyToSpawned(); RenderPreview(); }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle"))  PlayAnim("Idle", true);
            if (GUILayout.Button("걷기"))   PlayAnim("Run_Gear", true);
            if (GUILayout.Button("싸우기")) PlayAnim(
                selectedWeapon != null ? WeaponEquipUtil.AttackAnim(selectedWeapon.weaponType) : "Attack1", true);
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;

            EditorGUILayout.HelpBox(
                "무기가 안 보이면 프리팹 SkeletonGraphic의 Multiple CanvasRenderers,\n" +
                "InGame PNG의 Read/Write, 무기 SO의 gearRightIndex(≠ -1)를 확인하세요.",
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
                var camGo = new GameObject("__AdvPreview_Cam") { hideFlags = HideFlags.HideAndDontSave };
                previewCam = camGo.AddComponent<Camera>();
                previewCam.clearFlags      = CameraClearFlags.SolidColor;
                previewCam.backgroundColor = new Color(0.2f, 0.2f, 0.22f, 1f);
                previewCam.cullingMask     = 1 << PreviewLayer;
                previewCam.orthographic    = true;
                previewCam.targetTexture   = previewRT;
                previewCam.transform.position = new Vector3(5000f, 0f, -100f);   // 씬 오브젝트와 격리(빈 공간)
            }
        }

        /// <summary>임시 Canvas + 모험가 프리팹을 생성하고 외형·무기를 적용한다.</summary>
        private void Spawn()
        {
            RemoveSpawned();
            if (adventurerPrefab == null || selected == null) return;
            if (!Application.isPlaying) return;   // SkeletonGraphic은 플레이모드에서만 정상 초기화(canvasRenderers)

            EnsureRenderTarget();

            var canvasGo = new GameObject("__AdvPreview_Canvas") { hideFlags = HideFlags.HideInHierarchy };
            canvasGo.SetActive(false);   // Awake 지연 — 깨진 canvasRenderers를 먼저 정리하기 위해
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = previewCam;
            canvas.planeDistance = 10f;

            var inst = Instantiate(adventurerPrefab, canvasGo.transform);

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

        /// <summary>스폰된 모험가에 외형 + 무기를 적용한다.</summary>
        private void ApplyToSpawned()
        {
            if (spawnedApplier == null || selected == null) return;

            var pm = spawnedApplier.GetPartsManager();
            var graphic = pm != null ? pm.GetSkeletonGraphic() : null;
            if (graphic != null && !graphic.IsValid) graphic.Initialize(true);

            spawnedApplier.ApplyAppearance(selected.appearance);

            if (selectedWeapon != null) WeaponEquipUtil.Equip(pm, selectedWeapon);
            else                        WeaponEquipUtil.Unequip(pm);

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

        /// <summary>비플레이 모드에서 SkeletonGraphic 애니를 전진시키고 카메라를 RT에 다시 그린다.</summary>
        private void Tick()
        {
            if (spawnedApplier == null || previewCam == null) { StopTicking(); return; }
            // 플레이모드: SkeletonGraphic은 자체 Update로 애니를 진행하고, 타깃 카메라도 자동 렌더된다.
            // 따라서 도구 창만 다시 그려 최신 RenderTexture를 표시한다.
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
            allData = CSVDiffCore.All<AdventurerData>()
                .OrderBy(d => d.isNamed ? 0 : 1)
                .ThenBy(d => d.adventurerName)
                .ToList();
            Repaint();
        }

        private const string PrefKeyPrefab = "TWR_AdvPreview_Prefab";
        private const string PrefKeyWeapon = "TWR_AdvPreview_Weapon";

        /// <summary>모험가 프리팹/무기 선택을 EditorPrefs에 영구 저장(실행마다 다시 지정 불필요).</summary>
        private void SaveSettings()
        {
            EditorPrefs.SetString(PrefKeyPrefab, adventurerPrefab != null ? AssetDatabase.GetAssetPath(adventurerPrefab) : "");
            EditorPrefs.SetString(PrefKeyWeapon, selectedWeapon   != null ? AssetDatabase.GetAssetPath(selectedWeapon)   : "");
        }

        private void LoadSettings()
        {
            var pp = EditorPrefs.GetString(PrefKeyPrefab, "");
            var wp = EditorPrefs.GetString(PrefKeyWeapon, "");
            if (!string.IsNullOrEmpty(pp)) adventurerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pp);
            if (!string.IsNullOrEmpty(wp)) selectedWeapon   = AssetDatabase.LoadAssetAtPath<WeaponData>(wp);
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
            EditorGUILayout.LabelField(label, GUILayout.Width(80f));
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
