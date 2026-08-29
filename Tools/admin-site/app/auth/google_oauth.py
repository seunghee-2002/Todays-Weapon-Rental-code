"""Google OAuth 흐름 (authlib).

라우터:
- GET /auth/login    → Google 동의 화면으로 redirect
- GET /auth/callback → 토큰 교환 → email 검증 → 세션 발급 → / 로 redirect
- POST /auth/logout  → 세션 비우기
"""
from authlib.integrations.starlette_client import OAuth, OAuthError
from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import HTMLResponse, RedirectResponse

from app.auth.allowlist import is_allowed
from app.config import get_settings

router = APIRouter(prefix="/auth", tags=["auth"])

_oauth = OAuth()
_oauth.register(
    name="google",
    server_metadata_url="https://accounts.google.com/.well-known/openid-configuration",
    client_kwargs={"scope": "openid email profile"},
)


def _get_client():
    """authlib OAuth 클라이언트를 환경변수로부터 lazy 구성.

    Settings 가 import 시점에 평가되지 않도록 첫 호출 시에만 등록값을 주입.
    """
    settings = get_settings()
    client = _oauth.google
    # authlib 의 client 는 client_id/secret 을 register 시점에 받지만,
    # pydantic Settings 에서 lazy 로드 하기 위해 매 호출 시 보강.
    client.client_id = settings.google_client_id
    client.client_secret = settings.google_client_secret
    return client


@router.get("/login")
async def login(request: Request):
    """Google 동의 화면으로 redirect (state 파라미터로 CSRF 방어)."""
    client = _get_client()
    redirect_uri = f"{get_settings().public_base_url}/auth/callback"
    return await client.authorize_redirect(request, redirect_uri)


@router.get("/callback")
async def callback(request: Request):
    """Google 토큰 교환 → email 검증 → 세션 발급."""
    client = _get_client()
    try:
        token = await client.authorize_access_token(request)
    except OAuthError as e:
        return _render_failure(f"OAuth 실패: {e.error}")

    # authlib 가 id_token 파싱 결과를 token["userinfo"] 에 담아 준다.
    userinfo = token.get("userinfo") or {}
    email: str | None = userinfo.get("email")
    email_verified: bool = bool(userinfo.get("email_verified"))

    if not email or not email_verified:
        return _render_failure("Google 계정의 email_verified 가 false 입니다.")

    if not is_allowed(email):
        return _render_failure(f"허용되지 않은 계정입니다: {email}")

    # 세션 쿠키 발급. Starlette SessionMiddleware 가 직렬화·서명한다.
    request.session["email"] = email.strip().lower()
    request.session["name"] = userinfo.get("name") or email
    return RedirectResponse(url="/", status_code=302)


@router.post("/logout")
async def logout(request: Request):
    request.session.clear()
    return {"ok": True}


def _render_failure(message: str) -> HTMLResponse:
    """OAuth 실패 시 사람이 읽을 수 있는 안내 페이지."""
    html = f"""
    <html><head><meta charset="utf-8"><title>로그인 실패</title></head>
    <body style="font-family:sans-serif;padding:40px;max-width:600px;margin:auto;">
      <h2>로그인 실패</h2>
      <p style="color:#b00;">{message}</p>
      <p><a href="/auth/login">다시 시도</a></p>
    </body></html>
    """
    return HTMLResponse(content=html, status_code=401)
