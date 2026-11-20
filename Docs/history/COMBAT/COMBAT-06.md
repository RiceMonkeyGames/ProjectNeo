# COMBAT-06 | 2026-11-20 | Combat - Minimal Hero/Active Skill Execution

Last Updated: 2026-11-21 01:07:42 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [Combat](../../design/Combat.md#active-skills), [CombatAndSquads codemap](../../codemap/CombatAndSquads.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md), [Backlog-Done](../../archive/Backlog-Done.md)

Branch: `feature/combat-06`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim COMBAT-06`).

## Background

Q-62~Q-65 and Q-72/Q-73 (2026-11-19) fixed the skill rules: up to 3 slots (player 3, hero by grade), player casts directly, hero AI casts with a per-hero Auto toggle, cooldown only with every skill ready at battle start, data-driven target modes and effect families, player skills from Combat-tree perks, 3 fixed skills per hero. META-09 left the skill perks and the equip window to this task. `SkillDefinition` existed as an id/name shell only.

## Decisions

- User, 2026-11-20: player skill keys `Z` / `X` / `C`; manual hero cast = selected squad (`F1`-`F4`) + `Alt` + slot key; equip and per-hero Auto in the preparation equipment window (`I`); about 6 shared sample skills, one per effect family. Recorded in [Combat](../../design/Combat.md#active-skills) and [Decisions-Resolved](../../plan/Decisions-Resolved.md).
- Agent defaults (open to change): `Alt` + key is obeyed even while Auto is on; player `OwnSquad` skills fall back to allies within the skill radius (the player has no squad); a failed cast keeps the cooldown; AimPoint aims are pulled back to range; a recast buff refreshes its time instead of stacking; skill perk prerequisites (Whirlwind none, Shield Bash after Sharpened Blade, War Cry after Toughness, Healing Light after Iron Skin); skill UI text is Korean (UI-04).
- 2026-11-20, user: steps 1-5 merged to develop before the manual check and the step 6 work; the manual check is tracked as `QA-combat-06-skills`.
- Step 6 agent defaults (open to change): the equipment window switches between a gear view and a skill view with a top row on the right list (the window has room for about 6 rows per list); a learned skill goes into the first empty slot; an emptied slot is refilled from the remaining learned skills at battle start; old saves keep Auto on and fill the slots in learned order.

## Changes (steps 1-5, merged to develop 2026-11-20)

- Skills module (`Runtime/Skills/`, `ProjectNeo.Skills`): `SkillDefinition` (moved from Characters with its GUID), `SkillTargetMode`, `SkillEffectKind`, `SkillEffect`, `SkillEffectRules`, `SkillSlotRules`, `SkillSlots`, `SkillLoadout`, `SkillStatusTimers`, `SkillStatusEffects`, `SkillTargeting`, `SkillAim`, `SkillAimFinder`, `SkillCaster`, `SkillCastInfo`, `SkillCastResult`, `ISkillContext`, `GameSkillContext`, `PlaceholderSkillView`, `HeroSkillAI`, `SkillAutoRules`, `SkillAutoIntent`, `SkillAutoSettings`.
- Shared code (additive only): `CharacterActor.ApplyStagger`; `PlayerInputController.IsMovementSuspended`; `SquadCommandInput.View`; `GameRunLink.UnitOf` / `ActorsInRun`; `GameBattleSettings` skill keys, aim distance/radius and `SkillAuto`; `GameBattleController.Awake` adds `GameSkills` and `SkillHud`.
- Meta/Run: `PerkDefinition.GrantedSkill`, `PerkLoadout.LearnedSkills`, `RunStateSkills.cs` (`PlayerSkillLoadout`, `EquippedPlayerSkills`).
- UI: `SkillHud` bottom bar (code-built, restyled later by UI-08); `GamePerkWindow` rows drop to 48 px when a tree has more than 6 perks.
- Data via Editor APIs: `Editor/SkillSetup` (menu `ProjectNeo/Setup/Create Skills (COMBAT-06)`) creates 6 skills, `SkillAuto.asset` (linked from `Battle.asset`), hero fixed skills, then runs `PerkTreeSetup` which adds 4 Combat skill perks and refreshes `RunContent.asset`.
- Fix: `PerkTreeSetup.SaveOver` blanked every overwritten asset's `m_Name`; both setups now keep the file name.
- Tests: EditMode `SkillRulesTests` (15), `SkillDataTests` (5); PlayMode `SkillCasterPlayTests` (10), `HeroSkillAIPlayTests` (5), `GameSkillPlayTests` (4); `GamePerkWindowPlayTests` row count 15 -> 19.

## Changes (step 6, merged to develop 2026-11-21)

- Run: `RunHero.SkillAuto` (default on), `RunState.TryEquipPlayerSkill` / `UnequipPlayerSkill`; save fields `RunSnapshot.playerSkills` (slot order) and `HeroRecord.skillManual`; restore sets the slot ids only (perks are set up later by the scene; unlearned ids drop on the next refresh).
- Flow: `GameEquipment` skill view (`ToggleView`, `TryEquipSkill`, `TryUnequipSkill`, `ToggleHeroAuto`, `EquippedRowTexts` / `InventoryRowTexts`); slot keys from `GameSkills.KeyOf` / `ModifierKey`; `GameBattleController` sets `GameSkills.HeroAuto = hero => hero.SkillAuto`.
- Tests: EditMode `RunPerkTests.SkillLoadout_EquipsLearnedSkills_AndSurvivesTheRunSave`, `RunSaveTests.CaptureThenRestore_KeepsEachHerosSkillAuto_AndOldSavesStayAuto`; PlayMode `GameSkillPlayTests` equipment skill view test; `GameEquipmentPlayTests` inventory row count 1 -> 2 (view row).

## Verification

- `Tools/Agent/unity-verify-local.ps1 -Mode all` after the step 5 data run: compile passed, EditMode 602/602, PlayMode 281/281 (dotnet pre-check skipped on the last run because the Unity-generated csproj lagged behind new files; it passed on the earlier full runs).
- Step 6 (2026-11-21): `unity-verify-local.ps1 -Mode all -SkipDotnet` then `-Mode play -SkipDotnet` after the row-count fix: compile passed, EditMode 604/604, PlayMode 282/282.
- Manual play (2026-11-21, user, `Title.unity` -> new run -> `Game.unity`): passed the full `QA-combat-06-skills` checklist (Combat perk column and skill perk, `Z` cast with red disc and cooldown, empty-slot message, captain auto-cast and `F1` + `Alt`+`Z`, equipment window skill view equip/unequip, Auto off, save and continue, Korean text).
- Standalone build: not verified.

## Outcome

Done 2026-11-21 on user confirmation; `QA-combat-06-skills` closed with it. Archived in [Backlog-Done](../../archive/Backlog-Done.md).
