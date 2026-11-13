# Project Goal: Definition of Done

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Final objective, completion criteria, horizon granularity, and established architectural decisions. | Parent: [INDEX](../INDEX.md) | Related: [Milestones](Milestones.md), [design/README](../design/README.md)

## Final Objective

**Deliver a complete siege defense roguelike where players scout upcoming enemy waves, prepare heroes, troops, equipment, and walls, defend the castle through direct player combat and unit commanding, and carry forward casualties and rewards into subsequent battles.** Upon run termination, unlocks and perk points persist, empowering players to challenge new runs with diverse strategic configurations.

First-time players must intuitively grasp the game flow via in-game guidance, playing from start to run termination and restarting cleanly in a stable standalone executable. Release platforms, channels, dates, content scale, and final clear/boss rules remain **undecided**.

## Final Completion Criteria

| Dimension | Verification Target in Final Release |
|---|---|
| Core Gameplay | Scouting wave composition drives preparation choices that visibly impact combat outcomes; encourages varied builds over single static metas. |
| Siege Combat | Ground and wall-top combat, structural collapse, walkable path shifts, squad commands, diverse enemy approaches, morale/rout, and win/loss operate cohesively. |
| Prep & Progression | Facility interactions (hire, recruit, trade, craft, fortify, organize) reflect into combat and subsequent prep; skills and perks wire into runtime stats. |
| Run Loop Completeness | Start → Prep → Battle → Rewards → Next Stage → Termination → Results → Retry loop runs cleanly; wall HP, hero permadeath, grunt casualties, and rout returns carry over accurately. |
| Meta Progression & Saves | Commander EXP, perk points, unlock points, and unlocks persist across sessions; perks reset cleanly on new runs; robust against save corruption/migration. |
| Content & Expression | Finalized initial scope of heroes, enemies, stages, items, perks; clear visual and audio feedback for combat, commands, death, and routs. |
| Usability | Clear onboarding, controls, commands, prep, and retry flows; responsive input, configurable settings, legible UI. |
| Quality & Delivery | Target specs/headcounts/frame rate met in release builds with production assets; zero progression-blocking or save-destroying bugs; verified packaging. |

## Scope & Horizon Granularity

- **Core Release Scope**: Siege combat, preparation facilities, run iteration, meta progression, UI, skill integration, save systems, audio-visual feedback, verification.
- **Candidate Follow-ups**: Jump/guard/directional melee attacks (evaluated post-playtest); exterior foraging expeditions, hero loyalty, difficulty modes (post-core).
- **Granularity**: M1 detailed to tasks/assignments; M2/M3 planned as feature bundles/gates; M4+ kept as milestones until reached.

## Established Design & Implementation Decisions

| Item | Decision & Implementation Impact |
|---|---|
| Art Style | Low-poly Synty assets (pack undecided); decouple grid, footprints, heights, and visuals into data assets. |
| Combat Perspective | 3D First-Person (2026-11-11); ortho camera reserved for construction. Profile performance early. |
| Castle & Map | Single castle per run; free grid placement with dynamic NavMesh rebuilds. |
| Grunt Recruitment | Barracks dual function (Training + Recruitment); rescued prisoners act as supplemental reinforcements. |
| Monster Materials | Multiple distinct material definitions tracked in `Cost` and `Wallet`. |
| Perk Effects | Modular payload lists supporting both stats and special gameplay modifiers (e.g., morale boost). |
| Squad Sizing | Data-driven via ScriptableObjects (default 4 squads × 10 members). |
| Facility Trading | Tiered inventory pools at shop/blacksmith; unified buy/sell logic (fixed sale ratio across all items). |
| Run Persistence | Perk point pool and unlocks persist across runs; allocated perks reset on death. |
| Perk & EXP Curves | Commander EXP per stage + Reincarnation Bonus (% of run score on run end); mid-run level-ups grant instant perk points allocatable anytime. |
| Unlocks | Randomized gacha-style unlocks using Unlock Points; duplicate protection and guaranteed initial appearance supported. |
| Wall Breached & Combat | Main gate destruction counts as breach (defeat requires gate destroyed + all defenders dead/routed, OR player dies). Defenders fight atop walls via multi-tier NavMesh. |
| Enemy & Grunt Rules | Ground enemies breach or climb ladders; flying enemies assault walls directly. Slain grunts lost permanently; only routed grunts return. |
| Player Character | Shares `Character` architecture with NPC heroes (differs only in Input vs AI driver). |
| B-2 / B-3 Combat | WASD/mouse/Left-click melee; `max(min, Attack - Defense)`; closest reachable enemy; no friendly fire; swept-collision linear projectiles. |
| B-4 Build & Weapons | Ortho preview (no collision), independent stairs, harmless overlapping unit lift, height-based fall damage, extensible `ISiegeWeaponModule` destroyed simultaneously with supporting wall. |
