# SIEGE-08 | 2026-11-18 | Walls/Flow - Construction Delays (Work Orders)

Last Updated: 2026-11-18 11:01:32 (KST)

Status: **Done** on user confirmation (2026-11-18).

Branch: `feature/siege-08`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim SIEGE-08`, `e9a7b85`). Rules: [PrepEconomyMeta § Preparation Time](../../design/PrepEconomyMeta.md#preparation-time-q-53-user-decision-2026-11-18) (Q-53 and the 2026-11-18 work order details).

## Decisions (user, 2026-11-18)

| Case | Rule |
|---|---|
| Build order at 0% when battle starts | Removed; gold refunded in full |
| Stacking / mounting | Only on finished pieces |
| Repair gold | Paid in full when ordered; unfinished share refunded at battle start (rounded down) |
| Display | Translucent ghost material + progress label above; queue summary on the construction screen |
| Unfinished cannon | HP = progress, cannot fire (not armed, cannot be manned); stays unfinished across battles; completing a full repair later finishes it |
| Broken gate repair | Closes only when the repair finishes; if unfinished at battle start, closes then with the HP reached |

Q-53 (already decided): orders progress only while the PREP-06 clock advances; 2 crews in order; 1h per piece or weapon; repair = missing-HP fraction × 1h; demolishing unfinished work refunds fully; unfinished pieces enter battle with HP = progress and block paths.

## Implemented scope

- **Pure rules** (`Runtime/Run`): `WorkOrder` (`WorkKind` Build=1/Repair=2, `WorkTarget` Piece/Weapon; hours, done, gold paid, start HP; `UnfinishedRefund`, `RepairedHealth`), `WorkQueue` (crews work the first unfinished orders together; a finished crew carries leftover time to the next order; `Completed`/`Progressed` events; `HoursToFinishAll`). Numbers in `PrepTimeSettings` (`workCrews` 2, `pieceWorkHours` 1, `weaponWorkHours` 1, `fullRepairHours` 1; written into `Data/GameFlow/PrepTime.asset`).
- **Walls**: `PlacedStructure.IsUnderConstruction`, `PlacedSiegeWeapon.IsUnfinished`; `PlacementIssue.UnfinishedSupport = 8`, `MountIssue.UnfinishedSupport = 5` (checked in `ConstructionWorld.CheckPlacement`/`CheckMount`/`TryMount`); `ConstructionWorld.RemoveUnbuilt` (removes a 0% piece without collapse events); `UnderConstructionView` placeholder (ghost material from `PlacementViewSettings`, warm tint, `NN%` TMP label, restores original materials on finish).
- **Flow**: `GameConstructionWork` (added by `GameBuildEconomy` at runtime, no scene edit): orders on controller placement/mount (only when prep time is configured; direct `ConstructionWorld.TryPlace` calls stay instant), clock-driven progress, completion, `FinishForBattle` (called by `GameBattleController` after the battle-start save and before the battle input), `Summary()` line, repair orders, cancel on dismantle with repair-remainder refund. `GameBuildEconomy.RepairAll` places repair orders (gold now) and skips targets that already have an order; `SetCannonsArmed` skips unfinished weapons.
- **Save**: `CastleWorkState` on `CastlePieceState`/`CastleWeaponState` (+ `Unfinished` on weapons); record fields `workKind/workOrder/workHours/workDone/workGold/workStartHealth` (+ `unfinished`) in `CastlePieceRecord`/`CastleWeaponRecord`; `GameRunLink.PieceWork/WeaponWork/WeaponUnfinished` feed the capture; `GameRunRestorer.RestoredWork` is re-queued in saved order at preparation start. Older saves have no work fields and load as finished.
- **Tests updated**: `GameBuildEconomyPlayTests` (repairs complete after one wait; gate stays broken until then) and two `StackedConstructionPlayTests` (wait before stacking on a controller-placed wall) — behaviour change by design.

## Verification

| Scope | Result |
|---|---|
| `WorkQueueTests` (EditMode, 4) | Passed: two crews with leftover carry-over and finish estimate; single crew order and duplicate/remove; repair HP and rounded-down refund; castle capture of work/unfinished and record read-back (old/unknown kinds = no work). |
| `GameConstructionWorkPlayTests` (PlayMode, 5) | Passed: placed wall is under construction (`0%`, no stacking/mounting), `40%` after 0.4h, finished after waiting; two crews finish the first two of four, 50% pieces enter battle at 50% HP, a 0% piece is removed and refunded; unfinished cannon at 50% HP is not armed in battle, is completed by a full repair next preparation and is armed the battle after; repair gold paid at order, 75% wall and 25% gate at battle start with rounded-down refunds, gate stays broken until then; preparation save records work kind/order/progress. |
| Full run (`unity-verify-local.ps1 -Mode all`) | dotnet pre-check and compile passed; EditMode **480/480**; PlayMode **237/237** (132.79s). |
| Manual play (including the checks below) | Passed: the user confirmed on 2026-11-18. |
| Standalone build | Not verified. |

## Manual verification

1. Open `Assets/_Project/Scenes/Title.unity`, Play, select a slot, `New Game` (second click if the slot has a run).
2. Walk to the construction station, press `F`. Place a wall on an empty cell: it turns translucent with `0%` above it; the left status shows `Work orders 1 · 2 crews · 1h to finish`. Try to place another wall on top or mount a cannon on it: refused (`The piece below is still under construction` / `This piece is still under construction`).
3. `Esc` back to first person, hold `T` 1 second (wait 1h): the wall becomes solid; stacking on it is now allowed.
4. Place four walls in a row and wait 1h: the first two finish, the other two stay at `0%`; wait a little more via crafting (1h) to see them progress, or start the battle (`B`): pieces at 0% disappear and their gold returns; partly built ones stand (weaker).
5. Mount a cannon on a finished wall, do not wait, start the battle: if it had any progress it stays translucent and never fires; at 0% it disappears and is refunded.
6. After a battle, press `Repair All` in construction: gold is paid, damaged pieces get repair orders; a broken gate stays open until its repair finishes (wait), then closes.
7. Save check: place a wall, wait part way (craft 1h if 1 crew is busy), quit Play Mode during preparation, `Continue`: the wall is still translucent with the same progress.
- Reset: stop Play Mode; start a new game from Title (second click discards the saved run).
- Automated repeat: Test Runner EditMode `WorkQueueTests`, PlayMode `GameConstructionWorkPlayTests`.
