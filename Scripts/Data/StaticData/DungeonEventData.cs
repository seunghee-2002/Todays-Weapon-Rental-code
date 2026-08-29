// Scripts/Data/DungeonEventData.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    // 던전 이벤트 공통 베이스. 전투/비전투 전용 필드는 각각
    // DungeonBattleEventData / DungeonNonBattleEventData 서브클래스에 둔다.
    // (에셋 생성은 서브클래스의 CreateAssetMenu로만 한다 — 베이스는 메뉴 미노출)
    public class DungeonEventData : BaseData
    {
        public DungeonEventType eventType;
        public float probability;
        [TextArea(2, 4)]
        public string description; // 몬스터면 몬스터 이름, 이외는 이벤트 설명

        /// <summary>화면에 보일 설명. 한국어는 위 필드가 원본이고, 다른 언어는 Data 테이블에서 온다.</summary>
        public string DisplayDescription => DataLocalizer.DungeonEventDescription(this);

        [TextArea(1, 3)]
        public string summary; // 몬스터면 몬스터 영어 이름 (Animator 컨트롤러명 접두사). 표시용 아님 - 번역 제외
        [Tooltip("이 이벤트 후 다음 이벤트까지 대기 시간 (인게임 시간). 0이면 AdventureConfig.eventIntervalHours 사용")]
        public float intervalHours = 0f;
        [Tooltip("난이도 배수(0.8: 성공률의 80%적용)")]
        public float difficultyMultiplier = 1.0f;
    }
}