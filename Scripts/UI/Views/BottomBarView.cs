using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    public class BottomBarView : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button adventureProgressButton;
        [SerializeField] private Button questButton;
        [SerializeField] private Button settingsButton;

        [Header("Indicators")]
        [SerializeField] private GameObject inventoryIndicator;
        [SerializeField] private GameObject adventureProgressIndicator;
        [SerializeField] private GameObject questIndicator;

        #region 초기화

        private void Start()
        {
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            inventoryButton?.onClick.AddListener(OnInventoryClicked);
            adventureProgressButton?.onClick.AddListener(OnAdventureProgressClicked);
            questButton?.onClick.AddListener(OnQuestClicked);
            settingsButton?.onClick.AddListener(OnSettingsClicked);

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnWeaponAdded += OnInventoryWeaponAdded;
                InventoryManager.Instance.OnMaterialAdded += OnInventoryMaterialAdded;
                InventoryManager.Instance.OnActiveItemAdded += OnInventoryActiveItemAdded;
            }

            if (AdventureManager.Instance != null)
            {
                AdventureManager.Instance.OnAdventureStarted += OnAdventureStarted;
                AdventureManager.Instance.OnAdventureCompleted += OnAdventureCompleted;
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted += OnQuestStarted;
                QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            }
        }

        private void UnsubscribeEvents()
        {
            inventoryButton?.onClick.RemoveAllListeners();
            adventureProgressButton?.onClick.RemoveAllListeners();
            questButton?.onClick.RemoveAllListeners();
            settingsButton?.onClick.RemoveAllListeners();

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnWeaponAdded -= OnInventoryWeaponAdded;
                InventoryManager.Instance.OnMaterialAdded -= OnInventoryMaterialAdded;
                InventoryManager.Instance.OnActiveItemAdded -= OnInventoryActiveItemAdded;
            }

            if (AdventureManager.Instance != null)
            {
                AdventureManager.Instance.OnAdventureStarted -= OnAdventureStarted;
                AdventureManager.Instance.OnAdventureCompleted -= OnAdventureCompleted;
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted -= OnQuestStarted;
                QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            }
        }

        #endregion

        #region 버튼 이벤트 핸들러

        private void OnInventoryClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("bottom_bar", "inventory");
            UIManager.Instance?.OpenPanel<InventoryView>();
            inventoryIndicator?.SetActive(false);
        }

        private void OnAdventureProgressClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("bottom_bar", "adventure_progress");
            UIManager.Instance?.OpenPanel<AdventureProgressView>();
            UIControllerManager.Instance?.GetController<AdventureProgressController>()?.InitAfter();
            adventureProgressIndicator?.SetActive(false);
        }

        private void OnQuestClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("bottom_bar", "quest");
            UIManager.Instance?.OpenPanel<QuestView>();
            questIndicator?.SetActive(false);
        }

        private void OnSettingsClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("bottom_bar", "settings");
            UIManager.Instance?.OpenPanel<OptionPopupView>();
            Log.Info("설정 버튼 클릭");
        }

        #endregion

        #region 알림 이벤트 핸들러

        private void OnInventoryWeaponAdded(WeaponInstance _) => TryShowInventoryIndicator();
        private void OnInventoryMaterialAdded(string _, int __) => TryShowInventoryIndicator();
        private void OnInventoryActiveItemAdded(ActiveItemInstance _) => TryShowInventoryIndicator();

        private void OnAdventureStarted(AdventureInstance _) => TryShowAdventureProgressIndicator();
        private void OnAdventureCompleted(AdventureInstance _, AdventureResult __) => TryShowAdventureProgressIndicator();

        private void OnQuestStarted(WeeklyQuestInstance _) => TryShowQuestIndicator();
        private void OnQuestCompleted(WeeklyQuestInstance _) => TryShowQuestIndicator();

        private void TryShowInventoryIndicator()
        {
            if (UIManager.Instance?.GetPanel<InventoryView>()?.IsOpen ?? false) return;
            inventoryIndicator?.SetActive(true);
        }

        private void TryShowAdventureProgressIndicator()
        {
            if (UIManager.Instance?.GetPanel<AdventureProgressView>()?.IsOpen ?? false) return;
            adventureProgressIndicator?.SetActive(true);
        }

        private void TryShowQuestIndicator()
        {
            if (UIManager.Instance?.GetPanel<QuestView>()?.IsOpen ?? false) return;
            questIndicator?.SetActive(true);
        }

        #endregion

        #region 튜토리얼

        /// <summary>
        /// 튜토리얼 하이라이트용(7단계) - 가방(인벤토리) 버튼 RectTransform.
        /// 하단바는 하이라이트가 열릴 때(minimalUI:false) 비로소 SetActive(true)되므로, 이 접근자 시점(대사700 minimalUI:true)엔
        /// 아직 비활성이라 여기서 리빌드해도 무효다. 위치 확정(활성화 후 레이아웃 즉시 확정)은 TutorialManager.ShowHighlight의
        /// Canvas.ForceUpdateCanvases에서 수행한다.
        /// </summary>
        public RectTransform GetInventoryButtonRect() => inventoryButton?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트용(9단계) - 모험 진행 버튼 RectTransform. GetInventoryButtonRect와 동일하게 위치 확정은 ShowHighlight에서 처리.</summary>
        public RectTransform GetAdventureButtonRect() => adventureProgressButton?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트용(10단계) - 퀘스트 버튼 RectTransform.</summary>
        public RectTransform GetQuestButtonRect() => questButton?.transform as RectTransform;

        #endregion
    }
}
