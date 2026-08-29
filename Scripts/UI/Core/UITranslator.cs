using UnityEngine;
using UnityEngine.Localization.Settings;
using TodaysWeaponRental;

/// <summary>
/// UI에 표시될 데이터를 유저가 읽기 좋은 문자열로 변환하는 유틸리티 클래스.
/// enum 라벨은 UI_Common 스트링 테이블에서 현재 로케일로 조회한다 (미번역 로케일은 폴백으로 한국어 표시).
/// 키 규칙: {EnumType}_{멤버명} — enum 멤버명을 바꾸면 UI_Common 테이블 키도 같이 바꿔야 한다.
/// </summary>
public static class UITranslator
{
    private const string TableName = "UI_Common";

    // UI_Common 테이블에서 현재 로케일 문자열 조회. 프리로드된 테이블이라 사실상 딕셔너리 조회다.
    private static string Get(string key)
        => LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);

    // 등급(Grade)에 따른 문자열 변환
    public static string GetString(Grade grade)
    {
        return grade switch
        {
            Grade.Common => Get("Grade_Common"),
            Grade.Uncommon => Get("Grade_Uncommon"),
            Grade.Rare => Get("Grade_Rare"),
            Grade.Epic => Get("Grade_Epic"),
            Grade.Legendary => Get("Grade_Legendary"),
            _ => Get("Common_Unknown") // 확실하지 않은 값에 대한 예외 처리
        };
    }

    // 무기 유형(WeaponType)에 따른 문자열 변환
    public static string GetString(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Sword    => Get("WeaponType_Sword"),
            WeaponType.Axe      => Get("WeaponType_Axe"),
            WeaponType.Bow      => Get("WeaponType_Bow"),
            WeaponType.Crossbow => Get("WeaponType_Crossbow"),
            WeaponType.Staff    => Get("WeaponType_Staff"),
            WeaponType.Tome     => Get("WeaponType_Tome"),
            WeaponType.Dagger   => Get("WeaponType_Dagger"),
            WeaponType.Shuriken => Get("WeaponType_Shuriken"),
            _                   => Get("Common_Unknown")
        };
    }

    // 방어구 유형(ArmorType)에 따른 문자열 변환
    public static string GetString(ArmorType armorType)
    {
        return armorType switch
        {
            ArmorType.Unarmored => Get("ArmorType_Unarmored"),
            ArmorType.LightArmor => Get("ArmorType_LightArmor"),
            ArmorType.HeavyArmor => Get("ArmorType_HeavyArmor"),
            ArmorType.MagicalArmor => Get("ArmorType_MagicalArmor"),
            _ => Get("Common_Unknown")
        };
    }

    // 모험가 유형(AdventurerStat)에 따른 문자열 변환
    public static string GetString(AdventurerStat adventurerStat)
    {
        return adventurerStat switch
        {
            AdventurerStat.STR => Get("AdventurerStat_STR"),
            AdventurerStat.DEX => Get("AdventurerStat_DEX"),
            AdventurerStat.INT => Get("AdventurerStat_INT"),
            AdventurerStat.LUK => Get("AdventurerStat_LUK"),
            _ => Get("Common_Unknown")
        };
    }

    // 특성 유형(TraitType)에 따른 문자열 변환
    public static string GetString(TraitType traitType)
    {
        return traitType switch
        {
            TraitType.Lucky        => Get("TraitType_Lucky"),
            TraitType.Veteran      => Get("TraitType_Veteran"),
            TraitType.Looter       => Get("TraitType_Looter"),
            TraitType.Swift        => Get("TraitType_Swift"),
            TraitType.Rich         => Get("TraitType_Rich"),
            TraitType.Porter       => Get("TraitType_Porter"),
            TraitType.Berserker    => Get("TraitType_Berserker"),
            TraitType.Enduring     => Get("TraitType_Enduring"),
            TraitType.Rising       => Get("TraitType_Rising"),
            TraitType.Famous       => Get("TraitType_Famous"),
            TraitType.Haggler      => Get("TraitType_Haggler"),
            TraitType.Coward       => Get("TraitType_Coward"),
            TraitType.Focused      => Get("TraitType_Focused"),
            TraitType.Butcher      => Get("TraitType_Butcher"),
            TraitType.EasyExpert   => Get("TraitType_EasyExpert"),
            TraitType.BattleManiac => Get("TraitType_BattleManiac"),
            _ => Get("Common_Unknown")
        };
    }

    // 특성(TraitType)의 효과 설명 문자열. 증감 값에 초록/빨강 색상 태그를 입힌다.
    public static string GetTraitEffectString(TraitType trait)
    {
        var cfg = ConfigManager.Instance.Trait;
        string greenHex = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGreenColor());
        string redHex   = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetRedColor());
        string grayHex  = "#" + ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetGrayColor());
        return trait switch
        {
            TraitType.Lucky        => TraitLine("TraitEffect_Lucky", greenHex, redHex, grayHex,
                                          ("value", TraitPct(cfg.traitLuckyGreatSuccessBonus))),
            TraitType.Veteran      => TraitLine("TraitEffect_Veteran", greenHex, redHex, grayHex,
                                          ("value", $"{cfg.traitVeteranExplorationMultiplier:0.##}")),
            TraitType.Looter       => TraitLine("TraitEffect_Looter", greenHex, redHex, grayHex,
                                          ("value", cfg.traitLooterMaterialBonus)),
            TraitType.Swift        => TraitLine("TraitEffect_Swift", greenHex, redHex, grayHex,
                                          ("value", $"{cfg.traitSwiftDurationMultiplier:0.##}")),
            TraitType.Rich         => TraitLine("TraitEffect_Rich", greenHex, redHex, grayHex,
                                          ("value", TraitPct(cfg.traitRichTipRate))),
            TraitType.Porter       => TraitLine("TraitEffect_Porter", greenHex, redHex, grayHex,
                                          ("value", TraitMultPct(cfg.traitPorterMaterialPriceMultiplier))),
            TraitType.Berserker    => TraitLine("TraitEffect_Berserker", greenHex, redHex, grayHex,
                                          ("success", TraitMultPct(cfg.traitBerserkerSuccessMultiplier)),
                                          ("death",   TraitMultPct(cfg.traitBerserkerDeathMultiplier))),
            TraitType.Enduring     => TraitLine("TraitEffect_Enduring", greenHex, redHex, grayHex,
                                          ("value", cfg.traitEnduringProtectionBonus)),
            TraitType.Rising       => TraitLine("TraitEffect_Rising", greenHex, redHex, grayHex,
                                          ("value", TraitPct(cfg.traitRisingBonusPerTier))),
            TraitType.Famous       => TraitLine("TraitEffect_Famous", greenHex, redHex, grayHex,
                                          ("value", cfg.traitFamousReputationBonus)),
            TraitType.Haggler      => TraitLine("TraitEffect_Haggler", greenHex, redHex, grayHex,
                                          ("low",  $"{cfg.traitHagglerRateLow * 100f:F0}%"),
                                          ("mid",  $"{cfg.traitHagglerRateMid * 100f:F0}%"),
                                          ("high", TraitPct(cfg.traitHagglerRateHigh))),
            TraitType.Coward       => TraitLine("TraitEffect_Coward", greenHex, redHex, grayHex,
                                          ("death",   TraitMultPct(cfg.traitCowardDeathMultiplier)),
                                          ("success", TraitMultPct(cfg.traitCowardSuccessMultiplier))),
            TraitType.Focused      => TraitLine("TraitEffect_Focused", greenHex, redHex, grayHex,
                                          ("duration", $"{cfg.traitFocusedDurationMultiplier:0.##}"),
                                          ("success",  TraitPct(cfg.traitFocusedSuccessBonus))),
            TraitType.Butcher      => TraitLine("TraitEffect_Butcher", greenHex, redHex, grayHex,
                                          ("material", cfg.traitButcherMaterialBonus),
                                          ("fee",      TraitMultPct(cfg.traitButcherFeeMultiplier))),
            TraitType.EasyExpert   => TraitLine("TraitEffect_EasyExpert", greenHex, redHex, grayHex,
                                          ("low",  TraitPct(cfg.traitEasyExpertLowTierBonus)),
                                          ("high", TraitPct(cfg.traitEasyExpertHighTierPenalty))),
            TraitType.BattleManiac => TraitLine("TraitEffect_BattleManiac", greenHex, redHex, grayHex,
                                          ("value", TraitPct(cfg.traitBattleManiacSuccessBonus))),
            _                      => ""
        };
    }

    /// <summary>가산 비율(0.1) → 부호 포함 % 문자열("+10%").</summary>
    private static string TraitPct(float fraction)
    {
        string sign = fraction >= 0f ? "+" : "";
        return $"{sign}{fraction * 100f:F0}%";
    }

    /// <summary>곱배율(1.1 / 0.85) → 부호 포함 상대 % 문자열("+10%" / "-15%").</summary>
    private static string TraitMultPct(float multiplier) => TraitPct(multiplier - 1f);

    /// <summary>특성 효과 한 줄. 색상 hex 3종은 모든 문구가 공유하므로 여기서 한 번에 넣는다.</summary>
    private static string TraitLine(string key, string green, string red, string gray,
                                    params (string Name, object Value)[] args)
    {
        var dict = new System.Collections.Generic.Dictionary<string, object>
        {
            { "green", green }, { "red", red }, { "gray", gray }
        };
        foreach (var a in args) dict[a.Name] = a.Value;
        return LocalizationSettings.StringDatabase.GetLocalizedString(
            "UI_Common", key, arguments: new object[] { dict });
    }

    // 단어 끝 받침에 따라 목적격 조사(을/를)를 반환한다. 한글 음절이 아니면 "을(를)".
    // 조사는 한국어에만 있는 문법 요소라, 다른 로케일에서는 빈 문자열을 돌려준다
    // (번역문은 "{name}{particle} ..." 자리에 조사 없이 그대로 이어붙게 쓴다).
    public static string ObjectParticle(string word)
    {
        if (LocalizationSettings.SelectedLocale != null &&
            LocalizationSettings.SelectedLocale.Identifier.Code != "ko-KR") return "";
        if (string.IsNullOrEmpty(word)) return "을(를)";
        char last = word[^1];
        if (last < 0xAC00 || last > 0xD7A3) return "을(를)";   // 한글 음절이 아니면 안전 표기
        bool hasFinalConsonant = ((last - 0xAC00) % 28) != 0;
        return hasFinalConsonant ? "을" : "를";
    }

    // 방문자 유형(VisitorType)에 따른 문자열 변환
    public static string GetString(VisitorType visitorType)
    {
        return visitorType switch
        {
            VisitorType.Adventurer => Get("VisitorType_Adventurer"),
            VisitorType.WeaponShop => Get("VisitorType_WeaponShop"),
            VisitorType.Blacksmith => Get("VisitorType_Blacksmith"),
            VisitorType.EventNPC => Get("VisitorType_EventNPC"),
            VisitorType.DeadEvent => Get("VisitorType_DeadEvent"),
            VisitorType.InvestorResult => Get("VisitorType_InvestorResult"),
            VisitorType.Herald => Get("VisitorType_Herald"),
            _ => Get("Common_Unknown")
        };
    }

    // 대화창 역할 라벨용 짧은 표기. 문장에 들어가는 GetString과 달리 좁은 라벨 칸에 맞춘 표기다.
    public static string GetVisitorRoleLabel(VisitorType visitorType)
    {
        return visitorType switch
        {
            VisitorType.Adventurer => Get("VisitorRole_Adventurer"),
            VisitorType.WeaponShop => Get("VisitorRole_WeaponShop"),
            VisitorType.Blacksmith => Get("VisitorRole_Blacksmith"),
            VisitorType.EventNPC => Get("VisitorRole_EventNPC"),
            VisitorType.DeadEvent => Get("VisitorRole_DeadEvent"),
            VisitorType.InvestorResult => Get("VisitorRole_InvestorResult"),
            VisitorType.Herald => Get("VisitorRole_Herald"),
            _ => Get("Common_Unknown")
        };
    }

    // 재료 유형(MaterialType)에 따른 문자열 변환
    public static string GetString(MaterialType materialType)
    {
        return materialType switch
        {
            MaterialType.Enforce => Get("MaterialType_Enforce"),
            MaterialType.Craft => Get("MaterialType_Craft"),
            MaterialType.Special => Get("MaterialType_Special"),
            _ => Get("Common_Unknown")
        };
    }

    // 액티브 아이템 유형(ActiveItemType)에 따른 문자열 변환
    public static string GetString(ActiveItemType activeItemType)
    {
        return activeItemType switch
        {
            ActiveItemType.Charm            => Get("ActiveItemType_Charm"),
            ActiveItemType.Potion           => Get("ActiveItemType_Potion"),
            ActiveItemType.EscapeRope       => Get("ActiveItemType_EscapeRope"),
            ActiveItemType.SwiftShoes       => Get("ActiveItemType_SwiftShoes"),
            ActiveItemType.DisassemblyKnife => Get("ActiveItemType_DisassemblyKnife"),
            ActiveItemType.GoldAmulet       => Get("ActiveItemType_GoldAmulet"),
            ActiveItemType.TreasureMap      => Get("ActiveItemType_TreasureMap"),
            ActiveItemType.DeathWard        => Get("ActiveItemType_DeathWard"),
            ActiveItemType.FameScroll       => Get("ActiveItemType_FameScroll"),
            ActiveItemType.Doll             => Get("ActiveItemType_Doll"),
            ActiveItemType.ForgeStone       => Get("ActiveItemType_ForgeStone"),
            _ => Get("Common_Unknown")
        };
    }

    // 액티브 아이템 사용 컨텍스트(ActiveItemUsage)에 따른 문자열 변환
    public static string GetString(ActiveItemUsage usage)
    {
        return usage switch
        {
            ActiveItemUsage.Immediate  => Get("ActiveItemUsage_Immediate"),
            ActiveItemUsage.Adventure  => Get("ActiveItemUsage_Adventure"),
            ActiveItemUsage.Blacksmith => Get("ActiveItemUsage_Blacksmith"),
            _ => Get("Common_Unknown")
        };
    }

    // 호감도 수치에 따른 문자열 변환
    public static string GetString(AffectionLevel affection)
    {
        return affection switch
        {
            AffectionLevel.Low => Get("AffectionLevel_Low"),
            AffectionLevel.Medium => Get("AffectionLevel_Medium"),
            AffectionLevel.High => Get("AffectionLevel_High"),
            AffectionLevel.Max => Get("AffectionLevel_Max"),
            _ => Get("Common_Unknown")
        };
    }

    // 유산 업그레이드 키에 따른 문자열 변환
    public static string GetString(UpgradeKey upgradeKey)
    {
        return upgradeKey switch
        {
            UpgradeKey.StartingGold           => Get("UpgradeKey_StartingGold"),
            UpgradeKey.WeaponRare             => Get("UpgradeKey_WeaponRare"),
            UpgradeKey.WeaponEpic             => Get("UpgradeKey_WeaponEpic"),
            UpgradeKey.StartingInsight        => Get("UpgradeKey_StartingInsight"),
            UpgradeKey.InventorySlots         => Get("UpgradeKey_InventorySlots"),
            UpgradeKey.EnforceRate            => Get("UpgradeKey_EnforceRate"),
            UpgradeKey.EvolveRate             => Get("UpgradeKey_EvolveRate"),
            UpgradeKey.EnforceCost            => Get("UpgradeKey_EnforceCost"),
            UpgradeKey.EvolveCost             => Get("UpgradeKey_EvolveCost"),
            UpgradeKey.MaterialReduction      => Get("UpgradeKey_MaterialReduction"),
            UpgradeKey.RerollCount            => Get("UpgradeKey_RerollCount"),
            UpgradeKey.RerollCost             => Get("UpgradeKey_RerollCost"),
            UpgradeKey.DisassembleBonus       => Get("UpgradeKey_DisassembleBonus"),
            UpgradeKey.CommissionRate         => Get("UpgradeKey_CommissionRate"),
            UpgradeKey.TipRate                => Get("UpgradeKey_TipRate"),
            UpgradeKey.GreatSuccessRate       => Get("UpgradeKey_GreatSuccessRate"),
            UpgradeKey.AdventureSpeed         => Get("UpgradeKey_AdventureSpeed"),
            UpgradeKey.RegularAdventurer      => Get("UpgradeKey_RegularAdventurer"),
            UpgradeKey.MorningEventGuarantee  => Get("UpgradeKey_MorningEventGuarantee"),
            UpgradeKey.NamedSpawnWeight       => Get("UpgradeKey_NamedSpawnWeight"),
            UpgradeKey.ShopRefresh            => Get("UpgradeKey_ShopRefresh"),
            _                                 => Get("Common_Unknown")
        };
    }

    // 평판 점수에 따른 문자열 변환
    public static string GetString(ReputationLevel reputation)
    {
        return reputation switch
        {
            ReputationLevel.Bronze => Get("ReputationLevel_Bronze"),
            ReputationLevel.Silver => Get("ReputationLevel_Silver"),
            ReputationLevel.Gold => Get("ReputationLevel_Gold"),
            ReputationLevel.Platinum => Get("ReputationLevel_Platinum"),
            ReputationLevel.Diamond => Get("ReputationLevel_Diamond"),
            _ => Get("Common_Unknown")
        };
    }

    // 골드 비용 표시 문자열. 할인이 있으면 감소분을 할인색(연노랑)으로 덧붙인다.
    public static string GetGoldCostString(int baseCost, int finalCost)
    {
        int discountAmount = baseCost - finalCost;
        if (discountAmount <= 0) return $"{finalCost:N0}";

        string discountHex = ColorUtility.ToHtmlStringRGB(ColorManager.Instance.GetDiscountColor());
        return $"{finalCost:N0}<color=#{discountHex}>(-{discountAmount:N0})</color>";
    }

    // 총 분(minute)을 "N시간N분" 형식 문자열로 변환 (0인 단위는 생략)
    public static string FormatDuration(int totalMinutes)
    {
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        if (hours <= 0)   return Duration("Common_Duration_M", hours, minutes);
        if (minutes <= 0) return Duration("Common_Duration_H", hours, minutes);
        return Duration("Common_Duration_HM", hours, minutes);
    }

    private static string Duration(string key, int hours, int minutes)
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(
            "UI_Common", key,
            arguments: new object[] { new System.Collections.Generic.Dictionary<string, object>
            {
                { "hours", hours }, { "minutes", minutes }
            } });
    }

    /// <summary>
    /// 주간 의뢰의 요구 문구.
    ///
    /// SO의 <see cref="QuestRequirement.requirementText"/>를 번역하지 않고 여기서 조립한다.
    /// 그 필드는 WeeklyQuestGenerator가 구워 넣은 값이라, 생성기를 다시 돌리면 한국어가
    /// 바뀌면서 번역만 옛 문장으로 남는다. 게다가 문장을 이루는 부품(던전 이름/무기 타입/
    /// 등급)은 이미 전부 번역돼 있어서, 통째로 구운 147개를 또 번역하는 건 같은 문장을
    /// 두 번 관리하는 셈이다. 템플릿 5개로 조립하면 생성기를 다시 돌려도 그대로 맞는다.
    ///
    /// 생성기가 만들지 않는 QuestType(제작/강화/진화 등)은 아직 쓰이는 의뢰가 없다.
    /// 나중에 생기면 구운 원문으로 떨어지므로, 그때 템플릿을 추가한다.
    /// </summary>
    public static string QuestRequirementText(QuestRequirement r)
    {
        if (r == null) return "";

        switch (r.questType)
        {
            case QuestType.SuccessfulAdventures:
                return Req("Quest_ReqSuccess", r.targetCount);
            case QuestType.GreatSuccessCount:
                return Req("Quest_ReqGreat", r.targetCount);
            case QuestType.RentSpecificGrade:
                return Req("Quest_ReqGrade", r.targetCount, "grade", GetString(r.minGrade));
            case QuestType.RentSpecificWeapon:
                return Req("Quest_ReqWeapon", r.targetCount, "weapon", GetString(r.specificWeaponType));
            case QuestType.CompleteSpecificDungeon:
                var dungeon = DataManager.Instance?.GetDungeon(r.specificDungeonID);
                return Req("Quest_ReqDungeon", r.targetCount,
                           "dungeon", dungeon != null ? dungeon.DisplayName : r.specificDungeonID);
            default:
                return r.requirementText;
        }
    }

    private static string Req(string key, int count, string argName = null, string argValue = null)
    {
        var args = new System.Collections.Generic.Dictionary<string, object> { { "count", count } };
        if (argName != null) args[argName] = argValue;
        return LocalizationSettings.StringDatabase.GetLocalizedString(
            "UI_Screens", key, arguments: new object[] { args });
    }
}
