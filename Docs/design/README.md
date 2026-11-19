# Game Design Overview

Last Updated: 2026-11-19 20:47:14 (KST)

> Purpose: Ground-truth game design index, core loop, run structure, and art direction. | Parent: [INDEX](../INDEX.md) | Related: [Combat](Combat.md), [PrepEconomyMeta](PrepEconomyMeta.md), [Backlog](../Backlog.md)

The `Docs/design/` folder is the ground truth for game rules. The Google Doc mirror in [GameDesignGoogleDoc.md](../archive/GameDesignGoogleDoc.md) is legacy reference only. Release platform, timing, and final content scale remain undecided.

## Topic Map

- [Combat.md](Combat.md) — Perspective, squad commands, damage/combat rules, units, win/loss conditions.
- [WallsAndSiege.md](WallsAndSiege.md) — Castle walls, stacking, construction economy, enemies, siege strategy, ladders.
- [MoraleAndWave.md](MoraleAndWave.md) — Ally Breaking Point morale, enemy morale, rout/exit rules, wave preview.
- [PrepEconomyMeta.md](PrepEconomyMeta.md) — Preparation facilities, barracks/unit deployment, currencies, rewards, meta progression.
- [OpenQuestions.md](OpenQuestions.md) — Unresolved design items.

## Overview & Core Loop

A siege defense roguelike where players prepare heroes, minions, weapons, and castle walls during Preparation, and defend against enemy waves in Battle. Stages are fixed; heroes, rewards, and shop inventories randomize per run. Gameplay centers on "counter-building" by scouting upcoming waves and crafting solutions with gold and monster materials.

- **Run loop**: Preparation → Wave Preview → Battle → Random Rewards → Next Stage Preparation. Castle wall HP carries over to the next battle.
- **Defeat**: Ends the run. Active perks reset and a new run begins (no reincarnation bonus, Q-71, 2026-11-19).
- **Clear**: Clearing all stages triggers the ending (confirmed 2026-11-11). Team decisions 2026-11-19: 7 stages in a fixed order (Q-67), one boss per stage in the last wave (Q-68), and after the last stage the run continues into a harder loop (Q-69; rules Q-75). The interim 3 stages without bosses (Q-51) stay until CONTENT-01 and SIEGE-09 land; see [MoraleAndWave](MoraleAndWave.md).
- **Scene layout** (confirmed 2026-11-11): `Title` scene and `Game` scene. All phases transition inside the single castle `Game` scene without reloading; facilities open as pop-up windows.

## Run Structure

| Element | Fixed / Random | Notes |
|---|---|---|
| Stage (Wave composition) | Fixed | Mastery through memorization and counterplay |
| Castle & Map | Fixed | Entire run takes place in a single castle |
| Tavern Heroes | Random | Primary driver of build variety across runs |
| Battle Rewards | Random | See [PrepEconomyMeta.md](PrepEconomyMeta.md) |
| Shop Inventory | Random | Prevents static optimal builds |

## Art Style & Future Additions

- **Art Style**: Low-poly aesthetic using Synty assets (specific packs undecided). Placeholder graphics initially; grid scale, cell footprints, heights, and visual references are decoupled into data assets.
- **Future Additions (post-core)**: Foraging / Expeditions outside walls, Hero Loyalty, Difficulty Modes.
