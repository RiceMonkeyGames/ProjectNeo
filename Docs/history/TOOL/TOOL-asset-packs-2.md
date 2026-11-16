# TOOL-asset-packs-2 | 2026-11-17 | Tooling - Second Batch of Synty Packs in Asset Submodule

Last Updated: 2026-11-17 01:21:40 (KST)

## Scope and implementation

- Branch: `feature/tool-asset-packs-2`; youngho. The user placed six `.unitypackage` files in a local folder outside the repository and asked the agent to import them, move them under `Assets/ThirdParty`, and push them to the asset submodule (`RiceMonkeyGames/ProjectNeo-Assets`). No game code, scene or prefab changes.
- Pre-check without importing: each package's `*/pathname` entries were extracted to a scratch folder (`tar --wildcards`) and their GUIDs checked against the project with `AssetDatabase.GUIDToAssetPath`. No package contained `ProjectSettings/` or `Packages/` entries; the only script is `PolygonElvenRealm/Scripts/SimpleRotate.cs`.

| Package | Root folder | Result |
|---|---|---|
| POLYGON AncientEmpire v1.5.0 | `PolygonAncientEmpire` | Imported (~25 min), moved |
| POLYGON Knights v1.5.1 | `PolygonKnights` | Imported (~5 min), moved |
| POLYGON Dungeon v1.9.4 | `PolygonDungeon` | Imported, moved; see GUID overlap below |
| Polygon Elven Realm v1.5.1 | `PolygonElvenRealm` | Imported, moved |
| POLYGON Dungeon Realms v1.3.3 | `PolygonDungeonRealms` | Already present (untracked) under `Assets/ThirdParty` with all 3341 GUIDs; not re-imported, committed as is |
| POLYGON Modular Fantasy Hero v1.3.1 | `PolygonFantasyHeroCharacters` | Skipped: same pack already committed (all 1772 GUIDs at the same paths) |

- Import used `AssetDatabase.ImportPackage(path, false)` via `script eval` (obsolete in Unity 6000.6 but functional; replacement `UnityEditor.AssetPackage.Package.Import`). Each new root was moved from `Assets/` to `Assets/ThirdParty/` with `AssetDatabase.MoveAsset`, keeping GUIDs. Afterwards every GUID from the five imported/present packages resolved under `Assets/ThirdParty/`.
- GUID overlap: four Dungeon assets share GUIDs with files already in PolygonFantasyKingdom/PolygonFantasyRivals (`FX_CandleFlame.mat`, `SM_Veh_Boat_01_Snow_Convex.asset`, `SM_Prop_Candle_Flame_01.fbx`, `FX_Standard.mat` → `Polygon_FX.mat`). Unity updated them in place; the two that changed (`FX_CandleFlame.mat`, `SM_Prop_Candle_Flame_01.fbx.meta`) were restored with git, so the existing Kingdom/Rivals versions are kept and Dungeon references resolve to them.
- Also restored before importing: a stray Editor re-serialization of `Synty/InterfaceFantasyWarriorHUD/Samples/Prefabs/Navigation_FantasyWarrior_Overlay01.prefab`.
- Size: 22,827 files, ~873 MB added. Largest file `PolygonElvenRealm/Textures/Misc/Skybox_Stars_only.dds` 96.0 MB (under GitHub's 100 MB limit; above its 50 MB warning). No Git LFS, per [TOOL-asset-submodule](TOOL-asset-submodule.md).
- Submodule commit `451f25d5` on `main` pushed to `ProjectNeo-Assets` (GitHub warned GH001 about files over 50 MB; accepted). The main repository records the new pointer on the task branch.

## Verification

- Compile passed after import (`unity-verify.ps1 -Mode compile`); `scriptCompilationFailed=False`.
- Main repository: `ProjectSettings/` and `Packages/` unchanged; no files left directly under `Assets/` outside `_Project`, `Settings`, `Tests`, `TextMesh Pro`, `ThirdParty`.
- Not verified: visual check of the new packs. Their materials likely use the Built-in `Standard` shader (magenta under URP), as found for the earlier packs in [ART-03](../ART/ART-03.md); conversion is left to the task that places them. Tests were not rerun (no code under `Assets/_Project` changed).

## Manual verification

1. After pulling, run `git submodule update --init --recursive` and let the Editor reimport (first import can take tens of minutes).
2. In the Project window, open `Assets/ThirdParty/` and confirm `PolygonAncientEmpire`, `PolygonDungeon`, `PolygonDungeonRealms`, `PolygonElvenRealm`, `PolygonKnights` exist.
3. Open a pack demo scene (e.g. `Assets/ThirdParty/PolygonKnights/Scenes/`) read-only and expect magenta materials until URP conversion; do not save the demo scene.
4. Console shows no compile errors.
