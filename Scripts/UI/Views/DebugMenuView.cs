using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// [에디터 전용] 디버그 버튼 메뉴.
    /// - 토글 버튼으로 최소화/최대화
    /// - 이전/다음 페이지로 디버그 항목을 페이지당 N개씩 분할 표시
    /// - 디버그 항목은 코드(BuildEntries)에서 라벨+액션으로 등록한다.
    ///   버튼 슬롯은 프리팹에서 동적 생성 후 페이지 전환 시 재사용한다.
    ///
    /// 본문 전체가 #if UNITY_EDITOR라 릴리즈 빌드에는 치트 로직이 컴파일되지 않는다.
    /// 클래스 자체는 남긴다 - InGameScene에 하드 배치된 오브젝트라 클래스를 통째로
    /// #if로 감싸면 Missing Script가 되기 때문이다. 빌드에서는 스스로 비활성화만 한다.
    /// </summary>
    public class DebugMenuView : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Slots")]
        [SerializeField] private DebugMenuButton slotPrefab;
        [SerializeField] private Transform slotParent;
        [SerializeField] private int slotsPerPage = 10;

        [Header("Body / Toggle")]
        [SerializeField] private GameObject body;            // 최소화 시 숨길 영역(슬롯 + 네비)
        [SerializeField] private Toggle toggleButton;        // 최소화/최대화 (시작 Off, On = 펼침)

        [Header("Navigation")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI pageText;   // "1 / 2"

        private readonly List<(string label, Action action)> entries = new();
        private readonly List<DebugMenuButton> slots = new();
        private int pageIndex;
        private bool minimized;

        private int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)entries.Count / Mathf.Max(1, slotsPerPage)));

        #region 초기화

        private void Start()
        {
            BuildEntries();
            CreateSlots();

            toggleButton?.onValueChanged.AddListener(OnToggleChanged);
            prevButton?.onClick.AddListener(OnPrevClicked);
            nextButton?.onClick.AddListener(OnNextClicked);

            toggleButton?.SetIsOnWithoutNotify(false); // 시작 Off (focus 꺼짐), 첫 클릭에 On
            SetMinimized(true);
            RefreshPage();
        }

        private void OnDestroy()
        {
            toggleButton?.onValueChanged.RemoveAllListeners();
            prevButton?.onClick.RemoveAllListeners();
            nextButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region 항목 등록

        /// <summary>디버그 항목을 라벨+액션으로 등록한다. 새 항목은 여기에 한 줄 추가하면 된다.</summary>
        private void BuildEntries()
        {
            entries.Clear();

            // 기존 디버그 버튼
            Add("3시간 스킵",        () => TimeManager.Instance?.SkipThreeHours());
            Add("모험가",       () => VisitorManager.Instance?.TestSpawnAdventurer());
            Add("무기상인",     () => VisitorManager.Instance?.TestSpawnWeaponShop());
            Add("대장장이",     () => VisitorManager.Instance?.TestSpawnBlacksmith());
            Add("전체 아이템",  AddAllItems);
            Add("백만 골드",  () => EconomyManager.Instance?.AddGold(1000000, "cheat"));
            Add("아침 이벤트",  () => VisitorManager.Instance?.TestSpawnEventNPC());
            Add("다음날로",      () => TimeManager.Instance?.DebugSkipToNextDayAt9());
            // 시세 계단 확인용 — 그 전날 21시에 서고, 다음날로 넘기면 아침 6:03 공지를 탄다
            Add("133일(예고 전날)", () => TimeManager.Instance?.DebugJumpToDayEnd(133));
            Add("140일(계단 전날)", () => TimeManager.Instance?.DebugJumpToDayEnd(140));
            Add("결과생성(기본무기)",  () => AdventureManager.Instance?.CreateRandomDebugResult(false));
            Add("결과생성(랜덤무기)",  () => AdventureManager.Instance?.CreateRandomDebugResult(true));
            Add("전몬스터 완료모험",  () => AdventureManager.Instance?.CreateAllMonsterCategoryCompletedDebugAdventure());
            Add("전몬스터 진행모험",  () => AdventureManager.Instance?.CreateAllMonsterCategoryOngoingDebugAdventure());
            Add("전비전투 완료모험",  () => AdventureManager.Instance?.CreateAllNonBattleCompletedDebugAdventure());
            Add("전비전투 진행모험",  () => AdventureManager.Instance?.CreateAllNonBattleOngoingDebugAdventure());
            Add("사망 완료모험",      () => AdventureManager.Instance?.CreateDeathDebugAdventure());
            Add("완료 모험 제거",    () => AdventureManager.Instance?.RemoveCompletedDebugAdventures());

            // 아침 이벤트 9종
            Add("이벤트: 무기강화",       () => VisitorManager.Instance?.TestSpawnWeaponEnhanceNPC());
            Add("이벤트: 교환상인",       () => VisitorManager.Instance?.TestSpawnWeaponExchangeNPC());
            Add("이벤트: 수상한투자자",   () => VisitorManager.Instance?.TestSpawnSuspiciousInvestorNPC());
            Add("이벤트: 떠돌이대장장이", () => VisitorManager.Instance?.TestSpawnWanderingBlacksmithNPC());
            Add("이벤트: 길드사절단",     () => VisitorManager.Instance?.TestSpawnGuildEnvoyNPC());
            Add("이벤트: 수수께끼상자",   () => VisitorManager.Instance?.TestSpawnMysteryBoxNPC());
            Add("이벤트: 난민돕기",       () => VisitorManager.Instance?.TestSpawnRefugeeHelpNPC());
            Add("이벤트: 수집가",         () => VisitorManager.Instance?.TestSpawnCollectorNPC());
            Add("이벤트: 암시장상인",     () => VisitorManager.Instance?.TestSpawnBlackMarketNPC());

            // 사망 모험가 이벤트
            Add("네임드 죽이기",     () => VisitorManager.Instance?.TestKillNamedAdventurer());
            Add("Dead: 청소",        () => VisitorManager.Instance?.TestSpawnDeadEventNPC(DeadEventKind.NoVisitor));
            Add("Dead: 부활제안",    () => VisitorManager.Instance?.TestSpawnDeadEventNPC(DeadEventKind.ReviveOffer));
            Add("Dead: 기적생환",    () => VisitorManager.Instance?.TestSpawnDeadEventNPC(DeadEventKind.MiracleRevive));
        }

        private void Add(string label, Action action) => entries.Add((label, action));

        #endregion

        #region 슬롯 / 페이지

        private void CreateSlots()
        {
            if (slotPrefab == null || slotParent == null)
            {
                Log.Warn("[DebugMenuView] slotPrefab 또는 slotParent가 비어있습니다.");
                return;
            }

            for (int i = 0; i < slotsPerPage; i++)
                slots.Add(Instantiate(slotPrefab, slotParent));
        }

        private void RefreshPage()
        {
            pageIndex = Mathf.Clamp(pageIndex, 0, PageCount - 1);

            for (int i = 0; i < slots.Count; i++)
            {
                int entryIndex = pageIndex * slotsPerPage + i;
                if (entryIndex < entries.Count)
                {
                    var (label, action) = entries[entryIndex];
                    slots[i].Bind(label, action);
                    slots[i].SetVisible(true);
                }
                else
                {
                    slots[i].SetVisible(false);
                }
            }

            if (pageText != null) pageText.text = $"{pageIndex + 1} / {PageCount}";
            if (prevButton != null) prevButton.interactable = pageIndex > 0;
            if (nextButton != null) nextButton.interactable = pageIndex < PageCount - 1;
        }

        private void OnPrevClicked()
        {
            if (pageIndex <= 0) return;
            pageIndex--;
            RefreshPage();
        }

        private void OnNextClicked()
        {
            if (pageIndex >= PageCount - 1) return;
            pageIndex++;
            RefreshPage();
        }

        #endregion

        #region 최소화 / 최대화

        private void OnToggleChanged(bool isOn) => SetMinimized(!isOn);

        private void SetMinimized(bool value)
        {
            minimized = value;
            body?.SetActive(!minimized);
        }

        #endregion

        #region 디버그 액션 (다중 라인)

        private void AddAllItems()
        {
            if (InventoryManager.Instance == null) return;

            // 타입별로 그룹화해 라운드로빈으로 지급 → 50슬롯 상한에 걸려도 모든 타입이 고루 들어간다.
            var weaponsByType = DataManager.Instance.GetAllWeapons()
                .GroupBy(w => w.weaponType)
                .Select(g => new Queue<WeaponData>(g))
                .ToList();

            bool addedAny = true;
            while (addedAny && InventoryManager.Instance.CanAddWeapon())
            {
                addedAny = false;
                foreach (var queue in weaponsByType)
                {
                    if (queue.Count == 0) continue;
                    if (!InventoryManager.Instance.CanAddWeapon()) break;
                    InventoryManager.Instance.AddWeapon(new WeaponInstance(queue.Dequeue()));
                    addedAny = true;
                }
            }

            var allMaterials = DataManager.Instance.GetAllMaterials();
            foreach (var material in allMaterials)
                InventoryManager.Instance.AddMaterial(material.StaticID, 100);

            var allActiveItems = DataManager.Instance.GetAllActiveItems();
            foreach (var activeItem in allActiveItems)
                InventoryManager.Instance.AddActiveItem(new ActiveItemInstance(activeItem));
        }

        #endregion
#else
        private void OnEnable()
        {
            // 빌드에서는 스스로 비활성화한다. 비활성이면 이후 로직이 아예 돌지 않는다.
            // Awake가 아니라 OnEnable인 이유: ConsistenceUI.SetMinimal이 이 오브젝트를 다시 켜므로
            // 켜질 때마다 다시 꺼야 한다.
            // Destroy는 쓰지 않는다 - 이 오브젝트를 참조하는 인스펙터 필드(ConsistenceUI.debugButtonsRoot 등)가
            // dangling 상태가 되어 접근 시 MissingReferenceException이 난다.
            gameObject.SetActive(false);
        }
#endif
    }
}
