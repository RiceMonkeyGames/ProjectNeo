# Pending Decisions & Active Operational Policies

Last Updated: 2026-11-13 15:54:34 (KST)

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
| Q-08 | Run score formula, run duration, ending conditions, boss roles | M2 score/reincarnation, M4 | Partial (2026-11-11): Clearing all stages triggers ending. Stage count, bosses, scoring undecided. |
| Q-10 | Final troop headcount, baseline gear, performance budget | Art integration, M6 | Undecided. B-1 500-unit test is not a production guarantee. |
| Q-11 | Command keys, combat HUD, prep UI layout | B-5.3, M2 UI | Partial: F1–F10 in Siege prototype; default F1–F4, `, 1, 2, 3. Final HUD layout undecided. |
| Q-12 | Initial skill/consumable rules, cooldowns, targeting, perk effects | M2 minimal skill/item execution | Undecided. Skills exist as identifiers only. |
| Q-13 | Mid-run saves, resume capability, save slots | M2 lifecycle, M3 persistence | Meta save mandatory; mid-run save unconfirmed. |
| Q-14 | Release platforms, distribution channels, launch dates, scale | M4 scope, M7 delivery | Undecided. Focus on core completion criteria first. |
| Q-15 | Supported input devices, settings, languages, accessibility | M2 framework, M5 UI | Undecided. Avoid arbitrary additions. |
| Q-31 | Equipment retention upon hero permadeath | `C-3` casualty carry-over | Undecided. `C-1a` preserves fallen hero gear in `RunState.FallenHeroes`. |
| Q-32 | Can player character serve as squad captain | `D-8` squad setup UI | Undecided. `C-1a` `TrySetLeader` permits assigning player character. |
| Q-35 | Blacksmith crafting model (storefront vs recipes) and upgrades | `D-6` blacksmith | Undecided. Requires crafting recipes and gear enhancement definitions. |
| Q-41 | Shared ladder traversal interaction rules | Shared siege/combat movement | Partial (2026-11-13): User approved allies AND player ascending/descending. Controls, opposing traffic/queues, commands, combat and toppling rules still pending. Coordinate combat owner before implementation. |
