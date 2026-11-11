# ProjectNeo

3D 3인칭 수성전 로그라이크. 다음 웨이브를 보고 영웅·병력·장비·성벽을 준비하고, 플레이어 직접 전투와 부대 지휘로 성을 지킨다. Unity **6000.6.8f1**, URP를 사용한다. 현재는 임시 그래픽이며 Synty 에셋으로 교체할 예정이다.

## 문서 안내

이 README가 문서의 시작점이다. 기획·계획·현재 코드·작업 방법·과거 기록을 아래처럼 나눠 관리한다. 구글 독스는 사람이 읽는 참고용이며 저장소 Markdown이 기준이다.

| 알고 싶은 것 | 기준 문서 |
|---|---|
| 무엇을 만드는가: 규칙·경험·미정 기획 | [게임 기획](Docs/GameDesign.md) |
| 어디까지 만들고 어떤 순서로 완성하는가: GOAL·현재 상태·세 명의 분업 | [장기 구현 계획](Docs/ImplementationPlan.md) |
| 현재 코드·씬·에셋은 어디에 있고 무엇을 하는가 | [코드 지도](Docs/CodeMap.md) |
| 함께 작업하는 방법: 소유 파일·통합·아트 분리·Unity CLI | [개발·협업 안내](Docs/DevelopmentGuide.md) |
| 무엇을 검증했는가: 완료 이력·테스트·성능 측정 | [개발 기록](Docs/DevelopmentHistory.md) |
| 에이전트가 작업할 때 먼저 지킬 규칙 | [AGENTS.md](AGENTS.md) |

`CLAUDE.md`는 `AGENTS.md`를 불러오는 진입 파일이다. 별도 규칙이나 계획을 중복해서 적지 않는다. 문서가 엇갈리면 게임 규칙은 GameDesign, 작업 순서·상태는 ImplementationPlan, 실제 코드 설명은 CodeMap을 먼저 확인하고 잘못된 내용을 바로잡는다.

## 현재 실행해 볼 수 있는 것

- [ConstructionPrototype](Assets/_Project/Scenes/ConstructionPrototype.unity): Q/E로 시점 회전, 성벽·계단 선택, 격자 반투명 미리보기, 좌클릭 설치·우클릭 취소·R 조각 회전. 성벽 위 이동과 발밑 조각 파괴로 낙하를 시험할 수 있다. 현재 빌드 첫 씬이다.
- [CombatPrototype](Assets/_Project/Scenes/CombatPrototype.unity): WASD 이동, 마우스 시점, 좌클릭 근접 공격. 허수아비와 체력·카메라 시험. Esc로 커서를 푼다.
- [UnitBattlePrototype](Assets/_Project/Scenes/UnitBattlePrototype.unity): 아군 10명과 적 10명의 근접·원거리 자동 전투 관전.

에디터에서 씬을 열고 Play를 누른다. 아직 준비 → 수성전 → 보상으로 이어지는 게임 전체 흐름은 없다. 세 씬은 기반 기능을 확인하는 프로토타입이다.

## 작업을 시작할 때

1. 장기 구현 계획의 1장 GOAL, 2장 결정 대기, 3장 현재 상태, 5~6장 가까운 작업·분업을 읽는다.
2. 해당 게임 기획과 CodeMap을 확인한다.
3. 현재는 단독 작업 순서로 진행한다. 협업은 사용자가 시작을 알린 뒤 담당 범위·공통 계약·통합 담당자를 정해 적용한다.

현재 출시 플랫폼·시기·콘텐츠 규모는 미정이다. 장기 계획은 완성 조건에서 거꾸로 필요한 단계를 나눈 것이며, 먼 단계의 세부 내용은 플레이 결과에 따라 구체화한다.
