"""운영자 이메일 화이트리스트 검사."""
from app.config import get_settings


def is_allowed(email: str | None) -> bool:
    """email 이 ADMIN_ALLOWED_EMAILS 리스트에 있으면 True.

    None / 빈 문자열 / 화이트리스트가 비어 있으면 False.
    비교는 소문자 정규화 후 수행.
    """
    if not email:
        return False
    allowed = get_settings().allowed_emails
    if not allowed:
        return False
    return email.strip().lower() in allowed
