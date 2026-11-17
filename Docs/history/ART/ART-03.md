# ART-03 | 2026-11-16 | Art - Synty Character Models and Locomotion Animation for Prototype Units

Last Updated: 2026-11-16 23:05:36 (KST)

## Scope and implementation

- Branch: `feature/art-03`; youngho. The user asked to place models only on units already implemented in the prototype and delegated model selection from the role-based candidate list. Animation was first deferred; the user then asked to add animations chosen by name from the animation assets in the same task.
- Unit-to-model mapping (`Editor/CharacterModelSetup`, menu `ProjectNeo/Setup/Apply Character Models`):

| Unit prefab | Model | Pack |
|---|---|---|
| `allied_melee` | `SM_Chr_Soldier_Male_01` | PolygonFantasyKingdom |
| `allied_ranged` | `SM_Chr_Soldier_Female_01` | PolygonFantasyKingdom |
| `hostile_melee` (inherited by variant `enemy_ladder`) | `Character_BR_Slayer_01` | PolygonFantasyRivals |
| `hostile_ranged` (inherited by variant `enemy_archer`) | `Character_DarkElf_01` | PolygonFantasyRivals |
| `enemy_gate_breaker` (variant of `hostile_melee`) | `Character_BR_BigOrk_01` | PolygonFantasyRivals |
| `enemy_wall_breaker` | `Character_BR_Troll_01` | PolygonFantasyRivals |
| `enemy_flyer` | `Character_SpiritDemon_01` | PolygonFantasyRivals |
| `enemy_flyer_high_archer` | `Character_BR_RedDemon_01` | PolygonFantasyRivals |

- The model is a nested prefab instance named `Model` under each unit's `Visual`, scaled so its height matches `CombatProfile.Height` (2m, same as the collider capsule). Any colliders inside the model are removed. The capsule `Body` renderer is disabled (object kept as the fallback tint target). In `enemy_gate_breaker` the inherited base model is deactivated and its own model is added.
- The submodule materials use the Built-in `Standard` shader (magenta in URP). The setup creates URP Lit copies (`_MainTex` → `_BaseMap`, `_Color` → `_BaseColor`, smoothness 0.1) under `Assets/_Project/Art/Characters/Materials/` and assigns them on the instances, so `Assets/ThirdParty` is not modified.
- `PlaceholderCombatView` gained an optional `model` field (`ConfigureModel`). With a model, it tints all model renderers through `_BaseColor` (hit: brighten 2.2x for 0.12s, dead: dark gray) and lays the model on its back on death instead of squashing `Body`. Without a model the previous behaviour is unchanged (the player prefab still uses it).
- Placeholder role markers (ladder bar, breaker box, flyer wings), health bars and attack markers are kept as role cues.
- The menu is idempotent (rerun: same result, no duplicate models). Setup menus that regenerate unit prefabs (`UnitBattleSetup`, `SiegePrototypeSetup`, `FlyerSetup`, `WallBreakerSetup`, etc.) discard the models; rerun this menu afterwards.
- Player prefab unchanged (first-person; its body is shadow-only).

## Locomotion animation

- The submodule animation pack (`Synty/AnimationBaseLocomotion`) contains only locomotion clips (idle, walk, run, sprint, crouch, strafe, turn, jump/fall). There are no attack, hit or death clips, so those stay placeholder (attack marker, brighten, lie down).
- All chosen models have valid Humanoid avatars, so Polygon Humanoid clips are retargeted. Controllers are generated once under `Assets/_Project/Art/Characters/Animation/` (kept if they already exist):
  - `AC_Unit_Ground_Masc` / `AC_Unit_Ground_Femn`: 1D blend tree on float `Speed`: `A_Idle_Standing_*` at 0, `A_Walk_F_*` at the root-motion walk clip speed (1.46 m/s masc), `A_Run_F_*` at the root-motion run clip speed (2.60 m/s). In-place clips are used; root motion is off.
  - `AC_Unit_Flyer`: single `Hover` state looping `A_InAir_FallShort_Masc`.
- Assignment: Masc for allied_melee, hostile_melee/enemy_ladder, gate and wall breakers; Femn for allied_ranged and hostile_ranged/enemy_archer; Flyer for both flyers.
- New runtime view component `Combat/CharacterModelAnimator` on `Visual`: feeds horizontal speed (NavMeshAgent velocity when on the NavMesh, otherwise position delta) to `Speed` with 0.1s damping, and disables the Animator on death so the pose freezes while `PlaceholderCombatView` lays the model down. Animator culling is `CullUpdateTransforms`.
- No climb animation: units on ladders play idle/walk based on horizontal speed.

## Verification

- Models only: compile passed; EditMode 453/453; PlayMode 217/217 (131.7s).
- With animation: compile passed; EditMode 453/453; PlayMode 217/217 (235.7s; the wrapper 180s wait timed out and the run was followed to completion). The longer run may be skinned-mesh/Animator cost or editor noise; not profiled.
- Game scene Play Mode check: 12 allied squad units had drivers, Humanoid animators and played `A_Idle_Standing_Masc/Femn`; close-up render showed a natural idle pose. One soldier sent 10m away with its AI disabled blended to `A_Walk_F_Masc` (weight 0.95, Speed 1.38). Play Mode was exited afterwards.
- Preview-scene render of all unit prefabs: models show textured (no magenta), ~2m tall (T-pose there because preview-scene Animators do not evaluate in Edit Mode).
- Not verified: full manual battle play, run blend and flyer pose in battle, hit/death visuals on models in play, frame rate with many skinned units, standalone build.
- Unrelated: `Assets/ThirdParty` shows a local re-serialization of `Synty/InterfaceFantasyWarriorHUD/Samples/Prefabs/Navigation_FantasyWarrior_Overlay01.prefab` that is not part of this task and is not committed.

## Manual verification

1. Open `Assets/_Project/Scenes/Game.unity`, enter Play Mode.
2. In prep, walk to the courtyard soldiers: melee squads show the male Kingdom soldier, ranged squads the female soldier, playing a breathing idle; ordering a squad to move (F1–F3, 1) blends to walk/run.
3. Start battle by holding `B` for 1 second in prep. Enemies: armored Slayer (ladder troops with ladder bar), dark elf archers, orc gate breaker, troll wall breaker, spirit demon / red demon flyers (with placeholder wings, legs dangling in an in-air loop). Walking enemies animate walk/run; attacks still show only the placeholder attack marker.
4. Shoot or hit an enemy: the model briefly brightens. On death the pose freezes, it turns dark and lies on its back; its health bar disappears.
5. Check no unit appears magenta and model sizes roughly match the old capsules (hit detection unchanged).
6. Stop Play Mode to reset. To restore the models after regenerating unit prefabs, run `ProjectNeo/Setup/Apply Character Models`.
