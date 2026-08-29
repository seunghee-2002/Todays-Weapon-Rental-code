// admin-site SPA. Vanilla JS + fetch. 빌드 파이프라인 없음.
//
// 동작 개요:
// 1) 페이지 진입 → /api/leaderboard 호출로 세션 유효성 확인
//    - 401 이면 로그아웃 상태로 표시
//    - 200 이면 로그인 상태로 UI 노출
// 2) 플레이어 조회 / 리더보드 탭 전환
// 3) BAN/UNBAN 액션은 모달로 확인 후 호출

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => document.querySelectorAll(sel);

async function api(method, path, body) {
  const opts = { method, credentials: "same-origin", headers: {} };
  if (body !== undefined) {
    opts.headers["Content-Type"] = "application/json";
    opts.body = JSON.stringify(body);
  }
  const res = await fetch(path, opts);
  let json = null;
  try { json = await res.json(); } catch (_) { /* ignore */ }
  if (!res.ok) {
    const msg = (json && (json.detail?.message || json.message || json.detail?.error)) || res.statusText;
    const err = new Error(msg);
    err.status = res.status;
    err.body = json;
    throw err;
  }
  return json;
}

function toast(msg, isError = false) {
  const el = $("#toast");
  el.textContent = msg;
  el.classList.toggle("error", isError);
  el.hidden = false;
  clearTimeout(toast._t);
  toast._t = setTimeout(() => { el.hidden = true; }, 3500);
}

function fmtUntil(ms) {
  if (!ms || ms <= 0) return "영구";
  try {
    return new Date(ms).toLocaleString("ko-KR");
  } catch (_) { return String(ms); }
}

// ---------- 부트스트랩: 세션 확인 ----------

async function bootstrap() {
  try {
    // 가벼운 인증 체크 핑. /api/leaderboard?limit=1 은 require_admin 의존성이라
    // 미인증이면 401 을 그대로 받는다.
    await api("GET", "/api/leaderboard?limit=1");
    showLoggedIn();
  } catch (e) {
    if (e.status === 401) {
      showLoggedOut();
    } else {
      // Unity 측 일시 장애여도 로그인 상태이긴 함 → UI 는 노출하되 토스트로 알림
      showLoggedIn();
      toast(`초기 로드 실패: ${e.message}`, true);
    }
  }
}

function showLoggedOut() {
  $("#loggedOut").hidden = false;
  $("#loggedIn").hidden = true;
  $("#loginLink").hidden = false;
  $("#logoutBtn").hidden = true;
  $("#who").textContent = "";
}

function showLoggedIn() {
  $("#loggedOut").hidden = true;
  $("#loggedIn").hidden = false;
  $("#loginLink").hidden = true;
  $("#logoutBtn").hidden = false;
  loadLeaderboard().catch((e) => toast(`리더보드 로드 실패: ${e.message}`, true));
}

// ---------- 탭 ----------

$$(".tab").forEach((btn) => {
  btn.addEventListener("click", () => {
    const tab = btn.dataset.tab;
    $$(".tab").forEach((b) => b.classList.toggle("active", b === btn));
    $$(".tab-panel").forEach((p) => {
      p.classList.toggle("active", p.id === `tab-${tab}`);
    });
  });
});

// ---------- 플레이어 조회 ----------

$("#searchBtn").addEventListener("click", searchPlayer);
$("#playerIdInput").addEventListener("keydown", (e) => {
  if (e.key === "Enter") searchPlayer();
});

async function searchPlayer() {
  const pid = $("#playerIdInput").value.trim();
  if (!pid) {
    toast("PlayerId 를 입력하세요.", true);
    return;
  }
  const card = $("#playerResult");
  card.hidden = false;
  card.innerHTML = `<p>조회 중...</p>`;
  try {
    const data = await api("GET", `/api/players/${encodeURIComponent(pid)}`);
    renderPlayer(data);
  } catch (e) {
    if (e.status === 404) {
      card.innerHTML = `<p class="muted">해당 PlayerId 에 데이터가 없습니다.</p>`;
    } else {
      card.innerHTML = `<p class="error">조회 실패: ${e.message}</p>`;
    }
  }
}

