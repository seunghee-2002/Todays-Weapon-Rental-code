using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 대장간 무기 패널 View
    /// 좌측: WeaponType별 필터 토글 + 무기 스크롤뷰
    /// 우측: 선택된 무기 DetailPanel (고정) + Upgrade/Reroll/Disassemble 버튼
    /// </summary>
    public class BlacksmithWeaponPanelView : MonoBehaviour
    {
        [Header("Weapon Type Filter Toggles")]
        [Tooltip("[0]=All, [1]=Sword, [2]=Axe, [3]=Bow, [4]=Crossbow, [5]=Staff, [6]=Tome, [7]=Dagger, [8]=Shuriken")]
        [SerializeField] private Toggle[] weaponTypeFilterToggles;
        [Tooltip("weaponTypeFilterToggles와 동일 인덱스로 대응하는 Focus 오브젝트")]
        [SerializeField] private GameObject[] weaponTypeFocusObjects;

        [Header("Weapon Scroll View")]
        [SerializeField] private ScrollRect weaponScrollRect;
        [SerializeField] private Transform weaponListContainer;
        [SerializeField] private GameObject weaponCardPrefab;

        [Header("빈 목록 텍스트")]
        [SerializeField] private TextMeshProUGUI listEmptyText;

        [Header("Detail Panel")]
        [SerializeField] private TextMeshProUGUI weaponTypeText;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private TextMeshProUGUI weaponEnforceLevelText;
        [SerializeField] private TextMeshProUGUI weaponRerollCountText;
        [SerializeField] private Image weaponImage;
        [SerializeField] private Image weaponBGColor;
        [SerializeField] private Image weaponFrameColor;
        [SerializeField] private Transform effectContainer;
        [SerializeField] private GameObject effectPrefab;

        [Header("Action Buttons")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeButtonText;
        [SerializeField] private Button rerollButton;

        [Header("Disassemble (상시 노출)")]
        [Tooltip("분해모드 진입 버튼")]
        [SerializeField] private Button disassembleButton;
        [Tooltip("분해모드 중 노출되는 실행 버튼")]
        [SerializeField] private Button disassembleExecuteButton;
        [Tooltip("분해모드 중 선택 개수 표시 텍스트")]
        [SerializeField] private TextMeshProUGUI selectedCountText;
        [Tooltip("분해모드 중 노출되는 취소 버튼")]
        [SerializeField] private Button disassembleCancelButton;
        [Tooltip("분해모드 중 선택 영역 외를 가리는 dim")]
        [SerializeField] private GameObject disassembleDim;
        [Tooltip("분해 보상 증가 대장장이일 때 켜지는 인디케이터 (분해 버튼의 자식으로 배치)")]
        [SerializeField] private GameObject disassembleBonusIndicator;

        private BlacksmithController controller;
        private WeaponInstance selectedWeapon;
        private WeaponType? currentTypeFilter = null;
        private BlacksmithWeaponCardItem selectedCard;

        // weaponNameText는 무기 등급에 따라 색이 바뀌므로, 빈 상태로 되돌릴 때 사용할 인스펙터 원래 색을 최초 1회 캐싱
        private Color defaultNameColor;
        private bool defaultNameColorCached = false;

        private bool isDisassembleMode = false;
        private readonly HashSet<string> checkedWeaponIDs = new HashSet<string>();

        private Dictionary<string, BlacksmithWeaponCardItem> weaponCards = new Dictionary<string, BlacksmithWeaponCardItem>();

        #region 초기화

        private void OnDestroy()
        {
            upgradeButton?.onClick.RemoveAllListeners();
            rerollButton?.onClick.RemoveAllListeners();
            disassembleButton?.onClick.RemoveAllListeners();
            disassembleExecuteButton?.onClick.RemoveAllListeners();
            disassembleCancelButton?.onClick.RemoveAllListeners();

            if (weaponTypeFilterToggles != null)
            {
                foreach (var toggle in weaponTypeFilterToggles)
                    toggle?.onValueChanged.RemoveAllListeners();
            }

            ClearWeaponList();
        }

        public void Initialize(BlacksmithController ctrl)
        {
            controller = ctrl;

            if (!defaultNameColorCached && weaponNameText != null)
            {
                defaultNameColor = weaponNameText.color;
                defaultNameColorCached = true;
            }

            upgradeButton?.onClick.RemoveAllListeners();
            rerollButton?.onClick.RemoveAllListeners();
            disassembleButton?.onClick.RemoveAllListeners();
            disassembleExecuteButton?.onClick.RemoveAllListeners();
            disassembleCancelButton?.onClick.RemoveAllListeners();

            upgradeButton?.onClick.AddListener(OnUpgradeClicked);
            rerollButton?.onClick.AddListener(OnRerollClicked);
            disassembleButton?.onClick.AddListener(OnDisassembleClicked);
            disassembleExecuteButton?.onClick.AddListener(OnDisassembleExecuteClicked);
            disassembleCancelButton?.onClick.AddListener(OnDisassembleCancelClicked);

            SetWeaponScrollLocked(false);   // 튜토리얼 잔여 잠금 방어

            selectedWeapon = null;
            currentTypeFilter = null;
            isDisassembleMode = false;
            checkedWeaponIDs.Clear();
            disassembleExecuteButton?.gameObject.SetActive(false);
            disassembleCancelButton?.gameObject.SetActive(false);
            disassembleButton?.gameObject.SetActive(false);
            disassembleDim?.SetActive(false);
            disassembleBonusIndicator?.SetActive(BlacksmithManager.Instance.IsDisassembleBoostActive);

            SubscribeFilterToggles();
            ResetFilterToggles();
            RefreshWeaponList();
        }

        private void SubscribeFilterToggles()
        {
            if (weaponTypeFilterToggles == null) return;

            var weaponTypes = (WeaponType[])System.Enum.GetValues(typeof(WeaponType));
            for (int i = 0; i < weaponTypeFilterToggles.Length; i++)
            {
                var toggle = weaponTypeFilterToggles[i];
                if (toggle == null) continue;

                toggle.onValueChanged.RemoveAllListeners();

                int capturedIndex = i;
                if (i == 0)
                {
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        SetFocusObject(capturedIndex, isOn);
                        if (!isOn) return;
                        currentTypeFilter = null;
                        RefreshWeaponList();
                    });
                }
                else
                {
                    int typeIndex = i - 1;
                    if (typeIndex < weaponTypes.Length)
                    {
                        WeaponType type = weaponTypes[typeIndex];
                        toggle.onValueChanged.AddListener(isOn =>
                        {
                            SetFocusObject(capturedIndex, isOn);
                            if (!isOn) return;
                            currentTypeFilter = type;
                            RefreshWeaponList();
                        });
                    }
                }
            }
        }

        private void SetFocusObject(int index, bool isOn)
        {
            if (weaponTypeFocusObjects == null || index >= weaponTypeFocusObjects.Length) return;
            weaponTypeFocusObjects[index]?.SetActive(isOn);
        }

        private void ResetFilterToggles()
        {
            if (weaponTypeFilterToggles != null && weaponTypeFilterToggles.Length > 0 && weaponTypeFilterToggles[0] != null)
                weaponTypeFilterToggles[0].isOn = true;
        }

        #endregion

        #region 무기 목록

        public void RefreshWeaponList()
        {
            var allWeapons = InventoryManager.Instance.GetAvailableWeapons();
            var allWeaponIDs = new HashSet<string>(allWeapons.Select(w => w.instanceID));

            // 인벤토리에서 완전히 제거된 경우에만 selectedWeapon 초기화
            if (selectedWeapon != null && !allWeaponIDs.Contains(selectedWeapon.instanceID))
            {
                selectedWeapon = null;
                selectedCard = null;
            }

            var weapons = allWeapons;
            if (currentTypeFilter.HasValue)
                weapons = weapons.Where(w => w.weaponData.weaponType == currentTypeFilter.Value).ToList();

            weapons = weapons
                .OrderByDescending(w => w.currentGrade)
                .ThenByDescending(w => w.enforceLevel)
                .ToList();

            ShowListEmptyText(weapons.Count == 0
                ? L(currentTypeFilter.HasValue ? "Inventory_EmptyWeaponFiltered" : "Inventory_EmptyWeaponAll")
                : null);

            // 필터된 목록에 없는 카드 제거
            var weaponIDs = new HashSet<string>(weapons.Select(w => w.instanceID));
            var toRemove = weaponCards.Where(kvp => !weaponIDs.Contains(kvp.Key)).ToList();
            foreach (var kvp in toRemove)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                weaponCards.Remove(kvp.Key);
            }

            // 카드 추가 및 순서 정렬
            for (int i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weaponCards.TryGetValue(weapon.instanceID, out var card))
                {
                    card.UpdateUI();
                    card.transform.SetSiblingIndex(i);
                }
                else
                {
                    CreateWeaponCard(weapon, i);
                }
            }

            // 분해모드: 체크 상태 복원. 인벤토리에서 완전히 사라진 무기만 정리하고,
            // 필터로 가려진 무기의 체크는 세션 동안 유지한다.
            if (isDisassembleMode)
            {
                checkedWeaponIDs.RemoveWhere(id => !allWeaponIDs.Contains(id));
                foreach (var kvp in weaponCards)
                    kvp.Value?.SetChecked(checkedWeaponIDs.Contains(kvp.Key));

                if (selectedCard != null)
                {
                    selectedCard.SetSelected(false);
                    selectedCard = null;
                }
                ShowEmptyDetail();
                UpdateExecuteButton();
                weaponScrollRect?.ResetPosition(this);
                return;
            }

            // 선택 상태 복원: 필터된 목록에 있으면 detail 표시, 없으면 숨김
            if (selectedWeapon != null && weaponCards.TryGetValue(selectedWeapon.instanceID, out var restoredCard))
            {
                if (selectedCard != null && selectedCard != restoredCard)
                    selectedCard.SetSelected(false);
                selectedCard = restoredCard;
                selectedCard.SetSelected(true);
                ShowWeaponDetail(selectedWeapon);
            }
            else
            {
                if (selectedCard != null)
                {
                    selectedCard.SetSelected(false);
                    selectedCard = null;
                }
                ShowEmptyDetail();
            }

            weaponScrollRect?.ResetPosition(this);
        }

        private void CreateWeaponCard(WeaponInstance weapon, int siblingIndex)
        {
            if (weaponCardPrefab == null || weaponListContainer == null) return;

            var obj = Instantiate(weaponCardPrefab, weaponListContainer);
            var card = obj.GetComponent<BlacksmithWeaponCardItem>();
            if (card != null)
            {
                card.Initialize(weapon, OnWeaponCardClicked);
                weaponCards[weapon.instanceID] = card;
                obj.transform.SetSiblingIndex(siblingIndex);
            }
        }

        /// <summary>
        /// 특정 무기 카드 갱신 (강화/진화 완료 후 컨트롤러에서 호출)
        /// </summary>
        public void RefreshWeaponCard(WeaponInstance weapon)
        {
            if (weapon == null) return;

            if (weaponCards.TryGetValue(weapon.instanceID, out var card))
            {
                card.UpdateUI();
                // 현재 선택된 무기라면 DetailPanel도 갱신
                if (selectedWeapon?.instanceID == weapon.instanceID)
                    ShowWeaponDetail(weapon);
            }
            else
            {
                RefreshWeaponList();
            }
        }

        private void ClearWeaponList()
        {
            foreach (var kvp in weaponCards)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            weaponCards.Clear();
        }

        private void OnWeaponCardClicked(WeaponInstance weapon)
        {
            if (isDisassembleMode)
            {
                ToggleCheck(weapon);
                return;
            }

            selectedWeapon = weapon;

            if (selectedCard != null)
                selectedCard.SetSelected(false);
            if (weaponCards.TryGetValue(weapon.instanceID, out var card))
            {
                selectedCard = card;
                selectedCard.SetSelected(true);
            }

            ShowWeaponDetail(weapon);

            // 튜토리얼: 무기 선택 시 현재 대장간 단계에 맞는 다음 하이라이트로 진행(라우팅은 TutorialManager)
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialBlacksmithWeaponSelected();
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

        #region Detail Panel

        private void ShowEmptyDetail()
        {
            if (weaponTypeText != null) weaponTypeText.text = "";
            if (weaponNameText != null)
            {
                weaponNameText.text = L("Blacksmith_SelectWeaponHint");
                weaponNameText.color = defaultNameColor;
            }
            if (weaponRerollCountText != null)
                weaponRerollCountText.text = "";
            weaponBGColor?.gameObject.SetActive(false);
            upgradeButton?.gameObject.SetActive(false);
            rerollButton?.gameObject.SetActive(false);
            // 분해모드 중에는 disassembleButton 대신 execute/cancel이 노출되므로 건드리지 않는다
            if (!isDisassembleMode)
                disassembleButton?.gameObject.SetActive(false);
            // 같은 메서드 안 다른 참조들과 동일하게 null 가드
            if (effectContainer != null)
                effectContainer.gameObject.SetActive(false);
        }

        private void ShowWeaponDetail(WeaponInstance weapon)
        {
            upgradeButton?.gameObject.SetActive(true);
            rerollButton?.gameObject.SetActive(true);
            disassembleButton?.gameObject.SetActive(true);

            if (weaponTypeText != null)
                weaponTypeText.text = UITranslator.GetString(weapon.weaponData.weaponType);
            if (weaponNameText != null)
            {
                weaponNameText.text = $"{weapon.weaponData.DisplayName} +{weapon.enforceLevel}";
                weaponNameText.color = ColorManager.Instance.GetGradeAccentColor(weapon.currentGrade);
            }
                
            if (weaponEnforceLevelText != null)
                weaponEnforceLevelText.text = $"+{weapon.enforceLevel}";
            if (weaponRerollCountText != null)
                weaponRerollCountText.text = L("Blacksmith_WeaponRerollCount",
                    ("count", weapon.rerollCount), ("max", weapon.DisplayMaxRerollCount));
            if (weaponImage != null && weapon.weaponData.icon != null)
                weaponImage.sprite = weapon.weaponData.icon;

            // 등급 스프라이트
            if (weaponBGColor != null)
            {
                weaponBGColor.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
                weaponBGColor.gameObject.SetActive(true);
            }
            if (weaponFrameColor != null)
                weaponFrameColor.sprite = IconManager.Instance?.GetFrameByGrade(weapon.currentGrade);

            UpdateEffectList(weapon);
            UpdateActionButtons(weapon);
        }

        private void UpdateEffectList(WeaponInstance weapon)
        {
            if (effectContainer == null || effectPrefab == null) return;

            effectContainer.gameObject.SetActive(true);

            foreach (Transform child in effectContainer)
                Destroy(child.gameObject);

            foreach (var effect in weapon.effects)
            {
                var obj = Instantiate(effectPrefab, effectContainer);
                var item = obj.GetComponent<WeaponEffectListItem>();
                item?.Initialize(effect, false);
            }
        }

        private void UpdateActionButtons(WeaponInstance weapon)
        {
            // Upgrade 버튼: Enforce 가능이면 "강화", Evolve 가능이면 "진화", 둘 다 불가면 비활성화
            if (upgradeButton != null)
            {
                if (weapon.CanEnforce)
                {
                    upgradeButton.interactable = true;
                    if (upgradeButtonText != null) upgradeButtonText.text = C("Common_Enforce");
                }
                else if (weapon.CanEvolve)
                {
                    upgradeButton.interactable = true;
                    if (upgradeButtonText != null) upgradeButtonText.text = C("Common_Evolve");
                }
                else
                {
                    upgradeButton.interactable = false;
                    if (upgradeButtonText != null) upgradeButtonText.text = L("Blacksmith_UpgradeMax");
                }
            }

            if (rerollButton != null)
                rerollButton.interactable = weapon.CanReroll;
        }

        #endregion

        #region 버튼 이벤트

        private void OnUpgradeClicked()
        {
            if (selectedWeapon == null) return;
            controller?.OnUpgradeButtonClicked(selectedWeapon);
        }

        private void OnRerollClicked()
        {
            if (selectedWeapon == null) return;
            controller?.OnRerollButtonClicked(selectedWeapon);
        }

        private void OnDisassembleClicked()
        {
            // 보유 무기가 1개뿐이면 분해모드를 거치지 않고 바로 확인 팝업으로
            var allWeapons = InventoryManager.Instance.GetAvailableWeapons();
            if (allWeapons.Count == 1)
            {
                controller?.OnSingleDisassembleRequested(allWeapons[0]);
                return;
            }

            EnterDisassembleMode();
        }

        private void OnDisassembleExecuteClicked()
        {
            // 체크된 무기는 다른 필터에서 선택됐을 수 있으므로 인벤토리 전체에서 해석한다.
            var weapons = InventoryManager.Instance.GetAvailableWeapons()
                .Where(w => checkedWeaponIDs.Contains(w.instanceID))
                .ToList();

            if (weapons.Count == 0) return;
            controller?.OnBatchDisassembleRequested(weapons);
        }

        private void OnDisassembleCancelClicked()
        {
            ExitDisassembleMode();
        }

        #endregion

        #region 분해모드

        private void EnterDisassembleMode()
        {
            isDisassembleMode = true;
            checkedWeaponIDs.Clear();

            // 진입 시 선택 중이던 무기는 자동으로 체크 (해제 전에 ID 캡처)
            string autoCheckID = selectedWeapon?.instanceID;
            if (autoCheckID != null)
                checkedWeaponIDs.Add(autoCheckID);

            // 단일 선택 해제 및 디테일 비움
            if (selectedCard != null)
            {
                selectedCard.SetSelected(false);
                selectedCard = null;
            }
            selectedWeapon = null;
            ShowEmptyDetail();

            // 모든 카드 체크 상태 반영 (자동 체크된 무기만 켜짐)
            foreach (var kvp in weaponCards)
                kvp.Value?.SetChecked(checkedWeaponIDs.Contains(kvp.Key));

            // 버튼 그룹 전환
            disassembleButton?.gameObject.SetActive(false);
            disassembleExecuteButton?.gameObject.SetActive(true);
            disassembleCancelButton?.gameObject.SetActive(true);
            disassembleDim?.SetActive(true);
            UpdateExecuteButton();
        }

        public void ExitDisassembleMode()
        {
            isDisassembleMode = false;

            foreach (var id in checkedWeaponIDs)
            {
                if (weaponCards.TryGetValue(id, out var card))
                    card?.SetChecked(false);
            }
            checkedWeaponIDs.Clear();

            disassembleExecuteButton?.gameObject.SetActive(false);
            disassembleCancelButton?.gameObject.SetActive(false);
            // 분해모드 종료 시 무기 선택이 해제된 빈 상태이므로 진입 버튼도 숨긴다 (무기 선택 시에만 노출)
            disassembleButton?.gameObject.SetActive(false);
            disassembleDim?.SetActive(false);
        }

        private void ToggleCheck(WeaponInstance weapon)
        {
            string id = weapon.instanceID;
            if (!checkedWeaponIDs.Remove(id))
                checkedWeaponIDs.Add(id);

            if (weaponCards.TryGetValue(id, out var card))
                card?.SetChecked(checkedWeaponIDs.Contains(id));

            UpdateExecuteButton();
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

        private static string C(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", key);

        private void UpdateExecuteButton()
        {
            int count = checkedWeaponIDs.Count;
            if (selectedCountText != null)
                selectedCountText.text = L("Blacksmith_SelectedCount", ("count", count));
            if (disassembleExecuteButton != null)
                disassembleExecuteButton.interactable = count > 0;
        }

        #endregion

        #region 튜토리얼 하이라이트 접근자

        /// <summary>튜토리얼 하이라이트용 — 강화/진화(Upgrade) 버튼 RectTransform.</summary>
        public RectTransform GetUpgradeButtonRect()
        {
            var rect = upgradeButton?.transform as RectTransform;
            // 버튼이 HorizontalLayoutGroup 안이라 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }

        /// <summary>튜토리얼 하이라이트 중 목록이 스크롤돼 대상 카드가 어긋나지 않도록 잠근다.</summary>
        public void SetWeaponScrollLocked(bool locked)
        {
            if (weaponScrollRect != null)
                weaponScrollRect.enabled = !locked;
        }

        /// <summary>튜토리얼 하이라이트용 — 재부여(Reroll) 버튼 RectTransform.</summary>
        public RectTransform GetRerollButtonRect()
        {
            var rect = rerollButton?.transform as RectTransform;
            // 버튼이 HorizontalLayoutGroup 안이라 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (rect?.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            return rect;
        }

        /// <summary>튜토리얼 하이라이트용 — 지정 WeaponData StaticID를 가진 첫 무기 카드의 RectTransform.</summary>
        public RectTransform GetWeaponCardRect(string weaponDataStaticID)
        {
            var weapon = InventoryManager.Instance?.GetAvailableWeapons()
                ?.FirstOrDefault(w => w?.weaponData != null && w.weaponData.StaticID == weaponDataStaticID);
            if (weapon == null) return null;

            // GridLayoutGroup 배치가 다음 패스에 반영되므로 위치를 읽기 전에 즉시 리빌드.
            if (weaponListContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            if (weaponCards.TryGetValue(weapon.instanceID, out var card) && card != null)
                return card.transform as RectTransform;
            return null;
        }

        #endregion
    }
}
