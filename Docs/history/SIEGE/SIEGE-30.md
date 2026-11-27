# SIEGE-30 | 2026-11-27 | SIEGE - Collapse Look for Towers, Bastions & Three-Storey Breaches

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-27: SIEGE-30 - Collapse Look for Towers, Bastions & Three-Storey Breaches

- **Assignee**: `youngho` | **Branch**: `feature/siege-30` | **Claim commit**: `860c0d7`
- **Origin**: registered 2026-11-23 from the user's spike review. Rules: Collapse look in [WallsAndSiege](../../design/WallsAndSiege.md) (decided 2026-11-27: the stretched V breach stays, the alternative was removed). Built in the 7.5m spike `SyntyGrid75Spike`; it shares the dressing files with `SIEGE-27` and `SIEGE-29`.
- **Scope**:
  - Rebuild on ruins: a fallen bastion or tower spot could not be rebuilt in the spike.
  - Collapse look: a fallen tower or bastion leaves rubble plus a low jagged stump with a collider and NavMesh, and torn-off scars on the touched wall faces (look only; wall HP unchanged).
  - Breach look: a second V breach look (jagged end pieces stacked per storey) was compared in the spike, then the user kept the stretched V.
- **Implementation**:
  - Rebuild on ruins (`Walls/ConstructionWorld.cs`): `CheckPlacement` cached the outside-cell set by `LayoutRevision`, but combat collapses do not bump `LayoutRevision`. A fallen bastion or tower cell therefore stayed "inside" and placement failed with `BastionNeedsWall` / `TowerNeedsWall` (the Game scene showed it for bastions too). A private collapse counter, bumped in `Collapse`, now also invalidates the cache. `LayoutRevision` semantics are unchanged.
  - Game dev key (`Flow/GameDevKeys.cs`): `F11` collapses the wall, bastion, stairs or tower piece at the screen centre (Editor and development builds only) and logs the piece. The aim helper `CastleKitTestKeys.Aimed(Camera, RaycastHit[])` is now `public static` and shared with the spike `K` key.
  - Collapse look (`Walls/CastleKitDresser.cs`, `Walls/CastleKitRules.cs`): the dresser records each fallen bastion or tower cell with its pre-collapse top height (`rubble` is now `Dictionary<Vector2Int,float>`; the `CastleKitRules.Collect` rubble parameter is `IReadOnlyDictionary<Vector2Int,float>`).
    - `AddRubble` places a rubble pile and a low jagged stump: two `BrokenTop` halves per exposed face, heights 0.6, 1 and 1.3 times `StumpHeight`, chosen deterministically by cell and face.
    - A stump box collider (`RoomCollider`, height `StumpColliderHeight`) enters the NavMesh. Faces between cells of the same fallen 2x2 tower are skipped.
    - On each touched wall face, the same steep V half as a wall breach is added through the new shared helper `AddBreachHalf` (height = min(fallen top, wall top)).
    - `AddTop` drops battlements on a wall face toward a fallen piece that stood as high as the wall.
    - Everything lives in the fallen cell, so any rebuild there clears it. Wall panels and HP are unchanged.
  - Settings (`Walls/CastleKitSettings.cs`): new fields `stumpHeight` (1.6) and `stumpColliderHeight` (1.0). Existing assets use the defaults.
  - Breach look (`SyntyGrid75Spike` only): the stacked-per-storey alternative and the spike `N` toggle were removed after the comparison. The stretched V stays.
- **Tests**:
  - EditMode `CastleKitRulesTests` updated and added: `CollapsedBastion_LeavesRubbleAStumpAndAScar_WhileTheWallKeepsItsPanels`, `LowerFallenBastion_KeepsTheBattlementThatStoodAboveIt`, `FallenTower_SkipsTheFacesBetweenItsOwnCells`, `RubbleCell_IsNotDressedOnceSomethingIsBuiltThere`, `ThreeStoreyCollapse_StretchesOneVHalfPerSide_WithNoStackedEndPieces`.
  - PlayMode `GameConstructionWorkPlayTests.전투에서_무너진_보루_자리에_다시_보루를_지을_수_있다` (Game scene): rebuild on a bastion spot that fell in combat. Confirmed to fail without the fix.
  - Full suite (2026-11-27, `unity-verify.ps1 -Mode all`): code rules passed; EditMode 953/953 passed; PlayMode 372/381 passed. The 9 PlayMode failures are pre-existing and unrelated to this task: the same tests (plus `GameOrganizationPlayTests.편성_키를_다시_누르면_창이_닫힌다`) also fail on the pre-task code (370/380). They are player eye height (`CombatPrototypePlayTests`, `WallCommandPlayTests`), one-cell ground stairs (`StackedConstructionPlayTests`), and HUD layout or text checks (`GameBattlePlayTests`, `GameEnemyIntelPlayTests`, `GameFacilityPlayTests`, `PrepHudPlayTests`). After two small review fixes, `CastleKitRulesTests` (23/23) and the new PlayMode test pass.
- **Known follow-up**:
  - In Game, battlement collision and cover come from `CastleBattlements` (rule-based). That still treats the fallen face as a battlement face. Aligning the rules with the kit shape belongs to `SIEGE-20`.
  - Towers in Game come with `SIEGE-24`. They carry over the stump and scars automatically because the dresser is shared.
- **Manual Verification**:
  - Spike: open `Assets/_Project/Scenes/SyntyGrid75Spike.unity` and Play. Aim at the seeded bastion or a tower and press `K` (collapse aimed piece). Expected: rubble pile and a low jagged stump in its cells, a steep V-breach half against each touched wall face, and no battlements on that wall face when the piece was as tall as the wall. Collapse a 2- or 3-storey wall with `K`: the V breach is one half per side stretched to the fallen height.
  - Spike rebuild: build a tower or bastion on the fallen spot. Expected: placement succeeds, and the stump and scars are cleared.
  - Game: enter `Assets/_Project/Scenes/Game.unity` from `Title`, press `F9` (gold), build a bastion in front of a wall, press `F10` (finish work), aim at it and press `F11`. Expected: the Console logs `[GameDevKeys] Bastion ...`, the bastion leaves the stump and the steep half against the wall, and a new bastion can be placed on the same cell, which clears them.
  - Reset: stop Play Mode (collapses are not saved to the scene).
