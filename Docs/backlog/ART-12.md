---
id: ART-12
title: Dedicated Hero Models & UI Portraits
status: Waiting
assignee: Unassigned
branch:
area: ART
depends_on: []
created: 2026-11-26
---

# ART-12 — Dedicated Hero Models & UI Portraits

## Scope & Completion Criteria

Give each playable hero (`HeroDefinition`: Swordsman, Archer, Knight, Paladin) a distinct Synty character model and generate consistent UI portrait sprites to populate the empty thumbnail slots reserved by `UI-07` / `UI-13` ([ArtAssets](../codemap/ArtAssets.md), [PrepEconomyMeta](../design/PrepEconomyMeta.md)).

### Planned Design & Scope

1. **Unique Hero Visual Models**:
   - Audit [ArtAssets](../codemap/ArtAssets.md) to select unused character and weapon prefabs from approved packs (`PolygonFantasyKingdom`, `PolygonElvenRealm`, `PolygonKnights`) so no hero shares a model with generic soldiers (`allied_melee`, `allied_ranged`), the player (`Character_Knight_01_Blue`), enemies, or courtyard NPCs.
   - Apply via Editor setup script (`CharacterModelSetup`) with URP material copies under `Assets/_Project/Art/` and matching combat/locomotion animator controllers strictly under each hero prefab's `Visual` child.

2. **Hero & Unit UI Portraits**:
   - Add an optional `Portrait` (`Sprite`) field on `HeroDefinition` (and `SoldierDefinition` / player hero data) if not already present, with safe null fallback.
   - Generate consistent bust/headshot portrait textures via Editor capture tool (extending `ArtAssetPreviewCapture`) and wire them into the existing thumbnail slots in Squad Organization (`UI-07`), Tavern (`UI-13`), Equipment/Skills (`I`), and Hero Hall (`PREP-10`).

### Open Before Start (Quick Confirm with User)

- Confirm the specific Synty character/weapon model picks per hero (showing preview thumbnails from `Assets/ThirdParty`) before applying.

### Completion Criteria

- Every current hero displays its unique 3D model in courtyard and battle scenes and its portrait across UI windows without breaking stats, animation, or combat hitboxes.
- `Docs/codemap/ArtAssets.md` updated with full pack paths and regenerated preview PNGs; manual visual verification walkthrough provided.
