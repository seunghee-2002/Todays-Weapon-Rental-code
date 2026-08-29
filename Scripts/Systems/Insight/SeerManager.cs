// Scripts/Systems/SeerManager.cs
using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    public enum SeerResultType { AbstractIndicator }

    public enum AbstractLevel { 암담, 험난, 보통, 길조, 대길조 }

    public enum LuckLevel { 흉, 소길, 중길, 대길 }

    [Serializable]
    public class SeerResult
    {
        public SeerResultType resultType;

        // 수정구 서사는 **세이브에 구워진다**(GameData.seerResults). 그래서 번역된 문장이
        // 아니라 SO 원문 + 키를 저장하고, 표시할 때 푼다 — 문장을 구우면 상담할 때의
        // 언어가 그대로 굳어 나중에 언어를 바꿔도 안 바뀐다.
        // 모험가 이름(AdventurerInstance.nameKey)과 같은 방식이다.
        public string description;      // SO 원문. 키가 없는 구버전 세이브에선 이게 그대로 나온다
        public string descriptionKey;   // Data 테이블 키

        public AbstractLevel abstractLevel;
        public LuckLevel luckLevel;

        /// <summary>
        /// 현재 언어로 푼 수정구 서사. 언어를 바꾸면 이미 본 결과도 함께 바뀐다.
        /// </summary>
        public string DisplayDescription
            => DataLocalizer.Get(descriptionKey, description).Replace("\\n", "\n");
    }

    public class SeerManager : BaseManager<SeerManager>
    {
        [SerializeField] private SerializableDictionary<string, SeerResult> seerResults = new();

        #region 초기화

        public void Initialize(GameData gameData)
        {
            seerResults.Clear();
            if (gameData.seerResults != null)
            {
                foreach (var kv in gameData.seerResults)
                    seerResults[kv.Key] = kv.Value;
            }
            Log.Info("[SeerManager] Initialized.");
        }

        public void SaveToGameData(GameData gameData)
        {
            gameData.seerResults.Clear();
            foreach (var kv in seerResults)
                gameData.seerResults[kv.Key] = kv.Value;
        }

        private void Start()
        {
            SubscribeEvents();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged += OnNewDay;
        }

        private void UnsubscribeEvents()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged -= OnNewDay;
        }

        #endregion

        #region View로부터 호출되는 메서드

        public int GetSeerCost()
        {
            // 1~7단계 점술 비용은 전령(본부)이 보전 — 튜토리얼 중엔 0원.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                return 0;

            // 기본가 x 주차 계단 배율 — "점술가의 눈이 높아졌다"
            // (골드_경제_구조.md 4장: 운세 보정의 값어치가 판돈에 비례하므로 고정가면 후반에 실질 무료가 된다)
            var cfg = ConfigManager.Instance.Seer;
            float mult = ConfigManager.Instance.PriceMult(p => p.seerCost);
            return Mathf.RoundToInt(cfg.seerBaseCost * mult);
        }

        public bool CanConsult(AdventurerInstance adventurer, DungeonData dungeon)
        {
            string key = MakeKey(adventurer, dungeon);
            return !seerResults.ContainsKey(key);
        }

        public SeerResult GetExistingResult(AdventurerInstance adventurer, DungeonData dungeon)
        {
            string key = MakeKey(adventurer, dungeon);
            return seerResults.TryGetValue(key, out var result) ? result : null;
        }

        /// <summary>
        /// 정상 상담. LUK 기반 가중치 랜덤으로 운세를 판정한다.
        /// </summary>
        public SeerResult Consult(AdventurerInstance adventurer, DungeonData dungeon)
        {
            if (!CanConsult(adventurer, dungeon))
            {
                Log.Warn("[SeerManager] Consult 조건 불충족");
                return null;
            }

            // 차감 실패 시 결과 생성/캐시 없이 중단
            if (!EconomyManager.Instance.SpendGold(GetSeerCost(), "점술가 상담"))
            {
                Log.Warn("[SeerManager] Consult: 골드 부족");
                return null;
            }

            string key    = MakeKey(adventurer, dungeon);
            var    result = GenerateResult(adventurer, dungeon, useEasterEgg: false);
            seerResults[key] = result;

            QuestManager.Instance?.UpdateProgress(QuestType.SeerComplete);
            Log.Info($"[SeerManager] Consult 완료 — Abstract:{result.abstractLevel} Luck:{result.luckLevel}");
            return result;
        }

        /// <summary>
        /// 이스터에그 상담. 수정구를 직접 건드렸을 때 호출된다.
        /// LUK 무시, SeerConfig.egg 가중치로 운세를 판정한다.
        /// </summary>
        public SeerResult ConsultEasterEgg(AdventurerInstance adventurer, DungeonData dungeon)
        {
            if (!CanConsult(adventurer, dungeon))
            {
                Log.Warn("[SeerManager] ConsultEasterEgg 조건 불충족");
                return null;
            }

            // 차감 실패 시 결과 생성/캐시 없이 중단
            if (!EconomyManager.Instance.SpendGold(GetSeerCost(), "점술가 상담 (이스터에그)"))
            {
                Log.Warn("[SeerManager] ConsultEasterEgg: 골드 부족");
                return null;
            }

            string key    = MakeKey(adventurer, dungeon);
            var    result = GenerateResult(adventurer, dungeon, useEasterEgg: true);
            seerResults[key] = result;

            Log.Info($"[SeerManager] ConsultEasterEgg 완료 — Abstract:{result.abstractLevel} Luck:{result.luckLevel}");
            return result;
        }

        /// <summary>
        /// 모험 시작 시 AdventureManager가 cumulativeModifier 초기값으로 사용.
        /// 점술 결과가 없으면 0 반환.
        /// </summary>
        public float GetLuckModifier(AdventurerInstance adventurer, DungeonData dungeon)
        {
            string key = MakeKey(adventurer, dungeon);
            if (!seerResults.TryGetValue(key, out var result))
                return 0f;

            var cfg = ConfigManager.Instance.Seer;
            return result.luckLevel switch
            {
                LuckLevel.흉   => cfg.luckModifierBad,
                LuckLevel.소길 => cfg.luckModifierMinor,
                LuckLevel.중길 => cfg.luckModifierGood,
                LuckLevel.대길 => cfg.luckModifierGreat,
                _              => 0f
            };
        }

        /// <summary>
        /// 첫 인사 대사. 비용은 {0}에 삽입된다.
        /// </summary>
        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        public string GetGreetingLine(int cost)
        {
            var cfg = ConfigManager.Instance.Seer;
            // PickRandom이 번역까지 마친 문장을 돌려주므로 {0} 치환은 그 뒤다.
            // 반대로 하면 번역문의 {0}이 그대로 화면에 뜬다.
            // 풀이 비었을 때의 폴백도 SO 대사(첫 줄)를 참조한다 — 같은 한국어를 별도 키로
            // 이중 관리하면 번역이 서로 갈린다.
            string template = SeerConfig.PickRandom(
                cfg.greetingLines,
                DataLocalizer.FromTable(DataLocalizer.SeerLineKey(nameof(cfg.greetingLines), 0)),
                nameof(cfg.greetingLines));
            return string.Format(template, $"{cost:N0}");
        }

        /// <summary>
        /// 점술 결과 직후 점술가가 LuckLevel에 따라 건네는 코멘트.
        /// </summary>
        public string GetSeerComment(LuckLevel luck)
        {
            var cfg = ConfigManager.Instance.Seer;
            return luck switch
            {
                LuckLevel.흉   => SeerConfig.PickRandom(cfg.commentBadLines,   L("Seer_CommentBad"),   nameof(cfg.commentBadLines)),
                LuckLevel.소길 => SeerConfig.PickRandom(cfg.commentMinorLines, L("Seer_CommentMinor"), nameof(cfg.commentMinorLines)),
                LuckLevel.중길 => SeerConfig.PickRandom(cfg.commentGoodLines,  L("Seer_CommentGood"),  nameof(cfg.commentGoodLines)),
                LuckLevel.대길 => SeerConfig.PickRandom(cfg.commentGreatLines, L("Seer_CommentGreat"), nameof(cfg.commentGreatLines)),
                _              => L("Seer_CommentUnknown")
            };
        }

        /// <summary>
        /// 수정구 직접 클릭(이스터에그) 대사. touchCount는 1부터 시작하는 세션 누적 클릭 수.
        /// </summary>
        public string GetEasterEggLine(int touchCount)
        {
            var cfg = ConfigManager.Instance.Seer;
            var (pool, path) = touchCount switch
            {
                1  => (cfg.easterEggLines1, nameof(cfg.easterEggLines1)),
                2  => (cfg.easterEggLines2, nameof(cfg.easterEggLines2)),
                _  => (cfg.easterEggLines3, nameof(cfg.easterEggLines3)),
            };
            return SeerConfig.PickRandom(pool, L("Seer_EasterEggTouch"), path);
        }

        /// <summary>
        /// 이스터에그 점술 발동 시 결과 코멘트 앞에 붙는 프리페이스 대사.
        /// </summary>
        public string GetEasterEggConsultPreface()
        {
            var cfg = ConfigManager.Instance.Seer;
            return SeerConfig.PickRandom(
                cfg.easterEggConsultPrefaceLines,
                L("Seer_EasterEggPreface"),
                nameof(cfg.easterEggConsultPrefaceLines));
        }

        /// <summary>
        /// 수정구 경고 대사 이후 next 클릭 시 나오는 후속 대사.
        /// </summary>
        public string GetEasterEggFollowUpLine()
        {
            var cfg = ConfigManager.Instance.Seer;
            return SeerConfig.PickRandom(
                cfg.easterEggFollowUpLines,
                DataLocalizer.FromTable(
                    DataLocalizer.SeerLineKey(nameof(cfg.easterEggFollowUpLines), 0)),
                nameof(cfg.easterEggFollowUpLines));
        }

        /// <summary>
        /// 재방문(이미 상담함) 대사. visitCount는 1부터 시작하는 세션 누적 방문 수.
        /// </summary>
        public string GetRevisitLine(int visitCount)
        {
            var cfg = ConfigManager.Instance.Seer;
            var (pool, path) = visitCount switch
            {
                1  => (cfg.revisitLines1, nameof(cfg.revisitLines1)),
                2  => (cfg.revisitLines2, nameof(cfg.revisitLines2)),
                _  => (cfg.revisitLines3, nameof(cfg.revisitLines3)),
            };
            return SeerConfig.PickRandom(pool, L("Seer_RevisitFallback"), path);
        }

        #endregion

        #region 이벤트 핸들러

        public void OnNewDay(int day)
        {
            seerResults.Clear();
        }

        #endregion

        #region 내부 메서드

        private static string MakeKey(AdventurerInstance adventurer, DungeonData dungeon)
            => $"{adventurer.instanceID}_{dungeon.StaticID}";

        private SeerResult GenerateResult(AdventurerInstance adventurer, DungeonData dungeon, bool useEasterEgg)
        {
            AbstractLevel abstractLevel = GenerateAbstractLevel(adventurer, dungeon);
            LuckLevel     luckLevel     = useEasterEgg
                ? GenerateEasterEggLuckLevel()
                : GenerateLuckLevel(adventurer);
            string description = BuildDescription(abstractLevel, luckLevel, out string descriptionKey);

            return new SeerResult
            {
                resultType     = SeerResultType.AbstractIndicator,
                description    = description,
                descriptionKey = descriptionKey,
                abstractLevel  = abstractLevel,
                luckLevel      = luckLevel,
            };
        }

        /// <summary>
        /// 모험가 최고 스탯 / 던전 임계값 + 오늘 ArmorType 최고 상성으로 5단계 판정.
        /// </summary>
        private AbstractLevel GenerateAbstractLevel(AdventurerInstance adventurer, DungeonData dungeon)
        {
            float bestStatScore = 0f;
            foreach (WeaponType wt in Enum.GetValues(typeof(WeaponType)))
            {
                float score = adventurer.GetStatScore(wt);
                if (score > bestStatScore) bestStatScore = score;
            }

            float statRatio = dungeon.baseStatThreshold > 0f
                ? bestStatScore / dungeon.baseStatThreshold
                : 0f;

            ArmorType todayArmorType = QuestBoardManager.Instance.GetTodayArmorType(dungeon.StaticID);
            float bestArmorBonus = 0f;
            foreach (WeaponType wt in Enum.GetValues(typeof(WeaponType)))
            {
                float bonus = TypeAdvantage.weaponArmorBonus[(int)wt, (int)todayArmorType];
                if (bonus > bestArmorBonus) bestArmorBonus = bonus;
            }

            float estimatedRate = Mathf.Clamp01(statRatio * (1f + bestArmorBonus));

            AbstractLevel trueLevel = estimatedRate switch
            {
                >= 0.8f => AbstractLevel.대길조,
                >= 0.6f => AbstractLevel.길조,
                >= 0.4f => AbstractLevel.보통,
                >= 0.2f => AbstractLevel.험난,
                _       => AbstractLevel.암담,
            };

            // 60% 적중 / 40% 오차(-1~+1)
            bool isAccurate = UnityEngine.Random.value < 0.6f;
            if (isAccurate)
                return trueLevel;

            int offset = UnityEngine.Random.Range(-1, 2); // -1, 0, +1
            int adjusted = Mathf.Clamp((int)trueLevel + offset, 0, (int)AbstractLevel.대길조);
            return (AbstractLevel)adjusted;
        }

        /// <summary>
        /// 모험가 LUK 스탯 기반 가중치 랜덤으로 LuckLevel 판정.
        /// </summary>
        private LuckLevel GenerateLuckLevel(AdventurerInstance adventurer)
        {
            int luk = adventurer.LUK;
            var cfg = ConfigManager.Instance.Seer;

            float wBad, wMinor, wGood, wGreat;
            if (luk <= 25)
            {
                wBad = cfg.luk0BadWeight; wMinor = cfg.luk0MinorWeight;
                wGood = cfg.luk0GoodWeight; wGreat = cfg.luk0GreatWeight;
            }
            else if (luk <= 50)
            {
                wBad = cfg.luk1BadWeight; wMinor = cfg.luk1MinorWeight;
                wGood = cfg.luk1GoodWeight; wGreat = cfg.luk1GreatWeight;
            }
            else if (luk <= 75)
            {
                wBad = cfg.luk2BadWeight; wMinor = cfg.luk2MinorWeight;
                wGood = cfg.luk2GoodWeight; wGreat = cfg.luk2GreatWeight;
            }
            else
            {
                wBad = cfg.luk3BadWeight; wMinor = cfg.luk3MinorWeight;
                wGood = cfg.luk3GoodWeight; wGreat = cfg.luk3GreatWeight;
            }

            return RollLuckLevel(wBad, wMinor, wGood, wGreat);
        }

        /// <summary>
        /// 이스터에그 전용 LuckLevel 판정. LUK 무시, SeerConfig.egg 가중치 사용.
        /// </summary>
        private LuckLevel GenerateEasterEggLuckLevel()
        {
            var cfg = ConfigManager.Instance.Seer;
            return RollLuckLevel(cfg.eggBadWeight, cfg.eggMinorWeight, cfg.eggGoodWeight, cfg.eggGreatWeight);
        }

        private static LuckLevel RollLuckLevel(float wBad, float wMinor, float wGood, float wGreat)
        {
            float total = wBad + wMinor + wGood + wGreat;
            float roll  = UnityEngine.Random.value * total;

            if (roll < wBad)                    return LuckLevel.흉;
            if (roll < wBad + wMinor)           return LuckLevel.소길;
            if (roll < wBad + wMinor + wGood)   return LuckLevel.중길;
            return LuckLevel.대길;
        }

        /// <summary>
        /// 수정구 서사 한 줄을 뽑는다. 결과가 세이브에 구워지므로 **번역하지 않은 원문과 키**를
        /// 함께 돌려주고, 푸는 것은 <see cref="SeerResult.DisplayDescription"/>에 맡긴다.
        /// </summary>
        private string BuildDescription(AbstractLevel abs, LuckLevel luck, out string key)
        {
            var pool = ConfigManager.Instance.Seer.GetDescriptionPool(abs, luck, out string path);
            return SeerConfig.PickRandomKeyed(pool, DefaultDescription(abs, luck), path, out key);
        }

        private static string DefaultDescription(AbstractLevel abs, LuckLevel luck)
        {
            return L((abs, luck) switch
            {
                (AbstractLevel.대길조, LuckLevel.대길) => "Seer_Fortune_Best_Great",
                (AbstractLevel.대길조, LuckLevel.중길) => "Seer_Fortune_Best_Good",
                (AbstractLevel.대길조, LuckLevel.소길) => "Seer_Fortune_Best_Minor",
                (AbstractLevel.대길조, LuckLevel.흉)   => "Seer_Fortune_Best_Bad",

                (AbstractLevel.길조, LuckLevel.대길)   => "Seer_Fortune_Good_Great",
                (AbstractLevel.길조, LuckLevel.중길)   => "Seer_Fortune_Good_Good",
                (AbstractLevel.길조, LuckLevel.소길)   => "Seer_Fortune_Good_Minor",
                (AbstractLevel.길조, LuckLevel.흉)     => "Seer_Fortune_Good_Bad",

                (AbstractLevel.보통, LuckLevel.대길)   => "Seer_Fortune_Normal_Great",
                (AbstractLevel.보통, LuckLevel.중길)   => "Seer_Fortune_Normal_Good",
                (AbstractLevel.보통, LuckLevel.소길)   => "Seer_Fortune_Normal_Minor",
                (AbstractLevel.보통, LuckLevel.흉)     => "Seer_Fortune_Normal_Bad",

                (AbstractLevel.험난, LuckLevel.대길)   => "Seer_Fortune_Rough_Great",
                (AbstractLevel.험난, LuckLevel.중길)   => "Seer_Fortune_Rough_Good",
                (AbstractLevel.험난, LuckLevel.소길)   => "Seer_Fortune_Rough_Minor",
                (AbstractLevel.험난, LuckLevel.흉)     => "Seer_Fortune_Rough_Bad",

                (AbstractLevel.암담, LuckLevel.대길)   => "Seer_Fortune_Dire_Great",
                (AbstractLevel.암담, LuckLevel.중길)   => "Seer_Fortune_Dire_Good",
                (AbstractLevel.암담, LuckLevel.소길)   => "Seer_Fortune_Dire_Minor",
                (AbstractLevel.암담, LuckLevel.흉)     => "Seer_Fortune_Dire_Bad",

                _ => "Seer_Fortune_Unknown"
            });
        }

        #endregion
    }
}
