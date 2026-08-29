using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 마법 재부여 팝업 View (WeaponPanel에서 재부여 버튼 클릭 시 열림)
    /// 재부여 실행 결과(이전/새 효과 선택)와 추가 재부여까지 단일 팝업에서 처리한다.
    /// </summary>
    public class BlacksmithRerollConfirmView : BaseView
    {
        [Header("Weapon Info")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private Image BGImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TextMeshProUGUI enforceLevelText;
        [SerializeField] private TextMeshProUGUI rerollCountText;

        [Header("Effect List (잠금 버튼 포함)")]
        [SerializeField] private Transform effectListContainer;
        [SerializeField] private GameObject rerollEffectItemPrefab;

        [Header("Cost Info")]
        [SerializeField] private TextMeshProUGUI goldCostText;

        [Header("Buttons")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollButtonText;
        [SerializeField] private Button addRerollButton;
        [SerializeField] private TextMeshProUGUI addRerollCostText;
        [SerializeField] private Button closeButton;

        [Header("Reopen (재부여 완료)")]
        [SerializeField] private GameObject reopenRerollGroup;

        [Header("Select Panel (이전/새 효과 선택)")]
        [SerializeField] private GameObject selectPanel;
        [SerializeField] private Transform beforeEffectContainer;
        [SerializeField] private Transform afterEffectContainer;
        [SerializeField] private Button selectBeforeButton;
        [SerializeField] private Button selectAfterButton;
        [SerializeField] private GameObject effectListItemPrefab;
        [SerializeField] private TextMeshProUGUI selectRerollCountText;

        private BlacksmithController controller;
        private WeaponInstance selectedWeapon;
        private List<WeaponEffect> pendingAfterEffects;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = true;
            canEscape = true;
        }

        protected override void SubscribeEvents()
        {
            rerollButton?.onClick.AddListener(OnRerollClicked);
            addRerollButton?.onClick.AddListener(OnAddRerollClicked);
            closeButton?.onClick.AddListener(OnCloseClicked);
            selectBeforeButton?.onClick.AddListener(OnSelectBefore);
            selectAfterButton?.onClick.AddListener(OnSelectAfter);
        }

        protected override void UnsubscribeEvents()
        {
            rerollButton?.onClick.RemoveAllListeners();
            addRerollButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
            selectBeforeButton?.onClick.RemoveAllListeners();
            selectAfterButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(WeaponInstance weapon, BlacksmithController ctrl, bool clearLocked = true)
        {
            controller = ctrl;
            selectedWeapon = weapon;
            pendingAfterEffects = null;
            if (clearLocked) controller?.OnRerollWeaponSelected(weapon);

            reopenRerollGroup?.SetActive(false);
            SetSelectPanelActive(false);
            RefreshUI();

            // 튜토리얼 2-E: 재부여 확인 화면이 열리면 재부여 실행 버튼 하이라이트
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialRerollConfirmOpened();
        }

        /// <summary>튜토리얼 하이라이트용 — 재부여 실행 버튼 RectTransform.</summary>
        public RectTransform GetRerollButtonRect()
        {
            var rect = rerollButton?.transform as RectTransform;
            // 버튼이 HorizontalLayoutGroup 안이라 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }

        /// <summary>튜토리얼 하이라이트용 — "이전 선택" 버튼(=이전 효과 컨테이너) RectTransform.</summary>
        public RectTransform GetSelectBeforeButtonRect()
        {
            var rect = selectBeforeButton?.transform as RectTransform;
            // ContentSizeFitter로 효과 개수에 따라 크기가 달라지므로 위치/크기를 읽기 전에 즉시 리빌드.
            if (rect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            return rect;
        }

        #endregion

        #region UI 갱신

        public void RefreshUI()
        {
            if (selectedWeapon == null) return;

            var weapon = selectedWeapon;

            if (weaponIcon != null && weapon.weaponData.icon != null)
                weaponIcon.sprite = weapon.weaponData.icon;
            if (weaponNameText != null)
            {
                weaponNameText.text = weapon.weaponData.DisplayName + $"+{weapon.enforceLevel}";
                weaponNameText.color = ColorManager.Instance.GetGradeAccentColor(weapon.currentGrade);
            }
            if (BGImage != null)
                BGImage.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
            if (frameImage != null)
                frameImage.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            if (enforceLevelText != null)
                enforceLevelText.text = $"+{weapon.enforceLevel}";
            if (rerollCountText != null)
                rerollCountText.text = L("Blacksmith_RerollCount",
                    ("count", weapon.rerollCount), ("max", weapon.DisplayMaxRerollCount));

            BuildEffectList(weapon);
            UpdateCostDisplay(weapon);
            UpdateRerollButton(weapon);
            UpdateAddRerollButton(weapon);
        }

        private void BuildEffectList(WeaponInstance weapon)
        {
            if (effectListContainer == null) return;

            for (int i = effectListContainer.childCount - 1; i >= 0; i--)
            {
                var child = effectListContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            for (int i = 0; i < weapon.effects.Count; i++)
            {
                int index = i;
                var obj = Instantiate(rerollEffectItemPrefab, effectListContainer);
                var item = obj.GetComponent<RerollEffectItem>();
                item?.Initialize(
                    weapon.effects[i],
                    controller.IsRerollEffectLocked(index),
                    () => controller.OnRerollEffectLockToggled(index, weapon)
                );
            }
        }

        public void UpdateCostDisplay(WeaponInstance weapon)
        {
            if (goldCostText == null) return;

            int lockedCount = controller.GetLockedEffectCount();

            if (controller.IsAllEffectsLocked(weapon))
            {
                string redHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetRedColor());
                rerollButtonText.text = L("Blacksmith_RerollUnavailable", ("color", redHex));
                goldCostText.text = "-";
                return;
            }
            else
            {
                rerollButtonText.text = LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", "Common_Reroll");
            }

            int goldCost = BlacksmithManager.Instance.GetRerollGoldCost(weapon, lockedCount, out int costBeforeDiscount);
            goldCostText.text = UITranslator.GetGoldCostString(costBeforeDiscount, goldCost);
            goldCostText.color = EconomyManager.Instance.CurrentGold >= goldCost
                ? ColorManager.Instance.GetBlackColor()
                : ColorManager.Instance.GetRedColor();
        }

        public void UpdateRerollButton(WeaponInstance weapon)
        {
            if (rerollButton == null) return;

            // 재부여를 다 썼으면 버튼 숨김 (이때 addRerollButton/closeButton만 노출).
            bool canReroll = BlacksmithManager.Instance.CanAffordReroll(weapon);
            rerollButton.gameObject.SetActive(canReroll);
            if (!canReroll) return;

            // 전체 잠금 여부는 컨트롤러의 세션 상태이므로 함께 확인. 골드는 EnsureGold가 보충.
            rerollButton.interactable = !controller.IsAllEffectsLocked(weapon);
        }

        private void UpdateAddRerollButton(WeaponInstance weapon)
        {
            if (addRerollButton == null) return;

            bool canExtra = weapon.CanExtraReroll;
            addRerollButton.gameObject.SetActive(canExtra);

            if (canExtra && addRerollCostText != null)
            {
                int legacyCost = LegacyManager.Instance?.GetExtraRerollCost(weapon) ?? 0;
                addRerollCostText.text = $"{legacyCost}";
            }
        }

        #endregion

        #region 재부여 결과 표시 (Controller로부터 호출)

        /// <summary>
        /// 재부여 실행 후 이전/새 효과 선택 오버레이를 띄운다(결과 팝업 대체).
        /// </summary>
        public void ShowRerollResult(WeaponInstance weapon, List<WeaponEffect> before, List<WeaponEffect> after)
        {
            selectedWeapon = weapon;
            pendingAfterEffects = after;

            reopenRerollGroup?.SetActive(true);
            if (selectRerollCountText != null)
                selectRerollCountText.text = L("Blacksmith_RerollNth", ("count", weapon.rerollCount));

            BuildReadonlyEffectList(beforeEffectContainer,  before);
            BuildReadonlyEffectList(afterEffectContainer, after);

            RefreshUI();
            SetSelectPanelActive(true);   // RefreshUI 뒤에 호출해야 메인 버튼 잠금이 유지됨

            // 비활성 상태에서 채운 리스트는 ContentSizeFitter/LayoutGroup이 갱신되지 않으므로, 활성화 후 강제 리빌드
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)beforeEffectContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)afterEffectContainer);

            // 튜토리얼 2-E: 이전/새 효과 선택을 안내만 한다(선택은 자유).
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialRerollResultShown();
        }

        private void BuildReadonlyEffectList(Transform effectContainer, List<WeaponEffect> effects)
        {
            if (effectContainer == null) return;

            for (int i = effectContainer.childCount - 1; i >= 0; i--)
            {
                var child = effectContainer.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            // 잠긴 효과는 before/after 모두 동일 인덱스를 유지하므로 인덱스로 잠금 여부 판정.
            for (int i = 0; i < effects.Count; i++)
            {
                var obj = Instantiate(effectListItemPrefab, effectContainer);
                var item = obj.GetComponent<WeaponEffectListItem>();
                item?.Initialize(effects[i], false);
                item?.SetDimmed(controller.IsRerollEffectLocked(i));
            }
        }

        private void SetSelectPanelActive(bool active)
        {
            selectPanel?.SetActive(active);

            // 선택 중에는 메인 버튼 잠금(선택 강제). 해제 시 RefreshUI가 reroll 버튼 상태를 복구.
            if (active && rerollButton != null) rerollButton.interactable = false;
            if (closeButton != null) closeButton.interactable = !active;
            canEscape = !active;
        }

        #endregion

        #region 버튼 이벤트

        private void OnRerollClicked()
        {
            if (selectedWeapon == null) return;
            controller?.OnRerollWeapon(selectedWeapon);
        }

        private void OnAddRerollClicked()
        {
            if (selectedWeapon == null) return;
            controller?.OnAddRerollCharge(selectedWeapon);
        }

        private void OnSelectBefore()
        {
            // before 선택 = 새 효과 폐기 확정. 결과를 저장한다
            pendingAfterEffects = null;
            controller?.OnRerollChoiceConfirmed();
            SetSelectPanelActive(false);
            RefreshUI();
            CloseImmediatelyIfTutorial();
        }

        private void OnSelectAfter()
        {
            if (selectedWeapon != null && pendingAfterEffects != null)
                selectedWeapon.ApplyRerolledEffects(pendingAfterEffects);

            pendingAfterEffects = null;
            // after 선택 = 새 효과 적용 확정. 결과를 저장한다
            controller?.OnRerollChoiceConfirmed();
            SetSelectPanelActive(false);
            RefreshUI();
            CloseImmediatelyIfTutorial();
        }

        /// <summary>
        /// 튜토리얼 2-E: 효과 선택이 끝나면 팝업을 바로 닫아 마무리(2-F)로 넘긴다.
        /// 추가 재부여는 튜토리얼에서 다루지 않으므로 플레이어가 닫기를 누를 필요가 없다.
        /// </summary>
        private void CloseImmediatelyIfTutorial()
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                controller?.OnRerollClosed();
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private void OnCloseClicked()
        {
            if (selectPanel != null && selectPanel.activeSelf) return;   // 선택 중 닫기 차단
            controller?.OnRerollClosed();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
