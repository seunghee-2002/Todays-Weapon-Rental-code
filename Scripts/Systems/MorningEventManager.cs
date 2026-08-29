// Scripts/Systems/MorningEventManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public class MorningEventManager : BaseManager<MorningEventManager>
    {
        private MorningEventConfig Config => ConfigManager.Instance.MorningEvent;

        public bool IsEventCompleted => GameManager.Instance.GameData.morningEventCompleted;

        #region 초기화

        public void Initialize(GameData gameData) { }

        // 시세 공지 표시 시각 — 6시 정각은 QuestResultView와 겹쳐 어색하므로 한 틱(3분) 뒤에 띄운다.
        private const int PriceNoticeHour   = 6;
        private const int PriceNoticeMinute = 3;

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged += OnNewDay;
                TimeManager.Instance.OnTimeChanged += OnTimeChangedForPriceNotice;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged -= OnNewDay;
                TimeManager.Instance.OnTimeChanged -= OnTimeChangedForPriceNotice;
            }
        }

        private void OnNewDay(int day)
        {
            var pd = GameManager.Instance.GameData;
            pd.morningEventCompleted = false;
            pd.mysteryBoxTier = -1;
            pd.blackMarketOfferSaveData = null;

            // 투자한 날로부터 2일 이상 경과(InvestorResult NPC 등장일 다음날)하면 미확인 투자 결과 초기화
            if (pd.hasPendingInvestment && pd.lastInvestorDay >= 0 && day - pd.lastInvestorDay >= 2)
            {
                pd.hasPendingInvestment            = false;
                pd.pendingInvestorReturnedGold     = 0;
                pd.pendingInvestorResultDialogueID = null;
                Log.Info($"[MorningEventManager] 투자 결과 미확인 만료: lastInvestorDay={pd.lastInvestorDay}, currentDay={day}");
            }
        }

        /// <summary>
        /// 시세 공지 시각(6:03) 판정. 6시 정각은 퀘스트 결과창이 뜨는 시점이라 한 틱 뒤로 미룬다.
        /// 시간은 3분 틱으로만 진행하므로 6:03은 하루에 정확히 한 번 지나간다(스킵 중에도 틱은 밟는다).
        /// </summary>
        private void OnTimeChangedForPriceNotice(int hour, int minute)
        {
            if (hour != PriceNoticeHour || minute != PriceNoticeMinute) return;
            TryShowPriceStepNotice(GameManager.Instance?.GameData?.currentDay ?? 0);
        }

        /// <summary>
        /// 시세 계단 공지 — 계단 1주일 전(예고)과 계단 당일에 길드 전령이 알린다.
        /// 공지일은 PriceTierConfig의 계단 주차에서 전부 유도되므로 저장 플래그가 필요 없다.
        /// 튜토리얼 중(1일차)에는 계단이 없어 자연히 걸리지 않는다.
        /// </summary>
        private void TryShowPriceStepNotice(int day)
        {
            var tier = ConfigManager.Instance?.PriceTier;
            if (tier == null) return;

            int stepWeek = tier.GetNoticeStepWeek(day, out bool isToday);
            if (stepWeek <= 0) return;

            var notice = tier.GetStepNotice(stepWeek, out int stepIndex);
            var lines = isToday ? notice?.todayLines : notice?.noticeLines;
            if (lines == null || lines.Count == 0) return;

            // 표시 직전에 현재 언어로 옮긴다. 키는 계단 위치 + 예고/당일로 만들어지므로
            // GetStepNotice가 돌려준 stepIndex가 필요하다.
            var localized = DataLocalizer.PriceTierNoticeLines(stepIndex, isToday, lines);

            // 대화창은 시간을 멈추지 않는 패널이라(튜토리얼 전제) 여기서 정지/재개를 감싼다.
            TimeManager.Instance?.PauseTime();
            TutorialManager.Instance?.ShowHeraldNotice(localized, notice.portrait, () => TimeManager.Instance?.ResumeTime());
            Log.Info($"[MorningEventManager] 시세 계단 공지 — {stepWeek}주차 ({(isToday ? "당일" : "예고")})");
        }

        public void MarkEventCompleted()
        {
            GameManager.Instance.GameData.morningEventCompleted = true;
        }

        /// <summary>morning_event_resolved 발행 — 수락(accept)은 각 Execute 성공 지점, 거절(reject)은 캡처 가능한 지점만</summary>
        private void SendEventResolved(MorningEventType type, string choice)
        {
            AnalyticsManager.Instance?.Send("morning_event_resolved", new Dictionary<string, object>
            {
                { "event_type", GetEventTypeAnalyticsName(type) },
                { "choice", choice }
            });
        }

        /// <summary>Analytics event_type 표기 (Documents/Analytics_이벤트_설계.md). VisitorManager의 morning_event_shown도 사용</summary>
        public static string GetEventTypeAnalyticsName(MorningEventType type)
        {
            switch (type)
            {
                case MorningEventType.WeaponEnhance:       return "weapon_enhance";
                case MorningEventType.WeaponExchange:      return "weapon_exchange";
                case MorningEventType.SuspiciousInvestor:  return "investor";
                case MorningEventType.WanderingBlacksmith: return "blacksmith_event";
                case MorningEventType.GuildEnvoy:          return "guild_envoy";
                case MorningEventType.MysteryBox:          return "mystery_box";
                case MorningEventType.RefugeeHelp:         return "refugee";
                case MorningEventType.Collector:           return "collector";
                case MorningEventType.BlackMarket:         return "black_market";
                default:                                   return type.ToString();
            }
        }

        #endregion

        #region 이벤트 선택

        /// <summary>
        /// 가중치 기반 랜덤 이벤트 타입 선택
        /// </summary>
        public MorningEventType SelectRandomEventType()
        {
            float[] weights = Config.eventWeights;
            bool blockInvestor = GameManager.Instance.GameData.hasPendingInvestment;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                // 투자 결과 대기 중이면 SuspiciousInvestor(index 2) 가중치 제외
                if (blockInvestor && i == (int)MorningEventType.SuspiciousInvestor) continue;
                total += weights[i];
            }

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (blockInvestor && i == (int)MorningEventType.SuspiciousInvestor) continue;
                cumulative += weights[i];
                if (roll < cumulative)
                    return (MorningEventType)i;
            }
            return MorningEventType.WeaponEnhance;
        }

        #endregion

        #region 이벤트 진입점

        /// <summary>
        /// VisitorNPC 클릭 시 호출 — 이벤트 타입별 전용 View/Controller로 전달
        /// </summary>
        public void StartEvent(MorningEventType type, VisitorNPC npc)
        {
            if (IsEventCompleted)
            {
                Log.Info("[MorningEventManager] 오늘의 이벤트 상호작용이 이미 완료되었습니다.");
                TimeManager.Instance?.ResumeTime();
                npc?.EndInteraction();
                return;
            }

            // 각 패널은 GetOrInstantiatePanel로 인스턴스화(컨트롤러 등록)된 뒤
            // OpenEvent → 인트로 대화 종료 후 OnOpened()에서 열린다.
            switch (type)
            {
                case MorningEventType.WeaponEnhance:       Open<WeaponEnhanceEventView, WeaponEnhanceEventController>(npc); break;
                case MorningEventType.WeaponExchange:      Open<WeaponExchangeEventView, WeaponExchangeEventController>(npc); break;
                case MorningEventType.SuspiciousInvestor:  Open<InvestorEventView, InvestorEventController>(npc); break;
                case MorningEventType.WanderingBlacksmith: Open<BlacksmithEventView, BlacksmithEventController>(npc); break;
                case MorningEventType.GuildEnvoy:          Open<GuildEnvoyEventView, GuildEnvoyEventController>(npc); break;
                case MorningEventType.MysteryBox:          Open<MysteryBoxEventView, MysteryBoxEventController>(npc); break;
                case MorningEventType.RefugeeHelp:         Open<RefugeeEventView, RefugeeEventController>(npc); break;
                case MorningEventType.Collector:           Open<CollectorEventView, CollectorEventController>(npc); break;
                case MorningEventType.BlackMarket:         Open<BlackMarketEventView, BlackMarketEventController>(npc); break;
                default:
                    Log.Warn($"[MorningEventManager] 알 수 없는 이벤트 타입: {type}");
                    npc?.EndInteraction();
                    break;
            }
        }

        private void Open<TView, TController>(VisitorNPC npc)
            where TView : MorningEventViewBase
            where TController : MorningEventControllerBase<TView>
        {
            // 패널을 인스턴스화(컨트롤러 등록)하되, 인트로 대화 동안 보이지 않도록 숨겨둔다.
            // 대화 종료 후 OpenPanelAndShow()의 OpenPanel에서 표시된다.
            var view = UIManager.Instance?.GetOrInstantiatePanel<TView>();
            view?.gameObject.SetActive(false);
            UIControllerManager.Instance.GetController<TController>()?.OpenEvent(npc);
        }

        #endregion

        #region 1. 랜덤 무기 강화 (신비한 마법사)

        /// <summary>
        /// 무기 등급을 확률에 따라 변동시킨다.
        /// </summary>
        public (bool success, int gradeDelta, string message) ExecuteWeaponEnhance(WeaponInstance weapon)
        {
            if (weapon == null)
                return (false, 0, L("MorningEvent_SelectWeaponFirst"));

            int currentGrade = (int)weapon.currentGrade;
            float roll = Random.value;

            float minus1 = Config.enhanceMinus1Chance;
            float zero   = minus1 + Config.enhanceZeroChance;
            float plus1  = zero   + Config.enhancePlus1Chance;
            float plus2  = plus1  + Config.enhancePlus2Chance;

            int delta;
            if (roll < minus1)        delta = -1;
            else if (roll < zero)     delta = 0;
            else if (roll < plus1)    delta = 1;
            else if (roll < plus2)    delta = 2;
            else                      delta = 3;

            int newGrade = Mathf.Clamp(currentGrade + delta, 0, 4);
            int actualDelta = newGrade - currentGrade;

            if (actualDelta < 0)
            {
                for (int i = 0; i < -actualDelta; i++)
                    weapon.Downgrade();
            }
            else
            {
                for (int i = 0; i < actualDelta; i++)
                {
                    weapon.enforceLevel = weapon.MaxEnforceLevel; // Evolve 조건 충족
                    BlacksmithManager.Instance.EvolveWeapon(weapon);
                }
            }

            string message = actualDelta switch
            {
                > 0 => L("WeaponEnhance_GradeUp", ("delta", actualDelta),
                           ("from", (Grade)currentGrade), ("to", (Grade)newGrade)),
                0   => L("WeaponEnhance_GradeSame"),
                _   => L("WeaponEnhance_GradeDown", ("delta", -actualDelta),
                           ("from", (Grade)currentGrade), ("to", (Grade)newGrade))
            };

            Log.Info($"[MorningEventManager] 무기 강화: {weapon.weaponData?.weaponName} {(Grade)currentGrade}→{(Grade)newGrade}");
            SendEventResolved(MorningEventType.WeaponEnhance, "accept");
            return (true, actualDelta, message);
        }

        #endregion

        #region 2. 교환 상인

        /// <summary>
        /// 교환 결과 등급별 확률 테이블 반환 (index = Grade enum 순서).
        /// </summary>
        public float[] GetExchangeGradeTable(WeaponInstance a, WeaponInstance b)
        {
            if (a == null || b == null) return null;
            int sum = (int)a.currentGrade + (int)b.currentGrade;
            return Config.GetExchangeTable(sum);
        }

        /// <summary>
        /// 무기 2개를 소비하고 결과 등급의 랜덤 무기를 지급한다.
        /// </summary>
        public (bool success, WeaponInstance result, string message) ExecuteWeaponExchange(WeaponInstance a, WeaponInstance b)
        {
            if (a == null || b == null)
                return (false, null, L("WeaponExchange_SelectTwo"));

            int sum = (int)a.currentGrade + (int)b.currentGrade;
            float[] table = Config.GetExchangeTable(sum);
            Grade resultGrade = RollGradeFromTable(table);

            var candidates = DataManager.Instance.GetWeaponsByGrade(resultGrade);
            if (candidates == null || candidates.Count == 0)
                return (false, null, L("WeaponExchange_NoResultData"));

            WeaponData picked = candidates[Random.Range(0, candidates.Count)];
            WeaponInstance resultWeapon = new WeaponInstance(picked);

            InventoryManager.Instance.RemoveWeapon(a);
            InventoryManager.Instance.RemoveWeapon(b);
            InventoryManager.Instance.AddWeapon(resultWeapon);

            string message = L("WeaponExchange_Result", ("grade", resultGrade), ("name", picked.DisplayName));
            Log.Info($"[MorningEventManager] 무기 교환: {a.weaponData?.weaponName}+{b.weaponData?.weaponName} → {picked.weaponName}({resultGrade})");
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.WeaponExchange");
            SendEventResolved(MorningEventType.WeaponExchange, "accept");
            return (true, resultWeapon, message);
        }

        private Grade RollGradeFromTable(float[] table)
        {
            float roll = Random.value;
            float cumulative = 0f;
            for (int i = 0; i < table.Length; i++)
            {
                cumulative += table[i];
                if (roll < cumulative) return (Grade)i;
            }
            return (Grade)(table.Length - 1);
        }

        #endregion

        #region 3. 수상한 투자자

        /// <summary>
        /// 투자 즉시 결과 확정 후 PlayerData에 저장. 다음날 Morning에 NPC 등장.
        /// </summary>
        public (bool success, string message) ExecuteInvestment(int amount)
        {
            int currentDay = GameManager.Instance.GameData.currentDay;
            int maxAmount  = Config.investMinGold + currentDay * Config.investMaxGoldPerDay;

            if (amount < Config.investMinGold)
                return (false, L("Investor_MinAmount", ("gold", Config.investMinGold.ToString("N0"))));
            if (amount > maxAmount)
                return (false, L("Investor_MaxAmount", ("gold", maxAmount.ToString("N0"))));
            if (!EconomyManager.Instance.SpendGold(amount, "수상한 투자자 투자"))
                return (false, M("Economy_NotEnoughGold"));

            float roll    = Random.value;
            float lose    = Config.investLoseChance;
            float success = lose + Config.investSuccessChance;
            float big     = success + Config.investBigChance;

            int returned;
            string resultLabel;
            if (roll < lose)
            {
                returned = 0;
                resultLabel = "먹튀";
            }
            else if (roll < success)
            {
                returned = Mathf.RoundToInt(amount * Config.investSuccessMulti);
                resultLabel = "성공";
            }
            else if (roll < big)
            {
                returned = Mathf.RoundToInt(amount * Config.investBigMulti);
                resultLabel = "대성공";
            }
            else
            {
                returned = Mathf.RoundToInt(amount * Config.investJackpotMulti);
                resultLabel = "대박";
            }

            var pd = GameManager.Instance.GameData;
            pd.hasPendingInvestment          = true;
            pd.lastInvestorDay               = pd.currentDay;
            pd.pendingInvestorReturnedGold   = returned;   // 0 = 먹튀
            pd.pendingInvestorResultDialogueID = returned > 0
                ? resultLabel switch
                {
                    "대박"   => "Investor_Jackpot",
                    "대성공" => "Investor_GreatSuccess",
                    _        => "Investor_Success"
                }
                : null;

            Log.Info($"[MorningEventManager] 투자 결과 확정: {resultLabel}, {amount}G → {returned}G (내일 등장)");
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.Investment");
            SendEventResolved(MorningEventType.SuspiciousInvestor, "accept");
            return (true, L("Investor_Invested", ("gold", amount.ToString("N0"))));
        }

        #endregion

        #region 4. 떠돌이 대장장이

        /// <summary>
        /// 무료 강화 실행 (재료 소모 없음)
        /// </summary>
        public (bool success, int plusAmount, List<string> enforcedEffectIDs, string message) ExecuteWanderingBlacksmithEnhance(WeaponInstance weapon)
        {
            if (weapon == null)
                return (false, 0, null, L("MorningEvent_SelectWeaponFirst"));

            float roll = Random.value;
            float p1 = Config.blacksmithEnhancePlus1;
            float p2 = p1 + Config.blacksmithEnhancePlus2;

            int plus;
            if (roll < p1)       plus = 1;
            else if (roll < p2)  plus = 2;
            else                 plus = 3;

            var enforcedIDs = new List<string>();
            int applied = 0;
            for (int i = 0; i < plus; i++)
            {
                var id = weapon.Enforce();
                if (id == null) break;   // 만강 도달 - 더 이상 적용 불가
                enforcedIDs.Add(id);
                applied++;
            }

            // 실제 적용 횟수 기준으로 안내한다. 만강 무기에 허위 "+N 강화 성공!"을 띄우지 않는다
            if (applied == 0)
            {
                Log.Info($"[MorningEventManager] 떠돌이 대장장이 강화 불가(만강): {weapon.weaponData?.weaponName}");
                return (false, 0, enforcedIDs, L("WeaponEnhance_AlreadyMax"));
            }

            string message = L("WeaponEnhance_Result", ("applied", applied), ("level", weapon.enforceLevel));
            Log.Info($"[MorningEventManager] 떠돌이 대장장이 강화: {weapon.weaponData?.weaponName} +{applied} → +{weapon.enforceLevel}");
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.WanderingBlacksmithEnhance");
            return (true, applied, enforcedIDs, message);
        }

        #endregion

        /// <summary>
        /// 강화 또는 리롤 중 하나를 자동으로 선택해 적용한다 (50% 확률).
        /// </summary>
        public (bool success, bool isEnhance, List<WeaponEffect> previousEffects, int enforceLevelDelta, List<string> enforcedEffectIDs, string message) ExecuteWanderingBlacksmithAuto(WeaponInstance weapon)
        {
            if (weapon == null)
                return (false, false, null, 0, null, L("MorningEvent_SelectWeaponFirst"));

            int previousEnforceLevel = weapon.enforceLevel;
            // 만강(강화 불가) 무기는 강화 branch가 아무것도 못 하므로 리롤로 강제한다
            bool doEnhance = weapon.CanEnforce && Random.value < 0.5f;

            if (doEnhance)
            {
                // Enforce는 효과의 currentValue를 제자리에서 바꾸므로, before 표시용은 깊은 복사 스냅샷이 필요하다.
                var previousEffects = weapon.effects?.Select(e => new WeaponEffect(e)).ToList() ?? new List<WeaponEffect>();
                var (success, plus, enforcedIDs, msg) = ExecuteWanderingBlacksmithEnhance(weapon);
                int enforceLevelDelta = weapon.enforceLevel - previousEnforceLevel;
                SendEventResolved(MorningEventType.WanderingBlacksmith, "accept");
                return (success, true, previousEffects, enforceLevelDelta, enforcedIDs, L("BlacksmithEvent_EnhanceResult", ("message", msg)));
            }
            else
            {
                // Reroll은 새 효과를 반환만 하므로, 옛 효과를 스냅샷한 뒤 ApplyRerolledEffects로 실제 적용한다.
                var previousEffects = weapon.effects?.Select(e => new WeaponEffect(e)).ToList() ?? new List<WeaponEffect>();
                var newEffects = weapon.Reroll(new HashSet<int>(), free: true);
                weapon.ApplyRerolledEffects(newEffects);
                int enforceLevelDelta = weapon.enforceLevel - previousEnforceLevel;
                Log.Info($"[MorningEventManager] 떠돌이 대장장이 자동 리롤: {weapon.weaponData?.weaponName}");
                GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.WanderingBlacksmithReroll");
                SendEventResolved(MorningEventType.WanderingBlacksmith, "accept");
                return (true, false, previousEffects, enforceLevelDelta, null, L("BlacksmithEvent_RerollResult"));
            }
        }

        #region 5. 길드 사절단

        /// <summary>
        /// 평판 등급에 따라 선물/강제납부를 결정하고 즉시 처리한다.
        /// </summary>
        public (bool isGift, int goldAmount, List<string> materialIDs, string message) ExecuteGuildEnvoy()
        {
            ReputationLevel level = ReputationManager.Instance.CurrentLevel;
            int levelIndex = (int)level;

            bool isGift = Random.value < Config.guildGiftChance[levelIndex];
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                isGift = true;   // 튜토리얼 3단계: 선물 수령 고정(강제납부 없음)

            if (isGift)
            {
                float goldRoll = Random.value;
                int gold;
                if (goldRoll < Config.guildGoldLowChance)
                    gold = Config.guildGoldLow;
                else if (goldRoll < Config.guildGoldLowChance + Config.guildGoldMidChance)
                    gold = Config.guildGoldMid;
                else
                    gold = Config.guildGoldHigh;

                EconomyManager.Instance.AddGold(gold, "길드 사절단 선물");

                int matCount = Config.guildMaterialCount;
                var materialIDs = AddRandomRewardMaterials(matCount);

                string message = L("GuildEnvoy_GiftResult", ("gold", gold), ("count", matCount));
                Log.Info($"[MorningEventManager] 길드 사절단 선물: {gold}G + 재료 {matCount}개");
                GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.GuildEnvoyGift");
                SendEventResolved(MorningEventType.GuildEnvoy, "accept");
                return (true, gold, materialIDs, message);
            }
            else
            {
                float goldRoll = Random.value;
                int required = goldRoll < Config.guildForceLowChance
                    ? Config.guildForceLow
                    : Config.guildForceHigh;

                int actualPay = Mathf.Min(required, EconomyManager.Instance.CurrentGold);
                EconomyManager.Instance.SpendGold(actualPay, "길드 사절단 강제납부");

                string message = L("GuildEnvoy_TaxResult", ("gold", actualPay));
                Log.Info($"[MorningEventManager] 길드 사절단 강제납부: {actualPay}G");
                GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.GuildEnvoyForcePay");
                SendEventResolved(MorningEventType.GuildEnvoy, "accept");
                return (false, -actualPay, new List<string>(), message);
            }
        }

        #endregion

        #region 6. 수수께끼 상자

        /// <summary>
        /// 오늘 등장할 상자 등급(0=일반, 1=희귀, 2=신비)을 반환한다.
        /// 이미 결정된 경우 저장된 값을 사용하고, 미결정인 경우 가중치 기반으로 결정 후 저장한다.
        /// </summary>
        public int GetRandomMysteryBoxTier()
        {
            var pd = GameManager.Instance.GameData;
            if (pd.mysteryBoxTier >= 0)
                return pd.mysteryBoxTier;

            float[] weights = Config.boxTierWeights;
            float total = 0f;
            foreach (float w in weights) total += w;

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            int tier = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) { tier = i; break; }
            }

            pd.mysteryBoxTier = tier;
            // 등급 추첨 확정 - 재시작으로 등급을 다시 굴리는 것 차단
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.MysteryBoxTier");
            return tier;
        }

        /// <summary>
        /// 상자 등급에 따라 가격을 반환한다. (고정가 — 일수 비례 제거)
        /// </summary>
        public int GetMysteryBoxCost(int tier)
        {
            return tier switch
            {
                0 => Config.boxNormalCost,
                1 => Config.boxRareCost,
                2 => Config.boxMythicCost,
                _ => Config.boxNormalCost
            };
        }

        /// <summary>
        /// 상자 등급별 보상 종류와 확률 목록을 반환한다 (확률 안내 패널용).
        /// 순서: 재료 → 골드 → 무기 → 꽝. 무기는 tier별 등급 세부를 함께 표시한다.
        /// </summary>
        public List<MysteryBoxOdds> GetMysteryBoxOdds(int tier)
        {
            (float mat, float gold, float wpn, string matLabel, string weaponGrades) = tier switch
            {
                0 => (Config.boxNormalMaterial, Config.boxNormalGold, Config.boxNormalWeapon, L("MysteryBox_OddsMaterial1"), WeaponGradeLabel(Grade.Uncommon)),
                1 => (Config.boxRareMaterial,   Config.boxRareGold,   Config.boxRareWeapon,   L("MysteryBox_OddsMaterial2"), $"{WeaponGradeLabel(Grade.Rare)}/{WeaponGradeLabel(Grade.Epic)}"),
                2 => (Config.boxMythicMaterial, Config.boxMythicGold, Config.boxMythicWeapon, L("MysteryBox_OddsMaterial3"), WeaponGradeLabel(Grade.Legendary)),
                _ => (Config.boxNormalMaterial, Config.boxNormalGold, Config.boxNormalWeapon, L("MysteryBox_OddsMaterial1"), WeaponGradeLabel(Grade.Uncommon))
            };

            float nothing = Mathf.Max(0f, 1f - mat - gold - wpn);

            return new List<MysteryBoxOdds>
            {
                new MysteryBoxOdds(matLabel, mat),
                new MysteryBoxOdds(L("MysteryBox_OddsGold"), gold),
                new MysteryBoxOdds(L("MysteryBox_OddsWeapon", ("grades", weaponGrades)), wpn),
                new MysteryBoxOdds(L("MysteryBox_OddsNothing"), nothing),
            };
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

        private static string WeaponGradeLabel(Grade grade) => UITranslator.GetString(grade);

        /// <summary>
        /// 상자 구매 및 즉시 개봉. tier: 0=일반, 1=희귀, 2=신비
        /// </summary>
        public (bool success, MysteryBoxRewardType rewardType, int goldAmount, List<string> materialIDs, WeaponInstance weapon, string message) ExecuteMysteryBox(int tier)
        {
            int cost = GetMysteryBoxCost(tier);
            if (!EconomyManager.Instance.SpendGold(cost, $"수수께끼의 상자(tier {tier})"))
                return (false, MysteryBoxRewardType.Nothing, 0, null, null, M("Economy_NotEnoughGold"));

            var result = tier switch
            {
                0 => OpenBoxNormal(),
                1 => OpenBoxRare(),
                2 => OpenBoxMythic(),
                _ => OpenBoxNormal()
            };

            Log.Info($"[MorningEventManager] 수수께끼 상자(tier {tier}) 개봉: {result.message}");
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.MysteryBox");
            SendEventResolved(MorningEventType.MysteryBox, "accept");
            return (true, result.rewardType, result.goldAmount, result.materialIDs, result.weapon, result.message);
        }

        private (MysteryBoxRewardType rewardType, int goldAmount, List<string> materialIDs, WeaponInstance weapon, string message) OpenBoxNormal()
        {
            float roll = Random.value;
            float mat  = Config.boxNormalMaterial;
            float gold = mat + Config.boxNormalGold;
            float wpn  = gold + Config.boxNormalWeapon;

            if (roll < mat)
            {
                var ids = AddRandomRewardMaterials(1);
                return (MysteryBoxRewardType.Material, 0, ids, null, L("MysteryBox_RewardMaterial1"));
            }
            if (roll < gold)
            {
                int amount = Random.Range(Config.boxNormalGoldMin, Config.boxNormalGoldMax + 1);
                EconomyManager.Instance.AddGold(amount, "일반 상자");
                return (MysteryBoxRewardType.Gold, amount, null, null, L("MysteryBox_RewardGold", ("gold", amount)));
            }
            if (roll < wpn)
            {
                var w = AddRandomWeapon(Grade.Uncommon);
                if (w == null)
                    return FallbackWeaponToGold(Config.boxNormalGoldMin, Config.boxNormalGoldMax, "일반 상자(무기 대체)");
                return (MysteryBoxRewardType.Weapon, 0, null, w, L("MysteryBox_RewardWeaponUncommon"));
            }
            return (MysteryBoxRewardType.Nothing, 0, null, null, L("MysteryBox_RewardNothing"));
        }

        private (MysteryBoxRewardType rewardType, int goldAmount, List<string> materialIDs, WeaponInstance weapon, string message) OpenBoxRare()
        {
            float roll = Random.value;
            float mat  = Config.boxRareMaterial;
            float gold = mat + Config.boxRareGold;
            float wpn  = gold + Config.boxRareWeapon;

            if (roll < mat)
            {
                var ids = AddRandomRewardMaterials(2);
                return (MysteryBoxRewardType.Material, 0, ids, null, L("MysteryBox_RewardMaterial2"));
            }
            if (roll < gold)
            {
                int amount = Random.Range(Config.boxRareGoldMin, Config.boxRareGoldMax + 1);
                EconomyManager.Instance.AddGold(amount, "희귀 상자");
                return (MysteryBoxRewardType.Gold, amount, null, null, L("MysteryBox_RewardGold", ("gold", amount)));
            }
            if (roll < wpn)
            {
                Grade g = Random.value < 0.5f ? Grade.Rare : Grade.Epic;
                var w = AddRandomWeapon(g);
                if (w == null)
                    return FallbackWeaponToGold(Config.boxRareGoldMin, Config.boxRareGoldMax, "희귀 상자(무기 대체)");
                return (MysteryBoxRewardType.Weapon, 0, null, w, L("MysteryBox_RewardWeaponGrade", ("grade", UITranslator.GetString(g))));
            }
            return (MysteryBoxRewardType.Nothing, 0, null, null, L("MysteryBox_RewardNothing"));
        }

        private (MysteryBoxRewardType rewardType, int goldAmount, List<string> materialIDs, WeaponInstance weapon, string message) OpenBoxMythic()
        {
            float roll = Random.value;
            float mat  = Config.boxMythicMaterial;
            float gold = mat + Config.boxMythicGold;
            float wpn  = gold + Config.boxMythicWeapon;

            if (roll < mat)
            {
                var ids = AddRandomRewardMaterials(3);
                return (MysteryBoxRewardType.Material, 0, ids, null, L("MysteryBox_RewardMaterial3"));
            }
            if (roll < gold)
            {
                int amount = Random.Range(Config.boxMythicGoldMin, Config.boxMythicGoldMax + 1);
                EconomyManager.Instance.AddGold(amount, "신비 상자");
                return (MysteryBoxRewardType.Gold, amount, null, null, L("MysteryBox_RewardGold", ("gold", amount)));
            }
            if (roll < wpn)
            {
                // 신비 상자 무기 보상은 전설 확정 (밸런스: 전설 도박 정체성, EV 재계산 근거)
                var w = AddRandomWeapon(Grade.Legendary);
                if (w == null)
                    return FallbackWeaponToGold(Config.boxMythicGoldMin, Config.boxMythicGoldMax, "신비 상자(무기 대체)");
                return (MysteryBoxRewardType.Weapon, 0, null, w, L("MysteryBox_RewardWeaponGrade", ("grade", UITranslator.GetString(Grade.Legendary))));
            }
            return (MysteryBoxRewardType.Nothing, 0, null, null, L("MysteryBox_RewardNothing"));
        }

        #endregion

        #region 7. 난민 돕기

        public int GetRefugeeCost()
            => Config.refugeeCost;

        public (bool success, bool actuallyDonated, int repChange, string message) ExecuteRefugeeHelp(bool isDonating)
        {
            int repChange = 0;
            string message;

            if (isDonating)
            {
                int cost = GetRefugeeCost();
                if (!EconomyManager.Instance.SpendGold(cost, "난민 돕기 기부"))
                {
                    bool penalized = Random.value < Config.refugeeRejectRepPenaltyChance;
                    if (penalized)
                    {
                        repChange = -Config.refugeeRejectRepPenalty;
                        ReputationManager.Instance.AddReputation(repChange, "난민 거절(골드 부족)");
                        message = L("Refugee_RejectNoGoldRep", ("rep", repChange));
                    }
                    else
                    {
                        message = L("Refugee_RejectNoGold");
                    }
                    Log.Info($"[MorningEventManager] 난민 돕기: 골드 부족 자동 거절, rep={repChange}");
                    SendEventResolved(MorningEventType.RefugeeHelp, "reject");
                    // 골드 부족으로 실제로는 거절 처리됐으므로 actuallyDonated=false
                    return (true, false, repChange, message);
                }

                bool highRep = Random.value >= Config.refugeeDonateRepLowChance;
                repChange = highRep ? Config.refugeeDonateRepHigh : Config.refugeeDonateRepLow;
                ReputationManager.Instance.AddReputation(repChange, "난민 돕기");
                message = L("Refugee_Donated", ("rep", repChange));
            }
            else
            {
                bool penalized = Random.value < Config.refugeeRejectRepPenaltyChance;
                if (penalized)
                {
                    repChange = -Config.refugeeRejectRepPenalty;
                    ReputationManager.Instance.AddReputation(repChange, "난민 거절");
                    message = L("Refugee_RejectedRep", ("rep", repChange));
                }
                else
                {
                    message = L("Refugee_Rejected");
                }
            }

            Log.Info($"[MorningEventManager] 난민 돕기: donate={isDonating}, rep={repChange}");
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.RefugeeHelp");
            SendEventResolved(MorningEventType.RefugeeHelp, isDonating ? "accept" : "reject");
            // 여기 도달 시: 기부는 SpendGold 성공한 경우뿐(actuallyDonated=true), 거절은 false
            return (true, isDonating, repChange, message);
        }

        #endregion

        #region 8. 수집가

        /// <summary>
        /// 수집가에게 판매 가능한 무기 목록 반환 (최소 등급 이상)
        /// </summary>
        public List<WeaponInstance> GetCollectorEligibleWeapons()
        {
            int minGrade = (int)Config.collectorMinGrade;
            return InventoryManager.Instance.GetAvailableWeapons()
                .Where(w => (int)w.currentGrade >= minGrade)
                .ToList();
        }

        /// <summary>
        /// 수집가 판매 배수를 가중 롤한다. (×3 / ×4 / ×5)
        /// </summary>
        public float RollCollectorMultiplier()
        {
            float roll = Random.value;
            float mult3 = Config.collectorMult3Chance;
            float mult4 = mult3 + Config.collectorMult4Chance;

            if (roll < mult3)  return 3f;
            if (roll < mult4)  return 4f;
            return 5f;
        }

        /// <summary>
        /// 수집가에게 판매. 배수는 호출자(미니게임)가 결정해 전달한다.
        /// </summary>
        public (bool success, int goldAmount, float multiplier, string message) ExecuteCollectorSell(WeaponInstance weapon, float multiplier)
        {
            if (weapon == null)
                return (false, 0, 0f, L("MorningEvent_SelectWeaponFirst"));

            int basePrice  = weapon.weaponData?.basePrice ?? 0;
            int goldAmount = Mathf.RoundToInt(basePrice * multiplier);

            InventoryManager.Instance.RemoveWeapon(weapon);
            EconomyManager.Instance.AddGold(goldAmount, "수집가 판매");

            string message = L("Collector_SoldResult", ("multiplier", multiplier), ("gold", goldAmount));
            Log.Info($"[MorningEventManager] 수집가 판매: {weapon.weaponData?.weaponName} × {multiplier} = {goldAmount}G");
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.CollectorSell");
            SendEventResolved(MorningEventType.Collector, "accept");
            return (true, goldAmount, multiplier, message);
        }

        #endregion

        #region 9. 암시장 상인

        /// <summary>
        /// 등급 조건 이상의 무기 중 1개를 랜덤 선정하여 반환한다. weapon이 null이면 재고 없음.
        /// </summary>
        public (WeaponInstance weapon, int discountPrice) GetBlackMarketOffer()
        {
            var pd = GameManager.Instance.GameData;

            // 패널이 열린 채로 종료 후 불러오기 — 저장된 무기 복원
            // JsonUtility는 null 클래스를 {}로 직렬화하므로 weaponDataID로 유효성 검증
            if (pd.blackMarketOfferSaveData != null && !string.IsNullOrEmpty(pd.blackMarketOfferSaveData.weaponDataID))
            {
                var weaponData = DataManager.Instance.GetWeapon(pd.blackMarketOfferSaveData.weaponDataID);
                if (weaponData != null)
                {
                    var effects = InventoryManager.ReifyEffectsFromSave(pd.blackMarketOfferSaveData.effects);
                    var restored = new WeaponInstance(weaponData, effects, pd.blackMarketOfferSaveData);
                    int cachedPrice = Mathf.RoundToInt(restored.weaponData.basePrice * Config.blackMarketDiscount);
                    return (restored, cachedPrice);
                }
            }

            int minGrade = (int)Config.blackMarketMinGrade;
            var candidates = new List<WeaponData>();
            for (int g = minGrade; g <= 4; g++)
                candidates.AddRange(DataManager.Instance.GetWeaponsByGrade((Grade)g));

            if (candidates.Count == 0) return (null, 0);

            WeaponData picked = candidates[Random.Range(0, candidates.Count)];
            WeaponInstance offer = new WeaponInstance(picked);
            int discountPrice = Mathf.RoundToInt(picked.basePrice * Config.blackMarketDiscount);

            pd.blackMarketOfferSaveData = offer.ToSaveData();
            // 제안 무기 추첨 확정 - 재시작으로 제안을 다시 굴리는 것 차단
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.BlackMarketOffer");

            return (offer, discountPrice);
        }

        /// <summary>
        /// 암시장에서 구매.
        /// </summary>
        public (bool success, string message) ExecuteBlackMarketBuy(WeaponInstance weapon, int price)
        {
            if (weapon == null)
                return (false, L("BlackMarket_NoWeapon"));

            // 인벤토리 상한 검사 - 비용 차감 전에 차단한다
            if (!InventoryManager.Instance.CanAddWeapon())
                return (false, L("BlackMarket_InventoryFull"));

            if (!EconomyManager.Instance.SpendGold(price, "암시장 구매"))
                return (false, M("Economy_NotEnoughGold"));

            InventoryManager.Instance.AddWeapon(weapon);
            ReputationManager.Instance.AddReputation(-Config.blackMarketRepPenalty, "암시장 구매");

            GameManager.Instance.GameData.blackMarketOfferSaveData = null;

            string message = L("BlackMarket_Purchased",
                ("name", weapon.weaponData?.DisplayName), ("price", price.ToString("N0")),
                ("penalty", Config.blackMarketRepPenalty));
            Log.Info($"[MorningEventManager] 암시장 구매: {weapon.weaponData?.weaponName}, {price}G, rep -{Config.blackMarketRepPenalty}");
            GameManager.Instance?.SaveAfterCommittedAction("MorningEvent.BlackMarketBuy");
            SendEventResolved(MorningEventType.BlackMarket, "accept");
            return (true, message);
        }

        #endregion

        #region 내부 헬퍼

        private List<string> AddRandomRewardMaterials(int count)
        {
            var added = new List<string>();
            if (Config.rewardMaterialPool == null || Config.rewardMaterialPool.Length == 0)
            {
                Log.Warn("[MorningEventManager] rewardMaterialPool이 비어있습니다. Inspector에서 설정하세요.");
                return added;
            }

            for (int i = 0; i < count; i++)
            {
                string materialID = Config.rewardMaterialPool[
                    Random.Range(0, Config.rewardMaterialPool.Length)];
                InventoryManager.Instance.AddMaterial(materialID, 1);
                added.Add(materialID);
            }
            return added;
        }

        private WeaponInstance AddRandomWeapon(Grade grade)
        {
            // 인벤토리 상한 초과 지급 방지 - null 반환 시 호출부가 골드 대체 지급
            if (!InventoryManager.Instance.CanAddWeapon()) return null;

            var candidates = DataManager.Instance.GetWeaponsByGrade(grade);
            if (candidates == null || candidates.Count == 0) return null;
            WeaponData picked = candidates[Random.Range(0, candidates.Count)];
            var instance = new WeaponInstance(picked);
            InventoryManager.Instance.AddWeapon(instance);
            return instance;
        }

        /// <summary>
        /// 상자 무기 당첨인데 지급 불가(인벤 만석 등)일 때 골드 대체 보상을 지급한다
        /// </summary>
        private (MysteryBoxRewardType rewardType, int goldAmount, List<string> materialIDs, WeaponInstance weapon, string message)
            FallbackWeaponToGold(int goldMin, int goldMax, string source)
        {
            int amount = Random.Range(goldMin, goldMax + 1);
            EconomyManager.Instance.AddGold(amount, source);
            return (MysteryBoxRewardType.Gold, amount, null, null, L("MysteryBox_RewardGoldInstead", ("gold", amount)));
        }

        #endregion
    }
}
