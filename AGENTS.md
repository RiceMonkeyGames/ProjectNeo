# ProjectNeo

수성전 로그라이크 게임. Unity 6000.6.8f1, URP, 3D 3인칭. 아트는 로우 폴리(Synty 에셋 예정)이고 지금은 임시 그래픽으로 만든다.

이 파일은 이 저장소에서 일하는 모든 에이전트(Claude Code, Codex 등)가 먼저 읽는 안내다. `CLAUDE.md`는 이 파일을 불러오기만 한다.

## 먼저 읽을 문서

기록은 md 파일이 기준이다. 구글 독스는 사람이 읽는 용도라서 읽으러 가지 않는다.

1. `Docs/ImplementationPlan.md`: 지금 어디까지 했는지(3장), 다음에 할 일(3장 "다음 작업"), 구현 순서(4장), 작업 원칙(5장).
2. `Docs/GameDesign.md`: 기획 전체와 미정 사항. 구현 전에 해당 부분을 읽는다.
3. `Docs/CodeMap.md`: 지금 있는 코드가 무엇을 하는지.

## 일하는 규칙

- 기획이 애매하거나 빠진 부분은 임의로 정하지 말고 사용자에게 먼저 묻는다. 어쩔 수 없이 가정했으면 답변과 `ImplementationPlan.md` 2장에 적는다.
- 사용자가 새 결정을 말하면 대화에만 남기지 말고 `GameDesign.md`(기획)나 `ImplementationPlan.md`(구현 결정, 진행 상태)를 바로 고친다. 코드가 늘면 `CodeMap.md`도 고친다.
- 커밋은 사용자가 직접 한다. 요청받지 않으면 커밋하지 않는다.
- 사용자에게는 한국어로, 쉬운 말로 답한다.

## 코드 구조

- `Assets/_Project/Scripts/Runtime/<시스템>/`: 게임 코드. 어셈블리 `ProjectNeo.Runtime`, 네임스페이스 `ProjectNeo.<시스템>`.
- `Assets/_Project/Scripts/Editor/`: 에디터 전용 코드. 어셈블리 `ProjectNeo.Editor`.
- `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`: 테스트. 네임스페이스 `ProjectNeo.Tests`.
- 규칙과 계산은 화면과 분리된 일반 C# 클래스로 만들고 EditMode 테스트로 검증한다. 씬에서 움직이는 것은 PlayMode 테스트로 확인한다.
- 수치는 ScriptableObject 에셋에 둔다. 테스트에서 쓰도록 각 정의 클래스에 `Create(...)` 정적 메서드를 둔다.
- ScriptableObject와 MonoBehaviour는 파일 하나에 클래스 하나, 파일 이름은 클래스 이름과 같게 한다.
- 에셋에 저장되는 enum은 값에 숫자를 명시하고, 새 값은 끝에 추가한다.
- 랜덤은 `ProjectNeo.Core.IRandomSource`로만 쓴다.
- 아트는 나중에 갈아 끼운다. 겉모습과 규칙을 분리하는 원칙이 `ImplementationPlan.md` 5장 "아트 교체 대비"에 있다.

## Unity CLI 작업 방법

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
- 지금 통과해야 하는 수: EditMode 67개, PlayMode 6개(`SetupSmokePlayTests` 1개, `CombatPrototypePlayTests` 5개). `UnitScaleBenchmark`는 Explicit라서 평소에는 돌지 않는다.

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
