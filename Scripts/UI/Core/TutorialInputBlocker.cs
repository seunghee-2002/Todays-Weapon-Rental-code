using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 튜토리얼 입력 게이트. 관련 GraphicRaycaster 참조를 이 컴포넌트 한 곳에 모아 관리한다.
    ///
    /// 1) 배경 차단(폴링): 진행 중 "빈틈"(TutorialDialogueView·TutorialHighlightView가 모두 꺼진 전환 순간)에
    ///    backgroundRaycasters(VisitorParent=NPC, ConsistenceCanvas=상시 UI)를 끈다. 연타가 NPC·사이드바를 눌러
    ///    의도치 않은 패널이 열리는 것을 막는다.
    /// 2) 패널 차단(요청형): TutorialManager가 대화 대기 등 짧은 전환 구간에 SetPanelInputBlocked로 panelRaycaster를 잠시 끈다.
    ///    방금 열린 패널(예: 모험 준비)의 버튼이 대화/하이라이트가 뜨기 전에 연타로 눌리는 것을 막는다.
    ///
    /// PopupCanvas(팝업·토스트)와 결과창은 건드리지 않는다 — 하이라이트 없이 닫기/확인을 눌러 진행하는 구간이 많기 때문.
    /// 빈틈이 아닐 때(대화창·하이라이트가 열려 있을 때)는 각 뷰의 자체 blocker가 입력을 처리한다.
    /// </summary>
    public class TutorialInputBlocker : MonoBehaviour
    {
        [Tooltip("빈틈에서 차단할 raycaster. VisitorParent(NPC), ConsistenceCanvas(상시 UI)를 할당.")]
        [SerializeField] private GraphicRaycaster[] backgroundRaycasters;

        [Tooltip("짧은 전환 구간에 잠시 막을 PanelCanvas raycaster. TutorialManager가 제어한다.")]
        [SerializeField] private GraphicRaycaster panelRaycaster;

        // 정상 전환의 빈틈은 1~2프레임이다. 이보다 오래 지속되면 튜토리얼이 조용히 실패한 것으로 보고
        // 차단을 자동 해제한다(입력이 영구 잠기는 것을 막는 안전장치).
        private const float MAX_GAP_SECONDS = 3f;

        // 현재 적용된 차단 상태. 상태가 바뀔 때만 raycaster에 써서 불필요한 쓰기를 줄인다.
        private bool backgroundBlocked;
        private bool warnedPanelRaycasterMissing;

        private float gapElapsed;
        private bool gapTimedOut;

        private void Update()
        {
            if (!IsTutorialGap())
            {
                gapElapsed = 0f;
                gapTimedOut = false;
                ApplyBackground(false);
                return;
            }

            if (!gapTimedOut)
            {
                gapElapsed += Time.unscaledDeltaTime;
                if (gapElapsed >= MAX_GAP_SECONDS)
                {
                    gapTimedOut = true;
                    Log.Warn($"[TutorialInputBlocker] 대화창·하이라이트가 모두 꺼진 상태가 {MAX_GAP_SECONDS}초 넘게 지속되어 " +
                             "배경 입력 차단을 자동 해제합니다. 튜토리얼 진행이 멈췄을 수 있습니다.");
                }
            }

            ApplyBackground(!gapTimedOut);
        }

        private void OnDisable()
        {
            // 씬 전환·컴포넌트 비활성 시 차단 상태로 남지 않도록 raycaster를 복구한다.
            gapElapsed = 0f;
            gapTimedOut = false;
            ApplyBackground(false);
            if (panelRaycaster != null) panelRaycaster.enabled = true;
        }

        /// <summary>
        /// 대화 대기 등 짧은 전환 구간에 PanelCanvas 입력(GraphicRaycaster)을 막거나 푼다. TutorialManager가 호출.
        /// </summary>
        public void SetPanelInputBlocked(bool block)
        {
            if (panelRaycaster != null)
            {
                panelRaycaster.enabled = !block;
            }
            else if (block && !warnedPanelRaycasterMissing)
            {
                warnedPanelRaycasterMissing = true;
                Log.Warn("[TutorialInputBlocker] panelRaycaster 미연결 - 준비/전환 구간 연타 차단이 동작하지 않습니다. " +
                                 "인스펙터에서 panelRaycaster에 PanelCanvas의 GraphicRaycaster를 연결하세요.");
            }
        }

        /// <summary>튜토리얼 진행 중이며 대화창·하이라이트가 모두 꺼진 전환 순간(빈틈)인지.</summary>
        private bool IsTutorialGap()
        {
            var tutorial = TutorialManager.Instance;
            if (tutorial == null || !tutorial.IsTutorialActive) return false;

            return !IsPanelOpen<TutorialDialogueView>() && !IsPanelOpen<TutorialHighlightView>();
        }

        private bool IsPanelOpen<T>() where T : BaseView
        {
            return UIManager.Instance?.GetPanel<T>()?.IsOpen ?? false;
        }

        private void ApplyBackground(bool block)
        {
            if (backgroundBlocked == block) return;
            backgroundBlocked = block;

            if (backgroundRaycasters == null) return;
            foreach (var raycaster in backgroundRaycasters)
                if (raycaster != null) raycaster.enabled = !block;
        }
    }
}
