# Operations — 운영자 가이드

일상적인 운영 작업 절차. 처음 설치는 [setup.md](setup.md), API 상세는 [api.md](api.md) 참고.

## 1. 일상 작업 흐름

### 1.1 어뷰저 발견 → ban 적용

가장 자주 하게 될 작업.

**경로 A — 어드민 사이트 (권장, 자동)**

1. `https://<your-admin-host>` 접속 → Google 로그인
2. 메인 페이지에서 **Leaderboard** 탭 또는 **Search Player** 사용
   - 리더보드 상위에서 의심스러운 점수 발견 → 항목 클릭
   - 또는 PlayerId 알면 직접 입력
3. 플레이어 상세 화면에서 **BAN** 버튼 클릭
4. 모달:
   - **사유** 입력 (예: "치트 사용", "비정상 점수")
   - **기간**: 영구 / 또는 특정 시각까지
5. 확인 → 자동으로:
   - Cloud Save Private 키 set
   - 리더보드 점수 자동 삭제
   - UI 에 "BANNED" 배지 표시

**경로 B — Unity Dashboard (백업, 어드민 사이트 다운 시)**

1. Unity Dashboard → 프로젝트 → **Leaderboards** → `final-days` 에서 PlayerId 복사
2. Unity Dashboard → **Cloud Save** → Player Data → PlayerId 검색
3. **Private** 영역에서 키 추가:
   - `banned` = `"true"` (string)
   - `banReason` = `"치트 사용"` 등 (string, 선택)
   - `banUntil` = `"1748145600000"` (string, ms epoch, 선택)
4. **Leaderboards** 페이지로 돌아가 해당 PlayerId 점수 수동 삭제

> 두 경로 모두 결과적으로 같은 Cloud Save 키를 조작하므로 결과는 동일. 어드민 사이트가 동작 중이면 점수 삭제까지 자동.

### 1.2 ban 해제

**경로 A — 어드민 사이트**

1. 플레이어 상세 화면 → **UNBAN** 버튼 클릭 → 확인
2. 자동으로 `banned` / `banReason` / `banUntil` 3개 키 삭제
3. 사용자는 다음 앱 실행 또는 네트워크 재연결 시 정상화 (앱 재시작 강제 불필요)

**경로 B — Unity Dashboard**

1. Cloud Save → Player Data → PlayerId 검색
2. Private 영역의 `banned` 키 **삭제** (필수)
3. `banReason`, `banUntil` 도 같이 삭제 (선택, 권장)

### 1.3 기간 ban 자동 만료

`banUntil` 만 설정해 두면 Cloud Code 가 `Date.now() >= banUntil` 일 때 자동 통과시킨다.

- `banned` 키 자체는 대시보드에 남아 있을 수 있음 — 기능적으로 문제 없음
- 운영자가 정리 주기에 잔여 키를 같이 삭제 가능 (선택)
- 기간 만료된 사용자는 게임 측에서 자동으로 정상 진입

## 2. 점수 자동 삭제에 대한 이해

| 시점 | 어디서 점수가 삭제되나 |
|---|---|
| 어드민 사이트에서 ban 적용 시 | 어드민 백엔드가 `DELETE .../leaderboards/.../scores/players/{id}` 즉시 호출 |
| Unity Dashboard 로 키만 추가 시 | 사용자가 다음에 게임 실행 → `checkBanStatus` Cloud Code 가 ban 감지 → 자동 삭제 |
| 점수 등재 시도 시 | `submitLeaderboardScore` 가 ban 감지 → 등재 차단 (이미 삭제됐을 가능성 큼) |

**점수 복구는 불가능**. 한 번 삭제된 리더보드 점수는 영구 소실. 해제해도 사용자가 직접 다시 클리어해서 등재해야 함.

## 3. 허용 이메일 추가 (운영자 늘리기)

운영자가 1명에서 여러 명으로 늘 때.

1. Google OAuth Console 의 **OAuth consent screen** → **Test users** 에 새 이메일 추가 (앱 미게시 상태일 때만 필요)
2. 호스팅 플랫폼에서 환경변수 업데이트:
   ```powershell
   flyctl secrets set ADMIN_ALLOWED_EMAILS="a@x.com,b@y.com,c@z.com"
   ```
3. 자동 재배포되며 즉시 적용

> ⚠️ 이메일 추가는 신중히. 추가된 사람은 모든 플레이어를 ban/해제할 수 있다.

## 4. PlayerId 찾기

| 출처 | 방법 |
|---|---|
| 리더보드 항목 | Unity Dashboard → Leaderboards → `final-days` 의 각 행에 PlayerId 노출 |
| 사용자 신고 | 사용자에게 게임 내 옵션 화면에서 자기 PlayerId 를 보내 달라고 요청 |
| 게임 로그 | Unity Cloud Code Logs 에서 의심 이벤트의 `playerId` 필드 |
| 닉네임만 알 때 | **현재 미지원** — Cloud Save Admin API 는 닉네임 인덱스가 없음. DB 단계에서 추가 가능 |

## 5. 트러블슈팅

### 5.1 ban 적용했는데 사용자가 여전히 게임 진입함

확인 순서:
1. 어드민 사이트의 `GET /api/bans/{playerId}` 로 `banned: true` 응답 확인
2. 사용자 측: 게임 클라이언트가 ban 검사 응답을 받았는지
   - Unity Cloud Code Logs → `checkBanStatus` 스크립트 → 최근 호출에 `BANNED 응답` 로그 확인
   - 안 보이면 사용자가 앱을 켜지 않았거나 오프라인
3. 사용자에게 앱 강제 종료 후 재실행 요청 (앱 켜져 있는 동안엔 검사 시점이 제한적)

