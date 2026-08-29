# Architecture — 어드민 사이트 시스템 설계

## 1. 시스템 다이어그램

```
┌────────────────────┐                ┌────────────────────────────┐
│  Browser (운영자)   │                │  Unity 게임 클라이언트       │
│  · index.html      │                │  · UGSManager              │
│  · app.js (fetch)  │                │  · BanManager              │
└─────────┬──────────┘                │  · LeaderboardManager      │
          │                           └─────────┬──────────────────┘
   HTTPS  │ 세션 쿠키                            │ player auth (익명)
   (Google OAuth)                                │
          ▼                                      ▼
┌─────────────────────────────┐         ┌──────────────────────────┐
│  FastAPI 어드민 백엔드        │         │  UGS Cloud Code Scripts   │
│   /auth/login,/callback     │         │   · checkBanStatus.js     │
│   /api/bans  (POST/DELETE/  │         │   · startGameSession.js   │
│              GET)           │         │   · submitLeaderboardScore.js
│   /api/players              │         └──────┬───────────────────┘
│   /api/leaderboard          │                │ service auth
│   /healthz                  │                │
└──────┬──────────────────────┘                │
       │ Service Account (Basic Auth)          │
       │                                       │
       ▼                                       ▼
┌──────────────────────────────────────────────────────────────────┐
│                      Unity Services Admin REST API                │
│  ┌─────────────────────────┐    ┌──────────────────────────────┐ │
│  │  Cloud Save             │    │  Leaderboards                │ │
│  │  · Private items R/W    │◄───┤  · Score read / delete        │ │
│  │  · Default items R      │    │  · final-days 리더보드        │ │
│  └─────────────────────────┘    └──────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

핵심: **양측이 같은 Cloud Save 키를 본다**. 어드민 사이트와 게임 클라이언트는 직접 통신하지 않고, Unity Cloud Save 가 둘 사이의 데이터 동기화 매개체 역할.

## 2. 진실의 소스 (Source of Truth)

| 데이터 | 위치 | access class | R/W 권한 |
|---|---|---|---|
| `banned` (`"true"` / 없음) | Unity Cloud Save Player Data | private | Cloud Code/Admin API 만 R/W |
| `banReason` (string) | 동일 | private | 동일 |
| `banUntil` (ms epoch string) | 동일 | private | 동일 |

> **선택 이유**: Default access 면 게임 클라이언트가 `banned` 를 `"false"` 로 덮어쓰는 우회가 가능. Private 은 서버(Cloud Code/Admin API) 만 R/W 하므로 클라 변조 불가.
>
> 다만 현재 Cloud Code `checkBanStatus.js` 는 Private 1차 + Default 2차(fallback) 조회 — 운영자가 어디에 키를 두든 동작하도록. 보안 강화 단계에서 default fallback 제거 가능.

## 3. 데이터 흐름

### 3.1 운영자 ban 적용 흐름

```
1. 운영자: 어드민 UI 에서 PlayerId + reason 입력 → "BAN" 클릭
2. 브라우저:  POST /api/bans  { playerId, reason, until? }
3. 어드민:    require_admin 의존성으로 세션 검증
4. 어드민:    ban_service.apply_ban(actor_email, playerId, reason, until)
5. 서비스:    unity_client.set_private_items(playerId, {banned, banReason, banUntil})
                ↓ Unity Cloud Save Admin REST API
                  POST /cloud-save/v1/data/projects/{p}/environments/{e}/players/{playerId}/items
6. 서비스:    unity_client.delete_leaderboard_score(playerId)
                ↓ Unity Leaderboards Admin REST API
                  DELETE /leaderboards/v1/.../final-days/scores/players/{playerId}
