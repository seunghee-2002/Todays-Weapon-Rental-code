"""FastAPI 엔트리포인트.

- SessionMiddleware (HttpOnly + Secure + SameSite=Lax)
- 라우터 등록: auth, health, bans, players, leaderboard
- /static + / 루트는 정적 SPA 진입점
"""
import logging
from pathlib import Path

from fastapi import FastAPI
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from starlette.middleware.sessions import SessionMiddleware

from app.auth import google_oauth
from app.config import get_settings
from app.routes import bans, health, leaderboard, players

# 모든 응답/로그 한국어 (CLAUDE.md 규칙)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)


def create_app() -> FastAPI:
    settings = get_settings()

    app = FastAPI(
        title="Today's Weapon Rental — Admin API",
        version="0.1.0",
        # 운영 환경에서는 인증된 운영자만 접근하지만,
        # API 스펙 노출 자체가 필요하지 않으므로 docs 도 보호 가능.
        # 현재는 OpenAPI docs 그대로 열어둠 (require_admin 으로 401 처리됨).
    )

    is_https = settings.public_base_url.startswith("https://")
    app.add_middleware(
        SessionMiddleware,
        secret_key=settings.session_secret,
        same_site="lax",
        https_only=is_https,
        max_age=60 * 60 * 8,  # 8시간
    )

    # 라우터
    app.include_router(health.router)
    app.include_router(google_oauth.router)
    app.include_router(bans.router)
    app.include_router(players.router)
    app.include_router(leaderboard.router)

    # 정적 SPA
    static_dir = Path(__file__).parent / "static"
    app.mount(
        "/static",
        StaticFiles(directory=static_dir),
        name="static",
    )

    @app.get("/", include_in_schema=False)
    async def root():
        return FileResponse(static_dir / "index.html")

    return app


app = create_app()
