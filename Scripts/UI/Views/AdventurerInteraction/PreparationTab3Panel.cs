using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 준비 Tab 3 — 던전 목록 + 점술 + 모험 정보 칩 + Mood Label + 모험 시작.
    /// </summary>
    public class PreparationTab3Panel : MonoBehaviour
    {
        [Header("던전 목록")]
        [SerializeField] private ScrollRect dungeonScrollRect;
        [SerializeField] private Transform dungeonChoiceContainer;
        [SerializeField] private GameObject dungeonChoiceCardPrefab;

        [Header("모험 정보 칩")]
        [SerializeField] private Transform adventureInfoCardContainer;
        [SerializeField] private GameObject adventureInfoCardPrefab;

        [Header("점술")]
        [SerializeField] private Button seerButton;
        [SerializeField] private GameObject seerDoneIndicator;

        [Header("모험 시작")]
        [SerializeField] private Button startAdventureButton;
        [SerializeField] private Image startAdventureButtonImage;
        [SerializeField] private TextMeshProUGUI startAdventureButtonText;

        private AdventurePreparationController controller;

        private List<DungeonChoiceCardItem> dungeonCards = new List<DungeonChoiceCardItem>();
        private List<AdventureInfoCardItem> infoCards = new List<AdventureInfoCardItem>();
        private AdventurerInstance currentAdventurer;

        // 마지막으로 계산된 mood 색 (시작 가능 상태일 때 버튼에 적용)
        private bool hasMoodColor = false;
        private Color moodColor;
        private Color moodTextColor;
        private bool canStartCached = false;

        #region 초기화

        public void Initialize(AdventurePreparationController controller)
        {
            this.controller = controller;

            moodColor = ColorManager.Instance.GetWhiteColor();
            moodTextColor = ColorManager.Instance.GetBlackColor();

            seerButton?.onClick.RemoveAllListeners();
            startAdventureButton?.onClick.RemoveAllListeners();

            seerButton?.onClick.AddListener(() => this.controller?.OnSeerButtonClicked());
            startAdventureButton?.onClick.AddListener(() => this.controller?.OnStartAdventure());
        }

        #endregion

        #region 던전 목록

        public void UpdateDungeonList(List<DungeonData> choices, AdventurerInstance adventurer)
        {
            currentAdventurer = adventurer;
            ClearDungeonCards();

            foreach (var choice in choices)
            {
                var cardObj = Instantiate(dungeonChoiceCardPrefab, dungeonChoiceContainer);
                var card = cardObj.GetComponentOrNull<DungeonChoiceCardItem>();
                if (card != null)
                {
                    bool isDoubleReward = QuestBoardManager.Instance.IsHighlightedDungeon(choice.StaticID);
                    LuckLevel? luck = null;
                    if (adventurer != null)
                    {
                        var seerResult = SeerManager.Instance.GetExistingResult(adventurer, choice);
                        if (seerResult != null) luck = seerResult.luckLevel;
                    }
                    card.Initialize(choice, () => controller?.OnDungeonCardClicked(choice), isDoubleReward, luck);
                    dungeonCards.Add(card);
                }
            }

            dungeonScrollRect.ResetPosition(this);
        }

        public void RefreshSeerGlow(DungeonData dungeon)
        {
            if (dungeon == null || currentAdventurer == null) return;
            var result = SeerManager.Instance.GetExistingResult(currentAdventurer, dungeon);
            LuckLevel? luck = result?.luckLevel;
            foreach (var card in dungeonCards)
            {
                if (card.GetChoice() == dungeon)
                {
                    card.SetSeerLuckLevel(luck);
                    return;
                }
            }
        }

        public void UpdateSeerIndicator(bool done)
        {
            seerDoneIndicator?.SetActive(done);
        }

        public void HighlightSelectedDungeon(DungeonData selected)
        {
            foreach (var card in dungeonCards)
                card.SetSelected(card.GetChoice() == selected);
        }

        public void UpdateDungeonCardSimulation(DungeonData dungeon, ArmorType armorType)
        {
            foreach (var card in dungeonCards)
            {
                if (card.GetChoice() == dungeon)
                {
                    card.SetSimulatedArmorType(armorType);
                    return;
                }
            }
        }

        public void ClearDungeonCardSimulation(string dungeonStaticID)
        {
            foreach (var card in dungeonCards)
            {
                if (card.GetChoice()?.StaticID == dungeonStaticID)
                {
                    card.ClearSimulationDisplay();
                    return;
                }
            }
        }

        // 수색 완료 시 해당 카드의 방어타입 아이콘을 공개된 값으로 재계산 (? -> 실제 타입)
        public void RefreshDungeonCardArmorType(string dungeonStaticID)
        {
            foreach (var card in dungeonCards)
            {
                if (card.GetChoice()?.StaticID == dungeonStaticID)
                {
                    card.ClearSimulationDisplay();
                    return;
                }
            }
        }

        private void ClearDungeonCards()
        {
            foreach (var card in dungeonCards)
                if (card != null) Destroy(card.gameObject);
            dungeonCards.Clear();
        }

        #endregion

        #region 튜토리얼 하이라이트 접근자

        /// <summary>튜토리얼 하이라이트 중 목록이 스크롤돼 대상 카드가 어긋나지 않도록 잠근다.</summary>
        public void SetDungeonScrollLocked(bool locked)
        {
            if (dungeonScrollRect != null)
                dungeonScrollRect.enabled = !locked;
        }

        private DungeonChoiceCardItem FindCard(string dungeonStaticID)
        {
            if (dungeonChoiceContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            return dungeonCards.FirstOrDefault(c => c != null && c.GetChoice()?.StaticID == dungeonStaticID);
        }

        /// <summary>튜토리얼 하이라이트용 — 지정 던전 카드의 RectTransform.</summary>
        public RectTransform GetDungeonCardRect(string dungeonStaticID)
            => FindCard(dungeonStaticID)?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트용 — 지정 던전 카드의 방어타입 아이콘 RectTransform.</summary>
        public RectTransform GetDungeonArmorIconRect(string dungeonStaticID)
            => FindCard(dungeonStaticID)?.GetArmorIconRect();

        /// <summary>튜토리얼 하이라이트용 — 지정 던전 카드의 '2배' 표시 RectTransform.</summary>
        public RectTransform GetDungeonDoubleRewardIconRect(string dungeonStaticID)
            => FindCard(dungeonStaticID)?.GetDoubleRewardIconRect();

        /// <summary>튜토리얼 하이라이트용(6-D) — 모험 정보 칩 컨테이너 RectTransform.</summary>
        public RectTransform GetAdventureInfoContainerRect()
        {
            if (adventureInfoCardContainer is RectTransform containerRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
                return containerRect;
            }
            return null;
        }

        /// <summary>튜토리얼 하이라이트용(6-D) — 점술 버튼 RectTransform.</summary>
        public RectTransform GetSeerButtonRect() => seerButton?.transform as RectTransform;

        /// <summary>튜토리얼 하이라이트용(6-E) — 모험 시작 버튼 RectTransform.</summary>
        public RectTransform GetStartAdventureButtonRect() => startAdventureButton?.transform as RectTransform;

        /// <summary>튜토리얼 6-D — 현재 모험 정보 칩들에 클릭 콜백을 배선한다(칩 클릭=정보 확인 감지). null이면 해제.</summary>
        public void SetInfoChipsTutorialCallback(Action callback)
        {
            foreach (var chip in infoCards)
                if (chip != null) chip.SetTutorialClickCallback(callback);
        }

        #endregion

        #region 모험 정보 칩 / 통합 바

        public void ResetAdventureInfo()
        {
            ClearInfoCards();
            RefreshStartButtonMood(null);
        }

        public void UpdateAdventureInfo(List<AdventureInfoCardData> cards)
        {
            ClearInfoCards();
            if (cards == null) { RefreshStartButtonMood(null); return; }

            var sortedCards = cards
                .OrderByDescending(c => c.isConfirmed)
                .ThenByDescending(c => c.value)
                .ToList();

            foreach (var data in sortedCards)
            {
                if (adventureInfoCardPrefab == null || adventureInfoCardContainer == null) break;
                var obj = Instantiate(adventureInfoCardPrefab, adventureInfoCardContainer);
                var chip = obj.GetComponentOrNull<AdventureInfoCardItem>();
                if (chip != null)
                {
                    chip.SetInfo(data.type, data.value, data.isConfirmed, data.isMultiplier);
                    infoCards.Add(chip);
                }
            }

            RefreshStartButtonMood(cards);
        }

        private void ClearInfoCards()
        {
            foreach (var chip in infoCards)
                if (chip != null) Destroy(chip.gameObject);
            infoCards.Clear();
        }

        private void RefreshStartButtonMood(List<AdventureInfoCardData> cards)
        {
            var cfg = ConfigManager.Instance?.AdventureInfo;
            if (cfg == null) { hasMoodColor = false; ApplyStartButtonColor(); return; }

            if (cards == null || cards.Count == 0)
            {
                hasMoodColor = false;
            }
            else
            {
                var result = MoodLabelCalculator.Calculate(cards, cfg);
                hasMoodColor = true;
                moodColor = result.color;
                moodTextColor = result.textColor;
            }
            ApplyStartButtonColor();
        }

        #endregion

        #region 시작 버튼

        public void UpdateStartButton(bool canStart)
        {
            canStartCached = canStart;
            if (startAdventureButton != null)
                startAdventureButton.interactable = canStart;
            ApplyStartButtonColor();
        }

        private void ApplyStartButtonColor()
        {
            var cfg = ConfigManager.Instance?.AdventureInfo;

            Color bg;
            Color textColor;
            if (canStartCached && hasMoodColor)
            {
                bg = moodColor;
                textColor = moodTextColor;
            }
            else
            {
                bg = cfg != null ? cfg.startButtonDisabledColor : ColorManager.Instance.GetGrayColor();
                textColor = cfg != null ? cfg.startButtonDisabledTextColor : ColorManager.Instance.GetWhiteColor();
            }

            if (startAdventureButtonImage != null)
                startAdventureButtonImage.color = bg;
            if (startAdventureButtonText != null)
                startAdventureButtonText.color = textColor;
        }

        #endregion
    }
}
