# SIEGE-01 | 2026-11-16 | Siege/Flow - Link Pending Spawns to Win/Loss Evaluation

Last Updated: 2026-11-16 13:49:52 (KST)

Branch: none of its own; implemented and recorded on `feature/b-7.1` together with [B-7.1](../B/B-7.1.md). Owner: Hwang Seong-min. Formerly `B-9.2-wave` (fulfills the `B-9.2` wave-spawn link in [M1](../../plan/M1.md)).

## Background

`SiegeOutcomeRules.Evaluate` already took an `enemiesPending` argument, but `GameBattleController` always passed `0` because every enemy spawned at battle start. With timed waves (B-7.1, Q-51) an empty field before the last wave would have counted as victory.

## Implemented scope (inside B-7.1)

- `GameBattleController.Evaluate` passes `EnemiesOnField` and `EnemiesPending` (living enemies not yet released by the wave timetable).
- Victory therefore requires every wave released and every enemy dead or retreated (Q-51); defeat still takes priority on the same frame (Q-09).
- The outcome still resolves exactly once through the existing `battleRunning` guard in `Resolve`; a new wave re-opens the retreat judgement but cannot re-trigger an outcome.
- No separate claim/branch: the work was required for B-7.1 to function and was completed there. The claim step was skipped to avoid a develop/branch conflict on the adjacent backlog row.

## Verification

- Automated (on `feature/b-7.1`): PlayMode `GameWavePlayTests` — empty field with a wave pending stays in battle and does not pull the wave forward; victory and reward occur once after the last wave; the next battle restarts the timetable without duplicates. Existing single-result cases in `GameBattlePlayTests` (simultaneous player death and enemy wipe → defeat once) still pass. Full EditMode **437/437**, PlayMode **213/213**.
- Manual: on 2026-11-16 the user reported the once-only termination/outcome check in multi-wave battles as verified.

## Manual repeat

1. Open `Assets/_Project/Scenes/Game.unity`, press Play, hold B for 1 second.
2. Kill wave 1 before the next-wave timer ends. Expected: no victory, battle status keeps `웨이브 1/2 · 다음 Ns`.
3. After the last wave appears, kill all enemies. Expected: the reward screen appears once and the reward is paid once.
4. Reset: stop Play mode.
