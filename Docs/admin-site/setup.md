# Setup — 어드민 사이트 설치 / 환경변수 / 배포

처음 설정할 때 한 번만 진행하는 절차. 일상 운영은 [operations.md](operations.md) 참고.

## 1. 사전 준비

- Python 3.11+
- Unity Dashboard 프로젝트(Project ID, Environment ID 확보 가능한 상태)
- Google Cloud Console 접근 권한 (OAuth 2.0 Client 발급용)
- 호스팅 계정 (Fly.io / Railway / Render 중 택 1)

## 2. Unity Service Account 발급

어드민 사이트가 Unity Cloud Save / Leaderboards Admin REST API 를 호출하기 위한 자격증명.

1. Unity Dashboard → 프로젝트 → 좌측 **Administration** → **Service Accounts**
2. **Create Service Account** 클릭 → 이름 입력 (예: `admin-site`)
3. 권한 부여:
   - **Cloud Save Admin** (Read + Write)
   - **Leaderboards Admin** (Read + Write)
4. **Create Key** → `Key ID` 와 `Secret Key` 를 안전한 곳에 복사
   - ⚠️ Secret Key 는 발급 시 한 번만 표시됨. 분실 시 새 키 발급 필요.

확보해 둘 값:
```
UNITY_PROJECT_ID         = <Dashboard URL 의 프로젝트 ID>
UNITY_ENV_ID             = <Environments 메뉴의 production 환경 ID>
UNITY_SERVICE_KEY_ID     = <위에서 발급받은 Key ID>
UNITY_SERVICE_SECRET_KEY = <위에서 발급받은 Secret Key>
UNITY_LEADERBOARD_ID     = final-days
```

## 3. Google OAuth Client 발급

운영자 로그인용.

1. https://console.cloud.google.com → 프로젝트 선택 (또는 새로 생성)
2. **APIs & Services** → **OAuth consent screen**
   - User Type: External (개인 운영이면)
   - App name, support email 입력
   - Scopes: `email`, `profile`, `openid`
   - Test users: 운영자 본인 이메일 추가 (앱 미게시 상태에서는 등록된 사용자만 로그인 가능)
3. **APIs & Services** → **Credentials** → **Create Credentials** → **OAuth client ID**
   - Application type: **Web application**
   - Authorized redirect URIs:
     - 로컬: `http://localhost:8000/auth/callback`
     - 배포: `https://<your-admin-host>/auth/callback`
   - 발급되는 `Client ID`, `Client Secret` 복사

확보해 둘 값:
```
GOOGLE_CLIENT_ID         = <발급받은 Client ID>
GOOGLE_CLIENT_SECRET     = <발급받은 Client Secret>
```

## 4. 어드민 사이트 로컬 설치

> ⚠️ 본 문서 작성 시점에는 `admin-site/` 디렉터리가 아직 만들어지지 않았다. 코드 작성 후 이 절차를 따른다.

```powershell
cd c:\Users\dldud\GitHub\Magic-Rental-Shop\admin-site
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Copy-Item .env.example .env
```

`.env` 파일을 열어 위 단계들에서 확보한 값들로 채운다:

```
# 서버
SESSION_SECRET=<openssl rand -hex 32 또는 임의의 긴 랜덤 문자열>
PORT=8000
PUBLIC_BASE_URL=http://localhost:8000

# Google OAuth
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
ADMIN_ALLOWED_EMAILS=<운영자 이메일>

# Unity Services
UNITY_PROJECT_ID=...
UNITY_ENV_ID=...
UNITY_SERVICE_KEY_ID=...
UNITY_SERVICE_SECRET_KEY=...
UNITY_LEADERBOARD_ID=final-days
```

### 환경변수 설명

