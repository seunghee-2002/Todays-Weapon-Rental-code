using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class NicknameChangePopupController : BaseController<NicknameChangePopupView>
    {
        #region View로부터 호출되는 메서드

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        public async void OnConfirmClicked()
        {
            if (view == null || NicknameManager.Instance == null) return;

            view.ClearError();
            view.SetButtonsInteractable(false);

            var result = await NicknameManager.Instance.ChangeNicknameAsync(view.GetNickname());

            if (result == NicknameChangeResult.Success)
            {
                UIPopupController.Instance?.ShowToast(L("Nickname_Changed"));
                UIManager.Instance?.ClosePanel<NicknameChangePopupView>();
            }
            else
            {
                view.ShowError(GetErrorMessage(result));
                view.RefreshCost();
                view.SetButtonsInteractable(true);
            }
        }

        public void OnCancelClicked()
        {
            UIManager.Instance?.ClosePanel<NicknameChangePopupView>();
        }

        #endregion

        #region 내부 메서드

        private static string GetErrorMessage(NicknameChangeResult result) => result switch
        {
            NicknameChangeResult.Empty               => L("Nickname_ErrEmpty"),
            NicknameChangeResult.TooShort            => L("Nickname_ErrTooShort"),
            NicknameChangeResult.TooLong             => L("Nickname_ErrTooLong"),
            NicknameChangeResult.InvalidCharacters   => L("Nickname_ErrInvalidChars"),
            NicknameChangeResult.ContainsProfanity   => L("Nickname_ErrProfanity"),
            NicknameChangeResult.InsufficientLegacy  => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Economy_NotEnoughLegacy"),
            NicknameChangeResult.ServerError         => L("Nickname_ErrServer"),
            _                                        => L("Nickname_ErrUnknown"),
        };

        #endregion
    }
}
