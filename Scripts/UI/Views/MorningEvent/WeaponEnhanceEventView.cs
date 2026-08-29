// Scripts/UI/Views/MorningEvent/WeaponEnhanceEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class WeaponEnhanceEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.WeaponEnhance;
        protected override string OpeningDialogueID => "WeaponEnhance_Intro";
        protected override string EmptyDialogueID => "WeaponEnhance_Empty";

        // 강화 가능 무기(전설 미만)가 하나도 없으면 진행 불가
        protected override bool HasRequiredResource =>
            InventoryManager.Instance.GetAvailableWeapons().Any(w => w.currentGrade < Grade.Legendary);

        private WeaponEnhanceEventController Controller
            => UIControllerManager.Instance.GetController<WeaponEnhanceEventController>();

        [Header("닫기 / 패널")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resultCloseButton;
        [SerializeField] private GameObject listPanel;        // 무기 목록 화면
        [SerializeField] private GameObject resultPanel;

        // ── 무기 목록 ────────────────────────────────────────
        [Header("무기 목록")]
        [SerializeField] private ScrollRect weaponListScrollRect;
        [SerializeField] private Transform weaponListContainer;
        [SerializeField] private WeaponInventoryCardItem weaponCardItemPrefab;

        [Header("타입 필터 토글")]
        [SerializeField] private List<Toggle> weaponTypeFilterToggles;        // 0=전체, 1~N=WeaponType
        [SerializeField] private List<GameObject> weaponTypeFilterOnIndicators;

        [Header("빈 목록 텍스트")]
        [SerializeField] private TextMeshProUGUI listEmptyText;

        // ── 상세 패널 ─────────────────────────────────────────
        [Header("상세 패널")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailWeaponIcon;
        [SerializeField] private Image detailIconBG;
        [SerializeField] private Image detailFrame;
        [SerializeField] private TextMeshProUGUI detailWeaponNameText;
        [SerializeField] private TextMeshProUGUI detailWeaponTypeText;
        [SerializeField] private TextMeshProUGUI detailEnforceLevelText;
        [SerializeField] private Transform detailEffectContainer;
        [SerializeField] private WeaponEffectListItem effectListItemPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        [Header("오버레이")]
        [SerializeField] private Button detailOverlay;

        // ── 결과 패널 ─────────────────────────────────────────
        [Header("결과 패널 UI")]
        [SerializeField] private Image resultWeaponIcon;
        [SerializeField] private Image resultIconBG;
        [SerializeField] private Image resultFrame;
        [SerializeField] private TextMeshProUGUI resultWeaponNameText;
        [SerializeField] private TextMeshProUGUI resultGradeText;     // 이전 등급 → 새 등급 전이
        [SerializeField] private TextMeshProUGUI resultWeaponTypeText;
        [SerializeField] private Transform resultEffectContainer;

        private WeaponInstance selectedWeapon;
        private int currentTypeFilter = 0;
        private int lastGradeDelta;   // 결과 대화 분기용 (등급 상승/유지/하락)

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            closeButton?.onClick.AddListener(RequestClose);
            resultCloseButton?.onClick.AddListener(OnResultCloseClicked);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            backButton?.onClick.AddListener(OnBackClicked);
            detailOverlay?.onClick.AddListener(OnBackClicked);
            SubscribeTypeFilterToggles();
        }

        public override void OnOpened()
        {
            base.OnOpened();
            currentTypeFilter = 0;
            ResetTypeFilterToggles();
            ShowList();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            closeButton?.onClick.RemoveAllListeners();
            resultCloseButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            detailOverlay?.onClick.RemoveAllListeners();
            UnsubscribeTypeFilterToggles();
        }

        #endregion

        #region 목록 뷰

        private void ShowList()
        {
            selectedWeapon = null;
            listPanel?.SetActive(true);
            resultPanel?.SetActive(false);
            detailPanel?.SetActive(false);
            RefreshWeaponList();
        }

        private void RefreshWeaponList()
        {
            foreach (Transform child in weaponListContainer)
                Destroy(child.gameObject);

            var weapons = GetFilteredSortedWeapons();

            ShowListEmptyText(weapons.Count == 0
                ? L(currentTypeFilter == 0 ? "Inventory_EmptyWeaponAll" : "Inventory_EmptyWeaponFiltered")
                : null);

            foreach (var weapon in weapons)
            {
                var card = Instantiate(weaponCardItemPrefab, weaponListContainer);
                card.Initialize(weapon);
                card.OnCardClicked += OnWeaponCardClicked;
            }

            weaponListScrollRect?.ResetPosition(this);
        }

        private void OnWeaponCardClicked(WeaponInventoryCardItem card)
        {
            SendButtonClick("weapon_card");
            OpenDetail(card.Weapon);
        }

        private List<WeaponInstance> GetFilteredSortedWeapons()
        {
            var weapons = InventoryManager.Instance.GetAvailableWeapons();

            // 전설 등급은 강화 불가
            weapons = weapons.Where(w => w.currentGrade < Grade.Legendary).ToList();

            if (currentTypeFilter > 0)
            {
                WeaponType target = (WeaponType)(currentTypeFilter - 1);
                weapons = weapons.Where(w => w.weaponData.weaponType == target).ToList();
            }

            weapons = weapons.OrderByDescending(w => w.currentGrade).ToList();

            return weapons;
        }

        private void OnTypeFilterChanged(int index)
        {
            currentTypeFilter = index;
            RefreshWeaponList();
        }

        private void SubscribeTypeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;
            for (int i = 0; i < weaponTypeFilterToggles.Count; i++)
            {
                int index = i;
                weaponTypeFilterToggles[i]?.onValueChanged.AddListener(on => { if (on) OnTypeFilterChanged(index); });
                weaponTypeFilterToggles[i]?.onValueChanged.AddListener(on => SetTypeFilterIndicator(index, on));
            }
        }

        private void UnsubscribeTypeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;
            foreach (var toggle in weaponTypeFilterToggles)
                toggle?.onValueChanged.RemoveAllListeners();
        }

        private void ResetTypeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;
            for (int i = 0; i < weaponTypeFilterToggles.Count; i++)
            {
                bool on = (i == 0);
                weaponTypeFilterToggles[i]?.SetIsOnWithoutNotify(on);
                SetTypeFilterIndicator(i, on);
            }
        }

        private void SetTypeFilterIndicator(int index, bool on)
        {
            if (weaponTypeFilterOnIndicators != null && index < weaponTypeFilterOnIndicators.Count)
                weaponTypeFilterOnIndicators[index]?.SetActive(on);
        }

        // 공용 빈 목록 텍스트. message가 null이면 숨기고, 값이 있으면 해당 문구로 표시한다.
        private void ShowListEmptyText(string message)
        {
            if (listEmptyText == null) return;

            if (string.IsNullOrEmpty(message))
            {
                listEmptyText.gameObject.SetActive(false);
                return;
            }

            listEmptyText.text = message;
            listEmptyText.gameObject.SetActive(true);
        }

        #endregion

        #region 상세 뷰

        private void OpenDetail(WeaponInstance weapon)
        {
            selectedWeapon = weapon;
            detailPanel?.SetActive(true);
            SetSubPanelEscape(true, OnBackClicked);
            UpdateDetailUI();
        }

        private void UpdateDetailUI()
        {
            if (selectedWeapon == null) return;

            var data = selectedWeapon.weaponData;
            Grade grade = selectedWeapon.currentGrade;

            if (detailWeaponIcon != null && data?.icon != null)
                detailWeaponIcon.sprite = data.icon;
            if (detailIconBG != null)
                detailIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);
            if (detailFrame != null)
                detailFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);
            if (detailWeaponNameText != null)
            {
                string enforce = selectedWeapon.enforceLevel > 0 ? $" +{selectedWeapon.enforceLevel}" : "";
                detailWeaponNameText.text = (data?.DisplayName ?? "-") + enforce;
                detailWeaponNameText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }
            if (detailWeaponTypeText != null)
                detailWeaponTypeText.text = UITranslator.GetString(data?.weaponType ?? WeaponType.Sword);
            if (detailEnforceLevelText != null)
                detailEnforceLevelText.text = $"+{selectedWeapon.enforceLevel}";

            WeaponEffectListItem.Rebuild(detailEffectContainer, effectListItemPrefab, selectedWeapon.effects);
        }

        private void OnBackClicked()
        {
            SendButtonClick("back");
            detailPanel?.SetActive(false);
            selectedWeapon = null;
            SetSubPanelEscape(false);
        }

        #endregion

        #region 강화 실행

        private void OnConfirmClicked()
        {
            SendButtonClick("confirm");
            if (selectedWeapon == null) return;

            var data = selectedWeapon.weaponData;
            Grade grade = selectedWeapon.currentGrade;
            string enforce = selectedWeapon.enforceLevel > 0 ? $" +{selectedWeapon.enforceLevel}" : "";
            string coloredName = Colored((data?.DisplayName ?? "-") + enforce, ColorManager.Instance.GetGradeAccentColor(grade));

            UIPopupController.Instance?.ShowPopup(
                L("WeaponEnhance_SubmitConfirm", ("name", coloredName)),
                onConfirm: ExecuteEnhance,
                onCancel: () => { });
        }

        private void ExecuteEnhance()
        {
            if (selectedWeapon == null) return;

            Grade previousGrade = selectedWeapon.currentGrade;
            var (success, _, message) = Controller.OnEnhanceConfirmed(selectedWeapon);
            if (!success) { ShowPopupMessage(message); return; }
            lastGradeDelta = (int)selectedWeapon.currentGrade - (int)previousGrade;
            detailPanel?.SetActive(false);
            listPanel?.SetActive(false);
            resultPanel?.SetActive(true);
            MarkResultShown(OnResultCloseClicked);
            PopulateResultPanel(previousGrade);
        }

        private void OnResultCloseClicked()
        {
            SendButtonClick("result_close");

            // 패널을 닫은 뒤 등급 변동(상승/유지/하락)에 따른 대화를 출력한다.
            string id = lastGradeDelta > 0 ? "WeaponEnhance_Up"
                      : lastGradeDelta < 0 ? "WeaponEnhance_Down"
                      : "WeaponEnhance_Same";
            PlayClosing(id);
        }

        private void PopulateResultPanel(Grade previousGrade)
        {
            if (selectedWeapon == null) return;

            var data = selectedWeapon.weaponData;
            Grade grade = selectedWeapon.currentGrade;

            if (resultWeaponIcon != null && data?.icon != null)
                resultWeaponIcon.sprite = data.icon;

            if (resultIconBG != null)
                resultIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);

            if (resultFrame != null)
                resultFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);

            if (resultWeaponNameText != null)
            {
                string enforce = selectedWeapon.enforceLevel > 0 ? $" +{selectedWeapon.enforceLevel}" : "";
                resultWeaponNameText.text = (data?.DisplayName ?? "-") + enforce;
                resultWeaponNameText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }

            if (resultGradeText != null)
            {
                resultGradeText.text = $"{UITranslator.GetString(previousGrade)} → {UITranslator.GetString(grade)}";
                resultGradeText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }

            if (resultWeaponTypeText != null)
                resultWeaponTypeText.text = UITranslator.GetString(data?.weaponType ?? WeaponType.Sword);

            WeaponEffectListItem.Rebuild(resultEffectContainer, effectListItemPrefab, selectedWeapon.effects);
        }

        #endregion
    }
}
