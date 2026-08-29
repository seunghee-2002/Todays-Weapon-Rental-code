using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험진행 카드의 몬스터를 RenderTexture로 렌더링하는 공유 스테이지.
    /// 월드 한켠(메인 카메라 밖, 전용 레이어)에 몬스터를 생성하고 전용 카메라로 RT에 그린다.
    /// 캐러셀 중앙 카드 1장만 몬스터를 보여주므로 씬에 인스턴스 1개를 두고 공유한다.
    /// 카드의 monsterView(RawImage)가 이 RT를 표시 → 몬스터가 평범한 UI처럼 합성(정렬/클리핑/스케일 문제 없음).
    /// </summary>
    public class MonsterStageRenderer : MonoBehaviour
    {
        // 진행 카드용 공유 스테이지
        public static MonsterStageRenderer Instance { get; private set; }
        // 리플레이용 전용 스테이지 — 카메라 orthographicSize를 작게 잡아 몬스터를 크게 렌더한다(진행 카드와 분리)
        public static MonsterStageRenderer ReplayInstance { get; private set; }

        [Tooltip("체크 시 리플레이 전용 스테이지로 등록(ReplayInstance). 미체크면 진행 카드용(Instance).")]
        [SerializeField] private bool isReplayStage = false;
        [SerializeField] private Camera stageCamera;        // 직교, cullingMask=MonsterStage, clear=Solid(알파0), targetTexture=renderTexture
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private Transform spawnPoint;       // 카메라가 비추는 몬스터 생성 위치
        [SerializeField] private Transform particleSpawnPoint;   // 피격 파티클 생성 위치(미지정 시 spawnPoint 사용)
        [SerializeField] private string stageLayerName = "MonsterStage";

        public RenderTexture RenderTexture => renderTexture;

        private GameObject current;
        private int stageLayer = -1;
        private float defaultOrthoSize = 1f;   // 인스펙터에 설정된 카메라 기본 ortho — 프리셋 없는 스폰 시 복원

        #region 초기화

        private void Awake()
        {
            if (isReplayStage) ReplayInstance = this;
            else               Instance = this;
            stageLayer = LayerMask.NameToLayer(stageLayerName);
            if (stageCamera != null)
            {
                defaultOrthoSize = stageCamera.orthographicSize;
                stageCamera.targetTexture = renderTexture;
                stageCamera.enabled = false;   // 몬스터 없을 땐 렌더 정지(비용 절감)
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ReplayInstance == this) ReplayInstance = null;
        }

        #endregion

        #region 몬스터 / 파티클

        /// <summary>몬스터 프리팹을 스테이지에 생성하고 래퍼를 반환. category로 카메라 ortho 크기·스폰 오프셋(MonsterStageConfig)을 적용한다. 카메라 렌더 시작.</summary>
        public MonsterStageActor Spawn(GameObject prefab, MonsterCategory category)
        {
            Clear();
            if (prefab == null || spawnPoint == null) return null;

            ApplyCategoryPreset(category);   // 카메라 ortho + spawnPoint 위치를 먼저 확정한 뒤 생성
            current = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
            SetLayerRecursive(current, stageLayer);
            if (stageCamera != null) stageCamera.enabled = true;

            return current.GetComponent<MonsterStageActor>() ?? current.AddComponent<MonsterStageActor>();
        }

        // 카테고리별 프리셋(카메라 ortho 크기 + spawnPoint 오프셋) 적용.
        // 매 스폰마다 ortho·spawnPoint 위치를 명시적으로 재설정해 이전 카테고리 값이 남지 않게 한다(프리셋 없으면 카메라/위치 기본값).
        private void ApplyCategoryPreset(MonsterCategory category)
        {
            float ortho = defaultOrthoSize;
            Vector2 offset = Vector2.zero;

            var config = ConfigManager.Instance != null ? ConfigManager.Instance.MonsterStage : null;
            if (config != null && config.TryGetPreset(isReplayStage, category, out var preset))
            {
                if (preset.orthographicSize > 0f) ortho = preset.orthographicSize;
                offset = preset.spawnLocalOffset;
            }

            if (stageCamera != null) stageCamera.orthographicSize = ortho;
            if (spawnPoint != null)
            {
                var lp = spawnPoint.localPosition;
                spawnPoint.localPosition = new Vector3(offset.x, offset.y, lp.z);
            }
        }

        /// <summary>몬스터 제거 + 카메라 렌더 정지.</summary>
        public void Clear()
        {
            if (current != null) Destroy(current);
            current = null;
            if (stageCamera != null) stageCamera.enabled = false;
        }

        /// <summary>현재 스테이지 화면을 새 RenderTexture로 캡처해 반환(정적 스냅샷). 여러 노드가 동시에 몬스터를 표시할 때
        /// 공유 RT 대신 노드별 정적 이미지로 고정하는 데 사용. 호출측이 소유/Release 책임을 진다.</summary>
        public RenderTexture CaptureSnapshot()
        {
            if (stageCamera == null || renderTexture == null) return null;
            stageCamera.Render();   // 최신 프레임을 공유 RT에 강제 렌더
            var snap = new RenderTexture(renderTexture.descriptor);   // 크기/포맷/색공간 정확히 복제
            Graphics.Blit(renderTexture, snap);
            return snap;
        }

        /// <summary>몬스터 피격 위치에 파티클 생성(RT에 함께 렌더됨). 위치는 particleSpawnPoint, 미지정 시 spawnPoint.
        /// simulationSpeed: 재생 배속(라이브 연출의 게임 배속 반영, 기본 1).</summary>
        public void SpawnParticle(GameObject prefab, float lifetime = 3f, float simulationSpeed = 1f)
        {
            var anchor = particleSpawnPoint != null ? particleSpawnPoint : spawnPoint;
            if (prefab == null || anchor == null) return;
            var p = Instantiate(prefab, anchor.position, anchor.rotation, anchor);
            SetLayerRecursive(p, stageLayer);
            if (!Mathf.Approximately(simulationSpeed, 1f))
                foreach (var ps in p.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.simulationSpeed = simulationSpeed;
                }
            Destroy(p, lifetime);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        #endregion
    }
}
