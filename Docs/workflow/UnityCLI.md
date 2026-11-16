# Unity CLI Workflow

Last Updated: 2026-11-16 17:37:56 (KST)

> Purpose: Unity CLI 1.0.0-beta.17 commands for compile, tests, benchmarks, builds, and Editor-API asset edits. | Parent: [INDEX](../INDEX.md) | Related: [Principles](Principles.md), [Scenes](../codemap/Scenes.md)

Interact with open editor instances using `unity` (`com.unity.pipeline`). Check `unity status` to confirm `ready`. Do not assume editor is closed solely from `unreachable` (verify sandbox local socket restrictions). Flag `--caller plugin --skill unity-cli` is an invocation tag with no side effects.

> **Fast Wrapper (`/verify`)**: Use `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 [-Mode all|edit|play|compile] [-Filter <substr>]` (or `/verify`) for `dotnet build` 1차 사전 검사 + `unity status` 가드 + Refresh/컴파일 + `Logs/Editor.log` `error CS` 필터링 + EditMode/PlayMode 실행을 단일 bounded 출력으로 처리한다.

## Compilation

Editors may pause compilation after external file writes. Request compilation explicitly:

```bash
unity command script eval --caller plugin --skill unity-cli --result-only 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); "requested"'
unity status --until-ready --timeout 30
unity command script eval --caller plugin --skill unity-cli --result-only '"failed=" + UnityEditor.EditorUtility.scriptCompilationFailed + " compiling=" + UnityEditor.EditorApplication.isCompiling'
```

- `unity status --until-ready` may report transient failures during reload; repeat until `editor status` returns `ready`.
- Check compilation errors in `Logs/Editor.log` searching for `error CS` (do not read whole log).
- Do not use `unity recompile` or `unity command script recompile` (known to hang or fail in this project).

## Testing

```bash
# Full EditMode suite (synchronous output)
unity command test run --caller plugin --skill unity-cli --mode editor --result-only

# PlayMode suite (runs asynchronously in background; poll status)
unity command editor set_auto_tick --caller plugin --skill unity-cli
unity command test run --caller plugin --skill unity-cli --mode playmode --async-tests --result-only
unity command test status --caller plugin --skill unity-cli --result-only
```

- PlayMode may still be exiting when status reports `completed`. Verify `UnityEditor.EditorApplication.isPlaying == false` via `script eval` before starting EditMode.
- Always execute `editor set_auto_tick` before running PlayMode tests to prevent background frame throttling.
- Use `--filter <substring>` to target specific tests. `[Explicit]` tests require `--include-explicit`.
- `unity test` without `command` is rejected when editor is open.

When validating PlayMode visuals manually, enable background execution temporarily:

```bash
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --enabled true --result-only
# Verify visuals and frame ticks; exit PlayMode and restore setting:
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --enabled false --result-only
```

## AI Query Benchmark & Input Test Environment

- **AI Query Benchmark (`PERF-nav-query`)**: `Assets/Tests/PlayMode/UnitAIQueryBenchmark.cs` (`MeasureRealAIAt50v50And200v200`). Run sequentially in an idle editor:
  `unity command editor set_auto_tick --caller plugin --skill unity-cli` then `unity command test run --caller plugin --skill unity-cli --mode playmode --filter UnitAIQueryBenchmark --include-explicit --async-tests --result-only`.
  Outputs `Logs/benchmark_nav_query.csv` (50v50 and 200v200 Move, idle Hold, contact Hold, Engage; 30 warm-up + 120 measured frames at 1/30s; AI CPU dispatch only, excluding steering/physics/rendering). Teardown restores scene and settings.
- **Input Test Environment (`BUG-playmode-input-focus`)**: F/I/Esc methods in `GameEquipmentPlayTests`, `GameFacilityPlayTests`, and `GameFlowPlayTests` use `FocusIndependentInputScope` (`using` disposal snapshots/restores `backgroundBehavior` and `editorInputBehaviorInPlayMode`; never replace the InputSettings object or Unity destroys its `HideAndDontSave` instance). Verified by `FocusIndependentInputScopeTests`.

## Building & Packages

Standalone builds are rejected if the editor is open. Trigger build inside the open editor (output: `Builds/Windows/ProjectNeo.exe`, git-ignored):

```bash
unity command script eval --caller plugin --skill unity-cli --timeout 1500 --result-only 'ProjectNeo.Editor.BuildScript.BuildWindows(); "done"'
```

Install packages via:

```bash
unity command package add --caller plugin --skill unity-package-management --name <package_id> --confirm --wait --result-only
```

## Scenes, Assets & General Caveats

- Do not edit `.unity`, `.prefab`, `.asset`, or `.meta` files directly. Manipulate via Editor APIs (`AssetDatabase`, `PrefabUtility`, `EditorSceneManager`) via `script eval` or helper `.cs` scripts.
- `.cs` and `.asmdef` files may be authored directly; `.meta` files are generated by editor. Empty folders under `Assets/` contain `.gitkeep`; remove `.gitkeep` when adding files.
- Shell heredocs containing non-ASCII characters across batch C# file generation have caused encoding corruption; write files individually via dedicated tools.
- Do not merge `2>&1` when parsing CLI output; read stdout only.
- Standalone builds may automatically mutate URP assets under `Assets/Settings/` and files in `ProjectSettings/`.
- **Restore then reimport (QA-standalone-build)**: if you `git checkout` `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` after a build while the Editor stays open, reimport it before the next build (`UnityEditor.AssetDatabase.ImportAsset("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset", UnityEditor.ImportAssetOptions.ForceUpdate); UnityEditor.AssetDatabase.Refresh();`). Without the reimport the next build succeeds but the player cannot create the render pipeline (`Cannot create required material because shader is null`, repeating `NullReferenceException` in `Blitter.Initialize`). Always boot the player once and check its log after building.
## Destination-Wall Collapse Regression

- Source: `Assets/Tests/PlayMode/ConstructionPlayTests.cs`; methods `MoveDirective_ReplansWhenDestinationWallDisappears` and `EngageDirective_ReplansWhenDestinationWallDisappears`. Each has rebuild-delay cases 0–7 frames at 1/30s. Expected: all 16 reach within 0.6m of the exposed ground under the destroyed wall and move >1m. Move ends in Hold; Engage stops while retaining Engage.
- Manual repeat: Window > General > Test Runner > PlayMode, filter `ReplansWhenDestinationWallDisappears`, Run Selected on both methods/all cases. Tests load `Assets/_Project/Scenes/ConstructionPrototype.unity`, order the unit toward cell (5,8), destroy that destination wall, then vary rebuild start timing. They unload the scene and restore capture time/cursor. If interrupted, stop Play Mode and reopen ConstructionPrototype to reset. No physical input or standalone build verification is implied.
- CLI: `unity command editor set_auto_tick --caller plugin --skill unity-cli`, then `unity command test run --caller plugin --skill unity-cli --mode playmode --filter ReplansWhenDestinationWallDisappears --async-tests --result-only`; poll `unity command test status --caller plugin --skill unity-cli --result-only` to completed and verify Play Mode has exited before another operation.
