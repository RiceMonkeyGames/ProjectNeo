---
id: COMBAT-11
title: Bow Pitch Follows the Arrow's Launch Direction
status: In Progress
assignee: youngho
branch: feature/combat-11
area: COMBAT
depends_on: []
created: 2026-11-22
---

# COMBAT-11 - Bow Pitch Follows the Arrow's Launch Direction

## Scope & Completion Criteria

Registered 2026-11-22 from user request after BUG-wall-archer-shoots-through-footing: archers look like they shoot straight ahead wherever the enemy is. Measured bow clips cover the needed range (Hold `_Down` about -48 degrees, middle about -4 degrees, `_Up` about +42 degrees), but `CharacterModelAnimator.UpdateAim` sets `AimPitch` from the straight line to the target, while COMBAT-09 arrows leave along a ballistic launch direction (for example level ground at 16m: bow 0 degrees, arrow about +14 degrees). Make the bow pitch follow the launch direction (`ArrowBallistics.LaunchVelocity`), compensate the middle clip's built-in offset, and keep the result in pure rules covered by EditMode tests. Done when wall-top and ground archers visibly tilt the bow along the arrow's flight in manual play. Touches `CharacterModelAnimator` and `CharacterAnimationRules` (shared combat/animation): coordinate with the teammate.
