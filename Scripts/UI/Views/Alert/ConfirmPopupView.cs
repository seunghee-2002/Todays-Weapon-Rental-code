using System;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    public class ConfirmPopupView : BaseView
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
            canEscape = false;
        }
        
        /// <param name="confirmLabel">확인 버튼 텍스트. null이면 프리팹의 기본 라벨("예")을 유지한다.</param>
        public void Show(string message, Action onConfirm = null, Action onCancel = null, string confirmLabel = null)
        {
            if (messageText != null)
                messageText.text = message;

            // 확인 버튼 라벨은 평소 LocalizeStringEvent가 채운다. 직접 지정할 때는 그 컴포넌트를 꺼야
            // Open()의 OnEnable에서 기본 문구로 되돌아가지 않는다.
            if (!string.IsNullOrEmpty(confirmLabel) && confirmButtonText != null)
            {
                var localizeEvent = confirmButtonText.GetComponent<LocalizeStringEvent>();
                if (localizeEvent != null)
                    localizeEvent.enabled = false;

                confirmButtonText.text = confirmLabel;
            }

            onConfirmCallback = onConfirm;
            onCancelCallback = onCancel;

            cancelButton.gameObject?.SetActive(onCancel != null);
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

        public override void OnEscapeCancelled()
        {
            OnCancelClicked();
        }
    }
}