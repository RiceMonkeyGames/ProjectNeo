---
id: SIEGE-28
title: Castle Wall Tier Upgrades
status: Pending Decision
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-18, SIEGE-24, SIEGE-38]
created: 2026-11-23
---

# SIEGE-28 - Castle Wall Tier Upgrades

## Scope & Completion Criteria

**Note 2026-11-27 (user, basin map decision): tiers now apply to the north wall band only; storey height may change with the `SIEGE-38` module unit.**

Registered 2026-11-23 by the user (follow-up of [Q-59](../history/Q/Q-59.md)); not started. Direction decided by the user: Castle wall tiers in [WallsAndSiege](../design/WallsAndSiege.md) and [Decisions-Resolved](../plan/Decisions-Resolved.md).

- A castle-wide wall tier upgrade. Each tier raises the wall storey cap and wall HP, or the tower height, and unlocks wall structures. Heights are counted in wall storeys (5m each).
- The wall cap is only a maximum: raising existing walls (the run-start ring included) is a paid player action.
- Towers: no longer "one storey above the touching walls"; every tower's height follows the castle tier automatically, so towers stay taller than the highest allowed wall.
- The Lv2 battlement upgrade (now on the Defense facility) moves into the wall tiers. Decorations are not tier unlocks: the modular dressing adds them automatically when its conditions are met (e.g. `ART-08` banners on three-storey walls), and interior stairs follow the wall structure (`SIEGE-27`).

Example progression from the user (to be tuned later):

| Castle tier | Unlock | Wall cap | Tower height |
|---|---|---|---|
| 1 | - | 1 storey (5m) | Cannot be built |
| 2 | Towers | 1 storey | 2 storeys (10m) |
| 3 | Taller walls | 2 storeys (10m) | 2 storeys |
| 4 | Taller towers | 2 storeys | 3 storeys (15m) |
| 5 | Taller walls | 3 storeys (15m) | 3 storeys |
| 6 | Taller towers | 3 storeys | 4 storeys (20m) |

Open before start (ask the user):
- Final tier count and contents, which tier carries the battlement upgrade, bastions and other structure unlocks, HP per tier, upgrade costs and where the upgrade is bought (and whether the Defense facility stays).
- Cost of the automatic tower growth on a tier upgrade (free with the upgrade or paid per tower), and walls already above a cap in saved runs.

Done when the tier rules are final in [WallsAndSiege](../design/WallsAndSiege.md), the tier is stored in the run, upgrades raise caps, HP and tower heights and unlock structures as decided, and EditMode/PlayMode tests cover each tier.
