---
id: PREP-07
title: Scouting Action (Reveal Hidden Enemies)
status: Pending Decision
assignee: Unassigned
branch:
area: PREP
depends_on: []
created: 2026-11-18
---

# PREP-07 — Scouting Action (Reveal Hidden Enemies)

## Scope & Completion Criteria

Deferred 2026-11-20 by the user (claim cancelled before any code): the scouting action will be decided together with the outside-walls attack/foraging (farming) concept, so it no longer holds M2 open. Until then hidden groups stay `?` in preparation. Registered 2026-11-18 as the C-5 follow-up (user deferred scouting from C-5). Unblocked 2026-11-19: Q-55 decided there is no time of day or calendar, only the prep-hour budget; was kept in M2 by Q-54 before the deferral. One scout spends `PrepTimeSettings.ScoutHours` (3h) via `RunState.TrySpendPrepTime` and calls `EnemyIntel.RevealAll` on `GameBattleController.Intel`; offered only while `EnemyIntel.AnyHidden`, refused with `Not enough time.` when short. Open before start: input/location of the action (user decision) and whether the revealed state must survive a run save/continue. Rules: [MoraleAndWave](../design/MoraleAndWave.md#wave-preview).
