# BUG-battlement-inner-faces | 2026-11-22 | Walls - Battlements on Inner Wall Faces After Loading a Save

> Parent: [history/README](../README.md) | Design: [WallsAndSiege](../../design/WallsAndSiege.md) | Codemap: [WallsAndSiege](../../codemap/WallsAndSiege.md) | Branch: `fix/bug-battlement-inner-faces` | Assignee: youngho

## Problem & Root Cause

- After loading save slot 2 (stage 4 preparation), battlements stood on both faces of the castle walls, including the courtyard side.
- `BattlementRules.CollectOutside` flood-fills empty cells from the grid edge. Two collapsed ring cells at (24,59) and (25,59) let the flood reach the courtyard, so inner faces counted as outside.
- `CastleBattlements` recomputed the outside set only on `ConstructionWorld.LayoutRevision` changes. Battle collapses do not bump it, so the live session kept the stale closed-ring result, while a reload (`ClearForRestore` / `TryPlaceRestored`) recomputed from the gapped layout.
- A follow-up playtest found that the first fix still put battlements on the wall ends facing the gap, because the gap cell itself counted as outside.

## Decisions (user, 2026-11-22)

- The run-start (default) castle footprint, meaning its ring cells and courtyard, is always inside, even when ring cells have fallen. Walls the player builds outside it follow the normal flood-fill rule.
- Faces are recomputed on preparation edits, save loads and when the preparation phase begins. They stay fixed during battle.
- Broken-wall remains in collapsed cells were added to `SIEGE-13` as a visual follow-up.

## Implemented Changes

- [BattlementRules.cs](../../../Assets/_Project/Scripts/Runtime/Walls/BattlementRules.cs): `CollectOutside(..., alwaysInside)` overload that never floods into the given cells; `CollectEnclosed` collects every cell the start layout keeps unreachable from the grid edge.
- [CastleBattlements.cs](../../../Assets/_Project/Scripts/Runtime/Walls/CastleBattlements.cs): builds the start footprint once from `ConstructionWorld.InitialPieces` and passes it to `CollectOutside`; new `RecomputeOutside()` forces a recompute.
- [ConstructionWorld.cs](../../../Assets/_Project/Scripts/Runtime/Walls/ConstructionWorld.cs): exposes `InitialPieces`.
- [GameBattlements.cs](../../../Assets/_Project/Scripts/Runtime/Flow/GameBattlements.cs): calls `RecomputeOutside()` when the phase changes into `Preparation`.
- [BattlementRulesTests.cs](../../../Assets/Tests/EditMode/BattlementRulesTests.cs): 4 EditMode cases (start footprint, gapped ring leak without it, gapped ring with it including gap-end faces, closed ring and extensions unchanged).
- [GameBattlementPlayTests.cs](../../../Assets/Tests/PlayMode/GameBattlementPlayTests.cs): `GappedDefaultRing_KeepsTheSameFaces_AfterPreparationAndRestore` collapses two north-west ring cells, recomputes, rebuilds the castle through the restore API and compares all faces.

## Verification Scope

- Code rules check passed (0 violations).
- Unity compile passed; full EditMode 694/694 passed (before the gap-end change); after it, `BattlementRulesTests` 16/16 and `GameBattlementPlayTests` 7/7 passed.
- The full PlayMode suite timed out in the verify wrapper and was not confirmed.
- Manual play (user, 2026-11-22): loading save slot 2 in `Assets/_Project/Scenes/Game.unity` showed no inner-face battlements and no battlements on the wall ends beside the north-west gap.
