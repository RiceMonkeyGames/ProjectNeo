---
name: verify
description: Run Unity verification (dotnet build pre-check, unity status guard, Refresh/compile, Logs/Editor.log CS error filter, EditMode and/or PlayMode tests) with bounded output. Use after code changes or when running /verify.
disable-model-invocation: false
---

# /verify — Unity & Dotnet Verification

Run `Tools/Agent/unity-verify.ps1` instead of ad-hoc multi-step Unity/log commands.

## Arguments
- `$ARGUMENTS`: Optional flags mapped cleanly to the script:
  - Default (empty): `-Mode all` (`dotnet build` 1차 사전 검사 -> Unity compile -> EditMode -> PlayMode).
  - `edit`: `-Mode edit`
  - `play`: `-Mode play`
  - `compile`: `-Mode compile`
  - Optional test filter substring: `-Filter "<substring>"`
  - Skip `dotnet build` pre-check if needed: `-SkipDotnet`

## Steps

1. Execute the wrapper (single command):
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all
   ```
   (Adjust `-Mode` or pass `-Filter` based on `$ARGUMENTS` and task scope: pure C# tasks may use `-Mode edit`; MonoBehaviour/scene tasks use `-Mode all` or `-Mode play`.)

2. Inspect the structured summary lines (`DOTNET_PRECHECK`, `COMPILE_RESULT`, `EDITMODE_RESULT`, `PLAYMODE_RESULT`, and bounded error lines if non-zero exit):
   - Do **not** read `Logs/Editor.log` in full.
   - If `unity status` reports `isPlaying=true`, `isCompiling=true`, or another editor lock, stop and notify the user (recall `Docs/plan/Team.md` concurrency rule).
   - If `dotnet build` catches a compile error, fix the C# error before invoking Unity.

3. Report exact verified scope (which step ran, exact command, pass/fail result). Never mark manual playtest or standalone build as verified unless actually executed.
