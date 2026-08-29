---
tags:
  - Design
  - Completed
  - 모험
aliases:
  - Segment 바
created: 2026-05-25
updated: 2026-07-28
---

# Segment 바 구조와 개선안

모험 준비 Tab3 의 "성공률 영향 요소" 시각화에 대한 현황 정리와 리팩토링 후보.

---

## 0. 의도

> 모험가는 정확한 수치는 알 수 없지만, segment 바를 보고 모험을 보낼지 말지 판단할 수 있어야 한다.
> 시뮬레이션상 미확정 요소가 섞여 있어 그 불확실성도 재미로 가져간다.

핵심 합의 (2026-05-25):

- **개별 칩**과 **통합 바**는 의미가 같아야 한다 — *각 칩의 칸 수의 합 = 통합 바의 칸 수*.
- 통합 바의 1칸 단위는 *각 보너스의 `maxAbsValue` 기준*으로 쪼갠다 (현재 코드 유지).

---

## 1. 현재 구조

### 1.1 데이터 흐름

```
AdventurePreparationController.BuildAdventureInfoCards()
  → List<AdventureInfoCardData> (type, value, isConfirmed)
  → PreparationTab3Panel.UpdateAdventureInfo(cards)
        ├─ AdventureInfoCardItem.SetInfo()          (개별 칩, 칸 = 그 보너스의 강도)
        └─ ApplyUnifiedBarColors(cards)             (통합 바, 칩 칸들을 이어 붙임)
```

데이터 출처 8종 (AdventurePreparationController.cs:612-668 (`Assets/_Projects/Scripts/UI/Controllers/AdventurerInteraction/AdventurePreparationController.cs`)):

| # | BonusType | 값 출처 | 확정 조건 |
|---|---|---|---|
| 1 | Affection | 호감도 레벨 → Config 보너스 | 항상 확정 |
| 2 | Charm | 부적 보유 시 고정값 | 항상 확정 |
| 3 | Trait | 특성 매칭 보너스 + 멀티 | 통찰 80↑ 자동 공개 시 |
| 4 | DungeonArmor | breakdown.armorBonus | 정찰로 방어 타입 공개 시 |
| 5 | WeaponCondition | breakdown.conditionBonus | armorTypeBonus 없거나 방어 공개 시 |
| 6 | Collection | 던전/무기 마일스톤 보너스 | 항상 확정 |
| 7 | Seer | 점술 결과 modifier | 점술 완료 시 항상 |
| 8 | WeaponAdventurerMatch | baseRate + statEffectBonus | 통찰 가시성 조건 만족 시 |

### 1.2 설정 (BonusVisualInfo)

AdventureInfoConfig.cs:22-32 (`Assets/_Projects/Scripts/Data/Config/AdventureInfoConfig.cs`)

```csharp
public class BonusVisualInfo
{
    public BonusType   bonusType;
    public string      displayName;
    public Sprite      icon;
    public Color       positiveColor;
    public Color       negativeColor;
    [Range(3, 15)] public int segmentCount = 5;
    public float       maxAbsValue = 0.15f;   // 칸 1개 = maxAbsValue / segmentCount
}
```

→ **각 BonusType 마다** `(maxAbsValue, segmentCount)` 쌍을 따로 가짐. 따라서 *1 칸이 의미하는 성공률 폭이 타입마다 다르다*.

### 1.3 개별 칩 — AdventureInfoCardItem

AdventureInfoCardItem.cs:67-97 (`Assets/_Projects/Scripts/UI/ListItems/AdventureInfoCardItem.cs`)

```
ratio  = |value| / maxAbsValue
filled = CeilToInt(ratio * segmentCount)        // 0 ~ segmentCount 로 클램프
color  = value < 0 ? negativeColor : positiveColor
```

- `segmentContainer` 폭을 `segmentCount` 로 균등 분할 → 칸 폭 결정.
- `filled` 개수만큼 segment 프리팹을 인스턴스화해 색칠.
- 음수일 때도 양수와 동일하게 *왼쪽부터* 칸을 채움 (단지 색만 다름).

### 1.4 통합 바 — PreparationTab3Panel.ApplyUnifiedBarColors

PreparationTab3Panel.cs:172-238 (`Assets/_Projects/Scripts/UI/Views/AdventurerInteraction/PreparationTab3Panel.cs`)

양수 처리:
```
foreach (card in cards where card.value > 0):
    info     = AdventureInfo.GetBonusInfo(card.type)
    filled   = CeilToInt(|card.value| / info.maxAbsValue * info.segmentCount)
    unitValue = info.maxAbsValue / info.segmentCount
    for i in 0..filled:
        instantiate UnifiedBarSegment with (positiveColor, unitValue)
```

→ 칩에 쌓인 양수 칸들을 *그대로 이어 붙인다*. 칸 색은 각 보너스의 `positiveColor`.

음수 처리:
```
remaining = Σ |card.value| where card.value < 0     // 음수 보너스를 하나의 풀로 합침
for seg in unifiedSegments.reverse():               // 우측 segment 부터
    remaining = seg.ConsumeMinus(remaining)
```

`UnifiedBarSegment.ConsumeMinus` (UnifiedBarSegment.cs:28-42 (`Assets/_Projects/Scripts/UI/ListItems/UnifiedBarSegment.cs`)):
- `remaining ≥ unitValue` → indicator 켜고 unitValue 만큼 차감
- `remaining < unitValue` → minusSlider 부분 채움 후 0 리턴

칸 폭 조정:
- 통합 바 컨테이너 폭을 `unifiedSegments.Count` 로 나눈 값이 prefab 기본 폭보다 작으면 그 값 사용, 아니면 prefab 폭 유지.

---

## 2. 발견된 모호함 / 문제점

> "개별 칩의 합 = 통합 바" 라는 의도를 기준으로 검토한다.

### A. 1 칸의 "단위"가 보너스 타입마다 다르다 — 핵심 문제

`maxAbsValue`, `segmentCount` 를 타입마다 따로 두기 때문에:

| 타입 | maxAbsValue | segmentCount | 1칸 = |
|---|---|---|---|
| Affection | 0.15 | 5 | 3% 성공률 |
| Charm     | 0.05 | 5 | 1% 성공률 |
| Trait     | 0.20 | 5 | 4% 성공률 |

→ 통합 바에서 Affection 1 칸과 Charm 1 칸이 *시각적으로 같은 폭*이지만 *실제 기여도는 3 배 차이*.
→ "칸 수의 합" 으로 모험 가능성을 판단하라는 의도가 **시각적으로 거짓말**이 된다.

음수 차감도 같은 문제를 더 심하게 드러낸다. `ConsumeMinus` 가 우측 segment 부터 그 segment 의 `unitValue` 만큼 음수 풀을 깎는데, 우측에 어떤 타입 segment 가 와있느냐에 따라 *같은 음수 값이 다른 개수의 칸을 깎는다*. (Charm segment 가 우측이면 5 칸 다 깎이는데, Trait segment 가 우측이면 1~2 칸만 깎임.)

