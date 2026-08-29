using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

namespace TodaysWeaponRental
{
    /// <summary>
    /// 모험 결과 패널 View.
    /// 별점 + 수수료 연출 + 결과 아이템 컨테이너(평판/호감도/재료) UI.
    /// </summary>
    public class AdventureResultView : BaseView
    {
        [Header("Title")]
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private Image resultStarBG;
        [SerializeField] private TextMeshProUGUI resultDetailText;



        [Header("Star Rating")]
        [SerializeField] private GameObject[] starObjects = new GameObject[3];
        [SerializeField] private ParticleSystem[] starParticles = new ParticleSystem[3];
        [SerializeField] private ParticleSystem[] starLoopParticles = new ParticleSystem[3];
        [SerializeField] private ParticleSystem starBGParticle;

        [Header("Commission Area")]
        [SerializeField] private GameObject commissionArea;
        [SerializeField] private GameObject detailCommissionArea;         // 기본/대여/팁/특성 수수료 묶음 (남는 공간 차지)
        [SerializeField] private TextMeshProUGUI totalGoldText;           // "500G"
        [SerializeField] private CanvasGroup totalGoldRowCanvasGroup;
        [SerializeField] private GameObject penaltyGoldRow;               // TotalGoldRow 내부 자식 (패배 시 활성)
        [SerializeField] private TextMeshProUGUI penaltyGoldText;         // "-50%"
        [SerializeField] private GameObject baseCommissionRow;
        [SerializeField] private TextMeshProUGUI baseCommissionText;      // "기본 수수료 (20%): 100G"
        [SerializeField] private GameObject rentalCommissionRow;
        [SerializeField] private TextMeshProUGUI rentalCommissionText;    // "무기 대여 (15%): 75G"
        [SerializeField] private GameObject tipCommissionRow;
        [SerializeField] private TextMeshProUGUI tipCommissionText;       // "상성 팁 (10%): 50G"
        [SerializeField] private GameObject traitCommissionRow;
        [SerializeField] private TextMeshProUGUI traitCommissionText;
        [SerializeField] private GameObject commissionDivider;
        [SerializeField] private CanvasGroup commissionDividerCanvasGroup;
        [SerializeField] private GameObject playerGoldRow;
        [SerializeField] private TextMeshProUGUI playerGoldText;          // "플레이어 수령: 225G"
        [SerializeField] private CanvasGroup playerGoldRowCanvasGroup;
        [SerializeField] private GameObject noGoldEarnedRow;              // originalGold == 0 실패 시 표시
        [SerializeField] private TextMeshProUGUI noGoldEarnedText;        // "획득한 골드가 없습니다"

        [Header("Reward Area")]
        [SerializeField] private GameObject rewardArea;                   // 성공 시 활성, 실패 시 비활성
        [SerializeField] private Transform resultItemContainer;           // rewardArea의 자식
        [SerializeField] private GameObject adventureResultItemPrefab;
        [SerializeField] private GameObject tooltipResultItemPrefab;
        [SerializeField] private Button buyAllMaterialsButton;

        [Header("Confirm Button")]
        [SerializeField] private Button closeButton;

        [Header("스킵 오버레이")]
        [SerializeField] private GameObject skipOverlayObject;
        [SerializeField] private Button skipOverlayButton;

        [Header("Animation Settings")]
        [SerializeField] private float rowDelay = 0.2f;
        [SerializeField] private float textWidthLockPadding = 16f;

        [SerializeField] private AdventureResultController controller;
        private AdventureResult currentResult;
        private AdventureInstance currentAdventure;
        private bool skipRequested;
        private bool resultSfxPlayed;
        private List<AdventureResultItem> resultItems = new();
        private Coroutine sequenceCoroutine;

        public IReadOnlyList<AdventureResultItem> ResultItems => resultItems;

        #region 초기화

        protected override void Awake()
        {
            base.Awake();

            pauseTimeOnOpen = true;
            canEscape = false;
        }

