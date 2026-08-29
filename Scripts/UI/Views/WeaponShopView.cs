using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 무기 상점 UI View — WeaponGrid 및 헤더 영역만 담당.
    /// 상세 패널은 WeaponShopDetailView로 분리되었다.
    /// </summary>
    public class WeaponShopView : BaseView
    {
        protected override string GetThemeBgmKey() => "WeaponShop";

        [Header("UI Elements")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TextMeshProUGUI refreshCountText;
        [SerializeField] private TextMeshProUGUI refreshCostText;

        [Header("Weapon Grid")]
        [SerializeField] private ScrollRect weaponGridScrollRect;
        [SerializeField] private Transform weaponGridContainer;
        [SerializeField] private GameObject weaponCardPrefab;

        [Header("Controller")]
        [SerializeField] private WeaponShopController shopController;

        private List<WeaponShopCardItem> weaponCards = new List<WeaponShopCardItem>();

        private WeaponShopController Controller => shopController;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = false;
            canEscape = false;
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

        public void Initialize(List<WeaponShopItemData> items)
        {
            UpdateWeaponCard(items);
            UpdateRefreshButton();
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(RequestClose);
            refreshButton?.onClick.AddListener(OnRefreshClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
            refreshButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region UI 업데이트

        private void UpdateWeaponCard(List<WeaponShopItemData> items)
        {
            ClearWeaponCards();

            if (items == null) return;

            items = items.OrderByDescending(i => i.WeaponData.baseGrade).ToList();

            foreach (var item in items)
            {
                CreateWeaponCard(item);
            }

            weaponGridScrollRect.ResetPosition(this);
        }

        private void CreateWeaponCard(WeaponShopItemData item)
        {
            if (weaponCardPrefab == null || weaponGridContainer == null) return;

            GameObject cardObj = Instantiate(weaponCardPrefab, weaponGridContainer);
            WeaponShopCardItem card = cardObj.GetComponentOrNull<WeaponShopCardItem>();

            if (card != null)
            {
                card.Initialize(item, () => OnWeaponCardClicked(item));
                weaponCards.Add(card);
            }
        }

        private void ClearWeaponCards()
        {
            foreach (var card in weaponCards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }
            weaponCards.Clear();
        }

        public void UpdateRefreshButton()
        {
            if (refreshCostText != null)
            {
                int refreshCost = ConfigManager.Instance.WeaponShop.refreshCost;
                refreshCostText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "WeaponShop_RefreshCost",
                    arguments: new object[] { new Dictionary<string, object> { { "cost", refreshCost.ToString("N0") } } });
                refreshCostText.color = EconomyManager.Instance.HasEnoughGold(refreshCost)
                    ? ColorManager.Instance.GetGrayColor()
                    : ColorManager.Instance.GetRedColor();
            }

            if (refreshCountText != null)
            {
                int maxRefresh = ConfigManager.Instance.WeaponShop.maxRefreshCount + (LegacyManager.Instance?.GetShopRefreshBonus() ?? 0);
                refreshCountText.text = $"{WeaponShopManager.Instance.RefreshCount}/{maxRefresh}";
            }
        }

        public void RemoveWeaponCard(WeaponShopItemData item)
        {
            var card = weaponCards.Find(c => c.Item == item);
            if (card != null)
            {
                weaponCards.Remove(card);
                Destroy(card.gameObject);
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnWeaponCardClicked(WeaponShopItemData item)
        {
            Controller?.OnWeaponSelected(item);
        }

        private void OnRefreshClicked()
        {
            Controller?.OnRefreshShop();
        }

        private void OnCloseClicked()
        {
            Controller?.OnCloseShop();
        }

        // 닫기 버튼·ESC 공통 진입점: 나가기 확인 팝업 → 확인 시 실제 종료.
        // 튜토리얼 중에는 상점 닫기가 곧 튜토리얼 완료 판정 경로이므로, 확인 팝업 없이 종료 로직(구매 여부 판정)에 맡긴다.
        private void RequestClose()
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            {
                OnCloseClicked();
                return;
            }
            UIPopupController.Instance?.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", "WeaponShop_ExitConfirm"),
                OnCloseClicked, () => { });
        }

        public override void OnEscapeCancelled() => RequestClose();

        #endregion
    }
}
