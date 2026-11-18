# PREP-08 | 2026-11-19 | Prep - Return Fallen Hero Gear to Stash

Last Updated: 2026-11-19 01:11:10 (KST)

> Purpose: Implementation and verification log. | Parent: [history/README](../README.md) | Related: [Q-31](../Q/Q-31.md), [Combat](../../design/Combat.md#units), [FlowRunEconomy](../../codemap/FlowRunEconomy.md)

- Assignee: 신용준. Branch `feature/prep-08` from develop `cfc7aa6`.
- Rule (Q-31): equipment is never lost on hero permadeath; it returns to the run stash with its upgrade level.

## Changes

- `RunState.Apply`: when a hero dies, `ReturnEquipment` unequips every item into `Inventory` (stat modifiers removed through `Character.TryUnequip`) before the hero moves to `FallenHeroes`.
- `RunState.Restore` (`RunStateSnapshot.cs`): fallen heroes from older saves that still list equipment get the same treatment, so the gear appears in the stash and the next save records it there. No save format change.
- No UI change. The battle result screen does not mention returned gear.

## Verification

- `Tools/Agent/unity-verify.ps1 -Mode all`: dotnet pre-check passed, Unity compile passed, EditMode **492/492**, PlayMode **238/238**.
- New/changed EditMode tests: `RunStateTests.죽은_영웅은_영구_사망_목록으로_가고_부대장에서_빠지며_장비는_보관함으로_돌아온다` (gear in stash, re-equippable by another hero), `RunSaveTests.OldSaveWithEquippedFallenHero_RestoresTheGearToTheStash` (old save migration, upgrade level kept).
- Manual play: confirmed by the user on 2026-11-19 (gear of a fallen hero back in the stash and re-equippable). Standalone build: not verified.
