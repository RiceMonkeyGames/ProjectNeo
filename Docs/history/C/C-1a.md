# C-1a | 2026-11-12 | Run - Run Data Architecture & Combat I/O Boundaries

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-12: C-1a Run Data Architecture & Combat I/O Boundaries

- Author: Hwang Seong-min, branch `feature/run-data-boundary`. Merged into develop.
- Implemented pure C# `RunState`, `RunConfig`, `CastleState`, `BattleInput`, and `BattleResult` in `ProjectNeo.Run`. Attached to flow via `GameFlow.StartRun(RunState)` and `FinishBattle(BattleResult)`. Enforced single result processing. Surviving units fully recover HP; fallen heroes preserved in `FallenHeroes`.
- Verified: 158 EditMode passed; 74/75 PlayMode passed (one pre-existing flaky test).
