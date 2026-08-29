using UnityEngine;
using UnityEngine.UI;
using System;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 마법 재부여 탭에서 사용하는 부가효과 아이템.
    /// WeaponEffectListItem 위에 잠금 버튼 기능 추가.
    /// </summary>
    public class RerollEffectItem : MonoBehaviour
    {
        [SerializeField] private WeaponEffectListItem effectListItem;
        [SerializeField] private Button lockButton;
        [SerializeField] private Image lockIcon;  // 잠금/해제 아이콘 교체용

        private WeaponEffect targetEffect;

        public void Initialize(WeaponEffect effect, bool isLocked, Action onLockToggled)
        {
            targetEffect = effect;
            effectListItem?.Initialize(effect);
            UpdateLockUI(isLocked);

            lockButton?.onClick.RemoveAllListeners();
            lockButton?.onClick.AddListener(() => {
                onLockToggled?.Invoke();
                // Controller 상태가 바뀐 뒤 현재 아이콘 갱신은 Controller가 반환값 없이 토글만 하므로
                // 로컬에서 반전 표시 (실제 상태는 Controller가 보유)
                isLocked = !isLocked;
                UpdateLockUI(isLocked);
            });
        }

        private void UpdateLockUI(bool isLocked)
        {
            if (lockIcon != null)
                lockIcon.sprite = IconManager.Instance.GetLockIcon(isLocked);
            effectListItem?.SetDimmed(isLocked);
        }
    }
}