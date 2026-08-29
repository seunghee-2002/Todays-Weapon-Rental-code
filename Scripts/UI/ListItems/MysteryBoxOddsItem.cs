using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>수수께끼 상자 확률 안내 1행의 데이터 (보상 종류 + 당첨 확률).</summary>
    public readonly struct MysteryBoxOdds
    {
        public readonly string label;
        public readonly float  chance;   // 0~1

        public MysteryBoxOdds(string label, float chance)
        {
            this.label  = label;
            this.chance = chance;
        }
    }

    /// <summary>수수께끼 상자 확률 안내 패널의 1행. 보상 종류와 확률(%)을 표시한다.</summary>
    public class MysteryBoxOddsItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI chanceText;

        public void Initialize(MysteryBoxOdds odds)
        {
            if (labelText != null)  labelText.text  = odds.label;
            if (chanceText != null) chanceText.text = $"{odds.chance * 100f:0.#}%";
        }

        /// <summary>컨테이너의 기존 자식을 비우고 oddsList로 다시 채운다.</summary>
        public static void Rebuild(Transform container, MysteryBoxOddsItem prefab, IReadOnlyList<MysteryBoxOdds> oddsList)
        {
            if (container == null || prefab == null) return;

            // SetParent(null)로 즉시 분리: 지연 Destroy가 같은 프레임 레이아웃에 끼어드는 것을 막는다.
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            if (oddsList == null) return;

            foreach (var odds in oddsList)
                Instantiate(prefab, container).Initialize(odds);
        }
    }
}
