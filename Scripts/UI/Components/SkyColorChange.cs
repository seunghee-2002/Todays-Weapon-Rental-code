using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class SkyColorChange : MonoBehaviour
    {
        [Header("Sky Settings")]
        [SerializeField] private Image skyImage;
        [SerializeField] private CanvasGroup cloudGroup;
        [SerializeField] private List<Image> cloudImages;
        
        [Header("Sky Colors")]
        [SerializeField] private Color morningSkyColor = new Color32(255, 253, 225, 255);
        [SerializeField] private Color daySkyColor = new Color32(255, 255, 255, 255);
        [SerializeField] private Color eveningSkyColor = new Color32(236, 130, 115, 255);
        [SerializeField] private Color nightSkyColor = new Color32(80, 80, 80, 255);

        [Header("Cloud Colors")]
        [SerializeField] private Color morningCloudColor = new Color32(255, 251, 224, 216);
        [SerializeField] private Color dayCloudColor = new Color32(255, 255, 255, 255);
        [SerializeField] private Color eveningCloudColor = new Color32(255, 240, 230, 204);
        [SerializeField] private Color nightCloudColor = new Color32(128, 140, 158, 102);

        [SerializeField] private float transitionDuration = 1f;

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPhaseChanged += UpdateBG;
                UpdateBG(TimeManager.Instance.CurrentPhase);
            }
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPhaseChanged -= UpdateBG;
            }
        }

        private void UpdateBG(TimePhase newPhase)
        {
            UpdateSkyColor(newPhase);
            UpdateCloudColors(newPhase);
            UpdateCloudVisibility(newPhase);
        }

        private void UpdateSkyColor(TimePhase newPhase)
        {
            if (skyImage == null)
                skyImage = gameObject.GetOrAddComponent<Image>();

            Color targetColor = GetSkyColorForPhase(newPhase);
            skyImage.DOColor(targetColor, transitionDuration).SetLink(gameObject);
        }

        private void UpdateCloudColors(TimePhase newPhase)
        {
            Color targetColor = GetCloudColorForPhase(newPhase);
            foreach (var cloudImage in cloudImages)
            {
                if (cloudImage != null)
                {
                    cloudImage.DOColor(targetColor, transitionDuration).SetLink(gameObject);
                }
            }
        }

        private void UpdateCloudVisibility(TimePhase newPhase)
        {
            if (cloudGroup == null)
                return;

            float targetAlpha;
            switch (newPhase)
            {
                case TimePhase.Morning:
                    targetAlpha = 0.85f;
                    break;
                case TimePhase.Day:
                    targetAlpha = 1f;
                    break;
                case TimePhase.Evening:
                    targetAlpha = 0.8f;
                    break;
                case TimePhase.Night:
                    targetAlpha = 0.4f;
                    break;
                default:
                    targetAlpha = 1f;
                    break;
            }

            cloudGroup.DOFade(targetAlpha, transitionDuration).SetLink(gameObject);
        }

        private Color GetSkyColorForPhase(TimePhase phase)
        {
            switch (phase)
            {
                case TimePhase.Morning:
                    return morningSkyColor;
                case TimePhase.Day:
                    return daySkyColor;
                case TimePhase.Evening:
                    return eveningSkyColor;
                case TimePhase.Night:
                    return nightSkyColor;
                default:
                    return Color.white;
            }
        }

        private Color GetCloudColorForPhase(TimePhase phase)
        {
            switch (phase)
            {
                case TimePhase.Morning:
                    return morningCloudColor;
                case TimePhase.Day:
                    return dayCloudColor;
                case TimePhase.Evening:
                    return eveningCloudColor;
                case TimePhase.Night:
                    return nightCloudColor;
                default:
                    return Color.white;
            }
        }
    }
}
