"""FastAPI 의존성: require_admin.

세션 쿠키에서 email 을 꺼내 화이트리스트에 있는지 검증.
실패 시 401 + 안내 메시지.
"""
from fastapi import HTTPException, Request, status

from app.auth.allowlist import is_allowed


def require_admin(request: Request) -> str:
    """현재 요청이 인증된 운영자인지 검증.

    Returns:
        세션의 email (소문자 정규화 상태)
    Raises:
        HTTPException(401) 미인증/만료/화이트리스트 미일치
    """
    email = request.session.get("email") if hasattr(request, "session") else None
    if not email or not is_allowed(email):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail={
                "error": "UNAUTHORIZED",
                "message": "로그인이 필요합니다.",
            },
        )
    return email
