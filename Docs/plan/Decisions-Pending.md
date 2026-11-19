# Pending Decisions & Active Operational Policies

Last Updated: 2026-11-19 20:47:14 (KST)

> Purpose: Undecided or partial design/implementation decisions (`Q-*`) and active operational rules. | Parent: [INDEX](../INDEX.md) | Related: [Decisions-Resolved](Decisions-Resolved.md), [OpenQuestions](../design/OpenQuestions.md)

## Active Operational Decisions

- **AI Git handoff (2026-11-13 user revision)**: Follow the teammate finish workflow: present concrete changes and verification, obtain explicit user confirmation before Done/archive/task-branch commit/push, and leave develop merge and remote task-branch deletion to the user. Immediate backlog claim commit/push remains authorized. This supersedes the earlier same-day blanket AI commit/merge/push permission. Codex entrypoints under `.agents/skills/` reuse the teammate `.claude/skills/` instructions and Tools/Agent wrappers.

- **Backlog & Branch Lifecycle (2026-11-12)**: Claim `Waiting` → `In Progress` in `Docs/Backlog.md` and immediately commit/push before coding; branch `feature/<task-id-lowercase>` from latest `develop`; delete remote task branch after confirmed merge to `develop`.
- **Docs Tree & Task ID Rules (2026-11-13)**: Docs sharded into `Docs/INDEX.md` tree; M3+ feature IDs use domain prefixes (`COMBAT`, `SIEGE`, `FLOW`, `PREP`, `META`, `CONTENT`, `ART`, `UI`, `AUDIO`); unstarted irregular IDs normalized (`B-9.3-play` → `QA-game-loop`, `B-9.2-wave` → `SIEGE-01`, `B-4.6-action` → `SIEGE-02`); `In Progress` `C-1a-confirm` and reserved `E-1~4` kept until M3.
- **PERF-nav-query coordination (2026-11-12)**: Honor `DecisionInterval` for empty-target searches; retain immediate invalidation of existing targets and exact range/LoS rules.

## Pending / Partial `Q-*` Decisions

| ID | Decision Topic | Needed By / Blocking | Current Status |
|---|---|---|---|
| Q-01 | Synty pack and finalized piece specifications | Production art integration | Undecided. Generic modular architecture approved; dimensions in assets. |
| Q-10 | Final troop headcount, baseline gear, performance budget | Art integration, M6 | Undecided. B-1 500-unit test is not a production guarantee. |
| Q-11 | Command keys, combat HUD, prep UI layout | B-5.3, M2 UI | Partial: F1–F10 in Siege prototype; default F1–F4, `, 1, 2, 3. Final HUD layout undecided. |
| Q-13 | Mid-run saves, resume capability, save slots | M2 lifecycle, M3 persistence | Partial (2026-11-16, META-01): meta save implemented with three slots, save on every meta change and on quit, backup recovery. Run save and Title "Continue" implemented in FLOW-02 (2026-11-16) with choices delegated to the agent: one run per slot, automatic saves at preparation start / battle start / reward / quit during preparation, no mid-battle save, defeat deletes the run save. Still open: saving inside a battle, manual saves, and save migration when content ids change. |
| Q-14 | Release platforms, distribution channels, launch dates, scale | M4 scope, M7 delivery | Undecided. Focus on core completion criteria first. |
| Q-15 | Supported input devices, settings, languages, accessibility | M2 framework, M5 UI | Undecided. Avoid arbitrary additions. |
| Q-58 | Morale tuning values and prep-phase morale recovery facility | Morale balance | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-59 | Max building tiers, stair constraints, partial foundation collapse | Construction rules | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-60 | AI improvement pass scope and priorities (enemy side owned by teammate) | AI quality tasks | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-61 | M4 content scale (hero/enemy/item/stage counts, target play time) | M4 production tasks | Undecided. Registered 2026-11-18 from Milestones. |
| Q-72 | Perk Direction & Skill Perks | Q-66, META-02, COMBAT-06 skill acquisition | Undecided. Registered 2026-11-19. Q-66 follow-up. Set the overall perk direction (tree shape, categories, how many points per level) and the perks that grant new player skills. Decided: perks belong to the player (commander) only, heroes have none, and new player skills come from perk points earned on Commander level-up. Also decide whether skills learned through perks reset with perk allocations when a run ends. |
| Q-73 | Hero Grade Progression & Hero Skills | COMBAT-06 hero slots | Undecided. Registered 2026-11-19. Q-62 follow-up. How a hero's grade rises during a run (EXP, facility, promotion cost) and where a hero's skills come from (fixed per hero data, random on hire, chosen on grade up). |
| Q-74 | Boss Design | SIEGE-09 | Undecided. Registered 2026-11-19. Q-68 follow-up. Boss roles (siege engine, champion, commander buffing others), stat scaling across 7 stages, special rules, rewards, and whether bosses ship in M2 or later. |
| Q-75 | Post-Ending Loop Rules | FLOW-03 | Undecided. Registered 2026-11-19. Q-69 follow-up. Ending screen content, how difficulty rises per loop, rewards and unlock points in loops, run save during loops, and how the run finally ends (defeat only, or a quit option). |
| Q-66 | Skills, perks & progression (from Q-12) | COMBAT-06 skill acquisition | Partial (2026-11-19, team): perks are player (commander) only; heroes have no perks; new player skills come from perk points earned on Commander level-up. The rest waits on Q-72. |

Q-41 shared ladder interaction rules were approved on 2026-11-13 and moved to [Decisions-Resolved](Decisions-Resolved.md). The user subsequently confirmed shared-code edits and tap F boarding / hold F toppling. Runtime implementation is tracked as SIEGE-04.
