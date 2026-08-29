using System.Collections;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// EnemyMonster 프리팹 공통 래퍼.
    /// 카테고리마다 다른 Animator 상태 이름({카테고리}_{동작}: 예 Animal_Idle, Bird_Attack)을
    /// 논리 동작(Idle/Walk/Attack/Hit/Dead)으로 추상화한다.
    /// 몬스터 Animator는 파라미터/트랜지션이 없어 Animator.Play(상태이름) 직접 호출 방식이다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MonsterStageActor : MonoBehaviour
    {
        public enum MonsterMotion { Idle, Walk, Attack, Hit, Dead }

        [SerializeField] private Animator animator;
        [Tooltip("상태 이름 접두사. 비우면 Animator 컨트롤러 이름에서 자동 추출(예: Animal, Bird).")]
        [SerializeField] private string statePrefix = "";

        #region 초기화

        private void Reset() => animator = GetComponent<Animator>();

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (string.IsNullOrEmpty(statePrefix))
                statePrefix = ResolvePrefix();
        }

        /// <summary>Animator 컨트롤러 이름에서 카테고리 접두사를 추출. (Animal.controller → "Animal")</summary>
        private string ResolvePrefix()
        {
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            if (controller == null)
            {
                Log.Warn($"[MonsterStageActor] {name}: Animator 컨트롤러가 없어 statePrefix를 추출하지 못했습니다.");
                return "";
            }
            return controller.name;
        }

        #endregion

        #region 애니메이션 재생

        private Coroutine motionRoutine;
        private bool deadFrozen;   // 사망 마지막 프레임 정지 상태 — 시간 정지 동기화가 되살리지 않도록 보호
        private float timeScale = 1f;   // 재생 배속 (리플레이 1x/2x/4x 동기화용, 기본 1)

        /// <summary>지정 동작을 재생하고 해당 클립 길이(초)를 반환. 공방 타이밍 동기화에 사용.
        /// 모든 몬스터 클립은 loop이므로, 1회성 동작은 PlayAttack/PlayHit/PlayDead로 재생해 반복을 막는다.</summary>
        public float Play(MonsterMotion motion)
        {
            if (animator == null) return 0f;
            if (motionRoutine != null) { StopCoroutine(motionRoutine); motionRoutine = null; }
            deadFrozen = false;
            animator.speed = timeScale;   // 사망 정지(speed=0) 이후 재사용 대비 복원 (배속 반영)
            animator.Play($"{statePrefix}_{motion}", 0, 0f);
            animator.Update(0f);   // 상태 전환을 즉시 평가해 현재 클립 길이를 읽을 수 있게 함
            return animator.GetCurrentAnimatorStateInfo(0).length;
        }

        public float PlayIdle()   => Play(MonsterMotion.Idle);

        // loop 클립이라 1회만 보이도록: 공격/피격은 끝나면 Idle 복귀, 사망은 마지막 프레임에서 정지.
        public float PlayAttack() => PlayOnce(MonsterMotion.Attack, freezeAtEnd: false);
        public float PlayHit()    => PlayOnce(MonsterMotion.Hit, freezeAtEnd: false);
        public float PlayDead()   => PlayOnce(MonsterMotion.Dead, freezeAtEnd: true);

        /// <summary>1회성 동작 재생 후 정리(Idle 복귀 또는 마지막 프레임 정지)를 예약한다.</summary>
        private float PlayOnce(MonsterMotion motion, bool freezeAtEnd)
        {
            float len = Play(motion);
            if (len > 0f && isActiveAndEnabled)
                motionRoutine = StartCoroutine(AfterClip(motion, len, freezeAtEnd));
            return len;
        }

        private IEnumerator AfterClip(MonsterMotion motion, float len, bool freezeAtEnd)
        {
            yield return new WaitForSeconds(len);
            motionRoutine = null;
            if (freezeAtEnd)
            {
                // 죽은 포즈 유지: 마지막 프레임으로 보낸 뒤 정지
                animator.Play($"{statePrefix}_{motion}", 0, 0.999f);
                animator.Update(0f);
                animator.speed = 0f;
                deadFrozen = true;
            }
            else
            {
                Play(MonsterMotion.Idle);   // 반복 방지: 기본 상태 복귀
            }
        }

        /// <summary>시간 정지 동기화: 애니메이션을 멈추거나(true) 재개(false)한다. 사망 정지 포즈는 유지한다.</summary>
        public void SetPaused(bool paused)
        {
            if (animator == null || deadFrozen) return;
            // 정지 중 예약된 클립 정리(AfterClip)가 Idle로 되돌리지 않도록 함께 멈춘다.
            if (paused && motionRoutine != null) { StopCoroutine(motionRoutine); motionRoutine = null; }
            animator.speed = paused ? 0f : timeScale;
        }

        /// <summary>재생 배속 설정(1=정상, 2/4=리플레이 가속). 사망 정지 포즈는 유지한다.</summary>
        public void SetTimeScale(float scale)
        {
            timeScale = Mathf.Max(0f, scale);
            if (animator != null && !deadFrozen) animator.speed = timeScale;
        }

        #endregion
    }
}
