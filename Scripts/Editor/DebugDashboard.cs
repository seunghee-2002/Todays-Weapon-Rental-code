// Assets/_Projects/Scripts/Editor/DebugDashboard.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    public class DebugDashboard : EditorWindow
    {
        #region 탭 정의

        private enum Tab { GameData, Inventory, Visitors, Adventures }
        private Tab currentTab = Tab.GameData;

        private readonly string[] tabLabels = { "GameData", "Inventory", "Visitors", "Adventures" };

        #endregion

        #region 접기/펼치기 상태 — GameData 섹션

        private bool foldBasicInfo        = true;
        private bool foldAdventureStats   = true;
        private bool foldInventorySummary = true;
        private bool foldAdventurerStatus = true;
        private bool foldQuest            = true;
        private bool foldEtc              = true;
        private bool foldQuestBoard       = true;

        #endregion

        #region 접기/펼치기 상태 — Inventory 무기별 부가효과

        // instanceID → 부가효과 펼침 여부
        private readonly Dictionary<string, bool> weaponEffectFold = new Dictionary<string, bool>();

        #endregion

        #region 접기/펼치기 상태 — Adventures 행별

        // instanceID → 이벤트 결과 펼침 여부
        private readonly Dictionary<string, bool> adventureEventFold  = new Dictionary<string, bool>();
        // instanceID → 무기 부가효과 펼침 여부
        private readonly Dictionary<string, bool> adventureEffectFold = new Dictionary<string, bool>();

        #endregion

        #region 검색 / 인벤토리 필터

        private string inventorySearch = "";
        private string visitorSearch   = "";
        private string adventureSearch = "";

        private enum WeaponFilter { All, Rented, Available }
        private WeaponFilter weaponFilter = WeaponFilter.All;
        private readonly string[] weaponFilterLabels = { "전체", "대여중", "미대여" };

        #endregion

        #region 스크롤

        private Vector2 scrollGameData;
        private Vector2 scrollInventory;
        private Vector2 scrollVisitors;
        private Vector2 scrollAdventures;

        #endregion

        #region 자동 갱신

        private bool autoRefresh = true;
        private double lastRefreshTime;
        private const double RefreshInterval = 0.5;

        #endregion

        #region 열기

        [MenuItem("Tools/Today's Weapon Rental/Debug Dashboard")]
        public static void Open()
        {
            var window = GetWindow<DebugDashboard>("Debug Dashboard");
            window.minSize = new Vector2(420f, 500f);
        }

        #endregion

        #region Unity 이벤트

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!autoRefresh) return;
            if (!Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup - lastRefreshTime < RefreshInterval) return;

            lastRefreshTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            DrawToolbar();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("런타임 중에만 표시됩니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);

            switch (currentTab)
            {
                case Tab.GameData:  DrawGameDataTab();  break;
                case Tab.Inventory:   DrawInventoryTab();   break;
                case Tab.Visitors:    DrawVisitorsTab();    break;
                case Tab.Adventures:  DrawAdventuresTab();  break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, tabLabels, EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            autoRefresh = GUILayout.Toggle(autoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(50f));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                Repaint();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region GameData 탭

        private void DrawGameDataTab()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.GameData == null)
            {
                EditorGUILayout.HelpBox("GameManager 또는 PlayerData를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            var pd = gm.GameData;
            scrollGameData = EditorGUILayout.BeginScrollView(scrollGameData);

            if (DrawFoldout(ref foldBasicInfo, "기본 정보"))
            {
                DrawField("현재 날짜", pd.currentDay.ToString());
                DrawField("현재 시간", $"{pd.currentHour:D2}:{pd.currentMinute:D2}");
                DrawField("골드", pd.gold.ToString("N0"));
                DrawField("평판", pd.reputation.ToString("N0"));
                DrawField("유산 포인트", (LegacyManager.Instance?.LegacyPoints ?? 0).ToString());
            }

            EditorGUILayout.Space(4f);

            if (DrawFoldout(ref foldAdventureStats, "모험 통계"))
            {
                DrawField("총 성공", pd.totalSuccessfulAdventures.ToString());
                DrawField("총 실패", pd.totalFailedAdventures.ToString());
                DrawField("총 보너스 매칭", pd.totalBonusMatches.ToString());
            }

            EditorGUILayout.Space(4f);

            if (DrawFoldout(ref foldInventorySummary, "인벤토리 요약"))
            {
                DrawField("보유 무기", pd.ownedWeapons?.Count.ToString() ?? "0");
                DrawField("보유 재료 종류", pd.ownedMaterials?.Count.ToString() ?? "0");
                DrawField("보유 액티브 아이템", pd.ownedActiveItems?.Count.ToString() ?? "0");
            }

            EditorGUILayout.Space(4f);

            if (DrawFoldout(ref foldAdventurerStatus, "모험가 현황"))
            {
                DrawField("전체 인스턴스", pd.namedAdventurerInstances?.Count.ToString() ?? "0");
                DrawField("사망한 모험가", pd.namedAdventurerInstances?.Count(a => !a.isAlive).ToString() ?? "0");
            }

            EditorGUILayout.Space(4f);

            if (DrawFoldout(ref foldQuest, "퀘스트"))
            {
                DrawField("현재 주차", pd.currentWeek.ToString());
                DrawField("누적 벌금", pd.totalFinePaid.ToString("N0"));
            }

            EditorGUILayout.Space(4f);

            if (DrawFoldout(ref foldEtc, "기타"))
            {
                DrawField("튜토리얼 완료(회차)", pd.hasReceivedTutorialWeapons ? "완료" : "미완료");
                DrawField("마지막 대장장이 요청", pd.lastRequestedBlacksmithType.ToString());

                var legacy = LegacyManager.Instance?.GetLegacyData();
                DrawField("튜토리얼 완주(영구)", legacy != null && legacy.hasCompletedTutorial ? "완주" : "미완주");
                if (legacy != null && GUILayout.Button("튜토리얼 완주 플래그 리셋"))
                {
                    legacy.hasCompletedTutorial = false;
                    LegacyManager.Instance.SaveLegacy();
                    Debug.Log("[DebugDashboard] 튜토리얼 완주 플래그 리셋");
                }
            }

            EditorGUILayout.Space(4f);

            if (DrawFoldout(ref foldQuestBoard, "의뢰판"))
                DrawQuestBoardContent(pd);

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Inventory 탭

        private void DrawInventoryTab()
        {
            var im = InventoryManager.Instance;
            if (im == null)
            {
                EditorGUILayout.HelpBox("InventoryManager를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            inventorySearch = DrawSearchField(inventorySearch, "무기 이름 검색");
            weaponFilter    = (WeaponFilter)GUILayout.Toolbar((int)weaponFilter, weaponFilterLabels);
            EditorGUILayout.Space(4f);

            var allWeapons = im.GetAllWeapons();

            IEnumerable<WeaponInstance> filtered = allWeapons;
            if (weaponFilter == WeaponFilter.Rented)
                filtered = allWeapons.Where(w => w.isRented);
            else if (weaponFilter == WeaponFilter.Available)
                filtered = allWeapons.Where(w => !w.isRented);

            if (!string.IsNullOrEmpty(inventorySearch))
                filtered = filtered.Where(w => w.weaponData?.weaponName.Contains(inventorySearch) == true);

            var weapons = filtered.ToList();

            EditorGUILayout.LabelField($"무기 {weapons.Count}개", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            scrollInventory = EditorGUILayout.BeginScrollView(scrollInventory);

            if (weapons.Count == 0)
                EditorGUILayout.LabelField("표시할 무기 없음", EditorStyles.centeredGreyMiniLabel);
            else
                foreach (var w in weapons)
                    DrawWeaponRow(w);

            EditorGUILayout.EndScrollView();
        }

        private void DrawWeaponRow(WeaponInstance w)
        {
            string id = w.instanceID ?? "";
            if (!weaponEffectFold.ContainsKey(id))
                weaponEffectFold[id] = false;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            string rentedLabel = w.isRented ? " [대여중]" : "";
            string name        = w.weaponData != null ? w.weaponData.weaponName : "(unknown)";
            EditorGUILayout.LabelField($"{name}{rentedLabel}", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawInlineField("등급", UITranslator.GetString(w.currentGrade));
            DrawInlineField("타입", UITranslator.GetString(w.weaponData.weaponType));
            DrawInlineField("강화", $"+{w.enforceLevel}");
            EditorGUILayout.EndHorizontal();

            DrawField("StaticID", w.weaponData?.StaticID ?? "-");
            DrawField("재부여 횟수", w.rerollCount.ToString());

            if (w.isRented && !string.IsNullOrEmpty(w.rentedToAdventurerID))
                DrawField("대여 모험가 ID", w.rentedToAdventurerID[..Mathf.Min(8, w.rentedToAdventurerID.Length)] + "...");

            // 부가효과 — 기본 닫힘, 클릭으로 열기
            if (w.effects != null && w.effects.Count > 0)
            {
                weaponEffectFold[id] = EditorGUILayout.Foldout(
                    weaponEffectFold[id],
                    $"부가효과 ({w.effects.Count}개)",
                    true);

                if (weaponEffectFold[id])
                {
                    foreach (var effect in w.effects)
                    {
                        EditorGUILayout.LabelField(
                            $"  • {GetEffectDisplayText(effect)}",
                            EditorStyles.miniLabel);
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        #endregion

        #region Visitors 탭

        private void DrawVisitorsTab()
        {
            var vm = VisitorManager.Instance;
            if (vm == null)
            {
                EditorGUILayout.HelpBox("VisitorManager를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            visitorSearch = DrawSearchField(visitorSearch, "모험가 이름 검색");

            var visitors = vm.ActiveVisitors;
            List<VisitorNPC> filtered = string.IsNullOrEmpty(visitorSearch)
                ? visitors
                : visitors.Where(v =>
                    v.adventurerInstance?.adventurerData?.adventurerName.Contains(visitorSearch) == true ||
                    v.visitorType.ToString().Contains(visitorSearch)).ToList();

            EditorGUILayout.LabelField($"현재 방문자 {filtered.Count}명", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            scrollVisitors = EditorGUILayout.BeginScrollView(scrollVisitors);

            if (filtered.Count == 0)
                EditorGUILayout.LabelField("방문자 없음", EditorStyles.centeredGreyMiniLabel);
            else
                foreach (var v in filtered)
                    DrawVisitorRow(v);

            EditorGUILayout.EndScrollView();
        }

        private void DrawVisitorRow(VisitorNPC v)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            string displayName = v.visitorType == VisitorType.Adventurer && v.adventurerInstance != null
                ? v.adventurerInstance.adventurerData?.adventurerName ?? "(이름 없음)"
                : v.visitorType.ToString();

            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawInlineField("타입", UITranslator.GetString(v.visitorType));
            DrawInlineField("상호작용", v.isInteracting ? "중" : "-");
            DrawInlineField("퇴장 중", v.isLeaving ? "O" : "X");
            EditorGUILayout.EndHorizontal();

            if (v.visitorType == VisitorType.Adventurer && v.adventurerInstance != null)
            {
                var ai = v.adventurerInstance;
                DrawField("StaticID", ai.adventurerData?.StaticID ?? "-");
                DrawField("네임드 여부", ai.isNamed ? "네임드" : "일반");

                EditorGUILayout.BeginHorizontal();
                DrawInlineField("STR", ai.STR.ToString());
                DrawInlineField("DEX", ai.DEX.ToString());
                DrawInlineField("INT", ai.INT.ToString());
                DrawInlineField("LUK", ai.LUK.ToString());
                EditorGUILayout.EndHorizontal();

                if (ai.isNamed)
                {
                    DrawField("호감도", ai.affection.ToString());
                    DrawField("생존", ai.isAlive ? "생존" : "사망");
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        #endregion

        #region Adventures 탭

        private void DrawAdventuresTab()
        {
            var am = AdventureManager.Instance;
            if (am == null)
            {
                EditorGUILayout.HelpBox("AdventureManager를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            adventureSearch = DrawSearchField(adventureSearch, "모험가/던전 이름 검색");

            var ongoing   = am.OngoingAdventures;
            var completed = am.CompletedAdventures;

            List<AdventureInstance> filteredOngoing;
            List<AdventureInstance> filteredCompleted;

            if (string.IsNullOrEmpty(adventureSearch))
            {
                filteredOngoing   = ongoing;
                filteredCompleted = completed;
            }
            else
            {
                filteredOngoing   = ongoing.Where(a => MatchesAdventureSearch(a, adventureSearch)).ToList();
                filteredCompleted = completed.Where(a => MatchesAdventureSearch(a, adventureSearch)).ToList();
            }

            scrollAdventures = EditorGUILayout.BeginScrollView(scrollAdventures);

            EditorGUILayout.LabelField($"진행 중 ({filteredOngoing.Count}개)", EditorStyles.boldLabel);
            if (filteredOngoing.Count == 0)
                EditorGUILayout.LabelField("진행 중인 모험 없음", EditorStyles.centeredGreyMiniLabel);
            else
                foreach (var a in filteredOngoing)
                    DrawAdventureRow(a);

            EditorGUILayout.Space(6f);

            EditorGUILayout.LabelField($"완료 — 미확인 {filteredCompleted.Count}개", EditorStyles.boldLabel);
            if (filteredCompleted.Count == 0)
                EditorGUILayout.LabelField("완료된 모험 없음", EditorStyles.centeredGreyMiniLabel);
            else
                foreach (var a in filteredCompleted)
                    DrawAdventureRow(a);

            EditorGUILayout.EndScrollView();
        }

        private bool MatchesAdventureSearch(AdventureInstance a, string search)
        {
            if (a.adventurer?.adventurerData?.adventurerName.Contains(search) == true) return true;
            if (a.dungeon?.dungeonName.Contains(search) == true) return true;
            return false;
        }

        private void DrawAdventureRow(AdventureInstance a)
        {
            string id = a.instanceID ?? "";
            if (!adventureEventFold.ContainsKey(id))  adventureEventFold[id]  = false;
            if (!adventureEffectFold.ContainsKey(id)) adventureEffectFold[id] = false;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            string adventurerName = a.adventurer?.adventurerData?.adventurerName ?? "(없음)";
            string dungeonName    = a.dungeon?.dungeonName ?? "(없음)";
            EditorGUILayout.LabelField($"{adventurerName} → {dungeonName}", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawInlineField("완료", a.isCompleted ? "O" : "X");
            DrawInlineField("무기", a.weapon?.weaponData?.weaponName ?? "-");
            EditorGUILayout.EndHorizontal();

            if (a.isCompleted)
            {
                string resultLabel = a.isGreatSuccess ? "대성공" :
                                     a.isSuccess      ? "성공"   :
                                     a.isRetreated    ? "후퇴"   :
                                     a.isDeath        ? "사망"   : "실패";
                DrawField("결과", resultLabel);
            }

            DrawField("누적 골드", $"{a.accumulatedGold:N0}  (+보너스 {a.accumulatedBonusGold:N0})");

            // 획득 재료
            var allMaterials = new List<MaterialInstance>();
            if (a.accumulatedMaterials != null)     allMaterials.AddRange(a.accumulatedMaterials);
            if (a.accumulatedBonusMaterials != null) allMaterials.AddRange(a.accumulatedBonusMaterials);

            if (allMaterials.Count > 0)
            {
                EditorGUILayout.LabelField("획득 재료", EditorStyles.miniBoldLabel);
                foreach (var m in allMaterials)
                {
                    var matData = DataManager.Instance?.GetMaterial(m.materialDataID);
                    string matName = matData?.materialName ?? m.materialDataID;
                    EditorGUILayout.LabelField($"  • {matName} x{m.quantity}", EditorStyles.miniLabel);
                }
            }

            // 이벤트 진행
            int eventCount = a.progress?.events?.Count ?? 0;
            int currentIdx = a.progress?.currentEventIndex ?? 0;
            DrawField("이벤트 진행", $"{currentIdx} / {eventCount}");

            // 이벤트 결과 — 토글 (기본 닫힘). 시작 시 전체 해결되어 events[i].result로 1:1 보유.
            var resolved = a.progress?.events?.Where(e => e?.result != null).ToList();
            if (resolved != null && resolved.Count > 0)
            {
                adventureEventFold[id] = EditorGUILayout.Foldout(
                    adventureEventFold[id],
                    $"이벤트 결과 ({resolved.Count}건)",
                    true);

                if (adventureEventFold[id])
                {
                    foreach (var ev in resolved)
                    {
                        var er = ev.result;
                        string mark = er.isSuccess ? "✓" : "✗";
                        string prot = er.protectionActivated ? " [보호 발동]" : "";
                        EditorGUILayout.LabelField(
                            $"  {mark} {er.eventType}  골드+{er.goldReward}{prot}",
                            EditorStyles.miniLabel);
                    }
                }
            }

            // 무기 부가효과 — 토글 (기본 닫힘, 조건 충족 ★)
            var weaponEffects = a.weapon?.effects;
            if (weaponEffects != null && weaponEffects.Count > 0)
            {
                adventureEffectFold[id] = EditorGUILayout.Foldout(
                    adventureEffectFold[id],
                    $"무기 부가효과 ({weaponEffects.Count}개)",
                    true);

                if (adventureEffectFold[id])
                {
                    foreach (var effect in weaponEffects)
                    {
                        bool met  = AdventureManager.Instance.IsEffectConditionMet(effect, a.adventurer, a.dungeon, a.effectiveArmorType);
                        string mark = met ? "★" : "  ";
                        EditorGUILayout.LabelField(
                            $"  {mark} {GetEffectDisplayText(effect)}",
                            EditorStyles.miniLabel);
                    }

                    if (a.goldPreservationTriggered)
                        EditorGUILayout.LabelField("  ★ FailGoldBonus 발동됨", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        #endregion

        #region 공통 UI 헬퍼

        /// <summary>접기/펼치기 헤더. 펼쳐진 상태면 true 반환.</summary>
        private static bool DrawFoldout(ref bool state, string title)
        {
            state = EditorGUILayout.Foldout(state, title, true, EditorStyles.foldoutHeader);
            return state;
        }

        private static void DrawField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(130f));
            EditorGUILayout.LabelField(value);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawInlineField(string label, string value)
        {
            EditorGUILayout.LabelField($"{label}: {value}", GUILayout.Width(90f));
        }

        private static string DrawSearchField(string current, string placeholder)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("검색", GUILayout.Width(36f));
            string result = EditorGUILayout.TextField(current);
            if (string.IsNullOrEmpty(result))
                DrawPlaceholder(placeholder);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
            return result;
        }

        private static void DrawPlaceholder(string text)
        {
            var lastRect = GUILayoutUtility.GetLastRect();
            var style = new GUIStyle(EditorStyles.label)
            {
                normal    = { textColor = Color.grey },
                fontStyle = FontStyle.Italic
            };
            GUI.Label(lastRect, text, style);
        }

        #endregion

        #region 의뢰판 콘텐츠

        private static void DrawQuestBoardContent(GameData pd)
        {
            var board = pd.dailyQuestBoardData;

            if (board == null || board.generatedDay == 0)
            {
                EditorGUILayout.LabelField("의뢰판 미생성", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            DrawField("생성 일자",    $"Day {board.generatedDay}");
            DrawField("새로고침 횟수", board.refreshCount.ToString());
            DrawField("확정 여부",    board.isConfirmed ? "확정됨" : "미확정");

            if (!board.isConfirmed)
            {
                var poolIDs = board.poolDungeonIDs;
                if (poolIDs == null || poolIDs.Count == 0)
                {
                    EditorGUILayout.LabelField("  후보 던전 없음", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"후보 던전 ({poolIDs.Count}개)", EditorStyles.miniBoldLabel);
                    foreach (var id in poolIDs)
                    {
                        var dungeon = DataManager.Instance?.GetDungeon(id);
                        string name = dungeon != null ? $"{dungeon.dungeonName} ({dungeon.grade})" : id;
                        EditorGUILayout.LabelField($"  • {name}", EditorStyles.miniLabel);
                    }
                }
            }
            else
            {
                var selectedIDs  = board.selectedDungeonIDs;
                var highlightedIDs = board.highlightedDungeonIDs;

                if (selectedIDs == null || selectedIDs.Count == 0)
                {
                    EditorGUILayout.LabelField("  확정 던전 없음", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"확정 던전 ({selectedIDs.Count}개)", EditorStyles.miniBoldLabel);
                    foreach (var id in selectedIDs)
                    {
                        var dungeon = DataManager.Instance?.GetDungeon(id);
                        string name   = dungeon != null ? $"{dungeon.dungeonName} ({dungeon.grade})" : id;
                        string suffix = highlightedIDs != null && highlightedIDs.Contains(id) ? "  ★2배" : "";
                        EditorGUILayout.LabelField($"  • {name}{suffix}", EditorStyles.miniLabel);
                    }
                }
            }
        }

        #endregion

        #region 부가효과 텍스트

        private static string GetEffectDisplayText(WeaponEffect effect)
        {
            return effect.effectData.effectType switch
            {
                WeaponEffectType.DungeonGradeBonus        => $"던전등급({(Grade)effect.effectData.targetGrade}) 성공률 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.StatBonus                => $"{(AdventurerStat)effect.effectData.targetStat} +{effect.currentValue:F0}",
                WeaponEffectType.ArmorTypeBonus           => $"방어타입({(ArmorType)effect.effectData.targetArmorType}) 성공률 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.WeaponTypeMatchBonus     => $"무기 보정치 x{effect.currentValue:F2}",
                WeaponEffectType.AllStatBonus             => $"모든 스탯 +{effect.currentValue:F0}",
                WeaponEffectType.GreatSuccessBonus        => $"대성공 확률 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.RetreatPrevention        => $"시작 시 보호 +{Mathf.RoundToInt(effect.currentValue)}회",
                WeaponEffectType.DoubleReward             => $"이벤트 2배 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.BattleGoldBonus          => $"일반전투 골드 +{effect.currentValue * 100f:F0}%",
                WeaponEffectType.MiniBossGoldBonus        => $"미니보스 골드 +{effect.currentValue * 100f:F0}%",
                WeaponEffectType.BossGoldBonus            => $"보스 골드 +{effect.currentValue * 100f:F0}%",
                WeaponEffectType.TreasureGoldBonus        => $"보물상자 골드 +{effect.currentValue * 100f:F0}%",
                WeaponEffectType.AllGoldBonus             => $"모든 골드 +{effect.currentValue * 100f:F0}%",
                WeaponEffectType.MaterialAmountBonus      => $"재료 획득량 +{Mathf.RoundToInt(effect.currentValue)}개",
                WeaponEffectType.RestChanceBonus          => $"휴식 확률 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.TreasureChestChanceBonus => $"보물상자 확률 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.RareDropChanceBonus      => $"아이템 이벤트 확률 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.FailGoldBonus            => $"실패 골드 보존 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.TrapNegation             => $"함정 무효화 {effect.currentValue * 100f:F1}%",
                WeaponEffectType.SpecialMaterialChance    => $"특수 재료 확률 +{effect.currentValue * 100f:F1}%",
                WeaponEffectType.EnforceMaterialBonus     => $"진화 재료 +{Mathf.RoundToInt(effect.currentValue)}개",
                WeaponEffectType.EventCountBonus          => $"이벤트 수 +{Mathf.RoundToInt(effect.currentValue)}개",
                WeaponEffectType.AdventureTimeReduction   => $"이벤트 시간 -{effect.currentValue * 100f:F1}%",
                _                                         => effect.effectData.effectType.ToString()
            };
        }

        #endregion
    }
}
#endif