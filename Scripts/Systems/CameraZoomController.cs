using UnityEngine;
using DG.Tweening;
using System;

namespace TodaysWeaponRental
{
    public class CameraZoomController : BaseManager<CameraZoomController>
    {
        [Header("Camera")]
        [SerializeField] private Camera mainCamera;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomedSize = 2f;       // 줌인 후 Orthographic Size
        [SerializeField] private float zoomDuration = 0.4f;   // 줌인/아웃 시간

        [SerializeField] private bool isAnimating;
        public bool IsAnimating => isAnimating;

        private float originalSize;
        private Vector3 originalPosition;
        private bool isZoomed;

        protected override void Awake()
        {
            base.Awake();

            if (mainCamera == null)
                mainCamera = Camera.main;

            originalSize = mainCamera.orthographicSize;
            originalPosition = mainCamera.transform.position;
        }

        /// <summary>
        /// 특정 월드 위치로 줌인
        /// </summary>
        public void ZoomIn(Vector2 targetWorldPos, Action onComplete = null)
        {
            if (isZoomed) return;
            isZoomed = true;
            isAnimating = true;

            DOTween.Kill(mainCamera.transform);
            DOTween.Kill(mainCamera);

            Vector3 targetPos = new Vector3(targetWorldPos.x, targetWorldPos.y, originalPosition.z);

            DOTween.To(
                () => mainCamera.orthographicSize,
                x => mainCamera.orthographicSize = x,
                zoomedSize,
                zoomDuration
            ).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(mainCamera).SetLink(mainCamera.gameObject);

            mainCamera.transform.DOMove(targetPos, zoomDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    isAnimating = false;
                    onComplete?.Invoke();
                })
                .SetLink(mainCamera.gameObject);
        }

        /// <summary>
        /// 원래 위치/사이즈로 줌아웃
        /// </summary>
        public void ZoomOut(Action onComplete = null)
        {
            if (!isZoomed) return;
            isAnimating = true;

            // 진행 중이던 줌인 트윈을 취소한다. 취소하지 않으면 줌인 트윈이 남아
            // 줌아웃 후에도 카메라가 다시 줌인된 상태로 고착될 수 있다.
            DOTween.Kill(mainCamera.transform);
            DOTween.Kill(mainCamera);

            Sequence seq = DOTween.Sequence();
            seq.Append(
                DOTween.To(
                    () => mainCamera.orthographicSize,
                    x => mainCamera.orthographicSize = x,
                    originalSize,
                    zoomDuration
                ).SetEase(Ease.OutCubic)
            );
            seq.Join(
                mainCamera.transform.DOMove(originalPosition, zoomDuration).SetEase(Ease.OutCubic)
            );
            seq.SetUpdate(true);
            seq.SetLink(mainCamera.gameObject);
            seq.OnComplete(() =>
            {
                isZoomed = false;
                isAnimating = false;
                onComplete?.Invoke();
            });
        }
    }
}