# QA-opening-battle-balance | 2026-11-13 | Game - Debug Encounter Tuning

Last Updated: 2026-11-13 16:07:37 (KST)

## Decision and change

User clarified that this is a debugging prototype, not a formal stage, and requested rough tuning rather than further balance analysis. Game's `Assets/_Project/Data/GameFlow/Battle.asset` now spawns 12 enemies: 6 ladder, 2 gate breakers, 4 archers (previously 12/4/12, total 28). Updated `GameBattleSetup.CreateDebugEnemies` so regenerating Game battle settings retains this roster. Asset updated through Editor APIs. Unit stats, player HP, allies, outcome rules, and SiegePrototype settings remain unchanged. Formal stages/waves remain B-7.1.

## Verification

- Temporary explicit PlayMode observation, unchanged saved Game scene, 1/30s capture step, two fresh runs per condition. No recruiting/equipment/player attack/dodging. Player input disabled. Wall comparisons place the stationary player at cell (38,59); deployed comparison uses diagnostic prep Move orders to (31,59), (46,59), (38,59), waiting 30 simulated seconds. That prep deployment uses APIs and is not a claim that prep command UI exists. Observation helper removed after use.
- Original 28-enemy roster: courtyard lost at 56.30–59.33 simulated seconds; stationary exposed wall player died around 28.7s.
- Tuned roster: courtyard naturally reached Reward at **76.27s** (8 allies alive) and **78.40s** (3 alive), player HP 100 in both. No forced outcome, damage injection, or combat stat override. Main gate was destroyed in both; surviving allies completed combat normally.
- Tuned wall-player observations still lost at 29.07s; diagnostic wall deployment lost at 37.57/41.67s. This rough tuning permits the passive courtyard baseline to complete; it does not guarantee survival while stationary/exposed or establish player win rates.
- Observation compile passed; six-condition harness passed (one explicit test, 25.31s wall time). After removing the helper, final compile passed and existing **GameBattlePlayTests 10/10** passed (5.23s), using `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter GameBattlePlayTests -SkipDotnet`. Dotnet precheck also passed. Documentation check: history exists, broken links 0, diff check passed. Editor returned to clean Game scene outside PlayMode. User confirmed manual verification on 2026-11-13; detailed individual control checks were not separately reported. Standalone build unverified.

## Manual check and reset

1. Open `Assets/_Project/Scenes/Title.unity`, press Play, select New Game. Remain in the starting courtyard without recruiting/building for the easiest reproducible comparison.
2. Hold B for 1 second. Expect 12 enemies (6 ladder / 2 gate breakers / 4 archers) advancing from the north. WASD/mouse/left click control the player; F1–F3 select squads, 1 Move, 2 Attack, 3 Hold. A player standing exposed on the north wall can still die.
3. For the passive baseline, stay in the starting courtyard. Automated observations reached the normal reward screen after roughly 80 simulated seconds; timing/casualties may vary. Check `Claim Reward & Next Stage` enters preparation once. This checks reward flow, not formal next-stage difficulty or gate reset.
4. Stop Play Mode and reopen Title/New Game for a fresh castle and full roster. Do not use next-stage carry-over as a fresh balance comparison.

User confirmed verification and authorized completion on 2026-11-13 ("확인 완료 마무리"). Marked Done and archived; task-branch commit/push authorized. Develop merge remains with the user.
