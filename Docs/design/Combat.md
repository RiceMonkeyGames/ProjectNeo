# Game Design: Combat, Units & Win/Loss

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Rules for perspective, squad commands, damage, unit lifecycles, and battle outcomes. | Parent: [design/README](README.md) | Related: [WallsAndSiege](WallsAndSiege.md), [MoraleAndWave](MoraleAndWave.md)

## Perspective & Squad Commanding

- Default perspective is **3D first-person** (confirmed 2026-11-11): direct player control combined with squad commanding.
- Prototype combat uses WASD movement, mouse look, and left-click frontal melee attack (jump, guard, directional attacks are follow-ups).
- Player forms squads and assigns heroes as captains. No separate commands are given directly to individual heroes. Squads without captains obey identical orders.
- **Controls** (subject to playtest tuning): F1–F4 select squads, backtick (`` ` ``) selects all, `1` Move, `2` Attack, `3` Hold.
- **Command Behaviors**:
  - **Move**: Disregards enemies and marches to screen-center target; transitions to Hold upon arrival.
  - **Hold** (revised 2026-11-12, Bannerlord style): Defends guard post (where Hold was issued or Move finished).
    - *Melee*: Advances to engage walkable enemies within 6m (`HoldEngageRadius`, data-configurable); does not pursue beyond range or broken paths; can descend stairs to engage breaches.
    - *Ranged*: Fires within range; advances toward reachable enemies within `range + 6m` (`HoldRangedChaseExtra`, data-configurable) until in firing range; shoots downward at enemies beneath walls; does not chase unreachable enemies.
    - *Return*: Returns to post when targets clear; minor displacement within 1m does not trigger return; halts nearby if post is blocked.
    - Unreachable enemies (no walkable path) are ignored so defenders push ladders instead of stalling.
  - **Attack**: Marches toward target point while engaging enemies spotted in visual range; resumes march after clearing; waits in Attack posture if arrived/empty.
  - **Vision**: Configurable detection radius; wall LoS occlusion and FoV angles omitted currently.
  - Unreachable destinations or broken paths halt at closest reachable point; clustered stalls near destination count as arrived.
  - Squads without orders at battle start begin in Hold state. Squad count/caps are data-driven (default 10+ members).
  - Captain hero death drops squad morale by the largest fixed amount (2026-11-12).

## Basic Combat Rules

- Damage formula: `Attack − Defense`, minimum damage is `1`.
- Auto-targeting acquires closest enemy, re-evaluating on target death or unreachability.
- No friendly fire.
- Ranged attacks fire linear projectiles upon release: dodgeable post-launch, blocked by walls, pass through allies.

## Units

| Category | Description |
|---|---|
| Player Character | Controllable hero. Possesses stats, skills, and items identical to heroes. |
| Hero | Hired at tavern. Can act as squad captain. **Permadeath** on death; cannot be revived. |
| Minion (Grunt) | Recruited at barracks. Eliminated on death; only fleeing/routed minions return next battle. |

- Surviving heroes and minions fully recover HP before the next battle (2026-11-12). Only wall HP damage carries over.
- Fallen ally corpses remain on the ground during the next preparation phase (2026-11-12), excluded from squads and run.
- Fallen hero equipment retention/loss remains undecided (Q-31).

## Win/Loss Conditions

- Confirmed 2026-11-12: If victory and defeat occur simultaneously, **defeat takes priority**.
- If no siege-capable enemies remain and no walkable path exists to surviving allies, remaining enemies retreat off-map and are excluded from victory kill tallies (Q-30).

| Result | Conditions |
|---|---|
| **Victory** | All enemies killed or routed |
| **Defeat** | Main gate destroyed AND all allies killed (or routed), OR Player character dies |

Combat continues if main gate falls as long as surviving allies remain.
