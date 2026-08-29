"""ban_service 단위 테스트. UnityAdminClient 는 Fake 로 대체."""
from __future__ import annotations

import time
from typing import Dict

import pytest

from app.services import ban_service


class FakeUnityClient:
    """integrations.unity_client.UnityAdminClient 의 testing fake."""

    def __init__(self):
        self.store: Dict[str, Dict[str, str]] = {}     # player_id → key → value
        self.scores: Dict[str, float] = {}             # player_id → score

    async def set_protected_items(self, player_id, items):
        self.store.setdefault(player_id, {}).update(items)
        return None

    async def get_protected_items(self, player_id, keys):
        d = self.store.get(player_id, {})
        return {k: d[k] for k in keys if k in d}

    async def delete_protected_item(self, player_id, key):
        d = self.store.get(player_id, {})
        if key in d:
            d.pop(key)
            return True
        return False

    async def delete_leaderboard_score(self, player_id):
        if player_id in self.scores:
            self.scores.pop(player_id)
            return True
        return False


@pytest.mark.asyncio
async def test_apply_ban_sets_three_keys_and_deletes_score():
    c = FakeUnityClient()
    c.scores["P1"] = 42

    r = await ban_service.apply_ban(
        "admin@example.com",
        player_id="P1", reason="치트", until=None,
        client=c,
    )

    assert r.player_id == "P1"
    assert r.reason == "치트"
    assert r.until == 0
    assert r.score_deleted is True
    assert c.store["P1"]["banned"] == "true"
    assert c.store["P1"]["banReason"] == "치트"
    assert "banUntil" not in c.store["P1"]
    assert "P1" not in c.scores


@pytest.mark.asyncio
async def test_apply_ban_with_until_writes_banuntil_string():
    c = FakeUnityClient()
    until_ms = int(time.time() * 1000) + 60_000

    r = await ban_service.apply_ban(
        "admin@example.com",
        player_id="P2", reason="일시 정지", until=until_ms,
        client=c,
    )

    assert r.until == until_ms
    assert c.store["P2"]["banUntil"] == str(until_ms)


@pytest.mark.asyncio
async def test_lift_ban_removes_only_existing_keys():
    c = FakeUnityClient()
    c.store["P3"] = {"banned": "true", "banReason": "치트"}  # banUntil 없음

    r = await ban_service.lift_ban(
        "admin@example.com", player_id="P3", client=c,
    )

    assert set(r.deleted_keys) == {"banned", "banReason"}
    assert "P3" not in c.store or c.store["P3"] == {}


@pytest.mark.asyncio
async def test_get_ban_returns_false_when_no_key():
    c = FakeUnityClient()
    s = await ban_service.get_ban(
        "admin@example.com", player_id="P4", client=c,
    )
    assert s.banned is False
    assert s.until == 0


@pytest.mark.asyncio
async def test_get_ban_returns_true_for_permanent():
    c = FakeUnityClient()
    c.store["P5"] = {"banned": "true", "banReason": "영구"}

    s = await ban_service.get_ban(
        "admin@example.com", player_id="P5", client=c,
    )
    assert s.banned is True
    assert s.reason == "영구"
    assert s.until == 0


@pytest.mark.asyncio
async def test_get_ban_auto_expires_when_banUntil_passed():
    c = FakeUnityClient()
    past = int(time.time() * 1000) - 60_000
    c.store["P6"] = {"banned": "true", "banReason": "기간", "banUntil": str(past)}

    s = await ban_service.get_ban(
        "admin@example.com", player_id="P6", client=c,
    )
    assert s.banned is False


@pytest.mark.asyncio
async def test_get_ban_active_when_banUntil_future():
    c = FakeUnityClient()
    future = int(time.time() * 1000) + 60_000
    c.store["P7"] = {"banned": "true", "banReason": "기간", "banUntil": str(future)}

    s = await ban_service.get_ban(
        "admin@example.com", player_id="P7", client=c,
    )
    assert s.banned is True
    assert s.until == future
