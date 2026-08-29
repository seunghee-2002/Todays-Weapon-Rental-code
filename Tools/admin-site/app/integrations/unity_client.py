"""Unity Cloud Save Admin + Leaderboards Admin REST 클라이언트.

- 인증: Authorization: Basic base64(KEY_ID:SECRET) — httpx.BasicAuth 가 자동 처리
- 모든 호출은 비동기 httpx.AsyncClient
- 실패 시 UnityApiError 를 raise (서비스 레이어에서 502 로 매핑)

엔드포인트 path 는 Unity 공식 OpenAPI 스펙 기준. SDK 가 아니라 직접 REST 호출.
"""
from __future__ import annotations

import base64
import logging
from typing import Any, Dict, Iterable, List, Optional

import httpx

from app.config import Settings, get_settings

logger = logging.getLogger(__name__)

# Unity Services API base. 변경 가능성이 낮지만 향후 region 분리 시 settings 로 이전.
_SERVICES_BASE = "https://services.api.unity.com"


class UnityApiError(RuntimeError):
    """Unity Admin API 호출 실패. status 와 body 를 보존."""

    def __init__(self, status: int, body: Any, endpoint: str):
        super().__init__(f"Unity API {status} on {endpoint}: {body}")
        self.status = status
        self.body = body
        self.endpoint = endpoint


