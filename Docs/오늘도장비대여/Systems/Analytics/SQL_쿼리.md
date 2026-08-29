---
tags:
  - System
  - Analytics
  - Reference
aliases:
  - SQL 쿼리
  - Data Explorer
  - 쿼리 템플릿
created: 2026-07-27
updated: 2026-08-01
---

# SQL 쿼리 템플릿

> [!abstract] 한 줄 요약
> UGS 대시보드 -> Analytics -> **SQL Data Explorer**에 붙여넣어 실행하는 분석 쿼리 모음.
> [[분석목표]]의 G1~G27에 **1:1 대응**한다. 결과는 차트로 그려 Custom Dashboard에 고정하거나 Share 버튼으로 CSV 내보내기가 가능하다.

## 목차

| 분류 | 쿼리 |
|---|---|
| [모험](#모험) | G1 · G7 · G9 · G11 · G12 · G13 · G17 · G19 · G22 · G26 |
| [대장장이](#대장장이) | G4 · G21 · G23 |
| [퀘스트 & 이벤트](#퀘스트--이벤트) | G5 · G6 · G16 · G25 |
| [경제 & 성장](#경제--성장) | G8 · G10 · G20 |
| [게임 흐름 & 유산](#게임-흐름--유산) | G2 · G14 · G15 · G18 · G27 |
| [UI & UX](#ui--ux) | G3 · G24 |
| [패널 / 버튼 이벤트 보조 쿼리](#패널--버튼-이벤트-보조-쿼리) | 범용 |

> [!tip] 각 쿼리는 네 항목으로 읽는다
> **재는 것** (무엇을 세는가) / **읽는 법** (컬럼 의미) / **해석** (어떤 값이 무엇을 뜻하는가) / **주의** (오독 함정).

## 기본 사용법

- 모든 이벤트는 `EVENTS` 테이블 한 곳에 쌓인다 (Snowflake SQL 문법).
- 주요 기본 컬럼: `EVENT_NAME`, `EVENT_DATE`, `EVENT_TIMESTAMP`, `USER_ID`, `SESSION_ID`
- 커스텀 파라미터는 `EVENT_JSON` 컬럼에서 꺼낸다: `EVENT_JSON:파라미터명::타입`
  - 예: `EVENT_JSON:outcome::string`, `EVENT_JSON:day::int`, `EVENT_JSON:seer_used::boolean`
- 기간 필터는 `WHERE EVENT_DATE >= '2026-08-01'` 식으로 추가 (아래 쿼리에는 생략 — 필요 시 붙일 것)
- 계정/버전에 따라 스키마가 다를 수 있으니, 처음 한 번은 아래 "스키마 확인"으로 실제 컬럼명을 확인할 것

### 스키마 확인 / 수신 확인

```sql
-- 최근 이벤트 20건 훑어보기 (컬럼 구조 확인용)
SELECT EVENT_NAME, EVENT_TIMESTAMP, USER_ID, EVENT_JSON
FROM EVENTS
ORDER BY EVENT_TIMESTAMP DESC
LIMIT 20;
```

```sql
-- 이벤트별 일일 수신량 (수집이 정상 동작하는지 점검)
SELECT EVENT_DATE, EVENT_NAME, COUNT(*) AS cnt
FROM EVENTS
GROUP BY 1, 2
ORDER BY 1 DESC, 3 DESC;
```

---

## 모험

### G1 — 모험 UI 복잡도 (대화 열림 → 출발까지 소요 초)

- **재는 것** — 방문자 대화창이 열린 순간부터 모험이 출발할 때까지 흐른 **실시간** 초. 인게임 시간이 아니라 유저가 화면 앞에서 실제로 보낸 시간이다. 모험 준비 UI(대화 → 준비 → 무기 선택 → 던전 선택 → 점술가)를 통과하는 데 드는 인지 비용의 대리 지표.
- **읽는 법** — `median_sec`이 전형적인 유저의 소요 시간이고, `avg_sec`은 오래 헤맨 소수에 끌려가므로 참고용이다. `p90_sec`은 느린 쪽 상위 10% 경계. `samples`는 이 값이 기록된 모험 수.
- **해석** — 중앙값 자체가 길면 준비 단계가 많아 UI가 무거운 것. 중앙값은 짧은데 `p90_sec`이 중앙값의 3배 이상으로 벌어지면 "대부분은 금방 끝내는데 일부가 크게 헤맨다"는 뜻으로, 평균만 봐서는 안 보이는 혼란 구간이 있다는 신호다.
- **함께 볼 것** — G13(무기 직접 선택률)이 낮으면서 소요 초도 짧으면 "귀찮아서 기본 무기로 스킵"하는 패턴, 소요 초가 길면서 선택률이 낮으면 "고민하다 포기"하는 패턴이다.
- **주의** — `dialog_open_duration_sec`은 조건부 파라미터라 `IS NOT NULL` 필터가 들어 있다. `samples`가 전체 `adventure_started` 건수보다 크게 적으면 이 값이 기록되지 않는 진입 경로가 있다는 뜻이므로 코드 확인이 먼저다.

```sql
SELECT
    AVG(EVENT_JSON:dialog_open_duration_sec::int)    AS avg_sec,
    MEDIAN(EVENT_JSON:dialog_open_duration_sec::int) AS median_sec,
    PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY EVENT_JSON:dialog_open_duration_sec::int) AS p90_sec,
    COUNT(*) AS samples
FROM EVENTS
WHERE EVENT_NAME = 'adventure_started'
  AND EVENT_JSON:dialog_open_duration_sec IS NOT NULL;
```

### G7 — 던전 x 무기 등급별 성공률

- **재는 것** — 던전마다, 들고 간 무기 등급마다 `great_success` + `success` 비율. 밸런스 시뮬레이터의 예측 성공률과 실측을 대조하는 것이 이 쿼리의 본래 목적이다.
- **읽는 법** — `weapon_grade`는 0=Common ~ 4=Legendary. `success_rate`는 0~1 비율(0.62 = 62%). `total`은 해당 조합의 표본 수.
- **해석** — 한 던전 안에서 `weapon_grade`가 오를수록 `success_rate`가 단조 증가해야 정상이다. 증가가 없으면 그 던전에서는 무기 등급이 결과에 영향을 주지 못한다는 뜻(모험가 스탯 임계값이 지배). 반대로 등급 사이 격차가 지나치게 가파르면 등급 벽이 강해 저등급 무기만 가진 유저가 막힌다.
- **주의** — 던전 x 등급 조합은 셀이 잘게 쪼개져 표본이 금방 부족해진다. `total`이 10 미만인 행은 비율을 읽지 말 것.

```sql
SELECT
    EVENT_JSON:dungeon_id::string AS dungeon,
    EVENT_JSON:weapon_grade::int  AS weapon_grade,
    COUNT(*) AS total,
    AVG(CASE WHEN EVENT_JSON:outcome::string IN ('success','great_success') THEN 1.0 ELSE 0.0 END) AS success_rate
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1, 2
ORDER BY 1, 2;
```

### G9 — 방문자 스폰 대비 모험 전환율

- **재는 것** — 스폰된 방문자 수 대비 실제로 출발한 모험 수. "찾아온 손님을 얼마나 놓치는가"를 나타낸다. 방문자는 곧 대여 수익 기회이므로 이 비율이 낮으면 수익 기회 자체가 증발하는 것.
- **읽는 법** — `conversion_rate`가 1이면 스폰된 방문자를 전부 모험으로 연결한 것, 0.5면 절반을 흘려보낸 것이다.
- **해석** — 낮을 때 원인 후보는 세 가지다. (a) 방문자가 화면에서 눈에 안 띔 (b) 보낼 무기나 골드가 없어 못 보냄 (c) 시간대가 지나가 사라짐. 이 쿼리 하나로는 셋이 구분되지 않으므로, G15(일차별 모험 횟수)·G10(골드 흐름)과 같이 봐서 자원 부족인지 시인성 문제인지 좁혀야 한다.
- **정밀 버전** — `panel_opened(adventure_dialog)`가 구현돼 있으므로 "아예 클릭도 안 함"과 "클릭은 했지만 출발 안 함"을 분리할 수 있다. 아래 두 번째 쿼리를 쓸 것. 일차별 추이를 보고 싶으면 각 CTE에 `EVENT_JSON:day::int`를 넣고 `day` 기준으로 조인하면 된다.

```sql
-- 스폰 -> 출발 전환율 (전 구간 합산)
WITH spawned AS (
    SELECT COUNT(*) AS cnt FROM EVENTS WHERE EVENT_NAME = 'visitor_spawned'
), started AS (
    SELECT COUNT(*) AS cnt FROM EVENTS WHERE EVENT_NAME = 'adventure_started'
)
SELECT spawned.cnt AS visitors, started.cnt AS adventures,
       started.cnt / NULLIF(spawned.cnt, 0) AS conversion_rate
FROM spawned, started;
```

```sql
-- 정밀 버전: 스폰 -> 대화 -> 출발 2단계 분리
WITH spawned AS (
    SELECT COUNT(*) AS cnt FROM EVENTS WHERE EVENT_NAME = 'visitor_spawned'
), dialogs AS (
    SELECT COUNT(*) AS cnt FROM EVENTS
    WHERE EVENT_NAME = 'panel_opened' AND EVENT_JSON:panel::string = 'adventure_dialog'
), started AS (
    SELECT COUNT(*) AS cnt FROM EVENTS WHERE EVENT_NAME = 'adventure_started'
)
SELECT spawned.cnt AS visitors,
       dialogs.cnt AS dialogs_opened,
       started.cnt AS adventures,
       dialogs.cnt / NULLIF(spawned.cnt, 0) AS click_rate,
       started.cnt / NULLIF(dialogs.cnt, 0) AS start_rate
FROM spawned, dialogs, started;
```

`click_rate`가 낮으면 방문자 시인성/클릭 영역 문제, `start_rate`가 낮으면 준비 UI 이탈(G1과 교차).

### G11 — 점술가 사용 여부 x 성공률

- **재는 것** — 출발 전 점술가 상담을 한 모험과 안 한 모험의 성공률 차이. 점술가에 지불한 골드가 실제 결과 개선으로 돌아오는지 검증하는 지표다.
- **읽는 법** — 행이 두 개(`true`/`false`) 나온다. `total`은 각 그룹의 모험 수, `success_rate`는 그 그룹의 성공 비율. `true` 행의 `total` 비중이 곧 점술가 사용률이다.
- **해석** — `true` 쪽 성공률이 뚜렷하게 높아야 점술가가 제 값을 한다. 차이가 없으면 (1) 점술가가 주는 정보가 실제 판단에 반영되지 않거나 (2) 이미 알고 있는 정보를 유료로 파는 중이라는 뜻 → 비용 인하 또는 제공 정보 개편.
- **주의** — 사용률이 극단적으로 낮으면(전체의 5% 미만) 성공률 비교보다 "기능 발견율" 문제가 먼저다. 그리고 잘하는 유저가 점술가도 더 쓰는 **선택 편향**이 있으므로, 차이가 크게 나와도 "점술가 덕분"이라고 단정하지 말 것 — 이 쿼리는 인과가 아니라 상관을 본다.

```sql
SELECT
    EVENT_JSON:seer_used::boolean AS seer_used,
    COUNT(*) AS total,
    AVG(CASE WHEN EVENT_JSON:outcome::string IN ('success','great_success') THEN 1.0 ELSE 0.0 END) AS success_rate
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1;
```

### G12 — 통찰(스탯 공개 수) x 성공률

- **재는 것** — 출발 시점에 공개돼 있던 모험가 스탯 개수(0~4)별 성공률, 그리고 특성 공개 여부별 성공률. 통찰에 인게임 시간과 유산을 투자할 가치가 실제로 있는지 검증한다.
- **읽는 법** — 첫 쿼리는 `revealed_stats` 0~4의 5개 행, 둘째는 `trait_revealed` `true`/`false` 두 행. 각각 `total`(표본)과 `success_rate`.
- **해석** — `revealed_stats`가 늘수록 `success_rate`가 우상향해야 통찰 시스템이 설계대로 작동하는 것이다. 평평하다면 정보가 판단으로 이어지지 않는다는 뜻 — 공개된 스탯이 던전 선택 화면에서 잘 안 보이거나, 애초에 스탯이 성공률에 미치는 영향이 작거나 둘 중 하나다.
- **주의** — `revealed_stats = 0` 행에 표본 대부분이 몰려 있다면 그 자체가 결론이다(통찰 기능 미사용). 성공률 비교로 넘어가기 전에 분포부터 볼 것. G11과 마찬가지로 선택 편향이 섞여 있다.

```sql
SELECT
    EVENT_JSON:revealed_stat_count::int AS revealed_stats,
    COUNT(*) AS total,
    AVG(CASE WHEN EVENT_JSON:outcome::string IN ('success','great_success') THEN 1.0 ELSE 0.0 END) AS success_rate
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1
ORDER BY 1;
```

```sql
-- 특성 공개 여부 버전
SELECT
    EVENT_JSON:trait_revealed::boolean AS trait_revealed,
    COUNT(*) AS total,
    AVG(CASE WHEN EVENT_JSON:outcome::string IN ('success','great_success') THEN 1.0 ELSE 0.0 END) AS success_rate
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1;
```

### G13 — 무기 직접 선택 비율

- **재는 것** — 기본 무기가 아니라 플레이어가 직접 골라서 대여해준 모험의 비율. 무기 대여는 이 게임의 핵심 수익·성공률 조절 수단이므로, 이 비율이 곧 "핵심 메커니즘이 실제로 쓰이고 있는가"다.
- **읽는 법** — `player_selected_rate` 하나만 보면 된다(0~1). `total`은 전체 모험 수.
- **해석** — 낮으면 무기 선택이 사실상 미사용 상태다. 원인은 선택 UI 진입이 어렵거나, 인벤토리에 쓸 만한 무기가 없거나, 절차가 번거로운 것. 어느 쪽인지는 G1(소요 초)·G10(무기 구매 지출)과 교차해서 좁힌다.
- **주의** — 튜토리얼 구간(1일차)에서는 강제 흐름 때문에 비율이 왜곡된다. `AND EVENT_JSON:day::int > 1` 을 붙여 한 번 더 보는 편이 정확하다.

```sql
SELECT
    AVG(CASE WHEN EVENT_JSON:weapon_is_player_selected::boolean THEN 1.0 ELSE 0.0 END) AS player_selected_rate,
    COUNT(*) AS total
FROM EVENTS
WHERE EVENT_NAME = 'adventure_started';
```

### G17 — 던전별 모험 결과 분포 (사망 집중 구간 탐지)

- **재는 것** — 첫 쿼리는 던전별 `outcome` 5종(great_success/success/failure/death/retreat)의 원본 건수 분포, 둘째 쿼리는 사망률만 뽑아 높은 순으로 정렬한 것이다.
- **읽는 법** — 세 결과의 의미가 각각 다르므로 절대 뭉뚱그리지 말 것. `death`는 **무기 손실**이 발생하는 경제 타격, `failure`는 손실 없는 단순 실패, `retreat`는 플레이어가 스스로 물린 것이다.
- **해석** — 사망률 상위 던전이 곧 유저 자산이 녹아 없어지는 지점이고, 폐업(게임오버)으로 가는 주요 경로다. `retreat` 비율이 적당히 있는 것은 오히려 건강한 신호 — 유저가 위험을 사전에 인지하고 판단하고 있다는 뜻이다. 반대로 `retreat`가 0에 가까운데 `death`가 높으면, 위험 정보가 유저에게 전달되지 않아 그냥 죽고 있다는 뜻이다.
- **함께 볼 것** — G7과 같은 던전을 나란히 보면 "성공률은 괜찮은데 사망률만 높은 던전"(고위험 고보상, 의도된 설계일 수 있음)과 "성공률도 낮고 사망률도 높은 던전"(단순한 함정)이 구분된다.
- **주의** — 둘째 쿼리의 `HAVING COUNT(*) >= 10`은 표본 부족 던전을 걸러내는 장치다. 초기에는 이 조건 때문에 결과가 비어 보일 수 있는데, 숫자를 낮추기보다 표본이 쌓일 때까지 기다리는 게 맞다.

```sql
SELECT
    EVENT_JSON:dungeon_id::string AS dungeon,
    EVENT_JSON:outcome::string    AS outcome,
    COUNT(*) AS cnt
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1, 2
ORDER BY 1, 3 DESC;
```

```sql
-- 던전별 사망률만 뽑아 정렬
SELECT
    EVENT_JSON:dungeon_id::string AS dungeon,
    COUNT(*) AS total,
    AVG(CASE WHEN EVENT_JSON:outcome::string = 'death' THEN 1.0 ELSE 0.0 END) AS death_rate
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1
HAVING COUNT(*) >= 10
ORDER BY 3 DESC;
```

### G19 — 타입 매칭 실천율 + 매칭 여부 x 성공률

- **재는 것** — 첫 쿼리는 던전 방어 타입에 맞는 무기를 들려 보낸 비율(실천율), 둘째 쿼리는 매칭 여부에 따른 성공률 차이(효과 크기)다. **두 쿼리는 반드시 짝으로 봐야 결론이 난다.**
- **해석** — 네 가지 조합으로 판단한다.
  - 효과 있음 + 실천율 낮음 → 유저가 이득을 그냥 놓치는 중. 정보 표시(무기 선택 UI, 튜토리얼) 개선이 답.
  - 효과 있음 + 실천율 높음 → 메커니즘이 의도대로 작동. 건드릴 것 없음.
  - 효과 없음 + 실천율 높음 → 유저에게 헛수고를 시키는 중. 보너스 수치를 올리거나 메커니즘을 걷어내야 한다.
  - 효과 없음 + 실천율 낮음 → 시스템이 사실상 존재하지 않는 상태.
- **주의** — 아무 무기나 골라도 우연히 타입이 맞는 **기저 확률**이 있다(무기 타입 수의 역수 수준). 실천율은 이 기저보다 뚜렷하게 높아야 "의도적으로 맞추고 있다"고 말할 수 있다. 기저 확률을 모르면 무작위 대여가 강제되는 튜토리얼/초반 일차의 실천율을 비교 기준으로 삼으면 된다.

```sql
SELECT
    AVG(CASE WHEN EVENT_JSON:armor_type_match::boolean THEN 1.0 ELSE 0.0 END) AS match_rate,
    COUNT(*) AS total
FROM EVENTS
WHERE EVENT_NAME = 'adventure_started';
```

```sql
SELECT
    EVENT_JSON:armor_type_match::boolean AS matched,
    COUNT(*) AS total,
    AVG(CASE WHEN EVENT_JSON:outcome::string IN ('success','great_success') THEN 1.0 ELSE 0.0 END) AS success_rate
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1;
```

### G22 — 네임드 vs 일반 모험가 성공률

- **재는 것** — 단골(네임드) 모험가와 일반 모험가의 성공률 차이. "단골을 관리할 이유가 있는가"를 검증한다.
- **읽는 법** — `true`/`false` 두 행. `total` 비율이 곧 네임드 모험 비중이기도 하다.
- **해석** — 네임드가 뚜렷하게 높아야 단골 관리의 보상이 성립한다. 차이가 없으면 네임드 특성 보너스가 실질적이지 않다는 뜻 → 보너스 수치 상향. 반대로 네임드가 압도적으로 높으면 일반 모험가를 상대할 이유가 사라져 게임이 "네임드 대기 게임"이 된다.
- **주의** — 네임드는 등장 자체가 드물어 두 그룹의 `total` 격차가 크다. 네임드 표본이 수십 건 미만이면 비율 차이는 노이즈일 가능성이 높다.

```sql
SELECT
    EVENT_JSON:is_named_adventurer::boolean AS is_named,
    COUNT(*) AS total,
    AVG(CASE WHEN EVENT_JSON:outcome::string IN ('success','great_success') THEN 1.0 ELSE 0.0 END) AS success_rate
FROM EVENTS
WHERE EVENT_NAME = 'adventure_completed'
GROUP BY 1;
```

### G26 — 모험 결과 확인 지연

- **재는 것** — 모험이 완료된 시각부터 유저가 결과창을 실제로 연 시각까지의 경과 **실시간 분**. 앱을 껐다 켜도 유지되도록 `DateTime.UtcNow` 기준으로 계산한다.
- **해석** — 모험을 걸어두고 자리를 비우는 리듬이 실제로 생기는지 보는 직접 지표다. 대부분이 0~1분에 몰려 있으면 유저가 화면을 붙들고 결과를 지켜보고 있다는 뜻 → 모험 소요 시간이 너무 짧아 자리를 뜰 여지를 주지 않는 것. 반대로 수십 분~수 시간 꼬리가 길게 나오면 의도한 대기 리듬이 작동하는 것이다.
- **활용** — 이 분포가 곧 모험 소요 시간 설계의 근거가 된다. 유저가 실제로 자리를 비우는 시간보다 모험이 훨씬 짧으면 그 사이 대기 시간이 전부 버려진다.

```sql
SELECT
    EVENT_JSON:delay_real_min::int AS delay_min,
    COUNT(*) AS cnt
FROM EVENTS
WHERE EVENT_NAME = 'panel_opened'
  AND EVENT_JSON:panel::string = 'adventure_result'
GROUP BY 1
ORDER BY 1;
```

---

## 대장장이

### G4 — 기능별 사용 빈도

- **재는 것** — 대장장이 5개 기능(강화/진화/해체/재련/제작)이 각각 얼마나 실행됐는지. 버튼 클릭이 아니라 **실제로 완료된 실행**만 센다.
- **읽는 법** — 세 컬럼을 반드시 나눠서 봐야 한다. `users`는 그 기능을 한 번이라도 쓴 유저 수(= 발견율), `avg_per_user`는 쓴 사람이 얼마나 반복했는지(= 효용), `executions`는 둘의 곱이라 단독으로는 정보가 적다.
- **해석** — 진단이 갈린다.
  - `users`가 낮음 → 기능을 **발견하지 못함**. 탭 시인성·진입 동선 문제.
  - `users`는 높은데 `avg_per_user`가 낮음 → 한 번 써보고 **다시 쓸 이유를 못 느낌**. 비용 대비 효과 문제.
  - 예를 들어 `enforce` 50회 vs `reroll` 1회처럼 격차가 극단적이면 재련 쪽을 먼저 들여다볼 것.
- **주의** — `blacksmith_evolve_done`은 성공만, `blacksmith_enforce_done`은 성공·실패 모두 기록된다. 두 이벤트의 건수를 그대로 비교하면 강화가 과대평가되므로, 강화만 `AND EVENT_JSON:success::boolean = TRUE` 를 붙여 성공 기준으로 다시 한 번 비교해볼 것. 해체는 일괄 분해 시 무기당 1건이라 건수가 부풀 수 있다.

```sql
SELECT
    EVENT_NAME,
    COUNT(*) AS executions,
    COUNT(DISTINCT USER_ID) AS users,
    COUNT(*) / NULLIF(COUNT(DISTINCT USER_ID), 0) AS avg_per_user
FROM EVENTS
WHERE EVENT_NAME IN (
    'blacksmith_enforce_done', 'blacksmith_evolve_done',
    'blacksmith_disassemble_done', 'blacksmith_reroll_done', 'blacksmith_craft_done')
GROUP BY 1
ORDER BY 2 DESC;
```

### G21 — 강화 실패 후 재시도율

- **재는 것** — 강화에 실패한 뒤 유산을 써서 재시도하는 비율. G4가 "기능을 쓰는가"라면 이건 "실패했을 때 회복 행동을 하는가"에 초점이 있다.
- **읽는 법** — `sessions_with_fail`(실패가 있었던 세션 수) 대비 `sessions_with_retry`(재시도 클릭이 있었던 세션 수)의 비율.
- **해석** — 낮으면 유산 재시도 비용이 체감상 비싸거나, 재시도 버튼의 존재를 모르는 것이다. 유산은 회차 간 자원이라 "아껴두려는" 심리가 강하게 작동하므로, 낮게 나오는 것 자체는 이상하지 않다. G10의 `legacy_transaction` spend 비중과 같이 보면 유산을 어디에 쓰기로 결정하는지가 드러난다.
- **주의** — 이건 세션 단위 근사다. 실패는 A세션에서, 재시도는 다음 세션에서 하면 잡히지 않고, 한 세션에 실패가 여러 번 있어도 1로 센다. 정확한 전환율이 필요하면 SQL 대신 UGS **Funnels**로 두 이벤트를 순서 지정해 분석할 것.

```sql
-- 실패가 발생한 세션 중 재시도 클릭이 있었던 세션 비율
WITH fail_sessions AS (
    SELECT DISTINCT SESSION_ID
    FROM EVENTS
    WHERE EVENT_NAME = 'blacksmith_enforce_done' AND EVENT_JSON:success::boolean = FALSE
), retry_sessions AS (
    SELECT DISTINCT SESSION_ID
    FROM EVENTS
    WHERE EVENT_NAME = 'btn_clicked'
      AND EVENT_JSON:panel::string = 'blacksmith'
      AND EVENT_JSON:button::string = 'enforce_retry'
)
SELECT
    (SELECT COUNT(*) FROM fail_sessions)  AS sessions_with_fail,
    (SELECT COUNT(*) FROM retry_sessions) AS sessions_with_retry,
    (SELECT COUNT(*) FROM retry_sessions) / NULLIF((SELECT COUNT(*) FROM fail_sessions), 0) AS retry_rate;
```

### G23 — 대장장이 타입별 이용 빈도

```sql
SELECT
    EVENT_JSON:blacksmith_type::string AS blacksmith_type,
    COUNT(*) AS visits,
    COUNT(DISTINCT USER_ID) AS users
FROM EVENTS
WHERE EVENT_NAME = 'panel_opened'
  AND EVENT_JSON:panel::string = 'blacksmith'
GROUP BY 1
ORDER BY 2 DESC;
```

- **재는 것** — 대장장이 NPC 4종 타입(`cost_reduction` / `material_reduction` / `success_rate` / `disassemble_boost`)별 방문 횟수 분포.
- **해석** — 방문이 한 타입에 몰리면 나머지 타입의 혜택이 매력적이지 않다는 뜻이다. 단, **스폰 빈도 자체가 불균형해서** 그렇게 보일 수도 있으므로, 방문 횟수만으로 판단하지 말고 타입별 스폰 횟수 대비 방문 비율로 정규화해서 봐야 한다(스폰 이벤트가 없다면 스폰 확률 Config 값과 대조).
- **활용** — 특정 타입만 방문이 몰린다면 그 타입이 제공하는 혜택(비용 절감 등)이 현재 유저의 가장 큰 병목이라는 신호이기도 하다. G10의 대장장이 관련 지출 비중과 교차하면 병목이 골드인지 재료인지 성공률인지 판별된다.

---

## 퀘스트 & 이벤트

### G5 — 퀘스트 난이도 (소요일 / 실패율)

- **재는 것** — 첫 쿼리는 완료된 퀘스트가 발행일로부터 며칠 걸렸는지, 둘째 쿼리는 퀘스트별 완료/실패 건수와 실패율이다. 퀘스트 실패는 벌금 → 파산으로 이어지는 직접 경로라 폐업 원인 분석의 출발점이 된다.
- **읽는 법** — `avg_days`를 해당 퀘스트의 마감 기한과 비교해서 읽는다. 절대값 자체보다 "기한까지 얼마나 여유가 있었나"가 핵심이다. 둘째 쿼리의 `fail_rate`는 완료+실패를 분모로 한 비율.
- **해석** — `avg_days`가 마감 기한에 바짝 붙어 있는 퀘스트는 지금은 통과 중이어도 표본이 늘면 실패로 넘어갈 후보다. `fail_rate` 상위 퀘스트가 곧 벌금을 유발하는 퀘스트 = 조건 완화 1순위. 반대로 `avg_days`가 기한의 절반도 안 되는 퀘스트는 도전 요소가 없는 것.
- **주의** — 완료도 실패도 건수가 적은 퀘스트는 "쉬워서 조용한 것"이 아니라 **아직 표본이 없는 것**이다. 반드시 `completed + failed` 합을 먼저 확인할 것. 난이도 설계 기준값은 [[주간퀘스트_난이도_계수]]·[[주간퀘스트_레벨디자인]]와 대조한다.

```sql
SELECT
    EVENT_JSON:quest_id::string AS quest_id,
    COUNT(*) AS completions,
    AVG(EVENT_JSON:days_to_complete::int) AS avg_days
FROM EVENTS
WHERE EVENT_NAME = 'quest_completed'
GROUP BY 1
ORDER BY 3 DESC;
```

```sql
-- 퀘스트별 완료/실패 건수와 실패율
SELECT
    EVENT_JSON:quest_id::string AS quest_id,
    SUM(CASE WHEN EVENT_NAME = 'quest_completed' THEN 1 ELSE 0 END) AS completed,
    SUM(CASE WHEN EVENT_NAME = 'quest_failed'    THEN 1 ELSE 0 END) AS failed,
    SUM(CASE WHEN EVENT_NAME = 'quest_failed' THEN 1.0 ELSE 0.0 END) / NULLIF(COUNT(*), 0) AS fail_rate
FROM EVENTS
WHERE EVENT_NAME IN ('quest_completed', 'quest_failed')
GROUP BY 1
ORDER BY 4 DESC;
```

### G6 — 아침 이벤트 수락률

- **재는 것** — 아침 이벤트 9종 각각의 수락/거절 비율. 두 쿼리의 **분모가 다르다**는 점이 핵심이다.
  - 첫 쿼리(`resolved` 기준) — 선택창까지 가서 실제로 수락/거절을 누른 건만.
  - 둘째 쿼리(`shown` 대비) — 등장 자체를 분모로 삼아, 그냥 무시하고 지나간 경우까지 포함.
- **해석** — 두 값의 차이가 곧 "선택창까지 안 간 유저" 규모다. 차이가 크면 이벤트 UI를 그냥 닫아버리고 있다는 뜻. 특정 이벤트의 거절률이 압도적이면 보상이 요구 조건에 못 미치는 것이고, 반대로 수락률이 100%에 가까운 이벤트는 고민할 여지가 없는 것이라 "선택"으로 기능하지 못한다 — 둘 다 조정 대상이다. 이상적인 이벤트는 수락률이 극단에 붙지 않는다.
- **주의** — 아침 이벤트는 하루 1회 제한이 있어 유형별 표본이 천천히 쌓인다. 등장 확률이 낮은 유형은 한참 지나야 판단 가능하다.

```sql
-- resolved 기준 (수락/거절이 기록된 건)
SELECT
    EVENT_JSON:event_type::string AS event_type,
    SUM(CASE WHEN EVENT_JSON:choice::string = 'accept' THEN 1 ELSE 0 END) AS accepts,
    SUM(CASE WHEN EVENT_JSON:choice::string = 'reject' THEN 1 ELSE 0 END) AS rejects
FROM EVENTS
WHERE EVENT_NAME = 'morning_event_resolved'
GROUP BY 1;
```

```sql
-- 등장(스폰) 대비 수락률 — 클릭조차 안 하고 보낸 경우까지 분모에 포함
WITH shown AS (
    SELECT EVENT_JSON:event_type::string AS et, COUNT(*) AS shown_cnt
    FROM EVENTS WHERE EVENT_NAME = 'morning_event_shown' GROUP BY 1
), accepted AS (
    SELECT EVENT_JSON:event_type::string AS et, COUNT(*) AS accept_cnt
    FROM EVENTS
    WHERE EVENT_NAME = 'morning_event_resolved' AND EVENT_JSON:choice::string = 'accept'
    GROUP BY 1
)
SELECT s.et AS event_type, s.shown_cnt, COALESCE(a.accept_cnt, 0) AS accepts,
       COALESCE(a.accept_cnt, 0) / NULLIF(s.shown_cnt, 0) AS accept_rate
FROM shown s
LEFT JOIN accepted a ON s.et = a.et
ORDER BY 4;
```

### G16 — 퀘스트보드 던전 등급 선택 성향

- **재는 것** — 첫 쿼리는 의뢰판에서 확정한 던전의 등급(0~4) 분포, 둘째 쿼리는 일차가 지날수록 평균 선택 등급이 올라가는지 보는 성장 곡선이다.
- **해석** — 저등급에만 몰려 있으면 고등급 던전 콘텐츠가 사실상 소비되지 않는 상태다. 둘째 쿼리의 `avg_grade`는 **우상향이 정상** — 무기와 평판이 성장하면 더 어려운 던전을 고를 수 있어야 한다. 평평하다면 성장이 선택지 확장으로 이어지지 않는 것이고, 원인은 (a) 고등급 보상이 위험 대비 매력이 없거나 (b) 성장 속도가 던전 요구치를 못 따라가는 것 중 하나다. G7(등급별 성공률)을 같이 보면 후자인지 판별된다.
- **주의** — 후반 일차는 살아남은 소수 유저만 남아 `avg_grade`가 튄다. 일차별 표본 수를 같이 뽑아(`COUNT(*)` 추가) 확인할 것.

```sql
SELECT
    EVENT_JSON:dungeon_grade::int AS grade,
    COUNT(*) AS picks
FROM EVENTS
WHERE EVENT_NAME = 'quest_board_confirmed'
GROUP BY 1
ORDER BY 1;
```

```sql
-- 일차가 지날수록 고등급 도전이 늘어나는지
SELECT
    EVENT_JSON:day::int AS day,
    AVG(EVENT_JSON:dungeon_grade::int) AS avg_grade
FROM EVENTS
WHERE EVENT_NAME = 'quest_board_confirmed'
GROUP BY 1
ORDER BY 1;
```

### G25 — 의뢰판 새로고침 사용률

```sql
WITH opens AS (
    SELECT COUNT(*) AS cnt FROM EVENTS
    WHERE EVENT_NAME = 'panel_opened' AND EVENT_JSON:panel::string = 'quest_board'
), refreshes AS (
    SELECT COUNT(*) AS cnt FROM EVENTS
    WHERE EVENT_NAME = 'btn_clicked'
      AND EVENT_JSON:panel::string = 'quest_board'
      AND EVENT_JSON:button::string = 'refresh'
)
SELECT opens.cnt AS board_opens, refreshes.cnt AS refresh_clicks,
       refreshes.cnt / NULLIF(opens.cnt, 0) AS refresh_rate
FROM opens, refreshes;
```

- **재는 것** — 의뢰판을 연 횟수 대비 새로고침 버튼을 누른 비율. 매일 9시에 자동 생성되는 던전 목록의 **초기 큐레이션 만족도**를 재는 지표다.
- **해석** — 비율이 높으면 처음 제시된 목록이 마음에 들지 않는다는 뜻 → 던전 추천 로직이 유저의 현재 전력·퀘스트 목표와 어긋나 있다. 새로고침에는 골드 비용이 들기 때문에, 돈을 내고서라도 다시 뽑는다는 건 불만이 꽤 크다는 신호다.
- **활용** — `day`와 교차하면 후반으로 갈수록 새로고침이 늘어나는지 볼 수 있다. 늘어난다면 그건 불만이 아니라 "골드가 여유로워져서 최적화하는 것"일 수 있으므로, G10의 `quest_board_refresh` 지출 비중과 같이 해석해야 한다.

---

## 경제 & 성장

### G8 — 수색꾼 활용도

- **재는 것** — 일차별 수색꾼 파견 횟수. 수색꾼은 재료 수급 경로이므로, 이 값은 곧 "재료가 필요한 시점"의 대리 지표이기도 하다.
- **해석** — 초반 몇 일 이후 0에 수렴하면 기능이 잊혔거나(사이드바 버튼 시인성) 파견 비용 대비 얻는 재료가 아쉬운 것이다. 특정 일차에 급증한다면 그 시점에 재료 병목(강화·제작 요구량 급등)이 걸린다는 뜻이므로, 대장장이 비용 곡선을 그 구간에서 점검해야 한다.
- **함께 볼 것** — 파견 화면까지 갔는데 실제로 안 보내는 비율은 아래 쿼리로 본다. 화면 진입 자체가 없으면 기능 인지 문제, 진입 후 이탈이 많으면 비용 부담이나 효용 불명이다.

```sql
-- 수색 파견 화면 진입 대비 실제 파견 전환율
WITH opens AS (
    SELECT COUNT(*) AS cnt FROM EVENTS
    WHERE EVENT_NAME = 'panel_opened' AND EVENT_JSON:panel::string = 'scout_dispatch'
), sends AS (
    SELECT COUNT(*) AS cnt FROM EVENTS
    WHERE EVENT_NAME = 'btn_clicked'
      AND EVENT_JSON:panel::string = 'scout_dispatch'
      AND EVENT_JSON:button::string = 'send_scout'
)
SELECT opens.cnt AS dispatch_opens, sends.cnt AS send_clicks,
       sends.cnt / NULLIF(opens.cnt, 0) AS send_rate
FROM opens, sends;
```

> `scout_result` 패널은 코드에 존재하지 않으므로 "결과 확인율"은 측정 대상이 아니다.

```sql
SELECT EVENT_JSON:day::int AS day, COUNT(*) AS scouts
FROM EVENTS
WHERE EVENT_NAME = 'scout_sent'
GROUP BY 1
ORDER BY 1;
```

### G10 — 골드/유산 흐름 (source별 수지)

- **재는 것** — 골드가 **어디서 들어오고 어디로 나가는지**를 경로(`source`)별로 집계한 것. 경제 설계 검토의 중심 쿼리이고, 다른 지표에서 이상이 보일 때 원인을 찾으러 오는 곳이기도 하다.
- **읽는 법** — `direction`은 `earn`(수입) / `spend`(지출). `tx_count`(거래 건수)와 `total_gold`(금액 합)를 **반드시 같이** 볼 것. 건수는 많은데 금액이 작은 경로(잡일 보상)와 건수는 적은데 금액이 큰 경로(퀘스트 보상)는 유저 체감이 완전히 다르다.
- **해석** —
  - `earn` 쪽에서 한 source가 전체의 70%를 넘으면 경제가 그 경로 하나에 의존하는 것 → 그 경로 밸런스가 곧 게임 전체 밸런스가 된다.
  - `spend` 쪽 상위가 곧 실제 골드 sink다. 설계상 주요 sink여야 할 항목(무기 구매, 강화)이 상위에 없으면 유저가 그 기능을 안 쓰고 골드를 쌓아두고 있다는 뜻이다.
  - `earn` 합 대비 `spend` 합이 크게 작으면 인플레이션(골드가 남아돔), 반대면 만성 자금난이다.
- **유산 쿼리** — 같은 구조지만 유산은 회차를 넘어가는 자원이라 관점이 다르다. `spend` 합이 `earn` 합보다 현저히 작으면 유산이 쌓이기만 하고 순환되지 않는 것 = 소비처가 부족하거나 비싸서 아끼는 것. `balance_after` 추이를 같이 보면 확실해진다.
- **주의** — `source` 값이 `other`로 잡히는 건 `EconomyManager`의 한국어 reason이 매핑 테이블에 없다는 뜻이다. `other` 비중이 크면 분석 이전에 매핑부터 보강해야 한다. 그리고 대여 수수료는 코드상 `adventure_reward`에 합산되어 있어 분리되지 않는다.

```sql
SELECT
    EVENT_JSON:direction::string AS direction,
    EVENT_JSON:source::string    AS source,
    COUNT(*) AS tx_count,
    SUM(EVENT_JSON:amount::int) AS total_gold
FROM EVENTS
WHERE EVENT_NAME = 'gold_transaction'
GROUP BY 1, 2
ORDER BY 1, 4 DESC;
```

```sql
-- 유산 버전 (+ 잔액 추이로 쌓이기만 하는지 순환되는지 확인)
SELECT
    EVENT_JSON:direction::string AS direction,
    EVENT_JSON:source::string    AS source,
    COUNT(*) AS tx_count,
    SUM(EVENT_JSON:amount::int) AS total_legacy
FROM EVENTS
WHERE EVENT_NAME = 'legacy_transaction'
GROUP BY 1, 2
ORDER BY 1, 4 DESC;
```

### G20 — 유산 업그레이드 첫 구매 항목

- **재는 것** — 유저가 생애 첫 번째로 산 유산 업그레이드가 무엇인지(`purchase_order = 1`). 첫 구매는 곧 **첫 회차에서 가장 아쉬웠던 것**이라, 유저 불만을 직접 묻는 것에 가장 가까운 지표다.
- **해석** — `StartingGold`류에 몰리면 초반 자금이 빡빡하다는 직접적 신호다. 특정 항목 하나가 압도적이면 나머지 업그레이드의 가격 대비 효과가 매력적이지 않다는 뜻이므로, 그 항목을 너프하기보다 다른 항목을 손보는 쪽이 맞다.
- **주의** — 첫 구매는 유저당 딱 1건이라 표본이 유저 수와 같다. 다른 어떤 쿼리보다 표본이 느리게 쌓이므로 초기에는 참고만 할 것. `purchase_order` 필터를 2, 3으로 바꾸면 구매 순서 전체를 볼 수 있다.

```sql
SELECT
    EVENT_JSON:upgrade_key::string AS upgrade_key,
    COUNT(*) AS first_purchases
FROM EVENTS
WHERE EVENT_NAME = 'legacy_upgraded'
  AND EVENT_JSON:purchase_order::int = 1
GROUP BY 1
ORDER BY 2 DESC;
```

---

## 게임 흐름 & 유산

### G2 — 평균 게임 시간 (몇 일차에, 몇 분 플레이 후 게임오버?)

- **재는 것** — 첫 쿼리는 게임오버 시점의 평균/중앙 일차와 평균 실플레이 시간, 둘째 쿼리는 게임오버가 몇 일차에 몰리는지의 분포다. **난이도 곡선의 최종 성적표**에 해당한다.
- **읽는 법** — `avg_day`는 오래 버틴 소수에 끌려가므로 `median_day`가 실질적인 대표값이다. `avg_playtime_min`은 인게임 일차가 아니라 실제 소비 시간이라, 두 값을 같이 보면 "며칠을 몇 분 만에 소비하는가"(= 체감 밀도)가 나온다.
- **해석** — 둘째 쿼리의 분포에서 특정 일차에 봉우리가 서면, 그 지점에 구조적인 벽이 있다는 뜻이다. **주간 퀘스트 마감일(8일차 등)과 봉우리가 겹치는지 확인하는 것이 핵심** — 겹친다면 벌금이 폐업의 주된 방아쇠다. 봉우리 없이 완만하게 흩어져 있으면 난이도가 서서히 조여드는 정상적인 곡선이다.
- **주의** — 밸런스 시뮬레이터의 생존 목표는 **주차 단위**(초급 15 / 중급 35 / 상급 60주)로 잡혀 있고 여기 `day`는 일차다. 비교하려면 7을 곱해 환산할 것. 기준 수치는 [[밸런스_시뮬레이터_정리]] 참조.

```sql
SELECT
    AVG(EVENT_JSON:day::int)    AS avg_day,
    MEDIAN(EVENT_JSON:day::int) AS median_day,
    AVG(EVENT_JSON:total_playtime_sec::int) / 60.0 AS avg_playtime_min
FROM EVENTS
WHERE EVENT_NAME = 'game_over';
```

```sql
-- 게임오버 일차 분포 (난이도 곡선 확인)
SELECT EVENT_JSON:day::int AS day, COUNT(*) AS game_overs
FROM EVENTS
WHERE EVENT_NAME = 'game_over'
GROUP BY 1
ORDER BY 1;
```

### G14 — 게임오버 후 재시작율

- **재는 것** — 한 번이라도 게임오버를 겪은 유저 중, 그 이후에 다시 게임을 시작한 유저의 비율. 유산(영구 업그레이드) 시스템이 **재도전 동기로 실제 기능하는가**를 검증한다.
- **읽는 법** — `users_with_gameover`가 분모, `users_restarted`가 분자. 이 게임에서 가장 중요한 리텐션 지표에 해당한다.
- **해석** — 낮으면 게임오버 화면에서 "다음 회차엔 더 잘할 수 있다"는 기대가 전달되지 않는 것이다. 유산 획득량이 적거나, 업그레이드 효과가 체감되지 않거나, 게임오버 연출이 재도전 대신 종료로 읽히는 것. G20(첫 구매 항목)과 같이 보면 재시작한 유저가 무엇에 끌렸는지가 드러난다.
- **주의** — `USER_ID`는 설치 기준이라 재설치나 기기 변경 시 다른 유저로 잡힌다. 또 전체 기간 누적이므로 "게임오버 직후 바로 재시작"과 "며칠 뒤 돌아와 재시작"이 구분되지 않는다. 시점 구분이 필요하면 `e.EVENT_TIMESTAMP > g.go_time` 조건에 상한(예: `AND e.EVENT_TIMESTAMP < DATEADD(day, 1, g.go_time)`)을 추가한다.

```sql
WITH first_gameover AS (
    SELECT USER_ID, MIN(EVENT_TIMESTAMP) AS go_time
    FROM EVENTS WHERE EVENT_NAME = 'game_over' GROUP BY 1
), restarted AS (
    SELECT DISTINCT g.USER_ID
    FROM first_gameover g
    JOIN EVENTS e
      ON e.USER_ID = g.USER_ID
     AND e.EVENT_NAME = 'game_start'
     AND e.EVENT_TIMESTAMP > g.go_time
)
SELECT
    (SELECT COUNT(*) FROM first_gameover) AS users_with_gameover,
    (SELECT COUNT(*) FROM restarted)      AS users_restarted,
    (SELECT COUNT(*) FROM restarted) / NULLIF((SELECT COUNT(*) FROM first_gameover), 0) AS restart_rate;
```

### G15 — 일차별 모험 횟수 (핵심 루프 지속성)

- **재는 것** — 인게임 일차마다 유저 1명이 평균 몇 번 모험을 보냈는지. 모험 주선이 이 게임의 핵심 루프이므로, **루프가 몇 일차까지 정상 회전하는가**를 보는 지표다.
- **읽는 법** — `per_user`가 핵심이고 `adventures`(총 건수)는 유저 수에 비례해 움직이므로 단독 해석 불가. `users`는 그 일차에 도달한 유저 수 = 표본 신뢰도.
- **해석** — `day`가 커져도 `per_user`가 유지되거나 완만히 늘어야 건강하다. 중반(6일차~)부터 꺾이면 루프가 막힌 것이고, 원인은 대개 무기 재고 부족이나 골드 부족이다. G10의 `spend` 상위 항목과 G9(전환율)를 같이 보면 무엇이 말랐는지 판별된다. 반대로 후반에 `per_user`가 급증하면 살아남은 상위 유저만 남아 생기는 착시일 수 있다.
- **주의** — 뒤쪽 일차일수록 `users`가 급감한다. **`users`가 한 자릿수로 떨어지는 구간부터는 `per_user`를 해석하지 말 것.** 그래프로 그릴 때 `users`를 보조 축에 같이 얹어두면 어디까지 믿을 수 있는지 한눈에 보인다.

```sql
SELECT
    EVENT_JSON:day::int AS day,
    COUNT(*) AS adventures,
    COUNT(DISTINCT USER_ID) AS users,
    COUNT(*) / NULLIF(COUNT(DISTINCT USER_ID), 0) AS per_user
FROM EVENTS
WHERE EVENT_NAME = 'adventure_started'
GROUP BY 1
ORDER BY 1;
```

### G18 — 게임오버 시 평판 레벨 분포

- **재는 것** — 회차가 끝날 때 유저가 도달해 있던 평판 레벨(Bronze/Silver/Gold/Platinum/Diamond) 분포. 평판은 스폰 속도와 던전 선택지를 여는 열쇠라, 이 분포가 곧 **콘텐츠가 어디까지 소비되는가**를 뜻한다.
- **해석** — Bronze에 몰려 있으면 Silver 이상에 붙여둔 콘텐츠(빠른 방문자 스폰, 넓은 던전 선택지)가 사실상 배포되지 않은 상태다. 만든 콘텐츠가 유저에게 도달하지 않는 것이므로 평판 상승 속도나 레벨 요구치를 재검토해야 한다. 반대로 상위 레벨에 몰리면 평판이 너무 쉽게 올라 성장 실감이 옅어진다.
- **함께 볼 것** — G2의 게임오버 일차 분포와 겹쳐 보면 "며칠을 버텨서 어느 레벨까지 갔는가"가 나온다. 오래 버텼는데도 레벨이 낮다면 평판 획득량 자체가 부족한 것이다.
- **참고** — `day_begin`에도 `reputation_level`이 있어, `EVENT_NAME`을 바꾸면 "플레이 중 주로 머무는 레벨" 분포도 같은 쿼리로 볼 수 있다.

```sql
SELECT
    EVENT_JSON:reputation_level::string AS level,
    COUNT(*) AS game_overs
FROM EVENTS
WHERE EVENT_NAME = 'game_over'
GROUP BY 1
ORDER BY 2 DESC;
```

### G27 — 튜토리얼 이탈 구간 (유저별 최대 도달 단계)

- **재는 것** — 유저마다 1일차 튜토리얼 12단계 중 어디까지 갔는지의 최대값 분포. 신규 유저 이탈은 대부분 첫 세션에서 발생하고 D1 리텐션과 직결되므로, **27개 목표 중 우선순위가 가장 높은 지표**다.
- **읽는 법** — `reached_step` = 도달한 최대 단계, `users` = 그 단계까지 간 유저 수. 12가 완주다.
- **해석** — 단계별 `users`가 계단식으로 뚝 떨어지는 지점이 곧 이탈 구간이다. 예를 들어 4단계 30명 → 5단계 12명이면 4단계 안내에 문제가 있는 것. 대부분이 12에 몰려 있으면 튜토리얼은 정상이다. 어느 단계가 무엇인지는 `step_name`(TutorialStep enum)으로 확인하면 되고, `SELECT`에 `EVENT_JSON:step_name::string`을 추가하면 번호 대신 이름으로 볼 수 있다.
- **주의** — 이 값은 "그 단계에서 그만둔 유저"가 아니라 "지금까지 도달한 최대 단계"다. **아직 플레이 중인 유저도 낮은 값으로 잡히므로**, 기간 필터를 최근 하루로 좁히면 진행 중인 유저가 이탈처럼 보인다. 며칠 지난 구간을 대상으로 보거나 필터 없이 볼 것.
- **대안** — 같은 내용을 Analytics → **Funnels**에 퍼널 E(step 1 → 4 → 8 → 12)로 등록하면 단계별 이탈률을 시각적으로 볼 수 있다. `tutorial_step`만 있으면 되므로 지금 바로 등록 가능하다.

```sql
WITH max_step AS (
    SELECT USER_ID, MAX(EVENT_JSON:step::int) AS reached_step
    FROM EVENTS
    WHERE EVENT_NAME = 'tutorial_step'
    GROUP BY 1
)
SELECT reached_step, COUNT(*) AS users
FROM max_step
GROUP BY 1
ORDER BY 1;
```

---

## UI & UX

### G3 — 버튼 최초 클릭 시점 / 미사용 버튼

- **재는 것** — 버튼별로 (a) 한 번이라도 누른 유저 수와 (b) 그 첫 클릭이 평균 몇 일차에 일어났는지. 기능이 **화면에서 보이는가**를 재는 UI 가시성 지표다.
- **읽는 법** — `users_clicked`가 오름차순 정렬돼 있어 **맨 위에 오는 버튼이 가장 안 눌리는 버튼**이다. `avg_first_click_day`는 발견까지 걸린 시간.
- **해석** — `users_clicked`가 전체 유저 수 대비 현저히 낮은 버튼 = 존재를 모르는 기능. `avg_first_click_day`가 큰 버튼 = 있는 건 알아도 한참 뒤에야 쓰게 되는 기능으로, 튜토리얼이나 안내에서 다뤄줄 후보다. 대장장이 재련 탭처럼 깊이 들어가야 하는 기능이 상단에 뜨는지 확인할 것.
- **주의** — `is_first_time`은 클라이언트의 `PlayerPrefs`로 판정하므로, 재설치하면 다시 `true`가 된다. 그리고 이 쿼리는 "누른 유저"만 세므로 **한 번도 안 누른 유저 비율은 전체 유저 수를 따로 구해 나눠야** 나온다(`SELECT COUNT(DISTINCT USER_ID) FROM EVENTS`).

```sql
-- 버튼별 최초 클릭 일차 분포. 한 번도 안 눌린 유저 비율은
-- 전체 유저 수 대비 해당 버튼 is_first_time=true 발생 유저 수로 계산
SELECT
    EVENT_JSON:panel::string  AS panel,
    EVENT_JSON:button::string AS button,
    COUNT(DISTINCT USER_ID)   AS users_clicked,
    AVG(EVENT_JSON:day::int)  AS avg_first_click_day
FROM EVENTS
WHERE EVENT_NAME = 'btn_clicked'
  AND EVENT_JSON:is_first_time::boolean = TRUE
GROUP BY 1, 2
ORDER BY 3;
```

### G24 — 배속 설정 분포

- **재는 것** — 유저가 어떤 배속 값으로 바꾸는지의 분포. 기본 게임 속도가 적절한지 판단하는 지표다.
- **읽는 법** — `changes`는 변경 횟수, `users`는 변경한 적 있는 유저 수. 둘을 나눠 봐야 한다 — `users`가 전체 유저 대비 적으면 배속 버튼 **자체를 못 찾은** 것이라 분포를 해석할 근거가 없다.
- **해석** — 최대 배속에 몰려 있으면 기본 속도가 느리다는 뜻 → 기본값 상향이나 자동 배속을 검토할 것. 최저 배속으로 되돌리는 경우가 많다면 빠른 속도에서 놓치는 정보(방문자 등장, 이벤트)가 있다는 신호다.
- **활용** — `EVENT_JSON:phase::string`은 이 이벤트에 없지만 `day`는 있으므로, `day`나 `game_time_min`과 교차하면 하루 중 어느 구간에서 속도를 올리는지(= 지루한 구간이 어디인지) 볼 수 있다.

```sql
SELECT
    EVENT_JSON:speed_multiplier::float AS speed,
    COUNT(*) AS changes,
    COUNT(DISTINCT USER_ID) AS users
FROM EVENTS
WHERE EVENT_NAME = 'speed_changed'
GROUP BY 1
ORDER BY 1;
```

---

## 패널 / 버튼 이벤트 보조 쿼리

`panel_opened` / `panel_closed` / `btn_clicked`가 붙으면서 목표 27개가 전부 측정 가능해졌다. 아래는 특정 목표에 묶이지 않는 범용 쿼리다.

```sql
-- 패널별 진입 수와 평균 체류 시간 (어느 화면에 오래 머무는가)
SELECT
    EVENT_JSON:panel::string AS panel,
    COUNT(*) AS closes,
    AVG(EVENT_JSON:duration_sec::int)    AS avg_sec,
    MEDIAN(EVENT_JSON:duration_sec::int) AS median_sec
FROM EVENTS
WHERE EVENT_NAME = 'panel_closed'
  AND EVENT_JSON:duration_sec IS NOT NULL
GROUP BY 1
ORDER BY 2 DESC;
```

```sql
-- 패널 발견율: 전체 유저 대비 그 패널을 한 번이라도 연 유저 비율
WITH total AS (
    SELECT COUNT(DISTINCT USER_ID) AS users FROM EVENTS
)
SELECT
    EVENT_JSON:panel::string AS panel,
    COUNT(DISTINCT USER_ID) AS reached_users,
    COUNT(DISTINCT USER_ID) / NULLIF((SELECT users FROM total), 0) AS reach_rate
FROM EVENTS
WHERE EVENT_NAME = 'panel_opened'
GROUP BY 1
ORDER BY 3;
```

정렬 상단(= 도달율이 낮은 패널)이 곧 발견되지 않는 기능이다.

## 운영 팁

- 쿼리는 SQL Data Explorer에서 저장해두고 재사용할 것. 자주 보는 것은 차트로 만들어 **Custom Dashboard**에 고정.
- 무료 티어(월 5만 MAU 이하)는 쿼리 시간 기준 공정 사용량 내 무료. 전체 기간 스캔보다 `EVENT_DATE` 필터를 붙이는 습관을 들이면 쿼리가 빠르고 사용량도 아낀다.
- 표본이 적을 때(HAVING COUNT(*) >= 10 등) 비율 해석에 주의 — 출시 초기엔 건수부터 확인.
- 이벤트/파라미터를 코드에서 바꾸면 이 문서의 쿼리도 같이 갱신할 것.

---

## Related

- [[Analytics]] — Analytics 허브
- [[분석목표]] — 각 쿼리가 답하는 G번호 정의
- [[이벤트_스펙]] · [[버튼_이벤트]] — 쿼리가 참조하는 파라미터 정의
- [[퍼널]] — 전환율은 SQL보다 Funnel 기능이 편하다
- [[Balance]] — 실측 성공률·경제 지표는 밸런싱 근거가 된다
