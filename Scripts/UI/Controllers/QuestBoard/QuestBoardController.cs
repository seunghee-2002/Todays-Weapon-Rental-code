// Scripts/UI/Controllers/QuestBoard/QuestBoardController.cs
using UnityEngine;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 의뢰판 Controller — Phase 1(선택) / Phase 2(수색 파견) 단일 패널 통합 운영
    /// </summary>
    public class QuestBoardController : BaseController<QuestBoardView>
    {
        private List<string> selectedIDs      = new();
        private List<string> selectedScoutIDs = new();
        private int requiredCount             = 0;

        private static string M(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        #region 초기화

        protected override void OnEnable()
        {
            base.OnEnable();
            SyncCurrentState();
        }

        protected override void SubscribeControllerEvents()
        {
            if (QuestBoardManager.Instance != null)
            {
                QuestBoardManager.Instance.OnBoardGenerated += OnBoardGenerated;
                QuestBoardManager.Instance.OnBoardConfirmed += OnBoardConfirmed;
            }
            if (ScoutManager.Instance != null)
                ScoutManager.Instance.OnScoutComplete += OnScoutComplete;
        }

        protected override void UnsubscribeControllerEvents()
        {
            if (QuestBoardManager.Instance != null)
            {
                QuestBoardManager.Instance.OnBoardGenerated -= OnBoardGenerated;
                QuestBoardManager.Instance.OnBoardConfirmed -= OnBoardConfirmed;
            }
            if (ScoutManager.Instance != null)
                ScoutManager.Instance.OnScoutComplete -= OnScoutComplete;
        }

        private void SyncCurrentState()
        {
            var manager = QuestBoardManager.Instance;
            if (manager == null || !manager.IsTodayGenerated) return;

            // 미확정 → Phase 1 복원
            if (!manager.IsConfirmed)
            {
                selectedIDs.Clear();
                requiredCount = GetRequiredCount();
                view?.SetupSlots(manager.GetPoolDungeons());
                view?.UpdateSelectionCount(0, requiredCount);
                RefreshButtonUpdate();
                return;
            }

            // 확정 완료 → Phase 2 복원 (애니메이션 스킵)
            selectedIDs.Clear();
            selectedScoutIDs.Clear();
            view?.SetupSlots(manager.GetPoolDungeons());

            var confirmedIDs   = manager.GetAvailableDungeons().Select(d => d.StaticID).ToList();
            var highlightedIDs = manager.GetHighlightedDungeons().Select(d => d.StaticID).ToList();
            view?.ApplyConfirmedState(confirmedIDs, highlightedIDs, GetAvailableScoutCount(), skipAnimation: true);
            view?.RefreshSlotScoutStates(selectedScoutIDs);
        }

        #endregion

        #region QuestBoardManager 이벤트

        private void OnBoardGenerated(List<DungeonData> poolDungeons)
        {
            selectedIDs.Clear();
            selectedScoutIDs.Clear();
            requiredCount = GetRequiredCount();

            if (!UIManager.Instance.GetPanel<QuestBoardView>()?.IsOpen ?? true)
                UIManager.Instance.OpenPanel<QuestBoardView>();

            view?.SetupSlots(poolDungeons);
            view?.UpdateSelectionCount(0, requiredCount);
            RefreshButtonUpdate();
        }

        private void OnBoardConfirmed(List<DungeonData> confirmedDungeons, List<DungeonData> highlighted)
        {
            var confirmedIDs   = confirmedDungeons.Select(d => d.StaticID).ToList();
            var highlightedIDs = highlighted.Select(d => d.StaticID).ToList();

            selectedScoutIDs.Clear();
            view?.ApplyConfirmedState(confirmedIDs, highlightedIDs, GetAvailableScoutCount());
            // Phase 2 진입은 View 내부 애니메이션 종료 후 EnterScoutPhase에서 RefreshSlotScoutStates를 호출함.
        }

        #endregion

        #region Phase 1 — 슬롯/확정/새로고침

        public void OnInfoClicked(DungeonData dungeon)
        {
            AnalyticsManager.Instance?.SendButtonClick("quest_board", "dungeon_info", new Dictionary<string, object>
            {
                { "dungeon_id", dungeon.StaticID }
            });

            var stat  = AdventureManager.Instance.GetDungeonStat(dungeon.StaticID);
            var popup = UIManager.Instance.GetOrInstantiatePanel<DungeonDetailPopup>();
            popup?.Initialize(dungeon, stat);
            UIManager.Instance.OpenPanel<DungeonDetailPopup>();
        }

        public void OnSelectClicked(DungeonData dungeon)
        {
            AnalyticsManager.Instance?.SendButtonClick("quest_board", "dungeon_select", new Dictionary<string, object>
            {
                { "dungeon_id", dungeon.StaticID }
            });

            if (!QuestBoardManager.Instance.IsConfirmed)
                ToggleSelection(dungeon);
        }

        private void ToggleSelection(DungeonData dungeon)
        {
            string id = dungeon.StaticID;

            if (selectedIDs.Contains(id))
            {
                selectedIDs.Remove(id);
            }
            else
            {
                if (selectedIDs.Count >= requiredCount)
                {
                    UIPopupController.Instance?.ShowToast(M("QuestBoard_MaxSelectionReached"), type: PopupSfxType.Warning);
                    return;
                }
                selectedIDs.Add(id);
            }

            view?.UpdateSlotSelection(selectedIDs);
            view?.UpdateSelectionCount(selectedIDs.Count, requiredCount);
        }

        public void OnConfirmClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("quest_board", "confirm");

            if (selectedIDs.Count != requiredCount)
            {
                UIPopupController.Instance?.ShowToast(QuestBoardView.SelectDungeonsDesc(requiredCount), type: PopupSfxType.Warning);
                return;
            }

            bool success = QuestBoardManager.Instance.ConfirmBoard(selectedIDs);

            if (!success)
                UIPopupController.Instance?.ShowToast(M("QuestBoard_ConfirmSelection"), type: PopupSfxType.Warning);
        }

        public void OnSelectAllClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("quest_board", "select_all");

            // 전체 선택 ⇄ 전체 해제 토글
            if (selectedIDs.Count >= requiredCount)
            {
                selectedIDs.Clear();
            }
            else
            {
                var toAdd = QuestBoardManager.Instance.GetPoolDungeons()
                    .Where(d => !selectedIDs.Contains(d.StaticID))
                    .OrderByDescending(d => d.grade)
                    .Take(requiredCount - selectedIDs.Count)
                    .Select(d => d.StaticID)
                    .ToList();

                selectedIDs.AddRange(toAdd);
            }

            view?.UpdateSlotSelection(selectedIDs);
            view?.UpdateSelectionCount(selectedIDs.Count, requiredCount);
        }

        public void OnRefreshClicked()
        {
            // G25: 횟수 소진으로 막혀도 "새로고침을 원했다"는 사실이 지표다
            AnalyticsManager.Instance?.SendButtonClick("quest_board", "refresh", new Dictionary<string, object>
            {
                { "cost", QuestBoardManager.Instance.GetRefreshCost() }
            });

            if (!QuestBoardManager.Instance.CanRefresh)
            {
                UIPopupController.Instance?.ShowToast(M("QuestBoard_RefreshExhausted"), type: PopupSfxType.Warning);
                return;
            }

            int cost = QuestBoardManager.Instance.GetRefreshCost();

            UIPopupController.Instance.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Messages", "QuestBoard_RefreshConfirm",
                    arguments: new object[] { new Dictionary<string, object> { { "cost", cost.ToString("N0") } } }),
                onConfirm: () => EconomyManager.Instance.EnsureGold(cost, onReady: DoRefreshBoard),
                onCancel: () => { }
            );
        }

        private void DoRefreshBoard()
        {
            if (QuestBoardManager.Instance.RefreshBoard())
                SoundManager.Instance?.PlaySFX("Buy");
        }

        #endregion

        #region Phase 2 — 수색 파견

        public void OnDungeonSelectedForScout(DungeonData dungeon)
        {
            if (dungeon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("scout_dispatch", "dungeon_selected", new Dictionary<string, object>
            {
                { "dungeon_id", dungeon.StaticID }
            });

            if (!ScoutManager.Instance.CanSendScout(dungeon)) return;

            string id = dungeon.StaticID;
            bool nowSelected;

            if (selectedScoutIDs.Contains(id))
            {
                selectedScoutIDs.Remove(id);
                nowSelected = false;
            }
            else
            {
                selectedScoutIDs.Add(id);
                nowSelected = true;
            }

            view?.UpdateScoutSlotSelection(selectedScoutIDs);
            view?.UpdateScoutSelectionCount(selectedScoutIDs.Count, GetAvailableScoutCount(), GetSelectedScoutTotalCost());

            if (nowSelected && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialScoutDungeonSelected();
        }

        public void OnSelectAllScoutClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("scout_dispatch", "select_all");

            var available = QuestBoardManager.Instance.GetAvailableDungeons()
                .Where(d => ScoutManager.Instance.CanSendScout(d))
                .Select(d => d.StaticID)
                .ToList();

            if (available.Count == 0)
            {
                UIPopupController.Instance?.ShowToast(M("QuestBoard_NoScoutTarget"), type: PopupSfxType.Warning);
                return;
            }

            // 전체 선택 ⇄ 전체 해제 토글
            if (selectedScoutIDs.Count >= available.Count)
            {
                selectedScoutIDs.Clear();
            }
            else
            {
                selectedScoutIDs.Clear();
                selectedScoutIDs.AddRange(available);
            }

            view?.UpdateScoutSlotSelection(selectedScoutIDs);
            view?.UpdateScoutSelectionCount(selectedScoutIDs.Count, GetAvailableScoutCount(), GetSelectedScoutTotalCost());
        }

        public void OnSendScoutClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("scout_dispatch", "send_scout", new Dictionary<string, object>
            {
                { "quantity", selectedScoutIDs.Count },
                { "cost", GetSelectedScoutTotalCost() }
            });

            if (selectedScoutIDs.Count == 0)
            {
                UIPopupController.Instance?.ShowToast(
                    LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", "QuestBoard_SelectScoutDesc"),
                    type: PopupSfxType.Warning);
                return;
            }

            int totalCost = GetSelectedScoutTotalCost();
            EconomyManager.Instance.EnsureGold(totalCost, onReady: DoSendScout);
        }

        private void DoSendScout()
        {
            var targets = selectedScoutIDs
                .Select(id => DataManager.Instance.GetDungeon(id))
                .Where(d => d != null && ScoutManager.Instance.CanSendScout(d))
                .ToList();

            foreach (var dungeon in targets)
                ScoutManager.Instance.SendScout(dungeon);

            // 파견 개수와 무관하게 1회. selectedScoutIDs가 아니라 CanSendScout를 통과한 targets가 실제 파견분이다.
            if (targets.Count > 0)
                SoundManager.Instance?.PlaySFX("ScoutStart");

            selectedScoutIDs.Clear();
            ClosePanelByUser();

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialScoutDispatched();
        }

        public void OnCloseScoutClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("scout_dispatch", "close");

            if (selectedScoutIDs.Count > 0)
            {
                UIPopupController.Instance.ShowPopup(
                    M("QuestBoard_StopScoutWithSelection"),
                    onConfirm: ClosePanelByUser,
                    onCancel: () => { }
                );
                return;
            }

            ClosePanelByUser();
        }

        private void ClosePanelByUser()
        {
            var saveData = GameManager.Instance.GameData.dailyQuestBoardData;
            if (saveData != null) saveData.scoutPhaseClosedByUser = true;

            UIManager.Instance.ClosePanel<QuestBoardView>();
        }

        #endregion

        #region ScoutManager 이벤트

        private void OnScoutComplete(string dungeonStaticID, ArmorType armorType)
        {
            if (view == null || !view.IsOpen) return;
            if (view.CurrentPhase != QuestBoardView.Phase.Scout) return;

            // 완료된 던전이 선택 목록에 있었다면 제거 — 슬롯 선택 표시는 아래 RefreshSlotScoutStates가 재설정함
            selectedScoutIDs.Remove(dungeonStaticID);

            view?.RefreshSlotScoutStates(selectedScoutIDs);
            view?.UpdateScoutSelectionCount(selectedScoutIDs.Count, GetAvailableScoutCount(), GetSelectedScoutTotalCost());
        }

        #endregion

        #region 헬퍼

        private int GetRequiredCount()
        {
            // 풀 크기가 설정값보다 작으면 낮춘 값이 반환된다 - 소프트락 방지
            return QuestBoardManager.Instance.GetRequiredSelectionCount();
        }

        private int GetAvailableScoutCount()
        {
            return QuestBoardManager.Instance.GetAvailableDungeons()
                .Count(d => ScoutManager.Instance.CanSendScout(d));
        }

        private int GetSelectedScoutTotalCost()
        {
            int total = 0;
            foreach (var id in selectedScoutIDs)
            {
                var dungeon = DataManager.Instance.GetDungeon(id);
                if (dungeon == null) continue;
                total += ScoutManager.Instance.GetScoutCost(dungeon);
            }
            return total;
        }

        private void RefreshButtonUpdate()
        {
            int count = QuestBoardManager.Instance.RefreshCount;
            // 하드코딩 3 대신 실제 설정값 사용
            view?.UpdateRefreshButton(count, QuestBoardManager.Instance.MaxRefreshCount);
        }

        #endregion
    }
}
