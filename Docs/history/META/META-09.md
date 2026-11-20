# META-09 | 2026-11-20 | Meta - Perk Trees & Allocation UI

Last Updated: 2026-11-20 14:31:47 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [META-02](META-02.md), [Combat](../../design/Combat.md#active-skills), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

Branch: `feature/meta-09`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim META-09`).

## Background

META-02 applied perk effects in the run and offered only an editor-only F11 shortcut with seven sample perks. Q-72 asks for three perk trees (Combat, Economy, Unit Enhancement), 1 perk point per Commander level, allocation only in preparation and on the reward screen (Q-06), allocations reset at run end.

## Decisions (user, 2026-11-20)

- Perk contents as proposed: 15 perks, 5 per tree, 1 point each, with prerequisites (see [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression)); the META-02 sample perks and the F11 shortcut are removed.
- The window is built in code at runtime (no `Game.unity` edit, so the teammate's FLOW-03 scene work does not collide); K opens it in preparation and a `Perks (K)` button opens it on the reward screen; trees side by side; take a perk with two clicks (same as the unlock screen).
- Skill-granting perks and the 3-skill equip window move to COMBAT-06 (noted on its Backlog row).
- Agent defaults kept: a perk needs all its prerequisites; a perk taken on the reward screen saves the run again so Continue keeps it; Esc or the Close button closes the window.

## Changes

- Meta: `PerkDefinition.Prerequisites` and `Description` (+ `Create` parameters), `PerkAllocateCheck`, `PerkLoadout.Check` / `MissingPrerequisites`, `PerkTreeDefinition`, `PerkTreeCatalog` (`Validate`).
- Run: `RunState.CheckPerk`; `RunConfig.PerkTrees` (+ `WithPerkTrees`); pure `PerkWindowModel` (click twice, entry texts, `Describe` builds the effect text).
- Flow/UI: `GamePerkWindow` (runtime-built canvas, three columns, reward-screen open button, K toggle) added by `GameBattleController` when RunStart has perk trees; `GameBattleController.PerkWindow`; preparation hint shows `K perks`.
- Editor/data: `PerkTreeSetup` (`ProjectNeo/Setup/Create Perk Trees (META-09)`) created the 15 perks, two morale effects, three trees and `PerkTrees.asset` in `Data/GameFlow/Perks/`, linked RunStart and refreshed `RunContent.asset` (15 perks); the seven META-02 sample perk assets were deleted.
- Removed: `GameDevPerkShortcut` (F11) and `Editor/PerkSampleSetup`.
- Tests: new EditMode `PerkTreeTests` (6) and PlayMode `GamePerkWindowPlayTests` (2).
- Docs: the META-02 Run/Flow codemap row that was lost when META-02 was documented is restored next to the new META-09 row.

## Not in Scope / Follow-ups

- Skill perks and the skill equip window: COMBAT-06.
- Visual polish of the window (icons, tree lines): UI pass (Q-11).

## Verification

- 2026-11-20, `Tools/Agent/unity-verify-local.ps1 -Mode all`: `DOTNET_PRECHECK=PASSED`, `COMPILE=PASSED`, `EDITMODE=PASSED (Total=525 Passed=525 Failed=0 Skipped=0)`; that first PlayMode run after the editor restart stopped reporting (`PLAYMODE=TIMEOUT`, status stuck on `running` with the editor out of Play Mode). Re-run `-Mode play -SkipDotnet`: `PLAYMODE=PASSED (Total=249 Passed=249 Failed=0 Skipped=0)`.
- Manual check (user, 2026-11-20): with perk points set in the meta save, K opened the window in preparation with the three trees readable; two clicks took Sharpened Blade, Champion was refused with its missing prerequisites, Esc/Close closed it, the reward screen showed `Perks (K)`, and a perk taken there stayed taken after Continue.
- Verified scope: tree rules, window rules and texts (EditMode); window open/take/close in preparation, reward-screen button and battle lock in the Game scene (PlayMode). Layout and readability are covered by the manual check. Standalone build not run.