7. 어드민:    { ok: true, ban: {...}, scoreDeleted: true } 응답
8. 브라우저:  UI 업데이트 (해당 플레이어 행에 "BANNED" 배지)
```

### 3.2 사용자 ban 인지 흐름 (게임 측, 참고)

```
A. 앱 실행 시:
   UGSManager.SignInAnonymouslyAsync() 완료
        ↓
   BanManager.CheckBanStatusAsync()
        ↓
   CloudCode "checkBanStatus" 호출 → { banned, reason, until }
        ↓
   banned=true 면 BanManager.NotifyBanned
        ↓
   MainMenuController 가 OnBanDetected 이벤트 받음
        ↓
   MainConfirmPopupController.ShowMessage("이 계정은 이용이 제한되었습니다...")

B. 랭킹 제출 시:
   GameManager.OnGameOver() → LeaderboardManager.SubmitScoreAsync
        ↓
   CloudCode "submitLeaderboardScore" 호출
        ↓
   서버: checkBanStatus → banned=true 면 applicationErrorCode=2006 throw
        ↓
   클라: CloudCodeException catch → TryExtractBanInfo (e.Message regex)
        ↓
   HandleBannedInternal → 보류 점수/세션 토큰 폐기 + BanManager.NotifyBanned
```

게임 측 ban 시스템은 본 어드민 사이트 없이도 Unity Dashboard 만으로 동일하게 동작 (백업 경로).

### 3.3 ban 해제 흐름

```
1. 운영자:    어드민 UI 에서 해당 플레이어 → "UNBAN" 클릭
2. 브라우저:  DELETE /api/bans/{playerId}
3. 어드민:    require_admin → ban_service.lift_ban(actor_email, playerId)
4. 서비스:    unity_client.delete_private_items(playerId, ["banned", "banReason", "banUntil"])
5. 응답:     { ok: true }
6. 게임 측:   다음 앱 실행 또는 네트워크 재연결 시 checkBanStatus 가
              banned=false 응답 → BanManager.ClearBan → 정상 진입
