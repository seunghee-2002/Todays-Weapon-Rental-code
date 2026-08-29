using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    public enum PolicyDocumentType { TermsOfService, PrivacyPolicy }

    /// <summary>
    /// 약관 문서 뷰어. 서비스 이용약관과 개인정보처리방침을 모드로 전환해 표시한다.
    /// 최초 실행의 약관 동의(TermsAgreementView)와 설정의 방침 열람이 이 화면을 함께 쓴다.
    /// </summary>
    public class DataCollectionView : BaseView
    {
        [Header("본문")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private ScrollRect bodyScrollRect;
        [SerializeField] private TextAsset termsOfServiceText;
        [FormerlySerializedAs("policyText")]
        [SerializeField] private TextAsset privacyPolicyText;

        [Header("Buttons")]
        [SerializeField] private Button viewFullPolicyButton;
        [SerializeField] private Button closeButton;

        private const string TermsTitleKey = "DataCollection_TermsTitle";
        private const string PrivacyTitleKey = "DataCollection_PrivacyTitle";

        private DataCollectionController controller;

        /// <summary>현재 표시 중인 문서. Open 전에 SetDocument로 지정한다.</summary>
        public PolicyDocumentType CurrentDocument { get; private set; }

        public void SetController(DataCollectionController ctrl) => controller = ctrl;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;    // MainMenuScene에는 게임 시간이 흐르지 않는다
            isCanClickOverlay = false;  // 오버레이 클릭으로 닫지 않는다
            canEscape = true;
        }

        public override void Open()
        {
            base.Open();

            bool isTerms = CurrentDocument == PolicyDocumentType.TermsOfService;

            if (titleText != null)
                titleText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", isTerms ? TermsTitleKey : PrivacyTitleKey);

            if (bodyText != null)
            {
                TextAsset doc = isTerms ? termsOfServiceText : privacyPolicyText;
                bodyText.text = doc != null ? doc.text : string.Empty;
            }

            // 재오픈 시 이전 문서의 스크롤 위치가 남지 않도록 맨 위로 되돌린다
            if (bodyScrollRect != null)
                bodyScrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 표시할 문서 지정. UIManager.OpenPanel의 beforeOpen에서 호출해야 그 Open에 반영된다.
        /// </summary>
        public void SetDocument(PolicyDocumentType type) => CurrentDocument = type;

        protected override void SubscribeEvents()
        {
            viewFullPolicyButton?.onClick.AddListener(() => controller?.OnViewFullPolicyClicked());
            closeButton?.onClick.AddListener(() => controller?.OnCloseClicked());
        }

        protected override void UnsubscribeEvents()
        {
            viewFullPolicyButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
        }

        #endregion
    }
}
