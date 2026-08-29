using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace TodaysWeaponRental
{
    [Serializable]
    public class UIPanelPrefabEntry
    {
        public string viewTypeName;  // 인스펙터 가독성용 라벨. 등록 타입은 프리팹의 BaseView에서 직접 읽는다.
        public GameObject prefab;
    }

    public class UIManager : BaseManager<UIManager>
    {
        [Header("패널 프리팹 레지스트리")]
        [SerializeField] private UIPanelPrefabEntry[] panelPrefabs;

        private readonly Dictionary<Type, GameObject> prefabRegistry = new Dictionary<Type, GameObject>();
        private readonly Dictionary<Type, BaseView>   instanceCache  = new Dictionary<Type, BaseView>();
        private readonly List<BaseView>               openPanels     = new List<BaseView>();

        [SerializeField] private Transform panelParent;

        private GameObject consistenceUI;
        private ConsistenceUI consistenceUIComponent;

        // Analytics panel 값 매핑 (Documents/Analytics_이벤트_설계.md Level 2).
        // 여기 없는 패널(TutorialHighlightView 등)은 이벤트를 발행하지 않는다.
        private static readonly Dictionary<Type, string> analyticsPanelNames = new Dictionary<Type, string>
        {
            { typeof(InventoryView),            "inventory" },
            { typeof(WeaponDetailPopup),        "weapon_detail" },
            { typeof(MaterialDetailPopup),      "material_detail" },
            { typeof(ActiveItemDetailPopup),    "active_item_detail" },
            { typeof(AdventureProgressView),    "adventure_progress" },
            { typeof(AdventureResultView),      "adventure_result" },
            { typeof(WeaponShopView),           "weapon_shop" },
            { typeof(BlacksmithView),           "blacksmith" },
            { typeof(AdventureDialogueView),    "adventure_dialog" },
            { typeof(AdventurePreparationView), "adventure_preparation" },
            { typeof(SeerView),                 "seer" },
            { typeof(QuestView),                "quest" },
            { typeof(QuestBoardView),           "quest_board" },
            { typeof(LegacyUpgradeView),        "legacy_upgrade" },
            { typeof(OptionPopupView),          "option_popup" },
            { typeof(GameOverPopupView),        "game_over_popup" }
        };

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            DontDestroyOnLoad(gameObject);
            InitializePrefabRegistry();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void InitializePrefabRegistry()
        {
            if (panelPrefabs == null) return;

            foreach (var entry in panelPrefabs)
            {
                if (entry.prefab == null) continue;

                // 타입은 프리팹의 BaseView 컴포넌트에서 직접 얻는다.
                // Type.GetType(문자열) 리플렉션은 IL2CPP 매니지드 스트리핑에 취약하고
                // 인스펙터 viewTypeName 오타에도 조용히 실패한다.
                var view = entry.prefab.GetComponentInChildren<BaseView>(true);
                if (view == null)
                {
                    Log.Warn($"[UIManager] 프리팹 '{entry.prefab.name}'에서 BaseView를 찾을 수 없습니다.");
                    continue;
                }

                prefabRegistry[view.GetType()] = entry.prefab;
            }
        }

        public void SetPanelParent(Transform parent) => panelParent = parent;
        public void SetConsistenceUI(GameObject ui)
        {
            consistenceUI = ui;
            consistenceUIComponent = ui != null ? ui.GetComponentOrNull<ConsistenceUI>() : null;
        }

        // 안드로이드 뒤로가기(=ESC) 처리. InGameScene 전용이던 GameManager.Update()를 대체해 양 씬을 커버한다.
        // 튜토리얼 가드는 각 뷰의 OnEscapeCancelled(GuardBack)가 담당하므로, ESC도 닫기 버튼과 동일하게
        // CloseTopPanel → 뷰 메서드를 그대로 탄다(경로 일원화).
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (UIPopupController.Instance != null && UIPopupController.Instance.HandleEscape()) return;

            if (IsAnyPanelOpen())
            {
                CloseTopPanel();
                return;
            }

            if (SceneManager.GetActiveScene().name == SceneController.GAME_SCENE)
                OpenPanel<OptionPopupView>();
            else
                UIPopupController.Instance?.ShowPopup(
                    LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Common_QuitConfirm"),
                    onConfirm: Application.Quit, onCancel: () => { });
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CleanupInstantiatedPanels();
        }

        private void CleanupInstantiatedPanels()
        {
            // 씬 전환으로 사라지는 패널도 체류 시간을 남긴다
            foreach (var panel in openPanels)
            {
                if (panel != null)
                    SendPanelClosedAnalytics(panel);
            }
            openPanels.Clear();

            foreach (var view in instanceCache.Values)
            {
                if (view != null)
                    Destroy(view.transform.root.gameObject);
            }
            instanceCache.Clear();

            UIControllerManager.Instance?.ClearRegistry();
            TimeManager.Instance?.ResumeTime();
            consistenceUI?.SetActive(true);
            consistenceUIComponent?.SetMinimal(false);
        }

        #endregion

        #region 패널 조회 / 생성

        private BaseView GetOrInstantiatePanelInternal(Type type)
        {
            if (instanceCache.TryGetValue(type, out var cached) && cached != null)
                return cached;

            if (!prefabRegistry.TryGetValue(type, out var prefab))
            {
                Log.Error($"[UIManager] '{type.Name}' 프리팹이 등록되지 않았습니다.");
                return null;
            }

            var instance = Instantiate(prefab, panelParent);

            // 비활성 프리팹은 Instantiate 시 Awake가 호출되지 않는다.
            // 강제로 활성화해 Awake를 실행시키면 BaseController가 UIControllerManager에 등록된다.
            // 이후 BaseView.Awake()가 자동으로 다시 SetActive(false)를 호출해 패널을 숨긴다.
            if (!instance.activeSelf)
                instance.SetActive(true);

            var view = instance.GetComponentInChildren<BaseView>(true);
            if (view == null)
            {
                Log.Error($"[UIManager] '{type.Name}' 프리팹에서 BaseView를 찾을 수 없습니다.");
                Destroy(instance);
                return null;
            }

            instanceCache[type] = view;
            return view;
        }

        // 캐시에 없으면 인스턴스화해 반환. 사용을 보장해야 하는 경우 사용.
        public T GetOrInstantiatePanel<T>() where T : BaseView
            => GetOrInstantiatePanelInternal(typeof(T)) as T;

        // 캐시 조회 전용. 인스턴스화되지 않았으면 null 반환. IsOpen 체크 등 조회 용도로 사용.
        public T GetPanel<T>() where T : BaseView
        {
            instanceCache.TryGetValue(typeof(T), out var view);
            return view as T;
        }

        #endregion

        #region 패널 열기 / 닫기

        public void OpenPanel<T>(Action beforeOpen = null) where T : BaseView
        {
            var panel = GetOrInstantiatePanelInternal(typeof(T));
            if (panel == null) return;

            // 이미 열려 있으면 스택 중복 추가 방지 후 최상위 이동만 수행
            bool wasOpen = openPanels.Remove(panel);

            beforeOpen?.Invoke();
            panel.Open();
            panel.transform.SetAsLastSibling();
            openPanels.Add(panel);
            CheckPauseTime();
            UpdateConsistenceUIState();

            // 재오픈은 중복 발행하지 않는다. 컨트롤러 세팅이 끝난 뒤 추가 파라미터를 읽어야 하므로 맨 끝에 둔다.
            if (!wasOpen) SendPanelOpenedAnalytics(panel);
        }

        public void ClosePanel<T>() where T : BaseView
        {
            if (!instanceCache.TryGetValue(typeof(T), out var panel) || panel == null) return;

            bool wasOpen = openPanels.Remove(panel);
            panel.Close();
            if (wasOpen) SendPanelClosedAnalytics(panel);
            CheckResumeTime();
            UpdateConsistenceUIState();
        }

        public void CloseTopPanel()
        {
            if (openPanels.Count == 0) return;

            var top = openPanels[^1];
            if (!top.CanEscape)
            {
                top.OnEscapeCancelled();
                return;
            }

            openPanels.RemoveAt(openPanels.Count - 1);
            top.OnEscapeClicked();
            top.Close();
            SendPanelClosedAnalytics(top);
            CheckResumeTime();
            UpdateConsistenceUIState();
        }

        public bool IsAnyPanelOpen() => openPanels.Count > 0;

        /// <summary>열린 모든 패널을 강제로 닫는다(CanEscape 가드 무시). 튜토리얼 스킵 등 전역 정리용.</summary>
        public void CloseAllPanels()
        {
            if (openPanels.Count == 0) return;

            foreach (var panel in openPanels.ToArray())
            {
                panel.Close();
                SendPanelClosedAnalytics(panel);
            }

            openPanels.Clear();
            CheckResumeTime();
            UpdateConsistenceUIState();
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Level 2 panel_opened 발행. 화이트리스트에 없는 패널은 무시한다.
        /// 패널별 추가 파라미터(G23 blacksmith_type, G26 delay_real_min)는 여기서 채운다
        /// - AnalyticsManager가 Controller를 참조하지 않도록 하기 위함.
        /// </summary>
        private void SendPanelOpenedAnalytics(BaseView panel)
        {
            string name = GetAnalyticsPanelName(panel);
            if (name == null) return;

            Dictionary<string, object> extra = null;

            if (name == "blacksmith")
            {
                var blacksmith = BlacksmithManager.Instance?.CurrentBlacksmith;
                if (blacksmith != null)
                {
                    extra = new Dictionary<string, object>
                    {
                        { "blacksmith_type", BlacksmithManager.GetTypeAnalyticsName(blacksmith.type) }
                    };
                }
            }
            else if (name == "adventure_result")
            {
                var adventure = UIControllerManager.Instance?.GetController<AdventureResultController>()?.CurrentAdventure;
                if (adventure != null && adventure.completedAtUtcTicks > 0)
                {
                    long elapsedTicks = DateTime.UtcNow.Ticks - adventure.completedAtUtcTicks;
                    extra = new Dictionary<string, object>
                    {
                        { "delay_real_min", (int)Math.Max(0, elapsedTicks / TimeSpan.TicksPerMinute) }
                    };
                }
            }

            AnalyticsManager.Instance?.SendPanelOpened(name, extra);
        }

        /// <summary>
        /// Level 2 panel_closed 발행. 닫기 4경로(ClosePanel / CloseTopPanel / CloseAllPanels /
        /// CleanupInstantiatedPanels)의 공통 발행 지점이다.
        /// </summary>
        private void SendPanelClosedAnalytics(BaseView panel)
        {
            string name = GetAnalyticsPanelName(panel);
            if (name != null)
                AnalyticsManager.Instance?.SendPanelClosed(name);
        }

        // 아침 이벤트 9종은 하나의 panel 값으로 접는다(유형 구분은 morning_event_shown의 event_type이 담당).
        private static string GetAnalyticsPanelName(BaseView panel)
        {
            if (panel is MorningEventViewBase) return "morning_event";
            return analyticsPanelNames.TryGetValue(panel.GetType(), out string name) ? name : null;
        }

        #endregion

        #region 시간 제어

        private void CheckPauseTime()
        {
            foreach (var panel in openPanels)
            {
                if (panel.PauseTimeOnOpen)
                {
                    TimeManager.Instance?.PauseTime();
                    return;
                }
            }
        }

        private void CheckResumeTime()
        {
            if (HasTimePausingPanel()) return;
            TimeManager.Instance?.ResumeTime();
        }

        /// <summary>
        /// 열린 패널 중 시간 정지를 요구하는 것이 있는가.
        /// PauseTime/ResumeTime은 카운터가 아니라 단일 플래그라, UIManager 밖에서 재개할 때도
        /// 이 판정을 거쳐야 열려 있는 패널의 정지가 풀리지 않는다 (TimeManager.GoToNextDay).
        /// </summary>
        public bool HasTimePausingPanel()
        {
            foreach (var panel in openPanels)
            {
                if (panel.PauseTimeOnOpen) return true;
            }
            return false;
        }

        #endregion

        #region UI 표시 제어

        /// <summary>
        /// 열린 패널 스택을 보고 consistenceUI 상태를 갱신한다.
        /// 우선순위: 전체 숨김(HideUIOnOpen) > minimal(MinimalUIOnOpen) > 전체 표시.
        /// </summary>
        private void UpdateConsistenceUIState()
        {
            if (consistenceUI == null) return;

            bool anyHide = false;
            bool anyMinimal = false;
            foreach (var panel in openPanels)
            {
                if (panel.HideUIOnOpen) { anyHide = true; break; }
                if (panel.MinimalUIOnOpen) anyMinimal = true;
            }

            if (anyHide)
            {
                consistenceUI.SetActive(false);
                return;
            }

            consistenceUI.SetActive(true);
            consistenceUIComponent?.SetMinimal(anyMinimal);
        }

        #endregion
    }
}
