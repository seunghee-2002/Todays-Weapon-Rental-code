#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 페르소나 봇 — 의사결정 정책. 정책 정의: Documents/balance/reference/시뮬레이터_설계.md §5
    /// 판정(성공률·보상)은 전부 SimWorld가 실제 수식으로 수행하고, 페르소나는 선택만 한다.
    /// </summary>
    public abstract class SimPersona
    {
        public abstract string Name { get; }

        /// <summary>true = 낮 즉시 결과 확인(무기 회전 일 2회), false = 전령(18시)에서만 확인</summary>
        public virtual bool ImmediateConfirm => true;

        public abstract void OnBlacksmith(SimWorld w);
        public abstract void OnShop(SimWorld w, List<ShopItem> stock);
        public abstract List<BoardEntry> SelectBoard(SimWorld w, List<BoardEntry> pool);
        public abstract void OnScouts(SimWorld w);
        public abstract void OnVisitor(SimWorld w, SimVisitor v, ref float cursor);

        /// <summary>아침 이벤트 NPC 응대 — 참여 여부·대상만 고르고, 실행·판정은 SimWorld의 Morning* 미러가 한다</summary>
        public abstract void OnMorningEvent(SimWorld w, MorningEventType type);

        /// <summary>
        /// 회차 사이 유산 업그레이드 구매 (단계 2). 폐업 -> 유산 획득 -> 여기서 구매 -> 다음 회차.
        /// 주의: 이 정책이 회차 반복 결과를 지배할 수 있다 — 결과가 이상하면 게임이 아니라 이 정책부터 의심할 것
        /// (밸런스_미적용_영역과_시뮬_로드맵.md 단계 2 함정).
        /// </summary>
        public abstract void OnLegacyShop(SimBundle b, SimOptions opt, SimCareer career, System.Random rng);

        /// <summary>
        /// 출발 전 점술 상담 여부 (단계 5-1). 봇은 **LUK을 모른 채(LUK-blind)** 던전 등급만 보고 결정한다 —
        /// 상담 대상 선택이 LUK과 무관해야 리포트의 LUK 구간별 결과가 곧 운세 가중치의 실효값이 된다
        /// (단계 4의 trait-blind와 같은 이유). 기본은 이용 안 함.
        /// </summary>
        public virtual bool UseSeer(SimWorld w, BoardEntry entry) => false;

        /// <summary>
        /// 네임드 우대 대상인지 (단계 5-2). `preferNamed`가 꺼져 있으면 항상 false(named-blind)라
        /// "네임드 반영 자체의 기여"와 "우대 정책의 기여"를 분리 측정할 수 있다.
        /// 우대하는 이유: 네임드는 호감도가 누적돼 성공률이 오르고 재방문하므로, 좋은 무기·큰 던전을
        /// 몰아주는 편이 이론상 이득이다. 그 이론이 실제로 맞는지가 이번 측정의 질문이다.
        /// </summary>
        protected static bool PreferNamed(SimWorld w, SimVisitor v) => w.opt.preferNamed && v.named != null;

        /// <summary>네임드 우대 시의 점술 문턱 — 희귀+로 낮춘다 (일반은 페르소나별 기본 문턱)</summary>
        protected static bool UseSeerForNamed(SimWorld w, BoardEntry entry)
        {
            if (entry == null || (int)entry.dungeon.grade < (int)Grade.Rare) return false;
            int cost = w.SeerCost();
            return w.gold >= cost * 3 && w.gold - cost >= (w.CurrentQuest?.weeklyFine ?? 0);
        }

        /// <summary>
        /// 이 페르소나가 제작을 노리는 액티브 아이템 레시피 (단계 3).
        /// 모험 결과의 제작 재료 구매 필요량 계산과 아침 제작 정책이 공유한다. 기본은 제작 안 함.
        /// </summary>
        public virtual IEnumerable<ActiveItemRecipeData> CraftTargets(SimWorld w)
        {
            yield break;
        }

        /// <summary>
        /// 이 페르소나가 노리는 무기 제작 레시피 (단계 5-3).
        /// 아이템 레시피와 같은 재료 풀을 쓰므로 결과 화면 재료 구매 목표도 이걸 함께 본다 —
        /// 목표에 없으면 재료를 안 사서 제작이 영영 불가능해진다(3부 원칙 2: 지표 0이면 봇부터 의심).
        /// 기본은 제작 안 함.
        /// </summary>
        public virtual IEnumerable<WeaponRecipeData> WeaponCraftTargets(SimWorld w)
        {
            yield break;
        }

        #region 공용 헬퍼

        /// <summary>스탯(STR/DEX/INT/LUK) -> 그 스탯을 주력으로 쓰는 무기 타입</summary>
        protected static readonly WeaponType[][] StatToTypes =
        {
            new[] { WeaponType.Sword, WeaponType.Axe },        // STR
            new[] { WeaponType.Bow, WeaponType.Crossbow },     // DEX
            new[] { WeaponType.Staff, WeaponType.Tome },       // INT
            new[] { WeaponType.Dagger, WeaponType.Shuriken },  // LUK
        };

        /// <summary>공개된 최고 스탯 계열에서 보유 중인 최선의 무기</summary>
        protected static SimWeapon BestOfStatFamily(SimWorld w, int statIndex) =>
            StatToTypes[statIndex]
                .Select(t => BestFreeOfType(w, t))
                .Where(x => x != null)
                .OrderByDescending(x => x.grade).ThenByDescending(x => x.enforce)
                .FirstOrDefault();

        protected static SimWeapon BestFreeOfType(SimWorld w, WeaponType t) =>
            w.weapons.Where(x => !x.busy && x.Type == t)
                     .OrderByDescending(x => x.grade).ThenByDescending(x => x.enforce)
                     .FirstOrDefault();

        protected static List<SimWeapon> FreeWeapons(SimWorld w) =>
            w.weapons.Where(x => !x.busy).ToList();

        protected static BoardEntry SafestEntry(SimWorld w) =>
            w.board.OrderBy(e => e.dungeon.baseStatThreshold).FirstOrDefault();

        /// <summary>
        /// 던전 기대 보상 근사 — 완주 확률(회당 성공률^이벤트 수) x 총 보상.
        /// "가장 쉬운 던전" 고정으로 두면 던전 밸런스를 바꿔도 봇 선택이 안 바뀌어 효과를 측정할 수 없다.
        /// 실제 점수는 방문자마다 다르므로 평균 모험가 기준으로 근사한다(순위 비교용이라 절대값은 무의미).
        /// </summary>
        protected static float ExpectedValue(SimWorld w, DungeonData d)
        {
            const float AVG_SCORE = 130f;
            int events = w.b.adventure.maxEventCountByGrade[(int)d.grade];
            float rate = UnityEngine.Mathf.Clamp01(AVG_SCORE / d.baseStatThreshold);
            float reward = (d.baseRewardMin + d.baseRewardMax) * 0.5f * events;
            return UnityEngine.Mathf.Pow(rate, events) * reward;
        }

        /// <summary>
        /// 시뮬에 효과가 반영되는 업그레이드만 구매 대상으로 삼는다.
        /// 네임드(NamedSpawnWeight/RegularAdventurer) 계열은 시뮬이 해당 시스템을 모델링하지 않아
        /// 무효이므로 제외 — 봇이 유산을 낭비하면 정책 결함을 측정하게 된다.
        /// </summary>
        private static readonly UpgradeKey[] BaseEffectiveUpgrades =
        {
            UpgradeKey.StartingGold, UpgradeKey.WeaponRare, UpgradeKey.WeaponEpic,
            UpgradeKey.StartingInsight, UpgradeKey.InventorySlots,
            UpgradeKey.EnforceRate, UpgradeKey.EvolveRate, UpgradeKey.EnforceCost, UpgradeKey.EvolveCost,
            UpgradeKey.MaterialReduction, UpgradeKey.DisassembleBonus,
            UpgradeKey.CommissionRate, UpgradeKey.TipRate, UpgradeKey.GreatSuccessRate, UpgradeKey.AdventureSpeed,
            UpgradeKey.MorningEventGuarantee, UpgradeKey.ShopRefresh,
        };

        /// <summary>재부여 2종은 단계 5-3(useCraft)이 켜졌을 때만 효과가 있으므로 그때만 구매 대상이다.</summary>
        private static readonly UpgradeKey[] CraftEffectiveUpgrades =
            BaseEffectiveUpgrades.Concat(new[] { UpgradeKey.RerollCount, UpgradeKey.RerollCost }).ToArray();

        protected static UpgradeKey[] EffectiveUpgrades(SimOptions opt) =>
            opt.useCraft ? CraftEffectiveUpgrades : BaseEffectiveUpgrades;

        /// <summary>
        /// 타입당 보유할 무기 자루 수. 대여는 BestFreeOfType으로 "그 타입의 노는 무기"를 찾으므로,
        /// 타입당 1자루면 같은 타입 손님이 겹칠 때 전부 기본 무기로 나간다(실측 기본무기 비율 54%).
        /// Erlang B 손실 모델(타입당 3.3명/일, 모험 50분, 낮 540분 -> a=0.307)로 계산한 손실률은
        /// 1자루 23.5% / 2자루 3.5% / 3자루 0.4%다.
        /// </summary>
        public virtual int WeaponTypeCap(SimWorld w) => 1;

        /// <summary>
        /// 보유량이 인벤토리 상한의 threshold 비율을 넘으면 여분 무기를 해체해 골드·진화 재료를 회수한다.
        /// 타입별 최고 WeaponTypeCap자루는 남긴다(대여 커버리지 유지).
        /// </summary>
        protected static void DisassembleExcess(SimWorld w, float threshold)
        {
            int cap = w.b.inventory.inventorySlots;
            if (w.weapons.Count < cap * threshold) return;

            int target = UnityEngine.Mathf.RoundToInt(cap * threshold * 0.6f);
            int keepPerType = w.persona.WeaponTypeCap(w);
            var keep = new HashSet<SimWeapon>(
                w.weapons.GroupBy(x => x.Type)
                         .SelectMany(g => g.OrderByDescending(x => x.grade).ThenByDescending(x => x.enforce)
                                           .Take(keepPerType)));

            var junk = w.weapons.Where(x => !keep.Contains(x) && !x.busy)
                                .OrderBy(x => x.grade).ThenBy(x => x.enforce).ToList();

            foreach (var x in junk)
            {
                if (w.weapons.Count <= target) break;
                w.Disassemble(x);
            }
        }

        #endregion
    }

    /// <summary>상급자 "민맥스" — 기본 무기 타입 관찰 + 타입 매칭 + 수색 상시 + 즉시 확인</summary>
    public class ElitePersona : SimPersona
    {
        public override string Name => "상급자";

        public override void OnBlacksmith(SimWorld w)
        {
            DisassembleExcess(w, 0.8f);

            // 아이템 제작 (단계 3): 목표 레시피 재고를 채워둔다 (골드 3배 여유 가드)
            foreach (var recipe in CraftTargets(w))
            {
                if (recipe == null) continue;
                int stock = recipe.resultItem.itemType == ActiveItemType.ForgeStone ? 1 : 2;
                if (w.ItemCount(recipe.resultItem.StaticID) < stock && w.gold >= w.ItemCraftGold(recipe) * 3)
                    w.TryCraftItem(recipe);
            }

            // 무기 제작 (단계 5-3): 해금된 최고 등급 레시피부터. 재료가 모자라면 자동으로 실패하고 넘어간다
            foreach (var recipe in WeaponCraftTargets(w))
                if (w.gold >= w.WeaponCraftGold(recipe) * 3) w.TryCraftWeapon(recipe);

            // 재부여 (단계 5-3): 효과 값이 낮은 고등급 무기를 잠금 1개로 굴린다.
            // 잠금 0개는 좋은 효과까지 날리고, 3개 이상은 배율이 3배라 골드가 감당이 안 된다.
            var rerollTarget = w.weapons
                .Where(x => w.CanReroll(x) && x.grade >= Grade.Epic)
                .OrderBy(x => x.effects.Sum(e => e.value))
                .FirstOrDefault();
            if (rerollTarget != null && w.gold >= w.RerollGoldCost(1) * 3)
                w.TryReroll(rerollTarget, 1);

            // 강화 대상 — 개편으로 재료 조건이 사라져 "미완성 무기 중 최고 등급"이 그대로 대상이 된다.
            var enforceTarget = w.weapons
                .Where(x => x.enforce < x.MaxEnforce)
                .OrderByDescending(x => x.grade).ThenByDescending(x => x.enforce)
                .FirstOrDefault();

            // 강화: 기대비용 2배 여유가 있을 때, 하루 최대 2회 시도.
            // 비용 배수 가드(gold < cost x N)는 성공률이 바뀌면 강도가 같이 바뀌므로 기대비용을 쓴다.
            // 강화석은 4-6 실측(에픽 이상에서 명확히 이득)대로 에픽+ 대상에만 쓴다
            if (enforceTarget != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (enforceTarget.enforce >= enforceTarget.MaxEnforce) break;
                    var stone = enforceTarget.grade >= Grade.Epic ? w.BestItemInStock(ActiveItemType.ForgeStone) : null;
                    if (w.gold < w.EnforceExpectedGold(enforceTarget, stone) * 2) break;
                    w.TryEnforce(enforceTarget, stone);
                }
            }

            // 진화 대상 — 강화와 독립 쿼리. 강화 대상에 종속시키면 전설 무기를 얻는 순간
            // 대상이 전설에 고정돼(grade < Legendary 실패) 준비된 영웅 무기를 영영 못 본다.
            // 성공률 문턱은 두지 않는다 — 개편 후 최대치가 60%라 문턱을 두면 일반 등급 외에는 영원히 진화하지 않는다.
            var evolveTarget = w.weapons
                .Where(x => x.enforce >= x.MaxEnforce && x.grade < Grade.Legendary && w.HasEvolveMats(x))
                .OrderByDescending(x => x.grade)
                .FirstOrDefault();
            if (evolveTarget != null && w.gold >= w.EvolveGoldCost(evolveTarget) * 3)
            {
                var stone = evolveTarget.grade >= Grade.Epic ? w.BestItemInStock(ActiveItemType.ForgeStone) : null;
                w.TryEvolve(evolveTarget, stone);
            }
        }

        /// <summary>
        /// 제작 목표 — 최고 해금 부적 + (에픽+ 강화 대상이 있을 때) 최고 해금 강화석.
        /// 황금 아뮬렛은 제외한다: 아이템은 모험당 1개만 배정되는데, 부적의 성공률 가산이 매 판정에
        /// 복리로 걸려 성공률 90% 미만 구간에서는 항상 골드 배율을 이긴다(실측: 아뮬렛 사용 시 순수입 -3.4%).
        /// effectValue를 2배로 올려도 이 우열은 바뀌지 않는다 — 수치가 아니라 슬롯 경쟁 구조의 문제다.
        /// </summary>
        public override IEnumerable<ActiveItemRecipeData> CraftTargets(SimWorld w)
        {
            var charm = w.BestUnlockedRecipe(ActiveItemType.Charm);
            if (charm != null) yield return charm;
            if (w.weapons.Any(x => x.grade >= Grade.Epic && x.enforce < x.MaxEnforce))
            {
                var forge = w.BestUnlockedRecipe(ActiveItemType.ForgeStone);
                if (forge != null) yield return forge;
            }
        }

        /// <summary>
        /// 무기 제작 목표 — 해금된 것 중 **특수 재료 관문이 가장 적은** 레시피 1종.
        /// 처음엔 최고가(= 전설)를 노렸으나, 전설은 서로 다른 던전 3곳의 특수 재료를 동시에 요구해
        /// 사실상 도달 불가였고 그것만 파느라 에픽도 못 만들었다(진단 런에서 특수 재료 부족 100%).
        /// 민맥스라면 "가장 비싼 것"이 아니라 "실제로 완성 가능한 것 중 가장 좋은 것"을 노린다.
        ///
        /// 1종만 노리는 이유는 재료 구매 상한과 직결되기 때문 — 여러 종을 목표로 두면
        /// 안 쓸 재료까지 사들여 봇이 자멸한다(3부 원칙 1).
        /// </summary>
        public override IEnumerable<WeaponRecipeData> WeaponCraftTargets(SimWorld w)
        {
            var r = w.b.weaponRecipes.Where(x => w.day >= x.unlockedDay)
                                     .OrderBy(x => x.requiredMaterials
                                         .Count(m => m.material != null && m.material.StaticID.StartsWith("MAT_SPC")))
                                     .ThenByDescending(x => x.requiredGold)
                                     .FirstOrDefault();
            if (r != null) yield return r;
        }

        /// <summary>
        /// 초반은 2자루 - 1~10주는 방문자가 12.8명/일이라 타입당 1.6명뿐이라 3자루가 필요 없고,
        /// 과잉 구매로 초반 골드가 마르면 그대로 폐업한다(중급자 실측에서 확인된 패턴).
        /// </summary>
        public override int WeaponTypeCap(SimWorld w) => w.week <= 10 ? 2 : 3;

        public override void OnShop(SimWorld w, List<ShopItem> stock)
        {
            for (int pass = 0; ; pass++)
            {
                int typeCap = WeaponTypeCap(w);
                var owned = w.weapons.GroupBy(x => x.Type).ToDictionary(g => g.Key, g => g.Count());
                foreach (var item in stock.OrderBy(i => i.price))
                {
                    if (!w.CanAddWeapon()) break;
                    var type = item.data.weaponType;
                    int have = owned.TryGetValue(type, out int c) ? c : 0;
                    if (have >= typeCap) continue;
                    // 첫 자루는 커버리지라 골드 2배, 같은 타입 추가분은 후순위이므로 4배 여유를 요구한다
                    if (w.gold < item.price * (have == 0 ? 2 : 4)) continue;
                    if (w.BuyWeapon(item)) owned[type] = have + 1;
                }

                // 커버리지가 완성되면 새로고침하지 않는다 - 중복 구매까지 새로고침을 돌리면 그 비용이 폭증한다.
                // 인벤이 찼을 때도 마찬가지다 - 살 수 없는데 새로고침만 하면 골드를 버린다
                if (owned.Count >= 8 || !w.CanAddWeapon()) break;
                var refreshed = w.TryShopRefresh();
                if (refreshed == null) break;
                stock = refreshed;
            }
        }

        public override List<BoardEntry> SelectBoard(SimWorld w, List<BoardEntry> pool)
        {
            var targets = w.UnmetQuestDungeonIDs();

            // 목표 던전이 안 떴으면 새로고침 최대 2회.
            // 기대가치 가드: 새로고침 비용이 벌금 이상이면 그냥 벌금을 내는 게 싸다 — 도박하지 않는다.
            int fine = w.CurrentQuest?.weeklyFine ?? 0;
            for (int i = 0; i < 2; i++)
            {
                if (targets.Count == 0 || pool.Any(e => targets.Contains(e.dungeon.StaticID))) break;
                if (w.RefreshCost() >= fine) break;
                var np = w.TryRerollBoard();
                if (np == null) break;
                pool = np;
            }

            // 퀘스트 목표 최우선, 나머지는 기대 보상 순 (민맥스는 쉬운 던전이 아니라 이득이 큰 던전을 고른다)
            return pool.OrderByDescending(e => targets.Contains(e.dungeon.StaticID))
                       .ThenByDescending(e => ExpectedValue(w, e.dungeon))
                       .Take(w.SelectCount()).ToList();
        }

        public override void OnScouts(SimWorld w)
        {
            // 실비 지출: 선택한 던전만, 고등급(보상 큰 쪽) 우선. 골드 3배 여유 가드(강화/구매와 동일 기준).
            foreach (var e in w.board.OrderByDescending(x => (int)x.dungeon.grade))
            {
                int cost = w.ScoutCost(e.dungeon);
                if (w.gold < cost * 3) continue;
                w.DispatchScout(e);
            }
        }

        /// <summary>
        /// 점술은 판돈이 큰 던전(에픽+)에만. 운세 보정은 던전 난이도와 무관한 절대 가산이라
        /// 보상이 큰 판일수록 같은 300G의 값어치가 크다. 수색과 동일한 3배 여유 + 벌금 유보 가드.
        /// </summary>
        public override bool UseSeer(SimWorld w, BoardEntry entry)
        {
            if (entry == null || (int)entry.dungeon.grade < (int)Grade.Epic) return false;
            int cost = w.SeerCost();
            return w.gold >= cost * 3 && w.gold - cost >= (w.CurrentQuest?.weeklyFine ?? 0);
        }

        public override void OnVisitor(SimWorld w, SimVisitor v, ref float cursor)
        {
            // 0) 네임드 우대 — 재방문하는 고정 개체라 스탯/특성을 이미 파악했다고 보고 최적 타입을 정확히 맞춘다
            //    (일반 모험가는 그날 한정이라 관찰/대화로만 추정해야 한다)
            var type = v.observedDefaultType;
            SimWeapon weapon = null;
            if (PreferNamed(w, v))
            {
                type = v.trueBest;
                weapon = BestFreeOfType(w, type) ?? BestOfStatFamily(w, v.bestStatIndex);
            }

            // 1) 기본 무기 타입 관찰 (무료, 70% 정확)
            if (weapon == null)
            {
                type = v.observedDefaultType;
                weapon = BestFreeOfType(w, type);
            }

            // 2) 통찰 70+ : 최고 스탯이 자동 공개된다 — 대화비(시간) 없이 계열 무기를 고른다
            if (weapon == null && w.KnowsHighestStat)
                weapon = BestOfStatFamily(w, v.bestStatIndex);

            // 3) 그래도 없으면 무기 힌트 대화 (통찰 게이트 이상 + 옵션) — 정확한 최적 타입 획득
            if (weapon == null && w.opt.eliteUsesTalkHint && w.insightScore >= w.opt.eliteTalkInsightGate)
            {
                if (w.TrySpendTalk(ref cursor))
                {
                    type = v.trueBest;
                    weapon = BestFreeOfType(w, type);
                }
            }

            if (weapon != null)
            {
                // 퀘스트 목표 던전 최우선(확정만 하고 안 가면 목표를 못 채운다),
                // 그 다음 공개된 방어 타입과 상성, 동률이면 기대 보상 순
                var questTargets = w.UnmetQuestDungeonIDs();
                bool prefer = PreferNamed(w, v);
                var entry = w.board
                    .OrderByDescending(e => questTargets.Contains(e.dungeon.StaticID))
                    // 네임드는 판돈이 큰 던전을 우선 — 상성보다 기대 보상을 앞에 둔다
                    .ThenByDescending(e => prefer ? ExpectedValue(w, e.dungeon) : 0f)
                    .ThenByDescending(e => e.armorKnown
                        ? TypeAdvantage.weaponArmorBonus[(int)weapon.Type, (int)e.armor] : 0f)
                    .ThenByDescending(e => ExpectedValue(w, e.dungeon))
                    .FirstOrDefault();
                if (entry != null)
                {
                    // 부적은 큰 판(에픽+)에만 — 4-6 "상위 던전에 쓰면 이득" 정책 (로드맵 단계 3).
                    // 네임드 우대 시엔 희귀+로 문턱을 낮춘다(호감도가 쌓일 개체라 투자 가치가 크다)
                    int charmGate = prefer ? (int)Grade.Rare : (int)Grade.Epic;
                    var item = (int)entry.dungeon.grade >= charmGate
                        ? w.BestItemInStock(ActiveItemType.Charm) : null;
                    w.LaunchAdventure(v, weapon, entry, cursor, item, UseSeer(w, entry) || (prefer && UseSeerForNamed(w, entry)));
                }
            }
            else
            {
                // 대여 불가 -> 기본 무기로 최저 요구치 던전 (탐험도·평판 적립)
                var entry = SafestEntry(w);
                if (entry != null) w.LaunchAdventure(v, null, entry, cursor);
            }
        }

        /// <summary>
        /// 아침 이벤트 — "기대값 계산 후 이득일 때만" (로드맵 단계 1 정책).
        /// 상자/투자 EV는 Config에서 계산하므로 수치를 조정하면 봇 행동이 자동으로 따라온다.
        /// 현재 값이면 상자 EV 8~38%, 투자 EV 98%라 지출 0이 나오는데, 이는 봇 결함이 아니라
        /// "콘텐츠가 죽었다"는 측정 결과다 — 강제 참여 런과 비교해 판정한다 (로드맵 3부 원칙 5).
        /// </summary>
        public override void OnMorningEvent(SimWorld w, MorningEventType type)
        {
            switch (type)
            {
                case MorningEventType.WeaponEnhance:
                {
                    // 무료 + E[등급 변화] 양수 — 최고 등급 유휴 무기에 사용
                    var t = FreeWeapons(w).OrderByDescending(x => x.grade)
                                          .ThenByDescending(x => x.enforce).FirstOrDefault();
                    if (t != null) w.MorningWeaponEnhance(t);
                    break;
                }
                case MorningEventType.WanderingBlacksmith:
                {
                    // 무료 강화/리롤 — 미만강 우선, 최고 등급
                    var t = FreeWeapons(w).OrderByDescending(x => x.enforce < x.MaxEnforce)
                                          .ThenByDescending(x => x.grade).FirstOrDefault();
                    if (t != null) w.MorningWanderingBlacksmith(t);
                    break;
                }
                case MorningEventType.GuildEnvoy:
                    w.MorningGuildEnvoy();   // 전 평판 구간에서 선물 확률 60%+ = EV 양수
                    break;
                case MorningEventType.Collector:
                {
                    // 3~5배 판매 — 분해보다 압도적 이득. 타입별 최고 WeaponTypeCap자루는 남긴다 (커버리지 유지)
                    var sell = w.SpareWeapons()
                        .Where(x => (int)x.grade >= (int)w.b.morningEvent.collectorMinGrade)
                        .OrderByDescending(x => x.data.basePrice).FirstOrDefault();
                    if (sell != null) w.MorningCollectorSell(sell);
                    break;
                }
                case MorningEventType.MysteryBox:
                {
                    int tier = w.RollBoxTier();
                    int cost = w.BoxCost(tier);
                    if (w.BoxEV(tier) >= cost && w.gold >= cost * 3) w.BuyMysteryBox(tier);
                    break;
                }
                case MorningEventType.SuspiciousInvestor:
                {
                    int amount = w.b.morningEvent.investMinGold;
                    if (w.InvestEV() > 1f && w.gold >= amount * 3) w.MorningInvest(amount);
                    break;
                }
                case MorningEventType.RefugeeHelp:
                {
                    // 평판 구매 (E[+6.5]) — 골드 3배 여유 가드 (강화/구매와 동일 기준)
                    int cost = w.RefugeeCost();
                    if (w.gold >= cost * 3) w.MorningRefugee(true);
                    break;
                }
                case MorningEventType.BlackMarket:
                {
                    // 반값 구매 — 미보유 타입 커버리지 확대일 때만 (평판 -10 감수)
                    var (data, price) = w.RollBlackMarketOffer();
                    if (data != null && w.CanAddWeapon() && w.gold >= price * 2
                        && !w.weapons.Any(x => x.Type == data.weaponType))
                        w.BuyBlackMarket(data, price);
                    break;
                }
                case MorningEventType.WeaponExchange:
                {
                    // 여분 최저 등급 2자루 -> 상위 등급 도박 (여분은 어차피 분해 대상)
                    var spares = w.SpareWeapons().OrderBy(x => x.grade).ThenBy(x => x.enforce).ToList();
                    if (spares.Count >= 2) w.MorningWeaponExchange(spares[0], spares[1]);
                    break;
                }
            }
        }

        /// <summary>민맥스 우선순위 — 수입/처리량 직결 순. 유보 0% (전부 투자)</summary>
        private static readonly UpgradeKey[] LegacyPriority =
        {
            UpgradeKey.CommissionRate, UpgradeKey.StartingInsight, UpgradeKey.AdventureSpeed,
            UpgradeKey.GreatSuccessRate, UpgradeKey.TipRate, UpgradeKey.EnforceRate, UpgradeKey.EvolveRate,
            UpgradeKey.StartingGold, UpgradeKey.WeaponRare, UpgradeKey.WeaponEpic, UpgradeKey.ShopRefresh,
            UpgradeKey.DisassembleBonus, UpgradeKey.EnforceCost, UpgradeKey.EvolveCost,
            UpgradeKey.InventorySlots, UpgradeKey.MaterialReduction, UpgradeKey.MorningEventGuarantee,
        };

        /// <summary>재부여 2종은 단계 5-3(useCraft)이 켜졌을 때만 효과가 있다 — 지출 절감 계열이라 뒤에 붙인다</summary>
        private static readonly UpgradeKey[] LegacyPriorityCraft =
            LegacyPriority.Concat(new[] { UpgradeKey.RerollCost, UpgradeKey.RerollCount }).ToArray();

        public override void OnLegacyShop(SimBundle b, SimOptions opt, SimCareer career, System.Random rng)
        {
            var priority = opt.useCraft ? LegacyPriorityCraft : LegacyPriority;
            bool bought = true;
            while (bought)
            {
                bought = false;
                foreach (var key in priority)
                    if (career.CanPurchase(b, key)) { career.Purchase(b, key); bought = true; break; }
            }
        }
    }

    /// <summary>중급자 "감각파" — 스탯 테스트 1회 후 감으로 매칭</summary>
    public class MidPersona : SimPersona
    {
        public override string Name => "중급자";

        public override void OnBlacksmith(SimWorld w)
        {
            DisassembleExcess(w, 1.0f);

            // 아이템 제작 (단계 3): 기본 부적만, 골드 5배 여유일 때
            var charmRecipe = w.FirstUnlockedRecipe(ActiveItemType.Charm);
            if (charmRecipe != null && w.ItemCount(charmRecipe.resultItem.StaticID) < 2
                && w.gold >= w.ItemCraftGold(charmRecipe) * 5)
                w.TryCraftItem(charmRecipe);

            // 무기 제작 (단계 5-3): 감각파는 싼 에픽 레시피만, 골드 5배 여유일 때
            foreach (var recipe in WeaponCraftTargets(w))
                if (w.gold >= w.WeaponCraftGold(recipe) * 5) w.TryCraftWeapon(recipe);

            // 재부여 (단계 5-3): 잠금 없이(x1 = 최저가) 가끔만. 감각파는 잠금 개념을 안 쓴다.
            // useCraft를 먼저 확인해야 OFF 런이 메인 rng를 건드리지 않아 반영 이전과 동일해진다
            if (w.opt.useCraft && w.rng.NextDouble() < 0.15)
            {
                var rr = w.weapons.Where(x => w.CanReroll(x) && x.grade >= Grade.Rare)
                                  .OrderBy(x => x.effects.Sum(e => e.value)).FirstOrDefault();
                if (rr != null && w.gold >= w.RerollGoldCost(0) * 5) w.TryReroll(rr, 0);
            }

            // 진화는 재료 게이트가 있으므로 강화의 주 1회 쿨다운과 별개로, 재료가 모였을 때만 시도한다.
            // 싼 무기만 사는 중급자에게는 이것이 유일한 고등급 확보 경로다(등급 요구 퀘스트가 여기서 갈린다).
            var evolvable = w.weapons
                .Where(x => x.enforce >= x.MaxEnforce && x.grade < Grade.Legendary && w.HasEvolveMats(x))
                .OrderByDescending(x => x.grade).FirstOrDefault();
            if (evolvable != null && w.gold >= w.EvolveGoldCost(evolvable) * 5)
                w.TryEvolve(evolvable);

            if (w.day - w.LastEnhanceDay < 7) return;

            // 강화 대상 — 개편으로 재료 조건이 사라져 "미완성 무기 중 최고 등급"이 그대로 대상이 된다.
            var target = w.weapons
                .Where(x => x.enforce < x.MaxEnforce)
                .OrderByDescending(x => x.grade).FirstOrDefault();
            if (target == null) return;

            // 기대비용 3배 여유 — 비용 배수 가드는 성공률이 바뀌면 강도가 같이 바뀐다(상급자와 동일 이유)
            if (w.gold < w.EnforceExpectedGold(target) * 3) return;
            w.LastEnhanceDay = w.day;
            w.TryEnforce(target);
        }

        public override void OnShop(SimWorld w, List<ShopItem> stock)
        {
            // 싼 것부터 산다. 등급 우선으로 바꿔봤더니 초반 골드가 말라 5주차에 폐업했다(실측).
            // 고등급 무기 확보는 상급자의 영역으로 남긴다.
            //
            // 타입을 보지 않는 것은 결함이 아니다 - 방문마다 2자루씩 쌓아 40자루에 도달하면
            // 기본무기 비율이 0%가 된다(실측). 타입당 상한을 걸었더니 16자루로 줄고 5%로 악화됐다.
            int bought = 0;
            foreach (var item in stock.OrderBy(i => i.price))
            {
                if (bought >= 2) break;
                if (w.gold < item.price * 3) continue;
                if (w.BuyWeapon(item)) bought++;
            }
        }

        public override List<BoardEntry> SelectBoard(SimWorld w, List<BoardEntry> pool)
        {
            // 퀘스트 목표 던전이 떠 있으면 고른다 — 감각파라도 퀘스트 목록은 본다.
            // 새로고침까지 하지 않는 것이 상급자와의 차이다.
            var targets = w.UnmetQuestDungeonIDs();
            return pool.OrderByDescending(e => targets.Contains(e.dungeon.StaticID))
                       .ThenBy(e => e.dungeon.baseStatThreshold)
                       .Take(w.SelectCount()).ToList();
        }

        public override void OnScouts(SimWorld w) { }

        /// <summary>
        /// 감각파는 수색을 아예 안 사는 대신, 점술은 전설 던전 한정으로만 산다.
        /// 골드 10배 여유를 요구해 "정보에 돈을 거의 안 쓴다"는 중급자 성격을 유지한다.
        /// </summary>
        public override bool UseSeer(SimWorld w, BoardEntry entry)
        {
            if (entry == null || entry.dungeon.grade != Grade.Legendary) return false;
            int cost = w.SeerCost();
            return w.gold >= cost * 10 && w.gold - cost >= (w.CurrentQuest?.weeklyFine ?? 0);
        }

        public override void OnVisitor(SimWorld w, SimVisitor v, ref float cursor)
        {
            SimWeapon weapon = null;

            // 네임드 우대 — 얼굴을 아는 단골이라 최적 타입 무기를 바로 내준다
            if (PreferNamed(w, v))
                weapon = BestFreeOfType(w, v.trueBest) ?? BestOfStatFamily(w, v.bestStatIndex);

            if (weapon == null && w.KnowsHighestStat)
            {
                // 통찰 70+ : 최고 스탯이 무료로 공개된다 — 대화 없이 계열 무기를 고르고 시간도 아낀다
                weapon = BestOfStatFamily(w, v.bestStatIndex);
            }
            else if (weapon == null)
            {
                // 스탯 테스트 1회 (임의의 스탯) — 성공하고 값이 높으면 해당 계열 무기
                int statIdx = w.rng.Next(4);
                if (w.TrySpendTalk(ref cursor) && w.Roll(w.StatRevealChance()) && v.stats[statIdx] >= 50)
                    weapon = BestOfStatFamily(w, statIdx);
            }

            if (weapon == null)
            {
                var free = FreeWeapons(w);
                if (free.Count > 0) weapon = free[w.rng.Next(free.Count)];
            }

            // 퀘스트 목표 던전을 챙기되, 감각파는 매번 정확히 챙기지는 않는다(절반 정도).
            // 100% 챙기게 두면 상급자와 통과율이 거의 같아져 구분선이 사라진다(실측).
            var targets = w.UnmetQuestDungeonIDs();
            BoardEntry entry = null;
            if (targets.Count > 0 && w.rng.NextDouble() < 0.25)
                entry = w.board.FirstOrDefault(e => targets.Contains(e.dungeon.StaticID));
            entry ??= SafestEntry(w);
            if (entry == null) return;
            // 부적은 재고만 있으면 아무 모험에나 쓴다 (로드맵 단계 3: "중급 = 아무데나")
            var item = w.BestItemInStock(ActiveItemType.Charm);
            // weapon == null이면 기본 무기. 네임드 우대 시엔 점술 문턱을 희귀+로 낮춘다
            w.LaunchAdventure(v, weapon, entry, cursor, item,
                              UseSeer(w, entry) || (PreferNamed(w, v) && UseSeerForNamed(w, entry)));
        }

        /// <summary>제작 목표 — 기본 부적만</summary>
        public override IEnumerable<ActiveItemRecipeData> CraftTargets(SimWorld w)
        {
            var charm = w.FirstUnlockedRecipe(ActiveItemType.Charm);
            if (charm != null) yield return charm;
        }

        /// <summary>무기 제작 목표 — 해금된 것 중 가장 싼 레시피 1종 (감각파는 전설까지 안 노린다)</summary>
        public override IEnumerable<WeaponRecipeData> WeaponCraftTargets(SimWorld w)
        {
            var r = w.b.weaponRecipes.Where(x => w.day >= x.unlockedDay)
                                     .OrderBy(x => x.requiredGold).FirstOrDefault();
            if (r != null) yield return r;
        }

        /// <summary>아침 이벤트 — "상자만 가끔" (로드맵 단계 1 정책). 골드 5배 여유일 때 25% 확률로 구매</summary>
        public override void OnMorningEvent(SimWorld w, MorningEventType type)
        {
            if (type != MorningEventType.MysteryBox) return;
            int tier = w.RollBoxTier();
            int cost = w.BoxCost(tier);
            if (w.gold >= cost * 5 && w.rng.NextDouble() < 0.25) w.BuyMysteryBox(tier);
        }

        /// <summary>유산 구매 — 싼 것부터, 긴급 환전용으로 20% 유보</summary>
        public override void OnLegacyShop(SimBundle b, SimOptions opt, SimCareer career, System.Random rng)
        {
            int reserve = UnityEngine.Mathf.RoundToInt(career.legacyPoints * 0.2f);
            while (true)
            {
                UpgradeKey best = UpgradeKey.None;
                int bestCost = int.MaxValue;
                foreach (var key in EffectiveUpgrades(opt))
                {
                    if (!career.CanPurchase(b, key)) continue;
                    int cost = career.NextCost(b, key);
                    if (cost < bestCost && career.legacyPoints - cost >= reserve) { best = key; bestCost = cost; }
                }
                if (best == UpgradeKey.None) break;
                career.Purchase(b, best);
            }
        }
    }

    /// <summary>초급자 "막무가내" — 정보 행동 0, 확인은 전령 때만 (무기 회전 일 1회)</summary>
    public class NovicePersona : SimPersona
    {
        public override string Name => "초급자";
        public override bool ImmediateConfirm => false;

        public override void OnBlacksmith(SimWorld w) { }

        public override void OnShop(SimWorld w, List<ShopItem> stock)
        {
            var cheapest = stock.OrderBy(i => i.price).FirstOrDefault();
            if (cheapest != null && cheapest.price <= w.gold / 2)
                w.BuyWeapon(cheapest);
        }

        public override List<BoardEntry> SelectBoard(SimWorld w, List<BoardEntry> pool) =>
            pool.Take(w.SelectCount()).ToList();

        public override void OnScouts(SimWorld w) { }

        public override void OnVisitor(SimWorld w, SimVisitor v, ref float cursor)
        {
            var free = FreeWeapons(w);
            SimWeapon weapon = free.Count > 0 ? free[w.rng.Next(free.Count)] : null;
            var entry = w.board.Count > 0 ? w.board[w.rng.Next(w.board.Count)] : null;
            if (entry == null) return;
            w.LaunchAdventure(v, weapon, entry, cursor);
        }

        /// <summary>아침 이벤트 — 참여 안 함 (로드맵 단계 1 정책)</summary>
        public override void OnMorningEvent(SimWorld w, MorningEventType type) { }

        /// <summary>유산 구매 — 살 수 있는 것 중 랜덤, 어디 쓸지 몰라 50% 유보</summary>
        public override void OnLegacyShop(SimBundle b, SimOptions opt, SimCareer career, System.Random rng)
        {
            int reserve = UnityEngine.Mathf.RoundToInt(career.legacyPoints * 0.5f);
            while (true)
            {
                var candidates = new List<UpgradeKey>();
                foreach (var key in EffectiveUpgrades(opt))
                    if (career.CanPurchase(b, key) && career.legacyPoints - career.NextCost(b, key) >= reserve)
                        candidates.Add(key);
                if (candidates.Count == 0) break;
                career.Purchase(b, candidates[rng.Next(candidates.Count)]);
            }
        }
    }
}
#endif
