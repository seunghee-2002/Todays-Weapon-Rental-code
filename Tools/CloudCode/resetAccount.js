/**
 * resetAccount.js
 * UGS Cloud Code 모듈 — 데이터 초기화 시 호출 플레이어 자신의 서버 흔적 정리
 *
 * 호출 시점:
 *   UGSManager.ResetAccountAsync 가 새 계정을 발급받기 직전(= 옛 계정으로 인증된 상태)에 호출한다.
 *   SignOut 이후에 호출하면 새 계정 기준으로 동작해 옛 계정 데이터가 그대로 남는다.
 *
 * 동작:
 *   1. final-days 리더보드에서 자신의 점수 삭제 (best-effort, 기록이 없으면 무시)
 *      - 지우지 않으면 옛 PlayerId 의 기록이 랭킹에 남고,
 *        getPlayerNicknames 가 옛 닉네임을 계속 반환해 초기화 후에도 옛 닉네임이 노출된다
 *   2. Protected 의 playerNickname / nicknameChangeCount 를 빈 값으로 덮어쓰기
 *      - 삭제 API 대신 덮어쓰기를 쓰는 이유: setProtectedItemBatch 는 기존 모듈에서 검증된 경로다
 *
 * 지우지 않는 것:
 *   banned / banReason / banUntil — 운영 기록이므로 클라이언트 요청으로 삭제하지 않는다.
 *   어차피 계정 자체를 버리므로 이 키를 남겨도 새 계정에는 영향이 없다.
 *
 * 반환:
 *   { success: boolean }  — 두 단계 중 하나라도 예외로 끝나면 false (클라이언트는 로그만 남기고 진행)
 */

const { DataApi }         = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.0");

const LEADERBOARD_ID = "final-days";

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;

  const dataApi         = new DataApi(context);
  const leaderboardsApi = new LeaderboardsApi(context);

  let success = true;

  // (1) 리더보드 점수 삭제 — 등록 기록이 없으면 실패하므로 실패를 성공 판정에 넣지 않는다
  try {
    await leaderboardsApi.deleteLeaderboardPlayerScore(projectId, LEADERBOARD_ID, playerId);
    logger.info(`[resetAccount] 점수 삭제 완료 playerId=${playerId}`);
  } catch (e) {
    logger.info(`[resetAccount] 점수 삭제 실패 (등록 기록 없을 수 있음): ${e.message ?? e}`);
  }

  // (2) 닉네임/변경 횟수 비우기
  try {
    await dataApi.setProtectedItemBatch(projectId, playerId, {
      data: [
        { key: "playerNickname",      value: "",  writeLock: null },
        { key: "nicknameChangeCount", value: "0", writeLock: null }
      ]
    });
    logger.info(`[resetAccount] 닉네임 초기화 완료 playerId=${playerId}`);
  } catch (e) {
    success = false;
    logger.error(`[resetAccount] 닉네임 초기화 실패 playerId=${playerId}: ${e.message ?? e}`);
  }

  return { success };
};
