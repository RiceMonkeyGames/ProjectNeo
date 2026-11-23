---
id: COMBAT-18
title: Hero Auto-Skill Urgency Priority, Area Heal Clustering & True Engagement Check
status: Waiting
assignee: Unassigned
branch:
area: COMBAT
depends_on: []
created: 2026-11-23
---

# COMBAT-18 — Hero Auto-Skill Urgency Priority, Area Heal Clustering & True Engagement Check

## Scope & Completion Criteria

Upgrade [HeroSkillAI](../codemap/CombatAndSquads.md) and `SkillAutoRules` beyond fixed slot-order evaluation (`COMBAT-13`):

- Replace first-match slot order (`0 -> 1 -> 2`) with urgency scoring across ready slots (critical heal > low-morale stabilization > high-value stagger/cluster offense > general buff/single-target offense).
- Add area heal cluster selection (analogous to `BestClusterCenter`) weighted by missing HP ratio so `AimPoint` heals cover multiple wounded allies instead of centering solely on one unit.
- Tighten `ChooseSupport` engagement checks so wall-top melee heroes do not waste attack buffs on unreachable enemies standing beneath the wall within straight-line `EngageDistance`.
- Prioritize high-value stagger targets (bosses, siege breakers, ladder climbers) for stagger skills when available.
- Verify with EditMode `SkillAutoRulesTests` and PlayMode `HeroSkillAI` tests.
