---
tags:
  - Development
  - UI
aliases:
  - UI 구조
  - UIManager
  - View Controller
created: 2026-03-16
updated: 2026-08-01
---

# UI 구조

> [!abstract] 한 줄 요약
> `UIManager`가 패널 **인스턴스 생명주기**를, `UIControllerManager`가 Controller **lookup**을 담당한다. 비슷해 보이지만 책임이 달라 합치지 않는다.

## 목차

- [UIManager](#uimanager)
- [UIManager vs UIControllerManager](#uimanager-vs-uicontrollermanager)
- [주요 View / Controller 쌍](#주요-view--controller-쌍)
- [대화창 표시 방식](#대화창-표시-방식)
- [ListItem 컴포넌트](#listitem-컴포넌트)
- [새 View / Controller 추가 절차](#새-view--controller-추가-절차)

---

## UIManager

`Dictionary<Type, BaseView>` + `Stack<BaseView>`로 패널을 관리한다.

```csharp
UIManager.Instance.OpenPanel<WeaponShopView>();
UIManager.Instance.ClosePanel<WeaponShopView>();
UIManager.Instance.GetPanel<InventoryView>();
```

부가 책임:

- prefab 레지스트리 · 인스턴스 캐시 · 열린 패널 스택
- 시간 일시정지 / 공통 UI 숨김 같은 사이드 이펙트
- **Analytics `panel_opened` / `panel_closed` 중앙 발행** (화이트리스트 기반) -> [[Analytics_구현_메모]]

---

## UIManager vs UIControllerManager

> [!important] 책임이 달라 합치지 않는다
> 두 매니저는 비슷한 레지스트리 패턴(`Dictionary<Type, X>`)을 쓰지만 역할이 다르다.

| 매니저 | 책임 |
|---|---|
| **`UIManager`** | 패널 **인스턴스 라이프사이클**. prefab 레지스트리, 인스턴스 캐시, 열린 패널 스택, 시간 일시정지/공통 UI 숨김 |
| **`UIControllerManager`** | Controller 인스턴스 **lookup**. `BaseController.Awake`/`OnDestroy`에서 자동 등록/해제. 다른 Controller나 Manager가 특정 Controller를 가져올 때 사용 |

---

## 주요 View / Controller 쌍

| 기능 | View | Controller | 기획 문서 |
|---|---|---|---|
| 상단 HUD | `TopBarView` | — | [[시간]] · [[평판]] |
| 모험가 상호작용 | `AdventureDialogueView`, `AdventurePreparationView`, `WeaponSelectionView` | `AdventurerInteractionController` 계열 | [[모험가_UI]] |
| 모험 진행 | `AdventureProgressView`, `AdventureResultView` | `AdventureProgressController` | [[모험_UI]] |
| 무기점 | `WeaponShopView` | `WeaponShopController` | [[무기점_UI]] |
| 인벤토리 | `InventoryView`, `WeaponDetailPopup`, `MaterialDetailPopup` | `InventoryController` | [[인벤토리_UI]] |
| 대장장이 | `BlacksmithView` + 탭별 View (Craft/Enforce/Evolve/Reroll/Disassemble) | `BlacksmithController` | [[무기_UI]] |
| 퀘스트 | `QuestView`, `QuestDetailView`, `QuestResultView` | `QuestController` | [[퀘스트_UI]] |
| 퀘스트 보드 | `QuestBoardView`, `DungeonDetailPopup` | `QuestBoardController` | [[퀘스트_UI]] |
| 대화 | `SpineDialogueView` | `SpineDialogueController` | [[대화]] |
| 유산 업그레이드 | `LegacyUpgradeView` | `LegacyUpgradeController` | [[유산_UI]] |
| 아침 이벤트 | `MorningEventViewBase` 9종 | 이벤트별 Controller | [[아침이벤트_UI]] |
| 공통 팝업 | `ConfirmPopupView`, `ToastView`, `OptionPopupView` | — | |

---

## 대화창 표시 방식

> [!note] 노드 대화는 전부 `SpineDialogueView`로 연다
> `DialogueManager`는 모든 노드 대화에 **`SpineDialogueView`**를 연다. 이 View는 씬의 `VisitorNPC` spine을 보여준다 — **다른 방문자 페이드 + sorting order 상향**(대화 종료 시 원복).
>
> - 강조할 NPC는 `StartDialogue(..., spineTarget:)`으로 전달한다.
> - `null`이면(시스템 대화) 페이드 없이 텍스트만 표시한다.
> - 모험가 전용 `AdventureDialogueView`는 이 흐름과 **무관하다(불변)**.

> [!important] 카메라 줌은 대화 단위가 아니라 **상호작용 단위**다
> `VisitorNPC.StartInteraction` -> 줌인 / `EndInteraction` -> 줌아웃 (모험가 제외).
> 따라서 시작대화 -> 패널 -> 종료대화로 이어지는 상호작용 내내 줌이 유지되고 **종료 시 한 번만** 줌아웃된다.
>
> -> [[대화]] · [[기타]]

---

## ListItem 컴포넌트

위치: `UI/ListItems/` · 재사용 가능한 카드/리스트 아이템. 인스턴스 데이터를 받아 자신을 초기화한다.

| 도메인 | 컴포넌트 |
|---|---|
| 무기 | `WeaponInventoryCardItem`, `WeaponSelectionItem`, `WeaponShopCardItem` |
| 모험가 | `AdventurerButton`, `SideVisitorButton` |
| 재료 | `MaterialInventoryCardItem`, `MaterialDropItem` |
| 던전 | `DungeonChoiceCardItem`, `QuestBoardDungeonSlot` |
| 액티브 아이템 | `ActiveItemInventoryCard`, `ActiveItemSelectionItem` |
| 무기 효과 | `WeaponEffectListItem`, `RerollEffectItem` |

---

## 새 View / Controller 추가 절차

- [ ] 1. **표시할 UI 요소 목록을 먼저 작성해 리뷰를 요청한다** (데이터 항목 / 레이아웃 구조 / 입력 요소 / 조건부 표시 규칙)
- [ ] 2. 승인 후에 코딩을 시작한다. 승인 없이 UI 요소를 임의로 추가·삭제하지 않는다.
- [ ] 3. `UIManager` 인스펙터의 `panelPrefabs` 배열에 항목 추가
	- `viewTypeName` — View 클래스명 문자열 (예: `"InventoryView"`)
	- `prefab` — 패널 프리팹
- [ ] 4. Analytics 대상이면 `UIManager.analyticsPanelNames` 화이트리스트에 등록 -> [[이벤트_스펙]]

> [!tip] Controller 등록은 자동이다
> `BaseController.Awake`에서 `UIControllerManager`에 자동 등록되므로 코드/인스펙터 변경이 필요 없다.

---

## Related

- [[Development]] — 개발 레퍼런스 허브
- [[아키텍처]] — 계층 호출 규칙
- [[데이터_접근_원칙]] — View가 만질 수 있는 데이터
- [[Analytics]] — 패널·버튼 이벤트 발행
- [[Systems]] — 각 화면의 기획 문서(`{도메인}_UI`)
