using UnityEngine;
using UnityEngine.Localization.Settings;
using System;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class EconomyManager : BaseManager<EconomyManager>
    {
        private GameData gameData;

        // 프로퍼티
        public int CurrentGold => gameData?.gold ?? 0;

        // 이벤트
        public event Action<int> OnGoldChanged;

        public void Initialize(GameData data)
        {
            gameData = data;
            Log.Info($"EconomyManager: Initialized with {CurrentGold} gold.");
        }

        #region 골드 관리

        public void AddGold(int amount, string reason = "")
        {
            if (gameData == null)
            {
                Log.Error("EconomyManager: GameData is null. Call Initialize() first.");
                return;
            }
    
            if (amount <= 0)
            {
                Log.Warn($"EconomyManager: Cannot add non-positive amount {amount}");
                return;
            }

            gameData.gold += amount;

            OnGoldChanged?.Invoke(gameData.gold);
            SendGoldTransactionAnalytics(amount, "earn", reason);

            // 골드 획득 퀘스트는 금액을 누적한다. 퀘스트 보상 골드는 지급 시점에
            // 해당 퀘스트가 Completed 상태라 UpdateProgress에서 자동으로 걸러진다.
            QuestManager.Instance?.UpdateProgress(QuestType.GoldEarned, amount: amount);

            Log.Info($"EconomyManager: +{amount}G ({reason}) → Total: {gameData.gold}G");
        }

        public bool SpendGold(int amount, string reason = "") 
        {
            if (gameData == null)
            {
                Log.Error("EconomyManager: GameData is null. Call Initialize() first.");
                return false;
            }

            if (amount < 0)
            {
                Log.Warn($"EconomyManager: Cannot spend negative amount {amount}");
                return false;
            }

            // 0원은 차감할 게 없으므로 성공으로 본다(튜토리얼 비용 보전, 무료 매물 등).
            if (amount == 0) return true;

            if (gameData.gold < amount)
            {
                Log.Warn($"EconomyManager: Insufficient gold. Required: {amount}G, Current: {gameData.gold}G");
                return false;
            }

            gameData.gold -= amount;

            OnGoldChanged?.Invoke(gameData.gold);
            SendGoldTransactionAnalytics(amount, "spend", reason);

            return true;
        }

        /// <summary>gold_transaction 이벤트 발행 (G10 골드 흐름)</summary>
        private void SendGoldTransactionAnalytics(int amount, string direction, string reason)
        {
            AnalyticsManager.Instance?.Send("gold_transaction", new Dictionary<string, object>
            {
                { "amount", amount },
                { "direction", direction },
                { "source", GetSourceAnalyticsName(reason) }
            });
        }

        /// <summary>
        /// 한국어 reason 문자열 → Analytics source 값 매핑 (Documents/Analytics_이벤트_설계.md).
        /// reason은 UI/로그용 표기라 그대로 두고, 집계용 source만 여기서 정규화한다.
        /// </summary>
        private static string GetSourceAnalyticsName(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "other";

            // 지출 계열 (구체적인 것 먼저)
            if (reason.StartsWith("무기 구매")) return "weapon_purchase";
            if (reason.StartsWith("강화 비용")) return "blacksmith_enforce";
            if (reason.StartsWith("진화 비용")) return "blacksmith_evolve";
            if (reason.StartsWith("재부여 비용")) return "blacksmith_reroll";
            if (reason.StartsWith("제작 비용")) return "blacksmith_craft";
            if (reason.StartsWith("점술가 상담")) return "seer";
            if (reason == "수색꾼 파견") return "scout_dispatch";
            if (reason == "무기 상점 새로고침") return "shop_refresh";
            if (reason == "의뢰판 새로고침") return "quest_board_refresh";
            if (reason == "주간 벌금 납부") return "quest_fine";
            if (reason == "모험가 부활") return "adventurer_revive";

            // 획득 계열
            if (reason.StartsWith("모험 성공") || reason.StartsWith("모험 후퇴") || reason.StartsWith("보스 패배"))
                return "adventure_reward";
            if (reason == "퀘스트 보상") return "quest_reward";
            if (reason == "유산 골드 구매") return "legacy_gold_purchase";
            if (reason == "잡일 보상") return "chore_reward";
            if (reason == "수색꾼 환불") return "scout_refund";
            if (reason == "분해 보상") return "blacksmith_disassemble";
            if (reason.StartsWith("대화")) return "dialogue";

            // 아침 이벤트 계열 (투자/상자/사절단/수집가/암시장/난민)
            if (reason.StartsWith("수상한 투자자") || reason == "투자 결과" ||
                reason.Contains("상자") || reason.StartsWith("길드 사절단") ||
                reason == "수집가 판매" || reason == "암시장 구매" || reason == "난민 돕기 기부")
                return "morning_event";

            // 재료 구매 ("{재료명} 구매", "재료 일괄 구매" - 수색꾼 결과창/튜토리얼)
            if (reason.EndsWith("구매")) return "scout_material";

            return "other";
        }

        public bool HasEnoughGold(int amount)
        {
            return gameData.gold >= amount;
        }

        /// <summary>
        /// 골드가 충분하면 onReady를 즉시 호출.
        /// 부족하면 유산 구매 팝업을 띄우고, 충전 후 onReady / 취소 시 onCancel 호출.
        /// 골드를 직접 차감하지 않으므로 호출부(ExecuteCraft 등)의 SpendGold가 실제 차감을 담당한다.
        /// </summary>
        public void EnsureGold(int amount, Action onReady, Action onCancel = null)
        {
            if (HasEnoughGold(amount))
            {
                onReady?.Invoke();
                return;
            }

            int shortage = amount - CurrentGold;
            int legacyCostNeeded = LegacyManager.Instance.GetLegacyCostForGold(shortage);
            if (!LegacyManager.Instance.HasEnoughLegacyPoints(legacyCostNeeded))
            {
                UIPopupController.Instance?.ShowToast(
                    LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Economy_NotEnoughGold"),
                    type: PopupSfxType.Warning);
                onCancel?.Invoke();
                return;
            }

            UIPopupController.Instance?.ShowLegacyGoldPurchase(
                shortage: shortage,
                onConfirm: () =>
                {
                    int legacyCost = LegacyManager.Instance.GetLegacyCostForGold(shortage);
                    int goldGain   = LegacyManager.Instance.GetGoldForLegacy(legacyCost);
                    if (!LegacyManager.Instance.SpendLegacyPoints(legacyCost, "gold_purchase"))
                    {
                        UIPopupController.Instance?.ShowToast(
                            LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Economy_NotEnoughLegacy"),
                            type: PopupSfxType.Warning);
                        onCancel?.Invoke();
                        return;
                    }
                    AddGold(goldGain, "유산 골드 구매");
                    onReady?.Invoke();
                },
                onCancel: onCancel
            );
        }

        #endregion
    }
}