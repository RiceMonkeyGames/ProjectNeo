# Runtime Codemap: Walls & Siege

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Class map for castle construction, fall damage, siege weapons, and enemy siege AI. | Parent: [codemap/README](README.md) | Related: [CombatAndSquads](CombatAndSquads.md), [design/WallsAndSiege](../design/WallsAndSiege.md)

Base path: `Assets/_Project/Scripts/Runtime/`. Assembly: `ProjectNeo.Runtime`. Namespace: `ProjectNeo.<Folder>`.

| Folder | Key Classes | Role |
|---|---|---|
| Walls | `ConstructionProgress` | Construction duration and stage pacing calculations. Not yet linked to runtime placement. |
| Walls | `BuildGridDefinition`, `BuildPieceDefinition`, `BuildLayout` | Grid dimensions, `MaxLevels` (default 1), piece footprint, height, HP, and prefab definitions. Pure C# occupancy, rotation, boundaries, fixed gate zone checks, and detached stair connectivity queries. Layer stacking: keyed by `(x, level, z)`, pieces snap atop target column. `BuildPlacement.Level` and `Elevation` track layer and floor height. `Check(..., out level, out elevation)` validates placement; `Remove` followed by `Settle(dropped, unsupported)` collapses or removes unsupported pieces. `At(cell)`, `At(cell, level)`, `Top`, `ColumnHeight`, `IsCovered` query vertical layers. |
| Walls | `ConstructionWorld`, `PlacedStructure` | Spawns placement instances and colliders, elevates overlapping living units to new top surfaces without damage, piece health/destruction events, aggregates batch asynchronous NavMesh updates. |
| Walls | `PlacementViewSettings`, `PlacementController`, `BuildGridView` | Translucent preview materials/colors, mouse picking, placement, cancellation, rotation, grid rendering. Preview has zero collision/occupancy/NavMesh impact. |
| Walls | `SiegeWeaponDefinition`, `MountLayout`, `PlacedSiegeWeapon`, `ISiegeWeaponModule` | Unified siege weapon framework. Definition asset (ID, name, size, prefab), pure C# mount point occupancy rules (piece ID + mount ID), shared weapon entity container, extensible behavior interface. No cannon-specific hardcoding. |
| Walls | `BuildCameraSettings`, `ConstructionCameraController` | Orthographic camera angle, size, initial azimuth, rotation step, and transition duration settings. Q/E orbits pivot by 90 degrees while maintaining world/grid coordinates. Game asset enables middle-click drag pan, scroll zoom, Home reset, with tunable start pivot, sensitivity, and zoom clamps. |
| Walls | `FallSettings`, `FallDamageRules`, `UnitFallController` | Height-based fall damage calculation and collapse dropping/landing. Supports NavMeshAgent and CharacterController; restores navigation upon landing. `LiftForConstruction` teleports units harmlessly to new top surfaces upon placement. |
| Walls / UI | `ConstructionSandbox`, `ConstructionHud` | Test scene movement and foundation destruction buttons; uGUI/TMP piece picker and status HUD. |
| Walls | `WallCommandSandbox` | Integrated test scene Tab view toggle. Swaps construction and 1st-person camera, input, and HUD while retaining terrain and orders. Activates units after World builds NavMesh. |
| Siege | `BuildBudget`, `SiegeOutcomeRules`, `SiegeLadderState` | Pure C# siege rules. Temporary build budget deduction/refund, win/loss evaluation (defeat prioritizes simultaneous victory, Q-09) and retreat trigger, ladder state machine (placed → raising → raised, single climber queue, toppling). |
| Siege | `SiegeSettings` | Settings asset for budget, ladder timings/distances, enemy tick intervals, hotkeys (F topple, Tab view, R restart). |
| Siege | `SiegeLadder`, `PlaceholderLadderView` | Fixed ladder slots along wall perimeter. Active only when wall piece exists at slot. Raised by ground enemies, toppled by defenders atop walls. Blocked from toppling if enemies are actively climbing top. Visualized via placeholder bar under `Visual`. |
| Siege | `SiegeAttackerBrain` | Per-enemy siege decision AI. Delegates to `UnitCombatAI.Engage` if walkable ally exists; otherwise executes role-specific siege behaviors (`SiegeAttackerRole`: Ladder Grunt, Gate Breaker, Archer). Movement and attacks executed via `UnitDirective`. Ladder grunts redirect to gate if no ladders exist. `Retreat()` navigates unit off-map, setting state to `Fled` (excluded from active enemy tally). |
| Siege | `SiegePrepController`, `SiegeDirector` | Prep phase utilities (budget validation, demolition/refund, click-placement of squads) and match coordinator (spawns squads from `SiegeSettings`, orchestrates prep → battle → outcome, camera swaps, enemy activation, win/loss). Not connected to persistent run flow. |
| UI | `SiegeHud`, `SiegeStatusHud` | Test prep overlay, phase banners, troop count, and outcome screens. |
