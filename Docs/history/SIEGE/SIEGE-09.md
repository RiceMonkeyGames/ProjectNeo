# SIEGE-09 | 2026-11-20 | Siege/Content - Placeholder Stage Bosses

Last Updated: 2026-11-20 17:10:38 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [MoraleAndWave](../../design/MoraleAndWave.md), [Q-68](../Q/Q-68.md), [Q-74](../Q/Q-74.md), [CONTENT-01](../CONTENT/CONTENT-01.md), [WallsAndSiege codemap](../../codemap/WallsAndSiege.md)

Status: **Done** on user confirmation after manual play (2026-11-20).

Branch: `feature/siege-09`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim SIEGE-09`, `6654ea1`).

## Background

Q-68 set one boss per stage in the stage's last wave, and Q-74 set seven different bosses covering the siege, duelist, commander and artillery roles, with a dedicated boss health bar and extra gold and materials as the reward. CONTENT-01 left the last wave of every stage as the boss slot. META-08 (unlock points) and META-04 (run score) already had a boss-kill input that nothing filled.

## Decisions (user, 2026-11-20)

The user approved the proposed roster and the recommended answers to the four open points.

| Stage | Boss | Base enemy | Role | HP / ATK / DEF | Size | Special | Reward |
|---|---|---|---|---|---|---|---|
| 1 | Gate Crusher | Gate Breaker | Siege | 400 / 21 / 4 | x1.5 | Structure damage x2 | 30 gold, 2 hide |
| 2 | Duelist | Ladder | Duelist | 450 / 28 / 4 | x1.4 | Prefers player/heroes, move speed x1.3 | 40 gold, 2 bone |
| 3 | Marksman | Archer | Artillery | 350 / 26 / 2 | x1.4 | Range 22 (x2), wall-top targets (archer behavior) | 50 gold, 2 bone, 1 hide |
| 4 | War Captain | Ladder | Commander | 600 / 21 / 5 | x1.6 | Aura 12 m: ATK +20%, DEF +2 | 60 gold, 3 hide |
| 5 | Siege Giant | Wall Breaker | Siege | 900 / 28 / 6 | x1.9 | Structure damage x3, move speed x0.8 | 70 gold, 3 bone, 2 hide |
| 6 | Sky Hunter | Flyer | Duelist | 600 / 28 / 4 | x1.5 | Flies to the player/heroes first | 80 gold, 4 feather |
| 7 | Warlord | Gate Breaker | Commander | 1200 / 28 / 6 | x1.8 | Aura 15 m: ATK +30%, DEF +3 | 90 gold, 3 bone, 3 hide, 2 feather |

- Commander death: the aura ends and the remaining enemies of the boss's wave get ATK -20% (Warlord -30%) for the rest of the battle.
- Bosses never join the enemy retreat (Q-30); they fight until killed. (Enemies have no morale, so there is no morale rout to exclude.)
- The wave preview always shows the boss as `Boss: <name>`, even when its wave's other groups are hidden.
- A boss kill pays only the boss reward (no regular kill gold). It still counts as an enemy kill for the score (10) and adds 500 boss score points and 1 unlock point.
- Total boss gold over a full run: 420 (about +40% on the ~1,075 gold of CONTENT-01). Boss gold is part of the victory gold, so Ascension victory-gold reductions and perk gold bonuses apply to it.

## Implementation Choices

- Size: only the prefab's `Visual` child is scaled, so colliders, NavMesh agent size, ladder and wall-breaker approach data stay as tested.
- Stats: flat modifiers that reach the target values on top of the base soldier stats; Ascension percent modifiers (including `BossHealthPercent` / `BossAttackPercent`) multiply the boss values.
- Shared combat code touched with minimal, opt-in hooks (teammate review requested): `UnitCombatAI.TargetDistanceScale` (null by default, unchanged behavior), `FlyerBrain.PriorityTarget` with `FlyerTargetCandidate.IsPriority` (default false), and `BossUnit.StructureDamage` wrapped around the gate, wall and cannon damage calls in `SiegeAttackerBrain` and `WallBreakerAttack` (returns the same damage for non-boss enemies).

## Changes

- Runtime data/rules: `Stages/BossDefinition`, `BossRole`, `BossMaterialReward`, `BossRules`; `StageWave.boss` / `bossSpawnPoint`; `EncounterGroup.ForBoss` / `IsBoss` / `Boss` (`FromStage` adds the boss as the wave's last group, `WithCounts` keeps it at 1); `EnemyIntel.FromPlan` boss row and `EnemyIntel.BossLabel`.
- Runtime battle: `Siege/BossUnit` (Visual scale, stats, duelist targeting, aura, death penalty, structure damage hook); `Siege/FlyerPriorityTargeting`; `BattleEnemySpawner` adds `BossUnit` and uses the boss id; `GameBattleController` sets bosses up per wave, applies boss stats before Ascension, excludes bosses from retreat, exposes `Bosses` / `ActiveBoss` / `BossHud` and passes killed bosses to `GameRunLink.TryFinish`.
- Run/reward: `BattleResult.BossesKilled` / `AddBossKill`; `StageReward.Roll` adds boss gold/materials; `RunScoreTally.Record` counts bosses; `GameRewardController` passes `ScoreTally.BossesKilled` to `TryGrantRunEnd`.
- UI: `UI/BossHealthHud` (top-center name, red bar and `current / max`, code-built at runtime, no scene edit).
- Editor: `Editor/BossSetup` menu `ProjectNeo/Setup/Create Stage Bosses (SIEGE-09)`; run once, creating `Data/GameFlow/Stages/Bosses/boss_*.asset` (7) and filling the last wave of `Stage01-07.asset` (center spawn point).
- Tests: EditMode `BossRulesTests` (13) and `StageCatalogDataTests.ProjectStages_HaveOneDistinctBossInTheLastWave`; PlayMode `GameBossPlayTests` (2).
- Docs: [MoraleAndWave](../../design/MoraleAndWave.md), codemaps [WallsAndSiege](../../codemap/WallsAndSiege.md), [CombatAndSquads](../../codemap/CombatAndSquads.md), [FlowRunEconomy](../../codemap/FlowRunEconomy.md), [EditorAndTests](../../codemap/EditorAndTests.md).

## Not in Scope / Follow-ups

- Real boss units, behaviors and art: CONTENT-02 (M4).
- Value tuning after playtests (HP, aura, rewards). The Marksman range may need a revisit with COMBAT-09 (archer range changes).
- Teammate review of the shared combat hooks (QA-teammate-code-review).

## Verification

- 2026-11-20, `Tools/Agent/unity-verify-local.ps1 -SkipDotnet` (new files were not yet in the generated csproj): `COMPILE=PASSED`; `-Mode all` `EDITMODE=PASSED (Total=559 Passed=559 Failed=0 Skipped=0)`; PlayMode first run failed one test (`GameFlowPlayTests.AuthoredTitleAndPreparationUi_UsesEnglishAndProjectFonts`: boss HUD labels started with null text), fixed by starting them empty; then `-Mode play` `PLAYMODE=PASSED (Total=255 Passed=255 Failed=0 Skipped=0)`.
- Editor check after `ProjectNeo/Setup/Create Stage Bosses (SIEGE-09)`: stages 1-7 last waves hold `boss_gate_crusher`, `boss_duelist`, `boss_marksman`, `boss_war_captain`, `boss_siege_giant`, `boss_sky_hunter`, `boss_warlord` at the Center spawn point.
- Verified scope: pure rules (EditMode), real stage data (EditMode), Game scene integration for spawn/stats/HUD/aura/penalty/reward/score/retreat (PlayMode). Manual play confirmed by the user on 2026-11-20. Standalone build not run.
