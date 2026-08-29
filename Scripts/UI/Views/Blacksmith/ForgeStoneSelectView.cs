using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace TodaysWeaponRental
{
    public class ForgeStoneSelectView : BaseView
    {
        [Serializable]
        private struct ForgeStoneSlot
        {
            public Button button;
            public TMP_Text quantityText;
        }

        [SerializeField] private ForgeStoneSlot[] forgeStoneSlots;
        [SerializeField] private Button closeButton;
        [SerializeField] private ForgeStoneSelectController controller;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            pauseTimeOnOpen = false;
            isCanClickOverlay = true;
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

        #endregion

        #region 공개 메서드

        public void Initialize(ActiveItemData[] datas, int[] quantities)
        {
            for (int i = 0; i < forgeStoneSlots.Length; i++)
            {
                var slot = forgeStoneSlots[i];
                int qty = quantities[i];

                if (slot.quantityText != null)
                    slot.quantityText.text = $"x{qty}";

                if (slot.button != null)
                {
                    slot.button.interactable = qty > 0;
                    slot.button.onClick.RemoveAllListeners();
                    int idx = i;
                    slot.button.onClick.AddListener(() => controller?.OnItemSelected(datas[idx]));
                }
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnCloseClicked()
        {
            controller?.OnCloseClicked();
        }

        public override void OnEscapeClicked() => OnCloseClicked();

        #endregion
    }
}
