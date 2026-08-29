using UnityEngine;

namespace TodaysWeaponRental
{
    public abstract class BaseController<TView> : MonoBehaviour where TView : BaseView
    {
        [SerializeField] protected TView view;

        protected virtual void Awake()
        {
            if (view == null)
                view = GetComponentInChildren<TView>(true);

            UIControllerManager.Instance?.RegisterByType(GetType(), this);
        }

        protected virtual void OnEnable()
        {
            SubscribeControllerEvents();
            OnPanelOpen();
        }

        protected virtual void OnDisable() => UnsubscribeControllerEvents();

        /// <summary>패널이 열릴 때(Open → SetActive(true) → OnEnable) 호출. 파라미터 없는 초기화 로직을 오버라이드한다.</summary>
        protected virtual void OnPanelOpen() { }

        protected virtual void OnDestroy()
        {
            UIControllerManager.Instance?.UnregisterByType(GetType());
        }

        /// <summary>패널 활성화(OnEnable) 시 Manager 이벤트 구독</summary>
        protected virtual void SubscribeControllerEvents() { }

        /// <summary>패널 비활성화(OnDisable) 시 Manager 이벤트 해제</summary>
        protected virtual void UnsubscribeControllerEvents() { }
    }
}
