// Scripts/UI/Controllers/AdventurerInteraction/SeerController.cs
using System.Collections.Generic;
using System.Diagnostics;

namespace TodaysWeaponRental
{
    public class SeerController : BaseController<SeerView>
    {
        private AdventurerInstance currentAdventurer;
        private DungeonData currentDungeon;

        // 세션(패널) 단위 카운트 — 패널을 열 때(Initialize) 리셋된다. 저장하지 않음.
        private int crystalBallTouchCount;

        // 재방문 카운트 — 대상(모험가+던전)이 바뀔 때만 리셋된다. 패널을 열 때마다 리셋하면
        // 아래 ++revisitCount가 항상 1이 되어 revisitLines2/3이 영원히 안 나온다. 저장하지 않음.
        private int    revisitCount;
        private string revisitKey;

        // 이스터에그 발동 시 대사창 클릭으로 넘겨줄 대기 코멘트. null이면 대기 없음.
        private string pendingEasterEggComment;

        // 수정구 경고 이후 대사창 클릭으로 넘겨줄 후속 대사. null이면 대기 없음.
        private string pendingCrystalBallFollowUp;

        #region 초기화

        public void Initialize(AdventurerInstance adventurer, DungeonData dungeon)
        {
            currentAdventurer = adventurer;
            currentDungeon    = dungeon;
            crystalBallTouchCount = 0;

            // SeerManager의 결과 캐시와 같은 키 형식 — 재방문 대사는 그 결과 단위로 센다.
            string key = $"{adventurer.instanceID}_{dungeon.StaticID}";
            if (key != revisitKey)
            {
                revisitKey   = key;
                revisitCount = 0;
            }

            pendingEasterEggComment   = null;
            pendingCrystalBallFollowUp = null;
            view?.UpdateDisplay(adventurer, dungeon);

            var existing = SeerManager.Instance.GetExistingResult(adventurer, dungeon);
            if (existing != null)
                view?.ShowResult(existing, dialogue: SeerManager.Instance.GetRevisitLine(++revisitCount), playAnimation: false);
        }

        protected override void SubscribeControllerEvents()
        {
        }

        protected override void UnsubscribeControllerEvents()
        {
        }

        #endregion

        #region View로부터 호출되는 메서드

        /// <summary>
        /// Yes 버튼 클릭 → 정상 확률로 점술 진행.
        /// </summary>
        public void OnYesClicked()
        {
            if (currentAdventurer == null || currentDungeon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("seer", "yes", new Dictionary<string, object>
            {
                { "dungeon_id", currentDungeon.StaticID },
                { "cost", SeerManager.Instance.GetSeerCost() }
            });

            if (!SeerManager.Instance.CanConsult(currentAdventurer, currentDungeon)) return;

            int cost = SeerManager.Instance.GetSeerCost();
            EconomyManager.Instance.EnsureGold(cost, onReady: () => view?.PlaySeerParticle(OnGlowComplete));
        }

        /// <summary>
        /// 수정구 직접 클릭 → 이스터에그. 임계 횟수 이전 클릭은 결제 없이 경고 대사만 출력하고,
        /// 임계 횟수째 클릭부터 결제 + 균등 랜덤 확률로 점술을 진행한다.
        /// </summary>
        public void OnCrystalBallClicked()
        {
            if (currentAdventurer == null || currentDungeon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("seer", "crystal_ball", new Dictionary<string, object>
            {
                { "dungeon_id", currentDungeon.StaticID },
                { "cost", SeerManager.Instance.GetSeerCost() }
            });

            if (!SeerManager.Instance.CanConsult(currentAdventurer, currentDungeon)) return;

            crystalBallTouchCount++;

            int threshold = ConfigManager.Instance.Seer.easterEggConsultAtTouch;
            if (crystalBallTouchCount < threshold)
            {
                view?.ShowCrystalBallWarning(SeerManager.Instance.GetEasterEggLine(crystalBallTouchCount));
                pendingCrystalBallFollowUp = SeerManager.Instance.GetEasterEggFollowUpLine();
                return;
            }

            // 이스터에그 발동 시점 — 임계 이전 경고에서 남은 후속 대사를 폐기(안 그러면
            // 결과창 대사버튼 클릭 시 결과 코멘트 대신 경고 후속 대사가 먼저 나옴).
            pendingCrystalBallFollowUp = null;

            int cost = SeerManager.Instance.GetSeerCost();
            EconomyManager.Instance.EnsureGold(cost, onReady: () => view?.PlaySeerParticle(OnEasterEggGlowComplete));
        }

        /// <summary>
        /// 결과 확인 후 대사창 클릭. 이스터에그 발동으로 대기 중인 코멘트가 있으면
        /// 그 코멘트로 넘기고(2단계), 없으면 패널을 닫는다.
        /// </summary>
        public void OnDialogueButtonClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("seer", "dialogue");

            if (pendingCrystalBallFollowUp != null)
            {
                view?.ShowCrystalBallFollowUp(pendingCrystalBallFollowUp);
                pendingCrystalBallFollowUp = null;
                return;
            }
            if (pendingEasterEggComment != null)
            {
                view?.ShowFollowUpDialogue(pendingEasterEggComment);
                pendingEasterEggComment = null;
                return;
            }
            OnClose();
        }

        public void OnClose()
        {
            AnalyticsManager.Instance?.SendButtonClick("seer", "close");
            UIControllerManager.Instance.GetController<AdventurePreparationController>()?.RefreshSeerResult();
            currentAdventurer = null;
            currentDungeon    = null;
            UIManager.Instance.ClosePanel<SeerView>();
        }

        #endregion

        #region 내부 메서드

        private void OnGlowComplete()
        {
            var result = SeerManager.Instance.Consult(currentAdventurer, currentDungeon);
            if (result != null)
                view?.ShowResult(result, dialogue: SeerManager.Instance.GetSeerComment(result.luckLevel));
        }

        private void OnEasterEggGlowComplete()
        {
            var result = SeerManager.Instance.ConsultEasterEgg(currentAdventurer, currentDungeon);
            if (result == null) return;

            // 1단계: 프리페이스 대사 + 수정구 결과 서사. 코멘트는 대사창 클릭 시 2단계로 출력.
            pendingEasterEggComment = SeerManager.Instance.GetSeerComment(result.luckLevel);
            view?.ShowResult(result, dialogue: SeerManager.Instance.GetEasterEggConsultPreface(), hasNext: true);
        }

        #endregion
    }
}
