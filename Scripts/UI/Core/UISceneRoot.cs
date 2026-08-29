using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 씬별 UI 루트 참조를 각 매니저에 등록하는 컴포넌트.
    /// 각 씬의 Canvas 하위에 배치하고 필드들을 연결한다.
    /// </summary>
    public class UISceneRoot : MonoBehaviour
    {
        [SerializeField] private Transform panelParent;
        [SerializeField] private Transform popupParent;
        [SerializeField] private Transform toastContainer;
        [SerializeField] private GameObject consistenceUI;
        
        private void Awake()
        { 
            CheckParents();
        }

        private void Start()
        {
            CheckParents();
        }

        private void CheckParents()
        {
            if (panelParent != null)
                UIManager.Instance?.SetPanelParent(panelParent);

            if (popupParent != null)
                UIPopupController.Instance?.SetPopupParent(popupParent);

            if (toastContainer != null)
                UIPopupController.Instance?.SetToastContainer(toastContainer);

            if (consistenceUI != null)
                UIManager.Instance?.SetConsistenceUI(consistenceUI);
        }
    }
}