class UnityAdminClient:
    """Cloud Save + Leaderboards Admin REST 래퍼."""

    def __init__(self, settings: Optional[Settings] = None):
        self._settings = settings or get_settings()
        self._auth = httpx.BasicAuth(
            username=self._settings.unity_service_key_id,
            password=self._settings.unity_service_secret_key,
        )

    # ---------- 내부: HTTP 헬퍼 ----------

    async def _request(
        self,
        method: str,
        path: str,
        *,
        params: Optional[Dict[str, Any]] = None,
        json: Optional[Any] = None,
    ) -> Optional[Any]:
        """공통 요청 헬퍼. 2xx → JSON 또는 None. 그 외 → UnityApiError."""
        url = f"{_SERVICES_BASE}{path}"
        async with httpx.AsyncClient(auth=self._auth, timeout=15.0) as client:
            resp = await client.request(method, url, params=params, json=json)

        if 200 <= resp.status_code < 300:
            # 204 / 빈 body 대응
            if not resp.content:
                return None
            try:
                return resp.json()
            except ValueError:
                return None

        # 404 는 호출부에서 의미 있게 처리 (점수 없음 등)
        # 로그에 시크릿이 들어가지 않도록 body 만 짧게 남긴다.
        body_preview = (resp.text or "")[:500]
        logger.warning(
            "[UnityAdminClient] %s %s → %s (%s)",
            method, path, resp.status_code, body_preview,
        )
        raise UnityApiError(resp.status_code, _safe_body(resp), path)

    # ---------- Cloud Save: Protected Player Data ----------

    @property
    def _cs_player_base(self) -> str:
        return (
            f"/cloud-save/v1/data/projects/{self._settings.unity_project_id}"
            f"/environments/{self._settings.unity_env_id}/players"
        )

    async def set_protected_items(
        self, player_id: str, items: Dict[str, str]
    ) -> None:
        """Protected access 로 키들을 set (upsert).

        Cloud Save Admin spec: POST .../players/{playerId}/items?accessClass=protected
        body = { "key": "...", "value": "...", "writeLock": null }   (단일 아이템)

        Client SDK 의 batch `{"data":[...]}` 와 다르게 Admin API 는 1 호출 = 1 키.
        키 개수만큼 순차 POST 한다. 중간에 실패하면 UnityApiError 가 raise 되어
        호출부(ban_service.apply_ban) 가 502 로 변환한다.
        """
        path = f"{self._cs_player_base}/{player_id}/items"
        for key, value in items.items():
            await self._request(
                "POST", path,
                params={"accessClass": "protected"},
                json={"key": key, "value": value, "writeLock": None},
            )

    async def get_protected_items(
        self, player_id: str, keys: Iterable[str]
    ) -> Dict[str, str]:
        """Protected 영역에서 지정 키들의 현재 값을 dict 로 반환.

        없는 키는 dict 에서 누락된다 (예외 X). PlayerId 자체가 없으면 빈 dict.
        """
        return await self._get_items(player_id, keys, access_class="protected")

    async def get_default_items(
        self, player_id: str, keys: Iterable[str]
    ) -> Dict[str, str]:
        """Default 영역에서 키 조회 (예: playerNickname)."""
        return await self._get_items(player_id, keys, access_class="default")

    async def _get_items(
        self, player_id: str, keys: Iterable[str], *, access_class: str
    ) -> Dict[str, str]:
        path = f"{self._cs_player_base}/{player_id}/items"
        # Unity Cloud Save Admin 은 keys 를 콤마 구분이 아니라 반복 파라미터로 받는다.
        # httpx 는 list 를 주면 자동으로 ?keys=a&keys=b 형태로 직렬화한다.
        keys_list = list(keys)
        try:
            result = await self._request(
                "GET", path,
                params={"keys": keys_list, "accessClass": access_class},
            )
        except UnityApiError as e:
            # PlayerId 자체가 없는 경우 → 빈 dict 로 응답 (404)
            if e.status == 404:
                return {}
            raise

        out: Dict[str, str] = {}
        for item in (result or {}).get("results", []):
            key = item.get("key")
            value = item.get("value")
            if key is not None:
                out[key] = "" if value is None else str(value)
        return out

    async def delete_protected_item(self, player_id: str, key: str) -> bool:
        """Protected 영역의 단일 키 삭제. 없으면 False, 있으면 True."""
        path = f"{self._cs_player_base}/{player_id}/items/{key}"
        try:
            await self._request(
                "DELETE", path,
                params={"accessClass": "protected"},
            )
            return True
        except UnityApiError as e:
            if e.status == 404:
                return False
            raise

    # ---------- Leaderboards Admin ----------

    @property
    def _lb_base(self) -> str:
        return (
            f"/leaderboards/v1/projects/{self._settings.unity_project_id}"
            f"/environments/{self._settings.unity_env_id}/leaderboards"
        )

    async def delete_leaderboard_score(self, player_id: str) -> bool:
        """final-days 리더보드에서 player_id 점수 삭제.

        Returns:
            True 면 실제로 삭제됨. False 면 점수가 처음부터 없었음.
        """
        lid = self._settings.unity_leaderboard_id
        path = f"{self._lb_base}/{lid}/scores/players/{player_id}"
        try:
            await self._request("DELETE", path)
            return True
        except UnityApiError as e:
            if e.status == 404:
                return False
            raise

    async def get_leaderboard_score(self, player_id: str) -> Optional[float]:
        """특정 player 의 현재 점수. 미등록이면 None."""
        lid = self._settings.unity_leaderboard_id
        path = f"{self._lb_base}/{lid}/scores/players/{player_id}"
        try:
            result = await self._request("GET", path)
        except UnityApiError as e:
            if e.status == 404:
                return None
            raise
        if result is None:
            return None
        score = result.get("score")
        return None if score is None else float(score)

    async def list_leaderboard_scores(
        self, *, limit: int = 100, offset: int = 0
    ) -> Dict[str, Any]:
        """리더보드 상위 N 개 조회. 응답 그대로 반환."""
        lid = self._settings.unity_leaderboard_id
        path = f"{self._lb_base}/{lid}/scores"
        result = await self._request(
            "GET", path,
            params={"limit": limit, "offset": offset},
        )
        return result or {}


def _safe_body(resp: httpx.Response) -> Any:
    """응답 body 를 JSON 으로 시도하되 실패 시 텍스트 일부만."""
    try:
        return resp.json()
    except ValueError:
        return (resp.text or "")[:500]
