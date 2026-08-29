/**
 * getPlayerNicknames.js
 * UGS Cloud Code 모듈 — 다수 플레이어의 playerNickname 일괄 조회 (리더보드 닉네임 매핑용).
 *
 * params:
 *   playerIds (JSON array of string) — 닉네임을 조회할 PlayerId 목록 (1~100)
 *
 * 반환:
 *   { nicknames: { [playerId]: nickname } }
 *   - 닉네임이 없거나 조회 실패한 PlayerId 는 결과 map 에서 제외 (클라이언트에서 MaskPlayerId fallback)
 *
 * 에러 코드:
 *   3001 INVALID_PLAYER_IDS — 배열이 아니거나 비어 있음 / 100개 초과 / 항목 타입 불일치
 *
 * 권한:
 *   - Cloud Code 모듈은 service-level 권한이므로 임의 PlayerId 의 Protected access class
 *     데이터를 조회할 수 있다. 닉네임은 Protected에만 저장된다 —
 *     클라이언트가 직접 쓴 default class 값은 표시에 사용하지 않는다.
 */

const { DataApi } = require("@unity-services/cloud-save-1.4");

const MAX_PLAYER_IDS = 100;

module.params = {
  playerIds: { type: "JSON", required: true }
};

module.exports = async ({ params, context, logger }) => {
  const { projectId } = context;
  const { playerIds } = params;

  // ── 1. 입력 검증 ────────────────────────────────────────────────────
  if (!Array.isArray(playerIds) || playerIds.length === 0) {
    const err = new Error("playerIds 는 비어있지 않은 배열이어야 합니다.");
    err.applicationErrorCode = 3001;
    throw err;
  }
  if (playerIds.length > MAX_PLAYER_IDS) {
    const err = new Error(`playerIds 길이가 최대치(${MAX_PLAYER_IDS})를 초과했습니다.`);
    err.applicationErrorCode = 3001;
    throw err;
  }
  for (const id of playerIds) {
    if (typeof id !== "string" || id.length === 0) {
      const err = new Error("playerIds 항목은 모두 비어있지 않은 string 이어야 합니다.");
      err.applicationErrorCode = 3001;
      throw err;
    }
  }

  // 중복 제거 (같은 PlayerId 중복 시 호출 낭비 방지)
  const uniqueIds = Array.from(new Set(playerIds));

  // ── 2. 병렬 조회 ────────────────────────────────────────────────────
  const dataApi   = new DataApi(context);
  const nicknames = {};

  await Promise.all(uniqueIds.map(async (pid) => {
    try {
      const resp = await dataApi.getProtectedItems(projectId, pid, ["playerNickname"]);
      for (const item of (resp.data.results ?? [])) {
        if (item.key === "playerNickname" && typeof item.value === "string" && item.value.length > 0) {
          const sanitized = sanitizeDisplayName(item.value);
          if (sanitized.length > 0) nicknames[pid] = sanitized;
        }
      }
    } catch (e) {
      // 개별 실패는 결과에서 제외만 하고 전체 호출은 계속 진행 (클라이언트 fallback 으로 처리)
      logger.error(`[getPlayerNicknames] 조회 실패 playerId=${pid} msg=${e.message ?? e}`);
    }
  }));

  logger.info(`[getPlayerNicknames] requested=${uniqueIds.length} resolved=${Object.keys(nicknames).length}`);
  return { nicknames };
};

// ── 헬퍼 ────────────────────────────────────────────────────────────────────

/**
 * 클라이언트가 Cloud Save 에 직접 저장한 비정상 닉네임(TMP rich text 태그 등)이
 * 리더보드 UI 를 오염시키지 않도록 표시 직전에 서버 측에서 최종 정규화한다.
 * changeNickname.js 의 MAX_LENGTH(12)와 동기화.
 */
function sanitizeDisplayName(name) {
  return String(name || "")
    .replace(/[<>]/g, "")
    .trim()
    .slice(0, 12);
}
