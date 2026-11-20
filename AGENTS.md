# ProjectNeo

Last Updated: 2026-11-21 01:35:59 (KST)

A 3D first-person siege defense roguelike built with Unity 6000.6.8f1 and URP. Scout the upcoming wave, prepare heroes, troops, equipment, and walls, and defend the castle through direct player combat and unit commanding. Low-poly art style; Synty assets planned, currently using placeholder graphics.

This file is the primary entry point for all agents (`CLAUDE.md` imports this file via `@AGENTS.md`).

## Documents to Read First

Do **not** bulk-read `Docs/`. Read only:
1. [Docs/INDEX.md](Docs/INDEX.md) — Routing map from task type to leaf docs.
2. [Docs/Backlog.md](Docs/Backlog.md) — Active tasks, assignments, and statuses (`Done` tasks in [Docs/archive/Backlog-Done.md](Docs/archive/Backlog-Done.md)).
3. Only the specific leaf documents under `Docs/design/`, `Docs/plan/`, `Docs/codemap/`, `Docs/workflow/`, or `Docs/history/` required for your task.

Precedence on conflict: `Docs/design/` (game rules) > `Docs/plan/` (roadmap/status/decisions) > `Docs/workflow/` & `Docs/codemap/` (execution/code map) > `Docs/Backlog.md` (claims/live status).

## Session Start: Third-Party Asset Submodule

Licensed assets (Synty packs, etc.) live in the private submodule `Assets/ThirdParty` (repo `RiceMonkeyGames/ProjectNeo-Assets`). At the start of every session, and after any `git pull`, `git merge`, or branch switch:
1. Run `git submodule status`. A leading `-` means not initialized; a leading `+` means the checked-out commit differs from the one recorded by the main repository; a missing `Assets/ThirdParty` entry means the current branch predates the submodule (no action).
2. If `-` or `+`, run `git submodule update --init --recursive` without asking (it only downloads/checks out the recorded commit; no commits or pushes). If the Unity Editor is open, tell the user so the Editor reimports (or run `AssetDatabase.Refresh` per [Docs/workflow/UnityCLI.md](Docs/workflow/UnityCLI.md)).
3. If it fails with an authentication or "repository not found" error, stop and tell the user they need read access to `ProjectNeo-Assets`; do not retry, change the submodule URL, or edit `.gitmodules`.
4. Never commit inside `Assets/ThirdParty` or move its pointer unless the task is about third-party assets. Details: [Collaboration § Third-Party Asset Submodule](Docs/workflow/Collaboration.md).

## Backlog, Task IDs & Branch Workflow

- **Claim & Sync**: Pick a `Waiting` task in [Docs/Backlog.md](Docs/Backlog.md), mark `In Progress` with your assignee name, and **immediately commit and push to `develop`** before coding. Never take over another assignee's `In Progress` item.
- **Human Task Ownership**: Record the human requester as task assignee/owner, never the AI tool name (such as Codex or Claude). Apply this to future backlog claims and task history; preserve existing records and other teammates' ownership unless explicitly asked to correct them.
- **Branch Lifecycle**: From synced `develop`, create `feature/<task-id-lowercase>` (or `fix/<task-id-lowercase>`) and record it in the backlog. General code commits and task-branch pushes require explicit user confirmation after presenting the result. The user handles `develop` merges and remote task-branch deletion after confirmed integration. Retain unmerged work branches. Immediate backlog claim commit/push remains the exception authorized above.
- **Task ID Rules**: Allowed characters `A-Z`, `0-9`, `a-z`, `-` only; 1 task = 1 ID (no `~`, `·`, `.`, or ad-hoc suffixes). Legacy `A~D`, `G`, and reserved `E-1~4` (until M3) remain as-is. New M3+ features use `<DOMAIN>-<NN>` (`COMBAT`, `SIEGE`, `FLOW`, `PREP`, `META`, `CONTENT`, `ART`, `UI`, `AUDIO`); cross-cutting tasks use `Q-NN`, `BUG-<slug>`, `PERF-<slug>`, `QA-<slug>`, `DOC-<slug>`, `TOOL-<slug>`. Full rules in [Docs/workflow/Collaboration.md](Docs/workflow/Collaboration.md).
- **Completion**: Update affected `Docs/` leaves and `Docs/history/<PREFIX>/<TASK-ID>.md`, present verification results and manual checks, then obtain user confirmation before marking `Done`, archiving, and committing/pushing the task branch. Follow the shared finish skill; leave `develop` merge to the user.

