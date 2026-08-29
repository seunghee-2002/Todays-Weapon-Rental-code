// Scripts/Systems/DialogueManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 대화 시스템 관리자
    /// </summary>
    public class DialogueManager : BaseManager<DialogueManager>
    {
        private Dictionary<string, string> dynamicTextReplacements = new Dictionary<string, string>();


        // 현재 진행 중인 대화 정보
        private DialogueData currentDialogue;
        private int currentNodeIndex;
        private Action onDialogueComplete;
        private Action<int> onChoiceSelected;

        // ESC(뒤로가기) 시 확인 팝업 없이 즉시 종료(onComplete 실행)할 대화인지 여부.
        // 마무리 대화처럼 아무 기능 없이 대화만 하는 경우 true.
        private bool currentEscSkippable;
        public bool IsCurrentDialogueEscSkippable => currentEscSkippable;

        // ESC(뒤로가기) 확인 팝업 문구를 대화별로 지정할 때 사용. null이면 화자 이름 기반 기본 문구.
        private string currentEscCancelMessage;
        public string CurrentEscCancelMessage => currentEscCancelMessage;

        // 대화창에서 sorting order로 강조할 월드 NPC. null이면 캐릭터 없이 텍스트만.
        private VisitorNPC currentSpineTarget;
        
        // 이벤트
        public event Action<DialogueNode> OnDialogueNodeChanged;

        #region 초기화

        public void Initialize(GameData gameData)
        {
            // DialogueData는 DataManager에서 관리
        }
        
        #endregion
        
        #region 대화 시작/진행

        /// <summary>
        /// 다음 노드로 진행 (Normal 타입용)
        /// </summary>
        public void AdvanceDialogue()
        {
            if (currentDialogue == null)
            {
                Log.Warn("[DialogueManager] 진행 중인 대화가 없습니다.");
                return;
            }
            
            var currentNode = GetCurrentNode();
            if (currentNode == null) return;
            
            if (currentNode.nodeType != DialogueNodeType.Normal)
            {
                Log.Warn("[DialogueManager] Normal 타입이 아닌 노드에서 AdvanceDialogue 호출됨");
                return;
            }
            
            MoveToNode(currentNode.nextNodeIndex);
        }
        
        /// <summary>
        /// 선택지 선택 (Choice 타입용)
        /// </summary>
        public void SelectChoice(int choiceIndex)
        {
            if (currentDialogue == null)
            {
                Log.Warn("[DialogueManager] 진행 중인 대화가 없습니다.");
                return;
            }
            
            var currentNode = GetCurrentNode();
            if (currentNode == null) return;
            
            if (currentNode.nodeType != DialogueNodeType.Choice)
            {
                Log.Warn("[DialogueManager] Choice 타입이 아닌 노드에서 SelectChoice 호출됨");
                return;
            }
            
            if (choiceIndex < 0 || choiceIndex >= currentNode.choices.Count)
            {
                Log.Error($"[DialogueManager] 잘못된 선택지 인덱스: {choiceIndex}");
                return;
            }
            
            var choice = currentNode.choices[choiceIndex];

            // 선택지 효과 적용
            ApplyChoiceEffects(choice);

            // 선택 결과 콜백 (수락/거절 등 도메인 처리용)
            onChoiceSelected?.Invoke(choiceIndex);

            // 다음 노드로 이동
            MoveToNode(choice.nextNodeIndex);
        }
        
        /// <summary>
        /// 대화 강제 종료
        /// </summary>
        public void EndDialogue()
        {
            if (currentDialogue == null) return;
            
            currentDialogue = null;
            currentNodeIndex = 0;

            // 대화창 닫기 (Close에서 NPC sorting order 원복)
            UIManager.Instance.ClosePanel<SpineDialogueView>();
            currentSpineTarget = null;

            onChoiceSelected = null;
            currentEscSkippable = false;
            currentEscCancelMessage = null;

            // 완료 콜백 실행. 콜백이 새 대화를 시작해 onDialogueComplete를 다시 세팅할 수 있으므로,
            // 로컬에 담고 필드를 먼저 비운 뒤 호출한다(재진입 시 새 콜백이 덮어써지는 것 방지).
            var complete = onDialogueComplete;
            onDialogueComplete = null;
            complete?.Invoke();

            ClearReplacements();
        }

        /// <summary>
        /// 대화 취소(ESC/뒤로가기). EndDialogue와 달리 onDialogueComplete를 호출하지 않아
        /// 상점 오픈·대장장이 타입 확정 등 완료 시 부작용이 발생하지 않는다.
        /// spineTarget이 있으면 즉시 EndInteraction으로 NPC를 돌려보낸다.
        /// </summary>
        public void CancelDialogue()
        {
            if (currentDialogue == null) return;

            var spineTarget = currentSpineTarget;

            currentDialogue = null;
            currentNodeIndex = 0;

            UIManager.Instance.ClosePanel<SpineDialogueView>();
            currentSpineTarget = null;

            onChoiceSelected = null;
            onDialogueComplete = null;
            currentEscSkippable = false;
            currentEscCancelMessage = null;

            ClearReplacements();

            spineTarget?.EndInteraction();
        }

        #endregion

        #region 동적 텍스트 설정

        /// <summary>
        /// 대화 시작. 모든 부가 인자는 선택.
        /// textReplacements: { "adventurerName": "용사", "shopName": "마법 대여점" }
        /// spineTarget: 대화 중 sorting order로 강조할 월드 NPC (null이면 캐릭터 없이 텍스트만)
        /// </summary>
        public void StartDialogue(
            string dialogueID,
            Action onComplete = null,
            Dictionary<string, string> textReplacements = null,
            VisitorNPC spineTarget = null,
            Action<int> onChoiceSelected = null,
            bool escSkippable = false,
            string escCancelMessage = null)
        {
            DialogueData data = DataManager.Instance.GetDialogue(dialogueID);
            if (data == null) return;

            if (data.nodes == null || data.nodes.Count == 0)
            {
                Log.Error($"[DialogueManager] 대화 노드가 비어있습니다: {dialogueID}");
                return;
            }

            if (data.isSkippable &&
                LegacyManager.Instance?.PlayerData != null &&
                !LegacyManager.Instance.PlayerData.isNPCDialogueEnabled)
            {
                onComplete?.Invoke();
                return;
            }

            // 동적 치환 데이터 저장
            dynamicTextReplacements = textReplacements ?? new Dictionary<string, string>();

            currentDialogue = data;
            currentNodeIndex = 0;
            onDialogueComplete = onComplete;
            this.onChoiceSelected = onChoiceSelected;
            currentSpineTarget = spineTarget;
            currentEscSkippable = escSkippable;
            currentEscCancelMessage = escCancelMessage;

            OpenDialoguePanel();

            // 첫 노드 표시
            ShowCurrentNode();
        }

        /// <summary>
        /// 대화창 패널을 연다. 모든 대화는 SpineDialogueView를 사용한다.
        /// </summary>
        private void OpenDialoguePanel()
        {
            UIManager.Instance?.OpenPanel<SpineDialogueView>(() =>
                UIControllerManager.Instance.GetController<SpineDialogueController>()
                    ?.Initialize(currentDialogue, currentSpineTarget));
        }

        /// <summary>
        /// 텍스트 치환 처리
        /// </summary>
        private string ReplaceText(string text)
        {
            if (string.IsNullOrEmpty(text) || dynamicTextReplacements.Count == 0)
                return text;
            
            string result = text;
            foreach (var kvp in dynamicTextReplacements)
            {
                result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// 현재 노드의 텍스트를 치환하여 반환
        /// </summary>
        public string GetCurrentNodeText()
        {
            var node = GetCurrentNode();
            if (node == null) return "";

            // 번역을 먼저 얹고 치환은 그 뒤에 한다 - 번역문에도 {ADVENTURER_NAME} 같은
            // 자리표시자가 그대로 들어 있어야 하기 때문이다.
            return ReplaceText(DataLocalizer.DialogueText(currentDialogue, currentNodeIndex, node.dialogueText));
        }

        /// <summary>
        /// 현재 노드의 화자 이름을 치환하여 반환
        /// </summary>
        public string GetCurrentSpeakerName()
        {
            var node = GetCurrentNode();
            if (node == null) return "";

            // 노드에 이름이 없으면 대화 기본 화자 - 키도 그에 맞춰 갈라진다.
            string speakerName = string.IsNullOrEmpty(node.speakerName)
                ? DataLocalizer.DialogueDefaultSpeaker(currentDialogue)
                : DataLocalizer.DialogueSpeaker(currentDialogue, currentNodeIndex, node.speakerName);

            return ReplaceText(speakerName);
        }

        /// <summary>
        /// 선택지 텍스트 치환하여 반환
        /// </summary>
        public string GetReplacedChoiceText(DialogueChoice choice)
        {
            if (choice == null) return "";

            // 선택지 키는 노드 인덱스까지 필요하다. 현재 노드에서 위치를 찾는다.
            var node = GetCurrentNode();
            int choiceIndex = node?.choices?.IndexOf(choice) ?? -1;
            if (choiceIndex < 0) return ReplaceText(choice.choiceText);

            return ReplaceText(DataLocalizer.DialogueChoice(
                currentDialogue, currentNodeIndex, choiceIndex, choice.choiceText));
        }

        /// <summary>
        /// 대화 종료 시 치환 데이터 정리
        /// </summary>
        private void ClearReplacements()
        {
            dynamicTextReplacements.Clear();
        }

        #endregion

        #region 대장장이

        /// <summary>
        /// 선택지 텍스트로부터 대장장이 타입 추출
        /// </summary>
        private BlacksmithType GetBlacksmithTypeFromChoice(DialogueChoice choice)
        {
            var node = GetCurrentNode();
            if (node == null) return BlacksmithType.None;

            int index = node.choices.IndexOf(choice);

            // 마지막 선택지는 "아무도 부르지 않는다" — 요청 없음(None)이면 다음 대장장이는 완전 랜덤 스폰
            if (index == node.choices.Count - 1)
                return BlacksmithType.None;

            var array = ConfigManager.Instance.Blacksmith.blacksmithDataArray;

            if (index >= 0 && index < array.Length)
                return array[index].type;

            Log.Warn($"[DialogueManager] 대장장이 선택지 인덱스 범위 초과: {index}");
            return BlacksmithType.None;
        }

        #endregion
        
        #region 내부 메서드
        
        private void ShowCurrentNode()
        {
            var node = GetCurrentNode();
            if (node == null)
            {
                EndDialogue();
                return;
            }
            
            OnDialogueNodeChanged?.Invoke(node);
        }
        
        private void MoveToNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= currentDialogue.nodes.Count)
            {
                // 대화 종료
                EndDialogue();
                return;
            }
            
            currentNodeIndex = nodeIndex;
            ShowCurrentNode();
        }
        
        private DialogueNode GetCurrentNode()
        {
            if (currentDialogue == null) return null;
            if (currentNodeIndex < 0 || currentNodeIndex >= currentDialogue.nodes.Count) return null;
            
            return currentDialogue.nodes[currentNodeIndex];
        }
        
        /// <summary>
        /// 선택지 효과 적용
        /// </summary>
        private void ApplyChoiceEffects(DialogueChoice choice)
        {
            // 골드 효과
            if (choice.giveGold)
            {
                if (choice.goldAmount > 0)
                    EconomyManager.Instance?.AddGold(choice.goldAmount, "대화 보상");
                else if (choice.goldAmount < 0)
                    EconomyManager.Instance?.SpendGold(-choice.goldAmount, "대화 지출");
            }
            
            // 아이템 효과 - 미구현. 조용히 무시되면 유저가 보상을 못 받았는지도 모르므로
            // 에러로 표면화한다. 구현 전까지 giveItem 플래그가 켜진 대화 데이터는 사용 금지
            if (choice.giveItem && !string.IsNullOrEmpty(choice.itemID))
            {
                Log.Error($"[DialogueManager] 아이템 지급 미구현 - 지급 무시됨: {choice.itemID} x{choice.itemCount} (해당 대화 데이터 수정 필요)");
            }
            
            // 대장장이 타입 선택 효과 (특별 처리)
            // 선택지 텍스트로 타입 판별
            if (currentDialogue != null && currentDialogue.StaticID == "BLACKSMITH_TYPE_SELECTION")
            {
                BlacksmithType selectedType = GetBlacksmithTypeFromChoice(choice);
                BlacksmithManager.Instance?.SetRequestedBlacksmithType(selectedType);
                Log.Info($"[DialogueManager] 대장장이 타입 요청: {selectedType}");
            }
        }
        
        #endregion
        
        #region 조건 체크
        
        /// <summary>
        /// 선택지 조건 만족 여부 확인
        /// </summary>
        public bool CheckChoiceCondition(DialogueChoice choice)
        {
            // 골드 조건
            if (choice.requireGoldCheck)
            {
                if (EconomyManager.Instance.CurrentGold < choice.requiredGold)
                    return false;
            }
            
            // 아이템 조건 - 미구현. 조건 검사 없이 통과시키는 대신 선택지를 차단한다.
            // 구현 전까지 requireItemCheck 플래그가 켜진 대화 데이터는 사용 금지
            if (choice.requireItemCheck)
            {
                Log.Error($"[DialogueManager] 아이템 조건 체크 미구현 - 선택지 차단: {choice.requiredItemID} (해당 대화 데이터 수정 필요)");
                return false;
            }

            return true;
        }
        
        #endregion
    }
}
