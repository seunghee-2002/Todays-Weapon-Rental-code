// Scripts/Data/Config/EndlessQuestConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 캠페인 종료 후 무한 반복 구간(엔드리스)의 규칙.
    /// 설계: Documents/balance/reference/주간퀘스트_레벨디자인.md §8
    ///
    /// 캠페인은 주차별로 짜인 커리큘럼이지만, 엔드리스는 난이도 등급 풀에서 매주 뽑는다.
    /// 플레이어는 "이번 주에 어려운 게 안 걸리길" 바라며 버티고, 벌금 곡선이 계속 올라
    /// 회차는 언젠가 반드시 끝난다.
    /// </summary>
    [CreateAssetMenu(fileName = "EndlessQuestConfig", menuName = "TodaysWeaponRental/Config/EndlessQuestConfig")]
    public class EndlessQuestConfig : ScriptableObject
    {
        [Header("구간")]
        [Tooltip("캠페인 마지막 주차. 이 주차를 넘으면 엔드리스 추첨으로 전환한다. 템플릿 판정 기준이기도 하다 (weekNumber > 이 값 = 엔드리스 템플릿)")]
        public int campaignLastWeek = 60;

        [Header("벌금 - 주차 곡선")]
        [Tooltip("엔드리스 첫 주차의 벌금. 캠페인 마지막 주차 값과 이어지도록 맞춘다")]
        public int fineBase = 79200;
        [Tooltip("주당 벌금 증가액. 성장은 멈췄는데 실패 비용만 오르므로 회차가 반드시 끝난다 - 종말 속도를 정하는 값")]
        public int finePerWeek = 4000;

        [Header("난이도 추첨 가중치 (Easy / Normal / Hard / Extreme)")]
        [Tooltip("클수록 자주 뽑힌다. 어려운 등급이 드물어야 '안 걸리길 비는' 긴장이 생긴다")]
        public float[] difficultyWeights = { 30f, 35f, 25f, 10f };

        [Header("반복 방지")]
        [Tooltip("직전 N주에 나온 템플릿은 다시 뽑지 않는다 (풀보다 작아야 한다)")]
        public int noRepeatWindow = 3;
        [Tooltip("Extreme이 2주 연속 나오지 않게 막는다. 연속으로 걸리면 운이 아니라 사형 선고가 된다")]
        public bool blockConsecutiveExtreme = true;

        /// <summary>해당 주차의 벌금. 캠페인 구간은 SO 고정값을 쓰므로 호출부가 걸러야 한다.</summary>
        public int FineForWeek(int week) =>
            fineBase + Mathf.Max(0, week - (campaignLastWeek + 1)) * finePerWeek;

        /// <summary>난이도 등급의 추첨 가중치. 배열이 짧으면 0(미출제)으로 본다.</summary>
        public float WeightOf(QuestDifficulty difficulty)
        {
            int i = (int)difficulty;
            return difficultyWeights != null && i < difficultyWeights.Length ? difficultyWeights[i] : 0f;
        }
    }
}
