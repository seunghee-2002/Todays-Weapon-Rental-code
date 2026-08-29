using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;

namespace TodaysWeaponRental
{
    public class BlacksmithRecipeItemCard : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private Image itemBG;
        [SerializeField] private Image itemFrame;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private GameObject craftableDot;

        public void Initialize(Sprite icon, Action onClickAction, bool isCraftable, Grade itemGrade = Grade.Common)
        {
            if (itemIcon != null && icon != null)
                itemIcon.sprite = icon;

            if (itemBG != null)
                itemBG.color = ColorManager.Instance.GetGradeCardBackgroundColor(itemGrade);

            if (itemFrame != null)
                itemFrame.sprite = IconManager.Instance.GetFrameByGrade(itemGrade);

            if (button != null && onClickAction != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClickAction.Invoke());
            }

            SetSelected(false);
            SetCraftable(isCraftable);
        }

        public void SetSelected(bool selected)
        {
            selectedIndicator?.SetActive(selected);
        }

        public void SetCraftable(bool isCraftable)
        {
            craftableDot?.SetActive(isCraftable);
        }
    }
}
