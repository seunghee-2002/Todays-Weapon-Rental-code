# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> 언어 규칙·작업 방식(플랜 승인, Simplicity First, 보고 형식 등) 공통 규칙과 `/git`·`/push` 커맨드는 **유저 레벨 `~/.claude/`** 에 있다. 이 파일은 **이 프로젝트 고유 내용만** 담는다.

## Project Overview

길드 경영 시뮬레이션 게임. 모험가에게 모험을 주선하고 무기를 대여해 수수료를 받으며, 던전 모험 결과를 관리하고 평판을 쌓는다.

- **게임 이름:** `Today's Weapon Rental` / 한글 `오늘도 장비 대여`
- **Unity 2022.3.62f3**, C#, namespace `TodaysWeaponRental`
- All scripts under `Assets/_Projects/Scripts/`
- All ScriptableObject assets under `Assets/_Projects/Data/`
- Two scenes: `MainMenuScene`, `InGameScene`

> **`Magic Rental Shop`은 예전 이름이다.** (`Magic-Rental-Shop`, `MagicRentalShop`, 한글 `오늘의 무기 대여점` 포함)
> 새 코드/문서에 쓰지 말 것. 아래 세 군데에만 의도적으로 남아있다 — 전부 Unity나 외부 도구가 덮어쓰는 값이라 손대도 되돌아온다.
> - 저장소 폴더명 `Magic-Rental-Shop/`
> - `Magic-Rental-Shop.sln` / `.slnx` — Unity가 폴더명 기준으로 재생성
> - `ProjectSettings.asset`의 `projectName` — UGS 클라우드 프로젝트 이름 캐시
> - (빌드 산출물 `Magic-Rental-Shop_BurstDebugInformation_DoNotShip/`, `Tools/BalanceSim/*_log.txt` 등 과거 실행 로그도 동일)

## Development Commands

This is a Unity project — build and run through the Unity Editor. There are no CLI build scripts.

**Running Tests:** Unity Editor → Window > General > Test Runner (uses `com.unity.test-framework`). Test scripts go in `Assets/_Projects/Scripts/Tests/` — **currently empty** (테스트 코드 없음).

**VSCode:** Open `Magic-Rental-Shop.slnx` as the solution (파일명은 예전 이름 - 위 Project Overview 참고). The Unity debugger uses `vstuc` (see `.vscode/launch.json`).

**Editor Tools** (`Scripts/Editor/`, Unity 메뉴 `Tools > Today's Weapon Rental`): CSV Tool(SO↔CSV 동기화), Debug Dashboard, Adventurer/VisitorNPC Preview Window, Adventure Log Exporter 등.

---

## Architecture

### 4-Layer Structure

```
Data         ScriptableObject (*Data), RuntimeInstance (*Instance), SaveData (*SaveData)
Manager      BaseManager<T> singletons — business logic only (Scripts/Systems/, 부트스트랩은 Scripts/Core/)
View         BaseView — display only, no business logic (Scripts/UI/Views/)
Controller   BaseController<TView> — bridges View ↔ Manager (Scripts/UI/Controllers/)
```

**UI 폴더 세부:** `UI/Core`(UIManager, UIControllerManager, BaseView/BaseController, ColorManager, IconManager, OverlayController), `UI/Views`, `UI/Controllers`, `UI/Components`(재사용 위젯), `UI/ListItems`(리스트/카드 아이템).

**Layer call rules:**
- View → Controller (도메인 매니저는 반드시 Controller 경유)
- View → `DataManager` (정적 SO 카탈로그) 직접 호출 허용 — 읽기 전용
- View → `AnalyticsManager` 직접 호출 허용 — 관측 전용(게임 상태 미변경). 컨트롤러가 없는 View(하단바, 아침 이벤트 9종 등)에서 `btn_clicked`를 발행하기 위함
- Controller → Manager + View updates
- Manager → other Managers only (never references View/Controller)

### Key Patterns

**Singleton Managers:** All inherit `BaseManager<T>`, accessed via `T.Instance`.

