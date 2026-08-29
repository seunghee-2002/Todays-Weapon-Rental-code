using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    public abstract class BaseView : MonoBehaviour
    {
        [SerializeField] protected bool pauseTimeOnOpen = true;
        public bool PauseTimeOnOpen => pauseTimeOnOpen;

        [SerializeField] protected bool hideUIOnOpen = true;
        public bool HideUIOnOpen => hideUIOnOpen;

        // minimal 모드: TopBar만 남기고 나머지 바/디버그 버튼을 숨긴다 (hideUIOnOpen이 false일 때만 의미 있음).
        [SerializeField] protected bool minimalUIOnOpen = false;
        public bool MinimalUIOnOpen => minimalUIOnOpen;

        /// <summary>
        /// 상시 UI(consistenceUI) 노출 정책을 런타임에 변경한다(튜토리얼 단계별 제어용).
        /// UIManager.OpenPanel의 beforeOpen에서 호출해야 그 Open의 UpdateConsistenceUIState에 반영된다.
        /// </summary>
        public void SetUIVisibility(bool hideUI, bool minimalUI)
        {
            hideUIOnOpen = hideUI;
            minimalUIOnOpen = minimalUI;
        }

        // 클릭 가능한 오버레이 버튼. null이면 외부 클릭으로 닫히지 않음.
        [SerializeField] protected Button overlayButton;

        // 기존 호환성 유지용
        protected bool isCanClickOverlay = true;

        [SerializeField] protected bool canEscape = true;
        public bool CanEscape => canEscape;

        /// <summary>ESC로 패널이 닫힐 때 호출 (닫히기 직전)</summary>
        public virtual void OnEscapeClicked() { }

        /// <summary>ESC를 눌렀으나 CanEscape=false라 닫히지 않을 때 호출</summary>
        public virtual void OnEscapeCancelled() { }

        public bool IsOpen => gameObject.activeSelf;

        // 이 패널이 현재 SoundManager에 push한 테마 BGM 키. null이면 테마 없음(배경음 유지).
        private string activeThemeKey;

        /// <summary>
        /// 이 패널이 열릴 때 재생할 테마 BGM 키. null이면 배경음을 유지한다.
        /// 정적 테마는 상수를, 랜덤 테마는 매 호출 랜덤 키를 반환하도록 오버라이드한다.
        /// 던전 등급 등 Open 이후에야 결정되는 테마는 SetActiveThemeBgm을 대신 사용한다.
        /// </summary>
        protected virtual string GetThemeBgmKey() => null;

        /// <summary>
        /// 런타임에 결정되는 테마 BGM을 Open 이후 시점에 진입시킨다(리플레이 등급 등).
        /// Close 시 자동으로 ExitTheme된다. GetThemeBgmKey와 중복 사용하지 않는다.
        /// </summary>
        protected void SetActiveThemeBgm(string key)
        {
            if (string.IsNullOrEmpty(key) || activeThemeKey != null) return;
            activeThemeKey = key;
            SoundManager.Instance?.EnterTheme(key);
        }

        protected virtual void Awake()
        {
            gameObject.SetActive(false);
        }

        public virtual void Open()
        {
            gameObject.SetActive(true);
            if (overlayButton != null)
            {
                // 이미 열린 패널이 재오픈(OpenPanel 재호출)될 때 리스너가 중복 등록되지 않도록
                // 제거 후 등록한다. RemoveAllListeners는 인스펙터 연결까지 지우므로 쓰지 않는다
                overlayButton.onClick.RemoveListener(OnOverlayClicked);
                overlayButton.onClick.AddListener(OnOverlayClicked);
            }
            EventSystem.current?.SetSelectedGameObject(null);
            SetActiveThemeBgm(GetThemeBgmKey());
        }

        public virtual void Close()
        {
            overlayButton?.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
            if (activeThemeKey != null)
            {
                SoundManager.Instance?.ExitTheme(activeThemeKey);
                activeThemeKey = null;
            }
        }

        private void OnOverlayClicked()
        {
            UIManager.Instance?.CloseTopPanel();
        }

        protected virtual void OnEnable()  => SubscribeEvents();
        protected virtual void OnDisable() => UnsubscribeEvents();

        protected abstract void SubscribeEvents();
        protected abstract void UnsubscribeEvents();
    }
}
