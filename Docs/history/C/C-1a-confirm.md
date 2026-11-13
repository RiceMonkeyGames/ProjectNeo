# C-1a-confirm | 2026-11-13 | Run - Combat Outcome Format Review

Last Updated: 2026-11-13 20:51:18 (KST)

> Purpose: Review memo for the combat lead (youngho) and verification log. | Parent: [history/README](../README.md) | Related: [C-1a](C-1a.md), [MoraleAndWave](../../design/MoraleAndWave.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md)

### 2026-11-13: Review Memo (Draft, awaiting combat lead sign-off)

- Author: Hwang Seong-min, branch `feature/c-1a-confirm`. Doc-only; no code changed yet.

#### Current format (`Assets/_Project/Scripts/Runtime/Run/BattleResult.cs`)

| Field | Meaning | Filled by |
|---|---|---|
| `BattleNumber` | Copied from `BattleInput`; `RunState.Apply` accepts it once. | `GameRunLink.TryFinish` |
| `Outcome` | `Victory` / `Defeat`. | `GameBattleController.Resolve` |
| `EndReason` | `EnemiesDefeated=0`, `EnemiesRetreated=1` (Q-30), `PlayerDied=2`, `GateAndAlliesLost=3`. | `GameBattleController.EndReason` |
| `Fates[unitId]` | `Alive=0`, `Dead=1`, `Fled=2`. Missing unit = `Alive`. | `RunUnitBinding.CreateResult` (only `Alive`/`Dead` today) |
| `CastleAfter` | Castle snapshot after battle; `null` = unchanged. | `GameRunLink.CaptureCastle` |
| `EnemiesKilled[enemyId]` | Dead enemies per definition ID (C-4 material reward). | `GameBattleController.CountKills` (dead only) |

#### Findings

1. **Enemy exits are not recorded.** [MoraleAndWave](../../design/MoraleAndWave.md) says morale-routed and Q-30 retreating enemies count as exited, are excluded from kill counts, and their reasons are recorded separately in `BattleResult`. Only kills exist today.
2. **Enemy morale rout has no end reason.** `EnemiesRetreated` covers only Q-30 withdrawal.
3. **Ally `Fled` is never produced.** `CreateResult` takes an `isDead` predicate, so routed allies currently resolve to `Alive` (which already returns them next battle). `RunState.Apply` treats `Fled` like `Alive`.

#### Proposed changes (user decision 2026-11-13; pending combat lead sign-off)

| # | Proposal | Detail |
|---|---|---|
| P1 | Add enemy exit counts by type and reason | New enum `EnemyExitReason { Retreated = 0 /* Q-30 */, Routed = 1 /* morale */ }` and `EnemiesExited[enemyId][reason] = count` with an `AddExit(enemyId, reason, count)` method mirroring `AddKill`. Exited enemies never appear in `EnemiesKilled`. |
| P2 | Add `BattleEndReason.EnemiesRouted = 4` | Appended (persistent enum values unchanged). Used when the battle ends with no enemies left and at least one left by morale rout. |
| P3 | Keep ally fate format as-is | `UnitFate` and `CreateResult(isDead)` unchanged for now; B-8.2 decides whether routed allies need `Fled` explicitly. |

#### Questions for the combat lead

1. P1: Is per-type, per-reason granularity enough for B-8.2 and C-4, or do you also need per-squad enemy exits?
2. P2: When both Q-30 retreat and morale rout happen in one battle, which reason wins? (Proposal: `EnemiesRouted` if any enemy routed, else `EnemiesRetreated`, else `EnemiesDefeated`.)
3. P3: Will B-8.2 need to distinguish routed allies from survivors in the result (e.g., for C-3 carry-over or the reward screen)? If yes, we switch `CreateResult` to a `Func<T, UnitFate>`.
4. Defeat with routed allies (`GateAndAlliesLost` when all allies are dead **or** routed): does B-8.2 report "routed" to `SiegeOutcome` evaluation so this reason fires correctly?
5. Who fills exits at runtime: `GameBattleController` (tracking `retreated` today) or the B-8.2 morale code? Shared-file edit order to agree in [Team](../../plan/Team.md).

#### Next steps

- After sign-off: implement P1/P2 in `BattleResult`, `RunUnitBinding.CreateResult`, `GameRunLink.TryFinish`, `GameBattleController`; add EditMode tests; update [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md) and remove the "draft" note in `BattleResult`.

### 2026-11-13: Closed

- User reported youngho had already reviewed the format and is applying changes in B-8.2 (`feature/b-8.2` edits `BattleResult`, `RunUnitBinding`, `GameRunLink`). Proposals P1–P3 above are reference only; B-8.2 owns the final format.
- User confirmed `Done` on 2026-11-13. Doc-only; no Unity compile or tests run.
