# SIEGE-39 | 2026-11-28 | SIEGE - Free-Placed Inner Castle from the Synty Demo

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-28: SIEGE-39 - Free-Placed Inner Castle from the Synty Demo

- **Assignee**: `신용준` (done inside [SIEGE-40](SIEGE-40.md)) | **Branch**: `feature/siege-40` | **Work commit**: `ddde4eb`
- **Decisions (user, 2026-11-28)**: done together with SIEGE-40. The whole Synty demo town and keep form the inner castle as placed in the demo (town walls and everything outside them removed by hand). Facilities use demo houses: tavern `Preset_Tavern_01`, shop `SM_Bld_Preset_House_07_Optimized`, training `Preset_Church_01_A`, smithy `Preset_Blacksmith_01`, construction and hero hall two `SM_Bld_Preset_House_02_A_Optimized` houses (middle and west). The user placed each NPC at its door. Ring-era facility buildings, citizens and the battle start flag were removed.
- **Implementation**: `Editor/GameBasinSetup` (`FacilityHouses`, `NpcSpots`): facility station roots sit at the house centres without visuals or colliders; NPC home and shelter points follow the door spots; wander points cleared.
- **Not done here**: citizens walking the demo paths (to register), facility building choice for later facilities.
- **Verification**: user checked in play that NPCs stand at the doors and open their facilities.