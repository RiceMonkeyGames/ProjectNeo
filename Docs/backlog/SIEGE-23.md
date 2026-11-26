---
id: SIEGE-23
title: Bastions on the 5m Grid
status: Waiting
assignee: youngho
branch:
area: SIEGE
depends_on: [SIEGE-18]
created: 2026-11-23
---

# SIEGE-23 - Bastions on the 5m Grid

## Scope & Completion Criteria

Registered 2026-11-23 from the team decision to move the Game to the 5m castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md)); source: [SIEGE-14](../history/SIEGE/SIEGE-14.md) breakage list item 7.

- Resize the SIEGE-11 bastion piece, mount point and gun port faces for 5m cells and tiers; a fallen bastion leaves rubble only and the wall behind it stays whole (spike rule).
- Keep the walkway connection warning and the ladder lane rule.

Done when bastions build, carry cannons and collapse correctly on the 5m Game grid, with `BastionRulesTests` and the cannon PlayMode tests passing.
