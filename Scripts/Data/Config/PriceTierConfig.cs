// Scripts/Data/Config/PriceTierConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상점 진열 등급 가중치 (한 주차 구간분). 인덱스 = (int)Grade, 길이 5.
    /// </summary>
    [Serializable]
    public class GradeWeightTier
    {
        [Tooltip("Common / Uncommon / Rare / Epic / Legendary 순")]
        public float[] weights = { 0.40f, 0.30f, 0.20f, 0.08f, 0.02f };
    }

    /// <summary>
    /// 계단 한 개분의 길드 전령 공지 대사. 수치는 넣지 않는다 — 상인들의 "사정"을 말하는 서사 문구다.
    /// 말투는 TutorialConfig.steps(길드 전령)와 같은 결로 맞춘다 — 같은 인물이 같은 대화창(TutorialDialogueView)으로
    /// 말하므로, 저녁 6시 보고 전령의 건조한 보고체와 섞이면 안 된다.
    /// (플레이어는 대장간/상점에서 값을 직접 보므로, 공지는 이유와 예고만 담당한다)
    /// </summary>
    [Serializable]
    public class StepNoticeLines
    {
        [Tooltip("이 계단 공지를 표시할 때의 전령 초상. 예고/당일 공통이다.")]
        public HeraldPortrait portrait = HeraldPortrait.Default;
        [Tooltip("계단 1주일 전 예고")]
        public List<string> noticeLines = new();
        [Tooltip("계단 당일 공지")]
        public List<string> todayLines = new();
    }

    /// <summary>
    /// 주차 계단 가격 체계 — 특정 주차마다 값이 한 번씩 뛰고, 마지막 계단 이후로는 수렴한다.
    ///
    /// **왜 주차인가** (Documents/balance/reference/골드_경제_구조.md 4장):
    /// - 일수 비례는 무한 선형 증가이고 예고가 불가능하다 (2026-07-27에 걷어낸 것)
    /// - 평판 연동은 승급 직후 값이 뛰어 "승급 페널티"가 된다
    /// - R 연동은 단가만 담고 처리량을 못 담아 수입 성장(6.2배)을 못 따라간다 (R은 1.7배)
    /// - 주차는 확정적이라 **길드 전령이 미리 예고할 수 있다**
    ///
    /// 계단 주차는 평판 도달 실측(상급 7/17/28/39주, 중급 10/18/26/35주) **직후**에 놓아
    /// 수입이 먼저 오르고 값이 따라오는 순서가 되게 잡았다.
    ///
    /// 같은 구조의 선례: QuestBoardConfig.questWeightTiers (의뢰판 던전 등급 가중치).
    /// </summary>
    [CreateAssetMenu(fileName = "PriceTierConfig", menuName = "TodaysWeaponRental/Config/PriceTierConfig")]
    public class PriceTierConfig : ScriptableObject
    {
        [Header("계단 경계")]
        [Tooltip("각 구간의 마지막 주차. { 10, 20, 30, 40 }이면 구간은 ~10 / 11~20 / 21~30 / 31~40 / 41~ 의 5개다.")]
        public int[] tierMaxWeeks = { 10, 20, 30, 40 };

        [Header("항목별 구간 배율 (길이 = tierMaxWeeks.Length + 1)")]
        [Tooltip("점술 상담료")]
        public float[] seerCost = { 1f, 1f, 1f, 1f, 1f };
        [Tooltip("강화/진화 골드. 후반 최대 지출 항목이자 '값어치가 함께 커지는' 유일하게 안전한 싱크다 " +
                 "(강화한 무기는 실제로 강해지므로 점술처럼 회수율이 무너지지 않는다)")]
        public float[] blacksmithCost = { 1f, 1f, 1f, 1f, 1f };
        [Tooltip("재부여 골드")]
        public float[] rerollCost = { 1f, 1f, 1f, 1f, 1f };
        [Tooltip("무기 제작 골드")]
        public float[] weaponCraftCost = { 1f, 1f, 1f, 1f, 1f };
        [Tooltip("액티브 아이템 제작 골드")]
        public float[] itemCraftCost = { 1f, 1f, 1f, 1f, 1f };
        [Tooltip("무기 상점 새로고침")]
        public float[] shopRefreshCost = { 1f, 1f, 1f, 1f, 1f };
        [Tooltip("의뢰판 새로고침")]
        public float[] boardRefreshCost = { 1f, 1f, 1f, 1f, 1f };
        [Tooltip("모험가 부활")]
        public float[] reviveCost = { 1f, 1f, 1f, 1f, 1f };

        [Header("무기 상점 진열 등급 가중치 (구간별)")]
        [Tooltip("길이 = tierMaxWeeks.Length + 1. 가격이 오르는 게 아니라 비싼 물건이 들어오는 방식이다.")]
        public GradeWeightTier[] weaponShopGradeWeights = new GradeWeightTier[5];

        [Header("길드 전령 시세 공지 대사 (계단별, 길이 = tierMaxWeeks.Length)")]
        public StepNoticeLines[] stepNotices = new StepNoticeLines[4];

        [Header("부활 기본 비용")]
        [Tooltip("VisitorManager.ReviveCost의 기준값. 여기에 reviveCost 배율이 곱해진다.")]
        public int reviveBaseCost = 1500;

        /// <summary>현재 주차가 몇 번째 구간인지. tierMaxWeeks를 넘어서면 마지막 구간.</summary>
        public int TierIndex(int week)
        {
            if (tierMaxWeeks == null) return 0;
            for (int i = 0; i < tierMaxWeeks.Length; i++)
                if (week <= tierMaxWeeks[i]) return i;
            return tierMaxWeeks.Length;
        }

        /// <summary>구간 배율 조회. 배열이 짧으면 마지막 값으로 클램프한다(에셋 편집 실수 방어).</summary>
        public float At(float[] tiers, int week)
        {
            if (tiers == null || tiers.Length == 0) return 1f;
            return tiers[Mathf.Min(TierIndex(week), tiers.Length - 1)];
        }

        /// <summary>
        /// 시세 계단 공지 판정. day가 계단 시작일이면 isToday = true, 그 7일 전이면 false로 두고
        /// 해당 계단 주차를 돌려준다. 공지일이 아니면 0.
        /// 계단은 주차 기준이라 날짜가 확정적이고, 그래서 미리 예고할 수 있다 (평판 연동으로는 불가능).
        /// </summary>
        public int GetNoticeStepWeek(int day, out bool isToday)
        {
            isToday = false;
            if (tierMaxWeeks == null) return 0;

            foreach (int maxWeek in tierMaxWeeks)
            {
                int stepWeek = maxWeek + 1;          // 계단이 적용되기 시작하는 주차
                int firstDay = (stepWeek - 1) * 7 + 1;
                if (day == firstDay)     { isToday = true; return stepWeek; }
                if (day == firstDay - 7) return stepWeek;
            }
            return 0;
        }

        /// <summary>
        /// 해당 계단의 공지 대사 묶음. 미설정이면 null (호출측이 공지를 건너뛴다).
        /// <paramref name="stepIndex"/>로 stepNotices 안의 위치도 함께 돌려준다 — 번역 키가
        /// 그 위치로 만들어지는데, 묶음만 받으면 어느 계단이었는지가 사라진다.
        /// </summary>
        public StepNoticeLines GetStepNotice(int stepWeek, out int stepIndex)
        {
            stepIndex = -1;
            if (tierMaxWeeks == null || stepNotices == null) return null;
            for (int i = 0; i < tierMaxWeeks.Length; i++)
                if (tierMaxWeeks[i] + 1 == stepWeek)
                {
                    if (i >= stepNotices.Length) return null;
                    stepIndex = i;
                    return stepNotices[i];
                }
            return null;
        }

        /// <summary>해당 주차의 무기 상점 등급 가중치. 미설정이면 null (호출측이 기존 동작 유지).</summary>
        public float[] GetWeaponShopGradeWeights(int week)
        {
            if (weaponShopGradeWeights == null || weaponShopGradeWeights.Length == 0) return null;
            var tier = weaponShopGradeWeights[Mathf.Min(TierIndex(week), weaponShopGradeWeights.Length - 1)];
            return tier?.weights != null && tier.weights.Length > 0 ? tier.weights : null;
        }
    }
}
