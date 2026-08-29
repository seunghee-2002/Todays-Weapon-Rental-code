using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "AdventureResultMotionConfig", menuName = "TodaysWeaponRental/Config/AdventureResultMotionConfig")]
    public class AdventureResultMotionConfig : ScriptableObject
    {
        [Serializable]
        public class TrackMarkerEntry
        {
            public DungeonEventType eventType;
            public GameObject prefab;
            public Color color;
        }

        [Serializable]
        public class MotionEntry
        {
            public DungeonEventType eventType;
            public string animationName = "Idle";
            public float duration = 1f;
        }

        [Serializable]
        public class NodeFXConfig
        {
            public DungeonEventType eventType;
            public GameObject particlePrefab;
            public string sfxKey;
            public string successSfxKey;
            public string failSfxKey;
        }

        [Header("Node Motions")]
        public List<MotionEntry> motions = new List<MotionEntry>();

        [Header("Node FX")]
        public List<NodeFXConfig> nodeFXConfigs = new List<NodeFXConfig>();

        [Header("Sequence Timing")]
        public float scrollDuration = 0.4f;
        public float nodeDelay = 0.2f;

        [Header("Transition")]
        public string runAnimationName = "Run";
        [Tooltip("무기 대여 시 달리기 애니메이션 (맨손이면 runAnimationName 사용)")]
        public string runGearAnimationName = "Run_Gear";
        public string entryAnimationName = "Walk";
        public float entryAnimationDuration = 1f;
        public string exitDeathAnimationName = "Die";
        public string exitLeaveAnimationName = "Walk";
        public float exitAnimationDuration = 1.5f;
        public float exitMoveDistance = 800f;

        [Header("Default")]
        public string defaultAnimationName = "Idle";
        public float defaultDuration = 1f;

        [Header("Ranged Weapon (Bow/Crossbow)")]
        [Tooltip("발사 애니 재생 시작 지점(TrackTime) — 화살을 당기기 시작하는 구간. Attack_Bow/Attack_Bolt 기준 0.667")]
        public float rangedDrawStartTime = 0.667f;
        [Tooltip("당김→발사→반동 한 사이클 재생 길이(초, 배속 미적용). Attack_Bow/Attack_Bolt 전체 길이 0.833")]
        public float rangedAttackDuration = 0.833f;

        [Header("Attack Effect Timing")]
        [Tooltip("근접 공격 애니 시작 후 공격 이펙트가 나갈 때까지 지연(초, 배속 미적용). 타격 순간에 맞춤")]
        public float meleeEffectDelay = 0.25f;
        [Tooltip("원거리 발사 애니 시작 후 실제 발사(이펙트)까지 지연(초, 배속 미적용). 당기기 시작~발사 이벤트까지")]
        public float rangedEffectDelay = 0.333f;

        [Header("Combat Fail")]
        public string hitAnimationName = "Hit";
        public float hitAnimationDuration = 0.5f;
        public float retreatDistance = 80f;
        public float retreatDuration = 0.25f;

        [Header("Boss Impact")]
        public float bossImpactDuration = 0.6f;
        public float bossDarkAlpha = 1f;
        public string bossImpactSfxKey = "BossImpact";

        [Header("Boss Retry SFX")]
        public string bossRetrySfxKey = "BossRetry";

        [Header("Protection SFX")]
        public string protectionActivatedSfxKey = "NULL";

        [Header("Reward SFX")]
        public string materialCollectSfxKey = "GetItem";

        [Header("Transition SFX")]
        public string entrySfxKey = "NULL";
        public string runSfxKey = "Run";
        public string exitSuccessSfxKey = "Success";

        [Header("Result Track")]
        public GameObject connectorTrackPrefab;
        public GameObject defaultTrackMarkerPrefab;
        public GameObject startTrackMarkerPrefab;
        public GameObject bossTrackMarkerPrefab;
        public GameObject retreatTrackMarkerPrefab;
        public GameObject returnTrackMarkerPrefab;
        public List<TrackMarkerEntry> trackMarkers = new List<TrackMarkerEntry>();

        public (GameObject prefab, Color color) GetTrackMarkerConfig(DungeonEventType type)
        {
            foreach (var entry in trackMarkers)
                if (entry.eventType == type)
                    return (entry.prefab != null ? entry.prefab : defaultTrackMarkerPrefab, entry.color);
            return (defaultTrackMarkerPrefab, ColorManager.Instance.GetGreenColor());
        }

        public string GetAnimationName(DungeonEventType type)
        {
            foreach (var entry in motions)
                if (entry.eventType == type) return entry.animationName;
            return defaultAnimationName;
        }

        public float GetDuration(DungeonEventType type)
        {
            foreach (var entry in motions)
                if (entry.eventType == type) return entry.duration;
            return defaultDuration;
        }

        public NodeFXConfig GetNodeFX(DungeonEventType type)
        {
            foreach (var fx in nodeFXConfigs)
                if (fx.eventType == type) return fx;
            return null;
        }
    }
}
