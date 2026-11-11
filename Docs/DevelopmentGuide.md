# 개발·협업 안내

마지막 갱신: 2026-11-11. 개발 순서와 작업 상태는 [ImplementationPlan.md](ImplementationPlan.md), 기획은 [GameDesign.md](GameDesign.md)가 기준이다.

## 1. 세 명의 작업 방식

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
| `GameDesign.md`, `ImplementationPlan.md`, `CodeMap.md` | 해당 작업의 문서 정리 담당자가 최신 내용을 읽고 반영한다. 같은 시간에 같은 문서를 쓰지 않는다. |

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

## 2. 코드와 검증 원칙

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

테스트 숫자는 완성도를 뜻하지 않는다. 현재 기준과 마지막 통과 기록은 구현 계획 3장과 개발 기록에 있다. 기능이 늘면 개별 규칙, 씬 연결, 실제 플레이, 실행 파일을 각각 필요한 수준으로 검증한다.

## 3. 아트 교체 대비

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

## 4. Unity CLI 작업 방법

`unity` 명령(Unity CLI 1.0.0-beta.17)으로 열려 있는 에디터를 조작한다. 프로젝트에 `com.unity.pipeline` 패키지가 들어 있다. 먼저 `unity status`로 에디터가 `ready`인지 확인한다. 에디터가 안 열려 있으면 사용자에게 열어 달라고 한다.

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
