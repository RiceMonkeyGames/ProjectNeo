# PERF-ai-alloc-and-split | 2026-11-24 | PERF - Remove Per-Frame AI Allocations & Refactor UnitCombatAI Sub-States

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-24: PERF-ai-alloc-and-split - Remove Per-Frame AI Allocations & Refactor UnitCombatAI Sub-States

- **Assignee**: `SeongminHwang` | **Branch**: `feature/perf-ai-alloc-and-split`
- **Scope**:
  - `SiegeLadder.FindPushers`: replaced the per-frame `CharacterActor.ActiveActors` scan with `CombatSpatialIndex.QueryTeam` around `TopPoint` (Hostile within `LadderBlockRadius`, Allied within `LadderPushRadius`), followed by the same exact feet-position check (`IsAtTop`). Players are checked directly through the new `PlayerInputController.Active` registry, because grid cells are built from collider bounds that can lag a teleport. Per-actor parts (`PusherParts`: player flag, `LadderTraveller`, `UnitCombatAI`) are cached and looked up again while a needed part is still missing. `PruneStalePushers` disposes push handles and parts of dead or disabled actors every frame.
  - `PlacedStructure.Bodies`: lazily cached root colliders, reset in `SetDoorParts`. `WallBreakerAttack` uses it in `Decide`, `TryApproach` and `InReach`. The four-face slot buffer is preallocated and reused.
  - `FlyerBrain`: `IsSheltered` and `FindGround` use a shared `RaycastNonAlloc` buffer (32 hits). `UpdateBody` re-probes the ground only after `GroundProbeInterval` (scaled by the AI LOD tier), after `GroundProbeMoveTolerance` of horizontal travel, or when the flyer drops below the cached ground. Altitude always uses the current height. Target choice and `CanReachAnyEnemy` use the new pure `ShelterSampleCache<TKey>` for at most `min(ShelterCacheSeconds, AttackerDecisionInterval / 2)`; public `IsSheltered` stays exact. After the first decision, decisions are staggered with `AIDecisionScheduler.InitialOffset` (`DecisionStaggerBuckets`). `AILodRules` slows decisions and ground probes only while holding with no target; pursuing flyers, hero flyers, and runs with no active camera stay at LOD 0.
  - `FlyerSettings`: new fields with `Create(...)` defaults: `decisionStaggerBuckets` (4), `groundProbeInterval` (0.1s), `groundProbeMoveTolerance` (0.25m), `shelterCacheSeconds` (0.5s), `shelterMoveTolerance` (0.25m), `enableAiLod` (true), `lod0Distance` (25m), `lod1Distance` (60m), `lodEvaluationInterval` (0.5s). Existing assets pick up the field-initializer defaults; no `.asset` was edited.
  - `UnitCombatAI`: extracted the pure `RangedFallbackState` (active flag, remaining time, cooldown) and `FlowFieldAdvanceState` (active flag, direction). `SetDirective`, `OnDied` and `OnDisable` now share one `ResetTransientState` (fallback cancel, machicolation opening, melee slot, flow field, target and retaliation target, block action, shot cache). Before this change, `OnDied` and `OnDisable` left the flow-field flag set. Changing `Target` releases the old target's melee slot.
  - Added EditMode tests `ShelterSampleCacheTests` (4) and `RangedFallbackStateTests` (3).
  - Added PlayMode `AiAllocAndSplitPlayTests` (7), run on the real `SiegePrototype` scene. They check that the roof probe, the ground probe (cached and forced re-probe paths), `FindPushers` and `PlacedStructure.Bodies` do not allocate (Unity `Is.Not.AllocatingGCMemory`). Control cases (`Physics.RaycastAll` with a hit, `GetComponents<Collider>`) do allocate. The tests also cover ground-probe throttling by move tolerance, distinct staggered decision times for four flyers spawned together, pursuing flyers at LOD 0 on the next frame, shared shelter samples, release of push handles and part caches of a disabled pusher one frame later, `WallBreakerAttack` targeting with the cached collider array, and `ResetTransientState` on target change, `SetDirective` and disable.
