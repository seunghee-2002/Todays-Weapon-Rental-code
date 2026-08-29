using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 메인 메뉴 UI 뷰
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        [Header("BG")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button optionButton;   // 데이터 복원은 이 옵션 팝업 안으로 이동
        [SerializeField] private CanvasGroup continueGroup;   // loadGameButton(ContinueDIM) 페이드용

        [Header("Sub Menu Toggle")]
        [SerializeField] private Toggle subMenuToggle;
        [SerializeField] private GameObject subMenuGroup;

        [Header("ETC Menu")]
        // 서브메뉴 토글과 무관하게, 동기화가 끝나면 항상 노출되는 좌측 그룹
        [SerializeField] private GameObject etcMenuGroup;
        [SerializeField] private Button termsOfServiceButton;
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button guideButton;

        [Header("Info Text")]
        [SerializeField] private TextMeshProUGUI saveInfoText;
        [SerializeField] private TextMeshProUGUI syncingText;   // 클라우드 동기화 대기 중에만 표시

        // SceneController의 씬 로딩 문구와 동일하게 맞춘다 (동기화는 진행률이 없어 % 제외)
        private const string SyncingMessageKey = "Common_Loading";

        public Button NewGameButton        => newGameButton;
        public Button LoadGameButton       => loadGameButton;
        public Button LeaderboardButton    => leaderboardButton;
        public Button UpgradeButton        => upgradeButton;
        public Button OptionButton         => optionButton;
        public Button TermsOfServiceButton => termsOfServiceButton;
        public Button PrivacyPolicyButton  => privacyPolicyButton;
        public Button GuideButton          => guideButton;

        private Vector2 titleBasePos;

        // 안내 문구는 메인 메뉴에 계속 떠 있어 로케일 변경 시 직접 갱신해야 한다.
        // UpdateUI 인자를 다시 받을 수 없으므로 마지막 상태를 기억해 둔다.
        private bool lastHasSaveData;

        private void Awake()
        {
            subMenuToggle?.onValueChanged.AddListener(OnSubMenuToggleChanged);

            subMenuToggle?.SetIsOnWithoutNotify(false); // 시작 Off
            subMenuGroup?.SetActive(false);

            SetSyncing(true); // 동기화 완료 전까지 메뉴 숨김 (타이틀은 계속 표시)

            PlayTitleIntroAnimation();

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            subMenuToggle?.onValueChanged.RemoveListener(OnSubMenuToggleChanged);

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale _) => RefreshSaveInfoText();

        /// <summary>타이틀 등장 애니메이션. 위에서 페이드인+낙하 후 둥둥 루프로 이어짐.</summary>
        private void PlayTitleIntroAnimation()
        {
            if (titleText == null) return;

            var rt = titleText.rectTransform;
            titleBasePos = rt.anchoredPosition;

            var c = titleText.color;
            c.a = 0f;
            titleText.color = c;
            rt.anchoredPosition = titleBasePos + new Vector2(0f, 40f);

            titleText.DOFade(1f, 0.8f).SetLink(gameObject);
            rt.DOAnchorPos(titleBasePos, 0.8f)
                .SetEase(Ease.OutCubic)
                .OnComplete(PlayTitleLoopAnimation)
                .SetLink(gameObject);
        }

        /// <summary>등장 애니메이션 완료 후 반복되는 둥둥 애니메이션.</summary>
        private void PlayTitleLoopAnimation()
        {
            if (titleText == null) return;

            titleText.rectTransform.DOAnchorPosY(titleBasePos.y + 10f, 1.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject);
        }

        private void OnSubMenuToggleChanged(bool isOn)
        {
            subMenuGroup?.SetActive(isOn);
        }

        /// <summary>
        /// 클라우드 동기화 대기 상태 표시.
        /// 대기 중에는 이어하기(ContinueDIM)와 메뉴 버튼을 감춰, 동기화가 끝나기 전에
        /// 유산 구매/새 게임/데이터 복원이 실행돼 클라우드 본에 덮어써지는 것을 막는다.
        /// </summary>
        public void SetSyncing(bool syncing)
        {
            if (syncingText != null)
            {
                if (syncing)
                    syncingText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                        "UI_Messages", SyncingMessageKey);
                syncingText.gameObject.SetActive(syncing);
            }

            if (syncing)
            {
                if (continueGroup != null)
                {
                    continueGroup.alpha = 0f;
                    continueGroup.interactable = false;
                    continueGroup.blocksRaycasts = false;
                }

                if (saveInfoText != null)
                    saveInfoText.alpha = 0f;

                subMenuToggle?.gameObject.SetActive(false);
                etcMenuGroup?.SetActive(false);
                return;
            }

            if (continueGroup != null)
            {
                continueGroup.interactable = true;
                continueGroup.blocksRaycasts = true;
                continueGroup.DOFade(1f, 0.5f).SetLink(gameObject);
            }

            if (saveInfoText != null)
                saveInfoText.DOFade(1f, 0.5f).SetLink(gameObject);

            subMenuToggle?.gameObject.SetActive(true);
            etcMenuGroup?.SetActive(true);
        }

        /// <summary>
        /// 저장 데이터 존재 여부, 강화 가능 여부에 따라 UI 업데이트.
        /// canUpgrade는 초회차(게임오버 이력 없음)면 false → 강화 버튼 숨김.
        /// </summary>
        public void UpdateUI(bool hasSaveData, bool canUpgrade)
        {
            // 세이브가 없으면 화면 터치(이어하기)가 곧 새 게임이라 새 게임 버튼은 중복 → 숨김
            if (newGameButton != null)
                newGameButton.gameObject.SetActive(hasSaveData);

            if (upgradeButton != null)
                upgradeButton.gameObject.SetActive(canUpgrade);

            lastHasSaveData = hasSaveData;
            RefreshSaveInfoText();
        }

        private void RefreshSaveInfoText()
        {
            if (saveInfoText == null) return;

            saveInfoText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", lastHasSaveData ? "MainMenu_ContinueTouch" : "MainMenu_NewGameTouch");
        }
    }
}