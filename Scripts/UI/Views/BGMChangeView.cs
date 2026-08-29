using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// BGM 변경 팝업 View.
    /// SoundManager에 등록된 변형 그룹마다 한 행을 만들어 곡을 고르게 한다.
    /// 저장 버튼이 없으므로 선택 즉시 적용하고, 닫힐 때 영구 저장한다.
    /// </summary>
    public class BGMChangeView : BaseView
    {
        [Header("목록")]
        [SerializeField] private Transform rowParent;
        [SerializeField] private BGMChangeItem rowPrefab;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        private readonly List<BGMChangeItem> spawnedRows = new List<BGMChangeItem>();

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            isCanClickOverlay = true;
            canEscape = true;
        }

        public override void Open()
        {
            base.Open();
            BuildRows();
        }

        /// <summary>
        /// 닫힐 때 미리듣기를 끄고 선택을 영구 저장한다.
        /// 저장 버튼이 없으므로 닫기/ESC/오버레이 어느 경로로 닫혀도 저장되도록 Close에 둔다.
        /// </summary>
        public override void Close()
        {
            SoundManager.Instance?.StopBgmPreview();

            base.Close();

            SoundManager.Instance?.SyncSettingsToPlayerData();
            LegacyManager.Instance?.SaveLegacy();
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

        #region UI 업데이트 메서드

        private void BuildRows()
        {
            ClearRows();

            if (SoundManager.Instance == null || rowPrefab == null || rowParent == null) return;

            foreach (var group in SoundManager.Instance.BgmVariantGroups)
            {
                if (group == null || string.IsNullOrEmpty(group.baseKey)) continue;

                var variants = SoundManager.Instance.GetVariantKeys(group.baseKey);
                if (variants.Count == 0)
                {
                    // baseKey + 번호 형태로 등록된 클립이 하나도 없는 경우 (슬롯 이름 오타 등)
                    Log.Warn($"[BGMChangeView] 변형 클립이 없는 그룹입니다: {group.baseKey}");
                    continue;
                }

                var row = Instantiate(rowPrefab, rowParent);
                SetupRow(row, group, variants);
                spawnedRows.Add(row);
            }
        }

        private void SetupRow(BGMChangeItem row, BgmVariantGroup group, IReadOnlyList<string> variants)
        {
            // 0번은 항상 랜덤, 그 뒤로 변형 키가 번호순으로 온다.
            // 0번의 실제 저장값은 SoundManager.RandomSelection이고 여기 넣는 건 표시 문자다.
            string[] options = new string[variants.Count + 1];
            options[0] = L("BGMChange_Random");
            for (int i = 0; i < variants.Count; i++)
                options[i + 1] = variants[i];

            string selection = SoundManager.Instance.GetBgmSelection(group.baseKey);
            int selectedIndex = 0;
            if (selection != SoundManager.RandomSelection)
                selectedIndex = Mathf.Max(0, System.Array.IndexOf(options, selection));

            // displayName에는 번역 키가 들어간다 (씬 인스펙터에서 지정). 비어 있으면 baseKey를 그대로 쓴다.
            string title = string.IsNullOrEmpty(group.displayName) ? group.baseKey : L(group.displayName);
            row.Setup(title, options, selectedIndex,
                index => OnSelectionChanged(group.baseKey, options, index),
                () => OnPlayClicked(group.baseKey));
        }

        private void ClearRows()
        {
            foreach (var row in spawnedRows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }

            spawnedRows.Clear();
        }

        #endregion

        #region 이벤트 핸들러

        /// <summary>행에서 곡을 바꿨을 때. 즉시 적용하고 그 자리에서 미리듣기를 재생한다.</summary>
        private void OnSelectionChanged(string baseKey, string[] options, int index)
        {
            if (SoundManager.Instance == null) return;

            string selection = index == 0 ? SoundManager.RandomSelection : options[index];
            SoundManager.Instance.SetBgmSelection(baseKey, selection);

            // 논리 키를 넘겨 방금 저장한 선택대로 들려준다 (랜덤이면 그 그룹 중 한 곡)
            SoundManager.Instance.PreviewBgm(baseKey);
        }

        /// <summary>행의 재생 버튼. 선택은 그대로 두고 현재 선택된 곡만 다시 들려준다.</summary>
        private void OnPlayClicked(string baseKey)
        {
            SoundManager.Instance?.PreviewBgm(baseKey);
        }

        // ESC/오버레이 클릭은 UIManager.CloseTopPanel이 Close()까지 처리하므로
        // OnEscapeClicked를 오버라이드하지 않는다. 오버라이드해서 ClosePanel을 부르면
        // Close()가 두 번 실행돼 저장이 중복된다.
        private void OnCloseClicked()
        {
            UIManager.Instance?.ClosePanel<BGMChangeView>();
        }

        #endregion
    }
}