### B. CeilToInt 의 시각적 과장

`CeilToInt(ratio * segmentCount)` 때문에 0.001 같은 미미한 값도 1 칸이 통째로 켜진다.
→ "있긴 한데 거의 없음" 과 "있고 의미 있음" 을 구분하지 못한다.
→ "미확정 + 작은 값" 인 던전 상성 같은 항목이 실제보다 강해 보일 수 있다.

### C. 미확정성이 통합 바에 반영되지 않는다

- 개별 칩은 `unknownIndicator` / `confirmIndicator` 로 미확정 여부를 표시 (AdventureInfoCardItem.cs:49-50 (`Assets/_Projects/Scripts/UI/ListItems/AdventureInfoCardItem.cs`)).
- 통합 바 segment 는 색만 갖고 *어떤 타입의 어떤 확정 상태에서 왔는지* 가 모두 사라진다.
- 미확정 항목 4 개 + 확정 항목 1 개로 채워진 바와, 확정 항목 5 개로 채워진 바가 **시각적으로 동일**.
- 의도("불확실성을 재미로") 와 정면 충돌.

### D. 칩 ↔ 통합 바 칸 폭이 시각적으로 연결되지 않는다

- 칩 칸 폭: 칩 컨테이너 폭 / 칩의 segmentCount
- 통합 바 칸 폭: min(통합 바 폭 / 전체 칸 수, prefab 폭)

→ Affection 칩에서 본 1 칸의 폭이 통합 바에서 다른 폭으로 나타남. 합계 메타포가 시각적으로 부서진다.

### E. 음수 보너스가 풀로 합쳐진다

- 음수 카드 여러 개가 들어와도 `remaining` 으로 합산되어 *우측 segment* 부터 깎임.
- 결과: "DungeonArmor 가 -0.05" 라는 정보가 통합 바에서는 위치/소속을 잃고 "그냥 우측이 깎인" 으로 보임.
- 음수 보너스가 어떤 타입인지 통합 바에서는 알 수 없다.

### F. `maxAbsValue` 초과 시 정보 손실

- value 가 maxAbsValue 를 초과하면 `Clamp` 로 segmentCount 칸에 갇힌다.
- 만렙 칩이 "딱 maxAbsValue 인지" vs "한참 넘었는지" 구분 불가.
- 게임 후반 누적 보너스가 maxAbsValue 를 자주 넘게 되면 *5/5 인 칩이 흔해져* 판단 정보로서의 가치가 사라진다.

### G. AdventureInfoConfig.GetTotalSegmentCount() 가 어디서도 안 쓰임

AdventureInfoConfig.cs:50-57 (`Assets/_Projects/Scripts/Data/Config/AdventureInfoConfig.cs`) — 통합 바 칸 수 상한선 산정용으로 추정되지만 호출처 없음. 데드 코드 후보.

### H. (참고) "개별 칩의 합 = 통합 바" 의도가 코드 주석에 없음

`UnifiedBarSegment.cs` 의 클래스 주석은 "양수 보너스 1칸 단위로 색칠" 만 적혀 있어, 의도(불확실성 표현, 합 = 판단 지표)를 다음 리딩 때 잃기 쉽다.

---

## 3. 개선안 후보

세 개의 결이 다른 안을 제시한다. 채택 안에 따라 작업 범위가 크게 달라진다.

### 안 1. **전역 1칸 단위로 통일** — "합 = 통합 바" 의도를 코드 수준에서 보장 (Recommended)

핵심 변경:
- `BonusVisualInfo.maxAbsValue` / `segmentCount` 제거.
- `AdventureInfoConfig` 에 **전역 상수** 추가:
  ```csharp
  public float segmentUnitValue   = 0.02f;   // 1 칸 = 성공률 2%
  public int   maxSegmentsPerChip = 8;       // 한 칩이 가질 수 있는 최대 칸 수 (시각적 안전선)
  ```
- 칩 칸 수 = `RoundToInt(|value| / segmentUnitValue)`, 상한 `maxSegmentsPerChip`.
- 통합 바 칸 = 모든 칩의 양수 칸을 이어 붙임 → *진짜로 성공률 합산이 된다*.
- 음수도 동일 단위로 칸을 만들고, *해당 칩 칸 옆에* 표시하거나 통합 바 우측에 별도 negative 영역을 둔다.

장점:
- 개별 칩의 1 칸과 통합 바의 1 칸이 *물리적으로 같은 의미* (성공률 X%) → 의도 일치.
- 통합 바 총 칸 수가 곧 "이 모험의 보너스 총합" 이 된다 — 플레이어가 직관적으로 비교 가능.
- 음수 차감 로직이 단순해진다 (단위가 통일되므로 풀/세그먼트 매핑이 일관).

단점:
- 보너스 종류별 "직관적 강도" 표현이 약해진다 (Charm 0.05 = 2~3 칸, Affection 0.15 = 7~8 칸). 차이가 정직하게 드러나는 게 장점이자 단점.
- 후반 누적 보너스가 너무 길어질 수 있음 → `maxSegmentsPerChip` 로 잘라야 함 (안 F 해소).

### 안 2. **칩은 자기 강도, 통합 바는 진짜 성공률 게이지로 분리**

핵심 변경:
- 개별 칩은 현재 유지 (자기 maxAbsValue 기준 강도).
- 통합 바는 **칩 칸 이어 붙이기를 그만두고**, `breakdown.totalRate` 같은 합산 성공률을 0~100% 영역으로 표시.
- 미확정 기여분은 통합 바에서 *반투명/패턴* 으로 별도 표시.

장점:
- 칩은 "이 요소가 자기 카테고리 내에서 얼마나 큰가" 라는 직관적 의미 유지.
- 통합 바는 "최종 성공률" 이라는 절대 지표로 명확해진다.
- 미확정성 시각화 (안 C) 자연스럽게 해소.

단점:
- 사용자가 합의한 "개별 칩의 합 = 통합 바" 메타포를 포기해야 한다. **이 안을 선택하려면 의도 자체를 다시 합의해야 함.**

### 안 3. **현재 구조 유지 + 명시적 단위 표기 + 미확정 시각화만 보강** (보수적)

핵심 변경:
- `maxAbsValue`/`segmentCount` 는 유지하되, 칩의 툴팁에 "1 칸 ≈ X% 성공률" 표기.
- 통합 바 segment 에 `isConfirmed` 데이터를 같이 넘겨, 미확정 segment 는 외곽선/줄무늬로 표시.
- `ConsumeMinus` 의 단위 일관성 문제 (안 A 후반부) 는 **음수 표시 위치를 해당 타입 segment 근처로 옮기는** 식으로 부분 완화.

장점:
- 변경 범위 최소. 프리팹/인스펙터 작업 적음.
- 의도 (불확실성 재미) 일부 보강.

단점:
- **안 A 의 근본 모순 (1 칸 단위 불일치) 은 해결 못함**. "개별 칩의 합 = 통합 바" 메타포가 여전히 시각적으로 거짓말.
- 단위 표기를 툴팁에서 봐야 한다 = 한눈에 판단하라는 본래 의도와 충돌.

