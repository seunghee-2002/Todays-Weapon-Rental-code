"""테스트 공통 fixture.

테스트 환경에서 실제 Unity / Google 호출 없이 동작하도록
필수 환경변수를 미리 주입한다.
"""
import os

import pytest

_TEST_ENV = {
    "SESSION_SECRET": "test-session-secret-with-enough-length-1234",
    "GOOGLE_CLIENT_ID": "test-client-id",
    "GOOGLE_CLIENT_SECRET": "test-client-secret",
    "ADMIN_ALLOWED_EMAILS": "admin@example.com",
    "UNITY_PROJECT_ID": "00000000-0000-0000-0000-000000000000",
    "UNITY_ENV_ID": "00000000-0000-0000-0000-000000000000",
    "UNITY_SERVICE_KEY_ID": "test-key-id",
    "UNITY_SERVICE_SECRET_KEY": "test-secret",
    "UNITY_LEADERBOARD_ID": "final-days",
    "PUBLIC_BASE_URL": "http://localhost:8000",
}

for k, v in _TEST_ENV.items():
    os.environ.setdefault(k, v)


@pytest.fixture(autouse=True)
def _clear_settings_cache():
    """Settings lru_cache 가 모듈 간 누수되지 않도록 매 테스트마다 초기화."""
    from app.config import get_settings
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()
