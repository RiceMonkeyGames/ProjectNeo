# SIEGE-06 | 2026-11-14 | Siege & Walls - Gate Door-Only Destruction & Stone Arch Walkability

Last Updated: 2026-11-14 17:26:18 (KST)

## Summary

Split the main gate into an indestructible stone arch (`StoneArch`, `ConstructionGateArch.asset`, `ConstructionStone.mat`) and a breakable wooden door (`Door`, `ConstructionGate.mat`) so gate destruction opens only the ground passage while keeping the gate-top surface and upper stacked structures intact and walkable.

## Changes

- **Mesh & Prefab (`ConstructionGateArchMesh.cs`, `ConstructionPrototypeSetup.cs`, `MainGate.prefab`)**:
  - Generated flat-shaded `Assets/_Project/Art/Placeholder/ConstructionGateArch.asset` (`4.2m` wide × `2.46m` peak arch opening within the `6m × 3m × 3m` cell bounds).
  - Updated `MainGate.prefab` in place (preserving GUID) with `Visual/StoneArch` (`ConstructionStone.mat`), `Visual/Door` (`ConstructionGate.mat`), three indestructible arch `BoxCollider`s, and a dedicated `Door` `BoxCollider` wired to `PlacedStructure.SetDoorParts`.
- **Gate Breach & Repair (`PlacedStructure.cs`, `ConstructionWorld.cs`, `Health.cs`, `GameBuildEconomy.cs`)**:
  - `PlacedStructure.Collapse()` routes `BuildPieceKind.Gate` to `ConstructionWorld.BreachGate` instead of `Collapse`, disabling only `doorCollider` and `doorVisual`, invoking `PieceCollapsing`, and triggering a NavMesh rebuild without removing the gate from `Layout` or settling upper pieces.
  - Added `Health.ReviveFull()` and `ConstructionWorld.NotifyGateRepaired` so `GameBuildEconomy.RepairAll` repairs broken gates during preparation, re-enables the door collider/visual, and rebuilds NavMesh.
- **Outcome, Morale & AI Targeting (`GameBattleController.cs`, `SiegeDirector.cs`, `GameSquadMorale.cs`, `SiegeAttackerBrain.cs`)**:
  - Updated `GateDestroyed` and `GameSquadMorale.OnPieceCollapsing` to check `!piece.Health.IsDead` since breached gates remain in `world.Structures`.
  - Updated `SiegeAttackerBrain.FindGate`, `GateInReach`, and `Update` to ignore breached gates (`Health.IsDead`) so attackers stop striking the arch and switch to `Engage` through the opened passage.
- **Tests (`StackedConstructionPlayTests.cs`, `GameBuildEconomyPlayTests.cs`, `ConstructionPlayTests.cs`, `GameBattlePlayTests.cs`)**:
  - Updated stacked-gate and gate-breaker tests to assert upper walls stay at level 1, take no damage, `StoneArch` remains active, `Door` is disabled, and ground NavMesh opens through the gate.
  - Added `GameBuildEconomyPlayTests.모두_수리는_파괴된_성문_문짝도_복구한다` verifying `RepairAll` restores a breached gate.

## Verification

- **Compile**: `Tools/Agent/unity-verify.ps1 -Mode compile` passed (`0` errors).
- **EditMode**: `Tools/Agent/unity-verify.ps1 -Mode all` — **381/381** passed (`7.23s`).
- **PlayMode**: All `ConstructionPlayTests`, `StackedConstructionPlayTests`, `GameBattlePlayTests`, `GameBuildEconomyPlayTests`, `GameFlowPlayTests`, `GameMoralePlayTests`, and `WallBreakerPlayTests` passed.
  - Note: `SiegePrototypePlayTests.Ladder_AlliesOnTopPushItDown_AndClimberTakesFallDamage` failed (`오르던 적이 떨어져 피해를 받지 않았다. Expected: True But was: False`) both in the full run and in isolation; unrelated to gate/arch changes.
- **Manual Play & Standalone Build**: Not yet verified.

## Manual Verification Guide

1. Open `Assets/_Project/Scenes/Game.unity` and enter Play Mode.
2. In Preparation, verify the main gate visually shows a stone arch (`ConstructionStone`) surrounding a wooden door (`ConstructionGate`), and stack a wall/stairs above the gate if desired.
3. Hold `B` for 1 second to start battle.
4. Let enemies (or test damage) destroy the gate door.
   - **Expected**: Only the wooden door disappears; the stone arch and any walls/units atop the gate remain in place without falling; units walk both through the open arch below and across the top of the gate; enemies stop attacking the arch and enter the courtyard.
5. After winning/returning to Preparation, use `Repair All` at a facility/construction view.
   - **Expected**: Repair cost includes the broken gate; `RepairAll` restores the wooden door and closes the ground passage again.
