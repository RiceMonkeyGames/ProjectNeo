# PREP-10 | 2026-11-20 | Prep - Hero Promotion & Hero Hall

Last Updated: 2026-11-20 16:43:27 (KST)

> Purpose: Implementation and verification log. | Parent: [history/README](../README.md) | Related: [Q-73](../Q/Q-73.md), [PrepEconomyMeta](../../design/PrepEconomyMeta.md), [FlowRunEconomy](../../codemap/FlowRunEconomy.md)

- Assignee: 신용준. Branch `feature/prep-10` from develop `b0c8fbe`.
- Rule (Q-73, details by the user 2026-11-20): new facility **Hero Hall** (`FacilityType.HeroHall = 5`; the barracks keeps `TrainingGround`). Heroes start at their hire tier grade; after each victory surviving squad captains gain 100 EXP (200 / 400 needed for grades 2 / 3, capped at the need); promotion costs 100g / 250g plus 1 prep hour; each grade above 1 adds +15% max HP and attack. Values in `Data/GameFlow/HeroPromotion.asset`.

## Changes

- Rules: `HeroPromotionSettings` (+ `HeroGradeStep`), `HeroPromoteCheck`, `RunState` partial `RunStateHeroGrades.cs` (`SetupHeroPromotion`, `GradedHeroes`, `StartingGrade`, `ExpToNextGrade`, `CheckPromote`, `TryPromote`, victory EXP in `Apply`, grade stat modifiers on the hero `Character`). `RunHero.Grade` / `Exp`.
- Save: `HeroRecord.grade` / `exp`; older saves (grade 0) start at the hire tier. `RunRestoreOptions.HeroPromotion` and `Facilities` (facilities missing from an older save, such as the Hero Hall, are added at level 0).
- Config: `RunConfig.HeroPromotion`, `AddFacility`. Scene run attaches the settings in `GameFacilities.PreparedState` and `GameBattleController` before the first save.
- Scene: `GameRunLink.TryPromote` (keeps a full-health unit full); hired heroes start at full health after the grade bonus. `GameFacilities` Hero Hall popup: hero rows (grade, role, EXP x/y, cost, time; click to promote or see the reason) and a grade bonus column.
- Editor: `HeroHallSetup` (menu `ProjectNeo/Setup/Add Hero Hall (PREP-10)`) creates the settings and facility assets, adds them to `RunStart`, adds the `HeroHall` building at (148.5, 0, 94.5) with a `FacilityStation` in `Game.unity`, and refreshes `RunContent`. `GameFacilitySetup` and `GameFlowSetup.ApplyCourtyard` now include the Hero Hall so reruns keep it.
- Skills per grade are data only (COMBAT-06 will use `Grade` for the slot count).

## Verification

- `Tools/Agent/unity-verify.ps1 -Mode all`: dotnet pre-check passed, Unity compile passed, EditMode **552/552**, PlayMode **254/254**.
- New EditMode tests: `HeroPromotionTests` (7: hire tier grade and bonus, victory EXP only for surviving captains with cap, no EXP on defeat/death, check reasons, promotion cost/time/bonus, setting/hall requirements, save round trip and old-save migration).
- PlayMode: `GameFacilityPlayTests` now expects six facilities and adds a Hero Hall popup test (hired hero grade, full health, refusal without EXP).
- Manual play: confirmed by the user on 2026-11-20 (Hero Hall popup, EXP refusal, promotion after two victories, grade stats). Standalone build: not verified.
