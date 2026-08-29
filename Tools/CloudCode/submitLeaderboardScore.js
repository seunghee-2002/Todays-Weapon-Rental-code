/**
 * submitLeaderboardScore.js
 * UGS Cloud Code 모듈 — 리더보드 점수 검증 후 제출
 *
 * params:
 *   claimedDays  (number) — 클라이언트가 제출하는 경과 일수
 *   sessionToken (string) — startGameSession에서 발급받은 세션 토큰
 *
 * metadata 정책:
 *   - { date: "<ISO 8601 UTC>" } 형태로 제출 시점만 기록한다.
 *   - 닉네임은 metadata 에 담지 않는다. UGS Leaderboards 는 동일 score 재제출 시
 *     metadata 갱신을 거부하므로 닉네임 변경과 동기화가 불가능하기 때문.
 *     닉네임 표시는 클라이언트가 getPlayerNicknames Cloud Code 로 PlayerId → 닉네임을
 *     일괄 조회해서 매핑한다 (LeaderboardManager.NicknameCache).
 *
 * 검증 단계:
 *   1. 타입/범위 검증           — 양의 정수, MAX_DAYS 이하
 *   2. 세션 토큰 검증 (Nonce)   — 저장된 토큰과 비교 + 재사용 방지
 *   3. 시간 기반 검증           — 최소 플레이 시간(일수 × 30초) 충족 여부
 *
 * 에러 코드:
 *   2001 INVALID_SCORE     — 음수/비정수/MAX_DAYS 초과
 *   2002 INVALID_SESSION   — 세션 토큰 없음 / 불일치
 *   2003 SESSION_REPLAYED  — 이미 사용된 세션 토큰 (Replay Attack)
 *   2004 TIME_VIOLATION    — 플레이 시간이 점수에 비해 너무 짧음
 *   2005 LEADERBOARD_ERROR — Leaderboards API 호출 실패 (리더보드 미생성 등)
 *   2006 BANNED            — 운영자에 의해 차단된 계정. message=JSON{reason,until}
 */

const { DataApi }         = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.0");

const MAX_DAYS        = 9999; // 이론상 최대 허용 일수
const MIN_SEC_PER_DAY = 30;  // 일수당 최소 실제 경과 시간(초)

