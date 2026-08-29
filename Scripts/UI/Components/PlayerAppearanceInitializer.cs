using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// Player 오브젝트에 부착하여 FixedAppearanceData를 AdventurerAppearanceApplier로 적용
    /// </summary>
    public class PlayerAppearanceInitializer : MonoBehaviour
    {
        [SerializeField] private AdventurerAppearanceApplier appearanceApplier;
        [SerializeField] private FixedAppearanceData appearanceData;

        #region 초기화

        private void Start()
        {
            if (appearanceApplier == null)
            {
                appearanceApplier = GetComponentInChildren<AdventurerAppearanceApplier>();
                if (appearanceApplier == null)
                {
                    Log.Error("[PlayerAppearanceInitializer] AdventurerAppearanceApplier를 찾을 수 없습니다.");
                    return;
                }
            }

            if (appearanceData == null)
            {
                Log.Warn("[PlayerAppearanceInitializer] appearanceData가 할당되지 않았습니다.");
                return;
            }

            appearanceApplier.ApplyAppearance(appearanceData);
            Log.Info("[PlayerAppearanceInitializer] Player 외형 적용 완료.");
        }

        #endregion
    }
}