---

## 4. 권장 진행 순서

1. **의도 재확인** — "개별 칩의 합 = 통합 바" 가 정말 핵심 요구라면 **안 1** 외 답이 없다. 만약 "각 보너스의 상대적 강도" 도 동등하게 중요하면 **안 2** 로 메타포를 재설계.
2. 채택 시 인스펙터 작업: `AdventureInfoConfig.asset` 의 `bonusVisuals` 리스트 항목들에서 `maxAbsValue` / `segmentCount` 의 의미가 바뀌거나 제거됨 — 디자이너와 1 회 합의 필요.
3. 통합 바 segment 가 *어느 타입에서 왔는지* 추적할 수 있게 `UnifiedBarSegment.Configure` 시그니처에 `BonusType` 과 `bool isConfirmed` 추가 — 안 1·2·3 공통으로 거의 항상 필요.
4. `AdventureInfoConfig.GetTotalSegmentCount()` 는 채택안에 따라 제거 또는 재정의.

---

## 5. 미확정성 시각화 (안과 무관하게 별도 검토 가치)

현재는 칩 옆 indicator 만 활용. segment 자체에도 다음 중 하나를 도입하면 의도("불확실성을 재미로") 가 강화된다:

- **반투명 색**: 미확정 segment 는 알파 0.5
- **줄무늬/패턴 오버레이**: 색은 유지, 위에 사선 패턴
- **외곽선 점선**: fill 은 그대로, 외곽선만 점선

UI 부담은 (1) < (2) < (3) 순. 안 1 채택 시 통합 바 segment 별 `isConfirmed` 가 자연스럽게 들어오므로 같이 진행 권장.

---

## 6. 결정사항 (2026-05-25 합의)

1. **통합 바 제거** — 합산 메타포가 정밀성 메시지를 줘서 도박성 의도와 충돌. 칩만 남긴다.
2. **칩은 안 3 (5 칸 고정)** — `BonusVisualInfo.segmentCount` 제거, 전역 `segmentCount = 5`. `maxAbsValue` 는 *타입별 "이 정도면 만렙" 기준* 으로 의미 재정의.
3. **통합 바 자리에 정성적 표지** — 합산 게이지가 아니라 *감정/분위기 단어*. "유리/불리" 같은 직접적 훈수는 금지. 다크한 톤의 단어로 base, 변동성 요소에 따라 단어 자체를 변형 (단순 접미사 X).

---

## 7. 보너스 값 분포 분석

`AdventureManager.CalculateSuccessRate` 와 `BuildAdventureInfoCards` 가 칩으로 노출하는 값들의 *실제 발생 범위* 를 코드/asset 에서 추출.

### 7.1 칩 타입별 값 범위

| BonusType | 출처 | 실제 값 분포 | 비고 |
|---|---|---|---|
| **Affection** | `AdventureConfig.affectionMaxBonus` 등 | 0 / +0.01 / +0.03 / +0.05 | 단계형 — 음수 없음 |
| **Charm** | `ActiveItemData.csv` (행운/수호 부적) | 0 / +0.15 / +0.20 | 음수 없음, 보유 시에만 표시. 코드상 baseRate 곱 보정이지만 칩에서는 가산값으로 노출 |
| **Trait** | `GetTraitSuccessBonus` + (`GetTraitSuccessMultiplier` − 1) | -0.15 ~ +0.20 | Focused +0.15, EasyExpert ±0.15~0.20, Rising +0.02~+0.10, BattleManiac +0.10, Berserker +0.10 곱, Coward −0.15 곱 |
| **DungeonArmor** | `TypeAdvantage.weaponArmorBonus[8,4]` | -0.30 / -0.20 / -0.10 / +0.10 ~ +0.40 | 8×4 상성 테이블. 음/양 모두 흔함. 곱 보정이지만 칩에선 가산값으로 노출 |
| **WeaponCondition** | `DungeonGradeBonus` + `ArmorTypeBonus` (효과값 currentValue 합) | 0 ~ +0.10 정도 (효과 보유 시) | 음수 거의 없음. 무기에 해당 효과가 있고 조건 충족 시만 발생 |
| **Collection** | 던전 마일스톤 +0.02/회 + 무기 마일스톤 +0.01/회 (최대 5회) | 0 ~ ~+0.15 (후반) | 음수 없음, 누적형 |
| **Seer** | `SeerConfig.luckModifier*` | -0.10 / +0.05 / +0.10 / +0.20 | 점술 완료 시에만 표시 |
| **WeaponAdventurerMatch** | `baseRate + statEffectBonus` (`SuccessRateBreakdown`) | 0 ~ +1.0 (스탯·효과 기반) | **다른 칩과 단위가 다름** — 다른 칩은 *증감 보정* 이지만 이 칩은 *기본 성공률 자체* (typically 0.3 ~ 0.9) |

### 7.2 핵심 관찰

- **WeaponAdventurerMatch 만 단위가 다르다.** 나머지 7 종은 모두 *±0.30 이내의 증감 보정* 인데, 이 칩 하나만 *0.3 ~ 0.9 의 베이스 성공률 자체*. E 풀에 그대로 합산하면 이 칩 하나가 모든 합을 지배한다.
- WeaponAdventurerMatch 는 **통찰 80↑ 또는 (스탯 3개 공개 + 방어 공개)** 시에만 노출 (AdventurePreparationController.cs:660-666 (`Assets/_Projects/Scripts/UI/Controllers/AdventurerInteraction/AdventurePreparationController.cs`)). 즉 *게임 후반 정보 진척도 지표* 역할.
- 미확정 칩의 후보는 사실상 두 개뿐:
  - **DungeonArmor** (방어 미정찰 시) — ±0.10 ~ ±0.40 의 큰 변동
  - **WeaponCondition** (방어 의존분이 있고 미정찰 시) — 0 ~ +0.10 의 중간 변동
- 따라서 V (변동 풀) 의 실제 도달 범위: 0 ~ +0.50 정도. T_high 임계는 0.10 이 적당.

### 7.3 보너스 합산 시나리오 분포 (시뮬레이션)

WeaponAdventurerMatch 제외한 *증감 보정 칩들* 의 합산 E_delta 가 *실제 게임에서 얼마나 자주 어느 값에 도달* 하는지 시나리오 기반 추정:

| 상황 | 활성 칩 조합 | E_delta |
|---|---|---|
| 초반, 호감도 없음 | (없음) | 0 |
| 초반 단골 (호감도 Medium) | Affection +0.01 | +0.01 |
| 호감도 Max + 부적 보유 | Affection +0.05, Charm +0.15 | +0.20 |
| 위 + 강한 특성 | + Focused +0.15 | +0.35 |
| 위 + 점술 대길 | + Seer +0.20 | +0.55 |
| 위 + 던전 상성 우세 (정찰완료) | + DungeonArmor +0.40 | +0.95 (천장) |
| 단골이지만 던전 상성 나쁨 (정찰완료) | Affection +0.05, DungeonArmor −0.30 | −0.25 |
| 위 + 겁쟁이 특성 + 흉점 | Affection +0.05, Trait −0.15, Seer −0.10, DungeonArmor −0.30 | −0.50 (바닥) |
| 평균적 케이스 (중반) | Affection +0.03, Collection +0.04, 무엇이든 ±0.05 | ±0.05 ~ +0.12 |

