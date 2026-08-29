// Assets/_Projects/Scripts/Editor/AdventureLogExporter.cs
// 메뉴: Tools > TodaysWeaponRental > Adventure Log Exporter
//
// ─── 출력 CSV ────────────────────────────────────────────────────────
//  AdventureLog_Ongoing.csv      — 진행 중인 모험
//  AdventureLog_Completed.csv    — 완료된 모험 인스턴스
//  AdventureLog_Results.csv      — 모험 결과 (AdventureResult)
//  AdventureLog_EventResults.csv — 완료된 이벤트 결과 (EventResult)
//  AdventureLog_Events.csv       — 예정/진행 이벤트 목록 (AdventureProgress.events)
//
// ─── 데이터 소스 ─────────────────────────────────────────────────────
//  Play 모드  : AdventureManager.Instance (실시간 데이터)
//  에디터 모드: Application.persistentDataPath/gamedata.json 역직렬화
//
// ─── 재료 드롭 형식 ───────────────────────────────────────────────────
//  한 컬럼에 "materialID:quantity,materialID:quantity" 형태로 합산
//
// ─── AdventureInstance 주요 필드 ─────────────────────────────────────
//  instanceID, adventurer.instanceID, adventurer.Name,
//  weapon.instanceID, weapon.weaponData.weaponName,
//  dungeon.StaticID, dungeon.dungeonName,
//  startTime, completedDay, isCompleted, isSuccess, isGreatSuccess,
//  isDeath, isRetreated, isUsingDefaultWeapon,
//  accumulatedGold, cumulativeModifier, protectionCharges,
//  accumulatedMaterials (→ 합산 문자열)
//
// ─── AdventureResult 주요 필드 ───────────────────────────────────────
//  adventureID, adventurerID, weaponID, dungeonID,
//  isSuccess, isDeath, isGreatSuccess, isRetreated,
//  isAdventurerTypeMatch, isDungeonTypeMatch,
//  deathProtectionUsed, weaponLost (실제 파괴만), isDefaultWeapon,
//  goldReward, reputationChange, affectionChange,
//  materialDrops (→ 합산 문자열)
//
// ─── EventResult 주요 필드 ───────────────────────────────────────────
//  adventureID (부모), eventIndex,
//  eventType, isSuccess, goldReward,
//  successRateAtTime, protectionActivated,
//  materialDrops (→ 합산 문자열)

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public class AdventureLogExporter : EditorWindow
    {
        private string csvFolder = "Assets/_Projects/Data/CSV/AdventureLog";
        private Vector2 scroll;

        // 마지막 로드 결과 캐시 (GUI 표시용)
        private string lastStatus = "";
        private bool lastStatusIsError;
        private int cachedOngoing;
        private int cachedCompleted;
        private int cachedResults;
        private int cachedEvents;
        private int cachedProgressEvents;

        [MenuItem("Tools/Today's Weapon Rental/Adventure Log Exporter")]
        public static void Open() => GetWindow<AdventureLogExporter>("Adventure Log Exporter");

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Adventure Log Exporter", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // 데이터 소스 표시
            bool isPlaying = Application.isPlaying;
            string sourceLabel = isPlaying
                ? "데이터 소스: AdventureManager.Instance (Play 모드)"
                : "데이터 소스: gamedata.json (에디터 모드)";
            EditorGUILayout.HelpBox(sourceLabel, isPlaying ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(4);
            csvFolder = EditorGUILayout.TextField("CSV 출력 폴더", csvFolder);
            EditorGUILayout.Space(8);

            // 데이터 미리보기 버튼
            if (GUILayout.Button("데이터 불러오기 (미리보기)", GUILayout.Height(26)))
                Preview();

            // 미리보기 결과 표시
            if (!string.IsNullOrEmpty(lastStatus))
            {
                EditorGUILayout.HelpBox(lastStatus,
                    lastStatusIsError ? MessageType.Error : MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("[ 내보내기 ]", EditorStyles.boldLabel);

            if (GUILayout.Button("전체 내보내기", GUILayout.Height(28)))
                ExportAll();

            EditorGUILayout.Space(4);
            if (GUILayout.Button($"진행 중인 모험  ({cachedOngoing}개)  → AdventureLog_Ongoing.csv"))
                Export(ExportType.Ongoing);
            if (GUILayout.Button($"완료된 모험     ({cachedCompleted}개)  → AdventureLog_Completed.csv"))
                Export(ExportType.Completed);
            if (GUILayout.Button($"모험 결과       ({cachedResults}개)   → AdventureLog_Results.csv"))
                Export(ExportType.Results);
            if (GUILayout.Button($"이벤트 결과 로그  ({cachedEvents}건)   → AdventureLog_EventResults.csv"))
                Export(ExportType.Events);
            if (GUILayout.Button($"예정/진행 이벤트  ({cachedProgressEvents}건)  → AdventureLog_Events.csv"))
                Export(ExportType.ProgressEvents);

            EditorGUILayout.EndScrollView();
        }

        // ─── 열거형 ────────────────────────────────────────────────────
        private enum ExportType { Ongoing, Completed, Results, Events, ProgressEvents }





        // ═══════════════════════════════════════════════════════════════
        // CSV 작성 — AdventureInstance 기반 (Play 모드)
        // ═══════════════════════════════════════════════════════════════

        #region Play 모드 CSV 작성

        private void WriteOngoing(List<AdventureInstance> list)
        {
            var rows = AdventureInstanceHdr();
            foreach (var a in list)
                rows.Add(AdventureInstanceRow(a));
            Write("AdventureLog_Ongoing", rows);
        }

        private void WriteCompleted(List<AdventureInstance> list)
        {
            var rows = AdventureInstanceHdr();
            foreach (var a in list)
                rows.Add(AdventureInstanceRow(a));
            Write("AdventureLog_Completed", rows);
        }

        private void WriteResults(List<AdventureResult> list)
        {
            var rows = Hdr(
                "adventureID", "adventurerID", "weaponID", "dungeonID",
                "isSuccess", "isDeath", "isGreatSuccess", "isRetreated",
                "isAdventurerTypeMatch", "isDungeonTypeMatch",
                "deathProtectionUsed", "weaponLost", "isDefaultWeapon",
                "goldReward", "reputationChange", "affectionChange",
                "materialDrops");
            foreach (var r in list)
                rows.Add(R(
                    r.adventureID, r.adventurerID, r.weaponID, r.dungeonID,
                    r.isSuccess.ToString(), r.isDeath.ToString(),
                    r.isGreatSuccess.ToString(), r.isRetreated.ToString(),
                    r.isAdventurerTypeMatch.ToString(), r.isDungeonTypeMatch.ToString(),
                    r.deathProtectionUsed.ToString(), r.weaponLost.ToString(), r.isDefaultWeapon.ToString(),
                    r.goldReward.ToString(), r.reputationChange.ToString(),
                    r.affectionChange.ToString(),
                    MaterialsToString(r.materialDrops)));
            Write("AdventureLog_Results", rows);
        }

        private void WriteEvents(List<AdventureInstance> ongoing, List<AdventureInstance> completed)
        {
            var rows = Hdr(
                "adventureID", "adventurerName", "dungeonName",
                "eventIndex", "eventType", "isSuccess",
                "goldReward", "successRateAtTime", "protectionActivated",
                "materialDrops");

            void AddRows(List<AdventureInstance> list)
            {
                foreach (var a in list)
                {
                    if (a.progress?.events == null) continue;
                    string advName  = a.adventurer?.Name ?? a.adventurer?.instanceID ?? "";
                    string dunName  = a.dungeon?.dungeonName ?? a.dungeon?.StaticID ?? "";
                    for (int i = 0; i < a.progress.events.Count; i++)
                    {
                        var e = a.progress.events[i]?.result;
                        if (e == null) continue;
                        rows.Add(R(
                            a.instanceID, advName, dunName,
                            i.ToString(),
                            e.eventType.ToString(), e.isSuccess.ToString(),
                            e.goldReward.ToString(),
                            e.successRateAtTime.ToString("F4", IC),
                            e.protectionActivated.ToString(),
                            MaterialsToString(e.materialDrops)));
                    }
                }
            }

            AddRows(ongoing);
            AddRows(completed);
            Write("AdventureLog_EventResults", rows);
        }

        private void WriteProgressEvents(List<AdventureInstance> ongoing, List<AdventureInstance> completed)
        {
            var rows = Hdr(
                "adventureID", "adventurerName", "dungeonName",
                "eventIndex", "eventDataID", "eventType",
                "isCompleted",
                "startTime", "cachedDurationMultiplier",
                "resultIsSuccess", "resultGoldReward",
                "resultSuccessRate", "resultProtectionActivated",
                "resultMaterialDrops");

            void AddRows(List<AdventureInstance> list)
            {
                foreach (var a in list)
                {
                    if (a.progress?.events == null) continue;
                    string advName = a.adventurer?.Name ?? a.adventurer?.instanceID ?? "";
                    string dunName = a.dungeon?.dungeonName ?? a.dungeon?.StaticID ?? "";
                    for (int i = 0; i < a.progress.events.Count; i++)
                    {
                        var e = a.progress.events[i];
                        if (e == null) continue;
                        var p = e.result;
                        rows.Add(R(
                            a.instanceID, advName, dunName,
                            i.ToString(),
                            e.eventData?.StaticID ?? "",
                            e.eventData?.eventType.ToString() ?? "",
                            (i < a.progress.currentEventIndex).ToString(),
                            e.startTime.ToString("F2", IC),
                            e.cachedDurationMultiplier.ToString("F4", IC),
                            p != null ? p.isSuccess.ToString()              : "",
                            p != null ? p.goldReward.ToString()             : "",
                            p != null ? p.successRateAtTime.ToString("F4", IC) : "",
                            p != null ? p.protectionActivated.ToString()    : "",
                            p != null ? MaterialsToString(p.materialDrops)  : ""));
                    }
                }
            }

            AddRows(ongoing);
            AddRows(completed);
            Write("AdventureLog_Events", rows);
        }

        private List<string[]> AdventureInstanceHdr() => Hdr(
            "instanceID", "adventurerInstanceID", "adventurerName",
            "weaponInstanceID", "weaponName",
            "dungeonID", "dungeonName",
            "startTime", "completedDay",
            "isCompleted", "isSuccess", "isGreatSuccess", "isDeath",
            "isRetreated", "isUsingDefaultWeapon",
            "accumulatedGold", "accumulatedBonusGold", "cumulativeModifier", "protectionCharges",
            "accumulatedMaterials", "accumulatedBonusMaterials", "weaponEffects");

        private string[] AdventureInstanceRow(AdventureInstance a)
        {
            bool isDef     = a.isUsingDefaultWeapon;
            string wpnName = isDef
                ? $"defaultWeapon_{a.weapon?.weaponData?.weaponType}"
                : (a.weapon?.weaponData?.weaponName ?? "");
            string wpnFx   = isDef ? "" : WeaponEffectsToString(a.weapon?.effects);

            return R(
                a.instanceID,
                a.adventurer?.instanceID ?? "",
                a.adventurer?.Name ?? "",
                a.weapon?.instanceID ?? "",
                wpnName,
                a.dungeon?.StaticID ?? "",
                a.dungeon?.dungeonName ?? "",
                a.startTime.ToString("F2", IC),
                a.completedDay.ToString(),
                a.isCompleted.ToString(), a.isSuccess.ToString(),
                a.isGreatSuccess.ToString(), a.isDeath.ToString(),
                a.isRetreated.ToString(), a.isUsingDefaultWeapon.ToString(),
                a.accumulatedGold.ToString(),
                a.accumulatedBonusGold.ToString(),
                a.cumulativeModifier.ToString("F4", IC),
                a.protectionCharges.ToString(),
                MaterialsToString(a.accumulatedMaterials),
                MaterialsToString(a.accumulatedBonusMaterials),
                wpnFx);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════
        // CSV 작성 — SaveData 기반 (에디터 모드)
        // ═══════════════════════════════════════════════════════════════

        #region 에디터 모드 CSV 작성 (SaveData 직접 읽기)

        private void ExportFromSaveData(ExportType? only = null)
        {
            string savePath = Path.Combine(Application.persistentDataPath, "gamedata.json");
            if (!File.Exists(savePath))
            {
                Debug.LogError($"[AdventureLogExporter] 세이브 파일 없음: {savePath}");
                return;
            }

            string json = File.ReadAllText(savePath);
            GameData pd = JsonUtility.FromJson<GameData>(json);
            if (pd == null)
            {
                Debug.LogError("[AdventureLogExporter] GameData 역직렬화 실패");
                return;
            }

            var maps = BuildLookupMaps(pd);

            if (only == null || only == ExportType.Ongoing)
                WriteSaveDataAdventures("AdventureLog_Ongoing", pd.ongoingAdventures, maps);
            if (only == null || only == ExportType.Completed)
                WriteSaveDataAdventures("AdventureLog_Completed", pd.completedAdventures, maps);
            if (only == null || only == ExportType.Results)
                WriteSaveDataResults(pd.completedAdventureResults, maps);
            if (only == null || only == ExportType.Events)
                WriteSaveDataEvents(pd.ongoingAdventures, pd.completedAdventures, maps);
            if (only == null || only == ExportType.ProgressEvents)
                WriteSaveDataProgressEvents(pd.ongoingAdventures, pd.completedAdventures, maps);

            cachedOngoing   = pd.ongoingAdventures?.Count ?? 0;
            cachedCompleted = pd.completedAdventures?.Count ?? 0;
            cachedResults   = pd.completedAdventureResults?.Count ?? 0;
            cachedEvents    =
                (pd.ongoingAdventures?.Sum(a => a.progress?.events?.Count(e => e?.result != null) ?? 0) ?? 0) +
                (pd.completedAdventures?.Sum(a => a.progress?.events?.Count(e => e?.result != null) ?? 0) ?? 0);

            lastStatus = $"(에디터) 로드 완료 — 진행중 {cachedOngoing}개 / 완료 {cachedCompleted}개 / " +
                         $"결과 {cachedResults}개 / 이벤트 {cachedEvents}건";
            lastStatusIsError = false;
        }

        private void WriteSaveDataAdventures(string fileName,
                                              List<AdventureInstanceSaveData> list,
                                              LookupMaps maps)
        {
            if (list == null) { Log($"{fileName}: 데이터 없음"); return; }

            var rows = Hdr(
                "instanceID", "adventurerInstanceID", "adventurerName",
                "weaponInstanceID", "weaponName",
                "dungeonDataID", "dungeonName",
                "startTime", "completedDay",
                "isCompleted", "isSuccess", "isGreatSuccess", "isDeath",
                "isRetreated", "isUsingDefaultWeapon",
                "accumulatedGold", "accumulatedBonusGold", "cumulativeModifier", "protectionCharges",
                "accumulatedMaterials", "accumulatedBonusMaterials", "weaponEffects");

            foreach (var a in list)
            {
                if (a == null) continue;
                string advName  = maps.adventurerNames.TryGetValue(a.adventurerInstanceID, out var an) ? an : "";
                bool   isDef    = a.isUsingDefaultWeapon;
                string wpnName;
                if (isDef)
                {
                    string defType = maps.adventurerDefaultWeaponType
                        .TryGetValue(a.adventurerInstanceID, out var dt) ? dt : "Unknown";
                    wpnName = $"defaultWeapon_{defType}";
                }
                else
                {
                    wpnName = maps.weaponNames.TryGetValue(a.weaponInstanceID, out var wn) ? wn : "";
                }
                string wpnFx    = isDef ? "" :
                    (maps.weaponEffects.TryGetValue(a.weaponInstanceID, out var wfx) ? wfx : "");
                string dunName  = maps.dungeonNames.TryGetValue(a.dungeonDataID, out var dn) ? dn : "";
                rows.Add(R(
                    a.instanceID, a.adventurerInstanceID, advName,
                    a.weaponInstanceID, wpnName,
                    a.dungeonDataID, dunName,
                    a.startTime.ToString("F2", IC),
                    a.completedDay.ToString(),
                    a.isCompleted.ToString(), a.isSuccess.ToString(),
                    a.isGreatSuccess.ToString(), a.isDeath.ToString(),
                    a.isRetreated.ToString(), a.isUsingDefaultWeapon.ToString(),
                    a.accumulatedGold.ToString(),
                    a.accumulatedBonusGold.ToString(),
                    a.cumulativeModifier.ToString("F4", IC),
                    a.protectionCharges.ToString(),
                    MaterialsToString(a.accumulatedMaterials),
                    MaterialsToString(a.accumulatedBonusMaterials),
                    wpnFx));
            }
            Write(fileName, rows);
        }

        private void WriteSaveDataResults(List<AdventureResult> list, LookupMaps maps)
        {
            if (list == null) { Log("AdventureLog_Results: 데이터 없음"); return; }

            var rows = Hdr(
                "adventureID", "adventurerID", "adventurerName",
                "weaponID", "weaponName",
                "dungeonID", "dungeonName",
                "isSuccess", "isDeath", "isGreatSuccess", "isRetreated",
                "isAdventurerTypeMatch", "isDungeonTypeMatch",
                "deathProtectionUsed", "weaponLost", "isDefaultWeapon",
                "goldReward", "reputationChange", "affectionChange",
                "materialDrops");

            foreach (var r in list)
            {
                if (r == null) continue;
                string advName = maps.adventurerNames.TryGetValue(r.adventurerID, out var an) ? an : "";
                string wpnName = maps.weaponNames.TryGetValue(r.weaponID, out var wn) ? wn : "";
                string dunName = maps.dungeonNames.TryGetValue(r.dungeonID, out var dn) ? dn : "";
                rows.Add(R(
                    r.adventureID, r.adventurerID, advName,
                    r.weaponID, wpnName,
                    r.dungeonID, dunName,
                    r.isSuccess.ToString(), r.isDeath.ToString(),
                    r.isGreatSuccess.ToString(), r.isRetreated.ToString(),
                    r.isAdventurerTypeMatch.ToString(), r.isDungeonTypeMatch.ToString(),
                    r.deathProtectionUsed.ToString(), r.weaponLost.ToString(), r.isDefaultWeapon.ToString(),
                    r.goldReward.ToString(), r.reputationChange.ToString(),
                    r.affectionChange.ToString(),
                    MaterialsToString(r.materialDrops)));
            }
            Write("AdventureLog_Results", rows);
        }

        private void WriteSaveDataEvents(List<AdventureInstanceSaveData> ongoing,
                                          List<AdventureInstanceSaveData> completed,
                                          LookupMaps maps)
        {
            var rows = Hdr(
                "adventureID", "adventurerInstanceID", "adventurerName",
                "dungeonDataID", "dungeonName",
                "eventIndex", "eventType", "isSuccess",
                "goldReward", "successRateAtTime", "protectionActivated",
                "materialDrops");

            void AddRows(List<AdventureInstanceSaveData> list)
            {
                if (list == null) return;
                foreach (var a in list)
                {
                    if (a?.progress?.events == null) continue;
                    string advName = maps.adventurerNames.TryGetValue(a.adventurerInstanceID, out var an) ? an : "";
                    string dunName = maps.dungeonNames.TryGetValue(a.dungeonDataID, out var dn) ? dn : "";
                    for (int i = 0; i < a.progress.events.Count; i++)
                    {
                        var e = a.progress.events[i]?.result;
                        if (e == null) continue;
                        rows.Add(R(
                            a.instanceID, a.adventurerInstanceID, advName,
                            a.dungeonDataID, dunName,
                            i.ToString(),
                            e.eventType.ToString(), e.isSuccess.ToString(),
                            e.goldReward.ToString(),
                            e.successRateAtTime.ToString("F4", IC),
                            e.protectionActivated.ToString(),
                            MaterialsToString(e.materialDrops)));
                    }
                }
            }

            AddRows(ongoing);
            AddRows(completed);
            Write("AdventureLog_EventResults", rows);
        }

        private void WriteSaveDataProgressEvents(List<AdventureInstanceSaveData> ongoing,
                                               List<AdventureInstanceSaveData> completed,
                                               LookupMaps maps)
        {
            var rows = Hdr(
                "adventureID", "adventurerInstanceID", "adventurerName",
                "dungeonDataID", "dungeonName",
                "eventIndex", "eventDataID", "eventType",
                "isCompleted",
                "startTime", "cachedDurationMultiplier",
                "resultIsSuccess", "resultGoldReward",
                "resultSuccessRate", "resultProtectionActivated",
                "resultMaterialDrops");

            void AddRows(List<AdventureInstanceSaveData> list)
            {
                if (list == null) return;
                foreach (var a in list)
                {
                    if (a?.progress?.events == null) continue;
                    string advName = maps.adventurerNames.TryGetValue(a.adventurerInstanceID, out var an) ? an : "";
                    string dunName = maps.dungeonNames.TryGetValue(a.dungeonDataID, out var dn) ? dn : "";
                    for (int i = 0; i < a.progress.events.Count; i++)
                    {
                        var e = a.progress.events[i];
                        if (e == null) continue;
                        var p = e.result;
                        rows.Add(R(
                            a.instanceID, a.adventurerInstanceID, advName,
                            a.dungeonDataID, dunName,
                            i.ToString(),
                            e.eventData?.StaticID ?? "",
                            e.eventData?.eventType.ToString() ?? "",
                            (i < a.progress.currentEventIndex).ToString(),
                            e.startTime.ToString("F2", IC),
                            e.cachedDurationMultiplier.ToString("F4", IC),
                            p != null ? p.isSuccess.ToString()                 : "",
                            p != null ? p.goldReward.ToString()                : "",
                            p != null ? p.successRateAtTime.ToString("F4", IC) : "",
                            p != null ? p.protectionActivated.ToString()       : "",
                            p != null ? MaterialsToString(p.materialDrops)     : ""));
                    }
                }
            }

            AddRows(ongoing);
            AddRows(completed);
            Write("AdventureLog_Events", rows);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════
        // LoadData / Export 오버라이드 — 에디터 모드 분기 처리
        // ═══════════════════════════════════════════════════════════════

        // Preview() — 에디터/Play 모드 공통 (CSV 출력 없이 카운트만 표시)
        private void Preview()
        {
            if (!Application.isPlaying)
            {
                string savePath = System.IO.Path.Combine(
                    Application.persistentDataPath, "gamedata.json");
                if (!System.IO.File.Exists(savePath))
                {
                    lastStatus = $"세이브 파일 없음: {savePath}";
                    lastStatusIsError = true;
                    return;
                }
                try
                {
                    string json = System.IO.File.ReadAllText(savePath);
                    GameData pd = JsonUtility.FromJson<GameData>(json);
                    if (pd == null) throw new Exception("GameData 역직렬화 실패");

                    cachedOngoing   = pd.ongoingAdventures?.Count ?? 0;
                    cachedCompleted = pd.completedAdventures?.Count ?? 0;
                    cachedResults   = pd.completedAdventureResults?.Count ?? 0;
                    cachedEvents    =
                        (pd.ongoingAdventures?.Sum(a => a.progress?.events?.Count(e => e?.result != null) ?? 0) ?? 0) +
                        (pd.completedAdventures?.Sum(a => a.progress?.events?.Count(e => e?.result != null) ?? 0) ?? 0);
                    cachedProgressEvents =
                        (pd.ongoingAdventures?.Sum(a => a.progress?.events?.Count ?? 0) ?? 0) +
                        (pd.completedAdventures?.Sum(a => a.progress?.events?.Count ?? 0) ?? 0);

                    lastStatus = $"(에디터) 세이브 파일 로드 완료 — " +
                                 $"진행중 {cachedOngoing}개 / 완료 {cachedCompleted}개 / " +
                                 $"결과 {cachedResults}개 / 이벤트결과 {cachedEvents}건 / 예정이벤트 {cachedProgressEvents}건";
                    lastStatusIsError = false;
                }
                catch (Exception e)
                {
                    lastStatus = $"로드 실패: {e.Message}";
                    lastStatusIsError = true;
                }
                return;
            }

            try
            {
                if (AdventureManager.Instance == null)
                    throw new InvalidOperationException("AdventureManager.Instance가 null");

                cachedOngoing   = AdventureManager.Instance.OngoingAdventures.Count;
                cachedCompleted = AdventureManager.Instance.CompletedAdventures.Count;
                cachedResults   = AdventureManager.Instance.CompletedResults.Count;
                cachedEvents    =
                    AdventureManager.Instance.OngoingAdventures
                        .Sum(a => a.progress?.events?.Count(e => e?.result != null) ?? 0) +
                    AdventureManager.Instance.CompletedAdventures
                        .Sum(a => a.progress?.events?.Count(e => e?.result != null) ?? 0);
                cachedProgressEvents =
                    AdventureManager.Instance.OngoingAdventures
                        .Sum(a => a.progress?.events?.Count ?? 0) +
                    AdventureManager.Instance.CompletedAdventures
                        .Sum(a => a.progress?.events?.Count ?? 0);

                lastStatus = $"로드 완료 — 진행중 {cachedOngoing}개 / 완료 {cachedCompleted}개 / " +
                             $"결과 {cachedResults}개 / 이벤트결과 {cachedEvents}건 / 예정이벤트 {cachedProgressEvents}건";
                lastStatusIsError = false;
            }
            catch (Exception e)
            {
                lastStatus = $"로드 실패: {e.Message}";
                lastStatusIsError = true;
            }
        }

        // Export() 재정의 — 에디터/Play 모드 분기
        private void Export(ExportType type)
        {
            if (!Application.isPlaying)
            {
                ExportFromSaveData(type);
                return;
            }

            try
            {
                var ongoing   = AdventureManager.Instance.OngoingAdventures.ToList();
                var completed = AdventureManager.Instance.CompletedAdventures.ToList();
                var results   = AdventureManager.Instance.CompletedResults.ToList();

                switch (type)
                {
                    case ExportType.Ongoing:   WriteOngoing(ongoing);           break;
                    case ExportType.Completed: WriteCompleted(completed);        break;
                    case ExportType.Results:   WriteResults(results);            break;
                    case ExportType.Events:         WriteEvents(ongoing, completed);         break;
                    case ExportType.ProgressEvents: WriteProgressEvents(ongoing, completed); break;
                }
                Preview();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdventureLogExporter] 내보내기 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // ExportAll() 재정의
        private void ExportAll()
        {
            if (!Application.isPlaying)
            {
                ExportFromSaveData(null);
                return;
            }

            try
            {
                var ongoing   = AdventureManager.Instance.OngoingAdventures.ToList();
                var completed = AdventureManager.Instance.CompletedAdventures.ToList();
                var results   = AdventureManager.Instance.CompletedResults.ToList();

                WriteOngoing(ongoing);
                WriteCompleted(completed);
                WriteResults(results);
                WriteEvents(ongoing, completed);
                WriteProgressEvents(ongoing, completed);
                Log("전체 내보내기 완료");
                Preview();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdventureLogExporter] 내보내기 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 에디터 모드 이름 조회 — SaveData + AssetDatabase 기반
        // ─────────────────────────────────────────────────────────────
        // PlayerData에서 instanceID → 이름 딕셔너리를 미리 구축
        // DataManager 없이 AssetDatabase로 SO를 직접 로드해 이름 추출
        // ═══════════════════════════════════════════════════════════════

        private class LookupMaps
        {
            // adventurerInstanceID → adventurerName
            public Dictionary<string, string> adventurerNames  = new Dictionary<string, string>();
            // weaponInstanceID     → weaponName
            public Dictionary<string, string> weaponNames      = new Dictionary<string, string>();
            // weaponInstanceID     → 효과 문자열 (isDefaultWeapon이면 null)
            public Dictionary<string, string> weaponEffects    = new Dictionary<string, string>();
            // weaponInstanceID     → isDefaultWeapon
            public Dictionary<string, bool>   weaponIsDefault  = new Dictionary<string, bool>();
            // adventurerInstanceID  → defaultWeaponType (에디터 모드 defaultWeapon 이름용)
            public Dictionary<string, string> adventurerDefaultWeaponType = new Dictionary<string, string>();
            // dungeonDataID (staticID) → dungeonName
            public Dictionary<string, string> dungeonNames     = new Dictionary<string, string>();
        }

        private LookupMaps BuildLookupMaps(GameData pd)
        {
            var maps = new LookupMaps();

            // ── 모험가 이름 매핑 ──────────────────────────────────────
            // activeAdventurerInstances[].instanceID → adventurerDataID → SO.adventurerName
            var adventurerSOs = AssetDatabase.FindAssets("t:AdventurerData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<AdventurerData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(so => so != null)
                .ToDictionary(so => so.StaticID, so => so.adventurerName);

            if (pd.namedAdventurerInstances != null)
            {
                foreach (var adv in pd.namedAdventurerInstances)
                {
                    if (adv == null) continue;
                    string name = adventurerSOs.TryGetValue(adv.adventurerDataID, out var n) ? n : adv.adventurerDataID;
                    maps.adventurerNames[adv.instanceID]              = name;
                    maps.adventurerDefaultWeaponType[adv.instanceID]  = adv.defaultWeaponType.ToString();
                }
            }

            // ── 무기 이름 매핑 ───────────────────────────────────────
            // ownedWeapons[].instanceID → weaponDataID → SO.weaponName
            var weaponSOs = AssetDatabase.FindAssets("t:WeaponData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(so => so != null)
                .ToDictionary(so => so.StaticID, so => so.weaponName);

            if (pd.ownedWeapons != null)
            {
                foreach (var wpn in pd.ownedWeapons)
                {
                    if (wpn == null) continue;

                    maps.weaponIsDefault[wpn.instanceID] = wpn.isDefaultWeapon;

                    if (wpn.isDefaultWeapon)
                    {
                        // defaultWeapon: 무기명은 타입 기반으로 구성, 효과는 빈 문자열
                        var so = weaponSOs.TryGetValue(wpn.weaponDataID, out var sn) ? sn : null;
                        // weaponType은 SO에서 읽을 수 없으므로 weaponDataID 기반 표기
                        maps.weaponNames[wpn.instanceID]   = $"defaultWeapon_{wpn.weaponDataID}";
                        maps.weaponEffects[wpn.instanceID] = "";
                    }
                    else
                    {
                        string name = weaponSOs.TryGetValue(wpn.weaponDataID, out var n) ? n : wpn.weaponDataID;
                        maps.weaponNames[wpn.instanceID]   = name;
                        maps.weaponEffects[wpn.instanceID] = EffectSaveDataToString(wpn.effects);
                    }
                }
            }

            // ── 던전 이름 매핑 ───────────────────────────────────────
            // staticID 그대로 SO 로드
            var dungeonSOs = AssetDatabase.FindAssets("t:DungeonData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DungeonData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(so => so != null);

            foreach (var d in dungeonSOs)
                maps.dungeonNames[d.StaticID] = d.dungeonName;

            return maps;
        }

        // ═══════════════════════════════════════════════════════════════
        // 유틸리티
        // ═══════════════════════════════════════════════════════════════

        #region 유틸

        private static readonly System.Globalization.CultureInfo IC =
            System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>
        /// WeaponEffect 목록 → "effectType(currentValue)|effectType(currentValue)" 형태 문자열
        /// targetGrade/Stat/ArmorType이 0이 아닌 경우 타입명도 함께 표시
        /// 예: ArmorTypeBonus:Heavy(0.15)|DungeonGradeBonus:Rare(0.08)
        /// </summary>
        private string WeaponEffectsToString(List<WeaponEffect> list)
        {
            if (list == null || list.Count == 0) return "";

            var parts = new System.Collections.Generic.List<string>();
            foreach (var e in list)
            {
                string target = "";
                if (e.effectData.targetArmorType != 0)
                    target = $":{(ArmorType)e.effectData.targetArmorType}";
                else if (e.effectData.targetGrade != 0)
                    target = $":{(Grade)e.effectData.targetGrade}";
                else if (e.effectData.targetStat != 0)
                    target = $":{(AdventurerStat)e.effectData.targetStat}";

                parts.Add($"{e.effectData.effectType}{target}({e.currentValue.ToString("F4", IC)})");
            }
            return string.Join("|", parts);
        }

        /// <summary>
        /// WeaponEffectSaveData 리스트 → 효과 문자열 (에디터 모드용)
        /// 형식: effectType:target(currentValue)|...
        /// </summary>
        private string EffectSaveDataToString(List<WeaponEffectSaveData> list)
        {
            if (list == null || list.Count == 0) return "";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var e in list)
            {
                string target = "";
                if (e.targetArmorType != 0)
                    target = $":{(ArmorType)e.targetArmorType}";
                else if (e.targetGrade != 0)
                    target = $":{(Grade)e.targetGrade}";
                else if (e.targetStat != 0)
                    target = $":{(AdventurerStat)e.targetStat}";
                parts.Add($"{e.effectType}{target}({e.currentValue.ToString("F4", IC)})");
            }
            return string.Join("|", parts);
        }

        /// <summary>
        /// materialDrops → "materialID:quantity|materialID:quantity" 형태 문자열
        /// </summary>
        private string MaterialsToString(List<MaterialInstance> list)
        {
            if (list == null || list.Count == 0) return "";

            var merged = new Dictionary<string, int>();
            foreach (var m in list)
            {
                if (m == null || string.IsNullOrEmpty(m.materialDataID)) continue;
                if (merged.ContainsKey(m.materialDataID))
                    merged[m.materialDataID] += m.quantity;
                else
                    merged[m.materialDataID] = m.quantity;
            }

            return string.Join("|", merged.Select(kv => $"{kv.Key}:{kv.Value}"));
        }

        private void EnsureDir(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            path = path.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
            string name   = System.IO.Path.GetFileName(path);
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private string CsvPath(string name)
            => System.IO.Path.Combine(csvFolder, $"{name}.csv").Replace("\\", "/");

        private List<string[]> Hdr(params string[] h) => new List<string[]> { h };
        private string[] R(params string[] v) => v;

        private void Write(string name, List<string[]> rows)
        {
            EnsureDir(csvFolder);
            var sb = new StringBuilder();
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(Esc)));
            File.WriteAllText(CsvPath(name), sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Log($"내보내기: {CsvPath(name)}  ({rows.Count - 1}행)");
        }

        private string Esc(string s)
        {
            if (s == null) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        private void Log(string msg) => Debug.Log($"[AdventureLogExporter] {msg}");

        #endregion
    }
}
#endif