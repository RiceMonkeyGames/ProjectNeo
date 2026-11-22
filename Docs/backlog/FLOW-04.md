---
id: FLOW-04
title: Save and Return to Title During Preparation
status: In Progress
assignee: SeongminHwang
branch: feature/flow-04
area: FLOW
depends_on: []
created: 2026-11-21
---

# FLOW-04 — Save and Return to Title During Preparation

## Scope & Completion Criteria

Registered 2026-11-21 from Q-13 ([Decisions-Resolved](../plan/Decisions-Resolved.md)). During preparation the player can choose `Save and return to Title`: the run is saved at the same moment quitting during preparation already saves (FLOW-02 `RunSaveTrigger`), then the flow returns to Title, where `Continue (Stage N)` resumes the same preparation. Not offered in battle or on the reward screen. Done when the action saves and returns, Continue restores the same state, and a failed save does not leave Title with a lost run. Open before start: where the action lives (there is no in-game menu during preparation yet; an `Esc` menu or the Options window from `UI-11` belongs to 신용준's UI work, so agree the entry point with them) and whether it needs a confirming second click.
