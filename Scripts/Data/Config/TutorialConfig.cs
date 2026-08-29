// Scripts/Data/Config/TutorialConfig.cs
using UnityEngine;
using System;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 전령 대사 시퀀스 식별자. 대화창 1회 = 키 1개.
    /// 값 = 단계 번호 x 100 + 순번 (백의 자리가 TutorialStep과 대응).
    /// 저장되지 않으므로(대사는 GameData에 안 들어감) 세이브 호환성과 무관하다.
    /// 다만 Unity가 enum을 int로 직렬화하므로, 값을 바꾸면 TutorialConfig.asset의 steps[].key를 같은 매핑으로
    /// 반드시 함께 재지정해야 한다 - 안 하면 대사가 통째로 밀린다.
    /// 새 대사는 해당 단계 블록 끝에 다음 번호로 추가하는 것이 기본이고,
    /// 9단계처럼 재생 순서 = 번호 순서로 정리한 블록은 중간 삽입 시 뒤 번호를 밀고 에셋도 함께 재지정한다.
    /// </summary>
    public enum TutorialLineKey
    {
        // 1단계: 무기상점 (TutorialStep.WeaponShop)
        WeaponShopIntro        = 100,  // 튜토리얼 진입 - 전령 자기소개 + 무기 대여업 설명 + 상인 안내 (3박스)
        WeaponShopFree         = 101,  // 상점 열림 직후 - 4종 전부 무료 안내
        WeaponShopClosing      = 102,  // 4종 구매 완료 - 1단계 마무리 인사

        // 2단계: 대장장이 (TutorialStep.Blacksmith)
        BlacksmithIntro        = 200,  // 2-A 진입 - 강화 배우기 예고 + 대장장이 안내
        BlacksmithCraftIntro   = 201,  // 2-B 제작 - 행운의 부적 제작 유도
        BlacksmithEnforceIntro = 202,  // 2-C 강화 - 조잡한 단검 강화 유도
        BlacksmithEvolveIntro  = 203,  // 2-D 진화 - 등급 상승 유도
        BlacksmithRerollIntro  = 204,  // 2-E 재부여 - 옵션 재굴림 유도
        BlacksmithOutro        = 205,  // 2-F 마무리 - 대장간 4종 요약 + 매일 방문 안내
        BlacksmithRerollResult = 206,  // 2-E 재부여 실행 후 - 이전/새 효과 선택 안내 (재생 순서는 204 다음, 205 앞)

        // 3단계: 아침 이벤트 (TutorialStep.MorningEvent)
        MorningEventIntro      = 300,  // 3-A 진입 - 아침 손님 + 사절단 등장 안내 (2박스)
        MorningEventGift       = 301,  // 3-B 사절단 상호작용 후 - 선물/평판 설명 + 아침 스킵 유도 (2박스)

        // 4단계: 의뢰판 (TutorialStep.QuestBoard)
        QuestBoardIntro        = 400,  // 4-A 진입 - 의뢰판 개념 + 카드 확인·3곳 선택 안내 (2박스)
        QuestBoardDoubleDungeon = 401, // 4-B 확정 후 - 2배 던전 발견 + 보상/소요시간 설명 (2박스)
        QuestBoardScoutIntro   = 402,  // 4-C - 던전 몬스터 변동 안내 (1박스, 하이라이트 없음)
        QuestBoardOutro        = 403,  // 4-E 수색 파견 후 - 결과 도착 예고 + 마무리 (1박스)
        QuestBoardScoutSend    = 404,  // 4-D - 수색꾼 개념 + 파견 유도 (1박스, 던전 A 하이라이트와 함께 표시)

        // 5단계: 모험 준비 진입 (TutorialStep.PrepEnter)
        PrepEnterIntro         = 500,  // 5-A 진입 - 첫 손님 등장 + 모험가 클릭 유도 (1박스)
        PrepEnterStructure     = 501,  // 5-B 준비 화면 진입 후 - 세 단계 구조 소개 (2박스)

        // 6단계: 모험 보내기 (TutorialStep.SendAdventure) — 6-A: 조사·테스트
        SendAdventureWeaponHintIntro = 600,  // 6-A-1 - 기본 무기 조사 유도 (1박스)
        SendAdventureAllStatsIntro   = 601,  // 6-A-2 - 종합 테스트 유도 (1박스)
        SendAdventureTestFollowup    = 602,  // 6-A-3 - 고급 판정 언급 + 특성 언급 + 지원 유도 (3박스)

        // 6단계 — 6-B: 지원(무기 대여 · 부적 선물)
        SendAdventureRentWeaponIntro = 603,  // 6-B-1 - 지원 단계 진입 후 무기 대여 유도 (1박스)
        SendAdventureStatArrowInfo   = 604,  // 6-B-2 - 대여 후 스탯 화살표 설명 (1박스, STR 화살표 하이라이트와 함께)
        SendAdventureCharmGiftIntro  = 605,  // 6-B-3 - 부적 선물 유도 (1박스, 부적 카드 하이라이트와 함께)

        // 6단계 — 6-C: 던전 A·B 비교 · 시뮬레이션
        SendAdventureCompareConfirmed = 606,  // 6-C-1 - 던전 A 방어타입 아이콘(노란색=확정) 하이라이트 (1박스)
        SendAdventureCompareUnknown   = 607,  // 6-C-2 - 던전 B 방어타입 아이콘('?'=미확정) 하이라이트 (1박스)
        SendAdventureCompareDouble    = 608,  // 6-C-3 - 던전 C '2배' 표시 하이라이트 (1박스)
        SendAdventureWeaponBonusMatch = 609,  // 6-C-4 - 검 마법갑옷 부가효과 일치 설명, 던전 A 하이라이트 (1박스)
        SendAdventureSelectDungeon    = 610,  // 6-C-5 - 던전 A 선택 유도 (1박스, A 카드 하이라이트). 시뮬레이션은 8단계(실패 던전)로 이월

        // 6단계 — 6-D: 모험 정보 · 점술
        SendAdventureInfoIntro       = 611,  // 6-D-1 - 모험 정보 칩 확인 유도 (1박스, 정보 칩 하이라이트와 함께)
        SendAdventureSeerIntro       = 612,  // 6-D-2 - 점술 유도 (1박스, 점술 버튼 하이라이트와 함께). 출발 유도는 6-E로 분리

        // 6단계 — 6-E: 모험 출발
        SendAdventureDepartIntro     = 613,  // 6-E-1 - 출발 유도 (1박스, 시작 버튼 하이라이트와 함께)
        SendAdventureDepartOutro     = 614,  // 6-E-2 - 출발 마무리 "모험가가 떠났어요!" (1박스, 준비화면 닫힌 뒤 배경 위)

        // 7단계: 가방 확인 (TutorialStep.Inventory)
        InventoryIntro        = 700,  // 7-1 진입 - 아이템 확인 유도 (1박스, 하단바 가방 버튼 하이라이트. 하단바를 보여야 하므로 minimalUI:false)
        InventoryManageIntro  = 701,  // 7-2 인벤토리 오픈 후 - 관리/보관 한계 설명 + 대여중 토글 유도 (2박스)
        InventoryRentedWeapon = 702,  // 7-3 토글 ON 후 - 대여 중인 검 확인 (1박스, 대여 무기 카드 하이라이트와 함께)
        InventoryOtherMenus   = 703,  // 7-4 - 나머지 하단바 버튼 언급 + 닫기 유도 (1박스, 닫기 버튼 하이라이트)

        // 8단계: 두 번째 손님 (TutorialStep.SecondGuest). 첫 모험 진행 중. 값 = 흐름 순서.
        SecondGuestGreeting         = 800,  // 8-1 모험가 입장 후 - 인사 (1박스, 모험가 하이라이트)
        SecondGuestWeaponHintIntro  = 801,  // 8-2 준비 화면 진입 후 - 기본 무기 조사 유도 (1박스, 조사 버튼 하이라이트)
        SecondGuestWeaponFound      = 802,  // 8-3 무기 조사 후(Tab1) - 주특기 검 발견 + 빌려줄 무기 궁리 (1박스, Tab2 이동 유도)
        SecondGuestBowSuggest       = 803,  // 8-4 Tab2 진입 후 - 검은 대여 중이라 활 추천 (1박스, 물푸레나무 활 카드 하이라이트)
        SecondGuestSimulationIntro  = 804,  // 8-6 Tab3 진입 - 다른 던전 유도 + 미확정('?') + 시뮬레이션 설명 (1박스, 던전 B 카드 하이라이트)
        SecondGuestSimulationAssume = 805,  // 8-7 던전 B 상세 진입 후 - "경장갑으로 가정해볼까요?" (1박스, 경장갑 토글 하이라이트)
        SecondGuestSimulationResult = 806,  // 8-8 경갑 시뮬 후 - 파란색=가정 설명 (1박스, 던전 B 선택 버튼 하이라이트)
        SecondGuestInfoUnconfirmed  = 807,  // 8-9 던전 B 선택 후 - 모험 정보의 '(미확정)' 표시 설명 (1박스, 모험 정보 하이라이트)
        SecondGuestDepartWhisper    = 808,  // 8-10 모험 정보 확인 후 - "(속닥)...출발!" (1박스, 시작 버튼 하이라이트)

        // 9단계: 모험 결과 (TutorialStep.AdventureResult) — 재생 순서 = 번호 순서. 두 모험 진행 중에 시작한다.
        // 9-A: 모험 화면 진입 + 진행 관찰 + 수면 마법 -> 저녁(18시) 전령 도착
        AdventureResultIntro        = 900,  // 9-A-1 진입 - "모험 버튼으로 살펴볼 수 있어요" (1박스, 하단바 모험 버튼 하이라이트. minimalUI:false)
        AdventureResultProgressInfo = 901,  // 9-A-2 진행 관찰 후 - 진행 화면은 진행 상황만, 결과는 저녁에 전령이 가져온다 (1박스)
        AdventureResultSleepMagic   = 902,  // 9-A-3 수면 마법 대사 (1박스, 이후 암전+진행화면 닫기+두 모험 완료+18시+전령 도착)
        AdventureResultWakeUp       = 903,  // 9-A-4 기상 - 저녁이 됐고 전령이 보고하러 왔다는 안내 (2박스, 이후 9-B 전령 하이라이트)

        // 9단계 — 9-B: 전령 클릭 -> 성공(A) 보고 리플레이 + 수수료 + 재료 매입
        AdventureResultSuccessCommission = 904,  // 9-B-1 결과 연출 완료 후 - 수수료 설명 + 재료 매입 유도 (2박스, 마지막에 첫 재료 아이템 하이라이트)
        AdventureResultDailyRoutine      = 905,  // 9-B-2 재료 매입 후 - "모험 중개->결과 확인->무기 관리 = 하루" (1박스, 닫기 버튼 하이라이트)

        // 9단계 — 9-C: 다음 보고 예고 -> 실패(B) 보고 리플레이 + 무기 파괴 + 교훈
        AdventureResultNextReport        = 906,  // 9-C-1 성공 결과창 닫은 후 - 다음 모험 예고 + 불안 복선 (1박스, 이후 전령이 B를 보고)
        AdventureResultFailureLesson     = 907,  // 9-C-2 실패 결과 연출 완료 후 - 실패 원인 + 활 파괴 + 교훈 (2박스, 마지막에 닫기 버튼 하이라이트)

        // 10단계: 퀘스트 소개 (TutorialStep.Quest)
        QuestIntro       = 1000,  // 10-1 진입 - 퀘스트 개념 안내 (1박스, 하단바 퀘스트 버튼 하이라이트. minimalUI:false)
        QuestExplain     = 1001,  // 10-2 퀘스트 화면 오픈 후 - 요구/진행/보상 + 벌금/게임오버 경고 + 완충 (3박스)
        QuestContentInfo = 1002,  // 10-3 - 퀘스트 콘텐츠(요구 조건/진행도) 영역 짚기 (1박스, 콘텐츠 영역 하이라이트와 함께, 마지막에 닫기 버튼 하이라이트)

        // 11단계: 나머지 UI (TutorialStep.RemainingUI) - 상단바 요소 순차 하이라이트, 조회 전용(조작 0)
        RemainingUIDay         = 1100,  // 11-1 날짜 하이라이트 (1박스)
        RemainingUITime        = 1101,  // 11-2 시간 하이라이트 (1박스)
        RemainingUIGold        = 1102,  // 11-3 보유 골드 하이라이트 (1박스)
        RemainingUIReputation  = 1103,  // 11-4 평판 게이지 하이라이트 (1박스)
        RemainingUISpeed       = 1104,  // 11-5 배속 버튼 하이라이트 (1박스)
        RemainingUILegacy      = 1105,  // 11-6 유산 포인트 하이라이트 (1박스, 마지막 -> 12단계 진입)

        // 12단계: 마무리 (TutorialStep.Finish) - 안목 언급 + 작별 + 20:00 스킵 자막 연출, 조작 0
        FarewellInsight  = 1200,  // 12-1 통찰(안목) 짧은 언급 (1박스, 하이라이트 없음)
        FarewellGoodbye  = 1201,  // 12-2 작별 인사 (1박스, 이후 자막 연출)
        FarewellSubtitle = 1202   // 12 자막(코드 생성 TMP 오버레이) - "이것저것 정리하느라..."
    }

    /// <summary>길드 전령 대화창 초상 종류. 대사 묶음(StepLines)별로 상황에 맞게 지정한다.</summary>
    public enum HeraldPortrait
    {
        Default = 0,     // 기본 (Guild)
        Embarrassed = 1, // 당황 (Guild_Embarrassed)
        Hello = 2,       // 인사 (Guild_Hello)
        Magic = 3,       // 마법 (Guild_Magic)
        Question = 4     // 의문 (Guild_Question)
    }

    [CreateAssetMenu(fileName = "TutorialConfig", menuName = "TodaysWeaponRental/Config/TutorialConfig")]
    public class TutorialConfig : ScriptableObject
    {
        [Serializable]
        public class StepLines
        {
            public TutorialLineKey key;

            // 이 대사 묶음을 표시할 때의 전령 초상(상황별). 기본값 Default.
            public HeraldPortrait portrait = HeraldPortrait.Default;

            [TextArea(2, 5)]
            public string[] lines;   // 대화창에 한 박스씩 순서대로 표시된다.

            // 현재 언어로 번역한 사본. 매번 새로 만들면 안 된다 - GetPortraitSprite가
            // 이 배열의 **참조**로 묶음을 되찾기 때문이다(아래 주석 참고).
            [NonSerialized] public string[] localized;
            [NonSerialized] public string localizedLocale;
        }

        [Header("1단계: 무료 지급 무기 ID (각 타입별 Common 등급 - 검/단검/지팡이/활)")]
        [SerializeField] private string[] tutorialWeaponIDs;

        [Header("2-A: 대장장이 ID (랜덤 타입이 오면 연출이 어긋남]")]
        [SerializeField] private string blacksmithIntroNPCID;

        [Header("2-C: 강화 대상 무기 ID (모험에 내보내지 않는 고정 무기)")]
        [SerializeField] private string enforceWeaponID;

        [Header("4단계: 의뢰판 고정 매물 던전 ID (A=수색·성공용 / B=미확정·시뮬레이션·실패용 / C=2배 던전 전시용)")]
        [SerializeField] private string tutorialDungeonAID;
        [SerializeField] private string tutorialDungeonBID;
        [SerializeField] private string tutorialDungeonCID;

        [Header("5단계: 첫 번째 튜토리얼 전용 모험가 ID (STR 특화·주특기 검 고정)")]
        [SerializeField] private string tutorialAdventurer1ID;

        [Header("8단계: 두 번째 튜토리얼 전용 모험가 ID (주특기 검 고정, 첫 모험가에게 대여 중이라 활을 빌려야 함)")]
        [SerializeField] private string tutorialAdventurer2ID;

        [Header("6-C: 대여용 연습용 검에 고정할 부가효과 ID (방어타입 일치-마법갑옷)")]
        [SerializeField] private string rentWeaponFixedEffectID;

        [Header("8단계: 대여용 물푸레나무 활에 고정할 부가효과 ID (방어타입 일치-경장갑, 시뮬 가정과 맞물림)")]
        [SerializeField] private string rentBowFixedEffectID;

        [Header("단계별 전령 대사 (조작 1회당 대사 1박스 원칙)")]
        [SerializeField] private List<StepLines> steps = new List<StepLines>();

        [Header("전령 초상 스프라이트 (HeraldPortrait 매핑)")]
        [SerializeField] private Sprite portraitDefault;      // Guild
        [SerializeField] private Sprite portraitEmbarrassed;  // Guild_Embarrassed
        [SerializeField] private Sprite portraitHello;        // Guild_Hello
        [SerializeField] private Sprite portraitMagic;        // Guild_Magic
        [SerializeField] private Sprite portraitQuestion;     // Guild_Question

        public IReadOnlyList<string> TutorialWeaponIDs => tutorialWeaponIDs;
        public string EnforceWeaponID => enforceWeaponID;
        public string BlacksmithIntroNPCID => blacksmithIntroNPCID;
        public string TutorialDungeonAID => tutorialDungeonAID;
        public string TutorialDungeonBID => tutorialDungeonBID;
        public string TutorialDungeonCID => tutorialDungeonCID;
        public string TutorialAdventurer1ID => tutorialAdventurer1ID;
        public string TutorialAdventurer2ID => tutorialAdventurer2ID;
        public string RentWeaponFixedEffectID => rentWeaponFixedEffectID;
        public string RentBowFixedEffectID => rentBowFixedEffectID;

        /// <summary>키에 해당하는 대사 목록(현재 언어). 없으면 빈 목록(대화창은 즉시 닫히고 onComplete로 진행).</summary>
        public IList<string> GetLines(TutorialLineKey key)
        {
            foreach (var step in steps)
            {
                if (step.key == key) return Localize(step);
            }

            Log.Error($"[TutorialConfig] '{key}' 대사가 없습니다!");
            return Array.Empty<string>();
        }

        /// <summary>
        /// 대사 묶음을 현재 언어로 옮긴 사본을 돌려준다. 언어별로 **한 번만** 만들고 그 뒤에는
        /// 같은 배열을 계속 돌려준다 — GetPortraitSprite가 참조 동일성으로 묶음을 되찾기 때문에
        /// 호출할 때마다 새 배열을 주면 초상 조회가 전부 실패한다.
        /// </summary>
        private static string[] Localize(StepLines step)
        {
            if (step.lines == null || step.lines.Length == 0) return Array.Empty<string>();

            string locale = DataLocalizer.CurrentLocaleCode;
            if (step.localized != null && step.localizedLocale == locale) return step.localized;

            var copy = new string[step.lines.Length];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = DataLocalizer.TutorialLine(step.key, i, step.lines[i]);

            step.localized       = copy;
            step.localizedLocale = locale;
            return copy;
        }

        /// <summary>
        /// 대사 묶음에 지정된 전령 초상 스프라이트를 반환한다. GetLines가 돌려준 lines 배열 참조로 묶음을 역방향 조회한다.
        /// 못 찾으면(비-GetLines 호출 등) null — 이 경우 View는 기존 초상을 유지한다.
        /// </summary>
        public Sprite GetPortraitSprite(IList<string> lines)
        {
            if (lines == null) return null;
            foreach (var step in steps)
            {
                // 비교 대상은 **번역 사본**이다. 원본 step.lines와 비교하면 한국어 외 언어에서
                // 전부 매칭에 실패하고, SetPortrait(null)이 되어 초상이 직전 것으로 남는다.
                // 로그도 안 남아서 화면을 봐야만 알 수 있는 종류의 고장이다.
                if (ReferenceEquals(step.localized, lines)) return MapPortrait(step.portrait);
            }
            return null;
        }

        /// <summary>
        /// 초상 종류를 직접 지정해 스프라이트를 얻는다. 대사가 TutorialConfig.steps에 없는 경로
        /// (시세 계단 공지 등 PriceTierConfig 대사)에서 쓴다 - 스프라이트는 여기에만 있다.
        /// </summary>
        public Sprite GetPortraitSprite(HeraldPortrait portrait) => MapPortrait(portrait);

        private Sprite MapPortrait(HeraldPortrait portrait)
        {
            switch (portrait)
            {
                case HeraldPortrait.Embarrassed: return portraitEmbarrassed;
                case HeraldPortrait.Hello:       return portraitHello;
                case HeraldPortrait.Magic:       return portraitMagic;
                case HeraldPortrait.Question:    return portraitQuestion;
                default:                         return portraitDefault;
            }
        }
    }
}
