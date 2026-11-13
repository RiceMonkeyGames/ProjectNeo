# QA-victory-flow | 2026-11-13 | Game Flow - Victory, Reward and Next Preparation

Last Updated: 2026-11-13 20:25:05 (KST)

## Scope and evidence

- Owner: Hwang Seong-min. Branch `feature/qa-victory-flow` from synced develop `566128c`.
- Follow-up from [QA-game-loop](QA-game-loop.md): the first playtest could not reach victory, so victory → reward → next preparation was unverified. Enabled by [QA-opening-battle-balance](QA-opening-battle-balance.md) (12-enemy debug roster).
- Evidence is a **user manual playtest in the Unity Editor** only. No code, assets or scenes changed; no automated tests, agent reproduction or standalone build were run.

## Procedure given

1. Open `Assets/_Project/Scenes/Title.unity` → Play → New Game.
2. In the starting courtyard hold B for 1 second; 12 enemies (6 ladder, 2 gate breakers, 4 archers) attack. Staying in the courtyard is the easiest winning baseline (automated observation won around 80 simulated seconds).
3. On the reward screen press `Claim Reward & Next Stage`; expect exactly one transition into preparation.
4. Note next-stage state (surviving allies, wall/gate damage state, whether B starts the next battle again).
5. Stop Play Mode; reset via Title → New Game.

## Result

User reported on 2026-11-13: **all steps worked normally** ("모두 정상 작동함"). Victory was reached, reward claim entered preparation once, and the next-stage checks raised no issue.

User additionally confirmed the defeat branch: on defeat, `Return to Title` returns to the Title scene.

Not captured: exact battle duration, surviving-ally count, and specific wall/gate values carried into the next stage. Formal stage balance (B-7.1) and run carry-over accounting (C-3) were outside this task. Standalone build unverified.

User confirmed completion on 2026-11-13 ("finish it"); marked Done and archived, task-branch commit/push authorized. Develop merge remains with the user.
