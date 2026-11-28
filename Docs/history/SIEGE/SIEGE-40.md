# SIEGE-40 | 2026-11-28 | SIEGE - Basin Map & North Wall Build Grid

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-28: SIEGE-40 - Basin Map & North Wall Build Grid

- **Assignee**: `신용준` | **Branch**: `feature/siege-40` (includes `feature/siege-38`) | **Claim commit**: `f9758bd` | **Work commits**: `d26f597`, `ddde4eb`
- **Decisions (user, 2026-11-28)**: wall on the innermost row of a 42 × 12 grid of 5m cells, gatehouse on cell 29 where the town road meets the wall, round end towers on cells 0 and 41 (not buildable); inside the castle = the wall and everything south of it (squads may be deployed on the wall). Recorded in [design/WallsAndSiege](../../design/WallsAndSiege.md).
- **Implementation**:
  - Rules: `BuildGridDefinition.castleBehindSouthEdge` and `blockedSideCells`, used by `BattlementRules` (outside floods from north/east/west only; off-grid south is inside), `CastleFootprint`, `GamePrepPlacement`, `BuildLayout`, `TowerRules`. Tests `NorthWallBasinRulesTests` (5).
  - Game scene rebuilt by menu `ProjectNeo/Setup/Apply Basin To Game Scene (SIEGE-40)` (`Editor/GameBasinSetup`): world reconfigured in place (37 references kept), basin environment under `Geometry`, spawn cell (29, 19), build camera, ladders; `GameBattleSetup` skips the battle start flag on basin grids.
  - Player: ground stick and 0.15s jump grace on uneven ground (`PlayerInputController`).
  - Run save version 4 (old saves dropped).
- **Follow-ups registered**: `SIEGE-37` (ladder carriers, note added), `SIEGE-41` (kit module audit), `SIEGE-42` (build grid visible over uneven ground), `UI-14` (minimap). Not registered yet: citizens on demo paths, Game lighting/fog vs the demo look.
- **Verification**: compile passed; full test run skipped at the user's request before the last commits. User checked in play: facilities and NPCs, jumping on uneven ground, preparation and battle on the north wall.