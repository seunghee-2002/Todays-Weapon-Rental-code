/**
 * getNicknameInfo.js
 * UGS Cloud Code 모듈 — 호출 플레이어 자신의 닉네임/변경 횟수 조회
 *
 * 닉네임은 Protected access class(클라이언트 읽기 전용 - 쓰기 불가)에 저장된다.
 * 클라이언트(NicknameManager)는 이 엔드포인트로 자신의 닉네임 상태를 읽는다.
 *
 * 반환:
 *   { nickname: string, changeCount: number }
 *   - 닉네임 미설정이면 nickname = "" (클라이언트가 기본 닉네임 부여 흐름 진행)
 */

const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const dataApi = new DataApi(context);

  const result = await dataApi.getProtectedItems(projectId, playerId, [
    "playerNickname",
    "nicknameChangeCount"
  ]);

  let nickname = "";
  let changeCount = 0;
  for (const item of (result.data.results ?? [])) {
    if (item.key === "playerNickname" && typeof item.value === "string") {
      nickname = item.value;
    }
    if (item.key === "nicknameChangeCount") {
      const parsed = parseInt(item.value, 10);
      if (!isNaN(parsed)) changeCount = parsed;
    }
  }

  logger.info(`[getNicknameInfo] playerId=${playerId} nickname='${nickname}' changeCount=${changeCount}`);
  return { nickname, changeCount };
};