- **Issues found by the behavior tests and fixed**:
  - `FindPushers` still allocated: `foreach` over `IReadOnlyList` interfaces (`PlayerInputController.Active`, and `traffic.Waiting` in `FriendlyUse`) boxes an enumerator. Both now use index loops. The `FlyerBrain` loops over `CharacterActor.ActiveActors` were changed the same way.
  - A flyer that acquired a target kept its previous (far) LOD tier until the next evaluation (up to `LodEvaluationInterval`). `UpdateLodTier` now returns LOD 0 at once while pursuing.
- **Known remaining allocation (not changed)**: `ConstructionWorld.Structures` and `Weapons` are exposed as `IReadOnlyCollection` over `Dictionary.Values`, so the per-decision loops in `FlyerBrain` (`Decide`, `HoldPoint`, `ComputeCruiseHeight`) and `WallBreakerAttack.Decide` still allocate one enumerator per loop. This happens once per decision, not per frame. Removing it needs a `ConstructionWorld` API change, which is left as a follow-up.
- **Regressions found and fixed during verification**:
  - `SiegePrototypePlayTests.SharedLadder_FriendlyQueuePreventsAutomaticToppling_ThenHoldFTopplesWithoutBoarding`: the player's cached `PusherParts.Traveller` was null because `LadderTraveller` is added after the first lookup. Fixed by `PusherParts.IsComplete`, which triggers a fresh lookup.
  - `FlyerSiegePlayTests.RoofedWallTopAlly_IsSkipped_UntilRoofIsGone`: a stale shelter cache and the LOD interval multiplier delayed retargeting after the roof was removed. Fixed by capping the cache age at half the decision interval and keeping pursuing flyers at LOD 0, with `Decide` run before `NextDecisionInterval`.
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`, `CODE_RULES=PASSED`.
  - `Tools/Agent/unity-verify-local.ps1 -Mode edit -SkipDotnet`: `870/870 PASSED`. The dotnet pre-check was skipped because the generated `.csproj` did not yet list the new files.
  - `-Mode play -Filter "SiegePrototypePlayTests"`: `31/31 PASSED`. `-Mode play -Filter "Flyer"`: `11/11 PASSED`.
  - `-Mode play -Filter "AiAllocAndSplitPlayTests"`: `7/7 PASSED`.
  - Full PlayMode after the final changes: `342/361 PASSED`, 19 failed (the same 19 as before the behavior tests were added). All 19 also fail without this change: `GameBattlementPlayTests` (5) and `GamePerkPlayTests` (1) were re-run on the stashed original code with identical failures, and the remaining failures (`StackedConstructionPlayTests`, `GameFlowPlayTests`, `GameBattlePlayTests` gate, `GameOrganizationPlayTests`, `GameUnlockPlayTests`, `PlayerHealthHudPlayTests`) are castle-layout, perk and UI tests outside the touched code.
  - `-Mode play -Filter "UnitAIQueryBenchmark" -IncludeExplicit` (no camera, so LOD 0 and no flow field), branch vs original code on the same PC. The results are equal within noise, so the `UnitCombatAI` restructure adds no cost:

| Case | Per team | Original mean ms | Branch mean ms | Original path queries/f | Branch path queries/f |
|---|---|---|---|---|---|
| HoldContact | 200 | 4.051 | 3.842 | 0.0 | 0.0 |
| Engage | 200 | 2.661 | 2.660 | 97.6 | 97.7 |
| Move | 200 | 0.465 | 0.451 | 0.0 | 0.0 |
| HoldIdle | 200 | 0.495 | 0.479 | 0.0 | 0.0 |

- **Manual Verification**: see the task completion report. Not run as part of this record.
