# LEGACY | 2026-11-11~12 | Combat/Command - View, Markers & Hold Revisions

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-11: Attack Order Revisions, Vision, and Markers

- Destination reachability rules (Q-23): unreachable targets halt at closest reachable point. Attack orders advance toward point while engaging enemies spotted in sight (Q-22).
- Added `PlaceholderSquadMarkers` rendering yellow selection discs beneath units and cyan markers at destinations. Dead unit health bars hidden.
- Fixed defect: Enemy 12m vision setting was not saved in scene via `SerializedObject`, causing immediate charging. Fixed and added regression test.
- Verified: 101 EditMode, 31 PlayMode passed.

### 2026-11-11: 1st-Person Default Perspective Confirmed

- Q-24 confirmed: Default play perspective switched to 1st-person across `CombatPrototype`, `SquadCommandPrototype`, and `Game` scenes. Construction preserves orthographic view.
- Converted scene cameras and player inputs via Editor APIs. Updated test suites to verify eye-level tracking, pitch clamps, and self-occlusion avoidance.
- Verified: 101 EditMode, 40 PlayMode passed.

### 2026-11-12: Hold Behavior Revisions (Bannerlord Style) & Archer LoS Fix

- Revised Hold directive: Melee units advance to engage walkable enemies within 6m (`HoldEngageRadius`) and return to post. Ranged units shoot within range.
- Fixed archer LoS occlusion: ignored ground collision beneath shooter's feet (`CombatPhysics.GroundUnder`), enabling wall-top archers to fire downward at enemies directly below ramparts.
- Verified: 111 EditMode, 54 PlayMode passed.

### 2026-11-12: Ranged Pursuit During Hold Directive

- Added `UnitAISettings.holdRangedChaseExtra` (default 6m): Ranged units in Hold advance toward walkable enemies within attack range + 6m until within firing range, then halt and fire. Return to post if enemies leave radius.
- Verified: 111 EditMode, 55 PlayMode passed.
