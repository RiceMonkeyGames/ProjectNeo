# PERF-ai-lod | 2026-11-23 | PERF - 3-Tier Distance & Visibility AI LOD for 200v200 Battles

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: PERF-ai-lod - 3-Tier Distance & Visibility AI LOD for 200v200 Battles

- **Assignee**: `신용준` | **Branch**: `feature/perf-ai-lod`
- **Scope**:
  - Added pure 3-tier AI Level of Detail rules (`AILodTier.Lod0/Lod1/Lod2`, `AILodRules`) in `Assets/_Project/Scripts/Runtime/Combat/AILodRules.cs` and once-per-frame active camera view cache `AILodContext` in `Assets/_Project/Scripts/Runtime/Combat/AILodContext.cs`.
  - Extended `UnitAISettings` with `EnableAiLod` (default `true`), `Lod0Distance` (`25m`), `Lod1Distance` (`60m`), and `LodEvaluationInterval` (`0.5s`).
  - Integrated `UpdateLodTier` into `UnitCombatAI`:
    - **LOD 0** (within `25m` in view cone, or heroes / units engaged with heroes/player): `1.0x` decision cadence, top-K path candidates (`4`), full `MeleeSlotBoard` inner/outer ring path checks (`3` tries with `CalculatePath`), `HighQualityObstacleAvoidance`, and every-frame `CharacterModelAnimator` detail updates.
    - **LOD 1** (`25m`–`60m`, near off-screen, or top-down orthographic camera clamp): `2.0x` decision and cover-crouch cadence, top-2 path candidates, single melee slot try using `NavMesh.SamplePosition` + `SetDestination` without a second `CalculatePath`, `MedQualityObstacleAvoidance`, and every-2nd-frame animator detail updates.
    - **LOD 2** (`> 60m` or off-screen non-combat reserve): `4.0x` decision cadence, top-1 path candidate, direct center approach (melee ring slots skipped), idle machicolation search skipped, `LowQualityObstacleAvoidance` (`NoObstacleAvoidance` while stationary in `Hold` with no target), and throttled animator detail/bow-yaw updates (`4x` in view, `8x` off-screen).
  - Immediate promotion to `Lod0` when hit by a hero/player (`attacker.Character != null`).
  - Added EditMode suite `AILodRulesTests` and PlayMode verification `AILod_DowngradesFarUnitToLod2_AndPromotesNearUnitToLod0` in `UnitCombatPlayTests`.
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`, `SERIALIZED_ASSET_CHANGES=0`, `CODE_RULES=PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode edit -Filter "AILodRulesTests"`: `3/3 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`: `21/21 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "EngagementPlayTests"`: `3/3 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitAIQueryBenchmark" -IncludeExplicit`: `1/1 PASSED` (200v200 `Engage` mean AI time dropped 32.7% from `3.721 ms` to `2.504 ms`, p50 dropped 48.2% from `3.930 ms` to `2.034 ms`, and mean path queries/frame dropped 31.5% from `143.0` to `97.9`):

| Case | Per team | Phase 2 mean ms | Phase 3 (AI LOD) mean ms | Phase 2 p50 ms | Phase 3 (AI LOD) p50 ms | Phase 2 path queries/f | Phase 3 path queries/f |
|---|---|---|---|---|---|---|---|
| Move | 50 | 0.134 | 0.125 | 0.127 | 0.130 | 0.0 | 0.0 |
| HoldIdle | 50 | 0.133 | 0.136 | 0.129 | 0.136 | 0.0 | 0.0 |
| HoldContact | 50 | 0.949 | 0.984 | 0.924 | 0.950 | 0.0 | 0.0 |
| Engage | 50 | 0.546 | **0.453 (-17.0%)** | 0.543 | **0.382 (-29.7%)** | 35.8 | 34.4 |
| Move | 200 | 0.505 | 0.555 | 0.493 | 0.556 | 0.0 | 0.0 |
| HoldIdle | 200 | 0.509 | 0.577 | 0.505 | 0.583 | 0.0 | 0.0 |
| HoldContact | 200 | 3.850 | 4.040 | 3.801 | 3.993 | 0.0 | 0.0 |
| Engage | 200 | 3.721 | **2.504 (-32.7%)** | 3.930 | **2.034 (-48.2%)** | 143.0 | **97.9 (-31.5%)** |

- **Manual Verification**:
  - CLI commands:
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "AILodRulesTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "EngagementPlayTests"`
  - Scene check: open `Assets/_Project/Scenes/Game.unity`, enter Play, toggle `P` (top-down squad placement) and `Tab` (construction view), start battle with `F`, verify near units use full melee ring spreading and animations while distant units engage and fight normally without frame hitches, then exit Play Mode to reset.