**Observer:** Managers expose C# `event Action<>` fields; UI and other managers subscribe.
```csharp
TimeManager.Instance.OnTimeChanged += handler;       // (int hour, int minute)
InventoryManager.Instance.OnWeaponAdded += handler;
ReputationManager.Instance.OnReputationChanged += handler;
```

**UI Panel Management:**
```csharp
UIManager.Instance.OpenPanel<WeaponShopView>();
UIManager.Instance.ClosePanel<WeaponShopView>();
UIManager.Instance.GetPanel<InventoryView>();
```

**Partial Classes:** Large managers are split across files by feature area, in a subfolder named after the manager (e.g., `Systems/AdventureManager/` = `AdventureManager.cs` + `.Calculations` + `.EventProcessor` + `.Sequence` + `.Mood`; `Systems/VisitorManager/` = `.cs` + `.Spawn` + `.Npc` + `.Instance`; `Systems/Insight/InsightManager` = `.cs` + `.Reveal` + `.Visibility`).

**Static Data Access:**
- `DataManager`는 **정적 SO 카탈로그**. ID lookup + 카탈로그 필터(`GetWeaponsByType` 등) + 역방향 인덱스만 책임.
- 도메인 의미가 있는 헬퍼는 **도메인 매니저**에 둔다 (예: `GetEnforceMaterialByGrade` → `BlacksmithManager`).
- RuntimeInstance는 정적 SO를 **참조로 직접** 보유하고, **SaveData에서만 ID 문자열로 굽는다**.
- SaveData → RuntimeInstance 변환은 도메인 매니저의 `Initialize(GameData)`에서 수행. RuntimeInstance 생성자는 `DataManager`를 모른다.
- 도메인 헬퍼 매핑 및 상세 규칙은 `Documents/오늘도장비대여/Development/데이터_접근_원칙.md` 참조.

### Core Managers

**Core (`Scripts/Core/`):**

| Class | Role |
|---|---|
| `GameManager` | Lifecycle, `InitializeManagers()` 초기화 순서의 단일 진입점, save/load 트리거. `[DefaultExecutionOrder(-100)]` |
| `DataManager` | Loads all SOs into Dictionaries; ID-based lookup (정적 SO 카탈로그) |
| `TimeManager` | Game time (1 real sec = 3 game min), phases, speed control |

**Domain (`Scripts/Systems/`):**

| Class | Role |
|---|---|
| `InventoryManager` | Weapon/Material/ActiveItem CRUD |
| `EconomyManager` | Gold management, cost calculations |
| `VisitorManager` | NPC visitor spawning and interaction (partial ×4) |
| `AdventureManager` | Adventure creation/progression/events/rewards (partial ×5) |
| `BlacksmithManager` | Enhance/evolve/reroll/disassemble/craft |
| `WeaponShopManager` | 무기 상점 재고/구매 |
| `ActiveItemManager` | 액티브 아이템 배정·소비 (Adventure/Immediate/Blacksmith 귀속) |
| `QuestManager` / `QuestBoardManager` | 주간 퀘스트 / 매일 낮 9시 의뢰판 생성·관리 |
| `ReputationManager` | Reputation tracking, spawn interval calculation |
| `InsightManager` (+`SeerManager`, `ScoutManager`) | 통찰 재화, 정보 공개/가시성 — `Systems/Insight/` |
| `DialogueManager` | 대화 시스템 |
| `MorningEventManager` | 아침 이벤트 |
| `TutorialManager` | 1일차 튜토리얼 (무기 상점 강제 스폰·무료 제공) |
| `LegacyManager` | 유산(영구 업그레이드) 시스템, `PlayerData` 소유. DontDestroyOnLoad — 두 씬 공유 |
| `SoundManager` / `EffectManager` | BGM·SFX (string 키) / 파티클+효과음 통합 재생 |
| `SaveManager` | JSON serialization via `JsonUtility` (static class) |
| `ConfigManager` | Central access point for all Config SOs |

**UGS 연동 (`Scripts/Systems/`, Unity Gaming Services):**

