# TOOL-codex-workflows | 2026-11-13 | Tooling - Shared Codex Workflow Entrypoints

Last Updated: 2026-11-13 14:41:26 (KST)

## Scope and decisions

- Owner: Codex. Branch `feature/tool-codex-workflows`; backlog claim pushed to develop in `80aee2b` before implementation.
- User requested alignment with teammate Git handoff and reuse of teammate Claude skills in Codex. Final policy: claim commit/push remains automatic; obtain explicit user confirmation before Done/archive/task-branch commit/push; user handles develop merge and remote task-branch deletion. Supersedes earlier blanket AI Git authorization.
- Updated AGENTS.md, Collaboration, Decisions-Pending, Backlog and INDEX. Existing `.claude/skills` and Tools/Agent implementations remain unchanged.
- Added `.agents/skills/{claim,verify,finish,sync-summary}/SKILL.md`. Each has Codex metadata and reads the matching `.claude/skills/<name>/SKILL.md`; both agents share the maintained instructions and PowerShell scripts. Relative links resolve from the skill folder; commands run from repo root. Codex `$name` maps to teammate `/name`; arguments come from the user's request.
- Discovery follows official OpenAI documentation: https://learn.chatgpt.com/docs/build-skills (repo `.agents/skills`, automatic detection, restart if updates do not appear). No global Codex config, user-wide skill install, Unity assets, game code or packages changed.

## Verification

- Official skill-creator `quick_validate.py` passed for all 4 skills. PyYAML was missing; installed only under `%TEMP%/ProjectNeo-skill-validator` and restored process PYTHONPATH afterward. No global Python environment changed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/sync-summary.ps1 -NoFetch`: up to date, 0 ahead/behind (at check time).
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet -DryRun`: expected skip/dry-run output, exit 0. No dotnet build, Unity compilation, tests or Play Mode executed.
- `git diff --check` passed. Shared finish checker: HISTORY_EXISTS=True, UNARCHIVED_DONE_ROWS=0, BROKEN_LINKS=0, GIT_DIFF_CHECK_OK=True. Separately resolved all 4 Codex entrypoint links to their existing shared teammate skill sources.
- Codex host skill catalog discovered and exposed all 4 entrypoints during this chat. Interactive skill-selector UI and end-to-end claim/Unity verification remain untested; they are not required to claim discovery success.
- User explicitly requested completion on 2026-11-13 ("이번작업 마무리"). Marked Done and archived; task branch commit/push authorized. Develop merge and remote branch deletion remain user-owned. The finish workflow itself is being used for this handoff.

## Manual verification / reset

In this repository, open the Codex skill selector and look for claim, verify, finish and sync-summary. If absent, restart Codex and reopen the project. Invoke `$sync-summary` (or say "sync-summary 스킬로 변경 확인") and expect a bounded remote-change summary using the shared PowerShell wrapper. `$claim <TASK-ID>` is a real claim/branch mutation; do not invoke it solely as a harmless discovery check. `$verify` starts real Unity checks by default; use only with an idle Editor. `$finish TOOL-codex-workflows` must present review/verification and ask confirmation before Done/archive/commit/push, leaving develop merge to the user. No game scene reset is needed; the read-only sync-summary changes no working files.
