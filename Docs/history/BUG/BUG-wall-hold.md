# BUG-wall-hold | 2026-11-12 | Combat/Squads - Flaky Stall Fix

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-12: BUG-wall-hold Flaky Stall Fix

- Author: youngho, branch `fix/wall-hold-stall`. Merged into develop.
- Fixed `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear` failure where rear units remained in Move state after stair collapse due to shared destination blocking.
- Implemented pure C# `MoveStallTracker`: Units stalled within 1.5m of destination without 0.1m progress over 0.5s are considered arrived, cleanly transitioning to Hold.
- Verified: 180 EditMode passed, 80 PlayMode passed (6 consecutive isolated passes on the previously flaky test).
