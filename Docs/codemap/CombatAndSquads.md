# Runtime Codemap: Core, Stats, Characters, Combat & Squads

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Class map for core utilities, stats, characters, combat AI, squads, and morale. | Parent: [codemap/README](README.md) | Related: [WallsAndSiege](WallsAndSiege.md), [design/Combat](../design/Combat.md)

Base path: `Assets/_Project/Scripts/Runtime/`. Assembly: `ProjectNeo.Runtime`. Namespace: `ProjectNeo.<Folder>`.

| Folder | Key Classes | Role |
|---|---|---|
| Core | `RandomSource`, `WeightedPool`, `Unlockable` | Seed-based random generator, weighted selection pool, common unlock types and interfaces. |
| Stats | `StatSheet`, `StatValue`, `StatModifier`, `StatType` | Base stats and source-specific modifiers: `(base + flat) * (1 + percent)`. |
| Characters | `HeroDefinition`, `SoldierDefinition`, `SkillDefinition`, `Character` | Hero/grunt/skill data. `Character` stores individual hero stats and equipment; player uses same class. |
| Characters | `PlayerControlSettings`, `PlayerInputController` | Control/camera settings asset, Input System bindings, and CharacterController movement. FirstPersonCamera is default. |
| Characters | `PlayerViewCamera`, `FirstPersonCamera` | Common camera interface and default 1st-person camera driven by `PlayerControlSettings`. Player visual casts shadow only while active. ThirdPersonCamera preserved unused. |
| Combat | `Health` | Pure C# health rules. Damage, death, max HP scaling, `RestoreFull` for living units (no resurrecting dead). Stat subscriptions unsubscribed via `Dispose()`. `CharacterActor.SoldierDefinition` reads grunt data. |
| Combat | `CombatProfile`, `CharacterActor`, `CombatTeam` | Body size, attack type, windup delay, min damage, projectile settings asset. Bridges heroes and grunts to common stats; handles teams, targeting, melee/ranged attacks, cooldowns, hit reactions, death. `Character` attached to heroes only; common access uses `Stats`. |
| Combat | `DamageRules`, `CombatPhysics` | Pure C# damage formulas, range, and line-of-sight checks. Attacks enemies in scene; allies do not occlude. Ranged attacks not blocked by shooter's ground object (`GroundUnder`). LoS checks and projectiles share identical rules. |
| Combat | `CombatProjectile` | Linear projectile. Stores damage, team, and direction upon launch; applies defense on hit. Passes through allies, hits first enemy or wall, terminates at max range or lifetime. |
| Combat | `UnitAISettings`, `UnitCombatAI` | Search radius, tick interval, rotation, movement, arrival thresholds. Picks closest reachable enemy, updates NavMesh path, melee/ranged attacks, stops on death. Empty-target Hold/Engage searches respect `DecisionInterval`; existing invalid targets drop immediately. Hold attack candidates use conservative collider AABB bounds before exact range/LoS; retained valid Engage targets reuse reachability path. Directives: `Engage`, `Hold` (melee advances within `HoldEngageRadius` 6m; ranged within attack range + `HoldRangedChaseExtra` 6m; return beyond `HoldReturnDistance` 1m), `Move`. Arrival: remaining <= `ArrivalDistance` (0.35m) or `MoveStallTracker` (`MoveStallProgress` 0.1m over `MoveStallSeconds` 0.5s within `MoveStallDistance` 1.5m). Default without orders: Engage. |
| Combat | `PlaceholderCombatView` | Subscribes to attack, damage, and death events under `Visual` child. Placeholder tinting, attack telegraphs, health bar, death visuals. Contains no combat logic. |
| Squads | `SquadConfig`, `Squad<TUnit>`, `Army<TUnit>` | Squad and army member caps, captain assignments. |
| Squads | `SquadCommander`, `SquadOrder`, `SquadFormation` | Pure C# squad command logic. Single/all selection, Move/Attack/Hold orders, default Hold state, arrival transition to Hold, rank-and-file formation offsets. Decoupled from scene and input. |
| Squads | `SquadCommandSettings`, `SquadCommandCenter`, `SquadCommandInput` | Command hotkeys (`InputAction`), formation settings asset (`Command.asset`), translates orders to `UnitDirective` and formation slots, tracks arrival, screen-center raycast. D-0b: `SquadCommandCenter.AddSquad`/`AddMember`/`TryGetGatherPoint` and `SquadCommander.AddSquad` grow squads at runtime; `PlaceholderSquadMarkers` creates discs lazily. |
| UI | `SquadCommandHud` | Test command overlay (selection, orders, headcount, prompts, reticle). Not final HUD. |
| Morale | `SquadMorale` | Squad morale tracker for perk hooks. Actual rout mechanics deferred to B-8. |
