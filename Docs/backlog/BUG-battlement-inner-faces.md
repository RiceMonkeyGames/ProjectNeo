---
id: BUG-battlement-inner-faces
title: Battlements Appear on Inner Wall Faces After Loading a Save
status: In Progress
assignee: youngho
branch: fix/bug-battlement-inner-faces
area: BUG
depends_on: []
created: 2026-11-22
---

# BUG-battlement-inner-faces - Battlements Appear on Inner Wall Faces After Loading a Save

## Symptom

Reported by youngho on 2026-11-22 during ART-06 manual play. After loading save slot 2 (stage 4 preparation), battlements stand on both faces of the castle walls, including the courtyard side. In the same session before saving, only the outer faces had them.

## Cause (investigated during ART-06)

- `BattlementRules.CollectOutside` treats every empty cell reachable from the grid edge as outside. The saved castle ring (x 24-55, z 28-59) has two empty cells at (24,59) and (25,59), most likely walls lost in an earlier battle. Because of that gap, the courtyard (for example the player start (40,34)) is reachable from outside, so inner faces get battlements.
- `CastleBattlements` recomputes the outside set only when `ConstructionWorld.LayoutRevision` changes. Battle collapses do not bump it, so the stale "closed ring" result stayed in the live session. Loading a save (`ClearForRestore` / `TryPlaceRestored`) bumps the revision and recomputes from the gapped layout. Result: the same castle looks different before and after a reload. Dismantling a ring piece during preparation should trigger the same thing without a reload.
- Not caused by ART-06 (visual only). Shared SIEGE-10 rule code: coordinate with the teammate.

## Decide before fixing

How the inside of a gapped ring should be judged. Options:
- Cells inside the default castle footprint (or the run-start ring) always count as inside.
- Close small gaps (for example up to 2 cells) before the flood fill.
- Keep the last outside set from a closed ring and persist it in the save.

Side effects to check: ladder sill height (`SillHeightAt`), crenel stand points (COMBAT-10), walkway width (COMBAT-08) and NavMesh all read the same face mask.

## Completion criteria

Loading a save shows the same battlement faces as before saving, and a gapped ring follows the chosen rule both live and after a reload. Add EditMode cases for `CollectOutside` with gaps and a PlayMode save/load case.
