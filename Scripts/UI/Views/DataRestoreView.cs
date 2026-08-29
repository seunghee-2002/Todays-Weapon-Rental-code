using DG.Tweening;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    public enum RestoreInputMode { Default, Required, Error }

    public class DataRestoreView : BaseView
    {
        [Header("버튼")]
        [SerializeField] private Button closeButton;

        [Header("복원 코드 생성")]
        [SerializeField] private Button generateCodeButton;
        [SerializeField] private TMP_Text playerIdText;
        [SerializeField] private Button copyButton;

        [Header("복원 코드 입력")]
        [SerializeField] private TMP_InputField restoreCodeInput;
        [SerializeField] private TextMeshProUGUI restoreCodePlaceholder;
        [SerializeField] private Button restoreButton;

        [Header("복원 코드 입력 모드")]
        [SerializeField] private GameObject restoreInputBgDefault;
        [SerializeField] private GameObject restoreInputBgRequired;
        [SerializeField] private GameObject restoreInputBgError;
        [SerializeField] private Image restoreInputIcon;
        [SerializeField] private GameObject restoreInputIconRequired;
        [SerializeField] private GameObject restoreInputIconError;

        [Header("상태")]
        [SerializeField] private GameObject loadingIndicator;

        [Header("Toast")]
        [SerializeField] private TMP_Text alarmToastText;
        [SerializeField] private CanvasGroup alarmToastGroup;
        [SerializeField] private TMP_Text warningToastText;
        [SerializeField] private CanvasGroup warningToastGroup;

        private DataRestoreController controller;
        private Tween alarmToastTween;
        private Tween warningToastTween;

        public void SetController(DataRestoreController ctrl) => controller = ctrl;

        #region BaseView

        protected override void Awake()
        {
            base.Awake();
            canEscape = true;
        }

        public override void Open()
        {
            base.Open();
            loadingIndicator?.SetActive(false);
            SetConfirmVisible(false);
            HideAllToasts();
            restoreCodeInput?.SetTextWithoutNotify("");
            generateCodeButton?.gameObject.SetActive(true);
            copyButton?.gameObject.SetActive(false);
            SetRestoreInputMode(RestoreInputMode.Default);

            // 아래 둘은 "코드가 아직 없는" 기본 상태를 명시적으로 그린다.
            // 컨트롤러는 활성 코드가 있을 때만 값을 덮어쓰므로, 여기서 호출하지 않으면
            // 프리팹에 구워진 한국어가 그대로 남는다(번역이 안 먹는 것처럼 보임).
            SetRestoreInputInteractable(true);
            if (playerIdText != null)
                playerIdText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "DataRestore_CodeLabel");
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(() => controller?.OnCloseClicked());
            generateCodeButton?.onClick.AddListener(() => controller?.OnGenerateCodeClicked());
            copyButton?.onClick.AddListener(() => controller?.OnCopyClicked());
            restoreButton?.onClick.AddListener(() => controller?.OnRestoreClicked());
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            generateCodeButton?.onClick.RemoveAllListeners();
            copyButton?.onClick.RemoveAllListeners();
            restoreButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region View 업데이트

        public void SetCodeGenerated(string playerId)
        {
            if (playerIdText != null) playerIdText.text = playerId;
            generateCodeButton?.gameObject.SetActive(false);
            copyButton?.gameObject.SetActive(true);
        }

        public void ShowAlarmToast(string message)
        {
            alarmToastTween?.Kill();
            if (alarmToastText != null) alarmToastText.text = message;
            if (alarmToastGroup != null)
            {
                alarmToastGroup.alpha = 1f;
                alarmToastGroup.gameObject.SetActive(true);
                alarmToastTween = DOTween.Sequence()
                    .AppendInterval(0.5f)
                    .Append(alarmToastGroup.DOFade(0f, 0.5f))
                    .OnComplete(() => alarmToastGroup.gameObject.SetActive(false))
                    .SetLink(gameObject);
            }
        }

        public void ShowWarningToast(string message)
        {
            warningToastTween?.Kill();
            if (warningToastText != null) warningToastText.text = message;
            if (warningToastGroup != null)
            {
                warningToastGroup.alpha = 1f;
                warningToastGroup.gameObject.SetActive(true);
                warningToastTween = DOTween.Sequence()
                    .AppendInterval(0.5f)
                    .Append(warningToastGroup.DOFade(0f, 0.5f))
                    .OnComplete(() => warningToastGroup.gameObject.SetActive(false))
                    .SetLink(gameObject);
            }
        }

        public void SetRestoreInputMode(RestoreInputMode mode)
        {
            restoreInputBgDefault?.SetActive(mode == RestoreInputMode.Default);
            restoreInputBgRequired?.SetActive(mode == RestoreInputMode.Required);
            restoreInputBgError?.SetActive(mode == RestoreInputMode.Error);
            restoreInputIconRequired?.SetActive(mode == RestoreInputMode.Required);
            restoreInputIconError?.SetActive(mode == RestoreInputMode.Error);

            if (restoreInputIcon != null)
            {
                restoreInputIcon.color = mode switch
                {
                    RestoreInputMode.Required => HexColor("3A3246"),
                    RestoreInputMode.Error    => HexColor("A72834"),
                    _                         => HexColor("625268"),
                };
            }
        }

        public void SetRestoreInputInteractable(bool interactable)
        {
            if (restoreCodeInput != null) restoreCodeInput.interactable = interactable;
            if (restoreCodePlaceholder != null)
                restoreCodePlaceholder.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Messages", interactable ? "Restore_PlaceholderInput" : "Restore_PlaceholderDisabled");
            if (restoreButton    != null) restoreButton.interactable    = interactable;
        }

        public void SetLoading(bool loading)
        {
            loadingIndicator?.SetActive(loading);
            if (generateCodeButton != null) generateCodeButton.interactable = !loading;
            if (restoreButton      != null) restoreButton.interactable      = !loading;
            canEscape = !loading;   // 클라우드 복원/코드 생성 진행 중 ESC 차단
        }

        public void SetConfirmVisible(bool visible, string message = "")
        {
            if (!visible) return;

            UIPopupController.Instance?.ShowPopup(message, () => controller?.OnConfirmYesClicked(), () => controller?.OnConfirmNoClicked());    
        }

        public string GetRestoreCode() => restoreCodeInput != null ? restoreCodeInput.text : "";

        #endregion

        #region 내부 메서드

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        private void HideAllToasts()
        {
            alarmToastTween?.Kill();
            warningToastTween?.Kill();
            alarmToastGroup?.gameObject.SetActive(false);
            warningToastGroup?.gameObject.SetActive(false);
        }

        #endregion
    }
}