| 변수 | 필수 | 설명 |
|---|---|---|
| `SESSION_SECRET` | ✅ | Starlette 세션 쿠키 서명 키. 노출 시 세션 위조 가능 → 안전하게 보관 |
| `PORT` | — | 기본 8000 |
| `PUBLIC_BASE_URL` | ✅ | OAuth redirect_uri 구성에 사용. 로컬은 `http://localhost:8000`, 배포는 `https://<host>` |
| `GOOGLE_CLIENT_ID` | ✅ | OAuth Client ID |
| `GOOGLE_CLIENT_SECRET` | ✅ | OAuth Client Secret |
| `ADMIN_ALLOWED_EMAILS` | ✅ | 콤마 구분된 허용 이메일 목록. 비어 있으면 아무도 로그인 못 함 |
| `UNITY_PROJECT_ID` | ✅ | Unity 프로젝트 ID |
| `UNITY_ENV_ID` | ✅ | Unity 환경 ID (production) |
| `UNITY_SERVICE_KEY_ID` | ✅ | Service Account Key ID |
| `UNITY_SERVICE_SECRET_KEY` | ✅ | Service Account Secret Key |
| `UNITY_LEADERBOARD_ID` | — | 기본 `final-days` |

## 5. 로컬 실행

```powershell
uvicorn app.main:app --reload --port 8000
```

브라우저로 `http://localhost:8000` 접속 → Google 로그인 페이지로 리다이렉트 → 허용 이메일로 로그인.

헬스체크:
```powershell
curl http://localhost:8000/healthz
# {"ok": true}
```

## 6. 배포 (Fly.io 예시)

> 다른 PaaS (Railway / Render / Cloud Run) 도 Docker 이미지 기반이라 절차는 유사.

### 6.1 Fly CLI 설치

```powershell
iwr https://fly.io/install.ps1 -useb | iex
flyctl auth login
```

### 6.2 앱 생성

```powershell
cd admin-site
flyctl launch  # 대화형. Dockerfile 자동 감지. Postgres/Redis 는 아니오 선택.
```

### 6.3 시크릿 주입

```powershell
flyctl secrets set `
  SESSION_SECRET="..." `
  GOOGLE_CLIENT_ID="..." `
  GOOGLE_CLIENT_SECRET="..." `
  ADMIN_ALLOWED_EMAILS="<운영자 이메일>" `
  UNITY_PROJECT_ID="..." `
  UNITY_ENV_ID="..." `
  UNITY_SERVICE_KEY_ID="..." `
  UNITY_SERVICE_SECRET_KEY="..." `
  PUBLIC_BASE_URL="https://<your-app>.fly.dev"
```

### 6.4 배포

```powershell
flyctl deploy
```

배포 완료 후 출력되는 URL 을 Google OAuth Console 의 **Authorized redirect URIs** 에 추가:
```
https://<your-app>.fly.dev/auth/callback
```

### 6.5 동작 확인

```powershell
curl https://<your-app>.fly.dev/healthz
# {"ok": true}
```

브라우저로 같은 URL 접속 → Google 로그인 흐름 동작 확인.

## 7. 허용 이메일 추가

운영자가 늘어날 때:

1. `flyctl secrets set ADMIN_ALLOWED_EMAILS="a@x.com,b@y.com,c@z.com"` (콤마 구분)
2. Google OAuth consent screen 의 Test users 에도 추가 (앱 미게시 상태에서)
3. 자동 재배포됨

## 8. 키 회전 (보안 권장)

### Unity Service Account
1. Dashboard → Service Accounts → 새 Key 발급
2. `flyctl secrets set UNITY_SERVICE_KEY_ID=... UNITY_SERVICE_SECRET_KEY=...`
3. 동작 확인 후 옛 Key 삭제

### SESSION_SECRET
- 변경하면 모든 운영자 세션이 즉시 무효 → 다시 로그인 필요
- `flyctl secrets set SESSION_SECRET=...`

## 9. 트러블슈팅

| 증상 | 원인 후보 | 확인 |
|---|---|---|
| `/auth/callback` 에서 redirect_uri_mismatch | Google Console 의 redirect URI 와 `PUBLIC_BASE_URL` 불일치 | 두 값이 정확히 같은지 (http/https, 끝 슬래시) |
| 로그인 직후 "허용되지 않은 계정" | 이메일이 화이트리스트에 없음 또는 `email_verified=false` | `ADMIN_ALLOWED_EMAILS` 값 확인 |
| `POST /api/bans` 가 502 | Unity Service Account 권한 부족 | Cloud Save Admin + Leaderboards Admin 권한 체크 |
| `getPrivateItems is not a function` | UGS SDK 메서드명 불일치 | `app/integrations/unity_client.py` 의 메서드 이름을 SDK 문서대로 조정 |

상세는 [operations.md](operations.md) 의 트러블슈팅 절 참고.
