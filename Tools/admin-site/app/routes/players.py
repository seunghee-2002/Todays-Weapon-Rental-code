"""GET /api/players/{playerId} — 플레이어 기본정보."""
from fastapi import APIRouter, Depends, HTTPException, Path

from app.auth.deps import require_admin
from app.integrations.unity_client import UnityAdminClient, UnityApiError
from app.services import ban_service

router = APIRouter(prefix="/api/players", tags=["players"])


@router.get("/{player_id}")
async def get_player(
    player_id: str = Path(..., min_length=1, max_length=64),
    actor: str = Depends(require_admin),
):
    """닉네임 + ban 상태 + 현재 점수 묶음 조회."""
    client = UnityAdminClient()
    try:
        # Default access 에 저장되는 playerNickname
        default_items = await client.get_default_items(
            player_id, ["playerNickname"]
        )
        ban_status = await ban_service.get_ban(
            actor, player_id=player_id, client=client,
        )
        score = await client.get_leaderboard_score(player_id)
    except UnityApiError as e:
        raise HTTPException(
            status_code=502,
            detail={
                "error": "UNITY_API_ERROR",
                "message": "Unity Admin API 호출에 실패했습니다.",
                "detail": {"unityStatus": e.status},
            },
        )

    nickname = default_items.get("playerNickname", "") or ""

    # 닉네임도 점수도 ban 도 전부 없으면 404 (PlayerId 자체가 미존재 가능성)
    if not nickname and not ban_status.banned and score is None:
        raise HTTPException(
            status_code=404,
            detail={
                "error": "PLAYER_NOT_FOUND",
                "message": "해당 PlayerId 에 대한 데이터가 없습니다.",
            },
        )

    return {
        "playerId": player_id,
        "nickname": nickname,
        "banned": ban_status.banned,
        "currentScore": score,
    }
