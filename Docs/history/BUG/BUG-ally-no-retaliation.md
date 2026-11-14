# BUG-ally-no-retaliation | 2026-11-14 | Combat - Retaliation and Target Switching

Last Updated: 2026-11-14 11:56:23 (KST)

> Parent: [history/README](../README.md) | Design: [Combat](../../design/Combat.md) | Branch: `fix/bug-ally-no-retaliation`

## Problem & Root Cause

- (1) `Hold` units ignored attackers outside `HoldChaseRadius` (`HoldEngageRadius` 6m / ranged `range + 6m`) even while taking hits.
- (2) `Engage` units locked onto a fleeing target (such as the player) until death or unreachability, ignoring nearby attackers.
- `CharacterActor.HitReceived` carried no attacker reference, and `UnitCombatAI` had no hit-reaction or proximity re-evaluation during chase.

## Implemented Changes

- [CharacterActor.cs](../../../Assets/_Project/Scripts/Runtime/Combat/CharacterActor.cs) & [CombatProjectile.cs](../../../Assets/_Project/Scripts/Runtime/Combat/CombatProjectile.cs): Added `HitReceivedFrom` (`Action<CharacterActor>`) and optional `attacker` parameter on `ReceiveDamage(float, CharacterActor = null)`, passing `this` on melee impact and `owner` on projectile impact.
- [UnitAISettings.cs](../../../Assets/_Project/Scripts/Runtime/Combat/UnitAISettings.cs): Added `HoldRetaliationRadius` (default `15f`) and `TargetSwitchCooldown` (default `1.5f`).
- [UnitCombatAI.cs](../../../Assets/_Project/Scripts/Runtime/Combat/UnitCombatAI.cs):
  - Subscribed to `HitReceivedFrom`; triggers retaliation when not suppressed (`SetRetaliationSuppressed`), not `Move`, and not `IsOnLadder`.
  - Enforces `TargetSwitchCooldown` to prevent flip-flopping; when already striking a target in range (`CanStrikeFromHere`), ignores ranged hits from other enemies and only allows melee hits (`AttackKind.Melee`) to switch.
  - During `Engage` chase (`!CanStrikeFromHere(Target)`), re-evaluates closer/immediately-strikeable reachable enemies.
  - During `Hold`, pursues out-of-radius attackers only within `HoldRetaliationRadius` (checked against `HoldPost` NavMesh path length) and returns to `HoldPost` once cleared or out of leash.
- [SiegeAttackerBrain.cs](../../../Assets/_Project/Scripts/Runtime/Siege/SiegeAttackerBrain.cs): Suppresses retaliation during active siege tasks (`ToGate`, `ToCannon`, `ToWall`, `Ladder`, `Retreating`) and clears suppression in `Assault` / `Archer` / post-climb `Engage`.
- [UnitCombatPlayTests.cs](../../../Assets/Tests/PlayMode/UnitCombatPlayTests.cs): Added 5 PlayMode regression tests covering Hold retaliation + return, leash limit, Engage melee retargeting while chasing, in-range ranged-hit lock, and Move/suppression exceptions.

## Verification Scope

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode all`:
  - `COMPILE=PASSED`
  - `EDITMODE=PASSED` (`Total=358 Passed=358 Failed=0 Skipped=0 Duration=6.83s`)
  - `PLAYMODE=PASSED` (`Total=180 Passed=180 Failed=0 Skipped=0 Duration=101.77s`, including `UnitCombatPlayTests` 17/17)
- Manual playtest in `Game.unity` and standalone build: pending user confirmation.
