/**
 * uploadSave.js
 * UGS Cloud Code 모듈 — 세이브 업로드 + 서버 시각 스탬프
 *
 * params:
 *   playerDataJson (string, optional) — GameData JSON. 제공 시 playerData 키에 저장
 *   legacyDataJson (string, optional) — PlayerData(legacy) JSON. 제공 시 legacyData 키에 저장
 *
 * 동작:
 *   제공된 데이터를 Default access class에 저장하면서 서버 시각(ms)을 함께 기록한다.
 *   클라이언트 시계 대신 이 스탬프(gameServerSavedAtMs/legacyServerSavedAtMs)로
 *   동기화 우선순위를 판단하면 기기 시간 조작으로 오래된 저장이 최신으로 오판되지 않는다.
 *
 * 반환:
 *   { gameStampMs: number, legacyStampMs: number }
 *   - 저장하지 않은 쪽 스탬프는 0
 *
 * 에러 코드:
 *   4001 INVALID_PAYLOAD — 두 데이터 모두 비어 있음
 */

const { DataApi } = require("@unity-services/cloud-save-1.4");

module.params = {
  playerDataJson: { type: "String", required: false },
  legacyDataJson: { type: "String", required: false }
};

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const dataApi = new DataApi(context);

  const playerDataJson = typeof params.playerDataJson === "string" ? params.playerDataJson : "";
  const legacyDataJson = typeof params.legacyDataJson === "string" ? params.legacyDataJson : "";

  if (!playerDataJson && !legacyDataJson) {
    const err = new Error("업로드할 데이터가 없습니다.");
    err.applicationErrorCode = 4001;
    throw err;
  }

  const nowMs = Date.now();
  const data = [];
  let gameStampMs = 0;
  let legacyStampMs = 0;

  if (playerDataJson) {
    data.push({ key: "playerData",          value: playerDataJson,  writeLock: null });
    data.push({ key: "gameServerSavedAtMs", value: String(nowMs),   writeLock: null });
    gameStampMs = nowMs;
  }

  if (legacyDataJson) {
    data.push({ key: "legacyData",            value: legacyDataJson, writeLock: null });
    data.push({ key: "legacyServerSavedAtMs", value: String(nowMs),  writeLock: null });
    legacyStampMs = nowMs;
  }

  await dataApi.setItemBatch(projectId, playerId, { data });

  logger.info(`[uploadSave] playerId=${playerId} game=${playerDataJson ? "O" : "X"} legacy=${legacyDataJson ? "O" : "X"} stamp=${nowMs}`);
  return { gameStampMs, legacyStampMs };
};
