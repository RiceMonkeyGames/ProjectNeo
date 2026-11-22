---
id: SIEGE-17
title: Enemy Ladder Reachability Check & Wall-Breaker Approach Spread
status: Waiting
assignee: Unassigned
branch:
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