| Class | Role |
|---|---|
| `UGSManager` | UGS 초기화/인증 (Authentication) |
| `CloudSaveManager` / `CloudSyncService` | Cloud Save 업로드·복원 / MainMenuScene 전용 클라우드↔로컬 동기화 (MonoBehaviour) |
| `LeaderboardManager` | 리더보드 |
| `NicknameManager` | 닉네임 (Cloud Code `changeNickname` 검증) |
| `BanManager` | 계정 차단 상태 (source of truth는 서버 Cloud Save) |

### Data Structures

**Game save (한 회차):** `GameData` → `persistentDataPath/gamedata.json` (게임오버 시 삭제)
**Player save (영구):** `PlayerData` → `persistentDataPath/legacy.json` (유산/영구 업그레이드)
**Dictionary serialization:** Uses custom `SerializableDictionary<K,V>` (not `Newtonsoft`).

**Static data** (ScriptableObjects in `Scripts/Data/StaticData/`): `WeaponData`, `AdventurerData`, `DungeonData`, `MaterialData`, `WeaponEffectData`, `BlacksmithData`, `DialogueData`, `WeeklyQuestData`, 던전/방문자 이벤트 Data, 외형 Data 등. 스탯 전용 SO는 `Scripts/Data/StatData/`(`AdventurerStatData`, `DungeonStatData`).

**Runtime instances** (`Scripts/Data/RuntimeInstance/`): `WeaponInstance`, `AdventurerInstance`, `AdventureInstance`, `ActiveItemInstance`, `MaterialInstance`, `WeeklyQuestInstance`, `VisitorNPC` 등.

**Balancing numbers** are all in Config SOs (`Assets/_Projects/Data/Config/`), accessed via `ConfigManager.Instance` (예외: `LegacyConfig`·`NicknameConfig`는 각 매니저가 직접 보유).

### Time System

```
Morning  06:00–09:00  NPC interaction
Daytime  09:00–18:00  Adventurer spawning, adventures progress
Evening  18:00–21:00  Wrap-up
Night    21:00+       Auto-pause → 확인 팝업 → GoToNextDay()
```
`TimeManager.SetTimeScale(float)` controls speed.

---

## Project-Specific Rules

### 밸런싱 작업 기록

밸런싱 수치(Config SO 값, 보상·비용·확률 등)를 바꾸면 **반드시 `/balance` 스킬로 `Documents/오늘도장비대여/Balance/Changelog/`에 변경 건별 기록을 남긴다** — 근거(왜) / 대상 시스템(무엇을) / 수정 전후(어떻게)를 문서화한다. 수치 조정과 **같은 작업 안에서** 처리하고, 포맷·절차는 `/balance` 스킬이 갖고 있다.

조정 **전에는** 대상 시스템에 해당하는 참고 문서(`Documents/오늘도장비대여/Balance/Reference/` — `밸런스_시뮬레이터_정리`의 R 기준 체계, 퀘스트 난이도 계수·레벨 디자인 등)와 과거 변경 기록(`Documents/오늘도장비대여/Balance/Changelog/*.md`)을 먼저 읽는다.

### Before Writing Any Code

전역 규칙(유사 패턴 3개 이상 검색)에 더해 이 프로젝트에서 추가로 확인할 것:

- Prefer adding a method to an existing Manager over creating a new class
- Adding a new SO type → check if `DataManager` load logic needs updating
- Adding new save data → update `GameData` (한 회차) or `PlayerData` (영구) and `SaveDataConverter`

### Style Rules

- **Fields:** `[SerializeField] private` — no public fields, no `_` prefix
- **Naming:** PascalCase for classes/methods/enums, camelCase for fields
- **Regions:**
  ```csharp
  #region 초기화
  #region View로부터 호출되는 메서드
  #region 이벤트 핸들러
  #region 내부 메서드
  ```
