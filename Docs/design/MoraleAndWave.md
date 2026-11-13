# Game Design: Morale, Rout & Wave Preview

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Ally Breaking Point morale, enemy morale, rout/exit accounting, and wave preview rules. | Parent: [design/README](README.md) | Related: [Combat](Combat.md), [OpenQuestions](OpenQuestions.md)

## Overview

Morale applies to both allies and enemies, managed per squad. Morale boosts come via perks or skills rather than standalone systems.

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
- **Effect Duration**: Collapse and Awaken last until battle end. Awakened squads still rout at `0`.
- **Rout**: At `0`, whole squad routs (captain included; player character has no morale). Routing allies run to a fixed courtyard retreat point (never out the gate) and exit combat; routing units remain attackable/killable.
- **Starting Below 100**: Enters battle with one reduced-strength Collapse effect from start and no breaking point check that battle.
- **Stat Modifier Bands**: `>= 150`: attack `+10%`. `< 100`: attack `-10%`.
- **Morale Loss Causes (Allies)**: Squad member death (proportional to starting size lost), Main gate destroyed (all squads, fixed), Nearby wall collapse (fixed), Nearby squad routing (fixed), Squad captain death (fixed, largest single loss). ("Nearby" starts at `15m`). Excluded: enemy hero/boss approach, explosions, ladder climbing, being surrounded.
- **In-Battle Gain**: Perks/skills and Awaken only (no passive recovery).
- **Awaken Effect**: Raises morale of self and nearby squads. (War cry rallying routed units dropped as too strong; *Last Stand* and *Steadfast* deferred).
- **Collapse Effects (random 1)**:
  - *Fear*: Chance (start 50%) to ignore each order.
  - *Panic*: Periodically abandons position toward castle interior.
  - *Contagion*: Periodically lowers nearby squads' morale slightly.
  - (*Berserk* charging out the gate dropped).
- **Persistence & Reorganization**: Morale belongs to the squad (`+20` after each battle, capped at `200`). Routed squads start next battle at floor `80` (keeps debuff, no check, can recover). Moving soldiers between squads does not alter squad morale; new squad starts at `150`; empty squad and its morale are removed.
- **Test Display**: `SquadCommandHud` shows ally morale number, state (`Normal`, `Collapse: <type>`, `Awaken`), and one-line check notice. Enemy morale hidden. Prep-phase morale recovery facilities and player proximity modifiers are later design.

## Enemy Morale (2026-11-12)

- Separate rules: **no** breaking point check, Collapse, or Awaken. Routs at `0` using off-map retreat movement (same as Q-30).
- **Loss Causes**: Squad member death (proportional), nearby enemy squad routing, enemy hero/captain death. (Ladder toppling/falling not used).
- **Starting Morale**: Set per enemy type and stage in data (wave preview display TBD).
- **Stat Modifier**: Below `100` gives attack `-10%` only (no above-150 bonus).
- **Exit Counting**: Moral-routed and Q-30 retreating enemies both count as exited and are excluded from victory kill counts (reasons recorded separately in `BattleResult`).

## Wave Preview

- Displays upcoming enemy types and headcounts by default.
- Certain enemies obscured as hidden (`?`), revealed upon appearing or scouting via high reconnaissance traits or scouting perks.
