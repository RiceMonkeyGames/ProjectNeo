# Collaboration, Backlog & Task ID Rules

> Purpose: Team workflow, per-task backlog schema, Task ID rules, shared file locks, and `.claude` boundaries. | Parent: [INDEX](../INDEX.md) | Related: [Backlog Stub](../Backlog.md), [Team](../plan/Team.md)

## 1. Backlog Claim & Branch Lifecycle

Active tasks live one file per task under `Docs/backlog/<TASK-ID>.md` (`TOOL-backlog-split`). Do **not** edit [Backlog.md](../Backlog.md) (read-only stub), [Backlog-Done.md](../archive/Backlog-Done.md) (frozen legacy archive), [history/README.md](../history/README.md) (static folder map), or [plan/Status.md](../plan/Status.md) (milestone-gate summary) during routine task claims or completions.

1. List tasks in one call via `Tools/Agent/backlog-list.ps1` (e.g. `-Status Waiting`, `-Area UI`) and open **only** the target `Docs/backlog/<TASK-ID>.md`. Never bulk-read `Docs/backlog/`.
2. Run `Tools/Agent/claim-check.ps1 -TaskId "<TASK-ID>"` (verifies `Docs/backlog/<TASK-ID>.md` lock status and checks `Docs/history/` to prevent reusing completed IDs).
3. On synced `develop`, update (or create) **only** `Docs/backlog/<TASK-ID>.md`: set `status: In Progress`, `assignee:` to the human requester's name (never an AI tool name), and `branch: feature/<task-id-lowercase>` (or `fix/<task-id-lowercase>`).
4. **Immediately commit and push** this single task file to shared `develop` before writing code so teammates/agents see the claim. If `origin/develop` moved forward with another teammate's different task file, `git pull --rebase origin develop` merges cleanly without conflicts.
5. Create `feature/<task-id-lowercase>` (or `fix/<task-id-lowercase>`) from that synced `develop`.
6. Never take over or edit another assignee's `In Progress` task file. Coordinate order before touching shared files.
7. Upon completion: update affected `Docs/` leaves and create `Docs/history/<PREFIX>/<TASK-ID>.md`, run `Tools/Agent/doc-finish-check.ps1 -TaskId "<TASK-ID>"`, present verification/manual checks, and obtain explicit user confirmation before removing `Docs/backlog/<TASK-ID>.md` (`git rm`) and committing/pushing the task branch.
8. The user handles `develop` merges and remote task-branch deletion after confirmed integration. Retain unmerged branches. General code commits/pushes require explicit user confirmation; immediate backlog claim commit/push in step 4 remains authorized.
9. **Timestamps (`TOOL-backlog-split`)**: Do **not** add or edit `Last Updated:` header lines on `Docs/` files. Git commit history is the source of truth for modification timestamps; record `YYYY-MM-DD` dates only in task frontmatter (`created:`) and immutable `Docs/history/` headers.
10. **Shared Skill / Wrapper Shortcuts**: Claude Code users and agents may use `.claude/skills/` (`/brief`, `/claim`, `/verify`, `/finish`, `/sync-summary`) backed by Windows PowerShell 5.1 scripts under `Tools/Agent/` (`session-brief.ps1`, `backlog-list.ps1`, `backlog-board.ps1` / `backlog-board.bat`, `claim-check.ps1`, `code-rules-check.ps1`, `unity-verify.ps1`, `doc-finish-check.ps1`, `sync-summary.ps1`) to run session warmup, filtered backlog listing, optional local read-only HTML dashboard generation (written to `%TEMP%\ProjectNeo\backlog-board.html` outside Git), preflight, static coding rule checks, bounded Unity verification, doc/link/frontmatter checks, and remote diffs in one call. `/finish` runs doc checks and presents manual verification steps first, asking user confirmation before removing `Docs/backlog/<TASK-ID>.md` and committing/pushing.
    Codex uses the corresponding `$brief`, `$claim`, `$verify`, `$finish`, `$sync-summary` entrypoints under `.agents/skills/`. All agents (Claude Code, Codex, Antigravity, etc.) share the same `Tools/Agent/` wrappers from the repository root; do not maintain separate workflow implementations.
