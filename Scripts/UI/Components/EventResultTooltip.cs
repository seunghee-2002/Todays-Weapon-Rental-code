using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using DG.Tweening;

namespace TodaysWeaponRental
{
    public class EventResultTooltip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup tooltipCanvasGroup;
        [SerializeField] private Button overlayButton;
        [SerializeField] private float fadeOutDuration = 0.25f;

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI eventTypeText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Transform materialContainer;
        [SerializeField] private GameObject materialSlotPrefab;

        private readonly List<GameObject> spawnedSlots = new();

        private void Awake()
        {
            overlayButton?.onClick.AddListener(OnOverlayClicked);
        }

        public void Show(EventResult result, int retryCount = 0)
        {
            if (result == null) return;

            DOTween.Kill(tooltipCanvasGroup);
            if (tooltipCanvasGroup != null) tooltipCanvasGroup.alpha = 1f;
            gameObject.SetActive(true);

            if (eventTypeText != null)
                eventTypeText.text = GetEventTypeName(result.eventType);
            if (descriptionText != null)
                descriptionText.text = BuildDescription(result, retryCount);
            if (rewardText != null)
                rewardText.text = BuildRewardText(result);

            RefreshMaterials(result);
        }

        public void Hide()
        {
            DOTween.Kill(tooltipCanvasGroup);
            gameObject.SetActive(false);
        }

