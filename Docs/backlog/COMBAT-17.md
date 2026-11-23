---
id: COMBAT-17
title: Melee Target Slot Congestion Penalty & Ranged Overkill Spread
status: In Progress
assignee: SeongminHwang
branch: feature/combat-17
area: COMBAT
depends_on: []
created: 2026-11-23
---

# COMBAT-17 — Melee Target Slot Congestion Penalty & Ranged Overkill Spread

## Scope & Completion Criteria

Improve target distribution across allied and enemy squads in [UnitCombatAI](../codemap/CombatAndSquads.md):

- **Melee target congestion**: Feed `MeleeSlotBoard` occupancy (e.g., inner ring saturation) into `FindTarget` scoring for melee units so surplus attackers spread to adjacent uncrowded enemies instead of queuing behind one saturated target.
- **Ranged overkill spread**: Track light per-target active ranged shooter counts and apply a configurable score penalty once assigned firepower exceeds a threshold for low-priority grunts, while keeping high-threat targets (`LadderClimber`, `SiegeBreaker`, bosses) focus-fired.
- **Wall crenel micro-reposition**: Allow `Hold` ranged defenders blocked by structures for a sustained period to step to a nearby free crenel stand point when available.
- Verify with EditMode scoring rules and PlayMode large-squad engagement tests.
