using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    public class AdventureDialogueView : BaseView
    {
        [Header("Adventurer Info")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private GameObject effectPoint;
        
        [Header("Affection")]
        [SerializeField] private TextMeshProUGUI affectionValueText;
        [SerializeField] private TextMeshProUGUI visitCountText;
        
        [Header("Dialog")]
        [SerializeField] private TextMeshProUGUI dialogText;
                
        [Header("Buttons")]
        [SerializeField] private Button prepareAdventureButton;
        [SerializeField] private Button closeButton;
        
        [Header("Controller")]
        [SerializeField] private AdventureDialogueController dialogController;

        private AdventureDialogueController Controller => dialogController;

        private VisitorNPC cachedVisitor;
        private int savedSortingOrder;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();
            
            pauseTimeOnOpen = true;
            isCanClickOverlay = false;
            canEscape = false;
        }
        
        public void Initialize(AdventurerInstance adventurer)
        {
            UpdateAdventurerInfo(adventurer);

            cachedVisitor = adventurer?.currentVisitor;
            if (cachedVisitor != null)
            {
                savedSortingOrder = cachedVisitor.GetSortingOrder();
                cachedVisitor.SuspendDepthSorting();   // 자동 원근 정렬이 101을 덮어쓰지 않도록
                cachedVisitor.SetSortingOrder(101);
            }

            if (adventurer != null && adventurer.isNamed)
            {
                // 이펙트 재생
                EffectManager.Instance?.PlayNamedAdventurerSpawnEffect(effectPoint);
            }
        }

        public override void Close()
        {
            if (cachedVisitor != null)
            {
                cachedVisitor.SetSortingOrder(savedSortingOrder);
                cachedVisitor.ResumeDepthSorting();
                cachedVisitor = null;
            }
            EffectManager.Instance?.StopParticle(effectPoint);
            base.Close();
        }

        protected override void SubscribeEvents()
        {
            prepareAdventureButton?.onClick.RemoveListener(OnPrepareAdventureClicked);
            prepareAdventureButton?.onClick.AddListener(OnPrepareAdventureClicked);

            closeButton?.onClick.RemoveListener(RequestClose);
            closeButton?.onClick.AddListener(RequestClose);
        }

        protected override void UnsubscribeEvents()
        {
            prepareAdventureButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
        }

        #endregion

        #region UI 업데이트
        
        private void UpdateAdventurerInfo(AdventurerInstance adventurer)
        {
            if (adventurer == null)
            {
                Log.Error("[AdventureDialogView] adventurer가 null입니다!");
                return;
            }
            
            // 기본 정보
            if (nameText != null)
                nameText.text = adventurer.Name;
            // 등급 뱃지는 네임드에게만 붙인다
            if (gradeText != null)
            {
                gradeText.gameObject.SetActive(adventurer.isNamed);
                if (adventurer.isNamed)
                    gradeText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                        "UI_Common", "AdventurerGrade_Named");
            }

            // 호감도
            UpdateAffection(adventurer);
            
            // 방문 정보 — 클릭 시 이번 방문이 이미 visitCount에 반영되므로 1 이하를 첫 방문으로 본다 (대사 로직과 동일 기준)
            visitCountText.gameObject.SetActive(adventurer.isNamed);
            if (adventurer.isNamed)
                visitCountText.text = adventurer.adventurerStatData.visitCount <= 1
                    ? LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", "AdventureDialogue_FirstVisit")
                    : LocalizationSettings.StringDatabase.GetLocalizedString(
                          "UI_Screens", "AdventureDialogue_VisitCount",
                          arguments: new object[] { new Dictionary<string, object> { { "count", adventurer.adventurerStatData.visitCount } } });
            
            // 대사
            UpdateDialog(adventurer);
        }
        
        private void UpdateAffection(AdventurerInstance adventurer)
        {
            affectionValueText.gameObject.SetActive(adventurer.isNamed);
            if (adventurer.isNamed)
            {
                if (affectionValueText != null)
                    affectionValueText.text = $"{adventurer.affection}/100";
            }
        }

        private void UpdateDialog(AdventurerInstance adventurer)
        {
            if (dialogText == null) return;

            // 상황(호감도/첫방문/성별)에 따른 랜덤 대사 - VisitorManager가 관리
            dialogText.text = VisitorManager.Instance != null
                ? VisitorManager.Instance.GetAdventurerDialogue(adventurer)
                : string.Empty;
        }

        #endregion

        #region 이벤트 핸들러
        
        private void OnPrepareAdventureClicked()
        {
            Controller?.OnPrepareAdventure();
        }


        // 닫기 버튼·ESC 공통 진입점: 돌려보내기 확인 팝업 → 확인 시 실제 종료.
        private void RequestClose()
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.GuardBack()) return;

            string typeLabel = cachedVisitor != null
                ? UITranslator.GetString(cachedVisitor.visitorType)
                : LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", "AdventureDialogue_Title");
            UIPopupController.Instance?.ShowPopup(
                UIPopupController.SendBackConfirmMessage(typeLabel),
                onConfirm: () => Controller?.OnClose(),
                onCancel: () => { });
        }

        public override void OnEscapeCancelled() => RequestClose();

        #endregion
    }
}