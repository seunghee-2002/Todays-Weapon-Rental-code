using UnityEngine;
using TodaysWeaponRental;   // Log 래퍼 (이 파일은 글로벌 네임스페이스에 있다)

public abstract class BaseData : ScriptableObject
{
    [SerializeField] protected string staticID;
    public string StaticID { get { return staticID; } set { staticID = value; } }
    
#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(staticID)){
            Log.Warn($"StaticID is empty in {name}. Assigning default ID.");
        }
    }
#endif
}