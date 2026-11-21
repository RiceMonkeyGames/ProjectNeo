# COMBAT-08 | 2026-11-21 | Combat - Squad formations and drag placement

Last Updated: 2026-11-21 14:49:27 (KST)

Owner: youngho. Branch: `feature/combat-08` (recreated from develop `237a09e` on 2026-11-21). Status: In progress. Automated checks pass; manual play is pending the user. Backlog status is not changed here; completion, archive and branch push wait for user confirmation.

## Decisions (user, 2026-11-21)

- Full decision record: [Decisions-Resolved](../../plan/Decisions-Resolved.md) (top entry). Rules: [Combat](../../design/Combat.md) (Formations & drag placement).
- Controls: battle right-click drag sets the front line (length = frontage width, ranks follow from head count, facing perpendicular and away from the camera side); right click without drag moves at the current width. `G` cycles formation (temporary until Q-11).
- Prep placement screen (`P`, top-down): left-click drag does the same; plain left click moves as before.
- Narrow ground: NavMesh depth probe everywhere, plus the wall walkway width as an upper limit on wall tops. Ranks that do not fit extend the line sideways.
- Per-soldier slot markers are shown while dragging.
- Formations: Line / Close / Loose / Column. The remembered prep placement keeps formation and width.

## Bannerlord 2 research summary

- Dragging sets the frontage, ranks follow head count, and each soldier has a marker.
- Bannerlord only projects invalid slots to the nearest walkable point, so units bunch up. The rank clamp with sideways widening is a ProjectNeo rule, not Bannerlord behavior.

## Implementation

- Pure rules `Squads/FormationLayout.cs`: `FilesForWidth`, `Shape` (rank clamp plus sideways widening), `MaxRanks`, `CenterOffset`, `Slots`, `FacingFromLine`, `Segment`.
- `Squads/FormationKind.cs`: Line=0, Close=1, Loose=2, Column=3 (persistent values).
- `Squads/FormationPreview.cs`: slot preview data for drag.
- `SquadCommander`: per-squad formation and width state (`FormationOf`, `WidthOf`, `SetFormation`, `SetWidth`, `Next`, `IssueTo`), `FormationChanged` event; `SquadOrder.FrontAnchored`.
- `SquadCommandSettings`: new fields `cycleFormation` (G), `dragFormation` (RMB), `closeSpacing`, `looseSpacing`, `columnFiles`, `minDragLength` (1m), `depthProbeDistance` (20m). Old assets fall back to code defaults.
- `SquadCommandCenter`: `PlanSlots` (NavMesh.Raycast depth probe both ways, plus optional `WalkwayWidth` cap minus 2x agent radius); `IssueLine`, `PreviewLine`, `CycleSelectedFormation`. Formation change reforms in place (Hold with a destination is re-issued as Move; squads without a destination gather at their gather point). Width is restored if an order is rejected. With several squads selected, the line is split equally, left to right in squad number order.
- `SquadCommandInput`: RMB drag and `G` cycle.
- `GamePrepPlacement`: left-drag on the `P` screen; wires `CastleBattlements.WalkwayWidthAt`; the placement memory keeps `FrontAnchored`.
- `SquadPlacementMemory.Entry.FrontAnchored`.
- `Walls/BattlementRules.WalkwayWidth` and `CastleBattlements.WalkwayWidthAt`: axis-aligned within 30 degrees, wall top only, subtracts battlement thickness on faces that have battlements.
- `PlaceholderSquadMarkers`: slot preview discs (`VisibleSlotCount`).
- `SquadCommandHud`: key line adds drag and `G` hints; placement hint says left-click drag draws a line.
- Roster re-forming (added 2026-11-21 at user request): `FormationLayout.AssignSlots` keeps soldiers already on a slot and gives each freed slot (front first) to the nearest unplaced soldier. `SquadCommandCenter.LateUpdate` watches each squad's commandable count (keyed by squad object, so renumbering is safe); `AddMember`, `MoveMember`, `RemoveMember` and `SetLeader` mark the squad, and count increases re-form at once. Count drops (death, rout) refill holding squads after `SquadCommandSettings.RefillDelay` (2s). `Reslot` re-plans a moving squad, and in a holding squad only re-posts soldiers whose slot changed (keep radius 45% of spacing), so fighters keep their targets. Attack orders and squads without a destination are untouched. Existing callers that walk newcomers to the gather point still work; the re-form overrides it.

Agent defaults (open to change):
- Minimum drag length 1m; depth probe 20m; Column 2 files wide.
- On the top-down `P` screen, "away from camera" means toward the top of the screen (the camera can be rotated to face any wall outward).
- Diagonal walls are limited only by the NavMesh probe.
- Formation and width live with the squad command state for the scene session; they are not written to save files.

## Verification

- `Tools/Agent/unity-verify.ps1 -Mode all`: **EditMode 640/640**, **PlayMode 290/290**.
- `Tools/Agent/code-rules-check.ps1`: `VIOLATIONS=0`.
- New EditMode: `FormationLayoutTests` (13, incl. `AssignSlots`), `BattlementRulesTests` WalkwayWidth (2).
- New PlayMode: `SquadCommandPlayTests.DrawnLine_SplitsAcrossSelectedSquads_SetsWidthAndFacing_AndPreviewsSlots`, `WallCommandPlayTests.Formation_OnWallTop_ClampsRanksAcrossTheWalkway`, `SquadCommandPlayTests.HoldingSquad_FillsAFrontGapFromTheRear_AndANewMemberTakesAFreeSlot`.
- No human manual play and no standalone build claimed yet.

## Manual verification (pending user)

- Re-run the new tests: Unity Test Runner → EditMode filter `FormationLayoutTests` and `BattlementRulesTests`; PlayMode filter `SquadCommandPlayTests` and `WallCommandPlayTests`. Expected: all green.
- Scene `Assets/_Project/Scenes/Game.unity`: in preparation, press `P` and left-click drag across the courtyard. Expected: a line with per-soldier slot markers; the selected squad's ranks change with the line length; a plain left click still moves.
- In battle (hold `B` in prep), select squads with `F1`-`F10`, right-drag on the ground. Expected: the line is split equally among selected squads, left to right by squad number; press `G` to cycle formation; the squad reforms in place.
- On a wall top, drag a line wider than the walkway. Expected: ranks are cut to fit and the extra soldiers extend the line sideways.
- Reset: stop Play Mode (formation and width are not saved).

## Follow-ups

- COMBAT-10: crenel firing slots.
- Q-11 / UI-08: final key layout (`G` and RMB are temporary).
