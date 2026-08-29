using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    public enum UpgradeCategory { Start, Blacksmith, Adventure, Special }

    public readonly struct UpgradeItemData
    {
        public readonly UpgradeKey key;
        public readonly string displayName;
        public readonly Sprite icon;
        public readonly int level;
        public readonly int maxLevel;
        public readonly int cost;
        public readonly bool canPurchase;
        public readonly bool isLocked;
        public readonly string prerequisiteDisplayName;
        public readonly string description;
        public readonly string upgradeStepDescription;

        public UpgradeItemData(
            UpgradeKey key, string displayName, Sprite icon,
            int level, int maxLevel, int cost,
            bool canPurchase, bool isLocked,
            string prerequisiteDisplayName, string description, string upgradeStepDescription)
        {
            this.key                      = key;
            this.displayName              = displayName;
            this.icon                     = icon;
            this.level                    = level;
            this.maxLevel                 = maxLevel;
            this.cost                     = cost;
            this.canPurchase              = canPurchase;
            this.isLocked                 = isLocked;
            this.prerequisiteDisplayName  = prerequisiteDisplayName;
            this.description              = description;
            this.upgradeStepDescription   = upgradeStepDescription;
        }
    }

    public class LegacyUpgradeController : BaseController<LegacyUpgradeView>
    {
        private UpgradeCategory currentCategory = UpgradeCategory.Start;

        #region 카테고리 정의

        private static readonly Dictionary<UpgradeCategory, UpgradeKey[]> CategoryKeys = new()
        {
            [UpgradeCategory.Start] = new[]
            {
                UpgradeKey.StartingGold, UpgradeKey.WeaponRare, UpgradeKey.WeaponEpic,
                UpgradeKey.StartingInsight, UpgradeKey.InventorySlots
            },
            [UpgradeCategory.Blacksmith] = new[]
            {
                UpgradeKey.EnforceRate, UpgradeKey.EvolveRate, UpgradeKey.EnforceCost,
                UpgradeKey.EvolveCost, UpgradeKey.MaterialReduction, UpgradeKey.RerollCount,
                UpgradeKey.RerollCost, UpgradeKey.DisassembleBonus
            },
            [UpgradeCategory.Adventure] = new[]
            {
                UpgradeKey.CommissionRate, UpgradeKey.TipRate,
                UpgradeKey.GreatSuccessRate, UpgradeKey.AdventureSpeed
            },
            [UpgradeCategory.Special] = new[]
            {
                UpgradeKey.RegularAdventurer, UpgradeKey.MorningEventGuarantee,
                UpgradeKey.NamedSpawnWeight, UpgradeKey.ShopRefresh
            },
        };

        #endregion

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            view?.SetController(this);
        }

        protected override void SubscribeControllerEvents()
        {
            if (LegacyManager.Instance == null) return;
            LegacyManager.Instance.OnLegacyPointsChanged += OnLegacyPointsUpdated;
            view?.RefreshLegacyPoints(LegacyManager.Instance.LegacyPoints);
        }

        protected override void UnsubscribeControllerEvents()
        {
            if (LegacyManager.Instance != null)
                LegacyManager.Instance.OnLegacyPointsChanged -= OnLegacyPointsUpdated;
        }

        #endregion

        #region 열기 / 닫기

        public void OpenPanel()
        {
            UIManager.Instance?.OpenPanel<LegacyUpgradeView>();
            view?.RefreshLegacyPoints(LegacyManager.Instance?.LegacyPoints ?? 0);
        }

        public void OnCloseClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("legacy_upgrade", "close");
            UIManager.Instance?.ClosePanel<LegacyUpgradeView>();
        }

        #endregion

        #region View로부터 호출되는 메서드

        public void OnCategorySelected(UpgradeCategory category)
        {
            AnalyticsManager.Instance?.SendButtonClick("legacy_upgrade", "category_selected", new Dictionary<string, object>
            {
                { "category", category.ToString() }
            });

            currentCategory = category;
            RefreshCurrentCategory();
        }

        public void OnBuyClicked(UpgradeKey key)
        {
            AnalyticsManager.Instance?.SendButtonClick("legacy_upgrade", "buy", new Dictionary<string, object>
            {
                { "upgrade_key", key.ToString() }
            });

            if (LegacyManager.Instance == null) return;

            bool success = LegacyManager.Instance.PurchaseUpgrade(key);
            if (!success)
            {
                Log.Warn($"[LegacyUpgradeController] 구매 실패: {key}");
                return;
            }

            view?.RefreshLegacyPoints(LegacyManager.Instance.LegacyPoints);
            view?.UpdateItemStates(BuildItemData());
        }

        #endregion

        #region 이벤트 핸들러

        private void OnLegacyPointsUpdated(int points)
        {
            if (view != null && view.IsOpen)
            {
                view.RefreshLegacyPoints(points);
                view.RefreshItems(BuildItemData());
            }
        }

        #endregion

        #region 내부 메서드

        private void RefreshCurrentCategory() => view?.RefreshItems(BuildItemData());

        private List<UpgradeItemData> BuildItemData()
        {
            if (LegacyManager.Instance == null) return new List<UpgradeItemData>();
            if (!CategoryKeys.TryGetValue(currentCategory, out var keys)) return new List<UpgradeItemData>();

            var items = new List<UpgradeItemData>(keys.Length);
            int currentPoints = LegacyManager.Instance.LegacyPoints;

            foreach (var key in keys)
            {
                int level    = LegacyManager.Instance.GetUpgradeLevel(key);
                int maxLevel = LegacyManager.Instance.GetUpgradeMaxLevel(key);
                int cost     = LegacyManager.Instance.GetUpgradeCost(key);

                UpgradeKey prereq    = LegacyManager.Instance.GetUpgradePrerequisite(key);
                bool isLocked        = prereq != UpgradeKey.None && LegacyManager.Instance.GetUpgradeLevel(prereq) <= 0;
                bool canPurchase     = !isLocked && level < maxLevel && currentPoints >= cost;

                string name     = UITranslator.GetString(key);
                string prereqName = prereq != UpgradeKey.None ? UITranslator.GetString(prereq) : "";
                string desc     = LegacyManager.Instance.GetUpgradeDescription(key);
                string stepDesc = LegacyManager.Instance.GetUpgradeStepDescription(key);
                Sprite icon     = LegacyManager.Instance.GetUpgradeIcon(key);

                items.Add(new UpgradeItemData(key, name, icon, level, maxLevel, cost, canPurchase, isLocked, prereqName, desc, stepDesc));
            }

            return items;
        }

        #endregion
    }
}
