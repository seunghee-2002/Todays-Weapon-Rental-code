using UnityEngine;
using TMPro;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 현재 보유 골드와 유산 포인트를 표시하는 패널.
    /// UIPopupController.ShowPlayerResourceBar()/HidePlayerResourceBar()로 Instantiate/Destroy 방식 관리한다.
    /// </summary>
    public class PlayerResourceBar : BaseView
    {
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI legacyText;

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = false;
            hideUIOnOpen = false;
            canEscape = false;
        }

        protected override void SubscribeEvents()
        {
            RefreshAll();
            if (EconomyManager.Instance != null)
                EconomyManager.Instance.OnGoldChanged += OnGoldChanged;
            if (LegacyManager.Instance != null)
                LegacyManager.Instance.OnLegacyPointsChanged += OnLegacyChanged;
        }

        protected override void UnsubscribeEvents()
        {
            if (EconomyManager.Instance != null)
                EconomyManager.Instance.OnGoldChanged -= OnGoldChanged;
            if (LegacyManager.Instance != null)
                LegacyManager.Instance.OnLegacyPointsChanged -= OnLegacyChanged;
        }

        private void RefreshAll()
        {
            if (EconomyManager.Instance != null) SetGold(EconomyManager.Instance.CurrentGold);
            if (LegacyManager.Instance != null) SetLegacy(LegacyManager.Instance.LegacyPoints);
        }

        private void OnGoldChanged(int newGold)
        {
            SetGold(newGold);
            Punch(goldText?.transform);
        }

        private void OnLegacyChanged(int newLegacy)
        {
            SetLegacy(newLegacy);
            Punch(legacyText?.transform);
        }

        /// <summary>
        /// 펀치 스케일 재생. DOPunchScale은 시작 시점의 스케일을 복귀 기준값으로 캡처하므로,
        /// 재생 중에 다시 호출되면(일괄분해처럼 골드 이벤트가 한 프레임에 여러 번) 부푼 중간값이
        /// 기준값이 되어 크기가 누적된 채 고정된다. 진행 중 펀치를 끊고 원래 크기로 되돌린 뒤 새로 건다.
        /// </summary>
        private void Punch(Transform target)
        {
            if (target == null) return;
            target.DOKill();
            target.localScale = Vector3.one;
            target.DOPunchScale(Vector3.one * 0.2f, 0.2f, 3, 0.3f).SetLink(gameObject);
        }

        private void SetGold(int value)
        {
            if (goldText != null) goldText.text = $"{value:N0}";
        }

        private void SetLegacy(int value)
        {
            if (legacyText != null) legacyText.text = $"{value:N0}";
        }
    }
}
