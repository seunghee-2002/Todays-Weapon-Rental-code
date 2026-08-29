using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using DG.Tweening;

namespace TodaysWeaponRental
{
    public class AdventureReplayView : BaseView
    {
        [Header("Controller")]
        [SerializeField] private AdventureReplayController controller;

        [Header("Header")]
        [SerializeField] private AdventurerAppearanceApplier portraitApplier;
        [SerializeField] private TextMeshProUGUI adventurerNameText;
        [SerializeField] private TextMeshProUGUI adventurerGradeText;
        [SerializeField] private Slider matchSlider;
        [SerializeField] private Button weaponIconButton;
        [SerializeField] private Image weaponIconImage;
        [SerializeField] private Image weaponBG;
        [SerializeField] private Image weaponFrame;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private TextMeshProUGUI weaponEnforceText;
        [SerializeField] private TextMeshProUGUI dungeonNameText;
        [SerializeField] private Image armorTypeTitleFrame;
        [SerializeField] private GameObject traitRoot;
        [SerializeField] private TextMeshProUGUI traitText;
        [SerializeField] private Transform weaponEffectContainer;
        [SerializeField] private WeaponEffectListItem weaponEffectItemPrefab;

        [Header("Protection Display")]
        [SerializeField] private GameObject protectionRoot;
        [SerializeField] private TextMeshProUGUI protectionCountText;
        [SerializeField] private TextMeshProUGUI protectionChangeText;

        [Header("Character")]
        [SerializeField] private AdventurerAppearanceApplier appearanceApplier;
        [SerializeField] private RectTransform characterRoot;
        private AdventureResultMotionConfig motionConfig;

        [Header("Boss Impact")]
        [SerializeField] private CanvasGroup bossImpactOverlay;

        [Header("Map")]
        [SerializeField] private ScrollRect mapScrollRect;
        [SerializeField] private RectTransform mapContainer;
        [SerializeField] private GameObject mapStartTilePrefab;
        [SerializeField] private int startNodesPerTile = 1;
        [SerializeField] private GameObject mapTilePrefab;
        [SerializeField] private int nodesPerTile = 2;
        [SerializeField] private GameObject nodeMarkerPrefab;
        [SerializeField] private float tileWidth = 1400f;
        [SerializeField] private float nodeYOffset = 0f;

        [Header("Result Track")]
        [SerializeField] private ScrollRect resultTrackScrollRect;
        [SerializeField] private Transform resultTrackContainer;
        [SerializeField] private Transform connectorLayer;
        [SerializeField] private Transform markerLayer;

        [Header("Accumulated Inventory")]
        [SerializeField] private TextMeshProUGUI accumulatedGoldText;
        [SerializeField] private RectTransform accumulatedGoldTarget;
        [SerializeField] private TextMeshProUGUI materialCountText;
        [SerializeField] private RectTransform materialIconTarget;
        [SerializeField] private RewardFlyAnimator rewardFlyAnimator;

        [Header("Control")]
        [SerializeField] private Button speedButton;
        [SerializeField] private TextMeshProUGUI speedScaleText; // "x1", "x2", "x4" 표시
        [SerializeField] private Button skipButton;
        [SerializeField] private GameObject completedIndicator; // 재생 완료 시 활성화 — 스킵 버튼으로 다음 진행 가능 표시

        [Header("Recall Mode")]
        [SerializeField] private EventResultTooltip eventResultTooltip;

        // 애니메이션 중 노드를 멈출 뷰포트 가로 비율 (왼쪽에서 2/3 지점에서 상호작용). 따라가기/포커스는 중앙(0.5).
        private const float InteractionAnchor = 2f / 3f;

        private readonly List<NodeMarker> mapNodeMarkers = new();
        private readonly List<TrackMarker> trackMarkers = new();
        private bool mapScrollSyncEnabled = true;
        private Coroutine sequenceCoroutine;
        private Coroutine bossImpactCoroutine;
        private float currentSpeed = 1f;
        private Vector2 characterNormalPos;
        private TrackMarker pendingTrackMarker;

        private AdventureInstance currentAdventure;
        private Action storedOnComplete;
        private bool skipRequested;
        private bool isReplayFinished;

        private int displayedGold;
        // 종류별 누적치는 내부 집계에만 사용하고, 화면에는 합계만 표시한다.
        private readonly Dictionary<string, int> materialCounts = new();

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = true;
            canEscape = false;

            motionConfig = ConfigManager.Instance.AdventureResultMotion;

            speedButton?.onClick.AddListener(OnSpeedButtonClicked);
            skipButton?.onClick.AddListener(OnSkipButtonClicked);
            weaponIconButton?.onClick.AddListener(() => controller?.OnWeaponIconClicked());
            mapScrollRect?.onValueChanged.AddListener(UpdateFocusByScroll);
        }

        protected override void SubscribeEvents() { }
        protected override void UnsubscribeEvents() { }

        #endregion

        #region 공개 인터페이스

        public void OpenAndPlay(AdventureInstance adventure, Action onComplete)
        {
            CancelReplay();

            currentAdventure = adventure;
            storedOnComplete = onComplete;

            // 던전 등급별 리플레이 테마 진입 (패널 Close 시 자동 이탈). 등급 정보가 Open 이후에야 확정되므로 여기서 진입.
            if (adventure?.dungeon != null)
                SetActiveThemeBgm($"AdventureReplay{adventure.dungeon.grade}");

            skipRequested = false;
            mapScrollSyncEnabled = true;
            if (skipButton != null) skipButton.interactable = true;
            if (completedIndicator != null) completedIndicator.SetActive(false);
            isReplayFinished = false;
            if (speedButton != null) speedButton.gameObject.SetActive(true);
            eventResultTooltip?.Hide();
            UpdateSpeedButtonDisplay(currentSpeed);

            UpdateHeader(adventure);
            InitializeMap(adventure);
            InitializeResultTrack();
            InitializeAccumulatedInventory();
            SetScrollsInteractable(false);

            // 레이아웃 확정 전에 캐릭터를 숨겨둠 (위치는 코루틴에서 확정 후 저장)
            var root = characterRoot ?? appearanceApplier?.GetComponent<RectTransform>();
            root?.gameObject.SetActive(false);

            sequenceCoroutine = StartCoroutine(PlayNodeSequence(adventure, onComplete));
        }

        /// <summary>재생 중단 정리(코루틴·DOTween·파티클·리플레이 스테이지). StopCoroutine은 DOTween/별도 MonoBehaviour를 멈추지 않으므로 함께 정리한다.</summary>
        private void CancelReplay()
        {
            if (bossImpactCoroutine != null) { StopCoroutine(bossImpactCoroutine); bossImpactCoroutine = null; }
            if (sequenceCoroutine != null) { StopCoroutine(sequenceCoroutine); sequenceCoroutine = null; }
            DOTween.Kill(mapScrollRect);
            if (bossImpactOverlay != null) { DOTween.Kill(bossImpactOverlay); bossImpactOverlay.alpha = 0f; }
            if (protectionChangeText != null) { DOTween.Kill(protectionChangeText); protectionChangeText.alpha = 0f; }
            rewardFlyAnimator?.CancelAll();
            EffectManager.Instance?.StopAllParticles();
            SoundManager.Instance?.StopAllSFX();
            MonsterStageRenderer.ReplayInstance?.Clear();
        }

        /// <summary>ESC(뒤로가기): 재생을 취소하고 모험 현황으로 복귀한다. 보상은 결과창이 열릴 때 지급되므로 미확인 상태가 유지되어 복구 처리가 필요 없다.</summary>
        public override void OnEscapeCancelled()
        {
            CancelReplay();
            UIManager.Instance.ClosePanel<AdventureReplayView>();
        }

