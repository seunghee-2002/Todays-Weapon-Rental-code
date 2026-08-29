using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 대장간 메인 패널 View (CraftPanel / WeaponPanel 2분할 구조)
    /// </summary>
    public class BlacksmithView : BaseView
    {
        protected override string GetThemeBgmKey() => "Blacksmith";

        [Header("Title")]
        [Tooltip("현재 대장장이의 효과를 표시하는 텍스트")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Stage Indicator")]
        [SerializeField] private Toggle step1Toggle;
        [SerializeField] private Toggle step2Toggle;
        [SerializeField] private GameObject stage1Focus;
        [SerializeField] private GameObject stage2Focus;

        [Header("Panel Views")]
        [SerializeField] private BlacksmithCraftPanelView craftPanelView;
        [SerializeField] private BlacksmithWeaponPanelView weaponPanelView;
        [SerializeField] private GameObject disassemblePanel;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button prevButton;

        [SerializeField] private BlacksmithController controller;

        public BlacksmithWeaponPanelView GetWeaponPanelView() => weaponPanelView;
        public BlacksmithCraftPanelView GetCraftPanelView() => craftPanelView;
        public RectTransform GetNextButtonRect() => nextButton?.transform as RectTransform;   // 무기 패널 전환 버튼(튜토리얼 하이라이트용)

        public enum Panel { Craft, Weapon }

        private bool isSwitching = false;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = false;
            canEscape = false;
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(RequestClose);
            nextButton?.onClick.AddListener(OnNextClicked);
            prevButton?.onClick.AddListener(OnPrevClicked);

            step1Toggle?.onValueChanged.AddListener(isOn => { if (isOn) OnPanelClicked(Panel.Craft); });
            step2Toggle?.onValueChanged.AddListener(isOn => { if (isOn) OnPanelClicked(Panel.Weapon); });
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            nextButton?.onClick.RemoveAllListeners();
            prevButton?.onClick.RemoveAllListeners();
            step1Toggle?.onValueChanged.RemoveAllListeners();
            step2Toggle?.onValueChanged.RemoveAllListeners();
        }

        public override void Open()
        {
            base.Open();
            UIPopupController.Instance?.ShowPlayerResourceBar();
        }

        public override void Close()
        {
            UIPopupController.Instance?.HidePlayerResourceBar();
            base.Close();
        }

        public void Initialize()
        {
            UpdateTitle();
            SwitchPanel(Panel.Craft);
        }

        /// <summary>현재 대장장이의 효과를 제목에 표시. 효과가 없으면 숨긴다.</summary>
        private void UpdateTitle()
        {
            if (titleText == null) return;

            string effect = BlacksmithManager.Instance?.GetCurrentEffectDescription() ?? "";
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(effect));
            titleText.text = effect;
        }

        #endregion

        #region UI 업데이트 메서드

        public void SwitchPanel(Panel panel)
        {
            craftPanelView?.gameObject.SetActive(false);
            weaponPanelView?.gameObject.SetActive(false);
            disassemblePanel?.gameObject.SetActive(false);

            switch (panel)
            {
                case Panel.Craft:
                    craftPanelView?.gameObject.SetActive(true);
                    craftPanelView?.Initialize(controller);
                    break;
                case Panel.Weapon:
                    weaponPanelView?.gameObject.SetActive(true);
                    weaponPanelView?.Initialize(controller);
                    break;
            }

            nextButton?.gameObject.SetActive(panel == Panel.Craft);
            prevButton?.gameObject.SetActive(panel == Panel.Weapon);

            UpdateStageIndicator(panel);

            // 튜토리얼 2-C: 무기 패널이 열리면(목록 생성 완료) 다음 하이라이트로 진행
            if (panel == Panel.Weapon
                && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialWeaponPanelOpened();
        }

        private void UpdateStageIndicator(Panel panel)
        {
            isSwitching = true;

            bool isCraft = panel == Panel.Craft;

            if (step1Toggle != null) step1Toggle.isOn = isCraft;
            if (step2Toggle != null) step2Toggle.isOn = !isCraft;

            if (stage1Focus != null) stage1Focus.SetActive(!isCraft);
            if (stage2Focus != null) stage2Focus.SetActive(isCraft);

            isSwitching = false;
        }

        #endregion

        #region 이벤트 핸들러

        private void OnPanelClicked(Panel panel)
        {
            if (isSwitching) return;
            controller?.OnPanelSelected(panel);
        }

        private void OnNextClicked()
        {
            controller?.OnNextPanelClicked();
        }

        private void OnPrevClicked()
        {
            controller?.OnPrevPanelClicked();
        }

        private void OnCloseClicked()
        {
            controller?.OnCloseClicked();
        }

        // 닫기 버튼·ESC 공통 진입점: 나가기 확인 팝업 → 확인 시 실제 종료.
        private void RequestClose()
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.GuardBack()) return;
            UIPopupController.Instance?.ShowPopup(LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", "Blacksmith_ExitConfirm"), OnCloseClicked, () => { });
        }

        public override void OnEscapeCancelled() => RequestClose();

        #endregion
    }
}
