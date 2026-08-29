using System.Linq;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 진행 Controller. 정렬된 모험 리스트를 View 캐러셀에 공급하고,
    /// Manager 이벤트를 받아 해당 카드(instanceID 매칭)를 갱신한다.
    /// </summary>
    public class AdventureProgressController : BaseController<AdventureProgressView>
    {
        #region 초기화

        protected override void OnPanelOpen()
        {
            RebuildCarousel();
            TutorialManager.Instance?.OnTutorialAdventureProgressOpened();   // 9단계 훅(가드는 TutorialManager 내부)
        }

        /// <summary>BottomBar가 패널 오픈 직후 호출 (레이아웃 확정 후 네비 스크롤 보정).</summary>
        public void InitAfter()
        {
            view?.ResetNavScroll();
        }

        protected override void SubscribeControllerEvents()
        {
            if (AdventureManager.Instance != null)
            {
                AdventureManager.Instance.OnAdventureCompleted             += HandleAdventureCompleted;
                AdventureManager.Instance.OnAdventureEventTriggerStarted   += HandleAdventureEventTriggerStarted;
            }
        }

        protected override void UnsubscribeControllerEvents()
        {
            if (AdventureManager.Instance != null)
            {
                AdventureManager.Instance.OnAdventureCompleted             -= HandleAdventureCompleted;
                AdventureManager.Instance.OnAdventureEventTriggerStarted   -= HandleAdventureEventTriggerStarted;
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void HandleAdventureCompleted(AdventureInstance adventure, AdventureResult result)
        {
            if (adventure == null) return;

            // View가 열려 있으면 카드를 그 자리에 두고 완료 표시만 덮어쓴다. (닫혀 있으면 다음 오픈 시 정렬로 처리)
            if (view.IsOpen)
                view.ShowCardCompleted(adventure);

            string toastMessage = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Messages", "AdventureProgress_EndedToast",
                arguments: new object[] { new Dictionary<string, object> { { "name", adventure.adventurer.Name } } });
            UIPopupController.Instance?.ShowToast(toastMessage);
        }

        private void HandleAdventureEventTriggerStarted(AdventureInstance adventure, AdventureEvent evt)
        {
            view?.UpdateCardEvent(adventure);
        }

        #endregion

        #region View로부터 호출되는 메서드

        public void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel<AdventureResultView>();
            UIManager.Instance.ClosePanel<AdventureReplayView>();
            UIManager.Instance.ClosePanel<AdventureProgressView>();
        }

        /// <summary>결과 패널 닫힌 후 처리 (AdventureReplayController에서 호출).</summary>
        public void OnResultClosed(AdventureInstance adventure)
        {
            int remainingCount = AdventureManager.Instance.OngoingAdventures.Count
                               + AdventureManager.Instance.CompletedAdventures.Count;

            if (remainingCount > 0)
                RebuildCarousel();
            else
                OnCloseClicked();
        }

        #endregion

        #region 내부 메서드

        /// <summary>정렬(진행 startTime↑ + 완료 startTime↑) 후 항상 index 0을 포커스해 캐러셀 재구성.</summary>
        private void RebuildCarousel()
        {
            view?.BuildCarousel(BuildSortedList(), 0);
        }

        /// <summary>진행 그룹(왼쪽, 오래된→최신) + 완료 그룹(오른쪽 끝, 오래된→최신). 결과 확인은 전령이 담당하므로 완료는 뒤로 민다.</summary>
        private List<AdventureInstance> BuildSortedList()
        {
            var list = new List<AdventureInstance>();
            list.AddRange(AdventureManager.Instance.OngoingAdventures.OrderBy(a => a.startTime));
            list.AddRange(AdventureManager.Instance.CompletedAdventures.OrderBy(a => a.startTime));
            return list;
        }

        #endregion
    }
}
