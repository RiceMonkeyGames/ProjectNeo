# BUG-wall-archer-leaves-wall | 2026-11-20 | Combat - Hold Units Stay on the Wall Top

Last Updated: 2026-11-20 11:52:10 (KST)

> Parent: [history/README](../README.md) | Design: [Combat](../../design/Combat.md) | Branch: `fix/bug-wall-archer-leaves-wall` | Owner: youngho

## Report (2026-11-20, user playtest)

A wall section is destroyed. An ally archer holds an intact wall next to it while an enemy stands right below the wall, out of the archer's shot. The archer walked down the stairs, out through the breach and attacked from the ground.

## Cause

`Hold` chased any enemy inside its chase radius (ranged: attack range + `HoldRangedChaseExtra`; melee: `HoldEngageRadius`) as long as a complete NavMesh path existed. The earlier rule ([BUG-wall-archer-los](BUG-wall-archer-los.md)) only kept the unit on the wall when there was no shot **and no path**; a breach plus stairs creates a path, so the chase started.

## Decision (user, 2026-11-20)

| Order | Behavior |
|---|---|
| `Hold` (and the Hold after `Move`) | Melee and ranged units do not follow a chase or retaliation path that drops more than `HoldMaxDescent` (1.5m) below the post. They stay on the wall and keep fighting what reaches the wall top or is in range. |
| `Engage` (Attack) | Unchanged: may leave the wall to chase. |
| `Move` | Unchanged: ignores enemies. |

The user chose to apply the rule to melee too, replacing the earlier "Hold melee can descend stairs to engage breaches" note.

## Implemented Changes

- `Combat/HoldDescentRule` (new, pure): `StaysAbove(corners, floorY)`.
- `UnitAISettings`: `holdMaxDescent` 1.5 (0 disables) and a `Create` parameter. Existing `.asset` files were not edited; the field uses the code default.
- `UnitCombatAI`: new `CanReachInHold` (`CanReach` plus the descent rule against `HoldPost.y`) used in the four Hold path checks: `UpdateHold` path refresh, `FindTargetNearPost`, and the Hold default/retaliation checks in `OnHitReceived`. Engage paths are unchanged.

`UnitCombatAI` is shared combat core; review with the enemy-assault owner before the develop merge. `SiegeAttackerBrain` also parks enemies in `Hold` (waiting at or raising a ladder, gate/cannon/wall strikes, archer standoff). Those posts are on the ground outside the castle, so only a chase down into ground more than 1.5m lower is affected; an enemy holding on a wall top would no longer chase down into the castle while in `Hold`. Ask the enemy-assault owner to confirm this side effect.

## Verification

- EditMode `HoldDescentRuleTests` (4 cases).
- PlayMode `WallTopArcherPlayTests`: `HoldArcherOnWallTop_DoesNotLeaveTheWall_WhenAPathDownExists` (9m wall with a ramp to the ground; enemy inside the chase radius but out of range) and `EngageArcherOnWallTop_StillLeavesTheWall_ToChaseAnEnemyBelow` (same terrain; proves the path exists and Engage is unchanged).
- Results (2026-11-20, `Tools/Agent/unity-verify.ps1 -Mode all`): compile passed; EditMode 514/514 passed; PlayMode 245/245 passed. Manual play pending.
