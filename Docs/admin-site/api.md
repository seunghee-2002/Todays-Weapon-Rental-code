# API Reference — 어드민 사이트 REST API

모든 응답은 JSON. `/healthz` 외에는 **인증 필수** (세션 쿠키).

## 공통 사항

### Base URL

- 로컬: `http://localhost:8000`
- 배포: `https://<your-admin-host>`

### 인증

- 운영자는 `GET /auth/login` 으로 Google OAuth 흐름 시작
- 콜백 후 `Set-Cookie: session=...; HttpOnly; Secure; SameSite=Lax`
- 이후 `/api/*` 호출 시 세션 쿠키 자동 전송 (브라우저)
- 외부 도구(`curl`)에서는 세션 쿠키를 수동으로 전송해야 함

### 공통 응답 코드

| HTTP | 의미 |
|---|---|
| 200 | 성공 |
| 401 | 인증 실패 (세션 없음/만료/화이트리스트 불일치) |
| 404 | 리소스 없음 (PlayerId 미존재 등) |
| 422 | 요청 body validation 실패 |
| 502 | Unity Admin API 오류 (서비스 계정 권한 부족 등) |

### 공통 에러 응답

```json
{
  "error": "에러 코드 문자열",
  "message": "사람이 읽을 메시지",
  "detail": { /* 상황별 추가 정보 */ }
}
```

---

## 1. `POST /api/bans` — ban 적용

플레이어를 ban 하고 리더보드 점수를 자동 삭제한다.

### 요청

```http
POST /api/bans
Content-Type: application/json

{
  "playerId": "VMXEOngt6PgHMRe1bVoKDJ0LopYw",
  "reason":   "치트 사용",
  "until":    null
}
```

| 필드 | 타입 | 필수 | 설명 |
|---|---|---|---|
| `playerId` | string | ✅ | Unity Authentication PlayerId (28자 영숫자) |
| `reason` | string | ✅ | 사용자에게 표시될 사유 (한국어 가능, 1~200자) |
| `until` | number\|null | — | 자동 해제 시각 (ms epoch). 영구 ban 이면 `null` 또는 생략 |

### 응답 (200)

```json
{
  "ok": true,
  "ban": {
    "playerId": "VMXEOngt6PgHMRe1bVoKDJ0LopYw",
    "reason":   "치트 사용",
    "until":    0
  },
  "scoreDeleted": true
}
```

`scoreDeleted` 가 `false` 면: 점수가 처음부터 없었거나 Leaderboards API 가 일시 장애 (ban 자체는 적용됨).

### 부작용

- Unity Cloud Save Player Data 의 Private 영역에 3개 키 set:
  - `banned = "true"`
  - `banReason = <reason>`
  - `banUntil = <until ms epoch as string>` (until 지정 시)
- Unity Leaderboards 의 `final-days` 에서 해당 PlayerId 점수 삭제

### curl 예시

```bash
curl -X POST https://admin.example.com/api/bans \
  -H "Content-Type: application/json" \
  -H "Cookie: session=..." \
  -d '{
    "playerId": "VMXEOngt6PgHMRe1bVoKDJ0LopYw",
    "reason": "치트 사용"
  }'
```

기간 ban 예시 (2026-05-25 12:00 KST 까지):
```bash
# 1748145600000 = 2026-05-25 12:00 KST = 2026-05-25T03:00:00Z 의 ms epoch
curl -X POST ... -d '{
  "playerId": "VMXEOngt6PgHMRe1bVoKDJ0LopYw",
  "reason": "일시 정지",
  "until": 1748145600000
}'
```

---

## 2. `DELETE /api/bans/{playerId}` — ban 해제

`banned` / `banReason` / `banUntil` 3개 키 모두 삭제.

### 요청

```http
DELETE /api/bans/VMXEOngt6PgHMRe1bVoKDJ0LopYw
```

### 응답 (200)

```json
{
  "ok": true,
  "deletedKeys": ["banned", "banReason", "banUntil"]
}
```

키가 일부만 존재해도 best-effort 로 삭제. 모두 없으면 `deletedKeys` 가 빈 배열.

### 주의

- 이미 자동 삭제된 리더보드 점수는 **복구되지 않는다**. 사용자가 다시 게임을 클리어해야 새 점수가 등재됨.
- 게임 클라이언트는 다음 앱 실행 또는 네트워크 재연결 시 `checkBanStatus` Cloud Code 를 호출해 자동으로 ban 해제를 감지.

