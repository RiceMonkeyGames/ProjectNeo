# LEGACY | 2026-11-11~12 | Flow/Scene - Castle Layout, Prep & Siege Wiring

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-11: Default Castle Layout in Game Scene

- Replaced placeholder layout with formal castle: 24×26 grid, 14×14 outer walls, north main gate, 3 interior stairs, 12×12 courtyard, and northern approach field. Dedicated game grid and camera assets created.
- Verified: 109 EditMode, 45 PlayMode passed.

### 2026-11-11: Gate Stacking & Overlapping Unit Elevation

- Confirmed: Walls stackable above main gate. Living units overlapping new placements elevated to top surface harmlessly (`UnitFallController.LiftForConstruction`). Player start position moved to courtyard (12, 8).
- Verified: 122 EditMode, 49 PlayMode passed.

### 2026-11-12: Main Castle Map Expansion & Courtyard Facility Layout

- Expanded map: Ground 240×300m, castle 96×96m, courtyard 90×90m, 80×100 grid. Placed 5 placeholder facility buildings, cross corridors, troop assembly area, and 120m northern approach space.
- Added camera navigation options to `ConstructionCameraController`: middle-click drag pan, scroll zoom (20–180), Home reset.
- Verified: 61 PlayMode passed.

### 2026-11-12: 1st-Person Prep Default & Construction Station Interaction

- Prep phase initialized in 1st-person navigation. Facing courtyard construction station triggers `F · Use Construction`; Esc exits back to 1st-person navigation at matching location/orientation.
- Implemented `PreparationInteractionSettings`, `PreparationInteraction`, `ConstructionStation`, and HUD prompt widgets.
- Verified: 62 PlayMode passed.

### 2026-11-12: Game Scene Siege Combat Integration & HUD Clean-up

- Connected `GameBattleController` to GameFlow Battle phase: 28 enemies advance from north upon interacting with red banner at gate. Collapsing gate triggers upper wall dropping and continued assault on collapsed pieces. 3-tier ladders functional. Defeat prioritizes simultaneous victory. Isolated surviving enemies without walkable paths retreat off-map.
- Restructured HUD into distinct screen quadrants, eliminating overlap.
- Verified: 136 EditMode, 70 PlayMode passed.
