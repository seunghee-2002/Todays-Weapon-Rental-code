using System.Collections.Generic;
using UnityEngine;
using System;

namespace TodaysWeaponRental{
    // Unity 직렬화 가능한 Dictionary
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();
        
        [SerializeField]
        private List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            this.Clear();

            if (keys.Count != values.Count)
            {
                Log.Error($"SerializableDictionary: keys count ({keys.Count}) != values count ({values.Count})");
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                if (ContainsKey(keys[i]))
                {
                    Log.Warn($"[SerializableDictionary] 중복 키 무시: '{keys[i]}' (index {i})");
                    continue;
                }
                this.Add(keys[i], values[i]);
            }
        }
    }
}
