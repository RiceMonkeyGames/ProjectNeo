# 프로젝트 컨텍스트

마지막 갱신: 2026-11-11. 경로는 저장소 루트 기준이다. 현재 실행 가능한 것, 코드·씬·에셋 지도, 개발 방법과 검증 근거를 이 문서에서 관리한다. 완성 목표·다음 작업·진행 상태는 [구현 로드맵](ImplementationPlan.md), 게임 규칙은 [게임 기획](GameDesign.md), 에이전트의 필수 규칙과 문서 입구는 [AGENTS.md](../AGENTS.md)가 기준이다.

## 찾아보기

- [1. 현재 실행과 확인 방법](#1-현재-실행과-확인-방법): 바로 열어볼 씬과 조작
- [2. 코드·씬·에셋 지도](#2-코드씬에셋-지도): 실제 구현 위치와 연결 범위
- [3. 협업과 통합](#3-협업과-통합): 협업 시작 후 적용할 운영 기준
- [4. 코드와 검증 원칙](#4-코드와-검증-원칙)
- [5. 아트 교체 대비](#5-아트-교체-대비)
- [6. Unity CLI 작업 방법](#6-unity-cli-작업-방법)
- [7. 과거 검증 기록](#7-과거-검증-기록): 필요할 때만 펼쳐 보는 완료 결과와 성능 측정

## 1. 현재 실행과 확인 방법

Unity 6000.6.8f1, URP, 3D 3인칭 프로젝트다. 현재는 임시 그래픽이며 Synty 에셋으로 교체할 예정이다.

| 씬 경로 | 확인할 것과 조작 |
|---|---|
| `Assets/_Project/Scenes/ConstructionPrototype.unity` | 현재 빌드 첫 씬. Q/E 시점 회전, 성벽·계단 선택, 격자 반투명 미리보기, 좌클릭 설치·우클릭 취소·R 조각 회전. 성벽 위 이동과 발밑 조각 파괴로 낙하 시험. 성벽 위 대포 설치와 받침 성벽 파괴 시 동반 파괴 시험 |
| `Assets/_Project/Scenes/CombatPrototype.unity` | WASD 이동, 마우스 시점, 좌클릭 근접 공격. 허수아비·체력·카메라 시험. Esc로 커서 해제 |
| `Assets/_Project/Scenes/UnitBattlePrototype.unity` | 아군 10명과 적 10명의 근접·원거리 자동 전투 관전 |
| `Assets/_Project/Scenes/SquadCommandPrototype.unity` | 플레이어 직접 조작 + 부대 3개 지휘. F1~F3 부대 선택, ` 전체 선택, 1 이동(화면 가운데가 가리키는 곳)·2 공격·3 중지. 왼쪽 위에 선택·지시·남은 인원 표시 |

에디터에서 씬을 열고 Play를 누른다. 종료 후 다시 Play하면 초기 상태로 돌아온다. 아직 준비 → 수성전 → 보상으로 이어지는 전체 게임 흐름은 없다. 세 씬은 기반 기능을 확인하는 프로토타입이다. 최신 테스트 통과 기준과 남은 연결은 [로드맵 3장](ImplementationPlan.md#3-현재-상태와-출발점)을 확인한다.

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
| Characters | `PlayerControlSettings`, `PlayerInputController`, `ThirdPersonCamera` | 조작·카메라 설정 에셋, Input System 입력과 CharacterController 이동, 벽을 피하는 3인칭 카메라 |
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
| Walls | `BuildGridDefinition`, `BuildPieceDefinition`, `BuildLayout` | 격자 크기, 조각 종류·점유 칸·높이·체력·프리팹 정의. 일반 C# 점유·회전·경계·고정 성문 영역 검사와 독립 계단의 연결 질의. |
| Walls | `ConstructionWorld`, `PlacedStructure` | 배치 모델과 몸통 생성, 실제 유닛 겹침 검사, 조각 체력·파괴 이벤트, 변경을 모아 비동기 NavMesh 갱신. |
| Walls | `PlacementViewSettings`, `PlacementController`, `BuildGridView` | 반투명 미리보기 재질·색, 마우스 선택·설치·취소·회전, 격자 표시. 미리보기는 충돌과 점유·NavMesh에 영향을 주지 않는다. |
| Walls | `SiegeWeaponDefinition`, `MountLayout`, `PlacedSiegeWeapon`, `ISiegeWeaponModule` | 수성 무기 공통 구조. 정의 에셋(ID·이름·크기·프리팹), 일반 C# 설치 지점 점유 규칙(지지 조각 ID + 지점 ID마다 하나), 모든 무기가 쓰는 공통 몸통, 무기별 동작을 붙이는 인터페이스. 대포 전용 클래스는 없다. |
| Walls | `BuildCameraSettings`, `ConstructionCameraController` | 직교 카메라의 각도·크기·초기 방향·회전 단위·전환 시간 에셋. Q/E로 판 중심을 90도씩 부드럽게 돌며 월드·격자·배치 좌표는 유지한다. |
| Walls | `FallSettings`, `FallDamageRules`, `UnitFallController` | 높이별 피해 계산과 파괴 시 낙하·착지. NavMeshAgent와 CharacterController 몸통을 지원하고 착지 후 이동을 복구한다. |
| Walls / UI | `ConstructionSandbox`, `ConstructionHud` | 시험 씬 전용 이동·발밑 조각 파괴, uGUI·TMP 건설 선택과 상태 표시. 정식 부대 지휘나 준비 화면은 아니다. |
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

대형은 지시한 순간 카메라가 보는 방향을 앞으로 삼는다. 대형 자리가 걸을 수 없는 곳이면 목적지 자체로 보낸다. 목적지까지 길이 없거나 끊기면 갈 수 있는 가장 가까운 곳에서 멈춘다(Q-23 결정). 성벽 위 지휘(B-5.4), 부대장 영웅, 정식 HUD는 아직 없다.

### B-4 건설 기초 씬

- `Assets/_Project/Scenes/ConstructionPrototype.unity`: 현재 빌드 첫 씬. 초기 방향 45도·내려다보는 각도 30도의 직교 카메라, 성벽·계단·고정 성문, 시험 유닛, 조각 선택과 이동·붕괴 시험 버튼.
- `Assets/_Project/Data/ConstructionPrototype/`: 격자, 성벽·계단·성문 정의, 미리보기, 낙하 설정, 시험 유닛 데이터. 한 칸 크기·점유 칸·높이·체력·외형 프리팹과 피해 수치를 여기서 바꾼다.
- `Assets/_Project/Prefabs/ConstructionPrototype/`: 기본 크기 1의 성벽·계단 경사로·성문 몸통. 루트에 충돌체와 `PlacedStructure`, `Visual` 자식에만 외형을 둔다. 계단은 현재 임시 경사로 메시다.
- `Assets/_Project/Art/Placeholder/Construction*`: 임시 재질과 경사로 메시. 특정 Synty 팩과 연결하지 않았다.
- `Assets/TextMesh Pro/`: 비대화식으로 가져온 TMP 필수 리소스. 예제는 가져오지 않았다. 시험 HUD는 실행 중 Windows의 맑은 고딕을 사용하며 글꼴 원본 파일을 프로젝트에 복사하지 않는다. 다른 플랫폼용 최종 글꼴은 M5에서 확정한다.

Q/E로 시점을 좌우 90도씩 돌려 높은 건물 뒤쪽을 확인한다. 조각 선택 → 마우스로 반투명 미리보기 이동 → 좌클릭 설치. R 또는 회전 버튼은 조각만 90도 회전하고 우클릭이나 취소 버튼으로 배치를 취소한다. 초록은 설치 가능, 빨강은 격자 밖·점유·고정 성문·유닛 겹침 등 설치 불가다. 계단은 성벽과 연결되지 않아도 먼저 설치할 수 있고 안내를 표시한다.

`Camera.asset`에서 현재 내려다보는 각도 30도, 초기 방향 45도, 직교 크기 20, 90도당 전환 시간 0.3초를 조정한다. 회전은 실제 시간 기준 SmoothStep 보간으로 천천히 출발·정지하며, 연속·역방향 입력에도 현재 화면 위치에서 이어진다. 연속 입력으로 회전량이 커지면 그만큼 전환 시간도 늘어난다.

Q/`RotateLeft()`와 E/`RotateRight()`는 화면 속 판의 왼쪽·오른쪽 회전을 뜻하며 카메라 공전 각도의 부호와는 반대다.

`성벽 위로 이동`은 시험 유닛이 계단을 통해 걸어 올라갈 수 있는 성벽으로 이동한다. `발밑 조각 파괴`는 그 유닛을 지지하는 조각에 체력만큼 시험 피해를 준다. 유닛은 떨어져 높이에 따른 피해를 받고, 지면에 착지한 뒤 갱신된 NavMesh에 다시 연결된다. Play를 다시 시작하면 초기 상태다.

`ConstructionWorld`는 스케일 1의 평평한 격자 원점을 제공한다. 조각은 `Geometry` 아래에만 생성하고 미리보기·격자선은 그 밖에 둔다. 최초 NavMesh는 시작 시 만들며 이후 요청은 한 번에 하나씩 비동기 처리한다. 진행 중 추가 변경이 생기면 한 번 더 갱신한다. 배치 ID는 현재 씬 실행 중 식별자이며 런 저장 계약은 아직 아니다.

현재 점유 키는 바닥 `(x, z)`이며 한 칸에 조각을 중복하거나 높이 층을 선택할 수 없다. 조각 높이 에셋을 바꾸는 것과 블록을 적층하는 것은 다른 기능이다. 향후 수성 무기는 `BuildPieceDefinition.MountPoints`의 설치 지점을 사용해 바닥 점유와 분리한다. `PlacedStructure.TryGetMountPose()`가 조각 크기·회전을 반영한 월드 위치를 제공한다. 현재 성벽 정의에 `top` 지점 하나가 있다.

**수성 무기(B-4.6):** `대포` 버튼 → 성벽을 가리키면 그 조각의 가장 가까운 설치 지점에 반투명 미리보기 → 좌클릭 설치, 우클릭 취소. 지점 하나에 무기 하나만 놓인다. 성벽이 아닌 곳·설치 지점이 없는 계단·이미 무기가 있는 자리는 빨간색으로 거부한다. `무기 받침 성벽 파괴`는 무기를 받치는 성벽을 부숴 무기가 함께 사라지는지 확인한다. `ConstructionWorld.TryMount`/`RemoveWeapon`/`Collapse`가 설치·철거·동반 파괴를 처리하고 `WeaponRemoved`로 알린다. 무기는 `Geometry` 밖의 `Mounts` 아래에 생성해 NavMesh를 바꾸지 않는다.

새 무기를 추가하려면 `SiegeWeaponDefinition` 에셋과 프리팹(루트에 `PlacedSiegeWeapon`, 외형은 `Visual` 자식, 가로·높이·세로 1 기준)을 만든다. 무기만의 동작은 `ISiegeWeaponModule` 컴포넌트를 프리팹에 붙이면 설치·제거 때 호출된다. 시험 씬에는 `SiegeWeaponSetup.AddToOpenScene`의 무기 배열에 한 줄을 더하고 HUD의 `weapons` 목록에 넣는다. 현재 대포(`Cannon.asset`, `Cannon.prefab`)는 쏘지 않는 임시 외형이며 충돌체가 없다.

현재 수성 무기의 발사·피해·자체 체력, 적의 성벽 공격, 재화 차감·수리·런 이월, 정식 건설/전투 전환은 연결하지 않았다. 기존 직접 조작 씬·관전 전투 씬은 계속 별도로 시험한다.

### 에디터와 테스트

- `Assets/_Project/Scripts/Editor/CombatPrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Combat Prototype` 또는 `Create()`로 씬·프리팹·에셋을 Unity API로 생성한다. 기존 씬이 있거나 현재 씬에 저장하지 않은 변경이 있으면 중단한다. 기존 테스트 수치 에셋도 덮어쓰지 않는다.
- `Assets/_Project/Scripts/Editor/UnitBattleSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Unit Battle Prototype` 또는 `Create()`로 B-3 씬·프리팹·수치 에셋·NavMesh를 생성한다. 기존 씬과 저장하지 않은 변경은 보존한다. `UpgradeTrainingTeams()`는 기존 허수아비 프리팹을 적 팀으로 갱신한다.
- `Assets/_Project/Scripts/Editor/ConstructionPrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Construction Prototype` 또는 `Create()`로 건설 씬·에셋·프리팹·시험 UI를 Unity API로 생성한다. 기존 씬이나 저장하지 않은 변경이 있으면 중단한다. TMP 필수 리소스를 먼저 가져와야 한다.
- `Assets/_Project/Scripts/Editor/SiegeWeaponSetup.cs`: 메뉴 `ProjectNeo/Setup/Add Siege Weapons To Construction Prototype`. 기존 건설 씬에 대포 에셋·프리팹과 시험 버튼을 추가한다. 이미 추가됐거나 저장하지 않은 변경이 있으면 중단한다. 새로 씬을 만들 때는 `ConstructionPrototypeSetup.Create()`가 함께 호출한다.
- `Assets/_Project/Scripts/Editor/SquadCommandSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Squad Command Prototype` 또는 `Create()`로 B-5 씬·수치 에셋·NavMesh·시험 HUD를 만든다. 기존 씬이나 저장하지 않은 변경이 있으면 중단한다. B-2·B-3의 프리팹이 먼저 있어야 한다. `Upgrade()`(메뉴 `Upgrade Squad Command Prototype`)는 기존 씬에 팀별 시야 에셋과 선택·목적지 표시를 더하며 여러 번 실행해도 결과가 같다. 프리팹 인스턴스의 값은 `SerializedObject`로 써야 씬에 저장된다.
- `Assets/_Project/Scripts/Editor/BuildScript.cs`: Windows 빌드. 기본 출력 `Builds/Windows/ProjectNeo.exe`.
- `Assets/Tests/EditMode/`: 스탯, 지갑, 시설, 공사·부대, 퍽, 아이템·상점, 캐릭터, 메타 진행, 체력 규칙.
- `Assets/Tests/PlayMode/CombatPrototypePlayTests.cs`: 실제 씬을 추가로 불러와 이동·충돌, 공격 선딜레이·사거리·가림·사망, 카메라 회전 제한과 벽 회피를 확인한다.
- `Assets/Tests/EditMode/DamageRulesTests.cs`: 방어력 차감, 최소 피해와 유효하지 않은 수치를 확인한다.
- `Assets/Tests/PlayMode/UnitCombatPlayTests.cs`: 팀과 피해, 대상 유지·교체, 투사체 아군 통과·회피·얇은 벽 충돌, 에이전트 사망 정지, 저장된 10 대 10 씬 전투를 확인한다. 테스트용 NavMesh와 씬, 고정 시간 간격은 종료 때 정리한다.
- `Assets/Tests/EditMode/BuildLayoutTests.cs`: 임의 격자 크기, 음수 좌표·회전 점유·실패 시 상태 보존, 독립 계단의 연결 갱신, 고정 성문 영역, 높이에 따른 낙하 피해.
- `Assets/Tests/PlayMode/ConstructionPlayTests.cs`: 카메라 4방향 회전과 픽킹 불변, 즉시 점프 없는 보간과 연속·역방향 입력, 미리보기·취소의 무영향, 회전 설치·중복 거부·비동기 갱신, 유닛 겹침·성문 영역, 실제 계단 이동·붕괴·낙하 피해와 에이전트 복귀, CharacterController 몸통의 낙하·복구, 연속 변경의 반영과 사라진 성벽 위 경로.
- `Assets/Tests/EditMode/MountLayoutTests.cs`: 설치 지점 점유가 바닥 점유와 분리되는지, 실패 사유와 ID 보존, 서로 다른 무기 종류의 공통 규칙, 지지 조각 제거 시 그 위 무기만 해제.
- `ConstructionPlayTests`의 수성 무기 2개: 미리보기 무영향, 성벽 위 설치 위치·크기, 중복·성벽 밖·계단 거부, 바닥 점유와 NavMesh 불변, 받침 성벽 파괴 시 동반 파괴와 무기 동작 부품(`RecordingSiegeWeaponModule`) 알림 1회, 철거.
- `Assets/Tests/EditMode/SquadCommanderTests.cs`: 기본 중지, 하나·전체 선택과 없는 부대 거부, 선택한 부대만 지시, 이동 도착 시 중지·공격 유지, 대형 자리의 개수·간격·방향.
- `Assets/Tests/PlayMode/SquadCommandPlayTests.cs`: 저장된 지휘 씬에서 기본 중지, 선택한 부대만 이동 후 중지, 이동 중 적 무시, 중지 중 닿는 적만 공격하고 쫓지 않음, 공격 지시의 교전과 적이 없을 때 대기, 화면 가운데 이동 지시와 선택 없는 지시 거부. 실제 키 입력은 사용자 직접 확인 대상이다.
- `Assets/Tests/PlayMode/SetupSmokePlayTests.cs`: 프레임 진행 확인.
- `Assets/Tests/PlayMode/UnitScaleBenchmark.cs`: 명시적으로 실행하는 성능 측정. 평소 테스트에서는 제외된다.

## 3. 협업과 통합

현재는 사용자 혼자 개발하며 협업은 시작하지 않았다. 사용자가 시작을 알릴 때까지 단독으로 작업한다. 아래는 향후 세 명이 참여할 때 적용할 운영 기준이다. 실제 담당자 이름과 장비 구성은 미정이고, 계획의 개발자 1·2·3은 실제 배정을 뜻하지 않는다.

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
- 검증: EditMode 88개, PlayMode 21개 통과. 미리보기·취소의 무영향, 회전 점유·중복 거부·유닛 겹침·성문 영역, 계단을 통한 실제 이동, 파괴·낙하 피해와 두 이동 몸통의 복구, 연속 NavMesh 변경을 확인했다. UI 선택·미리보기·설치를 Input System 경로로 확인했고 게임 프레임 진행·화면·콘솔을 점검했다. Windows 빌드 성공.
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

</details>
