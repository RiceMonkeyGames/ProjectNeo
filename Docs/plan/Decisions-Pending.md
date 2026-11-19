# Pending Decisions & Active Operational Policies

Last Updated: 2026-11-19 20:21:53 (KST)

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
| Q-07 | Unlock point distribution timing, gacha UI location, guaranteed policy | M2 reward contracts, M3 UI | Partial: Gacha appearance flags in data; distribution timing pending. |
| Q-10 | Final troop headcount, baseline gear, performance budget | Art integration, M6 | Undecided. B-1 500-unit test is not a production guarantee. |
| Q-11 | Command keys, combat HUD, prep UI layout | B-5.3, M2 UI | Partial: F1–F10 in Siege prototype; default F1–F4, `, 1, 2, 3. Final HUD layout undecided. |
| Q-13 | Mid-run saves, resume capability, save slots | M2 lifecycle, M3 persistence | Partial (2026-11-16, META-01): meta save implemented with three slots, save on every meta change and on quit, backup recovery. Run save and Title "Continue" implemented in FLOW-02 (2026-11-16) with choices delegated to the agent: one run per slot, automatic saves at preparation start / battle start / reward / quit during preparation, no mid-battle save, defeat deletes the run save. Still open: saving inside a battle, manual saves, and save migration when content ids change. |
| Q-14 | Release platforms, distribution channels, launch dates, scale | M4 scope, M7 delivery | Undecided. Focus on core completion criteria first. |
| Q-15 | Supported input devices, settings, languages, accessibility | M2 framework, M5 UI | Undecided. Avoid arbitrary additions. |
| Q-58 | Morale tuning values and prep-phase morale recovery facility | Morale balance | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-59 | Max building tiers, stair constraints, partial foundation collapse | Construction rules | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-60 | AI improvement pass scope and priorities (enemy side owned by teammate) | AI quality tasks | Undecided. Registered 2026-11-18 from OpenQuestions. |
| Q-61 | M4 content scale (hero/enemy/item/stage counts, target play time) | M4 production tasks | Undecided. Registered 2026-11-18 from Milestones. |
| Q-62 | Skill Owners & Slots (from Q-12) | COMBAT-06 | Undecided. Registered 2026-11-19. Which characters have active skills (player, heroes; squads without a captain?), how many skill slots each has, and how a character gets a skill (fixed in hero data, chosen on level-up, granted by equipment, perks or unlocks). |
| Q-63 | Skill Activation & Targeting (from Q-12) | COMBAT-06 | Undecided. Registered 2026-11-19. Player input per skill slot (final keys with Q-11); whether hero skills are cast by their AI, ordered by the player through squad commands, or both; targeting modes (self, aimed point/area, target unit, whole squad) and ranges. |
| Q-64 | Skill Cost & Cooldown (from Q-12) | COMBAT-06 | Undecided. Registered 2026-11-19. Cooldown only, or also a resource (stamina/mana) or per-battle charges; starting cooldown values; whether cooldowns reset at battle start; relation to the 10s consumable cooldown (PREP-04). |
| Q-65 | First Skill Set for M2 (from Q-12) | COMBAT-06 | Undecided. Registered 2026-11-19. Effect families to support first (area damage, buff/debuff, heal, morale gain such as Rally under the COMBAT-07 gain rules, stagger through COMBAT-04 poise) and the concrete starting list (how many for the player and per hero). |
| Q-66 | Skills, Perks & Progression (from Q-12) | COMBAT-06 | Undecided. Registered 2026-11-19. Perks that change skills (cooldown, power, extra effect); how new skills are unlocked (Commander level, unlock gacha Q-07, hero level); whether skill choices carry across runs. |
| Q-67 | Run Length & Pacing (from Q-08) | M2 run ending/score, M4 | Undecided. Registered 2026-11-19. Final stage count per run (interim 3), fixed order vs chosen route, target run play time (shared with Q-61), and how difficulty grows per stage. |
| Q-68 | Boss Stages (from Q-08) | M2 run ending/score, M4 | Undecided. Registered 2026-11-19. Whether bosses exist and on which stages (last only, every N stages), boss roles (siege engine, champion, commander buffing other enemies), special rules (phases, effects on ally morale) and boss rewards. |
| Q-69 | Run Ending & After the Ending (from Q-08) | M2 run ending/score, M4 | Undecided. Registered 2026-11-19. What the victory ending shows, whether a run can continue after the last stage (endless or harder loop), and what happens to the run save. Already decided: clearing all stages ends the run; defeat rules are unchanged. |
| Q-70 | Run Score Formula (from Q-08) | M2 run ending/score, M4 | Undecided. Registered 2026-11-19. Metrics and weights (stages cleared, kills, bosses, wall/gate HP left, surviving heroes/grunts, gold left, unused prep time, casualties), victory vs defeat multiplier, and where the score is shown. |
| Q-71 | Reincarnation Bonus (from Q-08) | M2 run ending/score, M4 | Undecided. Registered 2026-11-19. Share of the run score converted to Commander EXP (and possibly unlock points), different rates for victory and defeat, and caps against power creep. |

Q-41 shared ladder interaction rules were approved on 2026-11-13 and moved to [Decisions-Resolved](Decisions-Resolved.md). The user subsequently confirmed shared-code edits and tap F boarding / hold F toppling. Runtime implementation is tracked as SIEGE-04.
