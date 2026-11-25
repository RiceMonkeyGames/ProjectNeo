---
id: PREP-11
title: Artisan Workforce System (Rules, Time Contention, Hiring & UI)
status: In Progress
assignee: SeongminHwang
branch: feature/prep-11
area: PREP
depends_on: []
created: 2026-11-25
---

# PREP-11 — Artisan Workforce System (Rules, Time Contention, Hiring & UI)

## Scope & Completion Criteria

Registered 2026-11-25 by the user; design direction recorded in [PrepEconomyMeta](../design/PrepEconomyMeta.md) and [Decisions-Resolved](../plan/Decisions-Resolved.md). Replaces the passive fixed `WorkCrews = 2` FIFO queue (`Q-53` / `SIEGE-08`) with an explicit **Artisan** (`장인`) workforce so preparation time and labor allocation create meaningful trade-offs.

### Core Rules (Pure C# in `Runtime/Run/`)

1. **Artisan Data & State**:
   - Store starting artisan count (default `2`), maximum cap (default `5`), escalating hire gold costs (e.g., `50g` / `100g` / `150g`), and facility-upgrade work hours (default `1h`) in ScriptableObject data (`PrepTimeSettings` extension or dedicated `ArtisanSettings` with `Create(...)` factory, linked from `RunConfig`).
   - Track current run artisan count on `RunState` with `CheckHireArtisan` / `TryHireArtisan` (free clock action; blocked during pending battle, when at cap, or on insufficient gold). Keep `EffectiveArtisanCount` composable for future Economy perks, Ascension rules, or Meta unlock starting bonuses.
   - Persist hired artisan count and work-order priority ordering in `RunSnapshot` (`RunStateSnapshot` / `GameRunRestorer`); older saves without the field default cleanly to the starting artisan count.

2. **Shared Labor & Clock Contention**:
   - Extend `PrepClock.Advanced` (or introduce a pure advance payload with `hours` + `occupiedArtisans`) so every clock advance reports how many artisans were occupied by the triggering direct action.
   - **Direct Artisan Actions** (occupy `1` artisan during their clock step):
     - Blacksmith crafting and equipment upgrading (`CraftHours` / `UpgradeHours`, default `1h`).
     - Courtyard facility upgrades (Shop, Tavern, Training Ground, Smithy, Defense/Battlements, Hero Hall; data-configured `FacilityUpgradeHours`, default `1h`; refuses with `Not enough time.` without spending gold when time is short).
     - During a direct artisan action, only `max(0, EffectiveArtisanCount - occupiedArtisans)` active construction/repair orders progress in `WorkQueue`.
   - **Non-Artisan Clock Actions** (occupy `0` artisans):
     - Wait (`T`, `1h`) and Hero Hall promotion (`1h`) advance all `EffectiveArtisanCount` active construction/repair slots.

3. **Construction & Repair Slot Assignment / Priority Control**:
   - `WorkQueue` exposes active slots (first `N` incomplete orders) vs waiting orders, plus pure reorder methods (`MoveUp`, `MoveDown`, `MoveToFront`) and per-step concurrency `Advance(float hours, int availableArtisans)`.
   - Keep existing `Q-53` / `SIEGE-08` / `Q-77` battle-start rules (`0%` build removed with full refund; `>0%` build stands with progress HP; unfinished cannon unarmed; unfinished repair refunds remaining gold share; passive yellow battle-start warning).

### Scene Wiring & UI (`UI-12` / `UI-13` Style)

- Update `GameConstructionWork`, `GameFacilities`, `GameBuildEconomy`, and `GameBattlements` to route direct-action artisan occupancy and facility-upgrade time checks through `RunState`.
- **Preparation HUD & Construction Bar**: display current artisan count (`장인 N/Max`) and active/queued work summary.
- **Construction Mode Work Queue Panel**: list active artisan slots separately from waiting orders with reorder/promote and cancel controls, plus an Artisan hire button (`장인 고용` with next hire gold cost and cap state).
- **Facility & Smithy Popups**: display required prep hours and artisan requirement (`1시간 · 장인 1명` or matching short `UI-13` detail lines) on Blacksmith craft/upgrade and Facility upgrade actions.

### Open Before Start (Confirm with User)

- Visual mockup (A/B/C per UI rules) for the construction-mode artisan work queue & hire controls layout alongside the `UI-13` bottom build bar.
- Whether max artisan hire cap is flat (data cap `5`) or gated step-by-step by a facility/castle tier level.

### Completion Criteria

- Pure C# EditMode tests cover artisan concurrency (`N` active slots), direct-action contention (`N - 1` during craft/upgrade/facility upgrade, `N` during Wait/Hero promotion, `0` when `N == 1`), priority reordering, cancellation refunds, hiring checks/cap, facility-upgrade time refusal, and run save/restore.
- PlayMode tests in `Game` verify Blacksmith craft + Facility upgrade competing with active build/repair orders, priority reorder, and hiring updating slot count immediately.
- Code rules check and `Tools/Agent/unity-verify.ps1 -Mode all` pass; `Docs/codemap/FlowRunEconomy.md` and `Docs/codemap/WallsAndSiege.md` updated.
- Courtyard NPC visuals are intentionally deferred to [ART-09](ART-09.md).
