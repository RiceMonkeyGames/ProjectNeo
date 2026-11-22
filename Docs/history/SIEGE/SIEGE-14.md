# SIEGE-14 | 2026-11-23 | Siege - Spike: 5m Construction Grid with the Kingdom Castle Kit

Branch: `feature/siege-14`. Owner: youngho. Claim synchronized to develop (`docs: claim SIEGE-14`). Depends on [Q-01](../Q/Q-01.md). Design records: [WallsAndSiege](../../design/WallsAndSiege.md) (Towers, weapons on outworks). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md) (gate portcullis and gatehouse, no wall valleys, stairs behind a collapsed wall, towers). Code map: [WallsAndSiege codemap](../../codemap/WallsAndSiege.md), [EditorAndTests](../../codemap/EditorAndTests.md).

Goal (user, 2026-11-22): keep the `PolygonFantasyKingdom` demo walls at their own proportions. The kit is a 5m module (5x5m panels 0.5m thick, joint pillars, wooden walkway, 1.38m battlements, 5m storeys), while the Game grid is 3m cells and 3m tiers. Before changing the Game grid, build the full wall construction in a separate 5m spike scene, play it, and list what breaks; Game integration is a separate decision with the teammate. `Game.unity` and shared rule values were not changed.

## Decisions (user, 2026-11-22 .. 11-23)

| Item | Choice |
|---|---|
| Approach | Modular runtime dressing: kit pieces follow each column's neighbours so building and removing keep the pattern continuous |
| Gate | Outer frame with doors and portcullis, inner frame without; gate column two storeys by default (fixed upper wall); portcullis raised in preparation, lowered at battle start (Game wiring at integration) |
| Stairs between storeys | Automatic: straight flight on the lower walkway, quarter-turn flight on corner cells (option A) |
| Valleys (2-1-2 storeys) | Forbidden (`BuildGridDefinition.forbidValleys`, spike grid only); a collapsed storey takes every storey above it, so a fallen two-storey wall is rebuilt at full height |
| Breach look | Neighbours stay intact; the collapsed cell gets a V-shaped ruin so enemies can enter |
| Stairs behind a breach | Stay and close the gap, show the rubble section on their end, and are weaker (spike 50 HP vs wall 100) |
| Fallen bastion | Rubble only; the wall behind stays whole |
| Towers | New `Tower` outwork beside the unchanged bastion: 2x2, outside the wall line, one storey above the touching walls, nothing but tower tiers on top, roof mounts for siege weapons, allied ranged units on the roof +20% range and +20% damage (data-driven) |
| Materials | Demo normal maps on the project wall material copies |
| Battlements | Block movement; allied fire passes, enemy fire is blocked (same `ShotCover` rule as the Game) |

Agent defaults (temporary, tune after playtest): tower HP 400 per tier and 60 gold (Game only), four roof mounts 1.3m around the centre, spike stairs 50 HP, rubble pile at half height, breach V halves 0.7 of half a cell wide.

## Implemented scope

- Scene: `Assets/_Project/Scenes/SyntyGridSpike.unity` from menu `ProjectNeo/Setup/Create Synty Grid Spike (SIEGE-14)` (`Editor/SyntyGridSpikeSetup`): a `WallCommandPrototype` copy on a 16x14 grid of 5m cells with 3 levels, seeded ring (x 3..12, z 3..10), two-storey gatehouse, two-storey north row, stairs, a bastion and a three-tier tower; HUD wall/stairs/bastion/tower buttons; Tab switches construction / first person. Kit copies live in `Prefabs/CastleKit` with project URP materials.
- Pure rules: `Walls/CastleKitRules` (panels, accent and relief panels, shared joint pillars, walkway floors, battlements, gate frames and passage panels, automatic stairs, V breach ruins, stair-end rubble caps, rubble-only cells, tower dressing), `Walls/TowerRules` (placement, auto tiers, top-storey room, roof standing test), valley rules in `BuildLayout` (`PlacementIssue.Valley=12`, `ValleyFill`, `RemovalMakesValley`).
- Scene code: `Walls/CastleKitDresser` (rebuilds changed cells only, hides placeholder visuals, gate animation, breach and rubble memory, tower top-storey hollow collider with a roof slab, NavMesh rebuild requests), `Walls/TowerOccupantBuff`, spike test keys `Walls/CastleKitTestKeys` (`P` portcullis, `O` hold gate open, `B` breach/repair gate, `K` collapse the aimed wall, bastion, stairs or tower).
- Shared code, inert in the Game: `BuildPieceKind.Tower=4`, `PlacementIssue.TowerNeedsWall=13` / `TowerStack=14`, `BuildPieceDefinition.OccupantModifiers`, `BuildGridDefinition.forbidValleys`, `ConstructionWorld.GeometryRoot`, tower and valley hooks in `ConstructionWorld`, tower texts in the prototype `ConstructionHud`.
- Visible in the Game after merge: wall normal maps on `PolygonFantasyKingdom_Mat_Castle_Wall_01_URP` (also used by the placeholder wall, stairs and battlements), and five spike piece definitions listed in `RunContent.asset` (required by the catalog test).

