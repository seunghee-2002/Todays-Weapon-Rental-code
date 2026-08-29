---
tags:
  - System
  - Analytics
  - Development
aliases:
  - Analytics 구현 메모
  - 이벤트 발행 규칙
created: 2026-04-07
updated: 2026-08-01
---

# Analytics 구현 메모

> [!abstract] 이 문서의 역할
> **코드에서 이벤트를 발행할 때의 규칙.** 무엇을 수집할지는 [[분석목표]] · [[이벤트_스펙]] · [[버튼_이벤트]]에 있고, 여기서는 "어디에, 어떻게 넣는가"만 다룬다.

## 레벨별 발행 위치

| Level | 발행 주체 | 원칙 |
|---|---|---|
| **1** | 각 Manager | 상태 변화 지점 |
| **2** | `UIManager` **중앙 처리** | 화이트리스트 기반, Controller/View가 직접 부르지 않는다 |
| **3** | 각 Controller의 `On*Clicked()` 첫 줄 | 컨트롤러가 없으면 View에 직접 |
| **4** | 각 Manager의 **실행 완료 지점** | 성공한 결과만 |

> [!important] 계층 규칙을 어기지 않는다
> `AnalyticsManager`가 Controller/View를 참조하면 Manager -> Manager 규칙 위반이다. 따라서 `blacksmith_type`·`delay_real_min` 같은 추가 파라미터는 **`UIManager`가 읽어서** `SendPanelOpened(panel, extra)`의 `extra`로 넘긴다. -> [[Development]]

---

## Level 2 — `UIManager` 중앙 처리

- **화이트리스트** — `UIManager.analyticsPanelNames`(Type -> panel 문자열)에 있는 View만 발행한다. 등록 51개 패널 중 **17개만 대상**이며, 나머지(`TutorialHighlightView` 등)는 무시되므로 이벤트 볼륨이 튀지 않는다.
- **재오픈 가드** — `OpenPanel<T>()`의 `openPanels.Remove(panel)` 반환값으로 "이미 열려 있었는지"를 판정해 재오픈 중복 발행을 막는다. 발행은 **메서드 맨 끝** — `beforeOpen` 콜백과 컨트롤러 세팅이 끝난 뒤에 추가 파라미터를 읽어야 하기 때문이다.
- **닫기 4경로 일원화** — 아래 넷이 모두 `SendPanelClosedAnalytics()`를 통과한다. `ClosePanel<T>()`은 이미 닫힌 패널에 대한 중복 발행도 막는다.
	- `ClosePanel<T>()` — 닫기 버튼
	- `CloseTopPanel()` — ESC · 오버레이 클릭
	- `CloseAllPanels()` — 튜토리얼 스킵
	- `CleanupInstantiatedPanels()` — 씬 전환
- **`duration_sec`** — `AnalyticsManager.panelOpenRealtime`(panel 문자열 -> `Time.realtimeSinceStartup`)에 오픈 시각을 담아두고 `panel_closed`에서 차이를 계산한다. **오픈 기록이 없으면 파라미터를 생략한다**(탭 가상 패널 등).

---

## Level 3 — 버튼 이벤트

> [!important] 삽입 위치 규칙
> 자원 부족·조건 미충족 **조기 return 앞**에 둔다. G3가 재는 것은 "눌렀는가"이지 "성공했는가"가 아니다. 성패는 Level 4 도메인 이벤트가 담당한다.
> **예외** — 파라미터가 null 참조를 요구하면 그 null 가드 뒤로 내린다.

- 컨트롤러가 없는 곳은 **View에 직접** 넣는다 — 하단바 `BottomBarView`, 인벤토리 탭·카드 클릭, 아침 이벤트 9종.
- 아침 이벤트만 `MorningEventViewBase.SendButtonClick()` 헬퍼를 쓴다(`event_type` 자동 첨부).
- 값이 없을 수 있는 파라미터는 **딕셔너리에 아예 넣지 않는다**(UGS 조건부 파라미터). Event Manager에서 **required 설정 금지.**

---

## 목표별 구현 노트

### G3 — `is_first_time`

`AnalyticsManager`에서 `HashSet<string>`(키: `{panel}_{button}`)을 `PlayerPrefs`에 JSON으로 저장한다. 버튼 클릭 시 Set에 없으면 `is_first_time = true`로 전송한 뒤 Set에 추가한다.

> 재검증할 때는 `Edit > Clear All PlayerPrefs`.

### G1 — `dialog_open_duration_sec`

`adventure_dialog` 패널 오픈 시 `AnalyticsManager.RecordDialogOpenTime()`을 호출해 `Time.realtimeSinceStartup`을 저장하고, `adventure_started` 발행 시 경과초를 계산한다.

> 세션 내에서만 쓰이므로 `realtimeSinceStartup`으로 충분하다.

### G26 — `delay_real_min`

`AdventureInstance.completedAtUtcTicks`(모험 완료 시 `DateTime.UtcNow.Ticks` 기록, **세이브 포함**)와 현재 시각의 차이를 분으로 환산한다. 결과 패널 오픈 시 `UIManager`가 `AdventureResultController.CurrentAdventure`에서 읽는다.

> [!danger] `Time.realtimeSinceStartup`을 쓰지 않는 이유
> 앱 재시작 시 리셋되므로 "앱 끄고 나중에 결과 확인" 패턴이 통째로 누락된다. G26은 **앱 재시작을 넘어** 측정해야 한다.

