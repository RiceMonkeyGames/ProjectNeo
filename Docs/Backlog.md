# ProjectNeo Development Backlog

Last Updated: 2026-11-16 15:40:36 (KST)
Source: [Google Docs (Legacy)](https://docs.google.com/document/d/1zyEvUQFRwFjPP0i5M34nC7sStyZT3s60drXyg_ctz_Y/edit?tab=t.0#heading=h.c9sft4i3oi7)

> Purpose: Active task selection, assignment, and status tracking (`Done` tasks live in [Backlog-Done.md](archive/Backlog-Done.md)). | Parent: [INDEX](INDEX.md) | Related: [Collaboration](workflow/Collaboration.md), [Status](plan/Status.md)

## Workflow & ID Summary

- **Claim & Sync**: Pick `Waiting` → mark `In Progress` + assignee here → **immediately commit & push to `develop`** → create `feature/<task-id-lowercase>` from latest `develop` and record branch here. Never modify another assignee's `In Progress` item.
- **Completion**: Update affected leaves/history and present verification; after explicit user confirmation mark Done, archive, and commit/push the task branch. The user handles develop merge and remote branch deletion. Immediate claim commit/push remains authorized.
- **Task IDs**: Allowed chars `A-Z`, `0-9`, `a-z`, `-` only (1 task = 1 ID; no `~`, `·`, `.`, or ad-hoc suffixes). M3+ features use `<DOMAIN>-<NN>` (`COMBAT`, `SIEGE`, `FLOW`, `PREP`, `META`, `CONTENT`, `ART`, `UI`, `AUDIO`); cross-cutting use `Q-NN`, `BUG-<slug>`, `PERF-<slug>`, `QA-<slug>`, `DOC-<slug>`, `TOOL-<slug>`. Full rules in [Collaboration.md](workflow/Collaboration.md).
- **Statuses**: `Waiting` → `In Progress` → `Waiting for Link` / `Waiting for Verification` → `Done` (`Pending Decision`, `Waiting for Prerequisite` for blockers).

---

## Active Backlog Tasks

| Task ID | Description | Status | Assignee | Completion Criteria & Prerequisites |
|---|---|---|---|---|
| COMBAT-07 | Hooks for Morale Skills, Captains, and Perk Effects | In Progress | youngho | Branch `feature/combat-07`. Normalized from M1 roadmap `B-8.3` (prerequisites B-8.1, B-8.2, COMBAT-03 Done). Verify that a sample morale boost applies once to a real squad and unsubscribes cleanly (full skills in `COMBAT-06` / `META-02`). |
| C-5 | Wave Preview & Hidden Enemy Intel | Waiting for Prerequisite | Unassigned | B-7.1 Done 2026-11-16 (stage data readable without a running battle). Following run contracts, display upcoming enemy types, counts, and hidden indicators in prep UI. Scouting rules clarified first. |
| B-9.3 | M1 Full Integration, Iteration & Build Verification | Waiting for Prerequisite | Unassigned | Following B-4~8 integration, test repeated cycles of destruction, enemy variants, waves, morale, and outcomes; verify regressions and standalone build startup flow. |
| QA-standalone-build | Standalone Build Startup Flow Check | Waiting | Unassigned | Registered 2026-11-16 from [Status](plan/Status.md) remaining scope: most Done tasks record "standalone build unverified". Build the Windows player with the existing build script and check Title → Game → prep → battle → result once. Changing the first build scene requires notifying the teammate first ([Team](plan/Team.md)). Independent of B-7.1; full M1 regression stays in B-9.3. |
| QA-physical-input | Physical-Input Manual Play Sweep | Waiting | Unassigned | Registered 2026-11-16: user-driven Editor play pass over the manual checks still recorded as unverified in history — PREP-01 (smithy upgrades), PREP-04 (`Q` potion), SIEGE-07 (cannon crosshair/traverse), COMBAT-03 (morale HUD/Panic), SIEGE-04 (ladder keys/HUD), FLOW-01 (hold `B`). Record pass/fail per item; register `BUG-<slug>` rows for failures. No code changes expected. |
| META-02 | Runtime Perk Injection & Reward Routing | Pending Decision | Unassigned | Registered 2026-11-16: apply perk payloads to real run/battle stats and route victory unlock points. Blocked by Q-06 (perk allocation timing/pause) and Q-07 (unlock point distribution timing). |
| COMBAT-06 | Minimal Hero/Active Skill Execution | Pending Decision | Unassigned | Registered 2026-11-16: skills exist as identifiers only. Blocked by Q-12 (skill rules, cooldowns, targeting, perk effects). Shared combat core edits need teammate coordination. |
| SIEGE-08 | Construction Delays | Pending Decision | Unassigned | Registered 2026-11-16 (formerly roadmap bundle `C-6` construction pacing). No delay rule is recorded in `Docs/design/`; the user must decide whether builds take time, how long, and whether it applies in prep only. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
