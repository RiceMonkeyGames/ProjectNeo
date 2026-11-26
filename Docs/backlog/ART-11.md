---
id: ART-11
title: Ladder Climb Animation Integration
status: Waiting
assignee: Unassigned
branch:
area: ART
depends_on: []
created: 2026-11-26
---

# ART-11 — Ladder Climb Animation Integration

## Scope & Completion Criteria

Add ladder climbing animations to humanoid units (allies, enemies, and first-person player body) traversing `SharedLadder` (`SIEGE-04`), closing the animation gap tracked in [Status](../plan/Status.md) and [ArtAssets](../codemap/ArtAssets.md).

### Planned Design & Scope

1. **Animation Clip & Animator Wiring**:
   - Select or import a fitting ladder-climb loop clip from approved animation packs under `Assets/ThirdParty` (`Synty/AnimationBaseLocomotion`, `Kevin Iglesias/Human Animations`, or approved supplement per `Q-01`), keeping `Assets/ThirdParty` unmodified.
   - Add ladder-climb state and normalized climb-speed parameter (positive ascending, negative descending, zero paused while ladder is being pushed/settling per `BUG-ladder-push-visual`) to shared unit animator controllers via Editor setup script (`CharacterModelSetup`).
   - Hide or holster hand weapons during ladder climbing if visually overlapping rungs (visual-only toggle under `Visual`).

2. **Decoupled Presentation Rules**:
   - Drive animator parameters purely from `SharedLadder` / traversal state without altering climb durations (`SIEGE-21`), single-occupancy FIFO rules, topple physics, or fall damage.
   - Handle clean transition back to idle/locomotion at upper/lower endpoints and to fall/death animations when knocked off a toppled ladder or killed mid-climb.

### Completion Criteria

- PlayMode verification confirms climb-up, climb-down, push-pause, topple-fall, and death-on-ladder transitions animate cleanly for both allies and enemies without console/animator warnings.
- `Docs/codemap/ArtAssets.md` updated with the applied animation clip path; manual visual check steps provided.
