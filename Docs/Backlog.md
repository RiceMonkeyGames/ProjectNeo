# ProjectNeo Development Backlog

Last Updated: 2026-11-18 13:18:25 (KST)
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
| PREP-07 | Scouting Action (Reveal Hidden Enemies) | Pending Decision | Unassigned | Registered 2026-11-18 as the C-5 follow-up (user deferred scouting from C-5). Deferred 2026-11-18: blocked until an in-game current-time (clock) concept is decided; only the prep-hour budget exists today. One scout spends `PrepTimeSettings.ScoutHours` (3h) via `RunState.TrySpendPrepTime` and calls `EnemyIntel.RevealAll` on `GameBattleController.Intel`; offered only while `EnemyIntel.AnyHidden`, refused with `Not enough time.` when short. Open before start: input/location of the action (user decision) and whether the revealed state must survive a run save/continue. Rules: [MoraleAndWave](design/MoraleAndWave.md#wave-preview). |
| META-02 | Runtime Perk Injection & Reward Routing | Pending Decision | Unassigned | Registered 2026-11-16: apply perk payloads to real run/battle stats and route victory unlock points. Blocked by Q-06 (perk allocation timing/pause) and Q-07 (unlock point distribution timing). |
| COMBAT-06 | Minimal Hero/Active Skill Execution | Pending Decision | Unassigned | Registered 2026-11-16: skills exist as identifiers only. Blocked by Q-12 (skill rules, cooldowns, targeting, perk effects). Shared combat core edits need teammate coordination. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
