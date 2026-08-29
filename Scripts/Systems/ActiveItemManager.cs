using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 액티브 아이템 배정 및 효과 조회 매니저
    /// - Adventure 귀속: AssignItem → StartAdventure 시 ConsumeAssignedItem
    /// - Immediate: 대화 컨트롤러에서 직접 소비 (인형)
    /// - Blacksmith 귀속: SetBlacksmithItem → Execute* 시 ConsumeBlacksmithItem
    /// </summary>
    public class ActiveItemManager : BaseManager<ActiveItemManager>
    {
        // Adventure 귀속: adventurerID → 배정된 아이템 데이터 (인벤토리에서 이미 제거됨)
        [SerializeField] private SerializableDictionary<string, ActiveItemData> assignedItems = new();

        // Blacksmith 귀속: 단일 슬롯
        [SerializeField] private ActiveItemData blacksmithItem;

        #region 초기화

        public void Initialize(GameData gameData)
        {
            LoadFromGameData(gameData);
        }

        #endregion

        #region Adventure 귀속 메서드

        /// <summary>
        /// 아이템을 모험가에게 배정. 인벤토리에서 즉시 제거.
        /// </summary>
        public void AssignItem(ActiveItemData data, string adventurerID)
        {
            if (data == null || string.IsNullOrEmpty(adventurerID))
            {
                Log.Warn("[ActiveItemManager] AssignItem: data 또는 adventurerID가 null입니다.");
                return;
            }

            InventoryManager.Instance.RemoveActiveItem(data.StaticID);
            assignedItems[adventurerID] = data;

            Log.Info($"[ActiveItemManager] 배정: {data.itemName} → {adventurerID}");
        }

        /// <summary>
        /// 배정 취소. 아이템을 인벤토리로 복귀.
        /// </summary>
        public void UnassignItem(string adventurerID)
        {
            if (!assignedItems.TryGetValue(adventurerID, out var data))
            {
                Log.Warn($"[ActiveItemManager] UnassignItem: {adventurerID}에 배정된 아이템 없음");
                return;
            }

            assignedItems.Remove(adventurerID);
            InventoryManager.Instance.ReturnActiveItemToInventory(data);

            Log.Info($"[ActiveItemManager] 배정 취소: {data.itemName} 인벤 복귀");
        }

        /// <summary>
        /// 모험 시작 시 호출. 딕셔너리만 클리어.
        /// </summary>
        public void ConsumeAssignedItem(string adventurerID)
        {
            if (assignedItems.Remove(adventurerID))
            {
                Log.Info($"[ActiveItemManager] 소비: {adventurerID}의 배정 아이템");
                QuestManager.Instance?.UpdateProgress(QuestType.GiftComplete);
            }
        }

        /// <summary>
        /// 배정 아이템을 인벤토리로 반환 (모험 없이 방문 종료 등). 배정이 없으면 false
        /// </summary>
        public bool TryReturnAssignedItem(string adventurerID, string reason)
        {
            if (string.IsNullOrEmpty(adventurerID)) return false;
            if (!assignedItems.TryGetValue(adventurerID, out var data)) return false;

            assignedItems.Remove(adventurerID);
            InventoryManager.Instance?.ReturnActiveItemToInventory(data);
            Log.Info($"[ActiveItemManager] 배정 아이템 반환 ({reason}): {data.itemName}");
            return true;
        }

        /// <summary>
        /// 배정 아이템을 반환 없이 폐기 (모험가 사망 등). 배정이 없으면 false
        /// </summary>
        public bool TryDiscardAssignedItem(string adventurerID, string reason)
        {
            if (string.IsNullOrEmpty(adventurerID)) return false;
            if (!assignedItems.Remove(adventurerID)) return false;

            Log.Info($"[ActiveItemManager] 배정 아이템 폐기 ({reason}): {adventurerID}");
            return true;
        }

        /// <summary>
        /// 로드 후 sanity pass: 대상 모험가가 없거나 방문/생존 상태가 아니면 배정 아이템을 정리한다.
        /// 로드 시 남아 있는 배정은 "출발 전 배정"이므로 소유자가 유효하지 않으면 고아 상태다
        /// </summary>
        public void RepairAssignmentsAfterLoad()
        {
            if (assignedItems.Count == 0) return;

            foreach (string adventurerID in new List<string>(assignedItems.Keys))
            {
                var adventurer = VisitorManager.Instance?.GetAdventurerInstance(adventurerID);

                if (adventurer != null && adventurer.isAdventuring)
                {
                    // 출발한 모험가에게 배정이 남은 비정상 상태 - 효과는 이미 모험에 캐싱되어 있으므로
                    // 반환하면 중복 지급이 된다. 폐기로 정리
                    TryDiscardAssignedItem(adventurerID, "load-repair-already-adventuring");
                }
                else if (adventurer == null || !adventurer.isAlive || !adventurer.isVisiting)
                {
                    TryReturnAssignedItem(adventurerID, "load-repair-orphaned-assignment");
                }
            }
        }

        public ActiveItemData GetAssignedItem(string adventurerID)
        {
            return assignedItems.TryGetValue(adventurerID, out var data) ? data : null;
        }

        /// <summary>
        /// 배정된 모든 아이템을 인벤토리로 회수한다. 실제 모험이 아직 없어 모든 배정이 dangling인 상황
        /// (튜토리얼 스킵: 준비화면에서 선물된 부적이 유령 모험가에 배정된 채 남음)에서만 호출한다.
        /// </summary>
        public void ReturnAllAssignedItems()
        {
            if (assignedItems.Count == 0) return;

            foreach (string adventurerID in new List<string>(assignedItems.Keys))
            {
                InventoryManager.Instance?.ReturnActiveItemToInventory(assignedItems[adventurerID]);
            }
            assignedItems.Clear();
            Log.Info("[ActiveItemManager] 배정된 모든 아이템 인벤 회수(튜토리얼 정리)");
        }

        #endregion

        #region Blacksmith 귀속 메서드

        /// <summary>
        /// 강화석을 대장장이 슬롯에 배정. 인벤토리에서 즉시 제거.
        /// </summary>
        public void SetBlacksmithItem(ActiveItemData data)
        {
            if (data == null) return;

            if (blacksmithItem != null)
                ClearBlacksmithItem();

            InventoryManager.Instance.RemoveActiveItem(data.StaticID);
            blacksmithItem = data;
            Log.Info($"[ActiveItemManager] 대장장이 배정: {data.itemName}");
        }

        /// <summary>
        /// 배정 취소. 인벤토리로 복귀.
        /// </summary>
        public void ClearBlacksmithItem()
        {
            if (blacksmithItem == null) return;
            InventoryManager.Instance.ReturnActiveItemToInventory(blacksmithItem);
            Log.Info($"[ActiveItemManager] 대장장이 배정 취소: {blacksmithItem.itemName}");
            blacksmithItem = null;
        }

        /// <summary>
        /// 대장장이 작업 시 호출. 슬롯만 클리어.
        /// </summary>
        public void ConsumeBlacksmithItem()
        {
            if (blacksmithItem != null)
            {
                Log.Info($"[ActiveItemManager] 소비: {blacksmithItem.itemName}");
                blacksmithItem = null;
                // 강화석 소비는 선물 퀘스트로 치지 않는다 - 모험가에게 준 선물만 카운트
            }
        }

        public ActiveItemData GetBlacksmithItem() => blacksmithItem;

        #endregion

        #region 효과 조회 메서드

        private ActiveItemData GetAdventureItem(string adventurerID, ActiveItemType type)
        {
            var data = GetAssignedItem(adventurerID);
            return data?.itemType == type ? data : null;
        }

        /// <summary>부적: 모험 성공률 보너스 (%)</summary>
        public float GetCharmBonus(string adventurerID)
            => GetAdventureItem(adventurerID, ActiveItemType.Charm)?.effectValue ?? 0f;

        /// <summary>포션: 후퇴 방지 횟수 보너스</summary>
        public int GetPotionProtection(string adventurerID)
            => (int)(GetAdventureItem(adventurerID, ActiveItemType.Potion)?.effectValue ?? 0f);

        /// <summary>탈출 로프: 함정 회피 추가 확률 (0~1)</summary>
        public float GetEscapeRopeBonus(string adventurerID)
            => GetAdventureItem(adventurerID, ActiveItemType.EscapeRope)?.effectValue ?? 0f;

        /// <summary>신속한 신발: 진행시간 배율 (기본 1f, 예: 0.8 = -20%)</summary>
        public float GetSwiftShoesMultiplier(string adventurerID)
            => GetAdventureItem(adventurerID, ActiveItemType.SwiftShoes)?.effectValue ?? 1f;

        /// <summary>해체용 단검: 보너스 재료 획득 수</summary>
        public int GetDisassemblyKnifeBonus(string adventurerID)
            => (int)(GetAdventureItem(adventurerID, ActiveItemType.DisassemblyKnife)?.effectValue ?? 0f);

        /// <summary>황금 부적: 골드 획득 배율 보너스 (예: 0.25 = +25%)</summary>
        public float GetGoldAmuletBonus(string adventurerID)
            => GetAdventureItem(adventurerID, ActiveItemType.GoldAmulet)?.effectValue ?? 0f;

        /// <summary>
        /// 보물 지도: 던전 특수 재료 드롭률 **배율** 보너스 (예: 1.0 = x2).
        /// 희귀 드롭 이벤트에 보너스 재료 +N을 얹던 방식은 두 가지 문제가 있었다.
        /// 희귀 드롭 이벤트 자체가 칸당 10%라 대부분의 모험에서 아무 일도 일어나지 않았고,
        /// 나오는 재료가 던전 드롭 풀에서 가중치 추첨이라 무엇을 노리고 쓸 수가 없었다.
        /// 특수 재료는 보스(마지막 칸 확정)에서 판정되고 던전마다 1종씩 고유해서,
        /// 원하는 재료를 노리고 쓰는 아이템이 된다.
        /// </summary>
        public float GetTreasureMapBonus(string adventurerID)
            => GetAdventureItem(adventurerID, ActiveItemType.TreasureMap)?.effectValue ?? 0f;

        /// <summary>수호의 메달: 사망률 감소 비율 (예: 0.1 = -10%)</summary>
        public float GetDeathWardBonus(string adventurerID)
            => GetAdventureItem(adventurerID, ActiveItemType.DeathWard)?.effectValue ?? 0f;

        /// <summary>
        /// 명예의 두루마리: 성공 시 평판 **배율** 보너스 (예: 1.0 = x2).
        /// 절대값 +5였을 때는 평판이 등급 무관 +3 고정이라 어느 던전에서나 1.67회분이었으나,
        /// 평판이 `클리어칸/2 + 등급보너스`(일반 3 ~ 전설 16)로 바뀌면서
        /// 고등급일수록 상대 가치가 떨어지는 역전이 생겼다(전설에서 0.31회분).
        /// 다른 모험 아이템(부적·아뮬렛)이 전부 비율인 것과 맞춰 배율로 전환했다.
        /// </summary>
        public float GetFameScrollBonus(string adventurerID)
            => GetAdventureItem(adventurerID, ActiveItemType.FameScroll)?.effectValue ?? 0f;

        /// <summary>강화석: 강화/진화 성공률 배율 보너스 (예: 0.1 = ×1.1)</summary>
        public float GetForgeStoneBonus()
            => (blacksmithItem?.itemType == ActiveItemType.ForgeStone)
                ? blacksmithItem.effectValue
                : 0f;

        #endregion

        #region 저장/불러오기

        public void SaveToGameData(GameData gameData)
        {
            if (gameData == null) return;

            gameData.pendingAssignedItems.Clear();
            foreach (var kvp in assignedItems)
            {
                gameData.pendingAssignedItems.Add(new ActiveItemAssignmentSaveData
                {
                    adventurerID = kvp.Key,
                    itemDataID = kvp.Value.StaticID
                });
            }

            // 대장간 슬롯은 상태만 직렬화한다. 과거에는 여기서 인벤토리로 반환했지만
            // SaveToGameData는 모든 자동/수동 저장에서 호출되므로 저장할 때마다 슬롯이 비워졌다.
            // 반환은 ClearBlacksmithItem() 같은 명시적 흐름에서만 수행한다.
            // (SyncManagersToGameData의 ActiveItemManager → InventoryManager 호출 순서 의존도 함께 제거됨)
            gameData.pendingBlacksmithItemDataID = blacksmithItem?.StaticID;
        }

        private void LoadFromGameData(GameData gameData)
        {
            assignedItems.Clear();
            blacksmithItem = null;

            if (gameData == null) return;

            foreach (var assignment in gameData.pendingAssignedItems)
            {
                if (assignment == null || string.IsNullOrEmpty(assignment.itemDataID)) continue;
                var data = DataManager.Instance.GetActiveItem(assignment.itemDataID);
                if (data == null) continue;
                assignedItems[assignment.adventurerID] = data;
            }

            // 대장간 슬롯 복원. 배정 시 이미 인벤토리에서 제거된 아이템이므로 다시 제거하지 않는다
            if (!string.IsNullOrEmpty(gameData.pendingBlacksmithItemDataID))
            {
                blacksmithItem = DataManager.Instance.GetActiveItem(gameData.pendingBlacksmithItemDataID);
                if (blacksmithItem == null)
                    Log.Warn($"[ActiveItemManager] 알 수 없는 대장간 슬롯 아이템 스킵: {gameData.pendingBlacksmithItemDataID}");
            }
        }

        #endregion
    }
}
