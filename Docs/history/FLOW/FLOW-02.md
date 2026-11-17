# FLOW-02 | 2026-11-16 | Flow/Run - Run Save & Continue From Title

Last Updated: 2026-11-17 13:49:46 (KST)

Branch: `feature/flow-02`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim FLOW-02`). Design record: [PrepEconomyMeta](../../design/PrepEconomyMeta.md) (Run Save & Continue).

## Background

META-01 saved meta progression only, so the Title screen had no `Continue` and a run was lost whenever the game closed. The user asked for `Continue` on 2026-11-16 and delegated the open Q-13 choices to the agent's recommendation and conventional practice, asking that the result be easy to change.

## Decisions (agent recommendation, 2026-11-16; open to revision)

| Topic | Choice | Reason |
|---|---|---|
| Save style | Automatic only, one run per slot | Standard for run-based games; no save-scumming menu to design |
| Save moments | Preparation start, battle start, reward granted (as next stage), quit during preparation | Battle start captures everything done in preparation; the reward save closes the "quit on the reward screen and win again" loophole |
| Mid-battle | Not saved; resume returns to that battle's preparation | Battle state (positions, projectiles, AI, morale effects) is not serializable today and is the conventional cut |
| Defeat | Deletes the run save | A lost run has nothing to continue |
| New Game over a saved run | Second confirming click, then the saved run is discarded | Matches the existing slot-delete confirmation |
| Unrestorable save | Set aside as `*.corrupt-…`, start a new run at stage 1 | Never silently deletes player data; never blocks play |
| Content ids | Definition ids in the file; castle pieces use the asset GUID recorded in a catalog asset | Two piece assets are both named `Gate`; a GUID survives renames |

Every save moment is a switch in `RunSavePolicy` (`GameSession.RunSavePolicy`), so timing changes are a one-line edit.

## Implemented scope

Layers, each replaceable alone:

1. **Record (plain data)** `Run/RunSnapshot` and its records: ids and numbers only, `JsonUtility`-friendly; adding a field needs no migration.
2. **Rules (pure C#)** `Run/RunStateSnapshot.cs` (partial `RunState`): `Capture(stageNumber, content)`, `TryValidate(...)`, `Restore(snapshot, content, options, out error)`; `RunRestoreOptions` passes configs and the scene characters. Restore reproduces unit ids, seed, battle count, shop/tavern stock, upgrade levels and squad morale; validation rejects unknown ids, duplicate unit ids, more squads/members than the squad config, and a player count other than one before anything is created.
3. **Id lookup** `Run/IRunContentLookup` and the `Run/RunContentCatalog` asset (`Data/GameFlow/RunContent.asset`, referenced by `Battle.asset`), filled by `Editor/RunSaveSetup` (menu `ProjectNeo/Setup/Refresh Run Save Content (FLOW-02)`), which scans every definition asset under `Assets/_Project`. Re-run it after adding or deleting definitions; an EditMode test fails if the catalog is stale.
4. **File** `Run/RunSaveStore`: `run_slot1.json`…`run_slot3.json` (+ `.bak`) beside the meta files, same temp-file swap, backup, quarantine and newer-version rules; `Inspect`, `Load`, `Save`, `Delete`, `Discard`. `RunSaveStatus` (Empty 0, Loaded 1, RestoredFromBackup 2, Unreadable 3), `RunSaveFile` (`version` 1).
5. **Policy** `Run/RunSavePolicy` + `RunSaveTrigger` (PreparationStart 0, BattleStart 1, RewardGranted 2, QuitDuringPreparation 3).
6. **Session/flow** `GameSession`: `RunSaves`, `RunSavePolicy`, `HasRunSave`, `ContinueRun`, `TakePendingRun`, `SaveRun`, `DeleteRunSave`, `DiscardRunSave`, `Quitting` event. `GameFlow.ResumeRun(stage)` and `RestartRunAtFirstStage()`; `RunProgress` accepts a stage number.
7. **Scene** `Flow/GameRunRestorer`: validates first, then rebuilds the castle (`ConstructionWorld.ClearForRestore` / `TryPlaceRestored`, health, weapons), keeps scene soldiers of the same definition, removes extras (`SquadCommandCenter.RemoveMember`), spawns missing soldiers and hired heroes (`GameUnitSpawner.SpawnRestoredSoldier` / `SpawnRestoredHero`), restores the run with the scene characters and binds every unit (`RunUnitBinding.ForState`). `GameRunLink`: `Restorer`, `Content`, `WasRestored`, `CaptureSnapshot(stageOffset)`. `GameBattleController.SaveRun(trigger)` at the save moments and delete on defeat; `GameRewardController` saves after granting; `GameBuildEconomy.OnRunReady` grants start funds before the first save and never to a restored run.
8. **Title** `UI/TitleScreen`: `ContinueGame`, `CanContinue`, `IsNewGameArmed`, `ContinueText`; slot rows show ` - Stage N in progress`; deleting a slot deletes its run save. `Title.unity` gained `Button_Continue` above `Button_New Game` through `GameFlowSetup.ApplyTitleSaveSlots` (Editor API only).

Small restore hooks added to existing rules: `ItemInstance.RestoreUpgrade`, `FacilityState.RestoreLevel`, `ItemShop.RestoreStock`, `CastleState.FromRecords`. `Game.unity` and prefabs were not edited.

Shared files touched outside the flow area (coordinate before further edits): `Walls/ConstructionWorld` (two restore methods, no behaviour change), `Squads/SquadCommandCenter` (`RemoveMember`), `Economy/ItemShop`, `Economy/ItemInstance`, `Facilities/FacilityState`. `CharacterActor` and `UnitCombatAI` were not touched.

## Verification

`unity-verify-local.ps1 -Mode all -SkipDotnet`: compile passed, EditMode **467/467** (3.62s), PlayMode **222/222** (147.7s). The dotnet precheck was skipped because the generated `.csproj` files do not list the new files until Unity regenerates them.

- New EditMode `RunSaveTests` (14): whole-run capture/restore equality (re-captured JSON identical), unit numbers continue and the stocked shop is not re-rolled, an unstocked run rolls the same shop after restore, scene character sharing and mismatch rejection, empty squads dropped without changing the run, validation rejections, file round trip and backup, backup recovery and quarantine, newer version / empty record / delete / discard, session save-with-slot and continue, flow resume and fallback, policy switches, explicit piece ids and id problems, and the project catalog asset lists every definition and is linked from `Battle.asset`.
- New PlayMode `GameRunSavePlayTests` (5, saved `Title` and `Game` scenes, temporary save folder): preparation work is saved at battle start and a mid-battle quit continues in that preparation (gold, recruit, equipment and stat, facility level, damaged and dismantled walls, squads bound, battle can start); reward save continues at the next stage without a second reward or meta gain, and defeat deletes the save; a save that differs from the default scene shrinks a squad, removes a squad, restores a hired captain, squad morale and a breached gate; an unrestorable save is set aside and a new run starts at stage 1; New Game needs a second click over a saved run, slot delete removes the run save, and a disabled trigger does not save.

Defects found and fixed during verification:

1. Two castle piece assets are both named `Gate`, so asset names could not be ids; the catalog now records asset GUIDs.
2. The build grid remembered that a fixed gate had been registered and refused to place the saved gate; restore now starts from a fresh grid.
3. Start funds were granted lazily, once per scene, so a continued run gained them again; they are now granted when the run is created and never for a restored run.
4. A test started the battle while the path rebuild from a dismantled wall was still running (test fix only).

Not verified: physical mouse clicks and layout of the Title buttons, the save written on a real application quit, how restored units look where they stand, save files written by a standalone build, and behaviour when a save from an older build meets changed content beyond the "unknown id" case.

Known limits (by design, see the design record): unit positions and remembered deployment spots are not saved; soldiers without a squad are kept in the run but have no scene unit; the same id used by two assets of one kind is reported by the setup menu and the catalog test rather than resolved.

## Completion (2026-11-17)

The user reported the manual check below as working and authorized Done and the develop merge. Develop (UI-02 single-language UI text, third-party asset submodule) was merged into the branch first: `TitleScreen` and `GameFlowSetup` were reconciled with the English-only captions, develop's `Title.unity` was taken and `Button_Continue` was recreated through `GameFlowSetup.ApplyTitleSaveSlots`, and the branch now records the same `Assets/ThirdParty` commit as develop. Combined compile, EditMode **467/467** and PlayMode **223/223** passed. Real application quit and standalone build remain unverified.

## Manual check

1. Open `Assets/_Project/Scenes/Title.unity` and press Play. Expected: `Continue` is greyed out for an empty slot.
2. Select slot 1, click `New Game`. In preparation buy an item at the Shop, recruit a soldier at the Barracks, and build or dismantle one wall piece.
3. Hold B for 1 second to start the battle, then stop Play mode during the battle.
4. Press Play on `Title.unity` again. Expected: slot 1 reads `… - Stage 1 in progress` and the button reads `Continue (Stage 1)`.
5. Click `Continue`. Expected: preparation of stage 1 with the same gold, the bought item, the recruited soldier and the changed wall; units that died in the interrupted battle are alive.
6. Win the battle and stop Play mode on the reward screen. Play again and `Continue`. Expected: stage 2 preparation with the reward gold already counted; commander XP was not granted twice.
7. With a saved run, click `New Game` once. Expected: the notice `A run is in progress. Click New Game again to discard it.` Clicking another slot cancels; a second click starts a fresh run.
8. Lose a battle (let the player die). Back on the Title screen `Continue` is greyed out for that slot.
9. Files: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\ProjectNeo\Saves\run_slot1.json` (readable JSON) with `run_slot1.json.bak` after the second save.
10. To change when the run is saved, edit the defaults in `Assets/_Project/Scripts/Runtime/Run/RunSavePolicy.cs`.
11. Reset: stop Play mode and delete the `Saves` folder from step 9.
