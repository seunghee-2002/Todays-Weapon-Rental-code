using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 상시 표시 UI(consistenceUI) 루트에 부착되는 컴포넌트.
    /// minimal 모드에서 TopBar만 남기고 나머지 바와 디버그 버튼을 숨긴다.
    /// </summary>
    public class ConsistenceUI : MonoBehaviour
    {
        [Header("Minimal 모드에서 숨길 요소")]
        [SerializeField] private GameObject visitorSideBar;
        [SerializeField] private GameObject bottomBar;
        [SerializeField] private GameObject debugButtonsRoot;

        /// <summary>minimal=true면 TopBar를 제외한 바/디버그 버튼을 숨긴다.</summary>
        public void SetMinimal(bool minimal)
        {
            bool show = !minimal;
            // C#의 ?.는 Unity가 오버로드한 == 를 타지 않아 파괴된 오브젝트를 통과시킨다.
            // (파괴된 UnityEngine.Object는 C# 참조로는 여전히 non-null) 반드시 != null 로 비교할 것.
            if (visitorSideBar != null) visitorSideBar.SetActive(show);
            if (bottomBar != null) bottomBar.SetActive(show);
            if (debugButtonsRoot != null) debugButtonsRoot.SetActive(show);
        }
    }
}
