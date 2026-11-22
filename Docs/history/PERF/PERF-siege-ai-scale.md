# PERF-siege-ai-scale | 2026-11-23 | PERF - Squad-Level Idle Sleep, Siege Reachability Cache & Archer Search Budget

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: PERF-siege-ai-scale - Squad-Level Idle Sleep, Siege Reachability Cache & Archer Search Budget

- **Assignee**: `신용준` | **Branch**: `feature/perf-siege-ai-scale`
- **Scope**:
  - Added pure `SquadThreatWatch` (`Assets/_Project/Scripts/Runtime/Squads/SquadThreatWatch.cs`) computing each holding squad's bounding center and covering threat radius (`TryComputeBounds`, `CanSleepInHold`).
  - Integrated `UpdateSquadThreatWatch` into `SquadCommandCenter` (`0.25s` interval): queries `CombatSpatialIndex` once per holding squad and sets `UnitCombatAI.SetSquadNeighborhoodClear(true)` on all members when zero enemies are inside the squad's bounding sphere, allowing idle reserve squads (up to 10 squads × 21 units) to skip per-member `FindTargetInReach`, `FindTargetNearPost`, `UpdateMeleeThreat`, and `UpdateOpening` scans. Wakes immediately on `OnHitReceived` or any new directive.
  - Added pure `SiegeReachabilityCache` (`Assets/_Project/Scripts/Runtime/Siege/SiegeReachabilityCache.cs`) and `SiegeSettings.ReachabilityCacheTtl` (`0.5s`) / `MaxArcherRepositionSearchesPerFrame` (`2`).
  - Integrated `SiegeReachabilityCache` into `SiegeAttackerBrain`:
    - Ground attackers outside unbreached walls reuse the cached `unreachable` result while `ConstructionWorld.NavigationRevision`, the raised ladder bitmask (`CurrentLadderMask`), and `CharacterActor.RegistryRevision` remain unchanged (still checking a local 15m spatial query in case an ally is outside the walls).
    - Staggered initial `SiegeAttackerBrain` decision ticks across 8 buckets (`AIDecisionScheduler.InitialOffset`).
    - Capped simultaneous enemy archer side-step reposition searches (`TryFindFiringPosition`) via `TryConsumeArcherSearchToken` (`MaxArcherRepositionSearchesPerFrame = 2`) and limited `CanShootFrom` raycasts to the top-4 nearest in-range defenders via `TargetCandidateBuffer<CharacterActor>`.
  - Added EditMode test suite `SiegeScaleRulesTests`.
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`, `SERIALIZED_ASSET_CHANGES=0`, `CODE_RULES=PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode edit -Filter "SiegeScaleRulesTests"`: `3/3 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "SiegePrototypePlayTests"`: `27/27 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "EnemyCompositionPlayTests"`: `4/4 PASSED`.
  - `Tools/Agent/unity-verify.ps1 -Mode play -Filter "GameSquadCapsPlayTests"`: `2/2 PASSED`.
- **Manual Verification**:
  - CLI commands:
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode edit -Filter "SiegeScaleRulesTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "SiegePrototypePlayTests"`
    - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter "EnemyCompositionPlayTests"`
  - Scene check: open `Assets/_Project/Scenes/Game.unity`, enter Play, start battle with `F`, verify rear holding squads stay idle until enemies approach or hit them, verify ground siege enemies execute ladder/gate/wall duties before breach and transition to assault immediately when the gate or wall breaches or a ladder is raised, and verify enemy archers reposition smoothly behind battlements, then stop Play Mode to reset.