→ E_delta 의 *대부분의 도달 영역은 −0.20 ~ +0.30*. 극단 ±0.50 도 가능하지만 흔치 않음.

→ 따라서 base band 임계는 ±0.05 / ±0.15 가 자연스러운 분할.

---

## 8. 정성적 표지 (Mood Label) 설계

### 8.1 입력 정의

```
cards = BuildAdventureInfoCards(...) 의 결과
hasBaseChip = cards 에 WeaponAdventurerMatch 타입이 있음 (= 게임 후반 정보 진척)

E_delta = Σ value           where isConfirmed=true  AND type ≠ WeaponAdventurerMatch
E_base  = WeaponAdventurerMatch.value if hasBaseChip else null

V_plus  = Σ value           where isConfirmed=false AND value > 0
V_minus = Σ |value|         where isConfirmed=false AND value < 0
V       = V_plus + V_minus
```

### 8.2 단일 모드 (WeaponAdventurerMatch 제거 후)

WeaponAdventurerMatch 칩은 *제거 확정* (§9.5). 따라서 chip 풀에는 *증감 보정 단위* (±0.30 이내) 의 값들만 남음. 두 모드 분기 불필요.

```
E = Σ value           where isConfirmed=true
V_plus  = Σ value     where isConfirmed=false AND value > 0
V_minus = Σ |value|   where isConfirmed=false AND value < 0
V       = V_plus + V_minus
```

### 8.3 Base band (E 5 단계)

| 매우 어두움 | 어두움 | 중립 | 밝음 | 매우 밝음 |
|---|---|---|---|---|
| E ≤ -0.15 | -0.15 < E ≤ -0.05 | -0.05 < E < +0.05 | +0.05 ≤ E < +0.15 | E ≥ +0.15 |

### 8.4 Variance 상태 (V 4 가지)

T_low 는 *동적* — 현재 표시된 chip 들의 절댓값 합의 비율로 계산 (§9.4 참조).

```
total_magnitude = Σ |value| for all cards shown
T_low  = max(0.02, total_magnitude × 0.10)    # 동적
T_lean = 0.7                                   # 정적

if V < T_low:                               V_state = "none"   (변동 없음)
elif V_plus / V >= T_lean:                  V_state = "pos"    (변동성 보너스 우세)
elif V_minus / V >= T_lean:                 V_state = "neg"    (변동성 페널티 우세)
else:                                       V_state = "mixed"  (변동성 균형)
```

미확정 칩의 value 는 *시뮬레이션 그대로* 한쪽 (V_plus 또는 V_minus) 에 분류. unknown 이 mixed 자동 분류 되지 않음 — 시뮬레이션은 단일 결정값을 가정.

### 8.5 라벨 매트릭스 (5 × 4 = 20 cell)

각 셀은 *감정/분위기 단어* 한 개. 단순 접미사 ("(변수 있음)") 형태가 아니라 **단어 자체가 다름**. 라벨 후보안:

| Base band | V=none | V=pos (변동성↑보너스) | V=neg (변동성↑페널티) | V=mixed (균형) |
|---|---|---|---|---|
| **매우 밝음** | 환희 | 의기양양 | 자만 | 흥분 |
| **밝음** | 희망 | 설렘 | 조마조마 | 두근거림 |
| **중립** | 담담 | 호기심 | 부담 | 안절부절 |
| **어두움** | 불안 | 기대 | 답답 | 갑갑함 |
| **매우 어두움** | 암울 | **미련** *(혹시?)* | 절망 | **싱숭생숭** *(애매)* |

→ 사용자 예시 (암울 + V=pos → "기대", 암울 + V=mixed → "싱숭생숭") 와 일치.

### 8.6 검증 시나리오 — 모험 시작 시점 라벨

DungeonGrade chip 포함 (옵션 α: Common +0.15, Uncommon +0.06, Rare -0.03, Epic -0.09, Legendary -0.15). 단일 모드, 동적 T_low.

#### A. 초반 (정보 진척 X — DungeonGrade chip 만)

| # | 던전 | 활성 chip | E | total | T_low | E_band | V_state | **라벨** |
|---|---|---|---|---|---|---|---|---|
| A1 | Common | DG +0.15 | +0.15 | 0.15 | 0.02 | 매우 밝음 | none | **환희** |
| A2 | Uncommon | DG +0.06 | +0.06 | 0.06 | 0.02 | 밝음 | none | **희망** |
| A3 | Rare | DG -0.03 | -0.03 | 0.03 | 0.02 | 중립 | none | **담담** |
| A4 | Epic | DG -0.09 | -0.09 | 0.09 | 0.02 | 어두움 | none | **불안** |
| A5 | Legendary | DG -0.15 | -0.15 | 0.15 | 0.02 | 매우 어두움 | none | **암울** |

→ 던전 등급만으로 5 단계 라벨이 자연 분포. 다른 chip 이 여기서 라벨을 반전·강화.

#### B. 호감도·부적 (통찰 X, 정찰 X)

| # | 던전 | 추가 chip | E | total | T_low | E_band | V_state | **라벨** |
|---|---|---|---|---|---|---|---|---|
| B1 | Common | 호감도 Med (+0.01) | +0.16 | 0.16 | 0.02 | 매우 밝음 | none | **환희** |
| B2 | Common | 호감도 Max + 부적 (+0.05, +0.15) | +0.35 | 0.35 | 0.035 | 매우 밝음 | none | **환희** |
| B3 | Rare | 호감도 Max + 부적 | +0.17 | 0.23 | 0.023 | 매우 밝음 | none | **환희** |
| B4 | Epic | 호감도 Max + 부적 | +0.11 | 0.29 | 0.029 | 밝음 | none | **희망** |
| B5 | Legendary | 호감도 Max + 부적 | +0.05 | 0.35 | 0.035 | 밝음 (경계) | none | **희망** |

→ Legendary 도 부적+호감도Max 면 "희망" 까지 끌어올림. 환희는 아님 — 의도된 격차.

#### C. 통찰 + 점술 (특성·점술 공개)

| # | 던전 | 추가 chip | E | total | T_low | E_band | V_state | **라벨** |
|---|---|---|---|---|---|---|---|---|
| C1 | Rare | Focused +0.15 + 길점 +0.10 | +0.22 | 0.28 | 0.028 | 매우 밝음 | none | **환희** |
| C2 | Epic | Coward -0.15 + 호감도 Max | -0.19 | 0.29 | 0.029 | 매우 어두움 | none | **암울** |
| C3 | Legendary | EasyExpert -0.15 + 흉점 -0.10 | -0.40 | 0.40 | 0.04 | 매우 어두움 | none | **암울** |
| C4 | Common | Berserker +0.10 | +0.25 | 0.25 | 0.025 | 매우 밝음 | none | **환희** |
| C5 | Legendary | Focused +0.15 + 호감도 Max + 부적 + 길점 | +0.35 | 0.65 | 0.065 | 매우 밝음 | none | **환희** |

