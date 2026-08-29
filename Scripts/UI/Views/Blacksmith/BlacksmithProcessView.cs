using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 강화/진화/인챈트 프로세스 팝업 (자동 실행 + 게이지 애니메이션 + 결과)
    /// </summary>
    public class BlacksmithProcessView : BaseView
    {
        [Header("Process Type")]
        [SerializeField] private TextMeshProUGUI processTypeText;
        [SerializeField] private TextMeshProUGUI processDescriptionText;

        [Header("Gauge Animation")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private float weaponIconMinSize = 100f;
        [SerializeField] private float weaponIconMaxSize = 150f;
        [SerializeField] private GameObject gaugeArea;
        [SerializeField] private Slider gaugeSlider;
        [SerializeField] private float gaugeAnimDuration = 1.5f;

        [Header("ActionImg 애니메이션")]
        [SerializeField] private Image actionImg;
        [SerializeField] private Sprite[] processSprites;
        [SerializeField] private float actionFrameInterval = 0.15f;
        private Coroutine actionAnimCoroutine;

        private bool isEnforce; // true = 강화, false = 진화
        private bool skipRequested = false;

        [Header("스킵")]
        [SerializeField] private Button skipButton;

        private int previousEffectCount;
        private Func<(bool success, WeaponInstance result, string enforcedEffectID)> executeCallbackEnforce;
        private Func<(bool success, WeaponInstance result)> executeCallbackEvolve;
        private Action craftExecuteCallback;
        private Action onProcessComplete;
        private bool isProcessing = false;

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            canEscape = false;
        }

        public override void Open()
        {
            base.Open();
            // 강화 연출음은 하위 Blacksmith 테마를 잠시 멈췄다 복원하는 스팅어로 재생한다(테마 중첩 리셋 방지).
            SoundManager.Instance?.PlayStinger("Enforce");
        }

        public override void Close()
        {
            // 강화 연출음은 클립 길이가 연출보다 길다. 팝업이 닫히면 바로 끊고 하위 테마를 복원한다.
            SoundManager.Instance?.StopStinger();
            base.Close();
        }

        #region 초기화

        /// <summary>
        /// 강화용 초기화 (바로 실행)
        /// </summary>
        public void InitializeForEnforce(WeaponInstance weapon, Func<(bool, WeaponInstance, string)> executeFunc, Action onComplete = null)
        {
            isEnforce = true;
            InitializeCommon(C("Common_Enforce"), weapon);
            executeCallbackEnforce = executeFunc;
            executeCallbackEvolve = null;
            previousEffectCount = weapon.effects.Count;
            onProcessComplete = onComplete;
            StartCoroutine(ProcessCoroutine());
        }

        /// <summary>
        /// 진화용 초기화 (바로 실행)
        /// </summary>
        public void InitializeForEvolve(WeaponInstance weapon, Func<(bool, WeaponInstance)> executeFunc, Action onComplete = null, int prevEffectCount = 0)
        {
            isEnforce = false;
            previousEffectCount = prevEffectCount;
            InitializeCommon(C("Common_Evolve"), weapon);
            executeCallbackEvolve = executeFunc;
            executeCallbackEnforce = null;
            onProcessComplete = onComplete;
            StartCoroutine(ProcessCoroutine());
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private static string C(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", key);

        private void InitializeCommon(string processType, WeaponInstance weapon)
        {
            isProcessing = false;
            skipRequested = false;

            if (processTypeText != null)
                processTypeText.text = processType;

            if (processDescriptionText != null)
            {
                string weaponName = weapon.weaponData != null ? weapon.weaponData.DisplayName : C("Common_Weapon");
                processDescriptionText.text = L(isEnforce ? "Blacksmith_ProcessEnforcing" : "Blacksmith_ProcessEvolving",
                    ("weapon", weaponName));
            }

            if (weaponIcon != null && weapon.weaponData.inGame != null)
            {
                weaponIcon.sprite = weapon.weaponData.inGame;
                FitWeaponIconSize(weapon.weaponData.inGame);
                // 석궁만 시계방향 90도 회전 (그 외에는 회전 없음)
                weaponIcon.rectTransform.localEulerAngles =
                    weapon.weaponData.weaponType == WeaponType.Crossbow
                        ? new Vector3(0f, 0f, -90f)
                        : Vector3.zero;
            }

            gaugeArea?.SetActive(false);
            if (gaugeSlider != null)
                gaugeSlider.value = 0f;

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() => skipRequested = true);
            }
        }

        /// <summary>
        /// 제작용 초기화 (바로 실행). 성공/실패 판정 없이 게이지 연출 후 onComplete로 결과 표시.
        /// </summary>
        public void InitializeForCraft(Sprite icon, string itemName, Action executeFunc, Action onComplete = null)
        {
            isProcessing = false;
            skipRequested = false;
            craftExecuteCallback = executeFunc;
            onProcessComplete = onComplete;

            if (processTypeText != null)
                processTypeText.text = C("Common_Craft");

            if (processDescriptionText != null)
                processDescriptionText.text = L("Blacksmith_ProcessCrafting", ("item", itemName));

            if (weaponIcon != null && icon != null)
            {
                weaponIcon.sprite = icon;
                FitWeaponIconSize(icon);
                weaponIcon.rectTransform.localEulerAngles = Vector3.zero;
            }

            gaugeArea?.SetActive(false);
            if (gaugeSlider != null)
                gaugeSlider.value = 0f;

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() => skipRequested = true);
            }

            StartCoroutine(CraftProcessCoroutine());
        }

        /// <summary>
        /// 스프라이트 원본 비율을 유지하며 weaponIcon 크기 조절.
        /// 기본은 짧은 변을 weaponIconMinSize에 맞추되, 비율이 2배 이상으로 극단적이면 긴 변을 weaponIconMaxSize에 맞춤
        /// </summary>
        private void FitWeaponIconSize(Sprite sprite)
        {
            if (weaponIcon == null || sprite == null) return;
            float w = sprite.rect.width;
            float h = sprite.rect.height;
            if (w <= 0f || h <= 0f) return;

            float longSide = Mathf.Max(w, h);
            float shortSide = Mathf.Min(w, h);
            float scale = (longSide / shortSide >= 2f)
                ? weaponIconMaxSize / longSide
                : weaponIconMinSize / shortSide;
            weaponIcon.rectTransform.sizeDelta = new Vector2(w * scale, h * scale);
        }

        #endregion

        #region 프로세스 실행

        private IEnumerator ProcessCoroutine()
        {
            if (isProcessing) yield break;
            isProcessing = true;

            // 비용 차감 및 결과 판정을 애니메이션 전에 먼저 실행
            // → 골드 등 자원이 즉시 갱신되어 UI에 바로 반영됨
            bool   successResult      = false;
            WeaponInstance weaponResult = null;
            string enforcedEffectID   = null;

            if (isEnforce)
            {
                var (s, r, e) = executeCallbackEnforce.Invoke();
                successResult    = s;
                weaponResult     = r;
                enforcedEffectID = e;
            }
            else
            {
                var (s, r) = executeCallbackEvolve.Invoke();
                successResult = s;
                weaponResult  = r;
            }

            // 애니메이션 재생 (결과는 이미 확정, 연출만)
            gaugeArea?.SetActive(true);
            StartActionAnim();
            SoundManager.Instance?.PlaySFX("Upgrading");

            float elapsed = 0f;
            while (elapsed < gaugeAnimDuration)
            {
                if (skipRequested) break;
                elapsed += Time.deltaTime;
                if (gaugeSlider != null)
                    gaugeSlider.value = elapsed / gaugeAnimDuration;
                yield return null;
            }

            if (gaugeSlider != null)
                gaugeSlider.value = 1f;

            if (skipRequested)
                SoundManager.Instance?.StopAllSFX();   // 망치질(Upgrading)이 연출보다 길게 남지 않도록
            else
                yield return new WaitForSeconds(0.3f);

            skipButton?.onClick.RemoveAllListeners();
            StopActionAnim();
            UIManager.Instance.ClosePanel<BlacksmithProcessView>();

            if (isEnforce)
            {
                UIManager.Instance.OpenPanel<BlacksmithEnforceResultView>();
                UIManager.Instance.GetOrInstantiatePanel<BlacksmithEnforceResultView>()
                    ?.Initialize(successResult, weaponResult, enforcedEffectID, onProcessComplete);
            }
            else
            {
                UIManager.Instance.OpenPanel<BlacksmithEvolveResultView>();
                UIManager.Instance.GetOrInstantiatePanel<BlacksmithEvolveResultView>()
                    ?.Initialize(successResult, weaponResult, onProcessComplete, previousEffectCount);
            }

            isProcessing = false;
        }

        private IEnumerator CraftProcessCoroutine()
        {
            if (isProcessing) yield break;
            isProcessing = true;

            // 자원 차감 및 제작을 애니메이션 전에 먼저 실행 → 골드/재료가 즉시 갱신됨
            craftExecuteCallback?.Invoke();

            // 애니메이션 재생 (제작은 이미 완료, 연출만)
            gaugeArea?.SetActive(true);
            StartActionAnim();
            SoundManager.Instance?.PlaySFX("Upgrading");

            float elapsed = 0f;
            while (elapsed < gaugeAnimDuration)
            {
                if (skipRequested) break;
                elapsed += Time.deltaTime;
                if (gaugeSlider != null)
                    gaugeSlider.value = elapsed / gaugeAnimDuration;
                yield return null;
            }

            if (gaugeSlider != null)
                gaugeSlider.value = 1f;

            if (skipRequested)
                SoundManager.Instance?.StopAllSFX();   // 망치질(Upgrading)이 연출보다 길게 남지 않도록
            else
                yield return new WaitForSeconds(0.3f);

            skipButton?.onClick.RemoveAllListeners();
            StopActionAnim();
            UIManager.Instance.ClosePanel<BlacksmithProcessView>();

            // 제작은 결과창이 없으므로 강화 성공 연출음(BlacksmithSuccess)을 여기서 직접 재생한다.
            SoundManager.Instance?.PlaySFX("BlacksmithSuccess");

            onProcessComplete?.Invoke();

            isProcessing = false;
        }

        #endregion

        #region ActionImg 애니메이션

        private void StartActionAnim()
        {
            if (actionImg == null || processSprites == null || processSprites.Length == 0)
                return;

            StopActionAnim();
            actionAnimCoroutine = StartCoroutine(ActionAnimCoroutine());
        }

        private void StopActionAnim()
        {
            if (actionAnimCoroutine != null)
            {
                StopCoroutine(actionAnimCoroutine);
                actionAnimCoroutine = null;
            }
        }

        private IEnumerator ActionAnimCoroutine()
        {
            int index = 0;
            while (true)
            {
                actionImg.sprite = processSprites[index];
                index = (index + 1) % processSprites.Length;
                yield return new WaitForSeconds(actionFrameInterval);
            }
        }

        #endregion

        #region 버튼 핸들러

        public override void OnEscapeCancelled()
        {
            // 나중에 esc로 취소 등 기능 추가
            if (isProcessing) return;
        }

        #endregion

        protected override void SubscribeEvents()
        {
            // 스킵 버튼은 초기화 시점에 동적으로 할당
        }

        protected override void UnsubscribeEvents()
        {
        }
    }
}