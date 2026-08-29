using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 옵션 팝업 View
    /// BGM/SFX 볼륨 조절 및 저장 기능 제공
    /// </summary>
    public class OptionPopupView : BaseView
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

        [Header("Dialogue Settings")]
        [SerializeField] private Toggle npcGreetingSkipToggle;
        [SerializeField] private GameObject npcGreetingSkipOnObj;
        [SerializeField] private GameObject npcGreetingSkipOffObj;

        [Header("Adventure Skip Settings")]
        [SerializeField] private Toggle adventureSkipToggle;
        [SerializeField] private GameObject adventureSkipOnObj;
        [SerializeField] private GameObject adventureSkipOffObj;
        
        [Header("Player Info")]
        [SerializeField] private TextMeshProUGUI playerIdText;
        [SerializeField] private TextMeshProUGUI nicknameText;
        [SerializeField] private Button changeNicknameButton;

        [Header("Buttons")]
        [SerializeField] private Button bgmChangeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button closeButton;

        // SFX 테스트 사운드 스로틀 (드래그 중 중첩 재생/클리핑 방지)
        private float lastSfxTestTime;
        private const float SfxTestInterval = 0.1f;

        #region 초기화
        
        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = true;
            canEscape = true;
        }

        public override void Open()
        {
            base.Open();
            LoadCurrentSettings();
        }
        
        protected override void SubscribeEvents()
        {
            bgmSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
            sfxSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
            bgmChangeButton?.onClick.AddListener(OnBGMChangeClicked);
            saveButton?.onClick.AddListener(OnSaveClicked);
            mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
            npcGreetingSkipToggle?.onValueChanged.AddListener(OnNPCGreetingSkipToggleChanged);
            adventureSkipToggle?.onValueChanged.AddListener(OnAdventureSkipToggleChanged);
            bgmToggle?.onValueChanged.AddListener(OnBGMToggleChanged);
            sfxToggle?.onValueChanged.AddListener(OnSFXToggleChanged);
            changeNicknameButton?.onClick.AddListener(OnChangeNicknameClicked);
            if (NicknameManager.Instance != null)
            {
                NicknameManager.Instance.OnNicknameChanged += OnNicknameChanged;
                // 서버 로드가 늦게 끝나는 경우(메인메뉴를 거치지 않고 바로 진입 등) 표시를 채운다
                NicknameManager.Instance.OnChangeCountLoaded += RefreshNicknameDisplay;
            }
        }

        protected override void UnsubscribeEvents()
        {
            bgmSlider?.onValueChanged.RemoveAllListeners();
            sfxSlider?.onValueChanged.RemoveAllListeners();
            bgmChangeButton?.onClick.RemoveAllListeners();
            saveButton?.onClick.RemoveAllListeners();
            mainMenuButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
            npcGreetingSkipToggle?.onValueChanged.RemoveAllListeners();
            adventureSkipToggle?.onValueChanged.RemoveAllListeners();
            bgmToggle?.onValueChanged.RemoveAllListeners();
            sfxToggle?.onValueChanged.RemoveAllListeners();
            changeNicknameButton?.onClick.RemoveAllListeners();
            if (NicknameManager.Instance != null)
            {
                NicknameManager.Instance.OnNicknameChanged -= OnNicknameChanged;
                NicknameManager.Instance.OnChangeCountLoaded -= RefreshNicknameDisplay;
            }
        }

        /// <summary>
        /// 현재 사운드 설정 불러오기 (표시 전용).
        /// 슬라이더/토글은 Notify 없이 세팅하고 on/off 오브젝트를 직접 갱신한다.
        /// 이벤트 경유 갱신은 값이 기존과 같으면 발화하지 않아 첫 표시가 어긋날 수 있다.
        /// </summary>
        private void LoadCurrentSettings()
        {
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

            // NPC 대화 토글
            bool npcSkipOn = !LegacyManager.Instance.PlayerData.isNPCDialogueEnabled;
            npcGreetingSkipToggle?.SetIsOnWithoutNotify(npcSkipOn);
            npcGreetingSkipOnObj?.SetActive(npcSkipOn);
            npcGreetingSkipOffObj?.SetActive(!npcSkipOn);

            // 모험 스킵 토글
            bool adventureSkipOn = LegacyManager.Instance.PlayerData.isAdventureSkipEnabled;
            adventureSkipToggle?.SetIsOnWithoutNotify(adventureSkipOn);
            adventureSkipOnObj?.SetActive(adventureSkipOn);
            adventureSkipOffObj?.SetActive(!adventureSkipOn);

            if (playerIdText != null)
                playerIdText.text = UGSManager.Instance?.PlayerId ?? "-";

            RefreshNicknameDisplay();
        }

        private void RefreshNicknameDisplay()
        {
            if (nicknameText == null) return;
            string nick = NicknameManager.Instance?.CurrentNickname
                ?? LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Option_NicknamePlaceholder");
            nicknameText.text = nick;
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
        /// 저장 완료 확인 메시지 표시
        /// </summary>
        private void ShowSaveConfirmation()
        {
            UIPopupController.Instance?.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Save_Completed"),
                type: PopupSfxType.Notify);
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

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void OnSaveClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveGame(); // SaveGame이 사운드 설정도 PlayerData에 함께 저장

                // 저장 완료 피드백
                ShowSaveConfirmation();
            }
        }

        /// <summary>
        /// 게임 저장 후 메인메뉴로 이동
        /// </summary>
        private void OnMainMenuClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveGame(); // SaveGame이 사운드 설정도 PlayerData에 함께 저장
                SceneController.Instance?.LoadMainMenu();
            }
        }

        /// <summary>
        /// NPC 대화 스킵 토글 변경
        /// </summary>
        private void OnNPCGreetingSkipToggleChanged(bool isOn)
        {
            if (LegacyManager.Instance?.PlayerData != null)
            {
                LegacyManager.Instance.PlayerData.isNPCDialogueEnabled = !isOn;
            }

            if (npcGreetingSkipOnObj != null)
            {
                npcGreetingSkipOnObj.SetActive(isOn);
            }

            if (npcGreetingSkipOffObj != null)
            {
                npcGreetingSkipOffObj.SetActive(!isOn);
            }
        }

        /// <summary>
        /// 모험 스킵 토글 변경
        /// </summary>
        private void OnAdventureSkipToggleChanged(bool isOn)
        {
            if (LegacyManager.Instance?.PlayerData != null)
            {
                LegacyManager.Instance.PlayerData.isAdventureSkipEnabled = isOn;
            }

            if (adventureSkipOnObj != null)
            {
                adventureSkipOnObj.SetActive(isOn);
            }

            if (adventureSkipOffObj != null)
            {
                adventureSkipOffObj.SetActive(!isOn);
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭
        /// </summary>
        private void OnCloseClicked()
        {
            UIManager.Instance?.ClosePanel<OptionPopupView>();
        }

        private void OnChangeNicknameClicked()
        {
            UIManager.Instance?.OpenPanel<NicknameChangePopupView>();
        }

        private void OnBGMChangeClicked()
        {
            UIManager.Instance?.OpenPanel<BGMChangeView>();
        }

        private void OnNicknameChanged(string _) => RefreshNicknameDisplay();

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}