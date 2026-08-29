using System;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    public class NodeMarker : MonoBehaviour
    {
        [SerializeField] private Image eventIconImage;
        [SerializeField] private RawImage monsterView;   // 라이브 몬스터(공유 RT) 표시. 평소 비활성
        [SerializeField] private Transform effectTransform;
        [SerializeField] private Button button;

        private EventResult storedResult;
        private DungeonEventData eventData;
        private MonsterStageActor liveMonster;   // 이 노드가 공유 RT를 점유 중이면 non-null
        private RenderTexture idleSnapshot;   // 보호 생존 패배 노드가 공유 RT 대신 보유하는 정적 idle 스냅샷

        public Transform EffectTransform => effectTransform != null ? effectTransform : transform;
        public EventResult Result => storedResult;

        public void SetEvent(DungeonEventData data)
        {
            eventData = data;
            // 평상시 노드 비주얼: 비전투는 프롭 스프라이트(보물상자/모닥불/가시 등), 없으면 이벤트 타입 아이콘 폴백.
            // 전투 노드는 라이브 몬스터/묘비로만 표현한다 — 이벤트 타입 아이콘은 TrackMarker 전용이라 노드에 쓰지 않는다.
            Sprite sprite = data is DungeonNonBattleEventData nb
                ? (nb.propSprite != null ? nb.propSprite : IconManager.Instance?.GetIconByEventType(nb.eventType))
                : null;
            if (eventIconImage != null)
            {
                eventIconImage.sprite = sprite;
                eventIconImage.enabled = sprite != null;
            }
            if (monsterView != null) monsterView.enabled = false;
            liveMonster = null;   // 노드 재구성 시 점유 해제 (RT 정리는 호출측 책임)
            ReleaseSnapshot();    // 이전 노드에서 남은 스냅샷 RT 해제(재사용 누수 방지)
        }

        private void OnDestroy() => ReleaseSnapshot();

        // 묘비 스프라이트: 프리팹의 "Dead" 자식 SpriteRenderer (LayerLab 팩 공통 구조)
        private static Sprite ResolveTombstoneSprite(GameObject monsterPrefab)
        {
            if (monsterPrefab == null) return null;
            foreach (var sr in monsterPrefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null && sr.sprite != null &&
                    sr.gameObject.name.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0)
                    return sr.sprite;
            return null;
        }

        #region 라이브 몬스터 (공유 RT)

        /// <summary>이 노드에 라이브 몬스터를 띄운다(공유 RT 점유). idle 아이콘은 숨김.</summary>
        public void ShowLiveMonster(float timeScale)
        {
            if (eventData is not DungeonBattleEventData battle || battle.monsterPrefab == null) return;
            var renderer = MonsterStageRenderer.ReplayInstance;
            if (renderer == null) return;

            liveMonster = renderer.Spawn(battle.monsterPrefab, battle.monsterType);
            if (liveMonster == null) return;

            liveMonster.SetTimeScale(timeScale);
            liveMonster.PlayIdle();

            if (monsterView != null)
            {
                monsterView.texture = renderer.RenderTexture;
                monsterView.enabled = true;
            }
            if (eventIconImage != null) eventIconImage.enabled = false;
        }

        public void PlayMonsterAttack() => liveMonster?.PlayAttack();
        public void PlayMonsterDead()   => liveMonster?.PlayDead();

        /// <summary>처치 후: 라이브 몬스터 제거(RT 반납) + 묘비 정적 스프라이트로 고정.</summary>
        public void ShowTombstone()
        {
            ReleaseLiveMonster();
            var tomb = ResolveTombstoneSprite((eventData as DungeonBattleEventData)?.monsterPrefab);
            if (eventIconImage != null)
            {
                if (tomb != null) eventIconImage.sprite = tomb;
                eventIconImage.enabled = eventIconImage.sprite != null;
            }
        }

        /// <summary>보호 생존 패배: 라이브 몬스터를 idle 정적 스냅샷으로 고정(공유 RT 반납).
        /// 공유 RT는 1개뿐이라 여러 패배 노드를 동시에 라이브로 못 띄우므로 노드별 스냅샷으로 남긴다.</summary>
        public void ShowMonsterIdleSnapshot()
        {
            if (liveMonster == null) return;   // 라이브 몬스터가 없으면 스냅샷 불가 (전투 노드는 아이콘 폴백 없음)

            liveMonster.PlayIdle();   // 직전에 Attack 등이 재생됐어도 idle 포즈로 복원(내부에서 즉시 평가)
            var snap = MonsterStageRenderer.ReplayInstance?.CaptureSnapshot();
            ReleaseLiveMonster();     // 공유 RT 반납 → 다음 노드가 사용 가능

            if (snap == null) return;   // 캡처 실패

            ReleaseSnapshot();
            idleSnapshot = snap;
            if (monsterView != null) { monsterView.texture = snap; monsterView.enabled = true; }
            if (eventIconImage != null) eventIconImage.enabled = false;
        }

        private void ReleaseLiveMonster()
        {
            if (liveMonster != null)
            {
                MonsterStageRenderer.ReplayInstance?.Clear();
                liveMonster = null;
            }
            if (monsterView != null) monsterView.enabled = false;
        }

        private void ReleaseSnapshot()
        {
            if (idleSnapshot == null) return;
            idleSnapshot.Release();
            Destroy(idleSnapshot);
            idleSnapshot = null;
        }

        #endregion

        /// <summary>전투 결과 표시. 비전투 노드는 이벤트별 결과 아이콘으로 교체(전투는 묘비/몬스터 연출이 담당).</summary>
        public void SetResult(bool isSuccess, bool isCombat)
        {
            if (isCombat) return;   // 전투 노드는 ShowTombstone/PlayMonster*가 결과를 표현
            if (eventData is not DungeonNonBattleEventData nb) return;

            Sprite result = isSuccess ? nb.resultSuccessIcon : nb.resultFailIcon;
            if (result == null || eventIconImage == null) return;
            eventIconImage.sprite = result;
            eventIconImage.enabled = true;
        }

        public void StoreResult(EventResult result)
        {
            storedResult = result;
        }

        public void SetClickCallback(Action callback)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback?.Invoke());
        }
    }
}
