using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public static class ScrollViewExtensions
{
    public static void ResetPosition(this ScrollRect scrollRect, MonoBehaviour mono)
    {
        if (scrollRect == null || mono == null) return;
        
        // 오브젝트가 활성화되어 있는 경우에만 코루틴 실행
        if (mono.gameObject.activeInHierarchy)
        {
            mono.StartCoroutine(ResetRoutine(scrollRect));
        }
        else
        {
            // 비활성 상태라면 그냥 즉시 값을 설정 (코루틴 없이)
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private static IEnumerator ResetRoutine(ScrollRect scrollRect)
    {
        // UI 배치가 끝날 때까지 한 프레임 대기
        yield return new WaitForEndOfFrame();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.horizontalNormalizedPosition = 0f;
        }
    }
}