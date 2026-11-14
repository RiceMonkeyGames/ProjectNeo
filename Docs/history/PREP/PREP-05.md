# PREP-05 | 2026-11-15 | Pre-Battle Squad Placement

Last Updated: 2026-11-15 01:58:01 (KST)

Branch: `feature/prep-05`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim PREP-05`). Design records: [PrepEconomyMeta](../../design/PrepEconomyMeta.md) (Pre-Battle Squad Placement), [WallsAndSiege](../../design/WallsAndSiege.md) (Preparation Gate Opening).

## Decisions (user, 2026-11-15)

Initial choice C-A-A-B: squad orders in prep exploration **and** a top-down placement method; units walk; anywhere reachable inside the castle; last placement persists into the next preparation.

| Item | Choice |
|---|---|
| Remembered position | Last placement ordered during preparation; battle orders do not overwrite it; squads walk back at the next prep start |
| Castle interior | Rectangle covered by wall and gate pieces (same idea as the morale retreat point) |
| Top-down placement | First built as a "squad placement" tool inside the construction view; after user play feedback moved to a **separate view opened with `P`** (no build tools). Select with F1–F4 / ` or by clicking a unit; left-click moves; right-click clears |
| First-person orders | Kept in prep exploration |
| Key help overlap | The battle key-help box was overlapping the bottom prep hint; in prep the box is hidden and squad keys are merged into one prep hint line (single function, to refine later) |
| Units left outside | Gate can be opened/closed with F during prep; closes automatically at battle start |

## Implemented scope

- Pure rules: `SquadPlacementMemory` (per-squad entry, shifts on squad removal), `CastleInteriorRules` (bounds + inclusive contains).
- `GamePrepPlacement` (added by `GamePhaseView.Awake`): prep destination check for `SquadCommandInput.DestinationCheck`, memory from `SquadCommander.OrderChanged` (prep only), return-to-placement coroutine after the NavMesh rebuild, placement-view input and `ClickAt`.
- `GamePhaseView`: `EnterPlacement`/`ExitPlacement`/`IsPlacementView`; squad commands and squad HUD enabled in exploration; help box shown only in battle.
- `PreparationInteraction`/`Settings`: `P` opens/closes the placement view (Esc also closes); falls back to `P` if an older asset has no binding.
- `GameFlowHud`: `PreparationSquadHint` (squad keys line in exploration) and `PlacementHint` (controls, selection, last message); prompt height 96 in these views.
- Gate: `PlacedStructure.IsHeldOpen`/`SetHeldOpen`/`IsDoorOpen` separate from breach state; `ConstructionWorld.SetGateHeldOpen` (NavMesh rebuild, no collapse event); `GamePrepGate` (added by `GameBattleController.Awake`) attaches `PreparationGateStation` to gate pieces, closes held gates at battle start and moves allies/player in the doorway inward.
- No `.unity`, `.prefab` or `.asset` edits; all new parts are created at runtime.

Touched shared files outside the user's prep area: `PlacedStructure`, `ConstructionWorld` (SIEGE-06 gate door code) and `SquadCommandInput`; the teammate should be informed.

## Verification

Compile passed. Full `unity-verify-local.ps1 -Mode all -SkipDotnet` passed EditMode **397/397** (3.23s) and PlayMode **200/200** (118.25s). New cases: EditMode `SquadTests.PlacementMemory_RemembersPerSquad_ForgetsAndShiftsOnRemoval`, `SquadTests.CastleInterior_BoundsWrapPiecesAndIncludeEdges`; PlayMode `GameBattlePlayTests` exploration orders + outside rejection, separate placement view (key/unit-click selection, outside rejection, walking, close clears selection, help box hidden), prep-placement memory across a stage, and gate open/close + battle-start close with doorway clearing. `GameFlowPlayTests` phase-view case updated for commands enabled in prep exploration. Dotnet precheck skipped (stale generated `.csproj`).

The user manually confirmed the first version worked in the Editor and gave the three improvement items above. On 2026-11-15 the user manually played the revised version (separate view, merged hint, gate) in the Editor, confirmed it works, and authorized completion, archive, and commit/push of the task branch; develop merge remains with the user. Standalone build not verified.
