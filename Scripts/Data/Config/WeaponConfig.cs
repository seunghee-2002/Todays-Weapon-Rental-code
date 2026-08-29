// Scripts/Runtime/Config/WeaponConfig.cs
using System;
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "TodaysWeaponRental/Config/WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("Effect Count Per Grade")]
        public int commonEffectCount = 1;
        public int uncommonEffectCount = 2;
        public int rareEffectCount = 3;
        public int epicEffectCount = 4;
        public int legendaryEffectCount = 5;

        public int MaxRerollCount = 3;

        [Header("Effect Grade Probabilities Per Weapon Grade")]
        [Tooltip("순서: Common / Uncommon / Rare / Epic / Legendary")]
        public float[] commonProb    = { 0.6f, 0.2f, 0.12f, 0.06f, 0.02f };
        public float[] uncommonProb  = { 0.3f, 0.4f, 0.18f, 0.08f, 0.04f };
        public float[] rareProb      = { 0.1f, 0.2f, 0.4f,  0.2f,  0.1f  };
        public float[] epicProb      = { 0.0f, 0.1f, 0.2f,  0.4f,  0.3f  };
        public float[] legendaryProb = { 0.0f, 0.0f, 0.1f,  0.3f,  0.6f  };

        /// <summary>무기 등급별 부가효과 개수 (표시용 조회 - 하드코딩 대체)</summary>
        public int GetEffectCount(Grade grade)
        {
            return grade switch
            {
                Grade.Common    => commonEffectCount,
                Grade.Uncommon  => uncommonEffectCount,
                Grade.Rare      => rareEffectCount,
                Grade.Epic      => epicEffectCount,
                Grade.Legendary => legendaryEffectCount,
                _ => 0
            };
        }

        /// <summary>무기 등급별 효과 등급 확률 배열 (표시용 조회)</summary>
        public float[] GetEffectGradeProbabilities(Grade grade)
        {
            return grade switch
            {
                Grade.Common    => commonProb,
                Grade.Uncommon  => uncommonProb,
                Grade.Rare      => rareProb,
                Grade.Epic      => epicProb,
                Grade.Legendary => legendaryProb,
                _ => commonProb
            };
        }

        // 확률표 합계 검증 - 합이 1이 아니면 굴림 분포가 조용히 깨진다
        private void OnValidate()
        {
            ValidateProbabilityTable(nameof(commonProb), commonProb);
            ValidateProbabilityTable(nameof(uncommonProb), uncommonProb);
            ValidateProbabilityTable(nameof(rareProb), rareProb);
            ValidateProbabilityTable(nameof(epicProb), epicProb);
            ValidateProbabilityTable(nameof(legendaryProb), legendaryProb);
        }

        private static void ValidateProbabilityTable(string label, float[] values)
        {
            if (values == null || values.Length == 0)
            {
                Log.Error($"[WeaponConfig] {label}: 확률표가 비어 있습니다.");
                return;
            }

            float sum = 0f;
            foreach (float v in values)
            {
                if (v < 0f)
                    Log.Error($"[WeaponConfig] {label}: 음수 확률 포함 ({v})");
                sum += v;
            }

            if (Mathf.Abs(sum - 1f) > 0.001f)
                Log.Error($"[WeaponConfig] {label}: 확률 합계가 1이 아닙니다 (sum={sum})");
        }
    }
}
