// Scripts/UI/ListItems/QuestBoardDungeonSlot.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 의뢰판 던전 슬롯
    /// - 슬롯 전체 클릭: 던전 상세 팝업 오픈
    /// - 선택 버튼: 선택/해제 토글
    /// </summary>
    public class QuestBoardDungeonSlot : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image dungeonIcon;
        [SerializeField] private TextMeshProUGUI dungeonNameText;

        [Header("Buttons")]
        [SerializeField] private Button infoButton;    // 상세 팝업 오픈 버튼
        [SerializeField] private Button selectButton;  // 선택/해제 버튼

        [Header("State Indicators")]
        [SerializeField] private GameObject selectedIndicator;   // 선택 테두리
        [SerializeField] private GameObject disabledOverlay;     // 확정 후 비활성 오버레이
        [SerializeField] private GameObject ongoingIndicator;    // 수색 진행 중 표시
        [SerializeField] private GameObject highlightIndicator; // 2배 던전 표시

        [Header("Scout Duration (Phase 2)")]
        [SerializeField] private GameObject durationGroup;             // 수색 시간 표시 그룹
        [SerializeField] private TextMeshProUGUI durationText;         // "30분"

        private DungeonData dungeonData;
        private Action onInfoClickedCallback;   // 팝업 오픈 요청
        private Action onSelectClickedCallback; // 선택/해제 요청
        private bool isSelected = false;

        #region 초기화

        public void Initialize(DungeonData data, Action onInfoClicked, Action onSelectClicked)
        {
            dungeonData             = data;
            onInfoClickedCallback   = onInfoClicked;
            onSelectClickedCallback = onSelectClicked;

            UpdateUI();
            SetSelected(false);
            SetDisabled(false);
            SetHighlight(false);

            infoButton?.onClick.AddListener(OnInfoClicked);
            selectButton?.onClick.AddListener(OnSelectClicked);
        }

        private void OnDestroy()
        {
            infoButton?.onClick.RemoveAllListeners();
            selectButton?.onClick.RemoveAllListeners();
        }

        private void UpdateUI()
        {
            if (dungeonData == null) return;

            if (cardBackground != null)
                cardBackground.color = ColorManager.Instance.GetGradeCardBackgroundColor(dungeonData.grade);
            if (dungeonIcon != null)
                dungeonIcon.sprite = dungeonData.dungeonIcon;
            if (dungeonNameText != null)
                dungeonNameText.text = dungeonData.DisplayName;
        }

        #endregion

        #region 상태 전환

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            selectedIndicator?.SetActive(selected);
        }

        /// <summary>
        /// 확정 후 선택되지 않은 슬롯 비활성화
        /// </summary>
        public void SetDisabled(bool disabled)
        {
            disabledOverlay?.SetActive(disabled);
            if (infoButton != null)   infoButton.interactable   = !disabled;
            if (selectButton != null) selectButton.interactable = !disabled;
        }

        /// <summary>
        /// 2배 던전 표시 on/off
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            if (highlightIndicator == null) return;

            highlightIndicator.SetActive(highlight);
            if (highlight)
                selectedIndicator?.SetActive(false);
        }

        public void SetOngoing(bool ongoing)
        {
            ongoingIndicator?.SetActive(ongoing);
        }

        /// <summary>
        /// Phase 2 수색 시간 표시. minutes &lt;= 0이면 그룹을 숨긴다.
        /// </summary>
        public void SetDuration(int minutes)
        {
            bool show = minutes > 0;
            if (durationGroup != null) durationGroup.SetActive(show);
            if (show && durationText != null)
                durationText.text = UITranslator.FormatDuration(minutes);
        }

        public void SetSelectInteractable(bool interactable)
        {
            if (selectButton != null) selectButton.interactable = interactable;
        }

        /// <summary>
        /// 수색 단계 진입 시 선택 버튼 콜백을 교체한다.
        /// </summary>
        public void RebindSelectAction(Action onSelectClicked)
        {
            onSelectClickedCallback = onSelectClicked;
        }

        public DungeonData DungeonData => dungeonData;

        #endregion

        #region 애니메이션

        /// <summary>
        /// 슬롯 등장 애니메이션 (Scale 0 → 1, OutBack)
        /// </summary>
        public void PlayAppearAnimation()
        {
            transform.DOKill();
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>
        /// 등장 애니메이션 스킵 시 최종 상태로 즉시 전환
        /// </summary>
        public void SnapToAppearedState()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 2배 던전 강조 애니메이션 (PunchScale + 진동)
        /// </summary>
        public void PlayHighlightAnimation()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
            transform.DOShakePosition(0.4f, 8f, 15, 90f, false, true).SetUpdate(true).SetLink(gameObject);
        }

        #endregion

        #region 버튼 이벤트

        private void OnInfoClicked()
        {
            onInfoClickedCallback?.Invoke();
        }

        private void OnSelectClicked()
        {
            onSelectClickedCallback?.Invoke();
        }

        #endregion
    }
}