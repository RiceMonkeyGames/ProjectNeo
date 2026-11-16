# Collaboration, Backlog & Task ID Rules

Last Updated: 2026-11-16 21:12:35 (KST)

> Purpose: Team workflow, Task ID rules, shared file locks, and `.claude` boundaries. | Parent: [INDEX](../INDEX.md) | Related: [Backlog](../Backlog.md), [Team](../plan/Team.md)

## 1. Backlog Claim & Branch Lifecycle

1. Inspect [Backlog.md](../Backlog.md) and pick an unclaimed (`Waiting`) task.
2. Mark `In Progress` with the human requester's name as assignee in `Docs/Backlog.md`, not the AI tool name. Use the same human owner in future task history; do not rewrite past records or other teammates' ownership without an explicit request.
3. **Immediately commit and push** this backlog change to shared `develop` before writing code so teammates/agents see the claim.
4. Sync latest shared `develop`, create `feature/<task-id-lowercase>` (or `fix/<task-id-lowercase>`) from it, and record the branch name in `Docs/Backlog.md`.
5. Never take over or edit another assignee's `In Progress` task. Coordinate order before touching shared files.
6. Upon completion: update affected `Docs/` leaves and `Docs/history/<PREFIX>/<TASK-ID>.md`, present verification/manual checks, and obtain explicit user confirmation before marking `Done`, moving the row to [Backlog-Done.md](../archive/Backlog-Done.md), and committing/pushing the task branch.
7. The user handles `develop` merges and remote task-branch deletion after confirmed integration. Retain unmerged branches. General code commits/pushes require explicit user confirmation; immediate backlog claim commit/push in step 3 remains authorized.
8. **Shared Skill / Wrapper Shortcuts**: Claude Code users and agents may use `.claude/skills/` (`/claim`, `/verify`, `/finish`, `/sync-summary`) backed by Windows PowerShell 5.1 scripts under `Tools/Agent/` (`claim-check.ps1`, `unity-verify.ps1`, `doc-finish-check.ps1`, `sync-summary.ps1`) to run preflight, bounded verification, link/history checks, and remote diffs in one call. `/finish` runs doc checks and presents manual verification steps first, asking user confirmation before marking `Done`, archiving, and committing/pushing.
   Codex uses the corresponding `$claim`, `$verify`, `$finish`, `$sync-summary` entrypoints under `.agents/skills/`. These load the same `.claude/skills/<name>/SKILL.md` and use the same wrappers from the repository root; do not maintain separate workflow implementations. If a new skill does not appear in Codex, restart Codex and check its skill selector.

### Status Conventions

`Waiting` → `In Progress` → `Waiting for Link` / `Waiting for Verification` → `Done`.
- `Pending Decision`: Blocked on undecided user design questions.
- `Waiting for Prerequisite`: Blocked on upstream code/feature completion.
- `Backlog.md` owns task claiming and live status; [plan/Status.md](../plan/Status.md) and [plan/Milestones.md](../plan/Milestones.md) own roadmap dependencies. Never overwrite another assignee's `In Progress` status.

## 2. Task ID Naming Convention

- **Legacy ID Freeze**: Completed or active referenced IDs (`A-*`, `B-*`, `C-*`, `D-*`, `G-*`, in-progress `C-1a-confirm`) and reserved M3 IDs (`E-1~4` until M3 kickoff) stay as-is to preserve commit/doc links.
- **Allowed Characters**: New IDs use `A-Z`, `0-9`, `a-z`, and `-` only. Do not use `·`, `~`, `.`, spaces, or non-ASCII characters.
- **One Task = One ID**: Never bundle IDs (e.g., `C-1~2`, `Q-04·05`). Use separate rows or one primary ID. Never append ad-hoc suffixes like `-play`, `-confirm`, `-action`; create an independent ID instead.
- **New M3+ Feature IDs (`<DOMAIN>-<NN>`)**:
  - `COMBAT-NN`: Direct combat, skills/consumables, hit rules.
  - `SIEGE-NN`: Walls, placement, siege weapons, waves, enemy siege AI, morale/rout.
  - `FLOW-NN`: Scene transitions, run loop, outcome/reward flow.
  - `PREP-NN`: Courtyard facilities, shop, squad org, equipment, crafting, build economy.
  - `META-NN`: Persistent saves, unlocks, perks, reincarnation/run score (M3).
  - `CONTENT-NN`: Stage/hero/enemy/item rosters, bosses, balance (M4).
  - `ART-NN` / `UI-NN` / `AUDIO-NN`: Synty swaps, VFX/animations, production HUD/UX, audio (M5).
- **Cross-Cutting IDs (`<TYPE>-<slug>` / `Q-<NN>`)**:
  - `Q-NN` (design decision, zero-padded), `BUG-<slug>`, `PERF-<slug>`, `QA-<slug>` (manual playtest / regression / release checks), `DOC-<slug>`, `TOOL-<slug>`. Keep `slug` lowercase kebab-case (2–4 words).

