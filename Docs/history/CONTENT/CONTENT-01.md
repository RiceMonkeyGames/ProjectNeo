# CONTENT-01 | 2026-11-19 | Content - Expand Stage Catalog to 7 Stages

Last Updated: 2026-11-20 11:23:48 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [MoraleAndWave](../../design/MoraleAndWave.md#stage--wave-structure-q-51-user-approved-2026-11-14), [Q-67](../Q/Q-67.md), [B-7.1](../B/B-7.1.md), [EditorAndTests codemap](../../codemap/EditorAndTests.md)

Branch: `feature/content-01`. Owner: youngho. Claim synchronized to develop (`docs: claim CONTENT-01`).

## Background

Q-67 set the run to 7 stages in a fixed order with a 40-50 minute target. The stage catalog still held the 3 B-7.1 stages, so the run repeated stage 3 after the third victory.

## Decisions (user, 2026-11-19)

- Stages 1-3 stay as approved on 2026-11-16.
- Stages 4-7 approved as proposed. Only the six existing enemy prefabs are used; enemy totals rise 32 -> 38 -> 44 -> 50; wave gaps shrink to 35s (stage 4) and 30s (stages 5-7); every stage has 3 waves and the last wave is the boss slot for SIEGE-09.

| Stage | Wave 1 (0s) | Wave 2 | Wave 3 | Theme |
|---|---|---|---|---|
| 4 (32) | Ladder 6 L, Ladder 6 R, Archer 3 C | 35s: Gate Breaker 3 C, Wall Breaker 2 L, Archer 3 R | 70s: Flyer 3 C, High Flyer Archer 2 R, hidden Ladder 4 L | Pressure from both sides |
| 5 (38) | Ladder 8 C, Archer 4 L, Archer 4 R | 30s: Wall Breaker 3 L, Wall Breaker 3 R, Archer 4 C | 65s: hidden Gate Breaker 3 C, Flyer 4 L, High Flyer Archer 2 R, Ladder 3 R | Wall pressure |
| 6 (44) | Ladder 8 L, Ladder 8 R, Archer 4 C | 30s: Flyer 4 L, Flyer 4 R, hidden High Flyer Archer 3 C | 60s: Gate Breaker 3 C, Wall Breaker 3 L, Archer 4 R, High Flyer Archer 3 R | Air pressure |
| 7 (50) | Ladder 8 C, Ladder 6 L, Ladder 6 R, Archer 4 L | 30s: Gate Breaker 3 C, Wall Breaker 3 L, Wall Breaker 3 R, hidden Archer 4 R | 60s: Flyer 4 C, High Flyer Archer 3 L, High Flyer Archer 3 R, Ladder 3 C | Every enemy type |

L/C/R = left/center/right spawn point.

## Stage Clear Gold (current reward table)

`StageReward.Roll`: `BaseGold + GoldPerStage x (stage - 1)` plus kill gold. `StageRewards.asset` values (checked in the editor on 2026-11-19): base 50, +10 per stage; kill gold Ladder 3, Gate Breaker 5, Archer 3; Wall Breaker, Flyer and High Flyer Archer give no gold (not in the table). Gold if every enemy is killed (retreated enemies give none):

| Stage | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Clear gold | 50 | 60 | 70 | 80 | 90 | 100 | 110 |
| Kill gold (max) | 40 | 52 | 63 | 81 | 84 | 87 | 108 |
| Total (max) | 90 | 112 | 133 | 161 | 174 | 187 | 218 |

About 1,075 gold over a full 7-stage run, before boss rewards (SIEGE-09). Not retuned in this task.

## Changes

- `Editor/StageSetup.cs`: creates `Stage04-07.asset` with the compositions above; groups can be marked hidden; appends stages missing from an existing `StageCatalog.asset` without touching existing rows or their order. Menu run once: catalog now lists 7 stages. `Stage01-03.asset` unchanged.
- Tests: new EditMode `StageCatalogDataTests` (2: real catalog holds 7 stages in order and is wired into `Battle.asset`; every stage has 2-3 waves, first wave at 0s, rising start times, groups with prefab, count and spawn point). PlayMode `GameStageLoopPlayTests` now plays all 7 stages (renamed `실제_모든_스테이지를_...`); `GameWavePlayTests` expects 7 catalog entries.
- Docs: [MoraleAndWave](../../design/MoraleAndWave.md), [design/README](../../design/README.md), [EditorAndTests codemap](../../codemap/EditorAndTests.md).

## Not in Scope / Follow-ups

- Bosses in each last wave: SIEGE-09. Ending screen and harder loop after stage 7 (the last stage still repeats): FLOW-03.
- Reward retuning for 7 stages (Wall Breaker / Flyer / High Flyer Archer give no kill gold) was not changed.
- Performance with 50 enemies plus allies (Q-10) is checked only by manual play.

## Verification

- 2026-11-19, `Tools/Agent/unity-verify.ps1`: `COMPILE=PASSED`; `-Mode edit` `EDITMODE=PASSED (Total=505 Passed=505 Failed=0 Skipped=0)`; `-Mode play` `PLAYMODE=PASSED (Total=241 Passed=241 Failed=0 Skipped=0)`.
- Editor check after the setup menu: `count=7`, enemies per stage 12/18/27/32/38/44/50, hidden counts 0/2/0/4/3/3/4.
- Manual check (user, 2026-11-20): played the Game scene with the 7-stage catalog and confirmed the result; no issue found in CONTENT-01 scope. Bugs and requests raised in the same playtest went to separate backlog items (BUG-hired-unit-not-joining-squad, BUG-wall-archer-leaves-wall, Q-77, COMBAT-08, COMBAT-09, SIEGE-10).
- Verified scope: data rules (EditMode), Game scene integration for 7 back-to-back stages (PlayMode) and the user's manual play. Standalone build not run.
