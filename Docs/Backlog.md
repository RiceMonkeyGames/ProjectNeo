# ProjectNeo Development Backlog

Last Updated: 2026-11-14 22:59:29 (KST)
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
| BUG-stage2-enemies-idle | Stage 2 Enemies Do Not Advance | Waiting | Unassigned | Reported 2026-11-14 by user after the QA-game-loop recheck: in `Game.unity`, after winning Stage 1 and `Claim Reward & Next Stage`, the Stage 2 battle starts but enemies do not approach the castle. Expected: enemies advance and assault as in Stage 1. Roster, start position and castle state at repro were not captured. First reproduce (Stage 1 win → Stage 2 hold B) and identify the cause before fixing; add a regression test for a second-stage assault. |
| COMBAT-05 | Player Jump | Waiting | Unassigned | User feedback 2026-11-14: add jump to player controls. Confirm with the user at claim: key (Space assumed, not decided), jump height / air control, interaction with crouch, sprint, ladders and cannon manning, and whether jumping onto/over 3m wall pieces must stay impossible. Values in `PlayerControlSettings`. |
| SIEGE-07 | Cannon Usability: Fire-Ready Feedback & Aim Rotation | Pending Decision | Unassigned | User feedback 2026-11-14 on SIEGE-02 cannons: the player cannot tell whether the cannon can fire or is on cooldown, and wants to turn the cannon while manning it. Decisions needed before implementation: (1) fire-ready/reload indicator form (crosshair state, HUD bar/text, on-cannon visual, sound) and why the existing manned-HUD reload text goes unnoticed, (2) rotation method: cannon follows the aim vs separate rotate keys, (3) rotation limits/speed versus the current ±45° / 6–60m fire arc, and whether auto-fire cannons are affected. |
| PREP-05 | Pre-Battle Squad Placement | Pending Decision | Unassigned | User feedback 2026-11-14: squads cannot be positioned before the battle starts. Decisions needed before implementation: (1) method: enable F1–F3 / 1·2·3 orders during prep exploration vs placement in the construction view vs both, (2) whether units walk or are placed instantly (SiegePrototype uses instant placement), (3) allowed areas (wall tops, courtyard) and whether positions persist to the next stage. Related: Q-40 / FLOW-01 current-position battle start. |
| B-7.1 | Formal Wave Data & Spawning Rules | Waiting for Prerequisite | Unassigned | Structure decided by Q-51 (2026-11-14): 3 fixed stages, 2–3 timed waves, fixed northern spawn points, stage asset with wave list referencing existing enemy prefabs. Remaining prerequisite: teammate review of the data format. Spawn according to configured types, counts, and timings; no duplicates on pause/restart. |
| SIEGE-01 | Link Pending Spawns to Win/Loss Evaluation | Waiting for Prerequisite | Unassigned | Formerly `B-9.2-wave` (fulfills `B-9.2` wave-spawn link). After B-7.1, prevent premature victory if enemies remain to spawn; ensure battle termination and outcomes trigger exactly once. |
| C-5 | Wave Preview & Hidden Enemy Intel | Waiting for Prerequisite | Unassigned | Following B-7.1 and run contracts, display upcoming enemy types, counts, and hidden indicators in prep UI. Scouting rules clarified first. |
| B-9.3 | M1 Full Integration, Iteration & Build Verification | Waiting for Prerequisite | Unassigned | Following B-4~8 integration, test repeated cycles of destruction, enemy variants, waves, morale, and outcomes; verify regressions and standalone build startup flow. |
| PREP-04 | Combat Consumable Use | Pending Decision | Unassigned | User deferred (2026-11-13). Decide with Q-12: input keys (combat key ownership with teammate), quick slots, effects (e.g., potion heal %), limits. Existing data: `item_potion`. |
| BUG-wall-archer-los | Wall-Top Archer LoS, Range & Hold Retaliation | Pending Decision | Unassigned | Reported 2026-11-14 during manual play: wall-top `Hold` archers fail to retaliate against enemy archers below due to `HasClearShot` wall-edge occlusion or 3D range boundary. Decisions needed before implementation: (1) high-ground ranged bonus enabled/value, (2) `HasClearShot` wall-top/edge exception vs stepping to wall edge for clear LoS, (3) `Hold` behavior when LoS and path are both blocked. |
| COMBAT-04 | Cast & Interaction Interrupt System on Hit | Pending Decision | Unassigned | Proposal 2026-11-14: taking enemy damage should interrupt active actions such as pushing ladders. Decisions needed before implementation: (1) target actions (ladder push/raise, cannon operation, battle-start hold, etc.), (2) trigger condition (any damage vs minimum damage/stagger threshold, cooldown), (3) progress handling on interrupt (reset vs settle from current tilt) and UI/feedback. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
