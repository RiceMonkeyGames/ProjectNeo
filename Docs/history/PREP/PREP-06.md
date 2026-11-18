# PREP-06 | 2026-11-18 | Flow/Run/UI - Preparation Time Clock

Last Updated: 2026-11-18 10:31:28 (KST)

Status: **Done** on user confirmation (2026-11-18).

Branch: `feature/prep-06`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim PREP-06`, `89967eb`). Rules: [PrepEconomyMeta § Preparation Time](../../design/PrepEconomyMeta.md#preparation-time-q-53-user-decision-2026-11-18) (Q-53).

## Decisions

- Q-53 (user, 2026-11-18): 12-hour budget per preparation, unused hours lost; the clock advances only through clock actions; free actions stay allowed after the clock runs out; battle still starts by holding `B`. Numbers are data.
- Input and display (user, 2026-11-18): **hold `T` for 1 second** to wait one hour (like the `B` battle hold, with a progress percentage); the clock is shown **at the top center**.

## Implemented scope

- **Pure rules** (`Runtime/Run`):
  - `PrepTimeSettings` asset (`Data/GameFlow/PrepTime.asset`): budget 12h, craft 1h, upgrade 1h, wait 1h, scout 3h (scout cost is read by C-5 later). `Create(...)` factory.
  - `PrepClock`: budget, used, remaining; `TrySpend` changes nothing when short; `Advanced(hours)` event for SIEGE-08 work progress; `Begin` refills; `Restore` for saves.
  - `RunState` (`RunStatePrepTime.cs`): `SetupPrepTime`, `BeginPreparationClockIfNeeded` (once per preparation, keyed by `BattlesFought` like the shop restock; never while a battle is pending), `HasPrepTime`, `TrySpendPrepTime`, `CanWait`, `TryWait`, `ClockPreparation`. Without settings there is no time limit.
  - Blacksmith: `CraftCheck.NotEnoughTime = 4` and `EquipmentUpgradeCheck.NotEnoughTime = 9` are checked after funds; nothing is paid when time is short; success spends 1h.
  - Run save: `RunSnapshot.prepTime` (`PrepTimeRecord`: preparation, budget, used); `RunRestoreOptions.PrepTime`. Older saves without the record start the next preparation with a full budget.
  - `RunConfig.prepTime` (+ `WithPrepTime` for tests); `RunState.CreateNew` sets it up.
- **Scene**:
  - `GameBattleController` starts the clock at preparation start before the automatic save (`GameBuildEconomy.StartConfig` exposes the run config).
  - `PreparationInteraction` adds the `T` hold (`PreparationInteractionSettings.Wait`, `WaitHoldSeconds` = 1; falls back to `T` when an older asset has no binding). It needs a fresh press, is cancelled by release, modals, construction, placement and focus loss, and only works in first-person exploration with time left. `CanWait`, `WaitProgress`, `WaitKey`, `TryWait()`, `Waited` event.
  - `GameFlowHud` bottom preparation line adds `Hold T - Wait 1h` (with `· N%` while holding) only while waiting is possible.
  - Blacksmith rows show the time cost (`· 1h`); short time shows `Not enough time.`
  - `PrepClockHud` (new UI component) shows `Prep time 11h left / 12h` (`- out of time` when exhausted) during preparation in every view; hidden in battle, reward and result. It is drawn above facility/equipment windows; it hides while the nearly full-screen squad organization window is open (that window has no time actions and its title box would touch the line).
- **Editor**: `ProjectNeo/Setup/Configure Preparation Time (PREP-06)` (`PrepTimeSetup.Apply`) creates the asset if missing, links it to `RunStart.asset` if empty, and adds/updates the clock line in `Game.unity` (re-runnable). Applied through the Editor API.

### Layout note

The clock line sits at the very top center (0–22px at 1280×720), overlapping only the top padding of the existing phase box, which stays at its original position. During implementation, placing the line inside or above the phase box pushed text under the facility window. The phase box's own two-line label already touches the facility window's top edge by a few pixels at 1280×720 (pre-existing, unchanged).

## Verification

| Scope | Result |
|---|---|
| `PrepTimeTests` (EditMode, 5) | Passed: clock spend/overspend/refill/restore clamp; craft and upgrade cost time and leave gold/materials/items untouched when short; no spending during a pending battle and a full new budget at the next preparation; no limit without settings; save keeps used hours and an old save refills. |
| `GamePrepTimePlayTests` (PlayMode, 1) | Passed: real Game scene budget from the asset; clock visible and clear of facility/equipment windows, hidden over the organization window; `T` held 0.3s shows progress without spending; 1.3s spends exactly 1h; keeping it held does not wait again; out of time hides the wait hint and shows `out of time`; `B` still allowed; hidden in battle; next preparation refills. |
| Full run (`unity-verify-local.ps1 -Mode all`) | dotnet pre-check and compile passed; EditMode **476/476**; PlayMode **232/232** (130.78s). |
| Manual play | Passed: the user confirmed the checks below on 2026-11-18. |
| Standalone build | Not verified. |

## Manual verification

1. Open `Assets/_Project/Scenes/Title.unity`, press Play, select a slot and press `New Game` (second click if the slot has a run).
2. **Clock**: in preparation, the top center shows `Prep time 12h left / 12h` above the `Preparation - Stage 1 - Gold ...` box.
3. **Wait**: the bottom hint shows `Hold T - Wait 1h`. Tap `T` briefly: nothing happens. Hold `T` about 1 second: the percentage fills, then the clock shows `11h left`. Keep holding: no further change until you release and press again.
4. **Blacksmith**: walk to the smithy, press `F`. Craft rows and upgrade rows end with `· 1h`. Craft one item: the clock drops 1h while the window stays open (the clock stays visible above the window). Upgrade one stash item: another 1h.
5. **Out of time**: wait until `0h left / 12h - out of time`. The `Hold T` hint disappears. In the smithy, crafting/upgrading shows `Not enough time.` and gold does not change. Shop, tavern, barracks, `I` equipment, `O` squads and `P` placement still work. Holding `B` still starts the battle.
6. **Battle and next stage**: the clock is hidden during battle and on the reward screen. After `Claim Reward & Next Stage`, the clock is full again (`12h left`).
7. **Save**: spend a few hours, quit Play Mode during preparation, press Play, `Continue`: the clock shows the same remaining hours.
8. Organization window (`O`): the clock is hidden while it is open and returns when it closes.
- Reset: stop Play Mode; start a new game from Title (second click discards the saved run).
- Automated repeat: Test Runner EditMode `PrepTimeTests`, PlayMode `GamePrepTimePlayTests`.
