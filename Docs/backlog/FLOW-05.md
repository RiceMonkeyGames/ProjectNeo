---
id: FLOW-05
title: Saved Run Migration for the 5m Castle Grid
status: Waiting for Prerequisite
assignee: youngho
branch:
area: FLOW
depends_on: [SIEGE-18]
created: 2026-11-23
---

# FLOW-05 - Saved Run Migration for the 5m Castle Grid

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 2.

- Saved runs store castle cells per slot (FLOW-02, META-10 migration rules). Decide between migrating 3m castle layouts onto the 5m grid and resetting old castles with a refund, then implement it in `RunSaveMigration` with EditMode tests.
- Save the kit-specific state that the spike keeps only in memory if the design needs it across battles: breach and rubble cells, gate portcullis state.

Done when old saves load without errors into the 5m Game and the chosen migration is covered by tests.
