# ProjectNeo Development Backlog

Last Updated: 2026-11-19 20:21:53 (KST)
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
| PREP-07 | Scouting Action (Reveal Hidden Enemies) | Waiting | Unassigned | Registered 2026-11-18 as the C-5 follow-up (user deferred scouting from C-5). Unblocked 2026-11-19: Q-55 decided there is no time of day or calendar, only the prep-hour budget; stays in M2 (Q-54). One scout spends `PrepTimeSettings.ScoutHours` (3h) via `RunState.TrySpendPrepTime` and calls `EnemyIntel.RevealAll` on `GameBattleController.Intel`; offered only while `EnemyIntel.AnyHidden`, refused with `Not enough time.` when short. Open before start: input/location of the action (user decision) and whether the revealed state must survive a run save/continue. Rules: [MoraleAndWave](design/MoraleAndWave.md#wave-preview). |
| META-02 | Runtime Perk Injection & Reward Routing | Pending Decision | Unassigned | Registered 2026-11-16: apply perk payloads to real run/battle stats and route victory unlock points. Stays in M2 (Q-54, 2026-11-19). Q-06 decided 2026-11-19: perks are allocated only in preparation and on the reward screen, never in battle. Still blocked by Q-07 (unlock point distribution timing). |
| COMBAT-06 | Minimal Hero/Active Skill Execution | Pending Decision | Unassigned | Registered 2026-11-16: skills exist as identifiers only. Stays in M2 (Q-54, 2026-11-19). Blocked by Q-62~Q-66 (Q-12 split on 2026-11-19 into owners/slots, activation/targeting, cost/cooldown, first skill set, perk/progression links). Shared combat core edits need teammate coordination. |
| Q-07 | Team Decision: Unlock Point Timing & Gacha UI | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing pending decision. Decide distribution timing, gacha UI location and guaranteed policy. Blocks META-02. |
| Q-58 | Team Decision: Morale Tuning & Recovery Facility | Waiting | Unassigned | Registered 2026-11-18 from OpenQuestions. Decide morale values beyond the starting values in [MoraleAndWave](design/MoraleAndWave.md) and whether a prep-phase morale recovery facility exists. |
| Q-59 | Team Decision: Building Tier Limits & Edge Cases | Waiting | Unassigned | Registered 2026-11-18 from OpenQuestions. Decide max vertical tiers, stair constraints and partial foundation collapse handling. |
| Q-11 | Team Decision: UI Improvement Pass Scope | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing partial decision. Decide final combat HUD, command keys and prep UI layout plus the UI pass scope/priorities, then break it into `UI-NN` tasks. |
| Q-60 | Team Decision: AI Improvement Pass Scope | Waiting | Unassigned | Registered 2026-11-18 from OpenQuestions. Decide scope and priorities for enemy (teammate area) and ally AI quality, then break it into tasks. Known item: a living enemy in the line blocks ranged shots at the enemy behind it. |
| Q-13 | Team Decision: Remaining Save Scope for M3 | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing partial decision. Decide in-battle saves, manual saves and save migration when content ids change, then break M3 save/unlock/perk UI work into tasks. |
| Q-61 | Team Decision: M4 Content Scale | Waiting | Unassigned | Registered 2026-11-18 from [Milestones](plan/Milestones.md). Agree hero/enemy/item/stage counts and target play time before creating M4 production tasks. |
| Q-01 | Team Decision: Synty Packs & Piece Specs | Waiting | Unassigned | Registered 2026-11-18 as a meeting item for the existing pending decision. Decide which Synty packs to use and finalized piece specifications for art integration. |
| Q-62 | Team Decision: Skill Owners & Slots | Waiting | Unassigned | Registered 2026-11-19 from the Q-12 split (skills; blocks COMBAT-06). Which characters have active skills (player, heroes; squads without a captain?), how many skill slots each has, and how a character gets a skill (fixed in hero data, chosen on level-up, granted by equipment, perks or unlocks). |
| Q-63 | Team Decision: Skill Activation & Targeting | Waiting | Unassigned | Registered 2026-11-19 from the Q-12 split (skills; blocks COMBAT-06). Player input per skill slot (final keys with Q-11); whether hero skills are cast by their AI, ordered by the player through squad commands, or both; targeting modes (self, aimed point/area, target unit, whole squad) and ranges. |
| Q-64 | Team Decision: Skill Cost & Cooldown | Waiting | Unassigned | Registered 2026-11-19 from the Q-12 split (skills; blocks COMBAT-06). Cooldown only, or also a resource (stamina/mana) or per-battle charges; starting cooldown values; whether cooldowns reset at battle start; relation to the 10s consumable cooldown (PREP-04). |
| Q-65 | Team Decision: First Skill Set for M2 | Waiting | Unassigned | Registered 2026-11-19 from the Q-12 split (skills; blocks COMBAT-06). Effect families to support first (area damage, buff/debuff, heal, morale gain such as Rally under the COMBAT-07 gain rules, stagger through COMBAT-04 poise) and the concrete starting list (how many for the player and per hero). |
| Q-66 | Team Decision: Skills, Perks & Progression | Waiting | Unassigned | Registered 2026-11-19 from the Q-12 split (skills; blocks COMBAT-06). Perks that change skills (cooldown, power, extra effect); how new skills are unlocked (Commander level, unlock gacha Q-07, hero level); whether skill choices carry across runs. |
| Q-67 | Team Decision: Run Length & Pacing | Waiting | Unassigned | Registered 2026-11-19 from the Q-08 split (run structure and score). Final stage count per run (interim 3), fixed order vs chosen route, target run play time (shared with Q-61), and how difficulty grows per stage. |
| Q-68 | Team Decision: Boss Stages | Waiting | Unassigned | Registered 2026-11-19 from the Q-08 split (run structure and score). Whether bosses exist and on which stages (last only, every N stages), boss roles (siege engine, champion, commander buffing other enemies), special rules (phases, effects on ally morale) and boss rewards. |
| Q-69 | Team Decision: Run Ending & After the Ending | Waiting | Unassigned | Registered 2026-11-19 from the Q-08 split (run structure and score). What the victory ending shows, whether a run can continue after the last stage (endless or harder loop), and what happens to the run save. Already decided: clearing all stages ends the run; defeat rules are unchanged. |
| Q-70 | Team Decision: Run Score Formula | Waiting | Unassigned | Registered 2026-11-19 from the Q-08 split (run structure and score). Metrics and weights (stages cleared, kills, bosses, wall/gate HP left, surviving heroes/grunts, gold left, unused prep time, casualties), victory vs defeat multiplier, and where the score is shown. |
| Q-71 | Team Decision: Reincarnation Bonus | Waiting | Unassigned | Registered 2026-11-19 from the Q-08 split (run structure and score). Share of the run score converted to Commander EXP (and possibly unlock points), different rates for victory and defeat, and caps against power creep. |
| PREP-09 | Apply Final Squad Caps (10 Squads x 20) | Waiting | Unassigned | Registered 2026-11-19 from Q-57: raise the squad data from the temporary G-1 3 x 4 to at most 10 squads with 20 soldiers each (barracks auto-create, organization window, placement view, command HUD). Open before start: whether the hero captain counts toward the 20, select keys for squads 5-10 (F1-F10 exists in the Siege prototype; final layout Q-11), and a performance check with 200 allies plus enemies (Q-10). Rule: [Combat](design/Combat.md). |
| QA-teammate-code-review | Teammate Review of Pending Shared Changes | Waiting | Unassigned | Registered 2026-11-18. Teammate reviews the shared combat core call sites changed by BUG-wall-archer-los and the B-7.1 stage data format; record the outcome in their history files. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
