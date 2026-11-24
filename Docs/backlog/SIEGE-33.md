---
id: SIEGE-33
title: Cannon Cluster/Threat Targeting, Trajectory Obstacle Check & Flyer Lead Velocity
status: In Progress
assignee: SeongminHwang
branch: feature/siege-33
area: SIEGE
depends_on: []
created: 2026-11-23
---

# SIEGE-33 — Cannon Cluster/Threat Targeting, Trajectory Obstacle Check & Flyer Lead Velocity

## Scope & Completion Criteria

Improve autonomous defense weapon AI in [CannonModule](../codemap/WallsAndSiege.md) (`SIEGE-02`, `SIEGE-07`):

- Add a swappable `ICannonTargetSelector` strategy that scores enemy clusters within blast radius and weights siege threats (`WallBreaker`, `GateBreaker`, ladder groups) instead of firing only at the single nearest enemy.
- Sample the solved ballistic arc for blocking friendly/terrain colliders before firing so auto-fire does not waste shots into adjacent towers, bastions, or roof overhangs.
- Revalidate the lead-adjusted aim point against `AimContext` (arc and min/max range) before firing, and expose a common velocity source so `Lead` predicts non-NavMesh flyers (`FlyerBrain`).
- Verify with EditMode cannon targeting/ballistics tests and PlayMode cannon tests.
