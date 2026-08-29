/**
 * checkAppVersion.js
 * UGS Cloud Code 모듈 — 강제 업데이트 최소 버전 조회 (앱 실행 시 호출)
 *
 * 호출 시점:
 *   클라이언트가 UGS 익명 로그인을 마치고 곧바로 호출한다 (UGSManager).
 *   네트워크 재연결 시에도 다시 호출된다.
 *
 * 동작:
 *   MIN_VERSION 상수를 그대로 반환한다. 비교/판정은 클라이언트가 수행한다.
 *   클라이언트는 받은 값을 PlayerPrefs 에 기록해 오프라인 실행에서도 같은 판정을 재현한다.
 *
 * 반환:
 *   { minVersion: string }   // Unity Player Settings 의 bundleVersion 과 같은 "x.y.z" 형식
 *
 * 운영 절차:
 *   업데이트를 강제하려면 MIN_VERSION 을 새 빌드의 bundleVersion 으로 올린 뒤 재배포한다.
 *   강제하지 않을 사소한 패치는 MIN_VERSION 을 그대로 두면 된다.
 *
 * NOTE: 이 모듈은 throw 하지 않는다 — checkBanStatus 와 동일하게, 클라이언트가
 *       응답 body 만으로 분기하고 실패 시에는 조용히 통과시키도록 한다.
 */

const MIN_VERSION = "0.0.3";

module.exports = async ({ params, context, logger }) => {
  logger.info(`[checkAppVersion] minVersion=${MIN_VERSION} playerId=${context.playerId}`);

  return { minVersion: MIN_VERSION };
};
