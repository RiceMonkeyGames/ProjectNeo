---
id: FLOW-05
title: Saved Runs on the 7.5m Castle Grid
status: Waiting
assignee: youngho
branch:
area: FLOW
depends_on: [SIEGE-18, SIEGE-39]
created: 2026-11-23
---

# FLOW-05 - Saved Runs on the 7.5m Castle Grid

## Scope & Completion Criteria

**Note 2026-11-27 (user, basin map decision): the run save format changes again (north wall band grid in `SIEGE-40`, free-placed inner castle in `SIEGE-39`); old saves dropped. Decide the saved state on the new format.**

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 2. Rescoped 2026-11-26 (user) to the 7.5m Game grid.

Already settled: old saves are dropped, not migrated. `SIEGE-18` (7.5m grid) made version 1 (3m cells) unreadable, and `SIEGE-36` (26-cell ring) made version 2 unreadable (user decision 2026-11-25).

- Remaining: decide with the user whether the kit-specific state that the Game keeps only in memory must survive between battles and save loads (breach and rubble cells, gate portcullis state), and save it in the run if so.
- If nothing needs saving, close this task as superseded.

Done when the user's choice is recorded in the design docs and, if state is saved, it reloads correctly with EditMode tests.
