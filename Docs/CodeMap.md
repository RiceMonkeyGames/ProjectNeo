# 코드 지도

마지막 갱신: 2026-11-11. 경로는 저장소 루트 기준이다. 이 파일은 현재 존재하는 코드·씬·에셋만 설명한다. 장기 GOAL·작업 상태·세 명의 분업은 [ImplementationPlan.md](ImplementationPlan.md), 수정 소유권·통합·Unity 작업법은 [DevelopmentGuide.md](DevelopmentGuide.md), 과거 검증 결과는 [DevelopmentHistory.md](DevelopmentHistory.md)를 본다.

## 런타임 코드

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
| Combat | `UnitAISettings`, `UnitCombatAI` | 탐색 범위·판단 주기·회전·이동 설정. 가까운 추격 가능한 적 선택, 대상 유지, NavMesh 경로 갱신, 근접·원거리 공격, 사망 후 이동 정지. |
| Combat | `PlaceholderCombatView` | `Visual` 자식에서 공격·피격·사망 신호를 구독한다. 임시 색 변경, 공격 표시, 체력 막대와 사망 외형. 전투 판정을 하지 않는다. |
| Facilities | `FacilityDefinition`, `FacilityState` | 시설별 레벨 데이터, 품목 풀, 업그레이드 비용과 스탯 효과 |
| Squads | `SquadConfig`, `Squad<TUnit>`, `Army<TUnit>` | 부대와 군대 인원 상한, 부대장 지정. 아직 씬 지휘·이동은 없다. |
| Morale | `SquadMorale` | 퍽 효과에서 사용할 부대 사기 값. 실제 사기 감소·도망 규칙은 B-8에서 구현한다. |
| Walls | `ConstructionProgress` | 공사 기간과 스테이지 진행 계산. 실제 배치·런 이월과는 아직 연결하지 않았다. |
| Walls | `BuildGridDefinition`, `BuildPieceDefinition`, `BuildLayout` | 격자 크기, 조각 종류·점유 칸·높이·체력·프리팹 정의. 일반 C# 점유·회전·경계·고정 성문 영역 검사와 독립 계단의 연결 질의. |
| Walls | `ConstructionWorld`, `PlacedStructure` | 배치 모델과 몸통 생성, 실제 유닛 겹침 검사, 조각 체력·파괴 이벤트, 변경을 모아 비동기 NavMesh 갱신. |
| Walls | `PlacementViewSettings`, `PlacementController`, `BuildGridView` | 반투명 미리보기 재질·색, 마우스 선택·설치·취소·회전, 격자 표시. 미리보기는 충돌과 점유·NavMesh에 영향을 주지 않는다. |
| Walls | `BuildCameraSettings`, `ConstructionCameraController` | 직교 카메라의 각도·크기·초기 방향·회전 단위·전환 시간 에셋. Q/E로 판 중심을 90도씩 부드럽게 돌며 월드·격자·배치 좌표는 유지한다. |
| Walls | `FallSettings`, `FallDamageRules`, `UnitFallController` | 높이별 피해 계산과 파괴 시 낙하·착지. NavMeshAgent와 CharacterController 몸통을 지원하고 착지 후 이동을 복구한다. |
| Walls / UI | `ConstructionSandbox`, `ConstructionHud` | 시험 씬 전용 이동·발밑 조각 파괴, uGUI·TMP 건설 선택과 상태 표시. 정식 부대 지휘나 준비 화면은 아니다. |
| Meta | `PerkDefinition`, `PerkLoadout` | 퍽 정의와 스탯·특수 효과 목록, 포인트 사용, 적용 및 초기화 |
| Meta | `PerkSpecialEffect`, `MoraleBoostPerkEffect` | 전투 이벤트별 특수 효과와 사기 증가 예시. 실제 전투 이벤트 연결은 후속 작업이다. |
| Meta | `MetaProgress`, `MetaConfig`, `CommanderProgression`, `UnlockGacha` | 유지 데이터, 지휘관 경험치·퍽 포인트, 환생 보너스 환산, 중복 없는 해금 추첨과 확정 등장 표시. 파일 저장은 아직 없다. |

