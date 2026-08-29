// Scripts/Data/Config/WeaponShopConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 주차 구간 한 개분의 상인 인사말 풀. 인덱스 = PriceTierConfig.TierIndex.
    /// </summary>
    [Serializable]
    public class MerchantGreetingTier
    {
        [TextArea(2, 4)]
        public List<string> lines = new();
    }

    [CreateAssetMenu(fileName = "WeaponShopConfig", menuName = "TodaysWeaponRental/Config/WeaponShopConfig")]
    public class WeaponShopConfig : ScriptableObject
    {
        [Header("Shop Settings")]
        [Tooltip("최소 무기 개수")]
        public int minWeapons = 5;
        [Tooltip("최대 무기 개수")]
        public int maxWeapons = 10;
        [Tooltip("상점 새로고침 비용")]
        public int refreshCost = 100;
        [Tooltip("새로고침 최대 횟수")]
        public int maxRefreshCount = 3;

        // 등급별 스폰 가중치는 주차 계단이 필요해 PriceTierConfig.weaponShopGradeWeights로 옮겼다.

        // 무기 값은 등급별 basePrice 고정. 후반 지출 증가는 "값이 오르는" 게 아니라
        // "비싼 등급이 진열되는" 방식으로 만든다 (PriceTierConfig.weaponShopGradeWeights).

        [Header("등장 확률 (누적)")]
        [Tooltip("시작 확률 (마지막 등장 다음날 기준)")]
        public float spawnBaseChance = 0.10f;
        [Tooltip("등장 안 한 하루당 확률 증가량")]
        public float spawnChancePerDay = 0.10f;
        [Tooltip("평판 등급당 시작 확률 가산 (Bronze=0단계)")]
        public float spawnChancePerReputationLevel = 0.075f;
        [Tooltip("고정 등장일 (이 날들은 무조건 등장, 마지막 고정일 이후부터 확률 계산). 1일차는 튜토리얼이 처리")]
        public int[] fixedAppearanceDays = new int[] { 3, 8 };

        [Header("인사말 (주차 구간별)")]
        [Tooltip("길이 = PriceTierConfig.tierMaxWeeks.Length + 1. 떠돌이 상인이 단골 거래처로 굳어가는 흐름을 담는다.")]
        public MerchantGreetingTier[] merchantGreetings = new MerchantGreetingTier[5];

        /// <summary>
        /// 해당 주차 구간의 인사말 하나. 풀이 비어 있으면 null (호출측이 폴백 문구를 쓴다).
        /// 배열이 짧으면 마지막 구간으로 클램프한다(에셋 편집 실수 방어).
        /// </summary>
        public string GetMerchantGreeting(int tierIndex)
        {
            if (merchantGreetings == null || merchantGreetings.Length == 0) return null;

            // 번역 키는 클램프한 뒤의 구간과 실제로 뽑은 항목 위치로 만든다.
            int t = Mathf.Clamp(tierIndex, 0, merchantGreetings.Length - 1);
            var tier = merchantGreetings[t];
            if (tier?.lines == null || tier.lines.Count == 0) return null;

            int i = UnityEngine.Random.Range(0, tier.lines.Count);
            return DataLocalizer.MerchantGreeting(t, i, tier.lines[i]);
        }
    }
}