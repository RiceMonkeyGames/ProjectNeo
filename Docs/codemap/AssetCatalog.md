# Medieval Fantasy Asset Catalog

> Purpose: Full survey of `Assets/ThirdParty` (2026-11-27) listing every pack and prefab that fits the low-poly medieval fantasy concept, by role, so asset choices for walls, units, monsters, HUD, environment and effects start from one list. | Parent: [codemap/README](README.md) | Related: [ArtAssets](ArtAssets.md) (assets already applied and role rules), [Principles §2](../workflow/Principles.md)

This catalog lists what **can** be used. [ArtAssets](ArtAssets.md) stays the record of what **is** used; move a row there when a task applies an asset. Full prefab names per pack are in [`asset-catalog/`](asset-catalog/) (generated from prefab paths, names only; regenerate when the submodule changes).

## Pack verdicts

The submodule holds 87 pack folders (about 52,000 prefabs). Usable packs:

| Tier | Pack (prefabs listed) | Main contents | Note |
|---|---|---|---|
| Core | [PolygonFantasyKingdom](asset-catalog/PolygonFantasyKingdom.md) (2187) | Castle kit (348), houses (241), props (778), items, weapons (183), siege engines (15), townsfolk (22), FX | Main pack for walls, gate, buildings, props |
| Core | [PolygonKnights](asset-catalog/PolygonKnights.md) (366) | Castle buildings (210), knights and soldiers in 4 team colours (40), environment | Allied troops, heroes, extra castle pieces |
| Core | [PolygonElvenRealm](asset-catalog/PolygonElvenRealm.md) (752) | Elven buildings (280), props, environment, characters (11), weapons, FX | Allied archers; elven architecture is a different style |
| Core | [PolygonFantasyRivals](asset-catalog/PolygonFantasyRivals.md) (73) | 18 monster/boss characters with own weapons, FX | Main enemy pack |
| Core | [PolygonDarkFantasy](asset-catalog/PolygonDarkFantasy.md) (639) | Gothic buildings, props, skeletons, gargoyle, dark lord, witch, FX | Undead enemies, dark stages |
| Core | [Synty/PolygonGoblinWarCamp](asset-catalog/Synty_PolygonGoblinWarCamp.md) (608) | Goblin characters (15 bodies), war camp buildings, siege vehicles (13), weapons | Enemy army and enemy camp |
| Core | [PolygonDungeon](asset-catalog/PolygonDungeon.md) (825) | Dungeon environment (482), goblins, skeletons, ghosts, golem, hero knights | Enemies; `_FixedScale` duplicates |
| Core | [PolygonDungeonRealms](asset-catalog/PolygonDungeonRealms.md) (1116) | Dwarf realm environment, props, runes, dwarves, skeletons, undead knight | Dwarf allies or enemies, runes |
| Core | [PolygonFantasyHeroCharacters](asset-catalog/PolygonFantasyHeroCharacters.md) (900) | Modular hero parts (720), 120 presets, weapons | Dedicated hero models (`ART-12`) |
| Core | [PolygonFantasyCharacters](asset-catalog/PolygonFantasyCharacters.md) (39) | King, queen, wizard, witch, druid, peasants; spell props | Heroes, NPCs |
| Core | [PolygonVikings](asset-catalog/PolygonVikings.md) (375) | Norse buildings, props, environment, snow variants, 18 warriors | Northern enemy faction or allies |
| Core | [PolygonAdventure](asset-catalog/PolygonAdventure.md) (283) | Environment (163), village buildings, knights, peasants, vikings | Fillers, villagers |
| Core | [PolygonNature](asset-catalog/PolygonNature.md) (227) | Trees, plants, terrain pieces, rocks, FX | Basin mountains and forest (`SIEGE-40`, `ART-13`) |
| Core | [PolygonNatureBiomes](asset-catalog/PolygonNatureBiomes.md) (978) | Meadow forest, enchanted forest, alpine mountain, swamp (plus tropical jungle and arid desert) | Alpine mountain set fits the basin rim; jungle and desert are conditional |
| Core | [PolygonParticleFX](asset-catalog/PolygonParticleFX.md) (180) | Generic particle effects (fire, smoke, magic, impacts) | VFX (`ART-10`) |
| Core | [Synty/InterfaceFantasyWarriorHUD](asset-catalog/Synty_InterfaceFantasyWarriorHUD.md) (250) | Health/equipment, action bar, compass, minimap, crosshairs, portraits, enemy bars, popups | Current HUD pack |
| Core | [PolygonLegendaryChest](asset-catalog/PolygonLegendaryChest.md) (1), [PolygonBowCrossbow](asset-catalog/PolygonBowCrossbow.md) (6) | Reward chest; bows and crossbows | Rewards, ranged weapons |
| Core (animation) | `Synty/AnimationBaseLocomotion`, `Synty/AnimationIdles`, `Synty/AnimationSwordCombat`, `Synty/AnimationGoblinLocomotion`, `Kevin Iglesias/Human Animations`, `ExplosiveLLC` (warrior packs) | FBX clips and controllers, not prefabs | Current animation sources |
| Conditional | [PolygonFarm](asset-catalog/PolygonFarm.md) (452) | Crops, fences, barns, farm props, farmers, generic mountains and hills | Modern vehicles and clothing removed; check each prop for modern look |
| Conditional | [PolygonAncientEmpire](asset-catalog/PolygonAncientEmpire.md) (761) | Roman-style buildings, props, soldiers | Different era look; props and statues only after review |
| Conditional | [PolygonPirates](asset-catalog/PolygonPirates.md) (488) | Harbour buildings, props, cutlasses, items | Ships and firearms removed; docks and barrels may fit |
| Conditional | [PolygonSnow](asset-catalog/PolygonSnow.md) (30) | Snow props | Skiers and modern gear removed |
| Conditional | [PolygonWerewolf](asset-catalog/PolygonWerewolf.md) (6) | Werewolf and undead werewolf, axes | Monster candidates; attachments removed |
| Conditional | [PolygonIcons](asset-catalog/PolygonIcons.md) (427) | 3D icons (sword, potion, coin, key, shield, medal, …) | Modern and sports icons removed by name; review the rest |
| Conditional | [Synty/SidekickCharacters](asset-catalog/Synty_SidekickCharacters.md) (9) | Viking warriors, goblin fighter (Sidekick modular system) | Different character system from Polygon |
| Utility | `PolygonPrototype`, `PolygonStarter` | Grey-box blocks and primitives | Placeholders only, not final art |

