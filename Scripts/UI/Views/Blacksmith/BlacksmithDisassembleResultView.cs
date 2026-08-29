using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 일괄분해 완료 결과 팝업
    /// 총 획득 골드 + 획득 재료를 세로 리스트로 표시한다.
    /// </summary>
    public class BlacksmithDisassembleResultView : BaseView
    {

        [Header("획득 골드")]
        [SerializeField] private TextMeshProUGUI goldAmountText;

        [Header("획득 재료 리스트")]
        [SerializeField] private Transform materialListContainer;
        [SerializeField] private GameObject materialItemPrefab;

        [Header("버튼")]
        [SerializeField] private Button closeButton;

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = false;
            isCanClickOverlay = false;
            canEscape = true;
        }

        protected override void SubscribeEvents()
        {
            closeButton?.onClick.AddListener(OnCloseClicked);
        }

        protected override void UnsubscribeEvents()
        {
            closeButton?.onClick.RemoveAllListeners();
        }

        #region 초기화

        public void Initialize(int totalGold, int totalBonusGold, List<(MaterialData mat, int count)> materials)
        {
            if (goldAmountText != null)
            {
                goldAmountText.text = totalBonusGold > 0
                    ? $"+{totalGold:N0} (+{totalBonusGold:N0})"
                    : $"+{totalGold:N0}";
            }

            if (materialListContainer != null)
            {
                foreach (Transform child in materialListContainer)
                    Destroy(child.gameObject);

                if (materials != null && materialItemPrefab != null)
                {
                    materials = materials.OrderByDescending(m => m.mat.grade).ToList();
                    foreach (var (mat, count) in materials)
                    {
                        if (mat == null) continue;
                        var obj = Instantiate(materialItemPrefab, materialListContainer);
                        var item = obj.GetComponent<MaterialDropItem>();
                        item?.Initialize(mat, count);
                    }
                }
            }
        }

        #endregion

        #region 버튼 핸들러

        private void OnCloseClicked()
        {
            UIControllerManager.Instance.GetController<BlacksmithController>()?.OnDisassembleResultClosed();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
