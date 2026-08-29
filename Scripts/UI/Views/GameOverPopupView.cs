using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 게임오버 결과 팝업.
    /// 일수/평판/모험 보너스를 순서대로 보여준 뒤 유산 획득 애니메이션을 재생한다.
    /// 클릭 시 전체 애니메이션을 즉시 스킵한다.
    /// </summary>
    public class GameOverPopupView : BaseView
    {
        [Header("타이틀")]
        [SerializeField] private CanvasGroup titleGroup;

        [Header("일수 보너스")]
        [SerializeField] private CanvasGroup dayRow;
        [SerializeField] private TextMeshProUGUI daySourceText;
        [SerializeField] private TextMeshProUGUI dayBonusText;

        [Header("평판 보너스")]
        [SerializeField] private CanvasGroup reputationRow;
        [SerializeField] private TextMeshProUGUI reputationSourceText;
        [SerializeField] private TextMeshProUGUI reputationBonusText;

        [Header("모험 보너스")]
        [SerializeField] private CanvasGroup adventureRow;
        [SerializeField] private TextMeshProUGUI adventureSourceText;
        [SerializeField] private TextMeshProUGUI adventureBonusText;

        [Header("유산 합계")]
        [SerializeField] private CanvasGroup separatorGroup;
        [SerializeField] private CanvasGroup earnedLegacyGroup;
        [SerializeField] private TextMeshProUGUI earnedLegacyText;
        [SerializeField] private TextMeshProUGUI totalLegacyText;

        [Header("확인 버튼")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private CanvasGroup confirmButtonGroup;

        [Header("스킵 오버레이")]
        [SerializeField] private GameObject skipOverlayObject;
        [SerializeField] private Button skipOverlayButton;

        [SerializeField] private GameOverPopupController controller;

        // 데이터
        private int days;
        private int totalCumulativeReputation;
        private int totalAdventures;
        private int earnedLegacy;
        private int previousTotal;
        private int dayBonus;
        private int reputationBonus;
        private int adventureBonus;
        private string goldHex;

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = true;
            canEscape = false;
        }

        protected override void SubscribeEvents()
        {
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        protected override void UnsubscribeEvents()
        {
            confirmButton?.onClick.RemoveAllListeners();
        }
        
        #region 초기화

        public void Initialize(int days, int totalCumulativeReputation, int totalAdventures,
                               int earnedLegacy, int previousTotal)
        {
            this.days                      = days;
            this.totalCumulativeReputation = totalCumulativeReputation;
            this.totalAdventures           = totalAdventures;
            this.earnedLegacy              = earnedLegacy;
            this.previousTotal             = previousTotal;

            dayBonus        = days;
            reputationBonus = totalCumulativeReputation / 50;
            adventureBonus  = totalAdventures / 5;
            goldHex         = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGoldColor());

            ResetToInitialState();

            skipOverlayObject?.SetActive(true);
            if (skipOverlayButton != null)
            {
                skipOverlayButton.onClick.RemoveAllListeners();
                skipOverlayButton.onClick.AddListener(SkipToEnd);
            }

            StartCoroutine(PlayAnimationCoroutine());
        }

        private void ResetToInitialState()
        {
            SetAlpha(titleGroup, 0f);
            SetAlpha(dayRow, 0f);
            SetAlpha(reputationRow, 0f);
            SetAlpha(adventureRow, 0f);
            SetAlpha(separatorGroup, 0f);
            SetAlpha(earnedLegacyGroup, 0f);
            SetAlpha(confirmButtonGroup, 0f);

            daySourceText.text        = Stat("GameOver_DaySource", 0);
            dayBonusText.text         = "";
            reputationSourceText.text = Stat("GameOver_ReputationSource", 0);
            reputationBonusText.text  = "";
            adventureSourceText.text  = Stat("GameOver_AdventureSource", 0);
            adventureBonusText.text   = "";
            earnedLegacyText.text     = $"<color={goldHex}>+{earnedLegacy}</color>";
            totalLegacyText.text      = $"{previousTotal} <color={goldHex}>(+0)</color>";

            if (confirmButton != null) confirmButton.interactable = false;
        }

        #endregion

        #region 애니메이션 코루틴

        private IEnumerator PlayAnimationCoroutine()
        {
            // 타이틀
            yield return Fade(titleGroup, 0f, 1f, 0.25f);
            yield return Wait(0.2f);

            // 일수 보너스 행
            yield return Fade(dayRow, 0f, 1f, 0.2f);
            yield return CountUp(daySourceText, "GameOver_DaySource", 0, days, 0.35f);
            yield return Wait(0.15f);
            daySourceText.text = "<s>" + Stat("GameOver_DaySource", days) + "</s>";
            yield return CountUp(dayBonusText, null, 0, dayBonus, 0.25f, goldHex);
            yield return Wait(0.2f);

            // 평판 보너스 행
            yield return Fade(reputationRow, 0f, 1f, 0.2f);
            yield return CountUp(reputationSourceText, "GameOver_ReputationSource", 0, totalCumulativeReputation, 0.35f);
            yield return Wait(0.15f);
            reputationSourceText.text = "<s>" + Stat("GameOver_ReputationSource", totalCumulativeReputation) + "</s>";
            yield return CountUp(reputationBonusText, null, 0, reputationBonus, 0.25f, goldHex);
            yield return Wait(0.2f);

            // 모험 보너스 행
            yield return Fade(adventureRow, 0f, 1f, 0.2f);
            yield return CountUp(adventureSourceText, "GameOver_AdventureSource", 0, totalAdventures, 0.35f);
            yield return Wait(0.15f);
            adventureSourceText.text = "<s>" + Stat("GameOver_AdventureSource", totalAdventures) + "</s>";
            yield return CountUp(adventureBonusText, null, 0, adventureBonus, 0.25f, goldHex);
            yield return Wait(0.2f);

            // 구분선
            yield return Fade(separatorGroup, 0f, 1f, 0.2f);
            yield return Wait(0.1f);

            // earnedLegacy + totalLegacy 동시 등장, 바로 전송 애니메이션 시작
            int newTotal  = previousTotal + earnedLegacy;
            int earnedVal = earnedLegacy;
            int totalVal  = previousTotal;

            yield return Fade(earnedLegacyGroup, 0f, 1f, 0.25f);
            yield return Wait(0.3f);

            const float transferDuration = 0.8f;
            DOTween.To(() => earnedVal, x =>
            {
                earnedVal             = x;
                earnedLegacyText.text = $"<color={goldHex}>+{x}</color>";
            }, 0, transferDuration).SetEase(Ease.InOutCubic).SetUpdate(true).SetTarget(this).SetLink(gameObject);

            DOTween.To(() => totalVal, x =>
            {
                totalVal             = x;
                totalLegacyText.text = $"{x} <color={goldHex}>(+{x - previousTotal})</color>";
            }, newTotal, transferDuration).SetEase(Ease.InOutCubic).SetUpdate(true).SetTarget(this).SetLink(gameObject);

            yield return new WaitForSecondsRealtime(transferDuration + 0.05f);

            // 최종 값 보장
            earnedLegacyText.text = $"<color={goldHex}>+0</color>";
            totalLegacyText.text  = $"{newTotal} <color={goldHex}>(+{earnedLegacy})</color>";

            yield return Wait(0.2f);

            // 확인 버튼
            skipOverlayObject?.SetActive(false);
            skipOverlayButton?.onClick.RemoveAllListeners();
            yield return Fade(confirmButtonGroup, 0f, 1f, 0.25f);
            if (confirmButton != null) confirmButton.interactable = true;
        }

        #endregion

        #region 스킵

        private void SkipToEnd()
        {
            StopAllCoroutines();
            DOTween.Kill(this);
            skipOverlayObject?.SetActive(false);
            skipOverlayButton?.onClick.RemoveAllListeners();

            int newTotal = previousTotal + earnedLegacy;

            SetAlpha(titleGroup, 1f);
            SetAlpha(dayRow, 1f);
            daySourceText.text        = "<s>" + Stat("GameOver_DaySource", days) + "</s>";
            dayBonusText.text         = $"<color={goldHex}>+{dayBonus}</color>";
            SetAlpha(reputationRow, 1f);
            reputationSourceText.text = "<s>" + Stat("GameOver_ReputationSource", totalCumulativeReputation) + "</s>";
            reputationBonusText.text  = $"<color={goldHex}>+{reputationBonus}</color>";
            SetAlpha(adventureRow, 1f);
            adventureSourceText.text  = "<s>" + Stat("GameOver_AdventureSource", totalAdventures) + "</s>";
            adventureBonusText.text   = $"<color={goldHex}>+{adventureBonus}</color>";
            SetAlpha(separatorGroup, 1f);
            SetAlpha(earnedLegacyGroup, 1f);
            earnedLegacyText.text     = $"<color={goldHex}>+0</color>";
            totalLegacyText.text      = $"{newTotal} <color={goldHex}>(+{earnedLegacy})</color>";
            SetAlpha(confirmButtonGroup, 1f);
            if (confirmButton != null) confirmButton.interactable = true;
        }

        #endregion

        #region 헬퍼

        private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;
            group.alpha = from;
            group.DOFade(to, duration).SetUpdate(true).SetTarget(this).SetLink(gameObject);
            yield return new WaitForSecondsRealtime(duration);
        }

        /// <summary>지표 문구(생존/누적 평판/시도한 모험) 번역 조회.</summary>
        private static string Stat(string key, int count)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "count", count } } });

        /// <param name="key">번역 키. null이면 순수 숫자 보너스("+N") - 번역 대상이 아니다.</param>
        private IEnumerator CountUp(TextMeshProUGUI text, string key, int from, int to,
                                    float duration, string colorHex = null)
        {
            if (text == null) yield break;
            int current = from;
            DOTween.To(() => current, x =>
            {
                current    = x;
                string body = key != null ? Stat(key, x) : $"+{x}";
                text.text  = colorHex != null ? "<color=" + colorHex + ">" + body + "</color>" : body;
            }, to, duration).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this).SetLink(gameObject);
            yield return new WaitForSecondsRealtime(duration);
        }

        private IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        private void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        #endregion

        #region 버튼 핸들러

        private void OnConfirmClicked()
        {
            controller?.OnConfirmClicked();
        }

        public override void OnEscapeCancelled()
        {
            if (confirmButton != null && confirmButton.interactable)
                OnConfirmClicked();
            else
                SkipToEnd();
        }

        #endregion
    }
}
