using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class BlacksmithEvolveResultView : BaseView
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

         [Header("Effect GameObjects")]

        [Header("Effect List")]
        [SerializeField] private Transform effectListContainer;
        [SerializeField] private GameObject effectListItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        private Action onComplete;

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
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(bool success, WeaponInstance weapon, Action onComplete = null, int previousEffectCount = 0)
        {
            this.onComplete = onComplete;

            if (success)
                EffectManager.Instance?.PlayBlacksmithSuccessEffect(effectPoint);
            else
                EffectManager.Instance?.PlayBlacksmithFailEffect(effectPoint);

            if (resultText != null)
                resultText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", success ? "Blacksmith_EvolveSuccess" : "Blacksmith_EvolveFail");

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

            ShowEffectList(weapon, success, previousEffectCount);
        }

        private void ShowEffectList(WeaponInstance weapon, bool success, int previousEffectCount)
        {
            if (effectListContainer == null || effectListItemPrefab == null) return;

            foreach (Transform child in effectListContainer)
                Destroy(child.gameObject);

            for (int i = 0; i < weapon.effects.Count; i++)
            {
                // previousEffectCount 이후 인덱스 = 진화로 새로 추가된 효과
                bool isNew = success && i >= previousEffectCount;
                var obj = Instantiate(effectListItemPrefab, effectListContainer);
                obj.GetComponent<WeaponEffectListItem>()?.Initialize(weapon.effects[i], false, isNew);
            }
        }

        private void OnCloseClicked()
        {
            onComplete?.Invoke();
            UIControllerManager.Instance?.GetController<BlacksmithController>()?.OnEvolveResultClosed();
        }

        public override void OnEscapeClicked() => OnCloseClicked();
    }
}