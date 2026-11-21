---
id: SIEGE-12
title: Machicolations: Shooting Down at the Wall Base
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: []
created: 2026-11-21
---

# SIEGE-12 - Machicolations: Shooting Down at the Wall Base

## Scope & Completion Criteria

Registered 2026-11-21 from the user's decision in BUG-wall-archer-shoots-through-footing: the shooter's own wall piece now blocks line of sight and arrows, so wall-top archers can no longer hit enemies hugging the wall base (gate breakers, ladder carriers) from directly above ([Combat](../design/Combat.md) Line of sight). Add a dedicated way to shoot or drop at the wall base, for example machicolations (openings in an overhanging parapet above the gate or wall) or murder holes. Open before start: which pieces get it (gate only, any wall, a buildable upgrade), who uses it (AI archers, the player, both), attack type (arrows straight down, dropped stones or oil with area damage), cost/tier, and how AI ranged units pick these spots. Touches shared combat line-of-sight (`CombatPhysics`, `CombatProjectile`) and wall pieces: coordinate with the teammate.
