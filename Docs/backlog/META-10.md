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

Registered 2026-11-21 from Q-13 ([Decisions-Resolved](../plan/Decisions-Resolved.md)). When an update renames or removes a content id, a run save that still uses it should continue instead of being set aside (FLOW-02 rule today: the run is discarded and a new run starts at stage 1). A data rename table (old id -> new id) is applied before `RunState.TryValidate`/`Restore`; content that no longer exists is dropped from the run with a refund. Meta saves get the same rename for opened unlock entries and other stored ids. Saves that still cannot be read (damaged, unknown version) keep the existing set-aside rule. Pure rules with EditMode tests (`RunSaveTests`, `MetaSaveStoreTests` style). 2026-11-21 (user): full-price refund, developer log only, one shared rename asset, removed player hero swapped for the default player hero ([PrepEconomyMeta](../design/PrepEconomyMeta.md), [Decisions-Resolved](../plan/Decisions-Resolved.md)). Still open at start: the rename asset path and name, and the refund when a hero's hire cost or a piece's build cost changed since purchase (paid price vs current price; the run save keeps no paid price today).