        public void Initialize()
        {
            skipRequested = false;

            commissionArea?.SetActive(false);
            baseCommissionRow?.SetActive(false);
            rentalCommissionRow?.SetActive(false);
            tipCommissionRow?.SetActive(false);
            commissionDivider?.SetActive(false);
            playerGoldRow?.SetActive(false);
            traitCommissionRow?.SetActive(false);
            penaltyGoldRow?.SetActive(false);
            noGoldEarnedRow?.SetActive(false);

            rewardArea?.SetActive(false);
            buyAllMaterialsButton?.gameObject.SetActive(false);
            resultDetailText?.gameObject.SetActive(false);

            resultStarBG?.gameObject.SetActive(false);
            SetStarsActive(0);
            DisableAllStarParticles();

            // 이전 결과의 ResultItem이 잔존 표시되지 않도록 즉시 정리
            ClearResultItems();

            if (closeButton)
            {
                closeButton.gameObject.SetActive(false);
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (buyAllMaterialsButton != null)
            {
                buyAllMaterialsButton.onClick.RemoveAllListeners();
                buyAllMaterialsButton.onClick.AddListener(OnBuyAllMaterialsClicked);
            }
        }

        public void InitAfter(AdventureInstance adventure)
        {
            currentAdventure = adventure;
            currentResult = AdventureManager.Instance.CompletedResults
                .FirstOrDefault(r => r.adventureID == adventure.instanceID);
            if (currentResult == null) return;

            ShowTitleAndStars(currentResult);
            ShowDeathDetail(adventure, currentResult);

            skipRequested = false;
            resultSfxPlayed = false;

            if (skipOverlayObject != null)
                skipOverlayObject.SetActive(true);
            if (skipOverlayButton != null)
            {
                skipOverlayButton.onClick.RemoveAllListeners();
                skipOverlayButton.onClick.AddListener(PerformInstantSkip);
            }

            if (currentResult.isDeath)
                sequenceCoroutine = StartCoroutine(PlayDeathSequence(adventure, currentResult));
            else if (currentResult.isRetreated || !currentResult.isSuccess)
                sequenceCoroutine = StartCoroutine(PlayFailureSequence(adventure, currentResult));
            else
                sequenceCoroutine = StartCoroutine(PlayResultSequence(adventure, currentResult));
        }

        protected override void SubscribeEvents() { }
        protected override void UnsubscribeEvents() { }

        #endregion

        #region 타이틀 / 별점

        private static string L(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString("UI_Screens", key);

        /// <summary>골드 표기(전체 획득 / 플레이어 수령). 카운트업 중 매 프레임 호출된다.</summary>
        private static string Gold(string key, int amount)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "gold", amount.ToString("N0") } } });

        /// <summary>수수료 항목 라벨. percent는 부호까지 포함한 완성 문자열.</summary>
        private static string CommissionLabel(string key, string percent)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", key,
                   arguments: new object[] { new Dictionary<string, object> { { "percent", percent } } });

        private void ShowTitleAndStars(AdventureResult result)
        {
            if (!resultTitleText) return;

            string title;

            if (result.isDeath)  title = L("AdventureResult_TitleDeath");
            else if (result.isRetreated) title = L("AdventureResult_TitleRetreat");
            else if (!result.isSuccess) title = L("AdventureResult_TitleFail");
            else if (result.isGreatSuccess) title = L("AdventureResult_TitleGreatSuccess");
            else title = L("AdventureResult_TitleSuccess");

            resultTitleText.text = title;
            // 풀 스케일로 띄운 뒤 펀치하면 "이미 있던 텍스트가 다시 강조되는" 이중 강조로 보임.
            // 스케일 0에서 커지며 등장하는 단일 연출로 통일 (다른 결과 요소들과 동일 톤).
            resultTitleText.transform.DOKill();
            resultTitleText.transform.localScale = Vector3.zero;
            resultTitleText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        private void SetStarsActive(int starCount)
        {
            if (starObjects == null || starParticles == null) return;
            for (int i = 0; i < starObjects.Length; i++)
            {
                if (starObjects[i] == null) continue;
                bool active = i < starCount;
                starObjects[i].SetActive(active);
                if (active && starParticles[i] != null)
                    starParticles[i].Play();
            }
        }

        private void DisableAllStarParticles()
        {
            if (starParticles != null)
                foreach (var p in starParticles)
                    p?.gameObject.SetActive(false);

            if (starLoopParticles != null)
                foreach (var p in starLoopParticles)
                    p?.gameObject.SetActive(false);

            starBGParticle?.gameObject.SetActive(false);
        }

        private IEnumerator PlayStarSequence()
        {
            int stars = CalculateResultStars(currentAdventure, currentResult);

            if (!skipRequested)
                yield return new WaitForSeconds(0.5f);

            for (int i = 0; i < stars; i++)
            {
                if (starObjects != null && i < starObjects.Length && starObjects[i] != null)
                    starObjects[i].SetActive(true);

                if (skipRequested)
                {
                    if (starParticles != null && i < starParticles.Length && starParticles[i] != null)
                        starParticles[i].gameObject.SetActive(false);
                    if (starLoopParticles != null && i < starLoopParticles.Length && starLoopParticles[i] != null)
                        starLoopParticles[i].gameObject.SetActive(true);
                }
                else
                {
                    if (starParticles != null && i < starParticles.Length && starParticles[i] != null)
                        starParticles[i].gameObject.SetActive(true);
                }

                if (i == 0)
                    starBGParticle?.gameObject.SetActive(true);

                if (i == stars - 1)
                    ActivateResultStarBG();

                if (!skipRequested)
                    yield return new WaitForSeconds(0.5f);
            }

            if (stars == 0)
                ActivateResultStarBG();
        }

        private void ActivateResultStarBG()
        {
            if (resultStarBG == null) return;
            resultStarBG.color = ColorManager.Instance.GetResultStarBGColor(currentResult.isSuccess);
            resultStarBG.gameObject.SetActive(true);
        }

        /// <summary>
        /// 별점 계산 (0~3).
        /// 실패=0, 대성공=3, 그 외 성공은 상성 평가에 따라 1/2/3.
        /// </summary>
        private int CalculateResultStars(AdventureInstance adventure, AdventureResult result)
        {
            if (result.isDeath || result.isRetreated || !result.isSuccess)
                return 0;
            if (result.isGreatSuccess)
                return 3;
            if (adventure?.weapon?.weaponData == null || adventure.adventurer == null)
                return 1;

            AffinityGrade armorGrade = EvaluateWeaponArmorAffinity(
                adventure.weapon.weaponData.weaponType, adventure.effectiveArmorType);
            AffinityGrade statGrade = EvaluateWeaponAdventurerAffinity(
                adventure.weapon.weaponData.weaponType, adventure.adventurer);

            // 점수 합산: Best=2 / Normal=1 / Poor=0
            int sum = (int)armorGrade + (int)statGrade;
            if (sum >= 4) return 3;
            if (sum >= 2) return 2;
            return 1;
        }

        private enum AffinityGrade { Poor = 0, Normal = 1, Best = 2 }

        private AffinityGrade EvaluateWeaponArmorAffinity(WeaponType weaponType, ArmorType armorType)
        {
            float bonus = TypeAdvantage.weaponArmorBonus[(int)weaponType, (int)armorType];
            if (bonus >= 0.20f) return AffinityGrade.Best;
            if (bonus >= 0f)    return AffinityGrade.Normal;
            return AffinityGrade.Poor;
        }

        private AffinityGrade EvaluateWeaponAdventurerAffinity(WeaponType weaponType, AdventurerInstance adventurer)
        {
            int weaponPreferredStat = 0;
            float bestMul = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float mul = TypeAdvantage.weaponStatMultipliers[(int)weaponType, i];
                if (mul > bestMul) { bestMul = mul; weaponPreferredStat = i; }
            }

            int[] stats = { adventurer.STR, adventurer.DEX, adventurer.INT, adventurer.LUK };
            int targetValue = stats[weaponPreferredStat];

            int rank = 1;
            for (int i = 0; i < stats.Length; i++)
            {
                if (i == weaponPreferredStat) continue;
                if (stats[i] > targetValue) rank++;
            }

            if (rank == 1) return AffinityGrade.Best;
            if (rank == 2) return AffinityGrade.Normal;
            return AffinityGrade.Poor;
        }

        #endregion

        #region 결과 상세 텍스트

        private void ShowDeathDetail(AdventureInstance adventure, AdventureResult result)
        {
            rewardArea?.SetActive(!result.isDeath);
            resultDetailText?.gameObject.SetActive(result.isDeath);

            if (resultDetailText == null) return;

            // 활성화만 하고 등장 전까지 숨겨둔다. 풀 스케일로 두면 별점 연출 동안 먼저 보였다가
            // PlayDeathSequence가 다시 스케일 0->1로 팝업시켜 이중 강조로 보인다.
            if (result.isDeath)
                resultDetailText.transform.localScale = Vector3.zero;

            string body;
            if (result.isDeath)
            {
                body = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "UI_Screens", "AdventureResult_DeathBody",
                    arguments: new object[] { new Dictionary<string, object> { { "name", adventure.adventurer.Name } } });
                if (result.weaponLost)
                    body += "\n" + LocalizationSettings.StringDatabase.GetLocalizedString(
                        "UI_Screens", "AdventureResult_WeaponLost",
                        arguments: new object[] { new Dictionary<string, object> { { "weapon", adventure.weapon.weaponData.DisplayName } } });
                if (result.deathProtectionUsed)
                    body += "\n" + L("AdventureResult_DeathProtected");
            }
            else
            {
                body = L("AdventureResult_FailBody");
            }
            resultDetailText.text = body;
        }

        #endregion

        #region 메인 연출 코루틴

        private IEnumerator PlayResultSequence(AdventureInstance adventure, AdventureResult result)
        {
            PlayResultSfxOnce(result.isGreatSuccess ? "AdventureGreatSuccess" : "AdventureSuccess");

            yield return StartCoroutine(PlayStarSequence());

            // 수수료 요약 순차 등장
            yield return StartCoroutine(PlayCommissionSequence(result));

            // 결과 아이템 컨테이너 (평판 + 호감도 + 재료)
            yield return StartCoroutine(BuildResultItemContainer(adventure, result));

            ActivateCloseButton();
            if (skipOverlayObject != null)
                skipOverlayObject.SetActive(false);
            skipOverlayButton?.onClick.RemoveAllListeners();
        }

        private IEnumerator PlayDeathSequence(AdventureInstance adventure, AdventureResult result)
        {
            yield return StartCoroutine(PlayStarSequence());

            if (resultDetailText != null)
            {
                if (!skipRequested)
                {
                    resultDetailText.transform.localScale = Vector3.zero;
                    resultDetailText.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
                    yield return new WaitForSeconds(0.3f);
                    resultDetailText.transform.DOShakePosition(0.4f, 8f, 15).SetLink(gameObject);
                }
                else
                {
                    resultDetailText.transform.localScale = Vector3.one;
                }

                PlayResultSfxOnce("AdventureFail");
            }

            ActivateCloseButton();
            skipOverlayObject?.SetActive(false);
            skipOverlayButton?.onClick.RemoveAllListeners();
        }

        private IEnumerator PlayFailureSequence(AdventureInstance adventure, AdventureResult result)
        {
            // 별 0개 + 빨강 배경 (PlayStarSequence가 stars==0 분기에서 ActivateResultStarBG 호출)
            yield return StartCoroutine(PlayStarSequence());

            PlayResultSfxOnce("AdventureFail");

            if (!skipRequested) yield return new WaitForSeconds(0.5f);

            int originalGold = adventure.accumulatedGold + adventure.accumulatedBonusGold;

            // 페널티 강조 연출 (원래 골드 → 페널티 적용 후 골드)
            yield return StartCoroutine(PlayPenaltyHighlightSequence(adventure, result));

            // originalGold == 0 이면 수수료 분해 없이 "획득한 골드가 없습니다" 안내 표시
            if (originalGold == 0)
            {
                yield return StartCoroutine(PlayNoGoldEarnedSequence());
            }
            else
            {
                // 나머지 수수료 행 (기본/대여/팁/특성/구분선/플레이어 수령)
                yield return StartCoroutine(PlayCommissionRowsSequence(result));
            }

            // 결과 아이템 컨테이너 (평판 + 호감도 + 재료)
            yield return StartCoroutine(BuildResultItemContainer(adventure, result));

            ActivateCloseButton();
            if (skipOverlayObject != null)
                skipOverlayObject.SetActive(false);
            skipOverlayButton?.onClick.RemoveAllListeners();
        }

        private IEnumerator PlayPenaltyHighlightSequence(AdventureInstance adventure, AdventureResult result)
        {
            int originalGold = adventure.accumulatedGold + adventure.accumulatedBonusGold;
            int finalGold = result.totalGoldReward;
            float penaltyPct = originalGold > 0
                ? (1f - (finalGold / (float)originalGold)) * 100f
                : 0f;

            // originalGold == 0 이면 페널티 표기 자체가 어색하므로 PenaltyGoldRow 없이 빨간색으로 표시
            // 수수료 분해 row들도 모두 숨기고, 별도 안내 메시지를 PlayNoGoldEarnedSequence에서 표시
            if (originalGold == 0)
            {
                penaltyGoldRow?.SetActive(false);
                baseCommissionRow?.SetActive(false);
                rentalCommissionRow?.SetActive(false);
                tipCommissionRow?.SetActive(false);
                traitCommissionRow?.SetActive(false);
                commissionDivider?.SetActive(false);
                playerGoldRow?.SetActive(false);
                if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.alpha = 0f;
                if (commissionDividerCanvasGroup != null) commissionDividerCanvasGroup.alpha = 0f;
                if (playerGoldRowCanvasGroup != null) playerGoldRowCanvasGroup.alpha = 0f;

                // 활성화 전에 폭 확정
                LockTextWidth(totalGoldText, Gold("AdventureResult_TotalGold", finalGold));

                commissionArea?.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(commissionArea?.transform as RectTransform);

                if (skipRequested)
                {
                    if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.alpha = 1f;
                    SetGoldText(totalGoldText, finalGold, "AdventureResult_TotalGold", ColorManager.Instance.GetRedColor());
                    yield break;
                }

                if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
                yield return StartCoroutine(AnimateGoldText(totalGoldText, finalGold, "AdventureResult_TotalGold", ColorManager.Instance.GetRedColor()));
                yield break;
            }

            PrepareCommissionRows(result);
            // 카운트다운 최댓값(originalGold) 기준으로 폭 고정
            LockTextWidth(totalGoldText, Gold("AdventureResult_TotalGold", originalGold));
            LockTextWidth(playerGoldText, Gold("AdventureResult_PlayerGold", result.playerGoldReward));

            commissionArea?.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(commissionArea?.transform as RectTransform);

            noGoldEarnedRow?.SetActive(false);

            if (skipRequested)
            {
                if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.alpha = 1f;
                SetGoldText(totalGoldText, finalGold, "AdventureResult_TotalGold", ColorManager.Instance.GetRedColor());

                if (penaltyGoldRow != null)
                {
                    penaltyGoldRow.SetActive(true);
                    penaltyGoldRow.transform.localScale = Vector3.one;
                    if (penaltyGoldText != null)
                    {
                        penaltyGoldText.text = $"-{penaltyPct:0}%";
                        penaltyGoldText.color = ColorManager.Instance.GetRedColor();
                    }
                }
                yield break;
            }

            // 1. 원래 누적 골드 CountUp
            if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
            yield return StartCoroutine(AnimateGoldText(totalGoldText, originalGold, "AdventureResult_TotalGold", ColorManager.Instance.GetWhiteColor()));

            yield return new WaitForSeconds(rowDelay);

            // 2. PenaltyGoldRow 등장
            if (penaltyGoldRow != null)
            {
                penaltyGoldRow.SetActive(true);
                if (penaltyGoldText != null)
                {
                    penaltyGoldText.text = $"-{penaltyPct:0}%";
                    penaltyGoldText.color = ColorManager.Instance.GetRedColor();
                }
                penaltyGoldRow.transform.localScale = Vector3.zero;
                penaltyGoldRow.transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(gameObject);
                yield return new WaitForSeconds(0.18f);
            }

            // 3. PenaltyGoldRow 등장 후 totalGoldText 색상 변경 + CountDown
            totalGoldText.color = ColorManager.Instance.GetRedColor();
            if (!skipRequested) SoundManager.Instance?.PlaySFX("GetGold");

            float elapsed = 0f;
            float duration = 0.4f;
            while (elapsed < duration)
            {
                if (skipRequested) break;
                elapsed += Time.deltaTime;
                int display = Mathf.RoundToInt(Mathf.Lerp(originalGold, finalGold, elapsed / duration));
                totalGoldText.text = Gold("AdventureResult_TotalGold", display);
                yield return null;
            }
            totalGoldText.text = Gold("AdventureResult_TotalGold", finalGold);

            if (!skipRequested) yield return new WaitForSeconds(rowDelay);
        }

        private IEnumerator PlayNoGoldEarnedSequence()
        {
            if (noGoldEarnedRow == null) yield break;

            if (noGoldEarnedText != null)
                noGoldEarnedText.text = L("AdventureResult_NoGoldEarned");

            noGoldEarnedRow.SetActive(true);

            if (skipRequested)
            {
                noGoldEarnedRow.transform.localScale = Vector3.one;
                yield break;
            }

            noGoldEarnedRow.transform.localScale = Vector3.zero;
            noGoldEarnedRow.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(gameObject);
            yield return new WaitForSeconds(0.25f);

            if (!skipRequested) yield return new WaitForSeconds(rowDelay);
        }

        #endregion

        #region 수수료 연출 코루틴

        private IEnumerator PlayCommissionSequence(AdventureResult result)
        {
            // 활성화 전에 row 구성과 텍스트 폭을 먼저 확정 (활성화 직후 폭 점프 방지)
            PrepareCommissionRows(result);
            LockTextWidth(totalGoldText, Gold("AdventureResult_TotalGold", result.totalGoldReward));
            LockTextWidth(playerGoldText, Gold("AdventureResult_PlayerGold", result.playerGoldReward));

            commissionArea?.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(commissionArea?.transform as RectTransform);

            if (!skipRequested)
            {
                if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
                yield return StartCoroutine(AnimateGoldText(totalGoldText, result.totalGoldReward, "AdventureResult_TotalGold", ColorManager.Instance.GetWhiteColor()));
            }
            else
            {
                if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.alpha = 1f;
                SetGoldText(totalGoldText, result.totalGoldReward, "AdventureResult_TotalGold", ColorManager.Instance.GetWhiteColor());
            }

            if (!skipRequested) yield return new WaitForSeconds(rowDelay);

            yield return StartCoroutine(PlayCommissionRowsSequence(result));
        }

        private IEnumerator PlayCommissionRowsSequence(AdventureResult result)
        {
            ComputeCommissionGolds(result, out int baseGold, out int rentalGold, out int tipGold, out int traitGold);

            yield return StartCoroutine(ShowCommissionRowStep(baseCommissionRow, baseCommissionText,
                CommissionLabel("AdventureResult_CommissionBase", $"{result.baseCommissionRate * 100:0}"), baseGold));
            if (!skipRequested) yield return new WaitForSeconds(rowDelay);

            if (result.rentalCommissionRate > 0f)
            {
                yield return StartCoroutine(ShowCommissionRowStep(rentalCommissionRow, rentalCommissionText,
                    CommissionLabel("AdventureResult_CommissionRental", $"{result.rentalCommissionRate * 100:0}"), rentalGold));
                if (!skipRequested) yield return new WaitForSeconds(rowDelay);
            }

            if (result.tipCommissionRate > 0f)
            {
                yield return StartCoroutine(ShowCommissionRowStep(tipCommissionRow, tipCommissionText,
                    CommissionLabel("AdventureResult_CommissionTip", $"{result.tipCommissionRate * 100:0}"), tipGold));
                if (!skipRequested) yield return new WaitForSeconds(rowDelay);
            }

            if (!Mathf.Approximately(result.traitCommissionRate, 0f))
            {
                float pct = result.traitCommissionRate * 100f;
                string pctSign = pct >= 0f ? "+" : "";
                Color traitColor = traitGold < 0 ? ColorManager.Instance.GetRedColor() : ColorManager.Instance.GetWhiteColor();
                yield return StartCoroutine(ShowCommissionRowStep(traitCommissionRow, traitCommissionText,
                    CommissionLabel("AdventureResult_CommissionTrait", $"{pctSign}{pct:0}"), traitGold, traitColor));
                if (!skipRequested) yield return new WaitForSeconds(rowDelay);
            }

            if (!skipRequested)
            {
                if (commissionDividerCanvasGroup != null) commissionDividerCanvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
                yield return new WaitForSeconds(rowDelay);
            }
            else
            {
                if (commissionDividerCanvasGroup != null) commissionDividerCanvasGroup.alpha = 1f;
            }

            if (!skipRequested)
            {
                if (playerGoldRowCanvasGroup != null) playerGoldRowCanvasGroup.DOFade(1f, 0.2f).SetLink(gameObject);
                yield return StartCoroutine(AnimatePlayerGold(result.playerGoldReward));
            }
            else
            {
                if (playerGoldRowCanvasGroup != null) playerGoldRowCanvasGroup.alpha = 1f;
                ShowPlayerGoldImmediate(result.playerGoldReward);
            }

            if (!skipRequested) yield return new WaitForSeconds(rowDelay);
        }

        private void PrepareCommissionRows(AdventureResult result)
        {
            if (totalGoldRowCanvasGroup != null) totalGoldRowCanvasGroup.alpha = 0f;
            if (commissionDividerCanvasGroup != null) commissionDividerCanvasGroup.alpha = 0f;
            if (playerGoldRowCanvasGroup != null) playerGoldRowCanvasGroup.alpha = 0f;

            penaltyGoldRow?.SetActive(false);
            noGoldEarnedRow?.SetActive(false);

            if (baseCommissionRow != null)
            {
                baseCommissionRow.SetActive(true);
                if (baseCommissionText != null) baseCommissionText.text = string.Empty;
            }

            if (rentalCommissionRow != null && result.rentalCommissionRate > 0f)
            {
                rentalCommissionRow.SetActive(true);
                if (rentalCommissionText != null) rentalCommissionText.text = string.Empty;
            }

            if (tipCommissionRow != null && result.tipCommissionRate > 0f)
            {
                tipCommissionRow.SetActive(true);
                if (tipCommissionText != null) tipCommissionText.text = string.Empty;
            }

            if (traitCommissionRow != null && !Mathf.Approximately(result.traitCommissionRate, 0f))
            {
                traitCommissionRow.SetActive(true);
                if (traitCommissionText != null) traitCommissionText.text = string.Empty;
            }

            commissionDivider?.SetActive(true);

            if (playerGoldRow != null)
            {
                playerGoldRow.SetActive(true);
                if (playerGoldText != null) playerGoldText.text = string.Empty;
            }

            // AdjustDetailCommissionSpacing();
        }

        private void AdjustDetailCommissionHeight()
        {
            if (commissionArea == null || detailCommissionArea == null) return;
            var caRt = commissionArea.transform as RectTransform;
            var dcaRt = detailCommissionArea.transform as RectTransform;
            var vlg = commissionArea.GetComponent<VerticalLayoutGroup>();
            if (caRt == null || dcaRt == null || vlg == null) return;

            // 자기 자신의 이전 sizeDelta가 부모(CommissionArea)의 PreferredHeight를 부풀려서
            // 측정값을 오염시키는 피드백 루프 방지. 측정 전 0으로 리셋.
            var size = dcaRt.sizeDelta;
            size.y = 0f;
            dcaRt.sizeDelta = size;

            // RewardArea 재구성 → RewardItemContainer/CommissionArea 영역 재분배
            LayoutRebuilder.ForceRebuildLayoutImmediate(caRt.parent as RectTransform);

            float available = caRt.rect.height - vlg.padding.top - vlg.padding.bottom;
            float others = 0f;
            int activeCount = 0;
            foreach (Transform child in caRt)
            {
                if (!child.gameObject.activeSelf) continue;
                activeCount++;
                if (child.gameObject == detailCommissionArea) continue;
                others += (child as RectTransform).rect.height;
            }

            if (activeCount > 1)
            {
                float spacingTotal = vlg.spacing * (activeCount - 1);
                size.y = Mathf.Max(0f, available - others - spacingTotal);
                dcaRt.sizeDelta = size;
            }

            // CommissionArea 내부만 재정렬 (부모는 다시 안 건드림 → RewardItemContainer 위치 유지)
            LayoutRebuilder.ForceRebuildLayoutImmediate(caRt);
        }

        /// <summary>
        /// 항목별 정수 수수료 계산. 반올림 잔차(±1)를 기본 수수료 행에 흡수시켜
        /// 행들의 합 = 실제 지급액(playerGoldReward)이 되도록 보장한다.
        /// </summary>
        private void ComputeCommissionGolds(AdventureResult result,
            out int baseGold, out int rentalGold, out int tipGold, out int traitGold)
        {
            float total = result.totalGoldReward;
            baseGold   = Mathf.RoundToInt(total * result.baseCommissionRate);
            rentalGold = Mathf.RoundToInt(total * result.rentalCommissionRate);
            tipGold    = Mathf.RoundToInt(total * result.tipCommissionRate);
            traitGold  = Mathf.RoundToInt(total * result.traitCommissionRate);

            int sum = baseGold + rentalGold + tipGold + traitGold;
            baseGold += result.playerGoldReward - sum;
        }

        private IEnumerator ShowCommissionRowStep(GameObject row, TextMeshProUGUI text, string label, int gold, Color? color = null)
        {
            if (!skipRequested)
                yield return StartCoroutine(AnimateCommissionRow(row, text, label, gold, color));
            else
                ShowCommissionRowImmediate(row, text, label, gold, color);
        }

        private IEnumerator AnimateCommissionRow(GameObject row, TextMeshProUGUI text, string label, int gold, Color? color = null)
        {
            if (row == null || text == null) yield break;

            text.text = FormatCommissionText(label, gold);
            text.color = color ?? ColorManager.Instance.GetWhiteColor();
            if (!skipRequested) SoundManager.Instance?.PlaySFX("GetGold");

            text.transform.localScale = Vector3.zero;
            text.transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(gameObject);
            yield return new WaitForSeconds(0.18f);
        }

        private void ShowCommissionRowImmediate(GameObject row, TextMeshProUGUI text, string label, int gold, Color? color = null)
        {
            if (row == null || text == null) return;
            text.text = FormatCommissionText(label, gold);
            text.color = color ?? ColorManager.Instance.GetWhiteColor();
            text.transform.localScale = Vector3.one;
        }

        private static string FormatCommissionText(string label, int gold)
            => LocalizationSettings.StringDatabase.GetLocalizedString(
                   "UI_Screens", "AdventureResult_CommissionRow",
                   arguments: new object[] { new Dictionary<string, object> { { "label", label }, { "gold", gold.ToString("N0") } } });

        private IEnumerator AnimateGoldText(TextMeshProUGUI text, int amount, string key, Color color)
        {
            if (!text) yield break;
            text.color = color;
            if (!skipRequested) SoundManager.Instance?.PlaySFX("GetGold");

            LockTextWidth(text, Gold(key, amount));

            float elapsed = 0f;
            float duration = 0.4f;
            while (elapsed < duration)
            {
                if (skipRequested) break;
                elapsed += Time.deltaTime;
                int display = Mathf.RoundToInt(Mathf.Lerp(0, amount, elapsed / duration));
                text.text = Gold(key, display);
                yield return null;
            }
            text.text = Gold(key, amount);
        }

        private void SetGoldText(TextMeshProUGUI text, int amount, string key, Color color)
        {
            if (!text) return;
            text.color = color;
            text.text = Gold(key, amount);
        }

        private void LockTextWidth(TextMeshProUGUI text, string finalText)
        {
            if (text == null) return;
            float w = text.GetPreferredValues(finalText).x + textWidthLockPadding;
            var le = text.gameObject.GetOrAddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.minWidth = w;
        }

        private IEnumerator AnimatePlayerGold(int amount)
        {
            if (!playerGoldRow || !playerGoldText) yield break;

            if (!skipRequested) SoundManager.Instance?.PlaySFX("GetGold");

            LockTextWidth(playerGoldText, Gold("AdventureResult_PlayerGold", amount));

            float elapsed = 0f;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                if (skipRequested) break;
                elapsed += Time.deltaTime;
                int display = Mathf.RoundToInt(Mathf.Lerp(0, amount, elapsed / duration));
                playerGoldText.text = Gold("AdventureResult_PlayerGold", display);
                yield return null;
            }
            playerGoldText.text = Gold("AdventureResult_PlayerGold", amount);

            if (!skipRequested)
                playerGoldText.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 3, 0.3f).SetLink(gameObject);
        }

        private void ShowPlayerGoldImmediate(int amount)
        {
            if (!playerGoldRow || !playerGoldText) return;
            playerGoldText.text = Gold("AdventureResult_PlayerGold", amount);
            playerGoldText.transform.localScale = Vector3.one;
        }

        #endregion

        #region 결과 아이템 컨테이너

        private IEnumerator BuildResultItemContainer(AdventureInstance adventure, AdventureResult result)
        {
            if (resultItemContainer == null || adventureResultItemPrefab == null) yield break;

            ClearResultItems();

            // 1. 평판 (항상)
            var repItem = InstantiateTooltipResultItem();
            if (repItem != null) repItem.InitializeReputation(result.reputationChange);

            // 2. 호감도 (네임드만)
            bool isNamed = adventure.adventurer?.isNamed ?? false;
            if (isNamed)
            {
                var affItem = InstantiateTooltipResultItem();
                if (affItem != null) affItem.InitializeAffection(result.affectionChange);
            }

            // 3. 통찰 (대성공 시)
            if (result.insightChange > 0)
            {
                var insightItem = InstantiateTooltipResultItem();
                if (insightItem != null) insightItem.InitializeInsight(result.insightChange);
            }

            // 4. 재료 (드랍 있을 때만)
            bool hasMaterials = result.materialDrops != null && result.materialDrops.Count > 0;
            if (hasMaterials)
            {
                float priceMultiplier = adventure?.adventurer != null
                    ? AdventureManager.Instance.GetTraitMaterialPriceMultiplier(adventure.adventurer)
                    : 1f;

                // 재료 타입(Enforce→Special→Craft) → 개별 가격(내림차순) 순 정렬
                var sortedDrops = result.materialDrops
                    .Where(m => m.materialData != null)
                    .OrderBy(m => MaterialTypeSortRank(m.materialData.materialType))
                    .ThenByDescending(m => m.materialData.buyPrice);

                foreach (var material in sortedDrops)
                {
                    if (material.materialData == null) continue;

                    var matItem = InstantiateResultItem();
                    if (matItem != null)
                        matItem.InitializeMaterial(material.materialData, material.quantity, priceMultiplier, OnMaterialItemPurchaseClicked);
                }
            }

            // 모든 아이템 생성 후 레이아웃 한 번에 확정 → 위치 보장
            LayoutRebuilder.ForceRebuildLayoutImmediate(resultItemContainer as RectTransform);

            // RewardItem 줄 수가 확정된 후 CommissionArea 영역 크기가 결정되므로 높이 재계산
            AdjustDetailCommissionHeight();

            // 순차 페이드 인 (스킵 시 즉시 표시)
            // 코루틴이 yield로 멈춘 사이 resultItems가 변경(구매/재빌드)될 수 있으므로 스냅샷 순회
            var itemsSnapshot = resultItems.ToArray();
            if (skipRequested)
            {
                foreach (var item in itemsSnapshot)
                {
                    if (item == null) continue;
                    item.SnapToFinalState();
                }
            }
            else
            {
                foreach (var item in itemsSnapshot)
                {
                    if (item == null) continue;
                    item.PlayAppearAnimation();
                    // 스킵 검사보다 먼저 울리면, 남은 아이템 수만큼 GetItem이 한 프레임에 몰려 터진다.
                    if (skipRequested)
                    {
                        item.SnapToFinalState();
                        continue;
                    }
                    SoundManager.Instance?.PlaySFX("GetItem");
                    yield return new WaitForSeconds(rowDelay);
                }
            }

            RefreshBuyAllButton();
        }

        // 재료 정렬용 타입 우선순위: Enforce → Special → Craft
        private static int MaterialTypeSortRank(MaterialType type) => type switch
        {
            MaterialType.Enforce => 0,
            MaterialType.Special => 1,
            MaterialType.Craft   => 2,
            _ => 3
        };

        private AdventureResultItem InstantiateResultItem()
        {
            var go = Instantiate(adventureResultItemPrefab, resultItemContainer);
            var item = go.GetComponent<AdventureResultItem>();
            if (item != null) resultItems.Add(item);
            return item;
        }

        private AdventureResultItemTooltip InstantiateTooltipResultItem()
        {
            if (tooltipResultItemPrefab == null) return null;
            var go = Instantiate(tooltipResultItemPrefab, resultItemContainer);
            var item = go.GetComponent<AdventureResultItemTooltip>();
            if (item != null) resultItems.Add(item);
            return item;
        }

        private void OnMaterialItemPurchaseClicked(AdventureResultItem item)
        {
            controller?.OnMaterialPurchaseRequested(item, currentResult);
        }

        public void OnMaterialPurchaseCompleted(AdventureResultItem item)
        {
            item.MarkPurchased();
            RefreshBuyAllButton();
            TutorialManager.Instance?.OnTutorialMaterialPurchased();   // 9-B 훅(가드는 TutorialManager 내부)
        }

        /// <summary>튜토리얼 하이라이트용(9-B) - 첫 재료(구매 가능) 결과 아이템의 RectTransform. 없으면 null.</summary>
        public RectTransform GetFirstMaterialItemRect()
        {
            foreach (var item in resultItems)
                if (item != null && item.MaterialData != null && !item.IsPurchased)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(resultItemContainer as RectTransform);
                    return item.transform as RectTransform;
                }
            return null;
        }

        /// <summary>튜토리얼 하이라이트용(9-B) - 결과창 닫기 버튼 RectTransform.</summary>
        public RectTransform GetCloseButtonRect() => closeButton != null ? closeButton.transform as RectTransform : null;

        private void RefreshBuyAllButton()
        {
            bool hasUnpurchased = false;
            foreach (var item in resultItems)
            {
                if (item == null || item.MaterialData == null || item.IsPurchased) continue;
                hasUnpurchased = true;
                break;
            }

            buyAllMaterialsButton?.gameObject.SetActive(hasUnpurchased);
        }


        private void OnBuyAllMaterialsClicked()
        { 
            controller?.OnAllMaterialsPurchaseClicked(currentResult);
        }

        private void ClearResultItems()
        {
            foreach (var item in resultItems)
                if (item) Destroy(item.gameObject);
            resultItems.Clear();

            if (resultItemContainer != null)
            {
                foreach (Transform child in resultItemContainer)
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
            }
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// 결과 SFX(성공/실패/사망)는 1회만 재생한다.
        /// PerformInstantSkip이 시퀀스를 처음부터 재시작하므로 가드가 없으면 중복 재생된다.
        /// </summary>
        private void PlayResultSfxOnce(string clipName)
        {
            if (resultSfxPlayed) return;
            resultSfxPlayed = true;
            SoundManager.Instance?.PlaySFX(clipName);
        }

        private void PerformInstantSkip()
        {
            if (skipRequested) return;
            skipRequested = true;

            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
                sequenceCoroutine = null;
            }

            // 여기서 StopAllSFX를 부르지 않는다. 결과음(AdventureSuccess 등)은 시퀀스 앞부분에서 이미 울렸는데,
            // 끊어버리면 resultSfxPlayed 가드 때문에 재시작해도 다시 나오지 않아 팡파레가 잘린다.
            // 스킵 후 연출음이 쏟아지는 문제는 GetGold/GetItem의 skipRequested 가드로 막는다.

            if (currentResult == null) return;

            if (currentResult.isDeath)
                sequenceCoroutine = StartCoroutine(PlayDeathSequence(currentAdventure, currentResult));
            else if (currentResult.isRetreated || !currentResult.isSuccess)
                sequenceCoroutine = StartCoroutine(PlayFailureSequence(currentAdventure, currentResult));
            else
                sequenceCoroutine = StartCoroutine(PlayResultSequence(currentAdventure, currentResult));
        }

        private void ActivateCloseButton()
        {
            if (!closeButton) return;
            closeButton.gameObject.SetActive(true);
            if (skipRequested)
            {
                closeButton.transform.localScale = Vector3.one;
            }
            else
            {
                closeButton.transform.localScale = Vector3.zero;
                closeButton.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            // 9단계 훅: 결과 연출이 끝나면(닫기 버튼 활성) 튜토리얼 안내로 이어간다(가드는 TutorialManager 내부).
            TutorialManager.Instance?.OnTutorialResultSequenceComplete();
        }

        private void OnCloseClicked()
        {
            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            controller?.OnResultCloseClicked();
        }

        public override void OnEscapeCancelled()
        {
            // 연출 완료(닫기 버튼 활성) 후에는 닫기 버튼과 동일 동작.
            // 연출 중에는 닫기 버튼이 없으므로 실수 방지 확인 팝업을 거친다.
            if (closeButton != null && closeButton.gameObject.activeSelf)
            {
                OnCloseClicked();
                return;
            }
            UIPopupController.Instance?.ShowPopup(
                LocalizationSettings.StringDatabase.GetLocalizedString("UI_Messages", "AdventureResult_CloseConfirm"),
                OnCloseClicked, () => { });
        }

        #endregion
    }
}
