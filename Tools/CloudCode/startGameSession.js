/**
 * startGameSession.js
 * UGS Cloud Code 모듈 — 새 게임 세션 초기화
 *
 * 동작:
 *   1. 서버 측에서 랜덤 세션 토큰 생성 후 Cloud Save Protected access class에 저장
 *      - Protected는 클라이언트가 읽기만 가능하고 쓰기 불가 → 토큰 위조/시작시각 조작/사용플래그 리셋 차단
 *   2. sessionToken, 시작 시각, 사용 여부를 저장
 *   3. sessionToken만 클라이언트에 반환
 *
 * NOTE: UGS Cloud Code 런타임은 Node.js built-in 'crypto' 모듈을 지원하지 않으므로
 *       Math.random() 기반 토큰 생성을 사용한다.
 *       토큰은 서버가 생성해 Cloud Save에 저장하므로 클라이언트 위조 시
 *       submitLeaderboardScore의 토큰 대조 단계에서 차단된다.
 *
 * 밴 검사:
 *   본 스크립트는 밴 검사를 수행하지 않는다.
 *   - 앱 실행 시점: 별도 checkBanStatus Cloud Code 가 담당.
 *   - 랭킹 등록 시점: submitLeaderboardScore 가 자체 검사.
 *   따라서 밴된 계정도 새 게임 자체는 시작할 수 있으나 점수 등재는 차단된다.
 *
 * 반환:
 *   { sessionToken: string }
 */

const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;

  const dataApi        = new DataApi(context);
  const sessionToken   = generateSessionToken();
  const sessionStartAt = String(Date.now());

  // Protected access class - 클라이언트 쓰기 불가
  await dataApi.setProtectedItemBatch(projectId, playerId, {
    data: [
      { key: "sessionToken",     value: sessionToken,   writeLock: null },
      { key: "sessionStartAt",   value: sessionStartAt, writeLock: null },
      { key: "sessionTokenUsed", value: "false",        writeLock: null }
    ]
  });

  logger.info(`[startGameSession] playerId=${playerId} session started at=${sessionStartAt}`);
  return { sessionToken };
};

// ── 헬퍼 ────────────────────────────────────────────────────────────────────

/**
 * 충분한 엔트로피를 가진 세션 토큰을 생성한다.
 * 타임스탬프(36진수) + 두 개의 Math.random 문자열을 결합.
 */
function generateSessionToken() {
  const timestamp = Date.now().toString(36);
  const rand1     = Math.random().toString(36).substring(2);
  const rand2     = Math.random().toString(36).substring(2);
  return timestamp + rand1 + rand2;
}
