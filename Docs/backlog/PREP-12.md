---
id: PREP-12
title: Tavern Hero Re-roll & Offer Lock
status: In Progress
assignee: SeongminHwang
branch: feature/prep-12
area: PREP
depends_on: []
created: 2026-11-26
---

# PREP-12 — Tavern Hero Re-roll & Offer Lock

## Scope & Completion Criteria

Add gold re-rolling and offer locking to the Tavern, mirroring the approved Shop rules (`Q-46`, `PREP-02`/`PREP-03`) to mitigate hero pool dilution as the hero roster grows ([PrepEconomyMeta](../design/PrepEconomyMeta.md), [OpenQuestions](../design/OpenQuestions.md)).

### Planned Design & Scope

1. **Pure C# Rules (Mirroring `Q-46` Shop Rules)**:
   - **Re-roll**: Spend escalating run gold per preparation phase (data-configurable base/step in `RunConfig` / `TavernSettings`, e.g., `20g`, `40g`, `60g`, unbounded linear step; resets at each new preparation phase). No prep-clock cost (free action like Shop re-roll).
   - Re-rolling draws from the Tavern level that stocked the current preparation (so upgrading the Tavern still reflects on the next preparation phase) and refills empty/hired offer slots while preserving locked offers.
   - **Offer Locks**: Free to toggle per Tavern offer row; max simultaneous locks equals current Tavern level (updated immediately on Tavern upgrade). Locked hero offers survive re-rolls and the next preparation restock; hiring a locked hero clears its lock.
   - All randomness routed through `ProjectNeo.Core.IRandomSource`.

2. **Save Persistence & UI (`Game` Scene)**:
   - Persist Tavern lock flags and current-prep re-roll count in the run save (`FLOW-02`), maintaining backward compatibility with older saves (defaulting to unlocked / 0 re-rolls).
   - Add per-row lock toggle button and re-roll button (showing current gold cost and disabling when unaffordable or no unlocked slots exist) to the existing `UI-07` / `UI-13` Tavern popup.

### Open Before Start (Quick Confirm with User)

- Confirm starting re-roll gold step (`20g` vs `10g` matching Shop) and whether duplicate unlocked hero offers may appear alongside a locked offer (matching Shop `Q-46`).

### Completion Criteria

- Pure C# EditMode tests cover escalating re-roll cost, prep reset, lock cap vs Tavern level, persistence across re-rolls/restocks, hire clearing lock, and run save/load round-trip.
- PlayMode verification in `Game` confirms Tavern UI re-roll, lock toggling, gold deduction, and hiring a locked hero.
- `Docs/design/PrepEconomyMeta.md` and `Docs/design/OpenQuestions.md` updated; manual Play Mode walkthrough provided.