module.params = {
  claimedDays:  { type: "Numeric", required: true },
  sessionToken: { type: "String",  required: true }
};

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const { claimedDays, sessionToken } = params;

  const dataApi         = new DataApi(context);
  const leaderboardsApi = new LeaderboardsApi(context);

  // ── 1. 타입/범위 검증 ────────────────────────────────────────────────
  if (!Number.isInteger(claimedDays) || claimedDays <= 0) {
    const err = new Error("점수는 양의 정수여야 합니다.");
    err.applicationErrorCode = 2001;
    throw err;
  }
  if (claimedDays > MAX_DAYS) {
    const err = new Error(`점수가 최대치(${MAX_DAYS}일)를 초과합니다.`);
    err.applicationErrorCode = 2001;
    throw err;
  }

  // ── 1.5 밴 검사 ──────────────────────────────────────────────────────
  await checkBanStatus(dataApi, leaderboardsApi, projectId, playerId, logger);

  // ── 2. Cloud Save 일괄 읽기 ──────────────────────────────────────────
  // 세션 키는 Protected access class(클라이언트 쓰기 불가)에 있다.
  // playerData(세이브)는 클라이언트가 써야 하므로 Default class 유지.
  const protectedResult = await dataApi.getProtectedItems(projectId, playerId, [
    "sessionToken",
    "sessionStartAt",
    "sessionTokenUsed"
  ]);
  const defaultResult = await dataApi.getItems(projectId, playerId, ["playerData"]);

  const itemMap = {};
  for (const item of (protectedResult.data.results ?? [])) {
    itemMap[item.key] = item;
  }
  for (const item of (defaultResult.data.results ?? [])) {
    itemMap[item.key] = item;
  }

  // ── 3. 세션 토큰 검증 (Nonce + 재사용 방지) ──────────────────────────
  const storedToken = itemMap["sessionToken"]?.value;
  const tokenUsed   = itemMap["sessionTokenUsed"]?.value;

  if (!storedToken) {
    const err = new Error("세션 토큰이 없습니다. 게임을 새로 시작해주세요.");
    err.applicationErrorCode = 2002;
    throw err;
  }

  if (tokenUsed === "true") {
    logger.error(`[submitLeaderboardScore] REPLAY_ATTACK playerId=${playerId}`);
    const err = new Error("이미 사용된 세션 토큰입니다. (Replay Attack 차단)");
    err.applicationErrorCode = 2003;
    throw err;
  }

  if (sessionToken !== storedToken) {
    logger.error(`[submitLeaderboardScore] TOKEN_MISMATCH playerId=${playerId}`);
    const err = new Error("세션 토큰이 일치하지 않습니다.");
    err.applicationErrorCode = 2002;
    throw err;
  }

  // ── 4. 시간 기반 검증 ────────────────────────────────────────────────
  const sessionStartAt = parseInt(itemMap["sessionStartAt"]?.value ?? "0", 10);
  if (sessionStartAt > 0) {
    const elapsedSec  = (Date.now() - sessionStartAt) / 1000;
    const minRequired = claimedDays * MIN_SEC_PER_DAY;

    if (elapsedSec < minRequired) {
      logger.error(
        `[submitLeaderboardScore] TIME_VIOLATION playerId=${playerId} ` +
        `elapsed=${Math.round(elapsedSec)}s required=${minRequired}s days=${claimedDays}`
      );
      const err = new Error(
        `플레이 시간이 점수에 비해 너무 짧습니다. ` +
        `(경과 ${Math.round(elapsedSec)}초 / 최소 ${minRequired}초 필요)`
      );
      err.applicationErrorCode = 2004;
      throw err;
    }
  }

  // ── 4.5 저장 일수 관측 (거부하지 않는다) ─────────────────────────────
  // claimedDays 와 클라우드 playerData(GameData) 는 둘 다 클라이언트가 쓰는 값이다.
  // 치터는 저장본을 먼저 위조해 올리면 그대로 통과하므로 대조의 안티치트 실효가 없고,
  // 반대로 정상 플레이어는 일자 경계 업로드가 한 번 실패하거나 저장 없이 날짜가 오른
  // 경우 클라우드 currentDay 가 뒤처져 거부당한다. 클라이언트는 스크립트 오류를
  // 재시도 불가로 보고 보류 점수를 폐기하므로(LeaderboardManager default 분기)
  // 거부는 곧 기록 영구 소실이다. 그래서 불일치는 로그로만 남기고 제출은 통과시킨다.
  const savedGameJson = itemMap["playerData"]?.value;
  if (savedGameJson) {
    let savedCurrentDay = null;
    try {
      const savedGame = JSON.parse(savedGameJson);
      savedCurrentDay = Number(savedGame?.currentDay);
    } catch (_) {
      savedCurrentDay = null;
    }

    if (Number.isFinite(savedCurrentDay) && savedCurrentDay > 0 && claimedDays > savedCurrentDay) {
      logger.info(
        `[submitLeaderboardScore] DAY_MISMATCH(관측) playerId=${playerId} ` +
        `claimed=${claimedDays} savedCurrentDay=${savedCurrentDay}`
      );
    }
  }

  // ── 5. 토큰 사용 처리 (이후 Replay Attack 차단) ──────────────────────
  // Protected class에 읽어둔 writeLock 으로 조건부 갱신 — 동시 제출(TOCTOU) 시 한쪽만 성공한다.
  try {
    await dataApi.setProtectedItemBatch(projectId, playerId, {
      data: [{
        key: "sessionTokenUsed",
        value: "true",
        writeLock: itemMap["sessionTokenUsed"]?.writeLock ?? null
      }]
    });
  } catch (e) {
    const status = e.response?.status ?? e.status ?? 0;
    logger.error(
      `[submitLeaderboardScore] TOKEN_USE_CONFLICT playerId=${playerId} status=${status}: ${e.message ?? e}`
    );
    const err = new Error("이미 사용된 세션 토큰입니다. (동시 제출 차단)");
    err.applicationErrorCode = 2003;
    throw err;
  }

  // ── 6. 리더보드 점수 제출 ─────────────────────────────────────────────
  // Leaderboards API 는 metadata 를 JSON 객체(map[string]interface{})로 받는다.
  // JSON.stringify 로 문자열을 보내면 400 "cannot unmarshal string into ... metadata".
  // 클라이언트(C# LeaderboardEntry.Metadata)는 JSON 문자열로 받아 파싱한다
  // (LeaderboardManager.LeaderboardMetadata 와 키 이름 매칭 — 현재는 date).
  const scoreBody = {
    score:    claimedDays,
    metadata: { date: new Date().toISOString() }
  };

  try {
    // Cloud Code는 서비스 토큰으로 실행되므로 playerId를 명시해야 함
    await leaderboardsApi.addLeaderboardPlayerScore(projectId, "final-days", playerId, scoreBody);
  } catch (e) {
    // 진단용: SDK가 어떤 형태로 에러를 던지든 모든 속성을 통째로 로깅한다.
    // (axios의 e.response.data 가 노출 안 될 수도 있어 방어적으로 전체 덤프)
    let fullErr;
    try {
      fullErr = JSON.stringify(e, Object.getOwnPropertyNames(e));
    } catch (_) {
      fullErr = String(e);
    }
    const respStatus = e.response?.status ?? e.status ?? "?";
    logger.error(
      `[submitLeaderboardScore] LEADERBOARD_API_ERROR playerId=${playerId} ` +
      `status=${respStatus} ` +
      `sentBody=${JSON.stringify(scoreBody)} ` +
      `errorDump=${fullErr}`
    );
    const err = new Error(`리더보드 API 오류 (status=${respStatus}): ${e.message ?? e}`);
    err.applicationErrorCode = 2005;
    throw err;
  }

  logger.info(`[submitLeaderboardScore] playerId=${playerId} score=${claimedDays}일 제출 완료`);
  return { success: true, score: claimedDays };
};

