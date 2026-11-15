# BUG-wall-archer-los | 2026-11-15 | Combat - Wall-Top Archer Range & Hold Retaliation

Last Updated: 2026-11-15 16:28:26 (KST)

> Parent: [history/README](../README.md) | Design: [Combat](../../design/Combat.md) | Branch: `fix/bug-wall-archer-los` | Owner: Hwang Seong-min

## Report (2026-11-14, manual play)

Wall-top `Hold` archers failed to retaliate against enemy archers below. Suspected causes at the time: `HasClearShot` wall-edge occlusion or the 3D range boundary.

## Reproduction (temporary PlayMode diagnostics, removed after use)

Measured in `SiegePrototype` and `Game` with the real archer squad on a 1, 2 and 3 tier outer wall against the real enemy roster (ally and enemy archer range both 11):

- **Wall edge is not the cause.** The shooter's footing exemption works on every tier. Where a wall edge hides a defender, the enemy below cannot hit that defender either; on a 3-tier wall the defenders shot freely and took no damage.
- **Range boundary is the cause.** Enemy archers walk in only until the *front-most* defender is in their range. Rear-rank defenders 1–2m behind were out of range by the same margin and stood idle with no target while the front rank was being shot (2 of 4 archers idle on tiers 2 and 3). They have no walking path to the attackers, so `Hold` retaliation could not help.
- **Arrows could fall short.** Range is measured to the nearest point of the target's body, but the arrow aims at the body center and flew exactly `AttackRange`. At 9m height and 10.95m measured range the aimed point is about 11.8m away and the arrow vanished before reaching the target.
- Also observed, left unchanged: a living enemy standing in the line blocks a ranged unit's shot at the enemy behind it (about 13% of shooter/target pairs in a full battle); the unit simply shoots the nearer enemy. Recorded in [OpenQuestions](../../design/OpenQuestions.md) for the AI pass.

## Decision (delegated to the agent by the user, 2026-11-15; open to revision after playtest)

| Item | Choice | Reason |
|---|---|---|
| High-ground ranged bonus | On: range ×1.25 from a 1.5m drop; no damage bonus | Lets every rank on a wall top answer attackers that stop at the edge of their own range; damage advantage already exists (enemy upward shots deal 50%) |
| `HasClearShot` wall-top exception vs stepping to the edge | Neither; line of sight unchanged | Reproduction showed no edge problem, and the edge protects defenders; moving off post conflicts with `Hold` |
| `Hold` with no shot and no path | Stay on post, re-check each decision interval | Matches "hold this spot"; leaving a wall to chase is never useful when no path exists |

## Implemented Changes

- `Combat/RangeRules` (new, pure): `HighGroundMultiplier(drop, minimumDrop, bonus)`, `FlightDistance(range, aimDistance)`.
- `CombatProfile`: `highGroundMinimumDrop` 1.5, `highGroundRangeBonus` 0.25 and `Create` parameters. Existing `.asset` files were not edited; the new fields use code defaults, so the rule applies to every ranged unit (enemy archers standing on a wall top get it too; flyers do not).
- `CombatPhysics`: `AttackRange(attacker, target)` and `MaximumAttackRange(attacker)`; `IsInRange` uses the per-target range. Melee and airborne attackers keep the base range.
- `CharacterActor.ResolveImpact`: arrow flight distance from `RangeRules.FlightDistance` (one call site).
- `UnitCombatAI.FindTargetInReach`: bounds pre-filter uses `MaximumAttackRange` (one line). No other AI change; enemy archer approach distance is unchanged.

`CharacterActor` and `UnitCombatAI` are shared combat core files ([Team](../../plan/Team.md)); the edits are limited to the two call sites above and should be reviewed with the enemy-assault owner before the develop merge.

## Verification

- New PlayMode `WallTopArcherPlayTests` (synthetic 9m wall, range 11): `ArrowReachesEveryTargetThatPassedTheRangeCheck_WhenShootingSteeplyDown`, `HighGround_ExtendsRangeDownwardOnly_AndTheArrowLands`, `HoldArcherOnWallTop_FindsAndShootsEnemyInsideHighGroundRange_WithoutLeavingPost`, `HoldArcherOnWallTop_StaysOnPost_WhenEnemyIsBeyondHighGroundRange`. The first three failed against the previous `CharacterActor` and pass with the fix.
- New EditMode `RangeRulesTests` (12 cases).
- Full `unity-verify-local.ps1 -Mode all -SkipDotnet`: compile passed, EditMode **409/409**, PlayMode **204/204**. Dotnet precheck skipped (stale generated `.csproj`).

Not verified: manual play in `Game.unity` and standalone build. Battle balance is expected to shift toward wall-top archers; values were not tuned.

## Manual check

1. Open `Assets/_Project/Scenes/Game.unity`, press Play, and in preparation press `P` (squad placement view), select the archer squad (3) and left-click an outer north wall top; wait until they stand there.
2. Hold B for 1 second to start the battle and watch from the wall top or the courtyard.
3. Expected: when enemy archers stop and start shooting at the wall, every archer of the squad (front and rear rank) turns and shoots back without leaving the wall; arrows reach the enemy archers instead of vanishing short. Archers still do not shoot enemies hidden directly behind another wall piece.
4. Compare on a 2 or 3 tier wall section (build extra tiers at the construction station first).
5. Reset: stop Play mode. Nothing is saved to the scene or assets.

The user pre-authorized Done, archive, and commit/push of the task branch on 2026-11-15 once the full regression passed, without a manual playtest; the manual check above remains open and develop merge remains with the user.
