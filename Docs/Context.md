# 프로젝트 컨텍스트

마지막 갱신: 2026-11-12. 경로는 저장소 루트 기준이다. 현재 실행 가능한 것, 코드·씬·에셋 지도, 개발 방법과 검증 근거를 이 문서에서 관리한다. 완성 목표·다음 작업·진행 상태는 [구현 로드맵](ImplementationPlan.md), 게임 규칙은 [게임 기획](GameDesign.md), 에이전트의 필수 규칙과 문서 입구는 [AGENTS.md](../AGENTS.md)가 기준이다.

## 찾아보기

- [1. 현재 실행과 확인 방법](#1-현재-실행과-확인-방법): 바로 열어볼 씬과 조작
- [2. 코드·씬·에셋 지도](#2-코드씬에셋-지도): 실제 구현 위치와 연결 범위
- [3. 협업과 통합](#3-협업과-통합): 협업 시작 후 적용할 운영 기준
- [4. 코드와 검증 원칙](#4-코드와-검증-원칙)
- [5. 아트 교체 대비](#5-아트-교체-대비)
- [6. Unity CLI 작업 방법](#6-unity-cli-작업-방법)
- [7. 과거 검증 기록](#7-과거-검증-기록): 필요할 때만 펼쳐 보는 완료 결과와 성능 측정

## 1. 현재 실행과 확인 방법

Unity 6000.6.8f1, URP, 3D 1인칭 프로젝트다. 현재는 임시 그래픽이며 Synty 에셋으로 교체할 예정이다.

| 씬 경로 | 확인할 것과 조작 |
|---|---|
| `Assets/_Project/Scenes/ConstructionPrototype.unity` | 현재 빌드 첫 씬. Q/E 시점 회전, 성벽·계단 선택, 격자 반투명 미리보기, 좌클릭 설치·우클릭 취소·R 조각 회전. 성벽 위 이동과 발밑 조각 파괴로 낙하 시험. 성벽 위 대포 설치와 받침 성벽 파괴 시 동반 파괴 시험 |
| `Assets/_Project/Scenes/CombatPrototype.unity` | WASD 이동, 마우스 시점, 좌클릭 근접 공격. 허수아비·체력·카메라 시험. Esc로 커서 해제 |
| `Assets/_Project/Scenes/UnitBattlePrototype.unity` | 아군 10명과 적 10명의 근접·원거리 자동 전투 관전 |
| `Assets/_Project/Scenes/SquadCommandPrototype.unity` | 플레이어 직접 조작 + 부대 3개 지휘. F1~F3 부대 선택, ` 전체 선택, 1 이동(화면 가운데가 가리키는 곳)·2 공격·3 중지. 왼쪽 위에 선택·지시·남은 인원 표시 |
| `Assets/_Project/Scenes/WallCommandPrototype.unity` | 건설·성벽 위 부대 지휘 통합. Tab 건설↔1인칭 시점, F1~F3 선택·1 이동·2 공격·3 중지. 건설 화면의 `성벽 위로 이동`은 1번 부대를 이동시키고 `발밑 조각 파괴`는 그 부대 첫 유닛의 받침을 파괴 |
| `Assets/_Project/Scenes/SiegePrototype.unity` | 수성전 프로토타입. 준비 단계(건설·철거·부대 바로 배치, 임시 코스트) → `전투 시작` → 적 침공(뚫린 곳·사다리·성문 파괴·궁수) → 승패. 준비 중 Tab으로 건설↔1인칭, 전투 중에는 1인칭 고정. F를 누르고 있으면 가까운 사다리를 민다. 결과가 나오면 R로 다시 시작 |

| `Assets/_Project/Scenes/Title.unity` | 실제 게임의 진입 씬(G-0). `새 게임`을 누르면 게임 씬으로 가고 `종료`는 Play를 끝낸다 |
| `Assets/_Project/Scenes/Game.unity` | 실제 게임 씬(G-0·G-1). 지면 240×300m, 성 96×96m, 안뜰 90×90m와 임시 시설 다섯 곳. 중클릭 이동·휠 확대축소·Home 카메라 초기화. 준비 단계는 1인칭 WASD 이동. 건설소에 가까이 가서 바라보고 F → 건설 직교 시점으로 성벽·계단·대포 배치와 Q/E 회전, Esc로 1인칭 복귀. 성문 앞 붉은 깃발을 바라보고 F로 전투 시작 → 북쪽 들판에서 적 28명이 접근하며 1인칭 직접 조작과 F1~F3·1·2·3 부대 지휘. 적 전멸/퇴각 또는 패배 조건으로 자동 결과 → `보상 받고 다음 스테이지` 또는 `타이틀로`. 이 씬만 열고 Play해도 1스테이지 준비로 시작한다 |

에디터에서 씬을 열고 Play를 누른다. 종료 후 다시 Play하면 초기 상태로 돌아온다. `*Prototype` 씬은 모두 기반 기능을 확인하는 데모이고, 실제 게임은 `Title`·`Game` 두 씬으로 만든다. 지금 `Game` 씬은 건설소 방문·전투 시작·수성전·실제 승패를 연결했다. 정식 시설·웨이브·보상 지급은 미연결이다. 최신 테스트 통과 기준과 남은 연결은 [로드맵 3장](ImplementationPlan.md#3-현재-상태와-출발점)을 확인한다.

## 2. 코드·씬·에셋 지도

### 런타임 코드

기본 경로: `Assets/_Project/Scripts/Runtime/`. 어셈블리는 `ProjectNeo.Runtime`, 네임스페이스는 `ProjectNeo.<폴더명>`이다.

| 폴더 | 주요 코드 | 역할 |
|---|---|---|
| Core | `RandomSource`, `WeightedPool`, `Unlockable` | 시드 기반 랜덤 통로, 가중치 추첨, 공통 해금 종류와 인터페이스 |
| Stats | `StatSheet`, `StatValue`, `StatModifier`, `StatType` | 기본 스탯과 출처별 수정값. `(기본값 + 고정 증가) × (1 + 비율 증가)` |
| Economy | `Cost`, `Wallet`, `MaterialDefinition` | 골드와 여러 종류 재료의 비용 및 지갑 |
| Economy | `ItemDefinition`, `Inventory`, `EconomyConfig`, `ItemShop` | 장비·소모품 정의, 아이템 개체와 보관함, 판매 비율, 매물 추첨과 거래 |
| Characters | `HeroDefinition`, `SoldierDefinition`, `SkillDefinition`, `Character` | 영웅·잡졸·스킬 데이터. `Character`는 영웅 한 명의 스탯과 장비를 보유하며 플레이어도 같은 클래스를 쓴다. |
| Characters | `PlayerControlSettings`, `PlayerInputController` | 조작·카메라 설정 에셋, Input System 입력과 CharacterController 이동. 기본 시점은 FirstPersonCamera다. |
| Characters | `PlayerViewCamera`, `FirstPersonCamera` | 입력이 사용하는 시점 방향 공통 접점과 기본 1인칭 카메라. 감도·높이·상하 각도는 PlayerControlSettings를 쓰고, 활성 중 플레이어 Visual은 그림자만 보이며 종료 때 복구한다. ThirdPersonCamera 코드는 남아 있지만 현재 플레이 씬에서는 사용하지 않는다. |
| Combat | `Health` | 일반 C# 체력 규칙. 피해·사망, 최대 체력 변경 시 제한. 스탯 이벤트 구독은 `Dispose()`로 해제한다. |
| Combat | `CombatProfile`, `CharacterActor`, `CombatTeam` | 몸통 크기·공격 종류·선딜레이·최소 피해·투사체 설정 에셋. 영웅과 잡졸을 공통 스탯 경로로 연결하고 팀, 대상 후보, 근접·원거리 공격, 재공격 대기, 피격·사망을 처리한다. `Character`는 영웅만 가지며 공통 접근은 `Stats`를 쓴다. |
| Combat | `DamageRules`, `CombatPhysics` | 일반 C# 피해 공식과 사거리·사선 검사. 동일 씬의 적만 공격하고 아군 충돌체는 공격을 막지 않는다. |
| Combat | `CombatProjectile` | 직선 투사체. 발사 때 공격력·팀·방향을 저장하고 명중 때 방어력을 적용한다. 아군 통과, 첫 적·벽 충돌, 사거리·수명 종료를 처리한다. |
| Combat | `UnitAISettings`, `UnitCombatAI` | 탐색 범위·판단 주기·회전·이동·도착 거리 설정. 가까운 추격 가능한 적 선택, 대상 유지, NavMesh 경로 갱신, 근접·원거리 공격, 사망 후 이동 정지. `UnitDirective`(Engage 알아서 싸움 / Hold 자리 지키며 닿는 적만 공격 / Move 적 무시하고 이동 후 Hold)로 행동을 바꾼다. 지시를 받지 않은 유닛의 기본값은 Engage다. |
| Combat | `PlaceholderCombatView` | `Visual` 자식에서 공격·피격·사망 신호를 구독한다. 임시 색 변경, 공격 표시, 체력 막대와 사망 외형. 전투 판정을 하지 않는다. |
| Facilities | `FacilityDefinition`, `FacilityState` | 시설별 레벨 데이터, 품목 풀, 업그레이드 비용과 스탯 효과 |
| Squads | `SquadConfig`, `Squad<TUnit>`, `Army<TUnit>` | 부대와 군대 인원 상한, 부대장 지정. |
| Squads | `SquadCommander`, `SquadOrder`, `SquadFormation` | 일반 C# 지휘 규칙. 부대 선택(하나·전체), 선택한 부대에 이동·공격·중지 지시, 기본 상태 중지, 이동 도착 시 중지 전환, 줄 단위 대형 자리 계산. 씬·입력을 알지 않는다. |
| Squads | `SquadCommandSettings`, `SquadCommandCenter`, `SquadCommandInput` | 지휘 키(`InputAction`)·대형 수치 에셋, 지시를 유닛의 `UnitDirective`와 대형 자리로 옮기고 도착을 확인하는 씬 연결, 키 입력과 화면 가운데 조준점 계산. 키는 `Command.asset` 인스펙터에서 바꾼다. |
| UI | `SquadCommandHud` | 시험용 지휘 표시(선택·지시·남은 인원·안내·조준점). 정식 전투 HUD는 아니다. |
| Morale | `SquadMorale` | 퍽 효과에서 사용할 부대 사기 값. 실제 사기 감소·도망 규칙은 B-8에서 구현한다. |
| Walls | `ConstructionProgress` | 공사 기간과 스테이지 진행 계산. 실제 배치·런 이월과는 아직 연결하지 않았다. |
| Walls | `BuildGridDefinition`, `BuildPieceDefinition`, `BuildLayout` | 격자 크기·최대 층수(`MaxLevels`, 기본 1), 조각 종류·점유 칸·높이·체력·프리팹 정의. 일반 C# 점유·회전·경계·고정 성문 영역 검사와 독립 계단의 연결 질의. 층 쌓기: 점유 키는 (x, 층, z)이고 조각은 가리킨 칸의 맨 위에 놓인다. `BuildPlacement.Level`·`Elevation`이 층과 바닥 높이다. `Check(..., out level, out elevation)`이 놓일 자리를 알려주고, `Remove` 뒤 `Settle(dropped, unsupported)`이 받침을 잃은 조각을 내려앉히거나 뺀다. `At(cell)`은 0층, `At(cell, level)`·`Top`·`ColumnHeight`·`IsCovered`로 층을 읽는다. |
| Walls | `ConstructionWorld`, `PlacedStructure` | 배치 모델과 몸통 생성, 설치 후 겹친 살아 있는 유닛을 새 조각 윗면으로 옮기기, 조각 체력·파괴 이벤트, 변경을 모아 비동기 NavMesh 갱신. |
| Walls | `PlacementViewSettings`, `PlacementController`, `BuildGridView` | 반투명 미리보기 재질·색, 마우스 선택·설치·취소·회전, 격자 표시. 미리보기는 충돌과 점유·NavMesh에 영향을 주지 않는다. |
| Walls | `SiegeWeaponDefinition`, `MountLayout`, `PlacedSiegeWeapon`, `ISiegeWeaponModule` | 수성 무기 공통 구조. 정의 에셋(ID·이름·크기·프리팹), 일반 C# 설치 지점 점유 규칙(지지 조각 ID + 지점 ID마다 하나), 모든 무기가 쓰는 공통 몸통, 무기별 동작을 붙이는 인터페이스. 대포 전용 클래스는 없다. |
| Walls | `BuildCameraSettings`, `ConstructionCameraController` | 직교 카메라의 각도·크기·초기 방향·회전 단위·전환 시간 에셋. Q/E로 주시점을 90도씩 부드럽게 돌며 월드·격자·배치 좌표는 유지한다. 게임 전용 에셋에서는 중클릭 드래그 이동·휠 확대축소·Home 초기화를 켜고 시작 주시점·감도·줌 한계를 데이터로 조정한다. |
| Walls | `FallSettings`, `FallDamageRules`, `UnitFallController` | 높이별 피해 계산과 파괴 시 낙하·착지. NavMeshAgent와 CharacterController 몸통을 지원하고 착지 후 이동을 복구한다. `LiftForConstruction`은 건설 시 피해 없이 새 윗면으로 옮기고 이동을 복구한다. |
| Walls / UI | `ConstructionSandbox`, `ConstructionHud` | 시험 씬 전용 이동·발밑 조각 파괴, uGUI·TMP 건설 선택과 상태 표시. 정식 부대 지휘나 준비 화면은 아니다. |
| Walls | `WallCommandSandbox` | 통합 시험용 Tab 시점 전환. 건설/1인칭 카메라·입력·HUD를 전환하며 지형과 지시는 유지한다. World가 NavMesh를 만든 뒤 유닛을 활성화한다. 정식 준비·전투 단계가 아니다. |
| Flow | `GamePhase`, `BattleOutcome`, `GameFlow`, `RunProgress` | 일반 C# 흐름 규칙. 타이틀 → 준비 → 전투 → 승리면 보상 → 다음 스테이지 준비, 패배면 런 결과 → 타이틀. 허용되지 않은 전환은 false를 돌려주고 상태를 바꾸지 않는다. `RunProgress`는 런 동안만 유지되는 진행 정보(현재 스테이지)이며 타이틀로 돌아가면 버린다. |
| Flow | `GameSession`, `SceneCatalog`, `SceneSwitcher`, `GameDirector` | `GameSession.Flow`는 씬이 바뀌어도 유지되는 흐름 보관처로 실행을 시작할 때마다 새로 만든다. 씬 이름은 `SceneCatalog` 에셋에 둔다. 씬 전환은 `SceneSwitcher`에서만 하며 지금은 통째 교체다. `GameDirector`는 게임 씬 단독 실행 때 새 런을 시작하고 흐름이 타이틀로 가면 타이틀 씬을 부른다. 준비·전투 기능은 `GameSession.Flow.PhaseChanged`를 구독해 붙인다. |
| Flow | `GamePhaseView` | 게임 씬에서 단계에 맞춰 시점·입력·화면을 바꾼다. 준비 기본은 1인칭 이동이고 건설소에서만 건설 카메라·배치·건설 화면으로 전환, 전투는 1인칭 카메라·플레이어 입력·지휘 입력·지휘 화면과 커서 잠금, 보상·런 결과는 둘 다 끄고 커서를 푼다. 단계가 바뀌어도 지형·유닛·지시는 유지한다. World가 NavMesh를 만든 뒤 유닛을 활성화한다. `WallCommandSandbox`의 Tab 전환을 실제 단계 전환으로 바꾼 것이다. 건설소 F 상호작용과 Esc 복귀를 연결했고 나머지 시설 기능은 후속이다. |
| Flow | `PreparationInteractionSettings`, `PreparationInteraction`, `ConstructionStation` | 준비 중 가까이서 바라본 건설소를 F로 열고 Esc로 닫는다. 거리·입력 액션은 `Data/GameFlow/Interaction.asset`에 두고 실행 시 복제한다. 시점·입력 전환은 `GamePhaseView`, 안내·준비 조준점은 `GameFlowHud`가 맡는다. |
| Flow | `GameBattleSettings`, `GameBattleController`, `PreparationStation`, `BattleStartStation` | GameFlow의 전투 단계에 적 생성·공성전·승패·마지막 적 퇴각을 연결한다. 전투 종료 시 AI·공격·투사체를 멈추고, 다음 준비에서 살아 있는 아군·성벽 피해를 유지하며 시험 적 편성을 새로 만든다. 성문 앞 F 상호작용으로 시작한다. 게임 전용 `Data/GameFlow/Battle.asset`·`Siege.asset`에서 편성·등장 위치·공성 수치를 조정한다. |
| UI | `TitleScreen`, `GameFlowHud`, `RuntimeSystemFont` | 타이틀 버튼 동작, 단계 표시·건물 상호작용 안내·남은 병력·사다리 밀기·결과 화면(전투 연결 시 시작·승패 자리표시 버튼은 숨김), 임시 화면에 맑은 고딕을 실행 중 적용하는 공용 부품. 정식 화면이 아니다. |
| Siege | `BuildBudget`, `SiegeOutcomeRules`, `SiegeLadderState` | 일반 C# 규칙. 임시 건설 코스트의 차감·환불, 승패 판정(동시 성립은 패배 우선, Q-09 확정)·마지막 적 퇴각 조건, 사다리 상태(놓임→세우는 중→세워짐, 한 명씩 오르기, 밀어서 쓰러뜨리기). |
| Siege | `SiegeSettings` | 코스트·사다리 시간과 거리·적 판단 주기·키(F 밀기, Tab 시점, R 다시 시작) 에셋. |
| Siege | `SiegeLadder`, `PlaceholderLadderView` | 맵에 고정된 사다리 자리. 그 칸에 성벽이 있을 때만 쓰인다. 밑의 적이 세우고, 성벽 위 아군(싸울 적이 없는 유닛, 키를 누른 플레이어)이 민다. 올라온 적이 근처에 있으면 밀지 못한다. 외형은 Visual 자식의 임시 막대다. |
| Siege | `SiegeAttackerBrain` | 적 하나의 공성 판단. 걸어서 닿는 아군이 있으면 기존 `UnitCombatAI`의 Engage에 맡기고, 없으면 종류(`SiegeAttackerRole`: 사다리병·성문 파괴병·궁수)에 따라 사다리·성문·사격을 고른다. 이동·공격은 `UnitDirective`로 실행한다. 쓸 사다리가 없는 사다리병은 성문으로 간다. |
| Siege | `SiegePrepController`, `SiegeDirector` | 준비 단계 도구(코스트 확인, 이번에 지은 조각 철거·환불, 부대를 클릭한 자리에 바로 배치)와 한 판의 진행(시작할 때 `SiegeSettings` 편성대로 아군·적 생성, 준비→전투→결과, 시점 전환, 적 활성화, 승패). 정식 런 진행과는 연결하지 않았다. |
| UI | `SiegeHud`, `SiegeStatusHud` | 시험용 준비 화면 패널과 단계·남은 인원·결과 표시. |
| Meta | `PerkDefinition`, `PerkLoadout` | 퍽 정의와 스탯·특수 효과 목록, 포인트 사용, 적용 및 초기화 |
| Meta | `PerkSpecialEffect`, `MoraleBoostPerkEffect` | 전투 이벤트별 특수 효과와 사기 증가 예시. 실제 전투 이벤트 연결은 후속 작업이다. |
| Meta | `MetaProgress`, `MetaConfig`, `CommanderProgression`, `UnlockGacha` | 유지 데이터, 지휘관 경험치·퍽 포인트, 환생 보너스 환산, 중복 없는 해금 추첨과 확정 등장 표시. 파일 저장은 아직 없다. |

### B-2 씬과 에셋

- `Assets/_Project/Scenes/CombatPrototype.unity`: 플레이어, 허수아비 3개, 바닥과 충돌 확인용 벽. 직접 조작을 시험하는 별도 씬이다.
- `Assets/_Project/Data/CombatPrototype/`: 플레이어·허수아비 영웅 정의, `CombatProfile`, `PlayerControls`. 임시 수치는 여기서 바꾼다.
- `Assets/_Project/Prefabs/CombatPrototype/`: 공통 몸통과 `Visual` 자식으로 나뉜 플레이어·허수아비 프리팹. 플레이어 입력은 씬 카메라를 참조하므로 씬 인스턴스에 붙는다.
- `Assets/_Project/Art/Placeholder/`: 임시 재질. 캡슐과 상자 메시를 사용한다.
- `Assets/_Project/Input/InputSystem_Actions.inputactions`: 기존 `Player/Move`, `Player/Look`, `Player/Attack` 액션을 플레이어마다 복제해 사용한다. 공유 에셋의 활성 상태를 바꾸지 않는다.

플레이 방법: 씬을 열고 Play → WASD 이동, 마우스 시점, 좌클릭 공격. Esc로 커서를 풀고 좌클릭하면 다시 잡는다. 허수아비 체력 100, 공격력 25이므로 네 번 맞히면 죽는다. Play를 다시 시작하면 초기 상태로 돌아온다.

현재 입력 공격은 전방 각도·사거리·가림 검사를 통과한 가장 가까운 적 하나에 `공격력 − 방어력`(최소 1) 피해를 준다. 플레이어는 아군, 허수아비는 적이다. 허수아비 방어력은 0이라 네 번 맞히는 동작은 같다. 플레이어에게도 `ReceiveDamage()`로 같은 체력 규칙을 적용하지만 허수아비는 반격하지 않는다.

### B-3 씬과 에셋

- `Assets/_Project/Scenes/UnitBattlePrototype.unity`: 관전용 자동 전투 씬. 아군(파랑) 10명과 적(빨강) 10명, 각각 근접 7명·원거리 3명. 중앙 장애물과 NavMesh가 있다.
- `Assets/_Project/Data/UnitBattlePrototype/`: 양쪽 근접·원거리 잡졸 정의 4개, 근접·원거리 `CombatProfile`, AI 설정과 구운 NavMesh 데이터. 수치는 동작 검증용이며 아군이 더 강하다.
- `Assets/_Project/Prefabs/UnitBattlePrototype/`: 잡졸 프리팹 4개와 직선 투사체 프리팹. 유닛 몸통에는 전투·충돌체·NavMeshAgent·AI가 있고 `Visual` 자식에만 임시 외형이 있다.

씬을 열고 Play를 누르면 자동으로 싸운다. Play를 다시 시작하면 초기 상태로 돌아온다. 플레이어 조작 시험은 `CombatPrototype`에서 한다. 전투 결과 화면이나 런 진행은 아직 없다.

`CharacterActor.Configure(...)`는 비활성 오브젝트를 활성화하기 전에 호출한다. 활성화 시 스탯·체력이 생기고 대상 후보 목록에 등록되며, 사망·비활성화 때 목록에서 빠진다. AI는 같은 씬의 적 중 NavMesh 완전 경로가 있는 가장 가까운 대상을 고른다. 대상 선택과 경로 갱신 주기는 에셋에 둔다. 사선이 막혔으면 장애물을 돌아 접근한다.

원거리 공격은 선딜레이가 끝날 때 대상 위치를 향해 발사한다. 발사 뒤에는 유도하지 않으며 첫 적이나 벽에 맞으면 사라진다. 발사 전 대상이 사망·이탈했으면 발사를 취소한다. 날아가는 투사체는 발사자의 사망과 별개로 남은 사거리·수명 동안 이동한다.

### B-5 부대 지휘 씬

- `Assets/_Project/Scenes/SquadCommandPrototype.unity`: 평지 70m × 70m와 장애물 하나. 플레이어, 아군 부대 3개(근접 5·근접 5·원거리 4), 멀리 선 적 8명(근접 6·원거리 2). 빌드 목록의 끝에 추가했으며 첫 씬은 그대로 `ConstructionPrototype`이다.
- `Assets/_Project/Data/SquadCommandPrototype/`: `SquadConfig`(부대 4개 × 10명), `Command`(지휘 키·대형 열 수·간격·조준 거리), `AllyAI`(시야 15m)·`EnemyAI`(시야 12m), 구운 NavMesh. 유닛·플레이어 프리팹과 수치는 B-2·B-3 시험 씬의 것을 그대로 쓴다.

Play → F1~F3으로 부대 선택(` 는 전체) → 화면 가운데 점을 땅에 맞추고 1을 누르면 그곳으로 이동한다. 이동 중에는 싸우지 않고 도착하면 중지가 된다. 3 중지는 자리를 지키며 사거리 안의 적만 공격한다. 2 공격은 화면 가운데가 가리키는 지점으로 가면서 시야에 들어온 적을 쫓아가 싸우고, 처리하면 다시 지점으로 향한다. 도착했거나 적이 없으면 공격 지시를 유지한 채 기다린다. 가리킨 곳이 없으면 제자리에서 싸운다. Play를 다시 시작하면 초기 상태다.

**시야:** 유닛의 시야는 `UnitAISettings.DetectionRange` 하나다. 그 거리 안에 있고 NavMesh로 끝까지 갈 수 있는 적만 발견한다. 벽에 가려 안 보이는 것이나 시야각은 없다. 이 씬은 아군 15m(`AllyAI.asset`), 적 12m(`EnemyAI.asset`)이며 임시 수치다. 적은 아군이 12m 안으로 오기 전까지 서 있으므로 지시를 차례로 시험할 수 있다. 관전 전투 씬의 `AI.asset`(60m)은 그대로다.

**표시:** `SquadCommand/Visual`의 `PlaceholderSquadMarkers`가 선택한 부대의 살아 있는 유닛마다 발밑에 노란 원판을, 이동·공격 지시의 목적지에 청록색 원판을 띄운다. 목적지 표시는 선택을 바꿔도 남고, 이동이 끝나 중지가 되면 사라진다. 선택·지시 상태를 읽기만 하는 임시 외형이며 외곽선 등 최종 표현은 M5에서 이 부품을 바꾼다. 죽은 유닛의 임시 체력 막대는 숨긴다.

대형은 지시한 순간 카메라가 보는 방향을 앞으로 삼는다. 대형 자리가 걸을 수 없는 곳이면 목적지 자체로 보낸다. 0.5m보다 높이가 다른 층으로 대형 자리를 붙이지 않는다. 목적지까지 길이 없거나 끊기면 부분 경로 끝에서 멈춘다(Q-23 결정). 목적지 자체가 사라졌으면 `UnitAISettings.DestinationRecoveryDistance`(현재 4m) 안의 남은 NavMesh로 다시 계산한다. 부대장 영웅과 정식 HUD는 아직 없다.

### B-5.4 건설·성벽 지휘 통합 씬

- `Assets/_Project/Scenes/WallCommandPrototype.unity`: 기존 건설 씬을 복사해 부대 3개×4명과 성문 위의 플레이어를 연결했다. 적은 없으며 부대 이동·붕괴 검증용이다. 기존 씬들과 빌드 첫 씬은 유지한다.
- Tab으로 건설↔1인칭 지휘를 전환한다. 지휘는 F1~F3·`·1·2·3, 플레이어는 WASD·마우스·좌클릭·Esc를 쓴다. Q-24 확정으로 전투·평지 지휘 씬도 기본 1인칭을 사용한다.
- `Assets/_Project/Data/WallCommandPrototype/Command.asset`: 시험용 2열·간격 1.1m와 입력. 조준은 처음 맞은 면의 위쪽 법선이 0.5 이상일 때만 허용하고 NavMesh와 높이 차이는 0.5m까지 허용한다. 옆면을 가리켜 위층·아래층이 선택되는 것을 막는다. 이 수치들은 에셋에서 조정한다.
- 건설 화면의 `성벽 위로 이동`은 실제 1번 부대 지시를 내린다. Tab으로 1인칭에서 도착을 보거나, 다시 건설로 돌아와 `발밑 조각 파괴`로 첫 부대원의 낙하를 확인한다. 낙하 중 내린 이동 지시는 착지 후 이어진다. Play를 다시 시작하면 지형·인원·지시 모두 초기화된다.

### 수성전 프로토타입 씬

- `Assets/_Project/Scenes/SiegePrototype.unity`: `WallCommandPrototype`을 복사해 만들었다. 원래 씬은 그대로다. 격자 16×20칸(48m×60m), 성벽 줄은 격자 z=8이며 그보다 작은 쪽이 성 안이다. 처음에 4·11·12번 칸이 뚫려 있다. 계단 2개, 성문 1개(이 씬 전용 체력 600).
- `Assets/_Project/Data/SiegePrototype/`: `Grid`, `Gate`, `Camera`(넓은 판용 직교 크기), `Siege`(`SiegeSettings`), `EnemyRanged`(위쪽 사격 피해 0.5배). 유닛 프리팹과 나머지 수치는 기존 시험 씬 것을 쓴다.
- 임시 코스트: 시작 40, 성벽 2·계단 3·대포 6. 이 시험에서만 쓰는 값이다. 대포는 설치만 되고 쏘지 않는다.
- 사다리 자리는 성벽 줄의 1·6·11번 칸이다. 11번은 처음에 뚫려 있어 성벽을 지어야 쓰인다. 세우기 3초·밀기 2초·오르기 2.5초.
- **편성 바꾸기:** 아군 부대와 적 무리는 씬에 두지 않고 Play를 시작할 때 `Siege.asset`의 `Ally Squads`·`Enemy Groups`대로 만든다. 한 줄이 아군 부대 하나 또는 적 한 무리이고, 프리팹과 인원 수를 적는다. 줄을 더하거나 빼면 부대·무리 수가 바뀐다. 아군은 `SquadConfig` 상한(현재 4부대 × 10명)까지만 만든다. 처음 서는 자리와 줄 간격은 씬의 `SiegeDirector`에서 바꾼다. 정해 둔 자리(4곳)보다 부대가 많으면 마지막 자리의 양옆과 뒤로 세우고, 적 줄이 많으면 판 안에 들어오도록 줄 간격을 좁힌다.
- 준비 화면의 부대 배치 버튼은 실제 부대 수만큼 실행 중에 만들어진다(`SiegeHud`가 첫 버튼을 복제). 버튼이 늘면 패널이 길어지고, 화면보다 길면 패널 전체를 줄여 넣는다. 이 씬은 전용 `Assets/_Project/Data/SiegePrototype/Command.asset`을 쓰며 부대 선택 키가 F1~F10이다. 키는 이 에셋의 `Select Squad` 목록에서 바꾼다. 다른 시험 씬은 기존 F1~F4 그대로다.
- 부대 이름에는 종류가 붙는다(`1번 부대 · 근접`, `· 궁수`, 섞이면 `· 혼성`). `SquadCommandHud.SquadName`이 부대원의 공격 방식에서 읽으며, 준비 화면 버튼과 1인칭 부대 목록 모두 같은 이름을 쓴다. 평지·성벽 지휘 시험 씬의 목록에도 같이 표시된다.
- 기본 편성: 아군 근접 4·근접 4·궁수 4, 적 사다리병 8·성문 파괴병 4·궁수 4. 적은 전투 시작 때 성 밖 멀리서 나타난다.
- 적 프리팹은 `Assets/_Project/Prefabs/SiegePrototype/`의 `enemy_ladder`·`enemy_gate_breaker`·`enemy_archer`다. 기존 적 프리팹의 변형이며 `SiegeAttackerBrain`(종류)·`UnitFallController`·종류 표시를 가진다. 프리팹은 꺼진 상태로 저장돼 있어야 한다. 사다리병은 등에 갈색 널빤지, 성문 파괴병은 어깨에 검은 덩이를 임시 표시로 달고, 죽으면 `PlaceholderDeathHider`가 표시를 숨긴다.
- 위쪽 사격 피해 비율은 `CombatProfile`의 `upwardShotDamageRatio`(기본 1, 변화 없음)와 `upwardShotMinimumRise`다. 발사자 발 높이보다 대상이 기준 이상 높을 때 적용한다.
- 플레이어는 성문 뒤 땅에서 시작한다. 성벽 위는 적 궁수의 사거리 안이다.
- 넣지 않은 것: 사기·도망, 대포 발사, 여러 웨이브, 보상, 일반 성벽을 부수는 적, 비행 적.

### B-4 건설 기초 씬

- `Assets/_Project/Scenes/ConstructionPrototype.unity`: 현재 빌드 첫 씬. 초기 방향 45도·내려다보는 각도 30도의 직교 카메라, 성벽·계단·고정 성문, 시험 유닛, 조각 선택과 이동·붕괴 시험 버튼.
- `Assets/_Project/Data/ConstructionPrototype/`: 격자, 성벽·계단·성문 정의, 미리보기, 낙하 설정, 시험 유닛 데이터. 한 칸 크기·점유 칸·높이·체력·외형 프리팹과 피해 수치를 여기서 바꾼다.
- `Assets/_Project/Prefabs/ConstructionPrototype/`: 기본 크기 1의 성벽·계단 경사로·성문 몸통. 루트에 충돌체와 `PlacedStructure`, `Visual` 자식에만 외형을 둔다. 계단은 현재 임시 경사로 메시다.
- `Assets/_Project/Art/Placeholder/Construction*`: 임시 재질과 경사로 메시. 특정 Synty 팩과 연결하지 않았다.
- `Assets/TextMesh Pro/`: 비대화식으로 가져온 TMP 필수 리소스. 예제는 가져오지 않았다. 시험 HUD는 실행 중 Windows의 맑은 고딕을 사용하며 글꼴 원본 파일을 프로젝트에 복사하지 않는다. 다른 플랫폼용 최종 글꼴은 M5에서 확정한다.

Q/E로 시점을 좌우 90도씩 돌려 높은 건물 뒤쪽을 확인한다. 조각 선택 → 마우스로 반투명 미리보기 이동 → 좌클릭 설치. R 또는 회전 버튼은 조각만 90도 회전하고 우클릭이나 취소 버튼으로 배치를 취소한다. 초록은 설치 가능, 빨강은 격자 밖·점유·빈 고정 성문 자리 등 설치 불가다. 유닛과 겹쳐도 설치할 수 있고, 설치 후 살아 있는 유닛은 피해 없이 새 조각 윗면으로 옮긴다. 계단은 성벽과 연결되지 않아도 먼저 설치할 수 있고 안내를 표시한다.

`Camera.asset`에서 현재 내려다보는 각도 30도, 초기 방향 45도, 직교 크기 20, 90도당 전환 시간 0.3초를 조정한다. 회전은 실제 시간 기준 SmoothStep 보간으로 천천히 출발·정지하며, 연속·역방향 입력에도 현재 화면 위치에서 이어진다. 연속 입력으로 회전량이 커지면 그만큼 전환 시간도 늘어난다.

Q/`RotateLeft()`와 E/`RotateRight()`는 화면 속 판의 왼쪽·오른쪽 회전을 뜻하며 카메라 공전 각도의 부호와는 반대다.

`성벽 위로 이동`은 시험 유닛이 계단을 통해 걸어 올라갈 수 있는 성벽으로 이동한다. `발밑 조각 파괴`는 그 유닛을 지지하는 조각에 체력만큼 시험 피해를 준다. 유닛은 떨어져 높이에 따른 피해를 받고, 지면에 착지한 뒤 갱신된 NavMesh에 다시 연결된다. Play를 다시 시작하면 초기 상태다.

`ConstructionWorld`는 스케일 1의 평평한 격자 원점을 제공한다. 조각은 `Geometry` 아래에만 생성하고 미리보기·격자선은 그 밖에 둔다. 최초 NavMesh는 시작 시 만들며 이후 요청은 한 번에 하나씩 비동기 처리한다. 진행 중 추가 변경이 생기면 한 번 더 갱신한다. 배치 ID는 현재 씬 실행 중 식별자이며 런 저장 계약은 아직 아니다.

프로토타입 씬의 격자는 최대 1층이라 한 칸에 조각을 겹쳐 놓을 수 없다. 층 쌓기는 격자 에셋의 `MaxLevels`를 2 이상으로 둔 곳에서만 동작하며 현재 `Game` 씬(3층)이 쓴다. 건설 조작은 조각을 가리키면 그 조각이 선 칸을 고르므로 성벽을 가리키면 그 위에 미리보기가 뜬다. 조각이 부서지면 `ConstructionWorld.Collapse`가 위 조각을 내려앉히며 `PieceDropping`으로 알리고, 그 위의 유닛은 `UnitFallController`가 떨어뜨린다. 수성 무기는 `BuildPieceDefinition.MountPoints`의 설치 지점을 사용해 바닥 점유와 분리한다. `PlacedStructure.TryGetMountPose()`가 조각 크기·회전을 반영한 월드 위치를 제공한다. 현재 성벽 정의에 `top` 지점 하나가 있다.

**수성 무기(B-4.6):** `대포` 버튼 → 성벽을 가리키면 그 조각의 가장 가까운 설치 지점에 반투명 미리보기 → 좌클릭 설치, 우클릭 취소. 지점 하나에 무기 하나만 놓인다. 성벽이 아닌 곳·설치 지점이 없는 계단·이미 무기가 있는 자리는 빨간색으로 거부한다. `무기 받침 성벽 파괴`는 무기를 받치는 성벽을 부숴 무기가 함께 사라지는지 확인한다. `ConstructionWorld.TryMount`/`RemoveWeapon`/`Collapse`가 설치·철거·동반 파괴를 처리하고 `WeaponRemoved`로 알린다. 무기는 `Geometry` 밖의 `Mounts` 아래에 생성해 NavMesh를 바꾸지 않는다.

새 무기를 추가하려면 `SiegeWeaponDefinition` 에셋과 프리팹(루트에 `PlacedSiegeWeapon`, 외형은 `Visual` 자식, 가로·높이·세로 1 기준)을 만든다. 무기만의 동작은 `ISiegeWeaponModule` 컴포넌트를 프리팹에 붙이면 설치·제거 때 호출된다. 시험 씬에는 `SiegeWeaponSetup.AddToOpenScene`의 무기 배열에 한 줄을 더하고 HUD의 `weapons` 목록에 넣는다. 현재 대포(`Cannon.asset`, `Cannon.prefab`)는 쏘지 않는 임시 외형이며 충돌체가 없다.

현재 수성 무기의 발사·피해·자체 체력, 적의 성벽 공격, 재화 차감·수리·런 이월, 정식 건설/전투 전환은 연결하지 않았다. 기존 직접 조작 씬·관전 전투 씬은 계속 별도로 시험한다.

### 에디터와 테스트

- `Assets/_Project/Scripts/Editor/CombatPrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Combat Prototype` 또는 `Create()`로 씬·프리팹·에셋을 Unity API로 생성한다. 기존 씬이 있거나 현재 씬에 저장하지 않은 변경이 있으면 중단한다. 기존 테스트 수치 에셋도 덮어쓰지 않는다.
- `Assets/_Project/Scripts/Editor/UnitBattleSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Unit Battle Prototype` 또는 `Create()`로 B-3 씬·프리팹·수치 에셋·NavMesh를 생성한다. 기존 씬과 저장하지 않은 변경은 보존한다. `UpgradeTrainingTeams()`는 기존 허수아비 프리팹을 적 팀으로 갱신한다.
- `Assets/_Project/Scripts/Editor/ConstructionPrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Construction Prototype` 또는 `Create()`로 건설 씬·에셋·프리팹·시험 UI를 Unity API로 생성한다. 기존 씬이나 저장하지 않은 변경이 있으면 중단한다. TMP 필수 리소스를 먼저 가져와야 한다.
- `Assets/_Project/Scripts/Editor/SiegeWeaponSetup.cs`: 메뉴 `ProjectNeo/Setup/Add Siege Weapons To Construction Prototype`. 기존 건설 씬에 대포 에셋·프리팹과 시험 버튼을 추가한다. 이미 추가됐거나 저장하지 않은 변경이 있으면 중단한다. 새로 씬을 만들 때는 `ConstructionPrototypeSetup.Create()`가 함께 호출한다.
- `Assets/_Project/Scripts/Editor/SquadCommandSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Squad Command Prototype` 또는 `Create()`로 B-5 씬·수치 에셋·NavMesh·시험 HUD를 만든다. 기존 씬이나 저장하지 않은 변경이 있으면 중단한다. B-2·B-3의 프리팹이 먼저 있어야 한다. `Upgrade()`(메뉴 `Upgrade Squad Command Prototype`)는 기존 씬에 팀별 시야 에셋과 선택·목적지 표시를 더하며 여러 번 실행해도 결과가 같다. 프리팹 인스턴스의 값은 `SerializedObject`로 써야 씬에 저장된다.
- `Assets/_Project/Scripts/Editor/WallCommandSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Wall Command Prototype`으로 기존 건설 씬을 보존한 채 통합 씬을 만든다. 기존 통합 씬·저장하지 않은 변경은 덮어쓰지 않는다. B-2·B-3·건설·평지 지휘 에셋이 선행 조건이다.
- `Assets/_Project/Scripts/Editor/FirstPersonSetup.cs`: 메뉴 `ProjectNeo/Setup/Upgrade Player Views To First Person`으로 기존 전투·평지 지휘 씬의 카메라와 플레이어 입력 참조를 1인칭으로 전환한다. 현재 두 씬 모두 적용된 상태이며 다시 실행해도 카메라를 중복 추가하지 않는다. 씬 생성 코드도 기본 1인칭을 만든다.
- `Assets/_Project/Scripts/Editor/GameFlowSetup.cs`: 메뉴 `ProjectNeo/Setup/Enable Preparation Interaction`은 기존 Game 씬의 건설소·입력·안내 화면을 연결한다. `ApplyDefaultCastle` 재실행 때도 이를 다시 연결한다. 메뉴 `ProjectNeo/Setup/Create Game Flow Scenes`로 `Title`·`Game` 씬과 `Assets/_Project/Data/GameFlow/SceneCatalog.asset`을 만들고 두 씬을 빌드 목록 끝에 더한다. 기존 씬이나 저장하지 않은 변경이 있으면 중단한다. 이미 실행해 두 씬이 있다. `AddCastle()`(메뉴 `Add Castle To Game Scene`)은 `WallCommandPrototype`의 성·건설·플레이어·부대를 게임 씬으로 옮겨 `GamePhaseView`에 연결한다. 프로토타입 씬은 읽기만 하고 저장하지 않는다. 프로토타입 전용 시험 버튼은 숨기고 안내 문구를 바꾼다. 이미 성이 있으면 중단하며 현재 적용된 상태다. `ApplyDefaultCastle()`(메뉴 `Apply Default Castle To Game Scene`)은 게임 전용 `Data/GameFlow/Grid.asset`(80×100칸)·`BuildCamera.asset`(직교 크기 55, 확대축소 20~180)을 쓰도록 바꾸고 외벽·성문·계단의 초기 배치, 바닥 크기, 부대와 플레이어 시작 위치를 다시 적용한다. 플레이어는 안뜰 남쪽(격자 좌표 40, 34)에서 북쪽을 보고 시작한다. 배치 수치는 이 메서드 위의 상수이며 여러 번 실행해도 결과가 같다. 조각 정의(성벽·계단·성문)는 `Data/ConstructionPrototype/`의 것을 함께 쓴다.
- `Assets/Tests/EditMode/GameFlowTests.cs`: 흐름 단계 전환, 거부되는 전환, 전투 결과 1회 처리, 새 런의 진행 초기화, 단계 변경 알림.
- `Assets/_Project/Scripts/Editor/GameBattleSetup.cs`: Game 씬의 공성전·깃발 상호작용을 연결하고 기존 HUD 영역을 배치한다. 메뉴 `ProjectNeo/Setup/Connect Battle To Game Scene`·`Arrange Game HUD`.
- `Assets/Tests/PlayMode/GameBattlePlayTests.cs`: 실제 적 접근·승패·패배 우선·마지막 적 퇴각과 열린 경로·3층 사다리·성문 붕괴 조각 공격·다음 준비 이월·HUD 배경 포함 영역 분리.
- `Assets/Tests/PlayMode/GameFlowPlayTests.cs`: 저장된 `Title`·`Game` 씬으로 새 게임 진입, 씬 전환 없는 단계 변경, 패배 후 타이틀 복귀, 게임 씬 단독 시작, 준비의 건설 시점과 전투의 1인칭 지휘 전환·보상 중 입력 정지·유닛과 지시 유지·AudioListener와 EventSystem이 하나뿐인지, 기본 성이 성문 하나와 닫힌 외벽이고 안뜰에서 성벽 위로 길이 이어지며 밖으로는 나갈 수 없는지. 씬을 통째로 바꾸므로 끝날 때 빈 씬만 남긴다. 실제 버튼 클릭은 직접 확인 대상이다.
- `Assets/_Project/Scripts/Editor/SiegePrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Siege Prototype`. `WallCommandPrototype`을 복사해 수성전 씬을 만든다. 기존 씬·저장하지 않은 변경은 덮어쓰지 않는다. 씬을 연 뒤 `SerializedObject`로 연결할 에셋은 다시 불러와야 한다(씬을 열면 방금 만든 에셋 참조가 풀려 null로 저장된 적이 있다).
- `Assets/Tests/EditMode/SiegeRulesTests.cs`: 코스트 차감·환불, 승패 판정, 사다리 세우기·한 명씩 오르기·밀기·막힘, 위쪽 사격 피해 배율.
- `Assets/Tests/PlayMode/SiegePrototypePlayTests.cs`: 코스트 차감·철거 환불과 전투 시작 후 건설 잠금, 부대 즉시 배치, 뚫린 곳으로의 진입, 막힌 성에서 사다리 세우기·오르기·성문 공격, 성벽 위 아군의 사다리 밀기와 낙하 피해, 승리·패배 판정.
- `Assets/_Project/Scripts/Editor/BuildScript.cs`: Windows 빌드. 기본 출력 `Builds/Windows/ProjectNeo.exe`.
- `Assets/Tests/EditMode/`: 스탯, 지갑, 시설, 공사·부대, 퍽, 아이템·상점, 캐릭터, 메타 진행, 체력 규칙.
- `Assets/Tests/PlayMode/CombatPrototypePlayTests.cs`: 실제 씬을 추가로 불러와 이동·충돌, 공격 선딜레이·사거리·가림·사망, 카메라 회전 제한과 벽 회피를 확인한다.
- `Assets/Tests/EditMode/DamageRulesTests.cs`: 방어력 차감, 최소 피해와 유효하지 않은 수치를 확인한다.
- `Assets/Tests/PlayMode/UnitCombatPlayTests.cs`: 팀과 피해, 대상 유지·교체, 투사체 아군 통과·회피·얇은 벽 충돌, 에이전트 사망 정지, 저장된 10 대 10 씬 전투를 확인한다. 테스트용 NavMesh와 씬, 고정 시간 간격은 종료 때 정리한다.
- `Assets/Tests/EditMode/BuildLayoutTests.cs`: 임의 격자 크기, 음수 좌표·회전 점유·실패 시 상태 보존, 독립 계단의 연결 갱신, 고정 성문 영역, 높이에 따른 낙하 피해.
- `Assets/Tests/PlayMode/ConstructionPlayTests.cs`: 카메라 4방향 회전과 픽킹 불변, 즉시 점프 없는 보간과 연속·역방향 입력, 미리보기·취소의 무영향, 회전 설치·중복 거부·비동기 갱신, 겹친 유닛의 윗면 이동·성문 영역, 실제 계단 이동·붕괴·낙하 피해와 에이전트 복귀, CharacterController 몸통의 낙하·복구, 연속 변경의 반영과 사라진 성벽 위 경로.
- `Assets/Tests/EditMode/BuildStackingTests.cs`: 1층 격자의 중복 거부 유지, 맨 위에 쌓기와 최대 층, 계단 위 금지·성문 위 허용과 성문 파괴 후 내려앉기, 받침 높이가 다른 여러 칸 조각 거부, 2단 계단의 같은 층 연결, 내려앉기와 일부 받침 상실 시 제거, 덮인 성벽의 무기 설치 금지.
- `Assets/Tests/PlayMode/StackedConstructionPlayTests.cs`: 게임 씬에서 미리보기 층·높이, 2층 성벽과 2단 계단을 놓은 뒤 안뜰에서 2층까지의 NavMesh 경로, 3층 상한, 플레이어·여러 병력과 겹친 건설 후 윗면 이동·피해 없음·이동 복구, 성문 위 설치와 성문 파괴 후 내려앉기, 아래 성벽 파괴 시 위 성벽·무기의 내려앉기와 체력 유지·길 복구.
- `Assets/Tests/EditMode/MountLayoutTests.cs`: 설치 지점 점유가 바닥 점유와 분리되는지, 실패 사유와 ID 보존, 서로 다른 무기 종류의 공통 규칙, 지지 조각 제거 시 그 위 무기만 해제.
- `ConstructionPlayTests`의 수성 무기 2개: 미리보기 무영향, 성벽 위 설치 위치·크기, 중복·성벽 밖·계단 거부, 바닥 점유와 NavMesh 불변, 받침 성벽 파괴 시 동반 파괴와 무기 동작 부품(`RecordingSiegeWeaponModule`) 알림 1회, 철거.
- `ConstructionPlayTests`의 지휘 5개: 윗면·옆면·지면 조준 구분, 실제 이동 지시로 계단 오르기와 길 단절, 처음부터 끊긴 목적지, 목적지 성벽 소실 후 재탐색, 낙하 후 지상 지시 복귀.
- `Assets/Tests/PlayMode/WallCommandPlayTests.cs`: 입력·카메라·몸통 표시 전환과 지시 보존, 부대 전체의 성벽 도착과 다른 부대 유지, 계단 소실 후 정지, 받침 붕괴 후 부대 낙하·이동 복귀.
- `Assets/Tests/EditMode/SquadCommanderTests.cs`: 기본 중지, 하나·전체 선택과 없는 부대 거부, 선택한 부대만 지시, 이동 도착 시 중지·공격 유지, 대형 자리의 개수·간격·방향.
- `Assets/Tests/PlayMode/SquadCommandPlayTests.cs`: 저장된 지휘 씬에서 기본 중지, 선택한 부대만 이동 후 중지, 이동 중 적 무시, 중지 중 닿는 적만 공격하고 쫓지 않음, 공격 지시의 교전과 적이 없을 때 대기, 화면 가운데 이동 지시와 선택 없는 지시 거부. 실제 키 입력은 사용자 직접 확인 대상이다.
- `Assets/Tests/PlayMode/SetupSmokePlayTests.cs`: 프레임 진행 확인.
- `Assets/Tests/PlayMode/UnitScaleBenchmark.cs`: 명시적으로 실행하는 성능 측정. 평소 테스트에서는 제외된다.

## 3. 협업과 통합

2026-11-11부터 협업 중이다. 팀원이 적 침공과 전투 단계 한 판을, 사용자가 게임 진입 씬과 전체 흐름 뼈대를 맡는다. 현재 담당과 접점은 [로드맵 6.1](ImplementationPlan.md#61-담당-자리와-공통-수정-권한)이 기준이며 아래 운영 기준을 지금부터 적용한다. 팀원과는 별도 PC·별도 브랜치에서 작업하며(Q-26), 계획의 개발자 1·2·3 표는 인원이 늘 때 참고할 제안이다.

### 작업을 나누는 단위

- 한 작업에 주 담당자 한 명을 둔다. 규칙 코드, 해당 테스트, 완료 보고까지 같은 담당자가 책임진다.
- 시작 전에 작업 ID, 선행 조건, 수정할 폴더·파일, 다른 작업에 필요한 입출력, 검증 방법을 적는다. 작업 ID는 구현 계획의 ID를 쓴다.
- 서로 호출하는 코드가 있으면 데이터 형태와 호출 순서를 먼저 합의한다. 아직 없는 기능은 가짜 구현이나 테스트용 입력으로 검증하되 실제 기능 연결 완료와 구분한다.
- 같은 파일을 고칠 일은 먼저 끝낼 사람과 뒤에 연결할 사람을 정한다. 서로 다른 메서드라도 같은 파일이면 공동 수정으로 본다.
- 기능별 씬과 프리팹에서 검증하고, 통합 씬 반영은 한 사람이 차례대로 한다. 개별 씬 통과만으로 통합 완료로 표시하지 않는다.

### 작업 환경

| 환경 | 병렬로 가능한 것 | 차례로 해야 하는 것 |
|---|---|---|
| 각자 별도 PC 또는 별도 프로젝트 작업 폴더·브랜치·에디터 | 분리한 코드·테스트·씬·에셋 개발과 각자 검증 | 공유 계약 변경과 통합 브랜치 반영 |
| 같은 작업 폴더와 같은 Unity 에디터 공유 | 사전에 나눈 파일의 코드·문서 초안 작성 | 에셋 생성, 씬 저장, Refresh·컴파일, PlayMode, 테스트, 빌드, 패키지 변경 |

별도 작업 환경을 권장한다. 하나의 프로젝트 폴더를 여러 에디터에서 동시에 열지 않는다. 별도 작업 폴더라도 같은 PC에 에디터가 여러 개 있으면 Unity 명령에 `--project-path`로 대상을 지정한다. 같은 PC의 성능 측정은 다른 에디터의 부하를 피해서 진행한다.

공유 에디터를 쓸 경우 한 사람만 에디터를 조작한다. 시작·종료와 사용 목적을 다른 두 사람에게 알리고, 다른 사람이 PlayMode나 에셋 작업을 하는 동안 컴파일을 요청하지 않는다. 이 문서는 브랜치나 작업 폴더를 자동 생성하라는 지시가 아니다.

### 충돌이 잦은 공유 영역

| 영역 | 운영 기준 |
|---|---|
| `CharacterActor.cs`, `UnitCombatAI.cs` | 전투 몸통과 이동의 공통 접점. 확장 담당자 한 명이 연결하고 다른 사람은 별도 규칙·어댑터 파일에서 작업한다. |
| `CombatProfile`, 영웅·잡졸 정의, `StatType` | 직렬화 데이터 계약. 필드·enum 변경을 먼저 알리고 영향을 받는 에셋과 테스트를 함께 확인한다. |
| 공용 프리팹·통합 씬·NavMesh 데이터 | 작업별 소유자를 지정한다. 각자 저장한 파일을 텍스트 병합해서 해결하지 않는다. |
| `.asmdef`, `Packages/`, `ProjectSettings/`, 빌드 목록 | 통합 담당자 한 명이 반영한다. 자동 변경도 차이를 확인한다. |
| `GameDesign.md`, `ImplementationPlan.md`, `Context.md` | 해당 작업의 문서 정리 담당자가 최신 내용을 읽고 반영한다. 같은 시간에 같은 문서를 쓰지 않는다. |

문서 담당은 매 묶음마다 정한다. 사용자에게서 결정이 나오면 바로 관련 문서 담당자에게 넘겨 원문에 반영한다. 문서 잠금을 이유로 결정 기록을 다음 작업까지 미루지 않는다.

### 통합 순서

1. 시작할 작업 묶음의 계약과 소유 파일을 확인한다.
2. 각자 분리된 환경에서 작업하고 해당 테스트를 실행한다.
3. 통합 담당자가 공통 계약 → 규칙 → 씬 연결 → UI·외형 순으로 반영한다.
4. 공통 코드가 바뀌면 관련 회귀 테스트를 돌린다. 작업 묶음을 닫을 때 EditMode·PlayMode 전체와 통합 씬을 확인한다.
5. 배포 가능한 중간 결과를 내는 단계에서는 실제 실행 파일도 시험한다. 에디터에서 통과했다고 빌드 검증을 생략하지 않는다.
6. 담당자가 결과와 남은 문제를 적고 문서 담당자가 상태를 갱신한다. 커밋과 병합은 사용자 또는 명시적으로 권한을 받은 사람이 한다.

사용자에게 완료를 알릴 때는 직접 검증 순서를 함께 준다. 정확한 씬 경로 → Play 또는 테스트 실행 → 조작 → 기대 결과 → 초기화 방법 순으로 적는다. 필요한 경우 Test Runner 메뉴와 테스트 이름도 안내한다. 에디터를 조작한 작업은 검증용 씬을 저장된 비실행 상태로 열어두는 것을 기본으로 한다.

통합이 밀리면 새 기능을 더 벌리기보다 세 사람이 연결 오류와 재현 문제를 함께 해결한다. 작업 인원이 세 명이어도 모든 시점에 세 기능을 동시에 진행할 필요는 없다.

### 작업 인계 양식

```text
작업 ID / 제목:
담당자 / 통합 담당자:
상태: 미착수 | 결정 대기 | 진행 중 | 연결 대기 | 검증 중 | 완료
선행 작업 / 필요한 결정:
소유 파일 / 함께 수정하면 안 되는 파일:
제공하는 데이터·이벤트 / 사용하는 데이터·이벤트:
이번 작업 범위 / 후속 작업으로 남길 범위:
완료 기준 / 실제 검증 결과:
다음 담당자가 알아야 할 제한:
```

## 4. 코드와 검증 원칙

- 게임 코드: `Assets/_Project/Scripts/Runtime/<시스템>/`, 어셈블리 `ProjectNeo.Runtime`, 네임스페이스 `ProjectNeo.<시스템>`.
- 에디터 코드: `Assets/_Project/Scripts/Editor/`, 어셈블리 `ProjectNeo.Editor`.
- 테스트: `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`, 네임스페이스 `ProjectNeo.Tests`.
- 규칙과 계산은 화면과 분리한 일반 C# 클래스로 만들고 EditMode 테스트로 검증한다. 이동·씬 연결·파괴는 PlayMode와 직접 플레이로 확인한다.
- 수치는 ScriptableObject 에셋에 둔다. 테스트에서 만들 수 있도록 정의 클래스에 `Create(...)` 정적 메서드를 둔다.
- ScriptableObject와 MonoBehaviour는 파일 하나에 클래스 하나를 두고 파일 이름을 클래스 이름과 맞춘다.
- 저장되는 enum은 숫자를 명시한다. 기존 번호를 바꾸지 않고 새 값은 끝에 추가한다.
- 랜덤은 `ProjectNeo.Core.IRandomSource`를 통한다. 같은 시드와 입력이면 보상·품목 추첨 결과를 재현할 수 있어야 한다.
- 영웅·아이템·배치·부대의 런 내 식별 정보와 콘텐츠 정의 ID를 구분한다. 저장·전투 결과의 참조 방식은 사용하는 작업들이 함께 합의한다.
- 에디터·씬 객체 참조를 저장 데이터로 넘기지 않는다. 런과 메타 데이터의 수명, 보상의 중복 지급 방지, 이벤트 구독 해제를 통합 검증에 포함한다.

테스트 숫자는 완성도를 뜻하지 않는다. 현재 기준은 로드맵 3장, 과거 통과 기록은 이 문서 7장에 있다. 기능이 늘면 개별 규칙, 씬 연결, 실제 플레이, 실행 파일을 각각 필요한 수준으로 검증한다.

## 5. 아트 교체 대비

아래는 지금부터 지켜야 할 구현 원칙이며 전부 구현됐다는 뜻은 아니다. 실제 충족 여부는 기능·아트 통합 때 확인한다.


나중에 구한 에셋(모델, 애니메이션, 이펙트, 소리, UI 그림)으로 임시 그래픽을 갈아 끼운다. 어떤 에셋이 올지 모르므로 아래를 지킨다.

- **프리팹을 "몸통"과 "겉모습"으로 나눈다.** 몸통(루트)에는 규칙, 충돌체, 길찾기가 붙고, 겉모습은 그 아래 자식 오브젝트 하나에만 둔다. 교체할 때는 그 자식만 바꾼다.
- **충돌체와 크기는 겉모습에서 가져오지 않는다.** 유닛 반지름, 키, 공격 사거리, 격자 한 칸의 크기는 데이터에 숫자로 둔다. 모델 크기가 달라져도 전투 결과가 바뀌지 않는다.
- **규칙 코드는 애니메이션을 직접 부르지 않는다.** "공격 시작", "피격", "사망", "이동 속도 변화" 같은 신호만 내보내고, 겉모습 쪽이 그 신호를 듣고 애니메이션, 이펙트, 소리를 튼다. 임시 그래픽은 색 바꾸기 같은 간단한 반응만 한다.
- **공격 판정 시점은 데이터로 둔다.** 애니메이션 이벤트에 판정을 걸지 않는다. "공격 시작 후 몇 초에 맞는다"를 데이터에 적어 두면, 애니메이션을 바꿀 때 숫자만 맞추면 된다.
- **사람 모양 유닛은 표준 뼈대(휴머노이드)를 쓴다고 가정한다.** 에셋 스토어의 사람 모델과 애니메이션 대부분이 이 방식이라 서로 바꿔 끼울 수 있다. 장비와 무기를 붙일 자리(오른손, 왼손, 등)는 이름 붙은 빈 오브젝트로 미리 둔다.
- **겉모습은 데이터에서 참조한다.** 영웅, 잡졸, 적, 성벽 조각, 아이템 정의에 "겉모습 프리팹"과 "아이콘" 칸을 두고, 코드에 프리팹 이름을 적지 않는다.
- **성벽 조각은 격자 한 칸 크기를 기준으로 만든다.** 조각식(모듈형) 성벽 에셋으로 바꿀 때 칸 크기만 맞추면 되게 한다.
- **UI는 배치와 그림을 나눈다.** 글꼴, 색, 테두리 그림은 한 곳(공용 스타일)에서 바꿀 수 있게 한다.
- **임시 그래픽 파일은 `Assets/_Project/Art/Placeholder/`에 모은다.** 나중에 한 번에 찾아서 지울 수 있다. 받은 에셋은 `Assets/` 바로 아래 각자 폴더에 그대로 두고, 우리가 고친 것만 `_Project`로 가져온다.

## 6. Unity CLI 작업 방법

`unity` 명령(Unity CLI 1.0.0-beta.17)으로 열려 있는 에디터를 조작한다. 프로젝트에 `com.unity.pipeline` 패키지가 들어 있다. 먼저 `unity status`로 에디터가 `ready`인지 확인한다. `unreachable`만으로 종료됐다고 단정하지 않는다. Codex 샌드박스 안에서는 로컬 통신과 프로세스 조회가 제한될 수 있으므로, 필요하면 허용된 권한으로 같은 상태 조회를 다시 해 접근 제한인지 구분한다. 에디터가 안 열려 있으면 사용자에게 열어 달라고 한다.

아래 명령의 `--caller plugin --skill unity-cli`는 호출에 이름표를 붙이는 옵션이다. 동작에는 영향이 없다.

### 컴파일

파일을 쓴 뒤에는 에디터가 스스로 컴파일을 시작하지 않고 멈춰 있는 일이 있다. 직접 요청한다.

```bash
unity command script eval --caller plugin --skill unity-cli --result-only 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); "requested"'
unity status --until-ready --timeout 30
unity command script eval --caller plugin --skill unity-cli --result-only '"failed=" + UnityEditor.EditorUtility.scriptCompilationFailed + " compiling=" + UnityEditor.EditorApplication.isCompiling'
```

- `unity status --until-ready`는 다시 불러오는 동안 실패로 끝날 수 있다. `editor status`가 `ready`가 될 때까지 몇 번 반복한다.
- 컴파일 에러 내용은 `Logs/Editor.log`에서 `error CS`를 찾는다.
- `unity recompile`과 `unity command script recompile`은 이 프로젝트에서 멈추거나 실패했다. 쓰지 않는다.

### 테스트

```bash
# EditMode 전체. 결과가 바로 돌아온다.
unity command test run --caller plugin --skill unity-cli --mode editor --result-only

# PlayMode. 명령은 바로 돌아오고 테스트는 뒤에서 돈다. 결과는 test status로 확인한다.
unity command editor set_auto_tick --caller plugin --skill unity-cli
unity command test run --caller plugin --skill unity-cli --mode playmode --async-tests --result-only
unity command test status --caller plugin --skill unity-cli --result-only
```

- PlayMode 테스트의 `test status`가 completed여도 에디터가 Play를 빠져나오는 중일 수 있다. EditMode 실행 전 `script eval`로 `UnityEditor.EditorApplication.isPlaying`이 false인지 확인한다. 전환 중 EditMode를 시작하면 Test Runner 복구 오류와 명령 대기가 발생할 수 있다. 이번 발생은 에디터의 Assets → Refresh로 스크립트를 다시 불러온 뒤 해소하고 EditMode 122개 재통과를 확인했다.
- PlayMode를 돌리기 전에 `editor set_auto_tick`을 부른다. 에디터 창이 뒤에 있으면 프레임이 멈추기 때문이다.
- 이름으로 고르려면 `--filter <이름 일부>`를 쓴다. `[Explicit]` 테스트(성능 측정)는 `--include-explicit`을 붙여야 돈다.
- `unity test`는 에디터가 열려 있으면 거부된다.
- 현재 테스트 기준 개수는 [구현 계획 3장](ImplementationPlan.md#3-현재-상태와-출발점)에 기록한다. `UnitScaleBenchmark`는 Explicit라서 평소에는 돌지 않는다.

직접 PlayMode 화면을 검증할 때 `set_auto_tick`만으로 프레임이 계속 멈춰 있으면 Run In Background도 확인한다. 검증 중에만 아래 오버라이드를 켜고, 검증 종료 후 PlayMode를 멈춘 다음 끈다. 원래 프로젝트 값을 복원하는 명령이며 제품의 백그라운드 실행 정책을 정하는 작업이 아니다.

```bash
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --enabled true --result-only
# 화면과 프레임 진행을 검증하고 PlayMode를 종료한 뒤 복원한다.
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --enabled false --result-only
```

### 빌드

`unity build --execute-method`는 에디터가 열려 있으면 거부된다. 열린 에디터에서 직접 부른다.

```bash
unity command script eval --caller plugin --skill unity-cli --timeout 1500 --result-only 'ProjectNeo.Editor.BuildScript.BuildWindows(); "done"'
```

결과물은 `Builds/Windows/ProjectNeo.exe`에 생긴다. `Builds/`는 git에서 무시된다.

### 패키지

```bash
unity command package add --caller plugin --skill unity-package-management --name <패키지 이름> --confirm --wait --result-only
```

### 씬과 에셋

- 폴더, 씬, 프리팹, 에셋을 만들거나 옮길 때는 `.unity`, `.prefab`, `.asset`, `.meta` 파일을 직접 고치지 말고 에디터 명령으로 한다. `script eval`에서 `AssetDatabase`, `PrefabUtility`, `EditorSceneManager`를 쓰거나, 긴 코드는 `.cs` 파일로 써서 `script eval --file <경로>`로 돌린다.
- `.cs` 파일과 `.asmdef` 파일은 직접 써도 된다. `.meta`는 에디터가 만든다.
- 빈 폴더에는 `.gitkeep`이 들어 있다. 폴더에 파일을 넣으면 `.gitkeep`을 지운다.

### 그 밖의 주의

- 셸의 heredoc으로 한글이 든 C# 파일을 여러 개 한꺼번에 쓰면 깨진 적이 있다. 파일은 파일 쓰기 도구로 하나씩 쓴다.
- 명령 결과를 파싱할 때 `2>&1`로 합치지 않는다. stdout만 읽는다.
- 빌드하면 `Assets/Settings/` 아래 URP 에셋과 `ProjectSettings/` 일부가 저절로 바뀔 수 있다.

## 7. 과거 검증 기록

당시 완료 범위와 검증 근거를 보존한다. 과거 테스트 개수·임시 수치는 최신 기준이나 최종 성능 보장이 아니다. 현재 작업 상태는 로드맵 3장을 먼저 읽는다. 새 완료 결과는 이 장에 날짜와 함께 추가하고 현재 상태와 코드 지도도 갱신한다.

<details>
<summary>완료 이력·성능 측정·직접 검증 기록 펼치기</summary>

### 2026-11-11 이전부터 이어진 초기 작업

- 프로젝트 폴더, 어셈블리 4개, EditMode·PlayMode 테스트 환경, Windows 빌드 스크립트 구성 완료.
- 스탯·수정값, 비용과 여러 재료 지갑, 시설 레벨, 공사 기간, 부대 인원 상한의 1차 데이터 구조 완료.

### 2026-11-11: 단계 A 데이터 구조 보완 완료

| 기존 작업 ID | 완료한 범위 |
|---|---|
| A-1 | 퍽의 스탯·특수 효과 목록, 효과 대상, 이벤트 종류, 사기 증진 효과 예시 |
| A-2 | 장비·소모품 정의, 장비 스탯 수정, 구매가와 판매 비율 |
| A-3 | 아이템 개체와 보관함 |
| A-4 | 시설 레벨별 가중치 품목 풀과 시드 기반 추첨 |
| A-5 | 공통 구매·판매·재고 처리 |
| A-6 | 영웅·잡졸 정의, 영웅의 장비·스탯과 스킬 참조 |
| A-7 | 지휘관 경험치·퍽 포인트, 해금 포인트·목록, 중복 없는 해금, 확정 등장 표시, 퍽 찍기·초기화 |

당시 EditMode 64개 통과. 화면·런 루프·파일 저장·실제 스킬 발동·모든 퍽의 전투 연결까지 완료된 것은 아니다.

### 2026-11-11: 단계 B 기초 전투 결과

#### B-1 결과 (2026-11-11, 완료)

측정 방법: `Assets/Tests/PlayMode/UnitScaleBenchmark.cs`. 지면 120m × 120m, 높이 4m 성벽 한 줄(5m 조각 12개), 성 안쪽 경사로 2개. 캡슐 유닛에 NavMeshAgent를 붙여 지면과 성벽 위를 오가게 했다. 에디터 플레이 모드에서 240프레임 평균을 쟀다.

| 유닛 수 | 회피 품질 | 한 프레임 전체 | 그중 길찾기 | 그중 물리 |
|---|---|---|---|---|
| 100 | 높음 | 1.3 ms | 0.27 ms | 0.12 ms |
| 300 | 높음 | 2.0 ms | 0.50 ms | 0.18 ms |
| 500 | 높음 | 2.9 ms | 0.73 ms | 0.27 ms |
| 500 | 낮음 | 2.9 ms | 0.64 ms | 0.29 ms |
| 1000 | 낮음 | 6.6 ms | 1.26 ms | 0.56 ms |

- 60프레임 기준 한 프레임 예산은 16.7 ms다. 유닛 500개의 이동과 길찾기는 그중 3 ms 정도다.
- 길찾기 전체 다시 만들기: 처음 22 ms, 성벽 조각 하나를 없앤 뒤 11 ms. 비동기로 돌리면 4프레임에 걸쳐 10 ms다.
- 유닛이 경사로로 성벽 위에 올라가는 것을 확인했다.

**결정:** 유닛은 일반 게임오브젝트 + NavMeshAgent로 만든다. 성벽이 생기거나 부서지면 길찾기를 비동기로 다시 만든다. 더 복잡한 구조(DOTS 등)는 쓰지 않는다.

**이 측정에 빠진 것:** 화면 그리기 비용과 캐릭터 애니메이션 비용은 재지 못했다. 캡슐은 애니메이션이 없어서, Synty 캐릭터 수백 명이 움직일 때의 비용은 따로 재야 한다. 실제 병목은 이쪽일 가능성이 높다. Synty 팩을 넣은 뒤 같은 테스트에 캐릭터 모델을 붙여 다시 잰다.

#### B-2 결과 (2026-11-11, 완료)

- `CombatPrototype` 씬에 플레이어와 허수아비 3개, 바닥과 충돌 확인용 벽을 만들었다. 씬과 프리팹, 수치 에셋과 임시 재질은 Unity API로 생성했다.
- WASD 이동, 마우스 3인칭 시점, 좌클릭 전방 근접 공격. Esc로 커서를 풀고 좌클릭으로 다시 잡는다. 점프·방어·방향별 공격은 없다.
- 플레이어와 허수아비는 기존 `Character`와 `HeroDefinition`을 공유한다. 체력 규칙은 일반 C# `Health`, 씬 연결과 공격 처리는 `CharacterActor`, 입력은 `PlayerInputController`, 외형은 `Visual` 자식의 `PlaceholderCombatView`로 나뉜다.
- 기본 공격은 전방의 가장 가까운 대상 하나에 선딜레이 뒤 공격력만큼 피해를 준다. 재공격 대기, 벽 너머 공격 차단, 사망 시 공격 취소와 이동 중지를 넣었다. 임시 체력 100, 공격력 25여서 허수아비는 네 번 맞히면 죽는다. 팀 구분과 방어력 공식은 B-3 작업이다.
- 카메라는 위아래 회전 범위를 제한하고 장애물 앞에서 플레이어 쪽으로 당겨진다.
- **검증:** 컴파일 오류 없음. EditMode 67개, PlayMode 6개 통과(Explicit 성능 테스트 제외). 선딜레이·재공격 제한·사망·후방·사거리·벽 가림, 이동 속도·대각선 정규화·벽 충돌, 카메라 회전 제한·벽 회피를 확인했다. 실제 입력 경로로 이동과 공격을 실행하고 게임 화면의 허수아비 사망 표시와 프레임 진행을 확인했다.
- 수치 에셋과 씬 구성, 실행 방법, 각 코드 역할은 [이 문서의 코드 지도](#2-코드씬에셋-지도)에 정리했다.

#### B-3 결과 (2026-11-11, 완료)

- `CharacterActor`가 영웅과 잡졸의 `StatSheet`를 공유 경로로 사용한다. 영웅은 기존 `Character`를 유지하고, 잡졸은 `SoldierDefinition`으로 스탯을 만든다. 팀을 구분하고 죽은 유닛은 대상 후보에서 제외한다.
- 일반 C# `DamageRules`로 `최대(최소 피해, 공격력 − 방어력)`을 계산한다. 현재 최소 피해 에셋 값은 1이다. 기존 허수아비 프리팹도 적 팀으로 갱신해 B-2 동작을 유지했다.
- `UnitCombatAI`는 NavMeshAgent로 이동하고, 가장 가까운 추격 가능한 적을 잡는다. 가까운 다른 적이 생겨도 대상을 유지하고, 사망하거나 완전한 경로가 없으면 다시 고른다. 막힌 사선에서는 접근을 계속하며, 사망하면 에이전트도 멈춘다.
- `CombatProjectile`은 발사 시 정한 직선으로 이동한다. 아군은 통과하고 첫 적이나 벽에 닿으면 사라진다. 시작점 겹침과 프레임 사이 이동 구간을 모두 검사한다. 사거리 또는 수명이 끝나면 사라진다. 근접·원거리 모두 같은 피해 공식을 사용한다.
- `UnitBattlePrototype`은 관전용 검증 씬이다. 아군 10명과 적 10명이 각각 근접 7명·원거리 3명으로 구성되어 중앙 장애물을 돌아 싸운다. 아군에 더 높은 스탯을 준 임시 구성이다. 기존 `CombatPrototype`에서는 플레이어 직접 조작을 계속 시험할 수 있다.
- **검증:** EditMode 73개, PlayMode 15개 통과(Explicit 성능 테스트 제외). 차감식과 최소 피해, 잡졸 스탯 변경 반영, 아군 피해 방지, 대상 유지·사망 및 추격 불가 시 변경, 투사체 아군 통과·회피·얇은 벽 충돌, 사망 후 에이전트 정지, 실제 10 대 10 전투에서 높은 스탯 쪽의 승리를 확인했다. 에디터에서 실제 프레임·시간 진행과 전투 화면도 확인했고 새 콘솔 오류는 없었다.
- 부대 지시(B-5), 성벽별 적 행동 조합(B-6), 웨이브(B-7), 사기(B-8), 최종 승패 판정(B-9)은 아직 연결하지 않았다. 현재 씬은 살아남은 유닛이 더 이상 대상을 찾지 못하면 멈춘다.

### 2026-11-11: B-4 건설 기초와 직접 검증 흐름

- 협업은 아직 시작하지 않았고 사용자가 혼자 개발 중임을 확인했다. 세 명의 분업은 미래 계획으로 남겼다.
- 사용자 결정: 특정 Synty 팩을 기다리지 않고 범용 데이터 구조로 진행. 계단 직접 배치와 독립 설치 허용, 성벽 붕괴 시 낙하와 높이별 피해, 살아 있는 유닛과 겹친 설치 거부, 격자 반투명 미리보기와 좌클릭 설치·우클릭 취소·R 회전.
- 추가 확인: 참고 이미지처럼 격자 두 축이 대각선으로 보이는 45도 방향 직교 카메라. 현재는 바닥 2D 격자를 유지하고 적층·공중 옆면 확장은 구현하지 않는다. 향후 성벽 위 수성 무기 설치를 위해 바닥 점유와 설치 지점을 분리한다.
- `BuildLayout`의 점유·회전·경계·고정 성문 영역, 조각 정의·체력, `ConstructionWorld`의 배치·파괴·비동기 NavMesh 갱신, 독립 계단 연결 안내와 반투명 미리보기를 구현했다. 조각 몸통은 단위 크기 프리팹에 데이터의 크기를 적용하고 외형은 Visual 자식에 둔다.
- `UnitFallController`가 NavMeshAgent·CharacterController의 낙하·피해·착지·이동 복구를 처리한다. 시험 수치는 안전 높이 1m, 미터당 피해 12이며 최종 밸런스가 아니다. 실제 시험에서는 3m 성벽의 NavMesh 위에서 떨어져 약 25 피해가 들어가 체력 100→75를 확인했다.
- `ConstructionPrototype` 씬에 성벽·계단·고정 성문과 시험 유닛, uGUI·TMP 조각 선택·상태·이동·붕괴 시험 버튼을 만들었다. TMP 필수 리소스를 가져왔으며 시험 한글 HUD는 시스템 맑은 고딕을 사용한다.
- 성벽 정의에 예시 `top` 설치 지점과 크기·회전을 반영한 월드 위치 조회를 추가했다. 실제 수성 무기·슬롯 점유·지지 조각 파괴 처리는 B-4.6 후속이며 아직 완료가 아니다.
- 검증: EditMode 88개, PlayMode 21개 통과. 미리보기·취소의 무영향, 회전 점유·중복 거부·겹친 유닛의 윗면 이동·성문 영역, 계단을 통한 실제 이동, 파괴·낙하 피해와 두 이동 몸통의 복구, 연속 NavMesh 변경을 확인했다. UI 선택·미리보기·설치를 Input System 경로로 확인했고 게임 프레임 진행·화면·콘솔을 점검했다. Windows 빌드 성공.
- 빌드 과정에서 URP 전역 설정과 PlayerSettings의 Input System 사전 로드 참조가 자동 갱신됐다. 기존 씬 변경을 덮어쓰지 않았고, 커밋은 하지 않았다.
- 사용자 검증 요청에 따라 앞으로 완료 보고에 정확한 씬·테스트 경로, 조작 순서와 기대 결과, 초기화 방법을 필수로 안내하도록 AGENTS·이 문서의 개발 방법에 반영했다.

직접 검증: `Assets/_Project/Scenes/ConstructionPrototype.unity` 열기 → Play → 성벽/계단 선택 → 마우스로 이동 → R 회전 → 좌클릭 설치 → 같은 자리의 빨간 미리보기·설치 거부 확인 → 우클릭 취소. 이어 `성벽 위로 이동` → 올라간 뒤 `발밑 조각 파괴` → 지면 낙하·체력 감소 확인. Play 종료 후 다시 실행하면 초기화된다.

자동 검증 재실행: Unity `Window > General > Test Runner`에서 EditMode의 `BuildLayoutTests`와 PlayMode의 `ConstructionPlayTests`를 선택해 Run Selected. 전체 회귀 검증은 각 탭의 Run All로 실행한다. CLI는 이 문서 6장의 테스트 명령에 `--filter BuildLayoutTests` 또는 `--filter ConstructionPlayTests`를 붙인다.

### 2026-11-11: 건설 카메라 각도·회전·트랜지션 보완

- 사용자 피드백에 따라 내려다보는 각도를 45→30도로 낮추고 직교 크기를 23→20으로 조정했다. 대각선 초기 방향 45도와 직교 방식은 유지한다.
- Q/E 좌우 90도 회전을 추가했다. 카메라만 격자 중심을 돌며, R 조각 회전과 분리했다. 네 방향에서 픽킹 좌표·기존 배치·길찾기 상태가 유지된다.
- 즉시 각도를 바꾸던 처리를 SmoothStep 전환으로 고쳤다. 기본 90도당 0.3초이며 에셋에서 조정한다. 연속·반대 입력은 현재 화면 위치에서 이어지고, 누적 회전량에 비례해 전환 시간을 늘린다. 일시정지와 무관한 실제 시간을 사용한다.
- 검증: 컴파일 오류 없음, PlayMode 전체 23개 통과. 건설 관련 8개에는 네 방향의 배치 좌표 불변, 입력 즉시 점프 없음, 중간 각도·연속·역방향 입력과 최종 각도를 포함한다. 초기에 프레임 수로 제한한 테스트가 실제 시간 전환보다 먼저 끝나던 것을 실제 시간 기준 대기로 수정했다.
- 직접 검증: `ConstructionPrototype` → Play → Q/E를 한 번씩 눌러 부드러운 회전 확인 → 전환 중 연속 입력·반대 입력 → 성벽 뒤쪽에서 미리보기·설치 확인. Play 종료 후 다시 실행하면 초기 시점이다. 이번 카메라 변경 후 Windows 빌드는 다시 만들지 않았다.

### 2026-11-11: Q/E 회전 방향 수정

- 사용자 기대에 맞춰 Q/E 방향을 반전했다. 화면 속 판 기준으로 Q 왼쪽, E 오른쪽이다. 부드러운 전환·회전 중 재입력과 R 조각 회전은 유지한다.
- 컴파일 오류 없음. 카메라 관련 PlayMode 2개 통과(4방향·픽킹 좌표 불변, 보간·연속·역방향 입력). 전체 테스트와 빌드는 이 작은 입력 방향 변경에서 반복하지 않았다.
- 직접 검증: `ConstructionPrototype` → Play → Q로 왼쪽 회전, E로 오른쪽 회전 확인. 시점 전환 중 반대 키를 눌러도 부드럽게 이어져야 한다.

### 2026-11-11: B-4.6 수성 무기 공통 구조·설치·동반 파괴

- 사용자 결정: 건설 조작은 현재대로 유지. 받침 성벽이 부서지면 위의 대포도 함께 파괴. 수성 무기는 나중에 다양한 종류를 추가할 수 있게 확장성 있게 만든다.
- 대포 전용 클래스 없이 `SiegeWeaponDefinition`(데이터)·`MountLayout`(점유 규칙)·`PlacedSiegeWeapon`(공통 몸통)·`ISiegeWeaponModule`(무기별 동작 자리)로 나눴다. 대포는 에셋과 임시 프리팹 하나다.
- `ConstructionWorld`에 설치·철거·동반 파괴와 `WeaponRemoved` 이벤트, `PlacementController`에 무기 미리보기·설치, 시험 HUD에 `대포`·`무기 받침 성벽 파괴` 버튼을 추가했다. 버튼이 늘어 시험 패널의 간격과 버튼 높이를 줄였다.
- 범위 밖(Q-21 미정): 발사·피해·조작 주체·자체 체력·설치 방향 회전·통로 차단·비용. 현재 대포는 쏘지 않고 충돌체가 없다.
- 검증: 컴파일 오류 없음. EditMode 92개, PlayMode 25개 전체 통과. 에디터 Play에서 게임 화면을 캡처해 대포가 성벽 위에 놓이는 것과 패널 배치를 확인했고, `무기 받침 성벽 파괴` 호출로 조각 7→6개·무기 1→0개를 확인했다. 마우스로 직접 클릭하는 조작은 사용자 확인 대상이다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증(B-4.6): `Assets/_Project/Scenes/ConstructionPrototype.unity` → Play → `대포` → 성벽 위를 가리켜 초록 미리보기 확인 → 좌클릭 설치 → 같은 성벽을 다시 가리키면 빨강 → 계단·바닥도 빨강 → `무기 받침 성벽 파괴`로 성벽과 대포가 함께 사라지는지 확인. Play를 다시 시작하면 초기화된다.

### 2026-11-11: B-5.1~5.3 부대 지휘 (평지 시험)

- 사용자 결정: 지휘 키는 우선 F1~F4 선택·` 전체 선택·1 이동·2 공격·3 중지로 하고 불편하면 바꾼다(Q-11). 이동은 적을 무시하고 도착하면 중지, 중지는 자리를 지키며 닿는 적만 공격, 공격은 적이 없으면 지시를 유지한 채 대기, 기본 상태는 중지(Q-22).
- 일반 C# `SquadCommander`·`SquadFormation`으로 선택·지시·대형을 만들고, `UnitCombatAI`에 `UnitDirective`를 더해 지시별 행동을 연결했다. 지시를 받지 않은 유닛(적, 관전 전투)의 행동은 그대로다. `SquadCommandCenter`가 지시를 유닛에 옮기고 `SquadCommandInput`이 키와 화면 가운데 조준점을 처리한다. 키는 `SquadCommandSettings` 에셋의 `InputAction`이라 인스펙터에서 바꾼다.
- `SquadCommandPrototype` 씬과 시험 HUD를 Unity API로 만들었다. 빌드 목록 끝에 추가했고 첫 씬은 바꾸지 않았다.
- 범위 밖: 성벽 위 지휘와 경로가 끊겼을 때 처리(B-5.4, Q-23), 부대장 영웅(Q-05), 정식 HUD.
- 검증: 컴파일 오류 없음. EditMode 101개, PlayMode 29개 전체 통과. 에디터 Play에서 지시를 코드로 내려 이동과 HUD 표시를 화면 캡처로 확인했다. 실제 키를 누르는 조작은 자동 검증하지 않았다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `Assets/_Project/Scenes/SquadCommandPrototype.unity` → Play → F1 → 화면 가운데 점을 땅에 맞추고 1 → 1번 부대만 이동하고 도착하면 `중지`로 표시 → F2 → 2 → 2번 부대가 적을 찾아가 싸움 → ` → 3으로 전체 중지. 왼쪽 위 표시에서 선택(▶)·지시·남은 인원을 확인한다. Play를 다시 시작하면 초기화된다.
- 자동 검증 재실행: Test Runner에서 EditMode `SquadCommanderTests`, PlayMode `SquadCommandPlayTests`.

### 2026-11-11: 공격 지시 변경·시야·지휘 표시

- 사용자 결정: 갈 수 없는 목적지는 갈 수 있는 가장 가까운 곳에서 멈춘다(Q-23). 공격 지시는 가리킨 지점으로 가면서 발견한 적을 쫓아가 싸운다(Q-22 변경). 선택한 부대 유닛 발밑의 임시 원판과 목적지 표시를 지금 넣는다. 외곽선은 아트 단계로 미룬다.
- `SquadOrder`에 목적지 유무를 두고, `UnitCombatAI`의 Engage가 목적지를 받으면 "전진하며 교전"이 되게 했다. 길이 전혀 없는 이동은 제자리에서 끝낸다. 지휘 씬 아군에 시야 15m 에셋을 따로 연결했다.
- `PlaceholderSquadMarkers`로 유닛별 선택 원판과 목적지 원판을 추가했고, 죽은 유닛의 임시 체력 막대를 숨겼다.
- 수정한 실수: 지휘 씬을 처음 만들 때 적의 시야 12m 설정이 씬에 저장되지 않아 적이 시작하자마자 돌진했다. `SerializedObject`로 다시 저장했고, 적이 처음에 제자리에 있는지 확인하는 검증을 PlayMode 테스트에 넣었다.
- 검증: 컴파일 오류 없음. EditMode 101개, PlayMode 31개 전체 통과. 에디터 Play 화면 캡처로 유닛별 노란 원판·청록색 목적지 표시와 적이 멀리 서 있는 것을 확인했다. 실제 키 입력은 자동 검증하지 않았다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `SquadCommandPrototype` → Play → F1 → 1번 부대 유닛마다 노란 원판 확인 → 화면 가운데를 적 쪽 먼 땅에 맞추고 2 → 청록색 표시 지점으로 가다가 적이 15m 안에 들어오면 싸움 → F2 → 화면 가운데를 가까운 땅에 맞추고 1 → 2번 부대는 싸우지 않고 이동, 도착하면 표시가 사라지고 `중지`.

### 2026-11-11: B-5.4 성벽 위 지휘와 1인칭 시험 시점

- `WallCommandPrototype`에 기존 건설·부대 지휘를 연결했다. 시험용 3개 부대×4명이며 건설 화면의 이동·파괴 버튼은 1번 부대의 실제 지시와 첫 유닛을 사용한다. 기존 건설·전투·평지 지휘 씬을 덮어쓰지 않았다.
- 사용자 요청에 따라 1인칭 카메라를 추가했다. Tab으로 건설↔1인칭 지휘를 전환하고 지형·지시를 유지한다. 기본 게임 시점까지 바꾸는 범위는 Q-24 확인 중이다. 정식 준비·전투 상태 전환은 후속이다.
- 벽 옆면 조준·잘못된 층 선택을 막았다. 이동 경로를 명시적으로 계산해 부분 경로 끝에서 멈추고, 목적지 성벽이 사라졌으면 주변 NavMesh를 찾아 다시 계산한다. 낙하 중에는 AI가 기다리고 착지 후 같은 지시를 이어간다.
- 검증: 컴파일 오류 없음. EditMode 101개, PlayMode 전체 40개 통과. 새 9개는 조준·경로 갱신·낙하 5개와 통합 씬 4개다. 에디터 Play에서 프레임 진행을 확인하고 건설·1인칭 화면 및 부대의 성벽 도착을 캡처했다. Input System에 Tab 입력을 넣어 실제 전환 액션이 동작함을 확인했다. 검증 중 새 런타임 오류 없음. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `Assets/_Project/Scenes/WallCommandPrototype.unity` → Play → `성벽 위로 이동` → Tab → 1번 부대가 계단을 올라 성벽에 도착하면 중지 표시 → F2로 2번 부대 선택 → 마우스로 땅 또는 성벽 윗면을 가리켜 1 → Tab으로 건설 시점 복귀 → `발밑 조각 파괴` → 1번 부대원의 낙하·체력 감소 확인. Q/E 회전과 건설도 같은 씬에서 시험한다. 중지 후 다시 Play하면 전부 초기화된다.
- 자동 검증 재실행: `Window > General > Test Runner` → PlayMode → `WallCommandPlayTests` 또는 `ConstructionPlayTests` → Run Selected. 전체는 EditMode·PlayMode 각각 Run All. CLI 명령은 6장 테스트 방법에 해당 이름을 `--filter`로 붙인다.

### 2026-11-11: 게임 기본 시점을 1인칭으로 확정

- Q-24 사용자 답변: 시험 씬에 한정하지 않고 게임 기본 플레이 시점을 1인칭으로 변경한다. GameDesign·ImplementationPlan·AGENTS에 반영했다. 건설은 기존 직교 시점이다.
- 기존 `CombatPrototype`과 `SquadCommandPrototype`의 카메라를 Unity API로 교체하고 플레이어 입력 참조를 연결했다. 카메라 GameObject의 Camera·AudioListener·지휘 조준 참조는 유지했다. 두 씬의 생성 코드도 1인칭으로 바꿨다.
- 기존 카메라 검증을 1인칭 눈높이 추적·상하 각도 제한·자기 몸이 시야를 가리지 않는지 검증하도록 변경했다. EditMode 101개, PlayMode 전체 40개 통과. 전투 씬의 실제 프레임 진행과 1인칭 화면을 확인했고 새 런타임 오류는 없었다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `Assets/_Project/Scenes/CombatPrototype.unity` → Play → WASD 이동·마우스 시점·허수아비 가까이서 좌클릭 공격. 눈높이에서 보이고 자기 캡슐이 화면을 가리지 않아야 한다. Esc로 커서 해제. 종료 후 다시 Play하면 초기화된다. 부대 지휘는 `Assets/_Project/Scenes/SquadCommandPrototype.unity`에서 F1~F3 선택·1 이동·2 공격·3 중지를 확인한다.

### 2026-11-11: 협업 시작과 G-0 게임 진입·흐름 뼈대

- 사용자 결정: 협업 시작(팀원은 적 침공·전투 단계, 사용자는 진입 씬과 흐름 뼈대, 별도 PC·브랜치). 실제 게임 씬은 타이틀과 게임 두 개이며 준비·전투·보상·결과는 게임 씬 안에서 단계만 바꾼다. 기존 `*Prototype` 씬은 검증용 데모로 남긴다.
- 추가: `ProjectNeo.Flow`의 흐름 규칙·흐름 보관처·씬 목록·씬 전환·게임 씬 연결, `TitleScreen`·`GameFlowHud`·`RuntimeSystemFont`, `GameFlowSetup`, `Title`·`Game` 씬과 `SceneCatalog.asset`. 두 씬은 빌드 목록 끝에 더했고 첫 씬은 바꾸지 않았다. 기존 코드·씬은 고치지 않았다.
- 검증: 컴파일 오류 없음. EditMode 109개(새 8개), PlayMode 전체 43개(새 3개) 통과. 에디터 Play 화면의 배치·한글 표시·실제 버튼 클릭은 자동으로 확인하지 않았고 사용자 직접 확인 대상이다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Title.unity` → Play → `새 게임` → 게임 씬에서 `준비 · 스테이지 1` 표시 → `전투 시작` → `승리 (자리표시)` → `보상 받고 다음 스테이지` → `준비 · 스테이지 2` → `전투 시작` → `패배 (자리표시)` → `타이틀로` → 타이틀 화면 복귀 → 다시 `새 게임`을 누르면 스테이지 1부터다. `Assets/_Project/Scenes/Game.unity`만 열고 Play해도 스테이지 1 준비로 시작한다. Play를 멈추면 초기화된다.
- 자동 검증 재실행: Test Runner → EditMode `GameFlowTests`, PlayMode `GameFlowPlayTests`. CLI는 6장 명령에 `--filter GameFlow`를 붙인다.

### 2026-11-11: G-1 게임 씬에 성·건설·지휘 연결

- `WallCommandPrototype`의 성(격자·성벽·계단·성문)·건설 카메라와 배치·플레이어·부대 3개×4명·지휘를 Unity API로 `Game.unity`에 옮기고 `GamePhaseView`로 단계에 연결했다. 프로토타입 씬과 기존 코드는 고치지 않았다. 맵 크기·성 배치·부대 구성은 프로토타입의 임시 값 그대로다.
- 준비는 건설 시점, 전투는 1인칭 조작·지휘다. "준비 중에는 걸어 다니지 않고 전투 중에는 건설하지 않는다"는 프로토타입 구성을 따른 임시 동작이며 승인된 기획이 아니다. 다음 스테이지 준비로 넘어가도 유닛 위치·체력·성벽 상태는 초기화하지 않는다(이월·보충 규칙은 C-3).
- 건설 화면은 시험용 `ConstructionHud`를 재사용했다. 이 화면이 `ConstructionSandbox`를 참조하므로 그 오브젝트는 남기고 시험 버튼 세 개와 시험 유닛 체력 표시만 숨겼다. 정식 준비 화면은 D-2다.
- 검증: 컴파일 오류 없음. EditMode 109개, PlayMode 전체 44개(새 1개) 통과. 에디터 Play에서 준비 화면(건설 패널·단계 패널·성·부대)과 전투 화면(1인칭·지휘 표시·승리/패배 버튼)을 캡처해 한글과 배치를 확인했다. 실제 마우스·키 입력으로 하는 건설·이동·지휘·버튼 클릭은 사용자 직접 확인 대상이다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → 왼쪽 `성벽` 선택 후 격자에 좌클릭 설치, Q/E 회전 → 위쪽 `전투 시작` → 1인칭으로 바뀌면 WASD·마우스, F1 → 땅을 보고 1로 부대 이동 → Esc로 커서 해제 → `승리 (자리표시)` → `보상 받고 다음 스테이지` → 건설 시점으로 돌아오고 방금 지은 성벽과 옮긴 부대가 그대로 → `전투 시작` → Esc → `패배 (자리표시)` → `타이틀로` → 타이틀 → `새 게임`을 누르면 성과 부대가 처음 상태다.

### 2026-11-11: 게임 씬 기본 성 배치

- 사용자 요청으로 기본 성을 성답게 바꿨다. 격자 24×26칸, 14×14칸 외벽, 북쪽 메인 성문, 안쪽 계단 세 개, 비워 둔 안뜰 12×12칸, 북쪽 들판. 부대는 계단 아래와 안뜰 가운데, 플레이어는 성문 위에서 북쪽을 보고 시작한다. 게임 전용 격자·건설 카메라 에셋을 만들어 프로토타입 씬과 공유 에셋은 바꾸지 않았다.
- 적용 전 에디터의 Game 씬에 저장되지 않은 변경이 있었고 사용자 확인 후 버렸다.
- 검증: EditMode 109개, PlayMode 전체 45개(새 1개) 통과. 에디터 Play에서 준비 화면의 성 전체 모습과 전투 화면의 성문 위 시점을 캡처해 확인했다. 실제 입력 조작과 Windows 빌드는 확인하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → 외벽으로 둘러싸인 성과 안뜰의 부대 셋이 보인다 → Q/E로 돌려 남쪽 계단 확인 → 안뜰에 `성벽`을 놓아 본다 → `전투 시작` → 성문 위에서 북쪽 들판을 본다 → F1 선택 후 성벽 윗면을 보고 1 → 1번 부대가 계단으로 올라온다.

### 2026-11-11: 층 쌓기(Q-27 A안)와 2단 계단

- 사용자 결정: A안(지금 격자 + 층 쌓기), 2단 계단은 1층 성벽 위에 놓아 2층으로, 아래가 부서지면 위는 한 층 내려앉음, 층은 가리킨 칸 위에 자동. GameDesign 성벽 절과 로드맵 Q-27·Q-18에 반영했다.
- 변경: `BuildLayout`(층 점유·`Settle`), `BuildGridDefinition.MaxLevels`, `ConstructionWorld`(높이 반영·내려앉기·`PieceDropping`), `MountLayout`(덮인 성벽 금지·`HasWeaponOn`), `PlacementController`(조각을 가리켜 칸 고르기·미리보기 높이), `UnitFallController`(내려앉는 조각 위 유닛 낙하), `ConstructionHud`(층 표시와 받침 높이 안내). 게임 씬 격자를 3층으로 설정했다. 프로토타입 씬과 에셋은 바꾸지 않았고 기본 1층이라 기존 테스트가 그대로 통과한다.
- 검증: 컴파일 오류 없음. EditMode 118개(새 9개), PlayMode 전체 47개(새 2개) 통과. 에디터 Play에서 2층·3층 성벽과 2단 계단을 놓은 준비 화면을 캡처해 확인했다. 마우스로 성벽을 가리켜 쌓는 실제 조작, 내려앉는 조각 위 유닛의 낙하, Windows 빌드는 확인하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → `성벽` 선택 → 북쪽 성벽 조각을 가리키면 그 위에 초록 미리보기와 `2층` 표시 → 좌클릭으로 서쪽 모서리와 그 옆에 2층을 쌓는다 → `계단` 선택, R로 돌려 1층 성벽 두 칸 위에 2층 쪽으로 오르게 놓는다(연결 안내가 사라지면 이어진 것) → 같은 칸에 세 번 쌓으면 네 번째는 빨강 → `전투 시작` → F1 후 2층 윗면을 보고 1 → 부대가 땅 계단과 2단 계단으로 올라온다. 내려앉기는 지금 게임 씬에 부수는 수단이 없어 자동 테스트 `StackedConstructionPlayTests`로 확인한다.

### 2026-11-11: 성문 위 건설과 겹친 유닛의 윗면 이동

- 사용자 결정: 성문 위 성벽 설치 허용. 전투 중 건설 불가. 플레이어는 안뜰에서 시작한다. 준비 단계 기본도 1인칭 안뜰 이동이며 각 시설 건물에 찾아가 준비하고, 건설 담당 건물에서 선택했을 때만 건설 화면을 연다. 건물·상호작용·준비 중 직접 이동은 정식 준비 화면 작업에서 붙인다. 이번에는 현재의 준비 즉시 건설 화면 전환을 미연결 임시 구현으로 유지한다.
- 사용자 변경: 유닛과 겹쳐도 설치하고, 겹친 살아 있는 유닛은 새 조각 윗면으로 옮긴다. 이전의 유닛 겹침 설치 거부를 대체한다. 건설 이동 자체에는 피해를 주지 않는다.
- 변경: `BuildLayout`은 성문의 빈 바닥 자리만 보호하고 위 적층을 허용한다. `ConstructionWorld`는 설치 후 겹친 유닛을 조각 윗면(계단은 실제 발판)으로 옮긴다. `UnitFallController.LiftForConstruction`으로 낙하 피해 없이 이동하고 새 NavMesh를 기다린 뒤 이동을 복구한다. 유닛 중심이 가장자리에 걸친 경우 조각 안쪽으로 보정한다. `GameFlowSetup`과 `Game.unity`의 플레이어 시작 위치를 안뜰(12, 8)로 옮겼다. 씬은 Unity 에디터 API로 저장했다.
- 검증: 컴파일 오류 없음. EditMode 122개, PlayMode 전체 49개 통과. `BuildStackingTests`의 성문 4방향 적층·붕괴, `StackedConstructionPlayTests`의 성문 위 미리보기·설치·성문 파괴 후 내려앉기와 길 갱신, 플레이어와 병력 2명의 겹친 건설·2층 재적층·피해 없음·이동 복구·전투 진입 시 건설 비활성화, `ConstructionPlayTests`의 겹친 병력 이동을 검증했다. 실제 마우스 입력 플레이와 Windows 빌드는 확인하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity`를 열고 Play → `성벽` 선택 → 북쪽 성문 위를 가리켜 초록 미리보기 확인 → 좌클릭으로 설치한다. 안뜰 남쪽 플레이어나 부대가 선 칸에도 놓아 본다 → 설치가 허용되고 해당 유닛이 성벽 위로 올라가며 체력이 유지된다 → `전투 시작` → 안뜰에서 출발한 플레이어를 WASD로 움직이고 F1 후 성벽 위를 보고 1로 지휘한다. 전투 중에는 건설 화면이 사라져야 한다. Play 종료 후 다시 Play하면 초기화된다.
- 자동 재검증: Test Runner의 EditMode에서 `BuildStackingTests`, PlayMode에서 `StackedConstructionPlayTests`와 `ConstructionPlayTests`를 실행한다. 성문 붕괴는 게임 씬에 직접 파괴 버튼이 없어 해당 PlayMode 테스트로 확인한다.
### 2026-11-11: 수성전 프로토타입 (준비 단계·적 침공·사다리·승패)

- 사용자 결정(Q-28·Q-29): 준비 단계에서 건설·인원 배치 후 전투 시작, 전투 중 건설 불가. 근접 적은 성벽 위 아군을 못 때리고 뚫린 곳·무너진 성문으로 들어오며, 없으면 성문 파괴 또는 사다리. 사다리는 5칸당 하나·고정 자리·세우기 3초·한 명씩·아군이 밀어냄. 적 궁수의 위쪽 사격은 피해 절반. 적은 사다리병·성문 파괴병·궁수.
- `ProjectNeo.Siege` 폴더에 규칙·사다리·적 판단·준비 도구·진행을 추가했다. 기존 코드는 작게 확장했다: `PlacementController`의 설치 전 조건과 설치 알림, `ConstructionWorld.TryDismantle`, `UnitFallController.BeginFall(bool)`, `CombatProfile`의 위쪽 사격 비율과 `DamageRules.ElevationMultiplier`. 기본값에서는 기존 씬의 동작이 같다.
- 근접 적이 성벽 위를 못 때리는 것은 별도 규칙 없이 근접 사거리 1.5m와 성벽 높이 3m로 성립한다. 사거리 수치가 바뀌면 다시 확인해야 한다.
- 범위 밖: 사기·도망, 대포 발사(Q-21), 여러 웨이브, 보상, 일반 성벽 파괴 적, 비행 적, 승패 동시 성립 우선순위(Q-09, 지금은 패배 우선으로 임시 처리).
- 검증: 컴파일 오류 없음. EditMode 110개, PlayMode 전체 48개 통과(새 EditMode 9개, PlayMode 8개). 에디터 Play에서 준비 화면 캡처, 전투 시작 후 1인칭 고정, 적 수 감소, 플레이어 사망에 따른 패배 표시를 확인했다. 새 런타임 오류는 없었다. 마우스 클릭 설치·배치·철거, Tab·F·R 키 입력은 자동 검증하지 않았고 사용자 확인 대상이다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `Assets/_Project/Scenes/SiegePrototype.unity` → Play → `성벽`으로 뚫린 칸을 막거나 그대로 두기 → `1번 부대` 버튼 후 성벽 위 클릭으로 배치 → `전투 시작`. 뚫린 곳이 있으면 적이 그리로 들어오고, 다 막으면 사다리를 세우고 성문을 친다. 사다리 위쪽 끝 근처에 부대를 두면 사다리를 밀어낸다. 결과가 나오면 R. Play를 다시 시작해도 초기화된다.
- 자동 검증 재실행: Test Runner에서 EditMode `SiegeRulesTests`, PlayMode `SiegePrototypePlayTests`.

### 2026-11-11: 수성전 프로토타입 편성 데이터화·죽은 적 표시 수정

- 사용자 요청: 죽은 사다리병의 널빤지가 남는 문제 수정, 아군 부대 수와 적 무리를 바꿀 수 있게 하기.
- 아군·적을 씬에 미리 두던 방식을 없애고 `SiegeDirector`가 시작할 때 `Siege.asset`의 편성대로 만든다. `SquadCommandCenter.ConfigureRosters`로 편성을 넘긴다. 적은 종류별 프리팹 변형으로 분리했다. 준비 화면의 부대 버튼은 실제 부대 수만큼 보이고 인원 수를 표시한다.
- 종류 표시에 `PlaceholderDeathHider`를 붙여 죽으면 숨긴다.
- 생성한 씬과 `Siege.asset`은 지우고 다시 만들었다. 다른 씬은 건드리지 않았다.
- 검증: 컴파일 오류 없음. EditMode 110개, PlayMode 전체 50개 통과(편성이 에셋과 같은지, 죽은 적의 표시가 숨는지 2개 추가). 에디터 Play에서 준비 화면을 캡처해 부대 버튼과 패널이 잘리지 않는 것을 확인했다. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `Assets/_Project/Data/SiegePrototype/Siege.asset`을 고르고 인스펙터의 `Ally Squads`·`Enemy Groups`에서 숫자를 바꾸거나 줄을 추가 → `SiegePrototype` Play → 준비 화면의 부대 버튼 수·인원과 `다가오는 적` 숫자가 바뀌었는지 확인. 사다리병을 죽이면 널빤지가 사라진다.

### 2026-11-11: 부대 수에 맞춘 준비 화면

- 사용자 요청: `SquadConfig`의 부대 수 상한을 늘렸는데 부대 배치 버튼이 4개만 나오는 문제 수정.
- `SiegeHud`가 부대 수만큼 버튼을 만들고 패널 크기를 화면에 맞춘다. 화면 높이는 기준 해상도가 아니라 실제 캔버스 높이로 잰다(Game 창 비율에 따라 패널 위쪽이 잘리던 원인). `SiegeDirector`는 추가 부대의 시작 자리와 적 줄 간격을 판 안에 들어오게 잡는다.
- 검증: 컴파일 오류 없음, PlayMode 전체 50개 통과. 부대 7개·적 6무리(28명) 편성으로 에디터 Play 화면을 캡처해 버튼 7개와 패널이 잘리지 않는 것, 아군·적이 판 안에 서는 것을 확인했다. 새 콘솔 오류 없음. 버튼 클릭은 사용자 확인 대상이다.
- 이어서 사용자 요청으로 부대 선택 키를 F1~F10으로 늘리고(이 씬 전용 `Command.asset`), 부대 이름에 종류를 붙였으며, 1인칭 부대 목록 칸을 10부대까지 들어가게 키웠다. 기존 씬에는 메뉴 `ProjectNeo/Setup/Upgrade Siege Prototype`(`SiegePrototypeSetup.Upgrade`)으로 적용했다. 여러 번 실행해도 같다.
- 검증: EditMode 110개, PlayMode 50개 통과. Play 화면 캡처로 버튼과 목록의 `N번 부대 · 근접/궁수` 표시를 확인했고, 6번 부대 선택이 동작하며 키가 10개 등록된 것을 코드로 확인했다. 실제 F5~F10 키 입력은 사용자 확인 대상이다.

### 2026-11-12: 수성전 브랜치 병합 전 인계 기록

아래는 병합 전 브랜치의 변경 목록과 검증 기준을 보존한 기록이다. 현재 병합 검증과 통합 상태는 이 절 다음 기록 및 로드맵 3장을 따른다.

```text
작업 ID / 제목: 수성전 프로토타입 (B-6·B-9 일부를 앞당겨 시험, Q-28·Q-29)
브랜치 / 기준: feature/siege-prototype / main·develop의 8505401
상태: 검증 중 (자동 테스트 통과, 사용자 직접 플레이 진행 중)
필요한 결정: Q-30(적이 조금 남았을 때 아군 처리), Q-09(승패 동시 성립), Q-21(수성 무기 동작)
완료 기준 / 실제 검증 결과: EditMode 110개, PlayMode 50개 통과. Windows 빌드는 다시 만들지 않음
```

**새로 추가한 것(이 브랜치만 소유):** `Scripts/Runtime/Siege/`, `UI/SiegeHud.cs`·`SiegeStatusHud.cs`, `Combat/PlaceholderDeathHider.cs`, `Editor/SiegePrototypeSetup.cs`, `Scenes/SiegePrototype.unity`, `Data/SiegePrototype/`, `Prefabs/SiegePrototype/`, 테스트 `SiegeRulesTests`·`SiegePrototypePlayTests`, 임시 재질 `SiegeLadder`·`SiegeRam`.

**공용 파일에 더한 것(다른 브랜치와 겹칠 수 있음):** 모두 기존 동작을 바꾸지 않는 추가다.

| 파일 | 더한 것 | 기존 씬에 주는 영향 |
|---|---|---|
| `Combat/CombatProfile.cs` | 직렬화 필드 `upwardShotMinimumRise`(1.5)·`upwardShotDamageRatio`(1), `Create(...)` 끝에 선택 인자 2개 | 기본값 1이라 피해 변화 없음 |
| `Combat/CombatProjectile.cs`, `DamageRules.cs` | 발사 높이 기억, `ElevationMultiplier` | 위와 같음 |
| `Squads/SquadCommandCenter.cs` | `Config`, `ConfigureRosters(...)` | 없음 |
| `Squads/SquadCommandSettings.cs` | `ConfigureSelectKeys(...)` | 없음 |
| `UI/SquadCommandHud.cs` | `SquadName(...)`. 부대 목록이 `1번 부대 · 근접`으로 표시됨 | 평지·성벽 지휘 씬의 목록 글자도 바뀜 |
| `Walls/ConstructionWorld.cs` | `TryDismantle(...)`, 조각 제거를 `RemoveBody`로 묶음 | `Collapse` 동작 같음 |
| `Walls/PlacementController.cs` | `ConfirmGate`, `PiecePlaced`, `WeaponMounted` | 비어 있으면 기존과 같음 |
| `Walls/UnitFallController.cs` | `BeginFall(bool)` | 없음 |
| `Data/SquadCommandPrototype/SquadConfig.asset` | `maxSquads` 4 → 10 (사용자 변경) | 평지·성벽 지휘 씬도 같은 에셋을 쓴다. 상한만 늘어 편성은 그대로 |
| `ProjectSettings/EditorBuildSettings.asset` | 목록 끝에 `SiegePrototype` 씬 | 첫 씬은 그대로 |

**건드리지 않은 것:** `CharacterActor.cs`, `UnitCombatAI.cs`, 기존 씬·프리팹, `Packages/`, `.asmdef`. 적의 공성 행동은 `UnitCombatAI`를 고치지 않고 `SiegeAttackerBrain`이 `UnitDirective`로 지시하는 방식이다.

**병합할 때:** `.unity`·`.asset`·`.prefab`은 텍스트로 합치지 않는다. `EditorBuildSettings.asset`이 겹치면 씬 목록을 둘 다 살린다. `SquadConfig.asset`의 상한을 다른 브랜치가 다르게 바꿨으면 사용자에게 묻는다. 병합 뒤에는 EditMode·PlayMode 전체를 다시 돌린다.

### 2026-11-12: develop에 syj와 수성전 브랜치 병합

- 범위: `syj`의 `1a6769e`와 `feature/siege-prototype`의 `dc08986`을 `develop`에 반영했다. `Title`·`Game`과 별도 시험 씬 `SiegePrototype`을 모두 보존한다. 실제 게임 씬에 적·사다리·승패를 연결한 것은 아니다. 그 접점은 후속 통합 작업이다.
- 충돌 해결: Context의 코드 지도·테스트 지도·검증 기록은 양쪽 내용을 살리고, ImplementationPlan의 현재 상태·검증 기준은 병합 상태로 갱신했다. 중복된 결정 ID는 수성전 쪽을 Q-28·Q-29·Q-30으로 바꾸고 참조도 갱신했다. 병합 전 브랜치 인계 내용은 이 장으로 옮겨 보존했다.
- 공통 코드 보정: 자동 병합된 `ConstructionWorld.TryDismantle`에서 점유 제거·NavMesh 재생성 요청·Changed 알림을 복구했다. 적층 조각의 실제 Elevation으로 유닛 겹침을 검사하고, 위층을 공중에 남기지 않도록 덮인 받침의 철거를 거부한다. `StackedConstructionPlayTests.적층_성벽은_맨_위부터_철거하고_점유와_길찾기를_갱신한다`로 회귀를 확인한다.
- 빌드 목록: Unity Editor API로 기존 첫 씬과 Title·Game을 유지하고 SiegePrototype을 끝에 추가했다. 총 9개 씬이다. 씬·프리팹·콘텐츠 에셋은 각 브랜치의 결과를 보존했다.
- 검증: 컴파일 오류 없음. EditMode 131개·PlayMode 전체 60개 통과. 실제 입력 플레이와 Windows 빌드는 이번 병합에서 다시 확인하지 않았다.
- 직접 확인: `Assets/_Project/Scenes/SiegePrototype.unity` → Play → 성벽으로 뚫린 곳을 막거나 부대 배치 → 전투 시작 → 적 진입·성문 공격·사다리와 승패를 확인한다. R 또는 Play 재시작으로 초기화한다. `Assets/_Project/Scenes/Title.unity` → Play → 새 게임 → 준비 건설 화면 → 전투 시작 → 1인칭·F1~F3·1/2/3 지휘와 자리표시 승리/패배 → 보상·타이틀 전환을 확인한다. Play 재시작으로 초기화한다.
- 자동 재검증: Test Runner의 EditMode·PlayMode 전체 실행. 병합 접점은 `SiegePrototypePlayTests`, `StackedConstructionPlayTests`, `GameFlowPlayTests`, `ConstructionPlayTests`에서 확인한다.
### 2026-11-12: 메인 성과 지면 확대·안뜰 임시 시설 배치

- 사용자 승인: 전투 연결 전에 성 96×96m, 안뜰 90×90m, 전체 지면 240×300m로 확대하고 임시 시설·이동로·집결 공간을 둔다. 3m 조각·최대 3층을 유지한다. 북쪽 외벽 밖 여유는 정확히 120m다.
- 변경: `GameFlowSetup.ApplyDefaultCastle`은 게임 전용 격자를 80×100칸으로 갱신하고 외벽을 (24,28)~(55,59)에 배치한다. 북쪽 성문은 (39,59), 계단은 (27,57)·(52,57)·(39,29)다. 플레이어는 (40,34), 부대는 북쪽 집결 공간에서 시작한다. 재실행해도 같은 배치를 만들며 기존 프로토타입 씬은 바꾸지 않았다.
- `Geometry/CourtyardLayout` 아래에 주점·상점·훈련장·대장간·건설소 임시 건물과 십자 이동로·병력 집결 바닥을 둔다. 건물 루트가 충돌을, Visual 자식이 몸통·지붕·한글 간판 외형을 맡는다. 실제 시설 기능·방문 상호작용은 미연결이며, 현재 준비 즉시 건설 화면·전투 버튼으로 1인칭 이동하는 임시 흐름을 유지한다.
- `BuildCameraSettings`와 `ConstructionCameraController`에 게임 전용 탐색 옵션을 추가했다. 중클릭 드래그는 화면 방향으로 이동, 휠은 20~180 범위로 확대축소, Home은 주시점·줌 초기화다. 초기 주시점은 격자 비율 (0.5,0.44), 직교 크기 55, 거리 320m이고 게임 카메라 원거리는 1000m다. 기존 에셋은 탐색 옵션 기본 꺼짐으로 Q/E 회전 동작을 유지한다.
- 검증: PlayMode 전체 61개 통과. 확장 치수·북쪽 120m 여유·시설 5곳 앞까지의 경로·성벽 계단 경로·적층과 붕괴·카메라 이동 제한·확대축소 한계·초기화를 검증했다. 에디터 Play에서 프레임 증가와 건설·1인칭 화면을 캡처해 확인했으며 새 런타임 오류는 없다. 실제 중클릭·휠·Home 입력 검증은 완료하지 않았고 사용자 확인 대상이다. Windows 빌드는 실행하지 않았다. EditMode는 병합 당시 131개 통과 기준이며 이번에는 새 일반 C# 규칙 변경이 없어 다시 실행하지 않았다.
- 직접 확인: `Assets/_Project/Scenes/Game.unity` → Play → 성과 건물 다섯 곳을 확인 → Q/E 회전·휠 확대축소·중클릭 드래그로 북쪽 들판 확인 → Home으로 성 중심 복귀 → `전투 시작` → WASD·마우스로 시설 사이와 중앙 길을 걸어 크기 확인 → F1 후 북쪽 성벽 위를 보고 1로 이동 지시. 적은 아직 Game 씬에 연결하지 않았다. Play 종료 후 재시작하면 원래 배치·시점으로 초기화된다.
- 자동 재검증: Test Runner PlayMode 전체 또는 `GameFlowPlayTests`·`StackedConstructionPlayTests`. 스크린샷은 `Temp/expanded-castle-build.png`·`Temp/expanded-castle-first-person.png`에 있다.
### 2026-11-12: 준비 기본 1인칭·건설소 F 상호작용

- 사용자 요청: 준비 단계도 1인칭으로 시작하고 건설소에서 F로 건설 모드 진입. 건설 종료는 Esc로 구현했으며 플레이어 위치·카메라 방향은 유지한다. 준비/전투 단계 자체는 바꾸지 않고 준비 단계 안에서 시점만 전환한다.
- `GamePhaseView`는 준비 기본 1인칭, `EnterConstruction`·`ExitConstruction`으로 건설 시점을 전환한다. 건설 중 플레이어·지휘 입력을 끄고 커서를 풀며, 종료하면 선택·미리보기를 취소하고 같은 위치에서 1인칭 이동을 복구한다. 전투·보상·결과 진입 시 건설 모드는 닫힌다. 다음 준비도 1인칭으로 시작한다.
- 새 코드: `Flow/PreparationInteractionSettings.cs`(거리와 F/Esc InputAction, Create 제공), `PreparationInteraction.cs`(화면 가운데 가까운 대상 검사·입력 복제·해제), `ConstructionStation.cs`(건설소 몸통의 대상 표시). `UI/GameFlowHud.cs`에 준비 조준점과 안내 문구를 추가했다. 게임 전용 `Data/GameFlow/Interaction.asset`에서 거리(기본 3.5m)·입력을 조정한다. 기존 프로토타입에는 연결하지 않았다.
- 씬 연결: `GameFlowSetup.EnablePreparationInteraction` 메뉴는 기존 Game 씬에 입력·건설소·안내를 연결하며 중복 생성하지 않는다. `ApplyDefaultCastle`도 임시 건설소를 다시 만든 뒤 이를 연결한다. 씬과 에셋·meta는 모두 에디터 API로 만들었다. 다른 시설은 여전히 임시 외형이다.
- 검증: 컴파일 오류 없음. PlayMode 전체 62개 통과. `GameFlowPlayTests.건설소를_가까이_바라보고_F로_열고_Esc로_나오며_전투에서는_열리지_않는다`는 가상 키보드를 통해 실제 InputAction 경로로 F·Esc를 입력하고, 원거리·등 돌림 거부, 안내, 커서·위치 복구, 전투 중 차단을 확인한다. 준비↔건설↔전투↔다음 준비의 시점·입력·부대 유지와 기존 건설·수성전 회귀도 통과했다. EditMode는 기존 131개 통과 기준이며 이번에는 재실행하지 않았다. Windows 빌드는 하지 않았다.
- 화면 확인: Play 프레임 증가, 준비 시작의 1인칭, 건설소 앞 `F · 건설소 이용`, 상호작용 메서드 호출 후 건설 화면과 `건설 모드 · Esc 돌아가기`, 종료 후 1인칭 복귀를 확인했다. CUA 키 주입으로는 전환을 재현하지 못해 물리 키보드의 F·Esc 조작은 사용자 확인 대상으로 남긴다. 자동 InputAction 테스트는 통과했다. 화면은 `Temp/preparation-first-person.png`·`Temp/construction-station-build.png`에 있다.
- 직접 확인: `Assets/_Project/Scenes/Game.unity` → Play → 바로 1인칭으로 시작한다 → WASD·마우스로 시작 위치 오른쪽 앞의 `건설소` 건물까지 이동 → 건물을 가까이서 화면 가운데로 바라보면 `F · 건설소 이용` → F → 성 전체 건설 화면 → 성벽 배치·Q/E·휠·중클릭 → Esc → 원래 자리·방향에서 이동 재개. 먼 곳·다른 건물·등 돌린 상태에서는 F가 동작하지 않아야 한다. 전투 시작 버튼은 Esc로 커서를 풀어 누르며, 전투 중 F로 건설 화면을 열 수 없어야 한다. Play를 종료하고 다시 시작하면 원래 상태다.
- 자동 재검증: Test Runner PlayMode의 `GameFlowPlayTests` 또는 PlayMode 전체. 기존 씬은 `SiegePrototypePlayTests` 등으로 함께 회귀한다.
### 2026-11-12: Game 실제 전투 연결·HUD 겹침 정리

- `Flow/GameBattleController`가 기존 `GameSession.Flow`를 구독해 전투를 시작하고 `FinishBattle`로 결과를 전달한다. 별도 SiegeDirector나 아군 복제 없이 기존 플레이어·3부대 12명을 사용한다. `PreparationStation`을 공통 대상으로 두고 건설소와 성문 앞 붉은 깃발(`BattleStartStation`)을 F로 이용한다. 전투 중 건설은 차단한다.
- 게임 전용 `Data/GameFlow/Battle.asset`·`Siege.asset`에 시험 적 28명(사다리병 12·성문 파괴병 4·궁수 12)과 등장 위치·간격·공성 수치를 둔다. 준비 중에는 비활성 상태이며 전투 시작 때 북쪽 성벽에서 약 90m 떨어진 들판에 함께 등장해 접근한다. 한 명씩 순차 생성하는 방식은 아니다.
- 사용자 결정 반영: 사다리는 맨 위 성벽까지 닿는다. 성문 파괴 후 위 조각은 내려앉고 성문 파괴병이 성문 자리의 조각도 공격한다. 동시 승패는 패배 우선이다. 살아 있는 공성 병력이 없고 살아 있는 아군까지 진입 경로도 없으면 남은 적이 바깥으로 퇴각해 승리한다. 퇴각은 사망과 따로 센다.
- 전투 종료 시 공격·AI·이동·낙하를 멈추고 투사체를 정리한다. 다음 준비에는 생존 아군과 성벽 피해를 유지하고 시험 적 편성을 다시 만든다. 정식 보상 지급·스테이지별 다른 웨이브·병력 보충·성문 수리·사기는 아직 미구현이다.
- 에디터 연결은 `Editor/GameBattleSetup.cs`의 `ProjectNeo/Setup/Connect Battle To Game Scene`이다. `GameFlowSetup.ApplyDefaultCastle`도 이를 호출한다. `ProjectNeo/Setup/Arrange Game HUD`는 기존 HUD만 재배치한다. 씬·에셋은 에디터 API로 저장했다.
- HUD는 좌상단 부대 목록, 상단 중앙 단계, 우상단 적·아군 수와 성문 상태, 하단 두 줄 조작 안내로 분리했다. 준비 상호작용과 전투 상태는 별도 영역이며 건설 안내는 우상단, 복귀 안내는 건설 도구 오른쪽으로 둔다. 기존 임시 승리·패배 버튼 안내를 제거했다. 글자와 배경 모두 화면 비율에 맞춰 함께 배치한다. 에디터 최대화 화면과 창 복원 후 좁은 Game 뷰에서 겹침이 없음을 확인했다. 캡처는 `Temp/game-battle-hud-arranged.png`다.
- 자동 검증: EditMode 136개·PlayMode 전체 70개 통과. `SiegeRulesTests`의 퇴각 조건, `GameBattlePlayTests`의 적 접근·실제 승패·동시 패배 우선·퇴각/열린 진입로·3층 사다리·붕괴 성문 조각 공격·다음 준비 이월·HUD 배경 포함 영역 검사를 확인했다. Test Runner에서 해당 클래스 또는 전체 EditMode/PlayMode를 실행한다.
- 에디터 직접 실행: 깃발 대상 상호작용 후 적이 북쪽에서 접근했고, 자연 진행으로 성문 파괴·아군 0명·플레이어 체력 100에서 패배 결과가 나왔다(남은 적 18명). 새 런타임 오류는 없다. F/Esc는 Input System 가상 키보드 자동 검증을 통과했으나 CUA 키 주입이 동작하지 않아 물리 키보드는 사용자 확인 대상이다. Windows 빌드는 다시 만들지 않았다.
- 직접 확인: `Assets/_Project/Scenes/Game.unity` → Play → 1인칭으로 건설소에 접근해 몸통을 바라보고 F → 건설 후 Esc → 북쪽 중앙 길을 따라 성문 안쪽 오른편 붉은 깃발에 접근해 3.5m 안에서 바라보고 F → 적 침공·전투 HUD 확인 → F1~F3 부대 선택, 1 이동·2 공격·3 중지. 승리하면 보상 화면에서 다음 준비로, 패배하면 결과에서 타이틀로 이동한다. Play 종료 후 재시작하면 성벽·병력·스테이지가 초기화된다. Game 씬을 열어 두었다.

</details>
