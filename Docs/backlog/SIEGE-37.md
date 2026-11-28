---
id: SIEGE-37
title: Ladder Carriers Bring and Raise Their Own Ladders
status: In Progress
assignee: SeongminHwang
branch: feature/siege-37
area: SIEGE
depends_on: [SIEGE-20, SIEGE-40]
created: 2026-11-27
---

# SIEGE-37 - Ladder Carriers Bring and Raise Their Own Ladders

## Scope & Completion Criteria

**Note 2026-11-28 (user, basin map):** the user confirmed this direction for the basin Game: ladders are brought by monsters instead of being laid in advance. On the basin north wall (`SIEGE-40`) `GameBattleSetup` still lays fixed ladders every 2 cells along the single wall row (cells 4–36); this task removes them. The wall row is the grid's innermost row with `BuildGridDefinition.castleBehindSouthEdge`, so carriers raise ladders against its north face only.

Registered 2026-11-27 by the user during [SIEGE-20](../history/SIEGE/SIEGE-20.md) ([Decisions-Resolved](../plan/Decisions-Resolved.md), "Kit battlement cover rule"). Today the Editor setup (`GameBattleSetup`) lays ladders on the ground every 2 cells along the north wall before battle, and ladder enemies pick one from that fixed list and raise it. This task replaces that with ladder carriers who bring their own ladder and raise it at a free kit crenel.

- **Ladder lifecycle**: spawn the ladder at runtime with its carrier; add a carried phase to `SiegeLadderState` before `Down` / `Raising` / `Up`; decide what happens to the ladder when the carrier dies (dropped and reusable by another carrier, or lost).
- **Fixed ladder lists**: move `GameBattleController`, `BattleEnemySpawner`, `SiegeAttackerBrain` (ladder choice and reachability) and `SiegeReachabilityCache` (keyed by ladder index) from the fixed `SiegeLadder[]` to the live `SiegeLadder.Active` set. Check `LadderDefenseDirector` and the siege HUDs.
- **Slot choice**: carriers choose a free crenel on an outer wall face (SIEGE-20 crenel positions) by distance, crowding and defenders.
- **Design questions for the user before coding**: whether the ladder lanes that block bastion and tower placement (`ConstructionWorld.ReserveLadderLane`, `BastionRules`, `TowerRules`) are removed or kept; whether ladders may hang on bastion and tower faces; whether the scouting preview shows ladder counts.
- Update the PlayMode tests that rely on pre-placed ladders (about 13 files reference `SiegeLadder`).

Enemy assault is the teammate's area ([Team](../plan/Team.md)); agree on the owner before claiming.

Done when ladder enemies carry and raise their own ladders at free crenels in the `Game` battle, allies still push them down, and EditMode/PlayMode tests cover the carried phase and slot choice.
