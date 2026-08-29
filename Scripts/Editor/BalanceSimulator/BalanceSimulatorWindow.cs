#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 밸런스 시뮬레이터 실행 창.
    /// 페르소나 봇으로 회차를 다회 시뮬레이션해 평판 곡선·주간 성공 수·퀘스트 통과율을 산출한다.
    /// 설계: Documents/balance/reference/시뮬레이터_설계.md
    /// </summary>
    public class BalanceSimulatorWindow : EditorWindow
    {
        private const string OUTPUT_FOLDER = "Documents/Simulation";

        private int seeds = 100;
        private int weeks = 100;
        private bool runElite = true;
        private bool runMid = true;
        private bool runNovice = true;
        private bool eliteUsesTalkHint = true;
        private int eliteTalkInsightGate = 50;
        private bool forceMorningEvents = false;
        private int runsPerSeed = 5;
        private int goldPerLegacyOverride = 0;
        private bool useTraits = true;
        private bool useSeer = true;
        private bool useNamed = true;
        private bool preferNamed = true;
        private bool useCraft = true;

        private string lastResultPath;

        [MenuItem("Tools/Today's Weapon Rental/Balance Simulator")]
        public static void Open()
        {
            var win = GetWindow<BalanceSimulatorWindow>("Balance Simulator");
            win.minSize = new Vector2(340, 260);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("실행 설정", EditorStyles.boldLabel);
            seeds = EditorGUILayout.IntSlider("시드 수", seeds, 1, 500);
            weeks = EditorGUILayout.IntSlider("시뮬 주차", weeks, 1, 120);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("페르소나", EditorStyles.boldLabel);
            runElite = EditorGUILayout.ToggleLeft("상급자 (민맥스)", runElite);
            runMid = EditorGUILayout.ToggleLeft("중급자 (감각파)", runMid);
            runNovice = EditorGUILayout.ToggleLeft("초급자 (막무가내)", runNovice);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("상급자 A/B 옵션", EditorStyles.boldLabel);
            eliteUsesTalkHint = EditorGUILayout.ToggleLeft("무기 힌트 대화 사용", eliteUsesTalkHint);
            using (new EditorGUI.DisabledScope(!eliteUsesTalkHint))
                eliteTalkInsightGate = EditorGUILayout.IntSlider("힌트 사용 통찰 게이트", eliteTalkInsightGate, 0, 100);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("아침 이벤트", EditorStyles.boldLabel);
            forceMorningEvents = EditorGUILayout.ToggleLeft(
                new GUIContent("강제 참여 (진단용)",
                    "등장 확률을 무시하고 매일 스폰 + 전 페르소나가 유보금(주간 벌금) 한도 안에서 무조건 참여." +
                    " '이득일 때만' 정책의 지출 0이 봇 결함인지 콘텐츠 사망인지 구분하는 A/B용."),
                forceMorningEvents);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("유산 회차 반복", EditorStyles.boldLabel);
            runsPerSeed = EditorGUILayout.IntSlider(
                new GUIContent("시드당 최대 회차", "폐업 시에만 유산 획득 후 다음 회차. 생존 완주하면 조기 종료. 1 = 기존 단일 회차."),
                runsPerSeed, 1, 10);
            goldPerLegacyOverride = EditorGUILayout.IntField(
                new GUIContent("goldPerLegacy 오버라이드", "0 = LegacyConfig.asset 값 사용. 환전율 A/B 비교용."),
                goldPerLegacyOverride);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("특성 / 점술", EditorStyles.boldLabel);
            useTraits = EditorGUILayout.ToggleLeft(
                new GUIContent("특성 16종 반영",
                    "방문자마다 균등 롤 + GetTrait* 전 지점 미러. 끄면 특성 반영 이전 결과와 완전히 동일(무결성 검증 A/B)."),
                useTraits);
            useSeer = EditorGUILayout.ToggleLeft(
                new GUIContent("점술 반영",
                    "LUK 구간 가중 추첨으로 운세를 뽑아 매 전투 판정에 가산. 끄면 점술 반영 이전 결과와 완전히 동일(무결성 검증 A/B)."),
                useSeer);
            useNamed = EditorGUILayout.ToggleLeft(
                new GUIContent("네임드 + 호감도 반영",
                    "네임드 영속 인스턴스(가중치 1:5) + 재방문 + 호감도 누적/성공률 보너스. 끄면 반영 이전과 동일."),
                useNamed);
            using (new EditorGUI.DisabledScope(!useNamed))
                preferNamed = EditorGUILayout.ToggleLeft(
                    new GUIContent("  ㄴ 네임드 우대 정책",
                        "켜면 봇이 네임드에게 최적 무기·큰 던전·낮은 점술 문턱을 적용. 끄면 named-blind(일반과 동일 취급)."),
                    preferNamed);
            useCraft = EditorGUILayout.ToggleLeft(
                new GUIContent("무기 제작 + 재부여 반영",
                    "레시피 제작(해금일·재료·슬롯)과 재부여(골드만, 잠금 배율). 끄면 반영 이전과 동일."),
                useCraft);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("시뮬레이션 실행", GUILayout.Height(32)))
                Run();

            if (!string.IsNullOrEmpty(lastResultPath))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox($"마지막 결과: {lastResultPath}", MessageType.Info);
                if (GUILayout.Button("결과 폴더 열기"))
                    EditorUtility.RevealInFinder(lastResultPath);
            }
        }

        private void Run()
        {
            var opt = new SimOptions
            {
                seeds = seeds,
                weeks = weeks,
                eliteUsesTalkHint = eliteUsesTalkHint,
                eliteTalkInsightGate = eliteTalkInsightGate,
                forceMorningEvents = forceMorningEvents,
                runsPerSeed = runsPerSeed,
                goldPerLegacyOverride = goldPerLegacyOverride,
                useTraits = useTraits,
                useSeer = useSeer,
                useNamed = useNamed,
                preferNamed = preferNamed,
                useCraft = useCraft,
            };

            var personas = new List<SimPersona>();
            if (runElite) personas.Add(new ElitePersona());
            if (runMid) personas.Add(new MidPersona());
            if (runNovice) personas.Add(new NovicePersona());
            if (personas.Count == 0)
            {
                EditorUtility.DisplayDialog("Balance Simulator", "페르소나를 1개 이상 선택하세요.", "확인");
                return;
            }

            string mdPath = Execute(opt, personas, out string error);
            if (error != null)
            {
                EditorUtility.DisplayDialog("Balance Simulator", error, "확인");
                return;
            }
            if (mdPath != null) lastResultPath = mdPath;
        }

        /// <summary>
        /// 배치 실행 진입점. Unity를 -batchmode -executeMethod TodaysWeaponRental.BalanceSimulatorWindow.RunBatch 로 띄운다.
        /// 인자: -simSeeds N, -simWeeks N (생략 시 100/100), -simForceMorning (아침 이벤트 강제 참여 진단 런),
        /// -simRuns N (시드당 최대 회차, 기본 5), -simGoldPerLegacy N (환전율 오버라이드, 0=에셋값),
        /// -simTraits N (1=특성 반영(기본), 0=미반영 — 무결성 검증 A/B),
        /// -simSeer N (1=점술 반영(기본), 0=미반영),
        /// -simNamed N (1=네임드+호감도 반영(기본), 0=미반영), -simPreferNamed N (1=우대 정책(기본), 0=named-blind),
        /// -simCraft N (1=무기 제작·재부여 반영(기본), 0=미반영), -simForceReroll (재부여 강제 시도 진단 런),
        /// -simUpgradeSweep (유산 단독 효과 스윕 — 기준선+20종을 1회차씩 돌려 비교표만 낸다. 다른 리포트는 안 쓴다),
        /// -simEndlessSweep (엔드리스 템플릿 난이도 스윕 — 기준선+16종의 61주 이후 통과율/생존을 비교표로 낸다).
        /// 결과는 Documents/Simulation/에 쓴다.
        /// </summary>
        public static void RunBatch()
        {
            var opt = new SimOptions
            {
                seeds = ArgInt("-simSeeds", 100),
                weeks = ArgInt("-simWeeks", 100),
                eliteUsesTalkHint = true,
                eliteTalkInsightGate = 50,
                forceMorningEvents = HasArg("-simForceMorning"),
                runsPerSeed = ArgInt("-simRuns", 5),
                goldPerLegacyOverride = ArgInt("-simGoldPerLegacy", 0),
                useTraits = ArgInt("-simTraits", 1) != 0,
                useSeer = ArgInt("-simSeer", 1) != 0,
                useNamed = ArgInt("-simNamed", 1) != 0,
                preferNamed = ArgInt("-simPreferNamed", 1) != 0,
                useCraft = ArgInt("-simCraft", 1) != 0,
                forceReroll = HasArg("-simForceReroll"),
            };

            var personas = new List<SimPersona> { new ElitePersona(), new MidPersona(), new NovicePersona() };
            Debug.Log($"[BalanceSimulator] 배치 실행 — 시드 {opt.seeds}, 주차 {opt.weeks}, 회차 {opt.runsPerSeed}" +
                      (opt.forceMorningEvents ? ", 아침 이벤트 강제 참여" : "") +
                      (opt.goldPerLegacyOverride > 0 ? $", goldPerLegacy={opt.goldPerLegacyOverride}" : "") +
                      (opt.useTraits ? "" : ", 특성 미반영") +
                      (opt.useSeer ? "" : ", 점술 미반영") +
                      (opt.useNamed ? (opt.preferNamed ? ", 네임드 우대" : ", 네임드 blind") : ", 네임드 미반영") +
                      (opt.useCraft ? "" : ", 제작·재부여 미반영") +
                      (opt.forceReroll ? ", 재부여 강제" : ""));

            if (HasArg("-simUpgradeSweep"))
            {
                string sweepPath = ExecuteSweep(opt, personas, out string sweepError);
                if (sweepError != null) Debug.LogError($"[BalanceSimulator] {sweepError}");
                else Debug.Log($"[BalanceSimulator] 스윕 배치 완료 — {sweepPath}");
                return;
            }

            if (HasArg("-simEndlessSweep"))
            {
                string sweepPath = ExecuteEndlessSweep(opt, personas, out string sweepError);
                if (sweepError != null) Debug.LogError($"[BalanceSimulator] {sweepError}");
                else Debug.Log($"[BalanceSimulator] 엔드리스 스윕 배치 완료 — {sweepPath}");
                return;
            }

            string mdPath = Execute(opt, personas, out string error);
            if (error != null)
                Debug.LogError($"[BalanceSimulator] {error}");
            else
                Debug.Log($"[BalanceSimulator] 배치 완료 — {mdPath}");
        }

        private static int ArgInt(string name, int fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name && int.TryParse(args[i + 1], out int v)) return v;
            return fallback;
        }

        private static bool HasArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name) return true;
            return false;
        }

        /// <summary>시뮬레이션 본체. 실패하면 error에 사유를 담고 null을 반환한다.</summary>
        private static string Execute(SimOptions opt, List<SimPersona> personas, out string error)
        {
            error = null;

            var bundle = SimBundle.Load();
            if (!bundle.IsValid(out error)) return null;
            if (bundle.quests.Count == 0)
                Debug.LogWarning("[BalanceSimulator] WeeklyQuestData가 없습니다. 퀘스트/벌금 없이 시뮬레이션합니다.");

            var results = RunAll(bundle, opt, personas, "", out bool canceled);

            if (canceled || results.Count == 0)
            {
                Debug.LogWarning("[BalanceSimulator] 실행 취소됨");
                return null;
            }

            var (csvPath, mdPath) = SimReport.Write(results, opt, OUTPUT_FOLDER);
            Debug.Log($"[BalanceSimulator] 완료 — 런 {results.Count}개\nCSV: {csvPath}\n요약: {mdPath}");
            return mdPath;
        }

        /// <summary>
        /// 페르소나 x 시드 x 회차 루프 본체. 취소되면 canceled=true로 돌려준다.
        /// progressLabel이 비어있지 않으면 진행바 제목에 덧붙인다 (스윕 진행 표시용).
        /// </summary>
        private static List<RunResult> RunAll(SimBundle bundle, SimOptions opt, List<SimPersona> personas,
                                              string progressLabel, out bool canceled)
        {
            bool showProgress = !Application.isBatchMode;
            var results = new List<RunResult>();
            int total = personas.Count * opt.seeds;
            int done = 0;
            canceled = false;

            try
            {
                foreach (var p in personas)
                {
                    for (int seed = 1; seed <= opt.seeds; seed++)
                    {
                        if (showProgress && EditorUtility.DisplayCancelableProgressBar(
                                "Balance Simulator", $"{progressLabel}{p.Name} — 시드 {seed}/{opt.seeds}", (float)done / total))
                        {
                            canceled = true;
                            break;
                        }

                        // 유산 회차 반복 — 폐업 시에만 유산 획득 + 구매 후 다음 회차, 생존 완주 시 조기 종료
                        var career  = new SimCareer();
                        career.GrantMaxLevel(bundle, opt.soloUpgrade);   // 스윕 전용 — None이면 무동작
                        var shopRng = new System.Random(seed * 613 + p.Name[0]);
                        for (int run = 1; run <= opt.runsPerSeed; run++)
                        {
                            if (run > 1) p.OnLegacyShop(bundle, opt, career, shopRng);
                            var r = new SimWorld().Run(bundle, opt, p, seed, career, run);
                            r.upgradesOwned = career.TotalLevels;
                            results.Add(r);
                            if (r.bankruptWeek == 0) break;
                            career.legacyPoints += r.earnedLegacy;
                        }
                        done++;
                    }
                    if (canceled) break;
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }

            return results;
        }

        /// <summary>
        /// 유산 단독 효과 스윕 (단계 6, 로드맵 1-4). 기준선(업그레이드 0) + 미러된 업그레이드 20종을
        /// 각각 "그 하나만 만렙"으로 1회차씩 돌려, 같은 시드끼리 짝지은 델타를 비교표로 낸다.
        /// RegularAdventurer(단골 확정 방문)는 단골 시스템 미모델링이라 제외한다.
        /// </summary>
        private static readonly UpgradeKey[] SweepKeys =
        {
            UpgradeKey.StartingGold, UpgradeKey.WeaponRare, UpgradeKey.WeaponEpic,
            UpgradeKey.StartingInsight, UpgradeKey.InventorySlots,
            UpgradeKey.EnforceRate, UpgradeKey.EvolveRate, UpgradeKey.EnforceCost, UpgradeKey.EvolveCost,
            UpgradeKey.MaterialReduction, UpgradeKey.RerollCount, UpgradeKey.RerollCost,
            UpgradeKey.DisassembleBonus,
            UpgradeKey.CommissionRate, UpgradeKey.TipRate, UpgradeKey.GreatSuccessRate, UpgradeKey.AdventureSpeed,
            UpgradeKey.MorningEventGuarantee, UpgradeKey.NamedSpawnWeight, UpgradeKey.ShopRefresh,
        };

        private static string ExecuteSweep(SimOptions opt, List<SimPersona> personas, out string error)
        {
            error = null;

            var bundle = SimBundle.Load();
            if (!bundle.IsValid(out error)) return null;

            opt.runsPerSeed = 1;   // 단독 효과 측정 — 회차 반복/구매 정책이 섞이면 귀속이 불가능하다
            var arms = new List<SweepArm>();

            for (int i = 0; i <= SweepKeys.Length; i++)
            {
                var key = i == 0 ? UpgradeKey.None : SweepKeys[i - 1];
                opt.soloUpgrade = key;

                int cost = new SimCareer().GrantMaxLevel(bundle, key);
                Debug.Log($"[BalanceSimulator] 스윕 {i + 1}/{SweepKeys.Length + 1} — {(key == UpgradeKey.None ? "기준선" : key.ToString())} ({cost}p)");

                var res = RunAll(bundle, opt, personas, $"[{i + 1}/{SweepKeys.Length + 1}] {key} ", out bool canceled);
                if (canceled || res.Count == 0)
                {
                    Debug.LogWarning("[BalanceSimulator] 스윕 취소됨");
                    return null;
                }
                arms.Add(new SweepArm { key = key, totalCost = cost, results = res });
            }

            string path = SimReport.WriteUpgradeSweep(arms, opt, OUTPUT_FOLDER);
            Debug.Log($"[BalanceSimulator] 스윕 완료 — 구성 {arms.Count}개\n요약: {path}");
            return path;
        }

        /// <summary>
        /// 엔드리스 템플릿 난이도 스윕. 기준선(현행 순차 재생) + 캠페인 이후 템플릿 각각을
        /// 61주차부터 단독 반복 출제해 통과율/생존을 같은 축에서 잰다.
        /// 캠페인 구간은 전 구성이 동일하므로 차이는 그 템플릿에만 귀속된다.
        /// </summary>
        private static string ExecuteEndlessSweep(SimOptions opt, List<SimPersona> personas, out string error)
        {
            error = null;

            var bundle = SimBundle.Load();
            if (!bundle.IsValid(out error)) return null;

            var templates = bundle.quests.Where(q => q.weekNumber > bundle.CampaignWeeks)
                                         .Select(q => q.weekNumber).OrderBy(w => w).ToList();
            if (templates.Count == 0)
            {
                error = $"엔드리스 템플릿이 없습니다 (weekNumber > {bundle.CampaignWeeks}).";
                return null;
            }

            var arms = new List<EndlessArm>();
            for (int i = 0; i <= templates.Count; i++)
            {
                int fixedWeek = i == 0 ? 0 : templates[i - 1];
                opt.endlessFixedWeek = fixedWeek;

                string label = fixedWeek == 0
                    ? "기준선(난이도 추첨)"
                    : $"W{fixedWeek} [{bundle.QuestForWeek(fixedWeek).difficulty}] {bundle.QuestForWeek(fixedWeek).questTitle}";
                Debug.Log($"[BalanceSimulator] 엔드리스 스윕 {i + 1}/{templates.Count + 1} — {label}");

                var res = RunAll(bundle, opt, personas, $"[{i + 1}/{templates.Count + 1}] {label} ", out bool canceled);
                if (canceled || res.Count == 0)
                {
                    Debug.LogWarning("[BalanceSimulator] 엔드리스 스윕 취소됨");
                    return null;
                }
                arms.Add(new EndlessArm { fixedWeek = fixedWeek, label = label, results = res });
            }

            opt.endlessFixedWeek = 0;
            string path = SimReport.WriteEndlessSweep(arms, opt, bundle.CampaignWeeks, OUTPUT_FOLDER);
            Debug.Log($"[BalanceSimulator] 엔드리스 스윕 완료 — 구성 {arms.Count}개\n요약: {path}");
            return path;
        }
    }
}
#endif
