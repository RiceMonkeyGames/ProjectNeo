---
id: SIEGE-17
title: Enemy Ladder Reachability Check & Wall-Breaker Approach Spread
status: In Progress
assignee: 신용준
branch: feature/siege-17
area: SIEGE
depends_on: []
created: 2026-11-22
---

# SIEGE-17 — Enemy Ladder Reachability Check & Wall-Breaker Approach Spread

## Scope & Completion Criteria

Priority P2 task from [Q-60](../history/Q/Q-60.md) (AI improvement pass). Improves ground siege reliability in `SiegeAttackerBrain.ChooseLadder` and `WallBreakerAttack` ([WallsAndSiege](../design/WallsAndSiege.md), [WallsAndSiege codemap](../codemap/WallsAndSiege.md)).

- **Ladder reachability & fallback**: Filter ladder candidates in `ChooseLadder` by actual NavMesh reachability (`CalculatePath` complete) so blocked or disconnected ladders are skipped, and add crowd/post-topple re-evaluation so units do not stall indefinitely on a single blocked ladder.
- **Wall-breaker approach spread**: Distribute `WallBreakerAttack.TryApproach` stand points across the target wall face (similar to `GateSpread` for gate breakers) so multiple wall breakers can strike simultaneously without blocking each other out of melee range.
- **Pure rules & tests**: Add EditMode spread/scoring tests and PlayMode multi-attacker verification.

## 5m castle kit adjustments (added 2026-11-23 after SIEGE-14)

The Game is moving to 5m cells and tiers with the castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md), [SIEGE-14](../history/SIEGE/SIEGE-14.md)). Build these rules grid-agnostic and verify them on the 5m layout once `SIEGE-18` and the ladder resize `SIEGE-21` land:

- **Ladder reachability**: ladder lengths and slot spacing change for 5m to 15m walls (`SIEGE-21`); the reachability filter must use the new lengths and tops (kit crenel sill 0.76m for the hook point). Ladder lanes keep blocking bastions and now also towers (`TowerRules.Check` returns `LadderLane`).
- **Wall-breaker approach spread**: a 5m wall face leaves room for more simultaneous breakers; spread stand points along the face length from cell size. Towers (2×2, outside the wall line) are outer pieces too: decide with `SIEGE-26` whether breakers target a tower's bottom tier like a bastion.
- **Breaches**: with the valley rule on in the Game, a collapsed wall cell takes every storey above it and leaves a V-shaped ruin that agents walk through; stairs that land on the collapsed cell stay and block it (50 HP vs 100 for walls in the spike). Breakers and ladder units should re-plan through such breaches; targeting of blocking stairs is in `SIEGE-26`.
