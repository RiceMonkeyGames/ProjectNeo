---
id: CONTENT-04
title: EnemyDefinition Data Asset Refactor
status: In Progress
assignee: SeongminHwang
branch: feature/content-04
area: CONTENT
depends_on: []
created: 2026-11-26
---

# CONTENT-04 — EnemyDefinition Data Asset Refactor

## Scope & Completion Criteria

Introduce a dedicated `EnemyDefinition` ScriptableObject so stage data, wave preview, reward tables, and future M4 enemy expansion no longer rely on raw enemy prefab names ([MoraleAndWave](../design/MoraleAndWave.md), [Principles](../workflow/Principles.md)).

### Planned Design & Scope

1. **Data Contract (`EnemyDefinition` ScriptableObject with `Create(...)` factory)**:
   - Stable string `Id` (compatible with `META-10` content rename/removal tables).
   - Display name (replacing `enemy_gate_breaker` -> `Gate Breaker` prefab-name parsing in `C-5` wave preview).
   - Role/archetype tag (Ground Ladder, Gate Breaker, Wall Breaker, Ranged, Flyer, etc.), prefab reference, and optional morale susceptibility flag (default off per `Q-44`).
   - Kill bounty gold and monster material drop table (centralized from scattered prefab/reward lookups while keeping existing numeric balance identical).

2. **Stage, Wave Preview & Spawner Integration**:
   - Update stage wave entries (`Stage01-07.asset`) via Editor API to reference `EnemyDefinition` assets (or provide a clean migration/adapter preserving existing `Game` and prototype test scenes).
   - Route `EnemyIntel` / `C-5` wave preview labels and kill/retreat tallies through `EnemyDefinition.Id` and `DisplayName`.
   - Keep `BossDefinition` (`SIEGE-09`) cleanly referencing base `EnemyDefinition` without changing existing boss stats or behavior.

### Completion Criteria

- Zero gameplay or balance regressions across Stages 1–7 and all prototype scenes.
- EditMode tests cover `EnemyDefinition` lookup, wave preview display names, and bounty/material resolution.
- Full EditMode/PlayMode suites pass; `Docs/design/MoraleAndWave.md` and `Docs/codemap/` updated.
