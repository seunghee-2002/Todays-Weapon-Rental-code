// Scripts/UI/Core/ColorManager.cs
using UnityEngine;

namespace TodaysWeaponRental
{
    public class ColorManager : BaseManager<ColorManager>
    {
        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            DontDestroyOnLoad(gameObject);
        }

        #endregion


        [Header("텍스트 및 UI 요소 색상")]
        [SerializeField] private Color whiteColor;
        [SerializeField] private Color blackColor;
        [SerializeField] private Color goldColor;
        [SerializeField] private Color grayColor; // 어두운 색 배경에 활용
        [SerializeField] private Color greenColor; // 긍정적 수치에 활용
        [SerializeField] private Color redColor;   // 부정적 수치에 활용
        [SerializeField] private Color skyblueColor;
        [SerializeField] private Color orangeColor;
        [SerializeField] private Color discountColor; // 할인 수치에 활용 (초록 배경 위 가독성)

        [Header("스탯별 색상 (모험가 최고 스탯)")]
        [SerializeField] private Color statStrColor;  // STR = 빨강
        [SerializeField] private Color statDexColor;  // DEX = 하늘
        [SerializeField] private Color statIntColor;  // INT = 분홍
        [SerializeField] private Color statLukColor;  // LUK = 초록

        [Header("등급별 기본 색상")]
        [SerializeField] private Color commonColor;
        [SerializeField] private Color uncommonColor;
        [SerializeField] private Color rareColor;
        [SerializeField] private Color epicColor;
        [SerializeField] private Color legendaryColor;

        [Header("등급별 카드 배경색")]
        [SerializeField] private Color commonCardBackground;
        [SerializeField] private Color uncommonCardBackground;
        [SerializeField] private Color rareCardBackground;
        [SerializeField] private Color epicCardBackground;
        [SerializeField] private Color legendaryCardBackground;

        [Header("등급별 프레임/텍스트색")]
        [SerializeField] private Color commonAccent;
        [SerializeField] private Color uncommonAccent;
        [SerializeField] private Color rareAccent;
        [SerializeField] private Color epicAccent;
        [SerializeField] private Color legendaryAccent;

        [Header("부가효과 상태 색상")]
        [SerializeField] private Color effectMaxValueColor;
        [SerializeField] private Color effectInactiveColor;
        [SerializeField] private Color effectDefaultColor;
        [SerializeField] private Color effectMaxValueGlowColor;

        [Header("미션 성공 카드 색상")]
        [SerializeField] private Color missionCompletedCardColor;
        [SerializeField] private Color missionUncompletedCardColor;

        [Header("상성 아이콘 색상")]
        [SerializeField] private Color advantageIconColor;
        [SerializeField] private Color disadvantageIconColor;
        [SerializeField] private Color advantageTextColor;
        [SerializeField] private Color disadvantageTextColor;

        [Header("단계 상태 색상")]
        [SerializeField] private Color stepBGDefaultColor;
        [SerializeField] private Color stepBGCompletedColor;
        [SerializeField] private Color stepBorderDefaultColor;
        [SerializeField] private Color stepBorderCompletedColor;

        [Header("서브 탭 색상")]
        [SerializeField] private Color subTabActiveColor;
        [SerializeField] private Color subTabInactiveColor;

        [Header("글로우 색상")]
        [SerializeField] private Color glowWhiteColor;
        [SerializeField] private Color glowBrownColor;
        [SerializeField] private Color glowGreenColor;
        [SerializeField] private Color glowBlueColor;
        [SerializeField] private Color glowPurpleColor;
        [SerializeField] private Color glowRedColor;

        [Header("별 배경 글로우 색상")]
        [SerializeField] private Color successStarBGColor;
        [SerializeField] private Color failedStarBGColor;

        [Header("수수께끼 상자 글로우 색상")]
        [SerializeField] private Color mysteryBoxNormalColor;  // tier 0 = 일반
        [SerializeField] private Color mysteryBoxRareColor;    // tier 1 = 희귀
        [SerializeField] private Color mysteryBoxMythicColor;  // tier 2 = 신비

        [Header("버튼 텍스트 색")]
        [SerializeField] private Color greenButtonText;

        #region 공개 메서드

        public Color GetWhiteColor() => whiteColor;
        public Color GetBlackColor() => blackColor;
        public Color GetGoldColor() => goldColor;
        public Color GetGrayColor() => grayColor;
        public Color GetGreenColor() => greenColor;
        public Color GetRedColor() => redColor;
        public Color GetSkyBlueColor() => skyblueColor;
        public Color GetOrangeColor() => orangeColor;
        public Color GetDiscountColor() => discountColor;

        /// <summary>
        /// 등급별 기본 색상 반환(흰색, 초록색, 파란색, 보라색, 주황색)
        /// </summary>
        public Color GetGradeColor(Grade grade)
        {
            return grade switch
            {
                Grade.Common    => commonColor,
                Grade.Uncommon  => uncommonColor,
                Grade.Rare      => rareColor,
                Grade.Epic      => epicColor,
                Grade.Legendary => legendaryColor,
                _ => Color.white
            };
        }

