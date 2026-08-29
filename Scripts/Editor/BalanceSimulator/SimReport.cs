#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 시뮬 결과 집계 — 주차별 원본 CSV + 요약 마크다운.
    /// 산출 지표 정의: Documents/balance/reference/시뮬레이터_설계.md §6
    /// </summary>
    public static class SimReport
    {
        // 40주 캠페인 기준 — 티어 경계(시험주) 4/10/20/30/40 + 엔드리스 관찰점
        private static readonly int[] CheckpointWeeks = { 4, 10, 14, 20, 27, 30, 40, 50, 60 };
        private static readonly int[] ExamWeeks = { 4, 10, 20, 30, 40 };

        public static (string csvPath, string mdPath) Write(List<RunResult> results, SimOptions opt, string folder)
        {
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string csvPath = Path.Combine(folder, $"sim_raw_{stamp}.csv");
            string mdPath = Path.Combine(folder, $"sim_summary_{stamp}.md");

            File.WriteAllText(csvPath, BuildCsv(results), new UTF8Encoding(true));
            File.WriteAllText(mdPath, BuildSummary(results, opt), new UTF8Encoding(false));
            return (csvPath, mdPath);
        }

        #region CSV

        private static string BuildCsv(List<RunResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("persona,seed,run,week,attempts,successes,greats,deaths,visitors,served,left,defaultRuns," +
                          "income,spent,goldEnd,repEnd,repLevel,insightEnd,weapons,questPassed,finePaid," +
                          "spentWeapon,spentSmith,spentMaterial,spentScout,spentRefresh,scoutCount,talkCount," +
                          "spentMorning,incomeMorning,morningEvents,boxBuys,spentMorningBox," +
                          "spentItemCraft,spentCraftMat,craftCount,itemUseCount,seerCount,spentSeer," +
                          "spentWeaponCraft,weaponCraftCount,spentReroll,rerollCount");
            foreach (var r in results)
                foreach (var s in r.weekly)
                    sb.AppendLine($"{r.persona},{r.seed},{r.careerRun},{s.week},{s.attempts},{s.successes},{s.greats},{s.deaths}," +
                                  $"{s.visitors},{s.served},{s.left},{s.defaultRuns},{s.income},{s.spent},{s.goldEnd}," +
                                  $"{s.repEnd},{s.repLevel},{s.insightEnd},{s.weaponsOwned}," +
                                  $"{(s.questPassed ? 1 : 0)},{s.finePaid}," +
                                  $"{s.spentWeapon},{s.spentSmith},{s.spentMaterial},{s.spentScout},{s.spentRefresh}," +
                                  $"{s.scoutCount},{s.talkCount}," +
                                  $"{s.spentMorning},{s.incomeMorning},{s.morningEventCount},{s.boxBuyCount},{s.spentMorningBox}," +
                                  $"{s.spentItemCraft},{s.spentCraftMat},{s.craftCount},{s.itemUseCount}," +
                                  $"{s.seerCount},{s.spentSeer}," +
                                  $"{s.spentWeaponCraft},{s.weaponCraftCount},{s.spentReroll},{s.rerollCount}");
            return sb.ToString();
        }

        #endregion

        #region 유산 단독 효과 스윕 (단계 6, 로드맵 1-4)

        /// <summary>
        /// 유산 업그레이드 단독 효과 스윕 리포트. arms[0]은 반드시 기준선(UpgradeKey.None)이다.
        /// 모든 델타는 **같은 시드끼리 짝지은 차이의 중앙값**이다 — 독립 중앙값 비교는 시드 경로
        /// 발산만으로 순수입이 10% 흔들려(2026-07-28 상자 가중치 건) 업그레이드 효과가 묻힌다.
        /// </summary>
        public static string WriteUpgradeSweep(List<SweepArm> arms, SimOptions opt, string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"sim_upgrade_sweep_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(path, BuildUpgradeSweep(arms, opt), new UTF8Encoding(false));
            return path;
        }

        private static string BuildUpgradeSweep(List<SweepArm> arms, SimOptions opt)
        {
            var sb = new StringBuilder();
            var baseArm = arms[0];
            var personas = baseArm.results.Select(r => r.persona).Distinct().ToList();

            sb.AppendLine("# 유산 업그레이드 단독 효과 스윕");
            sb.AppendLine();
            sb.AppendLine($"> 생성: {DateTime.Now:yyyy-MM-dd HH:mm} / 시드 {opt.seeds}개 x 페르소나 {personas.Count}종 / 기간 {opt.weeks}주 / **1회차 고정** / 구성 {arms.Count}개(기준선 + {arms.Count - 1}종)");
            sb.AppendLine();
            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            sb.AppendLine("- 각 구성은 **그 업그레이드 하나만 만렙, 나머지 0**인 커리어로 1회차를 돌린 결과다. 회차 반복과 구매 정책을 끄고 측정하므로 차이는 그 업그레이드에만 귀속된다.");
            sb.AppendLine("- 모든 Δ는 **같은 시드끼리 짝지은 차이의 중앙값**이다. 시드 경로 발산만으로 순수입 중앙값이 10% 흔들리므로(2026-07-28 상자 가중치 건) 독립 비교로는 신호가 묻힌다.");
            sb.AppendLine("- 총비용 = 만렙까지 누적 포인트. 선행이 있으면 포함한다 (WeaponEpic = 희귀 300 + 에픽 1000 = 1300).");
            sb.AppendLine("- **포인트당 주간G** = Δ주간순수입 / 총비용. 1회차 안에서만 잰 값이라 회차를 거듭할수록 실제 가치는 이 값의 배수가 된다.");
            sb.AppendLine("- `RegularAdventurer`(7일마다 단골 확정 방문)는 단골 시스템이 시뮬에 없어 스윕에서 빠졌다 — 21종 중 20종이다.");
            sb.AppendLine("- 폐업이 잦은 페르소나(초급자)는 주간순수입보다 **Δ수명**이 주요 지표다. 짧게 살면 주간 평균이 왜곡된다.");
            sb.AppendLine();

            sb.AppendLine("## 기준선 (업그레이드 0)");
            sb.AppendLine();
            sb.AppendLine("| 페르소나 | 수명(주) | 주간 순수입 | 폐업률 |");
            sb.AppendLine("|---|---:|---:|---:|");
            foreach (var p in personas)
            {
                var rs = baseArm.results.Where(r => r.persona == p).ToList();
                sb.AppendLine($"| {p} | {Median(rs.Select(r => (float)Lifespan(r, opt))):F0} " +
                              $"| {Median(rs.Select(WeeklyNet)):F0}G " +
                              $"| {Pct(rs.Count(r => r.bankruptWeek > 0), rs.Count)} |");
            }
            sb.AppendLine();

            foreach (var p in personas)
            {
                var baseBySeed = baseArm.results.Where(r => r.persona == p).ToDictionary(r => r.seed);
                int baseBankrupt = baseBySeed.Values.Count(r => r.bankruptWeek > 0);

                var rows = new List<(UpgradeKey key, int cost, float dNet, float perPoint, float dLife, float dBankrupt)>();
                foreach (var arm in arms.Skip(1))
                {
                    var rs = arm.results.Where(r => r.persona == p).ToList();
                    var pairedNet  = new List<float>();
                    var pairedLife = new List<float>();
                    foreach (var r in rs)
                    {
                        if (!baseBySeed.TryGetValue(r.seed, out var bl)) continue;
                        pairedNet.Add(WeeklyNet(r) - WeeklyNet(bl));
                        pairedLife.Add(Lifespan(r, opt) - Lifespan(bl, opt));
                    }
                    float dNet = Median(pairedNet);
                    float dBank = rs.Count == 0 ? 0f
                        : 100f * rs.Count(r => r.bankruptWeek > 0) / rs.Count - 100f * baseBankrupt / baseBySeed.Count;
                    rows.Add((arm.key, arm.totalCost, dNet,
                              arm.totalCost > 0 ? dNet / arm.totalCost : 0f, Median(pairedLife), dBank));
                }

                sb.AppendLine($"## {p}");
                sb.AppendLine();
                sb.AppendLine("| 업그레이드 | 총비용(p) | Δ주간순수입 | 포인트당 주간G | Δ수명(주) | Δ폐업률(%p) |");
                sb.AppendLine("|---|---:|---:|---:|---:|---:|");
                foreach (var row in rows.OrderByDescending(x => x.perPoint))
                    sb.AppendLine($"| {row.key} | {row.cost} | {row.dNet:+#;-#;0}G | {row.perPoint:+0.00;-0.00;0} " +
                                  $"| {row.dLife:+#.#;-#.#;0} | {row.dBankrupt:+0.0;-0.0;0} |");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>런 1회의 주간 평균 순수입 (income - spent). 폐업 런은 생존한 주차만으로 나눈다.</summary>
        private static float WeeklyNet(RunResult r) =>
            r.weekly.Count == 0 ? 0f : (float)r.weekly.Sum(s => s.income - s.spent) / r.weekly.Count;

        #endregion

        #region 엔드리스 템플릿 난이도 스윕

        /// <summary>
        /// 엔드리스 템플릿 16종의 단독 난이도 측정. arms[0]은 기준선(현행 순차 재생)이다.
        /// 각 구성은 61주차부터 그 템플릿 하나만 반복 출제하므로, 61주 이후 통계 차이는 전부 그 템플릿에 귀속된다.
        /// </summary>
        public static string WriteEndlessSweep(List<EndlessArm> arms, SimOptions opt, int campaignWeeks, string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"sim_endless_sweep_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(path, BuildEndlessSweep(arms, opt, campaignWeeks), new UTF8Encoding(false));
            return path;
        }

        private static string BuildEndlessSweep(List<EndlessArm> arms, SimOptions opt, int campaignWeeks)
        {
            var sb = new StringBuilder();
            int first = campaignWeeks + 1;
            var personas = arms[0].results.Select(r => r.persona).Distinct().ToList();

            sb.AppendLine("# 엔드리스 템플릿 난이도 스윕");
            sb.AppendLine();
            sb.AppendLine($"> 생성: {DateTime.Now:yyyy-MM-dd HH:mm} / 시드 {opt.seeds}개 x 페르소나 {personas.Count}종 " +
                          $"/ 기간 {opt.weeks}주 / 회차 {opt.runsPerSeed} / 구성 {arms.Count}개(기준선 + {arms.Count - 1}종)");
            sb.AppendLine();
            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            sb.AppendLine($"- 각 구성은 **{first}주차부터 그 템플릿 하나만 반복 출제**한 결과다. 캠페인(1~{campaignWeeks}주)은 손대지 않으므로 같은 시드의 그 구간은 전 구성이 동일하다.");
            sb.AppendLine($"- **통과율**이 이 표의 핵심이다 — {first}주 이후 주차 중 퀘스트를 깬 비율. 난이도 분류(쉬움/평범/어려움/최고난도)가 실제로 통과율 순서대로인지 확인한다.");
            sb.AppendLine("- **도달 런**이 0이면 그 페르소나는 엔드리스에 못 갔다는 뜻이라 나머지 칸이 무의미하다.");
            sb.AppendLine("- **폐업률**은 도달 런 기준이다. 통과율이 낮은데 폐업률도 낮으면 벌금을 감당하고 있다는 뜻(= 위협이 아니다).");
            sb.AppendLine($"- **생존 주차**는 {first}주 도달 후 몇 주를 더 버텼는지의 중앙값이다. 통과율보다 직접적인 체감 지표다.");
            sb.AppendLine();

            foreach (var p in personas)
            {
                var rows = new List<(string label, int runs, float pass, float bankrupt, float survive, float net)>();
                foreach (var arm in arms)
                {
                    var reached = arm.results.Where(r => r.persona == p && r.weekly.Any(s => s.week >= first)).ToList();
                    var tail = reached.SelectMany(r => r.weekly.Where(s => s.week >= first)).ToList();
                    if (tail.Count == 0)
                    {
                        rows.Add((arm.label, 0, 0f, 0f, 0f, 0f));
                        continue;
                    }

                    rows.Add((
                        arm.label,
                        reached.Count,
                        100f * tail.Count(s => s.questPassed) / tail.Count,
                        100f * reached.Count(r => r.bankruptWeek > 0) / reached.Count,
                        Median(reached.Select(r => (float)(Lifespan(r, opt) - campaignWeeks))),
                        (float)tail.Sum(s => s.income - s.spent) / tail.Count));
                }

                sb.AppendLine($"## {p}");
                sb.AppendLine();
                sb.AppendLine("| 템플릿 | 도달 런 | 통과율 | 폐업률 | 생존 주차 | 주간 순수입 |");
                sb.AppendLine("|---|---:|---:|---:|---:|---:|");
                foreach (var row in rows)
                    sb.AppendLine(row.runs == 0
                        ? $"| {row.label} | 0 | - | - | - | - |"
                        : $"| {row.label} | {row.runs} | {row.pass:F1}% | {row.bankrupt:F1}% " +
                          $"| {row.survive:F0} | {row.net:+#;-#;0}G |");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion

        #region 요약

        private static string BuildSummary(List<RunResult> results, SimOptions opt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 밸런스 시뮬레이션 요약");
            sb.AppendLine();
            sb.AppendLine($"> 생성: {DateTime.Now:yyyy-MM-dd HH:mm} / 시드 {opt.seeds}개 x 페르소나 {results.Select(r => r.persona).Distinct().Count()}종 / 기간 {opt.weeks}주"
                          + (opt.runsPerSeed > 1 ? $" / 시드당 최대 {opt.runsPerSeed}회차" : "")
                          + (opt.goldPerLegacyOverride > 0 ? $" / **goldPerLegacy={opt.goldPerLegacyOverride} (오버라이드)**" : "")
                          + (opt.forceMorningEvents ? " / **아침 이벤트 강제 참여 (진단 런)**" : "")
                          + (opt.useTraits ? "" : " / **특성 미반영 (무결성 검증 런)**")
                          + (opt.useSeer ? "" : " / **점술 미반영 (무결성 검증 런)**")
                          + (opt.useNamed ? (opt.preferNamed ? " / 네임드 **우대 정책**" : " / 네임드 **named-blind**")
                                          : " / **네임드 미반영 (무결성 검증 런)**")
                          + (opt.useCraft ? "" : " / **무기 제작·재부여 미반영 (무결성 검증 런)**")
                          + (opt.forceReroll ? " / **재부여 강제 시도 (진단 런)**" : ""));
            sb.AppendLine("> 페르소나 정의·단순화 범위: [시뮬레이터_설계.md](../balance/reference/시뮬레이터_설계.md)");
            sb.AppendLine("> 보정 대상: [주간퀘스트_레벨디자인.md](../balance/reference/주간퀘스트_레벨디자인.md), [주간퀘스트_난이도_계수.md](../balance/reference/주간퀘스트_난이도_계수.md)");
            sb.AppendLine();

            // 기존 표들은 1회차만 집계 — 이전 리포트와의 비교 가능성 유지. 회차 반복은 전용 섹션에서 본다.
            var firstRuns = results.Where(r => r.careerRun == 1).ToList();

            AppendComparison(sb, firstRuns, opt);
            AppendCareerSection(sb, results, opt);

            foreach (var group in firstRuns.GroupBy(r => r.persona))
            {
                var runs = group.ToList();
                sb.AppendLine($"## {group.Key} (시드 {runs.Count}개)");
                sb.AppendLine();

                int bankrupts = runs.Count(r => r.bankruptWeek > 0);
                sb.AppendLine($"- **폐업률**: {Pct(bankrupts, runs.Count)}" +
                              (bankrupts > 0
                                  ? $" (폐업 주차 중앙값 {Median(runs.Where(r => r.bankruptWeek > 0).Select(r => (float)r.bankruptWeek)):F0}주)"
                                  : ""));
                sb.AppendLine($"- **생존 일수 중앙값**: {Median(runs.Select(r => (float)r.survivalDays)):F0}일");
                sb.AppendLine();

                        sb.AppendLine("### 평판 등급 최초 도달 주차 (중앙값 / 도달률) — 계수 문서 §2-3 대조");
                sb.AppendLine();
                sb.AppendLine("| 등급 | 계수 문서 추정 | 시뮬 중앙값 | 도달률 |");
                sb.AppendLine("|---|---|---|---|");
                string[] repNames = { "", "Silver", "Gold", "Platinum", "Diamond" };
                string[] docEst = { "", "약 7주", "약 21주", "약 41주", "약 60주" };
                for (int lv = 1; lv <= 4; lv++)
                {
                    var reached = runs.Where(r => r.firstRepLevelWeek[lv] > 0)
                                      .Select(r => (float)r.firstRepLevelWeek[lv]).ToList();
                    sb.AppendLine($"| {repNames[lv]} | {docEst[lv]} | " +
                                  (reached.Count > 0 ? $"{Median(reached):F0}주" : "-") +
                                  $" | {Pct(reached.Count, runs.Count)} |");
                }
                sb.AppendLine();

                // 등급 무기 최초 보유 주차
                sb.AppendLine("### 등급 무기 최초 보유 주차 (중앙값 / 보유율)");
                sb.AppendLine();
                sb.AppendLine("| 등급 | 시뮬 중앙값 | 보유율 |");
                sb.AppendLine("|---|---|---|");
                string[] gradeNames = { "일반", "고급", "희귀", "영웅", "전설" };
                for (int g = 2; g <= 4; g++)
                {
                    var got = runs.Where(r => r.firstGradeWeek[g] > 0)
                                  .Select(r => (float)r.firstGradeWeek[g]).ToList();
                    sb.AppendLine($"| {gradeNames[g]} | " +
                                  (got.Count > 0 ? $"{Median(got):F0}주" : "-") +
                                  $" | {Pct(got.Count, runs.Count)} |");
                }
                sb.AppendLine();

                // 체크포인트 주차 지표
                sb.AppendLine("### 체크포인트 주차 지표 (중앙값 [25~75 백분위])");
                sb.AppendLine();
                sb.AppendLine("| 주차 | 생존 시드 | 주간 시도 | 주간 성공 | 누적 평판 | 통찰 | 보유 골드 | 보유 무기 |");
                sb.AppendLine("|---:|---:|---|---|---|---|---|---:|");
                foreach (int wk in CheckpointWeeks.Where(x => x <= opt.weeks))
                {
                    var stats = runs.Select(r => r.weekly.FirstOrDefault(s => s.week == wk))
                                    .Where(s => s != null).ToList();
                    if (stats.Count == 0) { sb.AppendLine($"| {wk} | 0 | - | - | - | - | - | - |"); continue; }
                    sb.AppendLine($"| {wk} | {stats.Count} " +
                                  $"| {Band(stats.Select(s => (float)s.attempts))} " +
                                  $"| {Band(stats.Select(s => (float)s.successes))} " +
                                  $"| {Band(stats.Select(s => (float)s.repEnd))} " +
                                  $"| {Median(stats.Select(s => (float)s.insightEnd)):F0} " +
                                  $"| {Median(stats.Select(s => (float)s.goldEnd)):F0} " +
                                  $"| {Median(stats.Select(s => (float)s.weaponsOwned)):F0} |");
                }
                sb.AppendLine();

                // 시험주(티어 경계) 퀘스트 통과율 — 레벨디자인 합격선 검증
                sb.AppendLine("### 주간 퀘스트 통과율 (시험주 = 티어 경계)");
                sb.AppendLine();
                sb.AppendLine("| 주차 | 통과율 (생존 시드 기준) | 레벨디자인 합격선 |");
                sb.AppendLine("|---:|---|---|");
                string guide = group.Key == "상급자" ? "95% 이상" : group.Key == "중급자" ? "약 70%" : "40% 미만";
                foreach (int wk in ExamWeeks.Where(x => x <= opt.weeks))
                {
                    var stats = runs.Select(r => r.weekly.FirstOrDefault(s => s.week == wk))
                                    .Where(s => s != null).ToList();
                    sb.AppendLine($"| {wk} | " +
                                  (stats.Count > 0 ? Pct(stats.Count(s => s.questPassed), stats.Count) : "-") +
                                  $" | {guide} |");
                }
                sb.AppendLine();

                // 전체 통과율 곡선 (10주 구간)
                sb.AppendLine("### 퀘스트 통과율 10주 구간 평균");
                sb.AppendLine();
                sb.AppendLine("| 구간 | 통과율 | 주간 성공 중앙값 |");
                sb.AppendLine("|---|---|---|");
                for (int start = 1; start <= opt.weeks; start += 10)
                {
                    int end = Math.Min(start + 9, opt.weeks);
                    var stats = runs.SelectMany(r => r.weekly)
                                    .Where(s => s.week >= start && s.week <= end).ToList();
                    if (stats.Count == 0) continue;
                    sb.AppendLine($"| {start}~{end}주 | {Pct(stats.Count(s => s.questPassed), stats.Count)} " +
                                  $"| {Median(stats.Select(s => (float)s.successes)):F0} |");
                }
                sb.AppendLine();

                AppendSpendBreakdown(sb, runs, opt);
                AppendRatioTable(sb, runs, opt);
                AppendTraitSection(sb, runs, opt);
                AppendSeerSection(sb, runs, opt);
                AppendNamedSection(sb, runs, opt);
                AppendCraftDiagnostic(sb, runs, opt);
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 해석 주의");
            sb.AppendLine();
            sb.AppendLine("- v8 단순화: 남은 제외 항목은 **기분·함정·대화·네임드 부활 이벤트**뿐이다.");
            sb.AppendLine("- 무기 제작/재부여가 반영된다 (단계 5-3) — 제작은 해금일·인벤 슬롯·골드·재료(유산 감소),");
            sb.AppendLine("  재부여는 골드만 소모(잠금 배율 x 유산 배율)하고 잠그지 않은 슬롯을 같은 등급 풀에서 재추첨한다.");
            sb.AppendLine("  **봇은 재부여 결과를 무조건 수용한다** — 런타임의 before/after 선택을 모델링하면 정책이 결과를");
            sb.AppendLine("  지배하므로 최악 케이스로 하한을 잡았다. 유산 재부여 2종도 이제 구매 대상이다.");
            sb.AppendLine("  정책: 상급 = 최고가 레시피 1종 + 에픽+ 무기에 잠금 1개 재부여 / 중급 = 최저가 레시피 + 잠금 0 가끔 / 초급 = 미사용.");
            sb.AppendLine("- 점술이 반영된다 (단계 5-1) — 비용(seerBaseCost x 주차 계단 배율), LUK 구간 가중 추첨,");
            sb.AppendLine("  운세 보정(-0.1~+0.2)을 **매 전투 판정에 가산**(cumulativeModifier 미러). 결과는 되돌릴 수 없다.");
            sb.AppendLine("  정책: 상급 = 에픽+ 던전 / 중급 = 전설 던전 한정 / 초급 = 미사용. **봇은 LUK을 모르고 상담한다(LUK-blind).**");
            sb.AppendLine("  같은 cumulativeModifier를 쓰는 **함정 누적 페널티**와 같은 함수의 **기분 배율**은 이번 축이 아니라 미반영.");
            sb.AppendLine("- 특성 16종이 반영된다 (단계 4) — 방문자마다 균등 롤, GetTrait* 전 지점 미러(성공률/사망률/대성공/보호/");
            sb.AppendLine("  소요시간/탐험도/재료/수수료/평판/구매가). **봇은 특성을 보고 배정을 바꾸지 않는다(trait-blind)** —");
            sb.AppendLine("  특성별 표의 차이가 특성 자체의 가치가 되도록 한 설계다. 특성을 보고 골라 쓰는 플레이는 미모델.");
            sb.AppendLine("- 액티브 아이템이 반영된다 — 제작 재료 드롭(보스/RareDrop/특수)·필요량 상한 구매, 제작(ExecuteCraft 미러),");
            sb.AppendLine("  효과 11종(부적/메달/아뮬렛/포션/단검/지도/두루마리/신발/강화석 3종). 로프(함정 미모델)·인형(호감도 미추적) 제외.");
            sb.AppendLine("  사용 정책: 상급 = 에픽+ 던전 부적, 에픽+ 강화에 강화석 / 중급 = 기본 부적 아무데나 / 초급 = 미사용.");
            sb.AppendLine("- 유산이 반영된다 — 21종 중 17종 효과(시작 골드/통찰/무기/슬롯, 강화/진화 성공률·비용, 재료 감소,");
            sb.AppendLine("  분해/수수료/팁/대성공/모험속도, 상점 갱신, 아침 보장) + 폐업 유산 획득 + 벌금 긴급 환전.");
            sb.AppendLine("  재부여 2종·네임드 2종은 해당 시스템 미모델링으로 효과·구매 모두 제외. 기존 표는 전부 1회차 기준이다.");
            sb.AppendLine("- 아침 이벤트 9종이 반영된다 — 평판별 등장 확률(5~20%), 가중 추첨, 하루 1회.");
            sb.AppendLine("  페르소나 정책: 상급 = Config 기반 EV 계산 후 이득일 때만 / 중급 = 상자만 가끔 / 초급 = 무시.");
            sb.AppendLine("  상자 지출 0은 봇 결함이 아니라 EV<비용의 측정 결과일 수 있다 — 강제 참여 런과 비교해 판정한다.");
            sb.AppendLine("- 무기 부가효과는 반영된다 — 등급별 개수 롤, StatBonus/AllStatBonus/WeaponTypeMatchBonus의 성공률 기여,");
            sb.AppendLine("  던전등급·방어구 조건부 가산, RetreatPrevention 보호, GreatSuccessBonus, EnforceMaterialBonus.");
            sb.AppendLine("  강화는 효과 하나를 최대치로 올리는 실제 규칙을 따른다(최대 강화 = 효과 개수).");
            sb.AppendLine("- 인벤토리 상한(InventoryConfig.inventorySlots)과 무기 해체(골드·진화재료 회수)가 반영된다.");
            sb.AppendLine("- '타입 매칭 평판 +1'은 대여 무기가 모험가 최적 타입일 때 반영된다(기본 무기 제외). 호감도는 시뮬 미추적.");
            sb.AppendLine("- 의뢰판 던전 추첨에 주차별 등급 배율(계수 문서 §4-2, QuestBoardConfig.questWeightTiers)이 적용된다.");
            sb.AppendLine("- 주간 퀘스트는 현재 프로젝트에 구워진 WeeklyQuestData를 그대로 사용했다.");
            return sb.ToString();
        }

        /// <summary>
        /// 페르소나 3종 핵심 지표 비교 — 초급/중급/상급 구분선 판정용.
        /// 지표: 회차 수명 / 퀘스트 통과율 / 도달 평판 / 주간 순수입 (+ 정보 행동량).
        /// </summary>
        private static void AppendComparison(StringBuilder sb, List<RunResult> results, SimOptions opt)
        {
            var groups = results.GroupBy(r => r.persona).ToList();
            if (groups.Count == 0) return;

            sb.AppendLine("## 페르소나 비교 (구분선 판정)");
            sb.AppendLine();
            sb.AppendLine("| 지표 | " + string.Join(" | ", groups.Select(g => g.Key)) + " |");
            sb.AppendLine("|---|" + string.Concat(groups.Select(_ => "---|")));

            void Row(string label, Func<List<RunResult>, string> cell) =>
                sb.AppendLine($"| {label} | " + string.Join(" | ", groups.Select(g => cell(g.ToList()))) + " |");

            Row("회차 수명 (주)", runs => $"{Median(runs.Select(r => (float)Lifespan(r, opt))):F0}주");
            Row("폐업률", runs => Pct(runs.Count(r => r.bankruptWeek > 0), runs.Count));
            Row("실시간 플레이 (4배속)", runs =>
                $"{Median(runs.Select(r => (float)r.survivalDays)) * REAL_MINUTES_PER_DAY / 4f / 60f:F1}시간");
            Row("퀘스트 통과율 (전 주차)", runs =>
                Pct(runs.SelectMany(r => r.weekly).Count(s => s.questPassed), runs.Sum(r => r.weekly.Count)));
            Row("최고 도달 평판", runs => RepName(Median(runs.Select(r => (float)MaxRepLevel(r)))));
            Row("주간 순수입", runs => $"{WeeklyMedian(runs, s => s.income - s.spent):F0}G");
            Row("주간 수색 지출", runs => $"{WeeklyMedian(runs, s => s.spentScout):F0}G");
            Row("주간 수색/대화 횟수", runs => $"{WeeklyMedian(runs, s => s.scoutCount):F0} / " +
                                               $"{WeeklyMedian(runs, s => s.talkCount):F0}");
            sb.AppendLine();
            sb.AppendLine($"> 실시간 환산: 게임 하루 = {REAL_MINUTES_PER_DAY}분(1배속). 1배속은 위 값의 4배.");
            sb.AppendLine();
        }

        /// <summary>
        /// 유산 회차 반복 곡선 (단계 2) — "회차가 늘수록 수명이 늘어나는가"를 회차별로 본다.
        /// N회차 행 = N회차까지 도달한 시드만 포함 (생존 완주하면 커리어 조기 종료라 표본이 준다).
        /// </summary>
        private static void AppendCareerSection(StringBuilder sb, List<RunResult> results, SimOptions opt)
        {
            if (opt.runsPerSeed <= 1) return;

            sb.AppendLine("## 유산 회차 반복 (폐업 시에만 유산 획득 -> 구매 -> 다음 회차)");
            sb.AppendLine();
            sb.AppendLine("- 구매 정책: 상급 = 우선순위 그리디(유보 0%) / 중급 = 싼 것부터(유보 20%) / 초급 = 랜덤(유보 50%)");
            sb.AppendLine("- 시뮬 미반영 업그레이드 4종(재부여 2종·네임드 2종)은 구매 대상에서 제외했다.");
            sb.AppendLine("- **주의: 회차 곡선은 게임 밸런스와 구매 정책의 합작이다.** 결과가 이상하면 정책부터 의심할 것.");
            sb.AppendLine();

            foreach (var group in results.GroupBy(r => r.persona))
            {
                var runs = group.ToList();
                sb.AppendLine($"### {group.Key}");
                sb.AppendLine();
                sb.AppendLine("| 회차 | 도달 시드 | 수명 중앙값(주) | 폐업률 | 통과율 | 시작 업그레이드(중) | 획득 유산(중) | 환전 발동률 |");
                sb.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|");
                for (int run = 1; run <= opt.runsPerSeed; run++)
                {
                    var rr = runs.Where(r => r.careerRun == run).ToList();
                    if (rr.Count == 0) break;
                    int bankrupts = rr.Count(r => r.bankruptWeek > 0);
                    var bankruptOnly = rr.Where(r => r.bankruptWeek > 0).ToList();
                    sb.AppendLine($"| {run} | {rr.Count} " +
                                  $"| {Median(rr.Select(r => (float)Lifespan(r, opt))):F0} " +
                                  $"| {Pct(bankrupts, rr.Count)} " +
                                  $"| {Pct(rr.SelectMany(r => r.weekly).Count(s => s.questPassed), rr.Sum(r => r.weekly.Count))} " +
                                  $"| {Median(rr.Select(r => (float)r.upgradesOwned)):F0} " +
                                  (bankruptOnly.Count > 0
                                      ? $"| {Median(bankruptOnly.Select(r => (float)r.earnedLegacy)):F0} "
                                      : "| - ") +
                                  $"| {Pct(rr.Count(r => r.legacyConverted > 0), rr.Count)} |");
                }
                sb.AppendLine();
            }
        }

        /// <summary>주차별 지출 내역 — 가격 밸런스 판정용</summary>
        private static void AppendSpendBreakdown(StringBuilder sb, List<RunResult> runs, SimOptions opt)
        {
            sb.AppendLine("### 주간 수지 / 지출 분해 (10주 구간 중앙값)");
            sb.AppendLine();
            sb.AppendLine("| 구간 | 수입 | 지출 | 순수입 | 무기구매 | 대장간 | 재료 | 수색 | 새로고침 | 아침지출 | 아침수입 | 아이템 | 벌금 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            for (int start = 1; start <= opt.weeks; start += 10)
            {
                int end = Math.Min(start + 9, opt.weeks);
                var stats = runs.SelectMany(r => r.weekly)
                                .Where(s => s.week >= start && s.week <= end).ToList();
                if (stats.Count == 0) continue;
                float M(Func<WeekStat, int> sel) => Median(stats.Select(s => (float)sel(s)));
                sb.AppendLine($"| {start}~{end}주 | {M(s => s.income):F0} | {M(s => s.spent):F0} " +
                              $"| {M(s => s.income - s.spent):F0} | {M(s => s.spentWeapon):F0} " +
                              $"| {M(s => s.spentSmith):F0} | {M(s => s.spentMaterial):F0} " +
                              $"| {M(s => s.spentScout):F0} " +
                              $"| {M(s => s.spentRefresh):F0} " +
                              $"| {M(s => s.spentMorning):F0} | {M(s => s.incomeMorning):F0} " +
                              $"| {M(s => s.spentItemCraft + s.spentCraftMat):F0} " +
                              $"| {M(s => s.finePaid):F0} |");
            }
            sb.AppendLine();
        }

        /// <summary>
        /// R 기준 비율 — R = 모험 1회 성공 순수입(수수료). 모든 가격을 "모험 몇 회분"으로 읽는다.
        /// 절대 골드로는 비싼지 싼지 판단할 근거가 없어, 조정할 때마다 임의 수치를 고르게 된다.
        /// 구간별로 보는 이유: 일수 비례 배율이 후반에 폭주하는지가 여기서 드러난다.
        /// </summary>
        private static void AppendRatioTable(StringBuilder sb, List<RunResult> runs, SimOptions opt)
        {
            sb.AppendLine("### R 기준 비율 (R = 모험 1회 순수입)");
            sb.AppendLine();
            sb.AppendLine("| 구간 | R | 무기 1자루 | 제작 1자루 | 재부여 1회 | 대장간 1회 | 재료 1회 | 수색 1회 | 점술 1회 | 상자 1개 | 아이템 1개 | 주간벌금/수입 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            for (int start = 1; start <= opt.weeks; start += 10)
            {
                int end = Math.Min(start + 9, opt.weeks);
                var stats = runs.SelectMany(r => r.weekly)
                                .Where(s => s.week >= start && s.week <= end).ToList();
                if (stats.Count == 0) continue;

                long successes = stats.Sum(s => (long)s.successes);
                if (successes == 0) continue;
                float r = stats.Sum(s => (long)s.incomeAdventure) / (float)successes;
                if (r <= 0f) continue;

                string Per(long total, long count) =>
                    count == 0 ? "-" : $"{total / (float)count / r:F1}R";

                long income = stats.Sum(s => (long)s.income);
                string fineRatio = income == 0 ? "-"
                    : $"{100f * stats.Sum(s => (long)s.finePaid) / income:F0}%";

                sb.AppendLine($"| {start}~{end}주 | {r:F0}G " +
                              $"| {Per(stats.Sum(s => (long)s.spentWeapon), stats.Sum(s => (long)s.weaponBuyCount))} " +
                              $"| {Per(stats.Sum(s => (long)s.spentWeaponCraft), stats.Sum(s => (long)s.weaponCraftCount))} " +
                              $"| {Per(stats.Sum(s => (long)s.spentReroll), stats.Sum(s => (long)s.rerollCount))} " +
                              $"| {Per(stats.Sum(s => (long)s.spentSmith), stats.Sum(s => (long)(s.enforceCount + s.evolveCount)))} " +
                              $"| {Per(stats.Sum(s => (long)s.spentMaterial), stats.Sum(s => (long)s.materialBuyCount))} " +
                              $"| {Per(stats.Sum(s => (long)s.spentScout), stats.Sum(s => (long)s.scoutCount))} " +
                              $"| {Per(stats.Sum(s => (long)s.spentSeer), stats.Sum(s => (long)s.seerCount))} " +
                              $"| {Per(stats.Sum(s => (long)s.spentMorningBox), stats.Sum(s => (long)s.boxBuyCount))} " +
                              $"| {Per(stats.Sum(s => (long)(s.spentItemCraft + s.spentCraftMat)), stats.Sum(s => (long)s.craftCount))} " +
                              $"| {fineRatio} |");
            }
            sb.AppendLine();
        }

        /// <summary>
        /// 특성별 기여도 (단계 4) — 봇이 특성을 모르고 배정(trait-blind)하므로 특성 간 차이가 곧
        /// 특성 자체의 가치다. 16종이 균등 롤이라 건수는 비슷해야 하며, 판정 질문은
        /// "Swift(처리량)와 Famous(평판)의 격차가 뽑기 운으로 용인 가능한 수준인가"다 (로드맵 1-2).
        /// </summary>
        private static void AppendTraitSection(StringBuilder sb, List<RunResult> runs, SimOptions opt)
        {
            if (!opt.useTraits) return;
            int n = RunResult.TraitN;
            var count = new long[n]; var succ = new long[n]; var death = new long[n];
            var income = new long[n]; var matSpend = new long[n]; var rep = new long[n];
            var dur = new double[n];
            foreach (var r in runs)
                for (int i = 0; i < n; i++)
                {
                    count[i] += r.traitCount[i]; succ[i] += r.traitSuccess[i]; death[i] += r.traitDeath[i];
                    income[i] += r.traitIncome[i]; matSpend[i] += r.traitMatSpend[i]; rep[i] += r.traitRep[i];
                    dur[i] += r.traitDurMin[i];
                }
            if (count.Sum() == 0) return;

            // 전체 평균 대비 지수용 기준
            float avgIncome = income.Sum() / (float)count.Sum();

            string[] names = { "행운아 Lucky", "베테랑 Veteran", "수집가 Looter", "신속 Swift",
                               "부자 Rich", "짐꾼 Porter", "광전사 Berserker", "생존가 Enduring",
                               "성장하는 자 Rising", "유명인 Famous", "흥정꾼 Haggler", "겁쟁이 Coward",
                               "집중 Focused", "도축업자 Butcher", "양학 EasyExpert", "전투광 BattleManiac" };

            sb.AppendLine("### 특성별 기여도 (1회차 합산, 배정은 특성 무관)");
            sb.AppendLine();
            sb.AppendLine("| 특성 | 건수 | 성공률 | 사망률 | 수수료/건 | 지수 | 재료구매/건 | 평판/건 | 소요분/건 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => count[i] > 0 ? income[i] / (float)count[i] : 0f))
            {
                if (count[i] == 0) { sb.AppendLine($"| {names[i]} | 0 | - | - | - | - | - | - | - |"); continue; }
                float c = count[i];
                sb.AppendLine($"| {names[i]} | {count[i]} " +
                              $"| {100f * succ[i] / c:F0}% | {100f * death[i] / c:F1}% " +
                              $"| {income[i] / c:F0}G | {(avgIncome > 0f ? income[i] / c / avgIncome * 100f : 0f):F0} " +
                              $"| {matSpend[i] / c:F0}G | {rep[i] / c:F2} | {dur[i] / c:F0} |");
            }
            sb.AppendLine();
            sb.AppendLine("> 지수 = 수수료/건을 전체 평균 100으로 환산. 소요분이 짧은 특성(Swift)은 낮 시간을 덜 점유해");
            sb.AppendLine("> 표에 없는 처리량 이득이 추가로 있다. Famous/Veteran의 가치(평판/대성공 확정권)도 골드 열에는 안 잡힌다.");
            sb.AppendLine();
        }

        /// <summary>
        /// 점술 LUK 구간별 결과 (단계 5-1) — 봇이 LUK을 모르고 상담하므로(LUK-blind) 구간별 평균 보정이
        /// 곧 SeerConfig 가중치의 실효 기대값이다. 판정 질문: "LUK이 낮은 모험가에게 300G는 낭비인가".
        /// </summary>
        private static void AppendSeerSection(StringBuilder sb, List<RunResult> runs, SimOptions opt)
        {
            if (!opt.useSeer) return;
            var count = new long[4]; var succ = new long[4]; var modSum = new double[4];
            foreach (var r in runs)
                for (int i = 0; i < 4; i++)
                {
                    count[i] += r.seerLukCount[i]; succ[i] += r.seerLukSuccess[i]; modSum[i] += r.seerLukModSum[i];
                }
            if (count.Sum() == 0)
            {
                sb.AppendLine("### 점술 (단계 5-1)");
                sb.AppendLine();
                sb.AppendLine("- 상담 0회 — 이 페르소나는 점술을 이용하지 않는다(정책) 또는 골드 가드에 막혔다.");
                sb.AppendLine();
                return;
            }

            // SeerConfig 가중치로 계산한 이론 EV — 미러가 맞는지 대조용
            string[] names = { "LUK ~25", "LUK 26~50", "LUK 51~75", "LUK 76~" };

            sb.AppendLine("### 점술 LUK 구간별 결과 (상담 대상 선택은 LUK 무관)");
            sb.AppendLine();
            sb.AppendLine("| LUK 구간 | 상담 건수 | 평균 운세 보정 | 상담 후 완주율 |");
            sb.AppendLine("|---|---:|---:|---:|");
            for (int i = 0; i < 4; i++)
            {
                if (count[i] == 0) { sb.AppendLine($"| {names[i]} | 0 | - | - |"); continue; }
                sb.AppendLine($"| {names[i]} | {count[i]} " +
                              $"| {modSum[i] / count[i] * 100f:+0.0;-0.0}%p " +
                              $"| {100f * succ[i] / count[i]:F0}% |");
            }
            sb.AppendLine();
            sb.AppendLine("> 평균 운세 보정은 **매 전투 판정마다 가산**된다(cumulativeModifier) — 모험 1회에 한 번이 아니다.");
            sb.AppendLine("> 완주율은 상담한 모험만의 값이라 던전 등급 구성이 전체 평균과 다르다(정책상 고등급 편중).");
            sb.AppendLine();
        }

        /// <summary>
        /// 네임드 + 호감도 (단계 5-2). 판정 질문 두 가지:
        /// (1) 네임드 스폰 비율이 이론값(가중치 1:5 = 16.7%)에 수렴하는가
        /// (2) 호감도가 실제로 쌓여 재방문 루프가 도는가 — Max 도달 인원이 0이면 루프가 죽은 것이다
        /// </summary>
        private static void AppendNamedSection(StringBuilder sb, List<RunResult> runs, SimOptions opt)
        {
            if (!opt.useNamed) return;
            long visits = runs.Sum(r => (long)r.namedVisits);
            long attempts = runs.Sum(r => r.weekly.Sum(s => (long)s.attempts));
            if (attempts == 0) return;

            var cnt = new long[4]; var succ = new long[4];
            foreach (var r in runs)
                for (int i = 0; i < 4; i++) { cnt[i] += r.affLevelCount[i]; succ[i] += r.affLevelSuccess[i]; }

            sb.AppendLine("### 네임드 + 호감도 (단계 5-2)");
            sb.AppendLine();
            sb.AppendLine($"- 네임드 방문 비율: **{100f * visits / attempts:F1}%** (전체 모험 {attempts}건 중 {visits}건) / 이론값 16.7%");
            sb.AppendLine($"- 네임드 사망: {runs.Sum(r => (long)r.namedDeaths)}건 (부활 이벤트 미모델 -> 회차 내 영구 이탈)");
            sb.AppendLine($"- 회차 종료 시 호감도 Max 도달 네임드 수 (중앙값): {Median(runs.Select(r => (float)r.namedMaxAffection)):F1}명");
            sb.AppendLine();
            sb.AppendLine("| 출발 시 호감도 | 건수 | 완주율 | 성공률 보너스 |");
            sb.AppendLine("|---|---:|---:|---:|");
            string[] names = { "Low (0~25)", "Medium (26~50)", "High (51~75)", "Max (76~100)" };
            string[] bonus = { "0%p", "+1%p", "+3%p", "+5%p" };
            for (int i = 0; i < 4; i++)
            {
                if (cnt[i] == 0) { sb.AppendLine($"| {names[i]} | 0 | - | {bonus[i]} |"); continue; }
                sb.AppendLine($"| {names[i]} | {cnt[i]} | {100f * succ[i] / cnt[i]:F0}% | {bonus[i]} |");
            }
            sb.AppendLine();
            sb.AppendLine("> 호감도 등급별 완주율 차이는 성공률 보너스뿐 아니라 **배정 정책의 차이**도 섞여 있다");
            sb.AppendLine("> (우대 정책이면 호감도가 쌓인 네임드일수록 더 큰 던전에 간다). 보너스 단독 효과가 아니다.");
            sb.AppendLine();
        }

        /// <summary>
        /// 무기 제작 실패 사유 분해 (단계 5-3 진단) — 제작이 0에 수렴할 때
        /// 봇 결함(골드/슬롯 가드)인지 재료 병목인지, 재료면 일반(CRF)인지 특수(SPC)인지 가른다.
        /// </summary>
        private static void AppendCraftDiagnostic(StringBuilder sb, List<RunResult> runs, SimOptions opt)
        {
            if (!opt.useCraft) return;
            long locked = runs.Sum(r => (long)r.craftFailLocked);
            long slot   = runs.Sum(r => (long)r.craftFailSlot);
            long goldF  = runs.Sum(r => (long)r.craftFailGold);
            long crf    = runs.Sum(r => (long)r.craftFailCraftMat);
            long spc    = runs.Sum(r => (long)r.craftFailSpecialMat);
            long done   = runs.Sum(r => r.weekly.Sum(s => (long)s.weaponCraftCount));
            long tries  = locked + slot + goldF + Math.Max(crf, spc) + done;
            if (tries == 0) return;

            sb.AppendLine("### 무기 제작 시도 결과 분해 (진단)");
            sb.AppendLine();
            sb.AppendLine("| 결과 | 횟수 | 비율 |");
            sb.AppendLine("|---|---:|---:|");
            void Row(string label, long n) => sb.AppendLine($"| {label} | {n} | {100f * n / tries:F1}% |");
            Row("**제작 성공**", done);
            Row("해금일 미도달", locked);
            Row("골드 부족", goldF);
            Row("인벤토리 만석", slot);
            Row("일반 재료(MAT_CRF) 부족", crf);
            Row("**특수 재료(MAT_SPC) 부족**", spc);
            sb.AppendLine();
            sb.AppendLine("> 재료 부족은 두 종류가 동시에 성립할 수 있어 합계가 시도 수를 넘을 수 있다(분모는 중복 제외).");
            sb.AppendLine("> 특수 재료는 던전당 1종 x 보스 성공 시 3~10%라, 이 행이 지배적이면 레시피 요구량이 아니라");
            sb.AppendLine("> **드롭 구조가 병목**이라는 뜻이다.");
            sb.AppendLine();
        }

        /// <summary>게임 하루의 실시간 소요(분, 1배속). 06:00~21:00 = 900게임분, 1실초 = 3게임분.</summary>
        private const float REAL_MINUTES_PER_DAY = 5f;

        /// <summary>회차 수명(주) — 폐업했으면 폐업 주차, 살아남았으면 시뮬 종료 주차</summary>
        private static int Lifespan(RunResult r, SimOptions opt) =>
            r.bankruptWeek > 0 ? r.bankruptWeek : opt.weeks;

        private static int MaxRepLevel(RunResult r)
        {
            for (int lv = 4; lv >= 1; lv--)
                if (r.firstRepLevelWeek[lv] > 0) return lv;
            return 0;
        }

        private static string RepName(float level)
        {
            string[] names = { "Bronze", "Silver", "Gold", "Platinum", "Diamond" };
            return names[Mathf.Clamp(Mathf.RoundToInt(level), 0, 4)];
        }

        private static float WeeklyMedian(List<RunResult> runs, Func<WeekStat, int> sel) =>
            Median(runs.SelectMany(r => r.weekly).Select(s => (float)sel(s)));

        private static string Band(IEnumerable<float> values)
        {
            var list = values.OrderBy(x => x).ToList();
            if (list.Count == 0) return "-";
            return $"{Percentile(list, 0.5f):F0} [{Percentile(list, 0.25f):F0}~{Percentile(list, 0.75f):F0}]";
        }

        private static float Median(IEnumerable<float> values)
        {
            var list = values.OrderBy(x => x).ToList();
            return list.Count == 0 ? 0f : Percentile(list, 0.5f);
        }

        private static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0f;
            float idx = p * (sorted.Count - 1);
            int lo = Mathf.FloorToInt(idx);
            int hi = Mathf.CeilToInt(idx);
            return Mathf.Lerp(sorted[lo], sorted[hi], idx - lo);
        }

        private static string Pct(int n, int total) => total == 0 ? "-" : $"{100f * n / total:F0}%";

        #endregion
    }
}
#endif
