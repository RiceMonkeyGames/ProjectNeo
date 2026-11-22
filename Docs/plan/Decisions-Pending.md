# Pending Decisions & Active Operational Policies

Last Updated: 2026-11-20 21:07:07 (KST)

> Purpose: Undecided or partial design/implementation decisions (`Q-*`) and active operational rules. | Parent: [INDEX](../INDEX.md) | Related: [Decisions-Resolved](Decisions-Resolved.md), [OpenQuestions](../design/OpenQuestions.md)

## Active Operational Decisions

- **AI Git handoff (2026-11-13 user revision)**: Follow the teammate finish workflow: present concrete changes and verification, obtain explicit user confirmation before Done/archive/task-branch commit/push, and leave develop merge and remote task-branch deletion to the user. Immediate backlog claim commit/push remains authorized. This supersedes the earlier same-day blanket AI commit/merge/push permission. Codex entrypoints under `.agents/skills/` reuse the teammate `.claude/skills/` instructions and Tools/Agent wrappers.

- **Backlog & Branch Lifecycle (2026-11-12)**: Claim `Waiting` → `In Progress` in `Docs/Backlog.md` and immediately commit/push before coding; branch `feature/<task-id-lowercase>` from latest `develop`; delete remote task branch after confirmed merge to `develop`.
- **Docs Tree & Task ID Rules (2026-11-13)**: Docs sharded into `Docs/INDEX.md` tree; M3+ feature IDs use domain prefixes (`COMBAT`, `SIEGE`, `FLOW`, `PREP`, `META`, `CONTENT`, `ART`, `UI`, `AUDIO`); unstarted irregular IDs normalized (`B-9.3-play` → `QA-game-loop`, `B-9.2-wave` → `SIEGE-01`, `B-4.6-action` → `SIEGE-02`); `In Progress` `C-1a-confirm` and reserved `E-1~4` kept until M3.
- **PERF-nav-query coordination (2026-11-12)**: Honor `DecisionInterval` for empty-target searches; retain immediate invalidation of existing targets and exact range/LoS rules.

## Pending / Partial `Q-*` Decisions

| ID | Decision Topic | Needed By / Blocking | Current Status |
|---|---|---|---|
| Q-10 | Final troop headcount, baseline gear, performance budget | Art integration, M6 | Undecided. B-1 500-unit test is not a production guarantee. |
| Q-11 | Command keys, combat HUD, prep UI layout | B-5.3, M2 UI | Partial: F1–F10 in Siege prototype; default F1–F4, `, 1, 2, 3. Overall UI style and staging decided 2026-11-20 (UI-05 to UI-08, [Decisions-Resolved](Decisions-Resolved.md)); battle HUD layout and command keys still undecided (needed by UI-08). |
| Q-14 | Release platforms, distribution channels, launch dates, scale | M4 scope, M7 delivery | Undecided. Focus on core completion criteria first. |
| Q-15 | Supported input devices, settings, languages, accessibility | M2 framework, M5 UI | Undecided. Avoid arbitrary additions. |
| Q-58 | Morale tuning values and prep-phase morale recovery facility | Morale balance | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-59 | Max building tiers, stair constraints, partial foundation collapse | Construction rules | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-61 | M4 content scale (hero/enemy/item/stage counts, target play time) | M4 production tasks | Undecided. Registered 2026-11-18 from Milestones. |

Q-41 shared ladder interaction rules were approved on 2026-11-13 and moved to [Decisions-Resolved](Decisions-Resolved.md). The user subsequently confirmed shared-code edits and tap F boarding / hold F toppling. Runtime implementation is tracked as SIEGE-04.
