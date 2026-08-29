using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 이벤트별 성공률 툴팁에 표시할 값 묶음. 컨트롤러가 채워 넘기고 툴팁은 표시만 한다.
    /// 확률은 전부 0~1 비율.
    /// </summary>
    public struct EventRateTooltipData
    {
        // 전투 성공률 (이벤트 난이도 계수 반영)
        public float battleRate;
        public bool  hasMiniBoss;
        public float miniBossRate;
        public float bossRate;

        // 판정 확률
        public bool  hasTrap;
        public float trapEvadeRate;
        public bool  isDexKnown;      // 미공개면 함정 회피율을 ??로 가린다
        public float deathRate;
        public float survivalRate;
        public bool  isStrKnown;      // 미공개면 버티기 확률을 ??로 가린다
        public float greatSuccessRate;
        public bool  greatSuccessGuaranteed;

        // 변동 요소
        public float moodMin;
        public float moodMax;
        public bool  hasLuck;
        public float luckModifier;
        public float trapPenalty;
    }

    /// <summary>
    /// 던전 상세 패널의 성공률 영역을 누르면 뜨는 이벤트별 성공률 툴팁.
    /// 행은 전부 미리 배치돼 있고, 던전에 없는 이벤트 행만 숨긴다.
    /// </summary>
    public class PreparationEventRateTooltip : MonoBehaviour
    {
        [SerializeField] private Button overlayButton;

        [Header("전투 성공률")]
        [SerializeField] private TextMeshProUGUI battleRateText;
        [SerializeField] private TextMeshProUGUI miniBossRateText;
        [SerializeField] private TextMeshProUGUI bossRateText;

        [Header("판정 확률")]
        [SerializeField] private TextMeshProUGUI trapEvadeRateText;
        [SerializeField] private TextMeshProUGUI deathRateText;
        [SerializeField] private TextMeshProUGUI survivalRateText;
        [SerializeField] private TextMeshProUGUI greatSuccessRateText;

        [Header("변동 요소")]
        [SerializeField] private TextMeshProUGUI moodRangeText;
        [SerializeField] private TextMeshProUGUI luckModifierText;
        [SerializeField] private TextMeshProUGUI trapPenaltyText;

        private void Awake()
        {
            overlayButton?.onClick.AddListener(Hide);
        }

        public void Show(EventRateTooltipData data)
        {
            gameObject.SetActive(true);

            SetRow(battleRateText,   L("RateTooltip_BattleRate"), Percent(data.battleRate));
            SetRow(miniBossRateText, data.hasMiniBoss, L("RateTooltip_MiniBossRate"), Percent(data.miniBossRate));
            SetRow(bossRateText,     L("RateTooltip_BossRate"), Percent(data.bossRate));

            SetRow(trapEvadeRateText, data.hasTrap, L("RateTooltip_TrapEvadeRate"),
                data.isDexKnown ? Percent(data.trapEvadeRate) : Unknown());
            SetRow(deathRateText, L("RateTooltip_DeathRate"),
                Colored(Percent(data.deathRate), ColorManager.Instance.GetRedColor()));
            SetRow(survivalRateText, L("RateTooltip_SurvivalRate"),
                data.isStrKnown
                    ? Colored(Percent(data.survivalRate), ColorManager.Instance.GetGreenColor())
                    : Unknown());
            SetRow(greatSuccessRateText, L("RateTooltip_GreatSuccessRate"),
                Colored(data.greatSuccessGuaranteed ? L("RateTooltip_Guaranteed") : Percent(data.greatSuccessRate),
                    ColorManager.Instance.GetGreenColor()));

            SetRow(moodRangeText, L("RateTooltip_MoodRange"),
                Colored($"x{data.moodMin:0.##}", ColorManager.Instance.GetRedColor())
                + " ~ "
                + Colored($"x{data.moodMax:0.##}", ColorManager.Instance.GetGreenColor()));
            SetRow(luckModifierText, data.hasLuck, L("RateTooltip_LuckModifier"),
                Colored(Signed(data.luckModifier), data.luckModifier >= 0f
                    ? ColorManager.Instance.GetGreenColor()
                    : ColorManager.Instance.GetRedColor()));
            SetRow(trapPenaltyText, data.hasTrap, L("RateTooltip_TrapPenalty"),
                Colored(Signed(data.trapPenalty), ColorManager.Instance.GetRedColor()));
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #region 내부 헬퍼

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static void SetRow(TextMeshProUGUI label, string title, string value)
        {
            if (label == null) return;
            label.gameObject.SetActive(true);
            label.text = $"{title} {value}";
        }

        private static void SetRow(TextMeshProUGUI label, bool show, string title, string value)
        {
            if (label == null) return;
            label.gameObject.SetActive(show);
            if (show) label.text = $"{title} {value}";
        }

        private static string Percent(float rate) => $"{rate * 100f:0}%";

        private static string Signed(float value)
        {
            string sign = value > 0f ? "+" : "";
            return $"{sign}{value * 100f:0.#}%";
        }

        private static string Unknown()
            => Colored("??%", ColorManager.Instance.GetGrayColor());

        private static string Colored(string text, Color color)
            => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        #endregion
    }
}