        private void OnOverlayClicked()
        {
            DOTween.Kill(tooltipCanvasGroup);
            if (tooltipCanvasGroup != null)
                tooltipCanvasGroup.DOFade(0f, fadeOutDuration)
                    .OnComplete(() => gameObject.SetActive(false))
                    .SetLink(gameObject);
            else
                gameObject.SetActive(false);
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        /// <summary>성공률/회피율 표기. rate는 소수점 없이 반올림한 정수 문자열.</summary>
        private static string Rate(string key, float rate)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "rate", $"{rate:F0}" } } });

        private static string Percent(string key, float value)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "percent", $"{value:F0}" } } });

        private static string RateWithCount(string key, float rate, int count)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "rate", $"{rate:F0}" }, { "count", count } } });

        private string GetEventTypeName(DungeonEventType type)
        {
            string key = type switch
            {
                DungeonEventType.Battle        => "DungeonEventType_Battle",
                DungeonEventType.MiniBoss      => "DungeonEventType_MiniBoss",
                DungeonEventType.Boss          => "DungeonEventType_Boss",
                DungeonEventType.TreasureChest => "DungeonEventType_TreasureChest",
                DungeonEventType.RareDrop      => "DungeonEventType_RareDrop",
                DungeonEventType.Rest          => "DungeonEventType_Rest",
                DungeonEventType.Trap          => "DungeonEventType_Trap",
                DungeonEventType.TrapEvade     => "DungeonEventType_TrapEvade",
                _                              => null
            };
            return key != null
                ? LocalizationSettings.StringDatabase.GetLocalizedString("UI_Common", key)
                : type.ToString();
        }

        private string BuildDescription(EventResult result, int retryCount = 0)
        {
            switch (result.eventType)
            {
                case DungeonEventType.Battle:
                case DungeonEventType.MiniBoss:
                {
                    float rate = result.successRateAtTime * 100f;
                    if (result.isSuccess)
                        return Rate("Tooltip_BattleWin", rate);
                    return Rate("Tooltip_BattleLose", rate);
                }
                case DungeonEventType.Boss:
                {
                    float rate = result.successRateAtTime * 100f;
                    if (result.isSuccess)
                    {
                        if (retryCount > 0)
                            return RateWithCount("Tooltip_BossWinRetry", rate, retryCount + 1);
                        return Rate("Tooltip_BattleWin", rate);
                    }
                    if (retryCount > 0)
                        return RateWithCount("Tooltip_BossLoseRetry", rate, retryCount + 1);
                    return Rate("Tooltip_BattleLose", rate);
                }

                case DungeonEventType.TreasureChest:
                    return L("Tooltip_TreasureChest");

                case DungeonEventType.RareDrop:
                    return L("Tooltip_RareDrop");

                case DungeonEventType.Rest:
                    return L("Tooltip_Rest");

                case DungeonEventType.Trap:
                {
                    float rate = result.successRateAtTime * 100f;
                    return result.isSuccess
                        ? Rate("Tooltip_TrapEvaded", rate)
                        : Rate("Tooltip_TrapHit", rate);
                }

                default:
                    return string.Empty;
            }
        }

        private string BuildRewardText(EventResult result)
        {
            switch (result.eventType)
            {
                case DungeonEventType.Trap:
                    if (!result.isSuccess)
                    {
                        float penalty = Mathf.Abs(ConfigManager.Instance.Adventure.trapSuccessPenalty) * 100f;
                        string redHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetRedColor());
                        return "<color=" + redHex + ">" + Percent("Tooltip_TrapPenalty", penalty) + "</color>";
                    }
                    return "";

                case DungeonEventType.Rest:
                {
                    string greenHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor());
                    return "<color=" + greenHex + ">" + L("Tooltip_RestProtection") + "</color>";
                }

                case DungeonEventType.Battle:
                case DungeonEventType.MiniBoss:
                case DungeonEventType.TreasureChest:
                {
                    if (!result.isSuccess) return SurvivalText(result);
                    int gold = result.goldReward + result.bonusGold;
                    return gold > 0 ? GoldText(gold) : "";
                }

                case DungeonEventType.RareDrop:
                {
                    int materialTypes = CountMaterialTypes(result);
                    return materialTypes > 0 ? MaterialsText(materialTypes) : "";
                }

                case DungeonEventType.Boss:
                {
                    if (!result.isSuccess) return SurvivalText(result);
                    int gold = result.goldReward + result.bonusGold;
                    int materialTypes = CountMaterialTypes(result);
                    var parts = new List<string>();
                    if (gold > 0) parts.Add(GoldText(gold));
                    if (materialTypes > 0) parts.Add(MaterialsText(materialTypes));
                    return string.Join("\n", parts);
                }

                default:
                    return "";
            }
        }

        /// <summary>사망 굴림이 떴지만 STR 재굴림으로 살아남았을 때만 표시</summary>
        private string SurvivalText(EventResult result)
        {
            if (!result.survivedByStrength) return "";
            string greenHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor());
            return "<color=" + greenHex + ">" + L("Tooltip_SurvivedByStrength") + "</color>";
        }

        private static string GoldText(int gold)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", "Tooltip_GoldGained",
                   arguments: new object[] { new Dictionary<string, object> { { "gold", gold.ToString("N0") } } });

        private static string MaterialsText(int count)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", "Tooltip_MaterialsGained",
                   arguments: new object[] { new Dictionary<string, object> { { "count", count } } });

        private static int CountMaterialTypes(EventResult result)
        {
            var ids = new HashSet<string>();
            if (result.materialDrops != null)
                foreach (var m in result.materialDrops)
                    if (m != null && m.quantity > 0) ids.Add(m.materialDataID);
            if (result.bonusMaterials != null)
                foreach (var m in result.bonusMaterials)
                    if (m != null && m.quantity > 0) ids.Add(m.materialDataID);
            return ids.Count;
        }

        private void RefreshMaterials(EventResult result)
        {
            foreach (var go in spawnedSlots) Destroy(go);
            spawnedSlots.Clear();

            if (materialContainer == null || materialSlotPrefab == null) return;

            var all = new List<MaterialInstance>();
            if (result.materialDrops != null) all.AddRange(result.materialDrops);
            if (result.bonusMaterials != null) all.AddRange(result.bonusMaterials);

            foreach (var mat in all)
            {
                if (mat == null || mat.quantity <= 0) continue;
                var matData = mat.materialData;
                if (matData == null) continue;

                var go = Instantiate(materialSlotPrefab, materialContainer);
                spawnedSlots.Add(go);
                var slot = go.GetComponent<MaterialSlotUI>();
                slot?.Initialize(matData);
                slot?.SetCount(mat.quantity);
            }
        }
    }
}
