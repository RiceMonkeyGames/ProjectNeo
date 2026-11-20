# ART-04 | 2026-11-20 | Art - Imported Combat Animations and Hand Weapons for Prototype Units

Last Updated: 2026-11-21 01:49:40 (KST)

## Scope and decisions

- Branch: `feature/art-04`; youngho. Builds on [ART-03](ART-03.md) (Synty models + locomotion only).
- Asset prerequisite: the Kevin Iglesias `Human Animations` and ExplosiveLLC packs were committed to the `ProjectNeo-Assets` submodule (`98bb565db`, 5,195 files, ~980 MB, no file over 50 MB, all `.meta` present, ExplosiveLLC scripts already removed) and the pointer bumped on this branch (`ac4f914`).
- User decisions (2026-11-20):
  - Melee timing: keep gameplay `ImpactDelay` (0.18s) and **skip the start of the attack clip** so the weapon contact lands on the impact (no balance change).
  - **Hand weapons are part of ART-04** (not split into a separate task).
- User feedback after the first pass (2026-11-20): swords stood upright on corpses, bows were held the wrong way, death motions should be random, and archer movement looked wrong. All four were fixed in the same task (see "Feedback fixes").
- Clip source: Kevin Iglesias `Human Animations` (male/female humanoid). All ground units move with Synty Base Locomotion (archers through an 8-direction blend of its strafe clips; see round 3). ExplosiveLLC and Synty Sword Combat clips are not used.

## Implementation

- Runtime (all view-only; no combat rule changes):
  - `CharacterModelAnimator` (on `Visual`) now drives two layers from game signals only.
    - Base layer: `Locomotion` (Synty idle/walk/run on `Speed`), `CombatLocomotion` (combat idle — `CombatIdle1H01` or `BowIdle01` — with the same walk/run) while engaged (attacked within 2.5s or the brain asks to hold a drawn bow), `Crouch` (`Crouch01_Idle` / `Crouch01_Walk_Forward` on `Speed`; public `Crouched` property, no gameplay caller yet — for COMBAT-10), `Death0-5` (`CombatDeath01-06`, picked at random on each death through `CharacterModelAnimator.AnimationRandom` (`IRandomSource`)). Flyers keep the single `Hover` state.
    - Upper-body `Action` layer (`AM_UpperBody` mask: body, head, arms, fingers, hand IK; legs/root follow the base layer), default `Empty`:
      - Melee: `Attack0-4` = `Attack1H01-05_R` on `CharacterActor.AttackStarted`, picked at random through `CharacterModelAnimator.AnimationRandom` without repeating the previous swing (`CharacterAnimationRules.PickOther`), started at `CharacterAnimationRules.MeleeStartSeconds(contact, ImpactDelay)`.
      - Bow: `BowLoad` / `BowHold` / `BowRelease` = `BowShot01` Load/Hold/Release, each a 1D blend of `_Down` / mid / `_Up` on `AimPitch` (-1..1 for ±40°, target from `IAnimationCueSource.AimTarget`, else `UnitCombatAI.Target`). Sequence from pure `BowDrawCycle`: on attack start the draw is sped up (`ActionSpeed`) to finish by the impact, `Release` plays at the impact (arrow spawn), then it reloads and holds while engaged, or lowers otherwise.
      - `Hit` (`Damage01`) on `HitReceived` only when no attack/bow motion plays (the brighten flash still shows).
    - Death: plays the death clip and keeps the Animator running; `PlaceholderCombatView` no longer lays such models down (`PlaysDeath`). Flyers (no death clips) keep the ART-03 freeze + lay-down.
  - New: `CharacterAnimationProfile` (ScriptableObject with `Create(...)`; clip timings and tuning), `CharacterAnimationRules`, `BowDrawCycle`, `BowDrawPhase`, `IAnimationCueSource`.
  - `SiegeAttackerBrain` implements `IAnimationCueSource` (`HoldsDrawnBow` = `IsArcherFiring || IsArcherWaiting`, so SIEGE-10 waiting archers hold a drawn bow); `FlyerBrain` implements it with `AimTarget` = its pursued target. No other change in either brain. `UnitCombatAI` and `CharacterActor` are untouched (read through public `Target` and events only).
