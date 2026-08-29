"""POST/DELETE/GET /api/bans"""
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Path
from pydantic import BaseModel, Field

from app.auth.deps import require_admin
from app.integrations.unity_client import UnityApiError
from app.services import ban_service

router = APIRouter(prefix="/api/bans", tags=["bans"])


class ApplyBanRequest(BaseModel):
    player_id: str = Field(..., min_length=1, max_length=64, alias="playerId")
    reason: str = Field(..., min_length=1, max_length=200)
    until: Optional[int] = Field(default=None, ge=0)

    model_config = {"populate_by_name": True}


@router.post("")
async def apply_ban(
    body: ApplyBanRequest,
    actor: str = Depends(require_admin),
):
    """플레이어 ban 적용 + 리더보드 점수 자동 삭제."""
    try:
        result = await ban_service.apply_ban(
            actor,
            player_id=body.player_id,
            reason=body.reason,
            until=body.until,
        )
    except UnityApiError as e:
        raise _to_502(e)

    return {
        "ok": True,
        "ban": {
            "playerId": result.player_id,
            "reason": result.reason,
            "until": result.until,
        },
        "scoreDeleted": result.score_deleted,
    }


@router.delete("/{player_id}")
async def lift_ban(
    player_id: str = Path(..., min_length=1, max_length=64),
    actor: str = Depends(require_admin),
):
    """ban 해제 (3개 키 삭제)."""
    try:
        result = await ban_service.lift_ban(actor, player_id=player_id)
    except UnityApiError as e:
        raise _to_502(e)

    return {"ok": True, "deletedKeys": result.deleted_keys}


@router.get("/{player_id}")
async def get_ban(
    player_id: str = Path(..., min_length=1, max_length=64),
    actor: str = Depends(require_admin),
):
    """현재 ban 상태 조회."""
    try:
        status = await ban_service.get_ban(actor, player_id=player_id)
    except UnityApiError as e:
        raise _to_502(e)

    return {
        "banned": status.banned,
        "reason": status.reason,
        "until": status.until,
    }


def _to_502(e: UnityApiError) -> HTTPException:
    """UnityApiError → 502. 시크릿 누출 방지 위해 detail 최소화."""
    return HTTPException(
        status_code=502,
        detail={
            "error": "UNITY_API_ERROR",
            "message": "Unity Admin API 호출에 실패했습니다.",
            "detail": {"unityStatus": e.status},
        },
    )
