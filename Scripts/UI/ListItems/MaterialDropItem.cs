using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    public class MaterialDropItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image materialIcon;
        [SerializeField] private Image materialBG;
        [SerializeField] private Image materialFrame;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private Button detailButton;

        private MaterialData data;

        private void OnDestroy()
        {
            detailButton?.onClick.RemoveAllListeners();
        }

        public void Initialize(MaterialData data, int quantity)
        {
            if (data == null)
            {
                Log.Error("[MaterialDropItem] data가 null입니다!");
                return;
            }

            this.data = data;

            if (materialIcon != null) materialIcon.sprite = data.icon;
            if (materialBG != null) materialBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(data.grade);
            if (materialFrame != null) materialFrame.sprite = IconManager.Instance.GetFrameByGrade(data.grade);
            if (quantityText != null) quantityText.text = $"x{quantity}";

            detailButton?.onClick.RemoveAllListeners();
            detailButton?.onClick.AddListener(OnDetailClicked);
        }

        private void OnDetailClicked()
        {
            if (data == null) return;

            var popup = UIManager.Instance.GetOrInstantiatePanel<MaterialDetailPopup>();
            UIManager.Instance.OpenPanel<MaterialDetailPopup>();
            popup?.Initialize(data);
        }
    }
}
