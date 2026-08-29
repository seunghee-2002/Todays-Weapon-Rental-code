using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 상점 위/아래 길을 지나가는 경량 행인(WalkerNPC) 관리.
    /// 별도 타이머로 상시 스폰하며 저장하지 않는다.
    /// </summary>
    public partial class VisitorManager
    {
        private readonly List<WalkerNPC> activeWalkers = new();

        // 행인 스폰 타이머 (게임 시간 초 단위, 모험가 스폰과 동일하게 OnTimeChanged에서 +3f)
        private float lastWalkerSpawnTime = 0f;
        private float nextWalkerSpawnInterval = 0f;

        // 큰 스킵(10분 초과)이면 스킵 시작 시점에 즉시 제거하고, 스킵 종료 후 재시드하도록 예약
        private bool pendingWalkerReseed = false;

        /// <summary>
        /// 스킵 시작 시 호출(TimeManager.OnTimeSkipStarted). 10분 초과 스킵이면 행인을 즉시 제거하고
        /// 스킵 종료 후 페이즈별 재시드를 예약한다. 10분 이하 짧은 스킵은 무시(기존 행인 유지).
        /// </summary>
        private void HandleTimeSkipStarted(float totalMinutes)
        {
            if (totalMinutes <= 10f) return;

            ClearAllWalkers();
            pendingWalkerReseed = true;
        }

        /// <summary>OnTimeChanged마다 호출 — 밤을 제외하고 간격이 되면 행인을 스폰한다.</summary>
        private void UpdateWalkerSpawn()
        {
            if (walkerPrefab == null || TimeManager.Instance == null) return;

            // 스킵 중에는 스폰하지 않는다(제거는 스킵 시작 시점의 HandleTimeSkipStarted에서 이미 처리).
            if (TimeManager.Instance.IsSkippingTime) return;

            // 큰 스킵 직후: 스킵이 끝난 첫 정상 틱에 페이즈별로 재시드.
            if (pendingWalkerReseed)
            {
                pendingWalkerReseed = false;
                SpawnRestoreWalkers();
            }

            if (TimeManager.Instance.CurrentPhase == TimePhase.Night) return;

            // 최초 1회 간격 시드 (즉시 스폰 방지)
            if (nextWalkerSpawnInterval <= 0f)
            {
                nextWalkerSpawnInterval = Random.Range(Config.walkerSpawnMinInterval, Config.walkerSpawnMaxInterval);
                lastWalkerSpawnTime = 0f;
                return;
            }

            lastWalkerSpawnTime += 3f;
            if (lastWalkerSpawnTime < nextWalkerSpawnInterval) return;

            SpawnWalkerRun();
            lastWalkerSpawnTime = 0f;
            nextWalkerSpawnInterval = Random.Range(Config.walkerSpawnMinInterval, Config.walkerSpawnMaxInterval);
        }

        /// <summary>
        /// 스폰 1회분(run). 현재 페이즈별 시행 횟수만큼 독립 시행하여, 각 시행마다 walkerTrialChance 확률로
        /// 무리 하나를 스폰한다. → 한 run에 0~여러 무리가 랜덤하게 출몰(고정 1개가 아니라 더 불규칙).
        /// - 각 시행은 자기 길·방향을 독립 추첨한다(시행끼리는 서로 다른 쪽에서 나올 수 있음).
        /// - 무리 인원은 그 시행이 뽑은 '같은 쪽'에서 함께 나온다(depth·속도만 각자 다름).
        /// </summary>
        private void SpawnWalkerRun()
        {
            int trials = Mathf.Max(0, GetTrialsForPhase());
            for (int t = 0; t < trials; t++)
            {
                if (Random.value >= Config.walkerTrialChance) continue;

                int size = PickWalkerGroupSize();
                bool topRoad = Random.value < Config.walkerTopRoadChance;
                bool fromLeft = Random.value < 0.5f;

                if (size <= 1)
                    SpawnWalker(topRoad, fromLeft);
                else
                    StartCoroutine(SpawnWalkerGroupRoutine(size, topRoad, fromLeft));
            }
        }

        // 무리 인원: 1명 80% / 2명 15% / 3명 4.99% / 5명 0.01% (4명 없음)
        private int PickWalkerGroupSize()
        {
            float r = Random.value;
            if (r < 0.80f) return 1;
            if (r < 0.95f) return 2;
            if (r < 0.9999f) return 3;
            return 5;
        }

        // 무리 인원을 짧은 시간차로 하나씩 내보낸다(같은 길·방향, 각자 depth·속도는 다름).
        private IEnumerator SpawnWalkerGroupRoutine(int size, bool topRoad, bool fromLeft)
        {
            for (int i = 0; i < size; i++)
            {
                SpawnWalker(topRoad, fromLeft);
                if (i < size - 1)
                    yield return new WaitForSeconds(Random.Range(Config.walkerGroupStagger * 0.5f, Config.walkerGroupStagger));
            }
        }

        // Run/Walk 비율 제어: walkerRunChance만큼만 뛰고, 대부분은 걷는다(1~threshold).
        private float PickWalkerSpeed()
        {
            return Random.value < Config.walkerRunChance
                ? Random.Range(Config.walkerRunSpeedThreshold, Config.walkerMaxSpeed)   // Run
                : Random.Range(Config.walkerMinSpeed, Config.walkerRunSpeedThreshold);  // Walk
        }

        // 현재 페이즈별 독립 시행 횟수 (밤은 앞의 가드로 애초에 스폰 안 됨)
        private int GetTrialsForPhase()
        {
            return TimeManager.Instance.CurrentPhase switch
            {
                TimePhase.Morning => Config.walkerTrialsMorning,
                TimePhase.Day     => Config.walkerTrialsDay,
                TimePhase.Evening => Config.walkerTrialsEvening,
                _ => 0
            };
        }

        private void SpawnWalker(bool topRoad, bool fromLeft) => SpawnWalkerAt(topRoad, fromLeft, 0f);

        /// <summary>
        /// startProgress: 0이면 길 끝(정상 스폰), 0~1이면 경로 중간에서 시작(복원 시 x 랜덤 배치용).
        /// </summary>
        private void SpawnWalkerAt(bool topRoad, bool fromLeft, float startProgress)
        {
            float depth = Random.value;   // 좌우 공통 y 비율 (수평 이동 유지)

            Transform leftTop, leftBottom, rightTop, rightBottom;
            if (topRoad)
            {
                leftTop = topRoadLeftTop; leftBottom = topRoadLeftBottom;
                rightTop = topRoadRightTop; rightBottom = topRoadRightBottom;
            }
            else
            {
                leftTop = bottomRoadLeftTop; leftBottom = bottomRoadLeftBottom;
                rightTop = bottomRoadRightTop; rightBottom = bottomRoadRightBottom;
            }

            bool leftValid = leftTop != null || leftBottom != null;
            bool rightValid = rightTop != null || rightBottom != null;
            if (!leftValid || !rightValid) return;

            Vector3 leftPos = LerpRoadSegment(leftTop, leftBottom, depth);
            Vector3 rightPos = LerpRoadSegment(rightTop, rightBottom, depth);
            Vector3 fullStart = fromLeft ? leftPos : rightPos;
            Vector3 fullEnd   = fromLeft ? rightPos : leftPos;
            Vector3 start = Vector3.Lerp(fullStart, fullEnd, Mathf.Clamp01(startProgress));

            var appearance = GenerateWalkerAppearance();
            float speed = PickWalkerSpeed();

            GameObject obj = Instantiate(walkerPrefab, start, Quaternion.identity, walkerParent);
            var walker = obj.GetOrAddComponent<WalkerNPC>();
            walker.Initialize(appearance, start, fullEnd, speed, Config.walkerRunSpeedThreshold);

            activeWalkers.RemoveAll(w => w == null);   // 도착해 파괴된 행인 정리
            activeWalkers.Add(walker);
        }

        /// <summary>
        /// 게임 불러오기(씬 시작) 시 현재 페이즈에 맞는 수의 행인을 경로 중간(x·y 랜덤)에 즉시 스폰한다.
        /// 빈 거리로 시작하지 않도록 하는 시드 스폰.
        /// </summary>
        public void SpawnRestoreWalkers()
        {
            if (walkerPrefab == null || TimeManager.Instance == null) return;

            int count = TimeManager.Instance.CurrentPhase switch
            {
                TimePhase.Morning => Config.walkerRestoreMorning,
                TimePhase.Day     => Config.walkerRestoreDay,
                TimePhase.Evening => Config.walkerRestoreEvening,
                _ => 0
            };

            for (int i = 0; i < count; i++)
            {
                bool topRoad = Random.value < Config.walkerTopRoadChance;
                bool fromLeft = Random.value < 0.5f;
                SpawnWalkerAt(topRoad, fromLeft, Random.Range(0.1f, 0.85f));   // 경로 중간 어딘가
            }
        }

        private AdventurerAppearanceData GenerateWalkerAppearance()
        {
            if (appearanceGeneratorData == null) return null;

            Gender gender = Random.value < 0.5f ? Gender.Male : Gender.Female;
            var jobs = (AdventurerJobType[])System.Enum.GetValues(typeof(AdventurerJobType));
            var job = jobs[Random.Range(0, jobs.Length)];

            return AppearanceGenerator.Generate(appearanceGeneratorData, gender, job);
        }

        /// <summary>모험가 상호작용 줌 시 방문자와 함께 행인도 페이드한다.</summary>
        public void FadeWalkers(bool faded)
        {
            foreach (var walker in activeWalkers)
            {
                if (walker == null) continue;
                walker.SetFaded(faded);
            }
        }

        /// <summary>새 날 시작 시 남은 행인을 모두 정리한다.</summary>
        private void ClearAllWalkers()
        {
            foreach (var walker in activeWalkers)
            {
                if (walker != null && walker.gameObject != null)
                    Destroy(walker.gameObject);
            }
            activeWalkers.Clear();

            lastWalkerSpawnTime = 0f;
            nextWalkerSpawnInterval = 0f;
        }
    }
}
