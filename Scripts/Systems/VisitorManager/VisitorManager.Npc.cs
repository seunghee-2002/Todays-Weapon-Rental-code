using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public partial class VisitorManager
    {
        #region 모험가 외 NPC 생성

        private void SpawnWeaponShop()
        {
            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);
            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();

            npc.Initialize(VisitorType.WeaponShop, null, null);
            npc.ApplyAppearance(weaponShopAppearance);

            RegisterVisitor(npc);

            WeaponShopManager.Instance.MarkShopSpawned(TimeManager.Instance.CurrentDay);
        }

        private void SpawnBlacksmith()
        {
            BlacksmithManager.Instance.SpawnBlacksmith();
            SpawnBlacksmithNpc();
        }

        /// <summary>대장장이 데이터가 이미 선택된 상태에서 NPC 오브젝트만 생성한다.</summary>
        private void SpawnBlacksmithNpc()
        {
            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);

            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();
            npc.Initialize(VisitorType.Blacksmith, null, null);
            // 대장장이별 외형 적용 (BlacksmithConfig의 BlacksmithData.appearance)
            npc.ApplyAppearance(BlacksmithManager.Instance.CurrentBlacksmith?.appearance);
            RegisterVisitor(npc);
        }

        /// <summary>
        /// 모험 결과 보고 전령 스폰 (저녁 6시). 이미 있으면 무시한다.
        /// </summary>
        private void SpawnHerald()
        {
            if (CurrentHerald != null) return;

            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);
            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();

            npc.Initialize(VisitorType.Herald, null, null, TimeManager.Instance.IsSkippingTime);
            npc.ApplyAppearance(heraldAppearance);

            RegisterVisitor(npc);

            Log.Info("[VisitorManager] 전령 스폰");
        }

        private void CheckAndSpawnEventNPC()
        {
            int currentDay = TimeManager.Instance.CurrentDay;
            bool guaranteed = LegacyManager.Instance?.HasMorningEventGuarantee() == true
                              && currentDay % 7 == 0 && currentDay > 0;

            if (guaranteed)
            {
                Log.Info($"[VisitorManager] 아침 이벤트 보장 발동 (Day {currentDay}) — 이벤트 NPC 확정 스폰");
                SpawnEventNPC();
                return;
            }

            float eventChance = ReputationManager.Instance?.GetEventChance() ?? 0f;
            if (Random.value <= eventChance)
                SpawnEventNPC();
        }

        private void SpawnEventNPC(MorningEventType? forcedType = null)
        {
            MorningEventType type = forcedType ?? MorningEventManager.Instance.SelectRandomEventType();
            VisitorEventData eventData = DataManager.Instance.GetEventDataByType(type);

            AnalyticsManager.Instance?.Send("morning_event_shown", new Dictionary<string, object>
            {
                { "event_type", MorningEventManager.GetEventTypeAnalyticsName(type) }
            });

            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);

            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();
            npc.Initialize(VisitorType.EventNPC, null, eventData);
            npc.ApplyAppearance(eventData?.appearance);

            RegisterVisitor(npc);

            Log.Info($"[VisitorManager] 이벤트 NPC 스폰: {type}");
        }

        /// <summary>
        /// 수상한 투자자 결과 NPC 스폰 — 성공 시 다음 날 아침에 호출된다.
        /// </summary>
        public void SpawnInvestorResultNPC(string dialogueID, int returnedGold)
        {
            VisitorEventData eventData = DataManager.Instance.GetEventDataByType(MorningEventType.SuspiciousInvestor);

            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);

            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();
            npc.Initialize(VisitorType.InvestorResult);
            npc.ApplyAppearance(eventData?.appearance);
            npc.InitializeInvestorResult(dialogueID, returnedGold);

            RegisterVisitor(npc);
            Log.Info($"[VisitorManager] 투자자 결과 NPC 스폰: {dialogueID}, {returnedGold}G");
        }

        /// <summary>
        /// 사망 모험가 이벤트 NPC 스폰 (청소/부활제안/기적생환)
        /// </summary>
        public void SpawnDeadEventNPC(DeadEventKind kind, AdventurerInstance deadAdventurer)
        {
            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);

            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();
            // 시간 스킵 중 스폰이면 즉시 배치 (체류 타이머 고정 버그 방지, SpawnAdventurer와 동일)
            npc.Initialize(VisitorType.DeadEvent, deadAdventurer, null, TimeManager.Instance.IsSkippingTime);
            npc.deadEventKind = kind;

            switch (kind)
            {
                case DeadEventKind.NoVisitor:
                    npc.ApplyAppearance(noVisitorAppearance);
                    break;
                case DeadEventKind.ReviveOffer:
                    npc.ApplyAppearance(reviveOfferAppearance);
                    break;
                case DeadEventKind.MiracleRevive:
                    npc.ApplyAppearance(deadAdventurer?.appearance);
                    break;
            }

            RegisterVisitor(npc);
            Log.Info($"[VisitorManager] 사망 이벤트 NPC 스폰: {kind}, 대상: {deadAdventurer?.Name ?? "없음"}");
        }

        /// <summary>
        /// 튜토리얼용 무기 상점 스폰 (TutorialManager에서 호출)
        /// </summary>
        public VisitorNPC SpawnTutorialWeaponShop()
        {
            GameObject npcObj = Instantiate(VisitorPrefab, GetBottomRoadEntryPosition(), Quaternion.identity, visitorParent);
            VisitorNPC npc = npcObj.GetOrAddComponent<VisitorNPC>();

            if (npc != null)
            {
                npc.Initialize(VisitorType.WeaponShop, null, null);
                npc.ApplyAppearance(weaponShopAppearance);
                RegisterVisitor(npc);

                WeaponShopManager.Instance.MarkShopSpawned(TimeManager.Instance.CurrentDay);

                Log.Info("[VisitorManager] Tutorial weapon shop spawned");
            }

            return npc;
        }

        /// <summary>
        /// 튜토리얼용 길드 사절단 스폰 (TutorialManager에서 호출). 1일차 아침 이벤트는 사절단으로 고정한다.
        /// </summary>
        public void SpawnTutorialGuildEnvoy()
        {
            SpawnEventNPC(MorningEventType.GuildEnvoy);
            Log.Info("[VisitorManager] Tutorial guild envoy spawned (GuildEnvoy 고정)");
        }

        /// <summary>
        /// 튜토리얼용 대장장이 스폰 (TutorialManager에서 호출)
        /// </summary>
        public void SpawnTutorialBlacksmith(string blacksmithID = "blacksmith_004")
        {
            BlacksmithManager.Instance.SpawnBlacksmithByID(blacksmithID);
            SpawnBlacksmithNpc();
            Log.Info($"[VisitorManager] Tutorial blacksmith spawned ({blacksmithID} 고정)");
        }

        /// <summary>
        /// 튜토리얼용 모험가 스폰 (TutorialManager에서 호출). 데이터의 stat range를 그대로 쓰되
        /// (고정값이면 결정적), 기본무기는 랜덤 70/20/10 분기를 우회해 항상 검으로 지급한다.
        /// </summary>
        public void SpawnTutorialAdventurer(string adventurerID)
        {
            var data = DataManager.Instance?.GetAdventurer(adventurerID);
            if (data == null)
            {
                Log.Error($"[VisitorManager] 튜토리얼 모험가 데이터를 찾지 못했습니다: {adventurerID}");
                return;
            }

            var instance = CreateTutorialAdventurer(data);
            if (instance == null) return;

            var swordData = DataManager.Instance.GetDefaultWeapon(WeaponType.Sword);
            instance.defaultWeapon = swordData != null ? new WeaponInstance(swordData, true) : null;

            SpawnAdventurer(instance);
            Log.Info($"[VisitorManager] Tutorial adventurer spawned ({adventurerID} 고정, 기본무기: Sword)");
        }

        #endregion

        #region 모험가 외 관리

        /// <summary>
        /// 무기상점과의 상호작용 완료를 알림
        /// </summary>
        public void OnWeaponShopInteractionCompleted()
        {
            CheckAndPromptSkipMorning();
        }

        /// <summary>
        /// 대장장이와의 상호작용 완료를 알림
        /// </summary>
        public void OnBlacksmithInteractionCompleted()
        {
            CheckAndPromptSkipMorning();
        }

        /// <summary>
        /// 이벤트 NPC와의 상호작용 완료를 알림
        /// </summary>
        public void OnEventNPCInteractionCompleted()
        {
            CheckAndPromptSkipMorning();
        }

        /// <summary>
        /// 아침 NPC 상호작용이 끝날 때마다 호출 — 그날 등장한 아침 NPC를 모두 처리했다면
        /// "낮으로 진행할까요?" 팝업을 띄운다. 판정(CanSkipMorning)은 방금 처리한 NPC의
        /// 퇴장(RemoveVisitor→isLeaving)이 반영된 뒤라야 정확하므로, 패널이 닫히는 시점까지
        /// 기다렸다가 코루틴 안에서 검사한다.
        /// </summary>
        private void CheckAndPromptSkipMorning()
        {
            // 튜토리얼(1일차) 중에는 발동하지 않는다. 트리거 시점에 판단해야, 코루틴 대기 중
            // 튜토리얼이 종료되며 팝업이 새는 것을 막을 수 있다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                return;

            if (!hasPromptedSkipMorning && TimeManager.Instance.IsMorning())
                StartCoroutine(ShowSkipMorningPopup());
        }

        private System.Collections.IEnumerator ShowSkipMorningPopup()
        {
            yield return new WaitUntil(() => !UIManager.Instance.IsAnyPanelOpen());

            if (hasPromptedSkipMorning || !CanSkipMorning || !TimeManager.Instance.IsMorning())
                yield break;

            hasPromptedSkipMorning = true;
            UIPopupController.Instance.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Visitor_SkipMorningConfirm"),
                onConfirm: () => TimeManager.Instance.SkipMorning(),
                onCancel: () => {}
            );
        }

        /// <summary>
        /// 방문자가 떠날 때마다 호출 - 저녁에 남은 방문자가 없어지면(전령 보고 종료 포함)
        /// "밤으로 진행할까요?" 팝업을 띄운다. 아침과 마찬가지로 퇴장 처리가 반영된 뒤라야
        /// 판정(CanSkipEvening)이 정확하므로 패널이 닫히는 시점까지 기다렸다가 코루틴 안에서 검사한다.
        /// </summary>
        private void CheckAndPromptSkipEvening()
        {
            // 튜토리얼(1일차) 중에는 발동하지 않는다. 트리거 시점에 판단해야, 코루틴 대기 중
            // 튜토리얼이 종료되며 팝업이 새는 것을 막을 수 있다.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                return;

            if (!hasPromptedSkipEvening && TimeManager.Instance.IsEvening())
                StartCoroutine(ShowSkipEveningPopup());
        }

        private System.Collections.IEnumerator ShowSkipEveningPopup()
        {
            yield return new WaitUntil(() => !UIManager.Instance.IsAnyPanelOpen());

            if (hasPromptedSkipEvening || !CanSkipEvening)
                yield break;

            hasPromptedSkipEvening = true;
            UIPopupController.Instance.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "Visitor_SkipEveningConfirm"),
                onConfirm: () => TimeManager.Instance.SkipEvening(),
                onCancel: () => {}
            );
        }

        #endregion
    }
}