→ C5: Legendary 도 풀 정보 모이면 환희 도달 가능. 정찰·통찰 동기.

#### D. 미정찰 던전 (DungeonArmor V 풀)

| # | 던전 | 추가 chip | E | V₊ | V₋ | total | T_low | E_band | V_state | **라벨** |
|---|---|---|---|---|---|---|---|---|---|---|
| D1 | Common | DA +0.40 (미확정, 시뮬 정상성) | +0.15 | 0.40 | 0 | 0.55 | 0.055 | 매우 밝음 | pos | **의기양양** |
| D2 | Common | DA -0.30 (미확정, 시뮬 역상성) | +0.15 | 0 | 0.30 | 0.45 | 0.045 | 매우 밝음 | neg | **자만** |
| D3 | Epic | 호감도 Max + 부적, DA -0.20 (미확정) | +0.11 | 0 | 0.20 | 0.49 | 0.049 | 밝음 | neg | **조마조마** |
| D4 | Legendary | DA +0.30 (미확정) | -0.15 | 0.30 | 0 | 0.45 | 0.045 | 매우 어두움 | pos | **미련** |

#### E. 도박적 결정 (음수 확정 + 미확정 변동)

| # | 던전 | 추가 chip | E | V₊ | V₋ | total | T_low | E_band | V_state | **라벨** |
|---|---|---|---|---|---|---|---|---|---|---|
| E1 | Epic | EasyExpert -0.15 + 흉점 -0.10 + DA +0.30 (미확정) | -0.34 | 0.30 | 0 | 0.64 | 0.064 | 매우 어두움 | pos | **미련** ⭐ |
| E2 | Epic | E1 의 DA 미확정 -0.20 으로 뒤집힘 | -0.34 | 0 | 0.20 | 0.54 | 0.054 | 매우 어두움 | neg | **절망** |
| E3 | Legendary | Coward -0.15 + 흉점 -0.10 + DA -0.20 (정찰완료, 확정) | -0.60 | 0 | 0 | 0.60 | 0.06 | 매우 어두움 | none | **암울** |
| E4 | Rare | 호감도 Med + WC +0.08 (미확정) + DA +0.20 (미확정) | -0.02 | 0.28 | 0 | 0.31 | 0.031 | 중립 | pos | **호기심** |
| E5 | Legendary | 호감도 X + DA +0.30 (미확정) | -0.15 | 0.30 | 0 | 0.45 | 0.045 | 매우 어두움 | pos | **미련** |

⭐ E1 = 게임 핵심 결정 — "거의 절망적인데 미정찰이 반전시킬 수 있다" → 정찰 더 할지 강행할지.

#### 동적 임계 효과

- **A1 vs A5**: 같은 단일 chip 이지만 부호 반대 → 환희 vs 암울. 등급 자체가 라벨 좌우.
- **C3 vs C5**: 둘 다 Legendary 지만 E 차이 (-0.40 vs +0.35) → 암울 vs 환희. 풀 정보 vs 페널티 정보.
- **D1 vs D3**: total 크기 차이 (0.55 vs 0.49) → T_low 비슷하지만 V_state 다름. 호감도+부적이 E 를 끌어올려 D3 는 "조마조마", D1 은 "의기양양".
- **E1 → E2**: 같은 베이스인데 DA 미확정 부호만 반대 → 미련 vs 절망. *시뮬레이션 결과에 라벨이 민감* — 플레이어가 던전 선택 화면에서 armorType 시뮬레이션을 바꿔보며 분위기 살피는 동기 발생.

### 8.7 튜닝 가능 노브 (인스펙터로 노출)

`AdventureInfoConfig` 또는 새 `MoodLabelConfig` 에 둘 값:

```csharp
// Base band 임계 (단일 세트)
public float[] bandThresholds = { -0.15f, -0.05f, +0.05f, +0.15f };

// Variance 임계
public float varianceLowRatio       = 0.10f;   // total_magnitude 대비 비율
public float varianceLowFloor       = 0.02f;   // T_low 의 절대 하한
public float varianceLeanThreshold  = 0.7f;

// 라벨 사전 (5 band × 4 state = 20 entry)
[Serializable]
public class MoodLabelEntry {
    public Band band; public VarianceState state; public string label; public Color color;
}
public List<MoodLabelEntry> labels;
```

→ 디자이너가 단어/색/임계 모두 인스펙터로 조정 가능.

### 8.8 확정 사항 (2026-05-25)

- **라벨 단어 20 개** — 위 매트릭스 그대로 확정 (수정: 어두움/pos = "기대", 매우 어두움/pos = "미련", 매우 어두움/neg = "절망").
- **Band 임계** — 위 표 그대로 확정.
- **V_state 임계** — `T_low = 0.03`, `T_lean = 0.7` 그대로 확정.
- **시나리오 검증** — 직관적이라 확인됨.
- **WeaponAdventurerMatch 처리** — *칩에서 제거* (베이스값이라 단위가 안 맞음). 대신 다른 baseline chip 도입. → 9 절 참조.

---

## 9. baseline chip 재설계 — WeaponAdventurerMatch 제거 + 던전 등급 칩 도입

### 9.1 배경

WeaponAdventurerMatch (`baseRate + statEffectBonus`) 칩은 *베이스 성공률 자체* 라서:
- 값 범위 0.3 ~ 0.9 로 다른 칩 (±0.30) 과 *단위가 다름*
- E 풀에 합산하면 이 칩 하나가 모든 합을 지배
- 노출 조건이 *통찰 80↑ 또는 (스탯 3 + 방어 공개)* 라서 *후반에만 보임* → 두 모드 분기 코드 발생

→ 이 칩은 chip 풀에서 **제거**.

→ 그 빈자리에 *항상 노출되는 baseline chip* 이 필요. 이유: 초반 모험 (호감도 없는 첫 방문자, 정찰 X, 통찰 X) 에서 chip 이 *전혀 없는* 상태가 자주 발생 → mood label 이 "담담" 으로만 수렴 → 평가의 척도가 사라짐.

### 9.2 코드 재확인 결과 — 등급차 vs 던전 등급

`weaponGradeDiff = (int)weapon.currentGrade − (int)dungeon.grade` 의 사용처:

- AdventureManager.Calculations.cs:660 (`Assets/_Projects/Scripts/Systems/AdventureManager/AdventureManager.Calculations.cs`) — `CalculateDeathRate` 에서만 사용 (사망률 감소).
- `CalculateSuccessRate` 에는 **일절 등장하지 않음**.

→ *등급차 칩* 은 폐기.

