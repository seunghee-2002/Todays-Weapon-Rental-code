// Scripts/Data/Config/ReputationConfig.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "ReputationConfig", menuName = "TodaysWeaponRental/Config/ReputationConfig")]
    public class ReputationConfig : ScriptableObject
    {
        [Header("Reputation Thresholds")]
        [Tooltip("Bronze → Silver 임계값")]
        public int silverThreshold = 1000;
        [Tooltip("Silver → Gold 임계값")]
        public int goldThreshold = 3500;
        [Tooltip("Gold → Platinum 임계값")]
        public int platinumThreshold = 15000;
        [Tooltip("Platinum → Diamond 임계값")]
        public int diamondThreshold = 30000;

        [Header("Spawn Interval by Level (게임 시간 초 단위)")]
        [Tooltip("Bronze 등급 스폰 간격")]
        public Vector2 bronzeSpawnInterval = new Vector2(35f, 45f);
        [Tooltip("Silver 등급 스폰 간격")]
        public Vector2 silverSpawnInterval = new Vector2(30f, 40f);
        [Tooltip("Gold 등급 스폰 간격")]
        public Vector2 goldSpawnInterval = new Vector2(25f, 35f);
        [Tooltip("Platinum 등급 스폰 간격")]
        public Vector2 platinumSpawnInterval = new Vector2(20f, 30f);
        [Tooltip("Diamond 등급 스폰 간격")]
        public Vector2 diamondSpawnInterval = new Vector2(15f, 25f);

        [Header("등급 별 이벤트 발생 확률")]
        [Tooltip("Bronze 등급 이벤트 발생 확률")]
        public float bronzeEventChance = 0.05f;
        [Tooltip("Silver 등급 이벤트 발생 확률")]
        public float silverEventChance = 0.10f;
        [Tooltip("Gold 등급 이벤트 발생 확률")]
        public float goldEventChance = 0.10f;
        [Tooltip("Platinum 등급 이벤트 발생 확률")]
        public float platinumEventChance = 0.15f;
        [Tooltip("Diamond 등급 이벤트 발생 확률")]
        public float diamondEventChance = 0.20f;
    }
}