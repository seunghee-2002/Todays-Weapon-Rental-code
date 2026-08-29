using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 스팅어(성공/실패음) 재생 시 하위 레이어 처리 방식.
    /// 인스펙터에서 토글해 두 방식을 A/B 테스트한다.
    /// </summary>
    public enum StingerMode
    {
        DuckTheme,  // 테마를 살짝 줄인 채 스팅어를 겹쳐 재생(표준)
        PauseTheme  // 테마를 완전히 멈추고 스팅어만 재생
    }

    /// <summary>
    /// BGM과 SFX를 관리하는 사운드 매니저 (DontDestroyOnLoad 단일 싱글톤 — 양 씬 전담)
    /// BGM은 3레이어 우선순위 스택 + 더킹으로 동작한다.
    ///   레이어1 배경음(bgmSource): 항상 재생. 테마 활성 시 볼륨을 0으로 더킹, 종료 시 페이드인 복원.
    ///   레이어2 테마음(themeSource): 상황별 배경음. 스택 top을 재생.
    ///   레이어3 스팅어(stingerSource): 강화 성공/실패 등 순간 강조음.
    /// AudioClip은 Dictionary로 중앙 관리, 호출부는 string 키로 참조.
    /// </summary>
    public class SoundManager : BaseManager<SoundManager>
    {
        [Header("Audio Sources")]
        [SerializeField] private int sfxSourceCount = 5;     // 코드에서 생성할 SFX AudioSource 개수

        // AudioSource는 코드에서 생성/관리한다 (인스펙터 배선 불필요)
        private AudioSource bgmSource;      // 레이어1 배경음
        private AudioSource themeSource;    // 레이어2 테마음
        private AudioSource stingerSource;  // 레이어3 스팅어
        private AudioSource[] sfxSources;   // 여러 SFX 동시 재생용

        [Header("Audio Clips")]
        [FormerlySerializedAs("audioClips")]
        [SerializeField] private AudioClipData[] sfxClips;   // SFX 클립 (기존 audioClips에서 이름 변경 — 데이터 보존)
        [SerializeField] private AudioClipData[] bgmClips;   // BGM 클립 (배경/테마/스팅어)

        [Header("BGM 변형 선택")]
        [SerializeField] private BgmVariantGroup[] bgmVariantGroups;  // BGMChangeView에 노출할 변형 그룹

        [Header("BGM 레이어 설정")]
        [SerializeField] private float bgmFadeDuration = 0.5f;              // 레이어 전환/더킹 페이드 시간
        [SerializeField] private StingerMode stingerMode = StingerMode.DuckTheme;
        [Range(0f, 1f)]
        [SerializeField] private float stingerDuckVolume = 0.1f;          // DuckTheme 시 테마를 줄일 비율
        [SerializeField] private float stingerRestoreDuration = 2f;       // 스팅어 종료 후 하위 레이어를 되올리는 시간

        // AudioClip Dictionary (key: clipName)
        private Dictionary<string, AudioClip> clipDictionary = new Dictionary<string, AudioClip>();

        // 이름 끝이 숫자인 클립들을 접두사로 묶은 변형 그룹 (예: Battle1~5 -> "Battle"). 값은 숫자 오름차순 키 목록.
        // SFX는 정확한 키가 없을 때 랜덤 재생용으로, BGM은 변형 선택(ResolveBgmKey)용으로 조회한다.
        private readonly Dictionary<string, List<string>> variantGroups = new Dictionary<string, List<string>>();

        // baseKey -> 선택값(RandomSelection 또는 정확한 변형 키). 미저장 그룹은 BgmVariantGroup.defaultRandom을 따른다.
        private readonly Dictionary<string, string> bgmSelections = new Dictionary<string, string>();

        // BGM 레이어 상태
        // 테마 스택. logical = 호출부가 넘긴 논리 키(ExitTheme 매칭용), resolved = 실제 재생할 클립 키.
        // push 시점에 변형을 한 번만 확정한다 — 재생할 때마다 다시 해석하면 위 패널이 닫힐 때마다 랜덤 곡이 재추첨된다.
        private readonly List<(string logical, string resolved)> themeStack = new();
        private string currentBaseKey;                                     // 배경음으로 마지막에 요청된 논리 키
        private readonly Dictionary<AudioSource, Coroutine> fadeCoroutines = new();
        private Coroutine stingerCo;
        private AudioSource stingerDuckTarget;   // 스팅어 때문에 더킹/정지된 하위 레이어
        private bool stingerDuckPaused;          // 하위 레이어를 Pause했는지 (PauseTheme 모드)

        // SFX AudioSource 풀 관리
        private int currentSFXIndex = 0;

        // 볼륨 설정
        private float bgmVolume = 1f;
        private float sfxVolume = 1f;
        private bool isBGMMuted = false;
        private bool isSFXMuted = false;
        private bool isSFXSuppressed = false; // 빠른 진행 구간 중 임시 억제
        private bool settingsLoaded = false;  // PlayerData 사운드 설정 적용 여부 (성공 시 1회만)
        private bool sceneBgmHandled = false; // OnSceneLoaded가 씬 배경음을 처리했는지 (Start 폴백 중복 방지)

        // BGM 레이어들이 목표로 하는 최대 볼륨 (뮤트/볼륨 설정 반영)
        private float BgmTarget => isBGMMuted ? 0f : bgmVolume;

        // 프로퍼티
        public float BGMVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                RefreshBgmVolumes();
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                UpdateAllSFXVolume();
            }
        }

        public bool IsBGMMuted
        {
            get => isBGMMuted;
            set
            {
                isBGMMuted = value;
                RefreshBgmVolumes();
            }
        }

        public bool IsSFXMuted
        {
            get => isSFXMuted;
            set
            {
                isSFXMuted = value;
                UpdateAllSFXVolume();
            }
        }

        /// <summary>
        /// 빠른 진행 구간 중 SFX 재생을 임시로 억제/해제한다.
        /// </summary>
        public void SuppressSFX(bool suppress)
        {
            isSFXSuppressed = suppress;
        }

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return; // 중복 인스턴스는 base.Awake에서 파괴됨

            DontDestroyOnLoad(gameObject);

            // AudioSource 생성 (bgm/theme/stinger + SFX 풀)
            CreateAudioSources();

            // AudioClip Dictionary 생성
            BuildClipDictionary();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 필요한 AudioSource를 모두 코드에서 생성한다. 인스펙터 배선이 필요 없다.
        private void CreateAudioSources()
        {
            bgmSource     = CreateSource(loop: true);
            themeSource   = CreateSource(loop: true);
            stingerSource = CreateSource(loop: false);

            int count = Mathf.Max(1, sfxSourceCount);
            sfxSources = new AudioSource[count];
            for (int i = 0; i < count; i++)
                sfxSources[i] = CreateSource(loop: false);
        }

        private AudioSource CreateSource(bool loop)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D
            src.volume = 0f;
            return src;
        }

        private void Start()
        {
            // 저장된 사운드 설정(PlayerData) 적용.
            // 보통은 최초 씬의 sceneLoaded(Awake 이후, Start 이전 발생)에서 이미 적용되므로 여기는 안전망이다.
            LoadSettingsFromPlayerData();

            // 최초 씬(보통 MainMenu)의 배경음은 OnSceneLoaded가 시작한다.
            // 어떤 이유로 sceneLoaded를 받지 못한 경우에만 폴백으로 처리한다.
            if (!sceneBgmHandled)
                HandleSceneBGM(SceneManager.GetActiveScene().name);
        }

        // 영구 저장(PlayerData)에서 볼륨/뮤트 설정을 읽어 적용한다. (성공 시 1회만 — 이후 호출은 무시)
        private void LoadSettingsFromPlayerData()
        {
            if (settingsLoaded) return;

            PlayerData playerData = LegacyManager.Instance?.GetLegacyData();
            if (playerData == null) return;

            bgmVolume = playerData.bgmVolume;
            sfxVolume = playerData.sfxVolume;
            isBGMMuted = playerData.isBGMMuted;
            isSFXMuted = playerData.isSFXMuted;

            bgmSelections.Clear();
            foreach (var pair in playerData.bgmVariantSelections)
                bgmSelections[pair.Key] = pair.Value;

            UpdateAllSFXVolume();
            RefreshBgmVolumes();

            settingsLoaded = true;
        }

        // 현재 볼륨/뮤트 설정을 PlayerData에 동기화한다(메모리만).
        // 디스크 flush는 GameManager.SaveGame()의 SaveLegacy()가 담당하므로 여기선 쓰지 않는다.
        public void SyncSettingsToPlayerData()
        {
            PlayerData playerData = LegacyManager.Instance?.GetLegacyData();
            if (playerData == null) return;

            playerData.bgmVolume = bgmVolume;
            playerData.sfxVolume = sfxVolume;
            playerData.isBGMMuted = isBGMMuted;
            playerData.isSFXMuted = isSFXMuted;

            playerData.bgmVariantSelections.Clear();
            foreach (var pair in bgmSelections)
                playerData.bgmVariantSelections[pair.Key] = pair.Value;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        private void BuildClipDictionary()
        {
            clipDictionary.Clear();
            AddClips(sfxClips);
            AddClips(bgmClips);
            BuildVariantGroups();
        }

        // "이름 + 숫자" 형태의 clipName을 접두사별로 묶는다. (Battle1~5 -> "Battle")
        private void BuildVariantGroups()
        {
            variantGroups.Clear();

            foreach (var pair in clipDictionary)
            {
                string name = pair.Key;
                int suffixLength = VariantSuffixLength(name);
                if (suffixLength == 0) continue; // 숫자로 끝나지 않거나 전부 숫자면 제외

                string prefix = name.Substring(0, name.Length - suffixLength);
                if (!variantGroups.TryGetValue(prefix, out var list))
                {
                    list = new List<string>();
                    variantGroups[prefix] = list;
                }
                list.Add(name);
            }

            // Dictionary 순회 순서는 보장되지 않는다. BGM 변경 UI의 좌우 이동 순서와 직결되므로 숫자 오름차순으로 정렬한다.
            foreach (var list in variantGroups.Values)
                list.Sort((a, b) => VariantIndex(a).CompareTo(VariantIndex(b)));
        }

        // 이름 끝에 붙은 숫자의 자릿수. 숫자로 끝나지 않거나 전부 숫자면 0.
        private static int VariantSuffixLength(string name)
        {
            int end = name.Length;
            while (end > 0 && char.IsDigit(name[end - 1])) end--;

            return (end == name.Length || end == 0) ? 0 : name.Length - end;
        }

        // 변형 번호 (Blacksmith3 -> 3). 변형 형식이 아니면 0.
        private static int VariantIndex(string name)
        {
            int len = VariantSuffixLength(name);
            if (len == 0) return 0;

            return int.TryParse(name.Substring(name.Length - len), out int index) ? index : 0;
        }

        private void AddClips(AudioClipData[] clips)
        {
            if (clips == null) return;

            foreach (var data in clips)
            {
                if (data.clip == null)
                {
                    Log.Warn($"[SoundManager] AudioClip이 null입니다: {data.clipName}");
                    continue;
                }

                if (clipDictionary.ContainsKey(data.clipName))
                {
                    Log.Warn($"[SoundManager] 중복된 clipName: {data.clipName}");
                    continue;
                }

                clipDictionary[data.clipName] = data.clip;
            }
        }

        public void Initialize(GameData gameData)
        {
            if (gameData == null)
            {
                Log.Error("SoundManager: GameData is null");
                return;
            }

            // 볼륨/뮤트 설정은 앱 시작 시 Start()에서 PlayerData로부터 이미 로드됨.
            // 인게임 배경음: TimeManager 초기화(InitializeManagers에서 SoundManager보다 먼저 수행) 이후이므로
            // 현재 Phase로 배경음을 시작하고, 이후 Phase 전환을 구독한다.
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPhaseChanged -= OnPhaseChanged; // 재초기화 대비 중복 구독 방지
                TimeManager.Instance.OnPhaseChanged += OnPhaseChanged;
                SetInGameBaseByPhase(TimeManager.Instance.CurrentPhase);
            }
            else
            {
                SetInGameBaseByPhase(TimePhase.Day);
            }
        }

        private void UpdateAllSFXVolume()
        {
            if (sfxSources == null) return;

            float targetVolume = isSFXMuted ? 0f : sfxVolume;
            foreach (var source in sfxSources)
            {
                if (source != null)
                    source.volume = targetVolume;
            }
        }

        #endregion

        #region 씬 / Phase 연동

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 최초 씬의 sceneLoaded는 Start보다 먼저 발생하므로, 배경음 시작 전에 저장된 설정부터 적용한다.
            // (적용 전에 재생하면 기본 볼륨(1)을 목표로 페이드인이 걸려 저장된 볼륨이 무시된다)
            LoadSettingsFromPlayerData();

            // 씬 전환 시 이전 씬의 테마/스팅어 잔재 정리 (패널은 UIManager가 파괴만 하고 Close를 안 타므로 여기서 리셋)
            themeStack.Clear();
            if (stingerCo != null) { StopCoroutine(stingerCo); stingerCo = null; }
            stingerSource?.Stop();
            stingerDuckTarget = null;   // 이전 씬의 더킹 대상 복원은 아래 Stop/재생으로 대체
            stingerDuckPaused = false;
            themeSource?.Stop();

            // DontDestroyOnLoad 싱글톤이라 억제 플래그가 남으면 다음 씬까지 무음이 이어진다
            isSFXSuppressed = false;

            sceneBgmHandled = true;
            HandleSceneBGM(scene.name);
        }

        // 씬별 배경음 시작. InGame 배경음은 Phase 의존이라 Initialize()에서 시작하므로 여기선 다루지 않는다.
        private void HandleSceneBGM(string sceneName)
        {
            if (sceneName == SceneController.MAIN_MENU_SCENE)
                PlayBaseBGM("MainMenu");
        }

        private void OnPhaseChanged(TimePhase phase)
        {
            // 아침이 되면 HomeMorning, 낮이 되면 HomeLoop. 저녁/밤은 낮 배경음을 유지.
            // 변형 그룹 키를 그대로 넘기고, 랜덤/고정 판단은 ResolveBgmKey가 선택값에 따라 처리한다.
            if (phase == TimePhase.Morning)
                PlayBaseBGM("HomeMorning");
            else if (phase == TimePhase.Day)
                PlayBaseBGM("HomeLoop");
        }

        private void SetInGameBaseByPhase(TimePhase phase)
        {
            if (phase == TimePhase.Morning)
                PlayBaseBGM("HomeMorning");
            else
                PlayBaseBGM("HomeLoop"); // 낮/저녁/밤
        }

        #endregion

        #region BGM 변형 선택

        /// <summary>변형 선택값이 "랜덤"임을 나타내는 예약어. 정확한 변형 키와 구분된다.</summary>
        public const string RandomSelection = "RANDOM";

        // 미리듣기가 테마 스택에 쓰는 전용 논리 키. 실제 그룹 키와 절대 겹치지 않게 예약해 둔다.
        private const string PreviewStackKey = "__BGM_PREVIEW__";

        /// <summary>BGM 변경 UI에 노출할 변형 그룹 목록.</summary>
        public IReadOnlyList<BgmVariantGroup> BgmVariantGroups =>
            bgmVariantGroups ?? System.Array.Empty<BgmVariantGroup>();

        /// <summary>그룹의 변형 키 목록 (숫자 오름차순). 변형이 없으면 빈 목록.</summary>
        public IReadOnlyList<string> GetVariantKeys(string baseKey)
        {
            if (!string.IsNullOrEmpty(baseKey) && variantGroups.TryGetValue(baseKey, out var list))
                return list;

            return System.Array.Empty<string>();
        }

        /// <summary>그룹의 현재 선택값. 저장된 값이 없으면 그룹 기본값(랜덤 또는 첫 변형).</summary>
        public string GetBgmSelection(string baseKey)
        {
            if (string.IsNullOrEmpty(baseKey)) return RandomSelection;
            if (bgmSelections.TryGetValue(baseKey, out string selection)) return selection;

            var group = FindVariantGroup(baseKey);
            if (group != null && !group.defaultRandom)
            {
                var variants = GetVariantKeys(baseKey);
                if (variants.Count > 0) return variants[0];
            }

            return RandomSelection;
        }

        /// <summary>
        /// 그룹의 변형 선택을 바꾸고 즉시 반영한다.
        /// 지금 흐르는 배경음/테마가 그 그룹이면 곡을 교체하고, 스택에 잠긴 항목의 확정 키도 갱신한다.
        /// </summary>
        public void SetBgmSelection(string baseKey, string selection)
        {
            if (string.IsNullOrEmpty(baseKey) || string.IsNullOrEmpty(selection)) return;

            bgmSelections[baseKey] = selection;

            // 스택 아래에 잠긴 같은 그룹 항목도 갱신 — 위 패널이 닫혀 다시 top이 될 때 옛 곡으로 돌아가지 않게
            for (int i = 0; i < themeStack.Count; i++)
            {
                if (themeStack[i].logical == baseKey)
                    themeStack[i] = (baseKey, ResolveBgmKey(baseKey));
            }

            if (currentBaseKey == baseKey)
                PlayBaseBGM(baseKey);

            if (themeStack.Count > 0 && themeStack[^1].logical == baseKey)
                ApplyTopTheme();
        }

        /// <summary>
        /// 미리듣기를 시작/교체한다. 논리 키를 주면 현재 선택(랜덤 포함)대로 해석해 재생한다.
        /// 테마 최상단으로 올라가므로 창을 닫을 때 반드시 <see cref="StopBgmPreview"/>로 짝을 맞춘다.
        /// </summary>
        public void PreviewBgm(string clipKey)
        {
            if (string.IsNullOrEmpty(clipKey)) return;

            string resolved = ResolveBgmKey(clipKey);
            if (!clipDictionary.ContainsKey(resolved))
            {
                Log.Warn($"[SoundManager] 미리듣기 BGM을 찾을 수 없습니다: {clipKey}");
                return;
            }

            // 이미 미리듣기 중이면 스택 항목을 교체한다. pop 후 push하면 그 사이 하위 레이어가 잠깐 올라온다.
            int index = themeStack.FindLastIndex(entry => entry.logical == PreviewStackKey);
            if (index >= 0)
                themeStack[index] = (PreviewStackKey, resolved);
            else
                themeStack.Add((PreviewStackKey, resolved));

            ApplyTopTheme();
        }

        /// <summary>미리듣기를 끝내고 원래 BGM으로 되돌린다. (미리듣기 중이 아니면 무시)</summary>
        public void StopBgmPreview() => ExitTheme(PreviewStackKey);

        // 논리 키를 실제 클립 키로 해석한다.
        // 정확한 키가 있으면 그대로 쓰고(변형 없는 슬롯), 변형 그룹이면 선택값 또는 랜덤 1곡을 고른다.
        private string ResolveBgmKey(string logicalKey)
        {
            if (string.IsNullOrEmpty(logicalKey)) return logicalKey;
            if (clipDictionary.ContainsKey(logicalKey)) return logicalKey;
            if (!variantGroups.TryGetValue(logicalKey, out var variants) || variants.Count == 0) return logicalKey;

            string selection = GetBgmSelection(logicalKey);
            if (selection != RandomSelection && variants.Contains(selection)) return selection;

            return variants[Random.Range(0, variants.Count)];
        }

        private BgmVariantGroup FindVariantGroup(string baseKey)
        {
            if (bgmVariantGroups == null) return null;

            foreach (var group in bgmVariantGroups)
            {
                if (group != null && group.baseKey == baseKey) return group;
            }

            return null;
        }

        #endregion

        #region BGM 레이어1 — 배경음

        /// <summary>
        /// 배경음(레이어1)을 설정한다. 테마가 활성 중이면 더킹된 채로 교체되어 테마 종료 시 들린다.
        /// 변형 그룹 키(예: "HomeLoop")를 주면 현재 선택에 따라 곡이 정해진다.
        /// </summary>
        public void PlayBaseBGM(string clipKey)
        {
            if (bgmSource == null || string.IsNullOrEmpty(clipKey)) return;

            if (!clipDictionary.TryGetValue(ResolveBgmKey(clipKey), out AudioClip clip))
            {
                Log.Warn($"[SoundManager] 배경 BGM을 찾을 수 없습니다: {clipKey}");
                return;
            }

            currentBaseKey = clipKey;

            // 같은 곡이 이미 재생 중이면 볼륨만 상태에 맞게 정리
            if (bgmSource.clip == clip && bgmSource.isPlaying)
            {
                RefreshBgmVolumes();
                return;
            }

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = 0f;
            bgmSource.Play();

            // 테마 활성 중이면 배경은 더킹(0) 유지, 아니면 페이드인
            FadeSource(bgmSource, themeStack.Count > 0 ? 0f : BgmTarget, bgmFadeDuration);
        }

        #endregion

        #region BGM 레이어2 — 테마음

        /// <summary>
        /// 테마음(레이어2)을 스택에 push하고 재생한다. 배경음은 더킹된다.
        /// 변형 그룹 키(예: "Blacksmith")를 주면 push 시점에 곡이 확정된다.
        /// </summary>
        public void EnterTheme(string clipKey)
        {
            if (string.IsNullOrEmpty(clipKey)) return;

            string resolved = ResolveBgmKey(clipKey);
            if (!clipDictionary.ContainsKey(resolved))
            {
                Log.Warn($"[SoundManager] 테마 BGM을 찾을 수 없습니다: {clipKey}");
                return; // 누락 시 push하지 않아 배경음이 무음 더킹되는 것을 방지
            }

            themeStack.Add((clipKey, resolved));
            ApplyTopTheme();
        }

        /// <summary>
        /// 테마음(레이어2)을 스택에서 pop한다. clipKey를 주면 해당 항목만 제거(LIFO), 없으면 최상단 제거.
        /// 스택이 비면 테마를 멈추고 배경음을 복원한다.
        /// </summary>
        public void ExitTheme(string clipKey = null)
        {
            if (themeStack.Count == 0) return;

            int idx;
            if (string.IsNullOrEmpty(clipKey))
            {
                idx = themeStack.Count - 1;
            }
            else
            {
                idx = themeStack.FindLastIndex(entry => entry.logical == clipKey);
                if (idx < 0) return; // push된 적 없는 키(클립 누락 등) → 무시
            }

            themeStack.RemoveAt(idx);
            ApplyTopTheme();
        }

        // 스택 최상단 테마를 재생하거나, 비었으면 테마 정지 + 배경음 복원.
        private void ApplyTopTheme()
        {
            if (themeSource == null) return;

            if (themeStack.Count == 0)
            {
                FadeSource(themeSource, 0f, bgmFadeDuration);
                StartCoroutine(StopThemeAfterFade());
                FadeSource(bgmSource, BgmTarget, bgmFadeDuration); // 배경음 언더킹
                return;
            }

            string key = themeStack[^1].resolved;
            if (!clipDictionary.TryGetValue(key, out AudioClip clip)) return;

            FadeSource(bgmSource, 0f, bgmFadeDuration); // 배경음 더킹

            if (themeSource.clip == clip && themeSource.isPlaying)
            {
                FadeSource(themeSource, BgmTarget, bgmFadeDuration);
            }
            else
            {
                themeSource.clip = clip;
                themeSource.loop = true;
                themeSource.volume = 0f;
                themeSource.Play();
                FadeSource(themeSource, BgmTarget, bgmFadeDuration);
            }
        }

        private IEnumerator StopThemeAfterFade()
        {
            yield return new WaitForSecondsRealtime(bgmFadeDuration);
            if (themeStack.Count == 0)
                themeSource?.Stop();
        }

        #endregion

        #region BGM 레이어3 — 스팅어

        /// <summary>
        /// 스팅어(레이어3, 성공/실패음 등)를 1회 재생한다.
        /// StingerMode에 따라 현재 들리는 레이어(테마 우선, 없으면 배경)를 더킹하거나 정지시킨 뒤,
        /// 재생이 끝나면 원래대로 복원한다.
        /// </summary>
        public void PlayStinger(string clipKey)
        {
            if (stingerSource == null || string.IsNullOrEmpty(clipKey)) return;
            if (!clipDictionary.TryGetValue(clipKey, out AudioClip clip))
            {
                Log.Warn($"[SoundManager] 스팅어 BGM을 찾을 수 없습니다: {clipKey}");
                return;
            }

            if (stingerCo != null) StopCoroutine(stingerCo);
            stingerCo = StartCoroutine(StingerRoutine(clip));
        }

        /// <summary>
        /// 재생 중인 스팅어를 즉시 끊고 하위 레이어를 복원한다.
        /// 클립 길이보다 연출이 먼저 끝나는 경우(강화 프로세스 팝업 등) 호출한다.
        /// </summary>
        public void StopStinger()
        {
            if (stingerCo != null)
            {
                StopCoroutine(stingerCo);
                stingerCo = null;
            }

            stingerSource?.Stop();
            RestoreStingerDuck();
        }

        private IEnumerator StingerRoutine(AudioClip clip)
        {
            // 이전 스팅어가 남긴 더킹/정지 상태가 있으면 먼저 복원 (중첩 방지)
            RestoreStingerDuck();

            // 현재 들리는 레이어를 하강 대상으로 결정
            stingerDuckTarget = themeStack.Count > 0 ? themeSource : bgmSource;
            stingerDuckPaused = false;

            if (stingerDuckTarget != null)
            {
                if (stingerMode == StingerMode.PauseTheme)
                {
                    stingerDuckTarget.Pause();
                    stingerDuckPaused = true;
                }
                else // DuckTheme
                {
                    FadeSource(stingerDuckTarget, BgmTarget * stingerDuckVolume, 0.15f);
                }
            }

            stingerSource.clip = clip;
            stingerSource.loop = false;
            stingerSource.volume = BgmTarget;
            stingerSource.Play();

            while (stingerSource.isPlaying)
                yield return null;

            RestoreStingerDuck();
            stingerCo = null;
        }

        // 스팅어로 더킹/정지된 하위 레이어를 원래 상태로 되돌린다. (중복 호출 안전)
        private void RestoreStingerDuck()
        {
            if (stingerDuckTarget == null) return;

            if (stingerDuckPaused)
            {
                stingerDuckTarget.UnPause();
            }
            else
            {
                // 테마가 대상이면 최대치로, 배경이 대상이면 테마 활성 여부에 따라.
                // 복원은 stingerRestoreDuration으로 느리게 — 스팅어 직후 재생되는 결과 SFX(강화 성공/실패음)와
                // 테마 볼륨 상승이 겹쳐 들리는 것을 피하기 위해 일반 페이드보다 길게 잡는다.
                float restore = (stingerDuckTarget == bgmSource && themeStack.Count > 0) ? 0f : BgmTarget;
                FadeSource(stingerDuckTarget, restore, stingerRestoreDuration);
            }

            stingerDuckTarget = null;
            stingerDuckPaused = false;
        }

        #endregion

        #region BGM 볼륨 / 페이드 헬퍼

        // 뮤트/볼륨 변경 시 각 레이어 볼륨을 현재 상태에 맞게 즉시 재적용.
        // FadeSource(duration 0)를 거쳐 진행 중인 페이드를 취소한다 — 직접 대입만 하면 페이드 코루틴이 값을 되덮는다.
        private void RefreshBgmVolumes()
        {
            bool themeActive = themeStack.Count > 0;
            if (bgmSource != null)
                FadeSource(bgmSource, LayerTarget(bgmSource, themeActive ? 0f : BgmTarget), 0f);
            if (themeSource != null && themeActive)
                FadeSource(themeSource, LayerTarget(themeSource, BgmTarget), 0f);

            // 스팅어도 BgmTarget을 목표로 재생되므로 재생 중 볼륨/뮤트 변경을 함께 반영한다
            if (stingerSource != null && stingerSource.isPlaying)
                FadeSource(stingerSource, BgmTarget, 0f);
        }

        // 스팅어에 더킹된 레이어는 더킹 볼륨을 유지한다 - 원볼륨으로 되돌리면 스팅어와 겹쳐 들린다.
        // 일시정지 방식(PauseTheme)은 소리가 나지 않으므로 원래 목표값을 그대로 둔다(UnPause 시 반영).
        private float LayerTarget(AudioSource src, float normalTarget)
        {
            if (src == stingerDuckTarget && !stingerDuckPaused)
                return normalTarget * stingerDuckVolume;

            return normalTarget;
        }

        // AudioSource 볼륨을 target까지 duration에 걸쳐 페이드 (unscaled 시간 사용)
        private void FadeSource(AudioSource src, float target, float duration)
        {
            if (src == null) return;

            if (fadeCoroutines.TryGetValue(src, out var running) && running != null)
                StopCoroutine(running);

            if (!gameObject.activeInHierarchy || duration <= 0f)
            {
                src.volume = target;
                fadeCoroutines[src] = null;
                return;
            }

            fadeCoroutines[src] = StartCoroutine(FadeRoutine(src, target, duration));
        }

        private IEnumerator FadeRoutine(AudioSource src, float target, float duration)
        {
            float start = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            src.volume = target;
            fadeCoroutines[src] = null;
        }

        #endregion

        #region SFX 제어

        /// <summary>
        /// SFX 재생 (string으로 참조)
        /// pitch: 재생 속도 배율 (1f = 기본, 2f = 2배속, 0.5f = 절반 속도)
        /// </summary>
        public void PlaySFX(string clipName, float pitch = 1f, bool ignoreSuppression = false)
        {
            if (string.IsNullOrEmpty(clipName) || clipName == "NULL") return;
            if (isSFXSuppressed && !ignoreSuppression) return;
            if (TimeManager.Instance != null && TimeManager.Instance.CurrentTimeScale > 4f)
                return;

            if (sfxSources == null || sfxSources.Length == 0)
            {
                Log.Warn("[SoundManager] SFX AudioSource가 할당되지 않았습니다.");
                return;
            }

            if (!clipDictionary.TryGetValue(clipName, out AudioClip clip))
            {
                // 정확한 키가 없으면 변형 그룹(Battle -> Battle1~5)에서 랜덤 선택
                if (variantGroups.TryGetValue(clipName, out var variants) && variants.Count > 0)
                {
                    clip = clipDictionary[variants[Random.Range(0, variants.Count)]];
                }
                else
                {
                    Log.Warn($"[SoundManager] SFX를 찾을 수 없습니다: {clipName}");
                    return;
                }
            }

            AudioSource availableSource = GetAvailableSFXSource();

            if (availableSource != null)
            {
                // 소스는 pitch를 공유하므로, 라운드로빈으로 잡은 재생 중 소스에 다른 pitch를 대입하면
                // 이미 재생 중이던 클립까지 변조된다. 그럴 때만 기존 재생을 끊는다(보이스 스틸링)
                if (availableSource.isPlaying && !Mathf.Approximately(availableSource.pitch, pitch))
                    availableSource.Stop();

                availableSource.pitch = pitch;
                availableSource.PlayOneShot(clip);
            }
            else
            {
                Log.Warn("[SoundManager] 사용 가능한 SFX AudioSource가 없습니다.");
            }
        }

        /// <summary>
        /// 재생 중인 SFX를 모두 즉시 멈춘다. 연출 스킵처럼 화면이 갑자기 전환될 때 잔향을 끊는다.
        /// BGM 레이어는 건드리지 않는다(스팅어는 StopStinger가 따로 담당).
        /// 페이드가 아니라 하드 스톱인 이유: SFX 소스의 volume은 UpdateAllSFXVolume이 직접 덮어쓰므로
        /// 페이드 코루틴을 걸면 볼륨/뮤트 설정과 충돌한다.
        /// </summary>
        public void StopAllSFX()
        {
            if (sfxSources == null) return;

            foreach (var source in sfxSources)
                source?.Stop();
        }

        /// <summary>
        /// 사용 가능한 SFX AudioSource 찾기
        /// </summary>
        private AudioSource GetAvailableSFXSource()
        {
            if (sfxSources == null || sfxSources.Length == 0)
                return null;

            // 1차: 재생 중이지 않은 AudioSource 찾기
            foreach (var source in sfxSources)
            {
                if (source != null && !source.isPlaying)
                    return source;
            }

            // 2차: 모두 재생 중이면 Round-Robin
            currentSFXIndex = (currentSFXIndex + 1) % sfxSources.Length;
            return sfxSources[currentSFXIndex];
        }

        #endregion
    }

    /// <summary>
    /// Inspector에서 AudioClip을 등록하기 위한 데이터 구조
    /// </summary>
    [System.Serializable]
    public class AudioClipData
    {
        public string clipName;  // 고유 식별자 (예: "ButtonClick", "AdventureStart")
        public AudioClip clip;   // 실제 AudioClip
    }

    /// <summary>
    /// 플레이어가 곡을 고를 수 있는 BGM 변형 그룹.
    /// 변형 클립은 baseKey + 번호로 등록한다 (예: WeaponShop1, WeaponShop2).
    /// </summary>
    [System.Serializable]
    public class BgmVariantGroup
    {
        public string baseKey;      // 코드가 호출하는 논리 키 (예: "WeaponShop")
        public string displayName;  // BGM 변경 UI에 표시할 이름 (예: "무기 상점")
        public bool defaultRandom;  // 저장된 선택이 없을 때 랜덤 재생 (끄면 1번 곡 고정)
    }
}
