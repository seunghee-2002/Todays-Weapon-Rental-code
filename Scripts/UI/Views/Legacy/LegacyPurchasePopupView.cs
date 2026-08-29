using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 유산 소비 확인 팝업.
    /// 메시지 + 확인(유산 가격 표시) + 취소 버튼으로 구성.
    /// 골드 부족, 대출, talk, 수수료, 강화 재시도, 재부여 등 모든 유산 소비 상황에서 재사용.
    /// </summary>
    public class LegacyPurchasePopupView : BaseView
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;
        [SerializeField] private Button cancelButton;

        private Action onConfirmCallback;
        private Action onCancelCallback;

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = false;
            isCanClickOverlay = false;
            canEscape = true;
        }

        /// <summary>
        /// 팝업 내용을 설정하고 표시한다.
        /// </summary>
        /// <param name="message">본문 메시지 (예: "골드가 부족합니다.\n200G를 1유산으로 구매하시겠습니까?")</param>
        /// <param name="confirmLabel">확인 버튼 텍스트 (예: "1 유산")</param>
        /// <param name="onConfirm">확인 콜백</param>
        /// <param name="onCancel">취소 콜백</param>
        public void Show(string message, string confirmLabel, Action onConfirm = null, Action onCancel = null)
        {
            if (messageText != null)
                messageText.text = message;

            if (confirmButtonText != null)
                confirmButtonText.text = confirmLabel;

            onConfirmCallback = onConfirm;
            onCancelCallback = onCancel;
        }

        protected override void SubscribeEvents()
        {
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            cancelButton?.onClick.AddListener(OnCancelClicked);
        }

        protected override void UnsubscribeEvents()
        {
            confirmButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
        }

        private void OnConfirmClicked()
        {
            var callback = onConfirmCallback;
            onConfirmCallback = null;
            onCancelCallback = null;
            Close();
            callback?.Invoke();
        }

        private void OnCancelClicked()
        {
            var callback = onCancelCallback;
            onConfirmCallback = null;
            onCancelCallback = null;
            Close();
            callback?.Invoke();
        }

        public override void OnEscapeClicked()
        {
            OnCancelClicked();
        }
    }
}
