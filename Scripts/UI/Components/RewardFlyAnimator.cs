using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace TodaysWeaponRental
{
    public class RewardFlyAnimator : MonoBehaviour
    {
        // 이 배속 이상에서는 도착 효과음을 재생하지 않는다.
        // pitch를 배속에 맞춰 올리는 탓에 소리가 찢어지고, 노드마다 반복돼 산만해진다.
        private const float ArrivalSfxMaxSpeed = 2f;

        [SerializeField] private Image flyIconPrefab;
        [SerializeField] private Transform flyIconParent;
        [SerializeField] private Sprite goldSprite;
        [SerializeField] private float flyDuration = 0.5f;
        [SerializeField] private float arcHeight = 2.5f;
        [SerializeField] private float arrivalPunchStrength = 0.35f;
        [SerializeField] private float arrivalPunchDuration = 0.2f;
        [SerializeField] private string goldArrivalSFX = "GetGold";

        public Coroutine FlyGold(RectTransform source, RectTransform target, float speedMultiplier, Action onArrived)
            => StartCoroutine(FlyCoroutine(goldSprite, source, target, speedMultiplier, onArrived, goldArrivalSFX, punchTarget: true));

        // 재료는 여러 개가 같은 타겟으로 동시 도착 → 펀치 스케일이 누적돼 아이콘이 커지므로 펀치 없음(크기 불변).
        public Coroutine FlyMaterial(Sprite icon, RectTransform source, RectTransform target, float speedMultiplier, Action onArrived)
            => StartCoroutine(FlyCoroutine(icon, source, target, speedMultiplier, onArrived, null, punchTarget: false));

        public void CancelAll()
        {
            StopAllCoroutines();
            if (flyIconParent == null) return;
            for (int i = flyIconParent.childCount - 1; i >= 0; i--)
            {
                var child = flyIconParent.GetChild(i);
                DOTween.Kill(child);
                Destroy(child.gameObject);
            }
        }

        private IEnumerator FlyCoroutine(Sprite sprite, RectTransform source, RectTransform target, float speedMultiplier, Action onArrived, string arrivalSFX, bool punchTarget)
        {
            if (flyIconPrefab == null)
            {
                onArrived?.Invoke();
                yield break;
            }

            float dur = flyDuration / Mathf.Max(0.1f, speedMultiplier);

            var icon = Instantiate(flyIconPrefab, flyIconParent);
            icon.sprite = sprite;
            icon.raycastTarget = false;   // 이동 중 fly 아이콘이 버튼 클릭을 가로채지 않도록
            icon.gameObject.SetActive(true);

            Vector3 from = source.position;
            Vector3 to   = target.position;
            Vector3 mid  = (from + to) * 0.5f + Vector3.up * arcHeight;

            icon.transform.position = from;

            bool done = false;
            icon.transform.DOPath(new[] { mid, to }, dur, PathType.CatmullRom)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => done = true)
                .SetLink(icon.gameObject);

            yield return new WaitUntil(() => done);

            if (punchTarget)
            {
                // DOPunchScale은 시작 시점의 스케일을 복귀 기준값으로 캡처한다. 도착이 촘촘하면(고배속 등)
                // 펀치가 겹쳐 부푼 중간값이 기준값이 되어 크기가 누적된 채 고정되므로, 진행 중 펀치를 끊고
                // 원래 크기로 되돌린 뒤 새로 건다.
                target.DOKill();
                target.localScale = Vector3.one;
                target.DOPunchScale(Vector3.one * arrivalPunchStrength, arrivalPunchDuration / speedMultiplier, 5, 0.5f)
                      .SetLink(target.gameObject);
            }
            if (!string.IsNullOrEmpty(arrivalSFX) && speedMultiplier < ArrivalSfxMaxSpeed)
                SoundManager.Instance?.PlaySFX(arrivalSFX, speedMultiplier);
            Destroy(icon.gameObject);
            onArrived?.Invoke();
        }
    }
}
