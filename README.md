# ProjectNeo

Unity 6 / URP로 만드는 1인칭 공성 방어 로그라이크입니다. 다음 웨이브를 정찰하고 영웅·병력·장비·성벽을 준비한 뒤, 직접 전투와 부대 지휘로 성을 지킵니다.

## 요구 사항

- Windows 10/11 x64
- Unity 6000.6.8f1 (URP 17)
- `ProjectNeo-Assets` 저장소 읽기 권한 (Synty 등 유료 에셋 서브모듈)

## 시작하기

```powershell
git clone --recurse-submodules <저장소 주소>
git submodule update --init --recursive
```

Unity Hub에서 프로젝트를 열고 `Assets/_Project/Scenes/Title.unity`를 실행하면 됩니다.

## 폴더 구조

```
Assets/_Project/Scripts/Runtime   게임 코드 (전투, 공성, 성벽, 부대, 경제, 런 흐름, UI 등)
Assets/_Project/Scripts/Editor    에디터 도구
Assets/_Project/Data              ScriptableObject 수치 데이터
Assets/_Project/Scenes            Title·Game 씬과 프로토타입·검증 씬
Assets/Tests                      EditMode / PlayMode 테스트
Assets/ThirdParty                 외부 에셋 (비공개 서브모듈)
Docs                              기획, 계획, 작업 백로그, 기록
Tools/Agent                       백로그·검증용 PowerShell 스크립트
```

## 주요 기능

- 1인칭 직접 전투와 부대 지휘
- 웨이브 정찰 → 준비 → 공성 방어로 이어지는 런 흐름
- 성벽·시설 건설과 배치, 공중 유닛을 포함한 적 공성
- 영웅·병력·장비 준비, 특성 트리와 메타 진행

## 테스트

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all
```

## 문서

작업 전에 [Docs/INDEX.md](Docs/INDEX.md)에서 필요한 문서를 찾습니다. 협업 규칙은 [AGENTS.md](AGENTS.md)를 참고하세요.

## 사용 에셋·패키지

Synty (예정, 현재 임시 그래픽), Input System, AI Navigation, TextMesh Pro, Unity UI Extensions
