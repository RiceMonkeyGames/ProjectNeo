# Pending Decisions & Active Operational Policies

Last Updated: 2026-11-16 21:40:22 (KST)

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
| Q-06 | Mid-combat perk allocation and pause behavior | M2 perk hook, M3 UI | Undecided. Candidate: restricted to prep/reward phases. |
| Q-07 | Unlock point distribution timing, gacha UI location, guaranteed policy | M2 reward contracts, M3 UI | Partial: Gacha appearance flags in data; distribution timing pending. |
| Q-08 | Run score formula, run duration, ending conditions, boss roles | M2 score/reincarnation, M4 | Partial (2026-11-11): Clearing all stages triggers ending. Interim (Q-51, 2026-11-14): 3 stages and no bosses for M2. Final stage count, bosses, scoring undecided. |
| Q-10 | Final troop headcount, baseline gear, performance budget | Art integration, M6 | Undecided. B-1 500-unit test is not a production guarantee. |
| Q-11 | Command keys, combat HUD, prep UI layout | B-5.3, M2 UI | Partial: F1–F10 in Siege prototype; default F1–F4, `, 1, 2, 3. Final HUD layout undecided. |
| Q-12 | Initial skill/consumable rules, cooldowns, targeting, perk effects | M2 minimal skill/item execution | Partial (2026-11-15): consumable use has interim rules from PREP-04 (`Q`, 35% heal, 10s cooldown). Skills exist as identifiers only; skill rules, targeting and perk effects undecided. |
| Q-13 | Mid-run saves, resume capability, save slots | M2 lifecycle, M3 persistence | Partial (2026-11-16, META-01): meta save implemented with three slots, save on every meta change and on quit, backup recovery. Run save and Title "Continue" implemented in FLOW-02 (2026-11-16) with choices delegated to the agent: one run per slot, automatic saves at preparation start / battle start / reward / quit during preparation, no mid-battle save, defeat deletes the run save. Still open: saving inside a battle, manual saves, and save migration when content ids change. |
| Q-14 | Release platforms, distribution channels, launch dates, scale | M4 scope, M7 delivery | Undecided. Focus on core completion criteria first. |
| Q-15 | Supported input devices, settings, languages, accessibility | M2 framework, M5 UI | Undecided. Avoid arbitrary additions. |
| Q-31 | Equipment retention upon hero permadeath | `C-3` casualty carry-over | Undecided. `C-1a` preserves fallen hero gear in `RunState.FallenHeroes`. |
| Q-32 | Can player character serve as squad captain | `D-8` squad setup UI, later player-captain task | Interim (2026-11-13): Not allowed for now; player captaincy planned later, so keep captain assignment extensible. `C-1a` `TrySetLeader` currently permits the player and D-8 UI must block it. |

Q-41 shared ladder interaction rules were approved on 2026-11-13 and moved to [Decisions-Resolved](Decisions-Resolved.md). The user subsequently confirmed shared-code edits and tap F boarding / hold F toppling. Runtime implementation is tracked as SIEGE-04.