### 5.2 어드민 사이트 로그인이 안 됨 — "허용되지 않은 계정"

- `ADMIN_ALLOWED_EMAILS` 환경변수에 해당 이메일이 정확히 포함됐는지 (대소문자, 콤마 구분 공백 없음)
- Google 계정의 `email_verified` 가 `true` 인지 (보통 자동 verified)
- 호스팅 플랫폼의 환경변수 변경 후 재배포 됐는지

### 5.3 `POST /api/bans` 가 502

- Unity Service Account 권한 부족: Dashboard → Service Accounts → 해당 키에 **Cloud Save Admin** + **Leaderboards Admin** 둘 다 있는지 확인
- Key/Secret 회전 후 환경변수 미반영: `flyctl secrets list` 로 확인

### 5.4 게임 측 ScriptError 빨간 로그 (`submitLeaderboardScore`)

ban 된 사용자가 점수 제출 시도하면 Editor 콘솔에 빨간 ScriptError 가 찍힘.

```
ScriptError (422) ... Error: {"reason":"...","until":0}
```

이건 **UGS Cloud Code SDK 가 내부에서 4xx 응답을 받으면 무조건 LogError 로 한 줄 찍는 동작**. ban 만의 문제가 아니라 기존 에러 코드(2001~2005) 시에도 동일. 빌드된 게임에선 사용자에게 노출 안 됨 → **무시해도 됨**.

직후에 `[LeaderboardManager] BANNED (SubmitScore): reason=..., until=...` 노란 LogWarning 이 찍히면 정상 처리된 것.

## 6. 키 회전 / 보안 유지

| 자산 | 회전 주기 권장 | 절차 |
|---|---|---|
| Unity Service Account 키 | 6개월 또는 운영자 변경 시 | Dashboard 에서 새 Key 발급 → 환경변수 교체 → 동작 확인 → 옛 Key 삭제 |
| `SESSION_SECRET` | 운영자 변경 시 또는 의심 정황 시 | 환경변수 교체 → 모든 운영자가 다시 로그인 |
| `GOOGLE_CLIENT_SECRET` | 1년 또는 의심 정황 시 | Google Cloud Console 에서 새 Secret 발급 → 환경변수 교체 |

## 7. ban 운영 베스트 프랙티스

- **사유는 구체적으로**: "치트", "비정상" 같은 모호한 사유보다 "30초 이내 9999일 진행", "동일 계정 다중 점수 제출" 등 근거 명시 → 향후 항의/소명에 대응 가능
- **기간 vs 영구**: 첫 위반은 기간 ban (예: 24시간), 재범은 영구 ban 권장
- **해제 시 사유 메모**: 어드민 사이트가 stateless 라 해제 사유 자동 기록 안 됨. 별도 운영 메모에 기록 권장 (감사 로그 DB 단계까지)
- **사용자 메시지 친화적 작성**: 사유는 사용자에게 그대로 노출됨. 비방/욕설 금지

## 8. 일상 점검 체크리스트

매주 1회 권장.

- [ ] Unity Dashboard → Leaderboards → `final-days` 상위 10명 점검 (이상치 확인)
- [ ] Unity Dashboard → Cloud Code → 각 스크립트의 Logs 탭에서 error 로그 빈도 확인
- [ ] 어드민 사이트의 헬스체크: `curl https://<host>/healthz`
- [ ] 자기 자신의 Google 세션이 정상 동작하는지 (로그인 → ban 조회 → 로그아웃)

## 9. 사용자 항의 대응 스크립트

ban 된 사용자가 항의 메시지를 보냈을 때.

1. PlayerId 확보 (옵션 화면 또는 닉네임 정보)
2. `GET /api/bans/{playerId}` 로 현재 ban 상태 확인
3. `GET /api/players/{playerId}` 로 현재 닉네임/점수 확인
4. 판단:
   - **부당 ban (오인)**: `DELETE /api/bans/{playerId}` 로 해제. 점수 복구 불가 사실 안내
   - **정당 ban**: 사유 그대로 설명. 영구 ban 이면 해제 거부, 기간 ban 이면 만료 시점 안내
5. 향후 동일 사용자가 재범할 경우를 위해 별도 메모에 기록 (감사 로그 DB 단계 전까지)

## 10. 비상 절차

### 10.1 어드민 사이트 완전 다운

- 모든 ban/해제 작업은 [1.1 경로 B](#11-어뷰저-발견--ban-적용) (Unity Dashboard 직접 조작) 로 수행
- 호스팅 플랫폼 상태 확인:
  ```powershell
  flyctl status
  flyctl logs
  ```
- 일반적으로 재배포 또는 환경변수 점검으로 해결

### 10.2 Unity Service Account 키 유출 의심

- 즉시 Dashboard 에서 해당 Key **삭제**
- 새 Key 발급 → 환경변수 교체 → 동작 확인
- Cloud Code Logs 에서 유출 키로 호출된 흔적이 있는지 확인

### 10.3 `SESSION_SECRET` 유출 의심

- 즉시 환경변수 변경 → 모든 세션 무효화
- 호스팅 플랫폼의 액세스 로그 점검

---

## 이 문서 외 운영 자료

- [README.md](README.md) — 어드민 사이트 개요
- [setup.md](setup.md) — 처음 설치 절차
- [architecture.md](architecture.md) — 시스템 설계 / 데이터 흐름
- [api.md](api.md) — REST API 레퍼런스
- 플랜 원본 (Claude 내부): `C:\Users\dldud\.claude\plans\swift-hopping-fern.md` — 전체 ban 시스템 설계 기록
