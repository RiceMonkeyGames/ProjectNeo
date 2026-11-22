---
id: SIEGE-16
title: Flyer Cannon Targeting & Hover Slot Separation
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: []
created: 2026-11-22
---

# SIEGE-16 — Flyer Cannon Targeting & Hover Slot Separation

## Scope & Completion Criteria

Priority P2 task from [Q-60](../history/Q/Q-60.md) (AI improvement pass). Completes flying enemy AI in `FlyerBrain`, `FlyerSettings`, and `FlyerEngagement` ([WallsAndSiege](../design/WallsAndSiege.md), [WallsAndSiege codemap](../codemap/WallsAndSiege.md)).

- **Cannon targeting (design parity)**: Allow flyers to target mounted siege weapons (cannons) when no living ally target is available, matching the existing cannon durability rule in [WallsAndSiege](../design/WallsAndSiege.md).
- **Hover slot separation**: Distribute hover points when multiple flyers pursue the same target so they do not stack on the exact same world position.
- **Ranged flyer sightline clearance**: Ensure high-altitude projectile flyer variants pick unobstructed hover positions around tall walls and battlements.
- **Pure rules & tests**: Add EditMode hover-offset/targeting tests and PlayMode tests in `FlyerVerification` or dedicated PlayMode coverage.
