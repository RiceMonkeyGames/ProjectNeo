# Open Design Questions

Last Updated: 2026-11-15 16:28:26 (KST)

> Purpose: Undecided game design topics tracked alongside `plan/Decisions-Pending.md`. | Parent: [design/README](README.md) | Related: [Decisions-Pending](../plan/Decisions-Pending.md)

- Run length (max stage count, boss pacing) and ending specifics (clearing all stages triggers ending is decided).
- Specific perk varieties and scouting perk categorization.
- Morale tuning values beyond starting values in [MoraleAndWave.md](MoraleAndWave.md) (Ally Breaking Point rules decided 2026-11-12) and prep-phase morale recovery facilities.
- Combat/building tuning values (fall damage coefficients, etc.).
- Feasibility of mid-combat perk allocation.
- Metrics determining run score.
- Actual values for squad counts and squad member caps.
- Layered building edge cases (stairs constraints, partial foundation collapse) and max vertical tiers.
- Equipment handling on hero permadeath (Q-31); player character as squad captain later (Q-32 interim: not now).
- Tavern gold re-roll costs and locking slots (Shop rules decided as Q-46, 2026-11-13).
- Combat consumable use (keys, effects, limits) with Q-12; deferred 2026-11-13.
- **UI improvement pass** (user request 2026-11-15): Current HUDs are test displays (`SquadCommandHud`, `GameFlowHud` hint lines, construction panel). Scope, priorities and layout are undecided; collect concrete issues from playtests first. Known items: final combat HUD / command keys / prep UI layout (Q-11), the temporary merged squad-key line in the preparation hint (PREP-05, `GameFlowHud.PreparationSquadHint`), and cannon fire-ready feedback (SIEGE-07).
- **Enemy and ally AI improvement pass** (user request 2026-11-15): Enemy siege AI and ally squad AI need a quality pass; scope and priorities are undecided and the enemy side is the teammate's area ([Team](../plan/Team.md)). Known items: a living enemy standing in the line still blocks a ranged unit's shot at the enemy behind it (found during BUG-wall-archer-los, left unchanged); recent fixes to reference: wall-top archer range (BUG-wall-archer-los), far enemies idling in Assault when a path is already open (BUG-stage2-enemies-idle), ally retaliation (BUG-ally-no-retaliation).
