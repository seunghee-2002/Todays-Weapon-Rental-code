---
tags:
  - Development
  - MOC
aliases:
  - 개발 레퍼런스
  - 프로젝트
  - 프로젝트 구조
created: 2026-03-16
updated: 2026-08-01
---

# Development

> [!abstract] 한 줄 요약
> **"어떻게 만들어졌나"**를 다루는 아키텍처 레퍼런스. 게임 규칙·확률·기획은 [[Systems]]가 다룬다.

## 목차

- [[아키텍처]] — 4계층 구조 · 5가지 패턴 · 폴더 구조 · 매니저 목록
- [[데이터_구조]] — GameData / PlayerData · 정적 SO · 런타임 인스턴스 · Enum · Config
- [[데이터_접근_원칙]] — RuntimeInstance <-> SO · DataManager 책임 경계 **(아키텍처 원칙, 필독)**
- [[UI_구조]] — UIManager · View/Controller 쌍 · ListItem
- [[다국어_도입전략]] — Unity Localization 도입 전략 · 6 Phase 로드맵 · 폰트/키 설계
- [[다국어_진행상황]] — **번역 작업 대장** (어느 화면에 뭐가 남았나 · Phase별 상태)

---

## 게임 개요

| 항목 | 값 |
|---|---|
| 게임 이름 | `Today's Weapon Rental` / 한글 `오늘도 장비 대여` |
| 장르 | 길드 경영 시뮬레이션 |
| 엔진 | Unity 2022.3.62f3 |
| 네임스페이스 | `TodaysWeaponRental` |
| 씬 | `MainMenuScene`, `InGameScene` |

> 모험가에게 모험을 주선하고 무기를 대여해 수수료를 받으며, 던전 모험 결과를 관리하고 평판을 쌓는 게임.

> [!warning] `Magic Rental Shop`은 예전 이름이다
> `MagicRentalShop`, 한글 `오늘의 무기 대여점` 포함. **새 코드/문서에 쓰지 않는다.**
> 아래 세 곳에만 의도적으로 남아 있으며, 전부 Unity나 외부 도구가 덮어쓰는 값이라 손대도 되돌아온다.
> - 저장소 폴더명 `Magic-Rental-Shop/`
> - `Magic-Rental-Shop.sln` / `.slnx` — Unity가 폴더명 기준으로 재생성
> - `ProjectSettings.asset`의 `projectName` — UGS 클라우드 프로젝트 이름 캐시

---

## 새 기능 추가 체크리스트

- [ ] `BaseManager<T>` 상속 -> `Scripts/Systems/`에 추가
- [ ] UI가 필요하면 `BaseView` + `BaseController<TView>` -> `Scripts/UI/`에 추가
- [ ] 새 데이터 타입 -> `GameData`(한 회차) 또는 `PlayerData`(영구)에 필드 추가 + `SaveManager` 호환 확인
- [ ] `GameManager.InitializeManagers()`에 초기화 코드 추가
- [ ] 밸런싱 수치는 **Config SO로 분리** -> [[Balance]]
- [ ] `Scripts/Data/StaticData/`를 건드렸다면 **같은 작업에서** CSV Importer(`Scripts/Editor/CSV*.cs`)도 수정

> [!danger] StaticData(SO)와 CSV Importer는 반드시 짝을 맞춘다
> SO 필드명·타입만 바꾸고 CSV 코드를 안 바꾸면 **빌드는 통과해도 컬럼 인덱스가 밀려 데이터가 무음으로 깨진다.**
> 수정 후 `Tools > Today's Weapon Rental > CSV Tool`에서 Export -> 기존 CSV와 diff를 한 번 돌려 검증한다.

---

## 외부 라이브러리

| 라이브러리 | 용도 |
|---|---|
| **DOTween** | 애니메이션 트윈 |
| **TextMesh Pro** | UI 텍스트 |
| **Spine** | 2D 스켈레탈 애니메이션 |
| **Layer Lab 2D Art Maker** | 캐릭터 외형 생성 -> [[기타]] |
| **Cartoon FX Remaster (JMO Assets)** | 파티클 이펙트 |
| **Unity Gaming Services** | Authentication · Cloud Save · Cloud Code · Leaderboards · Analytics -> [[저장]] · [[온라인]] · [[Analytics]] |

> [!note] TextMeshPro 폰트 지원 범위
> 인게임에 표시되는 문자열은 게임 폰트가 지원하는 유니코드 범위 안의 문자만 쓴다: `32~126`(ASCII), `44032~55203`(한글 가~힣), `12593~12643`(한글 자모 ㄱ~ㅣ).
> 범위 밖 문자(em 대시, 말줄임표, 화살표, 둥근 따옴표, 가운뎃점, 곱셈기호, 이모지 등)는 두부(□)로 깨진다.
> **이 규칙은 런타임 TMP 문자열에만 적용된다** — 코드 주석·로그·이 문서 같은 `.md`에는 제한이 없다.
>
> **다국어 예외:** 위 범위는 **코드/프리팹의 한국어 원문**에만 적용한다. Localization String Table의 번역 텍스트(en/ja/zh-Hans)는 해당 로케일 폰트 아틀라스가 커버하는 범위를 허용하되, 릴리즈 전 번역 텍스트 기반으로 아틀라스를 재생성한다 -> [[다국어_도입전략]] 10절.

---

## 외부 링크

- [게임 소개 페이지](https://seunghee-2002.github.io/todays-weapon-rental/)
- [이용약관](https://seunghee-2002.github.io/privacy-policy/todays-weapon-rental/terms/)
- [개인정보처리방침](https://seunghee-2002.github.io/privacy-policy/todays-weapon-rental/privacy/)

> 코드 내 사용처: `DataCollectionController.cs` 상단 상수 (`TermsUrl`, `PrivacyUrl`)

---

## Related

- [[Home]] — Vault 전체 지도
- [[Systems]] — 기능·기획 레퍼런스 ("무엇이 있고 어떤 규칙으로 도는가")
- [[Balance]] — 밸런싱 기준과 변경 이력
- [[Analytics]] — 유저 행동 로그 수집
- [[데드코드_정리]] — 미사용 코드 분석 결과
