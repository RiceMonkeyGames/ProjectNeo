# PERF-ai-flow-field | 2026-11-23 | PERF - Hybrid Flow Field Navigation for Far-Distance (LOD 2) & Mass Siege Marching

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: PERF-ai-flow-field - Hybrid Flow Field Navigation for Far-Distance (LOD 2) & Mass Siege Marching

- **Assignee**: `신용준` | **Branch**: `feature/perf-ai-flow-field`
- **Scope**:
  - Added pure 2D XZ flow field generator `FlowFieldGrid` (`Assets/_Project/Scripts/Runtime/Combat/FlowFieldGrid.cs`) with zero-allocation pre-allocated buffers (`blocked[]`, `integration[]`, `directions[]`, `queue[]`), corner-cutting prevention across diagonal obstacles, multi-seed BFS/Dijkstra wave propagation (`Build`), and bilinear-interpolated world direction sampling (`TrySampleDirection`).
  - Added runtime service `BattleFlowFieldService` (`Assets/_Project/Scripts/Runtime/Combat/BattleFlowFieldService.cs`) maintaining shared opposing-team flow fields (`ToHostileField`, `ToAlliedField`, `DefaultCellSize = 3f`, `RebuildIntervalSeconds = 0.5f`), baking ground NavMesh obstacle cells once per scene / `ConstructionWorld.NavigationRevision`, and providing short local forward steps (`TryGetFlowStep`).
  - Extended `UnitAISettings` with `EnableFlowField` (default `true`), `FlowFieldMinDistance` (`18m`), and `FlowFieldStepDistance` (`8m`).
  - Integrated hybrid `TryUpdateFlowFieldEngage` into `UnitCombatAI`: far-distance (`AILodTier.Lod2`) ground units whose nearest enemy is beyond `FlowFieldMinDistance` advance along the shared flow field without calling individual full-map `NavMesh.CalculatePath`, and automatically hand off to exact `NavMeshAgent` pathfinding + `MeleeSlotBoard` ring slots upon entering `Lod1`/`Lod0`, closing within `FlowFieldMinDistance`, or encountering multi-tier elevation differences.
  - Updated `LargeBattleDebugHud` (`LargeBattleVerification.unity`) to display active `FlowField` unit counts, cumulative flow steps, and a toggleable (`F` key) 3D ground flow-direction bar grid.
  - Added EditMode test suite `FlowFieldGridTests` (4 cases).
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`, `CODE_RULES=PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode edit -Filter "FlowFieldGridTests"`: `4/4 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`: `21/21 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitAIQueryBenchmark" -IncludeExplicit`: `1/1 PASSED` (200v200 `Engage` mean AI time dropped 55.9% from `2.504 ms` to `1.103 ms`, p50 dropped 63.2% from `2.034 ms` to `0.749 ms`, p95 dropped 39.7% from `4.725 ms` to `2.848 ms`, and mean `CalculatePath` queries/frame dropped 68.8% from `97.9` to `30.5`):

| Case | Per team | Phase 3 (AI LOD) mean ms | Phase 5 (Flow Field) mean ms | Phase 3 p50 ms | Phase 5 p50 ms | Phase 3 p95 ms | Phase 5 p95 ms | Phase 3 path queries/f | Phase 5 path queries/f |
|---|---|---|---|---|---|---|---|---|---|
| Move | 50 | 0.125 | 0.127 | 0.130 | 0.134 | 0.168 | 0.165 | 0.0 | 0.0 |
| HoldIdle | 50 | 0.136 | 0.134 | 0.136 | 0.132 | 0.184 | 0.175 | 0.0 | 0.0 |
| HoldContact | 50 | 0.984 | 0.999 | 0.950 | 0.960 | 1.139 | 1.253 | 0.0 | 0.0 |
| Engage | 50 | 0.453 | **0.321 (-29.1%)** | 0.382 | **0.226 (-40.8%)** | 0.746 | **0.618 (-17.2%)** | 34.4 | **14.4 (-58.1%)** |
| Move | 200 | 0.555 | 0.491 | 0.556 | 0.500 | 0.697 | 0.636 | 0.0 | 0.0 |
| HoldIdle | 200 | 0.577 | 0.496 | 0.583 | 0.498 | 0.714 | 0.602 | 0.0 | 0.0 |
| HoldContact | 200 | 4.040 | 3.945 | 3.993 | 3.861 | 4.398 | 4.439 | 0.0 | 0.0 |
| Engage | 200 | 2.504 | **1.103 (-55.9%)** | 2.034 | **0.749 (-63.2%)** | 4.725 | **2.848 (-39.7%)** | 97.9 | **30.5 (-68.8%)** |
- **Manual Verification**:
  - CLI commands:
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "FlowFieldGridTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`
  - Scene check: open `Assets/_Project/Scenes/LargeBattleVerification.unity`, enter Play, observe cyan flow-field direction bars on the ground (`F` toggles bars on/off) and far `LOD 2` units advancing along the flow field before switching to `LOD 1`/`LOD 0` melee rings on contact, then stop Play Mode to reset.
