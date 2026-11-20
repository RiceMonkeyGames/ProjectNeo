# SIEGE-10 | 2026-11-20 | Siege - Auto Battlements on Top Wall Tier

Last Updated: 2026-11-20 19:55:44 (KST)

Branch: `feature/siege-10`. Owner: youngho. Claim synchronized to develop (`docs: claim SIEGE-10`). Design record: [WallsAndSiege](../../design/WallsAndSiege.md) (Battlements). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Feeds COMBAT-08 (wall-top width) and COMBAT-09 (arrow cover).

## Decisions (user, 2026-11-20)

| Item | Choice |
|---|---|
| Blocking | Both styles: Defense facility Lv1 low parapet, Lv2 crenellated battlements (merlons block, crenels open); castle-wide |
| Destruction | Not separately destructible; tied to the top wall piece |
| Ladders | First a gap; revised after playtest (screenshots: the low gap looked broken): pattern unbroken, ladder hangs in the middle crenel and rests on its sill |
| Edges | Outer faces only; joined walls, stair tops and inner faces stay open |
| Cannons | Both: sill-only gun port on cannon cells now; Bannerlord-style bastion registered as SIEGE-11 |
| Upgrade entry | "Battlements" button in the build-mode panel (build mode is entered at the courtyard `Construction` building, whose F stays build mode) |

Agent defaults (open to revision after playtest): all allies (not only those on that wall top) shoot through battlements; inside-the-castle walls get none; a face toward a lower wall cell (not open ground) stays open; the battle-time outside region is frozen at the last preparation edit; Defense facility trimmed from 3 to 2 levels (Lv2 costs the existing 60 gold); upgrade is a free action like other facility upgrades; placeholder sizes in `Battlements.asset`; Game scene only (prototype scenes unchanged).

## Implemented scope

- Pure rules: `BattlementStyle`, `BattlementSegment`, `BattlementSettings`, `BattlementRules` (outside flood fill, face mask, segments; every edge middle is a crenel).
- Scene: `CastleBattlements` (per-piece `Battlement` child with `BoxCollider` + `ShotCover` + `Visual` cube; NavMesh rebuild on change; initial build inside `ConstructionWorld.Awake` before the first NavMesh via `BuildBeforeNavigation`), `ConstructionWorld.LayoutRevision` / `RequestNavigationRebuild`, `SiegeLadder.EdgePoint` raised by `CastleBattlements.SillHeightAt`.
- Combat: `ShotCover` passes allied shots in `CombatPhysics.HasClearShot`, `CombatProjectile`, `CannonShell`, `CannonOperator` aim.
- Flow/data: `FacilityLevel.battlementStyle`; `GameBattlements` (Defense level → style, build-panel upgrade button `TryUpgrade`); `GameFacilities.DescribeDefense` window text (the Defense facility still has no station); `Facility_Defense.asset` 2 levels.
- Editor: `BattlementSetup` (`ProjectNeo/Setup/Add Battlements To Game Scene`) created `Battlements.asset`, set the Defense levels, and wired `Game.unity` (components + `BuildPanel/Button_Battlements` after `Button_Repair all`).

## Verification

- EditMode `BattlementRulesTests` 10/10.
- PlayMode `GameBattlementPlayTests` 6/6 (incl. upper-body aim over the parapet; incl. gun port: mounting a cannon removes that cell's merlons, removing it restores them): default parapets on outer faces with allied-only shot cover and an outside ray hitting the parapet; ladders rest on the sill with an unbroken base and no merlon within the ladder width; stacking moves the battlement and dismantling restores it; build-panel upgrade fails without gold, costs 60 gold, reaches max Lv2, switches every wall to crenellations (1 base + 2 merlons per cell) and rebuilds NavMesh.
- Full `unity-verify.ps1 -Mode all` (after the ladder and gun-port revisions): EditMode **555/555** (8.64s), PlayMode **258/258** (153.97s). Dotnet precheck skipped (no csproj).
- Defects found and fixed during verification: (1) a Defense `FacilityStation` added to the courtyard `Construction` building took over its existing build-mode target; removed (setup now strips it) and the upgrade moved to the build panel by user decision. (2) Battlements built after the first NavMesh forced a second rebuild on load, so `CanStart` stayed false for the first frames and 16 Game-scene tests failed; fixed by building them before the single initial NavMesh.
- Build panel layout: preferred height 727 vs panel 690 in the editor; the last element still ends inside the panel, but spacing is tighter.

Playtest fix (user, 2026-11-20): enemy archers walked up and stood facing defenders without shooting. Cause, confirmed with line casts in `Game.unity`: shooters always aimed at the body centre (1.0m above the walkway), and the 0.8m parapet hid it from 8–20m for defenders 1.5–2.2m from the edge, while the upper body (1.7m) was clear in every case. Fix: `CombatPhysics.TryGetShotPoint` falls back to the upper body (`CombatProfile.upperAimRatio` 0.85) for both the sight check and the arrow direction. This touches the shared combat core (`CombatPhysics`, `CharacterActor`, `CombatProfile`); tell the teammate before merging, and COMBAT-09 should build on it. New PlayMode case `EnemyArchers_ShootTheUpperBodyOverTheParapet_ButNotThroughMerlons`; full suite EditMode **555/555**, PlayMode **259/259** (148.92s).

Archer side-step (user, 2026-11-20): with Lv2 merlons an enemy archer in front of a defender hidden behind a merlon still had no shot. `SiegeAttackerBrain.DecideArcher` now tries side positions along the wall after 3s without a target (data in `SiegeSettings`) and exposes `IsArcherFiring` / `IsArcherRepositioning` for the coming animation work (ART-04). `CombatPhysics.HasClearShotFrom` added. The PlayMode upper-body case now also checks that the frontal spot is blocked and a side-step spot is clear. Full suite EditMode **555/555**, PlayMode **259/259** (145.42s). During this run the newly imported, still untracked `Assets/ThirdParty/ExplosiveLLC` pack broke compilation (demo scripts invalid in Unity 6); with the user's approval its 37 scripts were moved to the trash (animations kept). Backlog: ART-04 (apply animations, next) and COMBAT-10 (crouch-to-reload cover fire) registered.

Archer anti-jitter (user, 2026-11-20, "moving left and right outside would look odd"): the side-step now goes through the pure `ArcherCoverPlan` — the blocked clock runs only in range, one search per 10s, a found spot is held at least 5s, and with no spot the archer holds and waits (`IsArcherWaiting`, for the ART-04 bow-hold pose). EditMode `ArcherCoverPlanTests` (5). Full suite EditMode **560/560**, PlayMode **259/259** (146.23s).

Known and accepted (user, 2026-11-20): a cannon facing outward pokes about 0.07m into the 0.4m-thick sill (barrel tip ~1.17m from the cell centre, sill inner face at 1.1m). Left as is; the cannon is to be replaced by ballistae/trebuchets on bastions in SIEGE-11.

Not verified: how it looks and plays in `Game.unity` (battlement look, wall-top movement, enemy archer behaviour, cannon shots over the parapet, build panel readability), standalone build.
