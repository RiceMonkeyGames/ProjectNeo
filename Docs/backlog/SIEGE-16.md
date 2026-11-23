---
id: SIEGE-16
title: Flyer Cannon Targeting & Hover Slot Separation
status: In Progress
assignee: SeongminHwang
branch: feature/siege-16
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

## 5m castle kit adjustments (added 2026-11-23 after SIEGE-14)

The Game is moving to 5m cells and tiers with the castle kit ([Decisions-Resolved](../plan/Decisions-Resolved.md), [SIEGE-14](../history/SIEGE/SIEGE-14.md)). Build the flyer rules grid-agnostic (distances from data or cell size, never hard-coded 3m) and verify them on the 5m layout once `SIEGE-18` lands:

- **Mounted weapon targets**: cannons now mount on bastions and on the four roof points of a 2×2 `Tower`; flyer weapon targeting must cover tower roof mounts as well as bastion tops.
- **Hover heights and clearance**: walls are 5m per storey (one to three storeys, 15m max) and towers stand one storey above the touching walls; kit battlements are 1.38m merlons with a 0.76m crenel sill, using exact mesh colliders with allied `ShotCover`. Hover points and sightline checks must clear these heights and the tower corbel ring (0.5m overhang).
- **Tower roof occupants**: allied ranged units on a tower roof get +20% range and attack (`TowerOccupantBuff`), so they are high-value flyer targets; check that flyer target choice handles roof occupants sensibly.
