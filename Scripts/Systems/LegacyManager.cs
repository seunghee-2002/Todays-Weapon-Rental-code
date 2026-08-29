using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 유산(Legacy) 시스템 매니저.
    /// DontDestroyOnLoad — MainMenuScene과 InGameScene 양쪽에서 유지.
    /// </summary>
    public class LegacyManager : BaseManager<LegacyManager>
    {
        [SerializeField] private PlayerData playerData;
        [SerializeField] private LegacyConfig config;

        private Dictionary<UpgradeKey, LegacyUpgradeDefinition> upgradeDict;

        // 이벤트
        public event Action<int> OnLegacyPointsChanged;

        // 현재 유산 포인트
        public int LegacyPoints => playerData?.legacyPoints ?? 0;

        // 영구 데이터 접근 (UI 설정/닉네임/도감 등 회차와 무관하게 유지되는 데이터)
        public PlayerData PlayerData => playerData;

        // 한 번이라도 게임오버를 했는지 (초회차 판정). false면 초회차.
        public bool HasEverGameOver => playerData?.hasEverGameOver ?? false;

        // 전체 튜토리얼(2~12단계)을 한 번이라도 완주했는지 (영구). 완주 계정은 이후 회차에 1단계만 진행한다.
        public bool HasEverCompletedTutorial => playerData?.hasCompletedTutorial ?? false;

        // 강화 해금 안내 팝업을 이미 표시했는지 (1회성).
        public bool HasShownLegacyUnlockPopup => playerData?.hasShownLegacyUnlockPopup ?? false;

        // legacy.json 손상 상태 (본 파일/백업 모두 복구 실패).
        // true인 동안 SaveLegacy와 클라우드 legacy 업로드를 차단해 빈 데이터가 확정되는 것을 막는다
        public bool IsLegacyCorrupted { get; private set; }

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            DontDestroyOnLoad(gameObject);

            BuildUpgradeDict();

            var (status, loaded) = SaveManager.LoadPlayerSafe();
            IsLegacyCorrupted = status == LoadDataStatus.Corrupted;
            playerData = loaded ?? new PlayerData();

            if (IsLegacyCorrupted)
            {
                Log.Error("[LegacyManager] legacy.json 손상 감지 - 빈 데이터 확정 방지를 위해 저장/업로드 차단. " +
                               "클라우드 legacy 다운로드(ReplaceLegacyData)로 복구를 시도한다");
            }
            else
            {
                BackfillGameOverFlag();
            }

            Log.Info($"[LegacyManager] Initialized. LegacyPoints: {LegacyPoints}");
        }

        /// <summary>
        /// 외부(클라우드 동기화/복원)에서 받은 PlayerData로 메모리를 교체하고 이벤트를 재발행한다.
        /// 파일만 갱신하고 메모리를 방치하면 이후 SaveLegacy가 옛 메모리로 롤백시킨다.
        /// 손상 상태였다면 정상 데이터 주입으로 해제된다.
        /// </summary>
        public void ReplaceLegacyData(PlayerData data)
        {
            if (data == null) return;

            playerData = data;
            IsLegacyCorrupted = false;
            BackfillGameOverFlag();
            SaveLegacy();
            OnLegacyPointsChanged?.Invoke(playerData.legacyPoints);
            Log.Info($"[LegacyManager] 외부 데이터로 교체 완료. LegacyPoints: {LegacyPoints}");
        }

        #endregion

        #region 유산 포인트 API

        public void AddLegacyPoints(int amount, string source = "")
        {
            if (amount <= 0) return;
            playerData.legacyPoints += amount;
            OnLegacyPointsChanged?.Invoke(playerData.legacyPoints);
            // 획득 경로가 저장 호출을 누락해도 유산이 증발하지 않도록 내부에서 즉시 저장
            SaveLegacy();
            SendLegacyTransactionAnalytics(amount, "earn", source);
            Log.Info($"[LegacyManager] +{amount} 유산 → 누적: {playerData.legacyPoints}");
        }

        public bool SpendLegacyPoints(int amount, string source = "")
        {
            if (amount <= 0) return false;
            if (playerData.legacyPoints < amount)
            {
                Log.Warn($"[LegacyManager] 유산 부족. 필요: {amount}, 보유: {playerData.legacyPoints}");
                return false;
            }

            playerData.legacyPoints -= amount;
            OnLegacyPointsChanged?.Invoke(playerData.legacyPoints);
            SaveLegacy();
            SendLegacyTransactionAnalytics(amount, "spend", source);
            Log.Info($"[LegacyManager] -{amount} 유산 → 남은: {playerData.legacyPoints}");
            return true;
        }

        /// <summary>legacy_transaction 이벤트 발행 (G10 유산 흐름)</summary>
        private void SendLegacyTransactionAnalytics(int amount, string direction, string source)
        {
            AnalyticsManager.Instance?.Send("legacy_transaction", new Dictionary<string, object>
            {
                { "amount", amount },
                { "direction", direction },
                { "source", string.IsNullOrEmpty(source) ? "unknown" : source },
                { "balance_after", playerData.legacyPoints }
            });
        }

        public bool HasEnoughLegacyPoints(int amount) => playerData.legacyPoints >= amount;

        /// <summary>게임오버 이력 기록 (초회차 판정 해제). 저장은 호출자가 SaveLegacy로 수행.</summary>
        public void MarkGameOver()
        {
            playerData.hasEverGameOver = true;
        }

        /// <summary>
        /// 전체 튜토리얼 완주 기록 (영구) + 계정 최초 1회 완주 보상 유산 지급.
        /// 지급한 유산을 반환한다. 이미 완주 이력이 있으면 아무것도 하지 않고 0을 반환한다.
        /// 완주 플래그를 먼저 확정 저장한 뒤 지급해 중복 지급을 막는다.
        /// </summary>
        public int MarkTutorialCompleted()
        {
            if (playerData.hasCompletedTutorial) return 0;
            playerData.hasCompletedTutorial = true;
            SaveLegacy();
            Log.Info("[LegacyManager] 튜토리얼 완주 기록 (영구)");

            int reward = config?.tutorialCompleteReward ?? 0;
            if (reward > 0) AddLegacyPoints(reward, "tutorial_complete");
            return reward;
        }

        /// <summary>강화 해금 안내 팝업 표시 기록 (1회성). 단독 이벤트이므로 즉시 저장한다.</summary>
        public void MarkLegacyUnlockPopupShown()
        {
            if (playerData.hasShownLegacyUnlockPopup) return;
            playerData.hasShownLegacyUnlockPopup = true;
            SaveLegacy();
            Log.Info("[LegacyManager] 강화 해금 안내 표시 기록");
        }

        /// <summary>
        /// 게임오버 시 획득할 유산 계산
        /// 공식: currentDay + totalCumulativeReputation/50 + (success+failed)/5
        /// </summary>
        public int CalculateEarnedLegacyPoints(GameData gameData)
        {
            int dayBonus        = gameData.currentDay;
            int reputationBonus = gameData.totalCumulativeReputation / 50;
            int adventureBonus  = (gameData.totalSuccessfulAdventures + gameData.totalFailedAdventures) / 5;

            int total = dayBonus + reputationBonus + adventureBonus;
            Log.Info($"[LegacyManager] 유산 계산: 일수({dayBonus}) + 평판({reputationBonus}) + 모험({adventureBonus}) = {total}");
            return total;
        }

        /// <summary>
        /// 유산으로 골드 환전. 환전 비율은 LegacyConfig.goldPerLegacy.
        /// </summary>
        public int GetGoldForLegacy(int legacyAmount)
        {
            return legacyAmount * (config?.goldPerLegacy ?? 200);
        }

        /// <summary>
        /// 부족 골드를 메우는 데 필요한 최소 유산 수량 (올림).
        /// </summary>
        public int GetLegacyCostForGold(int gold)
        {
            return Mathf.CeilToInt((float)gold / (config?.goldPerLegacy ?? 200));
        }

        #endregion

        #region 업그레이드 API

        public int GetUpgradeLevel(UpgradeKey key)
        {
            return playerData?.GetUpgradeLevel(key) ?? 0;
        }

        /// <summary>현재 레벨 기준 다음 업그레이드 비용 (baseCost × scale^currentLevel)</summary>
        public int GetUpgradeCost(UpgradeKey key)
        {
            if (!upgradeDict.TryGetValue(key, out var def)) return 0;
            int currentLevel = GetUpgradeLevel(key);
            if (currentLevel >= def.maxLevel) return 0;
            return CalcCost(def.baseCost, def.costScalePerLevel, currentLevel);
        }

        public bool CanPurchaseUpgrade(UpgradeKey key)
        {
            if (!upgradeDict.TryGetValue(key, out var def)) return false;
            int currentLevel = GetUpgradeLevel(key);
            if (currentLevel >= def.maxLevel) return false;
            if (def.prerequisite != UpgradeKey.None && GetUpgradeLevel(def.prerequisite) <= 0) return false;
            return HasEnoughLegacyPoints(CalcCost(def.baseCost, def.costScalePerLevel, currentLevel));
        }

        public bool PurchaseUpgrade(UpgradeKey key)
        {
            if (!CanPurchaseUpgrade(key)) return false;
            if (!upgradeDict.TryGetValue(key, out var def)) return false;

            int currentLevel = GetUpgradeLevel(key);
            int cost         = CalcCost(def.baseCost, def.costScalePerLevel, currentLevel);
            if (!SpendLegacyPoints(cost, "legacy_upgrade")) return false;

            playerData.SetUpgradeLevel(key, currentLevel + 1);
            playerData.legacyUpgradePurchaseCount++;
            SaveLegacy();
            AnalyticsManager.Instance?.Send("legacy_upgraded", new Dictionary<string, object>
            {
                { "upgrade_key", key.ToString() },
                { "purchase_order", playerData.legacyUpgradePurchaseCount }
            });
            Log.Info($"[LegacyManager] 업그레이드 구매: {key} Lv{currentLevel + 1} (비용: {cost})");
            return true;
        }

        public int GetUpgradeMaxLevel(UpgradeKey key)
        {
            return upgradeDict.TryGetValue(key, out var def) ? def.maxLevel : 0;
        }

        /// <summary>선행 업그레이드 반환. 없으면 UpgradeKey.None 반환</summary>
        public UpgradeKey GetUpgradePrerequisite(UpgradeKey key)
        {
            if (!upgradeDict.TryGetValue(key, out var def)) return UpgradeKey.None;
            return def.prerequisite;
        }

        /// <summary>업그레이드 아이콘 스프라이트 반환. 정의 없으면 null</summary>
        public Sprite GetUpgradeIcon(UpgradeKey key)
        {
            return upgradeDict.TryGetValue(key, out var def) ? def.icon : null;
        }

        /// <summary>description 템플릿에서 {current}=effectValue×level, {value}=effectValue 치환하여 반환</summary>
        public string GetUpgradeDescription(UpgradeKey key)
        {
            if (!upgradeDict.TryGetValue(key, out var def)) return "";
            if (string.IsNullOrEmpty(def.description)) return "";

            int level = GetUpgradeLevel(key);
            // 번역을 먼저 얹고 치환은 그 뒤다. 순서를 바꾸면 이미 숫자로 채워진 문장을
            // 키로 찾게 되어 어떤 언어에서도 안 붙는다.
            return DataLocalizer.LegacyUpgradeDescription(def)
                .Replace("{current}", FormatEffectValue(def.effectValue * level))
                .Replace("{value}",   FormatEffectValue(def.effectValue));
        }

        /// <summary>upgradeStepDescription 템플릿에서 {value}=effectValue 치환하여 반환</summary>
        public string GetUpgradeStepDescription(UpgradeKey key)
        {
            if (!upgradeDict.TryGetValue(key, out var def)) return "";
            if (string.IsNullOrEmpty(def.upgradeStepDescription)) return "";
            return def.upgradeStepDescription.Replace("{value}", FormatEffectValue(def.effectValue));
        }

        private string FormatEffectValue(float v)
        {
            return v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.##");
        }

        #endregion

        #region 내부 메서드

        /// <summary>
        /// hasEverGameOver 필드 추가 이전에 저장된 세이브 백필.
        /// 유산 포인트나 업그레이드 보유는 과거 게임오버 전적을 의미하므로 플래그를 복구한다.
        /// </summary>
        private void BackfillGameOverFlag()
        {
            if (playerData.hasEverGameOver) return;

            // 튜토리얼 완주 보상으로도 유산이 생기므로, 완주 기록이 있는 계정의 legacyPoints는 게임오버 증거가 아니다.
            // (완주 보상만 받은 계정을 백필하면 강화가 게임오버 없이 해금된다)
            bool hasLegacyHistory = !playerData.hasCompletedTutorial && playerData.legacyPoints > 0;
            if (!hasLegacyHistory)
            {
                foreach (var kvp in playerData.permanentUpgrades)
                {
                    if (kvp.Value > 0)
                    {
                        hasLegacyHistory = true;
                        break;
                    }
                }
            }

            if (hasLegacyHistory)
            {
                playerData.hasEverGameOver = true;
                SaveLegacy();
                Log.Info("[LegacyManager] 기존 세이브 백필: hasEverGameOver = true (게임오버 전적 감지)");
            }
        }

        private void BuildUpgradeDict()
        {
            upgradeDict = new Dictionary<UpgradeKey, LegacyUpgradeDefinition>();
            if (config == null)
            {
                Log.Error("[LegacyManager] LegacyConfig가 할당되지 않았습니다!");
                return;
            }
            foreach (var def in config.upgrades)
            {
                upgradeDict[def.key] = def;
            }
        }

        private int CalcCost(int baseCost, float costScalePerLevel, int currentLevel)
        {
            return Mathf.RoundToInt(baseCost * Mathf.Pow(costScalePerLevel, currentLevel));
        }

        #endregion

        #region 효과 조회 메서드

        /// <summary>업그레이드 정의에서 effectValue를 가져온다. 정의가 없으면 0 반환.</summary>
        private float GetEffectValue(UpgradeKey key)
        {
            return upgradeDict.TryGetValue(key, out var def) ? def.effectValue : 0f;
        }

        // A. 시작 조건

        /// <summary>시작 골드 보너스 (effectValue*level, 정수)</summary>
        public int   GetStartingGoldBonus()          => Mathf.RoundToInt(GetEffectValue(UpgradeKey.StartingGold)    * GetUpgradeLevel(UpgradeKey.StartingGold));

        /// <summary>시작 통찰 보너스 (effectValue*level, 정수)</summary>
        public int   GetStartingInsightBonus()       => Mathf.RoundToInt(GetEffectValue(UpgradeKey.StartingInsight) * GetUpgradeLevel(UpgradeKey.StartingInsight));

        /// <summary>인벤토리 슬롯 보너스 (effectValue*level, 정수)</summary>
        public int   GetInventorySlotsBonus()        => Mathf.RoundToInt(GetEffectValue(UpgradeKey.InventorySlots)  * GetUpgradeLevel(UpgradeKey.InventorySlots));

        /// <summary>시작 시 Rare 무기 지급 여부</summary>
        public bool  HasWeaponRare()                 => GetUpgradeLevel(UpgradeKey.WeaponRare) > 0;

        /// <summary>시작 시 Epic 무기 지급 여부</summary>
        public bool  HasWeaponEpic()                 => GetUpgradeLevel(UpgradeKey.WeaponEpic) > 0;

        // B. 무기 & 대장간

        /// <summary>강화 성공률 보너스 (가산: effectValue/100*level, 예: 5*2/100 = +0.1)</summary>
        public float GetEnforceRateBonus()           => GetEffectValue(UpgradeKey.EnforceRate) / 100f * GetUpgradeLevel(UpgradeKey.EnforceRate);

        /// <summary>진화 성공률 보너스 (가산: effectValue/100*level)</summary>
        public float GetEvolveRateBonus()            => GetEffectValue(UpgradeKey.EvolveRate)  / 100f * GetUpgradeLevel(UpgradeKey.EvolveRate);

        /// <summary>강화 비용 배율 (1 - effectValue/100*level)</summary>
        public float GetEnforceCostMultiplier()      => 1f - GetEffectValue(UpgradeKey.EnforceCost) / 100f * GetUpgradeLevel(UpgradeKey.EnforceCost);

        /// <summary>진화 비용 배율 (1 - effectValue/100*level)</summary>
        public float GetEvolveCostMultiplier()       => 1f - GetEffectValue(UpgradeKey.EvolveCost)  / 100f * GetUpgradeLevel(UpgradeKey.EvolveCost);

        /// <summary>재료 감소 배율 — 제작·진화에 적용 (곱계산: (1-effectValue/100)^level, 예: (1-0.1)^2 = 0.81)</summary>
        public float GetMaterialReductionMultiplier() => Mathf.Pow(1f - GetEffectValue(UpgradeKey.MaterialReduction) / 100f, GetUpgradeLevel(UpgradeKey.MaterialReduction));

        /// <summary>재부여 최대 횟수 보너스 (effectValue*level, 정수)</summary>
        public int   GetRerollCountBonus()           => Mathf.RoundToInt(GetEffectValue(UpgradeKey.RerollCount) * GetUpgradeLevel(UpgradeKey.RerollCount));

        /// <summary>재부여 비용 배율 (1 - effectValue/100*level)</summary>
        public float GetRerollCostMultiplier()       => 1f - GetEffectValue(UpgradeKey.RerollCost)       / 100f * GetUpgradeLevel(UpgradeKey.RerollCost);

        /// <summary>분해 보상 배율 (1 + effectValue/100*level)</summary>
        public float GetDisassembleBonusMultiplier() => 1f + GetEffectValue(UpgradeKey.DisassembleBonus) / 100f * GetUpgradeLevel(UpgradeKey.DisassembleBonus);

        // C. 수입 & 모험

        /// <summary>대여 수수료 보너스율 (+effectValue/100*level, 합산)</summary>
        public float GetCommissionRateBonus()        => GetEffectValue(UpgradeKey.CommissionRate) / 100f * GetUpgradeLevel(UpgradeKey.CommissionRate);

        /// <summary>타입 매칭 팁 보너스율 (+effectValue/100*level, 합산)</summary>
        public float GetTipRateBonus()               => GetEffectValue(UpgradeKey.TipRate)        / 100f * GetUpgradeLevel(UpgradeKey.TipRate);

        /// <summary>대성공 확률 배율 (가산: 1 + effectValue/100*level, 예: effectValue=25, level=3 → 1.75)</summary>
        public float GetGreatSuccessRateMultiplier() => 1f + GetEffectValue(UpgradeKey.GreatSuccessRate) / 100f * GetUpgradeLevel(UpgradeKey.GreatSuccessRate);

        /// <summary>모험 속도 배율 (1 - effectValue/100*level)</summary>
        public float GetAdventureSpeedMultiplier()   => 1f - GetEffectValue(UpgradeKey.AdventureSpeed) / 100f * GetUpgradeLevel(UpgradeKey.AdventureSpeed);

        // E. 단발 소비

        /// <summary>AdventurerTalk 프리미엄 유산 비용. 공식: CeilToInt(timeCost / divisor)</summary>
        public int GetAdventurerTalkLegacyCost(int timeCost, bool isNamed)
        {
            float divisor = config?.adventurerTalkLegacyCostDivisor ?? 8f;
            float multiplier = isNamed ? (config?.namedTalkLegacyCostMultiplier ?? 3f) : 1f;
            return Mathf.CeilToInt(timeCost / divisor * multiplier);
        }

        /// <summary>재부여 추가 1회 유산 비용. 무기 등급별로 다르다</summary>
        public int GetExtraRerollCost(WeaponInstance weapon)
        {
            if (weapon == null) return 0;
            var cfg = config;
            if (cfg == null) return 0;

            int gradeIndex = Mathf.Clamp((int)weapon.currentGrade, 0, cfg.extraRerollCostByGrade.Length - 1);
            return cfg.extraRerollCostByGrade[gradeIndex];
        }

        /// <summary>
        /// 강화 즉시 재시도 유산 비용.
        /// 공식: enforceRetryCostByGrade[grade] + enforceLevel * enforceRetryCostPerLevel
        /// </summary>
        public int GetEnforceRetryCost(WeaponInstance weapon)
        {
            if (weapon == null) return 0;
            var cfg = config;
            if (cfg == null) return 0;

            int gradeIndex = Mathf.Clamp((int)weapon.currentGrade, 0, cfg.enforceRetryCostByGrade.Length - 1);
            return cfg.enforceRetryCostByGrade[gradeIndex] + weapon.enforceLevel * cfg.enforceRetryCostPerLevel;
        }

        // D. 방문자 & 상점

        /// <summary>단골 모험가 (7일마다 확정 방문)</summary>
        public bool  HasRegularAdventurer()          => GetUpgradeLevel(UpgradeKey.RegularAdventurer) > 0;

        /// <summary>아침 이벤트 보장 (7일마다 확정 발생)</summary>
        public bool  HasMorningEventGuarantee()      => GetUpgradeLevel(UpgradeKey.MorningEventGuarantee) > 0;

        /// <summary>네임드 스폰 가중치 보너스 (+effectValue*level)</summary>
        public float GetNamedSpawnWeightBonus()      => GetEffectValue(UpgradeKey.NamedSpawnWeight) * GetUpgradeLevel(UpgradeKey.NamedSpawnWeight);

        /// <summary>상점 갱신 횟수 보너스 (effectValue*level, 정수)</summary>
        public int   GetShopRefreshBonus()           => Mathf.RoundToInt(GetEffectValue(UpgradeKey.ShopRefresh)  * GetUpgradeLevel(UpgradeKey.ShopRefresh));

        #endregion

        #region 저장

        public PlayerData GetLegacyData() => playerData;

        public bool SaveLegacy()
        {
            if (IsLegacyCorrupted)
            {
                Log.Warn("[LegacyManager] legacy 손상 상태 - 빈 데이터 확정 방지를 위해 저장 차단");
                return false;
            }

            // legacy 독립 최신본 비교용 저장 시각 갱신
            playerData.legacyLastSavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return SaveManager.SavePlayer(playerData);
        }

        #endregion

        #region 디버그

        [ContextMenu("Debug: 유산 +10000")]
        public void Debug_AddLegacy10000()
        {
            AddLegacyPoints(10000);
            SaveLegacy();
            Log.Info($"[LegacyManager] DEBUG 유산 +10000 → 현재: {LegacyPoints}");
        }

        [ContextMenu("Debug: 유산 0으로 초기화")]
        public void Debug_ResetLegacyPoints()
        {
            playerData.legacyPoints = 0;
            OnLegacyPointsChanged?.Invoke(0);
            SaveLegacy();
            Log.Info("[LegacyManager] DEBUG 유산 포인트 0으로 초기화");
        }

        [ContextMenu("Debug: 업그레이드 전체 초기화")]
        public void Debug_ResetAllUpgrades()
        {
            foreach (var key in upgradeDict.Keys)
                playerData.SetUpgradeLevel(key, 0);
            SaveLegacy();
            Log.Info("[LegacyManager] DEBUG 업그레이드 전체 초기화");
        }

        #endregion
    }
}
