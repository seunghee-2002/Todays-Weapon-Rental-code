"""환경변수 로딩 (pydantic-settings)."""
from functools import lru_cache
from typing import List

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """admin-site 의 모든 런타임 설정.

    필수 값은 .env 또는 OS 환경변수로 주입한다. 누락 시 즉시 ValidationError.
    """

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
        case_sensitive=False,
    )

    # --- 서버 ---
    session_secret: str = Field(..., min_length=16)
    port: int = 8000
    public_base_url: str = "http://localhost:8000"

    # --- Google OAuth ---
    google_client_id: str
    google_client_secret: str
    # 콤마 구분 문자열 → 리스트 변환
    admin_allowed_emails: str = ""

    # --- Unity Services ---
    unity_project_id: str
    unity_env_id: str
    unity_service_key_id: str
    unity_service_secret_key: str
    unity_leaderboard_id: str = "final-days"

    @field_validator("public_base_url")
    @classmethod
    def _strip_trailing_slash(cls, v: str) -> str:
        return v.rstrip("/")

    @property
    def allowed_emails(self) -> List[str]:
        """ADMIN_ALLOWED_EMAILS 를 소문자 리스트로 정규화."""
        if not self.admin_allowed_emails:
            return []
        return [
            e.strip().lower()
            for e in self.admin_allowed_emails.split(",")
            if e.strip()
        ]


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """앱 전역에서 동일 인스턴스를 재사용."""
    return Settings()  # type: ignore[call-arg]
