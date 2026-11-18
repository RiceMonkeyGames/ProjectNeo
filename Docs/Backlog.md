# ProjectNeo Development Backlog

Last Updated: 2026-11-18 16:04:38 (KST)
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
| Q-54 | Team Decision: M2 Scope Cut | Waiting | Unassigned | Registered 2026-11-18 (team meeting item, top priority). Decide which remaining M2 features (skills COMBAT-06, perk injection META-02, unlock point routing, scouting PREP-07) stay in M2 and which move to M3, so M2 can close. Record in [Milestones](plan/Milestones.md). |
| Q-56 | Team Decision: Ownership After B-6~B-9 | Waiting | Unassigned | Registered 2026-11-18 (team meeting item, second priority). The teammate's B-6~B-9 scope is Done; agree who owns the M2 remainder and M3+ domains and update [Team](plan/Team.md). |
| Q-55 | Team Decision: In-Game Current-Time Concept | Waiting | Unassigned | Registered 2026-11-18. Decide whether the game has a time of day / calendar beyond the PREP-06 12h preparation budget and how it behaves. Blocks PREP-07. |
| Q-06 | Team Decision: Perk Allocation Timing & Pause | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing pending decision ([Decisions-Pending](plan/Decisions-Pending.md)). Candidate: prep/reward phases only. Blocks META-02. |
| Q-07 | Team Decision: Unlock Point Timing & Gacha UI | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing pending decision. Decide distribution timing, gacha UI location and guaranteed policy. Blocks META-02. |
| Q-12 | Team Decision: Skill Rules | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing pending decision. Decide skill rules, cooldowns, targeting and perk effects. Blocks COMBAT-06. |
| Q-08 | Team Decision: Run Length, Bosses & Score | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing partial decision (interim: 3 stages, no bosses). Decide final stage count, boss roles and run score formula. |
| Q-57 | Team Decision: Squad Counts & Member Caps | Waiting | Unassigned | Registered 2026-11-18 from [OpenQuestions](design/OpenQuestions.md). Decide final squad count and member cap values (temporary G-1 default: 3 × 4). |
| Q-58 | Team Decision: Morale Tuning & Recovery Facility | Waiting | Unassigned | Registered 2026-11-18 from OpenQuestions. Decide morale values beyond the starting values in [MoraleAndWave](design/MoraleAndWave.md) and whether a prep-phase morale recovery facility exists. |
| Q-59 | Team Decision: Building Tier Limits & Edge Cases | Waiting | Unassigned | Registered 2026-11-18 from OpenQuestions. Decide max vertical tiers, stair constraints and partial foundation collapse handling. |
| Q-11 | Team Decision: UI Improvement Pass Scope | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing partial decision. Decide final combat HUD, command keys and prep UI layout plus the UI pass scope/priorities, then break it into `UI-NN` tasks. |
| Q-60 | Team Decision: AI Improvement Pass Scope | Waiting | Unassigned | Registered 2026-11-18 from OpenQuestions. Decide scope and priorities for enemy (teammate area) and ally AI quality, then break it into tasks. Known item: a living enemy in the line blocks ranged shots at the enemy behind it. |
| Q-13 | Team Decision: Remaining Save Scope for M3 | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing partial decision. Decide in-battle saves, manual saves and save migration when content ids change, then break M3 save/unlock/perk UI work into tasks. |
| Q-61 | Team Decision: M4 Content Scale | Waiting | Unassigned | Registered 2026-11-18 from [Milestones](plan/Milestones.md). Agree hero/enemy/item/stage counts and target play time before creating M4 production tasks. |
| Q-01 | Team Decision: Synty Packs & Piece Specs | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing pending decision. Decide which Synty packs to use and finalized piece specifications for art integration. |
| PREP-08 | Return Fallen Hero Gear to Stash | Waiting | Unassigned | Registered 2026-11-18 from Q-31. When `RunState.Apply` moves a dead hero to `FallenHeroes`, move that hero's equipped items (with upgrade levels) into the run stash so the player or another hero can equip them. Check the run save/continue keeps the result, and decide whether the battle result screen should mention the returned items. Rule: [Combat](design/Combat.md#units). |
| QA-teammate-code-review | Teammate Review of Pending Shared Changes | Waiting | Unassigned | Registered 2026-11-18. Teammate reviews the shared combat core call sites changed by BUG-wall-archer-los and the B-7.1 stage data format; record the outcome in their history files. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
