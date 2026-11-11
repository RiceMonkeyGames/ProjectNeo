# ProjectNeo

수성전 로그라이크 게임. Unity 6000.6.8f1, URP, 3D 3인칭.

## 문서

기록은 md 파일이 기준이다. 구글 독스는 사람이 읽는 용도라서 읽으러 가지 않는다. 기획이나 계획이 바뀌면 아래 md를 먼저 고친다.

- `Docs/GameDesign.md`: 기획 전체와 미정 사항. 구현 전에 읽는다.
- `Docs/ImplementationPlan.md`: 구현 순서, 구현에 영향을 주는 결정, 진행 상태, 작업 원칙(아트 교체 대비 포함).

기획이 애매하거나 빠진 부분은 임의로 정하지 말고 사용자에게 먼저 묻는다.

## 코드 구조

- `Assets/_Project/Scripts/Runtime/<시스템>/`: 게임 코드. 어셈블리 `ProjectNeo.Runtime`, 네임스페이스 `ProjectNeo.<시스템>`.
- `Assets/_Project/Scripts/Editor/`: 에디터 전용 코드. `ProjectNeo.Editor`.
- `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`: 테스트.
- 규칙과 계산은 화면과 분리된 일반 C# 클래스로 만들고 EditMode 테스트로 검증한다.
- 수치는 ScriptableObject 에셋에 둔다. 테스트에서 쓰도록 각 정의 클래스에 `Create(...)` 정적 메서드를 둔다.
- ScriptableObject와 MonoBehaviour는 파일 하나에 클래스 하나, 파일 이름은 클래스 이름과 같게 한다.
- 랜덤은 `IRandomSource`로만 쓴다.

## Unity CLI 작업 방법

에디터가 열려 있는 상태에서 작업한다. 모든 `unity command` 호출에 `--caller plugin --skill unity-cli`를 붙인다.

- 파일을 쓴 뒤 컴파일:
  `unity command script eval --caller plugin --skill unity-cli --result-only 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); "requested"'`
  그 뒤 `unity status --until-ready --timeout 30`로 기다린다. `unity recompile`과 `unity command script recompile`은 이 프로젝트에서 멈추거나 실패했다.
- 컴파일 결과 확인: `unity command script eval ... '"failed=" + UnityEditor.EditorUtility.scriptCompilationFailed'`. 에러 내용은 `Logs/Editor.log`에서 `error CS`를 찾는다.
- 테스트: `unity command test run --caller plugin --skill unity-cli --mode editor --result-only` (PlayMode는 `--mode playmode`, 결과는 `unity command test status`로 확인).
- `unity test`와 `unity build --execute-method`는 에디터가 열려 있으면 거부된다. 빌드는 `unity command script eval`로 `ProjectNeo.Editor.BuildScript.BuildWindows()`를 부른다.
- 폴더, 씬, 프리팹, 에셋 이동은 파일을 직접 고치지 말고 에디터 명령(`script eval`의 `AssetDatabase`)으로 한다.
- 셸에서 heredoc으로 한글이 든 C# 파일을 여러 개 쓰면 깨진다. 파일은 Write 도구로 쓴다.
