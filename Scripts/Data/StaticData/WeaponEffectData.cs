using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "WED_", menuName = "TodaysWeaponRental/WeaponEffectData")]
    public class WeaponEffectData : BaseData
    {
        public Grade grade;
        public WeaponEffectType effectType;
        [Tooltip("Effect의 기본 수치 범위. 예: 10%~20% 증가 -> (0.1, 0.2)")]
        public Vector2 baseValueRange;
        [Tooltip("목표 스탯 (0: STR, 1: DEX, 2: INT, 3: LUK)")]
        public int targetStat;
        [Tooltip("목표 등급 (0: Common, 1: Uncommon, 2: Rare, 3: Epic, 4: Legendary)")]
        public int targetGrade;       
        [Tooltip("목표 방어구 타입 (0: Unarmored, 1: Light, 2: Heavy, 3: Magical)")]
        public int targetArmorType;
        [Tooltip("목표 기준 수치")]
        public int targetThreshold;

        [Tooltip("랜덤 선택 가중치 (기준 1.0f)")]
        public float weight = 1.0f;

#if UNITY_EDITOR
        // 분수(0~1) 단위 효과에 퍼센트 값(예: 25)이 입력되면 +2500% 같은 경제 붕괴가 생기므로
        // 에디터에서 단위 범위를 검증한다
        protected override void OnValidate()
        {
            base.OnValidate();

            if (weight < 0f)
                Log.Error($"[WeaponEffectData] {name}: weight가 음수입니다 ({weight})");

            if (IsFractionEffect(effectType) &&
                (baseValueRange.x < 0f || baseValueRange.y > 1f || baseValueRange.x > baseValueRange.y))
            {
                Log.Error($"[WeaponEffectData] {name}: {effectType}는 0~1 분수 단위입니다. " +
                               $"현재 범위 ({baseValueRange.x}~{baseValueRange.y})");
            }
        }

        /// <summary>코드에서 0~1 분수(확률/비율)로 소비되는 효과 타입</summary>
        private static bool IsFractionEffect(WeaponEffectType type)
        {
            return type is WeaponEffectType.GreatSuccessBonus
                or WeaponEffectType.DoubleReward
                or WeaponEffectType.RestChanceBonus
                or WeaponEffectType.TreasureChestChanceBonus
                or WeaponEffectType.RareDropChanceBonus
                or WeaponEffectType.FailGoldBonus
                or WeaponEffectType.TrapNegation
                or WeaponEffectType.SpecialMaterialChance
                or WeaponEffectType.AdventureTimeReduction;
        }
#endif
    }
}