## Shared Workflow Skills & Subagents

- Claude Code: `.claude/skills/` (`/brief`, `/claim`, `/verify`, `/finish`, `/sync-summary`). Codex: `.agents/skills/` (`$brief`, `$claim`, `$verify`, `$finish`, `$sync-summary`).
- Codex entrypoints read the matching `.claude/skills/<name>/SKILL.md`; both reuse `Tools/Agent/` PowerShell wrappers (`session-brief.ps1`, `claim-check.ps1`, `code-rules-check.ps1`, `unity-verify.ps1`, `doc-finish-check.ps1`, `sync-summary.ps1`). Keep workflow logic in those shared sources, not duplicated copies. Skills and subagents do not expand Git authorization.
- Claude Code subagents (`.claude/agents/`): `docs-git` (`haiku`, Docs/Git workflow only), `qa-verifier` (`sonnet`, read-only static/Unity verification & defect audit), and `repo-scout` (`haiku`, read-only symbol/doc lookup). Main session coordinates implementation, sequential Unity runs, and user approval gates ([Collaboration](Docs/workflow/Collaboration.md)).
- **Default delegation (Claude Code)**: This file is the user's standing request to use these subagents; do not wait to be asked. Delegate (1) Backlog claim/archive, history/codemap/`Last Updated` doc updates, and user-approved commits/pushes to `docs-git`; (2) post-implementation verification (code rules, Unity tests, diff audit against design, manual-check draft) to `qa-verifier`; (3) lookups spanning many files or docs to `repo-scout`. Keep in the main session: code edits, design questions to the user, approval gates, and one-command checks. Delegation never widens Git authorization; review each subagent report before acting on it or relaying it. Codex has no subagents and keeps working inline. Details: [Collaboration § Role Subagents](Docs/workflow/Collaboration.md).

## Working Rules

- Respond to the user in Korean, using clear and simple language.
- Write all `Docs/` documentation in English (preserve exact code identifiers or verbatim test output strings only when citing them).
- Use `Last Updated: YYYY-MM-DD HH:MM:SS (KST)` whenever editing docs; query the exact current time (never guess). When bulk-updating timestamps, replace only header lines (`^Last Updated:`), never this rule line.
- Never decide ambiguous design/architecture questions arbitrarily; present options with rationale and ask the user first. Record decisions immediately in `Docs/design/` and `Docs/plan/`.
- Always guide the user on manual verification upon completion (exact scene/file paths, controls, expected results, reset steps; test names/CLI commands for logic tasks). Never stop at automated test counts alone.
- Collaborating since 2026-11-11 (teammate: enemy assault/combat execution; user: game entry/core flow). Coordinate sequential edits on shared files/assets ([Docs/plan/Team.md](Docs/plan/Team.md)); never run compile, PlayMode, tests, or builds concurrently in one editor.
- Do not mix game code or unnecessary Unity operations into doc-only tasks. Distinguish verified scope: rules, integration, manual play, standalone builds.

## Implementation & Tool Principles

- Separate pure C# rules (EditMode) from MonoBehaviours/scene behavior (PlayMode/manual play) and from visuals under a `Visual` child ([Docs/workflow/Principles.md](Docs/workflow/Principles.md)).
- Store numerical values in ScriptableObjects with `Create(...)` factories; one class per file matching class name; explicit persistent enum values; route randomness via `ProjectNeo.Core.IRandomSource`.
- Never hand-edit `.unity`, `.prefab`, `.asset`, or `.meta` files; use Editor APIs (`script eval` or helper `.cs`). `.cs` and `.asmdef` may be authored directly.
- Shared `.claude/settings.json` enforces read/edit exclusions for generated/binary/serialized files (search `Logs/Editor.log` for `error CS` rather than reading whole). Agents not bound by it (e.g., Codex) must follow the same boundaries by convention ([Docs/workflow/Collaboration.md](Docs/workflow/Collaboration.md)).
- Check `unity status` first; follow [Docs/workflow/UnityCLI.md](Docs/workflow/UnityCLI.md) for compile/test/build. Never run `unity recompile` or `unity command script recompile`.
