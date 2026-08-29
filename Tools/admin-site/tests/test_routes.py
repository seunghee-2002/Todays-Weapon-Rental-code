"""라우터 통합 테스트.

- /healthz: 인증 없이 200
- /api/* : 미인증 401, 세션 주입 시 200
- UnityAdminClient 는 monkeypatch 로 fake 화
"""
from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from app.main import create_app


def _login_as(client: TestClient, email: str = "admin@example.com"):
    """SessionMiddleware 의 세션 쿠키를 직접 만들기 위해 헬퍼 라우트 추가 없이
    starlette 의 itsdangerous 직렬화 흐름을 그대로 활용.

    가장 간단한 방법: TestClient 의 cookie jar 에 우리가 만든 라우터로 세션을 주입한다.
    여기서는 임시 로그인 엔드포인트를 추가하지 않고, app.middleware 의 동작에
    맡기는 대신 직접 request.session 을 손대는 dependency override 를 사용한다.
    """
    # require_admin 의존성을 직접 오버라이드해 모든 라우트에서 통과시킨다.
    from app.auth.deps import require_admin
    client.app.dependency_overrides[require_admin] = lambda: email


def _logout(client: TestClient):
    client.app.dependency_overrides.clear()


@pytest.fixture
def app():
    return create_app()


@pytest.fixture
def client(app):
    return TestClient(app)


def test_healthz_is_public(client):
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json() == {"ok": True}


def test_api_requires_auth(client):
    r = client.get("/api/leaderboard?limit=1")
    assert r.status_code == 401


def test_apply_ban_route_calls_service(client, monkeypatch):
    captured = {}

    async def fake_apply_ban(actor, *, player_id, reason, until, client=None):
        captured["actor"] = actor
        captured["player_id"] = player_id
        captured["reason"] = reason
        captured["until"] = until

        from app.services.ban_service import ApplyBanResult
        return ApplyBanResult(
            player_id=player_id, reason=reason,
            until=until or 0, score_deleted=True,
        )

    monkeypatch.setattr("app.routes.bans.ban_service.apply_ban", fake_apply_ban)
    _login_as(client)

    r = client.post("/api/bans", json={
        "playerId": "ABC", "reason": "치트", "until": None,
    })
    _logout(client)

    assert r.status_code == 200, r.text
    data = r.json()
    assert data["ok"] is True
    assert data["ban"]["playerId"] == "ABC"
    assert data["scoreDeleted"] is True
    assert captured["actor"] == "admin@example.com"
    assert captured["reason"] == "치트"


def test_lift_ban_route(client, monkeypatch):
    async def fake_lift_ban(actor, *, player_id, client=None):
        from app.services.ban_service import LiftBanResult
        return LiftBanResult(deleted_keys=["banned"])

    monkeypatch.setattr("app.routes.bans.ban_service.lift_ban", fake_lift_ban)
    _login_as(client)

    r = client.delete("/api/bans/XYZ")
    _logout(client)

    assert r.status_code == 200
    assert r.json() == {"ok": True, "deletedKeys": ["banned"]}


def test_get_ban_route(client, monkeypatch):
    async def fake_get_ban(actor, *, player_id, client=None):
        from app.services.ban_service import BanStatus
        return BanStatus(banned=True, reason="치트", until=0)

    monkeypatch.setattr("app.routes.bans.ban_service.get_ban", fake_get_ban)
    _login_as(client)

    r = client.get("/api/bans/XYZ")
    _logout(client)

    assert r.status_code == 200
    assert r.json() == {"banned": True, "reason": "치트", "until": 0}


def test_leaderboard_route(client, monkeypatch):
    class FakeClient:
        async def list_leaderboard_scores(self, *, limit, offset):
            return {
                "total": 2,
                "results": [
                    {"rank": 1, "playerId": "A", "playerName": "Alice", "score": 99},
                    {"rank": 2, "playerId": "B", "playerName": "Bob",   "score": 88},
                ],
            }

    monkeypatch.setattr(
        "app.routes.leaderboard.UnityAdminClient", lambda: FakeClient()
    )
    _login_as(client)

    r = client.get("/api/leaderboard?limit=2")
    _logout(client)

    assert r.status_code == 200
    body = r.json()
    assert body["total"] == 2
    assert len(body["entries"]) == 2
    assert body["entries"][0]["nickname"] == "Alice"
