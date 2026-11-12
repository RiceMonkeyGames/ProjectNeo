# 과거 검증 기록

마지막 갱신: 2026-11-12 20:00:44 (KST). [Context.md](Context.md)에서 분리한 완료 결과·성능 측정·직접 검증 기록이다. 당시 완료 범위와 검증 근거를 보존하며, 과거 테스트 개수·임시 수치는 최신 기준이나 최종 성능 보장이 아니다. 현재 작업 상태는 [로드맵 3장](ImplementationPlan.md#3-현재-상태와-출발점)을 먼저 읽는다. 에이전트는 과거 결정의 이유나 검증 근거가 필요할 때만 이 파일을 연다. 필요한 날짜·작업 ID를 검색해서 해당 부분만 읽는다.

새 완료 결과는 이 파일 맨 끝에 `### 날짜: 제목` 형식으로 추가하고, 현재 상태는 로드맵 3장, 코드 위치는 Context 2장도 함께 갱신한다.

### 2026-11-12 공유 백로그 운영 도입

- 구글 독스 최상단에 개발 백로그 표(작업 ID·작업 내용·상태·담당자·완료 기준)와 사용 절차를 추가했다. 시작용 빈 행 3개를 두고 기존 기획·이미지는 보존했다. 브라우저 화면과 문서 Markdown 내보내기로 표가 기존 본문보다 앞에 있는 것을 확인했다.
- AGENTS에 공유 링크·착수 표시·다른 담당자 작업 보호·완료 후 Markdown/백로그 갱신·새 작업 등록 절차를 기록했다. ImplementationPlan 2장·7장과 이 문서의 협업 안내를 맞췄다. 게임 기획 본문과 작업 백로그의 역할도 구분했다.
- 문서만 변경했으며 게임 코드·씬·에셋은 수정하지 않았다. Unity 컴파일·테스트·빌드는 실행하지 않았다.
- 직접 확인: 공유 링크를 열고 탭 1 문서 내용에서 Ctrl+Home으로 이동한다. 맨 위에 「개발 백로그」 제목과 5열 표가 보이고, 그 아래 기존 기획·이미지가 이어져야 한다. AGENTS의 「백로그와 작업 흐름」에서 같은 링크와 절차를 확인한다. 시험할 때는 빈 행에 임시 항목을 쓰고 작업중·담당자 표시를 확인한 뒤 임시 입력만 지워 초기화한다.
- 후속 요청으로 초기 빈 표를 현재 계획 기반 15건으로 채웠다. 직접 플레이 검증, C-1a 런 경계 합의, 사기·무기·추가 적 결정과 웨이브·승패 집계·손실·보상·미리보기·시설 작업 분해·M1 통합 검증을 등록했다. 모두 미배정이며 기획·선행 대기를 구분했다. 미래 항목의 등록은 구현 완료나 실제 담당 배정을 뜻하지 않는다.
- 표 아래에 백로그 등록 시점 다섯 가지를 추가하고 AGENTS·ImplementationPlan에도 반영했다. 브라우저에서 추가한 표와 안내의 3페이지를 확인했고, 수정 전후 내보낸 Markdown에서 기존 기획 본문·이미지 이후 내용이 동일함을 확인했다. 직접 확인은 같은 링크의 Ctrl+Home에서 15개 작업과 아래 「백로그를 추가하는 순간」을 읽는다. 이제 표가 채워져 있으므로 기존 항목으로 상태 변경 시험을 하지 않고, 별도 임시 행을 추가해 시험한 뒤 그 임시 행만 삭제한다.

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

### 2026-11-12: 공성 수단을 잃은 적의 후퇴 (Q-30, 브랜치 작업 번호 Q-27)

- 사용자 결정: 추천안 채택. 사다리병·성문 파괴병이 모두 없고, 남은 적 중 걸어서 아군에게 닿는 적(뚫린 곳·무너진 성문으로 들어오는 적)도 없으면 남은 적이 후퇴한다.
- `SiegeOutcomeRules.ShouldRetreat`(일반 C#), `SiegeDirector`의 후퇴 지시와 `EnemiesFled`, `SiegeAttackerBrain.Retreat()`·`Fled`를 추가했다. 후퇴하는 적은 이동 지시로 판 바깥 끝까지 가고 싸우지 않으며, 도착하거나 길이 없거나 `SiegeSettings.retreatTimeout`(20초)이 지나면 사라진다. 모든 적이 첫 판단을 마친 뒤에만 판정하고, 지형 갱신 중에는 판정하지 않는다. 한 번 내린 후퇴는 되돌리지 않는다.
- 편성에 사다리병·성문 파괴병이 하나도 없고 성이 다 막혀 있으면 전투 시작 직후 남은 적이 바로 물러나 승리한다. 규칙대로의 동작이다.
- 상단 표시: 후퇴 중이면 "공성 수단을 잃은 적이 물러납니다", 떠난 적이 있으면 결과가 "승리! 남은 적이 물러났습니다."
- develop 병합: feature 브랜치에서 Q-27로 작업한 결정을 develop 번호 Q-30으로 바꿔 기록했다(develop의 Q-27은 층 쌓기). 코드 충돌은 없었고 문서 세 개의 충돌만 정리했다. 병합된 develop에서 Unity 컴파일·테스트는 아직 다시 돌리지 않았다.
- 검증(feature 브랜치 단독): 컴파일 오류 없음. EditMode 111개, PlayMode 전체 52개 통과(후퇴 규칙 1개, 막힌 성에서 후퇴·승리와 뚫린 성에서 계속 싸움 2개 추가). 에디터 Play에서 성벽을 막고 공성병 16명을 없애자 궁수 12명이 물러나 승리하는 것과 결과 문구를 화면으로 확인했다. 새 런타임 오류 없음.
- 직접 검증: `SiegePrototype` → Play → `성벽`으로 뚫린 4·11·12번 칸을 막음 → `전투 시작` → 사다리병과 성문 파괴병을 모두 처치(성벽 위에 부대를 배치해 두면 쉽다) → 남은 궁수가 성 밖으로 물러나 사라지고 "승리! 남은 적이 물러났습니다." 표시. 뚫린 칸을 남겨 두면 궁수가 그 길로 들어와 계속 싸운다.
- 자동 검증 재실행: Test Runner의 EditMode `SiegeRulesTests.Retreat_OnlyWhenNoBreachersAndNoWayIn`, PlayMode `SiegePrototypePlayTests.ClosedWalls_BreachersGone_RemainingArchersRetreat_AndVictory`·`OpenGap_BreachersGone_ArchersKeepFighting`.

### 2026-11-12: 원격 develop 후퇴 변경과 Game 전투 통합

- 원격 `develop`의 `85869c6`(팀원 작업 `221bb83` 포함)을 받았다. 로컬 성 확대·준비 상호작용·전투 연결·HUD 변경은 `42222d8`에 먼저 보존한 뒤 병합했다. 공용 C#은 자동 병합 결과를 확인했고, 문서 세 개의 충돌은 현재 Game 동작과 양쪽 검증 기록을 함께 보존했다.
- 팀원의 `SiegeDirector`·`SiegeAttackerBrain.Retreat/Fled`·후퇴 설정·안내·테스트를 유지한다. 기존 `GameBattleController`의 경로 확인·이탈 집계와 프로토타입의 판단 상태 기반 후퇴는 각 진행 담당이 실행하며, `ShouldRetreat`의 두 인자 형식을 모두 유지했다. Game의 적층 사다리·성문 자리에 내려온 조각 공격·게임 전용 설정 연결도 유지했다.
- 통합 검증: 컴파일 오류 없음. EditMode 전체 137개, PlayMode 전체 72개 통과. 팀원의 `Retreat_OnlyWhenNoBreachersAndNoWayIn`·`ClosedWalls_BreachersGone_RemainingArchersRetreat_AndVictory`·`OpenGap_BreachersGone_ArchersKeepFighting`과 기존 `GameBattlePlayTests`를 모두 포함한다. 이번 병합에서는 물리 입력 직접 플레이와 Windows 빌드를 추가 실행하지 않았다.
- 직접 확인: `Assets/_Project/Scenes/Game.unity` → Play → 성문 앞 붉은 깃발을 바라보고 F → 적 침공과 승패 확인. 프로토타입 후퇴는 `Assets/_Project/Scenes/SiegePrototype.unity` → Play → 빈 성벽을 막고 전투 시작 → 공성병을 처치하면 궁수가 물러나 승리, 진입로가 남으면 계속 싸워야 한다. Play 종료 후 재시작하면 초기화된다. Test Runner에서 `SiegeRulesTests`·`SiegePrototypePlayTests`·`GameBattlePlayTests`로 재검증할 수 있다. Game 씬을 비실행 상태로 열어 두었다.

### 2026-11-12: 중지(자리 지키기) 행동 변경과 성벽 위 궁수 사선

- develop 병합(2026-11-12): 코드 충돌 없음. 팀원의 Game 씬 전투(`GameBattleController`)는 적에게 이동 지시만 쓰므로 영향이 없고, Game 씬 아군 부대의 중지에는 새 규칙이 적용된다. 병합된 develop에서 Unity 재실행은 아직 하지 않았다.
- 사용자 결정(Q-22 변경): 성벽 위에 배치한 부대가 올라온 적을 보고도 서 있는 문제를 고친다. 배너로드 2의 수성전 유닛처럼, 근접은 자리에서 6m 안의 걸어서 닿는 적에게 다가가 싸우고 돌아오며, 원거리는 자리를 지킨 채 쏜다. 길을 따라 성벽에서 내려가 쫓는 것도 허용. 새 지시를 만들지 않고 중지 규칙 자체를 바꿨다.
- `UnitCombatAI`: `HoldPost`(중지를 받은 곳·이동이 끝난 곳·배치한 곳), 닿는 적 우선 → 근접은 자리 반경 안의 닿는 적 추격 → 없으면 자리로 복귀. 판단 주기마다 경로를 다시 구하고 길이 끊기거나 적이 반경 밖으로 나가면 놓는다. `UnitAISettings.holdEngageRadius`(기본 6m, 0이면 이전처럼 쫓지 않음). 기존 AI 에셋은 새 필드를 기본값 6으로 읽는다.
- 사다리 밀기: "싸울 적이 없을 때 민다"는 그대로다. 걸어서 닿지 않는 성벽 아래 적은 대상이 아니므로 그런 적만 있으면 계속 민다.
- 성벽 위 궁수: 성벽 한가운데에서 바깥면 2~3m 앞의 적을 쏘면 선이 발밑 성벽 모서리를 스쳐 막혔다. 원거리 공격이 딛고 선 물체(`CombatPhysics.GroundUnder`)를 사선 검사와 투사체 모두에서 무시하게 했다. 적 궁수가 아래에서 위로 쏠 때는 발밑이 땅이라 변화가 없다.
- 기존 테스트 변경: `SquadCommandPlayTests`의 중지 테스트를 새 규칙(6m 안의 적은 다가가 싸움, 14m 밖의 적은 쫓지 않고 자리로 복귀)으로 바꿨다. 이름은 `MovingSquadIgnoresEnemies_HoldingSquadEngagesNearPost_ThenReturns`.
- 검증: 컴파일 오류 없음. EditMode 111개, PlayMode 전체 54개 통과(성벽 위 궁수가 바로 아래 적을 쏨, 성벽 위 근접이 올라온 적과 싸우고 복귀 2개 추가). 에디터 Play에서 성벽을 다 막고 7개 부대를 성벽 위에 배치한 뒤 전투를 돌려 적 16명 중 14명이 처치되고 부대원이 성벽 위 자리를 지키는 것을 확인했다. 그 판은 조작하지 않은 플레이어가 안뜰에서 적 궁수에게 죽어 패배로 끝났다(기존 동작). 새 런타임 오류 없음. Windows 빌드는 다시 만들지 않았다.
- 직접 검증: `SiegePrototype` → Play → 뚫린 칸을 막고 근접 부대를 사다리 자리 근처 성벽 위에 배치 → `전투 시작` → 사다리로 올라온 적에게 근접 부대가 다가가 싸우고, 처치 뒤 원래 자리로 돌아오는지 확인. 궁수 부대를 성벽 위에 두면 성벽 바로 아래의 적을 쏜다. 평지 지휘 씬(`SquadCommandPrototype`)에서도 중지 부대가 6m 안의 적에게 다가간다.
- 자동 검증 재실행: PlayMode `SiegePrototypePlayTests.WallArchers_ShootEnemyRightBelowTheWall`·`WallMelee_EngagesEnemyOnWallNearPost_ThenReturns`, `SquadCommandPlayTests.MovingSquadIgnoresEnemies_HoldingSquadEngagesNearPost_ThenReturns`.

### 2026-11-12: 중지 중인 원거리 유닛의 추격

- 사용자 요청: 원거리 유닛도 공격 사거리 너머 일정 거리 안의 적을 쫓아가 공격한다.
- `UnitAISettings.holdRangedChaseExtra`(기본 6m)를 추가했다. 원거리의 추격 반경은 자리에서 공격 사거리 + 이 거리이고(`UnitCombatAI.HoldChaseRadius`), 근접은 기존 `holdEngageRadius`(6m)다. 원거리는 걸어서 닿는 적에게 사거리가 닿고 사선이 열릴 때까지 다가가 멈춰서 쏘며, 적이 반경 밖으로 나가거나 길이 끊기면 자리로 돌아온다. 0으로 두면 원거리는 자리를 떠나지 않는다.
- 해석: "쫓아간다"를 걸어서 닿는 적으로 한정했다. 막힌 성벽 밖의 적처럼 길이 없는 적에게는 다가가지 않는다. 이 해석이 다르면 사용자에게 다시 묻는다.
- 검증: 컴파일 오류 없음. EditMode 111개, PlayMode 전체 55개 통과(`SquadCommandPlayTests.HoldingArchers_ChaseEnemyJustBeyondRange_ThenReturn` 추가: 사거리 + 3m의 적에게 다가가 쏘고, 추격 반경 밖의 적은 쫓지 않고 자리로 복귀). 실제 화면 플레이는 이번에 다시 확인하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/SquadCommandPrototype.unity` → Play → 궁수 부대(3번)는 중지 상태로 두고 1번 부대로 적 쪽 땅을 가리켜 2(공격) → 적이 끌려와 궁수 부대 사거리(11m) 조금 밖에 오면 궁수 부대가 앞으로 나와 쏘고, 적이 사라지면 원래 자리로 돌아온다. 수성전 씬에서는 성 안으로 들어온 적에게 궁수 부대가 다가가 쏜다.

### 2026-11-12: C-1a 런 데이터·전투 입출력 경계

- 담당 황성민, 브랜치 `feature/run-data-boundary`(develop `362cccf` 기준). 사용자 요청으로 브랜치에 커밋하고 develop에 병합해 원격에 올렸다. 공유 백로그 C-1a는 `완료`로 바꾸고, 후속으로 전투 결과 형식 팀원 확인·C-1~2 Game 씬 연결·불안정 PlayMode 테스트 조사를 등록했다.
- 사용자 결정(모두 추천안): 이번 범위는 데이터·규칙·EditMode 테스트까지이고 Game 씬 연결은 C-1~2. 살아남은 영웅·잡졸은 다음 전투 전 체력 전부 회복. 잡졸은 한 명씩 런 안 고유 번호로 기록. 전투 결과 형식은 초안으로 만들고 팀원 확인.
- 추가: `Assets/_Project/Scripts/Runtime/Run/`의 `RunState`·`RunUnits`(`RunUnit`·`RunHero`·`RunSoldier`)·`RunConfig`·`RunSquadSetup`·`CastleState`·`BattleInput`·`BattleResult`. 기존 빈 `Run` 폴더의 `.gitkeep`은 지웠다. `GameFlow.StartRun(RunState)`·`FinishBattle(BattleResult)`와 `RunProgress.State`를 더했다. 기존 `StartRun()`·`FinishBattle(BattleOutcome)` 호출과 Game 씬 동작은 그대로다. 씬·에셋·프리팹은 바꾸지 않았다.
- 정한 동작: 결과에 없는 유닛은 살아남은 것으로 본다. 성 기록이 없는 결과는 성을 바꾸지 않는다. 결과는 `CreateBattleInput`이 준 전투 번호와 같아야 하고 한 번만 반영되며, 다른 번호·이미 반영·모르는 유닛이면 아무것도 바꾸지 않는다. 죽은 영웅은 장비를 가진 채 `FallenHeroes`로 옮긴다(Q-31 미정이라 장비를 지우지 않음). 플레이어를 부대장으로 두는 것은 막지 않았다(Q-32 미정).
- 남은 제한: 전투 결과 형식은 팀원 확인 대기. Game 씬은 아직 런 데이터를 만들거나 쓰지 않는다. `CastleState`는 부서진 성문 자리의 신규 배치 금지를 되살리지 못한다. 조각·무기 정의를 에셋 참조로 들고 있어 파일 저장(Q-13)에는 ID가 더 필요하다.
- 검증: 컴파일 오류 없음. EditMode 전체 158개 통과(새 `RunStateTests` 16개, `CastleStateTests` 5개). PlayMode 전체 75개 중 74개 통과. 실패한 `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear`는 단독 재실행 3번 중 1번 통과했고, C-1a 변경을 잠시 뺀 develop 원본에서도 3번 중 1번만 통과해 기존 불안정 테스트로 판단했다. Windows 빌드는 하지 않았다. 바깥 폴더 에디터를 처음 열 때 `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`이 저절로 바뀌었으며 이번 작업 변경이 아니다.
- 직접 검증: 씬 동작 변화가 없는 규칙 작업이다. Unity `Window > General > Test Runner` → EditMode → `RunStateTests`, `CastleStateTests`, `GameFlowTests`를 골라 Run Selected → 모두 초록이어야 한다. 기존 게임 흐름이 그대로인지는 `Assets/_Project/Scenes/Game.unity` → Play → 건설소 F·Esc → 깃발 F → 전투 → 결과로 확인한다. CLI는 6장 EditMode 명령을 쓴다.

### 2026-11-12: 마지막 갱신 시각 기록 규칙

- 사용자 요청: 문서의 `마지막 갱신`을 날짜와 시·분·초까지 한국 시간으로 적는다. AGENTS 「일하는 규칙」에 형식(`마지막 갱신: YYYY-MM-DD HH:MM:SS (KST)`)과 실제 시각을 확인해 적는 방법을 기록했고, Context·ImplementationPlan·GameDesign의 머리말을 새 형식으로 바꿨다. 문서만 바꿨으며 Unity 작업은 하지 않았다.
- 직접 확인: 세 문서 3번째 줄의 `마지막 갱신`이 시·분·초와 `(KST)`를 포함하는지, AGENTS 「일하는 규칙」 두 번째 항목에 형식이 적혀 있는지 본다.

### 2026-11-12: C-1~2 런 데이터의 Game 씬 연결

- 담당 황성민, 브랜치 `feature/run-scene-connection`(develop `833ca46` 기준). 사용자 요청으로 커밋(`2f2fd79`)해 develop에 병합(`22253a4`)하고 원격에 올렸다. 백로그 C-1~2·C-1a-confirm을 `작업중`·황성민으로 표시하고, 결과 형식 확인은 진행하며 함께 하기로 했다(사용자 결정).
- 사용자 결정: 씬에 놓인 유닛에 런 번호를 붙이는 방식(매번 새로 만들지 않음). 죽은 아군의 몸은 다음 준비에도 남긴다. 살아남은 유닛 회복은 C-1a 결정을 따른다.
- 추가: `Run/RunUnitBinding.cs`, `Flow/GameRunLink.cs`, `GameFlow.AttachRunState`, `RunProgress.Attach`, `Health.RestoreFull`, `CharacterActor.SoldierDefinition`. `GameBattleController`에 `RunLink`, 준비·전투 시작 호출, 적 프리팹 이름별 처치 수, 종료 이유를 더했다. 씬·프리팹·에셋은 바꾸지 않았다.
- 남은 제한: 적 ID는 임시로 프리팹 이름이다. 런 영웅과 씬 플레이어의 `Character`가 별개다. 고용·모집·저장 복원·도망은 범위 밖이다. `RunConfig`의 시작 재화는 아직 쓰지 않는다. 결과 형식은 팀원 확인 대기다.
- 검증: 컴파일 오류 없음. EditMode 163개 전체 통과(새 `RunUnitBindingTests` 4개, `HealthTests.RestoreFull_HealsLivingToMaximum_ButNeverResurrects`). PlayMode 76개 중 75개 통과(새 `GameBattlePlayTests.런_데이터가_씬_편성으로_만들어지고_전투_결과를_반영하며_다음_준비에_생존자를_회복한다` 통과). 실패는 기존 불안정 테스트 `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear`(BUG-wall-hold)다. 물리 입력 직접 플레이와 Windows 빌드는 하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → 성문 앞 붉은 깃발에서 F로 전투 → F1~F3으로 부대를 성 밖으로 보내 일부가 죽게 한 뒤 승리 → `보상 받고 다음 스테이지` → 다친 부대원과 플레이어의 체력 막대가 가득 차고, 죽은 부대원은 쓰러진 채 남으며 부대 목록 인원은 줄어 있어야 한다. 런 데이터 값은 화면에 아직 표시하지 않으므로 Test Runner의 위 PlayMode 테스트로 확인한다. Play를 멈추면 초기화된다.


### 2026-11-12: C-4 승리 보상 지급(프로토타입 값)

- 담당 황성민, 브랜치 `feature/stage-rewards`(develop `bec2a78` 기준). 사용자 요청으로 커밋(`c63747c`)해 develop에 병합(`f9f79cf`)하고 원격에 올렸다. 백로그 C-4를 `작업중`·황성민으로 표시했다.
- 사용자 결정: 프로토타입이라 보상 값은 임의로 정하고, 나중을 위해 모든 값을 데이터 에셋에서 바꿀 수 있게 한다.
- 추가: `Run/StageRewardTable`·`KillReward`·`StageReward`, `RunState.ApplyReward`·`Seed`·`SeedFor`·`LastReward`, `Flow/GameRewardController`, `GameSession.Meta`, `GameFlowHud`의 보상 내역·보유 골드 표시와 `PhaseText`·`StatusText`, `Editor/GameRewardSetup`. `GameRunLink`는 런마다 시드를 정한다. 에디터 메뉴로 `Data/GameFlow/Rewards/` 에셋 9개를 만들고 `Game.unity`의 `GameBattleController` 오브젝트에 컴포넌트를 붙였다(씬은 에디터 API로 저장).
- 프로토타입 값: 기본 50골드 + 스테이지마다 10, 사다리병 3골드·뼈 조각 1, 성문 파괴병 5골드·질긴 가죽 2, 궁수 3골드·깃털 1, 아이템 60% 확률로 1개(철검 1 : 회복약 2), 지휘관 경험치 40 + 스테이지마다 10, 해금 포인트 5. 지휘관 레벨 규칙은 `Meta.asset` 기본값(레벨당 100 + 50씩 증가, 레벨당 퍽 포인트 1).
- 수정한 실수: 처음 연결 때 씬을 연 뒤 방금 만든 에셋 참조가 풀려 표가 빈 값으로 저장됐다. 씬을 연 뒤 경로로 다시 불러오도록 고쳐 다시 실행했고 연결을 확인했다.
- 남은 제한: 포로 병력, 해금 포인트 사용·지급 정책(Q-07), 메타 파일 저장, 골드·재료 사용처는 없다. 적 ID는 프리팹 이름이다.
- 검증: 컴파일 오류 없음. EditMode 169개 전체 통과(새 `StageRewardTests` 6개). PlayMode 77개 중 76개 통과(새 `GameBattlePlayTests.승리하면_보상_표대로_골드_재료_메타를_한_번_지급하고_보상_화면과_다음_준비에_보인다` 통과). 실패는 기존 불안정 테스트 `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear`다. 물리 입력 직접 플레이와 Windows 빌드는 하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → 성문 앞 붉은 깃발에서 F → 적을 물리쳐 승리 → 보상 화면 오른쪽 위 칸에 `골드 +N (보유 N)`, 재료별 획득·보유 수, 아이템(뽑혔을 때), 지휘관 경험치·해금 포인트가 보인다 → `보상 받고 다음 스테이지` → 위쪽 제목이 `준비 · 스테이지 2 · 골드 N`. 값을 바꾸려면 `Assets/_Project/Data/GameFlow/Rewards/StageRewards.asset`을 고르고 인스펙터에서 수정한 뒤 다시 Play한다. Play를 멈추면 런·메타 모두 초기화된다.

### 2026-11-12: D-3~8 시설·편성 작업 분해

- 담당 황성민, 브랜치 `feature/facility-plan`(develop `1d69e4d` 기준). 공유 백로그 D-3~8-plan을 `작업중`·황성민으로 표시했다. 문서만 바꿨고 게임 코드·씬·에셋과 Unity 작업은 없다.
- 기존 시설·경제·캐릭터·흐름 코드를 점검해 있는 규칙과 없는 연결을 정리하고, ImplementationPlan 4.2.1에 D-0(시설 공통 틀)·D-1(업그레이드)·D-2(건설·수리 비용)·D-0b(런 유닛 씬 생성)·D-3(주점)·D-4(훈련장)·D-5(상점)·D-6(대장간)·D-7(장비)·D-8(편성)의 선행·필요한 결정·완료 기준·공용 파일과 추천 순서를 적었다.
- 새 미정 항목 Q-33(시설 화면 방식)·Q-34(매물 갱신·리롤)·Q-35(대장간 생산·강화)·Q-36(고용 영웅 등장·모집 잡졸 배정)을 ImplementationPlan 2장과 GameDesign 미정 목록에 추가했다. 결정 전에는 해당 작업을 구현하지 않는다.
- 직접 확인: `Docs/ImplementationPlan.md`의 「4.2.1 D 시설·편성 실행 작업」 표와 2장의 Q-33~Q-36, 공유 백로그 맨 아래 새 D 항목들을 읽는다.

### 2026-11-12: D-2 건설·수리 비용

- 담당 황성민, 브랜치 `feature/build-costs`(develop `1d69e4d` 기준, 커밋 전 D-3~8 분해 문서 변경을 함께 가져옴). 사용자 요청으로 D-3~8 분해와 함께 커밋(`61b5648`)해 develop에 병합(`1870a3b`)하고 원격에 올렸다. 백로그 D-2를 `작업중`·황성민으로 표시했다.
- 사용자 결정: 철거는 성문을 뺀 모든 조각·대포, 이번 준비에 지은 것은 전액·그 전 것은 비율 환불. 수리는 "모두 수리" 버튼. 부서진 성문 재건은 이번에 다루지 않음. 값은 프로토타입 임의 값을 데이터에 둔다.
- 추가: `Run/BuildCostTable`·`PieceGoldCost`·`WeaponGoldCost`·`BuildEconomy`, `Flow/GameBuildEconomy`, `Editor/GameBuildEconomySetup`, 에셋 `Data/GameFlow/Build/BuildCosts.asset`(성벽 10·계단 15·대포 30골드, 지난 것 50% 환불, 체력 1당 0.2골드)·`RunStart.asset`(시작 100골드). 에디터 메뉴로 `Game.unity`에 컴포넌트와 건설 화면 버튼 2개·안내 칸을 더했다(에디터 API 저장). 다른 코드 파일은 C-4 보상 테스트의 골드 비교만 시작 골드를 고려하도록 고쳤다.
- 남은 제한: 부서진 성문 재건 없음. 건설 조각이 안뜰 시설 건물과 겹치는지는 검사하지 않는다(기존과 같음). 준비 제목의 골드는 단계가 바뀔 때만 다시 그려 첫 준비 제목에는 시작 골드가 늦게 보일 수 있고, 건설 화면 안내 칸은 매 프레임 갱신된다.
- 검증: 컴파일 오류 없음. EditMode 175개 전체 통과(새 `BuildEconomyTests` 6개). PlayMode 80개 중 79개 통과(새 `GameBuildEconomyPlayTests` 3개 통과). 실패는 기존 불안정 테스트 `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear`다. 처음 실행에서 C-4 보상 테스트가 시작 골드 때문에 실패해 테스트를 고쳤고, `GameBattlePlayTests` 10개를 다시 돌려 모두 통과했다. 물리 입력 직접 플레이와 Windows 빌드는 하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → 건설소에서 F → 건설 화면 왼쪽 패널 아래 노란 글씨가 `골드 100` → `성벽`을 골라 놓으면 골드가 10 줄고 안내에 비용이 보인다 → `철거 (클릭)` 버튼 → 방금 지은 성벽 클릭 → 10골드 환불(처음부터 있던 성벽은 5골드), 성문은 거부 → 우클릭으로 철거 끝 → 전투 후 다음 준비에서 깎인 성벽이 있으면 `모두 수리`로 골드를 내고 체력 막대가 가득 찬다. 값은 `Assets/_Project/Data/GameFlow/Build/BuildCosts.asset`·`RunStart.asset`에서 바꾼다. Play를 멈추면 초기화된다.

### 2026-11-12: D-0 시설 공통 틀

- 담당 황성민, 브랜치 `feature/facility-frame`(develop `d46804f` 기준). 사용자 요청으로 커밋(`3ac3962`)해 develop에 병합(`d146f35`, 병합 후 EditMode 181개 통과)하고 원격에 올렸다. 백로그 D-0을 `작업중`·황성민으로 표시하고 다시 읽어 확인했다.
- 사용자 결정: Q-33 지금 HUD와 같은 uGUI+TMP 임시 팝업, Q-34 상점·주점 매물은 준비마다 새로 뽑고 골드 리롤은 후속.
- 추가: `Flow/GameFacilities`·`FacilityStation`, `Editor/GameFacilitySetup`, 시설 정의 에셋 5개 `Data/GameFlow/Facilities/Facility_{Tavern,Shop,TrainingGround,Smithy,Defense}.asset`(레벨 3단계, 업그레이드 골드 60·120, 매물 수 3·4·5, 풀은 비어 있음). 변경: `RunState`(시설 상태), `RunConfig`(시설 목록·`Create` 오버로드), `FacilityDefinition`(표시 이름), `Wallet.Materials`, `GamePhaseView`(팝업 화면 상태), `PreparationInteraction`(팝업 Esc 닫기), `GameFlowHud`(팝업 중 안내). 에디터 메뉴로 `RunStart.asset`에 시설을 넣고 `Game.unity`에 컴포넌트·건물 상호작용·`FacilityPanel`을 더했다(에디터 API 저장).
- 남은 제한: 팝업 본문은 자리표시다(업그레이드 D-1, 각 시설 기능 D-3~6). 수성 시설은 상태만 있고 화면은 기존 건설소다. 팝업 글자는 한글로 고정이며 한글 글꼴은 HUD의 `RuntimeSystemFont`가 실행 중에 바꿔 준다.
- 검증: 컴파일 오류 없음. EditMode 181개 전체 통과(새 `RunFacilityTests` 6개). PlayMode 83개 전체 통과(새 `GameFacilityPlayTests` 3개, 이번에는 불안정 테스트 BUG-wall-hold도 통과). 물리 키보드·마우스 직접 플레이와 Windows 빌드는 하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → 안뜰의 `상점`(또는 주점·훈련장·대장간) 건물 앞에서 건물을 바라보면 아래 안내가 `F · 상점 이용` → F → 화면 가운데 팝업에 `상점`, `레벨 1 / 3`, `골드 100`이 보이고 마우스 커서가 나타나며 WASD·시점이 멈춘다 → Esc 또는 `닫기 (Esc)` 버튼 → 원래 자리에서 다시 이동 → 성문 앞 깃발로 전투를 시작하면 건물에서 F를 눌러도 열리지 않는다. 시설 이름·레벨 값은 `Assets/_Project/Data/GameFlow/Facilities/`, 시설 목록은 `Assets/_Project/Data/GameFlow/Build/RunStart.asset`에서 바꾼다. Play를 멈추면 초기화된다.

### 2026-11-12: BUG-wall-hold 계단 소실 후 부대가 이동 상태로 남는 문제

- 백로그 `BUG-wall-hold`(담당 youngho, 브랜치 `fix/wall-hold-stall`, 기준 develop `d46804f`). 사용자 요청으로 커밋(`63bc842`)해 D-0이 들어간 develop에 병합했다. 코드 충돌은 없었고 문서 충돌(마지막 갱신 시각, 검증 기준 문장, 7장 기록)만 양쪽을 살려 정리했다. 병합된 develop에서 Unity 재실행은 아직 하지 않았다.
- 재현: `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear`가 develop에서 5번 중 5번 실패. 앞줄 두 명은 성벽 가장자리(z 3.67)에서 중지, 뒷줄 두 명은 그 바로 뒤(z 4.1~4.3)에서 이동 상태로 남았다.
- 원인: 계단이 사라지면 성벽 위에서 땅으로 가는 길이 끊겨 유닛마다 부분 경로를 받는데, 2열 대형의 같은 열 유닛은 끝점이 거의 같다. 앞 유닛이 그 점을 차지하면 뒤 유닛은 남은 거리 0.35m 이하가 될 수 없어 이동 지시가 끝나지 않았다. 중지 유닛이 0.35m만 밀려도 자리로 돌아가던 동작(2026-11-12 중지 변경)이 자리 다툼을 굳힌 것으로 본다.
- 수정: 일반 C# `MoveStallTracker`(남은 거리만 보는 막힘 판정)를 추가하고 `UnitCombatAI`의 이동 도착·지점 공격 도착·중지 복귀에 썼다. 목적지 근처 1.5m 안에서 0.5초 동안 0.1m도 줄지 않으면 도착으로 본다. 중지 복귀는 자리에서 1m보다 밀렸을 때만 시작한다. 수치는 `UnitAISettings`(`moveStallDistance`·`moveStallSeconds`·`moveStallProgress`·`holdReturnDistance`)이며 기존 AI 에셋은 기본값으로 읽는다. 경로 탐색 호출은 늘지 않았다.
- 남은 것(별도 작업 후보): 이동 중 0.25초마다 경로 재계산, 중지 중 반경 안의 적마다 경로 확인은 유닛 수가 많아지면 비용이 커질 수 있다. 지형이 바뀔 때만 재계산·판단 시점 분산·가까운 몇 명만 경로 확인 등으로 줄일 수 있다.
- 검증: 컴파일 오류 없음. 해당 테스트 단독 6번 연속 통과. EditMode 180개(새 `MoveStallTrackerTests` 5개), PlayMode 전체 80개 통과. 직접 플레이와 Windows 빌드는 하지 않았다.
- 직접 검증: 계단만 지우는 시험 버튼이 없어 원래 상황은 자동 테스트로 확인한다. 같은 원리의 일반 확인: `Assets/_Project/Scenes/SquadCommandPrototype.unity` → Play → ` 로 전체 선택 → 화면 가운데를 가운데 장애물 바로 앞 땅에 맞추고 1(이동) → 부대원이 몰려도 모두 근처에서 멈추고 왼쪽 위 지시가 전부 `중지`로 바뀌어야 한다. Play를 다시 시작하면 초기화된다.
- 자동 검증 재실행: Test Runner → PlayMode → `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear` 여러 번 Run Selected, EditMode `MoveStallTrackerTests`.

### 2026-11-12: D-5 상점 구매·판매와 D-1 시설 업그레이드

- 담당 황성민, 브랜치 `feature/shop-and-upgrade`(develop `206e524` 기준). 백로그 D-5·D-1을 `작업중`·황성민으로 표시하고 다시 읽어 확인했다.
- 사용자 지시: 두 작업을 함께 하고, 확장성 있게 기획대로 만들되 기획이 자세하지 않은 부분은 비워 두고 기능만 넣는다(프로토타입).
- 추가·변경: `RunState`(상점 창구 `Shop`, `ShopStockedFor`, `SetupShop`, `ShopSeedFor`, `RestockShopIfNeeded`, `TryBuy`, `TrySell`, `TryUpgradeFacility`), `RunConfig.Economy`·`WithEconomy`, `GameFacilities`(업그레이드 버튼, 상점 매물·보관함 목록, 결과 안내), `GameFlowHud`(준비 중 제목 골드를 매 프레임 갱신해 D-2의 늦은 골드 표시 제한 해소), `GameFacilitySetup`(거래 수치·아이템·상점 풀 데이터와 새 팝업), `GameRewardSetup.Item`을 내부 공개로. 에셋 `Data/GameFlow/Economy.asset`(판매 50%), 아이템 `item_wood_shield` 25·`item_leather_armor` 30·`item_chain_armor` 60·`item_steel_sword` 80골드, `Facility_Shop.asset` 레벨별 풀(1레벨 4종 중 3개, 2레벨 6종 중 4개, 3레벨 6종 중 5개, 가중치 임의 값). 에디터 메뉴로 `Game.unity` 팝업을 다시 만들었다(에디터 API 저장).
- 구현 판단(기획 미정 부분): 업그레이드는 이번 준비의 매물을 다시 뽑지 않고 다음 준비부터 반영한다. 같은 매물은 한 개씩만 판다. 판매가는 구매가의 골드만 기준으로 한다(기존 `ItemShop` 규칙). 주점·훈련장·대장간은 레벨만 오르고 효과는 각 시설 작업에서 붙인다. 리롤·고정 슬롯은 후속.
- 남은 제한: 수성 시설은 팝업이 없어 업그레이드 화면이 없다. 보관함은 8줄까지 보이고 나머지는 개수만 표시한다. 산 장비를 착용하는 기능은 D-7이다.
- 검증: 컴파일 오류 없음. EditMode 195개 전체 통과(새 `RunShopTests` 9개). PlayMode 85개 전체 통과(`GameFacilityPlayTests` 2개 추가). Play 중 상점·대장간 팝업 화면을 캡처해 매물·보관함·업그레이드 버튼과 한글 표시, HUD와의 겹침을 확인하고 패널 높이를 540으로 줄였다. 물리 키보드·마우스 직접 플레이와 Windows 빌드는 하지 않았다.
- 직접 검증: `Assets/_Project/Scenes/Game.unity` → Play → 상점 건물을 바라보고 F → 왼쪽 `매물`에 3개(예: 철검 · 40골드)가 보인다 → 하나를 클릭하면 위쪽 골드가 줄고 오른쪽 `보관함`에 `… 팔기 · 절반 골드`가 생긴다 → 그것을 클릭하면 절반 값이 돌아온다 → 오른쪽 위 `업그레이드 · 60골드` 클릭 → `레벨 2 / 3`, 버튼이 `120골드`로 바뀐다(매물은 다음 준비부터 4개) → Esc로 닫고 전투를 이긴 뒤 다음 준비에서 상점을 다시 열면 매물이 새로 바뀐다. 다른 건물에서도 업그레이드 버튼이 동작한다. 값은 `Assets/_Project/Data/GameFlow/Facilities/Facility_Shop.asset`(풀·매물 수·업그레이드 비용), `Economy.asset`(판매 비율), `Rewards/item_*.asset`(가격)에서 바꾼다. Play를 멈추면 초기화된다.

