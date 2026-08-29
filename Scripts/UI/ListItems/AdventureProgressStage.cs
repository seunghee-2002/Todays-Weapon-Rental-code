using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험진행 카드 animationArea의 실시간 연출 컨트롤러.
    /// 바인딩된 AdventureInstance.progress를 자가폴링해 현재 이벤트를 4단계(시작/실행/완료/이동)로 연출한다.
    /// 결과(evt.result)는 모험 시작 시 이미 확정돼 있으므로 매니저 이벤트를 구독하지 않는다.
    /// 캐러셀 중앙(활성) 카드에서만 동작한다(SetActive).
    /// </summary>
    public class AdventureProgressStage : MonoBehaviour
    {
        [Header("Background (uvRect 스크롤)")]
        [SerializeField] private RawImage backgroundRaw;

        [Header("Adventurer (좌)")]
        [SerializeField] private AdventurerAppearanceApplier adventurerApplier;

        [Header("Monster (우, RenderTexture)")]
        [SerializeField] private RawImage monsterView;   // MonsterStageRenderer의 RT 표시 (평소 비활성)

        [Header("Prop (보물상자/모닥불/가시)")]
        [SerializeField] private RectTransform propRoot;
        [SerializeField] private Image propImage;

        [Header("진입/퇴장 (배경 스크롤과 동기, 위치는 진행률 frac에 종속)")]
        [Tooltip("진입 시작: 정위치(homePos) 우측 오프셋(px). 이 거리만큼 화면 밖에서 흘러 들어온다")]
        [SerializeField] private float approachStartOffset = 250f;
        [Tooltip("퇴장 종료: 정위치(homePos) 좌측 오프셋(px). 이 거리만큼 화면 밖으로 흘러 나간다")]
        [SerializeField] private float exitEndOffset = 250f;
        [Tooltip("진입/퇴장 이동 속도(px/s). 클수록 빠르게 미끄러진다. 이동은 Time.deltaTime 기반이라 매끄럽다")]
        [SerializeField] private float slideSpeed = 500f;
        [Tooltip("진입 스킵 판정(frac 0~1). 카드를 봤을 때 frac이 이 값을 넘었으면 진입 없이 정위치에서 시작")]
        [SerializeField, Range(0f, 1f)] private float approachFracEnd = 0.12f;

        [Header("Particle Anchors")]
        [SerializeField] private Transform adventurerParticleAnchor;
        [Tooltip("모험가 발밑 앵커(AnimationArea 하위). Rest/함정회피 파티클 + 함정 가시가 도달하는 지점.")]
        [SerializeField] private Transform adventurerBottomAnchor;
        [SerializeField] private Transform propParticleAnchor;

        private AdventureProgressAnimConfig Cfg => ConfigManager.Instance.AdventureProgressAnim;

        private AdventureInstance adventure;
        private AdventureInstance appliedAdventure;   // 현재 스켈레톤/배경에 외형·무기가 적용된 모험 (재적용 회피용)
        private bool isActive;
        private bool animPaused;                  // 시간 정지 동기화 상태 (정지 진입/재개 전환 감지용)
        private AdventureEvent animatingEvent;   // 현재 연출 중인 이벤트 (변화 감지용, 참조 비교)
        private Coroutine sequence;

        private MonsterStageActor monster;   // 공유 RT 렌더러의 몬스터(이 카드가 소유 시 non-null)

        private bool bgScrolling;                // 배경 스크롤 ON 여부 (속도는 slideSpeed 단일 기준)
        private GameObject activeParticle;       // 유지 중인 이벤트 파티클 1개 (다음 노드 이동/시퀀스 정지 시 즉시 중단)
        private Vector2 propHomePos;
        private bool propHomeCaptured;
        private Vector2 monsterHomePos;
        private bool monsterHomeCaptured;

        private float appliedSpeed = -1f;   // 현재 연출에 반영된 게임 배속(1/2/4). 변경 감지용.

        #region 공개 API (카드가 호출)

        /// <summary>모험 1건을 바인딩하고 외형·무기·배경을 세팅한다. (카드 Bind에서 호출)</summary>
        public void Bind(AdventureInstance adventure)
        {
            StopSequence();
            this.adventure = adventure;
            animatingEvent = null;
            DespawnMonster();
            HideProp();
            bgScrolling = false;
            animPaused = false;
            SetAdventurerTimeScale(Speed);   // 재사용 카드가 정지(timeScale=0) 상태로 남지 않도록 복원(게임 배속 반영)

            if (adventure == null) return;

            // 외형·무기는 모험 단위로 고정이라, 같은 모험이 다시 이 슬롯에 오면 재적용할 필요가 없다.
            // (스킨 재조립 + 무기 텍스처 remap이라 슬롯 재사용마다 반복하면 비싸다)
            if (appliedAdventure != adventure)
            {
                ApplyAppearanceAndWeapon();
                appliedAdventure = adventure;
            }

            SetBackgroundTexture(adventure.dungeon?.mapBackground);
        }

        /// <summary>중앙(활성) 카드에서만 true. 비활성 시 연출을 멈추고 정지 상태로 둔다.</summary>
        public void SetActive(bool active)
        {
            if (isActive == active) return;
            isActive = active;

            // 활성/비활성 전환 시 정지 상태 초기화 — 풀링 재사용 카드가 timeScale=0으로 남지 않도록.
            animPaused = false;
            appliedSpeed = -1f;                          // 활성화 시 배속을 Update에서 재적용하도록 강제
            SetAdventurerTimeScale(active ? Speed : 1f); // 활성 카드만 게임 배속, 비활성은 1x Idle

            if (!active)
            {
                StopSequence();
                animatingEvent = null;
                DespawnMonster();
                HideProp();
                bgScrolling = false;
                PlayAdventurer(Cfg.idleAnim, true);
            }
        }

        /// <summary>빈 슬롯/완료 등으로 연출을 완전히 정리한다.</summary>
        public void Clear()
        {
            StopSequence();
            adventure = null;
            animatingEvent = null;
            DespawnMonster();
            HideProp();
            bgScrolling = false;
        }

        #endregion

        #region Update (자가폴링)

        private void Update()
        {
            if (!isActive || adventure == null) return;

            // 시간 정지 동기화: 정지 중엔 연출(스크롤·시퀀스·Spine)을 모두 멈추고, 재개되면 현재 이벤트 기준으로 재시작한다.
            bool timePaused = TimeManager.Instance != null && TimeManager.Instance.IsTimePaused;
            if (timePaused)
            {
                if (!animPaused) EnterPause();
                return;
            }
            if (animPaused) ExitPause();

            ApplyPlaybackSpeedIfChanged();   // 배속 변경을 모험가/몬스터 재생 속도에 즉시 반영
            ScrollBackground();

            var current = CurrentEvent();
            if (current == animatingEvent) return;

            animatingEvent = current;
            StopSequence();

            if (current != null)
                sequence = StartCoroutine(PlayEventSequence(current));
            else
            {
                PlayAdventurer(Cfg.idleAnim, true);
                bgScrolling = false;
            }
        }

        private void ScrollBackground()
        {
            if (!bgScrolling) return;
            ScrollBackgroundByPx(slideSpeed * Time.deltaTime * Speed);   // 진입/퇴장과 동일한 단일 속도(px/s), 게임 배속 반영
        }

        /// <summary>현재 진행 중 이벤트(포인터 기반, 시작됨·미완료). 없으면 null.</summary>
        private AdventureEvent CurrentEvent() => adventure?.progress?.CurrentEvent;

        /// <summary>현재 이벤트의 진행률 0~1. duration은 매니저 단일 소스(GetEventDuration) 사용.</summary>
        private float Frac(AdventureEvent evt)
        {
            if (evt == null || AdventureManager.Instance == null) return 1f;
            float dur = AdventureManager.Instance.GetEventDuration(evt);
            if (dur <= 0f) return 1f;
            return Mathf.Clamp01((TimeManager.Instance.CurrentTime - evt.startTime) / dur);
        }

        private IEnumerator WaitFrac(AdventureEvent evt, float target)
        {
            while (Frac(evt) < target) yield return null;
        }

        /// <summary>현재 게임 배속(1/2/4). 라이브 연출을 게임 시간에 맞춘다. 0 나눗셈/정지 방지로 하한 클램프.</summary>
        private float Speed => Mathf.Max(0.01f, TimeManager.Instance != null ? TimeManager.Instance.CurrentTimeScale : 1f);

        /// <summary>seconds(1배속 기준 초)를 게임 배속에 맞춰 대기. 배속이 도중 바뀌면 즉시 반영된다.
        /// frac(게임 시간)과 보조를 맞춰, 고배속에서 연출이 이벤트보다 늦어져 StopSequence로 잘리는 것을 막는다.</summary>
        private IEnumerator WaitScaled(float seconds)
        {
            if (seconds <= 0f) yield break;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime * Speed;
                yield return null;
            }
        }

        #endregion

        #region 시간 정지 동기화

        /// <summary>시간 정지: 진행 중 시퀀스를 멈추고 모험가/몬스터/배경 연출을 현재 프레임에서 정지한다.</summary>
        private void EnterPause()
        {
            animPaused = true;
            StopSequence();
            SetAdventurerTimeScale(0f);
            monster?.SetPaused(true);
            bgScrolling = false;
            appliedSpeed = -1f;   // 재개 시 배속을 재적용하도록 강제
        }

        /// <summary>시간 재개: 연출 속도를 복원하고, 현재 이벤트 기준으로 시퀀스를 다시 시작하도록 한다(Frac로 단계 자동 복원).</summary>
        private void ExitPause()
        {
            animPaused = false;
            SetAdventurerTimeScale(Speed);
            animatingEvent = null;
        }

        /// <summary>모험가 Spine 재생 속도 설정(0=정지, 그 외=게임 배속). 시간 정지/배속 동기화에 사용.</summary>
        private void SetAdventurerTimeScale(float scale)
        {
            var pm = adventurerApplier?.GetPartsManager();
            if (pm == null) return;

            var sg = pm.GetSkeletonGraphic();
            if (sg != null) { sg.timeScale = scale; return; }

            var sa = pm.GetSkeletonAnimation();
            if (sa != null) sa.timeScale = scale;
        }

        /// <summary>게임 배속이 바뀌면 모험가 Spine과 몬스터 재생 속도를 즉시 맞춘다. (정지 중엔 Update가 여기까지 오지 않음)</summary>
        private void ApplyPlaybackSpeedIfChanged()
        {
            float s = Speed;
            if (Mathf.Approximately(appliedSpeed, s)) return;
            appliedSpeed = s;
            SetAdventurerTimeScale(s);
            monster?.SetTimeScale(s);
            SetParticleSpeed(activeParticle, s);   // 유지형 이벤트 파티클도 배속 반영
        }

        #endregion

        #region 4단계 시퀀스

        private IEnumerator PlayEventSequence(AdventureEvent evt)
        {
            var type = evt.eventData?.eventType ?? DungeonEventType.Battle;

            SetupActors(evt, type);

            // 사망 이후 이어지는 이벤트(Retreat 등)는 달리지 않고 쓰러진 채 유지
            if (adventure.isDeath && !IsCombat(type))
            {
                PlayAdventurer(Cfg.dieAnim, false);
                bgScrolling = false;
                yield return WaitFrac(evt, 1f);
                yield break;
            }

            // 시작(입장): 달려 진입 + 배경 스크롤. 대상(몬스터/프롭)이 우측에서 배경과 같은 속도로 흘러 들어온다.
            bgScrolling = true;
            PlayAdventurer(RunAnim(), true);
            if (IsCombat(type))
            {
                if (monster != null) monster.PlayIdle();
                yield return ApproachTarget(monsterView != null ? monsterView.rectTransform : null, monsterHomePos, evt);
            }
            else if (ApproachesHome(type))
            {
                // 정위치(맨 오른쪽)까지 흘러 들어와 멈추는 prop: 보물/희귀드롭/휴식/함정
                CapturePropHome();
                if (propRoot != null) propRoot.gameObject.SetActive(true);
                yield return ApproachTarget(propRoot, propHomePos, evt);
            }
            else if (type == DungeonEventType.TrapEvade)
            {
                // 진입 대기 없이 즉시 회피 연출 (파티클 바로 재생)
            }
            else
            {
                // prop 없는 타입(Protection/이동형 등)은 달리며 짧게 진입
                yield return WaitFrac(evt, Cfg.enterPhaseEnd);
            }

            if (IsCombat(type))
                yield return CombatSequence(evt, type);
            else if (type == DungeonEventType.TreasureChest || type == DungeonEventType.RareDrop)
                yield return TreasureSequence(evt);
            else if (type == DungeonEventType.Rest)
                yield return RestSequence(evt);
            else if (type == DungeonEventType.Trap)
                yield return TrapSequence(evt);
            else if (type == DungeonEventType.Protection)
                yield return ProtectionSequence(evt);
            else if (type == DungeonEventType.TrapEvade)
                yield return TrapEvadeSequence(evt);
            else if (type == DungeonEventType.Retry)
                yield return RetrySequence(evt);
            else
                yield return MoveTypeSequence(evt); // Entrance/Retreat/Return
        }

        // 전투 (Battle/MiniBoss/Boss)
        private IEnumerator CombatSequence(AdventureEvent evt, DungeonEventType type)
        {
            // 실행: 공방 교대 (atomic — 새 공방은 frac<executePhaseEnd일 때만 시작)
            bgScrolling = false;
            while (Frac(evt) < Cfg.executePhaseEnd)
                yield return OneExchange(type);

            bool success = evt.result?.isSuccess ?? true;
            if (success)
            {
                // 완료(승리): 몬스터 사망 → 제거, 이동: 다음 노드로 달림
                if (monster != null) monster.PlayDead();
                PlayAdventurer(Cfg.idleAnim, true);
                yield return WaitFrac(evt, Cfg.completePhaseEnd);
                DespawnMonster();

                bgScrolling = true;
                PlayAdventurer(RunAnim(), true);
                yield return WaitFrac(evt, 1f);
            }
            else
            {
                // 완료(패배): 사망=Die(정지), 생존(후퇴/보호)=Hit
                bool death = adventure.isDeath;
                PlayAdventurer(death ? Cfg.dieAnim : Cfg.hitAnim, false);
                SpawnParticle(Cfg.adventurerHitParticle, adventurerParticleAnchor);
                if (monster != null) monster.PlayIdle();
                yield return WaitFrac(evt, death ? Cfg.completePhaseEnd : 1f);
            }
        }

        /// <summary>1합: 모험가 공격→몬스터 피격, 몬스터 공격→모험가 피격. atomic.
        /// 모험가 근접 공격은 스윙을 끝까지 재생한 뒤 Idle로 복귀(공격 애니메이션을 온전히 보여줌). 임팩트(클립×impactRatio) 시점에 이펙트·피격.
        /// 원거리는 발사(rangedEffectDelay) 시점에 이펙트·피격 후 발사 직후 Idle. 모든 대기는 게임 배속에 맞춘다(WaitScaled).
        /// 몬스터 클립 길이를 못 얻으면 Cfg.attackDuration 폴백. type: 몬스터 공격 이펙트 선택용.</summary>
        private IEnumerator OneExchange(DungeonEventType type)
        {
            // === 모험가 공격 → (적중) 몬스터 피격 ===
            bool isRanged = IsRangedWeapon();

            float atk;        // 근접 공격 클립 총 길이(풀 스윙 재생용). 원거리는 미사용.
            float toImpact;   // 공격 완료(근접=적중 스윙, 원거리=발사) 시점까지의 시간
            if (isRanged)
            {
                // 원거리(활/석궁): 발사 애니는 프레임0이 '이미 당겨진 상태'라 draw 구간부터 loop 재생해 당김→발사 순서로 만든다.
                PlayAdventurer(AttackAnim(), true, Cfg.rangedDrawStartTime);
                atk = 0f;
                toImpact = Mathf.Max(0.05f, Cfg.rangedEffectDelay);
            }
            else
            {
                atk = PlayAdventurer(AttackAnim(), false);
                if (atk <= 0f) atk = Cfg.attackDuration;
                toImpact = Mathf.Clamp(atk * Cfg.attackImpactRatio, 0.05f, atk);
            }
            yield return WaitScaled(toImpact);

            // 임팩트: 공격 이펙트(고정) → attackToHitDelay 뒤 몬스터 피격
            SpawnParticle(Cfg.adventurerAttackParticle, adventurerParticleAnchor);
            yield return WaitScaled(Cfg.attackToHitDelay);

            float monsterHit = monster != null ? monster.PlayHit() : 0f;
            MonsterStageRenderer.Instance?.SpawnParticle(Cfg.monsterHitParticle, 3f / Speed, Speed);   // RT 스테이지에 렌더(배속 반영)

            // 근접은 남은 스윙을 끝까지 재생한 뒤 Idle 복귀. 원거리는 발사 자세가 재장전처럼 보여 어색하므로 바로 Idle.
            float swingRemain = isRanged ? 0f : Mathf.Max(0f, atk - toImpact - Cfg.attackToHitDelay);
            if (swingRemain > 0f) yield return WaitScaled(swingRemain);
            PlayAdventurer(Cfg.idleAnim, true);

            // 몬스터 피격 리액션을 끝까지 보여준 뒤 몬스터 공격으로 (스윙 재생으로 이미 지난 시간은 차감)
            float hitRemain = Mathf.Max(0.05f, monsterHit) - swingRemain;
            if (hitRemain > 0f) yield return WaitScaled(hitRemain);

            // === 몬스터 공격 → (적중) 모험가 피격 ===
            float monsterAtk = monster != null ? monster.PlayAttack() : 0f;
            if (monsterAtk <= 0f) monsterAtk = Cfg.attackDuration;
            float toMonsterImpact = Mathf.Max(0.05f, monsterAtk * Cfg.attackImpactRatio);
            yield return WaitScaled(toMonsterImpact);

            // 공격 완료: 몬스터 공격 이펙트(타입별, RT 스테이지). 공격 자세를 attackToHitDelay만큼 유지한 뒤 모험가 피격.
            MonsterStageRenderer.Instance?.SpawnParticle(Cfg.GetMonsterAttackParticle(type), 3f / Speed, Speed);
            yield return WaitScaled(Cfg.attackToHitDelay);

            float adventurerHit = PlayAdventurer(Cfg.hitAnim, false);
            SpawnParticle(Cfg.adventurerHitParticle, adventurerParticleAnchor);
            if (monster != null) monster.PlayIdle();   // 공격 완료 후 몬스터도 Idle 복귀 (모험가와 동일)
            // 모험가 피격 리액션을 끝까지 보여준 뒤
            yield return WaitScaled(Mathf.Max(0.05f, adventurerHit, Cfg.hitReactDuration));

            yield return WaitScaled(Cfg.exchangeGap);
        }

        // 보물상자 / 희귀드롭
        private IEnumerator TreasureSequence(AdventureEvent evt)
        {
            // 도착(입장에서 상자가 흘러 들어와 정위치): 상자 열림 → 좋아하며 공격 반복 + 파티클 유지
            bgScrolling = false;
            SwapPropToOpened(evt);
            PlayAdventurer(AttackAnim(), true);   // attack 반복 = 좋아하는 연출
            SpawnEventParticle(evt);              // 1회 스폰 후 유지
            yield return WaitFrac(evt, Cfg.completePhaseEnd);

            // 이동: 파티클 중단 → 열린 상자가 좌측으로 흘러 나감
            StopActiveParticle();
            PlayAdventurer(RunAnim(), true);
            yield return ExitTarget(propRoot, propHomePos, evt);
        }

        // 휴식
        private IEnumerator RestSequence(AdventureEvent evt)
        {
            // 도착(입장에서 모닥불이 흘러 들어와 정위치): 정지 → 휴식 + 파티클 유지
            bgScrolling = false;
            PlayAdventurer(Cfg.idleAnim, true);
            SpawnEventParticle(evt);              // 1회 스폰 후 유지
            yield return WaitFrac(evt, Cfg.completePhaseEnd);

            // 이동: 파티클 중단 → 모닥불이 좌측으로 흘러 나감
            StopActiveParticle();
            PlayAdventurer(RunAnim(), true);
            yield return ExitTarget(propRoot, propHomePos, evt);
        }

        // 함정 (가시가 맨 오른쪽 정위치까지 흘러 들어옴 → 그 자리에서 밟기 반복: Hit+파티클, 사이엔 Idle)
        private IEnumerator TrapSequence(AdventureEvent evt)
        {
            // 진입(가시가 화면 밖 오른쪽 → 정위치)은 공용 처리(ApproachesHome)에서 이미 수행됨
            bgScrolling = false;
            yield return HitLoopUntil(evt, Cfg.completePhaseEnd, Cfg.trapHitInterval);

            // 이동: 파티클 중단 → 가시가 좌측으로 흘러 나감
            StopActiveParticle();
            PlayAdventurer(RunAnim(), true);
            yield return ExitTarget(propRoot, propHomePos, evt);
        }

        // 함정 회피 (Trap 이벤트 직후 발동하는 짧은 이벤트: 달리며 피했다는 파티클만 표시)
        private IEnumerator TrapEvadeSequence(AdventureEvent evt)
        {
            bgScrolling = true;
            PlayAdventurer(RunAnim(), true);
            SpawnEventParticle(evt);   // 회피 파티클 1회 스폰 후 유지 (다음 노드 이동 시 중단)
            yield return WaitFrac(evt, 1f);
        }

        // 보호 (attack + 파티클 유지)
        private IEnumerator ProtectionSequence(AdventureEvent evt)
        {
            bgScrolling = false;
            PlayAdventurer(AttackAnim(), true);
            SpawnEventParticle(evt);   // 1회 스폰 후 유지
            yield return WaitFrac(evt, 1f);
        }

        // 재도전 (전열 정비: 제자리 idle)
        private IEnumerator RetrySequence(AdventureEvent evt)
        {
            bgScrolling = false;
            PlayAdventurer(Cfg.idleAnim, true);
            yield return WaitFrac(evt, 1f);
        }

        // 이동형 (Entrance/Retreat/Return)
        private IEnumerator MoveTypeSequence(AdventureEvent evt)
        {
            bgScrolling = true;
            PlayAdventurer(RunAnim(), true);
            yield return WaitFrac(evt, 1f);
        }

        #endregion

        #region 외형 / 애니메이션

        private void ApplyAppearanceAndWeapon()
        {
            if (adventurerApplier == null || adventure?.adventurer == null) return;

            adventurerApplier.ApplyAppearance(adventure.adventurer.appearance);

            var pm = adventurerApplier.GetPartsManager();
            if (pm != null)
            {
                if (adventure.isUsingDefaultWeapon)
                    WeaponEquipUtil.Unequip(pm);                              // 맨손(무기 미대여)
                else
                    WeaponEquipUtil.Equip(pm, adventure.weapon.weaponData);   // 손 무기 장착 + inGame 텍스처 적용
            }

            PlayAdventurer(Cfg.idleAnim, true);
        }

        /// <summary>Spine 애니메이션 재생. 비루프 클립의 길이(초)를 반환해 atomic 대기에 사용.
        /// startTime: 재생 시작 지점(TrackTime) — 원거리 발사 애니를 당기는 구간부터 시작할 때 사용.</summary>
        private float PlayAdventurer(string animName, bool loop, float startTime = 0f)
        {
            if (string.IsNullOrEmpty(animName)) return 0f;
            var pm = adventurerApplier?.GetPartsManager();
            if (pm == null) return 0f;

            var sg = pm.GetSkeletonGraphic();
            if (sg != null && sg.AnimationState != null)
            {
                var entry = sg.AnimationState.SetAnimation(0, animName, loop);
                if (entry != null && startTime > 0f) entry.TrackTime = startTime;
                return entry?.Animation?.Duration ?? 0f;
            }

            var sa = pm.GetSkeletonAnimation();
            if (sa != null && sa.AnimationState != null)
            {
                var entry = sa.AnimationState.SetAnimation(0, animName, loop);
                if (entry != null && startTime > 0f) entry.TrackTime = startTime;
                return entry?.Animation?.Duration ?? 0f;
            }
            return 0f;
        }

        private string RunAnim()
            => (adventure != null && adventure.isUsingDefaultWeapon) ? Cfg.runAnim : Cfg.runGearAnim;

        private string AttackAnim()
        {
            if (adventure != null && adventure.isUsingDefaultWeapon) return Cfg.barehandAttackAnim;
            return WeaponEquipUtil.AttackAnim(adventure.weapon.weaponData.weaponType);
        }

        /// <summary>원거리 무기(활/석궁) 여부. 발사 애니를 draw 구간부터 재생할지 판정에 사용.</summary>
        private bool IsRangedWeapon()
        {
            if (adventure == null || adventure.isUsingDefaultWeapon || adventure.weapon?.weaponData == null)
                return false;
            return WeaponEquipUtil.RangedIdleAnim(adventure.weapon.weaponData.weaponType) != null;
        }

        #endregion

        #region 배경 / 몬스터 / 프롭 / 파티클

        private void SetBackgroundTexture(Sprite sprite)
        {
            if (backgroundRaw == null) return;
            backgroundRaw.texture = sprite != null ? sprite.texture : null;
            backgroundRaw.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        private void SetupActors(AdventureEvent evt, DungeonEventType type)
        {
            DespawnMonster();
            HideProp();

            if (IsCombat(type))
            {
                SpawnMonster(evt.eventData);
            }
            else
            {
                var prop = (evt.eventData as DungeonNonBattleEventData)?.propSprite;
                if (prop != null && propImage != null)
                    propImage.sprite = prop;
            }
        }

        private void SpawnMonster(DungeonEventData data)
        {
            var renderer = MonsterStageRenderer.Instance;
            var battle = data as DungeonBattleEventData;
            monster = battle != null ? renderer?.Spawn(battle.monsterPrefab, battle.monsterType) : null;
            if (monsterView != null)
            {
                monsterView.texture = renderer != null ? renderer.RenderTexture : null;
                monsterView.enabled = monster != null;
                CaptureMonsterHome();
            }
            monster?.SetTimeScale(Speed);   // 라이브 연출도 게임 배속에 맞춰 재생
            monster?.PlayIdle();
        }

        private void CaptureMonsterHome()
        {
            if (monsterView != null && !monsterHomeCaptured)
            {
                monsterHomePos = monsterView.rectTransform.anchoredPosition;
                monsterHomeCaptured = true;
            }
        }

        private void DespawnMonster()
        {
            // 공유 렌더러는 이 카드가 소유한 경우(monster != null)에만 정리 — 다른 카드의 몬스터를 지우지 않도록
            if (monster != null)
            {
                MonsterStageRenderer.Instance?.Clear();
                monster = null;
            }
            if (monsterView != null)
            {
                monsterView.enabled = false;
                if (monsterHomeCaptured) monsterView.rectTransform.anchoredPosition = monsterHomePos;
            }
        }

        private void CapturePropHome()
        {
            if (propRoot != null && !propHomeCaptured)
            {
                propHomePos = propRoot.anchoredPosition;
                propHomeCaptured = true;
            }
        }

        /// <summary>대상의 좌측 이동량(px)만큼 배경 uvRect도 스크롤해, 진입/퇴장 중 배경과 대상이 정확히 같은 속도로 보이게 한다.</summary>
        private void ScrollBackgroundByPx(float dxPx)
        {
            if (backgroundRaw == null || Mathf.Approximately(dxPx, 0f)) return;
            float w = backgroundRaw.rectTransform.rect.width;
            if (w <= 0f) return;
            var uv = backgroundRaw.uvRect;
            uv.x += dxPx / w;
            backgroundRaw.uvRect = uv;
        }

        /// <summary>대상이 화면 밖 오른쪽(homePos + approachStartOffset)에서 정위치(homePos)로 매끄럽게(Time.deltaTime) 흘러 들어온다.
        /// 시작 오프셋은 frac으로 보정 — 캐러셀로 이미 진행된 이벤트를 다시 보면 offset이 0이라 진입을 건너뛰고 정위치에서 시작.
        /// 이동량만큼 배경을 함께 스크롤해 속도를 일치시킨다.</summary>
        private IEnumerator ApproachTarget(RectTransform target, Vector2 homePos, AdventureEvent evt)
        {
            if (target == null) yield break;
            bgScrolling = false;   // 진입 중 배경은 대상과 동기로 직접 스크롤
            float offset = Mathf.Lerp(approachStartOffset, 0f, Mathf.Clamp01(Frac(evt) / Mathf.Max(0.0001f, approachFracEnd)));
            target.anchoredPosition = new Vector2(homePos.x + offset, homePos.y);
            while (offset > 0f)
            {
                float dx = slideSpeed * Time.deltaTime * Speed;
                offset = Mathf.Max(0f, offset - dx);
                target.anchoredPosition = new Vector2(homePos.x + offset, homePos.y);
                ScrollBackgroundByPx(dx);
                yield return null;
            }
            target.anchoredPosition = homePos;
        }

        /// <summary>이벤트 종료 후 대상이 현재 위치에서 이어서 좌측 화면 밖으로 매끄럽게 흘러나가 퇴장.
        /// (frac 재계산 없이 현재 위치에서 시작 — 오른쪽→중앙 순간이동 방지). restPos: 퇴장 기준 정지 위치. WaitFrac(1f)까지 포함.</summary>
        private IEnumerator ExitTarget(RectTransform target, Vector2 restPos, AdventureEvent evt)
        {
            if (target == null) { yield return WaitFrac(evt, 1f); yield break; }
            bgScrolling = false;   // 퇴장 중 배경은 대상과 동기로 직접 스크롤
            float offset = target.anchoredPosition.x - restPos.x;   // 현재 위치에서 이어서(순간이동 방지)
            while (offset > -exitEndOffset && Frac(evt) < 1f)
            {
                float dx = slideSpeed * Time.deltaTime * Speed;
                offset = Mathf.Max(-exitEndOffset, offset - dx);
                target.anchoredPosition = new Vector2(restPos.x + offset, restPos.y);
                ScrollBackgroundByPx(dx);
                yield return null;
            }
            target.gameObject.SetActive(false);
            target.anchoredPosition = restPos;
            // 남은 이동 단계: 대상은 화면 밖, 배경만 일정 속도로 스크롤하며 다음 이벤트까지 대기
            bgScrolling = true;
            yield return WaitFrac(evt, 1f);
        }

        private void HideProp()
        {
            if (propRoot == null) return;
            propRoot.gameObject.SetActive(false);
            if (propHomeCaptured) propRoot.anchoredPosition = propHomePos;
        }

        private GameObject VisualParticle(AdventureEvent evt)
        {
            return (evt.eventData as DungeonNonBattleEventData)?.particlePrefab;
        }

        /// <summary>비전투 이벤트 고유 파티클(particlePrefab)을 규칙에 따라 스폰하고 유지.
        /// 이전 유지 파티클은 제거(재시작 방지, 항상 1개만).</summary>
        private void SpawnEventParticle(AdventureEvent evt)
        {
            StopActiveParticle();
            var type = evt?.eventData?.eventType ?? DungeonEventType.Battle;
            activeParticle = InstantiateParticle(VisualParticle(evt), EventParticleAnchor(type));
        }

        /// <summary>이벤트 파티클 앵커: 보물/희귀/함정=prop(오른쪽), Rest/함정회피=모험가 발밑, 그 외(Protection 등)=모험가.</summary>
        private Transform EventParticleAnchor(DungeonEventType type)
        {
            if (UsesPropAnchor(type)) return propParticleAnchor;
            if ((type == DungeonEventType.Rest || type == DungeonEventType.TrapEvade) && adventurerBottomAnchor != null)
                return adventurerBottomAnchor;
            return adventurerParticleAnchor;
        }

        /// <summary>유지 중인 이벤트 파티클을 즉시 중단(다음 노드 이동/시퀀스 정지 시).</summary>
        private void StopActiveParticle()
        {
            if (activeParticle != null) { Destroy(activeParticle); activeParticle = null; }
        }

        /// <summary>보물 상자를 열린 스프라이트(resultSuccessIcon)로 교체. 미지정 시 유지.</summary>
        private void SwapPropToOpened(AdventureEvent evt)
        {
            var opened = (evt.eventData as DungeonNonBattleEventData)?.resultSuccessIcon;
            if (opened != null && propImage != null) propImage.sprite = opened;
        }

        /// <summary>targetFrac까지 (도착 즉시 Hit+파티클 → Idle로 interval초 대기)를 반복. 함정 밟기용.</summary>
        private IEnumerator HitLoopUntil(AdventureEvent evt, float targetFrac, float interval)
        {
            while (Frac(evt) < targetFrac)
            {
                float hit = PlayAdventurer(Cfg.hitAnim, false);
                SpawnEventParticle(evt);
                yield return WaitSecondsOrFrac(evt, Mathf.Max(Cfg.hitReactDuration, hit), targetFrac);
                if (Frac(evt) >= targetFrac) break;
                PlayAdventurer(Cfg.idleAnim, true);
                yield return WaitSecondsOrFrac(evt, interval, targetFrac);
            }
        }

        /// <summary>seconds초 또는 targetFrac 도달 중 먼저 오는 시점까지 대기.</summary>
        private IEnumerator WaitSecondsOrFrac(AdventureEvent evt, float seconds, float targetFrac)
        {
            float t = 0f;
            while (t < seconds && Frac(evt) < targetFrac) { t += Time.deltaTime * Speed; yield return null; }
        }

        /// <summary>일회성 파티클(전투 피격 등): 스폰 후 3초(배속 반영) 뒤 자동 제거.</summary>
        private void SpawnParticle(GameObject prefab, Transform anchor)
        {
            var p = InstantiateParticle(prefab, anchor);
            if (p != null) Destroy(p, 3f / Speed);   // 제거 시간도 배속에 맞춤(재생 속도와 함께 압축)
        }

        /// <summary>파티클 프리팹을 앵커 자식으로 생성(위치 0, UI 위로 정렬). 생성물 반환(실패 시 null).</summary>
        private GameObject InstantiateParticle(GameObject prefab, Transform anchor)
        {
            if (prefab == null || anchor == null) return null;
            var p = Instantiate(prefab, anchor);
            p.transform.localPosition = Vector3.zero;
            // 패널 캔버스가 Screen Space - Camera라, sortingOrder를 올리지 않으면 ParticleSystem이 UI 뒤에 묻힌다.
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                r.sortingOrder = 9999;
            SetParticleSpeed(p, Speed);   // 게임 배속에 맞춰 재생(고배속에서 느리게/오래 남지 않도록)
            return p;
        }

        /// <summary>파티클 GameObject 하위 모든 ParticleSystem의 재생 속도를 게임 배속에 맞춘다.</summary>
        private static void SetParticleSpeed(GameObject go, float speed)
        {
            if (go == null) return;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.simulationSpeed = speed;
            }
        }

        #endregion

        #region 내부 헬퍼 / 정리

        private static bool IsCombat(DungeonEventType type)
            => type == DungeonEventType.Battle
            || type == DungeonEventType.MiniBoss
            || type == DungeonEventType.Boss;

        // prop이 정위치(맨 오른쪽)까지 흘러 들어와 멈추는 타입(진입 공용 처리). TrapEvade는 prop 미표시.
        private static bool ApproachesHome(DungeonEventType type)
            => type == DungeonEventType.TreasureChest
            || type == DungeonEventType.RareDrop
            || type == DungeonEventType.Rest
            || type == DungeonEventType.Trap;

        // 파티클을 prop(오른쪽) 앵커에 스폰하는 타입: 보물/희귀(상자 반짝)·함정(가시 피격). Rest/Protection/TrapEvade는 모험가 앵커.
        private static bool UsesPropAnchor(DungeonEventType type)
            => type == DungeonEventType.TreasureChest
            || type == DungeonEventType.RareDrop
            || type == DungeonEventType.Trap;

        private void StopSequence()
        {
            if (sequence != null) { StopCoroutine(sequence); sequence = null; }
            StopActiveParticle();   // 다음 노드 이동/정지 시 유지 파티클 즉시 중단
        }

        private void OnDisable()
        {
            StopSequence();
        }

        private void OnDestroy()
        {
            DespawnMonster();
        }

        #endregion
    }
}
