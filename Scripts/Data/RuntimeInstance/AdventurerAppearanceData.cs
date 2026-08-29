using System;
using System.Collections.Generic;
using UnityEngine;
using LayerLab.ArtMaker;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험가 외형 데이터 (런타임 저장용)
    /// Dictionary는 직렬화 불가이므로 List 기반 직렬화 구조 사용
    /// </summary>
    [Serializable]
    public class AdventurerAppearanceData
    {
        public List<PartsIndexEntry> partsIndices = new();
        public Color skinColor = new Color(1f, 0.8f, 0.7f);
        public Color hairColor = new Color(0.5f, 0.3f, 0.1f);
        public Color beardColor = new Color(0.5f, 0.3f, 0.1f);
        public Color browColor = new Color(0.4f, 0.25f, 0.08f);

        public AdventurerAppearanceData(List<PartsIndexEntry> partsIndices = null, Color skinColor = default, Color hairColor = default, Color beardColor = default, Color browColor = default)
        {
            this.skinColor = skinColor == default ? new Color(1f, 0.8f, 0.7f) : skinColor;
            this.hairColor = hairColor == default ? new Color(0.5f, 0.3f, 0.1f) : hairColor;
            this.beardColor = beardColor == default ? new Color(0.5f, 0.3f, 0.1f) : beardColor;
            this.browColor = browColor == default ? new Color(0.4f, 0.25f, 0.08f) : browColor;
            {
                if (partsIndices != null)
                    this.partsIndices = partsIndices;
            }
        }

        #region 변환 메서드

        public Dictionary<PartsType, int> ToPartsDictionary()
        {
            var dict = new Dictionary<PartsType, int>();
            foreach (var entry in partsIndices)
                dict[entry.partsType] = entry.index;
            return dict;
        }

        public static AdventurerAppearanceData FromPartsDictionary(
            Dictionary<PartsType, int> dict,
            Color skinColor,
            Color hairColor,
            Color beardColor,
            Color browColor)
        {
            var data = new AdventurerAppearanceData
            {
                skinColor = skinColor,
                hairColor = hairColor,
                beardColor = beardColor,
                browColor = browColor
            };

            foreach (var kvp in dict)
                data.partsIndices.Add(new PartsIndexEntry(kvp.Key, kvp.Value));

            return data;
        }

        #endregion
    }

    [Serializable]
    public class PartsIndexEntry
    {
        public PartsType partsType;
        public int index;

        public PartsIndexEntry() { }

        public PartsIndexEntry(PartsType partsType, int index)
        {
            this.partsType = partsType;
            this.index = index;
        }
    }
}