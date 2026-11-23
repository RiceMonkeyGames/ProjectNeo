# SIEGE-17 | 2026-11-23 | SIEGE - Enemy Ladder Reachability Check & Wall-Breaker Approach Spread

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: SIEGE-17 - Enemy Ladder Reachability Check & Wall-Breaker Approach Spread

- **Assignee**: `SeongminHwang` | **Branch**: `feature/siege-17` | **Claim commit**: `4380fdc`
- **Scope**: P2 item from [Q-60](../Q/Q-60.md). Rules: [WallsAndSiege](../../design/WallsAndSiege.md) (agent defaults, open to revision after playtest). Code map: [WallsAndSiege codemap](../../codemap/WallsAndSiege.md).
- **Implementation**:
  - `Runtime/Siege/SiegeAttackerBrain.cs`: `ChooseLadder` tries ladders in score order and takes the first one whose base samples onto the NavMesh within `LadderUseRadius` with a `PathComplete` path and whose `TopPoint` has a wall-top NavMesh landing. The result is cached per ladder per `NavigationRevision` and ground/wall-top band, so most decisions add no path query. With no walkable ladder, the unit goes to the gate (existing fallback). A toppled ladder (`SiegeLadderState.ToppleCount` change) releases the unit for a fresh choice so the crowd spreads out. A stalled ladder (no approach progress and no raise/climb activity for `LadderStallSeconds`) is avoided for `LadderAvoidSeconds`.
  - `Runtime/Siege/LadderChoiceRules.cs` (new): pure ladder score (distance + crowd penalty, current-ladder bonus, avoid penalty) and next-best iteration.
  - `Runtime/Siege/LadderStallTracker.cs` (new): pure stall timer; only getting closer than the best distance so far, or ladder activity, counts as progress.
  - `Runtime/Siege/WallBreakerAttack.cs`: stand slots are spread along all four faces of the target base. Other active breakers' held slots are penalized, slots are tried in score order with at most `MaxSlotPathChecks` path queries, and a breaker already in reach keeps its slot.
  - `Runtime/Siege/WallBreakerSpreadRules.cs` (new): pure slot count/offset from face length, claim counting and slot score. Grid-agnostic (face length comes from cell size).
  - `Runtime/Siege/SiegeSettings.cs`: `LadderStallSeconds` 6s, `LadderAvoidSeconds` 10s, `LadderAvoidPenalty` 30m.
  - `Runtime/Siege/WallBreakerSettings.cs`: `SlotSpacing` 1.2m (never less than the body diameter), `SlotClaimPenalty` 5m, `MaxSlotPathChecks` 8; `Create(...)` gains matching optional parameters.
  - Existing assets are unchanged; the new fields use their defaults.
- **5m kit notes**:
  - Reachability uses each ladder's own `BasePoint`/`TopPoint`, so the new lengths and tops from `SIEGE-21` apply without changes.
  - A longer 5m face yields more wall-breaker slots automatically.
  - Breakers and ladder units re-plan through breaches because the reachability cache follows `NavigationRevision`.
  - Whether towers are wall-breaker targets is left as is until `SIEGE-26`. Full 5m layout verification waits for `SIEGE-21`.
- **Automated Verification**:
  - Unity compile passed.
  - EditMode `863/863` PASSED, including `SiegeApproachRulesTests` (11).
  - PlayMode `SiegeApproachPlayTests` `2/2` PASSED: `LadderUnit_SkipsLadderWhoseBaseIsCutOff_AndUsesReachableOne`, `TwoWallBreakers_TakeSeparateStandPoints_AndBothStrike`.
  - Full PlayMode `334/354`. 19 failures are in the same classes that failed on develop before this task (`GameBattlementPlayTests`, `StackedConstructionPlayTests`, `GameFlowPlayTests`, `GameBattlePlayTests`, `GameOrganizationPlayTests`, `GamePerkPlayTests`, `GameUnlockPlayTests`, `PlayerHealthHudPlayTests`; see [COMBAT-18](../COMBAT/COMBAT-18.md)). The 20th, `SquadCommandPlayTests.AttackOrder_AdvancesToThePoint_FightsEnemiesSeenOnTheWay_ThenWaitsThere`, passed when its class was re-run alone (8/8), so it is treated as flaky. `WallBreakerPlayTests`, `SiegePrototypePlayTests`, `AiCoreDefectsPlayTests` all passed.
  - The full PlayMode run exceeds the 180s wait in `unity-verify.ps1`, so results were read from `unity command test status`.
- **Manual Verification**: the user requested `/finish` after receiving the guide below (2026-11-23).
  - Open `Assets/_Project/Scenes/SiegePrototype.unity`, Play, close the wall gaps (x=4, 11, 12) in preparation, then start the battle.
  - Ladder enemies should spread across several ladders; after allies topple a ladder, its crowd should re-choose.
  - Wall breakers on the same wall should stand at least 1m apart along the face and strike together.
  - A ladder whose base is walled off should be skipped in favor of another ladder or the gate.
  - Reset: stop Play Mode; do not save the scene.
