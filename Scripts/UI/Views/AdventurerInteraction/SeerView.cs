// Scripts/UI/Views/AdventurerInteraction/SeerView.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    public class SeerView : BaseView
    {
        protected override string GetThemeBgmKey() => "Seer";

        [Header("Crystal Ball")]
        [SerializeField] private Button crystalBallButton;
        [SerializeField] private Image crystalBallImage;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private TextMeshProUGUI crystalBallText;

        [Header("Buttons")]
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [SerializeField] private GameObject skipObj;
        [SerializeField] private GameObject endIndicatorObj;
        [SerializeField] private Button dialogueButton;

        [Header("Seer Particle")]
        [SerializeField] private ParticleSystem seerParticle;
        [SerializeField] private float seerParticleDuration = 1.5f;

        [SerializeField] private SeerController controller;

        private Coroutine seerParticleCoroutine;
        private Coroutine typewriterCoroutine;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen   = true;
            isCanClickOverlay = false;
            canEscape         = true;
        }

        public override void Open()
        {
            base.Open();
            UIPopupController.Instance?.ShowPlayerResourceBar();
        }

        public override void Close()
        {
            UIPopupController.Instance?.HidePlayerResourceBar();
            base.Close();
        }

        protected override void SubscribeEvents()
        {
            crystalBallButton?.onClick.AddListener(OnCrystalBallClicked);
            yesButton?.onClick.AddListener(OnYesClicked);
            noButton?.onClick.AddListener(OnNoClicked);
            dialogueButton?.onClick.AddListener(OnDialogueButtonClicked);
        }

        protected override void UnsubscribeEvents()
        {
            crystalBallButton?.onClick.RemoveAllListeners();
            yesButton?.onClick.RemoveAllListeners();
            noButton?.onClick.RemoveAllListeners();
            dialogueButton?.onClick.RemoveAllListeners();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            StopSeerParticle();
        }

        #endregion

        #region UI 업데이트

        public void UpdateDisplay(AdventurerInstance adventurer, DungeonData dungeon)
        {
            bool canConsult = SeerManager.Instance.CanConsult(adventurer, dungeon);
            int  cost       = SeerManager.Instance.GetSeerCost();

            if (dialogueText != null)
                dialogueText.text = SeerManager.Instance.GetGreetingLine(cost);

            if (crystalBallText != null)
                crystalBallText.text = string.Empty;

            if (crystalBallImage != null)
                crystalBallImage.sprite = IconManager.Instance.GetCrystalBallBaseSprite();

            StopSeerParticle();

            crystalBallButton.interactable = canConsult;
            if (yesButton != null) yesButton.interactable = canConsult;

            SetConsultedState(false);
        }

        /// <summary>
        /// 점술 진행 연출. Seer 파티클을 재생하고 seerParticleDuration 후 정지한 뒤
        /// onComplete 콜백을 호출한다(점 보는 타이밍 게이트).
        /// </summary>
        public void PlaySeerParticle(Action onComplete)
        {
            crystalBallButton.interactable = false;
            SetInProgressState();

            if (seerParticleCoroutine != null)
                StopCoroutine(seerParticleCoroutine);
            seerParticleCoroutine = StartCoroutine(SeerParticleRoutine(onComplete));
        }

        /// <summary>
        /// 점술 진행 중(파티클 연출) 상태. 선택지를 숨기고 대사를 "..."로 표시한다.
        /// 연출 종료 후 ShowResult가 최종 상태를 복원한다.
        /// </summary>
        private void SetInProgressState()
        {
            yesButton?.gameObject.SetActive(false);
            noButton?.gameObject.SetActive(false);
            HideDialogueIndicators();
            if (dialogueButton != null) dialogueButton.interactable = false;
            if (dialogueText != null) dialogueText.text = "...";
            canEscape = false;   // 점술 연출 중 ESC로 무과금 취소되지 않도록 차단
        }

        private IEnumerator SeerParticleRoutine(Action onComplete)
        {
            if (seerParticle != null)
            {
                seerParticle.gameObject.SetActive(true);
                seerParticle.Play();
            }

            SoundManager.Instance?.PlaySFX("SeerConsult");

            yield return new WaitForSecondsRealtime(seerParticleDuration);

            if (seerParticle != null)
            {
                seerParticle.Stop();
                seerParticle.gameObject.SetActive(false);
            }

            seerParticleCoroutine = null;
            onComplete?.Invoke();
        }
 
        /// <summary>
        /// 점술 결과를 표시한다.
        /// dialogue는 점술가 대사(dialogueText), result.DisplayDescription은 수정구 텍스트(crystalBallText)에 출력한다.
        /// 수정구 이미지는 result.luckLevel에 맞춰 교체된다.
        /// playAnimation=true 이면 이미지 팝 + 타이프라이터를 재생한다.
        /// hasNext=true 이면 다음 대사가 남아 skip 인디케이터를, false면 end 인디케이터를 표시한다.
        /// </summary>
        public void ShowResult(SeerResult result, string dialogue, bool playAnimation = true, bool hasNext = false)
        {
            SetConsultedState(true);
            SetDialogueIndicator(hasNext);

            if (dialogueText != null)
                dialogueText.text = dialogue;

            if (crystalBallImage != null)
                crystalBallImage.sprite = IconManager.Instance.GetIconByLuckLevel(result.luckLevel);

            if (playAnimation)
            {
                if (crystalBallText != null)
                    StartTypewriter(result.DisplayDescription, 0.04f);
            }
            else
            {
                if (crystalBallText != null)
                    crystalBallText.text = result.DisplayDescription;
            }
        }

        /// <summary>
        /// 수정구를 임계 횟수 이전에 만졌을 때의 경고 상태. 경고 대사를 표시하고
        /// yes/no를 숨긴 뒤 next(skip) 인디케이터를 띄운다. 대사창 클릭으로
        /// ShowCrystalBallFollowUp(후속 대사)으로 넘어간다.
        /// </summary>
        public void ShowCrystalBallWarning(string dialogue)
        {
            if (dialogueText != null)
                dialogueText.text = dialogue;

            yesButton?.gameObject.SetActive(false);
            noButton?.gameObject.SetActive(false);
            SetDialogueIndicator(hasNext: true);
            if (dialogueButton != null) dialogueButton.interactable = true;
        }

        /// <summary>
        /// 경고 대사 이후 대사창 클릭 시 후속 대사를 표시하고 초기 상태로 복원한다
        /// (yes/no 재노출 + 인디케이터 숨김 + dialogueButton 비활성).
        /// </summary>
        public void ShowCrystalBallFollowUp(string dialogue)
        {
            if (dialogueText != null)
                dialogueText.text = dialogue;

            SetConsultedState(false);
        }

        /// <summary>
        /// 이스터에그 발동 후 다음 대사(코멘트)로 넘길 때 사용. 대사를 교체하고
        /// 남은 대사가 없으므로 end 인디케이터로 전환한다.
        /// </summary>
        public void ShowFollowUpDialogue(string dialogue)
        {
            if (dialogueText != null)
                dialogueText.text = dialogue;
            SetDialogueIndicator(false);
        }

        private void SetConsultedState(bool consulted)
        {
            yesButton?.gameObject.SetActive(!consulted);
            noButton?.gameObject.SetActive(!consulted);
            if (dialogueButton != null) dialogueButton.interactable = consulted;
            canEscape = true;   // 연출(SetInProgressState) 종료 후 ESC 복원

            // consulted=true일 때의 skip/end 표시는 호출부(ShowResult)가 hasNext로 결정한다.
            if (!consulted) HideDialogueIndicators();
        }

        /// <summary>
        /// 대사창 인디케이터 전환. hasNext=true면 다음 대사가 남아 skip을,
        /// false면 마지막 대사라 end를 표시한다. 항상 상호배타.
        /// </summary>
        private void SetDialogueIndicator(bool hasNext)
        {
            skipObj?.SetActive(hasNext);
            endIndicatorObj?.SetActive(!hasNext);
        }

        private void HideDialogueIndicators()
        {
            skipObj?.SetActive(false);
            endIndicatorObj?.SetActive(false);
        }

        private void StopSeerParticle()
        {
            if (seerParticleCoroutine != null)
            {
                StopCoroutine(seerParticleCoroutine);
                seerParticleCoroutine = null;
            }
            if (seerParticle != null)
            {
                seerParticle.Stop();
                seerParticle.gameObject.SetActive(false);
            }
        }

        private void StartTypewriter(string text, float charDelay)
        {
            if (typewriterCoroutine != null)
                StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = StartCoroutine(TypewriterRoutine(text, charDelay));
        }

        private IEnumerator TypewriterRoutine(string text, float charDelay)
        {
            crystalBallText.text = string.Empty;
            foreach (char c in text)
            {
                crystalBallText.text += c;
                yield return new WaitForSecondsRealtime(charDelay);
            }
            typewriterCoroutine = null;
        }

        #endregion

        #region 이벤트 핸들러

        private void OnCrystalBallClicked() => controller?.OnCrystalBallClicked();
        private void OnYesClicked()          => controller?.OnYesClicked();
        private void OnNoClicked()           => controller?.OnClose();
        private void OnDialogueButtonClicked() => controller?.OnDialogueButtonClicked();
        public override void OnEscapeClicked() => OnNoClicked();

        #endregion
    }
}
