using System.Collections.Generic;
using LayerLab.ArtMaker;
using Spine;
using Spine.Unity;
using Spine.Unity.AttachmentTools;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험가 PartsManager에 WeaponData를 손 무기로 적용한다.
    /// gearRightIndex 스킨을 정렬 템플릿으로 띄운 뒤, inGame 스프라이트로 텍스처만 교체(GetRemappedClone).
    /// 런타임(AdventureProgressStage)과 에디터 프리뷰 도구가 공유한다.
    /// </summary>
    public static class WeaponEquipUtil
    {
        private const string GearRightSlot = "gear_right";

        // inGame 스프라이트 → remap용 AtlasRegion 캐시.
        // GetRemappedClone(Sprite, ...) 경로는 호출마다 Material과 Texture2D를 새로 만들고 아무도 해제하지 않는다.
        // (Spine의 텍스처 캐시는 AtlasRegion 오버로드에만 걸려 있어 ClearCache로도 회수되지 않는다)
        // 카드 바인딩마다 새로 만들면 그대로 누수가 되므로, 스프라이트당 1개만 만들어 재사용한다.
        private static readonly Dictionary<Sprite, AtlasRegion> RegionCache = new Dictionary<Sprite, AtlasRegion>();

        #region 무기 적용

        /// <summary>대여 무기를 손에 장착. gearRightIndex=정렬 템플릿, inGame=실제 그림.</summary>
        public static void Equip(PartsManager pm, WeaponData weapon)
        {
            if (pm == null || weapon == null) return;
            pm.SetHideItem(PartsType.Gear_Right, false);
            pm.EquipParts(PartsType.Gear_Right, weapon.gearRightIndex);
            ApplySprite(pm, weapon.inGame);
        }

        /// <summary>맨손(무기 미대여) 처리.</summary>
        public static void Unequip(PartsManager pm)
        {
            if (pm == null) return;
            pm.SetHideItem(PartsType.Gear_Right, true);
        }

        /// <summary>WeaponType → 모험가 공격 애니메이션 (▶ 기획 2-3).</summary>
        public static string AttackAnim(WeaponType type) => type switch
        {
            WeaponType.Sword    => "Attack1",
            WeaponType.Axe      => "Attack2",
            WeaponType.Dagger   => "Attack3",
            WeaponType.Shuriken => "Attack3",
            WeaponType.Bow      => "Attack_Bow",
            WeaponType.Crossbow => "Attack_Bolt",
            WeaponType.Staff    => "Attack_Magic",
            WeaponType.Tome     => "Attack_Magic",
            _                   => "Attack1",
        };

        /// <summary>원거리 무기의 조준(Idle) 애니메이션 — 발사 전 wind-up용. 근접이면 null.</summary>
        public static string RangedIdleAnim(WeaponType type) => type switch
        {
            WeaponType.Bow      => "Idle_Bow",
            WeaponType.Crossbow => "Idle_Bolt",
            _                   => null,
        };

        #endregion

        #region 내부 메서드

        /// <summary>gear_right 템플릿 attachment에 inGame 스프라이트만 입힌다(정렬은 템플릿 재활용).</summary>
        private static void ApplySprite(PartsManager pm, Sprite sprite)
        {
            if (sprite == null) return;   // inGame 미지정 시 템플릿 무기 그대로 표시

            var graphic = pm.GetSkeletonGraphic();
            // 패널을 막 여는 첫 프레임엔 SkeletonGraphic이 아직 초기화 전이라 Skeleton/슬롯이 비어
            // gear_right 슬롯이 아틀라스 기본(전체 텍스처) 상태로 남는다. 먼저 valid를 보장한다.
            if (graphic != null && !graphic.IsValid) graphic.Initialize(false);
            Skeleton skeleton = graphic != null ? graphic.Skeleton : pm.GetSkeletonAnimation()?.Skeleton;
            if (skeleton == null) return;

            var slot = skeleton.FindSlot(GearRightSlot);
            var template = slot?.Attachment;   // EquipParts가 세팅한 gear_right_f_N
            if (template == null) return;

            Material mat = SourceMaterial(pm, graphic);
            if (mat == null) return;

            var region = GetOrCreateRegion(sprite, mat);

            // 원래 호출의 pivotShiftsMeshUVCoords:false 재현 — 중심이 아닌 피벗이 uv를 메시 밖으로 밀지 않게 한다.
            if (template is MeshAttachment) { region.offsetX = 0; region.offsetY = 0; }

            var remapped = template.GetRemappedClone(region, cloneMeshAsLinked: true,
                useOriginalRegionSize: false, scale: 1f / sprite.pixelsPerUnit);
            if (remapped != null) slot.Attachment = remapped;
        }

        /// <summary>스프라이트당 AtlasRegion(=Material+Texture2D)을 1개만 만들어 캐시한다. 무기 종류 수만큼만 생성되고 그 뒤로 고정된다.</summary>
        private static AtlasRegion GetOrCreateRegion(Sprite sprite, Material sourceMaterial)
        {
            if (!RegionCache.TryGetValue(sprite, out var region))
            {
                region = sprite.ToAtlasRegionPMAClone(sourceMaterial);
                RegionCache[sprite] = region;
            }
            return region;
        }

        /// <summary>도메인 리로드를 끈 에디터에서 이전 플레이 세션의 파괴된 Material/Texture 참조가 남지 않도록 초기화한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegionCache() => RegionCache.Clear();

        /// <summary>GetRemappedClone에 넘길 셰이더/포맷 기준 머티리얼(아틀라스 PrimaryMaterial).</summary>
        private static Material SourceMaterial(PartsManager pm, SkeletonGraphic graphic)
        {
            if (graphic != null && graphic.SkeletonDataAsset != null
                && graphic.SkeletonDataAsset.atlasAssets.Length > 0)
                return graphic.SkeletonDataAsset.atlasAssets[0].PrimaryMaterial;

            var sa = pm.GetSkeletonAnimation();
            if (sa != null && sa.SkeletonDataAsset != null
                && sa.SkeletonDataAsset.atlasAssets.Length > 0)
                return sa.SkeletonDataAsset.atlasAssets[0].PrimaryMaterial;

            return null;
        }

        #endregion
    }
}
