# TOOL-agent-workflows | 2026-11-13 | Tooling - Shared Agent Skills & PowerShell Workflow Wrappers

> Task ID: `TOOL-agent-workflows` | Branch: `feature/tool-agent-workflows` | Assignee: Hwang Seong-min (agent)

## Goal & Scope
Reduce repeated token/turn overhead and manual command chaining across the team (all on Windows) by introducing shared Claude Code skills under `.claude/skills/` backed by PowerShell 5.1 wrappers under `Tools/Agent/` and `.claude/settings.json` `permissions.allow` rules.

## Implemented Artifacts

### PowerShell 5.1 Wrappers (`Tools/Agent/`)
- `_common.ps1`: Shared repo root resolution, exact KST timestamp helper (`Get-KstTimestamp`), bounded stdout line writer (`Write-BoundedLines`), stdout-only command runner (`Invoke-StdoutOnly`), and Task ID -> history folder prefix mapper (`Get-TaskPrefix`).
- `claim-check.ps1`: Validates Task ID against `^[A-Za-z0-9-]+$`, checks `origin/develop` and working tree cleanliness, checks `Docs/Backlog.md` row status/assignee conflicts (exit `3` on existing `In Progress`/`Done`, exit `4` on invalid ID), outputs `KST_NOW` and `SUGGESTED_BRANCH`.
- `unity-verify.ps1`: Runs optional `dotnet build` 1차 pre-check (`ProjectNeo.Tests.EditMode.csproj` / `ProjectNeo.Runtime.csproj`), verifies `unity status` (`isPlaying`/`isCompiling` guard), triggers `AssetDatabase.Refresh` + `RequestScriptCompilation`, filters `Logs/Editor.log` for `error CS` without reading the whole log, and runs EditMode / PlayMode (`set_auto_tick`, async polling, `isPlaying == false` wait) with bounded error lines. Supports `-DryRun` and `-SkipDotnet`.
- `doc-finish-check.ps1`: Outputs `KST_NOW`, checks changed/uncommitted files, checks `Docs/history/<PREFIX>/<TASK-ID>.md` existence, counts unarchived `Done` rows in `Docs/Backlog.md`, verifies markdown file links across `AGENTS.md` and `Docs/**/*.md`, and runs `git diff --check`.
- `sync-summary.ps1`: Summarizes `HEAD..origin/develop` commits, changed files, `Docs/Backlog.md` row diffs, and `Assets/_Project/Scripts/Runtime/` `public`/`internal` signature diffs in bounded output.

### Claude Code Skills (`.claude/skills/`) & Permissions
- Added `/claim` ([claim/SKILL.md](../../../.claude/skills/claim/SKILL.md)), `/verify` ([verify/SKILL.md](../../../.claude/skills/verify/SKILL.md)), `/finish` ([finish/SKILL.md](../../../.claude/skills/finish/SKILL.md)) — enforces Phase 1 technical/doc check + manual verification guide + explicit user confirmation before marking `Done`/archiving/pushing, and `/sync-summary` ([sync-summary/SKILL.md](../../../.claude/skills/sync-summary/SKILL.md)).
- Updated [.claude/settings.json](../../../.claude/settings.json) with `"allow"` entries for `powershell` / `powershell.exe` execution of `Tools/Agent/*.ps1` and read-only git inspection commands (`git status`, `git diff --name-only`, `git diff --stat`, `git log -n`), leaving `git commit` and `git push` un-allowed so user confirmation/rules apply.

### Documentation Updates
- Updated [Docs/INDEX.md](../../INDEX.md), [Docs/workflow/Collaboration.md](../../workflow/Collaboration.md), [Docs/workflow/UnityCLI.md](../../workflow/UnityCLI.md), [Docs/history/README.md](../README.md), [Docs/Backlog.md](../../Backlog.md), and [Docs/archive/Backlog-Done.md](../../archive/Backlog-Done.md).

## Verification Results (Observed Scope)
- PowerShell script CLI tests executed in isolated worktree (`feature/tool-agent-workflows`):
  - `.claude/settings.json` JSON parse validation via `ConvertFrom-Json`
  - `claim-check.ps1`: verified valid new ID (exit 0), `In Progress` conflict detection (exit 3), invalid ID rejection (exit 4)
  - `unity-verify.ps1 -DryRun`: verified command plan generation and graceful `DOTNET_PRECHECK` skip behavior in worktree without `.csproj`
  - `sync-summary.ps1 -NoFetch`: verified bounded summary output
  - `doc-finish-check.ps1 -TaskId TOOL-agent-workflows`: verified `HISTORY_EXISTS=True`, `UNARCHIVED_DONE_ROWS=0`, `BROKEN_LINKS=0`, `GIT_DIFF_CHECK_OK=True`
- Live Unity Editor verification on merged `develop` (`2026-11-13 14:06:03 (KST)`):
  - Caught and fixed three real-environment issues in `Tools/Agent/unity-verify.ps1` and `Tools/Agent/_common.ps1`:
    1. `dotnet build --no-restore` failed with `NETSDK1004` before `Temp/obj/<Project>/project.assets.json` existed; switched to conditional restore (`--no-restore` only when `project.assets.json` exists) + `DOTNET_NOLOGO=1`.
    2. `unity command script eval` argument parsing broke on quotes/spaces inside `ProcessStartInfo`; replaced with quote-free/space-free C# expressions and JSON `.success`/`.result` parsing (`ConvertFrom-Json`).
    3. `Invoke-StdoutOnly` switched `StandardError` to `ReadToEndAsync()`, and `unity-verify.ps1` now parses EditMode/PlayMode JSON `Summary`/`Results` cleanly into 1-line summaries.
  - Verified live against open Unity 6000.6.8f1 (`port 7800/7801`, `ready`):
    - `Tools/Agent/unity-verify.ps1 -Mode edit`: `DOTNET_PRECHECK=PASSED (ProjectNeo.Tests.EditMode.csproj)`, `COMPILE=PASSED`, `EDITMODE=PASSED (Total=216 Passed=216 Failed=0 Skipped=0 Duration=3.21s)` (exit `0`).
    - `Tools/Agent/unity-verify.ps1 -Mode play -Filter FocusIndependentInputScopeTests`: `DOTNET_PRECHECK=PASSED`, `COMPILE=PASSED`, `PLAYMODE=PASSED (Total=2 Passed=2 Failed=0 Skipped=0 Duration=0.07s)` (exit `0`).
- Still left to user verification: full un-filtered PlayMode suite via `unity-verify.ps1 -Mode play` and interactive Claude Code `/claim`, `/verify`, `/finish`, `/sync-summary` execution.
