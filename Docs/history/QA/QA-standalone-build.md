# QA-standalone-build | 2026-11-16 | QA - Standalone Build Startup Flow Check

Last Updated: 2026-11-16 19:20:37 (KST)

Branch: `feature/qa-standalone-build`. Owner: 신용준. Claim synchronized to develop (`docs: claim QA-standalone-build`). No code, scene, or asset changes.

## Finding 1: the default build does not start at Title

The enabled build list is, in order: `ConstructionPrototype`, `UnitBattlePrototype`, `CombatPrototype`, `SampleScene`, `SquadCommandPrototype`, `WallCommandPrototype`, `Title`, `Game`, `SiegePrototype`. `BuildScript.BuildWindows` builds that list as is, so `Builds/Windows/ProjectNeo.exe` boots into `ConstructionPrototype` and has no way to reach `Title`. The build list was **not** changed: changing the first build scene requires notifying the teammate first ([Team](../../plan/Team.md)). Decision needed from the user: move `Title` to index 0 (and optionally drop prototype scenes from the player build).

**Resolved (user decision, 2026-11-16)**: the user chose to make `Title` the first build scene. `Title` was moved to index 0 through `EditorBuildSettings.scenes` (Editor API); the other scenes keep their order and stay enabled: `Title`, `ConstructionPrototype`, `UnitBattlePrototype`, `CombatPrototype`, `SampleScene`, `SquadCommandPrototype`, `WallCommandPrototype`, `Game`, `SiegePrototype`. Only `ProjectSettings/EditorBuildSettings.asset` changed. No code reads scenes by build index. The default build `Builds/Windows/ProjectNeo.exe` was rebuilt with `BuildScript.BuildWindows` and boots without errors (40 log lines, no exception). `GameFlow` PlayMode **13/13** passed afterwards. The teammate still has to be told about the first-scene change ([Team](../../plan/Team.md)); the agent cannot do that.

## Finding 2: restoring the URP global settings without a reimport breaks the next build

After the first build the agent restored the two build-modified files with `git checkout` while the Editor stayed open. The next build reported success, but the player could not create the render pipeline: `Cannot create required material because shader is null` and a `NullReferenceException` in `Blitter.Initialize` every frame (about 51,000 in 20 s). Reimporting `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` before building fixed it; the sequence restore → reimport → build → boot was then verified once more (40 log lines, no exception). The rule is recorded in [UnityCLI](../../workflow/UnityCLI.md). The working tree was left restored and reimported.

## What was built

To check the real game flow without touching project settings, a verification player was built through the open Editor with the same enabled scenes but `Title` moved to the front, into a separate git-ignored folder:

- Output: `Builds/WindowsTitleFirst/ProjectNeo.exe` (StandaloneWindows64, `BuildOptions.None`).
- Result: `Succeeded`, 0 errors, 499 warnings, 130 MB, 23 s.
- The build modified `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` and `ProjectSettings/ProjectSettings.asset` (known side effect, see [UnityCLI](../../workflow/UnityCLI.md)); both were restored with `git checkout`.

## Verified (automated, by the agent)

- The player process starts windowed (1280×720, D3D12, RTX 4060) and stays alive for 20 s with `Title` as the first scene.
- Player log (40 lines after 20 s): no exception, no `NullReference`, no script error.
- Log warnings only: `d3d12: failed to query info queue interface` (debug layer absent, harmless) and four "shader is not supported or has been stripped" lines for unused URP post-processing passes (`GaussianDepthOfField` ×2, `BokehDepthOfField`, `PaniniProjection`). No depth-of-field or Panini volume is used, so nothing visible is expected to be missing; not confirmed on screen.
- Starting the player did not create or change files in `persistentDataPath/Saves` (existing files there date from the META-01 manual check).

## Not verified

The agent could not click inside the built player (the desktop-control tool only accepts installed applications), so everything past the first frame is unverified: Title rendering and Korean font, slot selection, `New Game` → `Game` load, preparation, hold `B`, battle, result/reward, `Return to Title`, the META-01 save on reward and on real quit, and `Quit`. No tests were re-run for this task.

## Closeout (2026-11-16)

The user played the default build and reported two findings: Title has no "Continue" although the slot file remains (registered as `FLOW-02`), and the runtime English → Korean UI swap is unwanted (decision recorded, cleanup registered as `UI-02`). The user then confirmed `Done`. The agent did not observe the in-player click-through; which of the steps below the user covered is not recorded.

## Manual check (user)

1. Run `Builds\Windows\ProjectNeo.exe` (the default build, now Title-first; the temporary `Builds\WindowsTitleFirst` folder was deleted).
2. Title: three slot rows, `새 게임`, `고른 슬롯 삭제`, `종료` are readable in Korean. Select slot 3 and click `새 게임`.
3. Game: preparation starts in first person; walk with WASD, press F at the construction station and Esc to return.
4. Hold `B` for 1 second. Expected: battle starts, enemies come from the north in waves.
5. Let the battle end. Victory → reward screen with commander XP → `보상 받고 다음 스테이지` returns to preparation. Defeat → result screen → `타이틀로` returns to Title.
6. After one victory, quit with `종료` (or Alt+F4). Expected: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\ProjectNeo\Saves\meta_slot3.json` exists; starting the player again shows slot 3 preselected with the saved commander level.
7. Reset: delete `meta_slot3.json` (and its `.bak`) or use `고른 슬롯 삭제` on slot 3.
