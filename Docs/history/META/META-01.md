# META-01 | 2026-11-16 | Meta - Meta Progression Disk Persistence

Last Updated: 2026-11-16 15:03:30 (KST)

Branch: `feature/meta-01`. Owner: 신용준. Claim synchronized to develop (`docs: claim META-01`). Design record: [PrepEconomyMeta](../../design/PrepEconomyMeta.md) (Meta Save).

## Background

`MetaProgress` (commander level/XP, perk points, unlock points, unlocked ids, guaranteed next-run ids) lived only in `GameSession.Meta` for the lifetime of one application run. Meta save was already mandatory (Q-13); mid-run save is still undecided and stays out of scope.

## Decisions (user, 2026-11-16)

| Topic | Choice |
|---|---|
| Save timing | Right after every meta change, plus once more when the application quits |
| Slots | Three slots with a selection UI on the Title screen, implemented now |
| Failure handling | Keep the previous file as a backup on each save; if the main file is unreadable read the backup; if both fail, keep the damaged files under another name and start fresh |

Agent choices inside that frame (open to revision): JSON through `JsonUtility` under `Application.persistentDataPath/Saves`; file version `1`, and a file with a newer version is treated as unreadable (kept, never overwritten in place); the last used slot is remembered in `last_slot.txt`; slot deletion on the Title screen needs a second click; playing `Game.unity` directly selects no slot and never writes.

## Implemented scope

- `Meta/MetaSaveStore` (plain class, one directory): `SlotCount` 3, `CurrentVersion` 1, `Inspect(slot)` (no side effects), `Load(slot)` (restores the main file from the backup or quarantines unreadable files as `*.corrupt-<UTC stamp>`), `Save(slot, progress)` (temp file, then swap; only a readable main file is copied to `.bak`), `Delete(slot)`, `GetLastSlot` / `SetLastSlot`, `LastError`. Files: `meta_slot1.json` … `meta_slot3.json`, each with `.bak`.
- `Meta/MetaSaveFile` (`version`, `progress`), `Meta/MetaLoadResult`, `Meta/MetaLoadStatus` (Empty 0, Loaded 1, RestoredFromBackup 2, Unreadable 3).
- `Flow/GameSession`: `SaveStore`, `UseSaveStore` (tests), `ActiveSlot` (-1 = none), `SelectSlot(slot)`, `SaveMeta()`; `Reset()` also clears the active slot; application start re-registers the quit save.
- `Flow/GameRewardController`: calls `GameSession.SaveMeta()` after granting commander XP and unlock points (the only runtime meta change today).
- `UI/TitleScreen`: `SelectSlot(int)`, `DeleteSelectedSlot()` (two clicks), `SelectedSlot`, `IsDeleteArmed`, `SlotText(slot)`; `StartGame()` loads the selected slot before the Game scene. Slot rows show `Commander Lv N - Unlocks M`, `Empty`, `(from backup)` or `Damaged - starts fresh`.
- `Editor/GameFlowSetup`: `ApplyTitleSaveSlots()` (menu `ProjectNeo/Setup/Add Save Slots To Title Scene`) and the same rows in `CreateTitle`. `Title.unity` was changed through this Editor API only: three `Button_Slot N`, `Label_SaveStatus`, `Button_Delete slot`.
- `Game.unity`, prefabs and data assets were not edited. No combat, enemy or squad code was touched.

Later meta changes (unlock gacha spending, reincarnation bonus, perk point grants) must call `GameSession.SaveMeta()` when they are wired to the runtime (META-02).

## Verification

`unity-verify.ps1 -Mode all -SkipDotnet`: compile passed, EditMode **453/453** (7.75s), PlayMode **217/217** (111.15s). The dotnet precheck was skipped because the generated `.csproj` files were stale after the B-7.1 pull.

New cases: EditMode `MetaSaveStoreTests` (15: empty slot, round trip, slot independence, backup on second save, corrupt main restores backup and keeps the damaged file, both corrupt starts fresh, corrupt main never overwrites a good backup, newer version / negative values / `{}` unreadable, `Inspect` has no side effects, delete, last slot, invalid slot, session without slot never writes, session select/save, damaged-slot warning). PlayMode `GameFlowPlayTests` (2, saved `Title` scene): slot rows exist and show saved level, delete arming rules, the chosen slot is loaded into the Game scene and saved back; a saved slot is deleted on the second click.

All tests use temporary directories; after the full run `persistentDataPath/Saves` did not exist.

One defect found and fixed during verification: the PlayMode test assembly does not reference TextMesh Pro, so the label check goes through `TitleScreen.SlotText`.

Not verified: physical mouse clicks on the Title buttons, on-screen layout/readability, the save written on real application quit, and standalone build.

## Manual check

1. Open `Assets/_Project/Scenes/Title.unity` and press Play. Expected: three rows `슬롯 1~3: 비어 있음` under the title, slot 1 marked with `>`, then `새 게임`, `고른 슬롯 삭제`, `종료`.
2. Click slot 2, then `새 게임`. Win the first battle (kill all enemies) so the reward screen shows commander XP and unlock points.
3. Stop Play mode and press Play again on `Title.unity`. Expected: slot 2 is preselected and shows the commander level / unlock count from step 2; slots 1 and 3 are still empty.
4. Check the files: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\ProjectNeo\Saves\` contains `meta_slot2.json` and `last_slot.txt` (and `meta_slot2.json.bak` after a second reward).
5. Backup recovery: with Play stopped, open `meta_slot2.json` in a text editor, delete half of the text and save. Press Play. Expected (after a second reward exists): slot 2 shows `(백업에서 복구)`; after `새 게임` a `meta_slot2.json.corrupt-…` file appears next to a restored `meta_slot2.json`.
6. Delete: select slot 2 and click `고른 슬롯 삭제`. Expected: the button changes to `한 번 더 누르면 슬롯 2 삭제`; clicking another slot cancels; clicking the button again shows `슬롯 2을 지웠습니다.` and the row becomes empty.
7. Open `Assets/_Project/Scenes/Game.unity` directly and play a victory. Expected: no file in `Saves` changes (no slot is selected).
8. Reset: stop Play mode and delete the `Saves` folder from step 4.

User confirmed the manual check and authorized Done, archive, and commit/push of the task branch on 2026-11-16. Develop merge remains with the user; standalone build remains unverified.