## 3. Work Environments & Claude Exclusions

Active collaboration since 2026-11-11 across separate PCs/branches (Q-26). See [plan/Team.md](../plan/Team.md) for current ownership.

| Setup | Parallel Allowed | Sequential Required |
|---|---|---|
| Separate PC / Clone / Branch | Feature code, unit tests, isolated scenes/assets | Shared contract updates, integration merges |
| Shared Folder & Editor | Authoring code/docs in pre-allocated files | Asset creation, scene saves, compile, PlayMode, tests, builds, packages |

Avoid opening one project folder in multiple editor instances; use `--project-path` if multiple editors run on one machine.

**Claude Code read/edit exclusions & allow rules (`.claude/settings.json`)**: Claude Code does not support `.claudeignore`; shared `.claude/settings.json` uses `permissions.deny` and `permissions.allow`.
- Allow listed: PowerShell wrappers under `Tools/Agent/*.ps1` and read-only git inspection (`git status`, `git diff --name-only`, `git diff --stat`, `git log -n`). `git commit` and `git push` remain un-allowed for interactive confirmation.
- Read blocked: `Library/` except `Library/PackageCache/` (explicit path only), `Temp/`, `Logs/` old/worker logs (`*-prev.log`, `AssetImportWorker*`, `*.jsonl`), `UserSettings/`, `obj/`, `Build(s)/`, `*.csproj`, `*.sln(x)`; third-party `Assets/TextMesh Pro/`, `Assets/ThirdParty/`, `Assets/Synty/`, `Assets/Polygon*/`, `Assets/Plugins/`, `Assets/Samples/`; `*.meta`, `*.unity`, `*.shadergraph`, `*.anim`, `*SDF.asset`, `LightingData.asset`, `NavMesh*.asset`; textures under `Assets/`, models, audio, fonts, video, `*.dll`, `*.pdb`, `*.unitypackage`.
- Read allowed: `.cs`, `.asmdef`, `.prefab`, `.mat`, `.controller`, ScriptableObject `.asset`, `ProjectSettings/`, `Packages/`, `Docs/`, `Logs/Editor.log` (search `error CS` only; do not read whole file), images outside `Assets/` (save verification screenshots outside `Assets/`, `Temp/`, `Logs/`).
- Edit/Write blocked: `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.controller`, `*.anim` (Editor API only).
- Inspect scenes, GUIDs, and references via `script eval` (`EditorSceneManager`, `AssetDatabase.GUIDToAssetPath`). If a Synty pack imports under a new root folder, add it to `.claude/settings.json`.

### Third-Party Asset Submodule (`Assets/ThirdParty`)

Licensed third-party assets (Synty packs, paid Asset Store content) live in the **private** repository `RiceMonkeyGames/ProjectNeo-Assets`, mounted as a git submodule at `Assets/ThirdParty` (`.gitmodules` uses the relative URL `../ProjectNeo-Assets.git`, so it follows the main remote's SSH/HTTPS scheme). This keeps the main repository publishable without redistributing licensed files ([TOOL-asset-submodule](../history/TOOL/TOOL-asset-submodule.md)).
- **Import location**: import every third-party package under `Assets/ThirdParty/` (e.g. `Assets/ThirdParty/Synty/...`). Never import licensed assets anywhere else in the main repository: once committed there they stay in its history even after moving.
- **Clone**: `git clone --recurse-submodules <ProjectNeo URL>`; existing clones run `git submodule update --init --recursive`. Each member needs read access to the private repository. Optional: `git config submodule.recurse true` so checkout/pull update the submodule automatically.
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
| `Docs/` shared files (`Backlog.md`, `INDEX.md`, `plan/`) | Coordinate edits; keep changes scoped to your leaf files. |

**Integration Pipeline**:
1. Validate contracts and owned files.
2. Develop and verify in isolated scenes/workspaces.
3. Integration order: Contracts → Rules → Scene wiring → UI / Visuals.
4. Run regression EditMode/PlayMode suites upon shared code edits.
5. Verify standalone builds on release milestones.
6. Record outcomes in `Docs/history/<PREFIX>/<TASK-ID>.md` and update `Docs/Backlog.md`. Always provide manual verification walkthroughs (scene path → Play/tests → controls → expected results → reset steps).

```text
# Task Handoff Template
Task ID / Title:
Owner / Integration Lead:
Status: Waiting | Pending Decision | In Progress | Waiting for Link | Waiting for Verification | Done
Prerequisites / Required Decisions:
Owned Files / Locked Files:
Provided Data/Events / Consumed Data/Events:
Current Task Scope / Deferred Follow-up Scope:
Completion Criteria / Actual Verification Results:
Constraints for Next Owner:
```
