using UnityEngine;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class AdventureDialogueController : BaseController<AdventureDialogueView>
    {
        [SerializeField] private AdventurerInstance currentAdventurer;

        public void Initialize(AdventurerInstance adventurer)
        {
            currentAdventurer = adventurer;

            // G1: 대화 열림 -> 모험 시작까지 경과 측정 시작점
            AnalyticsManager.Instance?.RecordDialogOpenTime();

            view?.Initialize(adventurer);
        }

        public void OnPrepareAdventure()
        {
            if (currentAdventurer == null) return;

            AnalyticsManager.Instance?.SendButtonClick("adventure_dialog", "prepare_adventure");

            UIManager.Instance.ClosePanel<AdventureDialogueView>();

            UIManager.Instance.OpenPanel<AdventurePreparationView>(() =>
                UIControllerManager.Instance.GetController<AdventurePreparationController>()
                    ?.Initialize(currentAdventurer, QuestBoardManager.Instance.GetAvailableDungeons()));
        }

        public void OnClose()
        {
            AnalyticsManager.Instance?.SendButtonClick("adventure_dialog", "close");

            UIManager.Instance.ClosePanel<AdventureDialogueView>();
            VisitorManager.Instance.SetOtherVisitorsFaded(false, currentAdventurer?.currentVisitor);
            currentAdventurer?.currentVisitor?.EndInteraction();
            CameraZoomController.Instance?.ZoomOut();
            currentAdventurer = null;
        }
    }
}
