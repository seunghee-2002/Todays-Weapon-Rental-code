using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental 
{
    public class WeaponShopManager : BaseManager<WeaponShopManager>
    {
        // 상점 설정
        private WeaponShopConfig Config => ConfigManager.Instance.WeaponShop;
        
        // 현재 상점 상품 목록
        [SerializeField] private List<WeaponShopItemData> currentShopItems = new List<WeaponShopItemData>();
        [SerializeField] private int refreshCount;

        public List<WeaponShopItemData> CurrentShopItems => currentShopItems;
        public int RefreshCount => refreshCount;

        // 마지막으로 무기상점이 등장한 날 (확률 누적 기준일)
        [SerializeField] private int lastWeaponShopDay;

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        /// <summary>해당 날짜가 고정 등장일인지</summary>
        public bool IsFixedAppearanceDay(int day)
        {
            var fixedDays = Config.fixedAppearanceDays;
            return fixedDays != null && fixedDays.Contains(day);
        }

        /// <summary>마지막 고정일 이하라 아직 확률 계산을 시작하지 않은 구간인지 (고정일 제외)</summary>
        public bool IsBeforeProbabilityWindow(int day)
        {
            var fixedDays = Config.fixedAppearanceDays;
            int lastFixedDay = (fixedDays != null && fixedDays.Length > 0) ? fixedDays.Max() : 0;
            return day <= lastFixedDay;
        }

        /// <summary>
        /// 오늘 무기상점이 등장할지 판정.
        /// 고정 등장일이면 무조건 등장, 마지막 고정일 이하면 등장 안 함,
        /// 그 이후부터는 누적 확률로 굴린다. (1일차는 튜토리얼이 별도로 처리)
        /// </summary>
        public bool ShouldSpawnToday()
        {
            int currentDay = TimeManager.Instance.CurrentDay;

            if (IsFixedAppearanceDay(currentDay))
                return true;

            if (IsBeforeProbabilityWindow(currentDay))
                return false;

            return UnityEngine.Random.value < GetSpawnChance(currentDay);
        }

        /// <summary>
        /// 오늘 무기상점 등장 확률. base + (등장안한 일수-1) x 하루증가 + 평판등급 x 등급가산, 0~1 클램프.
        /// </summary>
        public float GetSpawnChance(int currentDay)
        {
            int daysSince = currentDay - lastWeaponShopDay;
            int repLevel = (int)(ReputationManager.Instance?.CurrentLevel ?? ReputationLevel.Bronze);

            float chance = Config.spawnBaseChance
                + (daysSince - 1) * Config.spawnChancePerDay
                + repLevel * Config.spawnChancePerReputationLevel;

            return Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 무기상점이 실제로 등장했을 때 호출 — 마지막 등장일을 기록해 확률을 초기화한다.
        /// </summary>
        public void MarkShopSpawned(int day)
        {
            lastWeaponShopDay = day;
            if (GameManager.Instance?.GameData != null)
                GameManager.Instance.GameData.lastWeaponShopDay = day;
        }

        #region 초기화

        public void Initialize(GameData gameData)
        {
            refreshCount = 0;
            currentShopItems.Clear();
            lastWeaponShopDay = gameData?.lastWeaponShopDay ?? 0;

            LoadFromGameData(gameData);
        }

        public List<WeaponShopItemData> GenerateShopItems()
        {
            currentShopItems.Clear();

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsFirstDay() && !TutorialManager.Instance.HasCompletedTutorial)
            {
                refreshCount = Config.maxRefreshCount;
                currentShopItems = TutorialManager.Instance.GenerateTutorialWeaponShop();
                return currentShopItems ?? new List<WeaponShopItemData>();
            }

            if (DataManager.Instance == null || TimeManager.Instance == null)
            {
                Log.Error("WeaponShopManager: Required managers not initialized");
                return new List<WeaponShopItemData>();
            }

            int itemCount = UnityEngine.Random.Range(Config.minWeapons, Config.maxWeapons + 1);
            var allWeapons = DataManager.Instance.GetAllWeapons();

            if (allWeapons == null || allWeapons.Count == 0)
            {
                Log.Error("WeaponShopManager: No weapons available in DataManager");
                return new List<WeaponShopItemData>();
            }

            for (int i = 0; i < itemCount; i++)
            {
                WeaponData weaponData = SelectRandomWeaponByGrade(allWeapons);
                if (weaponData != null)
                {
                    // 무기 값은 등급별 basePrice 그대로 둔다. 후반에 지출이 커지는 건
                    // "값이 오르는" 게 아니라 "비싼 등급이 진열되는" 방식이다(SelectGradeByWeight).
                    currentShopItems.Add(new WeaponShopItemData(weaponData, weaponData.basePrice));
                }
            }

            return currentShopItems;
        }

        private WeaponData SelectRandomWeaponByGrade(List<WeaponData> weapons)
        {
            // 등급 선택
            Grade selectedGrade = SelectGradeByWeight();

            // 해당 등급의 무기만 필터링
            var filteredWeapons = weapons.Where(w => w.baseGrade == selectedGrade).ToList();

            if (filteredWeapons.Count == 0)
            {
                Log.Warn($"WeaponShopManager: No weapons found for grade {selectedGrade}");
                // 폴백: 모든 무기 중 랜덤 선택
                return weapons[UnityEngine.Random.Range(0, weapons.Count)];
            }

            return filteredWeapons[UnityEngine.Random.Range(0, filteredWeapons.Count)];
        }

        private Grade SelectGradeByWeight()
        {
            // 주차 계단 — 후반일수록 고등급이 진열된다. 값이 오르는 게 아니라 물건이 좋아지는 방식이라
            // 플레이어에게 "승급 페널티"로 느껴지지 않는다 (골드_경제_구조.md 4장).
            float[] weights = ConfigManager.Instance.PriceTier?.GetWeaponShopGradeWeights(ConfigManager.CurrentWeek);
            if (weights == null || weights.Length == 0)
            {
                Log.Error("[WeaponShopManager] PriceTierConfig의 무기 상점 등급 가중치가 없습니다 - Common 폴백");
                return Grade.Common;
            }

            float totalWeight = weights.Sum();
            float randomValue = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                // 경계 미포함(<) - roll이 정확히 0일 때 weight 0 등급이 당첨되는 것 방지
                if (randomValue < cumulative)
                {
                    return (Grade)i;
                }
            }

            return Grade.Common; // 폴백
        }

        #endregion

        #region 구매 관리

        /// <summary>
        /// 무기 구매
        /// </summary>
        public bool PurchaseWeapon(WeaponShopItemData item)
        {
            if (item == null)
            {
                Log.Warn("WeaponShopManager: Cannot purchase null item");
                return false;
            }

            if (EconomyManager.Instance == null || InventoryManager.Instance == null)
            {
                Log.Error("[WeaponShopManager] Required managers not initialized");
                return false;
            }

            if (!InventoryManager.Instance.CanAddWeapon())
            {
                UIPopupController.Instance.ShowPopup(L("WeaponShop_InventoryFull"), type: PopupSfxType.Warning);
                return false;
            }

            // 골드 차감
            if (!EconomyManager.Instance.SpendGold(item.Price, $"무기 구매 ({item.WeaponData.DisplayName})"))
            {
                Log.Warn("WeaponShopManager: Not enough gold to purchase weapon");
                return false;
            }
            
            // 인벤토리 추가
            InventoryManager.Instance.AddWeapon(item.WeaponInstance);

            // 상점에서 제거
            currentShopItems.Remove(item);

            Log.Info($"WeaponShopManager: Purchased {item.WeaponData.weaponName} for {item.Price}G");
            QuestManager.Instance?.UpdateProgress(QuestType.WeaponPurchase);
            // 구매 확정 - 강제 종료로 구매 취소/효과 리롤하는 세이브스컴 차단
            GameManager.Instance?.SaveAfterCommittedAction("WeaponShop.Purchase");
            AnalyticsManager.Instance?.Send("weapon_purchased", new Dictionary<string, object>
            {
                { "weapon_id", item.WeaponData.StaticID },
                { "weapon_grade", (int)item.WeaponInstance.currentGrade },
                { "price", item.Price }
            });
            return true;
        }

        /// <summary>
        /// 구매 가능 여부 확인 — 인벤토리 슬롯 여유만 검사. 골드는 EnsureGold가 보충하므로 제외.
        /// (슬롯이 꽉 차면 유산 환전 후 구매 실패하는 낭비를 막기 위해 사전 차단)
        /// </summary>
        public bool CanPurchase(WeaponShopItemData item)
        {
            return item != null && InventoryManager.Instance.CanAddWeapon();
        }

        public bool CanRefresh
        {
            get
            {
                int maxRefresh = Config.maxRefreshCount + (LegacyManager.Instance?.GetShopRefreshBonus() ?? 0);
                return refreshCount < maxRefresh;
            }
        }

        /// <summary>
        /// 상인 인사말. 주차 구간별 풀에서 하나 뽑는다 — 떠돌이 상인이 단골 거래처로 굳어가는 흐름.
        /// 풀이 비어 있으면 기존 고정 문구로 폴백한다.
        /// </summary>
        public string GetGreetingLine()
        {
            int tier = ConfigManager.Instance.PriceTier?.TierIndex(ConfigManager.CurrentWeek) ?? 0;
            // 폴백도 SO 대사(첫 구간 첫 줄)를 참조한다 — 별도 키로 두면 같은 한국어가
            // 두 벌이 되어 번역이 갈린다.
            return Config.GetMerchantGreeting(tier)
                   ?? DataLocalizer.FromTable(DataLocalizer.MerchantGreetingKey(0, 0));
        }

        /// <summary>새로고침 비용 — 기본가 x 주차 계단 배율 (후반일수록 다시 뽑는 풀의 질이 좋아지므로)</summary>
        public int GetRefreshCost() =>
            Mathf.RoundToInt(Config.refreshCost * ConfigManager.Instance.PriceMult(p => p.shopRefreshCost));

        public bool RefreshShop()
        {
            if (!CanRefresh)
            {
                // 횟수 소진 안내 - 골드 부족 오표기 수정
                UIPopupController.Instance.ShowPopup(L("WeaponShop_RefreshExhausted"), type: PopupSfxType.Warning);
                return false;
            }

            // 골드 차감
            if (!EconomyManager.Instance.SpendGold(GetRefreshCost(), "무기 상점 새로고침"))
            {
                Log.Warn("WeaponShopManager: Not enough gold to refresh shop");
                return false;
            }

            // 새 상품 생성
            GenerateShopItems();
            refreshCount++;

            // 새로고침 결과(상품 목록) 확정 - 재시작 리롤 방지
            GameManager.Instance?.SaveAfterCommittedAction("WeaponShop.Refresh");
            return true;
        }

        public void CloseShop()
        {
            currentShopItems.Clear();
            refreshCount = 0;

            // 상점 아이템 저장
            SaveToGameData(GameManager.Instance.GameData);
        }

        #endregion

        #region 저장/로드

        public void SaveToGameData(GameData gameData)
        {
            if (gameData == null)
            {
                Log.Error("[WeaponShopManager] Cannot save: GameData is null");
                return;
            }

            gameData.savedShopItems = currentShopItems.Select(item => new WeaponShopItemSaveData
            {
                weaponDataID = item.WeaponData.StaticID,
                price = item.Price,
                // 인스턴스 전체를 저장해 로드 시 효과가 재추첨되지 않게 한다
                weaponSaveData = item.WeaponInstance.ToSaveData()
            }).ToList();

            gameData.savedShopRefreshCount = refreshCount;
            gameData.lastWeaponShopDay = lastWeaponShopDay;
        }

        private void LoadFromGameData(GameData gameData)
        {
            if (gameData.savedShopItems == null || gameData.savedShopItems.Count == 0)
                return;

            refreshCount = gameData.savedShopRefreshCount;

            foreach (var saveData in gameData.savedShopItems)
            {
                var weaponData = DataManager.Instance.GetWeapon(saveData.weaponDataID);
                if (weaponData == null)
                {
                    Log.Warn($"WeaponShopManager: WeaponData를 찾을 수 없음 (ID: {saveData.weaponDataID})");
                    continue;
                }

                // 저장된 인스턴스로 효과를 그대로 복원한다 (SaveToGameData가 항상 함께 저장한다)
                var effects = InventoryManager.ReifyEffectsFromSave(saveData.weaponSaveData.effects);
                var instance = new WeaponInstance(weaponData, effects, saveData.weaponSaveData);
                currentShopItems.Add(new WeaponShopItemData(instance, saveData.price));
            }

            Log.Info($"WeaponShopManager: 상점 아이템 {currentShopItems.Count}개 복원");
        }

        #endregion
    }

    /// <summary>
    /// 무기 상점 아이템 데이터
    /// </summary>
    [Serializable]
    public class WeaponShopItemData
    {
        [SerializeField] private WeaponInstance weaponInstance;
        [SerializeField] private int price;
        public WeaponInstance WeaponInstance => weaponInstance;
        public WeaponData WeaponData { get => weaponInstance.weaponData;}
        public int Price { get => price;}

        public WeaponShopItemData(WeaponData weaponData, int price)
        {
            this.weaponInstance = new WeaponInstance(weaponData);
            this.price = price;
        }

        // 저장/복원용 - 이미 생성된 인스턴스를 그대로 사용
        public WeaponShopItemData(WeaponInstance weaponInstance, int price)
        {
            this.weaponInstance = weaponInstance;
            this.price = price;
        }
    }

    [Serializable]
    public class WeaponShopItemSaveData
    {
        public string weaponDataID;
        public int price;
        // 효과 포함 전체 인스턴스
        public WeaponInstanceSaveData weaponSaveData;
    }
}
