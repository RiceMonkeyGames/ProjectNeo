# Third-Party Art Asset Map

> Purpose: One list of which `Assets/ThirdParty` packs serve which role and which assets are already applied, so new units, buildings or props do not reuse an asset already given to another role. | Parent: [codemap/README](README.md) | Related: [Principles §2](../workflow/Principles.md), [Decisions-Resolved (Q-01)](../plan/Decisions-Resolved.md), [EditorAndTests](EditorAndTests.md)

## Rules

- **Check this file before picking an asset.** Do not give a model already listed here to a second role (unit, hero, building piece). Weapons and props may be shared only when this file says so (for example the shared arrow).
- **Always write the full pack path.** Synty packs reuse names (`SM_Chr_Soldier_Female_01` exists in five packs), so list `Pack/Prefabs/...`, not the bare name.
- **Prefer the main pack for the role** (table below). Use a supplement pack only when the main pack has no fitting asset, and say why in the task's history record.
- **Update this file in the same task** that applies, replaces or removes an asset (add the row, the setup script, the task ID and the preview link).
- Applied assets are copied or instanced from `Assets/_Project/Scripts/Editor/` setup menus with URP material copies under `Assets/_Project/Art/`; `Assets/ThirdParty` itself is never modified ([Principles §2](../workflow/Principles.md)).

## Preview images

Each third-party prefab path in this file has a preview image in [`art-assets/`](art-assets/) named `<Pack>__<AssetName>.png` (512x512, front-right view, materials shown through in-memory URP copies). Open the `view` link in a table row to see it.

To add images after adding rows: with the Editor open, run menu `ProjectNeo/Docs/Capture Art Asset Previews` (captures only paths without an image; `Recapture All Art Asset Previews` redraws every image), or from the CLI:

```powershell
unity command script eval --caller plugin --skill unity-cli --timeout 300 --result-only 'ProjectNeo.Editor.ArtAssetPreviewCapture.Capture(false)'
```

The tool (`Editor/ArtAssetPreviewCapture`) reads every backticked `Pack/Prefabs/...` path in this file, loads `Assets/ThirdParty/<path>.prefab` and reports `captured`, `skipped` and `missing` counts; a `missing` path is a typo or an asset that is not a prefab. Then add the `view` link to the new row and commit the PNG with the doc.

## Packs per role (Q-01, 2026-11-22)

All paths are under `Assets/ThirdParty/`.

| Role | Main pack | Supplement packs |
|---|---|---|
| Castle walls, gate, buildings, props | `PolygonFantasyKingdom` | `PolygonKnights` |
| Allied soldiers and heroes | `PolygonFantasyKingdom`, `PolygonElvenRealm` | `PolygonKnights` |
| Enemies | `PolygonFantasyRivals` | `Synty/PolygonGoblinWarCamp`, `PolygonDarkFantasy` |
| Weapons and projectiles | Pack of the unit's model; `PolygonFantasyKingdom` for generic bows and arrows | — |
| Animation | `Synty/AnimationBaseLocomotion`, `Kevin Iglesias/Human Animations` | `ExplosiveLLC` (archer walk only) |
| UI | `Synty/InterfaceFantasyWarriorHUD` | — |
| Environment (not applied yet) | — | `PolygonNature`, `PolygonNatureBiomes` |

Other packs in the submodule (modern, sci-fi, zombie, seasonal and so on) are outside the game's style and are not used without a new decision.

## Applied assets

### Unit models and weapons (`CharacterModelSetup`, menu `ProjectNeo/Setup/Apply Character Models`; ART-03, ART-04)

| Unit prefab | Model | Weapon |
|---|---|---|
| `allied_melee` | `PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Soldier_Male_01` ([view](art-assets/PolygonFantasyKingdom__SM_Chr_Soldier_Male_01.png)) | `PolygonFantasyKingdom/Prefabs/Weapons/SM_Wep_Sword_01` ([view](art-assets/PolygonFantasyKingdom__SM_Wep_Sword_01.png)) |
| `allied_ranged` | `PolygonElvenRealm/Prefabs/Characters/SM_Chr_Soldier_Female_01` ([view](art-assets/PolygonElvenRealm__SM_Chr_Soldier_Female_01.png)) | `PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Bow_01` ([view](art-assets/PolygonFantasyKingdom__SM_Prop_Bow_01.png)) |
| `hostile_melee` (+ variant `enemy_ladder`) | `PolygonFantasyRivals/Prefabs/Characters/Character_BR_Slayer_01` ([view](art-assets/PolygonFantasyRivals__Character_BR_Slayer_01.png)) | `PolygonFantasyRivals/Prefabs/Weapons/SM_Wep_Slayer_01` ([view](art-assets/PolygonFantasyRivals__SM_Wep_Slayer_01.png)) |
| `hostile_ranged` (+ variant `enemy_archer`) | `PolygonFantasyRivals/Prefabs/Characters/Character_DarkElf_01` ([view](art-assets/PolygonFantasyRivals__Character_DarkElf_01.png)) | `PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Bow_02` ([view](art-assets/PolygonFantasyKingdom__SM_Prop_Bow_02.png)) |
| `enemy_gate_breaker` | `PolygonFantasyRivals/Prefabs/Characters/Character_BR_BigOrk_01` ([view](art-assets/PolygonFantasyRivals__Character_BR_BigOrk_01.png)) | `PolygonFantasyRivals/Prefabs/Weapons/SM_Wep_BigOrk_01` ([view](art-assets/PolygonFantasyRivals__SM_Wep_BigOrk_01.png)) |
| `enemy_wall_breaker` | `PolygonFantasyRivals/Prefabs/Characters/Character_BR_Troll_01` ([view](art-assets/PolygonFantasyRivals__Character_BR_Troll_01.png)) | `PolygonFantasyRivals/Prefabs/Weapons/SM_Wep_Troll_01` ([view](art-assets/PolygonFantasyRivals__SM_Wep_Troll_01.png)) |
| `enemy_flyer` | `PolygonFantasyRivals/Prefabs/Characters/Character_SpiritDemon_01` ([view](art-assets/PolygonFantasyRivals__Character_SpiritDemon_01.png)) | `PolygonFantasyRivals/Prefabs/Weapons/SM_Wep_SpiritDemon_01` ([view](art-assets/PolygonFantasyRivals__SM_Wep_SpiritDemon_01.png)) |
| `enemy_flyer_high_archer` | `PolygonFantasyRivals/Prefabs/Characters/Character_BR_RedDemon_01` ([view](art-assets/PolygonFantasyRivals__Character_BR_RedDemon_01.png)) | `PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Bow_02` ([view](art-assets/PolygonFantasyKingdom__SM_Prop_Bow_02.png)) |

