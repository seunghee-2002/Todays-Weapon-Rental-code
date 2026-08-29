using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 팝업/토스트의 성격. 성격별로 다른 효과음을 재생한다.
    /// </summary>
    public enum PopupSfxType
    {
        Warning,  // 할 수 없음 / 조건 미충족 - 거부 피드백
        Confirm,  // 예/아니오를 묻는 확인 팝업
        Notify,   // 결과 보고 / 정보 안내
    }

    /// <summary>
    /// 팝업(ConfirmPopup, LegacyPurchasePopup)과 Toast를 Instantiate/Destroy 방식으로 관리한다.
    /// UIPopupManager를 대체하는 DontDestroyOnLoad 싱글톤.
    /// </summary>
    public class UIPopupController : MonoBehaviour
    {
        public static UIPopupController Instance { get; private set; }

        [Header("팝업 프리팹")]
        [SerializeField] private GameObject confirmPopupPrefab;
        [SerializeField] private GameObject legacyPurchasePopupPrefab;
        [SerializeField] private GameObject overlayPrefab;
        private Transform popupParent;

        // ESC(뒤로가기)가 UIManager 패널 스택 밖에 있는 이 팝업들도 처리할 수 있도록 생성 순서대로 추적한다.
        private class PopupHandle
        {
            public GameObject Obj;
            public Action OnEscape;
        }
        private readonly List<PopupHandle> activePopups = new List<PopupHandle>();

        /// <summary>
        /// 열려있는 팝업이 있으면 가장 최근 팝업을 취소 경로로 닫고 true를 반환한다.
        /// UIManager.Update()가 ESC 처리 시 UIManager 패널보다 우선 호출한다.
        /// </summary>
        public bool HandleEscape()
        {
            if (activePopups.Count == 0) return false;
            activePopups[^1].OnEscape?.Invoke();
            return true;
        }

        [Header("PlayerResourceBar")]
        [SerializeField] private GameObject playerResourceBarPrefab;
        private GameObject playerResourceBarInstance;

        [Header("Toast")]
        [SerializeField] private GameObject toastPrefab;
        private Transform toastContainer;

        // 같은 메시지의 토스트가 겹쳐 쌓이지 않도록 현재 살아있는 토스트를 추적한다.
        private readonly List<(string message, GameObject obj)> activeToasts = new List<(string, GameObject)>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetPopupParent(Transform parent) => popupParent = parent;
        public void SetToastContainer(Transform container) => toastContainer = container;

        #region 팝업

        // 팝업/토스트 성격별 효과음 키
        private static string SfxKeyOf(PopupSfxType type) => type switch
        {
            PopupSfxType.Warning => "PopupWarning",
            PopupSfxType.Notify  => "PopupNotify",
            _                    => "PopupConfirm",
        };

        /// <summary>
        /// 확인/취소 팝업 표시.
        /// onCancel이 null이면 확인 버튼만 표시.
        /// </summary>
        /// <param name="confirmLabel">확인 버튼 텍스트. null이면 프리팹의 기본 라벨을 유지한다.</param>
        /// <param name="dismissable">false면 ESC(뒤로가기)로 닫히지 않는다. 강제 업데이트처럼 반드시 응답해야 하는 팝업용.</param>
        /// <returns>이 팝업을 콜백 없이 닫는 함수. 표시에 실패하면 null. 대부분의 호출부는 무시하면 된다.</returns>
        public Action ShowPopup(string message, Action onConfirm = null, Action onCancel = null,
                                PopupSfxType type = PopupSfxType.Confirm,
                                string confirmLabel = null, bool dismissable = true)
        {
            if (confirmPopupPrefab == null)
            {
                Log.Error("[UIPopupController] confirmPopupPrefab이 할당되지 않았습니다.");
                return null;
            }

            var obj = Instantiate(confirmPopupPrefab, popupParent);
            var view = obj.GetComponentInChildren<ConfirmPopupView>(true);
            if (view == null)
            {
                Log.Error("[UIPopupController] ConfirmPopupView를 찾을 수 없습니다.");
                Destroy(obj);
                return null;
            }

            var handle = new PopupHandle { Obj = obj };
            Action confirmWrapped = () =>
            {
                activePopups.Remove(handle);
                StartCoroutine(InvokeNextFrameThenDestroy(obj, onConfirm));
            };
            Action cancelWrapped = onCancel != null
                ? () =>
                  {
                      activePopups.Remove(handle);
                      StartCoroutine(InvokeNextFrameThenDestroy(obj, onCancel));
                  }
                : (Action)null;
            // 취소 버튼이 없는(확인 전용) 팝업은 ESC로 부작용 없이 닫히기만 한다.
            Action dismissWrapped = () =>
            {
                activePopups.Remove(handle);
                StartCoroutine(InvokeNextFrameThenDestroy(obj, null));
            };
            // dismissable=false면 OnEscape를 비워, ESC를 소비하되(뒤 패널로 새지 않도록) 닫지는 않는다.
            handle.OnEscape = dismissable ? (cancelWrapped ?? dismissWrapped) : null;

            view.Show(message, onConfirm: confirmWrapped, onCancel: cancelWrapped, confirmLabel: confirmLabel);
            view.Open();
            activePopups.Add(handle);
            SoundManager.Instance?.PlaySFX(SfxKeyOf(type));

            // 외부에서 팝업을 거둬들여야 할 때 쓴다(예: 강제 업데이트 재검사 결과 차단이 풀린 경우).
            // 직접 Destroy하면 activePopups에 죽은 핸들이 남아 ESC가 먹히지 않으므로 이 경로를 쓸 것.
            return dismissWrapped;
        }

        /// <summary>
        /// "이 OO를 돌려보내시겠습니까?" - 모험 준비 화면, 대화 화면, 대장장이 ESC 취소가
        /// 같은 문구를 쓰므로 방문자 종류 라벨만 받아 여기서 한 번에 조립한다.
        /// </summary>
        public static string SendBackConfirmMessage(string typeLabel)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Messages", "AdventureDialogue_SendBackConfirm",
                   arguments: new object[] { new Dictionary<string, object>
                   {
                       { "type", typeLabel },
                       { "particle", UITranslator.ObjectParticle(typeLabel) }
                   } });

        /// <summary>
        /// 아침 이벤트 전용 - 위와 달리 {type}에 사람이 아니라 이벤트 이름("수수께끼의 상자")이
        /// 들어오므로 뒤에 NPC를 붙인 별도 문구를 쓴다. 조사도 항상 "를"로 고정된다.
        /// </summary>
        public static string SendBackEventConfirmMessage(string eventName)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Messages", "MorningEvent_SendBackConfirm",
                   arguments: new object[] { new Dictionary<string, object>
                   {
                       { "type", eventName }
                   } });

        /// <summary>
        /// 골드 부족 시 유산으로 골드 구매 팝업 표시.
        /// shortage를 커버하는 최소 유산 수량을 자동 산출.
        /// </summary>
        public void ShowLegacyGoldPurchase(int shortage, Action onConfirm = null, Action onCancel = null)
        {
            if (legacyPurchasePopupPrefab == null)
            {
                Log.Error("[UIPopupController] legacyPurchasePopupPrefab이 할당되지 않았습니다.");
                onCancel?.Invoke();
                return;
            }

            int legacyCost = LegacyManager.Instance.GetLegacyCostForGold(shortage);
            int goldGain   = LegacyManager.Instance.GetGoldForLegacy(legacyCost);
            string goldHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGoldColor());
            const string legacyHex = "#A0FF80";
            string message = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Messages", "Economy_LegacyGoldPurchaseConfirm",
                arguments: new object[] { new Dictionary<string, object>
                {
                    { "goldColor",   goldHex },
                    { "gold",        goldGain.ToString("N0") },
                    { "legacyColor", legacyHex },
                    { "legacy",      legacyCost.ToString("N0") }
                } });
            string confirmLabel = $"{legacyCost:N0}";

            var obj = Instantiate(legacyPurchasePopupPrefab, popupParent);
            var view = obj.GetComponentInChildren<LegacyPurchasePopupView>(true);
            if (view == null)
            {
                Log.Error("[UIPopupController] LegacyPurchasePopupView를 찾을 수 없습니다.");
                Destroy(obj);
                onCancel?.Invoke();
                return;
            }

            var handle = new PopupHandle { Obj = obj };
            Action confirmWrapped = () =>
            {
                activePopups.Remove(handle);
                StartCoroutine(InvokeNextFrameThenDestroy(obj, onConfirm));
            };
            Action cancelWrapped = () =>
            {
                activePopups.Remove(handle);
                StartCoroutine(InvokeNextFrameThenDestroy(obj, onCancel));
            };
            handle.OnEscape = cancelWrapped;

            view.Show(message, confirmLabel, onConfirm: confirmWrapped, onCancel: cancelWrapped);
            view.Open();
            activePopups.Add(handle);
            SoundManager.Instance?.PlaySFX(SfxKeyOf(PopupSfxType.Confirm));
        }

        private IEnumerator InvokeNextFrameThenDestroy(GameObject obj, Action callback)
        {
            yield return null;
            callback?.Invoke();
            if (obj != null) Destroy(obj);
        }

        #endregion

        #region 오버레이

        /// <summary>
        /// popupParent에 풀스크린 투명 클릭 차단 오버레이를 띄운다.
        /// 클릭 시 onClick이 호출되며, 반환된 OverlayController의 GameObject 파괴는 호출자가 책임진다.
        /// </summary>
        public OverlayController ShowOverlay(Action onClick)
        {
            if (overlayPrefab == null)
            {
                Log.Error("[UIPopupController] overlayPrefab이 할당되지 않았습니다.");
                return null;
            }

            if (popupParent == null)
            {
                Log.Error("[UIPopupController] popupParent가 설정되지 않았습니다.");
                return null;
            }

            var obj = Instantiate(overlayPrefab, popupParent);
            var overlay = obj.GetComponent<OverlayController>();
            if (overlay == null)
            {
                Log.Error("[UIPopupController] OverlayController 컴포넌트를 찾을 수 없습니다.");
                Destroy(obj);
                return null;
            }

            overlay.Initialize(onClick);
            return overlay;
        }

        #endregion

        #region PlayerResourceBar

        /// <summary>
        /// 현재 골드/유산 포인트를 표시하는 PlayerResourceBar를 띄운다.
        /// 이미 표시 중이면 무시한다.
        /// </summary>
        public void ShowPlayerResourceBar()
        {
            if (playerResourceBarInstance != null) return;

            if (playerResourceBarPrefab == null)
            {
                Log.Error("[UIPopupController] playerResourceBarPrefab이 할당되지 않았습니다.");
                return;
            }

            var obj = Instantiate(playerResourceBarPrefab, popupParent);
            var view = obj.GetComponentInChildren<PlayerResourceBar>(true);
            if (view == null)
            {
                Log.Error("[UIPopupController] PlayerResourceBar를 찾을 수 없습니다.");
                Destroy(obj);
                return;
            }

            playerResourceBarInstance = obj;
            view.Open();
        }

        public void HidePlayerResourceBar()
        {
            if (playerResourceBarInstance == null) return;
            Destroy(playerResourceBarInstance);
            playerResourceBarInstance = null;
        }

        /// <summary>
        /// 표시 중인 PlayerResourceBar를 부모의 마지막 자식으로 옮겨 최상단에 렌더링되게 한다.
        /// 새 패널이 위로 열렸을 때 리소스바가 가려지지 않도록 호출한다.
        /// </summary>
        public void BringPlayerResourceBarToFront()
        {
            if (playerResourceBarInstance == null) return;
            playerResourceBarInstance.transform.SetAsLastSibling();
        }

        #endregion

        #region Toast

        /// <summary>
        /// 서서히 사라지는 토스트 메시지 표시.
        /// 동일한 메시지의 토스트가 아직 떠 있으면 무시한다.
        /// </summary>
        public void ShowToast(string message, float duration = 2f, PopupSfxType type = PopupSfxType.Notify)
        {
            // 이미 사라진(Destroy된) 토스트 정리
            activeToasts.RemoveAll(t => t.obj == null);

            if (activeToasts.Exists(t => t.message == message))
                return;

            if (toastPrefab == null)
            {
                Log.Error("[UIPopupController] toastPrefab이 할당되지 않았습니다.");
                return;
            }

            if (toastContainer == null)
            {
                Log.Error("[UIPopupController] toastContainer가 할당되지 않았습니다.");
                return;
            }

            var toastObj = Instantiate(toastPrefab, toastContainer);
            var toast = toastObj.GetComponentOrNull<ToastView>();

            if (toast != null)
            {
                toast.Show(message, duration);
                activeToasts.Add((message, toastObj));
                SoundManager.Instance?.PlaySFX(SfxKeyOf(type));
            }
            else
            {
                Log.Error("[UIPopupController] ToastView 컴포넌트를 찾을 수 없습니다.");
                Destroy(toastObj);
            }
        }

        #endregion
    }
}
