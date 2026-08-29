using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// ScriptableObject에 들어 있는 표시 텍스트(무기 이름, 던전 설명, 퀘스트 제목 등)를
    /// 현재 언어로 바꿔주는 헬퍼. Phase 5 전용이며 화면 라벨(UI_Screens 등)과는 무관하다.
    ///
    /// 한국어 테이블을 만들지 않는 것이 이 설계의 핵심이다 —
    /// SO에 적힌 한국어 원문이 곧 ko 값이므로, 같은 문장을 SO와 테이블에 이중 관리하지 않는다.
    /// 그래서 조회는 항상 "키가 있으면 번역, 없으면 SO 원문"으로 떨어진다.
    ///
    /// 키 규칙: {종류}_{StaticID}_{필드}   예) Weapon_WEA_SWD_COM_001_Name
    /// 리스트 항목은 뒤에 인덱스를 붙인다.  예) Config_Seer_greetingLines_0
    /// </summary>
    public static class DataLocalizer
    {
        public const string TableName = "Data";

        /// <summary>
        /// 키를 현재 언어로 조회한다. 한국어이거나 엔트리가 없으면 <paramref name="korean"/>을 그대로 돌려준다.
        /// </summary>
        public static string Get(string key, string korean)
        {
            if (string.IsNullOrEmpty(key)) return korean;

            // ko는 SO 원문이 곧 정답이라 테이블을 타지 않는다. 초기화 전에도 여기서 끝난다.
            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null || locale.Identifier.Code == "ko-KR") return korean;

            var table = LocalizationSettings.StringDatabase.GetTable(TableName);
            var entry = table?.GetEntry(key);
            if (entry == null || string.IsNullOrEmpty(entry.LocalizedValue)) return korean;

            return entry.LocalizedValue;
        }

        /// <summary>
        /// SO 원문 없이 **테이블에서만** 값을 얻는다. ko-KR도 테이블(번역가 참고용 ko 사본)을 탄다.
        /// SO 풀이 비어 폴백이 필요한 자리에서만 쓴다 — 그때는 참조할 SO 원문 자체가 없기 때문이다.
        /// 덕분에 폴백 문구를 별도 키로 이중 관리하지 않고 SO 대사 하나를 원본으로 쓸 수 있다.
        /// </summary>
        public static string FromTable(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";

            var table = LocalizationSettings.StringDatabase.GetTable(TableName);
            var entry = table?.GetEntry(key);
            // SO 대사는 줄바꿈을 두 글자 \n으로 담으므로 Get 경로와 같게 풀어 준다.
            return entry == null ? "" : entry.LocalizedValue.Replace("\\n", "\n");
        }

        /// <summary>`{종류}_{StaticID}_{필드}` 형태의 키를 만든다.</summary>
        public static string Key(string type, string staticId, string field)
            => $"{type}_{staticId}_{field}";

        /// <summary>리스트 필드용 — `{종류}_{이름}_{필드}_{인덱스}`.</summary>
        public static string Key(string type, string staticId, string field, int index)
            => $"{type}_{staticId}_{field}_{index}";

        /// <summary>대화 노드 안의 값 — `{종류}_{StaticID}_Node{n}_{필드}`.</summary>
        public static string NodeKey(string type, string staticId, int node, string field)
            => $"{type}_{staticId}_Node{node}_{field}";

        // ── 종류별 진입점 ────────────────────────────────
        // 콜사이트가 키 문자열을 직접 조립하지 않도록 여기서 한 번에 막는다.

        public static string WeaponName(WeaponData d)
            => d == null ? "" : Get(Key("Weapon", d.StaticID, "Name"), d.weaponName);

        public static string WeaponDescription(WeaponData d)
            => d == null ? "" : Get(Key("Weapon", d.StaticID, "Desc"), d.description);

        public static string DungeonName(DungeonData d)
            => d == null ? "" : Get(Key("Dungeon", d.StaticID, "Name"), d.dungeonName);

        public static string MaterialName(MaterialData d)
            => d == null ? "" : Get(Key("Material", d.StaticID, "Name"), d.materialName);

        public static string MaterialDescription(MaterialData d)
            => d == null ? "" : Get(Key("Material", d.StaticID, "Desc"), d.description);

        public static string ActiveItemName(ActiveItemData d)
            => d == null ? "" : Get(Key("ActiveItem", d.StaticID, "Name"), d.itemName);

        public static string ActiveItemDescription(ActiveItemData d)
            => d == null ? "" : Get(Key("ActiveItem", d.StaticID, "Desc"), d.description);

        /// <summary>아침 이벤트 이름. description은 에디터 메모라 번역하지 않는다.</summary>
        public static string VisitorEventName(VisitorEventData d)
            => d == null ? "" : Get(Key("VisitorEvent", d.StaticID, "Name"), d.eventName);

        /// <summary>던전 이벤트 설명(전투면 몬스터 이름). summary는 Animator 컨트롤러명이라 번역하지 않는다.</summary>
        public static string DungeonEventDescription(DungeonEventData d)
            => d == null ? "" : Get(Key("DungeonEvent", d.StaticID, "Desc"), d.description);

        /// <summary>
        /// 주간 의뢰 제목. 요구 문구(requirementText)와 설명(description)은 생성기가 구운 값이라
        /// 여기서 다루지 않는다 - 요구 문구는 <see cref="UITranslator.QuestRequirementText"/>가 조립한다.
        /// </summary>
        public static string QuestTitle(WeeklyQuestData d)
            => d == null ? "" : Get(Key("Quest", d.StaticID, "Title"), d.questTitle);

        // ── 대화 ──────────────────────────────────────────
        // 대사/화자/선택지는 노드 안에 있어서 한 자산에 같은 필드가 여러 번 나온다.
        // 그래서 키에 **실제 노드/선택지 인덱스**를 넣는다. 등장 순서로 번호를 매기면
        // 한국어가 없는 노드가 빠지면서 인덱스가 밀려 다른 노드의 대사가 나온다.

        public static string DialogueText(DialogueData d, int node, string korean)
            => d == null ? korean : Get(NodeKey("Dialogue", d.StaticID, node, "Text"), korean);

        public static string DialogueSpeaker(DialogueData d, int node, string korean)
            => d == null ? korean : Get(NodeKey("Dialogue", d.StaticID, node, "Speaker"), korean);

        /// <summary>노드에 화자 이름이 비어 있을 때 쓰는 대화 기본 화자.</summary>
        public static string DialogueDefaultSpeaker(DialogueData d)
            => d == null ? "" : Get(Key("Dialogue", d.StaticID, "DefaultSpeaker"), d.defaultSpeakerName);

        public static string DialogueChoice(DialogueData d, int node, int choice, string korean)
            => d == null ? korean : Get(NodeKey("Dialogue", d.StaticID, node, $"Choice{choice}"), korean);

        // ── Config SO ─────────────────────────────────────
        // Config SO는 BaseData가 아니라 StaticID가 없다. 대신 자산 이름을 줄인 것을 쓰고,
        // 그 뒤에 SO 루트부터의 필드 경로를 붙인다.
        //
        //     Config_{자산}_{경로}      예) Config_Seer_descGreat_bad_3
        //
        // 경로는 `Audit-SoText.ps1`의 `Read-ConfigRows`가 만드는 것과 **글자 그대로 같아야**
        // 한다 - `so-ledger.csv`의 Index 칸이 곧 이 경로다.
        //
        // 구조체 리스트는 위치가 아니라 **그 구조체가 이미 갖고 있는 식별자**로 주소를 만든다
        // (TutorialLineKey · BlacksmithData.StaticID · UpgradeKey · BonusType). 인스펙터에서
        // 순서를 바꿔도 안 깨지게 하기 위해서다.
        //
        // 필드 이름은 반드시 `nameof`로 넘긴다. 그래야 필드명을 바꿨을 때 키가 조용히 깨지는
        // 대신 컴파일이 깨진다.

        /// <summary>
        /// SeerConfig의 대사 풀 한 줄 - `Config_Seer_{경로}_{인덱스}`.
        /// 경로는 SO 직속 리스트면 필드 이름(`greetingLines`), 수정구 서사면
        /// `{추상단계필드}_{운세필드}`(`descGreat_bad`)다.
        /// </summary>
        public static string SeerLine(string path, int index, string korean)
            => Get(SeerLineKey(path, index), korean);

        /// <summary>
        /// 위 키만 만든다. 세이브에 구워지는 값(수정구 서사)은 **문장이 아니라 이 키**를
        /// 저장해야 언어를 바꿨을 때 이미 본 결과도 같이 바뀐다.
        /// </summary>
        public static string SeerLineKey(string path, int index)
            => $"Config_Seer_{path}_{index}";

        /// <summary>WeaponShopConfig의 상인 인사말 - `Config_WeaponShop_merchantGreetings{구간}_lines_{인덱스}`.</summary>
        public static string MerchantGreeting(int tier, int index, string korean)
            => Get(MerchantGreetingKey(tier, index), korean);

        /// <summary>위 키만 만든다. 풀이 비었을 때 FromTable로 SO 대사를 직접 참조하는 데 쓴다.</summary>
        public static string MerchantGreetingKey(int tier, int index)
            => $"Config_WeaponShop_merchantGreetings{tier}_{nameof(MerchantGreetingTier.lines)}_{index}";

        /// <summary>AdventureInfoConfig의 보너스 칩 이름 - `Config_AdventureInfo_{BonusType}_displayName`.</summary>
        public static string BonusVisualName(BonusVisualInfo d)
            => d == null ? "" : Get($"Config_AdventureInfo_{d.bonusType}_{nameof(d.displayName)}", d.displayName);

        /// <summary>
        /// LegacyConfig의 업그레이드 효과 설명 템플릿 - `Config_Legacy_{UpgradeKey}_description`.
        /// `{current}`·`{value}` 치환은 **이 뒤에** 해야 한다.
        /// </summary>
        public static string LegacyUpgradeDescription(LegacyUpgradeDefinition d)
            => d == null ? "" : Get($"Config_Legacy_{d.key}_{nameof(d.description)}", d.description);

        /// <summary>
        /// PriceTierConfig의 시세 계단 공지 대사 묶음을 통째로 옮긴다 -
        /// `Config_PriceTier_stepNotices{계단}_{noticeLines|todayLines}_{인덱스}`.
        /// 대화창은 묶음 단위로 받으므로 새 리스트를 만들어 돌려준다 (TutorialConfig와 달리
        /// 원본 배열의 참조를 유지할 필요가 없다 — 초상이 따로 넘어가기 때문).
        /// </summary>
        public static List<string> PriceTierNoticeLines(int step, bool isToday, IList<string> korean)
        {
            if (korean == null) return null;
            if (step < 0) return new List<string>(korean);

            string field = isToday ? nameof(StepNoticeLines.todayLines) : nameof(StepNoticeLines.noticeLines);
            var list = new List<string>(korean.Count);
            for (int i = 0; i < korean.Count; i++)
                list.Add(Get($"Config_PriceTier_stepNotices{step}_{field}_{i}", korean[i]));
            return list;
        }

        /// <summary>BlacksmithConfig의 대장장이 이름 - `Config_Blacksmith_{StaticID}_blacksmithName`.</summary>
        public static string BlacksmithName(BlacksmithData d)
            => d == null ? "" : Get($"Config_Blacksmith_{d.StaticID}_{nameof(d.blacksmithName)}", d.blacksmithName);

        /// <summary>요청 성공 인사말(주차 구간별) - `Config_Blacksmith_{StaticID}_greetingsOnSuccess_{구간}`.</summary>
        public static string BlacksmithGreetingSuccess(BlacksmithData d, int tier, string korean)
            => d == null ? korean : Get($"Config_Blacksmith_{d.StaticID}_{nameof(d.greetingsOnSuccess)}_{tier}", korean);

        /// <summary>요청 실패 인사말 - `Config_Blacksmith_{StaticID}_greetingOnFailure`.</summary>
        public static string BlacksmithGreetingFailure(BlacksmithData d)
            => d == null ? "" : Get($"Config_Blacksmith_{d.StaticID}_{nameof(d.greetingOnFailure)}", d.greetingOnFailure);

        /// <summary>TutorialConfig의 전령 대사 한 박스 - `Config_Tutorial_{TutorialLineKey}_lines_{인덱스}`.</summary>
        public static string TutorialLine(TutorialLineKey key, int index, string korean)
            => Get($"Config_Tutorial_{key}_{nameof(TutorialConfig.StepLines.lines)}_{index}", korean);

        /// <summary>
        /// 지금 조회에 쓰이는 로케일 코드. 번역 사본을 캐시해 두는 쪽이 **언제 다시 만들어야 하는지**
        /// 알기 위해 쓴다 (TutorialConfig가 초상 역방향 조회 때문에 사본을 재사용해야 한다).
        /// </summary>
        public static string CurrentLocaleCode
            => LocalizationSettings.SelectedLocale?.Identifier.Code ?? "";
    }
}
