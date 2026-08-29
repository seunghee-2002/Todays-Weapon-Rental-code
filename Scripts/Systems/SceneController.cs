using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 씬 전환 관리자
    /// </summary>
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        [Header("로딩")]
        [SerializeField] private GameObject loadingIndicator;   // 씬 전환 중 표시되는 지속형 로딩 화면(SceneController 자식, DontDestroyOnLoad)
        [SerializeField] private TMP_Text loadingText;          // 로딩 진행 텍스트 (예: "불러오는 중... 45%")
        [SerializeField] private float readyTimeout = 5f;       // 게임 씬 초기화(IsGameActive) 대기 안전장치(초)
        [SerializeField] private float loadStallTimeout = 15f;  // 씬 데이터 로드(progress<0.9) 정체 시 강제 진행 안전장치(초)

        // 씬 이름 상수
        public const string MAIN_MENU_SCENE = "MainMenuScene";
        public const string GAME_SCENE = "InGameScene";

        /// <summary>
        /// 에디터에서 게임 씬을 직접 실행했을 때 메인 메뉴로 자동 이동
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeEditorScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            
            // 에디터에서 게임 씬을 직접 플레이하는 경우 메인 메뉴로 이동
            if (currentScene == GAME_SCENE)
            {
                Log.Info($"[EditorPlay] Detected play from {GAME_SCENE}. Loading {MAIN_MENU_SCENE}...");
                SceneManager.LoadScene(MAIN_MENU_SCENE);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            loadingIndicator?.SetActive(false);

            // 안전망: 로딩 코루틴이 씬 활성화 프레임에 중단돼 로딩 화면을 못 끄더라도,
            // 새 씬이 실제로 올라오면 여기서 반드시 끈다.
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // 새 씬 로드 완료 시 호출 — 로딩 코루틴 생존 여부와 무관하게 로딩 화면을 내리는 안전망.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(HideWhenReady(scene.name));
        }

        #region 씬 전환

        /// <summary>
        /// 메인 메뉴 씬으로 전환
        /// </summary>
        public void LoadMainMenu()
        {
            StartCoroutine(LoadSceneRoutine(MAIN_MENU_SCENE));
        }

        /// <summary>
        /// 게임 씬으로 전환
        /// </summary>
        public void LoadGameScene()
        {
            // 이어하기/새 게임 모두 이 지점을 거치므로 게임 시작음은 여기서 한 번만 울린다.
            SoundManager.Instance?.PlaySFX("GameStart");
            StartCoroutine(LoadSceneRoutine(GAME_SCENE));
        }

        /// <summary>
        /// 현재 씬 재시작
        /// </summary>
        public void ReloadCurrentScene()
        {
            StartCoroutine(LoadSceneRoutine(SceneManager.GetActiveScene().name));
        }

        #endregion

        #region 내부 메서드

        // 로딩 화면을 띄운 채 비동기로 씬을 로드한다.
        // 게임 씬은 매니저 초기화(InitializeManagers 동기 프레임)까지 로딩 화면으로 가린 뒤 종료한다.
        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            Log.Info($"[SceneController] Loading {sceneName}...");
            loadingIndicator?.SetActive(true);
            SetLoadingText(LoadingPercentText(0));

            // 로딩이 어떤 이유로 꼬여도(progress 정체·예외) finally에서 로딩 화면을 반드시 끈다.
            // 그렇지 않으면 "불러오는 중..." 텍스트가 다음 화면에 그대로 남는다.
            try
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
                op.allowSceneActivation = false;

                // 씬 데이터 로드 완료 대기 (allowSceneActivation=false면 progress가 0.9에서 멈춤)
                // progress가 0.9에 도달하지 못하고 정체되는 경우를 대비한 타임아웃 안전장치.
                float loadElapsed = 0f;
                while (op.progress < 0.9f && loadElapsed < loadStallTimeout)
                {
                    loadElapsed += Time.unscaledDeltaTime;
                    SetLoadingText(LoadingPercentText(Mathf.RoundToInt(op.progress / 0.9f * 100f)));
                    yield return null;
                }

                // 활성화 → 새 씬의 Awake/Start(InitializeManagers 포함)가 로딩 화면 아래에서 실행됨
                SetLoadingText(LoadingPercentText(100));
                op.allowSceneActivation = true;
                while (!op.isDone)
                    yield return null;

                // 게임 씬은 매니저 초기화 완료(IsGameActive)까지 대기 — 초기화 렉을 로딩 화면으로 가림
                if (sceneName == GAME_SCENE)
                    yield return WaitForGameReady();
            }
            finally
            {
                loadingIndicator?.SetActive(false);
            }
        }

        // 새 씬이 올라온 뒤 로딩 화면을 내리는 안전망(OnSceneLoaded에서 호출).
        // 게임 씬은 초기화 렉을 가리려고 IsGameActive까지 잠깐 더 기다린 뒤 끈다.
        private IEnumerator HideWhenReady(string sceneName)
        {
            if (sceneName == GAME_SCENE)
                yield return WaitForGameReady();

            loadingIndicator?.SetActive(false);
        }

        // 게임 씬 매니저 초기화 완료(IsGameActive) 대기 — readyTimeout 초 안전장치.
        private IEnumerator WaitForGameReady()
        {
            float elapsed = 0f;
            while ((GameManager.Instance == null || !GameManager.Instance.IsGameActive) && elapsed < readyTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetLoadingText(string message)
        {
            if (loadingText != null)
                loadingText.text = message;
        }

        /// <summary>"불러오는 중... N%" 진행률 문구. MainMenuView의 동기화 문구와 같은 테이블을 쓴다.</summary>
        private static string LoadingPercentText(int percent)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Messages", "Common_LoadingPercent",
                   arguments: new object[] { new Dictionary<string, object> { { "percent", percent } } });

        #endregion
    }
}