// Scripts/Data/Config/VisitorConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험가 대사 — 한 상황에 대해 성별(남/여)별 대사 풀을 담는다.
    /// 요청한 성별 풀이 비어 있으면 반대 성별 풀로 대체한다.
    /// </summary>
    [Serializable]
    public class GenderedDialogueLines
    {
        [Tooltip("남성 모험가 대사")] public List<string> male = new();
        [Tooltip("여성 모험가 대사")] public List<string> female = new();

        public List<string> Get(Gender gender)
        {
            List<string> primary = gender == Gender.Female ? female : male;
            if (primary != null && primary.Count > 0) return primary;

            List<string> other = gender == Gender.Female ? male : female;
            return (other != null && other.Count > 0) ? other : primary;
        }
    }

    [CreateAssetMenu(fileName = "VisitorConfig", menuName = "TodaysWeaponRental/Config/VisitorConfig")]
    public class VisitorConfig : ScriptableObject
    {
        [Header("스폰 간격 (게임 시간 초 단위)")]
        [Tooltip("모험가 최소 스폰 간격")]
        public float adventurerSpawnMinInterval = 15f;
        [Tooltip("모험가 최대 스폰 간격")]
        public float adventurerSpawnMaxInterval = 45f;

        [Header("이동 속도")]
        [Tooltip("방문자 길드 내부 진입 + 전원 퇴장 속도 (유닛/초)")]
        public float visitorMoveSpeed = 1.5f;
        [Tooltip("전령 진입 속도 (유닛/초)")]
        public float heraldMoveSpeed = 3f;
        [Tooltip("튜토리얼 진행 중 방문자 이동 속도 (접근/진입/퇴장 전 구간, 유닛/초)")]
        public float tutorialMoveSpeed = 3f;

        [Header("행인(Walker)")]
        [Tooltip("행인 최소 스폰 간격 (게임 시간 초)")]
        public float walkerSpawnMinInterval = 3f;
        [Tooltip("행인 최대 스폰 간격 (게임 시간 초)")]
        public float walkerSpawnMaxInterval = 8f;
        [Tooltip("행인이 윗길로 지나갈 확률 (0~1)")]
        [Range(0f, 1f)] public float walkerTopRoadChance = 0.5f;
        [Tooltip("행인 최소 속도 (방문자 접근 구간과 공용)")]
        public float walkerMinSpeed = 1f;
        [Tooltip("행인 최대 속도 (방문자 접근 구간과 공용)")]
        public float walkerMaxSpeed = 2.5f;
        [Tooltip("이 속도 초과면 Run, 이하면 Walk 애니메이션")]
        public float walkerRunSpeedThreshold = 1.5f;
        [Tooltip("행인이 뛸(Run) 확률. 나머지는 Walk. 낮을수록 대부분 걷는다")]
        [Range(0f, 1f)] public float walkerRunChance = 0.15f;
        [Tooltip("스폰 1회(run)당 독립 시행 횟수 - 아침")]
        public int walkerTrialsMorning = 2;
        [Tooltip("스폰 1회(run)당 독립 시행 횟수 - 낮")]
        public int walkerTrialsDay = 7;
        [Tooltip("스폰 1회(run)당 독립 시행 횟수 - 저녁")]
        public int walkerTrialsEvening = 3;
        [Tooltip("각 독립 시행에서 무리가 스폰될 확률 (0~1). 낮을수록 뜸하게")]
        [Range(0f, 1f)] public float walkerTrialChance = 0.2f;
        [Tooltip("불러오기(복원) 시 즉시 스폰할 행인 수 - 아침")]
        public int walkerRestoreMorning = 2;
        [Tooltip("불러오기(복원) 시 즉시 스폰할 행인 수 - 낮")]
        public int walkerRestoreDay = 5;
        [Tooltip("불러오기(복원) 시 즉시 스폰할 행인 수 - 저녁")]
        public int walkerRestoreEvening = 3;
        [Tooltip("무리 내 행인 간 등장 시간차 (초)")]
        public float walkerGroupStagger = 0.35f;

        [Header("원근 정렬 (sortingOrder)")]
        [Tooltip("가장 뒤(y 최대, 윗길)에 놓일 최소 sortingOrder. 배경/건물 뒤 기준보다 위여야 함")]
        public int npcSortingOrderMin = 2;
        [Tooltip("가장 앞(y 최소, 아랫길)에 놓일 최대 sortingOrder. PanelCanvas(100)보다 아래여야 함")]
        public int npcSortingOrderMax = 95;
        [Tooltip("y를 몇 단계로 나눠 정렬할지 (많을수록 촘촘)")]
        public int npcSortingSteps = 40;

        [Header("체류시간 (게임 시간 초 단위)")]
        [Tooltip("무기상점 체류 시간")]
        public float weaponShopStayDuration = 60f;
        [Tooltip("모험가 체류 시간")]
        public float adventurerStayDuration = 45f;
        [Tooltip("이벤트 NPC 체류 시간")]
        public float eventNPCStayDuration = 90f;
        [Tooltip("대장장이 체류 시간")]
        public float blacksmithStayDuration = 60f;
        [Tooltip("전령 체류 시간 (게임 시간 분 단위, 18:00 등장 기준 21:00을 넘겨야 함)")]
        public float heraldStayDuration = 183f;

        [Header("경고 시스템")]
        [Tooltip("경고 표시 임계값 (게임 시간 초 단위)")]
        public float warningTimeThreshold = 10f;

        [Header("호감도 시스템")]
        [Tooltip("호감도 Max 달성 시 체류 시간")]
        public int maxAffectionStayDuration = 60;
        [Tooltip("호감도 Max 달성자의 재방문 가중치")]
        public float maxAffectionRevisitWeight = 0.7f;
        [Tooltip("일반 모험가의 재방문 가중치")]
        public float normalRevisitWeight = 0.3f;

        [Header("재방문 시스템")]
        [Tooltip("재방문 기본 확률")]
        public float baseRevisitChance = 0.5f;
        [Tooltip("호감도 Max 1명당 재방문 확률 증가")]
        public float maxAffectionBonusPerAdventurer = 0.2f;
        [Tooltip("최대 재방문 확률")]
        public float maxRevisitChance = 0.9f;
        [Tooltip("재방문 최소 대기 일수")]
        public int minRevisitDays = 3;
        [Tooltip("재방문 최대 대기 일수")]
        public int maxRevisitDays = 6;
        [Tooltip("재방문 판정에 필요한 최소 호감도")]
        public int minAffectionForRevisit = 50;

        [Header("일반 모험가 (isNamed=false)")]
        [Tooltip("아침에 사전 생성할 일반 모험가 방문 풀 크기")]
        public int normalAdventurerDailyPoolSize = 8;
        [Tooltip("스폰 시 네임드 선택 가중치")]
        public float namedAdventurerSpawnWeight = 1f;
        [Tooltip("스폰 시 일반 선택 가중치")]
        public float normalAdventurerSpawnWeight = 5f;
        [Tooltip("일반 모험가 스탯 최솟값")]
        public int normalAdventurerStatMin = 10;
        [Tooltip("일반 모험가 스탯 최댓값")]
        public int normalAdventurerStatMax = 70;

        [Header("모험가 대사 - 성별/상황별 랜덤 풀 (비면 코드 fallback 사용)")]
        [Tooltip("첫 방문 (방문 횟수 0)")]
        public GenderedDialogueLines firstVisitLines = new();
        [Tooltip("재방문 - 호감도 낮음 (0~25)")]
        public GenderedDialogueLines affectionLowLines = new();
        [Tooltip("호감도 보통 (26~50)")]
        public GenderedDialogueLines affectionMediumLines = new();
        [Tooltip("호감도 높음 (51~75)")]
        public GenderedDialogueLines affectionHighLines = new();
        [Tooltip("호감도 매우 높음 (76~99)")]
        public GenderedDialogueLines affectionMaxLines = new();
        [Tooltip("호감도 100 달성")]
        public GenderedDialogueLines maxAffectionReachedLines = new();
    }
}