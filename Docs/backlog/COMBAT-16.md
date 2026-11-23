---
id: COMBAT-16
title: Multi-Direction Ranged Melee-Threat Fallback Along Wall Walkways
status: Waiting
assignee: Unassigned
branch:
area: COMBAT
depends_on: []
created: 2026-11-23
---

# COMBAT-16 — Multi-Direction Ranged Melee-Threat Fallback Along Wall Walkways

## Scope & Completion Criteria

Improve the `COMBAT-14` ranged melee-threat fallback in [UnitCombatAI](../codemap/CombatAndSquads.md) and `RangedMeleeThreatRules`:

- Replace the single opposite-vector fallback probe with an ordered multi-angle search (0 deg, +/-45 deg, +/-90 deg) so ranged units on narrow wall walkways or corners sidestep along the walkway instead of failing into empty air behind the wall.
- Preserve same-level, `HoldMaxDescent`, short-path, and `RangedFallbackHoldLeash` guards, and ensure fallback points strictly increase separation from the threat.
- Use `RangedFallbackCooldown` when all candidate directions fail to avoid repeated path queries every two decision ticks.
- Verify with EditMode direction-candidate tests and PlayMode wall-top fallback tests.
