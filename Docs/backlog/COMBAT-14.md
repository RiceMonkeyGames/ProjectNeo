---
id: COMBAT-14
title: Ranged Target Priority, Melee-Threat Response & Hold Facing Restore
status: In Progress
assignee: SeongminHwang
branch: feature/combat-14
area: COMBAT
depends_on: []
created: 2026-11-22
---

# COMBAT-14 — Ranged Target Priority, Melee-Threat Response & Hold Facing Restore

## Scope & Completion Criteria

Priority P2 task from [Q-60](../history/Q/Q-60.md) (AI improvement pass). Covers ranged targeting quality and `Hold` post orientation in `UnitCombatAI`, `UnitAISettings`, and `SquadCommandCenter` ([Combat](../design/Combat.md), [CombatAndSquads](../codemap/CombatAndSquads.md)).

- **Data-driven ranged target priority**: Add configurable threat/role weights (for example, ladder climbers, enemy archers, siege breakers) building on the `TargetDistanceScale` pattern so wall-top and ground ranged units prioritize high-threat targets without breaking existing boss preferences.
- **Ranged melee-threat response**: Define and implement data-configurable behavior when a melee enemy closes on a ranged unit (keep distance / short fallback within leash while respecting `HoldMaxDescent` on wall tops, or clean melee self-defense target switch).
- **Hold facing restore**: Record the squad formation facing for each unit's `HoldPost` and rotate back to that facing after returning to post with no active target.
- **Pure rules & tests**: Add EditMode tests for priority scoring and facing/fallback rules, plus PlayMode verification.
