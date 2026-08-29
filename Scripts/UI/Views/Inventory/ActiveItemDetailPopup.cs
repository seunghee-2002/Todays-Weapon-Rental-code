using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 액티브 아이템 상세정보 팝업
    /// </summary>
    public class ActiveItemDetailPopup : BaseView
    {
        [Header("UI Elements")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private Image itemBG;
        [SerializeField] private Image itemFrame;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private Button closeButton;

        private ActiveItemData currentData;
        private Action onClose;   // 닫을 때 1회 호출(튜토리얼 제작 후 다음 단계 진행 등). 없으면 무시.

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

        public void Initialize(ActiveItemData data, Action onClose = null)
        {
            currentData = data;
            this.onClose = onClose;
            UpdateUI();
        }

        #endregion

        #region UI 업데이트 메서드

        private void UpdateUI()
        {
            if (currentData == null) return;

            Grade grade = currentData.usageContext.ToGrade();

            if (itemIcon != null && currentData.icon != null)
                itemIcon.sprite = currentData.icon;

            if (itemBG != null)
                itemBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);

            if (itemFrame != null)
                itemFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);

            if (itemNameText != null)
            {
                itemNameText.text = currentData.DisplayName;
                itemNameText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }
                

            if (descriptionText != null)
                descriptionText.text = currentData.DisplayDescription;

            if (typeText != null)
                typeText.text = UITranslator.GetString(currentData.itemType);
        }

        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            // 콜백이 새 패널/대화를 열 수 있으므로 먼저 비우고 닫은 뒤 호출한다.
            var callback = onClose;
            onClose = null;
            UIManager.Instance.ClosePanel<ActiveItemDetailPopup>();
            callback?.Invoke();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