- Editor (`CharacterModelSetup`, menu `ProjectNeo/Setup/Apply Character Models`):
  - Regenerates controllers per body and weapon style: `AC_Unit_Ground_Masc_Melee`, `AC_Unit_Ground_Masc_Bow` (was `AC_Unit_Ground_Femn_Bow` until round 5), `AC_Unit_Flyer_Melee`, `AC_Unit_Flyer_Bow` (the ART-03 `AC_Unit_Ground_Masc`/`_Femn`/`AC_Unit_Flyer` are deleted), the mask `AM_UpperBody`, and profiles `CAP_Unit_*` in `Assets/_Project/Art/Characters/Animation/`.
  - Measures each melee clip's contact time in a preview scene as the right-hand speed peak relative to the chest (10–80% of the clip): `Attack1H01-05_R` = 0.30 / 0.45 / 0.30 / 0.37 / 0.33s (so each clip starts 0.12–0.27s in).
  - Attaches a `Weapon` instance under the hand bone (right hand for melee, left for bows), colliders removed, URP material copies:

| Unit prefab | Weapon | Notes |
|---|---|---|
| `allied_melee` | Kingdom `SM_Wep_Sword_01` | |
| `allied_ranged` | Kingdom `SM_Prop_Bow_01` | rotated (0, 270, 270): upright, string toward the archer. Model is PolygonElvenRealm `SM_Chr_Soldier_Female_01` since round 6 |
| `hostile_melee` (+ `enemy_ladder`) | Rivals `SM_Wep_Slayer_01` | |
| `hostile_ranged` (+ `enemy_archer`) | Kingdom `SM_Prop_Bow_02` | `SM_Wep_DarkElf_01` is a scimitar |
| `enemy_gate_breaker` | Rivals `SM_Wep_BigOrk_01` | |
| `enemy_wall_breaker` | Rivals `SM_Wep_Troll_01` | |
| `enemy_flyer` | Rivals `SM_Wep_SpiritDemon_01` | |
| `enemy_flyer_high_archer` | Kingdom `SM_Prop_Bow_02` | `SM_Wep_RedDemon_01` is a staff |

- Weapon offsets are zero. Bows are rotated as below; melee weapons are flipped 180° about X since round 7. chosen from preview renders (Synty hand bones share axes, so Synty weapons fit at identity). Bow mesh axes: long Y, curve Z with the string on -Z, thin X. Synty left hand: Z up, X toward the archer. The rotation maps mesh Y to hand Z and mesh Z to hand -X (`LookRotation(-X, Z)` = Euler (0, 270, 270)).

## Feedback fixes

- **Weapons on corpses**: weapons stayed parented to the hand bone, so after the death clip a sword pointed straight up. The driver now holds a `weapon` reference (set by the setup) and, 0.6s into the death clip, reparents it to `Visual` and lays it flat on the ground at the hand position (`CharacterAnimationRules.FlatDirection`: thin axis up, long axis along its current horizontal heading).
- **Bow grip**: the first rotation (-90, 0, 0) put the bow plane across the body, so the string ran sideways in front of the archer. Replaced with (0, 270, 270) and checked in side and top renders.
- **Random death**: the stable per-unit variant is replaced by a random pick on each death (`CharacterModelAnimator.AnimationRandom`, formerly `DeathRandom`, a `SeededRandom` seeded from the tick count; tests can replace it).
- **Archer movement**: archers used only the Synty feminine forward walk/run, so side-steps and back-steps slid. Archer controllers now use a `FreeformDirectional2D` blend on new floats `MoveX`/`MoveZ` (body-local velocity, set for all units): Kevin `Idle01` (or `BowIdle01` in combat) at the center, and `Walk01_*` / `Run01_*` in eight directions at their root-motion speeds (1.94 / 3.87 m/s). Archers moving faster than `CharacterAnimationProfile.BowLowerSpeed` (1.5 m/s) also lower the bow instead of running with it drawn (`CharacterAnimationRules.KeepsBowDrawn`), unless the brain orders a held draw. A shot that has already started still finishes.

