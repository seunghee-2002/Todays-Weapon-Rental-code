/**
 * checkBanStatus.js
 * UGS Cloud Code 모듈 — 밴 상태 조회 (앱 실행 시 호출)
 *
 * 호출 시점:
 *   클라이언트가 UGS 익명 로그인을 마치고 곧바로 호출한다 (UGSManager).
 *   submitLeaderboardScore 가 랭킹 등록 시 자체적으로 다시 검사하므로,
 *   여기서의 결과는 클라 UI 안내(팝업) 용도일 뿐 보안 우회 가능성은 없다.
 *
 * 동작:
 *   1. Protected + Default access class 에서 banned/banReason/banUntil 키를 읽는다.
 *      (운영자가 어디에 키를 두든 동작하도록 두 영역 모두 조회)
 *   2. banUntil 이 있고 만료됐으면 비밴으로 응답.
 *   3. 밴이면 기존 리더보드 점수를 best-effort 로 삭제한다.
 *
 * 반환 (Cloud Code 응답 message 가 아닌 정상 응답 body):
 *   {
 *     banned: boolean,
 *     reason: string,   // 비밴 시 ""
 *     until:  number    // ms epoch, 영구 밴 또는 비밴 시 0
 *   }
 *
 * NOTE: 이 모듈은 throw 하지 않는다 — applicationErrorCode 분기 없이
 *       클라이언트가 응답 body 만으로 분기하도록 한다.
 *       오프라인/일시 장애 시에도 클라가 ban 정보 없음으로 처리하면 됨.
 */

const { DataApi }         = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.0");

const LEADERBOARD_ID = "final-days";
const BAN_KEYS       = ["banned", "banReason", "banUntil"];

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;

  const dataApi         = new DataApi(context);
  const leaderboardsApi = new LeaderboardsApi(context);

  const map = {};

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

  // (2) Default access class fallback
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
    logger.info(`[checkBanStatus] 비밴 playerId=${playerId} banned='${map.banned ?? "(none)"}'`);
    return { banned: false, reason: "", until: 0 };
  }

  // 기간 밴 만료 체크
  let untilMs = 0;
  const untilStr = map.banUntil;
  if (untilStr) {
    const parsed = parseInt(untilStr, 10);
    if (!isNaN(parsed)) {
      untilMs = parsed;
      if (Date.now() >= parsed) {
        logger.info(`[checkBanStatus] banUntil 만료 playerId=${playerId} until=${parsed}`);
        return { banned: false, reason: "", until: 0 };
      }
    }
  }

  // 점수 best-effort 삭제 (재호출 시 멱등)
  try {
    await leaderboardsApi.deleteLeaderboardPlayerScore(projectId, LEADERBOARD_ID, playerId);
    logger.info(`[checkBanStatus] BANNED — 점수 삭제 완료 playerId=${playerId}`);
  } catch (e) {
    logger.info(`[checkBanStatus] 점수 삭제 실패 (이미 없을 수 있음): ${e.message ?? e}`);
  }

  const reason = map.banReason ?? "";
  logger.info(`[checkBanStatus] BANNED 응답 playerId=${playerId} reason='${reason}' until=${untilMs}`);

  return { banned: true, reason, until: untilMs };
};
