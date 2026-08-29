// Scripts/Data/StaticData/DungeonBattleEventData.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    // 전투 계열 이벤트(Battle/MiniBoss/Boss) 전용 데이터.
    [CreateAssetMenu(fileName = "DungeonBattleEventData", menuName = "TodaysWeaponRental/DungeonBattleEventData")]
    public class DungeonBattleEventData : DungeonEventData
    {
        [Header("전투 연출")]
        [Tooltip("오른쪽에 배치할 몬스터 프리팹. null이면 몬스터 미표시.")]
        public GameObject monsterPrefab;
        [Tooltip("몬스터 카테고리. 렌더 스테이지의 카메라 ortho 크기·스폰 오프셋을 타입별로 결정한다.")]
        public MonsterCategory monsterType;
    }
}
