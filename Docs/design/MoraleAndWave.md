# Game Design: Morale, Rout & Wave Preview

Last Updated: 2026-11-18 02:52:45 (KST)

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
- **Gain Rules (COMBAT-07, 2026-11-17, user)**: A gain never triggers the Breaking Point Check, even when it moves morale across `100`. Gains (perk, skill, Rally) do not apply to a routed squad (morale `0`); a rout is never cancelled by a gain. Gains are capped at `200`. Battle-start perk effects (`PerkTrigger.BattleStart`) fire once per battle, right after each squad's battle morale is created.
- **Awaken Effects (random 1, Q-38 2026-11-13)**:
  - *Rally*: Raises morale of self and nearby squads.
  - *Last Stand*: Attack rises with the share of starting members lost (example start: up to `+50%`).
  - *Steadfast*: The squad does not rout this battle even at `0` morale and fights to the end.
  - (War cry rallying already-routed units dropped as too strong).
- **Collapse Effects (random 1)**:
  - *Fear*: Chance (start 50%) to ignore each order.
  - *Panic* (COMBAT-03 user decision, 2026-11-14): On its first periodic trigger, the squad abandons its position for a retreat point inside the castle and waits there until battle end, ignoring new orders. This does not exit combat, hide units, or record them as Fled. Reaching 0 morale still escalates to the existing rout/exit rules. A runtime `PanicRetreatPoint` marks the destination, initially using the existing courtyard retreat location. Normal Hold self-defense applies while waiting.
  - *Contagion*: Periodically lowers nearby squads' morale slightly.
  - (*Berserk* charging out the gate dropped).
- **Persistence & Reorganization**: Morale belongs to the squad and is stored on the run-data squad between battles (Q-44); every newly created squad, including squads auto-created by barracks recruiting, starts at `150`. After each battle every squad's morale becomes `max(current + 20, 80)`, capped at `200` (Q-39 2026-11-13), so a squad that held never ends below one that routed. Routed squads (0) therefore start next battle at `80` (keeps debuff, no check, can recover). Moving soldiers between squads does not alter squad morale; new squad starts at `150`; empty squad and its morale are removed (after a battle, a squad is removed at the next prep start only when it has no grunts and no living captain; a captain-only squad stays with its morale, Q-50 2026-11-13). Merging under-strength squads and a minimum squad size are pending (Q-45, decide with preparation-phase work).
- **Test Display**: `SquadCommandHud` shows ally morale number, state (`Normal`, `Collapse: <type>`, `Awaken`), and one-line check notice. Enemy morale hidden. Prep-phase morale recovery facilities and player proximity modifiers are later design.

## Enemy Morale (removed, Q-44 2026-11-13)

- Enemies are monsters and have **no morale** (like monsters without stress in Darkest Dungeon): no morale loss, rout, or attack modifier. The 2026-11-12 enemy morale rules are superseded.
- Enemies leave battle only through Q-30 retreat. Retreated enemies count as exited, are excluded from victory kill counts, and are recorded separately per enemy type in `BattleResult`.
- Code keeps the unused `MoraleSide.Enemy` value so morale-affected human enemies can be reintroduced later through enemy data (decide with wave data B-7.1 or later) without changing ally rules.

## Stage & Wave Structure (Q-51, user approved 2026-11-14)

Stages stay fixed per run (the same stage number always brings the same waves), so players can memorize and counter-build.

| Item | Decision |
|---|---|
| Run length | Build **3 stages** first for the M2 loop; the final stage count is decided in M4 (Q-08). |
| Waves per battle | Each battle has **2–3 waves**; each wave starts at a **fixed time** after the battle starts (timetable in data), not on kill count. |
| Spawn locations | **Several fixed points inside the northern field** (e.g. left / center / right); each wave entry names its point. No east/west approaches for now. |
| Bosses | **None in M2**; the last stage is simply the hardest hand-made composition. Boss roles stay in Q-08 / M4. |
| Victory | Victory only after **every wave has spawned** and every enemy is dead or retreated (Q-30). No survive-timer victory. |
| Difficulty | **Hand-authored composition per stage**; no automatic stage multiplier. |
| Data format | One stage data asset per stage holding a wave list; each entry gives enemy prefab, count, start time, and spawn point. Enemy types reference the **existing enemy prefabs** (no separate enemy definition asset yet). Teammate (enemy domain owner) review is pending before B-7.1 implementation. |

Tuning numbers (wave start times, counts per stage) live in the stage assets and are set during B-7.1. Hidden-enemy scouting rules remain with C-5.

### Stage Data Format & Wave Rules (Q-52, user decision 2026-11-16)

The user asked for a format that stays decoupled and easy to change, and started B-7.1 on it; teammate review of the format runs in parallel and may still adjust it.

| Item | Decision |
|---|---|
| Wave start | **Fixed timetable kept** (Q-51). An empty field does not pull the next wave forward. The start condition is a replaceable rule so this can change later without touching the spawner. |
| Retreat while waves remain | **Settings switch**, default **off**: enemies that lost their siege means (Q-30) do not retreat while a wave is still to come and fight on with the next wave. With the switch on, each wave is judged on its own as before. |
| Victory | Unchanged from Q-51: every wave released and every enemy dead or retreated. Enemies not yet released count as pending and block victory. |
| Stage beyond the catalog | Interim: the **last stage repeats** until the run ending (Q-08) is wired. The catalog length is the run length. |
| Pause / restart | Battle time advances only while the battle is running; each wave is released exactly once per battle and the timetable resets with every battle. |

Data shape (assets under `Data/GameFlow/Stages/`):

- **Stage catalog**: ordered stage list; stage number `N` uses entry `N`.
- **Stage**: wave list. Each **wave** has a start time (seconds after battle start) and one or more **groups**.
- **Group**: enemy prefab, count, and a **spawn point asset**.
- **Spawn point asset**: a named point given as a build-grid cell in the northern field (initially Left / Center / Right). Adding a point is adding an asset; no code or scene change. Formation spacing stays in the battle settings.

Layering, so each part can change alone:

1. **Timetable rule** (pure C#): decides which wave is due and how many enemies are pending. Knows nothing about prefabs, scenes, or Unity time.
2. **Stage data** (assets): who, how many, when, where. Read-only for C-5 preview, which needs no battle to be running.
3. **Encounter plan** (plain data): the battle reads one neutral plan built either from a stage asset or from the old single enemy list, so prototype scenes and existing tests keep working unchanged.
4. **Spawner + battle controller** (scene): places units for a group and releases them when the timetable says so.

The three stage compositions created with B-7.1 were accepted by the user after a balance check on 2026-11-16; they stay data in the stage assets and can be retuned without code changes.

## Wave Preview

Rules decided by the user on 2026-11-18 (Q-53). Numbers are data and open to tuning.

- **Default view** (preparation): the next battle's enemy **types and counts** for the whole stage (no per-wave split). Spawn direction and wave timing are not shown.
- **Hidden enemies**: a stage group can be marked hidden in stage data. Hidden groups appear as `? × N` (count shown, type hidden).
- **Scouting action**: the player can scout for `3` hours of preparation time ([Preparation Time](PrepEconomyMeta.md#preparation-time-q-53-user-decision-2026-11-18)). One scout reveals **every** hidden group of the coming stage, so it is offered only while something is hidden. It cannot be done if fewer than `3` hours remain.
- **Automatic reveal** (kept from the original design): a hero with a scouting trait or a scouting perk reveals hidden groups without spending time. This needs hero traits and perk injection (META-02) and follows later.
- Hidden enemies are also revealed once they appear on the battlefield.