- Unit prefabs live in `Assets/_Project/Prefabs/UnitBattlePrototype/` and `Assets/_Project/Prefabs/SiegePrototype/`. Heroes and the player have no dedicated model yet (the player is first-person).
- Shared on purpose: `SM_Prop_Bow_02` (enemy archers and the high flyer).
- Previews show the source prefab in its default pose (T-pose for characters), not the in-game scale or animation.

### Projectiles (`ArrowModelSetup`, menu `ProjectNeo/Setup/Apply Arrow Model`; ART-05)

| Prefab | Model |
|---|---|
| `Prefabs/UnitBattlePrototype/StraightProjectile` (every ranged unit) | `PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Arrow_01_Optimized` ([view](art-assets/PolygonFantasyKingdom__SM_Prop_Arrow_01_Optimized.png)) |

### Castle pieces (`CastleWallArtSetup`, menu `ProjectNeo/Setup/Apply Castle Wall Art`; ART-06)

| Prefab | Model |
|---|---|
| `Prefabs/ConstructionPrototype/Wall` | `PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Castle_Wall_Block_01` ([view](art-assets/PolygonFantasyKingdom__SM_Bld_Castle_Wall_Block_01.png)) |
| `Prefabs/ConstructionPrototype/Stairs` | `PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Stairs_Straight_Wide_01` ([view](art-assets/PolygonFantasyKingdom__SM_Bld_Stairs_Straight_Wide_01.png)) |
| Battlements (`CastleBattlements`, `Battlements.asset`) | Procedural boxes textured with the Kingdom castle stone (URP copy in `Art/Castle/Materials`) |

Reserved for `SIEGE-13` (not applied yet; a Kingdom broken wall or rubble mesh for collapsed cells is chosen at task start):

| Planned use | Model |
|---|---|
| 6m main gate | `PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Castle_Wall_Gate_01` ([view](art-assets/PolygonFantasyKingdom__SM_Bld_Castle_Wall_Gate_01.png)) |
| Sliced battlement modules | `PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Castle_Battlements_01` ([view](art-assets/PolygonFantasyKingdom__SM_Bld_Castle_Battlements_01.png)) |
| Sliced battlement modules | `PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Castle_Battlements_03` ([view](art-assets/PolygonFantasyKingdom__SM_Bld_Castle_Battlements_03.png)) |

### Animation (`CharacterModelSetup`; ART-03, ART-04)

No preview images (clips, not prefabs).

| Use | Source |
|---|---|
| Idle, walk, run, strafe (all ground units) | `Synty/AnimationBaseLocomotion/Animations/Polygon/` (`A_Idle_Standing_Masc`, `A_Walk_*_Masc`, `A_Run_*_Masc`) |
| Melee attacks, combat idle, bow load/hold/release, hit, death, crouch | `Kevin Iglesias/Human Animations/Animations/` (`HumanM@` clips) |
| Archer forward walk while drawing | `ExplosiveLLC/Warrior Pack Bundle 2 FREE/Archer Warrior Mecanim Animation Pack/Animations/` (`Archer@Walk`) |
| Flyer hover | `Synty/AnimationBaseLocomotion` (`A_InAir_FallShort_Masc`) |

### UI (`FantasyHudStyle`, `HudStyleLook`; UI-05, UI-06, UI-10)

No preview images; see the pack demo scenes and [prep HUD mockups](../mockups/prep-hud-mockups.html).

| Use | Source |
|---|---|
| Frames, parchment, buttons, icons | `Synty/InterfaceFantasyWarriorHUD` sprites (layout from the pack's demo scenes) |
| Title font | `Synty/InterfaceFantasyWarriorHUD/Fonts/Grenze/Grenze-SemiBold SDF` |
| Korean text font | Project font `Assets/_Project/Art/Fonts/NotoSansKR SDF` (not third-party) |