## B-2 씬과 에셋

- `Assets/_Project/Scenes/CombatPrototype.unity`: 플레이어, 허수아비 3개, 바닥과 충돌 확인용 벽. 직접 조작을 시험하는 별도 씬이다.
- `Assets/_Project/Data/CombatPrototype/`: 플레이어·허수아비 영웅 정의, `CombatProfile`, `PlayerControls`. 임시 수치는 여기서 바꾼다.
- `Assets/_Project/Prefabs/CombatPrototype/`: 공통 몸통과 `Visual` 자식으로 나뉜 플레이어·허수아비 프리팹. 플레이어 입력은 씬 카메라를 참조하므로 씬 인스턴스에 붙는다.
- `Assets/_Project/Art/Placeholder/`: 임시 재질. 캡슐과 상자 메시를 사용한다.
- `Assets/_Project/Input/InputSystem_Actions.inputactions`: 기존 `Player/Move`, `Player/Look`, `Player/Attack` 액션을 플레이어마다 복제해 사용한다. 공유 에셋의 활성 상태를 바꾸지 않는다.

플레이 방법: 씬을 열고 Play → WASD 이동, 마우스 시점, 좌클릭 공격. Esc로 커서를 풀고 좌클릭하면 다시 잡는다. 허수아비 체력 100, 공격력 25이므로 네 번 맞히면 죽는다. Play를 다시 시작하면 초기 상태로 돌아온다.

현재 입력 공격은 전방 각도·사거리·가림 검사를 통과한 가장 가까운 적 하나에 `공격력 − 방어력`(최소 1) 피해를 준다. 플레이어는 아군, 허수아비는 적이다. 허수아비 방어력은 0이라 네 번 맞히는 동작은 같다. 플레이어에게도 `ReceiveDamage()`로 같은 체력 규칙을 적용하지만 허수아비는 반격하지 않는다.

## B-3 씬과 에셋

- `Assets/_Project/Scenes/UnitBattlePrototype.unity`: 관전용 자동 전투 씬. 아군(파랑) 10명과 적(빨강) 10명, 각각 근접 7명·원거리 3명. 중앙 장애물과 NavMesh가 있다.
- `Assets/_Project/Data/UnitBattlePrototype/`: 양쪽 근접·원거리 잡졸 정의 4개, 근접·원거리 `CombatProfile`, AI 설정과 구운 NavMesh 데이터. 수치는 동작 검증용이며 아군이 더 강하다.
- `Assets/_Project/Prefabs/UnitBattlePrototype/`: 잡졸 프리팹 4개와 직선 투사체 프리팹. 유닛 몸통에는 전투·충돌체·NavMeshAgent·AI가 있고 `Visual` 자식에만 임시 외형이 있다.

씬을 열고 Play를 누르면 자동으로 싸운다. Play를 다시 시작하면 초기 상태로 돌아온다. 플레이어 조작 시험은 `CombatPrototype`에서 한다. 전투 결과 화면이나 런 진행은 아직 없다.

`CharacterActor.Configure(...)`는 비활성 오브젝트를 활성화하기 전에 호출한다. 활성화 시 스탯·체력이 생기고 대상 후보 목록에 등록되며, 사망·비활성화 때 목록에서 빠진다. AI는 같은 씬의 적 중 NavMesh 완전 경로가 있는 가장 가까운 대상을 고른다. 대상 선택과 경로 갱신 주기는 에셋에 둔다. 사선이 막혔으면 장애물을 돌아 접근한다.

원거리 공격은 선딜레이가 끝날 때 대상 위치를 향해 발사한다. 발사 뒤에는 유도하지 않으며 첫 적이나 벽에 맞으면 사라진다. 발사 전 대상이 사망·이탈했으면 발사를 취소한다. 날아가는 투사체는 발사자의 사망과 별개로 남은 사거리·수명 동안 이동한다.

## B-4 건설 기초 씬

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

