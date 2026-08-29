/**
 * redeemRestoreCode.js
 * UGS Cloud Code 모듈 — 복원 코드 사용 (검증 + 잠금 + 덮어쓰기)
 *
 * params:
 *   restoreCode (string) — generateRestoreCode 가 발급한 "playerId-secret" 형태의 코드
 *
 * 동작:
 *   1. 코드를 targetPlayerId / secret 으로 분해
 *   2. targetPlayerId 의 복원 데이터 조회 (Cloud Code 서비스 권한)
 *   3. secret 불일치            → NOT_FOUND (1003) - 계정 존재 여부를 노출하지 않는다
 *   4. isRestoreUsed == "true"  → ALREADY_USED (1001)
 *   5. 생성 후 24시간 초과      → EXPIRED (1002)
 *   6. isRestoreUsed = "true"로 잠금 (writeLock 조건부 갱신)
 *   7. 호출 플레이어의 playerData/legacyData 덮어쓰기
 *   8. playerDataJson/legacyDataJson 반환
 *
 * 에러 코드:
 *   1001 ALREADY_USED — 이미 사용된 코드
 *   1002 EXPIRED      — 24시간 만료
 *   1003 NOT_FOUND    — 코드 형식 오류 / secret 불일치 / 복원 데이터 없음
 *   1004 SELF_RESTORE — 자기 자신 복원 시도
 */

const { DataApi } = require("@unity-services/cloud-save-1.4");

const EXPIRE_MS = 24 * 60 * 60 * 1000; // 24시간

module.params = {
  restoreCode: { type: "String", required: true }
};

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const dataApi = new DataApi(context); // Cloud Code 서비스 권한 → 모든 플레이어 데이터 접근 가능

  // ── 0. 코드 분해 ──────────────────────────────────────────────
  const raw = String(params.restoreCode ?? "").trim();
  const sep = raw.lastIndexOf("-");
  if (sep <= 0 || sep >= raw.length - 1) {
    const err = new Error("복원 코드 형식이 올바르지 않습니다.");
    err.applicationErrorCode = 1003;
    throw err;
  }
  const targetPlayerId = raw.substring(0, sep);
  const secret         = raw.substring(sep + 1);

  // ── 0.5 자기 자신 복원 차단 ──────────────────────────────────────────
  if (targetPlayerId === playerId) {
    const err = new Error("자기 자신의 복원 코드로는 복원할 수 없습니다.");
    err.applicationErrorCode = 1004;
    throw err;
  }

  // ── 1. targetPlayerId의 복원 데이터 조회 ────────────────────────────
  const readResult = await dataApi.getItems(projectId, targetPlayerId, [
    "restorePlayerData",
    "restoreLegacyData",
    "restoreGeneratedAt",
    "restoreSecret",
    "isRestoreUsed"
  ]);

  const itemMap = {};
  for (const item of (readResult.data.results ?? [])) {
    itemMap[item.key] = item; // { key, value, writeLock }
  }

  // ── 2. 검증 ──────────────────────────────────────────────────────────
  // secret 불일치와 데이터 없음을 동일한 오류로 처리해 계정 존재 여부를 노출하지 않는다
  if (!itemMap["restorePlayerData"]?.value ||
      !itemMap["restoreSecret"]?.value ||
      itemMap["restoreSecret"].value !== secret) {
    const err = new Error("복원 데이터를 찾을 수 없습니다.");
    err.applicationErrorCode = 1003;
    throw err;
  }

  if (itemMap["isRestoreUsed"]?.value === "true") {
    const err = new Error("이미 사용된 복원 코드입니다.");
    err.applicationErrorCode = 1001;
    throw err;
  }

  const generatedAtMs = parseInt(itemMap["restoreGeneratedAt"]?.value ?? "0", 10);
  if (isNaN(generatedAtMs) || Date.now() - generatedAtMs > EXPIRE_MS) {
    const err = new Error("만료된 복원 코드입니다. (24시간 초과)");
    err.applicationErrorCode = 1002;
    throw err;
  }

  // ── 3. 잠금: targetPlayerId의 isRestoreUsed = "true" 쓰기 ───────────
  await dataApi.setItemBatch(projectId, targetPlayerId, {
    data: [{
      key:       "isRestoreUsed",
      value:     "true",
      writeLock: itemMap["isRestoreUsed"]?.writeLock ?? null // 낙관적 동시성 제어
    }]
  });

  // ── 4. 호출 플레이어의 playerData/legacyData 덮어쓰기 ───────────────
  // legacyData도 함께 복원해야 새 기기의 빈 유산이 기존 진행을 덮지 않는다
  const playerDataJson = itemMap["restorePlayerData"].value;
  const legacyDataJson = itemMap["restoreLegacyData"]?.value ?? "";

  const writeData = [{ key: "playerData", value: playerDataJson, writeLock: null }];
  if (legacyDataJson) {
    writeData.push({ key: "legacyData", value: legacyDataJson, writeLock: null });
  }

  await dataApi.setItemBatch(projectId, playerId, { data: writeData });

  logger.info(`[redeemRestoreCode] caller=${playerId} target=${targetPlayerId} 복원 완료 (legacy=${legacyDataJson ? "O" : "X"})`);
  return { playerDataJson, legacyDataJson };
};
