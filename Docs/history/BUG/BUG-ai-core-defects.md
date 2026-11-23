# BUG-ai-core-defects | 2026-11-23 | BUG - Fix AI Machicolation Leak, Hold Fallback, Flow Field Elevation & Ladder Crowding

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: BUG-ai-core-defects - Fix AI Machicolation Leak, Hold Fallback, Flow Field Elevation & Ladder Crowding

- **Assignee**: `SeongminHwang` | **Branch**: `fix/bug-ai-core-defects`
- **Scope**:
  - **Machicolation claim leak on Hold squad sleep (`UnitCombatAI`)**: Updated `UpdateHold` so active `ShotOpening` claims still enter `UpdateOpening` and release cleanly when `squadNeighborhoodClear` becomes true, while suppressing new `ClaimOpening` searches during squad sleep.
  - **`Hold` chase target elevation filter & fallback (`HoldDescentRule`, `UnitCombatAI`)**: Added pure `HoldDescentRule.EndpointMayStayAbove` to pre-filter lower-level enemies outside `HoldMaxDescent` before top-K selection in `FindTargetNearPost`, and added a fallback scan over remaining candidates when all top-K candidates fail `CanReachInHold`.
  - **Flow Field ground elevation & LOD2/ground-band guard (`FlowFieldGrid`, `BattleFlowFieldService`, `UnitCombatAI`)**: Added pure `FlowFieldGrid.ComputeGroundElevation` (minimum grounded elevation instead of scene actor average) and `FlowFieldGrid.IsWithinGroundBand`; updated `BattleFlowFieldService` to sample living non-airborne actor elevations and re-bake if lower ground actors spawn later; updated `UnitCombatAI.TryUpdateFlowFieldEngage` to enforce `AILodTier.Lod2`, skip units with `TargetDistanceScale`, filter ground-band candidates, and avoid mutating `Target` before Flow Field checks succeed.
  - **Allied ladder approach crowding (`LadderTraffic<T>`, `SiegeLadder`, `LadderTraveller`)**: Added approach-time reservations (`Reserve`, `Unreserve`, `Approaching`, `PlannedLoad`) disjoint from `Occupant`/`Waiting` (preserving `FriendlyUse` topple safety), exposed `PlannedCrowdExcluding` on `SiegeLadder`, and updated `LadderTraveller.FindRoute`/`TickAI` to reserve immediately on route choice so allied squads distribute across multiple ladders before reaching the base.
  - **Tests**: Extended EditMode tests (`HoldDescentRuleTests`, `FlowFieldGridTests`, `LadderTrafficTests`), added PlayMode sleep-release regression to `MachicolationPlayTests`, and added `Assets/Tests/PlayMode/AiCoreDefectsPlayTests.cs` (3 cases).
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `SCANNED_CS_FILES=7`, `VIOLATIONS=0`, `CODE_RULES=PASSED`, `SERIALIZED_ASSET_CHANGES=0`.
  - `Tools/Agent/unity-verify.ps1 -Mode edit`: `815/815 PASSED` (`5.5s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "AiCoreDefectsPlayTests"`: `3/3 PASSED` (`0.75s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "MachicolationPlayTests"`: `6/6 PASSED` (`0.64s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`: `21/21 PASSED` (`4.03s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "EngagementPlayTests"`: `3/3 PASSED` (`0.93s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "RangedThreatPlayTests"`: `7/7 PASSED` (`0.81s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "WallTopArcherPlayTests"`: `7/7 PASSED` (`1.08s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "SquadCommandPlayTests"`: `8/8 PASSED` (`4.16s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "SiegePrototypePlayTests"`: `31/31 PASSED` (`15.94s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "GameBattlePlayTests"`: `15/15 PASSED` (`7.24s`).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "GameSquadMovePlayTests"`: `1/1 PASSED` (`1.94s`).
- **Manual Verification**:
  - CLI commands:
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "FlowFieldGridTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "HoldDescentRuleTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "LadderTrafficTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "AiCoreDefectsPlayTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "MachicolationPlayTests"`
  - Scene check (`Assets/_Project/Scenes/Game.unity` or `Assets/_Project/Scenes/SiegePrototype.unity`):
    - Place a ranged squad on the gate top (`Hold`), let enemies approach the gate base and be killed; confirm the archer leaves the machicolation and another archer can reuse it for the next wave.
    - Keep a melee squad in `Hold` on a wall top while enemies mass beneath the wall and one climbs up; confirm the wall-top defenders immediately engage the climber on their level.
    - Order an allied squad outside the walls (`1` Move) onto a closed wall top with 2+ ladders; confirm units split across nearby ladders while walking toward them rather than queuing behind one ladder.
    - Stop Play Mode to reset.
