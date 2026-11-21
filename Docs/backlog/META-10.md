---
id: META-10
title: Save Migration for Renamed or Removed Content
status: Waiting
assignee: Unassigned
branch:
area: META
depends_on: []
created: 2026-11-21
---

# META-10 — Save Migration for Renamed or Removed Content

## Scope & Completion Criteria

Registered 2026-11-21 from Q-13 ([Decisions-Resolved](../plan/Decisions-Resolved.md)). When an update renames or removes a content id, a run save that still uses it should continue instead of being set aside (FLOW-02 rule today: the run is discarded and a new run starts at stage 1). A data rename table (old id -> new id) is applied before `RunState.TryValidate`/`Restore`; content that no longer exists is dropped from the run with a refund. Meta saves get the same rename for opened unlock entries and other stored ids. Saves that still cannot be read (damaged, unknown version) keep the existing set-aside rule. Pure rules with EditMode tests (`RunSaveTests`, `MetaSaveStoreTests` style). Open before start: which content kinds are covered (items, heroes, soldier types, perks, skills, facilities, castle pieces, unlock entries), the refund for each (for example item buy price, hero hire cost, nothing for perks since points return), whether the player is told what was dropped, and where the rename table asset lives.