---

## 3. `GET /api/bans/{playerId}` — ban 상태 조회

### 요청

```http
GET /api/bans/VMXEOngt6PgHMRe1bVoKDJ0LopYw
```

### 응답 (200) — ban 인 경우

```json
{
  "banned": true,
  "reason": "치트 사용",
  "until":  0
}
```

### 응답 (200) — ban 이 아닌 경우

```json
{
  "banned": false,
  "reason": "",
  "until":  0
}
```

> 이 엔드포인트는 게임 측 `checkBanStatus` Cloud Code 와 동일한 로직(Private 1차 + Default fallback)을 사용한다. 즉 결과가 정확히 일치한다.

---

## 4. `GET /api/players/{playerId}` — 플레이어 기본정보

ban 적용 전 PlayerId 가 맞는지 확인 / 닉네임 표시용.

### 요청

```http
GET /api/players/VMXEOngt6PgHMRe1bVoKDJ0LopYw
```

### 응답 (200)

```json
{
  "playerId":   "VMXEOngt6PgHMRe1bVoKDJ0LopYw",
  "nickname":   "PlayerVMXEOn",
  "banned":     false,
  "currentScore": 29
}
```

| 필드 | 비고 |
|---|---|
| `nickname` | Cloud Save 의 `playerNickname` (Default access). 없으면 빈 문자열 |
| `banned` | ban 상태 (위 3번 엔드포인트와 동일) |
| `currentScore` | 현재 `final-days` 리더보드 점수. 미등록이면 `null` |

### 응답 (404) — PlayerId 없음

```json
{
  "error":   "PLAYER_NOT_FOUND",
  "message": "해당 PlayerId 에 대한 데이터가 없습니다."
}
```

---

## 5. `GET /api/leaderboard` — 리더보드 조회

ban 후보를 찾을 때 사용. 상위 N 개 항목을 조회.

### 요청

```http
GET /api/leaderboard?limit=100&offset=0
```

| 쿼리 파라미터 | 기본값 | 범위 |
|---|---|---|
| `limit` | 100 | 1~100 |
| `offset` | 0 | 0~10000 |

### 응답 (200)

```json
{
  "leaderboardId": "final-days",
  "entries": [
    { "rank": 1, "playerId": "AbCd...", "nickname": "PlayerAbCd", "score": 99 },
    { "rank": 2, "playerId": "EfGh...", "nickname": "PlayerEfGh", "score": 87 }
  ],
  "total": 234
}
```

### 응답 (502)

Unity Leaderboards Admin API 실패 시.

```json
{
  "error":   "UNITY_API_ERROR",
  "message": "Leaderboards Admin API 호출에 실패했습니다.",
  "detail":  { "unityStatus": 503 }
}
```

---

## 6. `GET /healthz` — 헬스체크 (인증 불필요)

### 요청

```http
GET /healthz
```

### 응답 (200)

```json
{ "ok": true }
```

배포 플랫폼의 liveness/readiness probe 용도. 인증 미들웨어를 우회한다.

---

## 7. `GET /auth/login` / `GET /auth/callback`

Google OAuth 흐름. 사람 운영자가 브라우저로 사용. API 호출용 아님.

### `/auth/login`
→ Google 동의 화면으로 302 redirect.

### `/auth/callback?code=...&state=...`
→ 토큰 교환 → 이메일 화이트리스트 검증 →
- 성공: 세션 쿠키 set 후 `/` 로 redirect
- 실패: 401 + 안내 페이지

---

## 향후 추가 예정 엔드포인트 (참고)

[architecture.md](architecture.md) 의 "향후 확장 포인트" 에서 예고된 것들.

| 엔드포인트 | 단계 | 비고 |
|---|---|---|
| `GET /api/audit?actor=&action=&from=&to=` | 감사 로그 DB 단계 | ban 이력 조회 |
| `POST /api/events/suspicious` | Cloud Code webhook 단계 | 게임 서버에서 의심 이벤트 push |
| `POST /api/auto-ban-policies` | 자동 ban 정책 단계 | 룰 등록 |
| `GET /api/players/search?nickname=` | DB 인덱스 단계 | 닉네임 검색 |

각 엔드포인트는 추가 시 본 문서에 절을 늘려 기록한다.
