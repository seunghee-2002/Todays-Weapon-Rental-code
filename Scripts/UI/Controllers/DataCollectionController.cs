using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 약관 문서 뷰어 컨트롤러. 표시 중인 문서에 맞는 웹 원문을 열고 패널을 닫는다.
    /// </summary>
    public class DataCollectionController : BaseController<DataCollectionView>
    {
        private const string TermsUrl = "https://seunghee-2002.github.io/privacy-policy/todays-weapon-rental/terms/";
        private const string PrivacyUrl = "https://seunghee-2002.github.io/privacy-policy/todays-weapon-rental/privacy/";

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            view?.SetController(this);
        }

        #endregion

        #region View로부터 호출되는 메서드

        public void OnCloseClicked()
        {
            UIManager.Instance?.ClosePanel<DataCollectionView>();
        }

        public void OnViewFullPolicyClicked()
        {
            bool isTerms = view != null && view.CurrentDocument == PolicyDocumentType.TermsOfService;
            Application.OpenURL(isTerms ? TermsUrl : PrivacyUrl);
        }

        #endregion
    }
}