Excluded (outside the concept): modern, sci-fi, military, horror, holiday and city packs (`PolygonApocalypse*`, `PolygonCity`, `PolygonCyberCity`, `PolygonSciFi*`, `PolygonMilitary`, `PolygonWar`, `PolygonWarMap` (WW1), `PolygonHeist`, `PolygonOffice`, `PolygonShops`, `PolygonCasino`, `PolygonNightclubs`, `PolygonHorror*`, `Synty/PolygonHorrorAsylum`, `Synty/PolygonPoliceStation`, `PolygonStreetRacer`, `PolygonGangWarfare`, `PolygonConstruction`, `PolygonKids`, `PolygonWestern*`, `PolygonBattleRoyale`, `PolygonZombies`, `PolygonBossZombies`, `PolygonSpy`, `SimpleAirport`, seasonal and novelty packs); other-culture packs (`PolygonSamurai`, `Synty/PolygonAncientEgypt`, `PolygonFrogShrine`); chibi-proportion packs (`PolygonMini_Fantasy`, `PolygonMiniFantasyCharacters`, `PolygonMini_City`); other HUD packs (`Synty/InterfaceApocalypseHUD`, `InterfaceMilitaryCombatHUD`, `InterfaceSciFiSoldierHUD`); `PolygonDungeonMap` (library props only, listed in [asset-catalog](asset-catalog/PolygonDungeonMap.md) for interiors).

## By role

