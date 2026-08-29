using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 튜토리얼 하이라이트 / 입력 차단 오버레이.
    /// 풀스크린 투명 blocker가 입력을 흡수하되, 지정한 대상(RectTransform)의 스크린 사각형 안쪽만
    /// 클릭이 통과된다(ICanvasRaycastFilter). 시각적으로는 대상 사각형을 뺀 4방향을 어둡게 덮어(4-way 딤)
    /// 대상 영역만 밝게 남기고, 화살표로 대상을 지목한다.
    ///
    /// 프리팹 구성:
    /// - 루트(이 컴포넌트): 풀스크린 투명 Image(알파 0, Raycast Target ON) — 입력 차단 전용. stretch·pivot (0.5, 0.5).
    /// - 자식 dimTop/dimBottom/dimLeft/dimRight: 어두운 반투명 Image(Raycast Target OFF), anchor·pivot (0.5, 0.5).
    /// - 자식 arrowImage: 화살표 스프라이트(Raycast Target OFF, 기본 스프라이트는 아래를 가리키는 형태), anchor·pivot (0.5, 0.5).
    /// </summary>
    public class TutorialHighlightView : BaseView, ICanvasRaycastFilter
    {
        [Header("Dim (대상 사각형을 뺀 4방향)")]
        [SerializeField] private RectTransform dimTop;
        [SerializeField] private RectTransform dimBottom;
        [SerializeField] private RectTransform dimLeft;
        [SerializeField] private RectTransform dimRight;

        [Header("Arrow")]
        [Tooltip("대상을 가리키는 화살표(기본 스프라이트는 아래를 가리키는 형태).")]
        [SerializeField] private RectTransform arrowImage;
        [Tooltip("대상과 화살표 사이 간격(px).")]
        [SerializeField] private float arrowGap = 12f;
        [Tooltip("화살표 통통 애니메이션 진폭(px).")]
        [SerializeField] private float arrowBounce = 10f;

        [Header("Dim 여백")]
        [Tooltip("대상보다 구멍(밝은 영역)을 얼마나 더 크게 낼지(px).")]
        [SerializeField] private float holePadding = 8f;

        [Header("클릭 정착")]
        [Tooltip("대상 지정 직후 이 시간(초) 동안은 구멍도 막아, 연타/버퍼 클릭이 하이라이트가 뜨자마자 대상을 통과하는 것을 막는다.")]
        [SerializeField] private float clickSettleDelay = 0.2f;

        private RectTransform targetRect;
        private RectTransform secondaryTargetRect;   // 있으면 targetRect와의 바운딩 박스 합집합을 하이라이트한다.
        private Canvas targetCanvas;
        private Tween arrowTween;
        private float targetSetTime;   // SetTarget 호출 시각(unscaled). 정착 지연 계산용.
        private Canvas arrowSortingCanvas;   // 11단계(상단바 소개)에서만 화살표를 상시 UI 위로 올리기 위한 오버라이드 캔버스.
        private Canvas overlaySortingCanvas;         // 상시 UI(하단바 등) 대상 단계에서 오버레이 루트 전체를 상시 UI 위로 올리는 오버라이드 캔버스.
        private GraphicRaycaster overlaySortingRaycaster;   // 위 오버라이드 캔버스에서도 블로커가 입력을 받도록 함께 붙이는 레이캐스터.

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;
            hideUIOnOpen = false;
            isCanClickOverlay = false;
            canEscape = false;
        }

        public override void Close()
        {
            Clear();
            base.Close();
        }

        #endregion

        #region View 제어

        /// <summary>강조·통과시킬 대상을 지정한다. null이면 전체 입력 차단(딤·화살표 숨김).</summary>
        public void SetTarget(RectTransform target)
        {
            targetRect = target;
            secondaryTargetRect = null;
            targetCanvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            targetSetTime = Time.unscaledTime;
            UpdateOverlay();
        }

        /// <summary>두 대상의 바운딩 박스 합집합을 하이라이트한다(예: 10단계 QuestInfo + MiniQuestInfo). 둘은 같은 캔버스 하위여야 한다.</summary>
        public void SetTargets(RectTransform primary, RectTransform secondary)
        {
            targetRect = primary;
            secondaryTargetRect = secondary;
            targetCanvas = primary != null ? primary.GetComponentInParent<Canvas>() : null;
            targetSetTime = Time.unscaledTime;
            UpdateOverlay();
        }

        /// <summary>대상 해제(전체 차단 상태로 되돌림) + 딤·화살표 숨김.</summary>
        public void Clear()
        {
            targetRect = null;
            secondaryTargetRect = null;
            targetCanvas = null;
            arrowTween?.Kill();
            arrowTween = null;
            SetArrowSortingOverride(0);     // 단계 종료 시 화살표 sortingOrder 오버라이드 원복
            SetOverlaySortingOverride(0);   // 단계 종료 시 오버레이 루트 sortingOrder 오버라이드 원복
            SetActiveAll(false);
        }

        /// <summary>
        /// 화살표만 지정 sortingOrder로 끌어올린다(11단계 상단바 소개 전용). 상시 UI 캔버스(sortingOrder 300)보다
        /// 높은 값을 주면 상단바 위로 화살표가 보인다. order가 0 이하이면 오버라이드를 해제한다.
        /// 화살표 오브젝트에만 Canvas(overrideSorting)를 씌우므로 딤·대사창 렌더 순서는 그대로 유지된다.
        /// </summary>
        public void SetArrowSortingOverride(int order)
        {
            if (arrowImage == null) return;

            if (order > 0)
            {
                if (arrowSortingCanvas == null)
                    arrowSortingCanvas = arrowImage.gameObject.AddComponent<Canvas>();
                arrowSortingCanvas.overrideSorting = true;
                arrowSortingCanvas.sortingOrder = order;
            }
            else if (arrowSortingCanvas != null)
            {
                arrowSortingCanvas.overrideSorting = false;
            }
        }

        /// <summary>
        /// 오버레이 루트(블로커/딤/화살표 전체)를 지정 sortingOrder로 끌어올린다(상시 UI 대상 단계 전용).
        /// SetArrowSortingOverride가 화살표만 올려 시각만 해결하는 것과 달리, 블로커까지 상시 UI(300) 위로 올려
        /// 대상 버튼을 뺀 나머지 상시 UI 클릭을 블로커가 흡수하도록 한다. order가 0 이하이면 오버라이드를 걷어(컴포넌트 제거)
        /// PanelCanvas 기본 렌더/레이캐스트 상태로 되돌린다 — 비상승 단계(NPC/패널 하이라이트) 동작에 흔적을 남기지 않는다.
        /// </summary>
        public void SetOverlaySortingOverride(int order)
        {
            if (order > 0)
            {
                if (overlaySortingCanvas == null)
                {
                    overlaySortingCanvas = gameObject.AddComponent<Canvas>();
                    // 오버라이드 캔버스에서도 블로커가 레이캐스트를 받아 입력을 흡수하도록 함께 붙인다.
                    overlaySortingRaycaster = gameObject.AddComponent<GraphicRaycaster>();
                }
                overlaySortingCanvas.overrideSorting = true;
                overlaySortingCanvas.sortingOrder = order;
            }
            else
            {
                // GraphicRaycaster는 Canvas를 요구하므로 레이캐스터를 먼저 제거한다.
                if (overlaySortingRaycaster != null) { Destroy(overlaySortingRaycaster); overlaySortingRaycaster = null; }
                if (overlaySortingCanvas != null) { Destroy(overlaySortingCanvas); overlaySortingCanvas = null; }
            }
        }

        protected override void SubscribeEvents() { }
        protected override void UnsubscribeEvents() { }

        #endregion

        #region 입력 차단 (ICanvasRaycastFilter)

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // 대상이 없으면 전체 차단(레이캐스트 유효 → blocker가 흡수).
            if (targetRect == null || !targetRect.gameObject.activeInHierarchy)
                return true;

            // 대상 지정 직후 짧은 정착 시간 동안은 구멍도 막는다. 연타/버퍼 클릭이 하이라이트가 뜨자마자
            // 대상을 눌러 튜토리얼 상태보다 앞서가는 것(예: 다음 안내를 덮는 팝업이 먼저 열림)을 방지.
            if (Time.unscaledTime - targetSetTime < clickSettleDelay)
                return true;

            // 대상 사각형(보조 대상 포함) 안쪽이면 무효 처리해 아래(대상)로 클릭을 흘려보낸다.
            Camera cam = GetTargetCamera();
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPoint, cam)
                || (secondaryTargetRect != null && secondaryTargetRect.gameObject.activeInHierarchy
                    && RectTransformUtility.RectangleContainsScreenPoint(secondaryTargetRect, screenPoint, cam));
            return !inside;
        }

        #endregion

        #region 내부 메서드

        private Camera GetTargetCamera()
        {
            if (targetCanvas == null) return null;
            return targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;
        }

        /// <summary>대상 사각형을 뺀 4방향 딤을 배치하고, 대상을 가리키는 화살표를 놓는다.</summary>
        private void UpdateOverlay()
        {
            var overlayRect = transform as RectTransform;
            if (overlayRect == null || targetRect == null || !targetRect.gameObject.activeInHierarchy)
            {
                arrowTween?.Kill();
                arrowTween = null;
                SetActiveAll(false);
                return;
            }

            // 오버레이(풀스크린) 로컬 경계
            Rect area = overlayRect.rect;
            float left = area.xMin, right = area.xMax, bottom = area.yMin, top = area.yMax;

            // 대상의 스크린 사각형 → 오버레이 로컬 사각형
            if (!TryGetTargetLocalRect(overlayRect, out float tL, out float tR, out float tB, out float tT))
            {
                SetActiveAll(false);
                return;
            }

            // 구멍을 대상보다 약간 크게
            tL -= holePadding; tB -= holePadding;
            tR += holePadding; tT += holePadding;

            // 대상 사각형을 화면 안으로 클램프(밖으로 나간 대상 방어)
            tL = Mathf.Clamp(tL, left, right);
            tR = Mathf.Clamp(tR, left, right);
            tB = Mathf.Clamp(tB, bottom, top);
            tT = Mathf.Clamp(tT, bottom, top);

            // 4-way 딤: 상/하는 전체 폭, 좌/우는 대상 높이 구간만 (모서리 중복 없음)
            SetDim(dimTop,    new Vector2((left + right) * 0.5f, (tT + top) * 0.5f),    new Vector2(right - left, top - tT));
            SetDim(dimBottom, new Vector2((left + right) * 0.5f, (bottom + tB) * 0.5f), new Vector2(right - left, tB - bottom));
            SetDim(dimLeft,   new Vector2((left + tL) * 0.5f, (tB + tT) * 0.5f),        new Vector2(tL - left, tT - tB));
            SetDim(dimRight,  new Vector2((tR + right) * 0.5f, (tB + tT) * 0.5f),       new Vector2(right - tR, tT - tB));

            PlaceArrow(top, tL, tR, tB, tT);
        }

        /// <summary>대상(들) 월드 코너 → 오버레이 로컬 사각형(min/max). 보조 대상이 있으면 두 사각형의 합집합을 반환.</summary>
        private bool TryGetTargetLocalRect(RectTransform overlayRect, out float tL, out float tR, out float tB, out float tT)
        {
            tL = tR = tB = tT = 0f;

            if (!TryGetSingleLocalRect(targetRect, overlayRect, out tL, out tR, out tB, out tT))
                return false;

            if (secondaryTargetRect != null && secondaryTargetRect.gameObject.activeInHierarchy &&
                TryGetSingleLocalRect(secondaryTargetRect, overlayRect, out float sL, out float sR, out float sB, out float sT))
            {
                tL = Mathf.Min(tL, sL); tR = Mathf.Max(tR, sR);
                tB = Mathf.Min(tB, sB); tT = Mathf.Max(tT, sT);
            }
            return true;
        }

        /// <summary>단일 RectTransform의 월드 코너 → 오버레이 로컬 사각형(min/max) 변환.</summary>
        private bool TryGetSingleLocalRect(RectTransform rt, RectTransform overlayRect, out float tL, out float tR, out float tB, out float tT)
        {
            tL = tR = tB = tT = 0f;
            if (rt == null) return false;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners); // 0=좌하, 2=우상
            Camera targetCam = GetTargetCamera();
            Camera overlayCam = GetOverlayCamera(overlayRect);

            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(targetCam, corners[0]);
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(targetCam, corners[2]);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenMin, overlayCam, out Vector2 localMin))
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenMax, overlayCam, out Vector2 localMax))
                return false;

            tL = Mathf.Min(localMin.x, localMax.x);
            tR = Mathf.Max(localMin.x, localMax.x);
            tB = Mathf.Min(localMin.y, localMax.y);
            tT = Mathf.Max(localMin.y, localMax.y);
            return true;
        }

        private void SetDim(RectTransform dim, Vector2 center, Vector2 size)
        {
            if (dim == null) return;
            dim.gameObject.SetActive(true);
            dim.anchoredPosition = center;
            dim.sizeDelta = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
        }

        /// <summary>대상 위(공간 부족 시 아래)에 화살표를 배치하고 통통 애니메이션을 건다.</summary>
        private void PlaceArrow(float top, float tL, float tR, float tB, float tT)
        {
            if (arrowImage == null) return;

            float centerX = (tL + tR) * 0.5f;
            float half = arrowImage.rect.height * 0.5f;

            // 기본: 대상 위에서 아래를 가리킴. 위 공간이 부족하면 대상 아래에서 위를 가리킴.
            float posY = tT + arrowGap + half;
            bool below = posY + half > top;
            if (below) posY = tB - arrowGap - half;

            arrowImage.gameObject.SetActive(true);
            arrowImage.localRotation = below ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;

            Vector2 basePos = new Vector2(centerX, posY);
            arrowImage.anchoredPosition = basePos;

            // 대상 쪽으로 통통(위 배치면 아래로, 아래 배치면 위로)
            float toward = below ? arrowBounce : -arrowBounce;
            arrowTween?.Kill();
            arrowTween = arrowImage.DOAnchorPosY(basePos.y + toward, 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true) // 튜토리얼 시간 정지 중에도 동작
                .SetLink(gameObject);
        }

        private Camera GetOverlayCamera(RectTransform reference)
        {
            var canvas = reference.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private void SetActiveAll(bool active)
        {
            dimTop?.gameObject.SetActive(active);
            dimBottom?.gameObject.SetActive(active);
            dimLeft?.gameObject.SetActive(active);
            dimRight?.gameObject.SetActive(active);
            arrowImage?.gameObject.SetActive(active);
        }

        #endregion
    }
}
