using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public enum EffectDisplayState
    {
        Active,   // 조건 충족 확인됨
        Inactive  // 조건 불충족 확인됨
    }

    public class WeaponEffectListItem : MonoBehaviour
    {
        [SerializeField] private Image gradeBG;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI valueText;

        private const float BlinkInterval = 0.5f;

        private Coroutine blinkCoroutine;

        #region 초기화

        public void Initialize(WeaponEffect effect, bool isSummary = false, bool showIndicator = false,
            EffectDisplayState displayState = EffectDisplayState.Active)
        {
            if (gradeBG != null) gradeBG.color = ColorManager.Instance.GetGradeColor(effect.effectData.grade);

            string name  = GetEffectName(effect);
            string value = GetEffectValue(effect);
            Color  color;

            if (displayState == EffectDisplayState.Inactive)
                color = ColorManager.Instance.GetEffectInactiveColor();
            else
                color = IsMaxValue(effect) ? ColorManager.Instance.GetEffectMaxValueColor() : ColorManager.Instance.GetEffectDefaultColor();

            SetTexts(name, value, color);

            if (showIndicator)
                StartTextBlinkingDelayed(color);
            else
                StopTextBlinking(color);
        }

        public void InitializeWithMax(WeaponEffect effect, float maxValue)
        {
            if (gradeBG != null) gradeBG.color = ColorManager.Instance.GetGradeColor(effect.effectData.grade);

            string name  = GetEffectName(effect);
            string value = GetEffectValueWithMax(effect, maxValue);
            Color  color = Mathf.Approximately(effect.currentValue, maxValue)
                ? ColorManager.Instance.GetEffectMaxValueColor()
                : ColorManager.Instance.GetEffectDefaultColor();

            SetTexts(name, value, color);
            StopTextBlinking(color);
        }

        #endregion

        #region 리스트 빌더

        /// <summary>컨테이너의 기존 자식을 비우고 effects로 다시 채운다. showIndicator로 항목별 블링킹 지정.</summary>
        public static void Rebuild(Transform container, WeaponEffectListItem prefab,
            IReadOnlyList<WeaponEffect> effects, System.Func<WeaponEffect, bool> showIndicator = null)
        {
            if (container == null || prefab == null) return;

            // SetParent(null)로 즉시 분리: 지연 Destroy가 같은 프레임 레이아웃에 끼어드는 것을 막는다.
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            if (effects == null) return;

            foreach (var effect in effects)
                Instantiate(prefab, container).Initialize(effect, showIndicator: showIndicator?.Invoke(effect) ?? false);
        }

        #endregion

        #region 내부 메서드 - 텍스트 설정

        private void SetTexts(string name, string value, Color color)
        {
            if (nameText != null)  { nameText.text  = name;  nameText.color  = color; }
            if (valueText != null) { valueText.text = value; valueText.color = color; }
        }

        /// <summary>잠금 표시용 — 이름/값 텍스트 alpha를 흐리게(0.5) 또는 원래대로(1).</summary>
        public void SetDimmed(bool dimmed)
        {
            float a = dimmed ? 0.5f : 1f;
            if (nameText != null)  { var c = nameText.color;  c.a = a; nameText.color  = c; }
            if (valueText != null) { var c = valueText.color; c.a = a; valueText.color = c; }
        }

        #endregion

        #region 내부 메서드 - 반짝임

        private void StartTextBlinkingDelayed(Color baseColor)
        {
            if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
            blinkCoroutine = StartCoroutine(BlinkTextWithDelayRoutine(baseColor));
        }

        private IEnumerator BlinkTextWithDelayRoutine(Color baseColor)
        {
            yield return null; // 한 프레임 대기 (부모 활성화 보장)
            if (!gameObject.activeInHierarchy) yield break;

            bool  fadingToHighlight = true;
            float elapsed           = 0f;

            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BlinkInterval);
                Color c = Color.Lerp(
                    fadingToHighlight ? baseColor     : ColorManager.Instance.GetEffectMaxValueGlowColor(),
                    fadingToHighlight ? ColorManager.Instance.GetEffectMaxValueGlowColor() : baseColor,
                    t);

                if (nameText != null)  nameText.color  = c;
                if (valueText != null) valueText.color = c;

                if (elapsed >= BlinkInterval)
                {
                    elapsed           = 0f;
                    fadingToHighlight = !fadingToHighlight;
                }

                yield return null;
            }
        }

        private void StopTextBlinking(Color finalColor)
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            if (nameText != null)  nameText.color  = finalColor;
            if (valueText != null) valueText.color = finalColor;
        }

        private void OnDisable()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
        }

        #endregion

        #region 내부 메서드 - 텍스트 포매팅

        private string GetEffectName(WeaponEffect effect) => EffectName(effect.effectData);

        /// <summary>
        /// 무기 부가효과 이름. 무기 상점의 범위 표시(WeaponShopEffectRangeItem)가 같은 표기를
        /// 따로 들고 있어 두 화면의 라벨이 어긋났으므로, 여기 하나로 모았다.
        /// </summary>
        public static string EffectName(WeaponEffectData data)
        {
            var type = data.effectType;

            // 인자가 들어가는 두 종류만 별도 처리, 나머지는 enum명 -> 키 규칙으로 조회
            if (type == WeaponEffectType.StatBonus)
                return UITranslator.GetString((AdventurerStat)data.targetStat);
            if (type == WeaponEffectType.DungeonGradeBonus)
                return Arg("WeaponEffectType_DungeonGradeBonus", "grade",
                           UITranslator.GetString((Grade)data.targetGrade));
            if (type == WeaponEffectType.ArmorTypeBonus)
                return Arg("WeaponEffectType_ArmorTypeBonus", "armor",
                           UITranslator.GetString((ArmorType)data.targetArmorType));

            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Common", KnownEffectKeys.Contains(type) ? $"WeaponEffectType_{type}" : "WeaponEffectType_Unknown");
        }

        /// <summary>키가 실제로 존재하는 효과 타입. 없는 타입은 Unknown으로 떨어뜨린다.</summary>
        private static readonly HashSet<WeaponEffectType> KnownEffectKeys = new()
        {
            WeaponEffectType.WeaponTypeMatchBonus, WeaponEffectType.AllStatBonus,
            WeaponEffectType.GreatSuccessBonus, WeaponEffectType.RetreatPrevention,
            WeaponEffectType.DoubleReward, WeaponEffectType.BattleGoldBonus,
            WeaponEffectType.MiniBossGoldBonus, WeaponEffectType.BossGoldBonus,
            WeaponEffectType.TreasureGoldBonus, WeaponEffectType.AllGoldBonus,
            WeaponEffectType.MaterialAmountBonus, WeaponEffectType.RestChanceBonus,
            WeaponEffectType.TreasureChestChanceBonus, WeaponEffectType.RareDropChanceBonus,
            WeaponEffectType.FailGoldBonus, WeaponEffectType.TrapNegation,
            WeaponEffectType.SpecialMaterialChance, WeaponEffectType.EnforceMaterialBonus,
            WeaponEffectType.EventCountBonus, WeaponEffectType.AdventureTimeReduction
        };

        private static string Arg(string key, string argName, string value)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Common", key,
                   arguments: new object[] { new Dictionary<string, object> { { argName, value } } });

        private string GetEffectValue(WeaponEffect effect)
        {
            return effect.effectData.effectType switch
            {
                WeaponEffectType.StatBonus
                or WeaponEffectType.AllStatBonus
                    => $"+{effect.currentValue:F0}",

                WeaponEffectType.RetreatPrevention
                or WeaponEffectType.MaterialAmountBonus
                or WeaponEffectType.EnforceMaterialBonus
                or WeaponEffectType.EventCountBonus
                    => $"+{Mathf.RoundToInt(effect.currentValue)}",

                WeaponEffectType.WeaponTypeMatchBonus
                    => $"x{effect.currentValue:F2}",

                WeaponEffectType.AdventureTimeReduction
                    => $"-{effect.currentValue * 100f:0.#}%",

                WeaponEffectType.BattleGoldBonus
                or WeaponEffectType.MiniBossGoldBonus
                or WeaponEffectType.BossGoldBonus
                or WeaponEffectType.TreasureGoldBonus
                or WeaponEffectType.AllGoldBonus
                    => $"+{effect.currentValue * 100f:0.#}%",

                _ => $"+{effect.currentValue * 100f:0.#}%"
            };
        }

        private string GetEffectValueWithMax(WeaponEffect effect, float maxValue)
        {
            string goldHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGoldColor());

            return effect.effectData.effectType switch
            {
                WeaponEffectType.StatBonus
                or WeaponEffectType.AllStatBonus
                    => $"+{effect.currentValue:F0} → <color={goldHex}>{maxValue:F0}</color>",

                WeaponEffectType.RetreatPrevention
                or WeaponEffectType.MaterialAmountBonus
                or WeaponEffectType.EnforceMaterialBonus
                or WeaponEffectType.EventCountBonus
                    => $"+{Mathf.RoundToInt(effect.currentValue)} → <color={goldHex}>{Mathf.RoundToInt(maxValue)}</color>",

                WeaponEffectType.WeaponTypeMatchBonus
                    => $"x{effect.currentValue:F2} → <color={goldHex}>x{maxValue:F2}</color>",

                WeaponEffectType.AdventureTimeReduction
                    => $"-{effect.currentValue * 100f:0.#}% → <color={goldHex}>-{maxValue * 100f:0.#}%</color>",

                WeaponEffectType.BattleGoldBonus
                or WeaponEffectType.MiniBossGoldBonus
                or WeaponEffectType.BossGoldBonus
                or WeaponEffectType.TreasureGoldBonus
                or WeaponEffectType.AllGoldBonus
                    => $"+{effect.currentValue * 100f:0.#}% → <color={goldHex}>{maxValue * 100f:0.#}%</color>",

                _ => $"+{effect.currentValue * 100f:0.#}% → <color={goldHex}>{maxValue     * 100f:0.#}%</color>"
            };
        }

        private bool IsMaxValue(WeaponEffect effect)
        {
            var data = effect.effectData;
            if (data == null) return false;
            return Mathf.Approximately(effect.currentValue, data.baseValueRange.y);
        }

        #endregion
    }
}
