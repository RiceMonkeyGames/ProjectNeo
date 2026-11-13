# LEGACY | 2026-11-11~12 | Siege - Siege Prototype & Remote Unification

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-11: Siege Defense Prototype (Assault, Ladders, Win/Loss)

- Created dedicated `SiegePrototype.unity` scene and `ProjectNeo.Siege` framework. Melee enemies enter breaches or assault gates/climb ladders. Ladders spaced every 5 cells, raised in 3s, toppled by allies in 2s. Archers deal 50% damage shooting upward. Enemy roles: Ladder Grunts, Gate Breakers, Archers.
- Verified: 110 EditMode, 48 PlayMode passed.

### 2026-11-11: Dynamic Squad Roster & Death Visual Cleanup

- Dynamic squad spawning driven by `Siege.asset` (`Ally Squads`, `Enemy Groups`) via `SiegeDirector`. Attached `PlaceholderDeathHider` to hide prop visuals (planks, packs) upon death.
- Verified: 110 EditMode, 50 PlayMode passed.

### 2026-11-11: Dynamic Prep HUD & Expanded Squad Limits

- `SiegeHud` dynamically scales squad buttons to match roster headcount using canvas height. Expanded command hotkeys to F1–F10 via dedicated `Command.asset`. Displayed squad type in labels (`Squad N · Melee/Archer`).
- Verified: 110 EditMode, 50 PlayMode passed.

### 2026-11-12: Siege Branch Handoff & Merge into Develop

- Merged `syj` (`1a6769e`) and `feature/siege-prototype` (`dc08986`) into `develop`. Resolved conflicts, reconciled Q-28–30 IDs, restored occupancy cleanup in `ConstructionWorld.TryDismantle`, and ensured 9 scenes configured in build settings.
- Verified: 131 EditMode, 60 PlayMode passed.

### 2026-11-12: Remote Develop Merge & Combat Unification

- Integrated remote develop (`85869c6`) with local castle expansion and combat wiring (`42222d8`). Preserved teammate's retreat logic while maintaining multi-tier ladder support and Game scene settings.
- Verified: 137 EditMode, 72 PlayMode passed.
