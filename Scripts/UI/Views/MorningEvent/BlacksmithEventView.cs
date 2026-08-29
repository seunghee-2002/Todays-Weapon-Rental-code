// Scripts/UI/Views/MorningEvent/BlacksmithEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 떠돌이 대장장이 패널.
    /// 무기를 맡기면 강화/리롤 중 하나가 자동으로 적용된다.
    /// </summary>
    public class BlacksmithEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.WanderingBlacksmith;
        protected override string OpeningDialogueID => "Blacksmith_Intro";
        protected override string EmptyDialogueID => "Blacksmith_Empty";

        // 맡길 무기가 하나도 없으면 진행 불가
        protected override bool HasRequiredResource =>
            InventoryManager.Instance.GetAvailableWeapons().Count > 0;

        private BlacksmithEventController Controller
            => UIControllerManager.Instance.GetController<BlacksmithEventController>();

        [Header("닫기 / 목록 패널")]
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject listPanel;

        [Header("무기 목록")]
        [SerializeField] private ScrollRect weaponListScrollRect;
        [SerializeField] private Transform weaponListContainer;
        [SerializeField] private WeaponInventoryCardItem weaponCardItemPrefab;

        [Header("타입 필터 토글")]
        [SerializeField] private List<Toggle> weaponTypeFilterToggles;        // 0=전체, 1~N=WeaponType
        [SerializeField] private List<GameObject> weaponTypeFilterOnIndicators;

        [Header("빈 목록 텍스트")]
        [SerializeField] private TextMeshProUGUI listEmptyText;

        [Header("상세 패널")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailWeaponIcon;
        [SerializeField] private Image detailIconBG;
        [SerializeField] private Image detailFrame;
        [SerializeField] private TextMeshProUGUI detailWeaponNameText;
        [SerializeField] private TextMeshProUGUI detailWeaponTypeText;
        [SerializeField] private Transform detailEffectContainer;
        [SerializeField] private WeaponEffectListItem effectListItemPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;

        [Header("오버레이")]
        [SerializeField] private Button detailOverlay;

        [Header("결과 selection 패널 (before/after + 닫기)")]
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private TextMeshProUGUI selectionHeaderText;
        [SerializeField] private Transform selectBeforeEffectContainer;
        [SerializeField] private Transform selectAfterEffectContainer;
        [SerializeField] private Button selectCloseButton;

        private WeaponInstance selectedWeapon;
        private int currentTypeFilter = 0;
        private bool lastWasEnhance;   // 결과 대화 분기용 (강화/리롤)

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            closeButton?.onClick.AddListener(RequestClose);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            backButton?.onClick.AddListener(OnBackClicked);
            detailOverlay?.onClick.AddListener(OnBackClicked);
            selectCloseButton?.onClick.AddListener(OnSelectCloseClicked);
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
            confirmButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            detailOverlay?.onClick.RemoveAllListeners();
            selectCloseButton?.onClick.RemoveAllListeners();
            UnsubscribeTypeFilterToggles();
        }

        #endregion

        #region 목록 뷰

        private void ShowList()
        {
            selectedWeapon = null;
            listPanel?.SetActive(true);
            detailPanel?.SetActive(false);
            selectionPanel?.SetActive(false);
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

        #region 맡기기 실행

        private void OnConfirmClicked()
        {
            SendButtonClick("confirm");

            // 이미 최대강화인 무기는 강화로 결정되면 아무 변화가 없으므로, 맡기기 전에 경고 팝업을 띄운다.
            if (selectedWeapon != null && selectedWeapon.enforceLevel >= selectedWeapon.MaxEnforceLevel)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("BlacksmithEvent_MaxEnhanceConfirm"),
                    onConfirm: SubmitWeapon,
                    onCancel: () => { });
                return;
            }

            SubmitWeapon();
        }

        private void SubmitWeapon()
        {
            var (success, isEnhance, previousEffects, _, enforcedEffectIDs, message) =
                Controller.OnBlacksmithSubmitWeapon(selectedWeapon);

            if (!success) { ShowPopupMessage(message); return; }

            ShowSelectionResult(isEnhance, previousEffects, enforcedEffectIDs);
        }

        private void ShowSelectionResult(bool isEnhance, List<WeaponEffect> beforeEffects, List<string> enforcedEffectIDs)
        {
            detailPanel?.SetActive(false);
            listPanel?.SetActive(false);
            selectionPanel?.SetActive(true);
            MarkResultShown(OnSelectCloseClicked);   // 결과 화면: 완료 처리 + ESC/오버레이 닫기(=강화/리롤 마무리 대화)

            lastWasEnhance = isEnhance;

            if (selectionHeaderText != null)
                selectionHeaderText.text = L(isEnhance ? "BlacksmithEvent_EnhanceDone" : "Blacksmith_RerollDone");

            // before / after 효과 비교 (강화 시 max된 효과만 블링킹)
            WeaponEffectListItem.Rebuild(selectBeforeEffectContainer, effectListItemPrefab, beforeEffects);

            var enforcedSet = new HashSet<string>(enforcedEffectIDs ?? new List<string>());
            WeaponEffectListItem.Rebuild(selectAfterEffectContainer, effectListItemPrefab, selectedWeapon.effects,
                e => enforcedSet.Contains(e.effectDataID));

            // 활성화 직후 채운 리스트는 LayoutGroup/ContentSizeFitter가 갱신되지 않으므로 강제 리빌드
            if (selectBeforeEffectContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)selectBeforeEffectContainer);
            if (selectAfterEffectContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)selectAfterEffectContainer);
        }

        private void OnSelectCloseClicked()
        {
            SendButtonClick("select_close");

            // 패널을 닫은 뒤 결과(강화/리롤)에 따른 대화를 출력한다.
            string id = lastWasEnhance ? "Blacksmith_Enhance" : "Blacksmith_Reroll";
            PlayClosing(id);
        }

        #endregion
    }
}
