// Scripts/UI/Controllers/SpineDialogueController.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// Spine 기반 대화창 Controller.
    /// </summary>
    public class SpineDialogueController : BaseController<SpineDialogueView>
    {
        protected override void Awake()
        {
            base.Awake();
        }

        public void Initialize(DialogueData dialogueData, VisitorNPC spineTarget)
        {
            view?.Initialize(dialogueData, spineTarget);
        }

        public void OnNextButtonClicked()
        {
            DialogueManager.Instance?.AdvanceDialogue();
        }

        public void OnChoiceSelected(int choiceIndex)
        {
            DialogueManager.Instance?.SelectChoice(choiceIndex);
        }

        public void OnCancelDialogue()
        {
            DialogueManager.Instance?.CancelDialogue();
        }
    }
}
