---
id: SIEGE-12
title: Machicolations: Shooting Down at the Wall Base
status: In Progress
assignee: SeongminHwang
branch: feature/siege-12
area: SIEGE
depends_on: []
created: 2026-11-21
---

# SIEGE-12 - Machicolations: Shooting Down at the Wall Base

## Scope & Completion Criteria

Registered 2026-11-21 from the user's decision in BUG-wall-archer-shoots-through-footing: the shooter's own wall piece now blocks line of sight and arrows, so wall-top archers can no longer hit enemies hugging the wall base (gate breakers, ladder carriers) from directly above ([Combat](../design/Combat.md) Line of sight). Add a dedicated way to shoot or drop at the wall base, for example machicolations (openings in an overhanging parapet above the gate or wall) or murder holes. Open before start: which pieces get it (gate only, any wall, a buildable upgrade), who uses it (AI archers, the player, both), attack type (arrows straight down, dropped stones or oil with area damage), cost/tier, and how AI ranged units pick these spots. Touches shared combat line-of-sight (`CombatPhysics`, `CombatProjectile`) and wall pieces: coordinate with the teammate.

## Decisions (user, 2026-11-22)

Recorded in [WallsAndSiege](../design/WallsAndSiege.md) Machicolations:
- Gate top only, fixed, free, present from the start. Wall-piece machicolations: later task.
- Users: allied AI units and the player.
- Attack methods as swappable modules; this task ships arrows straight down only. Stones/oil: later modules.
- Method chosen automatically by attacker type (no UI).
- AI: ranged unit in `Hold` near a free opening moves to it when an enemy is below and no other target is visible, returns when none is left. One unit per opening.

## Completion Criteria

- Gate top has openings; arrows from a unit standing at one reach enemies at the gate base without the footing rule blocking them.
- Allied ranged AI auto-uses and leaves openings as above; the player with a bow can shoot down through them.
- Attack method is a module interface with the arrow module as the first implementation.
- EditMode tests for the rules (opening selection, LOS exemption); PlayMode/manual check in the Game scene against gate breakers.
