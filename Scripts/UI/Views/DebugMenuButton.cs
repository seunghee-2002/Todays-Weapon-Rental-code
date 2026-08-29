using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 디버그 메뉴의 버튼 슬롯. 라벨과 클릭 액션을 코드(DebugMenuView)에서 주입받는다.
    /// 프리팹으로 만들어 동적 생성·재사용한다.
    /// </summary>
    public class DebugMenuButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;

        /// <summary>라벨과 클릭 액션을 주입한다. 기존 리스너는 교체된다.</summary>
        public void Bind(string text, Action action)
        {
            if (label != null) label.text = text;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action?.Invoke());
            }
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
