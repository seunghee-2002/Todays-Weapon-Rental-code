using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class BlacksmithEnforceResultView : BaseView
    {
        [Header("Result Header")]
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image weaponBG;
        [SerializeField] private Image weaponFrame;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private TextMeshProUGUI weaponTypeText;
        [SerializeField] private TextMeshProUGUI enforceLevelText;
        [SerializeField] private GameObject effectPoint;

        [Header("Effect List")]
        [SerializeField] private Transform effectListContainer;
        [SerializeField] private GameObject effectListItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button retryButton;

        private Action onComplete;
        private WeaponInstance currentWeapon;

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = false;
            canEscape = true;
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
            retryButton?.onClick.AddListener(OnRetryClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            retryButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(bool success, WeaponInstance weapon, string enforcedEffectID, bool canRetry, Action onComplete = null)
        {
            this.onComplete = onComplete;
            currentWeapon   = weapon;

            if (success)
                EffectManager.Instance?.PlayBlacksmithSuccessEffect(effectPoint);
            else
                EffectManager.Instance?.PlayBlacksmithFailEffect(effectPoint);

            if (resultText != null)
                resultText.text = L(success ? "Blacksmith_EnforceSuccess" : "Blacksmith_EnforceFail");

            if (weapon == null) return;

            if (weaponIcon != null && weapon.weaponData.icon != null)
                weaponIcon.sprite = weapon.weaponData.icon;
            if (weaponBG != null)
                weaponBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
            if (weaponFrame != null)
                weaponFrame.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
            if (weaponNameText != null)
            {
                weaponNameText.text = $"{weapon.weaponData.DisplayName} +{weapon.enforceLevel}";
                weaponNameText.color = ColorManager.Instance.GetGradeAccentColor(weapon.currentGrade);
            }
            if (weaponTypeText != null)
                weaponTypeText.text = UITranslator.GetString(weapon.weaponData.weaponType);
            if (enforceLevelText != null)
                enforceLevelText.text = $"+{weapon.enforceLevel}";

            ShowEffectList(weapon, success, enforcedEffectID);
            RefreshRetryButton(canRetry);
        }

        // ProcessView에서 호출하는 기존 시그니처 — canRetry를 Controller에서 판단
        public void Initialize(bool success, WeaponInstance weapon, string enforcedEffectID, Action onComplete = null)
        {
            bool canRetry = !success && (UIControllerManager.Instance
                .GetController<BlacksmithController>()
                ?.CanEnforceRetry(weapon) ?? false);

            Initialize(success, weapon, enforcedEffectID, canRetry, onComplete);
        }

        private void RefreshRetryButton(bool canRetry)
        {
            if (retryButton == null) return;

            retryButton.gameObject.SetActive(canRetry);
        }

        private void ShowEffectList(WeaponInstance weapon, bool success, string enforcedEffectID)
        {
            if (effectListContainer == null || effectListItemPrefab == null) return;

            foreach (Transform child in effectListContainer)
                Destroy(child.gameObject);

            foreach (var effect in weapon.effects)
            {
                bool isEnforced = success && effect.effectDataID == enforcedEffectID;
                var obj = Instantiate(effectListItemPrefab, effectListContainer);
                obj.GetComponent<WeaponEffectListItem>()?.Initialize(effect, false, isEnforced);
            }
        }

        private void OnRetryClicked()
        {
            if (currentWeapon == null) return;

            int cost = LegacyManager.Instance?.GetEnforceRetryCost(currentWeapon) ?? 0;
            string weaponName = currentWeapon.weaponData.DisplayName;

            UIPopupController.Instance?.ShowPopup(
                L("Blacksmith_EnforceRetryConfirm", ("weapon", weaponName), ("cost", cost)),
                () => UIControllerManager.Instance.GetController<BlacksmithController>()
                          ?.OnEnforceRetryClicked(currentWeapon),
                () => { }
            );
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
            onComplete?.Invoke();
            UIControllerManager.Instance.GetController<BlacksmithController>()?.OnEnforceResultClosed();
        }

        public override void OnEscapeClicked() => OnCloseClicked();
    }
}