Pack shortcuts: FK FantasyKingdom, KN Knights, ER ElvenRealm, FR FantasyRivals, DF DarkFantasy, GW GoblinWarCamp, DG Dungeon, DR DungeonRealms, VK Vikings, AD Adventure.

| Role | Where to look |
|---|---|
| **Castle walls, gate, towers** (`SIEGE-38`) | FK `Prefabs/Castle` (348: walls, gates, towers, battlements, stairs); KN `Prefabs/Buildings` (210); DF `Prefabs/Building`; DR `Prefabs/Buildings`; GW `Prefabs/Buildings` (wooden palisades for the enemy camp) |
| **Inner castle buildings** (`SIEGE-39`) | FK `Prefabs/House` (241) and `Prefabs/Buildings` (64); KN `Prefabs/Buildings`; AD `Prefabs/Buildings`; ER `Prefabs/Buildings` (elven style) |
| **Siege engines** | FK `Prefabs/SiegeEngines` (15); GW `Prefabs/Vehicles` (13) |
| **Allied troops** | KN knights and soldiers (2 models each, 4 colours); FK `Soldier_Male_01`, `Soldier_Female_01`, `Rider_01`; ER soldiers and knight; AD knights and warriors; DG hero knights; VK warriors (as a faction) |
| **Heroes** (`ART-12`) | FantasyHeroCharacters (120 presets, modular parts); FantasyCharacters (king, wizard, sorcerer, witch, druid, rogue, bard); FK mage, monk, priest; ER priestess, rogue, scholar; DF hunters, plague doctor; DR dwarf heroes |
| **Monsters and enemies** | FR 18 (troll, big ork, barbarian giant, golems, medusa, dark elf, evil god, forest guardian, …); GW goblins 15 (archers, shaman, beast tamer, knight, kings, troll); DG goblins, skeleton soldiers and knight, ghosts, rock golem, tormented soul; DF skeletons (light, heavy, ranger), gargoyle, dark lord, witch; DR skeletons, undead knight; Werewolf; VK raiders |
| **Bosses** (`CONTENT-02`) | FR evil god, barbarian giant, golems, medusa; DF dark lord; GW kings; DG goblin war chief |
| **Townsfolk and NPCs** | FK 22 (bartender, blacksmiths, merchant, priest, peasants, jester, king, queen, …); ER commoners and nobility; AD peasants and shopkeepers; FantasyCharacters peasants; DR dwarf workers |
| **Weapons** | FK `Prefabs/Weapons` (183); DG (73); DR (81); FantasyHeroCharacters (54); each monster's own weapon in FR; BowCrossbow |
| **Props and items** | FK `Prefabs/Props` (778) and `Prefabs/Items` (241); DF props (271); DR props (345); GW props (209); DG props; VK props; Farm (filtered) |
| **Environment and terrain** | Nature (trees, rocks, terrain); NatureBiomes alpine mountain and meadow forest (basin rim); FK `Prefabs/Environments` (187); KN, ER, AD, VK environments; Farm generic mountains and hills |
| **Dungeon and interiors** | DG `Prefabs/Environments` (482); DR environments (388) and runes (92); DungeonMap library props |
| **VFX** (`ART-10`) | PolygonParticleFX (180); per-pack FX folders in FK, ER, FR, DF, DG, DR, GW, Nature, NatureBiomes |
| **HUD and UI** | `Synty/InterfaceFantasyWarriorHUD` (prefabs, 2363 sprites, animations); PolygonIcons (filtered, 3D icons for world markers or portraits) |
| **Animation** | Synty animation packs, Kevin Iglesias Human Animations, ExplosiveLLC warrior packs (see [ArtAssets](ArtAssets.md) Animation) |
| **Audio** | No audio files were checked in this survey (`AUDIO-02`, `AUDIO-03` source audio separately, `Q-83`) |

## Notes

- `_FixedScale` prefabs (Knights, Dungeon, FantasyCharacters) are duplicates with a fixed import scale; pick one variant per role.
- Not checked: per-prefab look (only names and folders), demo scenes, materials and textures. Confirm each pick with a preview capture ([ArtAssets § Preview images](ArtAssets.md)) before applying it.
