# PERF-ai-query-budget | 2026-11-22 | PERF - AI Decision Tick Staggering, Top-K Path Filtering & LoS Caching

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-22: PERF-ai-query-budget - AI Decision Tick Staggering, Top-K Path Filtering & LoS Caching

- **Assignee**: `신용준` | **Branch**: `feature/perf-ai-query-budget`
- **Scope**:
  - Added pure `AIDecisionScheduler` and `TargetCandidateBuffer<T>` in `Assets/_Project/Scripts/Runtime/Combat/AIDecisionScheduler.cs` to stagger simultaneous squad decision ticks (`InitialOffset`), track and budget per-frame `NavMesh.CalculatePath` calls (`TryConsumePathToken` / `HasPathBudget` / `RecordPathQuery`), and sort the top-K lowest-score enemy candidates in ascending priority order without GC allocations before running `CalculatePath`.
  - Updated `UnitAISettings` with `MaxPathCandidatesPerTick` (default `4`), `MaxPathQueriesPerFrame` (default `0` = unlimited unless configured), and `DecisionStaggerBuckets` (default `8`).
  - Updated `UnitCombatAI.FindTarget`, `UnitCombatAI.FindTargetNearPost`, and `SiegeAttackerBrain.AnyReachableEnemy` to evaluate `NavMesh.CalculatePath` only on the sorted top-K candidates first (with a full-scan fallback when all top-K candidates lie on disconnected islands or behind unbreached walls).
  - Cached line-of-sight (`hasClearShotCached`) in `UnitCombatAI.UpdateEngage` so physics raycasts run on decision intervals, target switches, or when `actor.CanAttack(Target)` is ready rather than every frame during ranged reload cooldowns, while holding ranged units in place when already in range with a clear shot.
  - Updated `SquadCommandCenter.Apply` to stagger initial decision timers across squad members when issuing `Attack` or `Hold` directives to multi-unit squads.
  - Extended `UnitAIQueryBenchmark` to record per-frame `CalculatePath` query counts (`mean_path_queries`) and added `AIDecisionSchedulerTests` (EditMode) plus `FindTarget_SortsTopKCandidates_AvoidingRedundantPathQueries` (`UnitCombatPlayTests`).
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`, `SERIALIZED_ASSET_CHANGES=0`, `CODE_RULES=PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode all`:
    - `EDITMODE=PASSED (Total=739 Passed=739 Failed=0 Skipped=0 Duration=4.61s)`
    - `PLAYMODE=PASSED (Total=325 Passed=325 Failed=0 Skipped=0 Duration=174.25s)`
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitAIQueryBenchmark" -IncludeExplicit`: `1/1 PASSED` (83% reduction in 200v200 synchronized decision-tick p95 spikes):

| Case | Per team | Before p95 ms (synchronized) | After p95 ms (staggered + top-K) | After mean ms | Mean path queries/frame |
|---|---|---|---|---|---|
| Move | 50 | 0.282 | 0.146 | 0.131 | 0.0 |
| HoldIdle | 50 | 0.942 | 0.275 | 0.231 | 0.0 |
| HoldContact | 50 | 1.198 | 1.186 | 0.971 | 0.0 |
| Engage | 50 | 2.526 | 0.590 | 0.541 | 35.8 |
| Move | 200 | 1.048 | 0.543 | 0.508 | 0.0 |
| HoldIdle | 200 | 12.579 | 2.127 | 2.044 | 0.0 |
| HoldContact | 200 | 4.366 | 4.070 | 3.826 | 0.0 |
| Engage | 200 | 25.268 | 4.215 | 3.812 | 143.0 |
- **Manual Verification**:
  - EditMode/PlayMode CLI commands:
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "AIDecisionSchedulerTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "UnitCombatPlayTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "EngagementPlayTests"`
  - Scene check: open `Assets/_Project/Scenes/Game.unity`, enter Play, press `F` at the start-battle prompt, select squads with `F1`–`F4` (or `` ` ``), issue `2` (Attack) and `3` (Hold), verify ranged defenders hold firing distance without rushing into melee during reload and melee units acquire and switch targets after kills, then exit Play Mode to reset.
