using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 아이템 선택 카드 - 모험가에게 선물할 아이템
    /// </summary>
    public class IconButton : MonoBehaviour
    {
        [SerializeField] private Image itemBG;
        [SerializeField] private Image itemGlow;
        [SerializeField] private Image itemIcon;
        [SerializeField] private Button button;
        [SerializeField] private GameObject indicator;

        public void Initialize(Sprite icon, Action onClickAction, bool showIndicator = false)
        {
            if (icon == null)
                return;

            if (itemIcon == null)
            {
                itemIcon = gameObject.GetComponentInChildren<Image>();
                if (itemIcon == null)
                {
                    itemIcon = gameObject.AddComponent<Image>();
                }
            }

            itemIcon.sprite = icon;

            if (button == null)
            {
                button = gameObject.GetComponentInChildren<Button>();
                if (button == null)
                {
                    button = gameObject.AddComponent<Button>();
                }
            }

            if (onClickAction != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClickAction.Invoke());
            }

            if (indicator != null)
                indicator.SetActive(showIndicator);
        }

        public void Initialize(Sprite icon, Sprite bg, Color glow, Action onClickAction, bool showIndicator = false)
        {
            if (icon == null || bg == null || glow == null)
                return;

            if (itemIcon != null)
                itemIcon.sprite = icon;

            if (itemBG != null)
                itemBG.sprite = bg;

            if (itemGlow != null)
                itemGlow.color = glow;

            if (button != null)
            {
                if (onClickAction != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => onClickAction.Invoke());
                }
            }

            if (indicator != null)
                indicator.SetActive(showIndicator);
        }

        // 등급(grade) 기반 표시 — 배경색 + 등급 glow색, 프레임 없음
        public void Initialize(Sprite icon, Color bgColor, Color glowColor, Action onClickAction, bool showIndicator = false)
        {
            if (icon == null)
                return;

            if (itemIcon != null)
                itemIcon.sprite = icon;

            if (itemBG != null)
                itemBG.color = bgColor;

            if (itemGlow != null)
                itemGlow.color = glowColor;

            if (button != null)
            {
                if (onClickAction != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => onClickAction.Invoke());
                }
            }

            if (indicator != null)
                indicator.SetActive(showIndicator);
        }
    }
}
