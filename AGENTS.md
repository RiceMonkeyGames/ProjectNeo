# ProjectNeo

A 3D first-person siege defense roguelike. Scout the upcoming wave, prepare heroes, troops, equipment, and walls, and defend the castle through direct player combat and unit commanding. Built with Unity 6000.6.8f1 and URP. Low-poly art style; Synty assets planned, currently using placeholder graphics.

This file is the primary entry point for all agents working in this repository. `CLAUDE.md` simply references this file. The repository maintains four core documentation files—Backlog, Context, Implementation Plan, and Game Design—plus one past verification log (History). History is not read routinely.

## Documents to Read First

Repository Markdown files serve as the ground truth for game rules, implementation details, verification evidence, task selection, assignment, and progress tracking.

1. [Docs/Backlog.md](Docs/Backlog.md): Development backlog. The Single Source of Truth for task selection, status tracking, and assignment. Check this first to pick tasks.
2. [Docs/Context.md](Docs/Context.md): Execution guide, code/scene/asset map. Read development principles and Unity CLI usage as relevant to your task. Past verification logs are in [Docs/History.md](Docs/History.md); search by date or task ID only when past rationales or verification evidence are needed.
3. [Docs/ImplementationPlan.md](Docs/ImplementationPlan.md): Implementation roadmap. Review Section 1 (GOAL), Section 2 (Pending Decisions), Section 3 (Current Status), Section 5 (Immediate Tasks), and Section 6 (Future Division of Labor).
4. [Docs/GameDesign.md](Docs/GameDesign.md): Game rules, design intent, and pending designs. Read the sections relevant to your implementation.

If documents conflict, GameDesign takes precedence for game rules, ImplementationPlan for roadmap/dependencies/implementation status, Context for code explanations and development workflows, and Backlog for task claiming and progress. Do not duplicate separate rules or plans into CLAUDE. Release platform, timing, and content scale remain undecided; long-term plans will be refined based on playtest results.

## Backlog & Workflow

- Backlog Location: [Docs/Backlog.md](Docs/Backlog.md).
- **Task Claiming & Immediate Merge**:
  1. Inspect `Docs/Backlog.md` and select an unclaimed (`Waiting`) task.
  2. Mark the task as `In Progress` with your assignee name in `Docs/Backlog.md`.
  3. **Immediately commit and merge (or push/sync) this backlog change to the shared branch so that other teammates and agents are immediately notified of the assignment to avoid collisions.**
  4. Begin implementation only after the in-progress marking is recorded and synced.
- **Strict Collision Avoidance**: Never modify or take over items marked `In Progress` by another assignee. Even if tasks differ, coordinate order if modifying the same file or shared asset. Refer to Section 6 of ImplementationPlan for actual assignment, file ownership, and shared contracts.
- **Task Completion**: After finishing work, update relevant Markdown docs, record results, verification details, or doc paths in `Docs/Backlog.md`, and change status to `Done`. Sync/merge the updated backlog. Do not conceal incomplete scopes or unverified items.
- **Registering New Items**: If you discover new tasks, bugs, or follow-ups during work, check for duplication with existing entries and add them to `Docs/Backlog.md`. If design or shared architecture is undecided, note that user confirmation is required rather than treating it as a finalized task.
- **When to Add / Update Backlog**: New feature requests / design decisions, discovering bugs/missing items/dependencies during implementation, feedback from tests/playtests/teammates, checking remaining linkages/unverified scope upon completion/integration, breaking down subsequent roadmap milestones. Record as soon as discovered; re-check before reporting completion. Do not create duplicates for existing tasks; update existing items.
- **Format for New Items**: In new items, document Task ID, description, current status, completion criteria, and prerequisites. For bugs, include reproduction steps, expected results, and actual results. Distinguish `Pending Decision` (undecided design) and `Waiting for Prerequisite` (missing upstream feature). Registration alone does not imply approval, start, or assignment. Keep distant roadmap items as feature bundles, breaking them down into actionable units as they draw near.
- **Backlog vs Implementation Plan**: `Docs/Backlog.md` manages task selection, assignment, and progress, while `ImplementationPlan.md` manages roadmaps, decisions, dependencies, and implementation status. If they diverge, reconcile by verifying the active assignee and actual changes/verification evidence. Never overwrite another assignee's in-progress status with an outdated local document.

## Working Rules

- Respond to the user in Korean, using clear and simple language.
- Document last updated timestamp must use the format: `Last Updated: YYYY-MM-DD HH:MM:SS (KST)` (or `마지막 갱신: YYYY-MM-DD HH:MM:SS (KST)`). Whenever modifying a document, update it to the exact current time right before saving (e.g., using `Get-Date` or `date "+%Y-%m-%d %H:%M:%S"`); never guess the timestamp.
- If ambiguous requirements, divergent interpretations, or questions arise regarding design, behavior, or shared architecture, do not decide arbitrarily. Present options with rationale and ask the user first. Do not proceed with implementation dependent on that decision until answered. Independent work with no impact may continue. Reflect undecided items and received decisions in ImplementationPlan Section 2 and corresponding design docs.
- When the user makes a new decision, do not leave it only in chat. Update GameDesign for design, ImplementationPlan for implementation decisions/status/dependencies immediately. Update Context's code map as codebase grows.
- Log detailed completion and verification results at the end of Docs/History.md along with the date. Do not accumulate past logs in current planning documents.
- Always guide the user on how to verify manually upon completion: exact scene/file paths, control steps, expected results, and reset procedures. Open relevant scenes when possible. For rule/logic tasks, guide test names and Test Runner or CLI commands. Never stop at merely reporting passing automated tests.
- General commits and code merges are made directly by the user, except for immediate backlog in-progress/done claim synchronization as specified in the Backlog & Workflow rules.
- Collaborating since 2026-11-11: Teammate handles enemy assault and single battle phase execution; User handles game entry scene and core loop scaffolding. Actual assignments, owned files, shared contracts, and integration responsibilities follow Section 6 of ImplementationPlan. Never overwrite others' work or unverified connections.
- Apply modifications sequentially when touching identical files, shared prefabs, integration scenes, or project settings. Never run compilation, PlayMode, tests, builds, or asset modifications simultaneously in the same Unity editor.
- Do not mix game code changes or unnecessary Unity operations into doc-only tasks. Differentiate and record completion scope: rules, integration, manual play, required builds.

## Implementation Principles

- Separate rules and calculations from visual presentation using plain C# and verify via EditMode. Verify scene behavior via PlayMode and manual play.
- Store numerical parameters in ScriptableObjects. Provide `Create(...)` factory methods for testing in definitions.
- Maintain one class per file for ScriptableObjects and MonoBehaviours; match file name to class name.
- Explicitly assign numeric values to persistent enums; add new values to the end. Route random generation through `ProjectNeo.Core.IRandomSource`.
- Separate gameplay rules, collision, and movement on the root from visual representation under a `Visual` child. Follow principles in Context Section 5.
- Do not edit `.unity`, `.prefab`, `.asset`, or `.meta` files directly. Create/move scenes, assets, and folders via Editor APIs. `.cs` and `.asmdef` files may be authored directly.
- Always check Unity connection status first with `unity status`. Do not assume the editor is closed solely from connection failures; verify sandbox constraints as well. If not open, ask the user to open it.
- Follow compilation, testing, build, and package commands and caveats in Context Section 6. Do not use `unity recompile` or `unity command script recompile`, which failed in this project.
