using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상점 DetailPanel — 부가효과 종류별 등급 범위 표시 아이템
    /// Initialize로 효과 이름(nameText)을 1회 설정하고,
    /// UpdateValue로 무기 등급이 바뀔 때마다 수치 범위(valueText)만 갱신한다.
    /// </summary>
    public class WeaponShopEffectRangeItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI valueText;

        private WeaponEffectType cachedEffectType;
        private int cachedTargetStat;
        private int cachedTargetGrade;
        private int cachedTargetArmorType;

        #region 초기화

        /// <summary>effectType + target 기반으로 nameText를 설정한다. 최초 1회 호출.</summary>
        public void Initialize(WeaponEffectData data)
        {
            cachedEffectType      = data.effectType;
            cachedTargetStat      = data.targetStat;
            cachedTargetGrade     = data.targetGrade;
            cachedTargetArmorType = data.targetArmorType;

            if (nameText != null)
                nameText.text = GetEffectName(data);
        }

        /// <summary>무기 등급에 따른 범위로 valueText를 갱신한다.</summary>
        public void UpdateValue(WeaponEffectData minData, WeaponEffectData maxData)
        {
            if (valueText == null) return;

            if (minData == null && maxData == null)
            {
                valueText.text = "-";
                return;
            }

            WeaponEffectData data = minData ?? maxData;
            bool isInt = WeaponEffect.IsIntegerType(data.effectType);

            float minVal = minData?.baseValueRange.x ?? maxData.baseValueRange.x;
            float maxVal = maxData?.baseValueRange.y ?? minData.baseValueRange.y;

            valueText.text = FormatValueRange(data.effectType, minVal, maxVal, isInt);
        }

        #endregion

        #region 내부 메서드

        // 같은 효과가 화면마다 다르게 읽히지 않도록 WeaponEffectListItem의 표기를 그대로 쓴다.
        private string GetEffectName(WeaponEffectData data) => WeaponEffectListItem.EffectName(data);

        private string FormatValueRange(WeaponEffectType type, float minVal, float maxVal, bool isInt)
        {
            return type switch
            {
                WeaponEffectType.StatBonus
                or WeaponEffectType.AllStatBonus
                or WeaponEffectType.RetreatPrevention
                or WeaponEffectType.MaterialAmountBonus
                or WeaponEffectType.EnforceMaterialBonus
                or WeaponEffectType.EventCountBonus
                    => isInt
                        ? $"+{Mathf.RoundToInt(minVal)} ~ +{Mathf.RoundToInt(maxVal)}"
                        : $"+{minVal:F0} ~ +{maxVal:F0}",

                WeaponEffectType.WeaponTypeMatchBonus
                    => $"x{minVal:F2} ~ x{maxVal:F2}",

                WeaponEffectType.AdventureTimeReduction
                    => $"-{minVal * 100f:0.#}% ~ -{maxVal * 100f:0.#}%",

                WeaponEffectType.BattleGoldBonus
                or WeaponEffectType.MiniBossGoldBonus
                or WeaponEffectType.BossGoldBonus
                or WeaponEffectType.TreasureGoldBonus
                or WeaponEffectType.AllGoldBonus
                    => $"+{minVal * 100f:0.#}% ~ +{maxVal * 100f:0.#}%",

                _ => $"+{minVal * 100f:0.#}% ~ +{maxVal * 100f:0.#}%"
            };
        }

        #endregion
    }
}
