// Scripts/UI/Views/MorningEvent/WeaponExchangeEventView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class WeaponExchangeEventView : MorningEventViewBase
    {
        protected override MorningEventType EventType => MorningEventType.WeaponExchange;
        protected override string OpeningDialogueID => "WeaponExchange_Intro";
        protected override string EmptyDialogueID => "WeaponExchange_Empty";

        // 교환하려면 무기가 2개 이상 필요
        protected override bool HasRequiredResource =>
            InventoryManager.Instance.GetAvailableWeapons().Count >= 2;

        private WeaponExchangeEventController Controller
            => UIControllerManager.Instance.GetController<WeaponExchangeEventController>();

        [Header("닫기")]
        [SerializeField] private Button closeButton;

        // ── 슬롯 ─────────────────────────────────────────────
        [Header("슬롯 A")]
        [SerializeField] private Button slotAButton;
        [SerializeField] private WeaponInventoryCardItem slotACardItem;
        [SerializeField] private GameObject slotAFocus;

        [Header("슬롯 B")]
        [SerializeField] private Button slotBButton;
        [SerializeField] private WeaponInventoryCardItem slotBCardItem;
        [SerializeField] private GameObject slotBFocus;

        [Header("결과 등급 미리보기")]
        [SerializeField] private GameObject gradePreviewContainer;
        [SerializeField] private GradePreviewItem gradePreviewItemPrefab;

        [Header("확인 버튼")]
        [SerializeField] private Button confirmButton;

        // ── 무기 선택 패널 ───────────────────────────────────
        [Header("무기 선택 패널")]
        [SerializeField] private GameObject weaponSelectPanel;
        [SerializeField] private ScrollRect weaponListScrollRect;
        [SerializeField] private Transform weaponListContainer;
        [SerializeField] private WeaponInventoryCardItem weaponCardItemPrefab;
        [SerializeField] private Button backButton;

        [Header("타입 필터 토글")]
        [SerializeField] private List<Toggle> weaponTypeFilterToggles;        // 0=전체, 1~N=WeaponType
        [SerializeField] private List<GameObject> weaponTypeFilterOnIndicators;

        [Header("빈 목록 텍스트")]
        [SerializeField] private TextMeshProUGUI listEmptyText;

        [Header("무기 상세 패널 (선택 패널 내)")]
        [SerializeField] private GameObject weaponDetailPanel;
        [SerializeField] private Image detailWeaponIcon;
        [SerializeField] private Image detailIconBG;
        [SerializeField] private Image detailFrame;
        [SerializeField] private TextMeshProUGUI detailWeaponNameText;
        [SerializeField] private TextMeshProUGUI detailWeaponTypeText;
        [SerializeField] private TextMeshProUGUI detailEnforceLevelText;
        [SerializeField] private Transform detailEffectContainer;
        [SerializeField] private WeaponEffectListItem effectListItemPrefab;
        [SerializeField] private Button detailConfirmButton;
        [SerializeField] private Button detailBackButton;

        [Header("오버레이")]
        [SerializeField] private Button selectOverlay;
        [SerializeField] private Button detailOverlay;

        private readonly WeaponInstance[] selected = new WeaponInstance[2];
        private WeaponInstance previewWeapon;
        private int activeSlot = -1;   // 현재 선택 중인 슬롯 (0=A, 1=B)
        private int currentTypeFilter = 0;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            closeButton?.onClick.AddListener(RequestClose);
            slotAButton?.onClick.AddListener(() => OpenWeaponSelect(0));
            slotBButton?.onClick.AddListener(() => OpenWeaponSelect(1));
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            backButton?.onClick.AddListener(OnBackClicked);
            detailConfirmButton?.onClick.AddListener(OnDetailConfirmClicked);
            detailBackButton?.onClick.AddListener(OnDetailBackClicked);
            selectOverlay?.onClick.AddListener(OnBackClicked);
            detailOverlay?.onClick.AddListener(OnDetailBackClicked);
            SubscribeTypeFilterToggles();
        }

        public override void OnOpened()
        {
            base.OnOpened();
            selected[0] = null;
            selected[1] = null;
            previewWeapon = null;
            activeSlot = -1;
            currentTypeFilter = 0;
            ResetTypeFilterToggles();
            weaponSelectPanel?.SetActive(false);
            weaponDetailPanel?.SetActive(false);
            UpdateSlotUI(0);
            UpdateSlotUI(1);
            UpdateGradePreview();
            UpdateConfirmButton();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            closeButton?.onClick.RemoveAllListeners();
            slotAButton?.onClick.RemoveAllListeners();
            slotBButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            detailConfirmButton?.onClick.RemoveAllListeners();
            detailBackButton?.onClick.RemoveAllListeners();
            selectOverlay?.onClick.RemoveAllListeners();
            detailOverlay?.onClick.RemoveAllListeners();
            UnsubscribeTypeFilterToggles();
        }

        #endregion

        #region 슬롯 UI

        private void UpdateSlotUI(int slot)
        {
            var weapon = selected[slot];
            bool filled = weapon != null;

            WeaponInventoryCardItem card = slot == 0 ? slotACardItem : slotBCardItem;

            if (card != null)
            {
                card.gameObject.SetActive(filled);
                if (filled) card.Initialize(weapon);
            }

            // 무기 미선택 슬롯만 focus 강조를 켠다.
            GameObject focus = slot == 0 ? slotAFocus : slotBFocus;
            focus?.SetActive(!filled);
        }

        private void UpdateConfirmButton()
        {
            if (confirmButton != null)
                confirmButton.interactable =
                    selected[0] != null &&
                    selected[1] != null &&
                    selected[0] != selected[1];
        }

        #endregion

        #region 등급 미리보기

        private void UpdateGradePreview()
        {
            if (gradePreviewContainer == null) return;

            foreach (Transform child in gradePreviewContainer.transform)
                Destroy(child.gameObject);

            if (selected[0] == null || selected[1] == null)
            {
                gradePreviewContainer.SetActive(false);
                return;
            }

            float[] table = MorningEventManager.Instance.GetExchangeGradeTable(selected[0], selected[1]);
            if (table == null) { gradePreviewContainer.SetActive(false); return; }

            gradePreviewContainer.SetActive(true);
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] <= 0f) continue;
                if (gradePreviewItemPrefab == null) continue;

                var item = Instantiate(gradePreviewItemPrefab, gradePreviewContainer.transform);
                item.Initialize((Grade)i, table[i]);
            }
        }

        #endregion

        #region 무기 선택 패널

        private void OpenWeaponSelect(int slot)
        {
            activeSlot = slot;
            currentTypeFilter = 0;
            ResetTypeFilterToggles();
            weaponDetailPanel?.SetActive(false);
            weaponSelectPanel?.SetActive(true);
            SetSubPanelEscape(true, OnBackClicked);
            RefreshWeaponList();
        }

        private void OnBackClicked()
        {
            SendButtonClick("back");
            activeSlot = -1;
            weaponDetailPanel?.SetActive(false);
            weaponSelectPanel?.SetActive(false);
            SetSubPanelEscape(false);
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
            OpenWeaponDetail(card.Weapon);
        }

        private List<WeaponInstance> GetFilteredSortedWeapons()
        {
            var weapons = InventoryManager.Instance.GetAvailableWeapons();

            // 이미 다른 슬롯에 선택된 무기 제외
            weapons = weapons.Where(w => w != selected[0] && w != selected[1]).ToList();

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

        #region 무기 상세 패널

        private void OpenWeaponDetail(WeaponInstance weapon)
        {
            previewWeapon = weapon;
            weaponDetailPanel?.SetActive(true);
            SetSubPanelEscape(true, OnDetailBackClicked);

            var data = weapon.weaponData;
            Grade grade = weapon.currentGrade;

            if (detailWeaponIcon != null && data?.icon != null)
                detailWeaponIcon.sprite = data.icon;
            if (detailIconBG != null)
                detailIconBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(grade);
            if (detailFrame != null)
                detailFrame.sprite = IconManager.Instance.GetFrameByGrade(grade);
            if (detailWeaponNameText != null)
            {
                string enforce = weapon.enforceLevel > 0 ? $" +{weapon.enforceLevel}" : "";
                detailWeaponNameText.text = (data?.DisplayName ?? "-") + enforce;
                detailWeaponNameText.color = ColorManager.Instance.GetGradeAccentColor(grade);
            }
            if (detailWeaponTypeText != null)
                detailWeaponTypeText.text = UITranslator.GetString(data?.weaponType ?? WeaponType.Sword);
            if (detailEnforceLevelText != null)
                detailEnforceLevelText.text = $"+{weapon.enforceLevel}";

            WeaponEffectListItem.Rebuild(detailEffectContainer, effectListItemPrefab, weapon.effects);
        }

        private void OnDetailConfirmClicked()
        {
            SendButtonClick("detail_confirm");
            if (previewWeapon == null) return;
            SelectWeapon(previewWeapon);
            weaponDetailPanel?.SetActive(false);
        }

        private void OnDetailBackClicked()
        {
            SendButtonClick("detail_back");
            previewWeapon = null;
            weaponDetailPanel?.SetActive(false);
            SetSubPanelEscape(true, OnBackClicked);
        }

        private void SelectWeapon(WeaponInstance weapon)
        {
            if (activeSlot < 0) return;

            selected[activeSlot] = weapon;

            UpdateSlotUI(0);
            UpdateSlotUI(1);
            UpdateGradePreview();
            UpdateConfirmButton();

            // 선택 완료 → 선택 패널 닫기
            OnBackClicked();
        }

        #endregion

        #region 교환 실행

        private void OnConfirmClicked()
        {
            SendButtonClick("confirm");
            string nameA = Colored(selected[0]?.weaponData?.DisplayName ?? "-", ColorManager.Instance.GetGradeAccentColor(selected[0].currentGrade));
            string nameB = Colored(selected[1]?.weaponData?.DisplayName ?? "-", ColorManager.Instance.GetGradeAccentColor(selected[1].currentGrade));

            UIPopupController.Instance?.ShowPopup(
                L("WeaponExchange_Confirm", ("nameA", nameA), ("nameB", nameB)),
                onConfirm: ExecuteExchange,
                onCancel: () => { }
            );
        }

        private void ExecuteExchange()
        {
            // 결과 대화 분기를 위해 교환 전 투입 최고 등급을 미리 캡처한다.
            int inputMaxGrade = Mathf.Max((int)selected[0].currentGrade, (int)selected[1].currentGrade);

            var (success, resultWeapon, message) = Controller.OnExchangeConfirmed(selected[0], selected[1]);
            if (!success) { ShowPopupMessage(message); return; }

            MorningEventManager.Instance.MarkEventCompleted();
            // 패널만 닫고 NPC 퇴장은 결과 대화 종료 후로 미룬다(대화 중 캐릭터가 나가지 않도록).
            Controller.CloseEventDeferLeave();

            string resultDialogueID = GetExchangeResultDialogueID(inputMaxGrade, (int)resultWeapon.currentGrade);

            var popup = UIManager.Instance.GetOrInstantiatePanel<WeaponDetailPopup>();
            UIManager.Instance.OpenPanel<WeaponDetailPopup>();
            popup?.Initialize(resultWeapon);
            // 결과 무기 팝업을 닫으면 결과 등급에 따른 대화를 출력하고, 대화 종료 후 NPC가 퇴장한다.
            popup?.SetOnClose(() => PlayDialogue(resultDialogueID, DismissEventNpc));
        }

        private string GetExchangeResultDialogueID(int inputMaxGrade, int resultGrade)
        {
            if (resultGrade > inputMaxGrade) return "WeaponExchange_Better";
            if (resultGrade < inputMaxGrade) return "WeaponExchange_Worse";
            return "WeaponExchange_Same";
        }

        #endregion
    }
}
