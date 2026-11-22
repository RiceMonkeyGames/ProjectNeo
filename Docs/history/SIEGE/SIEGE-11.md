# SIEGE-11 | 2026-11-22 | Siege - Siege Weapon Bastions

Branch: `feature/siege-11`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim SIEGE-11`). Design record: [WallsAndSiege](../../design/WallsAndSiege.md) (Bastions). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Builds on [SIEGE-02](SIEGE-02.md) mounts and [SIEGE-10](SIEGE-10.md) battlements.

## Decisions (user, 2026-11-22, recommended options accepted)

| Item | Choice |
|---|---|
| Where weapons mount | Bastions only; plain walls lose their mount point |
| Piece | New `Bastion` kind, 1×1, 3m (wall height), HP 150, 25 gold, built outside against the wall |
| Flank fire | Cannon auto ±90°, manned ±135° (were ±45° / ±90°) |
| Battlements | Outer faces as usual; only the face the weapon points at becomes a sill-only gun port |
| Ladders / enemies | No bastion on a ladder lane; ladders do not attach to bastions; Wall Breakers target bastions like outer walls |
| Bastion beside a taller wall | Allowed with a preview warning (follow-up decision the same day) |

Agent defaults (open to revision after playtest): a bastion must touch a `Wall` at its own tier and elevation, no bastion-only chains, bastions stack only on bastions and nothing else stacks on them, a bastion collapse counts as a wall loss for morale, saved weapons on plain walls are dropped on load and refunded at their recorded value (META-10 rule), preparation squad destinations still use wall/gate bounds.

## Implemented scope

- Pure rules: `BuildPieceKind.Bastion = 3`; `PlacementIssue` `BastionNeedsWall = 9`, `LadderLane = 10`, `BastionStack = 11`; `Walls/BastionRules` (`Check`, `TouchesWall`, `JoinsWalkway`); `MountLayout.FacingFaces`; `RunSaveMigration` drops and refunds weapons whose support lost the mount point.
- Scene code: `ConstructionWorld.CheckPlacement` runs the bastion rules with an outside set cached per `LayoutRevision` and ladder lanes from `ReserveLadderLane` / `ReleaseLadderLane` (registered by `SiegeLadder.Awake`); `CastleBattlements` gun port per face (`GunPortFaces`, `StyleFor(placement, face)`, `IsGunPortAt(cell, face)`, crenel stand points skip only that face); `PlacementController.BastionNeedsConnection`; `ConstructionHud` / `SiegeHud` bastion field, `SelectBastion`, `Bastion` getter and messages; `GameSquadMorale` wall-loss for bastions.
- Editor/data: `BastionSetup` (`ProjectNeo/Setup/Apply Bastions (SIEGE-11)`, run by the user in the Editor) created `Bastion.asset` / prefab / material, cleared the `Wall.asset` mount point, set `CannonSettings.asset` to 90 / 135, added the bastion to `BuildCosts.asset` and `RunContent.asset`, and added `Button_Bastion` to `Game`, `ConstructionPrototype` and `SiegePrototype`. `ConstructionPrototypeSetup` and `CannonSetup` now create the same values for fresh setups.

## Verification

- EditMode `BastionRulesTests` 9/9 and the new `RunSaveMigrationTests` refund case.
- PlayMode: all weapon tests moved onto bastions through `BastionTestSupport`; new cases for a paid bastion that refuses weapons while under construction, the gun port on the facing face only (6 → 4 merlons, 3 sills), and a two-tier bastion drop.
- Full suite (`unity-verify-local.ps1 -Mode all -SkipDotnet`): EditMode **698/698**, PlayMode **305/305**. Code rules check passed (0 violations). Dotnet precheck skipped because Unity had not yet regenerated the csproj for the new files.
- Issues found during verification: (1) a manned-arc test still expected ±90°; updated to ±135°. (2) A cannon-attack test waited with Unity's overloaded `!= null`, so a destroyed cannon ended the wait after one frame; it now waits on the real reference. (3) A one-tier bastion dropped beside a two-tier wall is a walkway island that NavMesh culls as too small; the test now also breaks the upper wall and checks the top becomes walkable, and the build preview warns about this case.
- `git diff --check` reports trailing spaces only in Unity-serialized scene YAML (`m_Name: ` and empty event arguments of the new button); left as Unity writes them.
- Not verified: manual play in the Game scene and standalone builds.