### G23 — `blacksmith_type`

`BlacksmithManager.Instance.CurrentBlacksmith.type`을 `BlacksmithManager.GetTypeAnalyticsName()`으로 변환해 `panel_opened(blacksmith)`의 extra로 전달한다.

### G21 — 재시도 전환율

클라이언트 집계가 **불필요**하다. 대시보드에서 `blacksmith_enforce_done(success=false)` 이후 동일 `session_id` 내 `btn_clicked(blacksmith, enforce_retry)` 발생 여부를 세션 단위 퍼널로 분석한다. -> [[퍼널]]

---

## 수집 게이트

### 동의 연동

`AnalyticsManager.StartCollection()`은 `HasAskedConsent`(동의 팝업 응답 여부)와 `IsOptedOut`(철회 여부) 가드를 통과해야 실제로 수집을 시작한다. 호출 경로는 두 곳이며 서로 타이밍을 보완하므로 어느 쪽이 먼저 와도 안전하다.

1. 최초 실행 약관 팝업 — `TermsAgreementController` -> `SetConsent`
2. 앱 재실행 시 UGS 초기화 직후 — `UGSManager.Start`

옵션의 수집 철회 토글은 `SetOptOut`으로 즉시 중단/재개한다.

### 에디터 전송 차단

`Application.isEditor`면 전송하지 않고 `Debug.Log`로만 출력한다(실지표 오염 방지). 수신 검증은 반드시 빌드에서 Event Browser로 수행한다. -> [[대시보드_등록]]

---

## 유지보수 규칙

> [!warning] 코드와 `events.json` 은 항상 같이 고친다
> 이벤트/파라미터를 추가·변경하면 **반드시** `Tools/Analytics/events.json` 을 같은 작업에서 갱신한다. 미등록 이벤트는 invalid 처리로 조용히 버려지므로, 코드만 고치면 데이터가 안 쌓이는 것을 한참 뒤에 알게 된다.
> 변경 후에는 Event Browser에서 **Invalid 0건**을 확인한다.

### CI가 막아준다

`.github/workflows/analytics-schema-check.yml` 이 매 푸시마다 `analytics.py check` 를 돌린다.

- **코드에만 있는 이벤트/파라미터** -> 빌드 실패 (UGS에서 버려질 것이므로)
- **`events.json` 에만 있는 항목** -> 경고 (등록만 남고 무해)
- **정적으로 못 읽은 호출 지점** -> `UNRESOLVED` 로 나열. 조용히 빠지지 않는다

스캐너는 `Send("이름", ...)` 와 `SendPanelOpened`/`SendPanelClosed`/`SendButtonClick` 호출을 읽고, 마지막 인자가 변수면 같은 메서드 안에서 그 딕셔너리를 역추적한다. 파라미터 딕셔너리를 **다른 메서드에서 만들어 넘기면** 추적이 끊겨 `UNRESOLVED` 로 잡히니, 호출 지점과 같은 메서드에서 구성한다.

검사 통과 후에는 `analytics.py render` 로 [[대시보드_등록]] 목록을 갱신하고 `dashboard_sync.py apply` 로 대시보드에 반영한다.

---

## 관련 코드

| 영역 | 위치 |
|---|---|
| 이벤트 발행 허브 | `Assets/_Projects/Scripts/Systems/AnalyticsManager.cs` |
| Level 2 패널 이벤트 · 화이트리스트 | `Assets/_Projects/Scripts/UI/Core/UIManager.cs` |
| 골드 source 정규화 | `Assets/_Projects/Scripts/Systems/EconomyManager.cs` (`GetSourceAnalyticsName`) |
| 대장장이 타입 표기 | `Assets/_Projects/Scripts/Systems/BlacksmithManager.cs` (`GetTypeAnalyticsName`) |
| 아침 이벤트 유형 표기 | `Assets/_Projects/Scripts/Systems/MorningEventManager.cs` |
| 시간대 표기 | `Assets/_Projects/Scripts/Core/TimeManager.cs` |
| 모험 이벤트 발행 | `Assets/_Projects/Scripts/Systems/AdventureManager/AdventureManager.Calculations.cs` |
| 모험 완료 시각 기록 | `Assets/_Projects/Scripts/Data/RuntimeInstance/AdventureInstance.cs` (`completedAtUtcTicks`) |
| 아침 이벤트 버튼 헬퍼 | `Assets/_Projects/Scripts/UI/Views/MorningEvent/MorningEventViewBase.cs` |
| UGS 초기화·동의 | `Assets/_Projects/Scripts/Systems/UGSManager.cs` |
| 에러 리포팅(`client_error`) | `Assets/_Projects/Scripts/Core/LogPolicy.cs` |
| 스키마 원본 | `Tools/Analytics/events.json` |
| 드리프트 검사·문서 생성 | `Tools/Analytics/analytics.py` |
| 대시보드 반영 | `Tools/Analytics/dashboard_sync.py` |

---

## Related

- [[Analytics]] — Analytics 허브
- [[이벤트_스펙]] · [[버튼_이벤트]] — 무엇을 발행하는가
- [[대시보드_등록]] — 스키마 등록·수신 검증
- [[Development]] — Manager/View/Controller 계층 규칙
- [[저장]] — `completedAtUtcTicks` 등 세이브에 포함되는 분석용 필드
