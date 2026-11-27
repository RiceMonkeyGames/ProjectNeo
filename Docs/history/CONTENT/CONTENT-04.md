# CONTENT-04 | 2026-11-27 | Content - EnemyDefinition Data Asset Refactor

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [MoraleAndWave](../../design/MoraleAndWave.md), [Decisions-Resolved](../../plan/Decisions-Resolved.md), [FlowRunEconomy codemap](../../codemap/FlowRunEconomy.md), [EditorAndTests codemap](../../codemap/EditorAndTests.md), [CONTENT-01](CONTENT-01.md)

Branch: `feature/content-04`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim CONTENT-04`).

## Background

Stage waves, the C-5 wave preview, the C-4 kill reward table and `BossDefinition` all identified enemies by raw prefab reference or prefab name. Display names came from a hard-coded Korean dictionary in `EnemyIntel`, and kill gold/materials lived in a separate `KillReward` list keyed by prefab name.

## Decisions (user, 2026-11-27)

1. `StageWaveGroup` gains a new `enemy` (`EnemyDefinition`) field. `enemyPrefab` stays only as a fallback; prefab-only groups (e.g. teammate-owned `SiegeSettings` lists) resolve through `EnemyCatalog.FindByPrefab`. `SiegeSettings` is untouched.
2. Kill gold and material drops move into `EnemyDefinition`; the `KillReward` list is removed and `StageRewardTable` references the `EnemyCatalog`.
3. Enemy ids keep the current prefab names (`enemy_ladder`, `enemy_gate_breaker`, ...), so existing kill/retreat ids are unchanged.
4. `BossDefinition.basePrefab` is replaced by `baseEnemy` (`EnemyDefinition`); `BasePrefab` is now derived from it.

## Changes

- New runtime types (`ProjectNeo.Stages`): `EnemyRole` (explicit values 0-5), `EnemyMaterialDrop`, `EnemyDefinition` (`Id`, `DisplayName`, `Role`, `Prefab`, `AffectedByMorale` default off per Q-44, `KillGold`, `MaterialDrops`, `Create(...)`), `EnemyCatalog` (`Find`, `FindByPrefab`, `Create`).
- `StageWaveGroup.Prefab` resolves definition prefab first, then the fallback. `EncounterGroup` carries `Enemy` and `EnemyId` (boss id > definition id > prefab name); `EncounterPlan.FromGroups` accepts an optional catalog.
- `EnemyIntel` reads `EnemyDefinition.DisplayName`; the Korean name dictionary was removed. Prefab-only groups fall back to a title-cased prefab name (`enemy_gate_breaker` -> `Gate Breaker`).
- `GameBattleSettings` gains `enemyCatalog`; `GameBattleController` and `BattleEnemySpawner` use catalog-resolved ids. `StageReward.Roll` reads kill gold and drops from the catalog. `KillReward.cs` deleted.
- Editor: new `EnemySetup` (menu `ProjectNeo/Setup/Create Enemy Definitions (CONTENT-04)`, idempotent) creates the six definitions and `EnemyCatalog.asset` under `Assets/_Project/Data/GameFlow/Enemies/`, wires `Battle.asset` and `StageRewards.asset`, and relinks stage groups. `StageSetup`, `BossSetup` and `GameRewardSetup` reuse it.
- Assets migrated through Editor APIs: Stage01-07 (63 groups linked, fallback prefab cleared), 7 boss assets, `Battle.asset`, `StageRewards.asset`. Reserialization added `aimSpread: 0` to boss assets (already the default).

## Enemy Data (values preserved from C-4)

| Id | Display name | Role | Kill gold | Drops |
|---|---|---|---|---|
| `enemy_ladder` | `사다리병` | GroundLadder | 3 | `mat_bone` x1 |
| `enemy_gate_breaker` | `성문 파괴병` | GateBreaker | 5 | `mat_hide` x2 |
| `enemy_archer` | `궁수` | Ranged | 3 | `mat_feather` x1 |
| `enemy_wall_breaker` | `성벽 파괴병` | WallBreaker | 0 | - |
| `enemy_flyer` | `비행병` | Flyer | 0 | - |
| `enemy_flyer_high_archer` | `고공 비행 궁수` | FlyingRanged | 0 | - |

## Verification

- Code rules check: passed. dotnet build and Unity compile: passed.
- EditMode: 1008/1008 passed (new `EnemyDefinitionTests`, `EnemyCatalogDataTests`; updated `BossRulesTests`, `EnemyIntelTests`, `StageCatalogDataTests`, `StageRewardTests`).
- PlayMode: 379/389 passed. All 10 failures are pre-existing and recorded in [SIEGE-23](../SIEGE/SIEGE-23.md) and [PREP-11](../PREP/PREP-11.md) (battle HUD overlap, enemy list vs BattleStatus, shop lock text, O-key close, PrepHud x2, first-person camera offset x2, `OneCellGroundStairs(0)/(2)`). Updated `GameBossPlayTests` and `GameBattlePlayTests` pass.
- Manual play in `Assets/_Project/Scenes/Game.unity` (preview names, kill rewards, boss wave) was left to the user.
