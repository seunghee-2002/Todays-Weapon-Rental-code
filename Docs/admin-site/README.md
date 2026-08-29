# 어드민 사이트 (admin-site)

Today's Weapon Rental 운영자가 **유저 밴/해제를 자동화** 하기 위한 별도 웹사이트.
향후 **로그 분석**, **자동 밴 정책**, **밴 이력 페이지** 등으로 확장 가능하도록 모듈식으로 설계되어 있다.

## 설계 원칙

- **Stateless MVP**: 별도 DB 없이 시작. 진실의 소스(source of truth)는 Unity Cloud Save 의 `banned`/`banReason`/`banUntil` 키.
- **Unity Dashboard 와 호환**: 어드민 사이트가 다운돼도 운영자는 Unity Dashboard 의 Cloud Save 화면에서 같은 키를 직접 조작해 ban 적용/해제 가능.
- **확장 우선**: 도메인 로직(`services/`), 외부 통합(`integrations/`), 라우터(`routes/`), 인증(`auth/`) 을 분리해 향후 로그 분석 모듈을 추가할 때 기존 코드를 손대지 않아도 됨.
- **보안 우선**: Google OAuth + 이메일 화이트리스트로만 진입. Unity Service Account 시크릿은 서버 측에만 존재.

## 기술 스택

| 구분 | 선택 | 비고 |
|---|---|---|
| 백엔드 | FastAPI (Python) | 비동기 + 자동 OpenAPI |
| 프론트엔드 | 정적 HTML + Vanilla JS | 빌드 파이프라인 없음 |
| 인증 | Google OAuth (authlib) | 화이트리스트 이메일만 |
| 외부 API | Unity Cloud Save Admin REST + Leaderboards Admin REST | httpx 클라이언트 |
| 배포 | Docker (Fly.io / Railway / Render) | HTTPS 자동 |
| DB | 없음 (MVP) | 로그 분석 단계에서 Postgres 추가 |

## 빠른 시작

```powershell
cd admin-site
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Copy-Item .env.example .env
# .env 파일을 열어 환경변수 값 채우기 (자세한 건 setup.md 참고)
uvicorn app.main:app --reload --port 8000
```

브라우저로 `http://localhost:8000` 접속 → Google 로그인 → 허용 이메일이면 어드민 페이지 진입.

## 디렉터리 구조 (레포 루트의 `admin-site/`)

```
admin-site/
├─ app/
│  ├─ main.py                   FastAPI 엔트리포인트, 미들웨어/라우터 등록
│  ├─ config.py                 환경변수 로드 (pydantic-settings)
│  ├─ auth/
│  │  ├─ google_oauth.py        OAuth 흐름 (authlib)
│  │  ├─ deps.py                require_admin 의존성 (세션 검증)
│  │  └─ allowlist.py           허용 이메일 화이트리스트
│  ├─ integrations/
│  │  └─ unity_client.py        Unity Admin REST API 클라이언트 (httpx)
│  ├─ services/
│  │  └─ ban_service.py         밴 도메인 로직 (set/delete/get)
│  ├─ routes/
│  │  ├─ bans.py                /api/bans
│  │  ├─ players.py             /api/players
│  │  ├─ leaderboard.py         /api/leaderboard
│  │  └─ health.py              /healthz
│  ├─ logs/                     (향후 로그 분석 확장 placeholder)
│  └─ static/
│     ├─ index.html             SPA 진입점
│     ├─ app.js                 페이지 라우팅 + API 호출
│     └─ style.css
├─ tests/                       pytest 테스트
├─ .env.example                 환경변수 템플릿
├─ requirements.txt
├─ Dockerfile                   배포용
└─ README.md                    이 사이트의 로컬 빠른 시작 (Documents/admin-site/README.md 와 별개)
```

## 문서 색인

| 문서 | 내용 |
|---|---|
| [README.md](README.md) | (이 파일) 개요와 빠른 시작 |
| [architecture.md](architecture.md) | 시스템 다이어그램, 데이터 흐름, 진실 소스 원칙, 확장 시나리오 |
| [setup.md](setup.md) | 설치, 환경변수, Unity Service Account/Google OAuth 발급, 배포 |
| [api.md](api.md) | REST API 레퍼런스 (요청/응답, 에러, curl 예시) |
| [operations.md](operations.md) | 운영자 가이드 — 일상 작업 절차 |

## 게임 측 ban 시스템과의 관계

본 어드민 사이트는 **게임 외부**에서 운영자가 사용한다. 게임 클라이언트와는 직접 통신하지 않으며, 양측 모두 Unity Cloud Save 의 같은 키를 본다.

```
[운영자 브라우저]                    [Unity 게임 클라이언트]
       │                                       │
       │ Google OAuth 세션                     │ UGS 익명 로그인
       ▼                                       ▼
[FastAPI 어드민]  ──── Unity Cloud Save ────  [Cloud Code]
        write ─────────► banned 키 ◄───────── read
                         banReason            (checkBanStatus
                         banUntil              + submitLeaderboardScore)
```

상세 흐름은 [architecture.md](architecture.md) 참고.

## 운영자 일일 작업

대부분의 작업은 [operations.md](operations.md) 의 절차를 따른다.

- 어뷰저 발견 → 어드민 사이트에서 ban 적용 (점수 자동 삭제)
- 사용자 항의/오해로 인한 ban → 어드민 사이트에서 해제
- Unity Dashboard 직접 조작은 어드민 사이트가 다운됐을 때만 사용 (백업 경로)

## 미래 확장 시나리오 (요약)

본 MVP 가 안정화되면 다음 단계로 확장 가능. 각 단계는 기존 코드를 손대지 않고 모듈만 추가하면 됨.

1. **감사 로그 DB** (Postgres) — 누가/언제/왜 ban 했는지 기록
2. **Unity Analytics → BigQuery export** — 어뷰저 패턴 탐지
3. **Cloud Code 의심 이벤트 webhook** — 시간 위반 같은 이벤트를 어드민 사이트로 push
4. **자동 ban 정책** — 룰엔진 (예: 1초당 100일 진행)
5. **ban 이력 페이지** — UI 노출

상세는 [architecture.md](architecture.md) 의 "향후 확장 포인트" 절 참고.
