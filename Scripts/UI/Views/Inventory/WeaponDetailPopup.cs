using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상세정보 팝업
    /// </summary>
    public class WeaponDetailPopup : BaseView
    {
        [Header("UI Elements")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image weaponBG;
        [SerializeField] private Image weaponFrame;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private TextMeshProUGUI weaponTypeText;
        [SerializeField] private TextMeshProUGUI enforcementLevelText;

        [Header("Effects")]
        [SerializeField] private Transform effectListContainer;
        [SerializeField] private WeaponEffectListItem effectListItemPrefab;
        [SerializeField] private GameObject lockedEffectSlotPrefab;

        [SerializeField] private Button closeButton;

        private WeaponInstance currentWeapon;
        private int currentRevealedCount = -1; // -1 = 전체 공개 (기본값)

        // 팝업이 닫힐 때 1회 호출되는 콜백. 모닝 이벤트의 "결과(팝업) → 대화" 연결에 사용한다.
        private System.Action onClose;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            
            pauseTimeOnOpen = false;
            isCanClickOverlay = true;
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
        
        public void Initialize(WeaponInstance weapon)
        {
            currentWeapon = weapon;
            currentRevealedCount = -1;
            UpdateUI();
        }

        /// <summary>암시장 미리보기용 — 통찰에 따라 일부 효과를 잠금 이미지로 표시한다.</summary>
        public void Initialize(WeaponInstance weapon, int revealedEffectCount)
        {
            currentWeapon = weapon;
            currentRevealedCount = revealedEffectCount;
            UpdateUI();
        }

        /// <summary>팝업이 닫힐 때 1회 실행할 콜백을 등록한다. (모닝 이벤트 결과 → 대화 연결용)</summary>
        public void SetOnClose(System.Action action) => onClose = action;

        public override void Close()
        {
            base.Close();
            var cb = onClose;
            onClose = null;          // 재사용 인스턴스에서 중복 호출 방지
            cb?.Invoke();
        }

        #endregion

        #region UI 업데이트 메서드
        
        private void UpdateUI()
        {
            if (currentWeapon == null || currentWeapon.weaponData == null) return;

            if (weaponIcon != null && currentWeapon.weaponData.icon != null)
                weaponIcon.sprite = currentWeapon.weaponData.icon;
            
            if (weaponNameText != null)
            {
                weaponNameText.text = $"{currentWeapon.weaponData.DisplayName} +{currentWeapon.enforceLevel}";
                weaponNameText.color = ColorManager.Instance.GetGradeAccentColor(currentWeapon.currentGrade);
            }
                
            if (weaponBG != null)
                weaponBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(currentWeapon.currentGrade);
            
            if (weaponFrame != null)
                weaponFrame.sprite = IconManager.Instance.GetFrameByGrade(currentWeapon.currentGrade);
            
            if (weaponTypeText != null)
                weaponTypeText.text = UITranslator.GetString(currentWeapon.weaponData.weaponType);

            if (enforcementLevelText != null)
                enforcementLevelText.text = $"+{currentWeapon.enforceLevel}";

            UpdateEffectList();
        }

        private void UpdateEffectList()
        {
            if (effectListContainer == null || effectListItemPrefab == null) return;

            foreach (Transform child in effectListContainer)
                Destroy(child.gameObject);

            if (currentWeapon.effects == null || currentWeapon.effects.Count == 0) return;

            int totalCount = currentWeapon.effects.Count;
            int revealedCount = currentRevealedCount < 0 ? totalCount : currentRevealedCount;

            for (int i = 0; i < totalCount; i++)
            {
                if (i < revealedCount)
                {
                    var item = Instantiate(effectListItemPrefab, effectListContainer);
                    item.Initialize(currentWeapon.effects[i]);
                }
                else if (lockedEffectSlotPrefab != null)
                {
                    Instantiate(lockedEffectSlotPrefab, effectListContainer);
                }
            }
        }
        
        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel<WeaponDetailPopup>();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}