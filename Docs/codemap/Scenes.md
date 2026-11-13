# Playable Scenes & Manual Controls

Last Updated: 2026-11-13 15:12:57 (KST)

> Purpose: Executable scene table and `Title`/`Game` scene verification controls. | Parent: [codemap/README](README.md) | Related: [PrototypeScenes](PrototypeScenes.md), [Status](../plan/Status.md)

Unity 6000.6.8f1, URP, 3D first-person project. Placeholder graphics planned for Synty swap. Open scenes in Editor and press Play; exiting Play Mode restores clean state. Every scene's directional `Sun` uses Soft shadows (URP main-light shadow distance 50m); setup generators emit it and `Editor/SceneSunShadowsMigration` (menu `ProjectNeo/Setup/Enable Scene Sun Shadows`) applies it to existing scenes ([ART-02](../history/ART/ART-02.md)).

| Scene Path | Verification Target & Controls |
|---|---|
| `Assets/_Project/Scenes/ConstructionPrototype.unity` | Initial build scene. Q/E camera orbit, wall/stairs selection, translucent grid preview, Left-click place, Right-click cancel, R rotate piece. Walking on walls, falling damage via floor destruction. Cannon placement on walls and simultaneous collapse on wall destruction. |
| `Assets/_Project/Scenes/CombatPrototype.unity` | WASD move, mouse look, Left-click melee attack. Dummy combat, health, camera test. Esc to release cursor. |
| `Assets/_Project/Scenes/UnitBattlePrototype.unity` | Automated 10v10 spectator combat (melee & ranged allies vs enemies). |
| `Assets/_Project/Scenes/SquadCommandPrototype.unity` | Direct player control + 3 squad commanding. F1–F3 select squad, ` select all, 1 Move (screen center target), 2 Attack, 3 Hold. Top-left UI displays selection, orders, remaining troops. |
| `Assets/_Project/Scenes/WallCommandPrototype.unity` | Integrated building and squad commanding atop walls. Tab toggles construction / 1st-person, F1–F3 select, 1 Move, 2 Attack, 3 Hold. Construction UI features `Move atop wall` (moves squad 1) and `Destroy foundation` (destroys support under squad 1's lead unit). |
| `Assets/_Project/Scenes/SiegePrototype.unity` | Siege defense prototype. Prep phase (build, demolish, instant squad placement, temp budget) → `Start Battle` → enemy assault (breaches, ladders, gate assault, archers) → win/loss. Tab toggles build/1st-person during prep; locked to 1st-person during battle. Hold F to topple nearby ladders. Press R upon outcome to restart. |
| `Assets/_Project/Scenes/Title.unity` | Actual game entry scene (G-0). `New Game` loads Game scene; `Quit` exits play. |
| `Assets/_Project/Scenes/Game.unity` | Actual game scene (G-0, G-1). Ground 240×300m, castle 96×96m, courtyard 90×90m, 5 placeholder facilities. Middle-click drag pan, scroll zoom, Home reset camera. Prep phase uses 1st-person WASD navigation. Approach construction station and press F → orthographic build view to place walls/stairs/cannons with Q/E rotation; Esc returns to 1st-person. Approach red banner at gate and press F to start battle → 28 enemies advance from north; direct 1st-person combat and F1–F3 / 1 / 2 / 3 squad commanding. Auto outcome on wipe/rout/defeat → `Claim Reward & Next Stage` or `Return to Title`. Opening and playing this scene directly starts at Stage 1 prep. |

`*Prototype` scenes are standalone mechanism validation demos; the actual game consists of `Title` and `Game`. Currently, `Game` links construction station interaction, battle trigger, siege combat, win/loss, run data, victory reward payout, build/repair costs, facility pop-ups with upgrades, the shop, barracks recruitment/training (D-4), grunt scene spawning (D-0b), and player equipment (`I` during prep). Tavern (D-3), squad organization (D-8), blacksmith (D-6), and formal wave sequencing remain unlinked. Check [Status.md](../plan/Status.md) for latest test pass baselines.
