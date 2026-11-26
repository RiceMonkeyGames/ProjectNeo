# SIEGE-21 | 2026-11-26 | SIEGE - Siege Ladders for 5m Walls

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-26: SIEGE-21 - Siege Ladders for 5m Walls

- **Assignee**: `SeongminHwang` (taken over from the `youngho` pre-assignment while `Waiting`, user decision) | **Branch**: `feature/siege-21` | **Claim commit**: `2dc6e91`
- **Dependency**: [SIEGE-18](SIEGE-18.md) (7.5m grid, 5m storeys, ladder slots every 2 cells). Breakage list item 5 of [SIEGE-14](SIEGE-14.md).
- **Decisions (user, 2026-11-26)**; recorded in [WallsAndSiege](../../design/WallsAndSiege.md) (Battlements → Ladder height scaling) and [Decisions-Resolved](../../plan/Decisions-Resolved.md):
  - Climb 2.5s per storey (5m 2.5s, 10m 5s, 15m 7.5s).
  - Raise 3s plus 1.5s per storey above the first (3 / 4.5 / 6s).
  - The raised ladder leans at 75° at every height; the foot moves out with the height (never closer than `ladderBaseOffset` 1.2m).
  - Slot spacing stays every 2 cells (15m). The task's older "25m" figure dated from 5m cells.
- **Already in place before this task**: the ladder length followed the wall top under the slot and its top rested on `CastleBattlements.SillHeightAt`, so the kit sill change in `SIEGE-20` carries over automatically. Bastions and towers already refuse ladder lane cells (`PlacementIssue.LadderLane`). [SIEGE-17](SIEGE-17.md) reachability reads each ladder's own `BasePoint`/`TopPoint` and needed no change.
- **Implementation**:
  - `Runtime/Siege/LadderScaleRules.cs` (new, pure): `Storeys`, `ClimbSeconds`, `RaiseSeconds`, `FootRun`.
  - `Runtime/Siege/SiegeLadderState.cs`: `RaiseSeconds` and `ConfigureRaiseSeconds` (progress fraction kept).
  - `Runtime/Siege/SiegeLadder.cs`: when the supporting wall height changes, computes the storey count (wall top / wall piece height) and sets the raise time; new `Storeys`, `ClimbSeconds`, `RaiseSeconds`; `BasePoint` uses the 75° foot run from the top edge.
  - `Runtime/Siege/SiegeAttackerBrain.cs`, `Runtime/Siege/LadderTraveller.cs`: climb progress uses the ladder's `ClimbSeconds`.
  - `Runtime/Siege/SiegeSettings.cs`: `ladderClimbSeconds` renamed to `ladderClimbSecondsPerStorey` (`FormerlySerializedAs`, value 2.5 kept); new `ladderRaiseSecondsPerExtraStorey` 1.5 and `ladderLeanDegrees` 75. No serialized asset changed.
  - Tests: new EditMode `LadderScaleRulesTests` (16). PlayMode `GameBattlePlayTests` three-storey ladder test now checks 1, 2 and 3 storeys (wall height, storeys, climb and raise time, 75° lean, foot inside the ladder lane cell) and that an enemy climbs to the 15m top. Ladder PlayMode tests that raised with the settings value now use `SiegeLadder.RaiseSeconds`.
- **Automated Verification**:
  - Code rules check passed (0 violations, 0 serialized asset changes). Unity compile passed.
  - EditMode `LadderScaleRulesTests` 16/16. PlayMode `SiegePrototypePlayTests` 37/37, `GameBattlementPlayTests` 7/7, `AiAllocAndSplitPlayTests` 7/7, `AiCoreDefectsPlayTests` 3/3, `SiegeApproachPlayTests` 2/2.
  - `GameBattlePlayTests`: the ladder test passes; `전투_HUD의_부대_단계_상태_조작_영역은_겹치지_않고_건설_안내도_도구를_가리지_않는다` failed twice. It checks construction-bar HUD layout and does not touch ladders; whether it already failed on `develop` was not confirmed.
  - Full suite: see the final run below.
- **Manual Verification**: the user checked the ladders in play and then requested `/finish` (2026-11-26). Steps:
  - Open `Assets/_Project/Scenes/Title.unity`, Play, New Game.
  - In preparation, raise a north-wall ladder slot (not next to a corner tower or the gatehouse) to two and three storeys.
  - Start the battle: each ladder reaches its wall top; the taller the wall, the farther the foot from the wall, leaning instead of standing upright; on three storeys raising takes about 6s and climbing about 7.5s.
  - Reset: stop Play Mode; do not save the scene.
- **Known limits / follow-ups**: kit sill height follows `SIEGE-20`. Ladders still exist only on the north wall slots.