11. **Claude Code Role Subagents (`.claude/agents/`)**: Keep the main session as the feature implementer, coordinator, and user-approval gate; delegate context-heavy or routine work to three focused subagents:
    - `docs-git` (`model: haiku`, preloads `claim`/`finish`/`sync-summary`): `Docs/` updates, per-task `Docs/backlog/<TASK-ID>.md` claim/removal, history drafts, and git operations. Write scope limited to `Docs/`; general commits/pushes require explicit user confirmation from the main session.
    - `qa-verifier` (`model: sonnet`, preloads `verify`, `Edit`/`Write` disallowed): runs `code-rules-check.ps1` + `unity-verify.ps1` sequentially (respecting the single Unity Editor constraint), audits diffs against `Docs/design/` and `Principles.md`, and returns defect findings plus a manual verification checklist.
    - `repo-scout` (`model: haiku`, `Read`/`Grep`/`Glob` only): fast symbol, call-site, and `Docs/codemap/` lookups to prevent search noise from bloating the main context.
12. **Default Delegation (TOOL-subagent-default, user decision 2026-11-21)**: Claude Code sessions use the role subagents above by default; the user does not need to ask each time.
    - `docs-git`: `/claim` (preflight, `Docs/backlog/<TASK-ID>.md` update), `/finish` removal/history steps, codemap drafts, and general commits/pushes **after** the user approved them in the main session (pass the approved scope: files, message, branch).
    - `qa-verifier`: after each implementation step, `code-rules-check.ps1` plus Unity verification (on this team's PCs the local wrapper if present), diff audit against `Docs/design/` and `Principles.md`, and a manual-check draft. Only one Unity run at a time: the main session does not start its own Unity run while `qa-verifier` is running one.
    - `repo-scout`: searches spanning many files, call-site tracing, codemap/history lookups.
    - Main session keeps: code and asset edits (via Editor APIs), ambiguous design/architecture questions, approval gates, final reporting, and quick one-command checks where a subagent would cost more than it saves.
    - Brief each subagent with the task ID, branch, approved scope and constraints (human assignee name, `Assets/ThirdParty` untouched, no `develop` merges). Treat its report as input to check, not as user approval.
    - Codex has no subagents: it keeps doing the same steps inline with the shared skills.
13. **Delegation Refinements (TOOL-subagent-tuning, user decision 2026-11-21)**: lessons from the first session under item 12.
    - **Main session only**: `develop` merges and conflict resolution (a subagent stopped at its turn limit halfway through a three-branch merge), and any push to `develop`, claim pushes included (the auto-mode permission check denied a subagent's claim push). `docs-git` may still prepare `Docs/backlog/<TASK-ID>.md` and doc files; the main session stages, commits and pushes them.
    - **Small steps inline**: Git or doc steps of a few commands (for example a single commit and push of already-edited files) stay in the main session; delegation pays off for multi-file doc work and long checks.
    - **Shared working tree**: a subagent that switches branches changes the files the main session sees. The main session makes no file edits until that subagent reports back.
    - **Commit trailer**: every commit uses the main-session model in `Co-Authored-By`, also when a subagent runs the commit. If a subagent will not write that trailer, the main session makes the commit.
    - **Check every report**: confirm the result with `git log` / `git status` / file reads before relaying it.

### Per-Task File Schema (`Docs/backlog/<TASK-ID>.md`)

Keep YAML frontmatter within the first 12 lines so `Tools/Agent/` scripts parse only headers without loading prose:

```markdown
---
id: <TASK-ID>
title: Short English Task Title
status: Waiting
assignee: Unassigned
branch:
area: <PREFIX>
depends_on: []
created: YYYY-MM-DD
---

# <TASK-ID> — Short English Task Title

## Scope & Completion Criteria

Concise English scope, prerequisites, and open decisions (relative links use `../design/...`, `../plan/...`, `../codemap/...`).
```

- `id` must match the filename (`<TASK-ID>.md`) exactly.
- Leave `branch:` empty (no trailing space) until claimed.
- `assignee:` accepts Korean or English human names and is exempt from the English-only `Docs/` check; cite verbatim Korean UI text in inline backticks.

### Status Conventions

`Waiting` → `In Progress` → `Waiting for Link` / `Waiting for Verification` → removed on completion (`Docs/history/<PREFIX>/<TASK-ID>.md`).
- `Pending Decision`: Blocked on undecided user design questions.
- `Waiting for Prerequisite`: Blocked on upstream code/feature completion.
- `Docs/backlog/<TASK-ID>.md` owns task claiming and live status; [plan/Status.md](../plan/Status.md) and [plan/Milestones.md](../plan/Milestones.md) own milestone-level roadmap state. Never overwrite another assignee's `In Progress` file.

## 2. Task ID Naming Convention

- **Legacy ID Freeze**: Completed or active referenced IDs (`A-*`, `B-*`, `C-*`, `D-*`, `G-*`, in-progress `C-1a-confirm`) and reserved M3 IDs (`E-1~4` until M3 kickoff) stay as-is to preserve commit/doc links.
- **Allowed Characters**: New IDs use `A-Z`, `0-9`, `a-z`, and `-` only. Do not use `·`, `~`, `.`, spaces, or non-ASCII characters.
- **One Task = One ID**: Never bundle IDs (e.g., `C-1~2`, `Q-04·05`). Use separate files or one primary ID. Never append ad-hoc suffixes like `-play`, `-confirm`, `-action`; create an independent ID instead.
- **New M3+ Feature IDs (`<DOMAIN>-<NN>`)**:
  - `COMBAT-NN`: Direct combat, skills/consumables, hit rules.
  - `SIEGE-NN`: Walls, placement, siege weapons, waves, enemy siege AI, morale/rout.
  - `FLOW-NN`: Scene transitions, run loop, outcome/reward flow.
  - `PREP-NN`: Courtyard facilities, shop, squad org, equipment, crafting, build economy.
  - `META-NN`: Persistent saves, unlocks, perks, run score (M3).
  - `CONTENT-NN`: Stage/hero/enemy/item rosters, bosses, balance (M4).
  - `ART-NN` / `UI-NN` / `AUDIO-NN`: Synty swaps, VFX/animations, production HUD/UX, audio (M5).
- **Cross-Cutting IDs (`<TYPE>-<slug>` / `Q-<NN>`)**:
  - `Q-NN` (design decision, zero-padded), `BUG-<slug>`, `PERF-<slug>`, `QA-<slug>` (manual playtest / regression / release checks), `DOC-<slug>`, `TOOL-<slug>`. Keep `slug` lowercase kebab-case (2–4 words).

## 3. Work Environments & Claude Exclusions

Active collaboration since 2026-11-11 across separate PCs/branches (Q-26). See [plan/Team.md](../plan/Team.md) for current ownership.

| Setup | Parallel Allowed | Sequential Required |
|---|---|---|
| Separate PC / Clone / Branch | Feature code, unit tests, isolated scenes/assets, per-task `Docs/backlog/<TASK-ID>.md` | Shared contract updates, integration merges |
| Shared Folder & Editor | Authoring code/docs in pre-allocated files | Asset creation, scene saves, compile, PlayMode, tests, builds, packages |

Avoid opening one project folder in multiple editor instances; use `--project-path` if multiple editors run on one machine.

**Claude Code read/edit exclusions & allow rules (`.claude/settings.json`)**: Claude Code does not support `.claudeignore`; shared `.claude/settings.json` uses `permissions.deny` and `permissions.allow`.
- Allow listed: PowerShell wrappers under `Tools/Agent/*.ps1` and read-only git inspection (`git status`, `git branch --show-current`, `git submodule status`, `git diff --name-only`, `git diff --stat`, `git log -n`). `git commit` and `git push` remain un-allowed for interactive confirmation.
- Read blocked: `Library/` except `Library/PackageCache/` (explicit path only), `Temp/`, `Logs/` old/worker logs (`*-prev.log`, `AssetImportWorker*`, `*.jsonl`), `UserSettings/`, `obj/`, `Build(s)/`, `*.csproj`, `*.sln(x)`; third-party `Assets/TextMesh Pro/`, `Assets/ThirdParty/`, `Assets/Synty/`, `Assets/Polygon*/`, `Assets/Plugins/`, `Assets/Samples/`; `*.meta`, `*.unity`, `*.shadergraph`, `*.anim`, `*SDF.asset`, `LightingData.asset`, `NavMesh*.asset`; textures under `Assets/`, models, audio, fonts, video, `*.dll`, `*.pdb`, `*.unitypackage`.
- Read allowed: `.cs`, `.asmdef`, `.prefab`, `.mat`, `.controller`, ScriptableObject `.asset`, `ProjectSettings/`, `Packages/`, `Docs/`, `Logs/Editor.log` (search `error CS` only; do not read whole file), images outside `Assets/` (save verification screenshots outside `Assets/`, `Temp/`, `Logs/`).
- Edit/Write blocked: `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.controller`, `*.anim` (Editor API only).
- Inspect scenes, GUIDs, and references via `script eval` (`EditorSceneManager`, `AssetDatabase.GUIDToAssetPath`). If a Synty pack imports under a new root folder, add it to `.claude/settings.json`.

### Third-Party Asset Submodule (`Assets/ThirdParty`)

Licensed third-party assets (Synty packs, paid Asset Store content) live in the **private** repository `RiceMonkeyGames/ProjectNeo-Assets`, mounted as a git submodule at `Assets/ThirdParty` (`.gitmodules` uses the relative URL `../ProjectNeo-Assets.git`, so it follows the main remote's SSH/HTTPS scheme). This keeps the main repository publishable without redistributing licensed files ([TOOL-asset-submodule](../history/TOOL/TOOL-asset-submodule.md)).
- **Import location**: import every third-party package under `Assets/ThirdParty/` (e.g. `Assets/ThirdParty/Synty/...`). Never import licensed assets anywhere else in the main repository: once committed there they stay in its history even after moving.
- **Clone**: `git clone --recurse-submodules <ProjectNeo URL>`; existing clones run `git submodule update --init --recursive`. Each member needs read access to the private repository. Optional: `git config submodule.recurse true` so checkout/pull update the submodule automatically. Agents check `git submodule status` at session start and after pull/merge/branch switch and run the update themselves when it shows `-` or `+` ([AGENTS.md](../../AGENTS.md) "Session Start: Third-Party Asset Submodule").
- **No Git LFS in the asset repository**: third-party packs rarely change, so binaries are stored as regular git objects (`binary` attribute) to avoid LFS storage/bandwidth quotas (initial import ~1.36 GB, ~965 MB of it LFS-type files). Keep every file under GitHub's 100 MB per-file limit. The main repository keeps LFS for team-authored binaries.
- **Bundled libraries**: packs sometimes ship code libraries next to their art (e.g. the HUD pack's `Unity UI Extensions`). Check they compile on the project's Unity version; if not, keep only what the pack references (`AssetDatabase.GetDependencies`) and record it in the task history ([TOOL-asset-submodule](../history/TOOL/TOOL-asset-submodule.md)).
- **Commit order**: commit and push inside `Assets/ThirdParty` first (asset files **with** their `.meta` files), then commit the updated submodule pointer (`git add Assets/ThirdParty`) in the main repository. Pushing the main repository with a pointer to an unpushed submodule commit breaks teammates' checkouts.
- **Ownership**: scenes, prefabs, and materials authored by the team stay in the main repository and reference submodule assets by GUID; without submodule access they open with missing references, which is expected for public clones.
- **License**: confirm the asset license covers every member before granting repository access; never make the asset repository public.

## 4. High-Contention Shared Areas & Integration Pipeline

| Area | Policy |
|---|---|
| `CharacterActor.cs`, `UnitCombatAI.cs` | Combat core junction; managed by primary owner. Others use adapters or rule helpers. |
| `CombatProfile`, Hero/Grunt defs, `StatType` | Serialized data contracts; coordinate field/enum edits beforehand and audit dependent assets. |
| Shared prefabs, Integration scenes, NavMesh | Explicit owner per task. Never text-merge conflicting asset files. |
| `.asmdef`, `Packages/`, `ProjectSettings/`, Build lists | Managed by integration lead; inspect diffs on auto-modified settings. |
| `Docs/backlog/<TASK-ID>.md`, `Docs/history/<PREFIX>/<TASK-ID>.md` | Isolated one file per task; never edit another assignee's `In Progress` file. |
| `Docs/` shared files (`INDEX.md`, `plan/`, `design/`, `codemap/`) | Keep changes scoped to affected leaf files; do not edit frozen/static stubs (`Backlog.md`, `archive/Backlog-Done.md`, `history/README.md`) or append per-task logs to `plan/Status.md`. |

**Integration Pipeline**:
1. Validate contracts and owned files.
2. Develop and verify in isolated scenes/workspaces.
3. Integration order: Contracts → Rules → Scene wiring → UI / Visuals.
4. Run regression EditMode/PlayMode suites upon shared code edits.
5. Verify standalone builds on release milestones.
6. Record outcomes in `Docs/history/<PREFIX>/<TASK-ID>.md` and remove `Docs/backlog/<TASK-ID>.md` after user confirmation. Always provide manual verification walkthroughs (scene path → Play/tests → controls → expected results → reset steps).

```text
# Task Handoff Template
Task ID / Title:
Owner / Integration Lead:
Status: Waiting | Pending Decision | In Progress | Waiting for Link | Waiting for Verification | Done (removed to history)
Prerequisites / Required Decisions:
Owned Files / Locked Files:
Provided Data/Events / Consumed Data/Events:
Current Task Scope / Deferred Follow-up Scope:
Completion Criteria / Actual Verification Results:
Constraints for Next Owner:
```
