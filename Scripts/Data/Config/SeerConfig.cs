// Scripts/Data/Config/SeerConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 수정구 서사 대사 — 한 AbstractLevel에 대해 LuckLevel별 대사 풀을 담는다.
    /// </summary>
    [Serializable]
    public class AbstractLuckLines
    {
        [Tooltip("흉")]   public List<string> bad   = new();
        [Tooltip("소길")] public List<string> minor = new();
        [Tooltip("중길")] public List<string> good  = new();
        [Tooltip("대길")] public List<string> great = new();
    }

    [CreateAssetMenu(fileName = "SeerConfig", menuName = "TodaysWeaponRental/Config/SeerConfig")]
    public class SeerConfig : ScriptableObject
    {
        [Header("비용")]
        [Tooltip("상담 기준 비용. 실제 비용 = 이 값 x PriceTierConfig.seerCost[주차 구간]")]
        public int seerBaseCost = 300;

        [Header("운세 — cumulativeModifier")]
        [Tooltip("흉: 이벤트 성공률 보정 (음수)")]
        public float luckModifierBad = -0.10f;
        [Tooltip("소길: 이벤트 성공률 보정")]
        public float luckModifierMinor = 0.05f;
        [Tooltip("중길: 이벤트 성공률 보정")]
        public float luckModifierGood = 0.10f;
        [Tooltip("대길: 이벤트 성공률 보정")]
        public float luckModifierGreat = 0.20f;

        [Header("LuckLevel 가중치 — LUK 0~25")]
        public float luk0BadWeight   = 40f;
        public float luk0MinorWeight = 35f;
        public float luk0GoodWeight  = 18f;
        public float luk0GreatWeight =  7f;

        [Header("LuckLevel 가중치 — LUK 26~50")]
        public float luk1BadWeight   = 25f;
        public float luk1MinorWeight = 38f;
        public float luk1GoodWeight  = 27f;
        public float luk1GreatWeight = 10f;

        [Header("LuckLevel 가중치 — LUK 51~75")]
        public float luk2BadWeight   = 12f;
        public float luk2MinorWeight = 30f;
        public float luk2GoodWeight  = 38f;
        public float luk2GreatWeight = 20f;

        [Header("LuckLevel 가중치 — LUK 76+")]
        public float luk3BadWeight   =  5f;
        public float luk3MinorWeight = 20f;
        public float luk3GoodWeight  = 40f;
        public float luk3GreatWeight = 35f;

        [Header("이스터에그 — LUK 무시 균등 랜덤")]
        [Tooltip("수정구를 직접 건드렸을 때 사용하는 가중치. 기본값은 균등(25/25/25/25).")]
        public float eggBadWeight   = 25f;
        public float eggMinorWeight = 25f;
        public float eggGoodWeight  = 25f;
        public float eggGreatWeight = 25f;

        // ─────────────────────────────────────────────
        // 대사 풀 — 각 리스트에서 랜덤 1개 추출. 비어 있으면 코드의 fallback 사용.
        // ─────────────────────────────────────────────

        [Header("첫 인사 — 비용은 {0}에 삽입 (예: \"점보러 오셨어요?\\n상담 비용: {0}G\")")]
        public List<string> greetingLines = new();

        [Header("상담 결과 코멘트 — LuckLevel별")]
        public List<string> commentBadLines   = new();
        public List<string> commentMinorLines = new();
        public List<string> commentGoodLines  = new();
        public List<string> commentGreatLines = new();

        [Header("수정구 직접 클릭(이스터에그) — 클릭 누적 단계별")]
        [Tooltip("이 횟수째 클릭부터 실제 결제+이스터에그 점술을 진행한다. 그 이전 클릭(1~3회차)은 경고 대사만 출력.")]
        public int easterEggConsultAtTouch = 4;
        [Tooltip("1회차 경고")]
        public List<string> easterEggLines1 = new();
        [Tooltip("2회차 경고")]
        public List<string> easterEggLines2 = new();
        [Tooltip("3회차 — 강한 경고(잠시만요! 등)")]
        public List<string> easterEggLines3 = new();
        [Tooltip("이스터에그 점술 발동 시 결과 코멘트 앞에 붙는 대사('만지면 안되지만.. 결과는 말해드릴게요' 등)")]
        public List<string> easterEggConsultPrefaceLines = new();
        [Tooltip("경고 후 next 클릭 시 나오는 후속 대사('참 말썽쟁이시네요. 그래서 점술 보러 오셨어요?' 등)")]
        public List<string> easterEggFollowUpLines = new();

        [Header("재방문(이미 상담함) — 방문 누적 단계별")]
        [Tooltip("1회차")]
        public List<string> revisitLines1 = new();
        [Tooltip("2회차")]
        public List<string> revisitLines2 = new();
        [Tooltip("3회차 이상")]
        public List<string> revisitLines3 = new();

        [Header("수정구 서사 대사 — 대길조 (LuckLevel별)")]
        public AbstractLuckLines descGreat = new();
        [Header("수정구 서사 대사 — 길조 (LuckLevel별)")]
        public AbstractLuckLines descGood = new();
        [Header("수정구 서사 대사 — 보통 (LuckLevel별)")]
        public AbstractLuckLines descNormal = new();
        [Header("수정구 서사 대사 — 험난 (LuckLevel별)")]
        public AbstractLuckLines descHarsh = new();
        [Header("수정구 서사 대사 — 암담 (LuckLevel별)")]
        public AbstractLuckLines descGrim = new();

        /// <summary>
        /// 대사 리스트에서 랜덤 1개를 뽑아 현재 언어로 돌려준다. 리스트가 비어 있으면 fallback을 반환한다.
        /// <paramref name="path"/>는 SO 루트부터 이 리스트까지의 필드 경로(`greetingLines`,
        /// `descGreat_bad`)이고 그대로 번역 키가 된다 — 호출측에서 <c>nameof</c>로 만든다.
        /// </summary>
        public static string PickRandom(List<string> pool, string fallback, string path)
        {
            if (pool == null || pool.Count == 0)
                return fallback;

            int i = UnityEngine.Random.Range(0, pool.Count);
            // 번역을 먼저 얹고 \n 치환은 그 뒤에 한다. 순서를 바꾸면 번역문에 적힌 \n이
            // 두 글자 그대로 화면에 뜬다.
            return DataLocalizer.SeerLine(path, i, pool[i]).Replace("\\n", "\n");
        }

        /// <summary>
        /// 대사 리스트에서 랜덤 1개를 뽑되 **번역하지 않고** 원문과 키를 함께 돌려준다.
        /// 세이브에 구워지는 값(수정구 서사)에 쓴다 — 번역된 문장을 구우면 상담할 때의 언어가
        /// 그대로 굳어, 나중에 언어를 바꿔도 그 결과만 안 바뀐다.
        /// 풀이 비어 있으면 fallback을 돌려주고 <paramref name="key"/>는 null이다.
        /// </summary>
        public static string PickRandomKeyed(List<string> pool, string fallback, string path, out string key)
        {
            key = null;
            if (pool == null || pool.Count == 0)
                return fallback;

            int i = UnityEngine.Random.Range(0, pool.Count);
            key = DataLocalizer.SeerLineKey(path, i);
            return pool[i];
        }

        /// <summary>
        /// AbstractLevel × LuckLevel 조합에 해당하는 수정구 서사 대사 풀을 반환한다.
        /// <paramref name="path"/>로 그 풀의 필드 경로(`descGreat_bad`)도 함께 돌려준다 —
        /// 리스트만 받으면 어느 조합이었는지가 사라져 번역 키를 만들 수 없다.
        /// </summary>
        public List<string> GetDescriptionPool(AbstractLevel abs, LuckLevel luck, out string path)
        {
            path = null;

            AbstractLuckLines lines;
            string absField;
            switch (abs)
            {
                case AbstractLevel.대길조: lines = descGreat;  absField = nameof(descGreat);  break;
                case AbstractLevel.길조:   lines = descGood;   absField = nameof(descGood);   break;
                case AbstractLevel.보통:   lines = descNormal; absField = nameof(descNormal); break;
                case AbstractLevel.험난:   lines = descHarsh;  absField = nameof(descHarsh);  break;
                case AbstractLevel.암담:   lines = descGrim;   absField = nameof(descGrim);   break;
                default: return null;
            }
            if (lines == null) return null;

            switch (luck)
            {
                case LuckLevel.흉:   path = absField + "_" + nameof(lines.bad);   return lines.bad;
                case LuckLevel.소길: path = absField + "_" + nameof(lines.minor); return lines.minor;
                case LuckLevel.중길: path = absField + "_" + nameof(lines.good);  return lines.good;
                case LuckLevel.대길: path = absField + "_" + nameof(lines.great); return lines.great;
                default: return null;
            }
        }
    }
}