- **Logging:** `Debug.Log($"[ClassName] 메시지");`
- **Namespace:** `namespace TodaysWeaponRental` on every script
- **TextMeshProUGUI에 표시되는 문자:** 게임 폰트가 지원하는 유니코드 범위 안의 문자만 사용한다. 지원 범위는 `32–126`(ASCII), `44032–55203`(한글 완성형 가–힣), `12593–12643`(한글 호환 자모 ㄱ–ㅣ)뿐이다.
  - 이 범위 밖 문자는 인게임에서 두부(□)로 깨지므로 코드/문자열 리터럴에 넣지 말 것.
  - 특히 자주 실수하는 문자: em 대시 `—`, en 대시 `–`, 말줄임표 `…`, 화살표 `→ ← ↑ ↓`, 둥근 따옴표 `‘ ’ “ ”`, 가운뎃점 `·`, 불릿 `•`, 곱셈기호 `×`, 이모지 등. 각각 `-`, `...`, `->`, `'`, `"`, 일반 문자로 대체한다.
  - **주의:** 이 규칙은 런타임에 TMP로 렌더링되는 문자열에만 적용된다. 코드 주석·로그·문서(`.md`)에는 제한 없음.
  - **다국어 예외:** 위 범위는 **코드/프리팹의 한국어 원문**에만 적용한다. Localization String Table의 번역 텍스트(en/ja/zh-Hans)는 해당 로케일 폰트 아틀라스가 커버하는 범위의 문자를 허용한다. 단, 릴리즈 전 번역 텍스트 기반으로 폰트 아틀라스를 재생성(Static 재베이크)해야 한다 — `Documents/오늘도장비대여/Development/다국어_도입전략.md` 10절 참고.

### Code Patterns

**Manager:**
```csharp
public class XxxManager : BaseManager<XxxManager>
{
    public event Action<Type> OnSomethingChanged;
    public void Initialize(GameData gameData) { }
    public void SaveToGameData(GameData gameData) { }
}
```

**Controller:**
```csharp
public class XxxController : BaseController<XxxView>
{
    protected override void Awake()
    {
        base.Awake();
    }
    public void OnXxxClicked() { }   // View callback naming: On + 행위
}
```

**View:**
```csharp
public class XxxView : BaseView
{
    public void UpdateDisplay(Data data) { }  // display methods only
}
```

### View Writing Procedure

When creating a new View or adding UI elements to an existing View:

1. **First, list the UI elements to be displayed and request review.**
2. Start coding only after review and approval.
3. Do not add or remove UI elements arbitrarily without approval.

Items to include in the review list:
- Data items to display (text, image, button, etc.)
- Layout structure (tabs, scroll, popup, etc.)
- User input elements (button, toggle, slider, etc.)
- Conditional visibility rules

### Adding New Views/Controllers

When adding a new View or Controller:
1. `UIManager` 인스펙터의 `panelPrefabs` 배열에 새 항목 추가 — `viewTypeName`에 View 클래스명 문자열(예: `"InventoryView"`), `prefab`에 패널 프리팹 지정.
2. Controller 등록은 자동: `BaseController.Awake`에서 `UIControllerManager`에 자동 등록되므로 코드/인스펙터 변경 없음.

### Adding New Features Checklist

1. `BaseManager<T>` → place in `Scripts/Systems/`
2. UI → `BaseView` + `BaseController<TView>` → `Scripts/UI/`
3. New data → add field to `GameData` (한 회차) or `PlayerData` (영구) + verify `SaveManager` compatibility
4. Register initialization in `GameManager.InitializeManagers()`
5. Extract balancing values to a Config SO

### StaticData(SO) ↔ CSV Importer 동기화

**`Scripts/Data/StaticData/` 아래의 SO(필드/타입/추가·삭제)를 건드렸다면, 같은 PR/작업에서 반드시 `Scripts/Editor/CSV*.cs`도 함께 수정한다.**

대상 파일 (모두 짝을 맞춰 유지):
- `CSVExportTab.cs` — `BuildRows_XXX()` 컬럼/필드
- `CSVImportTab.cs` — `Diff_XXX()` 헤더, `Apply_XXX()` 컬럼 수, `Validate` 규칙 배열, 신규 enum이면 `IsValidEnum`도 갱신
- `CSVToolWindow.cs` — 새 SO 타입이면 경로 필드, `TypeNames` 배열, 경로 주입(`InjectPaths`)도 추가
- 보조: `CSVApplierTab.cs`(preview CSV → 원본 CSV 덮어쓰기), `CSVDiffCore.cs`(공유 diff/로그/UI 유틸) — 위 세 파일과 시그니처가 얽히므로 함께 확인

