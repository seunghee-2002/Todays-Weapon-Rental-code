using System;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    public class TrackMarker : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button button;

        private static readonly Vector3 normalScale  = Vector3.one;
        private static readonly Vector3 focusedScale = new Vector3(1.5f, 1.5f, 1f);

        public void SetClickCallback(Action callback)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback?.Invoke());
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null) button.interactable = interactable;
        }

        public void SetColor(Color color)
        {
            if (fillImage != null) fillImage.color = color;
        }

        public void SetPending()
        {
            if (fillImage != null) fillImage.color = ColorManager.Instance.GetWhiteColor();
        }

        public void SetResult(bool isSuccess)
        {
            if (fillImage != null) fillImage.color = isSuccess ? ColorManager.Instance.GetGreenColor() : ColorManager.Instance.GetRedColor();
        }

        public void SetIcon(Sprite icon)
        {
            if (iconImage == null) return;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        public void SetFocus(bool active)
        {
            transform.localScale = active ? focusedScale : normalScale;
        }
    }
}