현재 점유 키는 바닥 `(x, z)`이며 한 칸에 조각을 중복하거나 높이 층을 선택할 수 없다. 조각 높이 에셋을 바꾸는 것과 블록을 적층하는 것은 다른 기능이다. 향후 수성 무기는 `BuildPieceDefinition.MountPoints`의 설치 지점을 사용해 바닥 점유와 분리한다. `PlacedStructure.TryGetMountPose()`가 조각 크기·회전을 반영한 월드 위치를 제공한다. 현재 성벽 정의에 `top` 지점 하나를 예시로 뒀으며, 슬롯 점유·실제 무기 설치·지지 조각 파괴 시 처리는 B-4.6에서 구현한다.

현재 B-4.6의 성벽 위 대포 등 구조물, 적의 성벽 공격, 재화 차감·수리·런 이월, 정식 건설/전투 전환은 연결하지 않았다. 기존 직접 조작 씬·관전 전투 씬은 계속 별도로 시험한다.

## 에디터와 테스트

- `Assets/_Project/Scripts/Editor/CombatPrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Combat Prototype` 또는 `Create()`로 씬·프리팹·에셋을 Unity API로 생성한다. 기존 씬이 있거나 현재 씬에 저장하지 않은 변경이 있으면 중단한다. 기존 테스트 수치 에셋도 덮어쓰지 않는다.
- `Assets/_Project/Scripts/Editor/UnitBattleSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Unit Battle Prototype` 또는 `Create()`로 B-3 씬·프리팹·수치 에셋·NavMesh를 생성한다. 기존 씬과 저장하지 않은 변경은 보존한다. `UpgradeTrainingTeams()`는 기존 허수아비 프리팹을 적 팀으로 갱신한다.
- `Assets/_Project/Scripts/Editor/ConstructionPrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Construction Prototype` 또는 `Create()`로 건설 씬·에셋·프리팹·시험 UI를 Unity API로 생성한다. 기존 씬이나 저장하지 않은 변경이 있으면 중단한다. TMP 필수 리소스를 먼저 가져와야 한다.
- `Assets/_Project/Scripts/Editor/BuildScript.cs`: Windows 빌드. 기본 출력 `Builds/Windows/ProjectNeo.exe`.
- `Assets/Tests/EditMode/`: 스탯, 지갑, 시설, 공사·부대, 퍽, 아이템·상점, 캐릭터, 메타 진행, 체력 규칙.
- `Assets/Tests/PlayMode/CombatPrototypePlayTests.cs`: 실제 씬을 추가로 불러와 이동·충돌, 공격 선딜레이·사거리·가림·사망, 카메라 회전 제한과 벽 회피를 확인한다.
- `Assets/Tests/EditMode/DamageRulesTests.cs`: 방어력 차감, 최소 피해와 유효하지 않은 수치를 확인한다.
- `Assets/Tests/PlayMode/UnitCombatPlayTests.cs`: 팀과 피해, 대상 유지·교체, 투사체 아군 통과·회피·얇은 벽 충돌, 에이전트 사망 정지, 저장된 10 대 10 씬 전투를 확인한다. 테스트용 NavMesh와 씬, 고정 시간 간격은 종료 때 정리한다.
- `Assets/Tests/EditMode/BuildLayoutTests.cs`: 임의 격자 크기, 음수 좌표·회전 점유·실패 시 상태 보존, 독립 계단의 연결 갱신, 고정 성문 영역, 높이에 따른 낙하 피해.
- `Assets/Tests/PlayMode/ConstructionPlayTests.cs`: 카메라 4방향 회전과 픽킹 불변, 즉시 점프 없는 보간과 연속·역방향 입력, 미리보기·취소의 무영향, 회전 설치·중복 거부·비동기 갱신, 유닛 겹침·성문 영역, 실제 계단 이동·붕괴·낙하 피해와 에이전트 복귀, CharacterController 몸통의 낙하·복구, 연속 변경의 반영과 사라진 성벽 위 경로.
- `Assets/Tests/PlayMode/SetupSmokePlayTests.cs`: 프레임 진행 확인.
- `Assets/Tests/PlayMode/UnitScaleBenchmark.cs`: 명시적으로 실행하는 성능 측정. 평소 테스트에서는 제외된다.
