"""GET /api/leaderboard — 리더보드 상위 N 개."""
from fastapi import APIRouter, Depends, HTTPException, Query

from app.auth.deps import require_admin
from app.config import get_settings
from app.integrations.unity_client import UnityAdminClient, UnityApiError

router = APIRouter(prefix="/api/leaderboard", tags=["leaderboard"])


@router.get("")
async def list_scores(
    limit: int = Query(100, ge=1, le=100),
    offset: int = Query(0, ge=0, le=10000),
    _actor: str = Depends(require_admin),
):
    client = UnityAdminClient()
    try:
        raw = await client.list_leaderboard_scores(limit=limit, offset=offset)
    except UnityApiError as e:
        raise HTTPException(
            status_code=502,
            detail={
                "error": "UNITY_API_ERROR",
                "message": "Leaderboards Admin API 호출에 실패했습니다.",
                "detail": {"unityStatus": e.status},
            },
        )

    # Unity Leaderboards Admin 응답 형식 정규화
    # spec: { "total": N, "results": [ { "rank", "playerId", "playerName", "score" }, ... ] }
    entries = []
    for item in raw.get("results", []) or []:
        entries.append({
            "rank": item.get("rank"),
            "playerId": item.get("playerId"),
            "nickname": item.get("playerName") or "",
            "score": item.get("score"),
        })

    return {
        "leaderboardId": get_settings().unity_leaderboard_id,
        "entries": entries,
        "total": raw.get("total", len(entries)),
    }
