using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 닉네임 변경 팝업 View.
    /// 입력 → 비용 표시(무료/유료) → 확인/취소. 실제 검증/변경 로직은 NicknameChangePopupController가 호출하는 NicknameManager에서 처리.
    /// </summary>
    public class NicknameChangePopupView : BaseView
    {
        [Header("입력")]
        [SerializeField] private TMP_InputField nicknameInput;

        [Header("비용 표시")]
        [SerializeField] private GameObject freeGroup;
        [SerializeField] private GameObject costGroup;
        [SerializeField] private TextMeshProUGUI costText;

        [Header("에러 표시")]
        [SerializeField] private GameObject errorObj;
        [SerializeField] private CanvasGroup errorCanvasGroup;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private float errorHoldDuration = 1.5f;
        [SerializeField] private float errorFadeDuration = 0.5f;

        [Header("버튼")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [SerializeField] private NicknameChangePopupController controller;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = true;
            isCanClickOverlay = true;
            canEscape = true;
        }

        public override void Open()
        {
            base.Open();
            if (nicknameInput != null) nicknameInput.SetTextWithoutNotify("");
            ClearError();
            RefreshCost();
            SetButtonsInteractable(true);
        }

        protected override void SubscribeEvents()
        {
            confirmButton?.onClick.AddListener(() => controller?.OnConfirmClicked());
            cancelButton?.onClick.AddListener(() => controller?.OnCancelClicked());
            // 서버 로드 전에는 비용이 유료로 폴백되므로, 로드가 끝나면 무료 표시로 다시 갱신한다
            if (NicknameManager.Instance != null)
                NicknameManager.Instance.OnChangeCountLoaded += RefreshCost;
        }

        protected override void UnsubscribeEvents()
        {
            confirmButton?.onClick.RemoveAllListeners();
            cancelButton?.onClick.RemoveAllListeners();
            if (NicknameManager.Instance != null)
                NicknameManager.Instance.OnChangeCountLoaded -= RefreshCost;
        }

        public override void OnEscapeClicked() => controller?.OnCancelClicked();

        #endregion

        #region View 업데이트

        public void RefreshCost()
        {
            int cost = NicknameManager.Instance?.GetNextChangeCost() ?? 0;
            bool isFree = cost == 0;

            if (freeGroup != null) freeGroup.SetActive(isFree);
            if (costGroup != null) costGroup.SetActive(!isFree);
            if (!isFree && costText != null)
            {
                costText.text = $"{cost:N0}";
                // 닉네임 변경은 유산으로 결제하므로 색상도 유산 보유 기준으로 판정.
                costText.color = (LegacyManager.Instance != null && LegacyManager.Instance.HasEnoughLegacyPoints(cost))
                    ? ColorManager.Instance.GetBlackColor()
                    : ColorManager.Instance.GetRedColor();
            }
        }

        public void ShowError(string msg)
        {
            if (errorObj == null || errorCanvasGroup == null) return;

            if (errorText != null) errorText.text = msg;

            errorCanvasGroup.DOKill();
            errorObj.SetActive(true);
            errorCanvasGroup.alpha = 1f;

            DOVirtual.DelayedCall(errorHoldDuration, () =>
            {
                if (errorCanvasGroup == null) return;
                errorCanvasGroup.DOFade(0f, errorFadeDuration)
                    .SetTarget(errorCanvasGroup)
                    .OnComplete(() =>
                    {
                        if (errorObj != null) errorObj.SetActive(false);
                    })
                    .SetLink(gameObject);
            }).SetTarget(errorCanvasGroup).SetLink(gameObject);
        }

        public void ClearError()
        {
            if (errorCanvasGroup != null) errorCanvasGroup.DOKill();
            if (errorObj != null) errorObj.SetActive(false);
        }

        public string GetNickname() => nicknameInput != null ? (nicknameInput.text ?? "").Trim() : "";

        public void SetButtonsInteractable(bool interactable)
        {
            if (confirmButton != null) confirmButton.interactable = interactable;
            if (cancelButton  != null) cancelButton.interactable  = interactable;
            canEscape = interactable;   // 서버 요청(닉네임 변경) 진행 중 ESC 차단
        }

        #endregion
    }
}