다만 `dungeon.grade` 자체는 *성공률에 간접 영향*:
- `baseStatThreshold` 가 등급별로 다름 (Common 110~130, Uncommon 140~155, Rare 180, Epic 230, Legendary 280)
- → effectBase = effectStatScore / baseStatThreshold. 등급 높을수록 같은 스탯으로 성공률 낮음.
- 즉 던전 등급 = 던전 난이도. 플레이어에게 *항상 공개* 됨.

### 9.3 던전 등급 칩 도입

**의미**: "이 던전의 분위기" 의 단순 척도 chip. 등급별 양/음 칸으로 분위기 균형추 역할.

**배경**: 다른 confirmed chip 들이 대부분 양수 (Affection, Charm, Collection, WeaponCondition) 라서 E 풀이 양수 권역에 쏠림 → 라벨이 양수 권역에 머무름. 던전 등급 chip 으로 음수 균형 도입.

- 새 BonusType: `DungeonGrade`
- 5 칸 fill (방향 포함):

| 던전 등급 | 칸 패턴 | value (unit=0.03) |
|---|---|---|
| Common (grade=0) | **+5 칸** (양수) | +0.15 |
| Uncommon (grade=1) | +2 칸 | +0.06 |
| Rare (grade=2) | -1 칸 (음수) | -0.03 |
| Epic (grade=3) | -3 칸 | -0.09 |
| Legendary (grade=4) | **-5 칸** | -0.15 |

- `maxAbsValue = 0.15` (= 5 × unit)
- `isConfirmed = true`
- 양수 칩은 positive color, 음수 칩은 negative color (시각적 분위기 차이)
- 의미: 던전 등급은 *난이도 신호*. 성공률에 직접 영향은 안 주지만 `baseStatThreshold` 를 통해 간접 상관 + 플레이어가 항상 아는 정보.

### 9.4 값 매핑 강도 — α 확정

칩 value 가 E 풀에 어떻게 기여하는지 — `unit` 선택이 라벨 분포를 좌우. 세 가지 옵션 비교:

| 옵션 | unit | Common | Uncommon | Rare | Epic | Legendary | 던전 등급만의 라벨 |
|---|---|---|---|---|---|---|---|
| **α** ✅ | 0.03 | +0.15 | +0.06 | -0.03 | -0.09 | -0.15 | **환희 / 희망 / 담담 / 불안 / 암울** |
| β | 0.02 | +0.10 | +0.04 | -0.02 | -0.06 | -0.10 | 희망 / 담담 / 담담 / 불안 / 불안 |
| γ | 0.01 | +0.05 | +0.02 | -0.01 | -0.03 | -0.05 | 희망경계 / 담담 / 담담 / 담담 / 불안경계 |

→ **옵션 α 확정** (2026-05-25). 던전 등급 5 단계가 *그대로* mood band 5 단계에 매핑됨 — 다른 chip 이 거기서 라벨을 반전·강화시키는 자연스러운 구조.

### 9.5 동적 임계값 (Dynamic V threshold)

#### 9.5.1 정적 vs 동적 비교

**정적**: T_low = 0.03 고정. 표시 정보 양과 무관.
- 장점: 단순. 절대적 의미가 명확.
- 단점: 초반 (작은 chip 만 노출) 에 V 가 0.05 정도여도 의미가 큰데, 임계 0.03 만 넘으면 일률적으로 동일 처리.

**동적**: T_low 가 *현재 표시된 chip 절댓값 합* 의 비율로 정의.
- 장점: *공개된 정보 안에서의 상대적 평가*. 초반에도 작은 V 가 의미있게 분류됨.
- 단점: 같은 V 값이라도 다른 chip 노출 상태에 따라 V_state 가 달라짐 — 라벨이 *현재 정보 상태 한정* 임.

→ 정보 진척에 따라 라벨이 재계산, 정찰·통찰 동기 강화. **동적 채택.**

#### 9.5.2 동적 임계 식

```
total_magnitude = Σ |value| for all cards shown
                = E_abs + V       (E_abs = Σ |value| where isConfirmed=true)

T_low_ratio  = 0.10     # 전체 강도의 10% 미만은 "변동 없음"
T_lean       = 0.7      # (그대로)

T_low = max(0.02, total_magnitude * T_low_ratio)    # 너무 작아지지 않도록 하한 둠
```

보이는 정보의 강도가 작으면 작은 V 도 의미있게, 강도가 크면 작은 V 는 무시.

### 9.6 확정 사항 (2026-05-25)

1. **던전 등급 칩 도입** — **확정**. `BonusType.DungeonGrade`. 칸 패턴 `+5/+2/-1/-3/-5` (Common→Legendary). 양/음 양쪽 색 사용. isConfirmed=true.
2. **값 매핑 강도** — **옵션 α 확정** (`unit = 0.03`, `maxAbsValue = 0.15`). 던전 등급 5 단계가 mood band 5 단계와 직접 매핑.
3. **동적 임계 도입** — **확정**. `T_low = max(0.02, total_magnitude × 0.10)`.
4. **미확정 시뮬레이션 처리** — **(a) 확정**. 시뮬레이션 값 그대로 한쪽 분류.
5. **WeaponAdventurerMatch 칩 제거** — **확정**. AdventurePreparationController.cs:660-666 (`Assets/_Projects/Scripts/UI/Controllers/AdventurerInteraction/AdventurePreparationController.cs`) 의 8 번 분기 삭제.

---

## 10. 기본 성공률 (baseRate) 구성 — 참고

`AdventureManager.CalculateSuccessRate` 의 흐름 (Calculations.cs:627-653 (`Assets/_Projects/Scripts/Systems/AdventureManager/AdventureManager.Calculations.cs`)):

```
effectBase = (스탯 + Stat효과들) / dungeon.baseStatThreshold      ← (A) 스탯 적합도
baseRate   = effectBase × (1 + armorBonus + charmRate)             ← (A)×(B+C)

final      = baseRate + Σ(추가 보너스들) × traitSuccessMultiplier
                ├ conditionBonus  (DungeonGradeBonus + ArmorTypeBonus)
                ├ collectionBonus
                ├ affectionBonus
                └ traitSuccessBonus
```

baseRate 한 덩어리에 영향을 주는 요소:

| # | 요소 | 출처 | 범위 | chip 노출 |
|---|---|---|---|---|
| A1 | 모험가 스탯 × 무기 타입별 가중치 | `TypeAdvantage.weaponStatMultipliers[8,4]` + AdventurerData | 0~1 (정규화 후) | ❌ 제거됨 |
| A2 | 무기 효과 — StatBonus / AllStatBonus / WeaponTypeMatchBonus | weapon.effects | 가변 | ❌ 제거됨 |
| A3 | 던전 baseStatThreshold (난이도 분모) | dungeon asset (110~280) | 분모 | ❌ |
| B | 무기-방어 상성 (armorBonus) | `TypeAdvantage.weaponArmorBonus[8,4]` | -0.30 ~ +0.40 | ✅ **DungeonArmor chip** |
| C | 부적 (charmRate) | ActiveItem Charm.effectValue | 0 / +0.15 / +0.20 | ✅ **Charm chip** |

