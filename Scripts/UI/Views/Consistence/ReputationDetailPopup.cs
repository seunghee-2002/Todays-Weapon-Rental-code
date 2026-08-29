using DG.Tweening;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.Settings;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 평판 상세 팝업
    /// - 등급별 혜택(스폰 간격 / 이벤트 확률 / 의뢰판) 표시
    /// - TopBar의 Reputation 자식으로 비활성화 상태로 배치되며, Show/Hide로 토글된다.
    /// </summary>
    public class ReputationDetailPopup : MonoBehaviour
    {
        [Header("Info")]
        [SerializeField] private TextMeshProUGUI reputationLevelText;
        [SerializeField] private TextMeshProUGUI benefitsText;

        [Header("Slide Animation")]
        [SerializeField] private float slideDuration = 0.25f;
        [SerializeField] private float slideFromOffsetY = 200f;

        private RectTransform rect;
        private Vector2 shownAnchoredPos;
        private Tween currentTween;

        #region 초기화

        private void EnsureInitialized()
        {
            // 비활성 상태로 씬에 배치되면 Awake가 호출되지 않으므로 Show() 시점에 안전하게 캐싱
            if (rect != null) return;
            rect = (RectTransform)transform;
            shownAnchoredPos = rect.anchoredPosition;
        }

        private void OnEnable()
        {
            if (ReputationManager.Instance != null)
                ReputationManager.Instance.OnReputationChanged += OnReputationChanged;
        }

        private void OnDisable()
        {
            if (ReputationManager.Instance != null)
                ReputationManager.Instance.OnReputationChanged -= OnReputationChanged;
        }

        #endregion

        #region Show / Hide

        public void Show()
        {
            EnsureInitialized();

            gameObject.SetActive(true);
            Refresh();

            currentTween?.Kill();
            rect.anchoredPosition = shownAnchoredPos + new Vector2(0f, slideFromOffsetY);
            currentTween = rect.DOAnchorPosY(shownAnchoredPos.y, slideDuration)
                               .SetEase(Ease.OutCubic)
                               .SetUpdate(true)
                               .SetLink(gameObject);
        }

        public void Hide()
        {
            currentTween?.Kill();
            rect.anchoredPosition = shownAnchoredPos;
            gameObject.SetActive(false);
        }

        #endregion

        #region 표시 갱신

        public void Refresh()
        {
            if (ReputationManager.Instance == null) return;

            if (reputationLevelText != null)
                reputationLevelText.text = UITranslator.GetString(ReputationManager.Instance.CurrentLevel);

            if (benefitsText != null)
            {
                var (min, max) = ReputationManager.Instance.GetAdventurerSpawnInterval();
                float chance = ReputationManager.Instance.GetEventChance();

                string text = L("Reputation_SpawnInterval", ("min", $"{min:F0}"), ("max", $"{max:F0}")) + "\n"
                            + L("Reputation_EventNpcChance", ("chance", $"{chance * 100f:F0}"));

                if (QuestBoardManager.Instance != null)
                {
                    var (poolSize, selectCount) = QuestBoardManager.Instance.GetBoardConfig(ReputationManager.Instance.CurrentLevel);
                    text += "\n" + L("Reputation_DungeonPool", ("pool", poolSize), ("select", selectCount));
                }

                benefitsText.text = text;
            }
        }

        #endregion

        #region 이벤트 핸들러

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        private static string L(string key, params (string Name, object Value)[] args)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var a in args) dict[a.Name] = a.Value;
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "UI_Screens", key, arguments: new object[] { dict });
        }

        private void OnReputationChanged(int newReputation)
        {
            if (gameObject.activeSelf)
                Refresh();
        }

        #endregion
    }
}
