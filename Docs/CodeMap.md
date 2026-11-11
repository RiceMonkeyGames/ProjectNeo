# 코드 지도

마지막 갱신: 2026-11-11. 경로는 저장소 루트 기준이다. 현재 진행 상태는 [ImplementationPlan.md](ImplementationPlan.md)가 기준이다.

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
| Walls | `ConstructionProgress` | 공사 기간과 스테이지 진행. 성벽 배치와 파괴는 아직 없다. |
| Meta | `PerkDefinition`, `PerkLoadout` | 퍽 정의와 스탯·특수 효과 목록, 포인트 사용, 적용 및 초기화 |
| Meta | `PerkSpecialEffect`, `MoraleBoostPerkEffect` | 전투 이벤트별 특수 효과와 사기 증가 예시. 실제 전투 이벤트 연결은 후속 작업이다. |
| Meta | `MetaProgress`, `MetaConfig`, `CommanderProgression`, `UnlockGacha` | 유지 데이터, 지휘관 경험치·퍽 포인트, 환생 보너스 환산, 중복 없는 해금 추첨과 확정 등장 표시. 파일 저장은 아직 없다. |

## B-2 씬과 에셋

- `Assets/_Project/Scenes/CombatPrototype.unity`: 플레이어, 허수아비 3개, 바닥과 충돌 확인용 벽. 빌드 목록 첫 씬이다.
- `Assets/_Project/Data/CombatPrototype/`: 플레이어·허수아비 영웅 정의, `CombatProfile`, `PlayerControls`. 임시 수치는 여기서 바꾼다.
- `Assets/_Project/Prefabs/CombatPrototype/`: 공통 몸통과 `Visual` 자식으로 나뉜 플레이어·허수아비 프리팹. 플레이어 입력은 씬 카메라를 참조하므로 씬 인스턴스에 붙는다.
- `Assets/_Project/Art/Placeholder/`: 임시 재질. 캡슐과 상자 메시를 사용한다.
- `Assets/_Project/Input/InputSystem_Actions.inputactions`: 기존 `Player/Move`, `Player/Look`, `Player/Attack` 액션을 플레이어마다 복제해 사용한다. 공유 에셋의 활성 상태를 바꾸지 않는다.

플레이 방법: 씬을 열고 Play → WASD 이동, 마우스 시점, 좌클릭 공격. Esc로 커서를 풀고 좌클릭하면 다시 잡는다. 허수아비 체력 100, 공격력 25이므로 네 번 맞히면 죽는다. Play를 다시 시작하면 초기 상태로 돌아온다.

현재 입력 공격은 전방 각도·사거리·가림 검사를 통과한 가장 가까운 적 하나에 `공격력 − 방어력`(최소 1) 피해를 준다. 플레이어는 아군, 허수아비는 적이다. 허수아비 방어력은 0이라 네 번 맞히는 동작은 같다. 플레이어에게도 `ReceiveDamage()`로 같은 체력 규칙을 적용하지만 허수아비는 반격하지 않는다.

## B-3 씬과 에셋

- `Assets/_Project/Scenes/UnitBattlePrototype.unity`: 관전용 자동 전투 씬. 아군(파랑) 10명과 적(빨강) 10명, 각각 근접 7명·원거리 3명. 중앙 장애물과 NavMesh가 있다. 현재 빌드 목록 첫 씬이다.
- `Assets/_Project/Data/UnitBattlePrototype/`: 양쪽 근접·원거리 잡졸 정의 4개, 근접·원거리 `CombatProfile`, AI 설정과 구운 NavMesh 데이터. 수치는 동작 검증용이며 아군이 더 강하다.
- `Assets/_Project/Prefabs/UnitBattlePrototype/`: 잡졸 프리팹 4개와 직선 투사체 프리팹. 유닛 몸통에는 전투·충돌체·NavMeshAgent·AI가 있고 `Visual` 자식에만 임시 외형이 있다.

씬을 열고 Play를 누르면 자동으로 싸운다. Play를 다시 시작하면 초기 상태로 돌아온다. 플레이어 조작 시험은 `CombatPrototype`에서 한다. 전투 결과 화면이나 런 진행은 아직 없다.

`CharacterActor.Configure(...)`는 비활성 오브젝트를 활성화하기 전에 호출한다. 활성화 시 스탯·체력이 생기고 대상 후보 목록에 등록되며, 사망·비활성화 때 목록에서 빠진다. AI는 같은 씬의 적 중 NavMesh 완전 경로가 있는 가장 가까운 대상을 고른다. 대상 선택과 경로 갱신 주기는 에셋에 둔다. 사선이 막혔으면 장애물을 돌아 접근한다.

원거리 공격은 선딜레이가 끝날 때 대상 위치를 향해 발사한다. 발사 뒤에는 유도하지 않으며 첫 적이나 벽에 맞으면 사라진다. 발사 전 대상이 사망·이탈했으면 발사를 취소한다. 날아가는 투사체는 발사자의 사망과 별개로 남은 사거리·수명 동안 이동한다.

## 에디터와 테스트

- `Assets/_Project/Scripts/Editor/CombatPrototypeSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Combat Prototype` 또는 `Create()`로 씬·프리팹·에셋을 Unity API로 생성한다. 기존 씬이 있거나 현재 씬에 저장하지 않은 변경이 있으면 중단한다. 기존 테스트 수치 에셋도 덮어쓰지 않는다.
- `Assets/_Project/Scripts/Editor/UnitBattleSetup.cs`: 메뉴 `ProjectNeo/Setup/Create Unit Battle Prototype` 또는 `Create()`로 B-3 씬·프리팹·수치 에셋·NavMesh를 생성한다. 기존 씬과 저장하지 않은 변경은 보존한다. `UpgradeTrainingTeams()`는 기존 허수아비 프리팹을 적 팀으로 갱신한다.
- `Assets/_Project/Scripts/Editor/BuildScript.cs`: Windows 빌드. 기본 출력 `Builds/Windows/ProjectNeo.exe`.
- `Assets/Tests/EditMode/`: 스탯, 지갑, 시설, 공사·부대, 퍽, 아이템·상점, 캐릭터, 메타 진행, 체력 규칙.
- `Assets/Tests/PlayMode/CombatPrototypePlayTests.cs`: 실제 씬을 추가로 불러와 이동·충돌, 공격 선딜레이·사거리·가림·사망, 카메라 회전 제한과 벽 회피를 확인한다.
- `Assets/Tests/EditMode/DamageRulesTests.cs`: 방어력 차감, 최소 피해와 유효하지 않은 수치를 확인한다.
- `Assets/Tests/PlayMode/UnitCombatPlayTests.cs`: 팀과 피해, 대상 유지·교체, 투사체 아군 통과·회피·얇은 벽 충돌, 에이전트 사망 정지, 저장된 10 대 10 씬 전투를 확인한다. 테스트용 NavMesh와 씬, 고정 시간 간격은 종료 때 정리한다.
- `Assets/Tests/PlayMode/SetupSmokePlayTests.cs`: 프레임 진행 확인.
- `Assets/Tests/PlayMode/UnitScaleBenchmark.cs`: 명시적으로 실행하는 성능 측정. 평소 테스트에서는 제외된다.
