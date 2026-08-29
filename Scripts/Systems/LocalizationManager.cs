using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 언어(로케일) 관리 매니저 (DontDestroyOnLoad 단일 싱글톤 — 양 씬 전담)
    /// 책임 4가지: 지원 로케일 고정 순서 제공 / 로케일 변경(+즉시 저장) /
    /// 변경 이벤트 릴레이 / 로케일별 폰트 폴백 순서 스왑(Han unification 대응).
    /// 저장·시작 복원은 Localization 패키지의 PlayerPrefLocaleSelector(키 selected-locale)가 전담한다.
    /// </summary>
    /// <remarks>
    /// 다른 스크립트가 Awake에서 OnLocaleChanged를 구독하므로 그보다 먼저 Instance가 준비돼야 한다.
    /// 실행 순서를 지정하지 않으면 씬 오브젝트 간 Awake 순서가 임의라 구독이 조용히 누락된다.
    /// </remarks>
    [DefaultExecutionOrder(-90)]
    public class LocalizationManager : BaseManager<LocalizationManager>
    {
        // 언어 선택 UI에 노출하는 고정 순서 (ko -> en -> ja -> zh)
        private static readonly string[] SupportedCodes = { "ko-KR", "en", "ja", "zh-Hans" };

        [Header("폰트 폴백")]
        [SerializeField] private TMP_FontAsset baseFont;     // Maplestory Light SDF
        [SerializeField] private TMP_FontAsset fallbackJP;   // ja 폰트 SDF (미제공 시 비워두면 스왑 생략)
        [SerializeField] private TMP_FontAsset fallbackSC;   // zh-Hans 폰트 SDF (미제공 시 비워두면 스왑 생략)

        public event Action<Locale> OnLocaleChanged;

        public Locale CurrentLocale => LocalizationSettings.SelectedLocale;

        private List<Locale> supportedLocales;

        /// <summary>
        /// 지원 로케일을 고정 순서(ko->en->ja->zh)로 반환.
        /// 첫 접근 시 초기화 완료를 동기 대기한다 (로컬 콘텐츠라 수 ms).
        /// </summary>
        public IReadOnlyList<Locale> SupportedLocales
        {
            get
            {
                if (supportedLocales == null)
                    BuildSupportedLocales();
                return supportedLocales;
            }
        }

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return; // 중복 인스턴스는 base.Awake에서 파괴됨

            DontDestroyOnLoad(gameObject);

            // 초기화 조기 킥 — 셀렉터 체인이 저장된 로케일(selected-locale)을 복원한다.
            // 시작 복원 로케일에는 SelectedLocaleChanged가 발화하지 않으므로
            // 초기화 완료 시점에 폰트 폴백 순서를 한 번 직접 적용한다.
            LocalizationSettings.InitializationOperation.Completed += _ => ApplyFontFallbackOrder(CurrentLocale);
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            base.OnDestroy();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 번역 테스트용 단축키 (에디터/개발 빌드 전용).
        /// F11: 다음 언어로 순환 — 부착된 라벨(LocalizeStringEvent)은 즉시 갱신된다.
        /// Shift+F11: 현재 씬 리로드 — 코드가 text에 직접 쓰는 문구까지 새 언어로 다시 그린다.
        /// </summary>
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F11)) return;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Log.Info("[LocalizationManager] 디버그: 현재 씬 리로드");
                SceneController.Instance?.ReloadCurrentScene();
                return;
            }

            CycleLocaleForDebug();
        }

        /// <summary>지원 로케일을 고정 순서로 한 칸 넘긴다.</summary>
        private void CycleLocaleForDebug()
        {
            var locales = SupportedLocales;
            if (locales == null || locales.Count == 0) return;

            int index = 0;
            for (int i = 0; i < locales.Count; i++)
            {
                if (locales[i] == CurrentLocale) { index = i; break; }
            }

            var next = locales[(index + 1) % locales.Count];
            ChangeLocale(next);
            UIPopupController.Instance?.ShowToast($"Locale: {next.Identifier.Code}", 1f);
        }
#endif

        #endregion

        #region View로부터 호출되는 메서드

        /// <summary>
        /// 로케일 변경. PlayerPrefLocaleSelector가 selected-locale 키에 자동 기록하므로
        /// 여기서는 안드로이드 강제 종료에 대비해 즉시 플러시만 한다.
        /// </summary>
        public void ChangeLocale(Locale locale)
        {
            if (locale == null || locale == CurrentLocale) return;

            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.Save();
            Log.Info($"[LocalizationManager] 로케일 변경: {locale.Identifier.Code}");
        }

        /// <summary>언어 선택 UI에 표시할 원어 이름. 항상 해당 언어로 고정 표기하며 번역하지 않는다.</summary>
        public static string GetNativeName(Locale locale)
        {
            if (locale == null) return string.Empty;

            switch (locale.Identifier.Code)
            {
                case "ko-KR":   return "한국어";
                case "en":      return "English";
                case "ja":      return "日本語";
                case "zh-Hans": return "简体中文";
                default:        return locale.Identifier.CultureInfo?.NativeName ?? locale.Identifier.Code;
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void HandleLocaleChanged(Locale locale)
        {
            ApplyFontFallbackOrder(locale);
            OnLocaleChanged?.Invoke(locale);
        }

        #endregion

        #region 내부 메서드

        private void BuildSupportedLocales()
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();

            supportedLocales = new List<Locale>();
            foreach (var code in SupportedCodes)
            {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                if (locale != null)
                    supportedLocales.Add(locale);
                else
                    Log.Warn($"[LocalizationManager] 로케일을 찾을 수 없습니다: {code}");
            }
        }

        /// <summary>
        /// ja/zh가 공유하는 한자 코드포인트(Han unification)는 폴백 1순위 폰트의 자형으로 그려지므로
        /// 로케일 변경 시 기본 폰트의 폴백 순서를 재배열한다. ja -> [JP, SC], zh-Hans -> [SC, JP], 그 외 -> [JP, SC].
        /// </summary>
        private void ApplyFontFallbackOrder(Locale locale)
        {
            if (baseFont == null || fallbackJP == null || fallbackSC == null) return;

            var fallbacks = baseFont.fallbackFontAssetTable;
            if (fallbacks == null) return;

            fallbacks.Remove(fallbackJP);
            fallbacks.Remove(fallbackSC);

            if (locale != null && locale.Identifier.Code == "zh-Hans")
            {
                fallbacks.Add(fallbackSC);
                fallbacks.Add(fallbackJP);
            }
            else
            {
                fallbacks.Add(fallbackJP);
                fallbacks.Add(fallbackSC);
            }
        }

        #endregion
    }
}
