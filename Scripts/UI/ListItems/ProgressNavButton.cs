using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 캐러셀 하단 네비게이터 카드.
    /// 모험가 초상화(+최고스탯 배경색) / 무기 아이콘(+등급 BG) / 던전 아이콘(+등급 Frame)을 표시하고,
    /// 선택 상태는 배경 Glow 펄스 + 디밍, 완료 상태는 디밍 + 완료 도장으로 알린다.
    /// </summary>
    public class ProgressNavButton : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private AdventurerAppearanceApplier appearanceApplier;
        [SerializeField] private Image adventureBG;     // 모험가 최고 스탯 색
        [SerializeField] private Image dungeonIcon;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image weaponBG;         // 무기 등급 BG 색
        [SerializeField] private Image weaponFrame;      // 무기 등급 Frame 색
        [SerializeField] private Image dungeonBG;        // 던전 등급 BG 색
        [SerializeField] private Image dungeonFrame;     // 던전 등급 Frame 색
        [SerializeField] private Button button;

        [Header("State Visuals")]
        [SerializeField] private Image selectionGlow;    // 선택 시 알파 펄스
        [SerializeField] private GameObject dimOverlay;  // 비선택/완료 시 어둡게
        [SerializeField] private GameObject completedStamp;  // 완료 도장

        [Header("Glow Tween")]
        [SerializeField] private float glowMinAlpha = 0.15f;
        [SerializeField] private float glowMaxAlpha = 0.6f;
        [SerializeField] private float glowDuration = 0.8f;

        private AdventureInstance adventure;
        private Action<AdventureInstance> onClickCallback;
        private bool isSelected;
        private bool isCompleted;
        private AdventurerAppearanceData appliedAppearance;   // 현재 스켈레톤에 적용된 외형 (풀 재사용 시 재적용 회피)
        private Coroutine freezeRoutine;

        public void Initialize(AdventureInstance adv, Action<AdventureInstance> onClick)
        {
            if (adv == null)
            {
                Log.Error("[ProgressNavButton] AdventureInstance가 null입니다!");
                return;
            }

            adventure = adv;
            onClickCallback = onClick;

            var appearance = adv.adventurer.appearance;
            if (appearance != null && appearance != appliedAppearance)
            {
                Unfreeze();
                appearanceApplier?.ApplyAppearance(appearance);
                appliedAppearance = appearance;
            }
            ScheduleFreeze();

            if (dungeonIcon != null && adv.dungeon != null)
                dungeonIcon.sprite = adv.dungeon.dungeonIcon;

            if (weaponIcon != null)
            {
                if (adv.isUsingDefaultWeapon)
                    weaponIcon.sprite = IconManager.Instance.GetDefaultWeaponIcon();
                else if (adv.weapon?.weaponData != null)
                    weaponIcon.sprite = adv.weapon.weaponData.icon;
            }

            ApplyGradeColors(adv);

            button?.onClick.RemoveAllListeners();   // 풀에서 재사용되므로 중복 구독 방지
            button?.onClick.AddListener(OnClick);

            isCompleted = adv.isCompleted;
            isSelected = false;
            RefreshVisual();
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveAllListeners();
        }

        #region Spine 정지

        /// <summary>
        /// 네비 썸네일은 정지 화면이면 충분하다. 한 프레임 뒤(정상 파이프라인으로 메시가 만들어진 뒤) Spine을 멈추고
        /// PartsManager도 끈다. 켜둔 채로 두면 버튼 하나하나가 매 프레임 메시를 다시 만들어
        /// 패널 캔버스 전체가 매 프레임 리빌드되고, PartsManager.LateUpdate도 매 프레임 슬롯 색을 다시 칠한다.
        /// </summary>
        private void ScheduleFreeze()
        {
            if (freezeRoutine != null) StopCoroutine(freezeRoutine);
            freezeRoutine = StartCoroutine(FreezeNextFrame());
        }

        private IEnumerator FreezeNextFrame()
        {
            yield return null;   // 이번 프레임 LateUpdate에서 메시가 1회 생성된 뒤

            var pm = appearanceApplier?.GetPartsManager();
            var graphic = pm?.GetSkeletonGraphic();
            if (graphic != null) graphic.freeze = true;
            if (pm != null) pm.enabled = false;

            freezeRoutine = null;
        }

        /// <summary>외형을 다시 적용하기 전에 정지를 푼다.</summary>
        private void Unfreeze()
        {
            if (freezeRoutine != null) { StopCoroutine(freezeRoutine); freezeRoutine = null; }

            var pm = appearanceApplier?.GetPartsManager();
            if (pm != null) pm.enabled = true;

            var graphic = pm?.GetSkeletonGraphic();
            if (graphic != null) graphic.freeze = false;
        }

        #endregion

        #region 색상 적용

        private void ApplyGradeColors(AdventureInstance adv)
        {
            var cm = ColorManager.Instance;
            if (cm == null) return;

            if (adventureBG != null)
                adventureBG.color = cm.GetHighestStatColor(adv.adventurer.GetHighestStat());

            if (adv.weapon != null)
            {
                if (weaponBG != null)
                    weaponBG.color = cm.GetGradeCardBackgroundColor(adv.weapon.currentGrade);
                if (weaponFrame != null)
                    weaponFrame.sprite = IconManager.Instance.GetFrameByGrade(adv.weapon.currentGrade);
            }

            if (adv.dungeon != null)
            {
                if (dungeonBG != null && adv.dungeon != null)
                dungeonBG.color = cm.GetGradeCardBackgroundColor(adv.dungeon.grade);
                if (dungeonFrame != null && adv.dungeon != null)
                dungeonFrame.sprite = IconManager.Instance.GetFrameByGrade(adv.dungeon.grade);
            }
            
        }

        #endregion

        #region 상태 표시

        /// <summary>선택 상태 설정 (배경 Glow + 디밍).</summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            RefreshVisual();
        }

        /// <summary>완료 상태 설정 (디밍 + 완료 도장).</summary>
        public void SetCompleted(bool completed)
        {
            isCompleted = completed;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            // 비선택이거나 완료된 카드는 어둡게
            dimOverlay?.SetActive(!isSelected || isCompleted);
            completedStamp?.SetActive(isCompleted);
            UpdateGlow(isSelected);
        }

        private void UpdateGlow(bool on)
        {
            if (selectionGlow == null) return;

            DOTween.Kill(selectionGlow);

            if (on)
            {
                selectionGlow.gameObject.SetActive(true);
                var c = selectionGlow.color;
                c.a = glowMaxAlpha;
                selectionGlow.color = c;
                selectionGlow.DOFade(glowMinAlpha, glowDuration).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
            }
            else
            {
                selectionGlow.gameObject.SetActive(false);
            }
        }

        #endregion

        private void OnClick()
        {
            onClickCallback?.Invoke(adventure);
        }
    }
}
