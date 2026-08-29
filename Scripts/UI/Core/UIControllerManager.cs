using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// UI Controller 동적 레지스트리.
    /// 각 BaseController가 Awake에서 자동으로 등록하며, OnDestroy에서 해제한다.
    /// DontDestroyOnLoad로 씬 전환에도 유지된다.
    /// </summary>
    public class UIControllerManager : BaseManager<UIControllerManager>
    {
        private readonly Dictionary<Type, object> controllers = new Dictionary<Type, object>();

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
                DontDestroyOnLoad(gameObject);
        }

        #region 레지스트리

        public void RegisterByType(Type type, object controller)
        {
            controllers[type] = controller;
        }

        public void UnregisterByType(Type type)
        {
            controllers.Remove(type);
        }

        public T GetController<T>() where T : class
        {
            if (controllers.TryGetValue(typeof(T), out var ctrl))
                return ctrl as T;

            Log.Warn($"[UIControllerManager] '{typeof(T).Name}'을(를) 찾을 수 없습니다. 아직 패널이 열리지 않았을 수 있습니다.");
            return null;
        }

        /// <summary>씬 전환 시 호출하여 레지스트리를 초기화한다.</summary>
        public void ClearRegistry()
        {
            controllers.Clear();
        }

        #endregion
    }
}
