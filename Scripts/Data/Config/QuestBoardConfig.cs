// Scripts/Data/Config/QuestBoardConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "QuestBoardConfig", menuName = "TodaysWeaponRental/Config/QuestBoardConfig")]
    public class QuestBoardConfig : ScriptableObject
    {
        /// <summary>주차 구간별 던전 등급 가중치 배율 (주간퀘스트_난이도_계수.md §4-2)</summary>
        [Serializable]
        public class QuestWeightTier
        {
            [Tooltip("이 티어가 적용되는 마지막 주차 (이하)")]
            public int maxWeek;
            [Tooltip("Common / Uncommon / Rare / Epic / Legendary 순 배율")]
            public float[] gradeMultipliers = new float[5];
        }

        [Header("Reconfirm Cost")]
        [Tooltip("새로고침 기준 비용. 실제 비용 = 이 값 x PriceTierConfig.boardRefreshCost[주차 구간]")]
        public int refreshBaseCost = 500;

        [SerializeField, Tooltip("의뢰판 최대 새로고침 횟수")]
        public int maxRefreshCount = 3;

        [Header("Pool Size by Reputation (풀 크기 / 선택 가능 수)")]
        [Tooltip("Bronze: 풀 크기")]
        public int bronzePoolSize   = 5;
        [Tooltip("Bronze: 선택 가능 수")]
        public int bronzeSelectCount = 3;

        [Tooltip("Silver: 풀 크기")]
        public int silverPoolSize   = 6;
        [Tooltip("Silver: 선택 가능 수")]
        public int silverSelectCount = 3;

        [Tooltip("Gold: 풀 크기")]
        public int goldPoolSize     = 7;
        [Tooltip("Gold: 선택 가능 수")]
        public int goldSelectCount  = 4;

        [Tooltip("Platinum: 풀 크기")]
        public int platinumPoolSize   = 8;
        [Tooltip("Platinum: 선택 가능 수")]
        public int platinumSelectCount = 5;

        [Tooltip("Diamond: 풀 크기")]
        public int diamondPoolSize   = 10;
        [Tooltip("Diamond: 선택 가능 수")]
        public int diamondSelectCount = 6;

        [Header("던전 등급 주차 배율 (계수 문서 §4-2)")]
        [Tooltip("유효 가중치 = DungeonData.questWeight x 등급 배율. maxWeek 오름차순으로 둘 것")]
        public List<QuestWeightTier> questWeightTiers = new List<QuestWeightTier>
        {
            new QuestWeightTier { maxWeek =   8, gradeMultipliers = new[] { 1.00f, 1.00f, 1f,  1f,  1f   } },
            new QuestWeightTier { maxWeek =  24, gradeMultipliers = new[] { 0.70f, 1.50f, 2f,  2f,  5f   } },
            new QuestWeightTier { maxWeek =  48, gradeMultipliers = new[] { 0.40f, 1.20f, 4f,  5f,  20f  } },
            new QuestWeightTier { maxWeek =  74, gradeMultipliers = new[] { 0.15f, 0.60f, 4f,  12f, 60f  } },
            new QuestWeightTier { maxWeek = 100, gradeMultipliers = new[] { 0.05f, 0.25f, 2f,  15f, 150f } },
        };

        /// <summary>
        /// 해당 주차의 던전 등급 가중치 배율. 마지막 티어의 maxWeek를 넘으면(엔드리스 구간)
        /// 마지막 티어 값을 유지한다.
        /// </summary>
        public float GetQuestWeightMultiplier(Grade grade, int week)
        {
            if (questWeightTiers == null || questWeightTiers.Count == 0) return 1f;

            var tier = questWeightTiers[questWeightTiers.Count - 1];
            for (int i = 0; i < questWeightTiers.Count; i++)
            {
                if (week <= questWeightTiers[i].maxWeek) { tier = questWeightTiers[i]; break; }
            }

            var mult = tier.gradeMultipliers;
            return mult != null && (int)grade < mult.Length ? mult[(int)grade] : 1f;
        }
    }
}