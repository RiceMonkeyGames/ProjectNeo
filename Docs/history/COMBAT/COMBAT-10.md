# COMBAT-10: Wall-Top Cover Fire (Crouch to Reload)

Last Updated: 2026-11-21 21:08:03 (KST)

Branch: `feature/combat-10`. Owner: youngho. Rules: [Combat](../../design/Combat.md) (Wall-top cover fire), [WallsAndSiege](../../design/WallsAndSiege.md) (Battlements). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Builds on [COMBAT-09](COMBAT-09.md) (aiming, arc and spread).

## Decisions (user, 2026-11-21)

- Parapet (sill) height 0.8m → 1.1m for Lv1 and Lv2, so a crouched 2m unit (1.1m tall) is fully covered while a standing one shows its upper body. The sill is also the crenel sill. Ladders follow the new height through `SillHeightAt`.
- Only AI-controlled allied ranged units standing behind their own battlement crouch. The player, enemies and ground archers do not.
- Crouch for most of the reload. The unit rises `coverRiseLead` (0.5s) before its next shot can fire, then aims and releases standing.
- Crenel slots: the front rank of a ranged squad snaps to the crenels nearest the front line; leftover archers form the rear ranks.
- Attackers unchanged: enemy archers hold a drawn bow while the target is hidden and fire once the target is exposed.
- Follow-up after playtest: archer attack speed 1.0 → 0.4 attacks per second for `allied_ranged`, `hostile_ranged` and `hero_archer`. Reason: the crouch was not visible (only about 0.2s crouched per 1s cycle, given the 0.3s `ImpactDelay`) and archers were too strong. Damage and the Marksman boss are unchanged.

## Changes

- `CombatProfile`: new `crouchHeightRatio` (0.55), `coverRiseLead` (0.5), `coverSearchRadius` (1.2); matching `Create(...)` parameters.
- `CharacterActor`: `IsCrouched`, `CurrentHeight`, `CooldownRemaining`, `SetCrouched`. Crouch resizes the `CapsuleCollider` with feet fixed; `AimPoint` uses `CurrentHeight`. Crouch resets on death, on disable and at ladder start.
- `CombatPhysics.IsBehindOwnCover`: overlap check for a `ShotCover` that allied shots pass through. Upper-body aim uses the target's `CurrentHeight`.
- `CoverCrouchRules` (new, pure): `ShouldCrouch` decision.
- `UnitCombatAI.UpdateCoverCrouch`: applies the crouch rule each AI update.
- `CharacterModelAnimator`: plays the `Crouch` state while the actor is crouched.
- `BattlementRules.CrenelCenters` (new, pure): crenel centre positions along an edge.
- `CastleBattlements.CollectCrenelStandPoints`: stand points 0.45m inside the battlement at each crenel. Skips gun ports, pieces under construction and end crenels at corners.
- `CrenelSlotRules.Arrange` (new, pure): places the front rank in the crenels and the leftovers in rear ranks. Used by `SquadCommandCenter.PlanSlots` through the `CrenelPoints` delegate and `IsRanged(squad)`. Wired in `GamePrepPlacement`.
- `BattlementSettings.parapetHeight` 0.8 → 1.1 in the Game asset `Data/GameFlow/Build/Battlements.asset` (set through the Editor API).
- Editor: `ArcherRangeMigration.ArcherAttackSpeed` (attack speed 0.4 for the three archer assets); `UnitBattleSetup` and `TavernSetup` create the same values for fresh data.
- Tests: new EditMode `Assets/Tests/EditMode/CoverFireRulesTests.cs`.

## Verification

- Code rules check (`code-rules-check.ps1`): PASSED, 0 violations.
- EditMode: 665/665 passed.
- PlayMode: 294/294 passed (full suite, about 187s), run after the attack-speed change. Ladder filter 17/17 after the QA fix.
- QA review: PASS. One Medium finding (crouch not reset on death or ladder start) was fixed. GC allocation in `CrenelSlotRules.Arrange` was reduced.
- Manual play: done by the user before finishing. In-game checks: crouch at Lv1 and Lv2, crenel slots, ladders.
- Standalone build: not run.

## Follow-ups

- Placeholder capsule visuals only shrink the hitbox; there is no crouch pose on the placeholder body.
- `unity-verify.ps1 -Mode play` poll window is shorter than the roughly 187s full PlayMode suite, so the unit-battle wrapper can time out. Fix the wrapper timeout (tooling) or run the suite with a longer window.
- Shared combat core files were touched (`CharacterActor`, `CombatPhysics`, `UnitCombatAI`, `CombatProfile`). Coordinate with the teammate on the enemy assault and combat execution side before merging.
