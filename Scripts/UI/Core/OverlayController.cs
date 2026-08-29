using System;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// popupParent에 띄우는 풀스크린 투명 클릭 차단 오버레이.
    /// 클릭 시 등록된 콜백을 실행한다. 자기 자신의 Destroy 책임은 호출자에게 있다.
    /// </summary>
    public class OverlayController : MonoBehaviour
    {
        [SerializeField] private Button button;

        private Action onClick;

        public void Initialize(Action onClick)
        {
            this.onClick = onClick;
            button?.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            button?.onClick.RemoveAllListeners();
            var cb = onClick;
            onClick = null;
            cb?.Invoke();
        }
    }
}
