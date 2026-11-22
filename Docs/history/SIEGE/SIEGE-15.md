# SIEGE-15 | 2026-11-23 | SIEGE - Enemy Assault Transition Thresholds & Archer Positioning Pass

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: SIEGE-15 - Enemy Assault Transition Thresholds & Archer Positioning Pass

- **Assignee**: `신용준` | **Branch**: `feature/siege-15`
- **Decisions** (user, 2026-11-23, recommended options): breach-or-leash assault for ground siege enemies; archers support from outside after a breach and enter only after a no-target delay; AI LOD also scales the siege decision interval. Rules: [WallsAndSiege](../../design/WallsAndSiege.md), [Decisions-Resolved](../../plan/Decisions-Resolved.md).
- **Scope**:
  - Pure `Siege/SiegeAssaultRules` (`IsBreachTarget`, `LeashRadius`, `ShouldAssault`), `Siege/CastleFootprint` (wall/gate rectangle extended away from `outward`; flood-fill outside set for face masks) and `Siege/ArcherApproachRules` (`ChooseFace`, `ApproachDirection`).
  - `SiegeSettings`: `assaultLeashRadius` 8m, `assaultLeashReleaseRadius` 12m, `archerBreachAdvanceDelay` 8s, `archerCrouchTolerance` 4s (no asset edits; field defaults apply).
  - `SiegeAttackerBrain`: `EvaluateAssault` (inside or wall top: any reachable ally; outside: sortie within leash via `TryReachableSortie`, or breach via `TryReachableBreach` with the shared `SiegeReachabilityCache` no-breach slot); archer commit after the idle delay; demotion from `Assault` resets the directive to `Hold`; `CanReachAnyEnemy` uses the same rule (retreat checks).
  - Archer positioning: approach point on the target face normal, side-steps along that face, top-K clear-shot checks by LOD tier, `ArcherCoverPlan.TargetDucked` reload-crouch tolerance, `UnitCombatAI.SetHoldChaseSuppressed` so supporting archers do not hold-chase hidden defenders through a gap.
  - AI LOD: decision interval × `AILodRules.IntervalMultiplier(ai.LodTier)`; NavMesh revision and ladder-mask changes wake immediately; hits cap the wait at `AttackerDecisionInterval`; LOD2 archers skip side-step searches.
  - `SiegeAttacker.HasEntryRoute` (archers that see a breach) feeds `SiegeDirector.ShouldOrderRetreat`.
  - Tests: EditMode `SiegeAssaultRulesTests` (14); PlayMode `SiegePrototypePlayTests` `ClosedWalls_FarSortieAlly_DoesNotPullSiegeEnemies_ButLeashDoes`, `OpenGap_EnemyArchersKeepShootingFromOutside_WhileBreakersAssault`, `ProtrudingWall_EnemyArcherApproachesItsSideFace`, `EnemyArcher_WaitsInPlace_WhileTargetReloadCrouches`.
- **Automated Verification**:
  - `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`, `SERIALIZED_ASSET_CHANGES=0`, `CODE_RULES=PASSED`.
  - `Tools/Agent/unity-verify-local.ps1 -Mode edit`: `772/772 PASSED`.
  - `-Mode play -Filter "SiegePrototypePlayTests"`: `31/31 PASSED`; `EnemyCompositionPlayTests` `4/4`; `GameBattle` `22/22`; `Retreat` `2/2`.
  - Full PlayMode (before the QA follow-up fixes): `329/330`; the only failure, `GameMoralePlayTests.ContagionTicksInScene_OnlyNearbyAlliesLoseMorale_AndHudShowsCheck`, also fails with all SIEGE-15 changes stashed (pre-existing).
  - `qa-verifier` audit: PASS; its two Medium findings (retreat/assault rule mismatch, stale `Engage` after assault demotion) and the per-hit re-decision cost were fixed.
- **Manual Verification** (user confirmed 2026-11-23):
  - `Assets/_Project/Scenes/SiegePrototype.unity`: close the three gaps (columns 4, 11, 12) in preparation and start; enemies keep ladder/gate duty. With gaps open, breakers enter and archers keep shooting from outside for about 8s before entering.
  - `Assets/_Project/Scenes/Game.unity`: Play, `F` to start battle; flank walls and bastions are shot from their own face; reload-crouching defenders do not make enemy archers side-step at once; a single far sortie ally does not pull the wave.
  - Reset: stop Play Mode; do not save the scene.
