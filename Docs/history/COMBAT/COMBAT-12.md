# COMBAT-12 | 2026-11-22 | Combat - Ranged Engage Occlusion Fix & Melee Engagement Spread

Branch: `feature/combat-12`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim COMBAT-12`). Implementation commit `07f1556`. P1 task from [Q-60](../Q/Q-60.md) (AI improvement pass, Option A). Design record: [Combat](../../design/Combat.md) (COMBAT-12 details). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Code map: [CombatAndSquads](../../codemap/CombatAndSquads.md).

## Decisions (user, 2026-11-22)

| Item | Choice |
|---|---|
| Ranged shot blocked by a living enemy (`Engage`) | Switch to the blocker when it is in range with a clear shot; otherwise keep the firing distance and wait. Structure blocking keeps the old walk-closer behavior |
| Melee spread | Nearest free slot on a ring around the target; off-NavMesh slots skipped; centre fallback when none is usable |
| Ring full | Extra melee attackers wait on an outer ring |
| Who | Every `UnitCombatAI` unit, allies and enemies |

Agent defaults (temporary data in `UnitAISettings`, open to revision after playtest): inner ring at both body radii plus a 0.15m gap, pulled in to 80% of the attack range; outer ring 1m further out; slots every 0.9m (6 inner slots for two 0.4m bodies), at most 16; a slot must be on the NavMesh within 0.5m and within 1m of the target's height; up to 3 path checks per ring per decision tick; also used by `Hold` chases (with the Hold descent rule); a melee unit walking to its slot keeps going when the target is already in reach and strikes on the way. `UnitAISettings.meleeSlots` switches slots off.

## Implemented scope

- Pure rules: `ShotBlock`, `RangedBlockAction`, `RangedOcclusionRules` (`Decide`, `Combine`), `MeleeRingRules` (ring radii, slot count, world-fixed slot offsets, nearest-first order), `MeleeSlotBoard<T>` (one slot per unit and per slot, stale claims dropped lazily).
- `CombatPhysics.ClassifyShot` (body-centre and upper-body lines; `IsClear` now shares the per-line `ClassifyLine`).
- `UnitCombatAI`: `HandleBlockedShot` in `UpdateEngage` (re-evaluated each decision interval, `BlockAction` exposed), `ApplyApproachPath` / `TryPathToSlot` / `TrySlot` / `IsWalkingToMeleeSlot` / `TryGetMeleeSlot` for `Engage` and `Hold` chase paths; claims released on new directive, death and disable.
- `UnitAISettings`: `meleeSlots`, `meleeSlotGap`, `meleeSlotSpacing`, `meleeOuterRingOffset`, `meleeSlotSampleDistance`, `meleeSlotLevelTolerance` with field defaults (no asset edits); `Create(..., meleeSlots)`.

## Verification

- EditMode `EngagementRulesTests` 10/10; PlayMode `EngagementPlayTests` 3/3 (an archer whose shot to a 14m target is blocked by an enemy stepping in at 6m switches to the blocker within 1s, hits it and moves less than 0.5m; 4 melee attackers from one side take 4 distinct inner slots, all in reach and at least 0.5m apart; 8 attackers fill the 6 inner slots and put 2 on the outer ring).
- Full `unity-verify-local.ps1 -Mode all`: EditMode **713/713**, PlayMode **313/313** (174.9s). Before the commit: EditMode **713/713**, `EngagementPlayTests` **3/3**. `code-rules-check.ps1`: 0 violations, no serialized asset changes. Dotnet precheck skipped.
- Defect found and fixed during implementation: melee units stopped as soon as the target was in reach (`isStopped = canAttack`), so they bunched up before reaching their slots; a unit walking to its slot now keeps going (stopping distance 0) and still strikes on the way.
- User manual check (2026-11-22): confirmed.

Notes: this changes the shared combat core (`CombatPhysics`, `UnitCombatAI`) and enemy assault behavior (enemy melee spread too); tell the teammate (enemy assault owner) before merging.

Not verified: standalone build; performance with large melee crowds (path checks are capped at 3 per ring per decision tick).