## Verification

- EditMode `CastleKitRulesTests` 18, `TowerRulesTests` 7, `ValleyRuleTests` 4; full EditMode 752/752 on the final commit. Full PlayMode 313/314 after the tower work; the one failure (`SiegePrototypePlayTests.WallMelee_EngagesEnemyOnWallNearPost_ThenReturns`, 0.66m vs 0.6m) passed 27/27 on rerun, and an earlier `GameFlowPlayTests.BattleHold` failure also passed on rerun (known flakes). Code rules check passed.
- Checked in play with captures and NavMesh queries: kit proportions next to 2m soldiers; dressing follows placement and removal; NavMesh paths complete through one- and two-storey breaches, over corner stairs, and from the courtyard through the tower door and interior stair to the roof; four cannons mount only on the tower roof; an archer on the roof goes 16 → 19.2 range and 18 → 21.6 attack and back when leaving; the player stops at battlements on walkways (40/40 spots) and the tower roof (17/17); allied wall-top sight lines pass the battlements 39/41, enemy lines are blocked 41/41.
- Play mode in a background editor needs `editor.playmode set_run_in_background --enabled true`; NavMesh queries right after entering play mode need a few frames for the rebuild.
- `git diff --check` reports trailing spaces only in Unity-serialized scene YAML.

## Manual check

Run the setup menu above, enter Play. Walk the north walkway, open the tower door and climb to its roof; press `P`, `O`, `B` at the gate; aim at a wall cell with stairs behind it and press `K`, then rebuild it in construction (Tab); build a tower outside the south wall (two tiers expected) and place cannons on its roof. Expected results are listed in the Verification section. Reset by running the setup menu again.

## Breakage list if the Game grid moves to 5m (size: S small, M medium, L large; owner in brackets)

1. Map and default castle (`Editor/GameFlowSetup`: 80x100 cells of 3m, ring cells 24..55 / 28..59, gate column 39, courtyard facility and squad cells): becomes 48x60 cells; the 96m ring must become 95m or 100m. M (youngho).
2. Saved runs store castle cells per slot (FLOW-02, META-10 migration): old saves need a migration or a reset. M (youngho).
3. Tier height 3m to 5m: 3 tiers = 15m. Q-01 chose two-tier 6m default walls; on 5m one tier (5m) is already demo height, so the default should be re-decided. Fall damage values (`FallSettings`) and jump height checks need tuning. S/M.
4. Battlements and cover (`BattlementSettings` 1.1m sill / 2.2m merlons vs kit `Battlements_01` 1.38m with a 0.76m crenel sill): crenel stand points, crouch-reload (COMBAT-10), ladder top (`SillHeightAt`) must follow the kit or the kit must be scaled; the spike already uses exact kit colliders with allied `ShotCover`. L (teammate, needs a rule decision).
5. Siege ladders: lengths, raise/climb timing and slot spacing for 5m-10m walls. M (teammate).
6. Main gate: 2x1 placeholder of 3m cells becomes a 1-cell 5m `Wall_Gate_01` with a two-storey gatehouse; door collider, breach logic, portcullis preparation/battle wiring and machicolation sizes follow. M (teammate).
7. Bastions (SIEGE-11): bastion piece and mount points sized for 3m cells. M (teammate).
8. Stairs: one cell, 5m over 5m keeps the same 45° slope; Q-59 stair constraints unchanged; stair HP vs walls to balance. S.
9. Walkway width 3m to 5m: formation rank limits (COMBAT-08) and crenel slots give more room; check spacing. S (teammate).
10. Balance per piece (one 5m piece covers about 2.8 times the area of a 3m piece): wall HP, build/repair cost and time, wall-breaker damage, cannon blast radius vs cells. S/M.
11. Construction camera zoom and grid lines (`BuildCameraSettings`). S.
12. Other prototype scenes (`ConstructionPrototype`, `SiegePrototype`, `WallCommandPrototype`) stay on 3m unless regenerated. S.
13. Tests: about 10 Game PlayMode test files use Game cells or heights; EditMode tests mostly build their own grids. M.
14. Kit dressing to finish: convex corner pieces, whether breaches and rubble are saved, replacing `CastleBattlements` boxes with the kit battlements. M (youngho).
15. Draw cost: about 10 kit instances per dressed bay (a 2-tier ring of ~120 bays is ~2,500 objects); consider static batching or mesh combining per cell. S/M.
16. Follow-on: `ART-07` box trim and `SIEGE-13` (battlement slicing, 6m gate) are replaced by the kit pieces if the Game adopts the kit.
17. Towers in the Game: cost in `BuildCosts.asset`, HUD button, enemy targeting of towers and of stairs that block a breach, morale on tower loss, valley rule on or off. M (youngho + teammate).
18. Spike setup recreates its piece definitions with new GUIDs on every run, so `RunContent.asset` changes each time the menu runs; update in place before teammates use the spike. S (youngho).
