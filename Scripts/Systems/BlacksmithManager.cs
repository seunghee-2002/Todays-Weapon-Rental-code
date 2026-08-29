using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class BlacksmithManager : BaseManager<BlacksmithManager>
    {
        private BlacksmithConfig Config => ConfigManager.Instance.Blacksmith;

        // 현재 대장장이
        [SerializeField] private BlacksmithData currentBlacksmith;
        public BlacksmithData CurrentBlacksmith => currentBlacksmith;

        public bool IsMaterialDiscountActive => currentBlacksmith != null &&
            (currentBlacksmith.type == BlacksmithType.MaterialReduction);

        public bool IsDisassembleBoostActive => currentBlacksmith != null &&
            (currentBlacksmith.type == BlacksmithType.DisassembleBoost);

        /// <summary>G23: panel_opened(blacksmith)의 blacksmith_type 표기.</summary>
        public static string GetTypeAnalyticsName(BlacksmithType type)
        {
            switch (type)
            {
                case BlacksmithType.CostReduction: return "cost_reduction";
                case BlacksmithType.MaterialReduction: return "material_reduction";
                case BlacksmithType.SuccessRateBoost: return "success_rate";
                case BlacksmithType.DisassembleBoost: return "disassemble_boost";
                default: return "none";
            }
        }

        #region 초기화

        public void Initialize(GameData gameData)
        {
        }

        /// <summary>
        /// 재부여 선택(before/after)이 확정되면 결과를 즉시 저장한다.
        /// after 선택으로 적용된 새 효과가 저장되는 유일한 경로다.
        /// </summary>
        public void ConfirmRerollChoice()
        {
            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.RerollChoice");
        }

        #endregion

        #region 대장장이 등장 시스템

        /// <summary>
        /// 대장장이 등장
        /// </summary>
        public void SpawnBlacksmith()
        {
            var requestedType = GameManager.Instance.GameData.lastRequestedBlacksmithType;

            // 타입 결정: 요청 타입이 있으면 Config.requestedTypeBonus 확률로 반영, 아니면 랜덤 타입
            BlacksmithType selectedType;
            if (requestedType != BlacksmithType.None && UnityEngine.Random.value < Config.requestedTypeBonus)
                selectedType = requestedType;
            else
                selectedType = GetRandomBlacksmithType();

            // 프리미엄 여부: Config.premiumSpawnChance 확률
            bool isPremium = UnityEngine.Random.value < Config.premiumSpawnChance;

            currentBlacksmith = SelectBlacksmith(selectedType, isPremium);

            if (currentBlacksmith == null)
            {
                Log.Error("[BlacksmithManager] SpawnBlacksmith 실패: 선택된 대장장이 없음");
                return;
            }

            Log.Info($"[BlacksmithManager] 스폰: {currentBlacksmith.blacksmithName} (프리미엄: {currentBlacksmith.isPremium})");
        }

        /// <summary>
        /// 특정 StaticID의 대장장이를 강제 지정한다(튜토리얼 고정용).
        /// 랜덤/요청 타입 결정을 건너뛰고 지정 데이터를 currentBlacksmith로 설정한다.
        /// 해당 ID가 없으면 일반 랜덤 스폰으로 폴백한다.
        /// </summary>
        public void SpawnBlacksmithByID(string staticID)
        {
            var match = Config.blacksmithDataArray?.FirstOrDefault(b => b != null && b.StaticID == staticID);
            if (match == null)
            {
                Log.Error($"[BlacksmithManager] SpawnBlacksmithByID 실패: StaticID '{staticID}' 없음 - 랜덤 스폰 폴백");
                SpawnBlacksmith();
                return;
            }

            currentBlacksmith = match;
            Log.Info($"[BlacksmithManager] 고정 스폰: {currentBlacksmith.blacksmithName} ({staticID})");
        }

        /// <summary>
        /// 배열에 존재하는 타입 중 하나를 균등 랜덤으로 반환
        /// </summary>
        private BlacksmithType GetRandomBlacksmithType()
        {
            var types = Config.blacksmithDataArray
                .Select(b => b.type)
                .Distinct()
                .ToArray();
            return types[UnityEngine.Random.Range(0, types.Length)];
        }

        /// <summary>
        /// 대장장이 선택 — (type, isPremium) 정확히 일치하는 데이터 반환
        /// </summary>
        private BlacksmithData SelectBlacksmith(BlacksmithType type, bool isPremium)
        {
            if (Config.blacksmithDataArray == null || Config.blacksmithDataArray.Length == 0)
            {
                Log.Error("[BlacksmithManager] blacksmithDataArray가 비어있습니다!");
                return null;
            }

            // 정확히 일치
            var match = Config.blacksmithDataArray.FirstOrDefault(b => b.type == type && b.isPremium == isPremium);
            if (match != null) return match;

            // 프리미엄 여부만 다른 동일 타입 폴백
            match = Config.blacksmithDataArray.FirstOrDefault(b => b.type == type);
            if (match != null)
            {
                Log.Warn($"[BlacksmithManager] type={type}, isPremium={isPremium} 데이터 없음 — 동일 타입 폴백");
                return match;
            }

            Log.Warn($"[BlacksmithManager] type={type} 데이터 없음 — 첫 번째 항목 폴백");
            return Config.blacksmithDataArray[0];
        }

        /// <summary>
        /// 요청 성공 여부 확인
        /// </summary>
        public bool IsRequestSuccessful()
        {
            if (currentBlacksmith == null) return false;

            var requestedType = GameManager.Instance.GameData.lastRequestedBlacksmithType;
            if (requestedType == BlacksmithType.None) return true;

            return currentBlacksmith.type == requestedType;
        }

        /// <summary>
        /// 현재 대장장이의 인사말 대화 ID 반환
        /// </summary>
        public string GetGreetingDialogueID()
        {
            if (currentBlacksmith == null)
            {
                Log.Warn("CurrentBlacksmith is null, using default greeting");
                return "Blacksmith_DefaultGreeting";
            }

            // 성공/실패 문구는 GetGreetingText가 {GREETING}으로 치환하므로 대화는 하나만 사용
            return "Blacksmith_Greeting";
        }

        /// <summary>
        /// 현재 대장장이의 인사말 텍스트 반환 (성공/실패 구분)
        /// </summary>
        public string GetGreetingText()
        {
            if (currentBlacksmith == null) return "";

            if (!IsRequestSuccessful()) return DataLocalizer.BlacksmithGreetingFailure(currentBlacksmith);

            // 성공 인사말은 주차 구간별로 한 벌씩 — 가격 계단과 같은 경계를 쓴다.
            // 배열이 짧으면 마지막 값으로 클램프한다(에셋 편집 실수 방어).
            var lines = currentBlacksmith.greetingsOnSuccess;
            if (lines == null || lines.Length == 0) return "";

            int tier = ConfigManager.Instance.PriceTier?.TierIndex(ConfigManager.CurrentWeek) ?? 0;
            // 번역 키에는 클램프한 뒤의 인덱스를 쓴다 - 실제로 꺼낸 항목의 위치여야 한다.
            int i = Mathf.Min(tier, lines.Length - 1);
            return DataLocalizer.BlacksmithGreetingSuccess(currentBlacksmith, i, lines[i]);
        }

        /// <summary>
        /// 동적 치환용 대장장이 이름 반환
        /// </summary>
        public string GetBlacksmithName()
        {
            return currentBlacksmith != null
                   ? DataLocalizer.BlacksmithName(currentBlacksmith)
                   : LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", "VisitorType_Blacksmith");
        }

        /// <summary>
        /// 현재 대장장이의 효과 설명 텍스트. 수치는 Config에서 직접 계산한다.
        /// </summary>
        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        public string GetCurrentEffectDescription()
        {
            if (currentBlacksmith == null) return "";

            bool premium = currentBlacksmith.isPremium;

            return currentBlacksmith.type switch
            {
                BlacksmithType.CostReduction =>
                    L("Blacksmith_PerkCostReduction", ("percent",
                        Mathf.RoundToInt((1f - (premium ? Config.costReductionPremium : Config.costReductionNormal)) * 100f))),
                BlacksmithType.MaterialReduction =>
                    L("Blacksmith_PerkMaterialReduction", ("percent",
                        Mathf.RoundToInt((1f - (premium ? Config.materialReductionPremium : Config.materialReductionNormal)) * 100f))),
                BlacksmithType.SuccessRateBoost =>
                    L("Blacksmith_PerkSuccessBoost", ("value",
                        $"{1f + (premium ? Config.successRateBoostPremium : Config.successRateBoostNormal):0.##}")),
                BlacksmithType.DisassembleBoost =>
                    L("Blacksmith_PerkDisassembleBoost", ("value",
                        $"{(premium ? Config.disassembleBoostPremium : Config.disassembleBoostNormal):0.##}")),
                _ => ""
            };
        }

        /// <summary>
        /// 다음 대장장이 타입 요청 설정
        /// </summary>
        public void SetRequestedBlacksmithType(BlacksmithType type)
        {
            GameManager.Instance.GameData.lastRequestedBlacksmithType = type;
            Log.Info($"Requested blacksmith type: {type}");
        }

        #endregion

        #region 타입별 효과 적용

        /// <summary>
        /// 비용 감소 적용.
        /// 대장간 골드 비용(제작/강화/진화/재부여)이 모두 이 경로를 지나므로,
        /// 튜토리얼 중에는 여기서 0을 돌려 전령(본부)이 전액 보전하는 것으로 처리한다.
        /// </summary>
        public int ApplyCostReduction(int baseCost)
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                return 0;   // 튜토리얼: 대장간 비용 전액 보전

            if (currentBlacksmith == null || currentBlacksmith.type != BlacksmithType.CostReduction)
                return baseCost;

            float reduction = currentBlacksmith.isPremium ? Config.costReductionPremium : Config.costReductionNormal;
            return Mathf.RoundToInt(baseCost * reduction);
        }

        /// <summary>
        /// 재부여 골드 비용. costBeforeDiscount는 대장장이 할인 적용 전 금액 (UI 할인 표기용).
        /// </summary>
        public int GetRerollGoldCost(WeaponInstance weapon, int lockedCount, out int costBeforeDiscount)
        {
            // 주차 계단 배율 — 강화/진화는 등급 연동인데 재부여만 고정가라 후반에 실질 무료가 됐다
            float tierMult = ConfigManager.Instance.PriceMult(p => p.rerollCost);
            int base_ = Mathf.RoundToInt(Config.rerollBaseGold * GetLockCostMultiplier(lockedCount) * tierMult);
            costBeforeDiscount = Mathf.RoundToInt(base_ * (LegacyManager.Instance?.GetRerollCostMultiplier() ?? 1f));
            return ApplyCostReduction(costBeforeDiscount);
        }

        /// <summary>로직 전용 오버로드 (할인 전 금액 불필요 시)</summary>
        public int GetRerollGoldCost(WeaponInstance weapon, int lockedCount = 0)
        {
            return GetRerollGoldCost(weapon, lockedCount, out _);
        }

        /// <summary>
        /// 제작 기준 골드 (주차 계단 배율 적용, 대장장이 할인 전).
        /// UI 표기와 실제 차감이 어긋나지 않도록 양쪽 모두 이 경로를 쓴다.
        /// </summary>
        public int GetCraftBaseGold(WeaponRecipeData recipe) =>
            recipe == null ? 0
                : Mathf.RoundToInt(recipe.requiredGold * ConfigManager.Instance.PriceMult(p => p.weaponCraftCost));

        public int GetCraftBaseGold(ActiveItemRecipeData recipe) =>
            recipe == null ? 0
                : Mathf.RoundToInt(recipe.requiredGold * ConfigManager.Instance.PriceMult(p => p.itemCraftCost));

        private float GetLockCostMultiplier(int lockedCount)
        {
            if (lockedCount <= 0) return 1f;
            var multipliers = Config.rerollLockCostMultipliers;
            if (multipliers == null || multipliers.Length == 0) return 1f;
            int index = Mathf.Clamp(lockedCount - 1, 0, multipliers.Length - 1);
            return multipliers[index];
        }

        /// <summary>
        /// 재료 감소 적용 — 블랙스미스 타입 배율 × 유산 배율 (곱계산)
        /// </summary>
        public int ApplyMaterialReduction(int baseMaterial)
        {
            float blacksmithMult = (currentBlacksmith != null && currentBlacksmith.type == BlacksmithType.MaterialReduction)
                ? (currentBlacksmith.isPremium ? Config.materialReductionPremium : Config.materialReductionNormal)
                : 1f;
            float legacyMult = LegacyManager.Instance?.GetMaterialReductionMultiplier() ?? 1f;

            return Mathf.Max(1, Mathf.RoundToInt(baseMaterial * blacksmithMult * legacyMult));
        }

        /// <summary>블랙스미스 타입 + 강화석 가산 보너스 합계 (공통)</summary>
        /// <summary>
        /// 최종 성공률 계산 및 UI 표시용 원시 보너스 반환.
        /// legacyBonus: 강화(GetEnforceRateBonus) 또는 진화(GetEvolveRateBonus) 전달.
        /// rawBonus: 캡 적용 전 보너스 합계 (UI 표시용, out으로 함께 반환).
        /// </summary>
        public float GetSuccessRate(float baseRate, float legacyBonus, out float rawBonus)
        {
            float blacksmithBonus = (currentBlacksmith != null && currentBlacksmith.type == BlacksmithType.SuccessRateBoost)
                ? (currentBlacksmith.isPremium ? Config.successRateBoostPremium : Config.successRateBoostNormal)
                : 0f;
            float forgeBonus = ActiveItemManager.Instance.GetForgeStoneBonus();

            rawBonus = blacksmithBonus + forgeBonus + legacyBonus;
            return Mathf.Min(100f, baseRate * (1f + rawBonus));
        }

        /// <summary>로직 전용 오버로드 (rawBonus 불필요 시)</summary>
        public float GetSuccessRate(float baseRate, float legacyBonus = 0f)
        {
            return GetSuccessRate(baseRate, legacyBonus, out _);
        }

        /// <summary>특정 강화석 보너스를 가정한 성공률 시뮬레이션 (현재 할당된 강화석 무시)</summary>
        public float GetSuccessRateSimulated(float baseRate, float legacyBonus, float forgeStoneBonus)
        {
            float blacksmithBonus = (currentBlacksmith != null && currentBlacksmith.type == BlacksmithType.SuccessRateBoost)
                ? (currentBlacksmith.isPremium ? Config.successRateBoostPremium : Config.successRateBoostNormal)
                : 0f;
            // 실제 판정(GetSuccessRate)과 동일하게 100% 캡 - 초과 표시 방지
            return Mathf.Min(100f, baseRate * (1f + blacksmithBonus + forgeStoneBonus + legacyBonus));
        }

        /// <summary>
        /// 분해 보상 부스트 적용 — 블랙스미스 배율 × 유산 배율 (곱계산)
        /// </summary>
        public float ApplyDisassembleBoost(float baseAmount)
        {
            float blacksmithMult = (currentBlacksmith != null && currentBlacksmith.type == BlacksmithType.DisassembleBoost)
                ? (currentBlacksmith.isPremium ? Config.disassembleBoostPremium : Config.disassembleBoostNormal)
                : 1f;
            float legacyMult = LegacyManager.Instance?.GetDisassembleBonusMultiplier() ?? 1f;

            return baseAmount * blacksmithMult * legacyMult;
        }

        #endregion

        #region 자원 검증

        /// <summary>제작 차단 사유. None이면 제작 가능.</summary>
        public enum CraftBlockReason { None, NotEnoughMaterials, InventoryFull }

        /// <summary>재료 감소를 적용한 필요 개수만큼 보유하고 있는지 검사.</summary>
        private bool HasEnoughMaterials(List<MaterialRequirement> requiredMaterials)
        {
            foreach (var mat in requiredMaterials)
            {
                int reducedCount = ApplyMaterialReduction(mat.count);
                if (InventoryManager.Instance.GetMaterialCount(mat.material.StaticID) < reducedCount)
                    return false;
            }

            return true;
        }

        /// <summary>무기 제작 차단 사유 — 슬롯 꽉 참 우선, 그 다음 재료 부족. 골드는 EnsureGold가 보충하므로 제외.</summary>
        public CraftBlockReason GetCraftBlockReason(WeaponRecipeData recipe)
        {
            if (recipe == null) return CraftBlockReason.NotEnoughMaterials;

            if (!InventoryManager.Instance.CanAddWeapon())
                return CraftBlockReason.InventoryFull;

            return HasEnoughMaterials(recipe.requiredMaterials)
                ? CraftBlockReason.None
                : CraftBlockReason.NotEnoughMaterials;
        }

        /// <summary>아이템 제작 차단 사유 — 아이템은 슬롯 제한 없음. 재료만 검사. 골드는 EnsureGold가 보충하므로 제외.</summary>
        public CraftBlockReason GetCraftBlockReason(ActiveItemRecipeData recipe)
        {
            if (recipe == null) return CraftBlockReason.NotEnoughMaterials;

            return HasEnoughMaterials(recipe.requiredMaterials)
                ? CraftBlockReason.None
                : CraftBlockReason.NotEnoughMaterials;
        }

        /// <summary>제작 가능 여부 — 재료(일반+특수) 보유 + 무기 슬롯 여유. 골드는 EnsureGold가 보충하므로 제외.</summary>
        public bool CanCraft(WeaponRecipeData recipe) => GetCraftBlockReason(recipe) == CraftBlockReason.None;

        /// <summary>아이템 제작 가능 여부 — 재료 보유. 아이템은 슬롯 제한 없음.</summary>
        public bool CanCraft(ActiveItemRecipeData recipe) => GetCraftBlockReason(recipe) == CraftBlockReason.None;

        /// <summary>강화 가능 여부 — 무기 상태만 확인. 재료는 쓰지 않고 골드는 EnsureGold가 보충.</summary>
        public bool CanAffordEnforce(WeaponInstance weapon)
        {
            return weapon != null && weapon.CanEnforce;
        }

        /// <summary>진화 가능 여부 — 무기 상태 + 진화재료 보유. 골드는 EnsureGold가 보충하므로 제외.</summary>
        public bool CanAffordEvolve(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.CanEvolve) return false;

            foreach (var (material, count) in GetEvolveMaterials(weapon.currentGrade))
            {
                if (InventoryManager.Instance.GetMaterialCount(material.StaticID) < count)
                    return false;
            }

            return true;
        }

        /// <summary>재부여 가능 여부 — 무기 상태만 확인. 재료는 쓰지 않고 골드는 EnsureGold가 보충.</summary>
        public bool CanAffordReroll(WeaponInstance weapon)
        {
            return weapon != null && weapon.CanReroll;
        }

        #endregion

        #region 제작 가능 캐시 — 알림도트용

        // 재료가 충분한 레시피 StaticID 집합. 도트는 "재료를 모았다"만 알리므로 무기 슬롯 여유와 골드는 보지 않는다.
        // (슬롯이 꽉 차면 도트는 떠도 제작 버튼이 GetCraftBlockReason으로 막는다.)
        // 매 프레임 계산을 피하기 위해 패널 열림/제작/분해 시점에만 RecalculateCraftableCache로 갱신.
        private readonly HashSet<string> craftableRecipeIDs = new HashSet<string>();

        /// <summary>제작 가능 캐시가 갱신되면 발행. View가 도트를 일괄 갱신.</summary>
        public event Action OnCraftableCacheChanged;

        /// <summary>레시피가 제작 가능 캐시에 포함되는지 조회.</summary>
        public bool IsRecipeCraftable(string recipeID)
        {
            return recipeID != null && craftableRecipeIDs.Contains(recipeID);
        }

        /// <summary>
        /// 전체 무기/아이템 레시피를 순회하며 재료 충족 여부를 다시 계산해 캐시 갱신.
        /// 패널 최초 열림 / 제작(재료 소모) / 무기 분해(재료 획득) 시점에만 호출.
        /// </summary>
        public void RecalculateCraftableCache()
        {
            craftableRecipeIDs.Clear();

            foreach (var recipe in DataManager.Instance.GetAllWeaponRecipes())
            {
                if (recipe != null && HasEnoughMaterials(recipe.requiredMaterials))
                    craftableRecipeIDs.Add(recipe.StaticID);
            }

            foreach (var recipe in DataManager.Instance.GetAllActiveItemRecipes())
            {
                if (recipe != null && HasEnoughMaterials(recipe.requiredMaterials))
                    craftableRecipeIDs.Add(recipe.StaticID);
            }

            OnCraftableCacheChanged?.Invoke();
        }

        #endregion

        #region 즉시 실행 메서드들

        /// <summary>
        /// 무기 제작 즉시 실행
        /// </summary>
        public WeaponInstance ExecuteCraft(WeaponRecipeData recipe)
        {
            if (recipe == null)
            {
                Log.Error("Recipe is null");
                return null;
            }

            // 자원/슬롯 원자 검증 — 부족하면 아무것도 차감하지 않고 중단
            if (!CanCraft(recipe))
            {
                Log.Warn("[BlacksmithManager] ExecuteCraft: 재료 부족 또는 무기 슬롯 부족");
                return null;
            }

            // 골드 차감 우선 — 실패 시 재료 보존
            int goldCost = ApplyCostReduction(GetCraftBaseGold(recipe));
            if (!EconomyManager.Instance.SpendGold(goldCost, "제작 비용"))
            {
                Log.Warn("[BlacksmithManager] ExecuteCraft: 골드 부족");
                return null;
            }

            foreach (var mat in recipe.requiredMaterials)
            {
                int reducedCount = ApplyMaterialReduction(mat.count);
                InventoryManager.Instance.RemoveMaterial(mat.material.StaticID, reducedCount);
            }

            // 무기 생성
            var weapon = new WeaponInstance(recipe.resultWeapon);
            InventoryManager.Instance.AddWeapon(weapon);

            Log.Info($"Crafted weapon: {weapon.weaponData.weaponName}");
            QuestManager.Instance?.UpdateProgress(QuestType.CraftComplete);
            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.CraftWeapon");
            AnalyticsManager.Instance?.Send("blacksmith_craft_done", new Dictionary<string, object>
            {
                { "recipe_id", recipe.StaticID },
                { "result_type", "weapon" }
            });
            return weapon;
        }

        /// <summary>
        /// 아이템 제작 즉시 실행
        /// </summary>
        public ActiveItemInstance ExecuteCraft(ActiveItemRecipeData recipe)
        {
            if (recipe == null)
            {
                Log.Error("Recipe is null");
                return null;
            }

            // 자원 원자 검증 — 부족하면 아무것도 차감하지 않고 중단
            if (!CanCraft(recipe))
            {
                Log.Warn("[BlacksmithManager] ExecuteCraft: 재료 부족");
                return null;
            }

            // 골드 차감 우선 — 실패 시 재료 보존
            int goldCost = ApplyCostReduction(GetCraftBaseGold(recipe));
            if (!EconomyManager.Instance.SpendGold(goldCost, "제작 비용"))
            {
                Log.Warn("[BlacksmithManager] ExecuteCraft: 골드 부족");
                return null;
            }

            foreach (var mat in recipe.requiredMaterials)
            {
                int reducedCount = ApplyMaterialReduction(mat.count);
                InventoryManager.Instance.RemoveMaterial(mat.material.StaticID, reducedCount);
            }

            // 아이템 생성
            var item = new ActiveItemInstance(recipe.resultItem);
            InventoryManager.Instance.AddActiveItem(item);

            Log.Info($"Crafted item: {item.activeItemData.itemName}");
            QuestManager.Instance?.UpdateProgress(QuestType.CraftComplete);
            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.CraftItem");
            AnalyticsManager.Instance?.Send("blacksmith_craft_done", new Dictionary<string, object>
            {
                { "recipe_id", recipe.StaticID },
                { "result_type", "item" }
            });
            return item;
        }

        /// <summary>
        /// 강화 즉시 실행
        /// </summary>
        public (bool success, WeaponInstance result, string enforcedEffectID) ExecuteEnforce(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.CanEnforce)
            {
                Log.Error("Invalid weapon for enforcement");
                return (false, null, null);
            }

            int baseCost   = CalculateEnforceCost(weapon);
            int legacyCost = Mathf.RoundToInt(baseCost * (LegacyManager.Instance?.GetEnforceCostMultiplier() ?? 1f));
            int goldCost   = ApplyCostReduction(legacyCost);
            if (!EconomyManager.Instance.SpendGold(goldCost, "강화 비용"))
            {
                Log.Warn("[BlacksmithManager] ExecuteEnforce: 골드 부족");
                return (false, weapon, null);
            }

            float baseRate = Config.enforceSuccessRates[weapon.enforceLevel];
            float finalRate = GetSuccessRate(baseRate, LegacyManager.Instance?.GetEnforceRateBonus() ?? 0f);
            // 강화석 소비 (성공/실패 무관, 시도 시 소비)
            ActiveItemManager.Instance.ConsumeBlacksmithItem();

            int levelBefore = weapon.enforceLevel;

            bool success = UnityEngine.Random.Range(0f, 100f) < finalRate;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                success = true;   // 튜토리얼: 강화 성공 보장
            string enforcedEffectID = null;

            if (success)
            {
                enforcedEffectID = weapon.Enforce();
                Log.Info($"Enforced weapon: {weapon.weaponData.weaponName} +{weapon.enforceLevel}");
                QuestManager.Instance?.UpdateProgress(QuestType.EnforceSuccess);
            }
            else
            {
                Log.Info($"Enforcement failed: {weapon.weaponData.weaponName}");
            }

            // 도박 결과 확정 - 강제 종료로 실패를 되돌리는 세이브스컴 차단
            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.Enforce");
            AnalyticsManager.Instance?.Send("blacksmith_enforce_done", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "weapon_grade", (int)weapon.currentGrade },
                { "level_before", levelBefore },
                { "level_after", weapon.enforceLevel },
                { "success", success }
            });
            return (success, weapon, enforcedEffectID);
        }

        /// <summary>
        /// 강화 즉시 재시도 — 골드/강화석 소모 없이 판정만 실행
        /// </summary>
        public (bool success, WeaponInstance result, string enforcedEffectID) ExecuteEnforceRetry(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.CanEnforce)
            {
                Log.Error("[BlacksmithManager] ExecuteEnforceRetry: 유효하지 않은 무기");
                return (false, null, null);
            }

            float baseRate  = Config.enforceSuccessRates[weapon.enforceLevel];
            float finalRate = GetSuccessRate(baseRate, LegacyManager.Instance?.GetEnforceRateBonus() ?? 0f);

            bool   success          = UnityEngine.Random.Range(0f, 100f) < finalRate;
            string enforcedEffectID = null;

            if (success)
            {
                enforcedEffectID = weapon.Enforce();
                Log.Info($"[BlacksmithManager] 재시도 강화 성공: {weapon.weaponData.weaponName} +{weapon.enforceLevel}");
                QuestManager.Instance?.UpdateProgress(QuestType.EnforceSuccess);
            }
            else
            {
                Log.Info($"[BlacksmithManager] 재시도 강화 실패: {weapon.weaponData.weaponName}");
            }

            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.EnforceRetry");
            return (success, weapon, enforcedEffectID);
        }

        /// <summary>
        /// 진화 즉시 실행
        /// </summary>
        public (bool success, WeaponInstance result) ExecuteEvolve(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.CanEvolve)
            {
                Log.Error("Invalid weapon for evolution");
                return (false, null);
            }

            // 재료 원자 검증 — 부족하면 차감/판정 없이 중단
            if (!CanAffordEvolve(weapon))
            {
                Log.Warn("[BlacksmithManager] ExecuteEvolve: 진화 재료 부족");
                return (false, weapon);
            }

            // 비용 소모 — 골드 차감 실패 시 판정 없이 중단
            int baseCost   = CalculateEvolveCost(weapon.currentGrade);
            int legacyCost = Mathf.RoundToInt(baseCost * (LegacyManager.Instance?.GetEvolveCostMultiplier() ?? 1f));
            int goldCost   = ApplyCostReduction(legacyCost);
            if (!EconomyManager.Instance.SpendGold(goldCost, "진화 비용"))
            {
                Log.Warn("[BlacksmithManager] ExecuteEvolve: 골드 부족");
                return (false, weapon);
            }

            // 재료 차감 — 실패해도 소모. EvolveWeapon 이전에 차감해야 현재 등급 기준 재료가 빠진다.
            foreach (var (material, count) in GetEvolveMaterials(weapon.currentGrade))
                InventoryManager.Instance.RemoveMaterial(material.StaticID, count);

            // 성공률 계산
            int gradeIndex = (int)weapon.currentGrade;
            float baseRate = Config.evolveSuccessRates[gradeIndex];
            float finalRate = GetSuccessRate(baseRate, LegacyManager.Instance?.GetEvolveRateBonus() ?? 0f);
            // 강화석 소비 (성공/실패 무관, 시도 시 소비)
            ActiveItemManager.Instance.ConsumeBlacksmithItem();

            bool success = UnityEngine.Random.Range(0f, 100f) < finalRate;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                success = true;   // 튜토리얼: 진화 성공 보장

            if (success)
            {
                Grade gradeBefore = weapon.currentGrade;
                EvolveWeapon(weapon);
                Log.Info($"Evolved weapon: {weapon.weaponData.weaponName} to {weapon.currentGrade}");
                QuestManager.Instance?.UpdateProgress(QuestType.EvolveSuccess);
                AnalyticsManager.Instance?.Send("blacksmith_evolve_done", new Dictionary<string, object>
                {
                    { "weapon_id", weapon.weaponData.StaticID },
                    { "grade_before", (int)gradeBefore },
                    { "grade_after", (int)weapon.currentGrade }
                });
            }
            else
            {
                Log.Info($"Evolution failed: {weapon.weaponData.weaponName}");
            }

            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.Evolve");
            return (success, weapon);
        }

        /// <summary>
        /// 분해 보상 순수 계산 (부작용 없음). 등급/강화 레벨/부스트 반영.
        /// </summary>
        private (int gold, int bonusGold, int material, int bonusMaterial, MaterialData mat) CalcDisassembleReward(WeaponInstance weapon)
        {
            // 기본 보상 계산 (강화 레벨 보너스 포함)
            int gradeIndex = (int)weapon.currentGrade;
            float baseGold     = Config.disassembleGoldByGrade[gradeIndex];
            float baseMaterial = Config.disassembleMaterialByGrade[gradeIndex];
            float enforceBonus = 1f + (weapon.enforceLevel * Config.disassembleEnforceBonus);
            baseGold     *= enforceBonus;
            baseMaterial *= enforceBonus;

            int baseGoldInt     = Mathf.RoundToInt(baseGold);
            int baseMaterialInt = Mathf.RoundToInt(baseMaterial);

            // 분해 부스트 적용 (블랙스미스 + 유산)
            int finalGold     = Mathf.RoundToInt(ApplyDisassembleBoost(baseGold));
            int finalMaterial = Mathf.RoundToInt(ApplyDisassembleBoost(baseMaterial));

            var material = GetEnforceMaterialByGrade(weapon.currentGrade);
            return (finalGold, finalGold - baseGoldInt, finalMaterial, finalMaterial - baseMaterialInt, material);
        }

        /// <summary>
        /// 분해 즉시 실행
        /// </summary>
        public (int gold, int bonusGold, int materialCount, int bonusMaterial, MaterialData materialData) ExecuteDisassemble(WeaponInstance weapon)
        {
            var result = DisassembleCore(weapon);
            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.Disassemble");
            return result;
        }

        /// <summary>
        /// 분해 실제 처리 (저장 호출 없음). 일괄분해가 N회 저장을 피하기 위해 분리
        /// </summary>
        private (int gold, int bonusGold, int materialCount, int bonusMaterial, MaterialData materialData) DisassembleCore(WeaponInstance weapon)
        {
            if (weapon == null)
            {
                Log.Error("Weapon is null");
                return (0, 0, 0, 0, null);
            }

            var (finalGold, bonusGold, finalMaterial, bonusMaterial, material) = CalcDisassembleReward(weapon);

            // 보상 지급
            EconomyManager.Instance.AddGold(finalGold, "분해 보상");

            if (material != null)
                InventoryManager.Instance.AddMaterial(material.StaticID, finalMaterial);

            InventoryManager.Instance.RemoveWeapon(weapon);

            Log.Info($"Disassembled weapon: {weapon.weaponData.weaponName}, Gold: {finalGold}(+{bonusGold}), Material: {finalMaterial}(+{bonusMaterial})");
            AnalyticsManager.Instance?.Send("blacksmith_disassemble_done", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "weapon_grade", (int)weapon.currentGrade }
            });
            return (finalGold, bonusGold, finalMaterial, bonusMaterial, material);
        }

        /// <summary>
        /// 일괄분해 미리보기용 총 골드 계산 (부작용 없음)
        /// </summary>
        public (int totalGold, int totalBonusGold) PreviewBatchDisassembleGold(IEnumerable<WeaponInstance> weapons)
        {
            int totalGold = 0;
            int totalBonusGold = 0;
            if (weapons == null) return (0, 0);

            foreach (var weapon in weapons)
            {
                if (weapon == null) continue;
                var (gold, bonusGold, _, _, _) = CalcDisassembleReward(weapon);
                totalGold += gold;
                totalBonusGold += bonusGold;
            }
            return (totalGold, totalBonusGold);
        }

        /// <summary>
        /// 일괄분해 실행. 골드/재료 지급 및 무기 제거 후 결과를 집계해 반환.
        /// </summary>
        public (int totalGold, int totalBonusGold, List<(MaterialData mat, int count)> materials) ExecuteBatchDisassemble(List<WeaponInstance> weapons)
        {
            int totalGold = 0;
            int totalBonusGold = 0;
            var matMap = new Dictionary<MaterialData, int>();

            if (weapons == null)
                return (0, 0, new List<(MaterialData, int)>());

            // RemoveWeapon이 인벤토리를 변경하므로 사본 순회
            foreach (var weapon in weapons.ToList())
            {
                if (weapon == null) continue;

                var (gold, bonusGold, materialCount, _, material) = DisassembleCore(weapon);
                totalGold += gold;
                totalBonusGold += bonusGold;

                if (material != null)
                {
                    matMap.TryGetValue(material, out int prev);
                    matMap[material] = prev + materialCount;
                }
            }

            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.BatchDisassemble");

            var materials = matMap.Select(kv => (kv.Key, kv.Value)).ToList();
            return (totalGold, totalBonusGold, materials);
        }

        /// <summary>
        /// 비용 차감 후 새 효과를 생성해 반환. effects는 아직 적용하지 않음.
        /// 사용자가 확인하면 weapon.ApplyRerolledEffects()를 호출.
        /// </summary>
        public (List<WeaponEffect> newEffects, WeaponInstance result) ExecuteReroll(WeaponInstance weapon, HashSet<int> lockedIndices)
        {
            if (weapon == null || !weapon.CanReroll)
            {
                Log.Error("Invalid weapon for reroll");
                return (null, null);
            }

            int lockedCount = lockedIndices?.Count ?? 0;

            int goldCost = GetRerollGoldCost(weapon, lockedCount);
            if (!EconomyManager.Instance.SpendGold(goldCost, "재부여 비용"))
            {
                Log.Warn("[BlacksmithManager] ExecuteReroll: 골드 부족");
                return (null, null);
            }

            var newEffects = weapon.Reroll(lockedIndices);
            QuestManager.Instance?.UpdateProgress(QuestType.RerollComplete);

            // 골드/횟수 소모를 즉시 확정 저장한다. 효과는 아직 미적용이므로
            // 선택 화면 도중 강제 종료하면 이전 효과로 고정되고, 환불/재굴림 세이브스컴이 차단된다.
            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.Reroll");

            Log.Info($"Rerolled weapon: {weapon.weaponData.weaponName} (count: {weapon.rerollCount})");
            AnalyticsManager.Instance?.Send("blacksmith_reroll_done", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "locked_count", lockedCount }
            });
            return (newEffects, weapon);
        }

        /// <summary>강화 즉시 재시도 유산 차감 — 검증+차감을 원자화. 실패 시 false(차감 없음).</summary>
        public bool TryConsumeEnforceRetry(WeaponInstance weapon)
        {
            if (weapon == null) return false;
            int cost = LegacyManager.Instance.GetEnforceRetryCost(weapon);
            return LegacyManager.Instance.SpendLegacyPoints(cost, "enforce_retry");
        }

        /// <summary>재부여 추가 충전 — 검증+유산 차감+충전을 원자화. 실패 시 false(차감 없음).</summary>
        public bool TryAddExtraRerollCharge(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.CanExtraReroll) return false;
            int cost = LegacyManager.Instance.GetExtraRerollCost(weapon);
            if (!LegacyManager.Instance.SpendLegacyPoints(cost, "extra_reroll")) return false;
            weapon.AddExtraRerollCharge();
            // 유산은 SpendLegacyPoints가 즉시 저장하지만 충전 상태(hasExtraRerollCharge)는
            // gameData 소속이므로 함께 확정 저장한다
            GameManager.Instance?.SaveAfterCommittedAction("Blacksmith.ExtraRerollCharge");
            return true;
        }

        #endregion

        #region 진화 효과 업그레이드

        /// <summary>
        /// 진화 시 기존 효과를 targetGrade에 맞춰 한 단계 상향 (in-place 수정).
        /// 매니저가 카탈로그를 조회해 RuntimeInstance에 새 SO 참조를 주입한다.
        /// </summary>
        public void ApplyEvolveUpgrades(List<WeaponEffect> effectList, Grade targetGrade)
        {
            foreach (var effect in effectList)
            {
                var oldData = effect.effectData;
                if (oldData == null) continue;

                if (oldData.grade >= targetGrade)
                    continue;

                Grade newEffectGrade = (Grade)Mathf.Min((int)oldData.grade + 1, (int)Grade.Legendary);
                var newData = DataManager.Instance.GetWeaponEffectUpgraded(effect.effectDataID, newEffectGrade);

                if (newData != null)
                {
                    effect.effectData   = newData;
                    effect.effectDataID = newData.StaticID;
                    float midNew        = (newData.baseValueRange.x + newData.baseValueRange.y) / 2f;
                    effect.baseValue    = WeaponEffect.IsIntegerType(newData.effectType) ? Mathf.RoundToInt(midNew) : midNew;
                    effect.currentValue = effect.baseValue;
                }
                else
                {
                    float midOld        = (oldData.baseValueRange.x + oldData.baseValueRange.y) / 2f;
                    effect.baseValue    = WeaponEffect.IsIntegerType(oldData.effectType) ? Mathf.RoundToInt(midOld) : midOld;
                    effect.currentValue = effect.baseValue;
                }
            }
        }

        /// <summary>
        /// 진화 후 효과 미리보기용 — 원본 effects는 건드리지 않고 복사본을 업그레이드해 반환.
        /// </summary>
        public List<WeaponEffect> GetSimulatedEvolvedEffects(WeaponInstance weapon)
        {
            Grade nextGrade = weapon.currentGrade + 1;
            var copy = weapon.effects.Select(e => new WeaponEffect(e)).ToList();

            ApplyEvolveUpgrades(copy, nextGrade);
            return copy;
        }

        /// <summary>
        /// 무기 진화 일괄 실행 — 효과 업그레이드와 등급/레벨 처리를 합쳐 orchestrate.
        /// 비용/성공률 처리는 호출자(`ExecuteEvolve` 등)가 책임진다.
        /// </summary>
        public void EvolveWeapon(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.CanEvolve) return;

            Grade nextGrade = weapon.currentGrade + 1;
            ApplyEvolveUpgrades(weapon.effects, nextGrade);
            weapon.Evolve();
        }

        #endregion

        #region 헬퍼 메서드

        /// <summary>
        /// 강화 비용 계산
        /// 공식: baseGold × (enforceLevel + 1)
        /// </summary>
        public int CalculateEnforceCost(WeaponInstance weapon)
        {
            if (weapon == null)
            {
                Log.Error("[BlacksmithManager] CalculateEnforceCost: weapon is null");
                return 0;
            }

            // 등급 x 강화레벨 x 주차 계단. 대장간은 "값어치가 함께 커지는" 축이라
            // 계단을 걸어도 함정이 되지 않는다 (골드_경제_구조.md 4장)
            int baseGold = Config.enforceBaseGoldByGrade[(int)weapon.currentGrade];
            return Mathf.RoundToInt(baseGold * (weapon.enforceLevel + 1)
                                    * ConfigManager.Instance.PriceMult(p => p.blacksmithCost));
        }

        /// <summary>
        /// 진화 비용 계산
        /// </summary>
        public int CalculateEvolveCost(Grade currentGrade)
        {
            return Mathf.RoundToInt(Config.evolveCostByGrade[(int)currentGrade]
                                    * ConfigManager.Instance.PriceMult(p => p.blacksmithCost));
        }

        public MaterialData GetEnforceMaterialByGrade(Grade grade)
        {
            string materialID = grade switch
            {
                Grade.Common => "MAT_ENF_001",
                Grade.Uncommon => "MAT_ENF_002",
                Grade.Rare => "MAT_ENF_003",
                Grade.Epic => "MAT_ENF_004",
                Grade.Legendary => "MAT_ENF_005",
                _ => "MAT_ENF_001"
            };

            return DataManager.Instance.GetMaterial(materialID);
        }

        /// <summary>
        /// 진화 재료 - 주력(현재 등급) + 상위(다음 등급). 재료 감소를 이미 적용한 개수를 돌려준다.
        /// Legendary는 진화 불가이므로 빈 리스트 (GetEnforceMaterialByGrade의 폴백이 Legendary+1을 Common 재료로 만드는 것을 차단).
        /// </summary>
        public List<(MaterialData material, int count)> GetEvolveMaterials(Grade grade)
        {
            var result = new List<(MaterialData, int)>();
            if (grade >= Grade.Legendary) return result;

            var main = GetEnforceMaterialByGrade(grade);
            if (main != null) result.Add((main, ApplyMaterialReduction(Config.evolveMainMaterialCount)));

            var next = GetEnforceMaterialByGrade(grade + 1);
            if (next != null) result.Add((next, ApplyMaterialReduction(Config.evolveNextMaterialCount)));

            return result;
        }

        #endregion

        #region 저장/로드

        /// <summary>
        /// 대장장이 복원 (로드 시 사용)
        /// </summary>
        public void RestoreBlacksmith(BlacksmithType type, bool isPremium)
        {
            // 정확 일치 실패 시 같은 type 우선, 그다음 임의 데이터로 폴백한다.
            // 폴백 없이 return하면 복원된 방문자가 데이터 없는 대장장이로 남는다
            var restored = Config.blacksmithDataArray
                .FirstOrDefault(b => b.type == type && b.isPremium == isPremium)
                ?? Config.blacksmithDataArray.FirstOrDefault(b => b.type == type)
                ?? Config.blacksmithDataArray.FirstOrDefault();

            if (restored == null)
            {
                Log.Error($"[BlacksmithManager] 복원 가능한 대장장이 데이터 없음: type={type}, isPremium={isPremium}");
                return;
            }

            if (restored.type != type || restored.isPremium != isPremium)
                Log.Warn($"[BlacksmithManager] 대장장이 폴백 복원: 요청 ({type}, premium={isPremium}) → ({restored.type}, premium={restored.isPremium})");

            currentBlacksmith = restored;
            Log.Info($"[BlacksmithManager] 복원: {currentBlacksmith.blacksmithName}");
        }

        #endregion
    }
}
