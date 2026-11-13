# D-0b | 2026-11-13 | Prep/Flow - Spawning Recruited Grunts into the Game Scene

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-13: D-0b - Spawning Recruited Grunts into the Game Scene

- Assignee: Hwang Seong-min (agent). Branch `feature/run-unit-spawn`, merged to develop at user request (2026-11-13). Scope narrowed by user decision (2026-11-13): grunts only; hero prefab, captain spawning and captain squad orders moved to D-3. Verification via editor-only F9 debug key (user decision); remove once D-4 lands.
- Rules (pure C#): `RunState.TryFindRecruitSquad`/`TryRecruit`/`IndexOf` implement Q-36 (chosen squad if free, else first free squad, else new squad up to cap, rejected at cap or during a pending battle; no cost charged). `RunUnitBinding.BindNew` pairs a new run unit once. `SquadCommander.AddSquad` grows orders/selection.
- Scene: `SoldierDefinition.UnitPrefab` (shared data field; teammate notice needed). `GameRunLink.TryRecruit` syncs run and `SquadCommandCenter` (`AddSquad`, `AddMember`, `TryGetGatherPoint`). `GameUnitSpawner` instantiates inactive, applies ally AI/fall settings, places in front of the `FacilityStation`, walks to the squad gather point (new empty squad holds). `PlaceholderSquadMarkers` adds discs lazily. `GameUnitSpawnSetup` (Editor API) attached the spawner to `Game.unity` and filled `allied_melee`/`allied_ranged` prefab slots.
- Bug found during verification: inactive instances have no `UnitCombatAI.Actor` before Awake, so `TryRecruit` rejected every recruit; fixed by resolving `CharacterActor` via `GetComponent`.
- Automated: EditMode 210/210 (new `RunRecruitTests` 8, `SquadCommanderTests.AddSquad_*` 1). PlayMode 93/93 including new `GameUnitSpawnPlayTests` 2 (spawn near barracks, walk to squad, on NavMesh, in battle input, receives Attack order, death removed from run, battle-time recruit rejected; full squads → new squad selectable with markers).
- Re-run 2026-11-13 10:45 KST: EditMode 210/210, PlayMode 93/93, no compile errors.
- Manual play (user, 2026-11-13): Game scene F9 recruit spawn, walk to squad, new squad creation and battle participation confirmed with no issues.
- Not verified: standalone build, interaction with teammate morale/rout work (B-8).
