using UnityEngine;

namespace TodaysWeaponRental
{
    public abstract class BaseManager<T> : MonoBehaviour where T : BaseManager<T>
    {
        public static T Instance { get; private set; }
        
        #region 초기화

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = (T)this;
        }
        
        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        #endregion
    }
}