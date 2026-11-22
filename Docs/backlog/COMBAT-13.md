---
id: COMBAT-13
title: Hero Auto-Skill Targeting & Condition Improvements
status: In Progress
assignee: SeongminHwang
branch: feature/combat-13
area: COMBAT
depends_on: []
created: 2026-11-22
---

# COMBAT-13 — Hero Auto-Skill Targeting & Condition Improvements

## Scope & Completion Criteria

Priority P1 task from [Q-60](../history/Q/Q-60.md) (AI improvement pass). Improves autonomous hero skill casting in `HeroSkillAI`, `SkillAutoRules`, and `SkillAutoSettings` ([Combat](../design/Combat.md), [CombatAndSquads](../codemap/CombatAndSquads.md)).

- **Morale-gain threshold**: Add a data-configurable morale threshold in `SkillAutoSettings` so morale-rally skills do not fire immediately when the target squad's morale is already near or at maximum.
- **Area-offense cluster targeting**: For `AimPoint` area offense skills, pick the best valid enemy cluster center in skill range rather than aiming solely at `UnitCombatAI.Target`.
- **Independent skill-range target lookup**: When `UnitCombatAI.Target` is null (for example, a melee hero in `Hold` whose skill outranges melee reach), allow `HeroSkillAI` to find a valid target within the skill's own range.
- **Pure rules & tests**: Cover threshold and cluster-selection rules in EditMode tests and verify battle auto-cast behavior in PlayMode.
