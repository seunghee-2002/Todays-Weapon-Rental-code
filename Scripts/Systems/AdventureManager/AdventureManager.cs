// Scripts/Systems/AdventureManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    public partial class AdventureManager : BaseManager<AdventureManager>
    {
        public event Action<AdventureInstance> OnAdventureStarted;
        public event Action<AdventureInstance, AdventureResult> OnAdventureCompleted;
        public event Action<AdventureInstance, AdventureEvent> OnAdventureEventTriggerStarted;

        [SerializeField] private List<AdventureInstance> ongoingAdventures = new List<AdventureInstance>();
        [SerializeField] private List<AdventureInstance> completedAdventures = new List<AdventureInstance>();
        [SerializeField] private List<AdventureResult> completedResults = new List<AdventureResult>();

        // 로드 중 복원에 실패해 드롭된 모험의 저장 데이터.
        // GameManager의 로드 후 sanity pass(RepairDroppedAdventureReferences)에서 잠금 해제에 사용
        private readonly List<(AdventureInstanceSaveData saveData, string reason)> droppedAdventureSaves = new();
        
        public List<AdventureInstance> OngoingAdventures => ongoingAdventures;
        public List<AdventureInstance> CompletedAdventures => completedAdventures;
        public List<AdventureResult> CompletedResults => completedResults;

        private AdventureConfig Config => ConfigManager.Instance.Adventure;
        private TraitConfig TraitCfg => ConfigManager.Instance.Trait;
        
        #region 초기화

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged += OnTimeChanged;
                TimeManager.Instance.OnDayChanged += OnDayChanged;
            }
        }

        public void Initialize(GameData gameData)
        {
            LoadFromGameData(gameData);
        }

        private void OnTimeChanged(int hour, int minute)
        {
            UpdateAdventures();
        }

        private void OnDayChanged(int day)
        {
            ResetHeraldReport();
            TrimResultLog();
        }

        /// <summary>
        /// 모험 결과 로그를 상한 이내로 유지한다. 플레이 기록 보존을 위해 확인이 끝난 결과도 남기고,
        /// 상한을 넘은 만큼만 오래된 순으로 버린다(completedResults는 완료 순서로 쌓이므로 앞쪽이 오래된 것).
        /// 건당 약 0.9KB라 1000건이면 세이브에 약 850KB 추가 - Cloud Save 한도 5MB의 17% 수준이다.
        /// </summary>
        private void TrimResultLog()
        {
            int excess = completedResults.Count - Config.maxCompletedResultLog;
            if (excess <= 0) return;

            // 결과 확인 전(completedAdventures에 남아 있는) 기록은 삭제하지 않는다.
            // 지우면 ConfirmAdventureResult가 결과를 찾지 못해 보상이 유실되고
            // completedAdventures에서도 제거되지 않아 모험가가 영구히 모험 중 상태로 묶인다.
            int removed = 0;
            for (int i = 0; i < completedResults.Count && removed < excess; )
            {
                AdventureResult result = completedResults[i];
                if (result != null && completedAdventures.Any(a => a != null && a.instanceID == result.adventureID))
                {
                    i++;
                    continue;
                }

                completedResults.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
                Log.Info($"[AdventureManager] 모험 결과 로그 정리: {removed}건 제거 (상한 {Config.maxCompletedResultLog}, 현재 {completedResults.Count})");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged -= OnTimeChanged;
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
            }
        }

        #endregion

        #region 저장/불러오기
        
        private void SaveOngoingAdventures()
        {
            var gameData = GameManager.Instance.GameData;
            SaveToGameData(gameData);
        }

        public void SaveToGameData(GameData gameData)
        {
            gameData.ongoingAdventures = ongoingAdventures.ToSaveDataList();
            gameData.completedAdventures = completedAdventures.ToSaveDataList();
            gameData.completedAdventureResults = completedResults;
        }

        private void LoadFromGameData(GameData gameData)
        {
            ongoingAdventures.Clear();
            completedAdventures.Clear();
            completedResults.Clear();

            droppedAdventureSaves.Clear();

            // 진행중인 모험 로드
            foreach (AdventureInstanceSaveData saveData in gameData.ongoingAdventures)
            {
                if (saveData != null)
                {
                    AdventureInstance adventure = new AdventureInstance(saveData);

                    // adventurer가 null이면 스킵 (사망한 모험가)
                    if (adventure.adventurer != null && adventure.weapon != null && adventure.dungeon != null)
                    {
                        // §9 호환 가드: 시작 시 전체 해결되지 않은(마지막 이벤트 result=null) 구버전 진행모험은 드롭.
                        // 새 세이브는 항상 전 이벤트가 resolved 상태. 옛 세이브는 삭제 권장(크래시 방지용 가드).
                        var evts = adventure.progress?.events;
                        if (evts == null || evts.Count == 0 || evts[^1].result == null)
                        {
                            Log.Warn($"Skipping unresolved (legacy-format) ongoing adventure: {saveData.instanceID}");
                            droppedAdventureSaves.Add((saveData, "unresolved-legacy-format"));
                            continue;
                        }
                        ongoingAdventures.Add(adventure);
                    }
                    else
                    {
                        string reason = adventure.adventurer == null ? "Adventurer dead/missing" :
                                       adventure.weapon == null ? "Weapon missing" : "Dungeon data missing";
                        Log.Warn($"Skipping invalid adventure instance: {saveData.instanceID} [{reason}]");
                        droppedAdventureSaves.Add((saveData, reason));
                    }
                }
            }

            // 완료한 모험 로드
            foreach (AdventureInstanceSaveData saveData in gameData.completedAdventures)
            {
                if (saveData != null)
                {
                    AdventureInstance adventure = new AdventureInstance(saveData);

                    // adventurer가 null이면 스킵 (사망한 모험가)
                    if (adventure.adventurer != null && adventure.weapon != null && adventure.dungeon != null)
                    {
                        completedAdventures.Add(adventure);
                    }
                    else
                    {
                        string reason = adventure.adventurer == null ? "Adventurer dead/missing" :
                                       adventure.weapon == null ? "Weapon missing" : "Dungeon data missing";
                        // 완료 모험 드롭은 보상 소실 가능성이 있으므로 에러 레벨로 남긴다
                        Log.Error($"Dropping completed adventure (reward may be lost): {saveData.instanceID} [{reason}]");
                        droppedAdventureSaves.Add((saveData, reason));
                    }
                }
            }

            // 완료된 모험 결과 로드
            foreach (AdventureResult saveData in gameData.completedAdventureResults)
            {
                if (saveData != null)
                {
                    ReifyMaterialsFromSave(saveData.materialDrops);
                    ReifyMaterialsFromSave(saveData.purchasedMaterials);
                    completedResults.Add(saveData);
                }
                else
                {
                    Log.Warn("Skipping null adventure result entry");
                }
            }
        }

        private static void ReifyMaterialsFromSave(List<MaterialInstance> list)
        {
            if (list == null) return;
            foreach (var m in list)
            {
                if (m != null && m.materialData == null && !string.IsNullOrEmpty(m.materialDataID))
                    m.materialData = DataManager.Instance.GetMaterial(m.materialDataID);
            }
        }

        /// <summary>
        /// 로드 후 sanity pass: 복원에 실패해 드롭된 모험이 잡고 있던
        /// 모험가(isAdventuring)/무기(isRented) 잠금을 저장 데이터의 참조 ID로 해제한다
        /// </summary>
        public void RepairDroppedAdventureReferences()
        {
            if (droppedAdventureSaves.Count == 0) return;

            foreach (var (saveData, reason) in droppedAdventureSaves)
            {
                if (!string.IsNullOrEmpty(saveData.weaponInstanceID))
                {
                    var weapon = InventoryManager.Instance?.GetWeaponInstance(saveData.weaponInstanceID);
                    if (weapon != null && weapon.isRented)
                    {
                        weapon.Return();
                        Log.Warn($"[AdventureManager] 드롭 모험 무기 잠금 해제: {saveData.weaponInstanceID}");
                    }
                }

                if (!string.IsNullOrEmpty(saveData.adventurerInstanceID))
                {
                    var adventurer = VisitorManager.Instance?.GetAdventurerInstance(saveData.adventurerInstanceID);
                    if (adventurer != null && adventurer.isAdventuring)
                    {
                        adventurer.isAdventuring = false;
                        Log.Warn($"[AdventureManager] 드롭 모험 모험가 잠금 해제: {saveData.adventurerInstanceID}");
                    }
                }

                Log.Warn($"[AdventureManager] Dropped adventure cleaned up: {saveData.instanceID}, reason={reason}");
            }

            droppedAdventureSaves.Clear();
        }

        #endregion
    }
}
