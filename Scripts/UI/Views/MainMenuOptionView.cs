using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// MainMenuScene 전용 옵션 팝업 View.
    /// BGM/SFX 볼륨, 데이터 수집 철회, 데이터 복원, 데이터 초기화를 제공한다.
    /// InGame의 OptionPopupView와 달리 게임 저장/메인메뉴 이동 기능은 없다.
    /// </summary>
    public class MainMenuOptionView : BaseView
    {
        [Header("BGM Settings")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Toggle bgmToggle;
        [SerializeField] private GameObject bgmOnObj;
        [SerializeField] private GameObject bgmOffObj;
        [SerializeField] private TextMeshProUGUI bgmValueText;

        [Header("SFX Settings")]
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private GameObject sfxOnObj;
        [SerializeField] private GameObject sfxOffObj;
        [SerializeField] private TextMeshProUGUI sfxValueText;

        [Header("데이터 수집 철회")]
        [SerializeField] private Toggle analyticsOptOutToggle;
        [SerializeField] private GameObject analyticsOptOutOnObj;
        [SerializeField] private GameObject analyticsOptOutOffObj;

        [Header("Buttons")]
        [SerializeField] private Button bgmChangeButton;
        [SerializeField] private Button languageButton;
        [SerializeField] private Image languageImage;   // 언어 버튼의 현재 언어 국기
        [SerializeField] private Button dataRestoreButton;
        [SerializeField] private Button resetDataButton;
        [SerializeField] private Button closeButton;

        [Header("기타")]
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private GameObject loadingIndicator;

        private MainMenuOptionController controller;

        // SFX 테스트 사운드 스로틀 (드래그 중 중첩 재생/클리핑 방지)
        private float lastSfxTestTime;
        private const float SfxTestInterval = 0.1f;

        public void SetController(MainMenuOptionController ctrl) => controller = ctrl;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;    // MainMenuScene에는 게임 시간이 흐르지 않는다
            isCanClickOverlay = false;  // 오버레이 클릭으로 닫지 않는다 (overlayButton도 연결하지 않을 것)
            canEscape = true;
        }

        public override void Open()
        {
            base.Open();
            loadingIndicator?.SetActive(false);
            LoadCurrentSettings();
        }

        /// <summary>
        /// 닫힐 때 사운드 설정을 영구 저장한다.
        /// MainMenuScene에는 GameManager.SaveGame() 흐름이 없으므로 직접 저장해야 하며,
        /// 닫기 버튼/ESC/오버레이 클릭 어느 경로로 닫혀도 저장되도록 Close에 둔다.
        /// </summary>
        public override void Close()
        {
            base.Close();

            SoundManager.Instance?.SyncSettingsToPlayerData();
            LegacyManager.Instance?.SaveLegacy();
        }

        protected override void SubscribeEvents()
        {
            bgmSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
            sfxSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
            bgmToggle?.onValueChanged.AddListener(OnBGMToggleChanged);
            sfxToggle?.onValueChanged.AddListener(OnSFXToggleChanged);
            analyticsOptOutToggle?.onValueChanged.AddListener(OnAnalyticsOptOutToggleChanged);
            bgmChangeButton?.onClick.AddListener(OnBGMChangeClicked);
            languageButton?.onClick.AddListener(OnLanguageClicked);
            dataRestoreButton?.onClick.AddListener(() => controller?.OnDataRestoreClicked());
            resetDataButton?.onClick.AddListener(() => controller?.OnResetDataClicked());
            closeButton?.onClick.AddListener(OnCloseClicked);
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLocaleChanged += OnLocaleChanged;
        }

        protected override void UnsubscribeEvents()
        {
            bgmSlider?.onValueChanged.RemoveAllListeners();
            sfxSlider?.onValueChanged.RemoveAllListeners();
            bgmToggle?.onValueChanged.RemoveAllListeners();
            sfxToggle?.onValueChanged.RemoveAllListeners();
            analyticsOptOutToggle?.onValueChanged.RemoveAllListeners();
            bgmChangeButton?.onClick.RemoveAllListeners();
            languageButton?.onClick.RemoveAllListeners();
            dataRestoreButton?.onClick.RemoveAllListeners();
            resetDataButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLocaleChanged -= OnLocaleChanged;
        }

        /// <summary>
        /// 현재 설정 불러오기 (표시 전용).
        /// 슬라이더/토글은 Notify 없이 세팅하고 on/off 오브젝트를 직접 갱신한다.
        /// 이벤트 경유 갱신은 값이 기존과 같으면 발화하지 않아 첫 표시가 어긋날 수 있다.
        /// </summary>
        private void LoadCurrentSettings()
        {
            if (versionText != null)
                versionText.text = $"v{Application.version}";

            // 수집 철회 토글: ON = 철회(수집 안 함)
            bool optOut = AnalyticsManager.Instance?.IsOptedOut ?? false;
            analyticsOptOutToggle?.SetIsOnWithoutNotify(optOut);
            analyticsOptOutOnObj?.SetActive(optOut);
            analyticsOptOutOffObj?.SetActive(!optOut);

            RefreshLanguageIcon();

            if (SoundManager.Instance == null) return;

            // BGM: 뮤트이거나 볼륨 0이면 off 표시 (토글-끔 동작과 동일하게 슬라이더/텍스트도 0)
            float bgmVolume = SoundManager.Instance.BGMVolume;
            bool bgmOn = !SoundManager.Instance.IsBGMMuted && bgmVolume > 0f;
            bgmSlider?.SetValueWithoutNotify(bgmOn ? bgmVolume : 0f);
            bgmToggle?.SetIsOnWithoutNotify(bgmOn);
            bgmOnObj?.SetActive(bgmOn);
            bgmOffObj?.SetActive(!bgmOn);
            UpdateBGMText(bgmOn ? bgmVolume : 0f);

            // SFX: BGM과 동일 규칙
            float sfxVolume = SoundManager.Instance.SFXVolume;
            bool sfxOn = !SoundManager.Instance.IsSFXMuted && sfxVolume > 0f;
            sfxSlider?.SetValueWithoutNotify(sfxOn ? sfxVolume : 0f);
            sfxToggle?.SetIsOnWithoutNotify(sfxOn);
            sfxOnObj?.SetActive(sfxOn);
            sfxOffObj?.SetActive(!sfxOn);
            UpdateSFXText(sfxOn ? sfxVolume : 0f);
        }

        #endregion

        #region UI 업데이트 메서드

        private void UpdateBGMText(float value)
        {
            if (bgmValueText != null)
            {
                bgmValueText.text = Mathf.RoundToInt(value * 100).ToString();
            }
        }

        private void UpdateSFXText(float value)
        {
            if (sfxValueText != null)
            {
                sfxValueText.text = Mathf.RoundToInt(value * 100).ToString();
            }
        }

        /// <summary>
        /// 언어 버튼의 국기 아이콘을 현재 로케일에 맞춰 갱신한다.
        /// </summary>
        private void RefreshLanguageIcon()
        {
            if (languageImage == null) return;

            var locale = LocalizationManager.Instance?.CurrentLocale;
            if (locale == null) return;

            var icon = IconManager.Instance?.GetIconByLocaleCode(locale.Identifier.Code);
            if (icon != null)
                languageImage.sprite = icon;
        }

        /// <summary>
        /// 데이터 초기화 진행 중 표시. 진행 중에는 버튼과 ESC를 모두 잠근다.
        /// </summary>
        public void SetLoading(bool loading)
        {
            loadingIndicator?.SetActive(loading);

            if (dataRestoreButton != null) dataRestoreButton.interactable = !loading;
            if (resetDataButton   != null) resetDataButton.interactable   = !loading;
            if (closeButton       != null) closeButton.interactable       = !loading;

            canEscape = !loading;
        }

        #endregion

        #region 이벤트 핸들러

        private void OnBGMVolumeChanged(float value)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.BGMVolume = value;
                UpdateBGMText(value);

                if (bgmToggle != null)
                {
                    bgmToggle.isOn = value > 0;
                }
            }
        }

        private void OnBGMToggleChanged(bool isOn)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.IsBGMMuted = !isOn;
                bgmOnObj?.SetActive(isOn);
                bgmOffObj?.SetActive(!isOn);

                if (isOn)
                {
                    // 저장된 볼륨이 0이면 50으로 켠다 (0인 채 켜지면 무음이라 켠 의미가 없음)
                    float volume = SoundManager.Instance.BGMVolume;
                    bgmSlider.value = volume > 0f ? volume : 0.5f;
                }
                else
                {
                    bgmSlider.SetValueWithoutNotify(0);
                    UpdateBGMText(0);
                }
            }
        }

        private void OnSFXVolumeChanged(float value)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SFXVolume = value;
                UpdateSFXText(value);

                if (sfxToggle != null)
                {
                    sfxToggle.isOn = value > 0;
                }

                // SFX 테스트 사운드 재생 — 드래그 중 매 프레임 재생되어 소리가 겹치는(클리핑) 것을 방지하고자
                // 최소 간격으로 스로틀. 볼륨 0일 땐 재생 생략.
                if (value > 0f && Time.unscaledTime - lastSfxTestTime >= SfxTestInterval)
                {
                    lastSfxTestTime = Time.unscaledTime;
                    SoundManager.Instance.PlaySFX("ButtonClick");
                }
            }
        }

        private void OnSFXToggleChanged(bool isOn)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.IsSFXMuted = !isOn;
                sfxOnObj?.SetActive(isOn);
                sfxOffObj?.SetActive(!isOn);

                if (isOn)
                {
                    // 저장된 볼륨이 0이면 50으로 켠다 (0인 채 켜지면 무음이라 켠 의미가 없음)
                    float volume = SoundManager.Instance.SFXVolume;
                    sfxSlider.value = volume > 0f ? volume : 0.5f;
                }
                else
                {
                    sfxSlider.SetValueWithoutNotify(0);
                    UpdateSFXText(0);
                }
            }
        }

        private void OnBGMChangeClicked()
        {
            UIManager.Instance?.OpenPanel<BGMChangeView>();
        }

        private void OnLanguageClicked()
        {
            UIManager.Instance?.OpenPanel<TranslateView>();
        }

        /// <summary>TranslateView가 위에 열린 채 언어가 바뀌어도 국기가 따라오도록 갱신한다.</summary>
        private void OnLocaleChanged(Locale _)
        {
            RefreshLanguageIcon();
        }

        /// <summary>데이터 수집 철회 토글 변경. ON = 철회(수집 중단)</summary>
        private void OnAnalyticsOptOutToggleChanged(bool isOn)
        {
            analyticsOptOutOnObj?.SetActive(isOn);
            analyticsOptOutOffObj?.SetActive(!isOn);

            controller?.OnAnalyticsOptOutChanged(isOn);
        }

        // ESC/오버레이 클릭은 UIManager.CloseTopPanel이 Close()까지 처리하므로
        // OnEscapeClicked를 오버라이드하지 않는다. 오버라이드해서 ClosePanel을 부르면
        // Close()가 두 번 실행돼 설정 저장이 중복된다.
        private void OnCloseClicked()
        {
            UIManager.Instance?.ClosePanel<MainMenuOptionView>();
        }

        #endregion
    }
}
