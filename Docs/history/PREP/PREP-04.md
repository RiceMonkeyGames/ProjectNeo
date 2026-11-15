# PREP-04 | 2026-11-15 | Prep/Combat - Combat Consumable Use

Last Updated: 2026-11-15 23:56:33 (KST)

Branch: `feature/prep-04`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim PREP-04`). Design record: [PrepEconomyMeta](../../design/PrepEconomyMeta.md) (Consumables).

## Background

Potions (`item_potion`) could be bought, rewarded and sold but had no use. The user deferred the rules on 2026-11-13 (with Q-12) and on 2026-11-15 delegated the choices to the agent's recommendation.

## Decision (agent recommendation, 2026-11-15; open to revision after playtest)

| Item | Choice | Reason |
|---|---|---|
| Key | `Q`, stored as a data binding in `GameBattleSettings` | Unused by movement (WASD/Shift/Ctrl/Space), interaction (F/B/I/O/P) and squad keys (F1–F4, `, 1–3); reachable without leaving WASD; remappable when Q-11 fixes the layout |
| Quick slots | None yet; the oldest usable consumable in the run stash is used | Only one consumable exists; slot UI belongs to the UI pass |
| Effect | Instant heal of `UseHealRatio` × maximum HP (potion 35%), clamped to maximum | Ratio scales with equipment/perk HP; no channel time, so no dependency on the undecided interrupt system (COMBAT-04) |
| Limits | Battle phase only, living player only, self only, blocked at full HP (not consumed), shared 10s cooldown reset at battle end, no carry cap | Prevents waste and potion spam while keeping the stash as the single inventory |
| Feedback | Battle status line `Q · 회복약 ×N (재사용 Ns)` and a short reason when a use is refused | Reuses the existing test HUD |

Temporary values (35%, 10s) are data and were not tuned against the economy (potion price 10 gold).

## Implemented scope

- `Economy/ItemDefinition`: `useHealRatio` (default 0.35), `UseHealRatio` (0 for equipment), `CreateConsumable` factory. Existing `.asset` files were not edited; `item_potion` uses the code default.
- `Combat/Health`: `Heal(amount)` (clamped, never revives, raises `Changed`).
- `Run/ConsumableUse` + `ConsumableUseCheck` (Ok 0, NoItem 1, Dead 2, FullHealth 3, CoolingDown 4, NotInBattle 5): pure rules.
- `Flow/GameBattleSettings`: `UseConsumable` input action (`<Keyboard>/q`), `ConsumableCooldown` (10); `Create` parameter.
- `Flow/GameConsumables`: runtime component added by `GameBattleController.Awake` (`Consumables`), `TryUse`, `Count`, `CooldownRemaining`, `Message`, `Hint`, `Used` event. Falls back to the default binding if a serialized settings asset has no binding.
- `UI/GameFlowHud`: consumable line in the battle status text.
- No scene, prefab or asset edits; no enemy or squad AI changes.

## Verification

Compile passed. Full `unity-verify-local.ps1 -Mode all -SkipDotnet` passed EditMode **403/403** and PlayMode **203/203**. Dotnet precheck skipped (stale generated `.csproj`).

New cases: EditMode `ConsumableUseTests` (6: heal ratio defaults, heal amount, stash selection, check order, `Health.Heal`, settings defaults). PlayMode `GameConsumablePlayTests` (3, real `Game` scene): refused in preparation, heals 35% and removes one potion in battle, cooldown refusal; refused with no potion / full HP / dead; a simulated `Q` key press uses a potion, the battle status shows count and cooldown, and a second use works after the cooldown. The existing `Battle.asset` was confirmed to resolve the default `Q` binding and 10s cooldown.

One defect found and fixed during verification: the EditMode test assembly does not reference the Input System, so the binding assertion was moved to the PlayMode test.

Not verified: physical-key play, on-screen readability of the status line, and standalone build.

## Manual check

1. Open `Assets/_Project/Scenes/Game.unity` and press Play.
2. In preparation walk to the Shop, press F and buy one or two `회복약` (10 gold each); Esc to close.
3. Hold B for 1 second to start the battle. Expected: the battle status panel shows `Q · 회복약 ×N`.
4. At full HP press Q. Expected: nothing is consumed and `체력이 가득 차 있습니다.` appears briefly.
5. Take damage from an enemy, then press Q. Expected: HP rises by 35% of maximum at once, the count drops by one and `(재사용 10초)` counts down; pressing Q again during the countdown consumes nothing.
6. Win the battle and reach the next preparation: the remaining potion count is kept in the stash (Shop sell list).
7. Reset: stop Play mode. Nothing is saved to the scene or assets.

The user pre-authorized Done, archive, and commit/push of the task branch on 2026-11-15 once the full regression passed, without a manual playtest; the manual check above remains open and develop merge remains with the user.

On user request (2026-11-15) the branch was merged into develop together with the other two tasks of the same session; combined EditMode 424/424 and PlayMode 209/209 passed.
