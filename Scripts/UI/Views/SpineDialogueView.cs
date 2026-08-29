// Scripts/UI/Views/SpineDialogueView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// Spine 기반 대화창 UI View. 모든 노드 대화가 이 View를 사용한다.
    /// 화자 초상화 대신 씬에 떠 있는 VisitorNPC의 spine을 보여준다 — 다른 방문자 페이드 +
    /// sorting order 상향(대화 종료 시 원복). 카메라 줌인/아웃은 대화 단위가 아니라
    /// 상호작용 단위(VisitorNPC.StartInteraction/EndInteraction)에서 관리한다.
    /// </summary>
    public class SpineDialogueView : BaseView
    {
        [Header("Speaker Info")]
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI roleText;   // 방문자 유형 라벨 (모험가/상인/대장장이 등)

        [Header("Dialogue Content")]
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Header("Buttons")]
        [SerializeField] private Button dialogueButton;
        [SerializeField] private Button backButton;   // 뒤로가기 버튼 (ESC와 동일 동작)

        [Header("Indicators")]
        [SerializeField] private GameObject canNextIndicator;
        [SerializeField] private GameObject endIndicator;

        [Header("Choices")]
        [SerializeField] private GameObject choicePanel;     // 선택지 패널
        [SerializeField] private Transform choiceContainer;  // 선택지 버튼들이 들어갈 컨테이너
        [SerializeField] private GameObject choiceButtonPrefab; // 선택지 버튼 프리팹

        [Header("Controller")]
        [SerializeField] private SpineDialogueController dialogueController;

        private List<GameObject> activeChoiceButtons = new List<GameObject>();

        // 골드 필요 선택지가 표시되는 동안에만 PlayerResourceBar를 띄웠는지 추적
        private bool resourceBarShown;

        private SpineDialogueController Controller => dialogueController;

        // 강조할 월드 NPC (sorting order로 앞으로 끌어올림). null이면 캐릭터 없이 텍스트만.
        private VisitorNPC cachedVisitor;
        private int savedSortingOrder;

        // OnDialogueNodeChanged 중복 구독 방지 (대화 중첩 시작 시)
        private bool dialogueNodeSubscribed;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = false;
            canEscape = false;
        }

        public void Initialize(DialogueData dialogueData, VisitorNPC spineTarget)
        {
            if (dialogueData == null)
            {
                Log.Error("[SpineDialogueView] DialogueData가 null입니다.");
                return;
            }

            // 열린 상태에서 다른 대화가 연쇄 시작될 수 있으므로 이전 세션을 먼저 정리한다.
            // 정리 없이 덮어쓰면 이전 NPC의 sortingOrder가 영영 복원되지 않는다
            RestoreCachedVisitor();

            // 다른 방문자 페이드 + sorting order로 앞으로 끌어올림 (줌은 상호작용 단위에서 관리)
            cachedVisitor = spineTarget;
            if (cachedVisitor != null)
            {
                savedSortingOrder = cachedVisitor.GetSortingOrder();
                cachedVisitor.SuspendDepthSorting();   // 자동 원근 정렬이 101을 덮어쓰지 않도록
                cachedVisitor.SetSortingOrder(101);

                VisitorManager.Instance?.SetOtherVisitorsFaded(true, cachedVisitor);
            }

            SetDialogueSubscription(true);
        }

        public override void Close()
        {
            SetResourceBarVisible(false);

            RestoreCachedVisitor();
            base.Close();
        }

        /// <summary>이전 대화 대상 NPC의 sortingOrder/페이드를 원복하고 캐시를 비운다</summary>
        private void RestoreCachedVisitor()
        {
            if (cachedVisitor == null) return;

            cachedVisitor.SetSortingOrder(savedSortingOrder);
            cachedVisitor.ResumeDepthSorting();
            VisitorManager.Instance?.SetOtherVisitorsFaded(false, cachedVisitor);
            cachedVisitor = null;
        }

        /// <summary>OnDialogueNodeChanged 구독을 플래그로 관리해 중복 구독을 막는다</summary>
        private void SetDialogueSubscription(bool subscribe)
        {
            if (DialogueManager.Instance == null) return;

            if (subscribe && !dialogueNodeSubscribed)
            {
                DialogueManager.Instance.OnDialogueNodeChanged += OnDialogueNodeChanged;
                dialogueNodeSubscribed = true;
            }
            else if (!subscribe && dialogueNodeSubscribed)
            {
                DialogueManager.Instance.OnDialogueNodeChanged -= OnDialogueNodeChanged;
                dialogueNodeSubscribed = false;
            }
        }

        protected override void SubscribeEvents()
        {
            dialogueButton?.onClick.AddListener(OnDialogueButtonClicked);
            backButton?.onClick.AddListener(OnBackButtonClicked);
        }

        protected override void UnsubscribeEvents()
        {
            dialogueButton?.onClick.RemoveListener(OnDialogueButtonClicked);
            backButton?.onClick.RemoveListener(OnBackButtonClicked);

            SetDialogueSubscription(false);
        }

        #endregion

        #region UI 업데이트

        private void OnDialogueNodeChanged(DialogueNode node)
        {
            if (node == null) return;

            UpdateSpeakerInfo(node);
            UpdateDialogueText(node);
            UpdateButtons(node);
        }

        private void UpdateSpeakerInfo(DialogueNode node)
        {
            if (DialogueManager.Instance == null) return;

            // 동적 치환된 화자 이름
            string displayName = DialogueManager.Instance.GetCurrentSpeakerName();

            if (speakerNameText != null)
            {
                speakerNameText.gameObject.SetActive(!string.IsNullOrEmpty(displayName));
                speakerNameText.text = displayName;
            }

            UpdateRoleLabel(displayName);
        }

        // 역할 라벨은 이름표의 한 쌍이라 화자 이름이 없는 대화에서는 같이 숨긴다
        // (한쪽만 남으면 역할 뱃지만 붕 뜬다). 표기 자체는 방문자 유형에서 온다.
        private void UpdateRoleLabel(string speakerName)
        {
            if (roleText == null) return;

            roleText.gameObject.SetActive(cachedVisitor != null && !string.IsNullOrEmpty(speakerName));
            if (cachedVisitor != null)
                roleText.text = UITranslator.GetVisitorRoleLabel(cachedVisitor.visitorType);
        }

        private void UpdateDialogueText(DialogueNode node)
        {
            if (dialogueText != null && DialogueManager.Instance != null)
            {
                // 동적 치환된 대화 텍스트
                dialogueText.text = DialogueManager.Instance.GetCurrentNodeText();
            }
        }

        private void UpdateButtons(DialogueNode node)
        {
            switch (node.nodeType)
            {
                case DialogueNodeType.Normal:
                    dialogueButton?.gameObject.SetActive(true);
                    if (dialogueButton != null) dialogueButton.interactable = true;
                    bool isLastNode = node.nextNodeIndex < 0;
                    canNextIndicator?.SetActive(!isLastNode);
                    endIndicator?.SetActive(isLastNode);
                    HideChoicePanel();
                    break;

                case DialogueNodeType.Choice:
                    // 대화 박스는 유지해 대사를 노출하고, 대사 클릭 진행만 차단한다.
                    if (dialogueButton != null) dialogueButton.interactable = false;
                    canNextIndicator?.SetActive(false);
                    endIndicator?.SetActive(false);
                    ShowChoicePanel(node.choices);
                    break;
            }
        }

        private void ShowChoicePanel(List<DialogueChoice> choices)
        {
            if (choicePanel != null)
                choicePanel.SetActive(true);

            // 기존 선택지 버튼 제거
            ClearChoiceButtons();

            // 새 선택지 버튼 생성
            bool hasGoldChoice = false;
            for (int i = 0; i < choices.Count; i++)
            {
                CreateChoiceButton(choices[i], i);
                if (choices[i].requireGoldCheck) hasGoldChoice = true;
            }

            // 골드가 필요한 선택지가 있으면 현재 골드를 볼 수 있도록 리소스바 표시
            SetResourceBarVisible(hasGoldChoice);
        }

        private void HideChoicePanel()
        {
            if (choicePanel != null)
                choicePanel.SetActive(false);

            ClearChoiceButtons();

            SetResourceBarVisible(false);
        }

        // 골드 필요 선택지가 보이는 동안에만 PlayerResourceBar를 유지한다.
        private void SetResourceBarVisible(bool visible)
        {
            if (visible == resourceBarShown) return;

            if (visible)
                UIPopupController.Instance?.ShowPlayerResourceBar();
            else
                UIPopupController.Instance?.HidePlayerResourceBar();

            resourceBarShown = visible;
        }

        private void CreateChoiceButton(DialogueChoice choice, int index)
        {
            if (choiceButtonPrefab == null || choiceContainer == null)
            {
                Log.Error("[SpineDialogueView] choiceButtonPrefab 또는 choiceContainer가 설정되지 않았습니다.");
                return;
            }

            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceContainer);
            activeChoiceButtons.Add(buttonObj);

            // 버튼 텍스트 설정
            var textComponent = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                // 동적 치환된 선택지 텍스트 (없으면 원본 사용)
                string choiceText = DialogueManager.Instance?.GetReplacedChoiceText(choice);
                if (string.IsNullOrEmpty(choiceText))
                {
                    choiceText = choice.choiceText;
                }

                // 조건 표시 (골드 필요)
                if (choice.requireGoldCheck)
                {
                    choiceText += $" ({choice.requiredGold:N0}G)";
                }

                textComponent.text = choiceText;
            }

            // 버튼 클릭 이벤트
            var button = buttonObj.GetOrAddComponent<Button>();
            if (button != null)
            {
                int choiceIndex = index; // 클로저용 로컬 변수
                button.onClick.AddListener(() => OnChoiceButtonClicked(choiceIndex));

                // 조건 체크 - 만족하지 않으면 버튼 비활성화
                bool canSelect = DialogueManager.Instance.CheckChoiceCondition(choice);
                button.interactable = canSelect;
            }
        }

        private void ClearChoiceButtons()
        {
            foreach (var btn in activeChoiceButtons)
            {
                if (btn != null)
                    Destroy(btn);
            }
            activeChoiceButtons.Clear();
        }

        #endregion

        #region 이벤트 핸들러

        private void OnDialogueButtonClicked()
        {
            Controller?.OnNextButtonClicked();
        }

        private void OnChoiceButtonClicked(int choiceIndex)
        {
            Controller?.OnChoiceSelected(choiceIndex);
        }

        private void OnBackButtonClicked() => OnEscapeCancelled();

        public override void OnEscapeCancelled()
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.GuardBack()) return;

            // 마무리 대화처럼 기능 없이 대화만 하는 경우 확인 없이 즉시 종료(onComplete 실행).
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsCurrentDialogueEscSkippable)
            {
                DialogueManager.Instance.EndDialogue();
                return;
            }

            // 대화별 지정 문구(예: 대장장이 타입 선택 취소)가 있으면 그것을, 없으면 화자 이름 기반 돌려보내기 문구.
            string message = DialogueManager.Instance?.CurrentEscCancelMessage;
            if (string.IsNullOrEmpty(message))
            {
                string speaker = speakerNameText != null ? speakerNameText.text : "";
                message = !string.IsNullOrEmpty(speaker)
                    ? UIPopupController.SendBackConfirmMessage(speaker)
                    : LocalizationSettings.StringDatabase.GetLocalizedString(
                          "UI_Screens", "SpineDialogue_EndConfirm");
            }

            UIPopupController.Instance?.ShowPopup(message,
                onConfirm: () => Controller?.OnCancelDialogue(),
                onCancel: () => { });
        }

        #endregion
    }
}
