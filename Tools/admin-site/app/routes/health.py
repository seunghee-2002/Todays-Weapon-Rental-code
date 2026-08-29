"""헬스체크. 인증 미들웨어 우회."""
from fastapi import APIRouter

router = APIRouter(tags=["health"])


@router.get("/healthz")
async def healthz():
    return {"ok": True}
