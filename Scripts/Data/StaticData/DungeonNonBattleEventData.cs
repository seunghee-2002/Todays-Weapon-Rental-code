// Scripts/Data/StaticData/DungeonNonBattleEventData.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    // 비전투 이벤트(Entrance/TreasureChest/Rest/Trap/RareDrop/Protection/Retry/Retreat/Return/TrapEvade) 전용 데이터.
    [CreateAssetMenu(fileName = "DungeonNonBattleEventData", menuName = "TodaysWeaponRental/DungeonNonBattleEventData")]
    public class DungeonNonBattleEventData : DungeonEventData
    {
        [Header("비전투 노드 비주얼")]
        [Tooltip("비전투 노드의 프롭 스프라이트(보물상자/모닥불/가시 등). 진행 카드·리플레이 공통.")]
        public Sprite propSprite;
        [Tooltip("이벤트 고유 파티클(보물 발견/함정 적중/휴식/보호 등).")]
        public GameObject particlePrefab;

        [Header("리플레이 노드 결과 아이콘")]
        [Tooltip("성공 결과일 때 노드 아이콘으로 교체할 스프라이트. (TreasureChest/RareDrop/Rest/Trap회피)")]
        public Sprite resultSuccessIcon;
        [Tooltip("실패 결과일 때 노드 아이콘으로 교체할 스프라이트. (현재 Trap 피격만 사용)")]
        public Sprite resultFailIcon;
    }
}
