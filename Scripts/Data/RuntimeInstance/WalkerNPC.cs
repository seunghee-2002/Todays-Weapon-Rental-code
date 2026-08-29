using UnityEngine;
using DG.Tweening;
using Spine.Unity;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 상호작용/타이머/저장이 없는 경량 행인 NPC. spine 외형만 적용하고
    /// 길 한쪽 끝에서 반대쪽 끝까지 직선으로 지나간 뒤 스스로 파괴된다.
    /// </summary>
    public class WalkerNPC : MonoBehaviour
    {
        private AdventurerAppearanceApplier appearanceApplier;
        private float moveSpeed;
        private float runThreshold;
        private Tweener moveTween;

        // 원근 정렬: y가 높을수록 sortingOrder를 낮춰 뒤로 보낸다
        private MeshRenderer spineRenderer;

        // 튜토리얼은 시간을 강제 정지한 채 NPC 연출을 진행하므로 정지 동기화에서 제외한다.
        private bool IsTutorial => TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;

        /// <summary>
        /// 시간 정지에 반응하지 않는 경우.
        /// - 튜토리얼: 시간을 강제 정지한 채 NPC 연출을 진행한다.
        /// - 밤(21시 자동 정지): 전령만 움직이고 거리가 통째로 얼어붙으면 어색하므로 가던 길은 마저 가게 둔다.
        /// </summary>
        private bool IgnoresTimePause =>
            IsTutorial || (TimeManager.Instance != null && TimeManager.Instance.IsDayEnded());

        private void Update() => ApplyDepthSorting();

        #region 초기화

        /// <summary>
        /// 외형 적용 후 start -> end 직선 이동을 시작한다. 속도가 runThreshold를 넘으면 Run, 아니면 Walk.
        /// </summary>
        public void Initialize(AdventurerAppearanceData appearance, Vector3 start, Vector3 end, float speed, float runThreshold)
        {
            moveSpeed = speed;
            this.runThreshold = runThreshold;

            appearanceApplier = GetComponentInChildren<AdventurerAppearanceApplier>();
            if (appearance != null)
                appearanceApplier?.ApplyAppearance(appearance);

            transform.position = start;
            SetFacingDirection(end - start);
            PlayMoveAnimation();

            float duration = Vector3.Distance(start, end) / Mathf.Max(0.01f, moveSpeed);

            DOTween.Kill(transform);
            moveTween = transform.DOMove(end, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(false)
                .OnComplete(() => Destroy(gameObject))
                .SetLink(gameObject);

            // 게임 배속에 맞춰 이동 속도 동기화 + 이후 배속 변경 실시간 반영
            moveTween.timeScale = TimeManager.Instance != null ? TimeManager.Instance.CurrentTimeScale : 1f;
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeScaleChanged += HandleTimeScaleChanged;
                TimeManager.Instance.OnTimePausedChanged += HandleTimePausedChanged;
                ApplyTimePause(TimeManager.Instance.IsTimePaused);   // 정지 중 스폰(복원 등)이면 멈춘 상태로 시작
            }
        }

        private void HandleTimeScaleChanged(float scale)
        {
            if (moveTween != null && moveTween.IsActive())
                moveTween.timeScale = scale;
        }

        private void HandleTimePausedChanged(bool paused) => ApplyTimePause(paused);

        #endregion

        #region 내부 메서드

        /// <summary>
        /// 시간 정지 동기화. 이동 트윈은 실시간으로 도는 DOTween이라 정지에 반응하지 않으므로 직접 멈춘다.
        /// Spine 재생도 함께 멈춰 제자리걷기가 되지 않도록 한다.
        /// </summary>
        private void ApplyTimePause(bool paused)
        {
            if (paused && IgnoresTimePause) return;

            if (moveTween != null && moveTween.IsActive())
            {
                if (paused) moveTween.Pause();
                else moveTween.Play();
            }

            SetSpineTimeScale(paused ? 0f : 1f);
        }

        private void SetSpineTimeScale(float scale)
        {
            var skeletonAnim = GetComponentInChildren<SkeletonAnimation>();
            if (skeletonAnim != null)
            {
                skeletonAnim.timeScale = scale;
                return;
            }

            var skeletonGraphic = GetComponentInChildren<SkeletonGraphic>();
            if (skeletonGraphic != null)
                skeletonGraphic.timeScale = scale;
        }

        private void PlayMoveAnimation()
        {
            var partsManager = appearanceApplier?.GetPartsManager();
            partsManager?.PlayAnimation(moveSpeed > runThreshold ? "Run" : "Walk");
        }

        // y가 높을수록(멀수록) sortingOrder를 낮춰 뒤로. 안전 대역 안으로 클램프(매니저가 계산).
        private void ApplyDepthSorting()
        {
            if (spineRenderer == null)
            {
                var sa = GetComponentInChildren<SkeletonAnimation>();
                if (sa == null) return;
                spineRenderer = sa.GetComponent<MeshRenderer>();
                if (spineRenderer == null) return;
            }

            var mgr = VisitorManager.Instance;
            if (mgr == null) return;
            spineRenderer.sortingOrder = mgr.ComputeSortingOrder(transform.position.y);
        }

        /// <summary>이동 방향에 따라 캐릭터 좌우 반전 (VisitorNPC와 동일 규칙)</summary>
        private void SetFacingDirection(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) < 0.001f) return;

            Vector3 scale = transform.localScale;
            scale.x = direction.x < 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        /// <summary>모험가 상호작용 줌 시 방문자와 함께 흐려진다 (VisitorNPC.SetFaded와 동일 구현).</summary>
        public void SetFaded(bool faded, float fadedAlpha = 0.2f)
        {
            float targetAlpha = faded ? fadedAlpha : 1f;

            var skeletonAnim = GetComponentInChildren<SkeletonAnimation>();
            if (skeletonAnim != null)
            {
                DOTween.To(
                    () => skeletonAnim.skeleton.A,
                    x => skeletonAnim.skeleton.A = x,
                    targetAlpha, 0.3f).SetLink(gameObject);
                return;
            }

            var skeletonGraphic = GetComponentInChildren<SkeletonGraphic>();
            if (skeletonGraphic != null)
                skeletonGraphic.DOFade(targetAlpha, 0.3f).SetLink(gameObject);
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeScaleChanged -= HandleTimeScaleChanged;
                TimeManager.Instance.OnTimePausedChanged -= HandleTimePausedChanged;
            }
        }

        #endregion
    }
}
