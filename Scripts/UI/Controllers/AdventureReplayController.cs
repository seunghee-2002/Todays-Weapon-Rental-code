using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 노드 리플레이 Controller
    /// Progress → Replay → 팝업 → Result 흐름을 중계한다.
    /// </summary>
    public class AdventureReplayController : BaseController<AdventureReplayView>
    {
        private AdventureInstance currentAdventure;

        #region View로부터 호출되는 메서드

        public void OnSpeedSelected(float speed) => SetSpeed(speed);
        public void OnSkipClicked() => view?.Skip();

        private void EnterRecallMode() => view?.EnterRecallMode();

        private void SetSpeed(float speed)
        {
            view?.SetSpeed(speed);
            LegacyManager.Instance.PlayerData.replaySpeed = speed;
        }

        public void OnWeaponIconClicked()
        {
            // 기본무기(미대여)는 무기 상세가 없으므로 클릭 무반응
            if (currentAdventure?.weapon == null || currentAdventure.isUsingDefaultWeapon) return;
            var popup = UIManager.Instance.GetOrInstantiatePanel<WeaponDetailPopup>();
            UIManager.Instance.OpenPanel<WeaponDetailPopup>();
            popup?.Initialize(currentAdventure.weapon);
        }

        public void OnGoToResultClicked()
        {
            UIPopupController.Instance?.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "AdventureReplay_GoToResultConfirm"),
                onConfirm: () => {
                    UIManager.Instance.GetOrInstantiatePanel<AdventureResultView>();
                    UIControllerManager.Instance.GetController<AdventureResultController>()?.OpenAndPlay(currentAdventure);
                },
                onCancel: () => { }
            );
        }

        /// <summary>
        /// 리플레이 패널을 열고 재생을 시작한다 (AdventureProgressController에서 호출).
        /// </summary>
        public void OpenAndPlay(AdventureInstance adventure)
        {
            if (adventure == null)
            {
                Log.Error("[AdventureReplayController] adventure가 null입니다.");
                return;
            }

            currentAdventure = adventure;
            UIManager.Instance.OpenPanel<AdventureReplayView>();
            float savedSpeed = LegacyManager.Instance.PlayerData.replaySpeed;
            view?.SetSpeed(savedSpeed);
            view?.SetTutorialSkipDisabled(TutorialManager.Instance?.IsTutorialActive ?? false);   // 9-B: 리플레이 스킵 불가
            view?.OpenAndPlay(adventure, OnReplayCompleted);
        }

        #endregion

        #region 내부 메서드

        // 애니메이션 종료(자연 종료·스킵 공통) → 리콜 모드 진입 + 결과 이동 팝업 표시.
        private void OnReplayCompleted()
        {
            EnterRecallMode();

            // 튜토리얼(9-B): "결과창으로 이동?" 확인 팝업을 우회하고 바로 결과창으로 진입한다(팝업 하이라이트 회피).
            if (TutorialManager.Instance?.IsTutorialActive ?? false)
            {
                UIManager.Instance.GetOrInstantiatePanel<AdventureResultView>();
                UIControllerManager.Instance.GetController<AdventureResultController>()?.OpenAndPlay(currentAdventure);
                return;
            }

            OnGoToResultClicked();
        }

        #endregion
    }
}
