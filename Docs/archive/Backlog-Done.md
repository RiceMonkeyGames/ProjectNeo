# Completed Backlog Archive (Done)

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Archive of completed (`Done`) backlog items moved out of `Docs/Backlog.md` to reduce active context size. | Parent: [INDEX](../INDEX.md) | Related: [Backlog](../Backlog.md), [history/README](../history/README.md)

| Task ID | Description | Assignee | Outcome & History Link |
|---|---|---|---|
| DOC-doc-tree-and-id | Shard Docs into tree structure and formalize Task ID naming rules | Hwang Seong-min (agent) | Branch `feature/doc-tree-and-id`. Split Context/ImplementationPlan/GameDesign/History into `Docs/` tree with `INDEX.md`, moved `Done` rows here, codified M3+ domain IDs and normalized unstarted irregular IDs (`QA-game-loop`, `SIEGE-01`, `SIEGE-02`). [History](../history/DOC/DOC-doc-tree-and-id.md) |
| DOC-workflow-branches | Document task branch creation and remote cleanup workflow | Codex | User decision (2026-11-12): Create feature branch from latest develop after claim; delete remote branch after confirmed integration. [History](../history/DOC/DOC-workflow-branches.md) |
| C-1a | Agree on Run Data & Combat Outcome Boundary | Hwang Seong-min | Merged to develop (`9429e85`). Pure C# `RunState`, `CastleState`, `BattleInput`, `BattleResult`. [History](../history/C/C-1a.md) |
| Q-04·05 | Finalize Morale, Rout, and Captain Death Rules | youngho | Decided 2026-11-12 (Bannerlord morale + Darkest Dungeon Breaking Point). Recorded in [MoraleAndWave](../design/MoraleAndWave.md). [History](../history/Q/Q-04-05.md) |
| C-4 | Runtime Reward Payout & Prep Phase Linking | Hwang Seong-min | Branch `feature/stage-rewards`, merged (`f9f79cf`). EditMode 169, PlayMode 76/77. [History](../history/C/C-4.md) |
| D-3~8-plan | Decompose Facility & Squad Tasks into Actionable Units | Hwang Seong-min | Registered D-0..D-8 and Q-33..Q-36. [History](../history/D/D-3-8-plan.md) |
| C-1~2 | Link Run Data to Game Scene | Hwang Seong-min | Branch `feature/run-scene-connection`, merged (`22253a4`). EditMode 163, PlayMode 75/76. [History](../history/C/C-1-2.md) |
| BUG-wall-hold | Flaky PlayMode Test: Squad Stops on Wall After Stair Disappears | youngho | Branch `fix/wall-hold-stall`, merged (`63bc842`). `MoveStallTracker`; EditMode 180, PlayMode 80. [History](../history/BUG/BUG-wall-hold.md) |
| PERF-nav-query | Profile & Optimize Unit AI Path Calculation Overhead | Codex | Merged (`f7207f3`, pushed `59c262d`). Idle Hold 200v200 AI mean 83.371 → 1.679 ms; PlayMode 91/91. [History](../history/PERF/PERF-nav-query.md) |
| BUG-playmode-input-focus | Make synthetic keyboard PlayMode tests independent of editor focus | Codex | Branch `feature/playmode-input-focus`, pushed (`c1506ba`). `FocusIndependentInputScope`; unfocused PlayMode 93/93. [History](../history/BUG/BUG-playmode-input-focus.md) |
| D-0 | Facility Common Framework (State & Building Pop-ups) | Hwang Seong-min | Branch `feature/facility-frame`, merged (`d146f35`). EditMode 181, PlayMode 83. [History](../history/D/D-0.md) |
| D-5 | Shop Buying & Selling | Hwang Seong-min | Branch `feature/shop-and-upgrade`, merged (`681d08d`). EditMode 195, PlayMode 85. [History](../history/D/D-1-5.md) |
| D-1 | Facility Upgrades | Hwang Seong-min | Branch `feature/shop-and-upgrade`, merged (`681d08d`). EditMode 195, PlayMode 85. [History](../history/D/D-1-5.md) |
| D-2 | Construction & Repair Costs | Hwang Seong-min | Branch `feature/build-costs`, merged (`1870a3b`). EditMode 175, PlayMode 79/80. [History](../history/D/D-2.md) |
| D-0b | Instantiate Run Units into Scene | Hwang Seong-min | Branch `feature/run-unit-spawn`, merged. EditMode 210, PlayMode 93. [History](../history/D/D-0b.md) |
| D-4 | Barracks Recruitment & Training | Hwang Seong-min | Branch `feature/barracks-recruit`, merged (`06baa0b`). EditMode 216, PlayMode 97. [History](../history/D/D-4.md) |
| D-7 | Equipment System | Hwang Seong-min | Branch `feature/player-equipment`, merged (`050948a`). EditMode 201, PlayMode 88. [History](../history/D/D-7.md) |