체크리스트:
- 필드 추가/제거 → Export `Hdr(...)`와 Import `Hdr(...)`/`Validate` 규칙 배열의 컬럼 수가 SO와 일치하는지 확인
- enum 필드 추가 → `Validate`의 `"enum:XXX"`와 `IsValidEnum`의 switch에 추가
- 서브 리스트 필드(예: `armorTypeVariants`) 추가 → 서브 CSV(`XxxData_Foo`) Export/Import 메서드 한 쌍 추가
- 새 SO 타입 추가 → `TypeNames`, `AllTargets`, `GetTargets`, 폴더 경로(`SoXxx`), `ComputeDiff` 분기, `ApplyToSO` 분기 모두 추가
- SO 필드명/타입만 바꾸고 CSV 코드를 안 바꾸면 빌드는 통과해도 데이터가 무음으로 깨지므로(특히 컬럼 인덱스가 밀림) **반드시 같이 수정**

Importer 수정 후에는 CSV Tool 윈도우(`Tools > Today's Weapon Rental > CSV Tool`)에서 Export → 기존 CSV와 diff 확인을 한 번 돌려 형식이 맞는지 검증한다.

### UGS 자동화 (Cloud Code · Analytics)

**저장소가 UGS의 단일 원본이다.** 대시보드에서 직접 고치면 자동화가 되돌린다.

| 대상 | 원본 | 반영 |
|---|---|---|
| Cloud Code Scripts | `Tools/CloudCode/*.js` | `main` 푸시 시 GitHub Actions가 자동 배포(프로덕션 즉시 반영) |
| Analytics 이벤트 스키마 | `Tools/Analytics/events.json` | `Tools/Analytics/dashboard_sync.py apply` (Unity가 공개 API를 안 줘서 Playwright로 대시보드 조작) |

- **Analytics 이벤트/파라미터를 코드에서 추가·변경하면 같은 작업에서 `Tools/Analytics/events.json`도 갱신한다.** 미등록 이벤트는 UGS가 invalid로 조용히 버린다. `python Tools/Analytics/analytics.py check`가 코드와 대조하며, CI(`analytics-schema-check`)가 같은 검사를 돌려 어긋나면 실패시킨다. 이후 `render`로 `Documents/오늘도장비대여/Systems/Analytics/대시보드_등록.md`의 생성 섹션을 갱신한다.
- **`NicknameConfig.asset`의 닉네임 규칙을 바꾸면 `Tools/CloudCode/sync_profanity.ps1`을 실행**해 `changeNickname.js`에 반영한다. CI가 `-Check`로 검증하므로 안 하면 배포가 막힌다.
- Cloud Code 변경 전후로 `python Tools/CloudCode/deploy.py diff`로 원격과의 차이를 확인할 수 있다(자격증명 필요, `Tools/CloudCode/.env.example` 참고).

### Post-Work Reporting - 인스펙터 작업

전역 규칙의 보고 항목 2번(**수동 작업**)은 이 프로젝트에서 **Unity 인스펙터 작업**을 뜻한다. 다음을 구체적으로 나열한다:

- New `[SerializeField]` slots to wire up to objects/components
- ScriptableObject field values to set
- UI elements to add/change in prefabs/scenes
- If none: explicitly state "인스펙터 작업 없음"

3번(How to test)도 Unity Editor 또는 런타임에서 확인하는 절차로 적는다.

---

## External Libraries

- **DOTween** — animation tweening
- **TextMesh Pro** — UI text
- **Spine** — 2D skeletal animation
- **Layer Lab 2D Art Maker** — character appearance generation
- **Unity Gaming Services** — Authentication, Cloud Save, Cloud Code, Leaderboards, Analytics (UGS 연동 매니저들이 사용)
- **Cartoon FX Remaster (JMO Assets)** — particle effects

## Documentation Priority

Detailed project reference: `Documents/오늘도장비대여/` (Obsidian Vault). 진입점은 `Home.md`, 아키텍처는 `Development/`, 기능 기획은 `Systems/`, 밸런싱은 `Balance/`.
