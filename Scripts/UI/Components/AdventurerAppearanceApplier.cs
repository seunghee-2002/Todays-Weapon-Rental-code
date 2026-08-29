using LayerLab.ArtMaker;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// VisitorNPC에 부착하여 AdventurerAppearanceData를 PartsManager에 적용하는 컴포넌트
    /// PartsManager는 자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용
    /// </summary>
    public class AdventurerAppearanceApplier : MonoBehaviour
    {
        [SerializeField] private PartsManager partsManager;
        [SerializeField] private FixedAppearanceData previewAppearance; // 에디터 미리보기 및 외형 미지정 시 사용할 기본 외형
        public PartsManager GetPartsManager() => partsManager;

        private bool initialized;   // PartsManager.Init() 완료 여부 (스켈레톤당 1회면 충분)

        #region 초기화

        private void Awake()
        {
            if (partsManager == null)
                partsManager = GetComponentInChildren<PartsManager>();
        }

        #endregion

        #region 외형 적용

        public void ApplyAppearance(AdventurerAppearanceData appearance)
        {
            // 외형을 못 받으면 기본값(previewAppearance)으로 대체
            if (appearance == null)
            {
                if (previewAppearance != null && previewAppearance.appearance != null)
                {
                    Log.Info("[AdventurerAppearanceApplier] appearance가 null이라 previewAppearance를 사용합니다.");
                    appearance = previewAppearance.appearance;
                }
                else
                {
                    Log.Warn("[AdventurerAppearanceApplier] appearance가 null이고 previewAppearance도 지정되지 않았습니다.");
                    return;
                }
            }

            if (partsManager == null)
            {
                partsManager = GetComponentInChildren<PartsManager>();
                if (partsManager == null)
                {
                    Log.Error("[AdventurerAppearanceApplier] PartsManager를 찾을 수 없습니다.");
                    return;
                }
            }

            EnsureInitialized();
            ApplyPartIndices(appearance);
            ApplyColors(appearance);
        }

        /// <summary>
        /// PartsManager.Init()은 스켈레톤의 스킨 500여 개를 16개 카테고리로 재스캔하고 스킨을 두 번 재조립한다.
        /// 스켈레톤이 바뀌지 않는 한 결과가 같으므로 인스턴스당 1회만 실행한다.
        /// </summary>
        private void EnsureInitialized()
        {
            if (initialized) return;

            // Init()은 유효한 Skeleton이 있어야 스킨 목록을 채운다. 아직이면 먼저 초기화하고,
            // 그래도 준비되지 않았으면 플래그를 세우지 않아 다음 호출에서 재시도한다.
            var graphic = partsManager.GetSkeletonGraphic();
            if (graphic != null && !graphic.IsValid) graphic.Initialize(false);

            partsManager.Init();
            initialized = graphic != null ? graphic.IsValid : partsManager.GetSkeletonAnimation() != null;
        }

        public void ApplyAppearance(FixedAppearanceData fixedData)
        {
            if (fixedData == null)
            {
                Log.Warn("[AdventurerAppearanceApplier] fixedData가 null입니다.");
                return;
            }
            ApplyAppearance(fixedData.appearance);
        }

        private void ApplyPartIndices(AdventurerAppearanceData appearance)
        {
            var indices = appearance.ToPartsDictionary();

            foreach (PartsType partsType in System.Enum.GetValues(typeof(PartsType)))
            {
                if (partsType == PartsType.None) continue;
                if (!indices.ContainsKey(partsType))
                    indices[partsType] = 0;
            }

            partsManager.SetSkinActiveIndex(indices);
        }

        private void ApplyColors(AdventurerAppearanceData appearance)
        {
            partsManager.ChangeSkinColor(appearance.skinColor);
            partsManager.ChangeHairColor(appearance.hairColor);
            partsManager.ChangeBeardColor(appearance.beardColor);
            partsManager.ChangeBrowColor(appearance.browColor);
        }

        #endregion

        // 에디터에서 previewAppearance로 미리보기를 강제 구성하는 함수
        [ContextMenu("Preview In Editor")]
        public void PreviewInEditor()
        {
            if (partsManager == null)
                partsManager = GetComponentInChildren<PartsManager>();

            if (partsManager == null)
            {
                Log.Error("[AdventurerAppearanceApplier] PartsManager를 찾을 수 없습니다.");
                return;
            }

            // 1. SkeletonGraphic을 먼저 valid 상태로 만든다 — ApplyAppearance 내부 Init이 유효한 skeleton을 읽도록
            var graphic = partsManager.GetSkeletonGraphic();
            if (graphic != null && !graphic.IsValid)
                graphic.Initialize(true);

            // 2. 기본 외형(previewAppearance) 적용 — appearance를 null로 넘겨 fallback 경로를 탄다
            ApplyAppearance((AdventurerAppearanceData)null);

            // 3. 에디터 화면 강제 갱신
            if (graphic != null)
                graphic.Update(0);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(partsManager);
            UnityEditor.SceneView.RepaintAll();
#endif

            Log.Info("[AdventurerAppearanceApplier] 에디터 미리보기 갱신 완료");
        }
    }
}