        public void SetSpeed(float speed)
        {
            currentSpeed = Mathf.Max(0.1f, speed);
            UpdateSpeedButtonDisplay(currentSpeed);
        }

        /// <summary>튜토리얼(9-B): 리플레이 스킵 불가 오버라이드. 재생 동안 스킵 버튼을 숨겨 끝까지 시청하게 한다.</summary>
        public void SetTutorialSkipDisabled(bool disabled)
        {
            if (skipButton != null) skipButton.gameObject.SetActive(!disabled);
        }

        public void Skip()
        {
            if (skipRequested) return;
            skipRequested = true;
            if (skipButton != null) skipButton.interactable = false;

            // 잔여 연출은 4배속으로 처리하되, 사용자가 고른 배속과 달라지므로 표시는 갱신하지 않는다.
            // 배속 버튼은 여기서 숨긴다 — 곧 EnterRecallMode에서 숨겨질 버튼이라 "x4"가 잠깐 보였다 사라지고,
            // 스킵 중 클릭하면 4f 기준으로 순환해 엉뚱한 배속이 저장된다.
            if (currentSpeed < 4f)
                currentSpeed = 4f;
            if (speedButton != null) speedButton.gameObject.SetActive(false);

            // StopCoroutine은 코루틴만 멈추고 DOTween 트윈은 독립 실행됨 → 먼저 명시적으로 정리
            if (bossImpactCoroutine != null) { StopCoroutine(bossImpactCoroutine); bossImpactCoroutine = null; }
            DOTween.Kill(mapScrollRect);
            if (bossImpactOverlay != null) { DOTween.Kill(bossImpactOverlay); bossImpactOverlay.alpha = 0f; }
            if (protectionChangeText != null) { DOTween.Kill(protectionChangeText); protectionChangeText.alpha = 0f; }
            // RewardFlyAnimator는 별도 MonoBehaviour이므로 StopCoroutine으로 멈추지 않음
            rewardFlyAnimator?.CancelAll();
            EffectManager.Instance?.StopAllParticles();
            SoundManager.Instance?.StopAllSFX();            // 노드 연출음(BossImpact 4초 등)의 잔향이 결과 화면까지 끌리지 않도록
            MonsterStageRenderer.ReplayInstance?.Clear();   // 중단된 라이브 몬스터 정리 (SkipToEnd가 묘비/이벤트 아이콘으로 재설정)

            if (sequenceCoroutine != null)
                StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(SkipToEnd());
        }

        public void EnterRecallMode()
        {
            isReplayFinished = true;
            SetScrollsInteractable(true);
            // 배속 버튼은 숨기고, 스킵 버튼을 결과 보기 역할로 재활성화
            if (speedButton != null) speedButton.gameObject.SetActive(false);
            if (skipButton != null) skipButton.interactable = true;
            if (completedIndicator != null) completedIndicator.SetActive(true);
            eventResultTooltip?.Hide();

            foreach (var marker in trackMarkers)
                marker?.SetInteractable(true);

            foreach (var marker in mapNodeMarkers)
            {
                if (marker.Result == null) continue;
                var captured = marker;
                int retryCount = captured.Result.eventType == DungeonEventType.Boss
                    ? GetBossRetryCount(captured.Result) : 0;
                marker.SetClickCallback(() => eventResultTooltip?.Show(captured.Result, retryCount));
            }
        }

        #endregion

        #region 시퀀스

        private IEnumerator PlayNodeSequence(AdventureInstance adventure, Action onComplete)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            // 레이아웃 확정 후 캐릭터 정위치 저장 → 화면 왼쪽 밖으로 이동 → 활성화
            var charRoot = characterRoot ?? appearanceApplier?.GetComponent<RectTransform>();
            if (charRoot != null)
            {
                characterNormalPos = charRoot.anchoredPosition;
                charRoot.anchoredPosition = new Vector2(
                    characterNormalPos.x - (motionConfig?.exitMoveDistance ?? 800f),
                    characterNormalPos.y);
                charRoot.gameObject.SetActive(true);
            }

            // 캐릭터가 활성화된 뒤에 무기 장착 — 진행 카드와 동일하게 손에 정상 무기가 입혀짐
            EquipReplayWeapon(adventure);

            yield return StartCoroutine(PlayEntryAnimation());
            AppendStartTrackMarker();

            // Entrance 제외한 (이벤트, 결과) 쌍 구성 — mapNodeMarkers 인덱스와 1:1 대응
            var allEvents = adventure.progress.events;
            var replayNodes = new List<(AdventureEvent evt, EventResult result)>();
            for (int i = 0; i < allEvents.Count; i++)
            {
                if (allEvents[i].eventData?.eventType == DungeonEventType.Entrance) continue;
                replayNodes.Add((allEvents[i], allEvents[i].result));
            }

            // 실제 결과가 있는 노드까지만 재생 (사망/후퇴 시)
            int replayCount = 0;
            foreach (var (_, r) in replayNodes)
                if (r != null) replayCount++;
            if (replayCount == 0) replayCount = replayNodes.Count;

            // 초기 보호 횟수: 출발 시점에 저장된 값(무기 효과+특성+포션 합산)을 사용한다.
            int charges = adventure.initialProtectionCharges;
            UpdateProtectionDisplay(charges);

