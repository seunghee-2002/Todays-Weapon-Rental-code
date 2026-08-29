using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 최초 실행 약관 동의 컨트롤러.
    /// 필수 동의와 선택 동의를 각각 다른 저장소에 기록한다.
    /// </summary>
    public class TermsAgreementController : BaseController<TermsAgreementView>
    {
        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            view?.SetController(this);
        }

        #endregion

        #region View로부터 호출되는 메서드

        /// <summary>
        /// 시작 버튼. 필수 항목을 아직 체크하지 않은 상태면 버튼이 "모두 동의하고 시작"이므로
        /// 선택 항목까지 동의한 것으로 처리한다.
        /// </summary>
        public void OnStartClicked()
        {
            bool requiredAgreed = view != null && view.IsRequiredAgreed;
            bool optionalAgreed = requiredAgreed ? view.IsOptionalAgreed : true;

            TermsAgreement.MarkAgreed();
            AnalyticsManager.Instance?.SetConsent(optionalAgreed);

            UIManager.Instance?.ClosePanel<TermsAgreementView>();
        }

        /// <summary>동의하지 않음. 필수 약관 없이는 서비스를 제공할 수 없으므로 게임을 종료한다.</summary>
        public void OnDeclineClicked()
        {
            UIPopupController.Instance?.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Terms_RejectQuitConfirm"),
                onConfirm: Application.Quit,
                onCancel: () => { },
                type: PopupSfxType.Warning);
        }

        public void OnViewTermsClicked()
        {
            UIManager.Instance?.OpenPanel<DataCollectionView>(() =>
                UIManager.Instance.GetOrInstantiatePanel<DataCollectionView>()?.SetDocument(PolicyDocumentType.TermsOfService));
        }

        public void OnViewPrivacyClicked()
        {
            UIManager.Instance?.OpenPanel<DataCollectionView>(() =>
                UIManager.Instance.GetOrInstantiatePanel<DataCollectionView>()?.SetDocument(PolicyDocumentType.PrivacyPolicy));
        }

        #endregion
    }
}
