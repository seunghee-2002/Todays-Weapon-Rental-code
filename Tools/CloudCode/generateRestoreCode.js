/**
 * generateRestoreCode.js
 * UGS Cloud Code 모듈 — 복원 코드 생성
 *
 * 동작:
 *   1. 이미 다른 기기에서 복원이 완료된 상태(isRestoreUsed=true)면 발급을 거부한다.
 *      - 원 기기 초기화 감지 상태가 새 발급으로 지워지는 것을 방지.
 *   2. 호출 플레이어의 playerData/legacyData 를 읽어 스냅샷으로 저장한다.
 *   3. 랜덤 secret 을 생성해 저장하고, "playerId-secret" 형태의 복원 코드를 반환한다.
 *      - PlayerId 만 알아서는 복원할 수 없다. secret 은 소유자의 Cloud Save 에만 존재.
 *
 * 에러 코드:
 *   1003 — 저장 데이터 없음
 *   1005 — 이전 복원이 사용 완료됨 (원 기기 초기화 선행 필요)
 *
 * 반환:
 *   { restoreCode: string }
 */

const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const dataApi = new DataApi(context);

  // ── 0. 사용 완료 상태 확인 ──────────────────────────────────
  const stateResult = await dataApi.getItems(projectId, playerId, ["isRestoreUsed"]);
  const usedItem = (stateResult.data.results ?? []).find((i) => i.key === "isRestoreUsed");
  if (usedItem?.value === "true") {
    const err = new Error("이전 복원 코드가 이미 사용되었습니다. 이 기기를 초기화한 뒤 다시 시도해주세요.");
    err.applicationErrorCode = 1005;
    throw err;
  }

  // ── 1. 자신의 playerData/legacyData 읽기 ───────────────────────────
  // legacyData(유산/영구 데이터)도 스냅샷에 포함해야 기기 이전 시 명성/해금/영구 재화가 유지된다
  const result = await dataApi.getItems(projectId, playerId, ["playerData", "legacyData"]);
  const itemMap = {};
  for (const item of (result.data.results ?? [])) {
    itemMap[item.key] = item;
  }

  if (!itemMap["playerData"]?.value) {
    const err = new Error("저장된 게임 데이터가 없습니다.");
    err.applicationErrorCode = 1003;
    throw err;
  }

  const playerDataJson = itemMap["playerData"].value;
  const legacyDataJson = itemMap["legacyData"]?.value ?? "";
  const generatedAt    = String(Date.now());
  const restoreSecret  = generateSecret();

  // ── 2. 복원 스냅샷 + secret 저장 ───────────────────────────────────
  // Cloud Save 는 본인만 읽을 수 있으므로 secret 이 타인에게 노출되지 않는다.
  await dataApi.setItemBatch(projectId, playerId, {
    data: [
      { key: "restorePlayerData",  value: playerDataJson, writeLock: null },
      { key: "restoreLegacyData",  value: legacyDataJson, writeLock: null },
      { key: "restoreGeneratedAt", value: generatedAt,    writeLock: null },
      { key: "restoreSecret",      value: restoreSecret,  writeLock: null },
      { key: "isRestoreUsed",      value: "false",        writeLock: null }
    ]
  });

  const restoreCode = playerId + "-" + restoreSecret;
  logger.info(`[generateRestoreCode] playerId=${playerId} generatedAt=${generatedAt}`);
  return { restoreCode };
};

// ── 헬퍼 ────────────────────────────────────────────────────────────────────

/**
 * 충분한 엔트로피의 복원 secret 생성.
 * UGS Cloud Code 런타임은 crypto 모듈이 없어 Math.random 조합을 사용한다
 * (startGameSession 토큰과 동일 방식). secret 은 서버 저장값과 대조로만 쓰인다.
 */
function generateSecret() {
  const t  = Date.now().toString(36);
  const r1 = Math.random().toString(36).substring(2);
  const r2 = Math.random().toString(36).substring(2);
  return (t + r1 + r2).substring(0, 24);
}
