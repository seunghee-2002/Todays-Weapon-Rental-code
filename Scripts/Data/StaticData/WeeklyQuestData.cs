// Scripts/Data/WeeklyQuestData.cs
using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public enum QuestType
    {
        SuccessfulAdventures,
        RentSpecificGrade,
        RentSpecificWeapon,
        CompleteSpecificDungeon,
        GreatSuccessCount,
        CraftComplete,
        EnforceSuccess,
        EvolveSuccess,
        RerollComplete,
        SeerComplete,
        WeaponPurchase,
        GoldEarned,      // 유일한 금액 누적형 - UpdateProgress의 amount로 증가
        GiftComplete
    }

    public enum QuestStatus
    {
        Active,
        Completed,
        Claimed,
        Failed
    }

    /// <summary>
    /// 엔드리스(캠페인 이후) 반복 구간의 난이도 등급. 런타임 추첨이 이 값으로 풀을 나눈다.
    /// 캠페인 주차(1~60)는 순서가 고정이라 이 값을 쓰지 않는다.
    ///
    /// 등급 기준은 실측 통과율이다 - 아키타입(휴식/표준/시험)이 아니다.
    /// 실측상 난이도를 만드는 건 병목 3종뿐이며(2026-07-29 엔드리스 스윕), 등급 요구는
    /// 전설 2회를 빼면 통과율에 거의 영향이 없다:
    ///   1) 저등급(일반/고급) 던전 요구 - 엔드리스 의뢰판 가중치가 Rare+ 대비 최대 30배 눌림
    ///   2) 전설 등급 2회 요구 - 전설 무기는 1자루뿐이라 주 2회전이 안 됨
    ///   3) 석궁/마도서 요구 - 개체가 5·6종뿐 (다른 6타입은 13종씩)
    /// </summary>
    public enum QuestDifficulty
    {
        Easy,       // 병목 0개
        Normal,     // 약한 병목 1개
        Hard,       // 병목 1개
        Extreme,    // 병목 2개
    }

    [System.Serializable]
    public class QuestRequirement
    {
        public QuestType questType;
        public int targetCount;
        public Grade minGrade;
        public WeaponType specificWeaponType;
        public string specificDungeonID;
        [TextArea(1, 2)]
        public string requirementText;
    }

    [CreateAssetMenu(fileName = "WeeklyQuestData", menuName = "TodaysWeaponRental/WeeklyQuestData")]
    public class WeeklyQuestData : BaseData
    {
        public string questTitle;

        /// <summary>화면에 보일 제목. 한국어는 위 필드가 원본이고, 다른 언어는 Data 테이블에서 온다.</summary>
        public string DisplayTitle => DataLocalizer.QuestTitle(this);

        /// <summary>WeeklyQuestGenerator가 요구 목록을 이어붙여 구운 값. 화면에 안 쓴다 - 번역 제외.</summary>
        [TextArea(3, 5)]
        public string description;
        public int weekNumber;
        public List<QuestRequirement> requirements = new List<QuestRequirement>();
        public int goldReward;
        public int reputationReward;
        public int insightReward;
        public int weeklyFine;
        public int reputationPenalty;
        [Tooltip("엔드리스 구간 난이도 등급. 캠페인 주차(1~60)에서는 사용하지 않는다.")]
        public QuestDifficulty difficulty;
    }
}