using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 언어 선택 팝업 View.
    /// 프리팹에 고정 배치한 토글 4개(ko -> en -> ja -> zh 순서)로 언어를 고르고, 확인 팝업을 거쳐 변경한다.
    /// 언어 표기는 프리팹의 아이콘이 담당하고(코드 무관), 선택 표시는 토글별 CheckIndicator를 SetActive로 관리한다.
    /// 저장·시작 복원은 Localization 패키지가 전담하므로 여기서는 변경 요청만 한다.
    /// 패널 타이틀은 프리팹의 LocalizeStringEvent(UI_Common/Translate_Title)가 갱신한다.
    /// </summary>
    public class TranslateView : BaseView
    {
        /// <summary>언어 한 줄 분량의 UI 묶음. LocalizationManager.SupportedLocales와 같은 순서로 연결한다.</summary>
        [Serializable]
        private class LocaleRow
        {
            public Toggle toggle;
            public GameObject checkIndicator;   // 선택 표시 (SetActive로 관리)
        }

        [Header("언어 토글 (ko -> en -> ja -> zh 순서)")]
        [SerializeField] private LocaleRow[] localeRows;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = true;
            canEscape = true;
        }

        public override void Open()
        {
            base.Open();
            RefreshSelection();
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);

            if (localeRows != null)
            {
                for (int i = 0; i < localeRows.Length; i++)
                {
                    int index = i; // 클로저 캡처용
                    localeRows[i]?.toggle?.onValueChanged.AddListener(isOn => OnToggleChanged(index, isOn));
                }
            }

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLocaleChanged += OnLocaleChanged;
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();

            if (localeRows != null)
            {
                foreach (var row in localeRows)
                    row?.toggle?.onValueChanged.RemoveAllListeners();
            }

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLocaleChanged -= OnLocaleChanged;
        }

        #endregion

        #region UI 업데이트 메서드

        /// <summary>
        /// 현재 로케일 행만 토글 on + CheckIndicator 표시.
        /// 토글은 Notify 없이 세팅한다 — 이벤트 경유 갱신은 확인 팝업을 다시 띄운다.
        /// </summary>
        private void RefreshSelection()
        {
            var locales = LocalizationManager.Instance?.SupportedLocales;
            var current = LocalizationManager.Instance?.CurrentLocale;
            if (locales == null || localeRows == null) return;

            for (int i = 0; i < localeRows.Length; i++)
            {
                if (localeRows[i] == null) continue;

                bool isSelected = i < locales.Count && locales[i] == current;
                localeRows[i].toggle?.SetIsOnWithoutNotify(isSelected);
                localeRows[i].checkIndicator?.SetActive(isSelected);
            }
        }

        #endregion

        #region 이벤트 핸들러

        /// <summary>토글 선택. 현재 언어면 무시하고, 다르면 확인 팝업을 거쳐 변경한다. 취소하면 되돌린다.</summary>
        private void OnToggleChanged(int index, bool isOn)
        {
            if (!isOn) return; // ToggleGroup이 끈 반대쪽 토글의 이벤트는 무시

            var manager = LocalizationManager.Instance;
            if (manager == null || index >= manager.SupportedLocales.Count) return;

            var locale = manager.SupportedLocales[index];
            if (locale == manager.CurrentLocale) return;

            // 확인 문구는 현재 언어로 표시한다. 대상 언어 이름({name})은 항상 원어 표기.
            string message = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Common", "Translate_ConfirmChange",
                arguments: new object[] { new Dictionary<string, object> { { "name", LocalizationManager.GetNativeName(locale) } } });

            // onCancel이 null이면 확인 버튼만 나오므로, 취소 버튼을 띄우고 취소/ESC 시 토글을 되돌린다
            UIPopupController.Instance?.ShowPopup(message,
                onConfirm: () => LocalizationManager.Instance?.ChangeLocale(locale),
                onCancel: RefreshSelection);
        }

        private void OnLocaleChanged(Locale _)
        {
            RefreshSelection();
        }

        // ESC/오버레이 클릭은 UIManager.CloseTopPanel이 Close()까지 처리하므로
        // OnEscapeClicked를 오버라이드하지 않는다 (BGMChangeView와 동일 규칙).
        private void OnCloseClicked()
        {
            UIManager.Instance?.ClosePanel<TranslateView>();
        }

        #endregion
    }
}