→ A1+A2+A3 (= effectBase) 한 덩어리가 WeaponAdventurerMatch chip 에 담겨 있었던 부분. **제거 확정.**
→ B, C 는 별도 chip 으로 *±0.30 이하 단위* 에 적정하게 들어가 있어 **유지**.

이로써 모든 chip 이 ±0.30 단위로 통일됨 → 동적 임계가 정상 작동.

---

## 11. 코드 변경 계획서

### 11.1 변경 요약 (5 가지 작업)

1. **통합바 시스템 제거** — UI/스크립트/프리팹 전부.
2. **DungeonGrade chip 도입** — 새 BonusType 추가, controller 에서 chip 생성.
3. **WeaponAdventurerMatch chip 제거** — controller `BuildAdventureInfoCards` 분기 삭제.
4. **칩 segmentCount 5 고정** — `BonusVisualInfo.segmentCount` 필드 제거, 전역 5 로 통일.
5. **정성적 표지 (Mood Label) 도입** — 통합바 자리에 라벨 UI + 계산 로직.

### 11.2 파일별 변경

#### A. `Scripts/Data/Config/AdventureInfoConfig.cs` — **수정**

추가:
```csharp
public enum BonusType {
    Affection, Charm, Trait, DungeonArmor, WeaponCondition, Collection, Seer,
    DungeonGrade,                  // ← 신규 추가
    // WeaponAdventurerMatch  ← 제거
}

public enum MoodBand { VeryDark, Dark, Neutral, Bright, VeryBright }
public enum VarianceState { None, Positive, Negative, Mixed }

[Serializable]
public class MoodLabelEntry {
    public MoodBand band;
    public VarianceState state;
    public string label;
    public Color color;
}
```

`BonusVisualInfo` 수정:
```csharp
public class BonusVisualInfo {
    public BonusType bonusType;
    public string displayName;
    public Sprite icon;
    public Color positiveColor = Color.cyan;
    public Color negativeColor = Color.red;
    // [Range(3,15)] public int segmentCount = 5;  ← 제거 (전역 5 고정)
    public float maxAbsValue = 0.15f;
}
```

`AdventureInfoConfig` 클래스에 추가:
```csharp
[Header("전역 칩 설정")]
public int segmentCount = 5;

[Header("Mood Label - Band")]
public float bandThresholdVeryDark   = -0.15f;
public float bandThresholdDark       = -0.05f;
public float bandThresholdBright     = +0.05f;
public float bandThresholdVeryBright = +0.15f;

[Header("Mood Label - Variance")]
public float varianceLowRatio       = 0.10f;
public float varianceLowFloor       = 0.02f;
public float varianceLeanThreshold  = 0.7f;

[Header("Mood Label - 라벨 사전 (5 × 4 = 20)")]
public List<MoodLabelEntry> moodLabels;

[Header("DungeonGrade chip")]
public float dungeonGradeUnit = 0.03f;     // 칸당 value
```

제거:
- `int GetTotalSegmentCount()` (호출처 없음, 데드 코드)

#### B. `Scripts/UI/Controllers/AdventurerInteraction/AdventurePreparationController.cs` — **수정**

AdventurePreparationController.cs:612-668 (`Assets/_Projects/Scripts/UI/Controllers/AdventurerInteraction/AdventurePreparationController.cs`) `BuildAdventureInfoCards`:

- **추가**: 메서드 진입 직후, 던전 선택 여부 무관하게 (selectedDungeon != null 일 때만) DungeonGrade chip 추가:
  ```csharp
  if (selectedDungeon != null) {
      float gradeValue = (5 - (int)selectedDungeon.grade × 2) × cfg.dungeonGradeUnit;
      // Common=+5×0.03=+0.15, Uncommon=+2×0.03=+0.06, Rare=-1×0.03=-0.03, Epic=-3×0.03=-0.09, Legendary=-5×0.03=-0.15
      cards.Add(new AdventureInfoCardData {
          type = BonusType.DungeonGrade,
          value = gradeValue,
          isConfirmed = true
      });
  }
  ```
  
  ※ 5/2/-1/-3/-5 매핑은 `int[] dungeonGradeSegments = { 5, 2, -1, -3, -5 }` 같은 룩업 배열로 깔끔하게 처리.

- **제거**: 8 번 분기 (WeaponAdventurerMatch) 통째로 삭제 — 663~666 줄.

#### C. `Scripts/UI/Views/AdventurerInteraction/PreparationTab3Panel.cs` — **수정**

**제거**:
- `[SerializeField] Transform unifiedBarContainer`
- `[SerializeField] GameObject unifiedBarSegmentPrefab`
- `List<UnifiedBarSegment> unifiedSegments`
- `float _cachedUnifiedBarWidth, _cachedSegmentBaseWidth`
- `ApplyUnifiedBarColors()` 메서드 전체
- `UpdateAdventureInfo()` 안에서 `ApplyUnifiedBarColors()` 호출
- `ResetAdventureInfo()` 안에서 `ApplyUnifiedBarColors(null)` 호출

**추가**:
- `[SerializeField] TextMeshProUGUI moodLabelText`
- `[SerializeField] Image moodLabelBackground` (선택)
- `void UpdateMoodLabel(List<AdventureInfoCardData> cards)` — 계산 후 텍스트/색 갱신
- `ResetAdventureInfo()` 와 `UpdateAdventureInfo()` 에서 UpdateMoodLabel 호출

#### D. `Scripts/UI/ListItems/AdventureInfoCardItem.cs` — **수정**

`BuildSegments(BonusVisualInfo info, float value)`:
- segmentCount 를 인자로 받지 말고 `ConfigManager.Instance.AdventureInfo.segmentCount` 사용 (전역 5)
- `info.segmentCount` 참조 모두 → 전역 5
- 음수 value 일 때 `negativeColor` 사용 (이미 그렇게 되어 있음)

#### E. `Scripts/Systems/Adventure/MoodLabelCalculator.cs` (신규) — **신규**

계산 로직 분리:
```csharp
namespace TodaysWeaponRental
{
    public static class MoodLabelCalculator
    {
        public struct Result {
            public MoodBand band;
            public VarianceState state;
            public string label;
            public Color color;
        }

        public static Result Calculate(List<AdventureInfoCardData> cards, AdventureInfoConfig cfg)
        {
            // 1. E, V_plus, V_minus 계산
            // 2. total_magnitude, T_low 계산
            // 3. MoodBand 분류
            // 4. VarianceState 분류
            // 5. cfg.moodLabels 에서 매칭 entry 찾음 → label, color 리턴
        }
    }
}
```

PreparationTab3Panel 의 `UpdateMoodLabel` 이 이 static 메서드 호출.

#### F. `Scripts/UI/ListItems/UnifiedBarSegment.cs` — **삭제**

스크립트 + `.meta` 파일.

### 11.3 Asset / Prefab 변경 (인스펙터 작업)

