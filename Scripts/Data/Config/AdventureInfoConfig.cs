// Scripts/Data/Config/AdventureInfoConfig.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TodaysWeaponRental
{
    public enum BonusType
    {
        Affection,
        Charm,
        Trait,
        DungeonArmor,
        WeaponCondition,
        Collection,
        Seer,
        WeaponAdventurerMatch,   // deprecated — chip 생성 제거됨. enum 값은 asset 호환성 위해 유지
        DungeonGrade,            // 신규: 던전 등급 baseline chip
    }

    public enum MoodBand { VeryDark, Dark, Neutral, Bright, VeryBright }
    public enum VarianceState { None, Positive, Negative, Mixed }

    [Serializable]
    public class BonusVisualInfo
    {
        public BonusType bonusType;
        public string displayName;
        public Sprite icon;
        public Color positiveColor = Color.cyan;
        public Color negativeColor = Color.red;
        [Tooltip("이 보너스의 최대 절댓값(0~1 비율). 칩의 1칸 = maxAbsValue / 전역 segmentCount")]
        public float maxAbsValue = 0.15f;
    }

    [Serializable]
    public class MoodLabelEntry
    {
        public MoodBand band;
        public VarianceState state;
        [Tooltip("모험 시작 버튼 배경 색")]
        public Color color = Color.white;
        [Tooltip("모험 시작 버튼 텍스트 색")]
        public Color textColor = Color.black;
    }

    public struct AdventureInfoCardData
    {
        public BonusType type;
        public float value;
        public bool isConfirmed;
        public bool isMultiplier;   // true면 곱연산 보정 → 툴팁을 x1.4배 형태로 표시 (상성·부적·곱연산 특성)
    }

    [CreateAssetMenu(fileName = "AdventureInfoConfig", menuName = "TodaysWeaponRental/Config/AdventureInfoConfig")]
    public class AdventureInfoConfig : ScriptableObject
    {
        [Header("Chip 시각")]
        public List<BonusVisualInfo> bonusVisuals;

        [Header("전역 칩 설정")]
        [Tooltip("모든 chip 의 segment 칸 수 (고정)")]
        public int segmentCount = 5;

        [Header("DungeonGrade chip")]
        [Tooltip("칸당 value (Common=+5칸, Legendary=-5칸 패턴)")]
        public float dungeonGradeUnit = 0.03f;
        [Tooltip("등급별 칸 패턴 (Common, Uncommon, Rare, Epic, Legendary)")]
        public int[] dungeonGradeSegments = { 5, 2, -1, -3, -5 };

        [Header("Mood Label - Band 임계")]
        public float bandThresholdVeryDark   = -0.15f;
        public float bandThresholdDark       = -0.05f;
        public float bandThresholdBright     = +0.05f;
        public float bandThresholdVeryBright = +0.15f;

        [Header("Mood Label - Variance 임계")]
        [Tooltip("total_magnitude 대비 T_low 비율")]
        public float varianceLowRatio = 0.10f;
        [Tooltip("T_low 의 절대 하한")]
        public float varianceLowFloor = 0.02f;
        [Tooltip("한쪽 우세 판정 임계 (V_plus/V 또는 V_minus/V)")]
        public float varianceLeanThreshold = 0.7f;

        [Header("Mood Label - 라벨 사전 (5 × 4 = 20)")]
        public List<MoodLabelEntry> moodLabels;

        [Header("모험 시작 버튼 - 비활성(시작 불가) 색")]
        public Color startButtonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public Color startButtonDisabledTextColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        public BonusVisualInfo GetBonusInfo(BonusType type) =>
            bonusVisuals?.FirstOrDefault(b => b.bonusType == type);

        public MoodLabelEntry GetMoodLabel(MoodBand band, VarianceState state) =>
            moodLabels?.FirstOrDefault(m => m.band == band && m.state == state);
    }
}
