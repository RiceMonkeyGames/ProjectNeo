# ART-02 | 2026-11-13 | Art - Soldier Shadows (Scene Sun Soft Shadows)

Last Updated: 2026-11-13 15:19:13 (KST)

## Scope and implementation

- Branch: `feature/art-02`; Claude. User chose Soft shadows for all scenes on 2026-11-13.
- Cause: every scene setup script created the directional `Sun` via `AddComponent<Light>()` without setting `shadows`, so it defaulted to `LightShadows.None`. No object in those scenes cast shadows, not only soldiers. URP (`PC_RPAsset`) already supported main-light shadows (distance 50m, 4 cascades, 2048), and unit prefab renderers already had shadow casting On; neither was changed.
- Setup generators (`CombatPrototypeSetup`, `ConstructionPrototypeSetup`, `GameFlowSetup`, `UnitBattleSetup`, `SquadCommandSetup`) now set `shadows = LightShadows.Soft`.
- New `Editor/SceneSunShadowsMigration` (menu `ProjectNeo/Setup/Enable Scene Sun Shadows`) switches every directional light under `Assets/_Project/Scenes` to Soft through `SerializedObject`. First run: `Enabled soft shadows on 7 directional lights in 7 scenes; already soft=1.` Rerun: 0 changed, already soft=8. Each scene diff is a single line.

## Verification

- Compile passed; EditMode 216/216 (3.21s); full PlayMode 118/118 (56.26s; SIEGE-03 baseline was 53.74s, so no notable slowdown in tests).
- Preview-scene render of 6 `allied_melee` units under a Soft directional light: shadows visible on the ground at ~6m and ~28m camera distance. Health bars also cast small shadows (prototype placeholder, left as is).
- Beyond 50m (URP shadow distance) shadows fade out by design. Frame-rate impact with large battles not measured.
- User manually verified shadows in the Game scene and confirmed completion on 2026-11-13. Large-battle frame rate was not measured; standalone build unverified.

## Manual verification

1. Open `Assets/_Project/Scenes/Game.unity`, enter Play Mode.
2. In prep, walk near allied soldiers in the courtyard: each soldier, walls, stairs and facilities show ground shadows.
3. Use the construction view (F at construction station) or step back to view soldiers ~30m away: shadows remain visible; very far (>50m) they fade out.
4. Press F at the red gate banner to start battle; check enemies also cast shadows and the frame rate feels unchanged.
5. Optionally open `UnitBattlePrototype.unity` and Play to view 10v10 combat with shadows. Stop Play Mode to reset.
