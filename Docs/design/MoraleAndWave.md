# Game Design: Morale, Rout & Wave Preview

Last Updated: 2026-11-13 19:28:47 (KST)

> Purpose: Ally Breaking Point morale, enemy morale, rout/exit accounting, and wave preview rules. | Parent: [design/README](README.md) | Related: [Combat](Combat.md), [OpenQuestions](OpenQuestions.md)

## Overview

Morale applies to ally squads only (enemies have no morale since Q-44, 2026-11-13), managed per squad. Morale boosts come via perks or skills rather than standalone systems.

| Change | Cause & Effect |
|---|---|
| Decrease | Sustaining heavy casualties drops squad morale, reducing combat efficiency. |
| Routing | Drops below threshold cause units to flee battlefield. |
| Increase | Boosted by player skills, captain traits, perk effects. Slight combat buff. |
| Rally | Routed ally units return for subsequent battles. |

## Ally Morale: Breaking Point (2026-11-12)

Combines Bannerlord-style squad morale with a Darkest Dungeon-style resolve check. Starting values live in data assets and remain subject to tuning.

- **Scale**: `0–200` per squad; new squad starts at `150`.
- **Breaking Point Check**: At `100` morale, checks **once per battle**: **Collapse** (negative) or **Awaken** (positive, `25%` base chance; modified by captain traits/perks). No repeat check in the same battle even if morale recovers above 100 and drops again.
- **Effect Duration**: Collapse and Awaken last until battle end. Awakened squads still rout at `0`, except under *Steadfast*.
- **Rout**: At `0`, whole squad routs (captain included; player character has no morale). Routing allies run to a fixed courtyard retreat point (never out the gate) and exit combat; routing units remain attackable/killable.
- **Rout Movement & Exit (Q-44 2026-11-13)**: The retreat point is computed from the castle grid at the courtyard edge farthest from the enemy approach (no scene marker); its offset is tuned in a settings asset. Routing allies ignore all orders. A unit counts as exited only when it reaches the retreat point; if it cannot reach it within `20s` (same as the Q-30 timeout) it exits in place. Exited units are hidden for the rest of the battle. If the battle ends while a unit is still running it is recorded as `Fled`; a unit killed on the way is `Dead`. Fled units reappear at the retreat point in the next preparation phase and walk back to their squad.
- **Defeat with Routed Allies (Q-44)**: Exited allies no longer count as alive for the defeat check. With the main gate intact and the player alive, the battle continues even if every squad has routed (player fights alone); with the gate destroyed, all allies dead or exited means defeat.
- **Starting Below 100**: Enters battle with one reduced-strength Collapse effect from start and no breaking point check that battle.
- **Stat Modifier Bands**: `>= 150`: attack `+10%`. `< 100`: attack `-10%`.
- **Morale Loss Causes (Allies)**: Squad member death (proportional to the squad's member count at the start of this battle; tuned so a squad starting at `150` reaches `0` when `90%` of its members die, i.e. full-squad loss ≈ `166.7`, Q-44), Main gate destroyed (all squads, fixed), Nearby wall collapse (fixed), Nearby squad routing (fixed), Squad captain death (fixed, largest single loss). ("Nearby" starts at `15m`). Excluded: enemy hero/boss approach, explosions, ladder climbing, being surrounded.
- **In-Battle Gain**: Perks/skills and Awaken only (no passive recovery).
- **Awaken Effects (random 1, Q-38 2026-11-13)**:
  - *Rally*: Raises morale of self and nearby squads.
  - *Last Stand*: Attack rises with the share of starting members lost (example start: up to `+50%`).
  - *Steadfast*: The squad does not rout this battle even at `0` morale and fights to the end.
  - (War cry rallying already-routed units dropped as too strong).
- **Collapse Effects (random 1)**:
  - *Fear*: Chance (start 50%) to ignore each order.
  - *Panic*: Periodically abandons position toward castle interior.
  - *Contagion*: Periodically lowers nearby squads' morale slightly.
  - (*Berserk* charging out the gate dropped).
- **Persistence & Reorganization**: Morale belongs to the squad and is stored on the run-data squad between battles (Q-44); every newly created squad, including squads auto-created by barracks recruiting, starts at `150`. After each battle every squad's morale becomes `max(current + 20, 80)`, capped at `200` (Q-39 2026-11-13), so a squad that held never ends below one that routed. Routed squads (0) therefore start next battle at `80` (keeps debuff, no check, can recover). Moving soldiers between squads does not alter squad morale; new squad starts at `150`; empty squad and its morale are removed. Merging under-strength squads and a minimum squad size are pending (Q-45, decide with preparation-phase work).
- **Test Display**: `SquadCommandHud` shows ally morale number, state (`Normal`, `Collapse: <type>`, `Awaken`), and one-line check notice. Enemy morale hidden. Prep-phase morale recovery facilities and player proximity modifiers are later design.

## Enemy Morale (removed, Q-44 2026-11-13)

- Enemies are monsters and have **no morale** (like monsters without stress in Darkest Dungeon): no morale loss, rout, or attack modifier. The 2026-11-12 enemy morale rules are superseded.
- Enemies leave battle only through Q-30 retreat. Retreated enemies count as exited, are excluded from victory kill counts, and are recorded separately per enemy type in `BattleResult`.
- Code keeps the unused `MoraleSide.Enemy` value so morale-affected human enemies can be reintroduced later through enemy data (decide with wave data B-7.1 or later) without changing ally rules.

## Wave Preview

- Displays upcoming enemy types and headcounts by default.
- Certain enemies obscured as hidden (`?`), revealed upon appearing or scouting via high reconnaissance traits or scouting perks.