#### G. `Assets/_Projects/Data/Config/AdventureInfoConfig.asset` — **수정 필요**

1. `bonusVisuals` 리스트:
   - 기존 8 개 항목에서 `segmentCount` 필드 자동 사라짐 (코드 변경 후)
   - **추가 항목**: `DungeonGrade`
     - displayName: "던전 등급"
     - icon: 새 스프라이트 또는 기존 던전 아이콘 재활용
     - positiveColor: 파랑 계열 (Common~Uncommon 표시)
     - negativeColor: 빨강 계열 (Epic~Legendary 표시)
     - maxAbsValue: **0.15**
   - **제거 항목**: `WeaponAdventurerMatch` (있다면)

2. 새 전역 필드:
   - `segmentCount = 5`
   - `bandThreshold*` 4 개 값
   - `varianceLowRatio = 0.10`, `varianceLowFloor = 0.02`, `varianceLeanThreshold = 0.7`
   - `dungeonGradeUnit = 0.03`

3. `moodLabels` 리스트 — 20 entry (Band × State 조합, 단어 + 색):

| Band \ State | None | Positive | Negative | Mixed |
|---|---|---|---|---|
| VeryBright | 환희 | 의기양양 | 자만 | 흥분 |
| Bright | 희망 | 설렘 | 조마조마 | 두근거림 |
| Neutral | 담담 | 호기심 | 부담 | 안절부절 |
| Dark | 불안 | 기대 | 답답 | 갑갑함 |
| VeryDark | 암울 | 미련 | 절망 | 싱숭생숭 |

→ 디자이너가 인스펙터에서 단어 변경 / 색상 변경 가능.

#### H. `Assets/_Projects/Prefabs/UI/PanelsInGame/AdventurePreparationView.prefab` — **수정 필요**

PreparationTab3 영역에서:
1. **제거**: unifiedBarContainer, unifiedBarSegmentPrefab 슬롯 (위에 매달려 있는 segment 들 + container) — 비주얼적으로도 삭제
2. **추가**: moodLabelText (TextMeshProUGUI), 필요 시 moodLabelBackground (Image) 슬롯 — 통합바가 있던 자리 또는 별도 위치
3. PreparationTab3Panel 인스펙터에서 새 필드 (moodLabelText, moodLabelBackground) 연결

#### I. 삭제 대상 프리팹

- `Assets/_Projects/Prefabs/UI/ListItem/AdventurerInteraction/UnifiedSegment.prefab` (+ `.meta`)

#### J. `Assets/_Projects/Prefabs/UI/ListItem/AdventurerInteraction/AdventureInfoCardItem.prefab` — **확인만**

- segmentContainer 자식이 5 개 슬롯 기준으로 동작하는지 확인. 현재 동적 생성이므로 무관할 가능성 큼.

### 11.4 인스펙터 작업 체크리스트

코드 변경 후 Unity Editor 에서 해야 할 작업:

- [ ] `AdventureInfoConfig.asset` 의 `bonusVisuals` 에 `DungeonGrade` 항목 추가 (8 → 9 개)
- [ ] `AdventureInfoConfig.asset` 의 새 전역 필드 값 설정 (bandThresholds, varianceLow*, dungeonGradeUnit, segmentCount)
- [ ] `AdventureInfoConfig.asset` 의 `moodLabels` 리스트에 20 entry 추가 (위 표대로)
- [ ] (옵션) WeaponAdventurerMatch 항목이 `bonusVisuals` 에 있다면 제거
- [ ] `AdventurePreparationView.prefab` 의 PreparationTab3 자식 객체들에서 unifiedBar 관련 GameObject 삭제
- [ ] 같은 prefab 에 moodLabel TMP 추가 및 인스펙터 연결
- [ ] `PreparationTab3Panel` 컴포넌트의 `unifiedBarContainer`, `unifiedBarSegmentPrefab` 슬롯이 자동으로 사라지는지 확인
- [ ] `UnifiedSegment.prefab` 파일 삭제

### 11.5 검증 절차

코드 + 인스펙터 작업 완료 후:

1. **컴파일 통과**: Unity Console 에 에러 없음.
2. **InGameScene 실행**, 모험가 클릭 → AdventurePreparationView 열림.
3. **던전별 chip 확인**:
   - Common 던전 선택: DungeonGrade chip 이 5칸 파랑으로 표시
   - Legendary 던전 선택: DungeonGrade chip 이 5칸 빨강으로 표시
   - WeaponAdventurerMatch chip 이 표시되지 않음 (통찰 풀이어도)
4. **통합바 없음**: 통합바 시각 요소가 화면에 없음.
5. **Mood label 표시**: 던전 선택 시 mood 라벨이 표시되고, 다음 조작에서 즉시 갱신:
   - 다른 던전 선택 → 라벨 변경
   - 점술 사용 → 라벨 변경
   - 정찰 사용 → DungeonArmor 가 V→E 풀로 이동, 라벨 변경
6. **시나리오 일치**: §8.6 의 A1, A5, B2, D1, E1 케이스를 직접 재현해서 예상 라벨과 일치 확인.
7. **인스펙터 노브 동작**: bandThreshold 값을 바꿔 라벨 분포가 변하는지 확인.

### 11.6 변경 영향 범위 및 리스크

- **무관 시스템**: SaveData, GameData, 다른 매니저 (AdventureManager, EconomyManager 등) 에 영향 없음. UI 표시 계층만 수정.
- **세이브 호환성**: `GameData` 필드 추가/제거 없음 → 기존 세이브 그대로 로드 가능.
- **하위 호환**: `BonusType.WeaponAdventurerMatch` enum 값 제거 시 다른 곳 참조 확인 필요. `BonusVisualInfo.segmentCount` 필드 제거 시 직렬화된 asset 에서 값 손실됨 (segmentCount 가 5 와 다르게 셋팅된 경우만 영향).
- **리스크**:
  1. AdventureInfoConfig.asset 이 segmentCount 가 *5 가 아닌 다른 값* 으로 설정되어 있었다면 동작 변경. → 작업 전 asset 확인 필요.
  2. PreparationTab3Panel 의 인스펙터 슬롯 변경 → prefab 의 새 슬롯 연결 누락 시 NullReference. → 11.4 체크리스트로 방어.

### 11.7 작업 순서 권장

1. AdventureInfoConfig.cs 수정 → 컴파일 통과 확인
2. AdventureInfoConfig.asset 인스펙터 작업 (DungeonGrade 항목, 전역 필드, moodLabels 리스트)
3. MoodLabelCalculator.cs 신규 작성
4. AdventurePreparationController.cs 수정 (chip 생성 분기)
5. AdventureInfoCardItem.cs 수정 (segmentCount 전역화)
6. PreparationTab3Panel.cs 수정 (통합바 제거 + mood label 추가)
7. AdventurePreparationView.prefab 인스펙터 작업 (UI 제거/추가)
8. UnifiedBarSegment.cs + UnifiedSegment.prefab 삭제
9. InGameScene 실행 검증

---

## Related

- [[Completed]]