## Feedback fixes (round 2)

- **Random melee swings**: attacks pick `Attack0-4` at random and never repeat the previous swing (`CharacterAnimationRules.PickOther` with `AnimationRandom`). The per-unit start index is removed.
- **Archers facing the wrong way while shooting**: the Kevin bow clips use a side-on archery stance (the whole body turns about 90°). With only the upper-body mask the legs kept the forward locomotion, so the torso twisted, and the arrow line pointed 30–50° away from the target.
  - The archer controller gets a synced layer `BowLegs` (index 2, synced to `Action`, mask `AM_LowerBody`: legs and foot IK). The driver raises its weight to 1 over 0.2s while the bow is drawn and the unit moves slower than 0.3 m/s, so the legs take the same stance.
  - In `LateUpdate` the driver turns only the model (`Visual/Model` yaw, view-only, ±90°, 360°/s) so the arrow line (right hand → left hand) points at the target. The line's offset from the model's forward is learned from the evaluated pose while holding the draw or on the first release frame (`CharacterAnimationRules.AimOffset`), then `BowModelYaw` = target bearing − offset. Clip pre-measurement in a preview scene was tried and rejected: the sampled offset (+20°) did not match the layered in-game pose (about −30°).
  - Result in `UnitBattlePrototype` Play Mode: model yaw settles around 54–67°, and the arrow line stays within about ±13° of the target during release (the hands move during the follow-through).
- **Walk motion**: the user was given a candidate list (Kevin `Walk01`/`Run01`/`Sprint01`/`StrafeWalk01`, Synty Base Locomotion `A_Walk_F_*`, Synty Goblin Locomotion `A_POLY_GBL_Walk_F_Neut`, ExplosiveLLC `WalkForward` and 2H `Walk`/`WalkSlow`) and chose Synty Base Locomotion for archers (round 3).

## Feedback fixes (round 3)

- **Archer walk**: on the user's choice, archers now use the same Synty Base Locomotion (Feminine) clips as the melee units instead of the Kevin `Walk01`/`Run01` set. The `MoveX`/`MoveZ` `FreeformDirectional2D` blend stays, so side- and back-steps do not slide: forward `A_Walk_F`/`A_Run_F`, forward-side and side `A_*_FwdStrafeFR/R/L/FL`, back and back-side `A_*_BckStrafeB/BR/BL`, at the forward root-motion speeds (1.5 / 2.6 m/s). The normal idle is Synty `A_Idle_Standing_Femn` (same as melee); the combat idle stays Kevin `BowIdle01`.

## Feedback fixes (round 4)

- **Archer forward walk**: the user wanted ExplosiveLLC `Archer Warrior Mecanim Animation Pack/Animations/Archer@Walk.FBX`. That free pack has forward-only movement (`WalkSlow` 1.07 m/s, `Walk` 2.14 m/s, `Run` 5.80 m/s; no strafe or back clips), so the user chose option 1. Only the forward walk of the archer blend uses `Archer@Walk`, placed at its own speed (2.14 m/s) so the feet don't skate. Diagonals, sides, back and all running keep the Synty clips. `Archer@Run` was not used because its 5.8 m/s is far above unit speed (about 3.5 m/s).
- **Animation events**: `Archer@Walk` fires `FootL`/`FootR` events whose ExplosiveLLC receivers were deleted with the pack's scripts. Every event then logged an error, which failed six `WallTopArcherPlayTests`. New view-only `CharacterAnimationEventSink` (no-op `FootL`, `FootR`, `Hit`, `Shoot`, `Land`) is added to each model by the setup. The submodule clip import settings are untouched.
- **Move direction with a turned model**: while a bow is drawn the model can be turned up to about 55° toward the target, so `MoveX`/`MoveZ` are now measured in the model's frame (`animator.transform`), not the unit's. Walking while aiming then picks the strafe clip that matches the real direction of travel.

