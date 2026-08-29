using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 결과창의 결과 아이템 컨테이너에서 사용하는 개별 아이템 컴포넌트.
    /// Reputation / Affection / Material 모드를 한 컴포넌트가 모드 분기로 처리한다.
    /// </summary>
    public class AdventureResultItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image itemBG;
        [SerializeField] private Image itemGlow;
        [SerializeField] protected Image itemIcon;
        [SerializeField] protected TextMeshProUGUI itemCountText;
        [SerializeField] protected Button interactButton;
        [SerializeField] protected GameObject itemFocus;              // 재료 모드에서만 활성. 구매 시 비활성.

        protected ResultItemType resultItemType;
        private MaterialData materialData;
        private int quantity;
        private int totalPrice;
        private Action<AdventureResultItem> onPurchaseRequested;
        private bool isPurchased;

        public ResultItemType Type => resultItemType;
        public MaterialData MaterialData => materialData;
        public int Quantity => quantity;
        public int TotalPrice => totalPrice;
        public bool IsPurchased => isPurchased;

        #region 초기화

        public void InitializeMaterial(MaterialData data, int qty, float priceMultiplier, Action<AdventureResultItem> purchaseCallback)
        {
            if (data == null)
            {
                Log.Error("[AdventureResultItem] MaterialData가 null입니다.");
                return;
            }

            resultItemType = ResultItemTypeExtensions.FromMaterialType(data.materialType);
            materialData = data;
            quantity = qty;
            totalPrice = Mathf.RoundToInt(data.buyPrice * qty * priceMultiplier);
            onPurchaseRequested = purchaseCallback;
            isPurchased = false;

            ApplyVisualByType();

            if (itemIcon != null) itemIcon.sprite = data.icon;
            if (itemCountText != null)
            {
                itemCountText.text = $"x{qty}";
                itemCountText.color = ColorManager.Instance.GetWhiteColor();
            }

            SetInteractButtonActive(true);
            if (interactButton != null)
            {
                interactButton.onClick.RemoveAllListeners();
                interactButton.onClick.AddListener(OnInteractClicked);
            }

            itemFocus?.SetActive(true);

            PrepareForAppear();
        }

        #endregion

        #region 외부 인터페이스

        /// <summary>구매 완료 처리: itemFocus 비활성 + 버튼 비활성</summary>
        public void MarkPurchased()
        {
            isPurchased = true;
            itemFocus?.SetActive(false);
            if (interactButton != null)
                interactButton.interactable = false;
            if (itemCountText != null)
                itemCountText.color = ColorManager.Instance.GetGrayColor();
        }

        #endregion

        #region 내부 메서드

        protected void ApplyVisualByType()
        {
            if (itemBG != null)
                itemBG.sprite = IconManager.Instance.GetResultItemBG(resultItemType);

            if (itemGlow != null)
                itemGlow.color = ColorManager.Instance.GetResultItemGlowColor(resultItemType);
        }

        protected void SetCountTextSigned(int value)
        {
            if (itemCountText == null) return;
            itemCountText.text = value >= 0 ? $"+{value}" : $"{value}";
            itemCountText.color = value >= 0 ? ColorManager.Instance.GetGreenColor() : ColorManager.Instance.GetRedColor();
        }

        protected void SetInteractButtonActive(bool active)
        {
            if (interactButton == null) return;
            interactButton.enabled = active;
            interactButton.interactable = active;
        }

        protected virtual void OnInteractClicked()
        {
            if (isPurchased) return;
            onPurchaseRequested?.Invoke(this);
        }

        /// <summary>등장 애니메이션 직전 초기 상태 세팅: alpha=0, scale=0.8. 위치는 LayoutGroup에 위임.</summary>
        public void PrepareForAppear()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            transform.localScale = Vector3.one * 0.8f;
        }

        /// <summary>등장 애니메이션 재생. alpha 0→1, scale 0.8→1.</summary>
        public void PlayAppearAnimation()
        {
            if (canvasGroup != null) canvasGroup.DOFade(1f, 0.3f).SetLink(gameObject);
            transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        /// <summary>스킵 시 즉시 보이도록 최종 상태로 설정.</summary>
        public void SnapToFinalState()
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;
        }

        #endregion
    }
}