            int markerIdx = -1;
            for (int i = 0; i < replayCount; i++)
            {
                var (evt, result) = replayNodes[i];
                DungeonEventType eventType = evt.eventData?.eventType ?? DungeonEventType.Battle;
                Sprite eventIcon = IconManager.Instance.GetIconByEventType(eventType);
                bool isCombatEvent = eventType == DungeonEventType.Battle ||
                                     eventType == DungeonEventType.MiniBoss ||
                                     eventType == DungeonEventType.Boss;

                // Group C(상태 표지): 마커 없음, 부수 효과는 직전 전투에서 처리됨
                if (IsGroupC(eventType)) continue;

                // 재도전: 직전 이벤트가 Protection/Retry → 같은 마커 사용
                bool isReAttempt = false;
                if (markerIdx >= 0 && i > 0 && replayNodes[i - 1].evt.eventData != null)
                {
                    isReAttempt = IsReAttemptOf(
                        replayNodes[i - 1].evt.eventData.eventType, eventType);
                }

                if (!isReAttempt)
                {
                    markerIdx++;
                    // 전투 노드는 달려가기 전부터 라이브 몬스터를 idle로 띄운다(도착 전 미리보기)
                    if (isCombatEvent && markerIdx < mapNodeMarkers.Count)
                        mapNodeMarkers[markerIdx].ShowLiveMonster(currentSpeed);
                    yield return StartCoroutine(RunToNode(markerIdx));
                    pendingTrackMarker = AppendTrackMarker(eventType, eventIcon);
                    int capturedIdx = markerIdx;
                    pendingTrackMarker?.SetClickCallback(() => ScrollMapToNode(capturedIdx));
                    pendingTrackMarker?.SetInteractable(false);
                    SetTrackFocus(markerIdx);
                    mapScrollSyncEnabled = true;
                }
                else if (mapScrollRect != null && motionConfig != null)
                {
                    // 재도전: 밀렸던 맵을 보스 노드 위치로 복귀
                    SoundManager.Instance?.PlaySFX(motionConfig.bossRetrySfxKey);
                    float reEngageDur = motionConfig.retreatDuration / currentSpeed;
                    var bossNodeRT = markerIdx < mapNodeMarkers.Count
                        ? mapNodeMarkers[markerIdx].GetComponent<RectTransform>() : null;
                    if (bossNodeRT != null)
                        yield return mapScrollRect
                            .DOHorizontalNormalizedPos(GetNodeNormalizedX(bossNodeRT, InteractionAnchor), reEngageDur)
                            .SetEase(Ease.InQuad)
                            .SetLink(gameObject)
                            .WaitForCompletion();
                }

                if (eventType == DungeonEventType.Boss && !isReAttempt)
                {
                    bossImpactCoroutine = StartCoroutine(PlayBossImpact(markerIdx));
                    yield return bossImpactCoroutine;
                    bossImpactCoroutine = null;
                }

                var nodeFX = motionConfig?.GetNodeFX(eventType);

                // 전투 노드는 무기 타입별 공격 애니(진행 카드와 동일), 비전투는 이벤트 기본 모션
                string nodeAnim = isCombatEvent
                    ? CombatAttackAnimName(eventType)
                    : (motionConfig != null ? motionConfig.GetAnimationName(eventType) : "Idle");

                // 원거리 무기(활/석궁): 발사 애니는 프레임 0이 '이미 당겨진 상태'라 바로 쏘고 끝에서 당긴다.
                // → 당기는 구간(rangedDrawStartTime)부터 loop 재생해 wrap 시키면 '당김 → 발사' 순서가 된다.
                //   한 사이클 뒤 2번째 발사로 넘어가기 전에 조준 Idle로 교체해 정지시킨다.
                string rangedAim = isCombatEvent ? RangedAimAnimName() : null;
                bool isRangedCombat = rangedAim != null && motionConfig != null;
                float motionDur;
                float fxDelay;   // 공격 애니 시작 후 이펙트가 나갈 때까지 지연(실제 타격 타이밍)
                if (isRangedCombat)
                {
                    PlayCharacterAnimation(nodeAnim, loop: true, startTime: motionConfig.rangedDrawStartTime);
                    motionDur = motionConfig.rangedAttackDuration / currentSpeed;
                    fxDelay = motionConfig.rangedEffectDelay / currentSpeed;
                }
                else
                {
                    // 전투 공격은 노드당 1회만 재생 — 루프를 켜면 motionDur 동안 공격이 반복됨
                    PlayCharacterAnimation(nodeAnim, loop: !isCombatEvent);
                    motionDur = (motionConfig != null ? motionConfig.GetDuration(eventType) : 1f) / currentSpeed;
                    // 근접 전투는 스윙이 닿는 순간에 이펙트, 비전투는 지연 없음(기존 동작 유지)
                    fxDelay = isCombatEvent && motionConfig != null ? motionConfig.meleeEffectDelay / currentSpeed : 0f;
                }

                // 공격 이펙트를 실제 타격 타이밍에 맞춰 지연 재생 (전체 대기 = motionDur 유지)
                fxDelay = Mathf.Clamp(fxDelay, 0f, motionDur);
                if (fxDelay > 0f) yield return new WaitForSeconds(fxDelay);
                if (markerIdx < mapNodeMarkers.Count)
                    EffectManager.Instance?.PlayReplayNodeEffect(nodeFX, mapNodeMarkers[markerIdx].EffectTransform);
                yield return new WaitForSeconds(motionDur - fxDelay);

                if (isRangedCombat)
                    PlayCharacterAnimation(rangedAim, loop: true);   // 재장전 재발사 방지 — 조준 자세로 고정

                // 일반 전투 실패 + Protection 발동: 재도전 없음, Hit 후 즉시 X 표시
                bool protectionConsumed = isCombatEvent && result != null && !result.isSuccess &&
                    i + 1 < replayNodes.Count &&
                    replayNodes[i + 1].evt.eventData?.eventType == DungeonEventType.Protection;
                // Boss 실패 + Retry 진행: 재도전 있음, 결과 보류
                bool bossRetry = isCombatEvent && result != null && !result.isSuccess &&
                    eventType == DungeonEventType.Boss && result.protectionActivated;

                // 전투 실패 시 Hit 애니메이션
                if (isCombatEvent && result != null && !result.isSuccess && motionConfig != null)
                {
                    PlayCharacterAnimation(motionConfig.hitAnimationName, loop: false);
                    yield return new WaitForSeconds(motionConfig.hitAnimationDuration / currentSpeed);

                    // Boss Retry: 캐릭터 대신 맵을 왼쪽으로 밀어 피격 후퇴 표현
                    if (bossRetry && mapScrollRect != null && mapContainer != null)
                    {
                        float retreatDur = motionConfig.retreatDuration / currentSpeed;
                        var vp = mapScrollRect.viewport ?? (RectTransform)mapScrollRect.transform;
                        float scrollable = mapContainer.rect.width - vp.rect.width;
                        float delta = scrollable > 0f ? motionConfig.retreatDistance / scrollable : 0f;
                        float retreatPos = Mathf.Clamp01(mapScrollRect.horizontalNormalizedPosition - delta);
                        yield return mapScrollRect
                            .DOHorizontalNormalizedPos(retreatPos, retreatDur)
                            .SetEase(Ease.OutQuad)
                            .SetLink(gameObject)
                            .WaitForCompletion();
                    }
                }

                // 결과 표시 — Boss Retry만 보류, Protection 소비는 즉시 X 표시
                if (result != null && markerIdx < mapNodeMarkers.Count && !bossRetry)
                {
                    mapNodeMarkers[markerIdx].SetResult(result.isSuccess, isCombatEvent);
                    mapNodeMarkers[markerIdx].StoreResult(result);

                    string resultSfx = result.isSuccess ? nodeFX?.successSfxKey : nodeFX?.failSfxKey;
                    if (!string.IsNullOrEmpty(resultSfx))
                        SoundManager.Instance?.PlaySFX(resultSfx);

                    if (isCombatEvent)
                    {
                        ConfirmTrackMarker(pendingTrackMarker, result.isSuccess);
                        // 승리=몬스터 사망, 패배=몬스터가 마지막 일격
                        if (result.isSuccess) mapNodeMarkers[markerIdx].PlayMonsterDead();
                        else                  mapNodeMarkers[markerIdx].PlayMonsterAttack();
                    }
                }

                // 보호 처리: Rest 충전 / 전투 실패 + 보호 발동 시 차감
                if (result != null)
                {
                    if (result.eventType == DungeonEventType.Rest)
                    {
                        int maxCharges = ConfigManager.Instance.Adventure.maxProtectionCharges;
                        int prev = charges;
                        charges = Mathf.Min(charges + 1, maxCharges);
                        if (charges > prev)
                        {
                            ShowProtectionChange(+1);
                            SoundManager.Instance?.PlaySFX(motionConfig?.protectionActivatedSfxKey ?? "");
                        }
                    }
                    else if (bossRetry || protectionConsumed)
                    {
                        charges = Mathf.Max(0, charges - 1);
                        ShowProtectionChange(-1);
                        SoundManager.Instance?.PlaySFX(motionConfig?.protectionActivatedSfxKey ?? "");
                    }

                    UpdateProtectionDisplay(charges);
                }

                // 보상 비행 — Boss Retry만 보류
                if (result != null && !bossRetry)
                    yield return StartCoroutine(PlayRewardFly(markerIdx, result));

                // 전투 노드 정리(다음 노드 스폰 전 공유 RT 반납): 처치=묘비, 보호 생존=idle 스냅샷 고정.
                // 사망/후퇴로 종료되는 노드는 라이브 몬스터를 그대로 유지한다.
                if (isCombatEvent && result != null && !bossRetry && markerIdx < mapNodeMarkers.Count)
                {
                    if (result.isSuccess)        mapNodeMarkers[markerIdx].ShowTombstone();
                    else if (protectionConsumed) mapNodeMarkers[markerIdx].ShowMonsterIdleSnapshot();
                }

                float nodeDelay = (motionConfig != null ? motionConfig.nodeDelay : 0.2f) / currentSpeed;
                yield return new WaitForSeconds(nodeDelay);
            }

