---
id: QA-castle-kit-tests
title: Game Tests on the 5m Castle Grid
status: Waiting for Prerequisite
assignee: youngho
branch:
area: QA
depends_on: [SIEGE-18]
created: 2026-11-23
---

# QA-castle-kit-tests - Game Tests on the 5m Castle Grid

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 13.

- About 10 Game PlayMode test files use Game cells or heights (3m); update them for the 5m grid and kit heights, and keep EditMode tests that build their own grids unchanged.

*Updated 2026-11-23 by SIEGE-18*: the Game is on 7.5m cells with 5m storeys (not 5m cells). Failing Game PlayMode tests after SIEGE-18: StackedConstructionPlayTests (8), GameBattlementPlayTests (5), GameBattlePlayTests (1), GameFlowPlayTests (1); prototype tests were already updated in SIEGE-18.

Done when the full EditMode and PlayMode suites pass on the 5m Game.
