"""밴 도메인 로직.

- routes/bans.py 는 이 모듈의 함수만 호출한다.
- 모든 함수가 actor_email 을 첫 번째 인자로 받는다 (향후 감사 로그 DB 단계에서 시그니처
  수정 없이 audit 기록 추가 가능).

진실의 소스는 Unity Cloud Save Protected 영역의 3개 키:
- banned     ("true")
- banReason  (사람이 읽는 사유)
- banUntil   (ms epoch as string. 영구 ban 이면 미설정)
"""
from __future__ import annotations

import logging
from dataclasses import dataclass
from typing import Optional

from app.integrations.unity_client import UnityAdminClient

logger = logging.getLogger(__name__)

_BAN_KEYS = ("banned", "banReason", "banUntil")


@dataclass
class BanStatus:
    """checkBanStatus 결과. Cloud Code 의 응답 포맷과 동일."""
    banned: bool
    reason: str
    until: int  # 0 이면 영구 또는 미설정


@dataclass
class ApplyBanResult:
    player_id: str
    reason: str
    until: int
    score_deleted: bool


@dataclass
class LiftBanResult:
    deleted_keys: list[str]


# ---------- apply ----------

async def apply_ban(
    actor_email: str,
    *,
    player_id: str,
    reason: str,
    until: Optional[int],
    client: Optional[UnityAdminClient] = None,
) -> ApplyBanResult:
    """플레이어를 ban 하고 리더보드 점수를 삭제한다.

    Args:
        actor_email: 작업 수행자 이메일 (감사 로그용. 현재는 로그만)
        player_id:   Unity Authentication PlayerId
        reason:      사용자에게 표시될 사유
        until:       ms epoch. None / 0 이면 영구
    """
    client = client or UnityAdminClient()
    items: dict[str, str] = {"banned": "true", "banReason": reason}
    normalized_until = 0
    if until is not None and until > 0:
        normalized_until = int(until)
        items["banUntil"] = str(normalized_until)

    await client.set_protected_items(player_id, items)
    score_deleted = False
    # 점수 삭제는 best-effort. 실패해도 ban 자체는 성공으로 본다.
    try:
        score_deleted = await client.delete_leaderboard_score(player_id)
    except Exception as e:  # noqa: BLE001 — 리더보드 일시 장애여도 ban 은 유지
        logger.warning(
            "[ban_service] leaderboard delete failed for %s: %s",
            player_id, e,
        )

    logger.info(
        "[ban_service] APPLY actor=%s player=%s reason=%s until=%s scoreDeleted=%s",
        actor_email, player_id, reason, normalized_until, score_deleted,
    )

    return ApplyBanResult(
        player_id=player_id,
        reason=reason,
        until=normalized_until,
        score_deleted=score_deleted,
    )


# ---------- lift ----------

async def lift_ban(
    actor_email: str,
    *,
    player_id: str,
    client: Optional[UnityAdminClient] = None,
) -> LiftBanResult:
    """ban 관련 3개 키를 best-effort 로 삭제."""
    client = client or UnityAdminClient()
    deleted: list[str] = []
    for key in _BAN_KEYS:
        if await client.delete_protected_item(player_id, key):
            deleted.append(key)

    logger.info(
        "[ban_service] LIFT  actor=%s player=%s deleted=%s",
        actor_email, player_id, deleted,
    )
    return LiftBanResult(deleted_keys=deleted)


# ---------- get ----------

async def get_ban(
    actor_email: str,
    *,
    player_id: str,
    client: Optional[UnityAdminClient] = None,
) -> BanStatus:
    """현재 ban 상태 조회.

    Cloud Code 의 checkBanStatus 와 동일한 의미론:
    - Protected 영역의 banned == "true" 면 ban
    - banUntil 이 있고 Date.now() >= banUntil 이면 ban 아님 (자동 만료)

    참고: Cloud Code 는 Protected 1차 + Default 2차 fallback 을 사용하지만,
    Admin REST 환경에선 Protected 만 사용해도 동일한 결과를 보장 (운영자가
    어드민 사이트로만 ban 을 적용하면 항상 Protected 에 들어감).
    """
    client = client or UnityAdminClient()
    items = await client.get_protected_items(player_id, _BAN_KEYS)

    if items.get("banned") != "true":
        return BanStatus(banned=False, reason="", until=0)

    reason = items.get("banReason", "") or ""
    until_raw = items.get("banUntil")
    until = 0
    if until_raw:
        try:
            until = int(until_raw)
        except (TypeError, ValueError):
            until = 0

    # 자동 만료 판정 (Cloud Code 와 동일)
    if until > 0:
        import time
        now_ms = int(time.time() * 1000)
        if now_ms >= until:
            return BanStatus(banned=False, reason="", until=0)

    return BanStatus(banned=True, reason=reason, until=until)
