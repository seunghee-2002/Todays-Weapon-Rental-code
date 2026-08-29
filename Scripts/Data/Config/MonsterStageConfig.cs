using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 몬스터 렌더 스테이지(MonsterStageRenderer)의 타입별 프레이밍 설정.
    /// 카테고리(MonsterCategory)마다 카메라 orthographic 크기와 spawnPoint 기준 local 오프셋을 지정해
    /// 프리팹 고유 크기 차이를 통일한다. 진행 카드/리플레이 두 스테이지를 분리해서 보유한다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterStageConfig", menuName = "TodaysWeaponRental/Config/MonsterStageConfig")]
    public class MonsterStageConfig : ScriptableObject
    {
        [Serializable]
        public class CategoryPreset
        {
            public MonsterCategory category;
            [Tooltip("이 카테고리 몬스터를 렌더할 때 카메라 orthographic 크기. 0 이하면 카메라 기본값 유지.")]
            public float orthographicSize;
            [Tooltip("spawnPoint를 기본 위치에서 이만큼(x, y) 이동시켜 스폰한다.")]
            public Vector2 spawnLocalOffset;
        }

        [Header("진행 카드 스테이지 (Instance)")]
        public List<CategoryPreset> progressPresets = new List<CategoryPreset>();
        [Header("리플레이 스테이지 (ReplayInstance)")]
        public List<CategoryPreset> replayPresets = new List<CategoryPreset>();

        /// <summary>stage(replay/progress)와 카테고리로 프리셋을 찾는다. 없으면 false(카메라 기본 동작 유지).</summary>
        public bool TryGetPreset(bool replay, MonsterCategory category, out CategoryPreset preset)
        {
            var list = replay ? replayPresets : progressPresets;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].category == category) { preset = list[i]; return true; }
            }
            preset = null;
            return false;
        }
    }
}
