# ART-05 | 2026-11-21 | Art - Arrow Model & Arrows Stuck on Hit

## Scope and decisions

- Branch: `feature/art-05`; SeongminHwang. Follows [COMBAT-09](../COMBAT/COMBAT-09.md) (ballistic arrows).
- User decisions (2026-11-21, at start): model `PolygonFantasyKingdom` `SM_Prop_Arrow_01_Optimized`; stuck arrows stay 30s; at most 100 stuck arrows in the scene, oldest removed first; arrows stuck in a character stay on the corpse until it disappears or the 30s run out; all arrows are cleared at battle end. Rules: [Combat](../../design/Combat.md), [Decisions-Resolved](../../plan/Decisions-Resolved.md).
- Implementation choices (reported to the user): arrows that hit the first-person player do not stick (they would block the view); the arrow model casts no shadow.

## Implementation

- `Runtime/Combat/StuckArrow`, `StuckArrowLedger`, `StuckArrowSettings`: see [CombatAndSquads codemap](../../codemap/CombatAndSquads.md).
- `CombatProjectile`: the only change is the destroy on hit, which now calls `StuckArrow.Stick` when the prefab has one. Kept to one line to limit the overlap with `BUG-wall-archer-shoots-through-footing` (youngho), which also edits this file.
- `Editor/ArrowModelSetup` (menu `ProjectNeo/Setup/Apply Arrow Model`, idempotent): replaces the `Visual` sphere of `Prefabs/UnitBattlePrototype/StraightProjectile.prefab` with an unlinked copy of the arrow model (colliders removed, URP material via `CharacterModelSetup.ToUrp`, now `internal`), offsets it so the arrowhead tip sits on the projectile point, and adds `StuckArrow` with `Data/UnitBattlePrototype/StuckArrow.asset`. `UnitBattleSetup.Create` calls it after creating the projectile.
- All ranged units (allied, enemy, hero archer, Marksman) share this prefab, so every arrow changes. Test-built projectile templates without `StuckArrow` keep the old disappear-on-hit behavior.

## Verification

- Automated: compile passed; EditMode 670/670 (new `StuckArrowLedgerTests`); PlayMode 298/298 (new `StuckArrowPlayTests`: wall stick and expiry, damage plus following the body and staying on the corpse, cap eviction, first-person player not stuck); `code-rules-check` passed.
- Manual: confirmed by the user in play (2026-11-21).
- Not verified: standalone build.
