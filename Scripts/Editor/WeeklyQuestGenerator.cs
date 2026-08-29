#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 주간 퀘스트 생성기 — 레벨 디자인 기반 개편.
    /// 설계: Documents/balance/reference/주간퀘스트_레벨디자인.md (아키타입 지도 §5, 도입 스케줄 §4, 조합 규칙 §6)
    /// 수치 근거: Documents/balance/reference/주간퀘스트_난이도_계수.md (계수·캡·평판 게이트)
    ///
    /// - 1~40주차: 주차 스크립트(아키타입) 기반 생성. 셔플 폐기, 랜덤은 무기 타입·던전 개체 선택에만.
    /// - 41~56주차: 엔드리스 반복 풀 템플릿 (난이도 4등급 x 4벌). 런타임 추첨은 QuestManager.
    /// - weekNumber가 같은 기존 자산은 재사용(덮어쓰기)해 GUID/참조를 보존한다.
    /// </summary>
    public static class WeeklyQuestGenerator
    {
        #region 상수 — 레벨디자인 문서

        // 40주 캠페인 = 280일. 하루 5분(4배속 1.25분)이라 4배속 기준 약 5.8시간 —
        // 목표 회차 플레이타임 5~6시간에 맞춘 값이다(2026-07-29 결정).
        // 60주(8.8시간)였을 때는 목표 수명(중급 35주)보다 캠페인이 길어 T4가 통째로 사장됐다.
        private const int CAMPAIGN_WEEKS = 40;
        private const int ENDLESS_FIRST = 41;
        private const int ENDLESS_LAST = 56;
        private const string FOLDER = "Assets/_Projects/Data/WeeklyQuests";

        // 모험 성공: SC(w) = min(round(3 + 0.425w), 20) — 40주 캠페인에서 SC(40)=20
        private const float SUCCESS_BASE = 3f, SUCCESS_PER_WEEK = 0.425f;
        private const int SUCCESS_CAP = 20;

        // 특정 무기 캡 — 실측(시뮬)상 교차 주차의 실제 병목이라 10에서 하향
        private const int WEAPON_CAP = 6;
        private const float WEAPON_COEF = 2f;   // 특정 무기 계수

        // 등급 요구 계수 (고급/희귀/영웅/전설) — 계수 문서 §3
        private static readonly float[] GradeCoef = { 0f, 1f, 3f, 7f, 15f };

        // 주차 -> 예상 평판 등급. 실측상 상급자만 Diamond에 닿고 중급자는 Silver에 머문다.
        // 상급자 기준으로 잡으면 중급자에게 과한 등급 요구가 나가므로 한 단계 보수적으로 둔다.
        // 40주 리스케일 (구 21/41주 = 진행도 35%/68% -> 14/27주).
        private static int ExpectedRepLevel(int w) => w >= 27 ? 2 : w >= 14 ? 1 : 0;

        // 평판별 등급 요구 캡 [평판][Grade] — 계수 문서 §3-1 ("-" = 0)
        private static readonly int[][] GradeCapByRep =
        {
            new[] { 0, 10, 3, 0, 0 },   // Bronze
            new[] { 0, 10, 5, 2, 0 },   // Silver
            new[] { 0, 10, 7, 3, 1 },   // Gold
            new[] { 0, 10, 7, 5, 2 },   // Platinum
            new[] { 0, 10, 7, 5, 3 },   // Diamond
        };

        // 평판별 던전 요구 캡 [평판][Grade] — 계수 문서 §4-1
        private static readonly int[][] DungeonCapByRep =
        {
            new[] { 2, 1, 0, 0, 0 },
            new[] { 2, 2, 1, 0, 0 },
            new[] { 3, 2, 1, 1, 0 },
            new[] { 3, 3, 2, 1, 1 },
            new[] { 4, 3, 2, 2, 1 },
        };

        // 도입 스케줄 §4 — 등급/던전 해금 주차 (40주 리스케일).
        // 각 등급은 해당 티어 시작 주에 도입된다 (고급 5=T1, 희귀 11=T2, 영웅 21=T3, 전설 31=T4).
        private static readonly int[] GradeIntroWeek = { 0, 5, 11, 21, 31 };      // Uncommon~Legendary
        private static readonly int[] DungeonIntroWeek = { 2, 3, 12, 22, 32 };    // Common~Legendary
        private const int GREAT_INTRO_WEEK = 6;
        private const int WEAPON_INTRO_WEEK = 2;
        private const int DUNGEON_DOUBLE_WEEK = 25;

        // 티어 경계 — 40주 압축. 앞 세 경계(4/10/20주 = 28/70/140일)는 가격 계단·의뢰판 배율과
        // 같은 지점에 놓아 난이도가 한 번에 뛰도록 맞췄다.
        private static readonly int[] TierMaxWeek = { 4, 10, 20, 30, 40 };
        // 티어 시험주 앵커 부하 §3-1 (T0~T4). 원안(8/15/21/35/32)은 실측 통과율이 합격선에
        // 크게 못 미쳐 후반 티어를 낮췄다.
        // 앵커는 등급 요구 수치만 통제한다. +50% A/B에서 중급 통과율이 74% -> 74%로 무변화라
        // (2026-07-29 실측) 난이도 손잡이가 아님이 확인됐다 - 캠페인도 엔드리스와 같이 던전이 병목이다.
        private static readonly float[] ExamAnchor = { 8f, 12f, 16f, 24f, 24f };
        private const float T0_START_ANCHOR = 4f;

        private enum Arch { Onboarding, Intro, Rest, Standard, SubPeak, Variation, Exam }

        // 아키타입 이용률 U §3-2
        private static float Utilization(Arch a) => a switch
        {
            Arch.Onboarding => 0.5f,
            Arch.Intro => 0.7f,
            Arch.Rest => 0.7f,
            Arch.Standard => 0.85f,
            Arch.SubPeak => 0.95f,
            Arch.Variation => 0.9f,
            Arch.Exam => 1.0f,
            _ => 0.85f,
        };

        // 보상 배율 §7
        private static float RewardMult(Arch a) => a switch
        {
            Arch.Onboarding => 0.9f,
            Arch.Intro => 0.9f,
            Arch.Rest => 0.9f,
            Arch.SubPeak => 1.1f,
            Arch.Variation => 1.1f,
            Arch.Exam => 1.5f,
            _ => 1f,
        };

        // 보상/벌금 기본 곡선 §7 — 40주 캠페인으로 기울기 리스케일 (x60/40 = 1.5).
        // 같은 진행도에 같은 값이 되도록 맞췄다: 100% 지점 골드 35,780 -> 35,800, 벌금 181,200 동일.
        private const float GOLD_BASE = 800f, GOLD_PER_WEEK = 875f;
        private const float REQ_REWARD_STEP = 0.3f;
        private const int REP_BASE = 30, REP_PER_WEEK = 10, REP_CAP = 400;
        // 벌금 — 폐업을 만들되 지출을 지배하지 않는 선. 3000+1500w는 상급자까지 무너뜨렸다.
        // 초반 벌금(BASE)이 실력 구분선을 만든다 — 초급은 1주차부터 못 깨고 중급은 초반엔 깬다.
        // 후반 기울기를 올리면 셋 다 같이 죽어 구분이 안 된다.
        // BASE는 초반 구분선(초급), PER_WEEK는 후반 압박(중급)을 담당한다 — 두 층을 따로 조절하는 손잡이다.
        // 생존 주차를 구분선으로 삼는다(설계 결정). 통과율 격차(상급 77% / 중급 56%)가
        // 벌금 납부 빈도 23% vs 44%로 벌어지므로, 기울기를 세우면 중급자가 먼저 무너진다.
        // R 기준 체계: 가격은 전부 R 배수로 고정됐으므로 생존 주차는 벌금 하나로만 조절한다.
        // 목표는 "실패한 주의 벌금 = 그 주 수입의 50~60%".
        // **2차 곡선**: 벌금(w) = FINE_BASE + FINE_QUAD x w^2 (2026-07-29 전환).
        // 선형 `BASE + w x SLOPE`로는 초급 15주와 중급 35주를 동시에 맞출 수 없다 -
        // 초반을 낮추면 후반도 낮아지고, 후반을 올리면 초반도 올라가기 때문이다
        // (문서가 "BASE가 음수여야 한다"고 지적한 조합).
        // 선형 4500일 때 초급이 9주차 벌금 41,700G 한 방에 죽었다(목표 15주).
        // 2차로 바꾸면 9주차가 10,313G로 내려가고 40주차 종착점(181,200G)은 그대로다.
        private const int FINE_BASE = 1200;
        private const float FINE_QUAD = 200f;
        // 평판 페널티 상한 150 — 주간 평판 획득(약 160)을 넘으면 실패 한 번에 등급이 무너진다
        // 기울기는 보상(REP_PER_WEEK 7)과 동률로 맞춘다. 13이면 2주차부터 벌점이 보상을 추월해
        // 퀘스트를 놓치는 순간 어떤 완주율로도 평판이 우상향하지 못했다(2026-07-29 진단).
        private const int PENALTY_BASE = 20, PENALTY_PER_WEEK = 10, PENALTY_CAP = 150;

        #endregion

        #region 주차 지도 §5

        // 40주 지도 §5. 파동 규칙 유지 — 시험주 직전은 휴식, 소피크·시험 비인접, 시험 직후는 도입.
        //   T0(1~4)   온 도 도 시
        //   T1(5~10)  도 도 표 소 휴 시
        //   T2(11~20) 도 도 표 소 휴 표 변 소 휴 시
        //   T3(21~30) 도 도 표 소 휴 표 변 소 휴 시
        //   T4(31~40) 도 도 표 소 휴 표 소 표 휴 시
        private static readonly int[] IntroWeeks = { 2, 3, 5, 6, 11, 12, 21, 22, 31, 32 };
        private static readonly int[] VariationWeeks = { 17, 27 };
        private static readonly int[] ExamWeeks = { 4, 10, 20, 30, 40 };
        private static readonly int[] RestWeeks = { 9, 15, 19, 25, 29, 35, 39 };
        private static readonly int[] SubPeakWeeks =
        {
            8,                          // T1
            14, 18,                     // T2
            24, 28,                     // T3
            34, 37,                     // T4
        };

        private static Arch ArchOf(int w)
        {
            if (w == 1) return Arch.Onboarding;
            if (ExamWeeks.Contains(w)) return Arch.Exam;
            if (VariationWeeks.Contains(w)) return Arch.Variation;
            if (IntroWeeks.Contains(w)) return Arch.Intro;
            if (RestWeeks.Contains(w)) return Arch.Rest;
            if (SubPeakWeeks.Contains(w)) return Arch.SubPeak;
            return Arch.Standard;
        }

        #endregion

        #region 표시용

        private static string GradeKor(Grade g) => g switch
        {
            Grade.Common => "일반",
            Grade.Uncommon => "고급",
            Grade.Rare => "희귀",
            Grade.Epic => "영웅",
            Grade.Legendary => "전설",
            _ => "일반",
        };

        private static string WeaponKor(WeaponType t) => t switch
        {
            WeaponType.Sword => "검",
            WeaponType.Axe => "둔기",
            WeaponType.Bow => "활",
            WeaponType.Crossbow => "석궁",
            WeaponType.Staff => "지팡이",
            WeaponType.Tome => "마법서",
            WeaponType.Dagger => "단검",
            WeaponType.Shuriken => "수리검",
            _ => "무기",
        };

        // 제목 매트릭스 — 레벨디자인 문서 부록 B (TMP 폰트 규칙: 한글/ASCII만)
        private static string Title(int w, Arch a, System.Random rng)
        {
            if (w == CAMPAIGN_WEEKS) return "전설의 완성";
            int tier = TierOf(w);
            string[] pool = (tier, a) switch
            {
                (0, Arch.Onboarding) => new[] { "개업 첫 주" },
                (0, Arch.Intro) => new[] { "첫 걸음", "새로운 일감" },
                (0, Arch.Exam) => new[] { "견습 졸업 시험" },
                (0, _) => new[] { "길드의 부탁", "소박한 목표", "새내기 상인" },

                (1, Arch.Intro) => new[] { "등급의 벽", "낯선 주문" },
                (1, Arch.SubPeak) => new[] { "밀려드는 의뢰" },
                (1, Arch.Rest) => new[] { "한숨 돌리기" },
                (1, Arch.Exam) => new[] { "상인 자격 시험" },
                (1, _) => new[] { "성장의 나날", "바빠지는 상점", "이름을 알리다" },

                (2, Arch.Intro) => new[] { "희귀한 인연", "까다로운 주문" },
                (2, Arch.SubPeak) => new[] { "쏟아지는 주문" },
                (2, Arch.Rest) => new[] { "짧은 휴식" },
                (2, Arch.Variation) => new[] { "새로운 방식" },
                (2, Arch.Exam) => new[] { "중견 상회의 증명" },
                (2, _) => new[] { "번창하는 상점", "명성의 시작", "숙련된 손길" },

                (3, Arch.Intro) => new[] { "영웅들의 무대", "높아진 기대" },
                (3, Arch.SubPeak) => new[] { "절정의 성수기" },
                (3, Arch.Rest) => new[] { "폭풍 전의 고요" },
                (3, Arch.Variation) => new[] { "두 배의 부탁" },
                (3, Arch.Exam) => new[] { "명가의 자격" },
                (3, _) => new[] { "거상의 길", "치열한 경쟁" },

                (4, Arch.Intro) => new[] { "전설로 가는 길", "정점을 향해" },
                (4, Arch.SubPeak) => new[] { "최후의 성수기" },
                (4, Arch.Rest) => new[] { "마지막 준비" },
                (4, Arch.Variation) => new[] { "거장의 변덕" },
                (4, Arch.Exam) => new[] { "거장의 증명" },
                (4, _) => new[] { "왕국의 신뢰", "불멸의 상회" },

                _ => new[] { "길드의 부탁" },
            };
            return pool[rng.Next(pool.Length)];
        }

        /// <summary>제목이 난이도를 예고한다 — 플레이어가 이번 주가 고비인지 보고 알 수 있어야 한다</summary>
        private static string EndlessTitle(QuestDifficulty d, System.Random rng)
        {
            string[] pool = d switch
            {
                QuestDifficulty.Easy => new[] { "거장의 여유", "단골들의 주문" },
                QuestDifficulty.Normal => new[] { "끝나지 않는 명성", "왕국의 기둥", "영원한 대여점" },
                QuestDifficulty.Hard => new[] { "쏟아지는 주문", "고달픈 한 주" },
                _ => new[] { "살아있는 전설", "감당 못할 의뢰" },
            };
            return pool[rng.Next(pool.Length)];
        }

        #endregion

        #region 생성 진입점

        [MenuItem("Tools/Today's Weapon Rental/Generate Weekly Quests")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(FOLDER))
            {
                Debug.LogError($"[WeeklyQuestGenerator] 폴더 없음: {FOLDER}");
                return;
            }

            var dungeonsByGrade = CollectDungeonsByGrade();
            var dungeonInfos = CollectDungeonInfos();
            var existingByWeek = CollectExistingQuests();
            var warnings = new List<string>();

            int created = 0, reused = 0;
            var generated = new List<UnityEngine.Object>();
            var rotation = new RotationState();

            for (int w = 1; w <= ENDLESS_LAST; w++)
            {
                var rng = new System.Random(w);   // 결정론 — 무기 타입/던전 개체 선택에만 사용
                bool endless = w > CAMPAIGN_WEEKS;
                Arch arch = endless ? Arch.Standard : ArchOf(w);
                QuestDifficulty diff = endless ? EndlessDifficulty(w) : QuestDifficulty.Normal;

                List<QuestRequirement> reqs = endless
                    ? BuildEndlessRequirements(w, dungeonInfos, rng)
                    : BuildRequirements(w, arch, dungeonsByGrade, rotation, rng, warnings);

                string title = endless ? EndlessTitle(diff, rng) : Title(w, arch, rng);

                string fileName = $"QUEST_W{w:D2}_{title}";
                string targetPath = $"{FOLDER}/{fileName}.asset";

                bool isReuse = existingByWeek.TryGetValue(w, out var data);
                if (isReuse)
                {
                    // 파일 이동을 먼저 끝낸다 — RenameAsset이 재임포트를 일으켜 메모리 수정본을
                    // 디스크 내용으로 되돌리므로, 채우기 전에 옮겨야 한다.
                    // (이 순서가 뒤집혀 있어서 이름만 새로 붙고 내용은 옛것으로 저장됐다)
                    string curPath = AssetDatabase.GetAssetPath(data);
                    if (curPath != targetPath)
                        AssetDatabase.RenameAsset(curPath, fileName);
                    reused++;
                }
                else
                {
                    data = ScriptableObject.CreateInstance<WeeklyQuestData>();
                    AssetDatabase.CreateAsset(data, targetPath);
                    created++;
                }

                PopulateQuest(data, w, arch, diff, title, reqs, endless);
                EditorUtility.SetDirty(data);

                generated.Add(data);
            }

            // 범위 밖(구 커리큘럼 잔여) 에셋 제거 — 남겨두면 마지막 주차 폴백이 엉뚱한 퀘스트를 집는다.
            // existingByWeek는 ENDLESS_LAST 이하만 담으므로 폴더를 다시 훑는다.
            int deleted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:WeeklyQuestData", new[] { FOLDER }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var q = AssetDatabase.LoadAssetAtPath<WeeklyQuestData>(path);
                if (q == null || q.weekNumber <= ENDLESS_LAST) continue;
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.objects = generated.ToArray();
            EditorUtility.FocusProjectWindow();

            foreach (var warn in warnings)
                Debug.LogWarning($"[WeeklyQuestGenerator] 검증: {warn}");

            Debug.Log($"[WeeklyQuestGenerator] 완료 - 신규: {created}, 갱신: {reused}, 삭제: {deleted} " +
                      $"(캠페인 {CAMPAIGN_WEEKS}주 + 엔드리스 템플릿 {ENDLESS_LAST - ENDLESS_FIRST + 1}개, 경고 {warnings.Count}건). " +
                      "DataManager.weeklyQuestDataArray에 폴더를 드래그해 재할당하세요. " +
                      "주의: 101주차 이후 런타임 선택(QuestManager)은 별도 패치가 필요합니다.");
        }

        #endregion

        #region 요구조건 생성 — 캠페인 (1~100주)

        /// <summary>주차 간 로테이션 상태 — 연속 중복 방지(§6 규칙 5)와 도입 연습 재출제(§5-2)</summary>
        private class RotationState
        {
            public WeaponType? lastWeaponType;
            public string lastDungeonID;
            public Grade lastHeavyGrade = Grade.Common;
            public int heavyGradeStreak;
            public int lightRotation;
        }

        private static List<QuestRequirement> BuildRequirements(
            int w, Arch arch, Dictionary<Grade, List<DungeonInfo>> dungeons,
            RotationState rot, System.Random rng, List<string> warnings)
        {
            var reqs = new List<QuestRequirement> { MakeSuccess(w) };
            float load = Utilization(arch) * AnchorAt(w);

            switch (arch)
            {
                case Arch.Onboarding:
                    break;

                case Arch.Intro:
                    AddIntroElement(w, reqs, dungeons, rot, rng);
                    break;

                case Arch.Rest:
                    // 가벼운 로테이션 1개 — 던전 고정.
                    // 실측(2026-07-29)상 난이도를 만드는 건 던전 요구뿐이고, 그마저 60주 중 29주에만
                    // 걸려 있어 캠페인이 평평했다. 무기 교대를 빼고 빈도를 올린다.
                    AddDungeon(reqs, w, LowestUnlockedDungeonGrade(w), dungeons, rot, rng);
                    break;

                case Arch.Standard:
                    AddHeavy(reqs, w, load, rot, rng);
                    AddPracticeOrLight(reqs, w, dungeons, rot, rng);
                    break;

                case Arch.SubPeak:
                    AddHeavy(reqs, w, load, rot, rng);
                    // 교차: 첫 교차(17주) 이후 소피크에서 격주 허용 (§6 규칙 6)
                    if (w > 17 && (w / 4) % 2 == 0 && reqs.Any(r => r.questType == QuestType.RentSpecificGrade))
                        AddWeapon(reqs, w, Mathf.Min(SuccessCount(w), 8), rot, rng);
                    else
                        AddPracticeOrLight(reqs, w, dungeons, rot, rng);
                    break;

                case Arch.Variation:
                    BuildVariation(w, reqs, dungeons, rot, rng);
                    break;

                case Arch.Exam:
                    reqs.Clear();
                    BuildExam(w, reqs, dungeons, rng);
                    break;
            }

            Validate(w, arch, reqs, warnings);
            return reqs.OrderBy(SortKey).ToList();
        }

        /// <summary>도입주: 신규 요소 1개, 최소 수치 (§4)</summary>
        private static void AddIntroElement(
            int w, List<QuestRequirement> reqs,
            Dictionary<Grade, List<DungeonInfo>> dungeons, RotationState rot, System.Random rng)
        {
            switch (w)
            {
                case 2: AddDungeon(reqs, w, Grade.Common, dungeons, rot, rng); break;
                case 3: AddDungeon(reqs, w, Grade.Uncommon, dungeons, rot, rng); break;
                case 5: AddGrade(reqs, w, Grade.Uncommon, 1); break;
                case 6: AddGreat(reqs, w); break;
                case 11: AddGrade(reqs, w, Grade.Rare, 1); break;
                case 12: AddDungeon(reqs, w, Grade.Rare, dungeons, rot, rng); break;
                case 21: AddGrade(reqs, w, Grade.Epic, 1); break;
                case 22: AddDungeon(reqs, w, Grade.Epic, dungeons, rot, rng); break;
                case 31: AddGrade(reqs, w, Grade.Legendary, 1); break;
                case 32: AddDungeon(reqs, w, Grade.Legendary, dungeons, rot, rng); break;
            }
        }

        /// <summary>변주주 §4: 33 첫 교차 / 61 던전 2회 / 87 3중 교차</summary>
        private static void BuildVariation(
            int w, List<QuestRequirement> reqs,
            Dictionary<Grade, List<DungeonInfo>> dungeons, RotationState rot, System.Random rng)
        {
            switch (w)
            {
                case 17:   // 첫 교차: 등급 + 특정 무기
                    AddGrade(reqs, w, Grade.Rare, ClampGrade(w, Grade.Rare, 3));
                    AddWeapon(reqs, w, Mathf.Min(SuccessCount(w), 6), rot, rng);
                    break;
                case 27:   // 3중 교차: 등급 + 무기 + 던전
                    AddGrade(reqs, w, Grade.Epic, ClampGrade(w, Grade.Epic, 3));
                    AddWeapon(reqs, w, Mathf.Min(SuccessCount(w), 8), rot, rng);
                    AddDungeon(reqs, w, Grade.Uncommon, dungeons, rot, rng);
                    break;
            }
        }

        /// <summary>시험주 명세 §5-1 (고정)</summary>
        private static void BuildExam(
            int w, List<QuestRequirement> reqs,
            Dictionary<Grade, List<DungeonInfo>> dungeons, System.Random rng)
        {
            reqs.Add(MakeSuccess(w));
            switch (w)
            {
                case 4:    // SC 5 — 첫 시험. 특정 무기 요구는 타입 운이 커서 2회로 둔다
                    reqs.Add(MakeWeapon(w, 2, rng, null));
                    reqs.Add(MakeDungeon(Grade.Uncommon, 1, dungeons, rng, null));
                    break;
                case 10:   // SC 7 — 고급 + 무기 교차 (교차 첫 등장 — 시험 한정 예외)
                    reqs.Add(MakeGrade(Grade.Uncommon, 4));
                    reqs.Add(MakeWeapon(w, 3, rng, null));
                    break;
                case 20:   // SC 12 — 희귀 + 대성공 + 희귀 던전
                    reqs.Add(MakeGrade(Grade.Rare, 3));
                    reqs.Add(MakeGreat());
                    reqs.Add(MakeDungeon(Grade.Rare, 1, dungeons, rng, null));
                    break;
                case 30:   // SC 16 — 희귀 + 무기 교차 + 영웅 던전
                    reqs.Add(MakeGrade(Grade.Rare, 5));
                    reqs.Add(MakeWeapon(w, 5, rng, null));
                    reqs.Add(MakeDungeon(Grade.Epic, 1, dungeons, rng, null));
                    break;
                // SC 20 — 최종 시험. 전설 던전 요구는 실측 통과율 2%의 병목이라 영웅 던전으로 낮춘다.
                // 전설 무기는 보유해도 1자루뿐이라 회전이 안 돌아 시험을 봉쇄한다(실측 13%).
                // 최종 시험의 상징은 영웅 이상 + 무기 교차로 낸다.
                case 40:
                    reqs.Add(MakeGrade(Grade.Epic, 3));
                    reqs.Add(MakeWeapon(w, 5, rng, null));
                    reqs.Add(MakeDungeon(Grade.Epic, 1, dungeons, rng, null));
                    break;
            }
        }

        /// <summary>표준/소피크의 최중량 목표 — 부하 예산에 맞춰 등급 요구(또는 무기)로 채운다 (§5-2)</summary>
        private static void AddHeavy(List<QuestRequirement> reqs, int w, float load, RotationState rot, System.Random rng)
        {
            Grade top = HighestUnlockedGrade(w);
            if (top == Grade.Common)
            {
                // 등급 미해금 (T0) — 무기가 최중량
                AddWeapon(reqs, w, Mathf.Clamp(Mathf.RoundToInt(load / WEAPON_COEF), 2, Mathf.Min(WEAPON_CAP, SuccessCount(w))), rot, rng);
                return;
            }

            // 같은 등급 3주 연속 금지 (§6 규칙 5) — 스트릭이 차면 한 단계 낮추거나 무기로 회피
            Grade pick = top;
            if (rot.lastHeavyGrade == top && rot.heavyGradeStreak >= 2)
            {
                Grade lower = LowerUnlockedGrade(w, top);
                if (lower != Grade.Common) pick = lower;
                else
                {
                    AddWeapon(reqs, w, Mathf.Clamp(Mathf.RoundToInt(load / WEAPON_COEF), 2, Mathf.Min(WEAPON_CAP, SuccessCount(w))), rot, rng);
                    rot.heavyGradeStreak = 0;
                    return;
                }
            }

            int n = ClampGrade(w, pick, Mathf.RoundToInt(load / GradeCoef[(int)pick]));
            AddGrade(reqs, w, pick, n);

            if (rot.lastHeavyGrade == pick) rot.heavyGradeStreak++;
            else { rot.lastHeavyGrade = pick; rot.heavyGradeStreak = 1; }
        }

        /// <summary>세 번째 슬롯 — 최근 도입 요소 연습 재출제, 아니면 던전/대성공 로테이션</summary>
        private static void AddPracticeOrLight(
            List<QuestRequirement> reqs, int w,
            Dictionary<Grade, List<DungeonInfo>> dungeons, RotationState rot, System.Random rng)
        {
            // 도입 후 2주 내 연습 재출제 (§3-2 도입-연습-시험)
            int recentIntro = IntroWeeks.Where(iw => iw < w && w - iw <= 2).DefaultIfEmpty(-1).Max();
            if (recentIntro > 0)
            {
                switch (recentIntro)
                {
                    case 2: AddDungeon(reqs, w, Grade.Common, dungeons, rot, rng); return;
                    case 3: AddDungeon(reqs, w, Grade.Uncommon, dungeons, rot, rng); return;
                    case 6: AddGreat(reqs, w); return;
                    case 12: AddDungeon(reqs, w, Grade.Rare, dungeons, rot, rng); return;
                    case 22: AddDungeon(reqs, w, Grade.Epic, dungeons, rot, rng); return;
                    case 32: AddDungeon(reqs, w, Grade.Legendary, dungeons, rot, rng); return;
                    // 등급 도입(5/11/21/31)은 AddHeavy가 이미 해당 등급을 출제하므로 던전으로 대체
                }
            }

            // 던전 고정 — 대성공 로테이션을 뺀다.
            // 대성공은 확정권(같은 던전 10회 성공)으로 사실상 자동 달성돼 병목이 아니다(엔드리스 실측 97.6%).
            // 던전만이 유일하게 작동하는 난이도 손잡이라 로테이션 슬롯을 전부 던전에 준다.
            rot.lightRotation++;
            AddDungeon(reqs, w, RotatingDungeonGrade(w, rot), dungeons, rot, rng);
        }

        #endregion

        #region 요구조건 생성 — 엔드리스 템플릿 (101~116)

        // 엔드리스 난이도 등급 — 실측(2026-07-29 엔드리스 스윕) 기반.
        // 아키타입(휴식/표준/시험)은 엔드리스에서 난이도와 무관했다. 실제로 통과율을 가르는 건
        // 아래 "병목" 3종뿐이고, 이 중 몇 개를 어떤 강도로 거느냐가 등급을 만든다.
        //   1) 저등급 던전(일반/고급) 요구 - 엔드리스 의뢰판 등급 배율이 Rare+ 대비 최대 30배 낮다
        //   2) 전설 등급 2회 요구 - 전설 무기는 보통 1자루라 주 2회전이 안 된다 (1회는 병목 아님)
        //   3) 석궁/마도서 요구 - 개체가 5·6종뿐이라 수급이 안 된다 (다른 6타입은 13종)
        // 등급별 4벌씩 16종. 61~64 Easy / 65~68 Normal / 69~72 Hard / 73~76 Extreme.
        // 주차 절대값이 아니라 **엔드리스 인덱스(0~15)** 로 판정한다.
        // 61~76 절대값으로 두었더니 캠페인을 40주로 줄였을 때 전 템플릿이 Easy로 뭉개졌다(2026-07-29).
        private static QuestDifficulty EndlessDifficulty(int w)
        {
            int i = w - ENDLESS_FIRST;
            return i < 4 ? QuestDifficulty.Easy
                 : i < 8 ? QuestDifficulty.Normal
                 : i < 12 ? QuestDifficulty.Hard
                 : QuestDifficulty.Extreme;
        }

        /// <summary>보상 배율은 난이도를 따른다 — 어려운 주는 크게 벌고, 실패하면 크게 잃는다</summary>
        private static float EndlessRewardMult(QuestDifficulty d) => d switch
        {
            QuestDifficulty.Easy => 0.8f,
            QuestDifficulty.Normal => 1f,
            QuestDifficulty.Hard => 1.3f,
            _ => 1.7f,
        };

        // 병목 무기 타입 — 개체 수가 절반 이하라 수급이 병목이 된다 (석궁 5종 / 마도서 6종 vs 나머지 13종).
        // 흔한 무기 타입 판별용 — 무기 요구는 난이도가 아니라 다양성 슬롯이다(아래 DungeonBand 주석 참조).
        private static readonly WeaponType[] ScarceWeapons = { WeaponType.Crossbow, WeaponType.Tome };

        /// <summary>
        /// 엔드리스 난이도를 결정하는 유일한 손잡이 — 던전의 유효 의뢰판 가중치
        /// (`questWeight x 등급 배율`, 엔드리스 배율은 `{0.05, 0.25, 2, 15, 150}`).
        ///
        /// 등급만으로는 통제가 안 된다: 같은 고급 던전도 w=0.5(해안가)는 68%, w=0.25(평범한 동굴)는 42%다.
        ///
        /// **무기 타입 병목은 폐기했다 (2026-07-29 2차 실측).**
        /// 석궁 5종/마도서 6종의 "희소성"에 걸었는데, 석궁 계수 개편(수요 0 -> 12.1%)과
        /// 페르소나 20종 개편으로 무기 타입별 모험가 공급이 고르게 퍼지자 통과율이
        /// 42 -> 78%(석궁 6회), 69 -> 86%(마도서 6회)로 무너졌다.
        /// 무기 수요를 고르게 만드는 것은 올바른 밸런싱 방향이므로 되돌릴 수 없고,
        /// **결함에 기댄 병목은 그 결함이 고쳐지는 순간 사라진다**는 교훈만 남았다.
        /// 던전 가중치는 의도적으로 설계된 희소성이라 같은 기간에 42.0 -> 42.4%로 안정적이었다.
        /// </summary>
        private enum DungeonBand
        {
            Abundant,   // 희귀·영웅: eff 0.5~1.5.   실측 92~98%
            Thin,       // 고급 w=0.5: eff 0.125.    실측 68%
            Scarce,     // 고급 w=0.25 / 일반 w=1: eff 0.05~0.0625. 실측 42%
            VeryScarce, // 일반 w=0.5: eff 0.025.    Extreme 전용
        }

        // 전설 던전은 엔드리스에 쓰지 않는다 — 같은 등급·가중치인데 armorType 하나로
        // 통과율이 35%(깊은 해저신전, MagicalArmor) ~ 97%(휘몰아치는 들판, LightArmor)까지 갈린다.
        // 밴드로 통제가 안 되므로 제외한다. (레벨디자인 §5-1이 시험주에서 내린 결론과 같다)
        private static bool InBand(DungeonInfo d, DungeonBand band) => band switch
        {
            DungeonBand.Abundant   => d.grade == Grade.Rare || d.grade == Grade.Epic,
            DungeonBand.Thin       => d.grade == Grade.Uncommon && d.questWeight >= 0.5f,
            DungeonBand.Scarce     => (d.grade == Grade.Uncommon && d.questWeight < 0.5f)
                                   || (d.grade == Grade.Common && d.questWeight >= 1f),
            _                      => d.grade == Grade.Common && d.questWeight < 1f,
        };

        private static List<QuestRequirement> BuildEndlessRequirements(
            int w, List<DungeonInfo> dungeons, System.Random rng)
        {
            // Diamond 캡 + SC 20 고정 (§8-3).
            // 성공 외 요구는 반드시 2개로 맞춘다 - 실측상 3개짜리는 등급을 벗어나 떨어진다
            // (W67 29.2%, W76 19.2% vs 같은 등급 69%/31%). 병목 하나가 더 붙는 게 아니라
            // 모험가·무기 배정이 세 목표로 쪼개져 동시 달성 확률이 무너지기 때문이다.
            // 레벨디자인 §2-1의 "슬롯 수는 난이도가 아니다"는 파생 목표가 겹칠 때 이야기고,
            // 병목이 걸린 엔드리스에는 적용되지 않는다.
            // 등급 = 던전 밴드로 결정한다. 두 번째 슬롯은 난이도에 영향이 없는 요소
            // (등급 요구·대성공·흔한 무기)로 채워 **다양성만** 준다 - 전부 실측상 병목이 아니다
            // (전설 2회 단독 90.7%, 대성공 1회 97.6%, 흔한 무기 6회 94~97%).
            var reqs = new List<QuestRequirement> { MakeSuccessFixed(20) };
            switch (w - ENDLESS_FIRST)
            {
                // ---- Easy (실측 ~95%): 던전 병목 없음 ----
                case 0: reqs.Add(MakeGrade(Grade.Epic, 5)); AddCommonWeapon(reqs, 6, rng); break;
                case 1: reqs.Add(MakeGrade(Grade.Rare, 7)); AddBandDungeon(reqs, DungeonBand.Abundant, 2, dungeons, rng); break;
                case 2: reqs.Add(MakeGrade(Grade.Epic, 4)); reqs.Add(MakeGreat()); break;
                case 3: reqs.Add(MakeGrade(Grade.Legendary, 2)); AddBandDungeon(reqs, DungeonBand.Abundant, 1, dungeons, rng); break;

                // ---- Normal (실측 ~68%): Thin 던전 ----
                case 4: reqs.Add(MakeGrade(Grade.Epic, 5)); AddBandDungeon(reqs, DungeonBand.Thin, 2, dungeons, rng); break;
                case 5: reqs.Add(MakeGrade(Grade.Rare, 7)); AddBandDungeon(reqs, DungeonBand.Thin, 2, dungeons, rng); break;
                case 6: reqs.Add(MakeGreat()); AddBandDungeon(reqs, DungeonBand.Thin, 2, dungeons, rng); break;
                case 7: reqs.Add(MakeGrade(Grade.Epic, 4)); AddBandDungeon(reqs, DungeonBand.Thin, 1, dungeons, rng); break;

                // ---- Hard (실측 ~42%): Scarce 던전 ----
                case 8: reqs.Add(MakeGrade(Grade.Epic, 5)); AddBandDungeon(reqs, DungeonBand.Scarce, 2, dungeons, rng); break;
                case 9: reqs.Add(MakeGreat()); AddBandDungeon(reqs, DungeonBand.Scarce, 2, dungeons, rng); break;
                case 10: reqs.Add(MakeGrade(Grade.Rare, 7)); AddBandDungeon(reqs, DungeonBand.Scarce, 1, dungeons, rng); break;
                case 11: reqs.Add(MakeGrade(Grade.Epic, 4)); AddBandDungeon(reqs, DungeonBand.Scarce, 2, dungeons, rng); break;

                // ---- Extreme (목표 ~30%): VeryScarce 던전 ----
                // 전설 등급 요구는 저가용 던전과 겹치면 배정이 충돌해 더 떨어진다(38.5% 실측).
                // Extreme에서는 그 겹침을 의도적으로 쓴다.
                case 12: reqs.Add(MakeGrade(Grade.Legendary, 2)); AddBandDungeon(reqs, DungeonBand.VeryScarce, 1, dungeons, rng); break;
                case 13: reqs.Add(MakeGrade(Grade.Epic, 5)); AddBandDungeon(reqs, DungeonBand.VeryScarce, 2, dungeons, rng); break;
                case 14: reqs.Add(MakeGreat()); AddBandDungeon(reqs, DungeonBand.VeryScarce, 2, dungeons, rng); break;
                default: reqs.Add(MakeGrade(Grade.Legendary, 2));                                // idx 15
                         AddBandDungeon(reqs, DungeonBand.VeryScarce, 2, dungeons, rng); break;
            }
            return reqs.OrderBy(SortKey).ToList();
        }

        private static void AddBandDungeon(List<QuestRequirement> reqs, DungeonBand band, int count,
            List<DungeonInfo> dungeons, System.Random rng)
        {
            var pool = dungeons.Where(d => InBand(d, band)).ToList();
            if (pool.Count == 0)
            {
                Debug.LogWarning($"[WeeklyQuestGenerator] 던전 밴드 {band}에 해당하는 던전이 없습니다");
                return;
            }
            var d = pool[rng.Next(pool.Count)];
            reqs.Add(new QuestRequirement
            {
                questType = QuestType.CompleteSpecificDungeon,
                targetCount = count,
                specificDungeonID = d.id,
                requirementText = $"{d.name} 클리어 {count}회",
            });
        }

        /// <summary>흔한 무기 타입(개체 13종)에서 고른다 — 병목이 아닌 무기 요구</summary>
        private static void AddCommonWeapon(List<QuestRequirement> reqs, int n, System.Random rng)
        {
            var pool = ((WeaponType[])Enum.GetValues(typeof(WeaponType)))
                       .Where(t => !ScarceWeapons.Contains(t)).ToArray();
            reqs.Add(MakeWeaponOfType(pool[rng.Next(pool.Length)], n));
        }

        private static QuestRequirement MakeWeaponOfType(WeaponType type, int n) => new QuestRequirement
        {
            questType = QuestType.RentSpecificWeapon,
            targetCount = n,
            specificWeaponType = type,
            requirementText = $"{WeaponKor(type)} 대여로 성공 {n}회",
        };

        #endregion

        #region 개별 요구 생성 + 캡

        private static int SuccessCount(int w) =>
            Mathf.Min(Mathf.RoundToInt(SUCCESS_BASE + w * SUCCESS_PER_WEEK), SUCCESS_CAP);

        private static QuestRequirement MakeSuccess(int w) => MakeSuccessFixed(SuccessCount(w));

        private static QuestRequirement MakeSuccessFixed(int n) => new QuestRequirement
        {
            questType = QuestType.SuccessfulAdventures,
            targetCount = n,
            requirementText = $"모험 성공 {n}회",
        };

        /// <summary>등급 요구 캡: MIN(평판 캡 §3-1, SC(w)) — 하위 목표 클램프 §2-1 포함</summary>
        private static int ClampGrade(int w, Grade g, int n) =>
            Mathf.Clamp(n, 1, Mathf.Min(GradeCapByRep[ExpectedRepLevel(w)][(int)g], SuccessCount(w)));

        private static void AddGrade(List<QuestRequirement> reqs, int w, Grade g, int n)
        {
            if (GradeIntroWeek[(int)g] > w) return;                      // 도입 전 출제 금지
            if (GradeCapByRep[ExpectedRepLevel(w)][(int)g] <= 0) return; // 평판 게이트
            if (reqs.Any(r => r.questType == QuestType.RentSpecificGrade)) return;   // 주당 등급 요구 1개
            reqs.Add(MakeGrade(g, ClampGrade(w, g, n)));
        }

        private static QuestRequirement MakeGrade(Grade g, int n) => new QuestRequirement
        {
            questType = QuestType.RentSpecificGrade,
            targetCount = n,
            minGrade = g,
            requirementText = $"{GradeKor(g)} 이상 무기로 성공 {n}회",
        };

        private static void AddGreat(List<QuestRequirement> reqs, int w)
        {
            if (w < GREAT_INTRO_WEEK) return;
            if (reqs.Any(r => r.questType == QuestType.GreatSuccessCount)) return;
            reqs.Add(MakeGreat());
        }

        // 대성공은 항상 1회 — 계수 문서 §3-3 캡 (현행 1 + w/40 폐기)
        private static QuestRequirement MakeGreat() => new QuestRequirement
        {
            questType = QuestType.GreatSuccessCount,
            targetCount = 1,
            requirementText = "대성공 1회",
        };

        private static void AddWeapon(List<QuestRequirement> reqs, int w, int n, RotationState rot, System.Random rng)
        {
            if (w < WEAPON_INTRO_WEEK) return;
            if (reqs.Any(r => r.questType == QuestType.RentSpecificWeapon)) return;
            n = Mathf.Clamp(n, 1, Mathf.Min(WEAPON_CAP, SuccessCount(w)));   // 하위 목표 <= SC §2-1
            var req = MakeWeapon(w, n, rng, rot);
            reqs.Add(req);
        }

        private static QuestRequirement MakeWeapon(int w, int n, System.Random rng, RotationState rot)
        {
            var type = (WeaponType)rng.Next(0, Enum.GetValues(typeof(WeaponType)).Length);
            // 같은 무기 타입 2주 연속 금지 (§6 규칙 5)
            if (rot != null && rot.lastWeaponType.HasValue && type == rot.lastWeaponType.Value)
                type = (WeaponType)(((int)type + 1 + rng.Next(7)) % 8);
            if (rot != null) rot.lastWeaponType = type;

            return new QuestRequirement
            {
                questType = QuestType.RentSpecificWeapon,
                targetCount = Mathf.Min(n, Mathf.Min(WEAPON_CAP, SuccessCount(w))),
                specificWeaponType = type,
                requirementText = $"{WeaponKor(type)} 대여로 성공 {Mathf.Min(n, Mathf.Min(WEAPON_CAP, SuccessCount(w)))}회",
            };
        }

        private static void AddDungeon(List<QuestRequirement> reqs, int w, Grade g,
            Dictionary<Grade, List<DungeonInfo>> dungeons, RotationState rot, System.Random rng)
        {
            if (DungeonIntroWeek[(int)g] > w) return;
            int cap = DungeonCapByRep[ExpectedRepLevel(w)][(int)g];
            if (cap <= 0) return;
            if (reqs.Any(r => r.questType == QuestType.CompleteSpecificDungeon)) return;

            int count = w >= DUNGEON_DOUBLE_WEEK ? Mathf.Min(2, cap) : 1;   // 던전 2회 스텝 §4
            // T3부터 희소 개체로 좁힌다 — 그 구간 의뢰판 배율이 상급자에게 유리해져
            // 등급만으로는 병목이 안 걸린다(MakeDungeon의 scarceOnly 주석 참조).
            var req = MakeDungeon(g, count, dungeons, rng, rot, scarceOnly: w > TierMaxWeek[2]);
            if (req != null) reqs.Add(req);
        }

        /// <param name="scarceOnly">
        /// 같은 등급 안에서 questWeight가 최저인 개체만 후보로 둔다. 캠페인 후반(T3~T4)에 쓴다 -
        /// 그 구간 의뢰판 배율이 엔드리스와 같아(`{0.05, 0.25, 2, 15, 150}`), 상급자는 새로고침으로
        /// 웬만한 던전 병목을 뚫는다. 엔드리스 실측상 상급자에게 통한 건 유효 가중치 최저 밴드뿐이다
        /// (Thin 94% / Scarce 76% / VeryScarce 47%).
        /// </param>
        private static QuestRequirement MakeDungeon(Grade g, int count,
            Dictionary<Grade, List<DungeonInfo>> dungeons, System.Random rng, RotationState rot,
            bool scarceOnly = false)
        {
            if (!dungeons.TryGetValue(g, out var pool) || pool.Count == 0) return null;
            if (scarceOnly)
            {
                float min = pool.Min(x => x.questWeight);
                var scarce = pool.Where(x => Mathf.Approximately(x.questWeight, min)).ToList();
                if (scarce.Count > 0) pool = scarce;
            }
            var d = pool[rng.Next(pool.Count)];
            // 같은 던전 2주 연속 금지 (§6 규칙 5)
            if (rot != null && pool.Count > 1 && d.id == rot.lastDungeonID)
                d = pool[(pool.IndexOf(d) + 1 + rng.Next(pool.Count - 1)) % pool.Count];
            if (rot != null) rot.lastDungeonID = d.id;

            return new QuestRequirement
            {
                questType = QuestType.CompleteSpecificDungeon,
                targetCount = count,
                specificDungeonID = d.id,
                requirementText = $"{d.name} 클리어 {count}회",
            };
        }

        #endregion

        #region 부하/해금 헬퍼

        private static int TierOf(int w)
        {
            for (int t = 0; t < TierMaxWeek.Length; t++)
                if (w <= TierMaxWeek[t]) return t;
            return TierMaxWeek.Length - 1;
        }

        /// <summary>주차별 부하 앵커 — 티어 구간 선형 보간 §3-1</summary>
        private static float AnchorAt(int w)
        {
            int tier = TierOf(w);
            float start = tier == 0 ? T0_START_ANCHOR : ExamAnchor[tier - 1];
            if (tier == 4) return ExamAnchor[4];   // T4는 32 고정
            int tierStart = tier == 0 ? 1 : TierMaxWeek[tier - 1] + 1;
            float t = Mathf.InverseLerp(tierStart, TierMaxWeek[tier], w);
            return Mathf.Lerp(start, ExamAnchor[tier], t);
        }

        private static Grade HighestUnlockedGrade(int w)
        {
            for (int g = 4; g >= 1; g--)
                if (GradeIntroWeek[g] <= w && GradeCapByRep[ExpectedRepLevel(w)][g] > 0)
                    return (Grade)g;
            return Grade.Common;   // 미해금
        }

        private static Grade LowerUnlockedGrade(int w, Grade below)
        {
            for (int g = (int)below - 1; g >= 1; g--)
                if (GradeIntroWeek[g] <= w && GradeCapByRep[ExpectedRepLevel(w)][g] > 0)
                    return (Grade)g;
            return Grade.Common;
        }

        private static Grade LowestUnlockedDungeonGrade(int w) =>
            DungeonIntroWeek[0] <= w ? Grade.Common : Grade.Common;

        /// <summary>던전 등급 로테이션 — 해금된 범위에서 높은/낮은 등급 교대</summary>
        private static Grade RotatingDungeonGrade(int w, RotationState rot)
        {
            var unlocked = new List<Grade>();
            for (int g = 0; g <= 4; g++)
                if (DungeonIntroWeek[g] <= w && DungeonCapByRep[ExpectedRepLevel(w)][g] > 0)
                    unlocked.Add((Grade)g);
            if (unlocked.Count == 0) return Grade.Common;
            return unlocked[rot.lightRotation % unlocked.Count];
        }

        #endregion

        #region 검증 §10 (생성 시 자동 체크)

        private static void Validate(int w, Arch arch, List<QuestRequirement> reqs, List<string> warnings)
        {
            int sc = SuccessCount(w);
            foreach (var r in reqs)
            {
                if (r.questType == QuestType.SuccessfulAdventures && r.targetCount != sc)
                    warnings.Add($"{w}주차: 성공 목표 {r.targetCount} != SC {sc}");
                if (r.questType != QuestType.SuccessfulAdventures && r.targetCount > sc)
                    warnings.Add($"{w}주차: 하위 목표({r.questType} {r.targetCount})가 SC {sc} 초과");
                if (r.questType == QuestType.GreatSuccessCount && (r.targetCount != 1 || w < GREAT_INTRO_WEEK))
                    warnings.Add($"{w}주차: 대성공 규칙 위반 (count {r.targetCount})");
                if (r.questType == QuestType.RentSpecificGrade)
                {
                    int cap = GradeCapByRep[ExpectedRepLevel(w)][(int)r.minGrade];
                    if (r.targetCount > cap || GradeIntroWeek[(int)r.minGrade] > w)
                        warnings.Add($"{w}주차: 등급 요구 캡/도입 위반 ({GradeKor(r.minGrade)} {r.targetCount}/{cap})");
                }
            }

            // 교차(등급+무기 동시)는 15주 시험 / 20주차 이후 소피크·변주·시험만 (§6 규칙 6)
            bool cross = reqs.Any(r => r.questType == QuestType.RentSpecificGrade)
                      && reqs.Any(r => r.questType == QuestType.RentSpecificWeapon);
            bool crossAllowed = w == 10 || (w >= 17 && (arch == Arch.SubPeak || arch == Arch.Variation || arch == Arch.Exam));
            if (cross && !crossAllowed)
                warnings.Add($"{w}주차({arch}): 허용되지 않은 교차 조합");
        }

        #endregion

        #region 퀘스트 필드 채우기

        private static void PopulateQuest(WeeklyQuestData data, int w, Arch arch, QuestDifficulty diff,
                                          string title, List<QuestRequirement> reqs, bool endless)
        {
            data.StaticID = $"QUEST_W{w:D2}";
            data.questTitle = title;
            data.weekNumber = w;
            data.requirements = reqs;
            data.difficulty = diff;
            data.description = (endless ? "이번 주 의뢰 목표 (반복 의뢰)\n" : "이번 주 의뢰 목표\n") +
                               string.Join("\n", reqs.Select(r => "- " + r.requirementText));

            // 엔드리스 템플릿의 보상/벌금은 캠페인 마지막 주차 기준으로 굽는다.
            // 벌금은 런타임(QuestManager.CalculateWeeklyFine)이 실제 주차 곡선으로 재계산해 덮어쓴다 (§8-4).
            int rw = endless ? CAMPAIGN_WEEKS : w;
            float mult = endless ? EndlessRewardMult(diff) : RewardMult(arch);
            float reqFactor = 1f + REQ_REWARD_STEP * (reqs.Count - 1);
            data.goldReward = Mathf.RoundToInt((GOLD_BASE + rw * GOLD_PER_WEEK) * reqFactor * mult);
            data.reputationReward = Mathf.Min(Mathf.RoundToInt((REP_BASE + rw * REP_PER_WEEK) * mult), REP_CAP);
            data.insightReward = 1;
            data.weeklyFine = FINE_BASE + Mathf.RoundToInt(FINE_QUAD * rw * rw);   // 벌금은 배율 미적용 §7
            data.reputationPenalty = Mathf.Min(PENALTY_BASE + rw * PENALTY_PER_WEEK, PENALTY_CAP);
        }

        private static int SortKey(QuestRequirement r) => r.questType switch
        {
            QuestType.SuccessfulAdventures => 0,
            QuestType.RentSpecificGrade => 1 + (int)r.minGrade,
            QuestType.GreatSuccessCount => 10,
            QuestType.RentSpecificWeapon => 11,
            QuestType.CompleteSpecificDungeon => 12,
            _ => 99,
        };

        #endregion

        #region 에셋 수집

        /// <summary>엔드리스 밴드 판정에 필요한 던전 정보 (등급 + 의뢰판 가중치)</summary>
        private class DungeonInfo
        {
            public string id;
            public string name;
            public Grade grade;
            public float questWeight;
        }

        /// <summary>엔드리스 전용 던전 수집 — 밴드 판정에 questWeight가 필요하다. 캠페인 경로는 건드리지 않는다.</summary>
        private static List<DungeonInfo> CollectDungeonInfos()
        {
            var list = new List<DungeonInfo>();
            foreach (var guid in AssetDatabase.FindAssets("t:DungeonData"))
            {
                var d = AssetDatabase.LoadAssetAtPath<DungeonData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d == null || string.IsNullOrEmpty(d.StaticID)) continue;
                list.Add(new DungeonInfo
                {
                    id = d.StaticID,
                    name = string.IsNullOrEmpty(d.dungeonName) ? d.StaticID : d.dungeonName,
                    grade = d.grade,
                    questWeight = d.questWeight,
                });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));   // 결정론적 순서
            return list;
        }

        private static Dictionary<Grade, List<DungeonInfo>> CollectDungeonsByGrade()
        {
            var result = new Dictionary<Grade, List<DungeonInfo>>();
            foreach (var d in CollectDungeonInfos())
            {
                if (!result.TryGetValue(d.grade, out var list))
                {
                    list = new List<DungeonInfo>();
                    result[d.grade] = list;
                }
                list.Add(d);
            }
            return result;   // CollectDungeonInfos가 이미 StaticID로 정렬 (결정론적 순서)
        }

        private static Dictionary<int, WeeklyQuestData> CollectExistingQuests()
        {
            var map = new Dictionary<int, WeeklyQuestData>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeeklyQuestData", new[] { FOLDER }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var q = AssetDatabase.LoadAssetAtPath<WeeklyQuestData>(path);
                if (q == null) continue;
                if (q.weekNumber < 1 || q.weekNumber > ENDLESS_LAST) continue;   // 범위 밖(테스트 등)은 건드리지 않음
                if (!map.ContainsKey(q.weekNumber)) map[q.weekNumber] = q;
            }
            return map;
        }

        #endregion
    }
}
#endif
