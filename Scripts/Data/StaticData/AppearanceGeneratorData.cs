using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LayerLab.ArtMaker;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 파츠 인덱스 풀 ScriptableObject
    ///
    /// [규칙]
    /// - 파츠 총 개수(count)만 입력 → 0~count-1이 기본 풀
    /// - 직업군별 전용 인덱스(exclusive) 입력 → 해당 직업만 사용 가능
    /// - 어느 직업에도 지정되지 않은 인덱스는 모든 직업 공용
    /// - hair/brow: 성별 exclusive로 구분 (maleExclusive에 남성용, femaleExclusive에 여성용)
    /// </summary>
    [CreateAssetMenu(fileName = "AppearanceGeneratorData", menuName = "TodaysWeaponRental/AppearanceGeneratorData")]
    public class AppearanceGeneratorData : ScriptableObject
    {
        #region 파츠 총 개수

        [Header("=== 파츠 총 개수 (0 ~ count-1 자동 생성) ===")]
        [SerializeField] private int helmetCount;
        [SerializeField] private int backCount;
        [SerializeField] private int topCount;
        [SerializeField] private int bottomCount;
        [SerializeField] private int bootsCount;
        [SerializeField] private int glovesCount;

        [Header("=== 신체 파츠 총 개수 ===")]
        [SerializeField] private int skinCount;
        [SerializeField] private int eyesCount;
        [SerializeField] private int mouthCount;
        [SerializeField] private int browCount;
        [SerializeField] private int hairCount;
        [SerializeField] private int beardCount;
        [SerializeField] private int eyewearCount;

        #endregion

        #region 직업군별 전용 인덱스 (남성)

        [Header("=== 남성 전사(STR) 전용 인덱스 ===")]
        [SerializeField] private List<int> maleStrHelmetExclusive = new();
        [SerializeField] private List<int> maleStrBackExclusive = new();
        [SerializeField] private List<int> maleStrTopExclusive = new();
        [SerializeField] private List<int> maleStrBottomExclusive = new();
        [SerializeField] private List<int> maleStrBootsExclusive = new();
        [SerializeField] private List<int> maleStrGlovesExclusive = new();

        [Header("=== 남성 궁수(DEX) 전용 인덱스 ===")]
        [SerializeField] private List<int> maleDexHelmetExclusive = new();
        [SerializeField] private List<int> maleDexBackExclusive = new();
        [SerializeField] private List<int> maleDexTopExclusive = new();
        [SerializeField] private List<int> maleDexBottomExclusive = new();
        [SerializeField] private List<int> maleDexBootsExclusive = new();
        [SerializeField] private List<int> maleDexGlovesExclusive = new();

        [Header("=== 남성 마법사(INT) 전용 인덱스 ===")]
        [SerializeField] private List<int> maleIntHelmetExclusive = new();
        [SerializeField] private List<int> maleIntBackExclusive = new();
        [SerializeField] private List<int> maleIntTopExclusive = new();
        [SerializeField] private List<int> maleIntBottomExclusive = new();
        [SerializeField] private List<int> maleIntBootsExclusive = new();
        [SerializeField] private List<int> maleIntGlovesExclusive = new();

        [Header("=== 남성 도적(LUK) 전용 인덱스 ===")]
        [SerializeField] private List<int> maleLukHelmetExclusive = new();
        [SerializeField] private List<int> maleLukBackExclusive = new();
        [SerializeField] private List<int> maleLukTopExclusive = new();
        [SerializeField] private List<int> maleLukBottomExclusive = new();
        [SerializeField] private List<int> maleLukBootsExclusive = new();
        [SerializeField] private List<int> maleLukGlovesExclusive = new();

        #endregion

        #region 직업군별 전용 인덱스 (여성)

        [Header("=== 여성 전사(STR) 전용 인덱스 ===")]
        [SerializeField] private List<int> femaleStrHelmetExclusive = new();
        [SerializeField] private List<int> femaleStrBackExclusive = new();
        [SerializeField] private List<int> femaleStrTopExclusive = new();
        [SerializeField] private List<int> femaleStrBottomExclusive = new();
        [SerializeField] private List<int> femaleStrBootsExclusive = new();
        [SerializeField] private List<int> femaleStrGlovesExclusive = new();

        [Header("=== 여성 궁수(DEX) 전용 인덱스 ===")]
        [SerializeField] private List<int> femaleDexHelmetExclusive = new();
        [SerializeField] private List<int> femaleDexBackExclusive = new();
        [SerializeField] private List<int> femaleDexTopExclusive = new();
        [SerializeField] private List<int> femaleDexBottomExclusive = new();
        [SerializeField] private List<int> femaleDexBootsExclusive = new();
        [SerializeField] private List<int> femaleDexGlovesExclusive = new();

        [Header("=== 여성 마법사(INT) 전용 인덱스 ===")]
        [SerializeField] private List<int> femaleIntHelmetExclusive = new();
        [SerializeField] private List<int> femaleIntBackExclusive = new();
        [SerializeField] private List<int> femaleIntTopExclusive = new();
        [SerializeField] private List<int> femaleIntBottomExclusive = new();
        [SerializeField] private List<int> femaleIntBootsExclusive = new();
        [SerializeField] private List<int> femaleIntGlovesExclusive = new();

        [Header("=== 여성 도적(LUK) 전용 인덱스 ===")]
        [SerializeField] private List<int> femaleLukHelmetExclusive = new();
        [SerializeField] private List<int> femaleLukBackExclusive = new();
        [SerializeField] private List<int> femaleLukTopExclusive = new();
        [SerializeField] private List<int> femaleLukBottomExclusive = new();
        [SerializeField] private List<int> femaleLukBootsExclusive = new();
        [SerializeField] private List<int> femaleLukGlovesExclusive = new();

        #endregion

        #region 성별 전용 인덱스 (hair / brow)

        [Header("=== Hair 성별 전용 인덱스 ===")]
        [Tooltip("남성 전용 hair 인덱스 → 여성 풀에서 제외됨")]
        [SerializeField] private List<int> maleHairExclusive = new();
        [Tooltip("여성 전용 hair 인덱스 → 남성 풀에서 제외됨")]
        [SerializeField] private List<int> femaleHairExclusive = new();

        [Header("=== Brow 성별 전용 인덱스 ===")]
        [Tooltip("남성 전용 brow 인덱스 → 여성 풀에서 제외됨")]
        [SerializeField] private List<int> maleBrowExclusive = new();
        [Tooltip("여성 전용 brow 인덱스 → 남성 풀에서 제외됨")]
        [SerializeField] private List<int> femaleBrowExclusive = new();

        #endregion

        #region 색상 팔레트

        [Header("=== 피부 색상 팔레트 (인종별) ===")]
        [SerializeField] private List<Color> skinColorAsian = new();
        [SerializeField] private List<Color> skinColorWhite = new();
        [SerializeField] private List<Color> skinColorBlack = new();

        [Header("=== 피부 인종 비율 (나머지는 흑인) ===")]
        [SerializeField, Range(0f, 1f)] private float skinRatioAsian = 0.85f;
        [SerializeField, Range(0f, 1f)] private float skinRatioWhite = 0.14f;

        [Header("=== 머리카락 색상 팔레트 ===")]
        [SerializeField] private List<Color> hairColorPalette = new();

        #endregion

        #region 풀 생성 로직

        private List<int> BuildPool(int count, List<int> myExclusive, List<List<int>> othersExclusive)
        {
            var otherExclusiveSet = new HashSet<int>(othersExclusive.SelectMany(x => x));
            return Enumerable.Range(0, count)
                .Where(i => myExclusive.Contains(i) || !otherExclusiveSet.Contains(i))
                .ToList();
        }

        private List<int> GetPool(int count,
            List<int> str, List<int> dex, List<int> intList, List<int> luk,
            AdventurerJobType jobType)
        {
            return jobType switch
            {
                AdventurerJobType.STR => BuildPool(count, str,   new List<List<int>> { dex, intList, luk }),
                AdventurerJobType.DEX => BuildPool(count, dex,   new List<List<int>> { str, intList, luk }),
                AdventurerJobType.INT => BuildPool(count, intList, new List<List<int>> { str, dex, luk }),
                AdventurerJobType.LUK => BuildPool(count, luk,   new List<List<int>> { str, dex, intList }),
                _ => Enumerable.Range(0, count).ToList()
            };
        }

        #endregion

        #region 인덱스 풀 접근

        public List<int> GetHelmetPool(Gender gender, AdventurerJobType jobType) => gender == Gender.Male
            ? GetPool(helmetCount, maleStrHelmetExclusive, maleDexHelmetExclusive, maleIntHelmetExclusive, maleLukHelmetExclusive, jobType)
            : GetPool(helmetCount, femaleStrHelmetExclusive, femaleDexHelmetExclusive, femaleIntHelmetExclusive, femaleLukHelmetExclusive, jobType);

        public List<int> GetBackPool(Gender gender, AdventurerJobType jobType) => gender == Gender.Male
            ? GetPool(backCount, maleStrBackExclusive, maleDexBackExclusive, maleIntBackExclusive, maleLukBackExclusive, jobType)
            : GetPool(backCount, femaleStrBackExclusive, femaleDexBackExclusive, femaleIntBackExclusive, femaleLukBackExclusive, jobType);

        public List<int> GetTopPool(Gender gender, AdventurerJobType jobType) => gender == Gender.Male
            ? GetPool(topCount, maleStrTopExclusive, maleDexTopExclusive, maleIntTopExclusive, maleLukTopExclusive, jobType)
            : GetPool(topCount, femaleStrTopExclusive, femaleDexTopExclusive, femaleIntTopExclusive, femaleLukTopExclusive, jobType);

        public List<int> GetBottomPool(Gender gender, AdventurerJobType jobType) => gender == Gender.Male
            ? GetPool(bottomCount, maleStrBottomExclusive, maleDexBottomExclusive, maleIntBottomExclusive, maleLukBottomExclusive, jobType)
            : GetPool(bottomCount, femaleStrBottomExclusive, femaleDexBottomExclusive, femaleIntBottomExclusive, femaleLukBottomExclusive, jobType);

        public List<int> GetBootsPool(Gender gender, AdventurerJobType jobType) => gender == Gender.Male
            ? GetPool(bootsCount, maleStrBootsExclusive, maleDexBootsExclusive, maleIntBootsExclusive, maleLukBootsExclusive, jobType)
            : GetPool(bootsCount, femaleStrBootsExclusive, femaleDexBootsExclusive, femaleIntBootsExclusive, femaleLukBootsExclusive, jobType);

        public List<int> GetGlovesPool(Gender gender, AdventurerJobType jobType) => gender == Gender.Male
            ? GetPool(glovesCount, maleStrGlovesExclusive, maleDexGlovesExclusive, maleIntGlovesExclusive, maleLukGlovesExclusive, jobType)
            : GetPool(glovesCount, femaleStrGlovesExclusive, femaleDexGlovesExclusive, femaleIntGlovesExclusive, femaleLukGlovesExclusive, jobType);

        public List<int> GetHairPool(Gender gender) => gender == Gender.Male
            ? BuildPool(hairCount, maleHairExclusive, new List<List<int>> { femaleHairExclusive })
            : BuildPool(hairCount, femaleHairExclusive, new List<List<int>> { maleHairExclusive });

        public List<int> GetBrowPool(Gender gender) => gender == Gender.Male
            ? BuildPool(browCount, maleBrowExclusive, new List<List<int>> { femaleBrowExclusive })
            : BuildPool(browCount, femaleBrowExclusive, new List<List<int>> { maleBrowExclusive });

        public List<int> GetSkinPool()    => Enumerable.Range(0, skinCount).ToList();
        public List<int> GetEyesPool()    => Enumerable.Range(0, eyesCount).ToList();
        public List<int> GetMouthPool()   => Enumerable.Range(0, mouthCount).ToList();
        public List<int> GetBeardPool()   => Enumerable.Range(0, beardCount).ToList();
        public List<int> GetEyewearPool() => Enumerable.Range(0, eyewearCount).ToList();

        public Color GetRandomSkinColor()
        {
            float roll = UnityEngine.Random.value;
            List<Color> pool = roll < skinRatioAsian ? skinColorAsian
                : roll < skinRatioAsian + skinRatioWhite ? skinColorWhite
                : skinColorBlack;

            return pool.Count > 0
                ? pool[UnityEngine.Random.Range(0, pool.Count)]
                : new Color(1f, 0.8f, 0.7f);
        }

        public Color GetRandomHairColor() => hairColorPalette.Count > 0
            ? hairColorPalette[UnityEngine.Random.Range(0, hairColorPalette.Count)]
            : new Color(0.5f, 0.3f, 0.1f);

        #endregion
    }

    public enum AdventurerJobType
    {
        STR,
        DEX,
        INT,
        LUK
    }
}