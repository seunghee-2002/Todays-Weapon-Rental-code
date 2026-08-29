using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험진행 카드 animationArea 연출의 타입 단위 타이밍/공용 애니메이션 설정.
    /// 이벤트 개체별 비주얼(몬스터 프리팹·프롭 스프라이트·고유 파티클)은 DungeonEventData에 둔다.
    /// </summary>
    [CreateAssetMenu(fileName = "AdventureProgressAnimConfig", menuName = "TodaysWeaponRental/Config/AdventureProgressAnimConfig")]
    public class AdventureProgressAnimConfig : ScriptableObject
    {
        [Header("단계 경계 (현재 이벤트 진행률 0~1, 소프트 타깃)")]
        [Tooltip("시작(입장) 단계 끝 — 이후 실행 단계 진입")]
        [Range(0f, 1f)] public float enterPhaseEnd = 0.05f;
        [Tooltip("실행 단계 끝 — 이 지점 넘으면 새 공방을 시작하지 않고, 진행 중 애니메이션을 끝낸 뒤 완료로 전환")]
        [Range(0f, 1f)] public float executePhaseEnd = 0.80f;
        [Tooltip("완료 단계 끝 — 이후 이동 단계(다음 노드로 달려가며 여유 시간)")]
        [Range(0f, 1f)] public float completePhaseEnd = 0.90f;

        [Header("모험가 애니메이션 이름")]
        public string idleAnim = "Idle";
        public string runAnim = "Run";
        public string runGearAnim = "Run_Gear";
        public string hitAnim = "Hit";
        public string dieAnim = "Die";
        [Tooltip("맨손(무기 미대여) 시 공격 애니메이션")]
        public string barehandAttackAnim = "Attack1";

        [Header("함정 연출")]
        [Tooltip("함정: 밟기(Hit+파티클) 반복 간격(초). 사이에는 Idle")]
        public float trapHitInterval = 2.5f;

        [Header("전투 공방 타이밍 (초)")]
        [Tooltip("공격 모션 중 타격이 적중하는 시점 비율(0~1). 작을수록 일찍 피격, 클수록 공격 후반에 피격")]
        [Range(0f, 1f)] public float attackImpactRatio = 0.55f;
        public float attackDuration = 0.5f;
        public float hitReactDuration = 0.3f;
        public float exchangeGap = 0.15f;
        [Tooltip("모험가 공격 완료(이펙트) 후 몬스터 피격 이펙트가 나갈 때까지의 짧은 지연(초).")]
        public float attackToHitDelay = 0.1f;

        [Header("원거리 무기(활/석궁) 공방 타이밍 (초)")]
        [Tooltip("발사 애니 재생 시작 지점(TrackTime) — 화살을 당기기 시작하는 구간. Attack_Bow/Attack_Bolt 기준 0.667")]
        public float rangedDrawStartTime = 0.667f;
        [Tooltip("원거리 발사 애니 시작 후 실제 발사(공격 완료)까지 지연(초). 당기기 시작~발사 이벤트까지")]
        public float rangedEffectDelay = 0.333f;

        [Header("공용 피격 파티클")]
        public GameObject adventurerHitParticle;
        public GameObject monsterHitParticle;

        [Header("공격 파티클")]
        [Tooltip("모험가 공격 시작 시 재생(고정). 무기 종류와 무관하게 동일.")]
        public GameObject adventurerAttackParticle;
        [Tooltip("몬스터 공격 시작 시 재생 — 일반 전투(Battle)")]
        public GameObject monsterAttackParticleNormal;
        [Tooltip("몬스터 공격 시작 시 재생 — 정예(MiniBoss)")]
        public GameObject monsterAttackParticleElite;
        [Tooltip("몬스터 공격 시작 시 재생 — 보스(Boss)")]
        public GameObject monsterAttackParticleBoss;

        /// <summary>전투 타입별 몬스터 공격 파티클. 알 수 없는 타입은 일반으로 처리.</summary>
        public GameObject GetMonsterAttackParticle(DungeonEventType type) => type switch
        {
            DungeonEventType.MiniBoss => monsterAttackParticleElite,
            DungeonEventType.Boss     => monsterAttackParticleBoss,
            _                         => monsterAttackParticleNormal,
        };
    }
}