        /// <summary>
        /// 모험가 최고 스탯에 따른 색상 반환 (STR=빨강, DEX=하늘, INT=분홍, LUK=초록)
        /// </summary>
        public Color GetHighestStatColor(AdventurerStat stat)
        {
            return stat switch
            {
                AdventurerStat.STR => statStrColor,
                AdventurerStat.DEX => statDexColor,
                AdventurerStat.INT => statIntColor,
                AdventurerStat.LUK => statLukColor,
                _ => whiteColor
            };
        }

        /// <summary>
        /// 등급별 카드 배경색 반환(어두운 색상) (Bg, Frame)
        /// </summary>
        public Color GetGradeCardBackgroundColor(Grade grade)
        {
            return grade switch
            {
                Grade.Common    => commonCardBackground,
                Grade.Uncommon  => uncommonCardBackground,
                Grade.Rare      => rareCardBackground,
                Grade.Epic      => epicCardBackground,
                Grade.Legendary => legendaryCardBackground,
                _ => Color.black
            };
        }

        /// <summary>
        /// 등급별 프레임/텍스트 색상 반환(밝은 색상)
        /// </summary>
        public Color GetGradeAccentColor(Grade grade)
        {
            return grade switch
            {
                Grade.Common    => commonAccent,
                Grade.Uncommon  => uncommonAccent,
                Grade.Rare      => rareAccent,
                Grade.Epic      => epicAccent,
                Grade.Legendary => legendaryAccent,
                _ => Color.white
            };
        }

        /// <summary>
        /// 등급별 글로우 색상 반환 (Common=Brown, Uncommon=Green, Rare=Blue, Epic=Purple, Legendary=Red)
        /// </summary>
        public Color GetGradeGlowColor(Grade grade)
        {
            return grade switch
            {
                Grade.Common    => glowBrownColor,
                Grade.Uncommon  => glowGreenColor,
                Grade.Rare      => glowBlueColor,
                Grade.Epic      => glowPurpleColor,
                Grade.Legendary => glowRedColor,
                _ => glowWhiteColor
            };
        }

        // 부가효과 상태에 따른 색상 반환
        public Color GetEffectMaxValueColor()  => effectMaxValueColor;
        public Color GetEffectInactiveColor()   => effectInactiveColor;
        public Color GetEffectDefaultColor()    => effectDefaultColor;
        public Color GetEffectMaxValueGlowColor() => effectMaxValueGlowColor;

        // 미션 완료 여부에 따른 카드 색상 반환
        public Color GetMissionCardColor(bool isCompleted)
        {
            return isCompleted ? missionCompletedCardColor : missionUncompletedCardColor;
        }

        public Color GetAdvantageIconColor(bool isAdvantage)
        {
            return isAdvantage ? advantageIconColor : disadvantageIconColor;
        }

        public Color GetAdvantageTextColor(bool isAdvantage)
        {
            return isAdvantage ? advantageTextColor : disadvantageTextColor;
        }

        public Color GetStepBGColor(bool isCompleted)
        {
            return isCompleted ? stepBGCompletedColor : stepBGDefaultColor;
        }

        public Color GetStepBorderColor(bool isCompleted)
        {
            return isCompleted ? stepBorderCompletedColor : stepBorderDefaultColor;
        }

        public Color GetSubTabColor(bool isActive)
        {
            return isActive ? subTabActiveColor : subTabInactiveColor;
        }

        /// <summary>
        /// 모험 결과 아이템 컨테이너에서 사용하는 글로우 색상 반환
        /// </summary>
        public Color GetResultItemGlowColor(ResultItemType type)
        {
            return type switch
            {
                ResultItemType.Reputation       => glowBrownColor,
                ResultItemType.Affection        => glowRedColor,
                ResultItemType.Insight          => glowBlueColor,
                ResultItemType.MaterialCraft    => glowGreenColor,
                ResultItemType.MaterialEnforce  => glowBlueColor,
                ResultItemType.MaterialSpecial  => glowPurpleColor,
                _ => glowWhiteColor
            };
        }

        public Color GetGlowByArmorType(ArmorType armorType)
        {
            return armorType switch
            {
                ArmorType.Unarmored => glowBrownColor,
                ArmorType.LightArmor => glowGreenColor,
                ArmorType.HeavyArmor => glowPurpleColor,
                ArmorType.MagicalArmor => glowBlueColor,
                _ => glowWhiteColor
            };
        }

        // 모험 결과 별 배경 색상 반환 (성공: successStarBGColor, 실패: failedStarBGColor)
        public Color GetResultStarBGColor(bool isSuccess) => isSuccess ? successStarBGColor : failedStarBGColor;

        /// <summary>
        /// 수수께끼 상자 tier별 글로우 색상 반환. tier: 0=일반, 1=희귀, 2=신비
        /// </summary>
        public Color GetMysteryBoxColor(int tier)
        {
            return tier switch
            {
                0 => mysteryBoxNormalColor,
                1 => mysteryBoxRareColor,
                2 => mysteryBoxMythicColor,
                _ => whiteColor
            };
        }

        public Color GetGreenButtonTextColor()  => greenButtonText;

        #endregion
    }
}
