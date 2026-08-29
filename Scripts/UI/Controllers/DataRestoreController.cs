using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class DataRestoreController : BaseController<DataRestoreView>
    {
        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        private enum ConfirmMode { Restore, GenerateCode }
        private ConfirmMode confirmMode;

        // 화면에 표시/복사할 현재 복원 코드 ("PlayerId-secret" 난수 코드)
        private string currentRestoreCode;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            view?.SetController(this);
        }

        protected override async void OnEnable()
        {
            base.OnEnable();

            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
                return;

            view?.SetLoading(true);
            var status = await CloudSaveManager.Instance.GetRestoreStatusAsync();
            if (this == null || view == null) return;

            if (status == RestoreStatus.Active)
            {
                // 활성 코드 재표시: secret은 본인 Cloud Save에서만 읽을 수 있다
                currentRestoreCode = await CloudSaveManager.Instance.GetActiveRestoreCodeAsync();
                if (this == null || view == null) return;

                if (!string.IsNullOrEmpty(currentRestoreCode))
                {
                    view?.SetCodeGenerated(currentRestoreCode);
                    view?.SetRestoreInputInteractable(false);
                }
            }
            view.SetLoading(false);
        }

        #endregion

        #region View로부터 호출되는 메서드

        public void OnCloseClicked()
        {
            UIManager.Instance?.ClosePanel<DataRestoreView>();
        }

        public async void OnGenerateCodeClicked()
        {
            if (UGSManager.Instance == null || !UGSManager.Instance.IsInitialized)
            {
                view?.ShowWarningToast(L("Restore_ServerConnecting"));
                return;
            }

            view?.SetLoading(true);
            var currentStatus = await CloudSaveManager.Instance.GetRestoreStatusAsync();

            if (currentStatus == RestoreStatus.Active)
            {
                currentRestoreCode = await CloudSaveManager.Instance.GetActiveRestoreCodeAsync();
                view?.SetLoading(false);
                if (!string.IsNullOrEmpty(currentRestoreCode))
                    view?.SetCodeGenerated(currentRestoreCode);
                view?.SetRestoreInputInteractable(false);
                view?.ShowAlarmToast(L("Restore_CodeAlreadyValid"));
                return;
            }
            view?.SetLoading(false);

            confirmMode = ConfirmMode.GenerateCode;
            view?.SetConfirmVisible(true, L("Restore_GenerateConfirm"));
        }

        public void OnCopyClicked()
        {
            // PlayerId가 아니라 발급받은 난수 복원 코드를 복사한다
            if (string.IsNullOrEmpty(currentRestoreCode))
            {
                view?.ShowWarningToast(L("Restore_GenerateFirst"));
                return;
            }
            GUIUtility.systemCopyBuffer = currentRestoreCode;
            view?.ShowAlarmToast(L("Restore_CopiedToClipboard"));
        }

        public void OnRestoreClicked()
        {
            string code = view?.GetRestoreCode() ?? "";
            if (string.IsNullOrWhiteSpace(code))
            {
                view?.ShowWarningToast(L("Restore_EnterCode"));
                view?.SetRestoreInputMode(RestoreInputMode.Required);
                return;
            }

            // 코드는 "PlayerId-secret" 형태 - 자기 자신 코드 사전 차단 (서버도 1004로 재검증)
            if (UGSManager.Instance != null && UGSManager.Instance.IsInitialized &&
                !string.IsNullOrEmpty(UGSManager.Instance.PlayerId) &&
                code.Trim().StartsWith(UGSManager.Instance.PlayerId + "-"))
            {
                view?.ShowWarningToast(L("Restore_OwnDeviceCode"));
                view?.SetRestoreInputMode(RestoreInputMode.Error);
                return;
            }

            confirmMode = ConfirmMode.Restore;
            view?.SetConfirmVisible(true, L("Restore_Confirm"));
        }

        public async void OnConfirmYesClicked()
        {
            view?.SetConfirmVisible(false);

            if (confirmMode == ConfirmMode.GenerateCode)
            {
                await ExecuteGenerateCodeAsync();
                return;
            }

            string restoreCode = view?.GetRestoreCode() ?? "";
            view?.SetLoading(true);

            try
            {
                var (result, restoredData, restoredLegacy) = await CloudSaveManager.Instance.RedeemRestoreCodeAsync(restoreCode);

                switch (result)
                {
                    case RestoreResult.AlreadyUsed:
                        view?.ShowWarningToast(L("Restore_CodeUsed"));
                        view?.SetRestoreInputMode(RestoreInputMode.Error);
                        view?.SetLoading(false);
                        return;

                    case RestoreResult.Expired:
                        view?.ShowWarningToast(L("Restore_CodeExpired"));
                        view?.SetRestoreInputMode(RestoreInputMode.Error);
                        view?.SetLoading(false);
                        return;

                    case RestoreResult.NotFound:
                        view?.ShowWarningToast(L("Restore_DataNotFound"));
                        view?.SetRestoreInputMode(RestoreInputMode.Error);
                        view?.SetLoading(false);
                        return;

                    case RestoreResult.SelfRestore:
                        view?.ShowWarningToast(L("Restore_OwnDeviceCode"));
                        view?.SetRestoreInputMode(RestoreInputMode.Error);
                        view?.SetLoading(false);
                        return;

                    case RestoreResult.Error:
                        view?.ShowWarningToast(L("Restore_Failed"));
                        view?.SetRestoreInputMode(RestoreInputMode.Error);
                        view?.SetLoading(false);
                        return;
                }

                // Success
                restoredData.lastSavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                SaveManager.SaveGame(restoredData);

                // legacy(유산/영구)도 함께 복원 - 파일과 런타임(LegacyManager) 모두 갱신
                if (restoredLegacy != null)
                    LegacyManager.Instance?.ReplaceLegacyData(restoredLegacy);

                await CloudSaveManager.Instance.UploadAsync(
                    restoredData,
                    LegacyManager.Instance?.GetLegacyData() ?? SaveManager.LoadPlayer());

                Log.Info($"[DataRestoreController] 복원 완료 (Day {restoredData.currentDay})");
                view?.ShowAlarmToast(L("Restore_Completed"));
                view?.SetRestoreInputMode(RestoreInputMode.Default);
                view?.SetLoading(false);

                await Task.Delay(1500);
                SceneController.Instance?.ReloadCurrentScene();
            }
            catch (Exception e)
            {
                Log.Error($"[DataRestoreController] 복원 실패: {e.Message}");
                view?.ShowWarningToast(L("Restore_Failed"));
                view?.SetRestoreInputMode(RestoreInputMode.Error);
                view?.SetLoading(false);
            }
        }

        public void OnConfirmNoClicked()
        {
            view?.SetConfirmVisible(false);
        }

        #endregion

        #region 내부 메서드

        private async Task ExecuteGenerateCodeAsync()
        {
            if (SaveManager.LoadGame() == null)
            {
                view?.ShowWarningToast(L("Restore_NoSaveData"));
                return;
            }

            view?.SetLoading(true);

            bool generated = false;
            try
            {
                var (result, restoreCode) = await CloudSaveManager.Instance.GenerateRestoreCodeAsync();

                switch (result)
                {
                    case GenerateRestoreResult.Success:
                        generated = true;
                        currentRestoreCode = restoreCode;
                        view?.SetCodeGenerated(restoreCode);
                        view?.ShowAlarmToast(L("Restore_CodeGenerated"));
                        UGSManager.Instance.StartRestoreCodePolling();
                        break;

                    case GenerateRestoreResult.MustWipeFirst:
                        // 이전 복원이 완료된 상태 - 초기화 감지 상태를 지우지 않도록 발급 차단
                        view?.ShowWarningToast(L("Restore_AlreadyRestoredElsewhere"));
                        break;

                    case GenerateRestoreResult.NoSaveData:
                        view?.ShowWarningToast(L("Restore_NoCloudData"));
                        break;

                    default:
                        view?.ShowWarningToast(L("Restore_GenerateFailed"));
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[DataRestoreController] 복원 코드 생성 실패: {e.Message}");
                view?.ShowWarningToast(L("Restore_GenerateFailed"));
            }
            finally
            {
                view?.SetLoading(false);
            }

            if (generated)
                view?.SetRestoreInputInteractable(false);
        }

        #endregion
    }
}
