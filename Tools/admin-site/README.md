# admin-site (로컬 사용 가이드)

Today's Weapon Rental 어드민 사이트의 **로컬 실행 / 개발용** README.
상세 설계·운영 문서는 [`../Documents/admin-site/`](../Documents/admin-site/) 참고.

## 빠른 시작 (Windows PowerShell)

```powershell
cd admin-site
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Copy-Item .env.example .env
# .env 파일을 열어 값을 채운다 (../Documents/admin-site/setup.md 참고)
uvicorn app.main:app --reload --port 8000
```

`http://localhost:8000/healthz` → `{"ok": true}` 확인 후
`http://localhost:8000` 으로 브라우저 접속 → Google 로그인.

## 테스트

```powershell
pytest
```

## 디렉터리

```
app/
  main.py            FastAPI 엔트리포인트
  config.py          pydantic-settings 환경변수 로더
  auth/              Google OAuth + 화이트리스트 + require_admin 의존성
  integrations/      Unity Admin REST 클라이언트 (httpx)
  services/          도메인 로직 (ban_service)
  routes/            HTTP 라우터
  logs/              (향후 로그 분석 placeholder)
  static/            HTML + Vanilla JS + CSS
tests/               pytest
```

## 관련 문서

- [`../Documents/admin-site/README.md`](../Documents/admin-site/README.md) — 개요
- [`../Documents/admin-site/architecture.md`](../Documents/admin-site/architecture.md) — 설계
- [`../Documents/admin-site/setup.md`](../Documents/admin-site/setup.md) — 환경변수 / 발급 절차
- [`../Documents/admin-site/api.md`](../Documents/admin-site/api.md) — REST API
- [`../Documents/admin-site/operations.md`](../Documents/admin-site/operations.md) — 운영자 가이드