function renderPlayer(p) {
  const card = $("#playerResult");
  const banned = p.banned;
  const scoreText = p.currentScore === null || p.currentScore === undefined
    ? "(미등록)" : p.currentScore;
  card.innerHTML = `
    <div class="row between">
      <div>
        <h3>${escapeHtml(p.nickname || "(닉네임 없음)")}
          ${banned ? '<span class="badge ban">BANNED</span>' : ''}
        </h3>
        <p><strong>PlayerId:</strong> <code>${escapeHtml(p.playerId)}</code></p>
        <p><strong>현재 점수:</strong> ${escapeHtml(String(scoreText))}</p>
        <div id="banDetail"></div>
      </div>
      <div class="actions-col">
        ${banned
          ? `<button class="btn primary" data-action="unban">UNBAN</button>`
          : `<button class="btn danger"  data-action="ban">BAN</button>`}
      </div>
    </div>
  `;

  // ban 일 때만 상세를 추가 호출
  if (banned) {
    api("GET", `/api/bans/${encodeURIComponent(p.playerId)}`)
      .then((b) => {
        $("#banDetail").innerHTML = `
          <p><strong>사유:</strong> ${escapeHtml(b.reason || "(없음)")}</p>
          <p><strong>만료:</strong> ${escapeHtml(fmtUntil(b.until))}</p>
        `;
      })
      .catch(() => { /* 일시 장애여도 무시 */ });
  }

  card.querySelector("[data-action]").addEventListener("click", (e) => {
    const action = e.currentTarget.dataset.action;
    if (action === "ban") openBanModal(p.playerId);
    else if (action === "unban") confirmUnban(p.playerId);
  });
}

// ---------- 리더보드 ----------

$("#reloadLbBtn").addEventListener("click", () => {
  loadLeaderboard().catch((e) => toast(`리더보드 로드 실패: ${e.message}`, true));
});

async function loadLeaderboard() {
  const data = await api("GET", "/api/leaderboard?limit=100&offset=0");
  $("#lbMeta").textContent = `${data.leaderboardId} · 전체 ${data.total} 명`;
  const tbody = $("#lbTable tbody");
  tbody.innerHTML = "";
  data.entries.forEach((row) => {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${row.rank ?? ""}</td>
      <td>${escapeHtml(row.nickname || "")}</td>
      <td><code class="pid">${escapeHtml(row.playerId || "")}</code></td>
      <td>${row.score ?? ""}</td>
      <td><button class="btn small" data-pid="${escapeAttr(row.playerId)}">조회</button></td>
    `;
    tbody.appendChild(tr);
  });
  tbody.querySelectorAll("button[data-pid]").forEach((b) => {
    b.addEventListener("click", () => {
      $("#playerIdInput").value = b.dataset.pid;
      $$(".tab").forEach((x) => x.classList.toggle("active", x.dataset.tab === "search"));
      $$(".tab-panel").forEach((p) => p.classList.toggle("active", p.id === "tab-search"));
      searchPlayer();
    });
  });
}

// ---------- BAN 모달 ----------

function openBanModal(playerId) {
  $("#banModalPid").textContent = playerId;
  $("#banReason").value = "";
  $("#banUntil").value = "";
  $("#banModal").hidden = false;
}

$("#banCancel").addEventListener("click", () => { $("#banModal").hidden = true; });
$("#banConfirm").addEventListener("click", confirmBan);

async function confirmBan() {
  const pid = $("#banModalPid").textContent;
  const reason = $("#banReason").value.trim();
  const untilLocal = $("#banUntil").value;
  if (!reason) {
    toast("사유를 입력하세요.", true);
    return;
  }
  const until = untilLocal ? new Date(untilLocal).getTime() : null;
  try {
    const r = await api("POST", "/api/bans", { playerId: pid, reason, until });
    $("#banModal").hidden = true;
    toast(`BAN 적용 완료 (점수 삭제: ${r.scoreDeleted ? "Y" : "N"})`);
    $("#playerIdInput").value = pid;
    await searchPlayer();
    await loadLeaderboard();
  } catch (e) {
    toast(`BAN 실패: ${e.message}`, true);
  }
}

async function confirmUnban(playerId) {
  if (!confirm(`${playerId} 의 ban 을 해제하시겠습니까?\n(삭제된 점수는 복구되지 않습니다.)`)) return;
  try {
    const r = await api("DELETE", `/api/bans/${encodeURIComponent(playerId)}`);
    toast(`해제 완료 (삭제된 키: ${r.deletedKeys.join(", ") || "없음"})`);
    await searchPlayer();
  } catch (e) {
    toast(`UNBAN 실패: ${e.message}`, true);
  }
}

// ---------- 로그아웃 ----------

$("#logoutBtn").addEventListener("click", async () => {
  try {
    await api("POST", "/auth/logout");
  } catch (_) { /* ignore */ }
  showLoggedOut();
});

// ---------- 유틸 ----------

function escapeHtml(s) {
  return String(s ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
function escapeAttr(s) { return escapeHtml(s); }

bootstrap();
