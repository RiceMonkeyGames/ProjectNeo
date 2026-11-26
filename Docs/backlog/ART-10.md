---
id: ART-10
title: Pooled VFX System & Core Combat/Siege Feedback VFX
status: Waiting for Prerequisite
assignee: Unassigned
branch:
area: ART
depends_on: [Q-84]
created: 2026-11-26
---

# ART-10 — Pooled VFX System & Core Combat/Siege Feedback VFX

## Scope & Completion Criteria

Build a pooled presentation-only VFX spawner and wire core combat, siege, skill, and morale visual effects approved under `Q-84` ([Principles](../workflow/Principles.md), [OpenQuestions](../design/OpenQuestions.md), [Milestones](../plan/Milestones.md)).

### Planned Design & Scope

1. **Pooled VFX Infrastructure (`VfxCueDefinition` & `VfxPoolDirector`)**:
   - Data-driven `VfxCueDefinition` ScriptableObjects (prefab, pool cap, per-cue cooldown, max distance culling, auto-return lifetime).
   - Zero per-hit GameObject instantiation; all instances stay strictly in presentation layers (`Visual` / dedicated pool root) with no colliders, NavMesh impact, or gameplay-rule coupling.
   - Clean battle-end cleanup matching stuck-arrow cleanup (`ART-05`).

2. **Core Feedback VFX Wiring**:
   - **Combat**: melee impact sparks/flashes, stagger/poise-break cue (`COMBAT-04`), arrow trail ribbons (`ART-05`), potion heal burst (`PREP-04`).
   - **Siege & Castle**: on-cannon fire-ready glow/smoke cue (`SIEGE-07` follow-up in [OpenQuestions](../design/OpenQuestions.md)), cannon muzzle flash/smoke, shell ground/wall explosion, gate/wall hit chips and collapse dust clouds.
   - **Active Skills (`COMBAT-06`)**: distinct cast/area/impact visuals for the 6 effect families (area damage, attack buff, enemy slow debuff, heal, morale rally, stagger strike).
   - **Morale (`COMBAT-03`)**: brief overhead burst/aura cue on squad `Awaken` and `Collapse` triggers.

### Completion Criteria

- EditMode tests verify pool cap/recycling, cooldown gating, and distance culling rules.
- PlayMode verification in `Game` confirms zero leaked pooled instances across stage transitions and stable frame times during multi-wave combat.
- `Docs/codemap/ArtAssets.md` (and `Docs/codemap/` VFX map) updated; manual visual verification walkthrough provided.
