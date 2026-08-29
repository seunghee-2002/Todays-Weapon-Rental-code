using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 대장간 Controller - MVC 패턴
    /// </summary>
    public class BlacksmithController : BaseController<BlacksmithView>
    {
        private VisitorNPC currentNPC;
        private HashSet<int> lockedEffectIndices = new HashSet<int>();

        private WeaponInstance lastEnforcedWeapon;
        private bool lastEnforceSuccess;
        private bool enforceRetryUsed;

        private WeaponInstance lastEvolvedWeapon;
        private bool lastEvolveSuccess;

        #region 초기화

        public void Initialize(VisitorNPC visitorNPC)
        {
            currentNPC = visitorNPC;

            view?.Initialize();

            // 튜토리얼: 대장간이 열리면 하이라이트를 걷고 안내 대화를 진행한다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialBlacksmithOpened();
        }

        #endregion

        #region View로부터 호출되는 메서드

        public void OnPanelSelected(BlacksmithView.Panel panel)
        {
            string button = panel == BlacksmithView.Panel.Craft ? "panel_craft" : "panel_weapon";
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", button);

            view?.SwitchPanel(panel);
        }

        public void OnNextPanelClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "next_panel");
            view?.SwitchPanel(BlacksmithView.Panel.Weapon);
        }

        public void OnPrevPanelClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "prev_panel");
            view?.SwitchPanel(BlacksmithView.Panel.Craft);
        }

        public void OnCloseClicked()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "close");
            BlacksmithManager.Instance?.SetRequestedBlacksmithType(BlacksmithType.None);

            var npc = currentNPC;
            currentNPC = null;

            UIManager.Instance?.ClosePanel<BlacksmithView>();

            // 대장장이는 대화 내내 남아있다가(선택지 노출), 타입 선택을 마친 뒤 퇴장한다.
            // 취소(ESC) 시엔 DialogueManager.CancelDialogue가 spineTarget.EndInteraction을 호출.
            DialogueManager.Instance?.StartDialogue(
                "BLACKSMITH_TYPE_SELECTION",
                onComplete: () => npc?.EndInteraction(),
                spineTarget: npc,
                escCancelMessage: L("Blacksmith_ExitSelectConfirm")
            );
        }

        /// <summary>
        /// 튜토리얼 2-F: 대장간을 닫고 대장장이를 퇴장시킨다.
        /// 아직 안 가르친 기능이므로 일반 닫기의 대장장이 타입 선택 대화는 생략한다(입장 시 인사 생략과 동일 취지).
        /// </summary>
        public void CloseForTutorial()
        {
            var npc = currentNPC;
            currentNPC = null;

            UIManager.Instance?.ClosePanel<BlacksmithView>();
            npc?.EndInteraction();
        }

        #endregion

        #region 제작 패널 콜백

        public void OnCraftWeapon(WeaponRecipeData recipe)
        {
            if (recipe == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "craft_weapon", new Dictionary<string, object>
            {
                { "recipe_id", recipe.StaticID }
            });

            WeaponInstance crafted = null;

            UIManager.Instance?.OpenPanel<BlacksmithProcessView>();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithProcessView>()?.InitializeForCraft(
                recipe.resultWeapon.icon,
                recipe.resultWeapon.DisplayName,
                () =>
                {
                    crafted = BlacksmithManager.Instance?.ExecuteCraft(recipe);
                    // 재료/골드 소모 후 제작 가능 캐시 갱신
                    BlacksmithManager.Instance?.RecalculateCraftableCache();
                },
                () =>
                {
                    if (crafted == null) return;
                    UIManager.Instance?.OpenPanel<WeaponDetailPopup>();
                    UIManager.Instance?.GetOrInstantiatePanel<WeaponDetailPopup>()?.Initialize(crafted);
                });
        }

        public void OnCraftItem(ActiveItemRecipeData recipe)
        {
            if (recipe == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "craft_item", new Dictionary<string, object>
            {
                { "recipe_id", recipe.StaticID }
            });

            bool isTutorialCharm = recipe.resultItem != null && recipe.resultItem.itemType == ActiveItemType.Charm
                && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;

            // 튜토리얼 2-B: 부적 제작이 시작되면(확인 후) 하이라이트를 닫는다.
            if (isTutorialCharm)
                TutorialManager.Instance.OnTutorialCharmCraftStarted();

            ActiveItemInstance crafted = null;

            UIManager.Instance?.OpenPanel<BlacksmithProcessView>();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithProcessView>()?.InitializeForCraft(
                recipe.resultItem.icon,
                recipe.resultItem.DisplayName,
                () =>
                {
                    crafted = BlacksmithManager.Instance?.ExecuteCraft(recipe);
                    // 재료/골드 소모 후 제작 가능 캐시 갱신
                    BlacksmithManager.Instance?.RecalculateCraftableCache();
                },
                () =>
                {
                    if (crafted == null) return;

                    // 튜토리얼 2-C: 부적 상세 팝업을 띄우되(하이라이트 없음), 닫으면 강화 안내로 진행.
                    Action onClose = null;
                    if (isTutorialCharm)
                        onClose = () => TutorialManager.Instance?.OnTutorialCharmCraftCompleted();

                    UIManager.Instance?.OpenPanel<ActiveItemDetailPopup>();
                    UIManager.Instance?.GetOrInstantiatePanel<ActiveItemDetailPopup>()?.Initialize(crafted.activeItemData, onClose);
                });
        }

        public void OnMaterialDetailClicked(MaterialData materialData)
        {
            if (materialData == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "material_detail", new Dictionary<string, object>
            {
                { "material_id", materialData.StaticID }
            });

            var popup = UIManager.Instance.GetOrInstantiatePanel<MaterialDetailPopup>();
            UIManager.Instance.OpenPanel<MaterialDetailPopup>();
            popup?.Initialize(materialData);
        }

        #endregion

        #region 무기 패널 콜백 — Upgrade / Reroll / Disassemble 진입점

        /// <summary>
        /// WeaponPanel의 Upgrade 버튼 클릭 (CanEnforce이면 강화, 아니면 진화)
        /// </summary>
        public void OnUpgradeButtonClicked(WeaponInstance weapon)
        {
            if (weapon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "upgrade", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "weapon_grade", (int)weapon.currentGrade }
            });

            if (weapon.CanEnforce)
            {
                UIManager.Instance?.OpenPanel<BlacksmithEnforceConfirmView>();
                UIPopupController.Instance?.BringPlayerResourceBarToFront();
                UIManager.Instance?.GetOrInstantiatePanel<BlacksmithEnforceConfirmView>()?.Initialize(weapon, this);
            }
            else if (weapon.CanEvolve)
            {
                UIManager.Instance?.OpenPanel<BlacksmithEvolveConfirmView>();
                UIPopupController.Instance?.BringPlayerResourceBarToFront();
                UIManager.Instance?.GetOrInstantiatePanel<BlacksmithEvolveConfirmView>()?.Initialize(weapon, this);
            }
        }

        /// <summary>
        /// WeaponPanel의 Reroll 버튼 클릭
        /// </summary>
        public void OnRerollButtonClicked(WeaponInstance weapon)
        {
            if (weapon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "reroll_open", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID }
            });

            UIManager.Instance?.OpenPanel<BlacksmithRerollConfirmView>();
            UIPopupController.Instance?.BringPlayerResourceBarToFront();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithRerollConfirmView>()?.Initialize(weapon, this);
        }

        #endregion

        #region 강화 콜백

        public void OnEnforceWeapon(WeaponInstance weapon)
        {
            if (weapon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "enforce_weapon", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "weapon_grade", (int)weapon.currentGrade }
            });

            // 튜토리얼 2-C: 강화 실행이 시작되면 하이라이트를 닫는다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialEnforceStarted();

            enforceRetryUsed = false;

            UIManager.Instance?.OpenPanel<BlacksmithProcessView>();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithProcessView>()?.InitializeForEnforce(weapon, () =>
            {
                var result = BlacksmithManager.Instance.ExecuteEnforce(weapon);
                lastEnforcedWeapon = result.result;
                lastEnforceSuccess = result.success;
                return result;
            },
            () =>
            {
                view?.GetWeaponPanelView()?.RefreshWeaponCard(weapon);
            });
        }

        public void OnEnforceRetryClicked(WeaponInstance weapon)
        {
            if (weapon == null || enforceRetryUsed) return;   // 연타 차단
            enforceRetryUsed = true;                          // 즉시 잠금 (차감보다 먼저)

            // G21: 유산 부족으로 실패해도 "재시도를 시도했다"는 사실이 지표이므로 차감보다 먼저 발행한다
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "enforce_retry", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID }
            });

            // 유산 검증·차감은 매니저가 원자적으로 책임. 실패 시 잠금 롤백.
            if (!BlacksmithManager.Instance.TryConsumeEnforceRetry(weapon))
            {
                enforceRetryUsed = false;
                UIPopupController.Instance?.ShowToast(M("Economy_NotEnoughLegacy"), type: PopupSfxType.Warning);
                return;
            }

            UIManager.Instance?.ClosePanel<BlacksmithEnforceResultView>();
            UIManager.Instance?.OpenPanel<BlacksmithProcessView>();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithProcessView>()
                ?.InitializeForEnforce(weapon, () =>
                {
                    var result = BlacksmithManager.Instance.ExecuteEnforceRetry(weapon);
                    lastEnforcedWeapon = result.result;
                    lastEnforceSuccess = result.success;
                    return result;
                },
                () =>
                {
                    view?.GetWeaponPanelView()?.RefreshWeaponCard(weapon);
                });
        }

        public bool CanEnforceRetry(WeaponInstance weapon)
        {
            if (weapon == null || enforceRetryUsed) return false;
            int cost = LegacyManager.Instance?.GetEnforceRetryCost(weapon) ?? 0;
            return LegacyManager.Instance?.HasEnoughLegacyPoints(cost) ?? false;
        }

        #endregion

        #region 진화 콜백

        public void OnEvolveWeapon(WeaponInstance weapon)
        {
            if (weapon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "evolve_weapon", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "weapon_grade", (int)weapon.currentGrade }
            });

            // 튜토리얼 2-D: 진화 실행이 시작되면 하이라이트를 닫는다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialEvolveStarted();

            int previousEffectCount = weapon.effects.Count;

            UIManager.Instance?.OpenPanel<BlacksmithProcessView>();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithProcessView>()?.InitializeForEvolve(weapon, () =>
            {
                var result = BlacksmithManager.Instance.ExecuteEvolve(weapon);
                lastEvolvedWeapon = result.result;
                lastEvolveSuccess = result.success;
                return result;
            },
            () =>
            {
                view?.GetWeaponPanelView()?.RefreshWeaponCard(weapon);
            }, previousEffectCount);
        }

        #endregion

        #region 분해 콜백

        /// <summary>
        /// 디테일 패널의 단일 분해 버튼 → 확인 팝업
        /// </summary>
        public void OnSingleDisassembleRequested(WeaponInstance weapon)
        {
            if (weapon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "disassemble_weapon", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "weapon_grade", (int)weapon.currentGrade }
            });

            ShowDisassembleConfirm(
                new List<WeaponInstance> { weapon },
                () => ExecuteSingleDisassemble(weapon));
        }

        /// <summary>
        /// 분해모드의 분해 실행 버튼 → 확인 팝업
        /// </summary>
        public void OnBatchDisassembleRequested(List<WeaponInstance> weapons)
        {
            if (weapons == null || weapons.Count == 0) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "disassemble_batch", new Dictionary<string, object>
            {
                { "quantity", weapons.Count }
            });

            ShowDisassembleConfirm(weapons, () => RunBatchDisassemble(weapons));
        }

        /// <summary>
        /// 영웅(Epic) 이상 무기가 포함되면 경고 후, 분해 확인 팝업을 띄운다.
        /// </summary>
        private void ShowDisassembleConfirm(List<WeaponInstance> weapons, Action onConfirm)
        {
            string message = BuildDisassembleMessage(weapons);
            bool hasHighGrade = weapons.Any(w => w != null && w.currentGrade >= Grade.Epic);

            if (hasHighGrade)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("Blacksmith_HighGradeConfirm"),
                    () => UIPopupController.Instance?.ShowPopup(message, onConfirm, () => { }),
                    () => { });
            }
            else
            {
                UIPopupController.Instance?.ShowPopup(message, onConfirm, () => { });
            }
        }

        /// <summary>
        /// 가장 등급 높은 무기 이름에 등급색을 입혀 확인 메시지를 생성한다.
        /// </summary>
        private string BuildDisassembleMessage(List<WeaponInstance> weapons)
        {
            var valid = weapons.Where(w => w != null).ToList();
            var top = valid
                .OrderByDescending(w => w.currentGrade)
                .ThenByDescending(w => w.enforceLevel)
                .First();

            string hex = ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGradeColor(top.currentGrade));
            string coloredName = $"<color=#{hex}>{top.weaponData.DisplayName}</color>";

            if (valid.Count <= 1)
                return L("Blacksmith_DisassembleOneConfirm",
                    ("name", coloredName),
                    ("particle", UITranslator.ObjectParticle(top.weaponData.DisplayName)));
            return L("Blacksmith_DisassembleManyConfirm",
                ("name", coloredName), ("count", valid.Count - 1));
        }

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private static string M(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", key);

        private void ExecuteSingleDisassemble(WeaponInstance weapon)
        {
            var (gold, bonusGold, materialCount, _, material) =
                BlacksmithManager.Instance.ExecuteDisassemble(weapon);

            var materials = new List<(MaterialData mat, int count)>();
            if (material != null)
                materials.Add((material, materialCount));

            // 분해로 재료 획득 + 무기 슬롯 여유 변화 → 제작 가능 캐시 갱신
            BlacksmithManager.Instance?.RecalculateCraftableCache();

            UIManager.Instance?.OpenPanel<BlacksmithDisassembleResultView>();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithDisassembleResultView>()
                ?.Initialize(gold, bonusGold, materials);

            view?.GetWeaponPanelView()?.RefreshWeaponList();
        }

        private void RunBatchDisassemble(List<WeaponInstance> weapons)
        {
            var (totalGold, totalBonusGold, materials) =
                BlacksmithManager.Instance.ExecuteBatchDisassemble(weapons);

            // 분해로 재료 획득 + 무기 슬롯 여유 변화 → 제작 가능 캐시 갱신
            BlacksmithManager.Instance?.RecalculateCraftableCache();

            UIManager.Instance?.OpenPanel<BlacksmithDisassembleResultView>();
            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithDisassembleResultView>()
                ?.Initialize(totalGold, totalBonusGold, materials);

            view?.GetWeaponPanelView()?.ExitDisassembleMode();
            view?.GetWeaponPanelView()?.RefreshWeaponList();
        }

        public void OnDisassembleResultClosed()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "disassemble_result_close");
            UIManager.Instance?.ClosePanel<BlacksmithDisassembleResultView>();
        }

        #endregion

        #region 마법 재부여 콜백

        public void OnRerollWeaponSelected(WeaponInstance weapon)
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "reroll_weapon_selected");
            lockedEffectIndices.Clear();
        }

        public void OnRerollEffectLockToggled(int effectIndex, WeaponInstance weapon)
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "reroll_effect_lock", new Dictionary<string, object>
            {
                { "effect_index", effectIndex },
                { "is_locked", !lockedEffectIndices.Contains(effectIndex) }
            });

            if (lockedEffectIndices.Contains(effectIndex))
                lockedEffectIndices.Remove(effectIndex);
            else
                lockedEffectIndices.Add(effectIndex);

            var rerollView = UIManager.Instance?.GetOrInstantiatePanel<BlacksmithRerollConfirmView>();
            rerollView?.UpdateCostDisplay(weapon);
            rerollView?.UpdateRerollButton(weapon);
        }

        public bool IsRerollEffectLocked(int effectIndex)
        {
            return lockedEffectIndices.Contains(effectIndex);
        }

        public int GetLockedEffectCount()
        {
            return lockedEffectIndices.Count;
        }

        public void OnRerollWeapon(WeaponInstance weapon)
        {
            if (weapon == null) return;

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "reroll_execute", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID },
                { "locked_count", lockedEffectIndices.Count }
            });

            // max 효과 중 잠기지 않은 슬롯이 있는지 확인
            int unlockedMaxCount = 0;
            for (int i = 0; i < weapon.effects.Count; i++)
            {
                if (lockedEffectIndices.Contains(i)) continue;

                var effect = weapon.effects[i];
                var effectData = effect.effectData;
                if (effectData == null) continue;

                float maxValue = WeaponEffect.IsIntegerType(effectData.effectType)
                    ? Mathf.Round(effectData.baseValueRange.y)
                    : effectData.baseValueRange.y;

                if (Mathf.Approximately(effect.currentValue, maxValue))
                    unlockedMaxCount++;
            }

            if (unlockedMaxCount > 0)
            {
                UIPopupController.Instance?.ShowPopup(
                    L("Blacksmith_RerollWarnConfirm"),
                    () => ExecuteRerollWithGoldCheck(weapon),
                    () => { }
                );
                return;
            }

            ExecuteRerollWithGoldCheck(weapon);
        }

        private void ExecuteRerollWithGoldCheck(WeaponInstance weapon)
        {
            int goldCost = BlacksmithManager.Instance.GetRerollGoldCost(weapon, lockedEffectIndices.Count);
            EconomyManager.Instance.EnsureGold(goldCost, onReady: () => ExecuteReroll(weapon));
        }

        private void ExecuteReroll(WeaponInstance weapon)
        {
            // 튜토리얼 2-E: 재부여가 실제 실행될 때 하이라이트를 닫는다(경고/골드 팝업 취소 시엔 유지).
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialRerollStarted();

            var currentEffects = new List<WeaponEffect>(weapon.effects);
            var (newEffects, result) = BlacksmithManager.Instance.ExecuteReroll(weapon, lockedEffectIndices);
            if (result == null) return;

            SoundManager.Instance?.PlaySFX("Buy");

            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithRerollConfirmView>()
                ?.ShowRerollResult(weapon, currentEffects, newEffects);
        }

        public void OnAddRerollCharge(WeaponInstance weapon)
        {
            if (weapon == null || !weapon.CanExtraReroll) return;   // 상태 미충족 — 조용히 무시

            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "reroll_extra", new Dictionary<string, object>
            {
                { "weapon_id", weapon.weaponData.StaticID }
            });

            // 유산 검증·차감·충전은 매니저가 원자적으로 책임. UI는 결과만 표현.
            if (!BlacksmithManager.Instance.TryAddExtraRerollCharge(weapon))
            {
                UIPopupController.Instance?.ShowToast(M("Economy_NotEnoughLegacy"), type: PopupSfxType.Warning);
                return;
            }

            UIManager.Instance?.GetOrInstantiatePanel<BlacksmithRerollConfirmView>()?.RefreshUI();
            view?.GetWeaponPanelView()?.RefreshWeaponCard(weapon);
        }

        public bool IsAllEffectsLocked(WeaponInstance weapon)
        {
            if (weapon == null) return false;
            return weapon.effects.Count > 0 && lockedEffectIndices.Count >= weapon.effects.Count;
        }

        #endregion

        #region 결과 팝업 닫기

        /// <summary>
        /// 재부여 before/after 선택이 확정됐을 때 View가 호출.
        /// 선택 결과를 즉시 저장한다
        /// </summary>
        public void OnRerollChoiceConfirmed()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "reroll_choice_confirm");
            BlacksmithManager.Instance?.ConfirmRerollChoice();
        }

        public void OnRerollClosed()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "reroll_close");
            lockedEffectIndices.Clear();
            UIManager.Instance?.ClosePanel<BlacksmithRerollConfirmView>();
            view?.GetWeaponPanelView()?.RefreshWeaponList();

            // 튜토리얼 2-E: 재부여 완료 후 마무리(2-F)로
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialRerollCompleted();
        }

        public void OnEnforceResultClosed()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "enforce_result_close");
            UIManager.Instance?.ClosePanel<BlacksmithEnforceResultView>();

            bool shouldReopen = lastEnforcedWeapon != null &&
                                (!lastEnforceSuccess || lastEnforcedWeapon.CanEnforce);

            if (shouldReopen)
            {
                UIManager.Instance?.OpenPanel<BlacksmithEnforceConfirmView>();
                UIManager.Instance?.GetOrInstantiatePanel<BlacksmithEnforceConfirmView>()
                    ?.Initialize(lastEnforcedWeapon, this);
            }
            else
            {
                view?.GetWeaponPanelView()?.RefreshWeaponList();
            }

            lastEnforcedWeapon = null;
            lastEnforceSuccess = false;

            // 튜토리얼 2-C: 강화 결과 확인 후 다음 단계로
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialEnforceCompleted();
        }

        public void OnEvolveResultClosed()
        {
            AnalyticsManager.Instance?.SendButtonClick("blacksmith", "evolve_result_close");
            UIManager.Instance?.ClosePanel<BlacksmithEvolveResultView>();

            bool shouldReopen = lastEvolvedWeapon != null && !lastEvolveSuccess;

            if (shouldReopen)
            {
                UIManager.Instance?.OpenPanel<BlacksmithEvolveConfirmView>();
                UIManager.Instance?.GetOrInstantiatePanel<BlacksmithEvolveConfirmView>()
                    ?.Initialize(lastEvolvedWeapon, this);
            }
            else
            {
                view?.GetWeaponPanelView()?.RefreshWeaponList();
            }

            lastEvolvedWeapon = null;
            lastEvolveSuccess = false;

            // 튜토리얼 2-D: 진화 결과 확인 후 다음 단계로
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                TutorialManager.Instance.OnTutorialEvolveCompleted();
        }

        #endregion
    }
}
