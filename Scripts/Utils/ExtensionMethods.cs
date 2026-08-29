using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 유틸리티 확장 메서드
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// GetComponent를 시도하고, 없으면 새로 추가 (AddComponent)
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null)
            {
                Log.Error("ExtensionMethods.GetOrAddComponent: GameObject is null");
                return null;
            }

            T component = gameObject.GetComponent<T>();
            
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
                Log.Warn($"[{gameObject.name}] {typeof(T).Name} component was missing, added automatically");
            }

            return component;
        }

        /// <summary>
        /// GetComponent를 시도하고, 없으면 null 반환 (명시적)
        /// </summary>
        public static T GetComponentOrNull<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null)
            {
                Log.Error("ExtensionMethods.GetComponentOrNull: GameObject is null");
                return null;
            }

            return gameObject.GetComponent<T>();
        }
    }
}
