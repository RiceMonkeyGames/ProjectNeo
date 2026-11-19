# META-06 | 2026-11-19 | Meta - Between-Run Unlock Screen

Last Updated: 2026-11-19 22:26:46 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [PrepEconomyMeta](../../design/PrepEconomyMeta.md#meta-progression), [META-05](META-05.md), [META-07](META-07.md), [Q-76](../Q/Q-76.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

Branch: `feature/meta-06`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim META-06`).

## Background

META-05 and META-07 added the unlock track rules, the save and the first track data, but entries could only be opened by editing the meta save. Q-76 places spending on the Title/meta screen between runs.

## Decisions (user, 2026-11-19)

- Layout: one window with the four tracks side by side (entries top to bottom), opened from an `Unlocks` button on the Title screen for the selected slot.
- Confirmation: click an entry once to arm it, click the same entry again to buy (same pattern as slot deletion and New Game on the Title screen).
- Agent defaults kept: every entry is shown (opened, buyable, or with the entries it still needs); refusals spend nothing and are explained on a status line; the slot is saved right after each purchase; the Title slot buttons now show the unlock point balance instead of the legacy unlock count.

## Changes

- `Meta/UnlockScreenModel` (pure rules and texts: `Click`, `EntryText`, `CanOpen`, `PointsText`, `Notice`, `Disarm`) and `UnlockClickResult` (`Armed`, `Opened`, `Refused`).
- `UI/UnlockScreen` (on the Title canvas): `Open(slot)` selects the slot through `GameSession.SelectSlot` when needed, builds one row per entry under four columns from the catalog, colors rows (opened / armed / buyable / locked), calls `GameSession.SaveMeta()` after a purchase (a failed save is reported on the status line), and raises `Closed`.
- `TitleScreen`: `OpenUnlocks()`, `ConfigureUnlocks`, `Unlocks`; refreshes the slot texts when the window closes; slot summary shows `Unlock points N`.
- `Editor/GameFlowSetup.ApplyTitleUnlockScreen` (`ProjectNeo/Setup/Add Unlock Screen To Title Scene (META-06)`) added the `Unlocks` button above `Delete selected slot` and the `UnlockOverlay` window to `Title.unity` through Editor APIs; re-running rebuilds them.
- Tests: new EditMode `UnlockScreenModelTests` (6); new PlayMode `TitleUnlockPlayTests` (2: buying from the saved Title scene saves the slot at once and updates the slot text; the `Unlocks` button opens the window and an empty slot is refused for lack of points).

## Not in Scope / Follow-ups

- Unlock points still come from each victory (C-4); granting them once at run end is META-08.
- Layout polish (fonts, icons, scrolling for longer tracks) belongs to the UI pass (Q-11).

## Verification

- 2026-11-19, `Tools/Agent/unity-verify-local.ps1`: `DOTNET_PRECHECK=PASSED`, `COMPILE=PASSED`; `-Mode all -SkipDotnet` `EDITMODE=PASSED (Total=509 Passed=509 Failed=0 Skipped=0)`, `PLAYMODE=PASSED (Total=243 Passed=243 Failed=0 Skipped=0)`.
- `doc-finish-check` reports `GIT_DIFF_CHECK_OK=False` only for trailing spaces that the Unity serializer writes into `Title.unity` (for example `m_Name: `; the committed scene already had 234 such lines). Scene files are changed only through Editor APIs, so they are left as written; `git diff --check` on every other changed file is clean.
- Manual check (user, 2026-11-19): the Title Unlocks window showed the points and four tracks, the click-twice purchase and prerequisite messages worked, the slot text updated on Close, and purchases persisted after restarting Play mode.
- Verified scope: rules (EditMode) and the saved Title scene wiring and save-on-purchase (PlayMode). On-screen layout and readability are covered by the manual check. Standalone build not run.