            // 보너스 비행 (대성공/아이템 보너스 등 EventResult에 없는 잔여 보상)
            yield return StartCoroutine(PlayBonusFly(adventure));

            AppendEndTrackMarker(adventure);

            // 퇴장 연출 진입 후 스킵을 받으면 SkipToEnd가 퇴장을 다시 재생(SFX 중복)하므로 여기서 차단.
            // 직후 onComplete -> EnterRecallMode에서 결과 보기 버튼으로 다시 활성화된다.
            if (skipButton != null) skipButton.interactable = false;

            yield return StartCoroutine(PlayExitAnimation(adventure));

            onComplete?.Invoke();
        }

        // Run 애니메이션과 맵 스크롤을 동시에 실행
        private IEnumerator RunToNode(int nodeIndex)
        {
            if (mapScrollRect == null || nodeIndex >= mapNodeMarkers.Count) yield break;

            mapScrollSyncEnabled = false;
            float scrollDur = (motionConfig != null ? motionConfig.scrollDuration : 0.4f) / currentSpeed;

            PlayCharacterAnimation(RunAnimName());
            SoundManager.Instance?.PlaySFX(motionConfig?.runSfxKey ?? "");

            var nodeRT = mapNodeMarkers[nodeIndex].GetComponent<RectTransform>();
            yield return mapScrollRect
                .DOHorizontalNormalizedPos(GetNodeNormalizedX(nodeRT, InteractionAnchor), scrollDur)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject)
                .WaitForCompletion();
        }

        private IEnumerator PlayBossImpact(int nodeIndex)
        {
            if (motionConfig == null || nodeIndex >= mapNodeMarkers.Count) yield break;

            float impactDur = motionConfig.bossImpactDuration / currentSpeed;
            SoundManager.Instance?.PlaySFX(motionConfig.bossImpactSfxKey);

            if (bossImpactOverlay != null)
            {
                bossImpactOverlay.alpha = 0f;
                bossImpactOverlay.DOFade(motionConfig.bossDarkAlpha, impactDur * 0.3f).SetLink(gameObject);
            }

            yield return new WaitForSeconds(impactDur);

            bossImpactOverlay?.DOFade(0f, impactDur * 0.3f).SetLink(gameObject);

            yield return new WaitForSeconds(impactDur * 0.3f);
        }

        private IEnumerator SkipToEnd()
        {
            if (currentAdventure == null) yield break;

            if (connectorLayer != null)
                foreach (Transform child in connectorLayer) Destroy(child.gameObject);
            if (markerLayer != null)
                foreach (Transform child in markerLayer) Destroy(child.gameObject);
            trackMarkers.Clear();
            yield return null;
            AppendStartTrackMarker();

            var allEvents = currentAdventure.progress.events;

            var replayNodes = new List<(AdventureEvent evt, EventResult result)>();
            for (int i = 0; i < allEvents.Count; i++)
            {
                if (allEvents[i].eventData?.eventType == DungeonEventType.Entrance) continue;
                replayNodes.Add((allEvents[i], allEvents[i].result));
            }

            int replayCount = 0;
            foreach (var (_, r) in replayNodes)
                if (r != null) replayCount++;
            if (replayCount == 0) replayCount = replayNodes.Count;

            int markerIdx = -1;

            for (int i = 0; i < replayCount; i++)
            {
                var (evt, result) = replayNodes[i];
                DungeonEventType eventType = evt.eventData?.eventType ?? DungeonEventType.Battle;
                Sprite eventIcon = IconManager.Instance.GetIconByEventType(eventType);

                if (IsGroupC(eventType)) continue;

                bool isReAttempt = markerIdx >= 0 && i > 0 && replayNodes[i - 1].evt.eventData != null &&
                    IsReAttemptOf(replayNodes[i - 1].evt.eventData.eventType, eventType);

                if (!isReAttempt)
                {
                    markerIdx++;
                    pendingTrackMarker = AppendTrackMarker(eventType, eventIcon);
                    int capturedIdx = markerIdx;
                    pendingTrackMarker?.SetClickCallback(() => ScrollMapToNode(capturedIdx));
                    pendingTrackMarker?.SetInteractable(false);
                }

                bool isCombatEvent = eventType == DungeonEventType.Battle ||
                                     eventType == DungeonEventType.MiniBoss ||
                                     eventType == DungeonEventType.Boss;

                bool bossRetry = isCombatEvent && result != null && !result.isSuccess &&
                    eventType == DungeonEventType.Boss && result.protectionActivated;

                // 보호 생존 패배: 다음 이벤트가 Protection이면 이 전투는 보호로 살아남은 패배 (자연 재생과 동일 판정)
                bool protectionConsumed = isCombatEvent && result != null && !result.isSuccess &&
                    i + 1 < replayNodes.Count &&
                    replayNodes[i + 1].evt.eventData?.eventType == DungeonEventType.Protection;

                if (result != null && markerIdx < mapNodeMarkers.Count && !bossRetry)
                {
                    mapNodeMarkers[markerIdx].SetResult(result.isSuccess, isCombatEvent);
                    mapNodeMarkers[markerIdx].StoreResult(result);

                    if (isCombatEvent)
                    {
                        ConfirmTrackMarker(pendingTrackMarker, result.isSuccess);
                        // 스킵: 라이브 연출 없이 최종 상태로 — 처치=묘비, 패배=몬스터 유지(자연 재생과 동일).
                        // 스킵은 라이브 몬스터를 안 띄우므로 패배 노드에서 여기서 스폰한다.
                        if (result.isSuccess) mapNodeMarkers[markerIdx].ShowTombstone();
                        else
                        {
                            mapNodeMarkers[markerIdx].ShowLiveMonster(currentSpeed);
                            // 보호 생존 노드는 공유 RT를 다음 노드에 넘겨야 하므로 스냅샷으로 고정.
                            // 스폰 직후 프레임은 아직 렌더/포즈가 반영되지 않아(빈 스냅샷) 한 프레임 대기한다.
                            // 대기 중에 직전 노드의 지연 Destroy도 완료되어 스냅샷이 겹치지 않는다.
                            if (protectionConsumed)
                            {
                                yield return null;
                                mapNodeMarkers[markerIdx].ShowMonsterIdleSnapshot();
                            }
                            // 종착 사망/후퇴 노드는 이후 스폰이 없으므로 라이브 몬스터를 그대로 둔다.
                        }
                    }
                }
                else if (isReAttempt && result != null && markerIdx >= 0 && markerIdx < mapNodeMarkers.Count)
                {
                    mapNodeMarkers[markerIdx].SetResult(result.isSuccess, isCombatEvent);
                    mapNodeMarkers[markerIdx].StoreResult(result);
                    if (isCombatEvent)
                    {
                        // 재도전 노드의 패배는 곧 종착(사망) — 자연 재생과 동일하게 몬스터를 남긴다.
                        if (result.isSuccess) mapNodeMarkers[markerIdx].ShowTombstone();
                        else                  mapNodeMarkers[markerIdx].ShowLiveMonster(currentSpeed);
                    }
                }
            }

            AppendEndTrackMarker(currentAdventure);

            if (markerIdx >= 0 && markerIdx < mapNodeMarkers.Count && mapScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                mapScrollRect.horizontalNormalizedPosition = GetNodeNormalizedX(
                    mapNodeMarkers[markerIdx].GetComponent<RectTransform>());
            }

            mapScrollSyncEnabled = true;
            if (markerIdx >= 0) SetTrackFocus(markerIdx);
            UpdateProtectionDisplay(currentAdventure.protectionCharges);

            displayedGold = currentAdventure.accumulatedGold + currentAdventure.accumulatedBonusGold;
            UpdateAccumulatedGoldDisplay();

            materialCounts.Clear();

            var adventureMats = AggregateMaterials(currentAdventure.accumulatedMaterials);
            foreach (var bonusMat in currentAdventure.accumulatedBonusMaterials ?? new List<MaterialInstance>())
            {
                if (bonusMat == null) continue;
                adventureMats.TryGetValue(bonusMat.materialDataID, out int prev);
                adventureMats[bonusMat.materialDataID] = prev + bonusMat.quantity;
            }

            foreach (var kvp in adventureMats)
                materialCounts[kvp.Key] = kvp.Value;
            UpdateMaterialCountDisplay();

            var charRoot = characterRoot ?? appearanceApplier?.GetComponent<RectTransform>();
            if (charRoot != null)
            {
                DOTween.Kill(charRoot);
                charRoot.anchoredPosition = characterNormalPos;
                charRoot.gameObject.SetActive(true);
            }

            // 퇴장 애니메이션과 동시에 리콜 진입 + 결과 이동 팝업 표시 (OnReplayCompleted가 담당)
            var onComplete = storedOnComplete;
            storedOnComplete = null;
            onComplete?.Invoke();

            yield return StartCoroutine(PlayExitAnimation(currentAdventure));
        }

        private IEnumerator PlayEntryAnimation()
        {
            if (appearanceApplier == null || motionConfig == null) yield break;

            var root = characterRoot ?? appearanceApplier.GetComponent<RectTransform>();
            if (root == null) yield break;

            float entryDur = motionConfig.entryAnimationDuration / currentSpeed;
            PlayCharacterAnimation(motionConfig.entryAnimationName, loop: true);
            SoundManager.Instance?.PlaySFX(motionConfig.entrySfxKey);

            // WaitForCompletion: 트윈이 실제로 끝난 뒤 다음 단계 진행 (WaitForSeconds는 타이밍 오차 발생)
            yield return root.DOAnchorPosX(characterNormalPos.x, entryDur)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .WaitForCompletion();
        }

        private IEnumerator PlayExitAnimation(AdventureInstance adventure)
        {
            if (appearanceApplier == null || motionConfig == null) yield break;

            float exitDur = motionConfig.exitAnimationDuration / currentSpeed;
            var root = characterRoot ?? appearanceApplier.GetComponent<RectTransform>();

            if (adventure.isDeath)
            {
                PlayCharacterAnimation(motionConfig.exitDeathAnimationName, loop: false);
                yield return new WaitForSeconds(exitDur);
                if (root != null) root.anchoredPosition = characterNormalPos;
                root?.gameObject.SetActive(false);
            }
            else
            {
                PlayCharacterAnimation(motionConfig.exitLeaveAnimationName, loop: true);
                SoundManager.Instance?.PlaySFX(motionConfig.exitSuccessSfxKey);
                root?.DOAnchorPosX(root.anchoredPosition.x + motionConfig.exitMoveDistance, exitDur)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        if (root != null) root.anchoredPosition = characterNormalPos;
                        root.gameObject.SetActive(false);
                    })
                    .SetLink(gameObject);
                yield return new WaitForSeconds(exitDur);
            }
        }

        #endregion

        #region 내부 메서드

        private void UpdateHeader(AdventureInstance adventure)
        {
            if (adventure == null) return;

            if (adventure.adventurer?.appearance != null)
            {
                appearanceApplier?.ApplyAppearance(adventure.adventurer.appearance);
                portraitApplier?.ApplyAppearance(adventure.adventurer.appearance);
                // 본체 무기 장착은 PlayNodeSequence에서 캐릭터 활성화 직후에 수행한다.
                // (여기서 장착하면 직후 SetActive(false) 토글로 gear_right attachment가 리셋됨)
            }

            if (adventurerNameText != null)
                adventurerNameText.text = adventure.adventurer?.Name ?? "";
            if (adventurerGradeText != null)
                adventurerGradeText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Common",
                    adventure.adventurer != null && adventure.adventurer.isNamed
                        ? "AdventurerGrade_Named" : "AdventurerGrade_Normal");
            if (dungeonNameText != null)
                dungeonNameText.text = adventure.dungeon?.DisplayName ?? "";

            // 무기 아이콘 슬롯(버튼/아이콘/배경/프레임)은 기본무기여도 표시 — 아이콘만 기본무기 전용 스프라이트로 교체.
            // 이름/강화수치는 기본무기일 때 숨긴다.
            var weapon = adventure.weapon;
            bool isDefaultWeapon = adventure.isUsingDefaultWeapon;

            if (weaponNameText != null) weaponNameText.gameObject.SetActive(!isDefaultWeapon);
            if (weaponEnforceText != null) weaponEnforceText.gameObject.SetActive(!isDefaultWeapon);

            // 궁합 슬라이더: 기본무기는 숨김, 대여무기는 무기 타입 전체 적합도(0~1) 표시
            if (matchSlider != null)
            {
                var adventurer = adventure.adventurer;
                if (isDefaultWeapon || weapon?.weaponData == null || adventurer == null)
                    matchSlider.gameObject.SetActive(false);
                else
                {
                    matchSlider.gameObject.SetActive(true);
                    float bestScore = adventurer.GetStatScore(adventurer.GetBestWeaponType());
                    float score = adventurer.GetStatScore(weapon.weaponData.weaponType);
                    matchSlider.value = bestScore > 0f ? Mathf.Clamp01(score / bestScore) : 0f;
                }
            }

            if (weapon != null)
            {
                if (weaponIconImage != null)
                {
                    if (isDefaultWeapon)
                        weaponIconImage.sprite = IconManager.Instance.GetDefaultWeaponIcon();
                    else if (weapon.weaponData?.icon != null)
                        weaponIconImage.sprite = weapon.weaponData.icon;
                }
                if (weaponBG != null)
                    weaponBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(weapon.currentGrade);
                if (weaponFrame != null)
                    weaponFrame.sprite = IconManager.Instance.GetFrameByGrade(weapon.currentGrade);
                if (!isDefaultWeapon)
                {
                    if (weaponNameText != null)
                        weaponNameText.text = $"{weapon.weaponData?.DisplayName ?? ""}";
                    if (weaponEnforceText != null)
                        weaponEnforceText.text = $"+{weapon.enforceLevel}";
                }
            }

            if (armorTypeTitleFrame != null)
                armorTypeTitleFrame.sprite = IconManager.Instance.GetTitleFrameByArmorType(adventure.effectiveArmorType);

            var adv = adventure.adventurer;
            bool showTrait = adv != null && (adv.isTraitRevealed || !adv.isNamed);
            traitRoot?.SetActive(showTrait);
            if (showTrait && traitText != null)
                traitText.text = UITranslator.GetString(adv.Trait);

            // 기본무기면 무기효과 목록도 숨긴다.
            if (weaponEffectContainer != null)
                weaponEffectContainer.gameObject.SetActive(!isDefaultWeapon);
            UpdateWeaponEffectList(isDefaultWeapon ? null : adventure.weapon);

            // 보호 표시는 시퀀스에서 초기값 계산 후 갱신 — 여기서는 숨김 처리
            protectionRoot?.SetActive(false);
        }

        private void UpdateWeaponEffectList(WeaponInstance weapon)
        {
            if (weaponEffectContainer == null || weaponEffectItemPrefab == null) return;
            foreach (Transform child in weaponEffectContainer)
                Destroy(child.gameObject);
            if (weapon?.effects == null) return;

            // 모험이 끝난 상태라 던전·방어타입이 확정 → 던전에서 활성화된 효과만 강조(비활성은 흐리게)
            foreach (var effect in weapon.effects)
            {
                var state = AdventureManager.Instance.GetEffectDisplayState(
                    effect, currentAdventure?.adventurer, currentAdventure?.dungeon,
                    currentAdventure?.effectiveArmorType ?? ArmorType.Unarmored);
                var item = Instantiate(weaponEffectItemPrefab, weaponEffectContainer);
                item.Initialize(effect, displayState: state);
            }
        }

        private void UpdateProtectionDisplay(int charges)
        {
            if (charges > 0)
                protectionRoot?.SetActive(true);
            if (protectionCountText != null && protectionRoot.activeInHierarchy)
                protectionCountText.text = $"{charges}";
        }

        private void ShowProtectionChange(int delta)
        {
            if (protectionChangeText == null) return;

            DOTween.Kill(protectionChangeText);

            protectionChangeText.text = delta > 0 ? $"+{delta}" : $"{delta}";
            protectionChangeText.color = delta > 0
                ? ColorManager.Instance.GetGreenColor()
                : ColorManager.Instance.GetRedColor();

            protectionChangeText.DOFade(0f, 0.8f / currentSpeed)
                .SetDelay(0.3f / currentSpeed)
                .From(1f)
                .SetLink(gameObject);
        }

        private void InitializeMap(AdventureInstance adventure)
        {
            if (mapContainer == null || mapTilePrefab == null || nodeMarkerPrefab == null) return;

            foreach (Transform child in mapContainer)
                Destroy(child.gameObject);

            mapNodeMarkers.Clear();

            // 마커 대상 필터링: Entrance + 상태 노드(Group C) + 재도전 노드 제외
            // Group C: Protection, Retry, Retreat, Return — 시각적 노드가 아닌 상태 표지
            // 재도전: Protection 직후 Battle/MiniBoss, Retry 직후 Boss — 직전 노드와 같은 위치
            var allEvents = adventure.progress.events;
            var events = new List<AdventureEvent>();
            for (int idx = 0; idx < allEvents.Count; idx++)
            {
                var data = allEvents[idx].eventData;
                if (data == null) continue;
                if (data.eventType == DungeonEventType.Entrance) continue;
                // Retreat/Return 이후는 모험 종료 — 미래 노드를 맵에 표시하지 않음
                if (data.eventType == DungeonEventType.Retreat ||
                    data.eventType == DungeonEventType.Return) break;
                if (IsGroupC(data.eventType)) continue;

                if (idx > 0 && allEvents[idx - 1].eventData != null &&
                    IsReAttemptOf(allEvents[idx - 1].eventData.eventType, data.eventType))
                    continue;

                events.Add(allEvents[idx]);
            }

            if (events.Count == 0) return;

            int nodeIndex = 0;
            nodeIndex = SpawnStartTile(nodeIndex, events, adventure);

            while (nodeIndex < events.Count)
                nodeIndex = SpawnTile(mapTilePrefab, nodesPerTile, nodeIndex, events, adventure);

            if (mapScrollRect != null)
                mapScrollRect.horizontalNormalizedPosition = 0f;
        }

        private int SpawnStartTile(int startIndex, List<AdventureEvent> events, AdventureInstance adventure)
        {
            var tile = Instantiate(mapStartTilePrefab ?? mapTilePrefab, mapContainer);
            var le = tile.GetComponent<LayoutElement>() ?? tile.AddComponent<LayoutElement>();
            le.preferredWidth = tileWidth;
            le.minWidth = tileWidth;

            if (tile.GetComponent<Image>() is Image tileImage && adventure.dungeon?.mapBackground != null)
                tileImage.sprite = adventure.dungeon.mapBackground;

            float unit = tileWidth / nodesPerTile;
            int end = Mathf.Min(startIndex + startNodesPerTile, events.Count);
            for (int i = startIndex; i < end; i++)
            {
                int posInTile = i - startIndex;
                var go = Instantiate(nodeMarkerPrefab, tile.transform);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(unit * (posInTile + 1.5f), nodeYOffset);
                var marker = go.GetComponent<NodeMarker>();
                marker?.SetEvent(events[i].eventData);
                if (marker != null) mapNodeMarkers.Add(marker);
            }
            return end;
        }

        private int SpawnTile(GameObject prefab, int capacity, int startIndex, List<AdventureEvent> events, AdventureInstance adventure)
        {
            var tile = Instantiate(prefab, mapContainer);
            var le = tile.GetComponent<LayoutElement>() ?? tile.AddComponent<LayoutElement>();
            le.preferredWidth = tileWidth;
            le.minWidth = tileWidth;

            if (tile.GetComponent<Image>() is Image tileImage && adventure.dungeon?.mapBackground != null)
                tileImage.sprite = adventure.dungeon.mapBackground;

            float unit = tileWidth / capacity;
            int end = Mathf.Min(startIndex + capacity, events.Count);
            for (int i = startIndex; i < end; i++)
            {
                int posInTile = i - startIndex;
                var go = Instantiate(nodeMarkerPrefab, tile.transform);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(unit * (posInTile + 0.5f), nodeYOffset);
                var marker = go.GetComponent<NodeMarker>();
                marker?.SetEvent(events[i].eventData);
                if (marker != null) mapNodeMarkers.Add(marker);
            }
            return end;
        }

        private void InitializeResultTrack()
        {
            if (connectorLayer != null)
                foreach (Transform child in connectorLayer) Destroy(child.gameObject);
            if (markerLayer != null)
                foreach (Transform child in markerLayer) Destroy(child.gameObject);

            trackMarkers.Clear();
        }

        private void AppendStartTrackMarker()
        {
            if (markerLayer == null || motionConfig?.startTrackMarkerPrefab == null) return;
            Instantiate(motionConfig.startTrackMarkerPrefab, markerLayer);
            ScrollResultTrackToEnd();
        }

        private void AppendConnector()
        {
            if (connectorLayer == null || motionConfig?.connectorTrackPrefab == null) return;
            Instantiate(motionConfig.connectorTrackPrefab, connectorLayer);
        }

        private TrackMarker AppendTrackMarker(DungeonEventType eventType, Sprite icon = null)
        {
            if (connectorLayer == null || markerLayer == null || motionConfig == null) return null;
            AppendConnector();

            bool isBoss   = eventType == DungeonEventType.Boss;
            bool isCombat = eventType == DungeonEventType.Battle || eventType == DungeonEventType.MiniBoss;

            GameObject prefab;
            if (isBoss)
                prefab = motionConfig.bossTrackMarkerPrefab ?? motionConfig.defaultTrackMarkerPrefab;
            else if (isCombat)
                prefab = motionConfig.defaultTrackMarkerPrefab;
            else
                prefab = motionConfig.GetTrackMarkerConfig(eventType).prefab;

            if (prefab == null) return null;

            var go = Instantiate(prefab, markerLayer);
            var marker = go.GetComponent<TrackMarker>();
            if (marker == null) { Destroy(go); return null; }

            trackMarkers.Add(marker);
            marker.SetIcon(icon);

            if (isCombat || isBoss)
                marker.SetPending();
            else
                marker.SetColor(motionConfig.GetTrackMarkerConfig(eventType).color);

            ScrollResultTrackToEnd(withOverhang: true);
            return marker;
        }

        private void ConfirmTrackMarker(TrackMarker marker, bool isSuccess)
        {
            if (marker == null) return;
            marker.SetResult(isSuccess);
            ScrollResultTrackToEnd(withOverhang: true);
        }

        private void AppendEndTrackMarker(AdventureInstance adventure)
        {
            if (connectorLayer == null || markerLayer == null || motionConfig == null) return;
            AppendConnector();

            var prefab = (adventure.isDeath || adventure.isRetreated)
                ? motionConfig.retreatTrackMarkerPrefab
                : motionConfig.returnTrackMarkerPrefab;

            if (prefab == null) return;
            Instantiate(prefab, markerLayer);
            ScrollResultTrackToEnd();
        }


        private void ScrollResultTrackToEnd(bool withOverhang = false)
        {
            if (resultTrackScrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            SyncContentWidth(withOverhang);
            resultTrackScrollRect.horizontalNormalizedPosition = 1f;
        }

        private void SyncContentWidth(bool withOverhang = false)
        {
            if (markerLayer == null || resultTrackContainer == null) return;
            var contentRT = (RectTransform)resultTrackContainer;
            var layerRT   = (RectTransform)markerLayer;

            float overhang = 0f;
            if (withOverhang && trackMarkers.Count > 0)
            {
                var rt = trackMarkers[0]?.GetComponent<RectTransform>();
                if (rt != null) overhang = rt.rect.width * 0.25f;
            }

            contentRT.sizeDelta = new Vector2(layerRT.rect.width + overhang, contentRT.sizeDelta.y);
        }

        // 무기 대여 시 Run_Gear, 맨손이면 Run (카드 연출과 동일하게 달리기만 구분)
        private string RunAnimName()
        {
            bool gear = currentAdventure != null && !currentAdventure.isUsingDefaultWeapon;
            string name = gear ? motionConfig?.runGearAnimationName : motionConfig?.runAnimationName;
            return string.IsNullOrEmpty(name) ? "Run" : name;
        }

        // 캐릭터 활성화 후 본체에 대여 무기를 장착(맨손이면 미장착). 진행 카드와 동일한 경로.
        private void EquipReplayWeapon(AdventureInstance adventure)
        {
            var pm = appearanceApplier?.GetPartsManager();
            if (pm == null) return;
            if (adventure.isUsingDefaultWeapon)
                WeaponEquipUtil.Unequip(pm);
            else if (adventure.weapon?.weaponData != null)
                WeaponEquipUtil.Equip(pm, adventure.weapon.weaponData);
        }

        // 원거리 무기(활/석궁) 대여 시 조준 Idle 애니 이름, 근접/맨손이면 null.
        private string RangedAimAnimName()
        {
            if (currentAdventure == null || currentAdventure.isUsingDefaultWeapon
                || currentAdventure.weapon?.weaponData == null)
                return null;
            return WeaponEquipUtil.RangedIdleAnim(currentAdventure.weapon.weaponData.weaponType);
        }

        // 전투 노드 공격 애니메이션: 맨손이면 이벤트 기본 모션, 무기 대여면 무기 타입별 공격(진행 카드와 동일).
        private string CombatAttackAnimName(DungeonEventType eventType)
        {
            if (currentAdventure == null || currentAdventure.isUsingDefaultWeapon
                || currentAdventure.weapon?.weaponData == null)
                return motionConfig != null ? motionConfig.GetAnimationName(eventType) : "Idle";
            return WeaponEquipUtil.AttackAnim(currentAdventure.weapon.weaponData.weaponType);
        }

        // SkeletonAnimation(월드) 또는 SkeletonGraphic(UI Canvas) 모두 지원
        // startTime: 재생 시작 지점(TrackTime) — 원거리 발사 애니를 당기는 구간부터 시작할 때 사용
        private void PlayCharacterAnimation(string animationName, bool loop = true, float startTime = 0f)
        {
            var pm = appearanceApplier?.GetPartsManager();
            if (pm == null) return;

            var skAnim = pm.GetSkeletonAnimation();
            if (skAnim != null)
            {
                var entry = skAnim.AnimationState.SetAnimation(0, animationName, loop);
                if (entry != null) { entry.TimeScale = currentSpeed; if (startTime > 0f) entry.TrackTime = startTime; }
            }
            else
            {
                var sg = pm.GetSkeletonGraphic();
                if (sg == null) return;
                var entry = sg.AnimationState.SetAnimation(0, animationName, loop);
                if (entry != null) { entry.TimeScale = currentSpeed; if (startTime > 0f) entry.TrackTime = startTime; }
            }
        }

        // viewportAnchor: 노드를 뷰포트 가로 어느 비율에 맞출지 (0.5=중앙, 0.666=왼쪽에서 2/3)
        private float GetNodeNormalizedX(RectTransform nodeRT, float viewportAnchor = 0.5f)
        {
            if (mapContainer == null || mapScrollRect == null) return 0f;

            var viewport = mapScrollRect.viewport ?? (RectTransform)mapScrollRect.transform;

            Vector2 localPos = (Vector2)mapContainer.InverseTransformPoint(nodeRT.position);
            float nodeX = localPos.x - mapContainer.rect.xMin;

            float contentWidth = mapContainer.rect.width;
            float viewportWidth = viewport.rect.width;

            if (contentWidth <= viewportWidth) return 0f;

            float targetX = nodeX - viewportWidth * viewportAnchor;
            return Mathf.Clamp01(targetX / (contentWidth - viewportWidth));
        }

        // 배속 버튼 클릭 시 ×1 → ×2 → ×4 → ×1 순환 (TopBarView.timeButton과 동일한 방식)
        private void OnSpeedButtonClicked()
        {
            controller?.OnSpeedSelected(NextReplaySpeed(currentSpeed));
        }

        // 스킵 버튼: 재생 중엔 스킵, 재생 종료 후엔 결과창으로 이동 (resultButton 역할 겸함)
        private void OnSkipButtonClicked()
        {
            if (isReplayFinished) controller?.OnGoToResultClicked();
            else controller?.OnSkipClicked();
        }

        private static float NextReplaySpeed(float speed)
        {
            if (Mathf.Approximately(speed, 1f)) return 2f;
            if (Mathf.Approximately(speed, 2f)) return 4f;
            return 1f;
        }

        private void UpdateSpeedButtonDisplay(float speed)
        {
            if (speedScaleText != null)
                speedScaleText.text = $"x{speed:F0}";
        }

        private int GetBossRetryCount(EventResult bossResult)
        {
            var events = currentAdventure?.progress?.events;
            if (events == null) return 0;
            int idx = events.FindIndex(e => e.result == bossResult);
            if (idx < 0) return 0;
            int count = 0;
            for (int j = idx - 1; j >= 0; j--)
            {
                var t = events[j].result != null ? events[j].result.eventType : events[j].eventData.eventType;
                if (t == DungeonEventType.Retry) count++;
                else if (t == DungeonEventType.Boss) continue;
                else break;
            }
            return count;
        }

        private void ScrollMapToNode(int nodeIndex)
        {
            if (mapScrollRect == null || nodeIndex >= mapNodeMarkers.Count) return;
            var nodeRT = mapNodeMarkers[nodeIndex].GetComponent<RectTransform>();
            mapScrollSyncEnabled = false;
            mapScrollRect.DOHorizontalNormalizedPos(GetNodeNormalizedX(nodeRT), 0.4f)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    mapScrollSyncEnabled = true;
                    SetTrackFocus(nodeIndex);
                })
                .SetLink(gameObject);
        }

        private void UpdateFocusByScroll(Vector2 _)
        {
            if (!mapScrollSyncEnabled || mapNodeMarkers.Count == 0 || trackMarkers.Count == 0) return;

            var viewport = mapScrollRect.viewport ?? (RectTransform)mapScrollRect.transform;
            float viewportCenterX = viewport.TransformPoint(viewport.rect.center).x;

            float nodeSpacing = mapNodeMarkers.Count >= 2
                ? Mathf.Abs(mapNodeMarkers[1].transform.position.x - mapNodeMarkers[0].transform.position.x)
                : tileWidth / nodesPerTile;

            if (viewportCenterX < mapNodeMarkers[0].transform.position.x - nodeSpacing * 0.5f)
            {
                SetTrackFocus(-1);
                return;
            }

            float minDist = float.MaxValue;
            int closestIdx = -1;
            for (int i = 0; i < mapNodeMarkers.Count; i++)
            {
                float dist = Mathf.Abs(mapNodeMarkers[i].transform.position.x - viewportCenterX);
                if (dist < minDist) { minDist = dist; closestIdx = i; }
            }

            SetTrackFocus(closestIdx);
        }

        private void SetTrackFocus(int focusIndex)
        {
            for (int i = 0; i < trackMarkers.Count; i++)
                trackMarkers[i]?.SetFocus(i == focusIndex);

            ScrollResultTrackToMarker(focusIndex);
        }

        private void ScrollResultTrackToMarker(int markerIndex)
        {
            if (resultTrackScrollRect == null || markerIndex < 0 || markerIndex >= trackMarkers.Count) return;
            var content = resultTrackScrollRect.content;
            if (content == null) return;
            var markerRT = trackMarkers[markerIndex]?.GetComponent<RectTransform>();
            if (markerRT == null) return;

            var viewport = resultTrackScrollRect.viewport ?? (RectTransform)resultTrackScrollRect.transform;
            float markerX = content.InverseTransformPoint(markerRT.position).x - content.rect.xMin;
            float scrollable = content.rect.width - viewport.rect.width;
            if (scrollable <= 0f) return;

            float targetX = markerX - viewport.rect.width * 0.5f;
            resultTrackScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(targetX / scrollable);
        }

        private void SetScrollsInteractable(bool interactable)
        {
            if (mapScrollRect != null) mapScrollRect.enabled = interactable;
            if (resultTrackScrollRect != null) resultTrackScrollRect.enabled = interactable;
        }

        private static bool IsGroupC(DungeonEventType type) =>
            type == DungeonEventType.Protection ||
            type == DungeonEventType.Retry ||
            type == DungeonEventType.Retreat ||
            type == DungeonEventType.Return ||
            type == DungeonEventType.TrapEvade;

        private static bool IsReAttemptOf(DungeonEventType prev, DungeonEventType curr) =>
            prev == DungeonEventType.Retry && curr == DungeonEventType.Boss;

        private void InitializeAccumulatedInventory()
        {
            displayedGold = 0;
            materialCounts.Clear();
            UpdateAccumulatedGoldDisplay();
            UpdateMaterialCountDisplay();
        }

        private void UpdateAccumulatedGoldDisplay()
        {
            if (accumulatedGoldText != null)
                accumulatedGoldText.text = displayedGold.ToString("N0");
        }

        private void UpdateMaterialCountDisplay()
        {
            if (materialCountText == null) return;
            int types = 0;
            foreach (var count in materialCounts.Values)
                if (count > 0) types++;
            materialCountText.text = types.ToString("N0");
        }

        #endregion

        #region 보상 비행

        private IEnumerator PlayRewardFly(int nodeIndex, EventResult result)
        {
            if (rewardFlyAnimator == null || result == null) yield break;

            var nodeRT = nodeIndex < mapNodeMarkers.Count
                ? mapNodeMarkers[nodeIndex].GetComponent<RectTransform>()
                : null;
            if (nodeRT == null) yield break;

            int totalGold = result.goldReward + result.bonusGold;
            if (totalGold > 0 && accumulatedGoldTarget != null)
            {
                bool arrived = false;
                rewardFlyAnimator.FlyGold(nodeRT, accumulatedGoldTarget, currentSpeed, () =>
                {
                    arrived = true;
                });
                yield return new WaitUntil(() => arrived || skipRequested);
                displayedGold += totalGold;
                UpdateAccumulatedGoldDisplay();
            }

            var allMats = new List<MaterialInstance>();
            if (result.materialDrops != null) allMats.AddRange(result.materialDrops);
            if (result.bonusMaterials != null) allMats.AddRange(result.bonusMaterials);
            if (allMats.Count == 0) yield break;

            int pending = 0;
            foreach (var mat in allMats)
            {
                if (mat == null || mat.quantity <= 0) continue;
                var matData = mat.materialData;
                if (matData == null) continue;

                if (materialIconTarget == null) continue;

                pending++;
                string id = mat.materialDataID;
                int qty = mat.quantity;
                rewardFlyAnimator.FlyMaterial(matData.icon, nodeRT, materialIconTarget, currentSpeed, () =>
                {
                    SoundManager.Instance?.PlaySFX(motionConfig?.materialCollectSfxKey ?? "");
                    materialCounts.TryGetValue(id, out int prev);
                    materialCounts[id] = prev + qty;
                    UpdateMaterialCountDisplay();
                    pending--;
                });
            }

            if (pending > 0)
                yield return new WaitUntil(() => pending <= 0 || skipRequested);
        }

        private IEnumerator PlayBonusFly(AdventureInstance adventure)
        {
            var bonusSource = characterRoot
                ?? appearanceApplier?.GetComponent<RectTransform>()
                ?? accumulatedGoldTarget;

            // 골드 잔여분 비행
            int adventureTotal = adventure.accumulatedGold + adventure.accumulatedBonusGold;
            int goldRemaining = adventureTotal - displayedGold;

            if (goldRemaining > 0 && accumulatedGoldTarget != null && rewardFlyAnimator != null && bonusSource != null)
            {
                bool arrived = false;
                rewardFlyAnimator.FlyGold(bonusSource, accumulatedGoldTarget, currentSpeed, () => arrived = true);
                yield return new WaitUntil(() => arrived || skipRequested);
            }
            displayedGold = adventureTotal;
            UpdateAccumulatedGoldDisplay();

            // 재료 잔여분 비행
            if (rewardFlyAnimator == null || bonusSource == null) yield break;

            var adventureMats = AggregateMaterials(adventure.accumulatedMaterials);
            foreach (var bonusMat in adventure.accumulatedBonusMaterials ?? new List<MaterialInstance>())
            {
                if (bonusMat == null) continue;
                adventureMats.TryGetValue(bonusMat.materialDataID, out int prev);
                adventureMats[bonusMat.materialDataID] = prev + bonusMat.quantity;
            }

            int matPending = 0;
            foreach (var (id, total) in adventureMats)
            {
                materialCounts.TryGetValue(id, out int shown);
                int remaining = total - shown;
                if (remaining <= 0) continue;

                var matData = DataManager.Instance.GetMaterial(id);
                if (matData == null) continue;

                if (materialIconTarget == null) continue;

                matPending++;
                int finalTotal = total;
                rewardFlyAnimator.FlyMaterial(matData.icon, bonusSource, materialIconTarget, currentSpeed, () =>
                {
                    materialCounts[id] = finalTotal;
                    UpdateMaterialCountDisplay();
                    matPending--;
                });
            }

            if (matPending > 0)
                yield return new WaitUntil(() => matPending <= 0 || skipRequested);
        }

        private static Dictionary<string, int> AggregateMaterials(List<MaterialInstance> list)
        {
            var result = new Dictionary<string, int>();
            if (list == null) return result;
            foreach (var m in list)
            {
                if (m == null) continue;
                result.TryGetValue(m.materialDataID, out int prev);
                result[m.materialDataID] = prev + m.quantity;
            }
            return result;
        }

        #endregion
    }
}
