// Scripts/Data/DialogueData.cs
using UnityEngine;
using System;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 대화 노드 타입
    /// </summary>
    public enum DialogueNodeType
    {
        Normal,     // 일반 대화 (확인 버튼으로 다음 진행)
        Choice     // 선택지 (여러 옵션 중 선택)
    }

    /// <summary>
    /// 대화 선택지 데이터
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        public string choiceText;           // 선택지 텍스트
        public int nextNodeIndex;           // 이 선택지를 고르면 이동할 노드 인덱스 (-1이면 대화 종료)
        
        [Header("Conditions (Optional)")]
        public bool requireGoldCheck;       // 골드 조건 체크 필요 여부
        public int requiredGold;            // 필요 골드
        
        public bool requireItemCheck;       // 아이템 조건 체크 필요 여부
        public string requiredItemID;       // 필요 아이템 ID
        
        [Header("Effects (Optional)")]
        public bool giveGold;               // 골드 지급 여부
        public int goldAmount;              // 지급/차감 골드 (음수면 차감)
        
        public bool giveItem;               // 아이템 지급 여부
        public string itemID;               // 지급할 아이템 ID
        public int itemCount;               // 지급할 개수
    }

    /// <summary>
    /// 대화 노드 데이터
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        public DialogueNodeType nodeType = DialogueNodeType.Normal;
        
        [TextArea(3, 10)]
        public string dialogueText;         // 대화 텍스트
        
        public string speakerName;          // 화자 이름 (비어있으면 기본 NPC 이름 사용)

        [Header("Normal Type")]
        public int nextNodeIndex = -1;      // Normal 타입일 때 다음 노드 (-1이면 대화 종료)
        
        [Header("Choice Type")]
        public List<DialogueChoice> choices = new List<DialogueChoice>(); // Choice 타입일 때 선택지들
    }

    /// <summary>
    /// 대화 전체 데이터
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueData", menuName = "TodaysWeaponRental/DialogueData")]
    public class DialogueData : BaseData
    {
        public string dialogueName;         // 대화 이름

        public bool isSkippable = false;    // 설정에서 스킵 가능한 대화인지 여부
        
        [Header("Default Speaker")]
        public string defaultSpeakerName;   // 기본 화자 이름

        [Header("Dialogue Flow")]
        public List<DialogueNode> nodes = new List<DialogueNode>(); // 대화 노드 목록
    }
}