using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 진행 중인 모험 1건을 표시하는 카드. 캐러셀 슬롯에서 재활용된다.
    /// animationArea(현재 이벤트 아이콘) + eventLogText(현재 로그 1줄) + completedOverlay(완료 표시)만 보유.
    /// </summary>
    public class AdventureProgressCard : MonoBehaviour
    {
        [Header("Current Event Display")]
        [SerializeField] private GameObject animationArea;
        [SerializeField] private GameObject bottomArea;
        [SerializeField] private AdventureProgressStage stage;
        [SerializeField] private Image typeIcon;
        [SerializeField] private TextMeshProUGUI eventLogText;

        [Header("Completed Overlay")]
        [SerializeField] private GameObject completedOverlay;   // 완료 표시 전용 (결과 확인은 전령이 담당)

        [Header("Carousel")]
        [SerializeField] private CanvasGroup canvasGroup;

        private AdventureInstance boundAdventure;

        public string InstanceID => boundAdventure?.instanceID;
        public AdventureInstance BoundAdventure => boundAdventure;
        public RectTransform RectTransform => (RectTransform)transform;
        public CanvasGroup CanvasGroup
        {
            get
            {
                if (canvasGroup == null) canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
                return canvasGroup;
            }
        }

        #region 바인딩

        /// <summary>
        /// 카드에 모험을 바인딩하고 표시를 갱신한다.
        /// </summary>
        public void Bind(AdventureInstance adventure)
        {
            boundAdventure = adventure;

            if (adventure == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);

            UpdateCurrentEventDisplay();

            completedOverlay?.SetActive(adventure.isCompleted);

            if (adventure.isCompleted)
                stage?.Clear();
            else
                stage?.Bind(adventure);
        }

        /// <summary>
        /// 빈 슬롯 처리 (인덱스 범위 밖). 비활성화하고 로그를 정리한다.
        /// </summary>
        public void Clear()
        {
            boundAdventure = null;
            stage?.Clear();
            ClearEventLog();
            completedOverlay?.SetActive(false);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 진행 중이던 카드가 완료되었을 때 호출 (완료 오버레이 덮어쓰기).
        /// </summary>
        public void ShowCompleted()
        {
            if (boundAdventure == null) return;
            animationArea?.SetActive(false);
            bottomArea?.SetActive(false);
            SetTypeIcon(null);   // 완료 시 타입 아이콘 숨김
            ClearEventLog();   // 완료 시 성공/실패 로그 텍스트 숨김
            completedOverlay?.SetActive(true);
            stage?.Clear();
        }

        /// <summary>중앙(포커스) 카드만 스테이지 연출을 활성화. (View가 슬롯 배치 시 호출)</summary>
        public void SetStageActive(bool active) => stage?.SetActive(active);

        #endregion

        #region 현재 이벤트 표시

        /// <summary>
        /// animationArea에 현재 진행 중 이벤트의 아이콘과 "진행 중" 로그를 표시.
        /// </summary>
        public void UpdateCurrentEventDisplay()
        {
            if (animationArea == null || boundAdventure == null) return;

            if (boundAdventure.isCompleted)
            {
                animationArea.SetActive(false);
                bottomArea?.SetActive(false);
                SetTypeIcon(null);
                ClearEventLog();
                return;
            }

            animationArea.SetActive(true);
            bottomArea?.SetActive(true);

            var progress = boundAdventure.progress;
            var events = progress.events;

            // 현재 진행 중 이벤트 → 없으면 직전 완료 이벤트(포인터 -1) → 그래도 없으면 첫 이벤트
            AdventureEvent target = progress.CurrentEvent;
            int prevIdx = progress.currentEventIndex - 1;
            if (target == null && prevIdx >= 0 && prevIdx < events.Count)
                target = events[prevIdx];
            if (target == null && events.Count > 0)
                target = events[0];

            SetTypeIcon(target?.eventData);

            // 진행 중인 이벤트에 따른 로그만 표시 (결과 로그는 리플레이가 담당).
            if (target?.eventData != null)
                SetLogText(GetProgressLabel(target.eventData),
                           GetColorForEventType(target.eventData.eventType));
            else
                ClearEventLog();
        }

        /// <summary>현재 이벤트의 타입 배지 아이콘 설정. 표시할 이벤트가 없으면 숨긴다.</summary>
        private void SetTypeIcon(DungeonEventData data)
        {
            if (typeIcon == null) return;

            if (data == null || IconManager.Instance == null)
            {
                typeIcon.gameObject.SetActive(false);
                return;
            }

            typeIcon.sprite = IconManager.Instance.GetIconByEventType(data.eventType);
            typeIcon.gameObject.SetActive(true);
        }

        #endregion

        #region 이벤트 로그 (현재 진행 중 이벤트 1줄만 표시)

        private void SetLogText(string text, Color color)
        {
            if (eventLogText == null) return;
            eventLogText.gameObject.SetActive(true);
            eventLogText.text = text;
            eventLogText.color = color;
        }

        private void ClearEventLog()
        {
            if (eventLogText != null)
            {
                eventLogText.text = string.Empty;
                eventLogText.gameObject.SetActive(false);
            }
        }

        /// <summary>현재 진행 중인 이벤트에 따른 "진행 중" 라벨.</summary>
        private string GetProgressLabel(DungeonEventData eventData)
        {
            return eventData.eventType switch
            {
                DungeonEventType.Battle        => Named("ProgressCard_Battle", eventData.DisplayDescription),
                DungeonEventType.MiniBoss      => Named("ProgressCard_MiniBoss", eventData.DisplayDescription),
                DungeonEventType.Boss          => L("ProgressCard_Boss"),
                DungeonEventType.TreasureChest or DungeonEventType.RareDrop => L("ProgressCard_Treasure"),
                DungeonEventType.Rest          => L("ProgressCard_Rest"),
                DungeonEventType.Protection    => L("ProgressCard_Protection"),
                DungeonEventType.Retry         => L("ProgressCard_Retry"),
                DungeonEventType.Retreat       => L("ProgressCard_Retreat"),
                DungeonEventType.Return        => L("ProgressCard_Return"),
                _ => L("ProgressCard_Moving"),   // Entrance / Trap / TrapEvade 등
            };
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        /// <summary>몬스터 이름({name})은 SO 값이라 <see cref="DungeonEventData.DisplayDescription"/>으로 넘긴다.</summary>
        private static string Named(string key, string name)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "name", name } } });

        private Color GetColorForEventType(DungeonEventType eventType)
        {
            return eventType switch
            {
                DungeonEventType.Entrance => new Color(0.6f, 1f, 0.6f),  // 연한 초록
                DungeonEventType.Battle => new Color(1f, 0.8f, 0.4f),    // 주황
                DungeonEventType.MiniBoss => new Color(1f, 0.5f, 0.2f),  // 진한 주황
                DungeonEventType.Boss => new Color(1f, 0.3f, 0.3f),      // 빨강
                DungeonEventType.TreasureChest or DungeonEventType.RareDrop => new Color(1f, 0.9f, 0.3f), // 황금
                DungeonEventType.Rest => new Color(0.8f, 0.8f, 1f),      // 연한 파랑
                DungeonEventType.Trap => new Color(0.9f, 0.2f, 0.2f),    // 진한 빨강
                DungeonEventType.TrapEvade => new Color(0.4f, 1f, 0.6f), // 연한 초록 (회피 성공)
                DungeonEventType.Retreat => new Color(0.6f, 0.6f, 0.6f), // 회색
                DungeonEventType.Retry => new Color(0.4f, 0.8f, 1f),     // 하늘색
                DungeonEventType.Return => new Color(0.5f, 0.5f, 0.5f),  // 진한 회색
                DungeonEventType.Protection => new Color(0.8f, 0.8f, 1f),// 연한 파랑
                _ => Color.white,
            };
        }

        #endregion
    }
}
