# SIEGE-37 | 2026-11-28 | SIEGE - Ladder Carriers Bring and Raise Their Own Ladders

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-28: SIEGE-37 - Ladder Carriers Bring and Raise Their Own Ladders

- **Assignee**: `SeongminHwang` | **Branch**: `feature/siege-37` | **Claim commit**: `7811423`
- **Decisions (user, 2026-11-28)**: every ladder-role enemy carries one ladder; a carrier walks at 0.7x speed, does not attack, keeps walking when hit and drops the ladder only on death; a dropped ladder can be picked up by another ladder enemy; ladder lanes are removed (bastions and towers may be placed anywhere outside); ladders hang only on plain wall faces, never on bastion or tower faces; the scouting preview shows no ladder count for now. Recorded in [design/WallsAndSiege](../../design/WallsAndSiege.md) and [plan/Decisions-Resolved](../../plan/Decisions-Resolved.md).
- **Implementation**:
  - Rules: `LadderPhase.Carried = 3` with `SiegeLadderState.TryLift` / `TryPlace` (ticks and toppling ignored while carried); `BattlementRules.LadderCrenelOffsets` (every whole crenel on a face); pure `LadderSlotRules` (score, spacing, nearby count). New `SiegeSettings` values: `ladderCarrySpeedMultiplier` 0.7, `ladderSlotSpacing` 3m, `ladderSlotCrowdRadius` 9m, `ladderSlotCrowdPenalty` 6, `ladderPickupRadius` 12m, `carriedLadderLength` 6.5m (temporary).
  - Scene objects: `SiegeLadder` carry state (`IsLoose`, `Carrier`, `Lift`, `DropLoose`, `TryClaimPickup`, `Claim`, `PlaceAt(LadderSlot)`, `TryDescribeSlot`); `LadderSlot` and `LadderSlotCatalog` (crenels on outer faces of top `Wall` pieces with an empty front cell, base and top on the NavMesh); `PlaceholderLadderView` shows the carried (shoulder height) and dropped (on the ground) look.
  - Enemy AI: `SiegeAttackerBrain` `Carrying` and `ToPickup` modes, MoveSpeed modifier while carrying, slot choice by score with path checks (falls back to the best free slot when no full path exists), drop on death, pickup within 12m. The ladder list is now a live `IReadOnlyList` (`SiegeAttacker.Bind` signature).
  - Battle: `BattleEnemySpawner` clones the inactive ladder template for each ladder enemy and clears ladders with the enemies; `GameBattleController` holds `ladderTemplate` and exposes `Ladders` from the spawner. Game scene updated by menu `ProjectNeo/Setup/Replace Game Ladders With Carried Template` (`GameBattleSetup.ApplyCarriedLadders`): pre-placed ladders removed, one inactive `LadderTemplate` under `GameLadders`. This menu does not rebuild the Game data assets.
  - Ladder lanes removed: `ConstructionWorld` lane API, `BastionRules` / `TowerRules` lane parameters, `PlacementIssue.LadderLane` (value 10 retired), HUD texts.
- **Verification**:
  - Unity compile passed. EditMode 1061/1061 passed (new `LadderCarryRulesTests`, `BattlementRulesTests.LadderCrenelOffsets_ListEveryWholeCrenel_OnTheFace`, updated bastion and tower rule tests).
  - PlayMode new and rewritten ladder tests passed: `GameBattlePlayTests` (carry at 0.7x and place at a north-wall crenel, drop on death and pickup, 3-storey ladder climb), `GameBattlementPlayTests.Ladders_RestOnTheCrenelSill_AndThePatternContinues`, `GameStageLoopPlayTests`, `GameAscensionPlayTests` (now reads speed from an enemy without a ladder).
  - Full PlayMode suite 346/398 before the `GameAscensionPlayTests` fix (that class then passed 2/2; the suite was not re-run). The remaining 51 failures are pre-existing. `GameBattlePlayTests` (9), `GameBattlementPlayTests` (3), `GameWavePlayTests` (2), `GameBossPlayTests` (1) and `GameCannonPlayTests` (1) fail identically on `develop` with this branch stashed. The others are layout, HUD and camera tests listed in [BUG-curtain-navmesh-read](../BUG/BUG-curtain-navmesh-read.md).
  - Visual check in Play Mode (scratch capture test, not committed): carriers hold ladders at shoulder height, then two ladders stand at north-wall crenels and one lies at the wall foot ready to raise.
  - Manual play and standalone build not verified.
- **Known limits / follow-ups**:
  - The basin spawn field is not NavMesh-connected to the wall front or the gate (paths are `PathPartial`, pre-existing since the basin map). Enemies do not reach the castle in normal play; the PlayMode tests start carriers in front of the wall. A separate fix session was started.
  - The carry slowdown is an additive percent modifier, so with ascension enemy speed +15% a carrier walks at 0.85x of base speed instead of 0.805x.
  - A carrier with no free slot waits holding its ladder. A retreating carrier takes its ladder with it.
  - Enemy assault is the teammate's area ([Team](../../plan/Team.md)); share this change with them.
