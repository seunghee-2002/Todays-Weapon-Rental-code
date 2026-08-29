using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// MainMenuScene 전용 옵션 팝업 컨트롤러.
    /// 데이터 복원 패널 열기, 데이터 수집 철회, 계정 초기화를 처리한다.
    /// </summary>
    public class MainMenuOptionController : BaseController<MainMenuOptionView>
    {
        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            view?.SetController(this);
        }

        #endregion

        #region View로부터 호출되는 메서드

        public void OnDataRestoreClicked()
        {
            UIManager.Instance?.OpenPanel<DataRestoreView>();
        }

        public void OnAnalyticsOptOutChanged(bool optOut)
        {
            AnalyticsManager.Instance?.SetOptOut(optOut);
        }

        public void OnResetDataClicked()
        {
            // 오프라인에서 초기화하면 로컬만 지워지고 클라우드 본이 남아
            // 다음 실행의 동기화로 데이터가 되살아난다
            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized ||
                Application.internetReachability == NetworkReachability.NotReachable)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("Reset_NetworkRequired"),
                    type: PopupSfxType.Warning);
                return;
            }

            UIPopupController.Instance?.ShowPopup(
                L("Reset_Confirm"),
                onConfirm: ShowFinalConfirm,
                onCancel: () => { },
                type: PopupSfxType.Warning);
        }

        #endregion

        #region 내부 메서드

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        private void ShowFinalConfirm()
        {
            UIPopupController.Instance?.ShowPopup(
                L("Reset_FinalConfirm"),
                onConfirm: () => _ = ResetDataAsync(),
                onCancel: () => { },
                type: PopupSfxType.Warning);
        }

        private async Task ResetDataAsync()
        {
            view?.SetLoading(true);

            bool success = await UGSManager.Instance.ResetAccountAsync();

            // 씬 전환 등으로 파괴됐으면 이후 UI 조작을 하지 않는다
            if (this == null) return;

            // 재발급에 실패해도 로컬 데이터는 이미 삭제된 상태이므로 화면을 그대로 두면 안 된다.
            // 새 계정은 네트워크 재연결 시 UGSManager가 자동으로 발급한다.
            if (!success)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("Reset_AccountIssueFailed"),
                    onConfirm: () => SceneController.Instance?.LoadMainMenu(),
                    type: PopupSfxType.Warning);
                return;
            }

            // 메인메뉴를 다시 로드해 초기화된 상태로 갱신한다
            SceneController.Instance?.LoadMainMenu();
        }

        #endregion
    }
}
