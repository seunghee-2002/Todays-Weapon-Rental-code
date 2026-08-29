using System.Collections;
using UnityEngine;

using TMPro;

namespace TodaysWeaponRental
{
    public class ToastView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        
        public void Show(string message, float displayDuration)
        {
            if (messageText != null)
                messageText.text = message;
            
            StartCoroutine(ShowAndFadeOut(displayDuration));
        }
        
        private IEnumerator ShowAndFadeOut(float displayDuration)
        {
            // Fade In
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float elapsed = 0f;
                
                while (elapsed < fadeInDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                    yield return null;
                }
                
                canvasGroup.alpha = 1f;
            }
            
            // Display
            yield return new WaitForSecondsRealtime(displayDuration);
            
            // Fade Out
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeOutDuration));
                    yield return null;
                }
            }
            
            Destroy(gameObject);
        }
    }
}