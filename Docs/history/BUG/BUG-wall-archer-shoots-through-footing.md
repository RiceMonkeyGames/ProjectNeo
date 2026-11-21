# BUG-wall-archer-shoots-through-footing | 2026-11-22 | Combat - Wall-Top Archers Shoot Through Their Own Wall Piece

> Parent: [history/README](../README.md) | Design: [Combat](../../design/Combat.md) | Branch: `fix/bug-wall-archer-shoots-through-footing` | Owner: youngho

## Report (2026-11-21, COMBAT-09 manual play)

Allied archers on the wall right above the gate hit gate breakers by shooting straight down through the wall. Cause: line of sight (`CombatPhysics.HasClearShot`) and `CombatProjectile` ignored the shooter's whole footing collider (`CombatPhysics.GroundUnder`). The exemption came from Q-22 (2026-11-12) as an implementation choice and BUG-wall-archer-los kept it; it was never a user design decision.

## Decision (user, 2026-11-21)

- First choice: block the footing fully. Full verification showed large dead zones under tall walls (about 1.5 times the wall height in front of a mid-wall archer) and three BUG-wall-archer-los high-ground tests failed.
- Final choice: edge grazing. A shot may pass through the shooter's footing only down to `CombatProfile.footingClearance` below the feet. The agent set 0.3m so that a mid-wall archer on a 3m gate cannot hit gate breakers within about 1m of the gate (1m would still let the upper body be hit).
- Near-vertical shots at the wall base stay blocked; a dedicated feature is registered as [SIEGE-12](../../backlog/SIEGE-12.md).

## Changes

- `CombatProfile`: new serialized `footingClearance` (default 0.3m, `Create(..., footingClearance)`). Existing assets load the default without edits.
- `CombatPhysics`: `FootingFloor(attacker)` (feet minus clearance); `IsClear` skips the footing only when the line exits it at or above that height (`GrazesFooting`, reverse raycast from the target end). `HasClearShotFrom` stays strict (no footing exemption).
- `CombatProjectile`: footing ignored only while the arrow centre height at contact is at or above the footing floor (sweep hits use the centre height, which also fixes initial-overlap hits reporting `point = 0`).
- Tests: `WallTopArcherPlayTests.WallArcher_CannotShootThroughItsOwnWallPiece_AtEnemyHuggingTheWallBase` (new); `ArrowReachesEveryTargetThatPassedTheRangeCheck_WhenShootingSteeplyDown` moves archer and target 0.9m toward the outer edge. `SiegePrototypePlayTests.WallArchers_ShootEnemyRightBelowTheWall` (enemy 2m out) is unchanged and still passes as an edge-graze shot.
- Docs: [Combat](../../design/Combat.md) Line of sight, [Decisions-Resolved](../../plan/Decisions-Resolved.md), [CombatAndSquads codemap](../../codemap/CombatAndSquads.md).

## Verification

- Code rules check passed. Compile passed; EditMode 665/665; PlayMode 295/295.
- `Ranged.asset` and `EnemyRanged.asset` load `FootingClearance` = 0.3 (script eval, no asset changes).
- Manual play: confirmed by the user on 2026-11-22.
- Standalone build: not run.
- Shared combat core (`CombatPhysics`, `CombatProjectile`) changed: teammate review before `develop` merge.
