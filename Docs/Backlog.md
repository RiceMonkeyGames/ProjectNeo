# ProjectNeo Development Backlog

Last Updated: 2026-11-13 13:24:59 (KST)
Source: [Google Docs (Legacy)](https://docs.google.com/document/d/1zyEvUQFRwFjPP0i5M34nC7sStyZT3s60drXyg_ctz_Y/edit?tab=t.0#heading=h.c9sft4i3oi7)

> Purpose: Active task selection, assignment, and status tracking (`Done` tasks live in [Backlog-Done.md](archive/Backlog-Done.md)). | Parent: [INDEX](INDEX.md) | Related: [Collaboration](workflow/Collaboration.md), [Status](plan/Status.md)

## Workflow & ID Summary

- **Claim & Sync**: Pick `Waiting` → mark `In Progress` + assignee here → **immediately commit & push to `develop`** → create `feature/<task-id-lowercase>` from latest `develop` and record branch here. Never modify another assignee's `In Progress` item.
- **Completion**: Update affected `Docs/` leaves, log results in `Docs/history/<PREFIX>/<TASK-ID>.md`, move completed row to [archive/Backlog-Done.md](archive/Backlog-Done.md), and sync. Delete remote task branch after confirmed user merge to `develop`.
- **Task IDs**: Allowed chars `A-Z`, `0-9`, `a-z`, `-` only (1 task = 1 ID; no `~`, `·`, `.`, or ad-hoc suffixes). M3+ features use `<DOMAIN>-<NN>` (`COMBAT`, `SIEGE`, `FLOW`, `PREP`, `META`, `CONTENT`, `ART`, `UI`, `AUDIO`); cross-cutting use `Q-NN`, `BUG-<slug>`, `PERF-<slug>`, `QA-<slug>`, `DOC-<slug>`, `TOOL-<slug>`. Full rules in [Collaboration.md](workflow/Collaboration.md).
- **Statuses**: `Waiting` → `In Progress` → `Waiting for Link` / `Waiting for Verification` → `Done` (`Pending Decision`, `Waiting for Prerequisite` for blockers).

---

## Active Backlog Tasks

| Task ID | Description | Status | Assignee | Completion Criteria & Prerequisites |
|---|---|---|---|---|
| C-1a-confirm | Teammate Review of Combat Outcome Format | In Progress | Hwang Seong-min | Review C-1a `BattleResult` draft (outcome, end reason, per-unit status, castle snapshot, kill counts) with combat lead; update code, tests, docs. Prerequisites: C-1a (Done). Decision (2026-11-12): Proceed concurrently with C-1~2. |
| QA-game-loop | Manual Playtest of Current Game Scene | Waiting for Verification | Unassigned | Formerly `B-9.3-play`. Title → Prep → F Construction / Esc Return → F Battle → Win/Loss → Next Prep loop. Document camera, squad commanding, input glitches, and reproduction steps. |
| Q-21 | Finalize Siege Weapon Mechanics | Pending Decision | Unassigned | Confirm auto/unit/player control, range, damage, HP, rotation, passage blocking, and costs with user. Cannons currently mountable only. |
| B-6.1 | Scope & Contract for Additional Enemy Behaviors | Pending Decision | Unassigned | Agree on targeting, movement, and failure handling for wall-destroying and aerial enemies. Existing ladder and gate assault logic will not be re-implemented. |
| B-7.1 | Formal Wave Data & Spawning Rules | Waiting for Prerequisite | Unassigned | After enemy definitions and spawn/termination contracts are agreed upon, spawn according to configured types, counts, and timings. No duplicates on pause/restart. |
| SIEGE-01 | Link Pending Spawns to Win/Loss Evaluation | Waiting for Prerequisite | Unassigned | Formerly `B-9.2-wave` (fulfills `B-9.2` wave-spawn link). After B-7.1, prevent premature victory if enemies remain to spawn; ensure battle termination and outcomes trigger exactly once. |
| Q-38 | Add Last Stand and Steadfast Awaken effects | In Progress | youngho | Branch: `feature/q-38`. Amend Q-04 Awaken rules: Awaken picks 1 of 3 at random (morale raise for self and nearby, Last Stand, Steadfast); Steadfast squads do not rout at 0. Update [MoraleAndWave](design/MoraleAndWave.md), Decisions-Resolved, history. Doc-only. Prerequisite: Q-04 (Done). |
| B-8.1 | Squad Morale Calculations & Rout Transitions | Waiting | youngho | Prerequisite Q-04·05 resolved 2026-11-12 (rules in [MoraleAndWave.md](design/MoraleAndWave.md)); reserved by youngho, not started. Verify casualties, boosts, stat debuffs, thresholds, and rout transitions in pure C# EditMode. |
| B-8.2 | Rout Movement, Exit, and Outcome Wiring | Waiting for Prerequisite | Unassigned | Following B-8.1 and outcome contracts, differentiate death vs routing vs battlefield exit to avoid double-counting. Distinguish from final enemy retreat. |
| SIEGE-02 | Siege Weapon Actions & Combat Integration | Waiting for Prerequisite | Unassigned | Formerly `B-4.6-action` (fulfills `B-4.6` weapon combat scope). Following Q-21 resolution, wire agreed behaviors and verify hits, damage, and concurrent destruction upon supporting wall collapse. |
| C-3 | Run Carry-over of Troop Casualties & Wall HP | Waiting for Prerequisite | Unassigned | Following C-1a and rout outcome contracts, carry over deaths, rout returns, and wall damage into subsequent battles without duplicate accounting. |
| C-5 | Wave Preview & Hidden Enemy Intel | Waiting for Prerequisite | Unassigned | Following B-7.1 and run contracts, display upcoming enemy types, counts, and hidden indicators in prep UI. Scouting rules clarified first. |
| B-9.3 | M1 Full Integration, Iteration & Build Verification | Waiting for Prerequisite | Unassigned | Following B-4~8 integration, test repeated cycles of destruction, enemy variants, waves, morale, and outcomes; verify regressions and standalone build startup flow. |
| D-8 | Squad Organization | Waiting for Prerequisite | Unassigned | Prerequisites: D-0, D-0b. Decisions: Q-32, Q-36. Completion: Squad reorganization UI (reassign grunts, assign captains, display headcount limits) reflects directly into battle commands and combat inputs. |
| D-3 | Tavern Hero Hiring | Waiting for Prerequisite | Unassigned | Prerequisites: D-0, D-0b, D-8. Decisions: Q-34, Q-36 resolved (2026-11-13). Completion: Hire heroes from unlocked pools per tier; pick squad to captain at hire (swaps existing captain to reserve); hero scene prefab, tavern spawning and captain squad orders (moved from D-0b, 2026-11-13); permadeath applies. |
| D-6 | Blacksmith Crafting, Upgrades & Sales | Pending Decision | Unassigned | Prerequisites: D-0, D-1, D-7. Decision: Q-35 crafting model and upgrade definitions. Completion: Obtain equipment via materials/gold; upgrades boost item stats; sellable. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