## Feedback fixes (round 5)

- **Archers use the masculine motion set**: the user asked that archers use the same `Masculine` set as the male units. `allied_ranged` (female soldier model) and `hostile_ranged`/`enemy_archer` (dark elf) now take `Body.Masculine`. Locomotion: Synty `A_Idle_Standing_Masc`, `A_Walk_*_Masc`, `A_Run_*_Masc` (forward walk stays ExplosiveLLC `Archer@Walk`). Bow, hit and death: Kevin `HumanM@` clips. The setup now builds `AC_Unit_Ground_Masc_Bow` / `CAP_Unit_Ground_Masc_Bow` and deletes the old `AC_Unit_Ground_Femn_Bow` / `CAP_Unit_Ground_Femn_Bow`. No unit uses the feminine set anymore; `Body.Feminine` stays in the setup for future use.
- Verification: compile passed, EditMode 599/599, PlayMode 262/262 (153.0s). The archer blend lists only `_Masc` Synty clips plus `Archer@Walk` and `HumanM@BowIdle01`; a `UnitBattlePrototype` render shows an archer releasing in the male bow stance. Before switching scenes, unsaved changes in `Game.unity` (origin unclear: the user's or the prefab rebuild) were discarded on the user's approval; `Game.unity` was reopened afterward.

## Feedback fixes (round 6)

- **Allied archer model**: the Kingdom female soldier looked too much like the Kingdom male melee soldier. From the recommendation list (DarkFantasy `SM_Chr_Hunter_Male_01`, Kingdom `SM_Chr_Peasant_Male_01`, FantasyCharacters `Character_Male_Rouge_01`, ElvenRealm soldier/rogue), the user picked `SM_Chr_Soldier_Female_01`. Five packs have a prefab with that name; the ElvenRealm one was assumed because it was the one on the list and the Kingdom one was already in use. `allied_ranged` now uses `Assets/ThirdParty/PolygonElvenRealm/Prefabs/Characters/SM_Chr_Soldier_Female_01.prefab` (bow, Masculine motion set and weapon rotation unchanged). Its Built-in materials are converted by the setup's URP copies, and a preview render shows it textured (no magenta), about 2m tall, holding the bow upright, and clearly distinct from the melee soldier.
- Pink models when browsing third-party prefabs directly: the source materials use the Built-in `Standard` shader. Models applied through the setup get URP copies, so nothing is needed in game. Converting the source materials (Render Pipeline Converter) would modify hundreds of files in the `ProjectNeo-Assets` submodule, so it was left for a separate task if wanted.
- Verification: compile passed, EditMode 599/599. Full PlayMode run 259/262; the three failures were `Game.unity` input-timing tests (`GameFlowPlayTests.BattleHold_CancelsOnReleaseAndStartsOnceAtCurrentWallPose`, `GameFlowPlayTests.ConstructionWheelInput_UsesNormalizedStepsAndRestoresCamera`, `GamePrepTimePlayTests` T-hold clock). After `unity command editor set_auto_tick` (the documented guard against background frame throttling, skipped in earlier rounds), `GameFlowPlayTests` passed 14/14 and `GamePrepTimePlayTests` 1/1. Earlier rounds brought the window to front instead, which explains their intermittent input-test failures.

## Feedback fixes (round 7)

- **Melee blades through the body**: in `Game.unity` preparation, the soldiers' swords ran through their own torsos. Melee weapon meshes extend the blade along +Y from the grip. Attached at identity, the blade came out of the little-finger side of the fist (a reverse grip for the Kevin clips), so in idle and combat idle it pointed down and back through the body. All melee weapons (`SM_Wep_Sword_01` and the Rivals Slayer, BigOrk, Troll and SpiritDemon weapons) now use `MeleeEuler` = (180, 0, 0), which puts the blade on the thumb side. Comparison renders of four candidate rotations in Synty idle, Kevin `CombatIdle1H01` and `Attack1H01_R` showed that the X-180 flip (Z-180 and (0,180,180) look the same) raises the weapon in front in combat idle and keeps it off the body in idle. The same applies to the Rivals weapons. In the plain Synty idle the blade sticks out sideways away from the body, because that clip's wrist was not made for holding a weapon.
- Verification: compile passed, EditMode 599/599, PlayMode 262/262 (async run with `editor set_auto_tick`, no failures). Back-view render matching the user's screenshot shows no blade through the body. `Game.unity` was reopened afterward.

## Feedback fixes (round 8)

- **Melee weapons worn at the hip**: the user asked that all allied and enemy melee units wear their weapon at the waist. Ground melee units (`allied_melee`, `hostile_melee`/`enemy_ladder`, `enemy_gate_breaker`, `enemy_wall_breaker`) now keep the weapon in a `WeaponSheath` socket under the hips bone and draw it for combat. Flyers keep their weapon in hand.
  - Rule (`WeaponSheathRule`, pure): draw at once when engaged (attacked within 2.5s) or when the aim target is within `DrawDistance` (8m); sheathe after `SheatheDelay` (3s) without a reason. If an attack starts while sheathed (e.g. a gate strike with no target actor), the weapon snaps to the hand with no draw motion.
  - Motion: action-layer states `Unsheathe`/`Sheathe` play Kevin `HumanM@UnsheatheHips01_R` / `SheatheHips01_R`. The weapon switches parent at the moment the right hand is closest to the hips bone, measured by the setup (`HandNearHipsTime`, stored in `CharacterAnimationProfile.SetSheathMotion`).
  - Socket pose (`MeasureSheathPose`, per unit model): position = right hand position at the grab moment (hips space). Orientation = blade hanging down and back with the flat side facing out, tilted back so the tip plus the head half-thickness clears the ground by about 15cm (10–75° below horizontal). The weapon keeps the same local pose (`MeleeEuler`) under the socket and the hand, so only its rotation changes on the swap. A first try that used the hand's grab rotation left weapons sticking out sideways; a second try ignored grip height and head thickness and put the greatsword, hammer and club 15–29cm into the ground. Final lowest points in idle: soldier 0.17m, Slayer 0.10m, orc 0.21m, troll 0.11m.
  - At death a sheathed weapon stays on the hip; only a weapon in hand is dropped.
- Verification: compile passed, EditMode 601/601 (`CharacterAnimationRulesTests` 20 cases, adding the sheath rule), PlayMode 262/262 (async with `set_auto_tick`). In `UnitBattlePrototype` Play Mode (window in front; auto-tick does not advance a normal Play session): all 14 melee units had weapons in hand while fighting, survivors played `Sheathe` about 3s after the fight and then wore the weapon at the hip. Front, side and back renders of the four units in idle show the weapons at the right hip. `Game.unity` was reopened afterward.

## Verification

- First pass: compile passed, EditMode 595/595, PlayMode 262/262 (153.6s).
- After the feedback fixes: compile passed, EditMode 597/597 (`CharacterAnimationRulesTests` now 16 cases, adding `KeepsBowDrawn` and `FlatDirection`), PlayMode 262/262 (151.0s). `UnitBattlePrototype` Play Mode: all six death variants appeared; close renders show the dropped bow and greatsword lying flat (0.03m above the feet, long axis horizontal) next to the corpses; an archer advancing at 3.5 m/s played the Kevin run cycle. Play Mode only advances while the Unity window has focus.
- After round 2: compile passed, EditMode 599/599 (`CharacterAnimationRulesTests` 18 cases, adding `PickOther` and `AimOffset`/`BowModelYaw`), PlayMode 262/262 (151.4s). In `UnitBattlePrototype` Play Mode, archers stood in the side-on stance with `BowLegs` weight 1, and close renders from the target side show the bow arm and face toward the target.
- After round 3: compile passed, EditMode 599/599. PlayMode had one intermittent failure per full run, a different test each time, both in `GameFlowPlayTests`: `BattleHold_CancelsOnReleaseAndStartsOnceAtCurrentWallPose` (wall pose offset 0.27) and then `ConstructionWheelInput_UsesNormalizedStepsAndRestoresCamera` (zoom 55 instead of 46.75). Each passed alone, and the whole `GameFlowPlayTests` class passed 14/14 on rerun. Both cover wall-pose timing or mouse-wheel input, not animation; the Unity window was being brought to the front during those runs, which likely let real input or frame timing interfere. The controller blend lists only Synty clips; in Play Mode an archer advancing at 3 m/s played the Synty run with the bow lowered.
- After round 4: compile passed, EditMode 599/599, PlayMode 262/262 (155.5s; the `GameFlowPlayTests` flakes did not recur). The archer controller blend shows `Walk` (`Archer@Walk`) at (0, 2.1) and Synty clips elsewhere; a `UnitBattlePrototype` render shows an archer stepping forward with the ExplosiveLLC walk while drawing. `Game.unity` was reopened afterward, as the user had it open.
- Preview renders of all eight unit prefabs posed at attack contact / bow hold: every weapon is in hand, bows upright with tips curving toward the archer.
- `UnitBattlePrototype.unity` Play Mode (sampled every ~3s): engaged units switched to `CombatLocomotion`; melee played `Attack0-4`; archers cycled `BowLoad` → `BowRelease` and lowered (`Empty`, `Locomotion`) after the fight; dead units played `Death0-5` (five of six variants seen) and lay on the ground; mid-battle and post-battle renders looked natural. Scene left unmodified; Play Mode exited.
- Not verified: `Game.unity` manual play, siege archers holding a drawn bow while waiting behind battlements (SIEGE-10 path), up/down aim blend on walls, flyer attack/bow poses in flight, `Hit` reaction visibility, frame rate with many animated units, standalone build.

## Known limits / follow-ups

- No ladder-climb clip: units on ladders still use locomotion by horizontal speed.
- Bows are rigid meshes: the string does not bend with the draw and no arrow is shown in hand.
- Placeholder attack markers (yellow bars), role markers and health bars are kept.
- `Crouch` has no gameplay caller until COMBAT-10.

## Manual verification

1. Open `Assets/_Project/Scenes/UnitBattlePrototype.unity` and press Play.
2. Watch the archers move: they walk or run with matching leg motion (also when stepping sideways or back), lower the bow while running, and hold it with the string toward their body. As the lines meet: soldiers drop into a combat stance and swing swords (varied swings, not in sync); Slayers swing their greatswords; archers raise the bow, draw and release in time with each arrow, and hold the draw between shots.
3. Fallen units play one of six fall-over deaths at random and lie on the ground (dark tint, no health bar); their sword, greatsword or bow drops and lies flat beside them instead of standing upright.
4. After the battle, survivors return to the normal idle/walk; archers lower their bows after about 2.5s.
5. Open `Assets/_Project/Scenes/Game.unity`, Play, hold `B` to start a battle: enemy archers below the wall aim upward; archers waiting behind battlements keep a drawn bow; gate/wall breakers swing their hammer/club at the gate; flyers hold their weapon (spirit demon spear, red demon bow).
6. Stop Play Mode to reset. If unit prefabs are regenerated by another setup menu, rerun `ProjectNeo/Setup/Apply Character Models`.
