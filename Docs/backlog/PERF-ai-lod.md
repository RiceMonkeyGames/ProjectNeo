---
id: PERF-ai-lod
title: 3-Tier Distance & Visibility AI LOD for 200v200 Battles
status: In Progress
assignee: 신용준
branch: feature/perf-ai-lod
area: PERF
depends_on: [PERF-ai-query-budget, PERF-ai-spatial-grid]
created: 2026-11-22
---

# PERF-ai-lod — 3-Tier Distance & Visibility AI LOD for 200v200 Battles

## Scope & Completion Criteria

Phase 3 of the 200 vs 200 large-scale battle AI optimization pass ([Combat](../design/Combat.md), [CombatAndSquads codemap](../codemap/CombatAndSquads.md), [Principles](../workflow/Principles.md)). Introduces a 3-tier AI Level of Detail (LOD) system that scales decision cadence, melee slot pathfinding, NavMesh RVO avoidance quality, and view-only animation work by camera distance, frustum visibility, and combat state while keeping pure combat rules (damage, HP, morale, win/loss) invariant.

### 1. Pure C# AI LOD Rules (`Combat/AILodRules.cs` & `UnitAISettings`)
- **LOD Tiers (` enum AILodTier { Lod0 = 0, Lod1 = 1, Lod2 = 2 } `)**:
  - **LOD 0 (Near / High Priority)**: Within `Lod0Distance` (`25m`, data-configurable) and inside camera frustum, OR currently targeting/attacked by the player character or a hero captain.
    - Decision interval multiplier: `1.0x` (0.25s).
    - Top-K path candidates: `3`; full `MeleeSlotBoard` inner/outer ring path checks (`SlotPathTries = 3`).
    - NavMeshAgent obstacle avoidance: `ObstacleAvoidanceType.HighQualityObstacleAvoidance`.
    - Visuals: full every-frame `CharacterModelAnimator` update (upper/lower layers, bow pitch, weapon sheath).
  - **LOD 1 (Mid / Active Frontline)**: Within `Lod1Distance` (`60m`) or near-range off-screen (`<= 25m` behind camera).
    - Decision interval multiplier: `2.0x` (0.50s).
    - Top-K path candidates: `2`; melee ring slots check `NavMesh.SamplePosition` only and reuse the target path without a second `agent.CalculatePath(slotPath)` (`SlotPathTries = 1`).
    - Cover crouch (`UpdateCoverCrouch`) and machicolation (`UpdateOpening`) check interval: `2.0x`.
    - NavMeshAgent obstacle avoidance: `ObstacleAvoidanceType.MedQualityObstacleAvoidance`.
    - Visuals: `CharacterModelAnimator` bow-yaw `LateUpdate` and secondary pose blends step every 2nd frame.
  - **LOD 2 (Far / Reserve / Off-Screen March)**: Beyond `60m` or off-screen reserve units with no target (`Target == null`).
    - Decision interval multiplier: `4.0x` (1.00s).
    - Top-K path candidates: `1`; melee approach slots bypassed (path directly to target `ReachPoint`).
    - Single-ray LoS check (body center only) and skip `UpdateOpening` while idle at post.
    - NavMeshAgent obstacle avoidance: `ObstacleAvoidanceType.LowQualityObstacleAvoidance` (or `NoObstacleAvoidance` while stationary in `Hold`).
    - Visuals: when off-screen (`!Renderer.isVisible`), suspend `CharacterModelAnimator` pose/blend calculations while keeping pure `CharacterActor` attack cooldowns and damage timing active.
- **Top-Down Camera Mode Rule**:
  - When orthographic top-down cameras are active (`ConstructionCameraController` / prep placement view), clamp active battle units within the camera view to at most `Lod1` so the entire battlefield remains responsive without running 400 units at `Lod0`.

### 2. Runtime LOD Evaluator (`Combat/UnitAILodController.cs` or `UnitCombatAI` integration)
- Re-evaluates each unit's `AILodTier` once every `LodEvaluationInterval` (e.g., `0.5s`, staggered across frames) using the active camera position/frustum planes cached once per frame.
- Immediate promotion to `Lod0` on `OnHitReceived` from the player or when entering `RangedMeleeThreatRadius` of the player.
- Exposed toggle `UnitAISettings.EnableAiLod` (default `true`) so deterministic single-unit EditMode/PlayMode tests can lock `Lod0` or test specific tiers explicitly.

### 3. Verification
- **EditMode tests (`AILodRulesTests`)**: Tier classification by distance, frustum dot/plane check, player-engagement promotion override, top-down mode clamping, and interval/candidate scaling.
- **PlayMode tests & 200v200 Benchmark**: Verify that combat outcomes and morale events remain valid across LOD transitions and record 200v200 `UnitAIQueryBenchmark` mean/p95 improvements.
