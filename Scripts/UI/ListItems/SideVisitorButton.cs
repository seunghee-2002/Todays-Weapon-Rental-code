using System;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 사이드바에 표시되는 개별 방문자 버튼
    /// </summary>
    public class SideVisitorButton : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private AdventurerAppearanceApplier appearanceApplier; // 방문자 Spine 외형용
        [SerializeField] private GameObject appearanceRoot;                // SkeletonGraphic 루트 오브젝트
        [SerializeField] private Image timerFillImage; // 체류 시간 진행바
        [SerializeField] private Image labelImage;     // VisitorType별 라벨(색 깃발)
        [SerializeField] private Image labelIcon;      // VisitorType별 타입 아이콘
        [SerializeField] private float maxIconSize = 25f; // 아이콘의 가장 긴 변이 차지할 표시 크기(px)

        private VisitorNPC linkedVisitor;
        private Color originalFillColor;

        public event Action<VisitorNPC> OnButtonClicked;

        #region 초기화

        public void Initialize(VisitorNPC visitor)
        {
            linkedVisitor = visitor;

            // UI 초기화
            UpdatePortrait();
            UpdateLabel();

            if (timerFillImage != null)
                originalFillColor = timerFillImage.color;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(HandleButtonClick);
            }

            // 입장 중 포함, 생성 즉시 게이지를 초기화한다 (분모는 입장 보정 전 원래 체류시간).
            float remaining = visitor.RemainingTime;
            float ratio = visitor.BaseStayDuration > 0 ? Mathf.Clamp01(remaining / visitor.BaseStayDuration) : 0f;
            UpdateTimer(ratio);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }

        #endregion

        #region UI Update

        private void UpdatePortrait()
        {
            if (linkedVisitor == null) return;

            // 모든 방문자 타입을 Spine 외형으로 표시한다 (VisitorNPC.Appearance에 적용된 외형 재현).
            if (appearanceRoot != null)
                appearanceRoot.SetActive(true);

            appearanceApplier?.ApplyAppearance(linkedVisitor.Appearance);
        }

        /// <summary>
        /// VisitorType에 해당하는 라벨(색 깃발)과 타입 아이콘 표시
        /// </summary>
        private void UpdateLabel()
        {
            if (linkedVisitor == null || IconManager.Instance == null) return;

            if (labelImage != null)
                labelImage.sprite = IconManager.Instance.GetVisitorLabelImage(linkedVisitor.visitorType);

            if (labelIcon != null)
            {
                Sprite icon = IconManager.Instance.GetIconByVisitorType(linkedVisitor.visitorType);
                labelIcon.sprite = icon;

                // 아이콘마다 원본 크기가 달라, 가장 긴 변을 maxIconSize에 맞춰 비율 유지 스케일
                if (icon != null)
                {
                    Vector2 native = new(icon.rect.width, icon.rect.height);
                    float maxDim = Mathf.Max(native.x, native.y);
                    float scale = maxDim > 0 ? maxIconSize / maxDim : 1f;
                    labelIcon.rectTransform.sizeDelta = native * scale;
                }
            }
        }

        /// <summary>
        /// 타이머 UI 업데이트
        /// </summary>
        public void UpdateTimer(float fillRatio)
        {
            // 진행바 업데이트
            if (timerFillImage != null)
            {
                timerFillImage.fillAmount = fillRatio;
            }
        }

        /// <summary>
        /// 경고 상태 설정 (진행바 색상)
        /// </summary>
        public void SetWarningState(bool isWarning)
        {
            if (timerFillImage != null)
            {
                timerFillImage.color = isWarning
                    ? ColorManager.Instance.GetRedColor()
                    : originalFillColor;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleButtonClick()
        {
            if (linkedVisitor == null)
                return;

            OnButtonClicked?.Invoke(linkedVisitor);
        }

        #endregion
    }
}
