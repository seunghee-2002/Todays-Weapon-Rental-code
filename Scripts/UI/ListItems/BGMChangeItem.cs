using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// BGM 변경 화면의 한 행. 그룹 이름 + 현재 곡 + 좌우 이동 버튼 + 재생 버튼.
    /// 목록 양 끝에서는 해당 방향 버튼을 숨긴다.
    /// </summary>
    public class BGMChangeItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bgmNameText;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button playButton;

        private string[] options;
        private int currentIndex;
        private Action<int> onIndexChanged;
        private Action onPlayClicked;

        #region 초기화

        public void Setup(string title, string[] options, int selectedIndex, Action<int> onIndexChanged, Action onPlayClicked)
        {
            this.options = options;
            this.onIndexChanged = onIndexChanged;
            this.onPlayClicked = onPlayClicked;
            currentIndex = (options == null || options.Length == 0)
                ? 0
                : Mathf.Clamp(selectedIndex, 0, options.Length - 1);

            if (titleText != null)
                titleText.text = title;

            leftButton?.onClick.RemoveAllListeners();
            rightButton?.onClick.RemoveAllListeners();
            playButton?.onClick.RemoveAllListeners();
            leftButton?.onClick.AddListener(() => Move(-1));
            rightButton?.onClick.AddListener(() => Move(1));
            playButton?.onClick.AddListener(() => this.onPlayClicked?.Invoke());

            UpdateDisplay();
        }

        #endregion

        #region 내부 메서드

        private void Move(int delta)
        {
            if (options == null) return;

            int next = currentIndex + delta;
            if (next < 0 || next >= options.Length) return;

            currentIndex = next;
            UpdateDisplay();
            onIndexChanged?.Invoke(currentIndex);
        }

        private void UpdateDisplay()
        {
            bool hasOptions = options != null && options.Length > 0;

            if (bgmNameText != null)
                bgmNameText.text = hasOptions ? options[currentIndex] : "-";

            leftButton?.gameObject.SetActive(hasOptions && currentIndex > 0);
            rightButton?.gameObject.SetActive(hasOptions && currentIndex < options.Length - 1);
        }

        #endregion
    }
}