```

`banUntil` 자동 만료도 별도 트리거 없이 동작 — Cloud Code 가 `Date.now() >= banUntil` 이면 통과시킨다.

## 4. 인증 모델

### 4.1 어드민 사이트 ← 운영자 브라우저

**Google OAuth + 이메일 화이트리스트** (authlib + Starlette session middleware).

```
1. /auth/login    → Google 동의 화면으로 리다이렉트 (state 파라미터로 CSRF 방어)
2. /auth/callback → 토큰 교환 → email 추출 → 화이트리스트 검증 → 세션 쿠키 발급
3. /api/*         → require_admin 의존성이 세션 검증 (미인증 시 401)
4. /healthz       → 인증 없이 접근 가능 (헬스체크용)
```

세션 쿠키 속성:
- `HttpOnly` (JS 접근 불가)
- `Secure` (HTTPS 전용, 배포 환경)
- `SameSite=Lax` (CSRF 부분 방어)

### 4.2 어드민 사이트 → Unity Services

**Service Account Basic Auth**.

- Unity Dashboard → Administration → Service Accounts 에서 키 발급
- 권한: **Cloud Save Admin** + **Leaderboards Admin**
- 환경변수: `UNITY_SERVICE_KEY_ID`, `UNITY_SERVICE_SECRET_KEY`
- 헤더: `Authorization: Basic base64(keyId:secret)`

> **절대 클라이언트(브라우저) JS 에 노출되지 않음**. 모든 Unity Admin API 호출은 FastAPI 백엔드에서 수행하고, 결과만 운영자에게 JSON 으로 전달.

## 5. 모듈 분리 (확장성 핵심)

```
routes/    ← HTTP 입출력만. 입력 검증 + 의존성 주입.
services/  ← 도메인 로직. 외부 시스템 호출 조합.
integrations/  ← 외부 시스템 클라이언트. HTTP/라이브러리 추상화.
auth/      ← 인증/인가.
logs/      ← (placeholder) 향후 로그 분석.
```

각 라우터는 `services/` 의 함수만 호출하고, `services/` 가 `integrations/` 와 (향후) DB 를 조합한다. 이 분리 덕분에:

- **테스트**: `services/` 는 `integrations/` 를 mock 해서 단위 테스트 가능
- **확장**: 새 운영 액션(닉네임 강제 변경, 경고 등) → 새 service 파일 + 새 routes 파일만 추가
- **DB 추가**: `ban_service` 의 함수 시그니처에 `actor_email`/`timestamp` 가 이미 있음 → DB 기록만 추가하면 호출부 수정 불필요

## 6. 향후 확장 포인트

본 MVP 안정화 후 다음 단계가 자연스럽게 이어지도록 설계되어 있다.

### 6.1 감사 로그 / ban 이력 DB

- **목적**: 누가/언제/왜 ban 했는지 기록 + 운영자 별 활동 통계
- **구현 위치**:
  - `app/db/` 신규 — SQLAlchemy/asyncpg
  - `services/ban_service.py` 의 `apply_ban`/`lift_ban` 끝에 `await audit_repo.insert(...)` 한 줄 추가
- **DB**: Supabase(Postgres) 추천 — 무료 티어 + 인증/REST 자동
- **테이블**:
  ```
  ban_audit (id, actor_email, action, player_id, reason, until, created_at)
  ```

### 6.2 Unity Analytics → BigQuery export

- **목적**: 어뷰저 패턴 자동 탐지 (짧은 플레이타임 + 고점수, 동일 IP 다계정 등)
- **구현 위치**:
  - `app/logs/etl.py` — 일별 ETL (cron 또는 GitHub Actions)
  - `app/logs/rules.py` — 패턴 룰 정의
  - `app/logs/dashboard.py` — 운영자 UI

### 6.3 Cloud Code 의심 이벤트 webhook

- **목적**: 시간 위반(2004) 같은 의심 이벤트를 실시간으로 어드민 사이트로 push
- **구현**:
  - `submitLeaderboardScore.js` 의 catch 블록에 `await fetch(SUSPICIOUS_WEBHOOK_URL, ...)` 한 줄 추가
  - 어드민 사이트에 `/api/events/suspicious` 라우터 + DB 적재 + 알림

### 6.4 자동 ban 정책

- **목적**: 룰엔진으로 어뷰저 자동 ban
- **구현**:
  - `services/auto_ban_policy.py` — 룰 정의 (예: 1초당 100일 진행)
  - `ban_service.apply_auto_ban(actor="system", reason, evidence)` 호출
  - actor 가 사람이 아닌 "system" 인 ban 은 운영자가 별도로 검토할 수 있게 audit 에 표시

### 6.5 ban 이력 페이지

- DB 가 갖춰지면 자연스럽게 추가되는 UI
- `routes/audit.py` + `static/audit.html`

각 확장은 **routes/services/integrations 분리 덕분에 기존 코드를 수정하지 않고 추가만 하면 된다**.

## 7. 보안 체크리스트

- [ ] 어드민 사이트의 비-인증 경로는 `/healthz` 와 `/auth/*` 만
- [ ] Google OAuth `state` 파라미터로 CSRF 방어 (authlib 기본)
- [ ] 세션 쿠키: `HttpOnly + Secure + SameSite=Lax`
- [ ] `email_verified=true` 인 Google 계정만 화이트리스트 검사 통과
- [ ] Unity Service Account 시크릿은 환경변수에만 보관, 응답에 절대 포함 금지
- [ ] HTTPS 강제 (호스팅 플랫폼이 자동 처리)
- [ ] (선택) 레이트 리미트 — `slowapi`. 운영자가 다수일 때만 필요

## 8. 알려진 제약

- **어드민 사이트 다운 시**: Unity Dashboard 의 Cloud Save 화면에서 직접 키 조작 가능. [operations.md](operations.md) 의 "Unity Dashboard 백업 경로" 참고.
- **닉네임 검색 미지원**: Unity Cloud Save Admin API 는 PlayerId 기반 조회만 지원. 닉네임으로 검색하려면 별도 DB 인덱스가 필요 → 로그 분석 단계에서 자연스럽게 추가 가능.
- **점수 복구 불가**: ban 시 자동 삭제된 리더보드 점수는 영구 소실. 해제해도 과거 점수는 안 돌아옴.