// ── 헬퍼 ────────────────────────────────────────────────────────────────────

/**
 * 운영자가 Protected Cloud Save 에 설정한 banned/banReason/banUntil 키를 확인한다.
 * - banned !== "true" 또는 키 없음    → 통과 (조용히 리턴)
 * - banUntil 이 있고 현재 시각 >= until → 만료로 간주, 통과
 * - 그 외                              → 점수 best-effort 삭제 후 2006 throw
 *
 * 에러 message 는 JSON.stringify({reason, until}) 형태로 클라이언트에 전달.
 * 클라이언트(LeaderboardManager.cs)는 이 message 를 JsonUtility.FromJson<BanInfo>() 로 파싱.
 *
 * 같은 함수가 startGameSession.js 에도 인라인 복제되어 있다.
 * (UGS Cloud Code Scripts 는 사용자 모듈 import 가 제한적이라 복제 방식 채택)
 */
async function checkBanStatus(dataApi, leaderboardsApi, projectId, playerId, logger) {
  const map = {};
  const BAN_KEYS = ["banned", "banReason", "banUntil"];

  // (1) Protected access class 조회
  try {
    const result = await dataApi.getProtectedItems(projectId, playerId, BAN_KEYS);
    const count  = (result?.data?.results ?? []).length;
    for (const item of (result?.data?.results ?? [])) {
      map[item.key] = item.value;
    }
    logger.info(`[checkBanStatus] protected 조회 OK playerId=${playerId} returned=${count}`);
  } catch (e) {
    logger.info(`[checkBanStatus] protected 조회 실패 playerId=${playerId}: ${e.message ?? e}`);
  }

  // (2) Default access class fallback — 운영자가 대시보드의 일반 영역에 둔 경우
  if (map.banned !== "true") {
    try {
      const result2 = await dataApi.getItems(projectId, playerId, BAN_KEYS);
      const count2  = (result2?.data?.results ?? []).length;
      for (const item of (result2?.data?.results ?? [])) {
        if (map[item.key] === undefined) map[item.key] = item.value;
      }
      logger.info(`[checkBanStatus] default 조회 OK playerId=${playerId} returned=${count2} mergedMap=${JSON.stringify(map)}`);
    } catch (e) {
      logger.info(`[checkBanStatus] default 조회 실패 playerId=${playerId}: ${e.message ?? e}`);
    }
  }

  if (map.banned !== "true") {
    logger.info(`[checkBanStatus] 비밴 — 통과 playerId=${playerId} banned='${map.banned ?? "(none)"}'`);
    return;
  }

  const untilStr = map.banUntil;
  let untilMs = null;
  if (untilStr) {
    const parsed = parseInt(untilStr, 10);
    if (!isNaN(parsed)) {
      untilMs = parsed;
      if (Date.now() >= parsed) {
        logger.info(`[checkBanStatus] banUntil 만료 — 통과: playerId=${playerId} until=${parsed}`);
        return;
      }
    }
  }

  if (leaderboardsApi) {
    try {
      await leaderboardsApi.deleteLeaderboardPlayerScore(projectId, "final-days", playerId);
      logger.info(`[checkBanStatus] BANNED — 점수 삭제 완료: playerId=${playerId}`);
    } catch (e) {
      logger.info(`[checkBanStatus] 점수 삭제 실패 (이미 없을 수 있음): ${e.message ?? e}`);
    }
  }

  const reason  = map.banReason ?? "";
  const payload = JSON.stringify({ reason, until: untilMs ?? 0 });
  logger.info(`[checkBanStatus] BANNED throw: playerId=${playerId} payload=${payload}`);

  const err = new Error(payload);
  err.applicationErrorCode = 2006;
  throw err;
}
