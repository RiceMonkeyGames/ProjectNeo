# PERF-ai-spatial-grid | 2026-11-23 | PERF - 2D Spatial Hash Grid for Fast AI Proximity & Target Queries

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: PERF-ai-spatial-grid - 2D Spatial Hash Grid for Fast AI Proximity & Target Queries

- **Assignee**: `신용준` | **Branch**: `feature/perf-ai-spatial-grid`
- **Scope**:
  - Added pure generic 2D XZ spatial hash grid `CombatSpatialGrid<T>` (`Assets/_Project/Scripts/Runtime/Combat/CombatSpatialGrid.cs`) partitioning world coordinates into uniform square cells (`cellSize = 8f`, configurable via `UnitAISettings.SpatialCellSize`), separating `CombatTeam.Allied` and `CombatTeam.Hostile` into zero-allocation linked-index arrays (`head` dictionary + `next[]` + `items[]` + `positions[]`).
  - Added runtime service `CombatSpatialIndex` (`Assets/_Project/Scripts/Runtime/Combat/CombatSpatialIndex.cs`) and `CharacterActor.RegistryRevision` counter so the grid rebuilds at most once per frame (or when actors are added, removed, killed, or change team) using collider bounds centers (`DefaultBodyPadding = 2f` for offset/large colliders).
  - Routed `UnitCombatAI` proximity and target queries (`FindMeleeThreat`, `FindTargetInReach`, `FindTargetNearPost`, `FindTarget`), `SiegeAttackerBrain` queries (`AnyReachableEnemy`, `CanShootFrom`), `HeroSkillAI` queries (`TryClusterAim`, `MostHurtAllyInRange`, `NearestEnemy`), and `ShotOpening` queries (`HasEnemyBelow`, `IsFreeFor`) through `CombatSpatialIndex`.
  - Added EditMode suite `CombatSpatialGridTests` covering positive/negative coordinates, team filtering, reuse/clear, and exact match against brute-force radius queries across 200 units.
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`, `SERIALIZED_ASSET_CHANGES=0`, `CODE_RULES=PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode edit -Filter "CombatSpatialGridTests"`: `4/4 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`: `20/20 PASSED` (including offset collider broad-phase retention test).
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "HeroSkillAIPlayTests"`: `8/8 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "MachicolationPlayTests"`: `5/5 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitAIQueryBenchmark" -IncludeExplicit`: `1/1 PASSED` (200v200 `HoldIdle` mean dropped a further 75.1% from `2.044 ms` to `0.509 ms`, and p95 dropped from `2.127 ms` to `0.549 ms`):

| Case | Per team | Phase 1 mean ms | Phase 2 (Spatial Grid) mean ms | Phase 1 p95 ms | Phase 2 (Spatial Grid) p95 ms |
|---|---|---|---|---|---|
| Move | 50 | 0.131 | 0.134 | 0.146 | 0.167 |
| HoldIdle | 50 | 0.231 | **0.133 (-42.4%)** | 0.275 | **0.157 (-42.9%)** |
| HoldContact | 50 | 0.971 | 0.949 | 1.186 | 1.148 |
| Engage | 50 | 0.541 | 0.546 | 0.590 | 0.603 |
| Move | 200 | 0.508 | 0.505 | 0.543 | 0.568 |
| HoldIdle | 200 | 2.044 | **0.509 (-75.1%)** | 2.127 | **0.549 (-74.2%)** |
| HoldContact | 200 | 3.826 | 3.850 | 4.070 | 4.085 |
| Engage | 200 | 3.812 | 3.721 | 4.215 | 4.089 |

- **Manual Verification**:
  - CLI commands:
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "CombatSpatialGridTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "HeroSkillAIPlayTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "MachicolationPlayTests"`
  - Scene check: open `Assets/_Project/Scenes/Game.unity`, enter Play, start battle with `F`, verify allied ranged/melee squads in `Hold` and `Engage` acquire nearby enemies, heroes auto-cast skills on enemy clusters, and wall-top archers use gate machicolations when enemies stand below the gate, then stop Play Mode to reset.
