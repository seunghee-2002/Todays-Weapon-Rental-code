using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 준비 Tab3 — 성공률 영향 요소 칩.
    /// 메인 영역엔 Icon만 노출. 채워진 칸 수만큼 segment 스프라이트를 동적 생성.
    /// 클릭 시 nameText/detailText가 있는 Tooltip 토글.
    /// </summary>
    public class AdventureInfoCardItem : MonoBehaviour
    {
        [Header("Main")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Transform segmentContainer;
        [SerializeField] private GameObject segmentPrefab;
        [SerializeField] private GameObject unknownIndicator;
        [SerializeField] private GameObject confirmIndicator;
        [SerializeField] private Button button;

        [Header("Tooltip")]
        [SerializeField] private GameObject tooltipObject;
        [SerializeField] private TextMeshProUGUI tooltipNameText;
        [SerializeField] private TextMeshProUGUI tooltipDetailText;
        [SerializeField] private GameObject focusIndicator;

        private float _cachedContainerWidth = -1f;

        // 튜토리얼 6-D: 칩 클릭(툴팁 열람)을 감지해 진행하기 위한 콜백. 평소엔 null.
        private Action onTutorialClick;

        /// <summary>튜토리얼 전용 — 칩 클릭 시 호출할 콜백을 등록한다(툴팁 토글과 별개로 추가 호출).</summary>
        public void SetTutorialClickCallback(Action callback) => onTutorialClick = callback;

        public void SetInfo(BonusType type, float value, bool isConfirmed, bool isMultiplier)
        {
            var info = ConfigManager.Instance.AdventureInfo?.GetBonusInfo(type);
            if (info == null)
            {
                Log.Warn($"[AdventureInfoCardItem] BonusVisualInfo 없음: {type}");
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (iconImage != null)
            {
                iconImage.sprite = info.icon;
                iconImage.enabled = info.icon != null;
            }

            BuildSegments(info, value);

            if (confirmIndicator != null) confirmIndicator.SetActive(isConfirmed);
            if (unknownIndicator != null) unknownIndicator.SetActive(!isConfirmed);

            if (tooltipNameText != null)
                tooltipNameText.text = DataLocalizer.BonusVisualName(info);
            if (tooltipDetailText != null)
                tooltipDetailText.text = FormatDetail(value, isConfirmed, isMultiplier);

            if (tooltipObject != null)
                tooltipObject.SetActive(false);
            if (focusIndicator != null)
                focusIndicator.SetActive(false);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClicked);
            }
        }

        private void BuildSegments(BonusVisualInfo info, float value)
        {
            if (segmentContainer == null) return;

            for (int i = segmentContainer.childCount - 1; i >= 0; i--)
                Destroy(segmentContainer.GetChild(i).gameObject);

            if (segmentPrefab == null) return;

            if (_cachedContainerWidth < 0f && segmentContainer is RectTransform containerRT)
                _cachedContainerWidth = containerRT.rect.width;

            int segmentCount = ConfigManager.Instance.AdventureInfo?.segmentCount ?? 5;
            if (segmentCount <= 0) segmentCount = 5;

            float ratio  = info.maxAbsValue > 0f ? Mathf.Abs(value) / info.maxAbsValue : 0f;
            int   filled = Mathf.Clamp(Mathf.CeilToInt(ratio * segmentCount), 0, segmentCount);
            Color color  = value < 0f ? info.negativeColor : info.positiveColor;

            float segmentWidth = CalculateSegmentWidth(segmentContainer, _cachedContainerWidth, segmentCount);

            for (int i = 0; i < filled; i++)
            {
                var seg = Instantiate(segmentPrefab, segmentContainer);
                seg.GetOrAddComponent<Image>().color = color;

                if (segmentWidth > 0f && seg.transform is RectTransform segRT)
                {
                    var size = segRT.sizeDelta;
                    size.x = segmentWidth;
                    segRT.sizeDelta = size;
                }
            }
        }

        private static float CalculateSegmentWidth(Transform container, float containerWidth, int segmentCount)
        {
            if (container == null || segmentCount <= 0 || containerWidth <= 0f) return 0f;

            float spacing = 0f;
            float padding = 0f;
            var hlg = container.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                spacing = hlg.spacing;
                padding = hlg.padding.left + hlg.padding.right;
            }

            float available = containerWidth - padding - spacing * (segmentCount - 1);
            return available > 0f ? available / segmentCount : 0f;
        }

        private void OnClicked()
        {
            if (tooltipObject != null)
            {
                bool show = !tooltipObject.activeSelf;
                tooltipObject.SetActive(show);
                if (focusIndicator != null)
                    focusIndicator.SetActive(show);
            }

            onTutorialClick?.Invoke();
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveAllListeners();
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

        private static string FormatDetail(float value, bool isConfirmed, bool isMultiplier)
        {
            string body = isMultiplier
                ? L("AdventureInfo_Multiplier", ("value", $"{1f + value:0.##}"))
                : $"{(value > 0f ? "+" : "")}{value * 100f:0.#}%";
            return isConfirmed ? body : L("AdventureInfo_Unconfirmed", ("body", body));
        }
    }
}
